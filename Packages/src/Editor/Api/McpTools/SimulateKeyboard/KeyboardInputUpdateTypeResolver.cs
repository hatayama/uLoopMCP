#nullable enable
#if ULOOPMCP_HAS_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace io.github.hatayama.uLoopMCP
{
    // Projects can process Input System events in dynamic, fixed, or manual mode.
    // Keyboard simulation must follow that configured loop to avoid frame mismatches.
    internal static class KeyboardInputUpdateTypeResolver
    {
        public static InputUpdateType Resolve()
        {
            InputSettings? settings = InputSystem.settings;
            if (settings == null)
            {
                return InputUpdateType.Dynamic;
            }

            InputSettings.UpdateMode updateMode = settings.updateMode;
            switch (updateMode)
            {
                case InputSettings.UpdateMode.ProcessEventsInFixedUpdate:
                    return InputUpdateType.Fixed;

                case InputSettings.UpdateMode.ProcessEventsManually:
                    return InputUpdateType.Manual;

                default:
                    return InputUpdateType.Dynamic;
            }
        }

        public static bool IsMatch(InputUpdateType current, InputUpdateType expected)
        {
            return (current & expected) == expected;
        }
    }
}
#endif
