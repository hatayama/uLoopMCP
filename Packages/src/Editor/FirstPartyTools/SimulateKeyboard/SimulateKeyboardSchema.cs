using System.ComponentModel;

using io.github.hatayama.UnityCliLoop.Runtime;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    public class SimulateKeyboardSchema : UnityCliLoopToolSchema
    {
        [Description("Keyboard action: Press(0) - one-shot key tap (Down then Up), KeyDown(1) - hold key down, KeyUp(2) - release held key")]
        public UnityCliLoopKeyboardAction Action { get; set; } = UnityCliLoopKeyboardAction.Press;

        [Description("Key name matching the Input System Key enum (e.g. \"W\", \"Space\", \"LeftShift\", \"A\", \"Enter\"). Case-insensitive.")]
        public string Key { get; set; } = "";

        [Description("Hold duration in seconds for Press action (default: 0 = one-shot tap). Ignored by KeyDown/KeyUp.")]
        public float Duration { get; set; } = 0f;
    }
}
