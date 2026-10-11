using System.Collections.Generic;
using System.IO;
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
    /// The startup snapshot capture is the first caller after a domain reload, so runs find the list
    /// already kept.
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

        static HotReloadCompilationAssemblies()
        {
            // Why: a compile that fails keeps the domain alive, so without this the list would keep
            // the asmdefs and source files from before that compile until the next reload.
            CompilationPipeline.compilationStarted += OnCompilationStarted;
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
            Cache.Invalidate();
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
