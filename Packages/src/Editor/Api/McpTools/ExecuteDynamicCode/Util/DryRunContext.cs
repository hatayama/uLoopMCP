// Dry-run context to simulate operations and collect logs
#if UNITY_EDITOR
using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    public class DryRunContext
    {
        public bool Enabled { get; private set; }
        public List<string> Logs { get; } = new List<string>();

        public DryRunContext(bool enabled)
        {
            Enabled = enabled;
        }

        public void Log(string message)
        {
            Logs.Add(message);
        }

        public static bool IsActive(DryRunContext ctx)
        {
            return ctx != null && ctx.Enabled;
        }
    }
}
#endif


