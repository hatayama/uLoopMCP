using System.IO;
using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.Application;
using io.github.hatayama.UnityCliLoop.CompositionRoot;
using io.github.hatayama.UnityCliLoop.Domain;
using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.Infrastructure;
using io.github.hatayama.UnityCliLoop.Presentation;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
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
