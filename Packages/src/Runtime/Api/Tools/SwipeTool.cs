using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace io.github.hatayama.uLoopMCP
{
    public sealed class SwipeSchema : BaseToolSchema
    {
        public float StartX { get; set; }
        public float StartY { get; set; }
        public float EndX { get; set; }
        public float EndY { get; set; }
        public int DurationMs { get; set; } = 300;
    }

    public sealed class SwipeResponse : BaseToolResponse
    {
        public bool Swiped { get; set; }
        public string Error { get; set; }
    }

    public sealed class SwipeTool : AbstractDeviceTool<SwipeSchema, SwipeResponse>
    {
        public override string ToolName => "swipe";

        protected override async Task<SwipeResponse> ExecuteAsync(SwipeSchema parameters, CancellationToken ct)
        {
            if (!EventSystemHelper.IsEventSystemAvailable())
            {
                return new SwipeResponse { Swiped = false, Error = "EventSystem not available" };
            }

            Vector2 startScreen = new(
                parameters.StartX * Screen.width,
                parameters.StartY * Screen.height
            );
            Vector2 endScreen = new(
                parameters.EndX * Screen.width,
                parameters.EndY * Screen.height
            );

            float durationSec = parameters.DurationMs / 1000f;
            int steps = Mathf.Max(1, Mathf.RoundToInt(durationSec * 60f));
            float stepInterval = durationSec / steps;

            PointerEventData pointerData = new(EventSystem.current)
            {
                position = startScreen,
                pressPosition = startScreen
            };

            GameObject beginTarget = EventSystemHelper.RaycastFirstHit(pointerData);
            if (beginTarget == null)
            {
                return new SwipeResponse { Swiped = false, Error = "No UI element at start position" };
            }

            ExecuteEvents.Execute(beginTarget, pointerData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(beginTarget, pointerData, ExecuteEvents.beginDragHandler);

            Vector2 previousPosition = startScreen;
            for (int i = 1; i <= steps; i++)
            {
                ct.ThrowIfCancellationRequested();

                float t = (float)i / steps;
                pointerData.position = Vector2.Lerp(startScreen, endScreen, t);
                pointerData.delta = pointerData.position - previousPosition;
                previousPosition = pointerData.position;

                GameObject dragTarget = EventSystemHelper.RaycastFirstHit(pointerData);
                if (dragTarget != null)
                {
                    ExecuteEvents.Execute(dragTarget, pointerData, ExecuteEvents.dragHandler);
                }

                await Task.Delay(Mathf.RoundToInt(stepInterval * 1000f), ct);
            }

            pointerData.position = endScreen;
            GameObject endTarget = EventSystemHelper.RaycastFirstHit(pointerData);
            if (endTarget != null)
            {
                ExecuteEvents.Execute(endTarget, pointerData, ExecuteEvents.endDragHandler);
                ExecuteEvents.Execute(endTarget, pointerData, ExecuteEvents.pointerUpHandler);
            }

            return new SwipeResponse { Swiped = true };
        }
    }
}
