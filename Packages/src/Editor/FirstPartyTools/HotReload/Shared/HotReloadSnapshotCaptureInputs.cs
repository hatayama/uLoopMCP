using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

using UnityCompilationAssembly = UnityEditor.Compilation.Assembly;

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
            StringComparison pathComparison,
            IReadOnlyList<HotReloadCaptureAssembly> assemblies,
            long assemblyListingMs)
        {
            Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty.");
            Debug.Assert(packageRoots != null, "packageRoots must not be null.");
            Debug.Assert(
                pathComparison == StringComparison.Ordinal || pathComparison == StringComparison.OrdinalIgnoreCase,
                "pathComparison must be Ordinal or OrdinalIgnoreCase.");
            Debug.Assert(assemblies != null, "assemblies must not be null.");

            ProjectRoot = projectRoot;
            CompileStart = compileStart;
            PackageRoots = packageRoots;
            PathComparison = pathComparison;
            Assemblies = assemblies;
            AssemblyListingMs = assemblyListingMs;
        }

        /// <summary>The root of this Editor's project, which owns the snapshots.</summary>
        internal string ProjectRoot { get; }

        /// <summary>When the compile behind the current assemblies started, as far as this session knows.</summary>
        internal HotReloadCompileStart CompileStart { get; }

        /// <summary>The registered packages, with whether hot reload treats each as read-only.</summary>
        internal IReadOnlyList<HotReloadSnapshotPackageRoot> PackageRoots { get; }

        /// <summary>How paths compare on this platform: case-insensitive on Windows only.</summary>
        internal StringComparison PathComparison { get; }

        /// <summary>The compiled assemblies to capture.</summary>
        internal IReadOnlyList<HotReloadCaptureAssembly> Assemblies { get; }

        /// <summary>How long listing the assemblies took, in milliseconds.</summary>
        internal long AssemblyListingMs { get; }

        /// <summary>
        /// Reads the inputs from Unity. Main thread only: the compile start lives in SessionState, the
        /// package roots come from the Package Manager, and the assemblies from
        /// CompilationPipeline.GetAssemblies, all of which Unity allows on the main thread alone.
        /// </summary>
        internal static HotReloadSnapshotCaptureInputs ReadOnMainThread()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            HotReloadCompileStart compileStart = HotReloadCompileStartRecord.Read();
            IReadOnlyList<HotReloadSnapshotPackageRoot> packageRoots = HotReloadSnapshotPackageRoots.ReadCurrent();
            StringComparison pathComparison = CurrentPathComparison();
            System.Diagnostics.Stopwatch listingWatch = System.Diagnostics.Stopwatch.StartNew();
            IReadOnlyList<UnityCompilationAssembly> compilationAssemblies = HotReloadCompilationAssemblies.Current();
            listingWatch.Stop();
            List<HotReloadCaptureAssembly> assemblies = new List<HotReloadCaptureAssembly>(compilationAssemblies.Count);
            foreach (UnityCompilationAssembly compilationAssembly in compilationAssemblies)
            {
                assemblies.Add(ToCaptureAssembly(compilationAssembly, packageRoots, pathComparison));
            }

            return new HotReloadSnapshotCaptureInputs(
                projectRoot,
                compileStart,
                packageRoots,
                pathComparison,
                assemblies,
                listingWatch.ElapsedMilliseconds);
        }

        /// <summary>
        /// Returns the comparison the package paths use on this platform, the same one
        /// ScriptPackageRoots.ToPhysicalPath uses: case-insensitive on Windows only.
        /// </summary>
        internal static StringComparison CurrentPathComparison()
        {
            return Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        }

        /// <summary>
        /// Turns one of Unity's compilation assemblies into what the capture reads, classifying it by
        /// its first source because an asmdef keeps one assembly inside one package.
        /// </summary>
        internal static HotReloadCaptureAssembly ToCaptureAssembly(
            UnityCompilationAssembly compilationAssembly,
            IReadOnlyList<HotReloadSnapshotPackageRoot> packageRoots,
            StringComparison pathComparison)
        {
            Debug.Assert(compilationAssembly != null, "compilationAssembly must not be null.");

            string[] sourceFiles = compilationAssembly.sourceFiles ?? Array.Empty<string>();
            bool isImmutable = sourceFiles.Length > 0 && HotReloadPackageFolderMatch.IsInImmutablePackage(
                sourceFiles[0].Replace('\\', '/'),
                packageRoots,
                pathComparison);
            return new HotReloadCaptureAssembly(compilationAssembly.name, sourceFiles, isImmutable);
        }
    }
}
