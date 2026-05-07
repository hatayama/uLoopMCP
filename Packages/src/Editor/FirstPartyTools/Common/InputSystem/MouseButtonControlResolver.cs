#if ULOOP_HAS_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace io.github.hatayama.UnityCliLoop
{
    // Converts the runtime mouse-button value into the Input System control used by injected events.
    internal static class MouseButtonControlResolver
    {
        public static ButtonControl GetButtonControl(Mouse mouse, MouseButton button)
        {
            Debug.Assert(mouse != null, "mouse must not be null.");

            switch (button)
            {
                case MouseButton.Right:
                    return mouse.rightButton;
                case MouseButton.Middle:
                    return mouse.middleButton;
                default:
                    Debug.Assert(button == MouseButton.Left, $"Unexpected MouseButton value: {button}");
                    return mouse.leftButton;
            }
        }
    }
}
#endif
