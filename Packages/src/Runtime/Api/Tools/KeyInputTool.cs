using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public sealed class KeyInputSchema : BaseToolSchema
    {
        public string KeyCode { get; set; }
    }

    public sealed class KeyInputResponse : BaseToolResponse
    {
        public bool Success { get; set; }
        public string KeyName { get; set; }
        public string Error { get; set; }
    }

    public sealed class KeyInputTool : AbstractDeviceTool<KeyInputSchema, KeyInputResponse>
    {
        public override string ToolName => "key-input";

        protected override Task<KeyInputResponse> ExecuteAsync(KeyInputSchema parameters, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(parameters.KeyCode))
            {
                return Task.FromResult(new KeyInputResponse
                {
                    Success = false,
                    Error = "keyCode is required"
                });
            }

            if (!System.Enum.TryParse(parameters.KeyCode, true, out UnityEngine.KeyCode parsedKey))
            {
                return Task.FromResult(new KeyInputResponse
                {
                    Success = false,
                    Error = $"Unknown keyCode: {parameters.KeyCode}"
                });
            }

            // InputTestFixture (Input System) is unavailable in runtime builds;
            // EventSystem only exposes submitHandler/cancelHandler, so arbitrary keys map to those two
            if (!EventSystemHelper.IsEventSystemAvailable())
            {
                return Task.FromResult(new KeyInputResponse
                {
                    Success = false,
                    Error = "EventSystem not available"
                });
            }

            EventSystemHelper.SimulateKey(parsedKey);

            return Task.FromResult(new KeyInputResponse
            {
                Success = true,
                KeyName = parsedKey.ToString()
            });
        }
    }
}
