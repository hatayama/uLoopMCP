using System.ComponentModel;

namespace io.github.hatayama.uLoopMCP
{
    public enum MouseAction
    {
        Click = 0,
        Drag = 1,
        DragStart = 2,
        DragMove = 3,
        DragEnd = 4
    }

    public class SimulateMouseSchema : BaseToolSchema
    {
        [Description("Mouse action: Click(0) - left click, Drag(1) - one-shot drag, DragStart(2) - begin drag and hold, DragMove(3) - move while holding drag, DragEnd(4) - release drag")]
        public MouseAction Action { get; set; } = MouseAction.Click;

        [Description("X position in screen pixels (origin: bottom-left)")]
        public float X { get; set; } = 0f;

        [Description("Y position in screen pixels (origin: bottom-left)")]
        public float Y { get; set; } = 0f;

        [Description("End X position in screen pixels for Drag action (origin: bottom-left)")]
        public float EndX { get; set; } = 0f;

        [Description("End Y position in screen pixels for Drag action (origin: bottom-left)")]
        public float EndY { get; set; } = 0f;

        [Description("Number of intermediate drag steps for Drag action (higher = smoother, each step = 1 frame)")]
        public int DragSteps { get; set; } = 5;
    }
}
