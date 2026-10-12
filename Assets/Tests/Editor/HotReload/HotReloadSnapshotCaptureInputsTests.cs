using System.IO;

using NUnit.Framework;

using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// Covers the values the snapshot capture reads from Unity on the main thread before it starts.
    /// </summary>
    public sealed class HotReloadSnapshotCaptureInputsTests
    {
        /// <summary>
        /// Verifies the inputs read on the main thread name this project's root and carry the
        /// registered packages.
        /// </summary>
        [Test]
        public void ReadOnMainThread_CarriesTheProjectRootAndThePackageRoots()
        {
            HotReloadSnapshotCaptureInputs inputs = HotReloadSnapshotCaptureInputs.ReadOnMainThread();

            Assert.That(inputs.ProjectRoot, Is.EqualTo(Path.GetFullPath(Path.Combine(Application.dataPath, ".."))));
            Assert.That(inputs.PackageRoots, Is.Not.Empty);
        }
    }
}
