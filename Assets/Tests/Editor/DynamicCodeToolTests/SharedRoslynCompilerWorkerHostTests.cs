using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    [TestFixture]
    public class SharedRoslynCompilerWorkerHostTests
    {
        [Test]
        public void ConfigureWorkerDotnetRuntimeEnvironment_WhenCalled_ShouldDisableMultilevelLookup()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.EnvironmentVariables[SharedRoslynCompilerWorkerHost.DotnetMultilevelLookupEnvironmentVariableName] = "1";

            SharedRoslynCompilerWorkerHost.ConfigureWorkerDotnetRuntimeEnvironment(startInfo);

            Assert.That(
                startInfo.EnvironmentVariables[SharedRoslynCompilerWorkerHost.DotnetMultilevelLookupEnvironmentVariableName],
                Is.EqualTo(SharedRoslynCompilerWorkerHost.DotnetMultilevelLookupDisabledValue));
        }

        [Test]
        public void CreateCompileRequestCommand_WhenPathIsWindowsAbsolutePath_ShouldEncodeAsciiPayload()
        {
            string requestFilePath =
                @"C:\Users\S06870\Documents\unity\vision-client_02\vision-client\Temp\uLoopMCPCompilation\DynamicCommand_1.worker";

            string command = SharedRoslynCompilerWorkerHost.CreateCompileRequestCommandForTests(requestFilePath);

            Assert.That(command, Does.StartWith(SharedRoslynCompilerWorkerHost.CompileRequestPathPrefix));
            Assert.That(command, Does.Not.Contain(requestFilePath));
            foreach (char character in command)
            {
                Assert.That(character, Is.LessThanOrEqualTo((char)127));
            }

            string encodedPath = command.Substring(SharedRoslynCompilerWorkerHost.CompileRequestPathPrefix.Length);
            string decodedPath = Encoding.UTF8.GetString(Convert.FromBase64String(encodedPath));
            Assert.That(decodedPath, Is.EqualTo(Path.GetFullPath(requestFilePath)));
        }

        [Test]
        public void CreateProgramSource_WhenRequestPathHasNoPrefix_ShouldRecoverRawPath()
        {
            string programSource = SharedRoslynCompilerWorkerHost.CreateProgramSourceForTests();

            Assert.That(programSource, Does.Contain("return RecoverRawRequestPath(requestPath);"));
            Assert.That(programSource, Does.Contain("FindWindowsDrivePathIndex"));
            Assert.That(programSource, Does.Not.Contain("Unsupported request path protocol"));
        }
    }
}
