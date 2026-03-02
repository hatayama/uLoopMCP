using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Registry for Device Agent tools.
    /// Tools are registered manually (no reflection scanning) to keep IL2CPP-safe.
    /// </summary>
    public sealed class DeviceToolRegistry
    {
        private readonly Dictionary<string, IUnityTool> _tools = new();

        public void Register(IUnityTool tool)
        {
            Debug.Assert(tool != null, "tool must not be null");
            Debug.Assert(!string.IsNullOrEmpty(tool.ToolName), "tool.ToolName must not be empty");
            _tools[tool.ToolName] = tool;
        }

        public IUnityTool GetTool(string name)
        {
            _tools.TryGetValue(name, out IUnityTool tool);
            return tool;
        }

        public string[] GetToolNames()
        {
            return _tools.Keys.ToArray();
        }
    }
}
