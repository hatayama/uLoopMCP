using System;

namespace io.github.hatayama.uLoopMCP
{
    [Serializable]
    public record ToolSettingsData
    {
        public string[] disabledTools = Array.Empty<string>();
    }
}
