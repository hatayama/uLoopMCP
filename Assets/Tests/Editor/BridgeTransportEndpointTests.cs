using System.IO;
using NUnit.Framework;

namespace io.github.hatayama.UnityCliLoop
{
    public class BridgeTransportEndpointTests
    {
        [Test]
        public void CanonicalizeProjectRoot_WhenPathIsFilesystemRoot_ShouldPreserveRoot()
        {
            string filesystemRoot = Path.GetPathRoot(Directory.GetCurrentDirectory());

            // Tests that root path canonicalization keeps the filesystem root stable.
            string canonicalProjectRoot = ProjectRootCanonicalizer.Canonicalize(filesystemRoot);

            Assert.That(canonicalProjectRoot, Is.EqualTo(filesystemRoot));
        }
    }
}
