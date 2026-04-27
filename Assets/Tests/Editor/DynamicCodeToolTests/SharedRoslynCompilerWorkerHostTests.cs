using System.Diagnostics;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    [TestFixture]
    public class SharedRoslynCompilerWorkerHostTests
    {
        private const string DotnetHostPath = @"C:\Unity\Editor\Data\NetCoreRuntime\dotnet.exe";

        [Test]
        public void ConfigureWorkerDotnetRuntimeEnvironment_WhenCalled_ShouldDisableMultilevelLookup()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.EnvironmentVariables[SharedRoslynCompilerWorkerHost.DotnetMultilevelLookupEnvironmentVariableName] = "1";

            SharedRoslynCompilerWorkerHost.ConfigureWorkerDotnetRuntimeEnvironment(
                startInfo,
                DotnetHostPath);

            Assert.That(
                startInfo.EnvironmentVariables[SharedRoslynCompilerWorkerHost.DotnetMultilevelLookupEnvironmentVariableName],
                Is.EqualTo(SharedRoslynCompilerWorkerHost.DotnetMultilevelLookupDisabledValue));
        }
    }
}
