using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public sealed class TapCoordinateSchema : BaseToolSchema
    {
        public float X { get; set; }
        public float Y { get; set; }
    }

    public sealed class TapCoordinateResponse : BaseToolResponse
    {
        public bool Tapped { get; set; }
        public string HitObjectName { get; set; }
        public string Error { get; set; }
    }

    public sealed class TapCoordinateTool : AbstractDeviceTool<TapCoordinateSchema, TapCoordinateResponse>
    {
        public override string ToolName => "tap-coordinate";

        protected override Task<TapCoordinateResponse> ExecuteAsync(TapCoordinateSchema parameters, CancellationToken ct)
        {
            if (!EventSystemHelper.IsEventSystemAvailable())
            {
                return Task.FromResult(new TapCoordinateResponse { Tapped = false, Error = "EventSystem not available" });
            }

            // Device screen resolutions vary; normalized coordinates (0.0-1.0) decouple callers from pixel dimensions
            Vector2 screenPos = new(
                parameters.X * Screen.width,
                parameters.Y * Screen.height
            );

            bool success = EventSystemHelper.SimulateClickAtPosition(screenPos, out GameObject hitObject);

            return Task.FromResult(new TapCoordinateResponse
            {
                Tapped = success,
                HitObjectName = hitObject != null ? hitObject.name : null
            });
        }
    }
}
