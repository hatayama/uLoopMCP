using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Processes JSON-RPC 2.0 requests for the Device Agent.
    /// Enforces auth.login before any other method, dispatches tool execution to main thread.
    /// </summary>
    public sealed class DeviceJsonRpcProcessor
    {
        private readonly DeviceToolRegistry _registry;
        private readonly string _expectedToken;
        private bool _authenticated;

        public DeviceJsonRpcProcessor(DeviceToolRegistry registry, string expectedToken)
        {
            System.Diagnostics.Debug.Assert(registry != null, "registry must not be null");
            System.Diagnostics.Debug.Assert(!string.IsNullOrEmpty(expectedToken), "expectedToken must not be empty");

            _registry = registry;
            _expectedToken = expectedToken;
        }

        public void ResetAuth()
        {
            _authenticated = false;
        }

        public async Task<string> ProcessRequestAsync(string requestJson, CancellationToken ct)
        {
            object requestId = null;

            JObject request = JObject.Parse(requestJson);
            requestId = request["id"]?.ToObject<object>();
            string method = request["method"]?.ToString();
            JToken paramsToken = request["params"];

            if (string.IsNullOrEmpty(method))
            {
                return CreateErrorResponse(requestId, DeviceAgentConstants.ErrorCodes.INVALID_REQUEST, "Missing method");
            }

            // auth.login is always allowed
            if (method == "auth.login")
            {
                return ProcessAuthLogin(requestId, paramsToken);
            }

            // All other methods require authentication
            if (!_authenticated)
            {
                return CreateErrorResponse(requestId, DeviceAgentConstants.ErrorCodes.UNAUTHORIZED, "Authentication required. Call auth.login first.");
            }

            // Dispatch tool execution to main thread
            return await ExecuteToolOnMainThreadAsync(requestId, method, paramsToken, ct);
        }

        private string ProcessAuthLogin(object requestId, JToken paramsToken)
        {
            string token = paramsToken?["token"]?.ToString();
            string cliVersion = paramsToken?["cliVersion"]?.ToString();

            if (string.IsNullOrEmpty(token))
            {
                return CreateErrorResponse(requestId, DeviceAgentConstants.ErrorCodes.INVALID_PARAMS, "Missing token parameter");
            }

            if (string.IsNullOrEmpty(cliVersion))
            {
                return CreateErrorResponse(requestId, DeviceAgentConstants.ErrorCodes.INVALID_PARAMS, "Missing cliVersion parameter");
            }

            // Version compatibility check
            if (!IsCompatibleVersion(cliVersion, DeviceAgentConstants.MIN_CLI_VERSION))
            {
                return CreateErrorResponse(requestId, DeviceAgentConstants.ErrorCodes.INCOMPATIBLE_VERSION,
                    $"CLI version {cliVersion} is not compatible. Minimum required: {DeviceAgentConstants.MIN_CLI_VERSION}");
            }

            if (token != _expectedToken)
            {
                return CreateErrorResponse(requestId, DeviceAgentConstants.ErrorCodes.UNAUTHORIZED, "Invalid token");
            }

            _authenticated = true;

            JObject result = new()
            {
                ["protocolVersion"] = DeviceAgentConstants.PROTOCOL_VERSION,
                ["agentVersion"] = DeviceAgentConstants.AGENT_VERSION,
                ["minCliVersion"] = DeviceAgentConstants.MIN_CLI_VERSION,
                ["capabilities"] = new JObject
                {
                    ["screenshotFormats"] = new JArray("png", "jpg"),
                    ["tools"] = JArray.FromObject(_registry.GetToolNames())
                }
            };

            return CreateSuccessResponse(requestId, result);
        }

        private async Task<string> ExecuteToolOnMainThreadAsync(object requestId, string method, JToken paramsToken, CancellationToken ct)
        {
            IUnityTool tool = _registry.GetTool(method);
            if (tool == null)
            {
                return CreateErrorResponse(requestId, DeviceAgentConstants.ErrorCodes.METHOD_NOT_FOUND, $"Unknown method: {method}");
            }

            // Execute on main thread via dispatcher
            TaskCompletionSource<BaseToolResponse> tcs = new();

            using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(DeviceAgentConstants.REQUEST_TIMEOUT_SECONDS));
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            CancellationToken linkedToken = linkedCts.Token;

            MainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    Task<BaseToolResponse> task = tool.ExecuteAsync(paramsToken);
                    task.ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            tcs.TrySetException(t.Exception.InnerException ?? t.Exception);
                        else if (t.IsCanceled)
                            tcs.TrySetCanceled();
                        else
                            tcs.TrySetResult(t.Result);
                    });
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            try
            {
                using (linkedToken.Register(() => tcs.TrySetCanceled()))
                {
                    BaseToolResponse response = await tcs.Task;
                    JToken resultToken = JToken.FromObject(response, JsonSerializer.Create(new JsonSerializerSettings
                    {
                        ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
                        NullValueHandling = NullValueHandling.Ignore
                    }));
                    return CreateSuccessResponse(requestId, resultToken);
                }
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                return CreateErrorResponse(requestId, DeviceAgentConstants.ErrorCodes.TIMEOUT, "Request timed out");
            }
            catch (Exception ex)
            {
                return CreateErrorResponse(requestId, DeviceAgentConstants.ErrorCodes.INTERNAL_ERROR, ex.Message);
            }
        }

        private static bool IsCompatibleVersion(string cliVersion, string minVersion)
        {
            if (!Version.TryParse(NormalizeSemVer(cliVersion), out Version cli)) return false;
            if (!Version.TryParse(NormalizeSemVer(minVersion), out Version min)) return false;
            return cli >= min;
        }

        // Strip pre-release/build metadata for System.Version compatibility
        private static string NormalizeSemVer(string version)
        {
            int dashIndex = version.IndexOf('-');
            if (dashIndex >= 0) version = version.Substring(0, dashIndex);
            int plusIndex = version.IndexOf('+');
            if (plusIndex >= 0) version = version.Substring(0, plusIndex);
            return version;
        }

        public static string CreateErrorResponse(object id, int code, string message)
        {
            JObject response = new()
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id != null ? JToken.FromObject(id) : JValue.CreateNull(),
                ["error"] = new JObject
                {
                    ["code"] = code,
                    ["message"] = message
                }
            };
            return response.ToString(Formatting.None);
        }

        private static string CreateSuccessResponse(object id, JToken result)
        {
            JObject response = new()
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id != null ? JToken.FromObject(id) : JValue.CreateNull(),
                ["result"] = result
            };
            return response.ToString(Formatting.None);
        }
    }
}
