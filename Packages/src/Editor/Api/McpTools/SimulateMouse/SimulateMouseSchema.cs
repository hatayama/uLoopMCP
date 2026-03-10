using System.ComponentModel;

namespace io.github.hatayama.uLoopMCP
{
    public class SimulateMouseSchema : BaseToolSchema
    {
        [Description("Mouse action: Click(0) - left click, Drag(1) - one-shot drag, DragStart(2) - begin drag and hold, DragMove(3) - move while holding drag, DragEnd(4) - release drag")]
        public MouseAction Action { get; set; } = MouseAction.Click;

        [Description("X position in screen pixels (origin: top-left)")]
        public float X { get; set; } = 0f;

        [Description("Y position in screen pixels (origin: top-left)")]
        public float Y { get; set; } = 0f;

        [Description("End X position in screen pixels for Drag action (origin: top-left)")]
        public float EndX { get; set; } = 0f;

        [Description("End Y position in screen pixels for Drag action (origin: top-left)")]
        public float EndY { get; set; } = 0f;

        [Description("Drag speed in pixels per second (0 for instant). Applies to Drag, DragMove, and DragEnd actions.")]
        public float DragSpeed { get; set; } = McpConstants.SIMULATE_MOUSE_DEFAULT_DRAG_SPEED;
    }
}
