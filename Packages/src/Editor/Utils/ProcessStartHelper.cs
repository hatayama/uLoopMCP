using System.ComponentModel;
using System.Diagnostics;

namespace io.github.hatayama.uLoopMCP
{
    public static class ProcessStartHelper
    {
        // Win32Exception from Process.Start means the OS cannot locate the executable.
        // Returning null lets callers handle "not found" via their existing null-check path.
        public static Process TryStart(ProcessStartInfo startInfo)
        {
            try
            {
                return Process.Start(startInfo);
            }
            catch (Win32Exception)
            {
                return null;
            }
        }
    }
}
