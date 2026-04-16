using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace io.github.hatayama.uLoopMCP
{
    internal sealed class PrewarmDynamicCodeUseCase : IPrewarmDynamicCodeUseCase
    {
        private const int AutoPrewarmDelayFrameCount = 1;
        private const int AutoPrewarmPassCount = 3;
        // Why: startup measurements showed the first user-visible execute-dynamic-code request
        // stayed cold until the default wrapper shape and Unity's logging path had both run once.
        // Why not a cheaper "return null;" snippet or a dedicated prewarm class name: those warm
        // compiler registration, but they miss the default execute-dynamic-code cache key and
        // leave the first real request measurably slower.
        // Why: after a forced compile + domain reload, the first two execute-dynamic-code requests
        // still paid cold-start costs, and only the third pass reached steady-state latency in measurements.
        // Why not stop at two passes: that still leaves the first post-reload user-visible request
        // hundreds of milliseconds slower than the warmed path, which is the exact regression we need to avoid.
        private const string AutoPrewarmCode =
            "using UnityEngine; bool previous = Debug.unityLogger.logEnabled; Debug.unityLogger.logEnabled = false; try { Debug.Log(\"Unity CLI Loop dynamic code prewarm\"); return \"Unity CLI Loop dynamic code prewarm\"; } finally { Debug.unityLogger.logEnabled = previous; }";
        private const string AutoPrewarmClassName = DynamicCodeConstants.DEFAULT_CLASS_NAME;
        private const string AutoPrewarmOperation = "dynamic_code_auto_prewarm";

        private readonly IDynamicCodeExecutionRuntime _runtime;
        private readonly IDynamicCodeAutoPrewarmExecutor _executor;
        private readonly CancellationToken _lifecycleCancellationToken;
        private readonly string _serverStartingLockToken;
        private readonly object _autoPrewarmLock = new();
        private Task _autoPrewarmTask;

        public PrewarmDynamicCodeUseCase(
            IDynamicCodeExecutionRuntime runtime,
            CancellationToken lifecycleCancellationToken = default,
            IDynamicCodeAutoPrewarmExecutor executor = null,
            string serverStartingLockToken = null)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _executor = executor ?? new TcpDynamicCodeAutoPrewarmExecutor();
            _lifecycleCancellationToken = lifecycleCancellationToken;
            _serverStartingLockToken = serverStartingLockToken;
        }

        public void Request()
        {
            RequestAsync().Forget();
        }

        public Task RequestAsync()
        {
            lock (_autoPrewarmLock)
            {
                if (_autoPrewarmTask != null && !_autoPrewarmTask.IsCompleted)
                {
                    return _autoPrewarmTask;
                }

                DynamicCodeStartupTelemetry.MarkPrewarmQueued();
                _autoPrewarmTask = RunAsync();
                return _autoPrewarmTask;
            }
        }

        private async Task RunAsync()
        {
            try
            {
                _lifecycleCancellationToken.ThrowIfCancellationRequested();
                if (!_runtime.SupportsAutoPrewarm())
                {
                    DynamicCodeStartupTelemetry.MarkPrewarmSkipped("fast_path_unavailable");
                    VibeLogger.LogInfo(
                        AutoPrewarmOperation,
                        "Skipping dynamic code auto prewarm because the fast path is unavailable",
                        new { reason = "fast_path_unavailable" });

                    return;
                }

                VibeLogger.LogInfo(
                    AutoPrewarmOperation,
                    "Starting dynamic code auto prewarm",
                    new
                    {
                        delay_frames = AutoPrewarmDelayFrameCount,
                        class_name = AutoPrewarmClassName,
                        pass_count = AutoPrewarmPassCount
                    });

                await EditorDelay.DelayFrame(AutoPrewarmDelayFrameCount, _lifecycleCancellationToken);
                DynamicCodeStartupTelemetry.MarkPrewarmStarted();

                for (int passIndex = 0; passIndex < AutoPrewarmPassCount; passIndex++)
                {
                    ExecuteDynamicCodeSchema parameters = new ExecuteDynamicCodeSchema
                    {
                        Code = AutoPrewarmCode,
                        CompileOnly = false,
                        YieldToForegroundRequests = true
                    };

                    // Why: the remaining domain-reload spike only disappeared when measurements warmed the
                    // full execute-dynamic-code entry path, not just the runtime facade underneath it.
                    // Why not keep the runtime-direct prewarm: that warmed compiler startup, but it still
                    // left the first real CLI request paying extra cost in the tool/JSON-RPC layers.
                    DynamicCodeAutoPrewarmResult result = await _executor.ExecuteAsync(
                        parameters,
                        _lifecycleCancellationToken);

                    if (IsExecutionBusy(result))
                    {
                        DynamicCodeStartupTelemetry.MarkPrewarmSkipped("runtime_busy");
                        VibeLogger.LogInfo(
                            AutoPrewarmOperation,
                            "Skipping dynamic code auto prewarm because execute-dynamic-code is busy",
                            new
                            {
                                class_name = AutoPrewarmClassName,
                                reason = "runtime_busy",
                                pass_index = passIndex + 1,
                                pass_count = AutoPrewarmPassCount
                            });
                        return;
                    }

                    if (WasCancelledByForegroundRequest(result))
                    {
                        DynamicCodeStartupTelemetry.MarkPrewarmYielded("foreground_request_preempted");
                        LogForegroundPreemption();
                        return;
                    }

                    if (!result.Success)
                    {
                        DynamicCodeStartupTelemetry.MarkPrewarmFailed(result.ErrorMessage);
                        VibeLogger.LogWarning(
                            AutoPrewarmOperation,
                            "Dynamic code auto prewarm failed",
                            new
                            {
                                class_name = AutoPrewarmClassName,
                                error_message = result.ErrorMessage,
                                pass_index = passIndex + 1,
                                pass_count = AutoPrewarmPassCount
                            });
                        return;
                    }

                    VibeLogger.LogInfo(
                        AutoPrewarmOperation,
                        "Dynamic code auto prewarm pass completed successfully",
                        new
                        {
                            class_name = AutoPrewarmClassName,
                            pass_index = passIndex + 1,
                            pass_count = AutoPrewarmPassCount
                        });
                }

                DynamicCodeStartupTelemetry.MarkPrewarmCompleted();
                VibeLogger.LogInfo(
                    AutoPrewarmOperation,
                    "Dynamic code auto prewarm completed successfully",
                    new
                    {
                        class_name = AutoPrewarmClassName,
                        pass_count = AutoPrewarmPassCount
                    });
                return;
            }
            catch (OperationCanceledException) when (_lifecycleCancellationToken.IsCancellationRequested)
            {
                DynamicCodeStartupTelemetry.MarkPrewarmSkipped("lifecycle_cancelled");
                return;
            }
            catch (ObjectDisposedException) when (_lifecycleCancellationToken.IsCancellationRequested)
            {
                DynamicCodeStartupTelemetry.MarkPrewarmSkipped("lifecycle_cancelled");
                return;
            }
            finally
            {
                ServerStartingLockService.DeleteLockFile(_serverStartingLockToken);
            }
        }

        private static bool WasCancelledByForegroundRequest(DynamicCodeAutoPrewarmResult result)
        {
            if (result == null)
            {
                return false;
            }

            return !result.Success
                && string.Equals(
                    result.ErrorMessage,
                    McpConstants.ERROR_MESSAGE_EXECUTION_CANCELLED,
                    StringComparison.Ordinal);
        }

        private static bool IsExecutionBusy(DynamicCodeAutoPrewarmResult result)
        {
            if (result == null)
            {
                return false;
            }

            return !result.Success
                && string.Equals(
                    result.ErrorMessage,
                    McpConstants.ERROR_MESSAGE_EXECUTION_IN_PROGRESS,
                    StringComparison.Ordinal);
        }

        private static void LogForegroundPreemption()
        {
            VibeLogger.LogInfo(
                AutoPrewarmOperation,
                "Dynamic code auto prewarm yielded to a foreground execute-dynamic-code request",
                new { reason = "foreground_request_preempted" });
        }

    }

    internal interface IDynamicCodeAutoPrewarmExecutor
    {
        Task<DynamicCodeAutoPrewarmResult> ExecuteAsync(
            ExecuteDynamicCodeSchema parameters,
            CancellationToken ct);
    }

    internal sealed class DynamicCodeAutoPrewarmResult
    {
        public bool Success { get; set; }

        public string ErrorMessage { get; set; }
    }

    internal sealed class TcpDynamicCodeAutoPrewarmExecutor : IDynamicCodeAutoPrewarmExecutor
    {
        private const string AutoPrewarmRequestId = "dynamic-code-auto-prewarm";
        private const int LoopbackPrewarmTimeoutMilliseconds = 10000;

        public async Task<DynamicCodeAutoPrewarmResult> ExecuteAsync(
            ExecuteDynamicCodeSchema parameters,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            string requestJson = CreateRequestJson(parameters);
            // Why: measurements showed that even the in-process JSON-RPC path left the first
            // post-reload CLI request around 500ms, while a real loopback TCP request fully
            // warmed the remaining listener/frame-parser path.
            // Why not stop at JsonRpcProcessor.ProcessRequest: that bypasses the transport layer
            // that real Unity CLI Loop requests still have to pay on their first trip after reload.
            using CancellationTokenSource timeoutCancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCancellationTokenSource.CancelAfter(LoopbackPrewarmTimeoutMilliseconds);
            try
            {
                string responseJson = await SendRequestOverLoopbackAsync(
                    requestJson,
                    timeoutCancellationTokenSource.Token);

                ct.ThrowIfCancellationRequested();
                return ParseResponse(responseJson);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return new DynamicCodeAutoPrewarmResult
                {
                    Success = false,
                    ErrorMessage = "dynamic code auto prewarm timed out"
                };
            }
        }

        private static string CreateRequestJson(ExecuteDynamicCodeSchema parameters)
        {
            JsonSerializer serializer = JsonSerializer.Create(
                new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
            JObject request = new JObject
            {
                ["jsonrpc"] = McpServerConfig.JSONRPC_VERSION,
                ["id"] = AutoPrewarmRequestId,
                ["method"] = McpConstants.TOOL_NAME_EXECUTE_DYNAMIC_CODE,
                ["params"] = JObject.FromObject(parameters, serializer),
                ["x-uloop"] = new JObject
                {
                    ["expectedProjectRoot"] = McpEditorSettings.GetProjectRootPath(),
                    ["expectedServerSessionId"] = McpEditorSettings.GetServerSessionId()
                }
            };

            return request.ToString(Formatting.None);
        }

        private static async Task<string> SendRequestOverLoopbackAsync(
            string requestJson,
            CancellationToken ct)
        {
            using TcpClient client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, McpServerController.ServerPort).ConfigureAwait(false);
            using NetworkStream stream = client.GetStream();

            string framedRequest = CreateContentLengthFrame(requestJson);
            byte[] requestBytes = Encoding.UTF8.GetBytes(framedRequest);
            await stream.WriteAsync(requestBytes, 0, requestBytes.Length, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
            return await ReadFramedResponseAsync(stream, ct).ConfigureAwait(false);
        }

        private static async Task<string> ReadFramedResponseAsync(
            NetworkStream stream,
            CancellationToken ct)
        {
            FrameParser frameParser = new FrameParser();
            byte[] readBuffer = new byte[BufferConfig.INITIAL_BUFFER_SIZE];
            using MemoryStream responseBuffer = new MemoryStream();

            while (true)
            {
                int bytesRead = await stream.ReadAsync(readBuffer, 0, readBuffer.Length, ct).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    throw new InvalidOperationException("Loopback prewarm connection closed before a JSON-RPC response was received.");
                }

                responseBuffer.Write(readBuffer, 0, bytesRead);
                byte[] bufferedBytes = responseBuffer.GetBuffer();
                int bufferedLength = checked((int)responseBuffer.Length);

                bool parsed = frameParser.TryParseFrame(
                    bufferedBytes,
                    bufferedLength,
                    out int contentLength,
                    out int headerLength);
                if (!parsed)
                {
                    continue;
                }

                if (!frameParser.IsCompleteFrame(bufferedBytes, bufferedLength, contentLength, headerLength))
                {
                    continue;
                }

                return frameParser.ExtractJsonContent(bufferedBytes, contentLength, headerLength);
            }
        }

        private static string CreateContentLengthFrame(string jsonContent)
        {
            int contentLength = Encoding.UTF8.GetByteCount(jsonContent);
            return $"Content-Length: {contentLength}\r\n\r\n{jsonContent}";
        }

        private static DynamicCodeAutoPrewarmResult ParseResponse(string responseJson)
        {
            JObject response = JObject.Parse(responseJson);
            JToken resultToken = response["result"];
            if (resultToken != null && resultToken.Type != JTokenType.Null)
            {
                ExecuteDynamicCodeResponse executeResponse = resultToken.ToObject<ExecuteDynamicCodeResponse>();
                return new DynamicCodeAutoPrewarmResult
                {
                    Success = executeResponse?.Success == true,
                    ErrorMessage = executeResponse?.ErrorMessage
                };
            }

            JToken errorToken = response["error"];
            return new DynamicCodeAutoPrewarmResult
            {
                Success = false,
                ErrorMessage = errorToken?["message"]?.ToString() ?? "Unknown error"
            };
        }
    }
}
