using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP
{
    [TestFixture]
    public class NpmInstallDiagnosticsTests
    {
        // --- ClassifyInstallError ---

        [Test]
        public void ClassifyInstallError_NullInput_ReturnsNull()
        {
            string result = NpmInstallDiagnostics.ClassifyInstallError(null);

            Assert.IsNull(result);
        }

        [Test]
        public void ClassifyInstallError_EmptyInput_ReturnsNull()
        {
            string result = NpmInstallDiagnostics.ClassifyInstallError("");

            Assert.IsNull(result);
        }

        [Test]
        public void ClassifyInstallError_WithEPERM_ReturnsPermissionGuidance()
        {
            string stderr = "npm ERR! Error: EPERM: operation not permitted, rename 'C:\\Users\\test'";

            string result = NpmInstallDiagnostics.ClassifyInstallError(stderr);

            Assert.IsNotNull(result);
            Assert.That(result, Does.Contain("permission"));
        }

        [Test]
        public void ClassifyInstallError_WithEACCES_ReturnsPermissionGuidance()
        {
            string stderr = "npm ERR! Error: EACCES: permission denied, access '/usr/lib/node_modules'";

            string result = NpmInstallDiagnostics.ClassifyInstallError(stderr);

            Assert.IsNotNull(result);
            Assert.That(result, Does.Contain("permission"));
        }

        [Test]
        public void ClassifyInstallError_WithOperationNotPermitted_ReturnsPermissionGuidance()
        {
            string stderr = "npm ERR! Error: operation not permitted, mkdir 'C:\\Program Files\\nodejs'";

            string result = NpmInstallDiagnostics.ClassifyInstallError(stderr);

            Assert.IsNotNull(result);
            Assert.That(result, Does.Contain("permission"));
        }

        [Test]
        public void ClassifyInstallError_UnrecognizedError_ReturnsNull()
        {
            string stderr = "npm ERR! 404 Not Found - GET https://registry.npmjs.org/nonexistent";

            string result = NpmInstallDiagnostics.ClassifyInstallError(stderr);

            Assert.IsNull(result);
        }

        // --- IsPermissionError ---

        [Test]
        public void IsPermissionError_WithEPERM_ReturnsTrue()
        {
            bool result = NpmInstallDiagnostics.IsPermissionError("EPERM: operation not permitted");

            Assert.IsTrue(result);
        }

        [Test]
        public void IsPermissionError_WithEACCES_ReturnsTrue()
        {
            bool result = NpmInstallDiagnostics.IsPermissionError("EACCES: permission denied");

            Assert.IsTrue(result);
        }

        [Test]
        public void IsPermissionError_CaseInsensitiveOperationNotPermitted_ReturnsTrue()
        {
            bool result = NpmInstallDiagnostics.IsPermissionError("Operation Not Permitted");

            Assert.IsTrue(result);
        }

        [Test]
        public void IsPermissionError_NoPermissionPattern_ReturnsFalse()
        {
            bool result = NpmInstallDiagnostics.IsPermissionError("404 Not Found");

            Assert.IsFalse(result);
        }

        // --- IsGlobalPrefixWritable ---

        [Test]
        public void IsGlobalPrefixWritable_MalformedPath_ReturnsTrue()
        {
            // Malformed paths cause ArgumentException/NotSupportedException from File APIs;
            // indeterminate result should fall through (true) to let npm attempt the install
            bool result = NpmInstallDiagnostics.IsGlobalPrefixWritable("invalid<>path|with:illegal*chars");

            Assert.IsTrue(result);
        }

        // --- BuildInstallErrorMessage ---

        [Test]
        public void BuildInstallErrorMessage_ContainsBothGuidanceAndStderr()
        {
            string guidance = "npm does not have permission to write to the global directory.";
            string rawStderr = "npm ERR! code EPERM\nnpm ERR! syscall rename";

            string result = NpmInstallDiagnostics.BuildInstallErrorMessage(guidance, rawStderr);

            Assert.That(result, Does.Contain(guidance));
            Assert.That(result, Does.Contain(rawStderr));
        }

        [Test]
        public void BuildInstallErrorMessage_GuidanceAppearsBeforeStderr()
        {
            string guidance = "Permission denied guidance";
            string rawStderr = "npm ERR! EPERM";

            string result = NpmInstallDiagnostics.BuildInstallErrorMessage(guidance, rawStderr);

            int guidanceIndex = result.IndexOf(guidance, System.StringComparison.Ordinal);
            int stderrIndex = result.IndexOf(rawStderr, System.StringComparison.Ordinal);
            Assert.That(guidanceIndex, Is.LessThan(stderrIndex));
        }
    }
}
