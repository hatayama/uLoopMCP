using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using UnityEditor;
using UnityEditor.Compilation;

using UnityEngine;

using UnityCompilationAssembly = UnityEditor.Compilation.Assembly;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// The compilation assembly list for hot reload, asked of Unity once per domain.
    /// Why: on a project with several hundred assemblies one CompilationPipeline.GetAssemblies() call
    /// takes 340-620 ms, while the list only changes through a compile, or through an import that
    /// changes the script set without compiling (see HotReloadCompilationAssemblyListPostprocessor).
    /// Whichever caller first needs the list after a domain reload asks Unity for it; later callers
    /// in the domain find it kept.
    /// A list rebuilt at startup from the saved list holds only the fields hot reload reads: name,
    /// outputPath, sourceFiles, defines, assemblyReferences and compiledAssemblyReferences.
    /// compilerOptions, flags and rootNamespace keep their defaults, so code that needs them reads
    /// CompilationPipeline.GetAssemblies instead.
    /// </summary>
    internal static class HotReloadCompilationAssemblies
    {
        private static readonly HotReloadCompilationAssemblyListStore Store =
            new HotReloadCompilationAssemblyListStore(
                Path.Combine(ProjectRoot(), HotReloadConstants.CompilationAssemblyListRelativePath),
                () => EditorUtility.scriptCompilationFailed,
                () => Cache.Generation,
                work => Task.Run(work));

        private static readonly HotReloadCompilationAssemblyCache Cache =
            new HotReloadCompilationAssemblyCache(FetchAndSaveIfClean);

        // Main thread only. Cancelled when the files it reads are about to change or the domain ends.
        private static CancellationTokenSource RefillCancellation;

        static HotReloadCompilationAssemblies()
        {
            // Why: a compile that fails keeps the domain alive, so without this the list would keep
            // the asmdefs and source files from before that compile until the next reload.
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            AssemblyReloadEvents.beforeAssemblyReload += CancelRefill;
        }

        /// <summary>
        /// Does nothing but make sure the static constructor has run, so the subscription to
        /// compilationStarted is made on the main thread when the domain loads rather than by
        /// whichever caller first asks for the list.
        /// </summary>
        internal static void EnsureInitialized()
        {
        }

        /// <summary>
        /// Returns the compilation assemblies. The list is shared; callers only read it.
        /// </summary>
        internal static IReadOnlyList<UnityCompilationAssembly> Current()
        {
            return Cache.Current();
        }

        /// <summary>
        /// Returns the first compilation assembly with this name, or null when none has it.
        /// </summary>
        internal static UnityCompilationAssembly FindByName(string assemblyName)
        {
            return Cache.FindByName(assemblyName);
        }

        /// <summary>
        /// Starts rebuilding the list from the one a previous domain saved and the compiled files,
        /// off the main thread, and keeps it unless a caller asked Unity first or the script set
        /// changed meanwhile. Completes with whether the list was kept; never faults. Main thread only.
        /// </summary>
        internal static Task<bool> RefillInBackground()
        {
            RefillCancellation?.Cancel();
            RefillCancellation = new CancellationTokenSource();
            HotReloadCompilationListRefill refill = new HotReloadCompilationListRefill(
                Store,
                Cache,
                HotReloadSnapshotCaptureInputs.ReadOnMainThread,
                rebuild => Task.Run(rebuild));
            return refill.RunAsync(RefillCancellation.Token);
        }

        // Main thread: the cache asks for the list from Current().
        private static UnityCompilationAssembly[] FetchAndSaveIfClean()
        {
            UnityCompilationAssembly[] fetched = CompilationPipeline.GetAssemblies();
            if (fetched.Length > 0)
            {
                Store.SaveInBackgroundIfClean(fetched, Cache.Generation, ProjectRoot());
            }

            return fetched;
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static void OnCompilationStarted(object context)
        {
            CancelRefill();
            Cache.Invalidate();
        }

        // Why the rebuild stops here: a compile rewrites the dlls and PDBs it reads, and a reload
        // ends the domain whose cache it would fill.
        private static void CancelRefill()
        {
            RefillCancellation?.Cancel();
        }

        /// <summary>
        /// Drops the kept list because an import changed the script set without a domain reload.
        /// </summary>
        internal static void DropForScriptSetChange()
        {
            Cache.Invalidate();
        }
    }
}
