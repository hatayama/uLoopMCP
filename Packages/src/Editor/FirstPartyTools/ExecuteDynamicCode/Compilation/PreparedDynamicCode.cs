using System.Collections.Generic;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    public sealed class PreparedDynamicCode
    {
        public string PreparedSource { get; }
        public bool IsScriptMode { get; }
        public List<HoistedLiteralBinding> HoistedLiteralBindings { get; }

        public PreparedDynamicCode(
            string preparedSource,
            bool isScriptMode,
            List<HoistedLiteralBinding> hoistedLiteralBindings)
        {
            PreparedSource = preparedSource;
            IsScriptMode = isScriptMode;
            HoistedLiteralBindings = hoistedLiteralBindings ?? new List<HoistedLiteralBinding>();
        }
    }
}
