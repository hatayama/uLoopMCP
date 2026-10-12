using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Everything the snapshot capture needs from Unity, read once on the main thread and held as
    /// values, so that the capture itself asks Unity nothing.
    /// </summary>
    internal sealed class HotReloadSnapshotCaptureInputs
    {
        internal HotReloadSnapshotCaptureInputs(
            string projectRoot,
            HotReloadCompileStart compileStart,
            IReadOnlyList<HotReloadSnapshotPackageRoot> packageRoots,
            StringComparison pathComparison)
        {
            Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty.");
            Debug.Assert(packageRoots != null, "packageRoots must not be null.");
            Debug.Assert(
                pathComparison == StringComparison.Ordinal || pathComparison == StringComparison.OrdinalIgnoreCase,
                "pathComparison must be Ordinal or OrdinalIgnoreCase.");

            ProjectRoot = projectRoot;
            CompileStart = compileStart;
            PackageRoots = packageRoots;
            PathComparison = pathComparison;
        }

        /// <summary>The root of this Editor's project, which owns the snapshots.</summary>
        internal string ProjectRoot { get; }

        /// <summary>When the compile behind the current assemblies started, as far as this session knows.</summary>
        internal HotReloadCompileStart CompileStart { get; }

        /// <summary>The registered packages, with whether hot reload treats each as read-only.</summary>
        internal IReadOnlyList<HotReloadSnapshotPackageRoot> PackageRoots { get; }

        /// <summary>How paths compare on this platform: case-insensitive on Windows only.</summary>
        internal StringComparison PathComparison { get; }

        /// <summary>
        /// Reads the inputs from Unity. Main thread only: the compile start lives in SessionState and
        /// the package roots come from the Package Manager, both of which Unity allows on the main
        /// thread alone.
        /// </summary>
        internal static HotReloadSnapshotCaptureInputs ReadOnMainThread()
        {
            return new HotReloadSnapshotCaptureInputs(
                Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                HotReloadCompileStartRecord.Read(),
                HotReloadSnapshotPackageRoots.ReadCurrent(),
                CurrentPathComparison());
        }

        /// <summary>
        /// Returns the comparison the package paths use on this platform, the same one
        /// ScriptPackageRoots.ToPhysicalPath uses: case-insensitive on Windows only.
        /// </summary>
        internal static StringComparison CurrentPathComparison()
        {
            return Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        }
    }
}
