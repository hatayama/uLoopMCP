using System.Diagnostics;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP
{
    [TestFixture]
    public class ProcessStartHelperTests
    {
        [Test]
        public void TryStart_NonExistentExecutable_ReturnsNull()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "/nonexistent/path/to/executable",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process result = ProcessStartHelper.TryStart(startInfo);

            Assert.IsNull(result);
        }

        [Test]
        public void TryStart_ValidExecutable_ReturnsNonNullProcess()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "/bin/echo",
                Arguments = "test",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            Process result = ProcessStartHelper.TryStart(startInfo);

            Assert.IsNotNull(result);
            result.WaitForExit();
            result.Dispose();
        }
    }
}
