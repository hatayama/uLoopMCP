using System.ComponentModel;

namespace io.github.hatayama.uLoopMCP
{
    public class SimulateMouseSchema : BaseToolSchema
    {
        [Description("Mouse action: Click(0) - left click, Drag(1) - one-shot drag, DragStart(2) - begin drag and hold, DragMove(3) - move while holding drag, DragEnd(4) - release drag, LongPress(5) - press and hold for Duration seconds")]
        public MouseAction Action { get; set; } = MouseAction.Click;

        [Description("Target X position in screen pixels (origin: top-left). For Drag action, this is the destination.")]
        public float X { get; set; } = 0f;

        [Description("Target Y position in screen pixels (origin: top-left). For Drag action, this is the destination.")]
        public float Y { get; set; } = 0f;

        [Description("Start X position in screen pixels for Drag action (origin: top-left). Drag starts here and moves to X,Y.")]
        public float FromX { get; set; } = 0f;

        [Description("Start Y position in screen pixels for Drag action (origin: top-left). Drag starts here and moves to X,Y.")]
        public float FromY { get; set; } = 0f;

        [Description("Drag speed in pixels per second (0 for instant). Applies to Drag, DragMove, and DragEnd actions.")]
        public float DragSpeed { get; set; } = McpConstants.SIMULATE_MOUSE_DEFAULT_DRAG_SPEED;

        [Description("Hold duration in seconds for LongPress action (default: 0.5).")]
        public float Duration { get; set; } = 0.5f;
    }
}
