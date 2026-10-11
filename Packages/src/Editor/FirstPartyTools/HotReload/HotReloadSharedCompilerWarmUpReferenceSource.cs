using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;

using io.github.hatayama.UnityCliLoop.ToolContracts;

using UnityCompilationAssembly = UnityEditor.Compilation.Assembly;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Lists the compile references the next shim compile would bind as they are, for the most
    /// recently edited assembly that is compiled, so the shared compiler worker can load them
    /// before the first run after a server reset.
    /// </summary>
    internal sealed class HotReloadSharedCompilerWarmUpReferenceSource
    {
        private readonly string _projectRoot;
        private readonly Func<string, IReadOnlyList<string>> _readLedger;
        private readonly Func<string, UnityCompilationAssembly> _findCompilationAssembly;
        private readonly Task _compilationListReady;
        private readonly Func<bool> _isCompilationListKept;

        internal HotReloadSharedCompilerWarmUpReferenceSource(
            string projectRoot,
            Func<string, IReadOnlyList<string>> readLedger,
            Func<string, UnityCompilationAssembly> findCompilationAssembly,
            Task compilationListReady,
            Func<bool> isCompilationListKept)
        {
            Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be empty.");
            Debug.Assert(readLedger != null, "readLedger must not be null.");
            Debug.Assert(findCompilationAssembly != null, "findCompilationAssembly must not be null.");
            Debug.Assert(compilationListReady != null, "compilationListReady must not be null.");
            Debug.Assert(isCompilationListKept != null, "isCompilationListKept must not be null.");
            _projectRoot = projectRoot;
            _readLedger = readLedger;
            _findCompilationAssembly = findCompilationAssembly;
            _compilationListReady = compilationListReady;
            _isCompilationListKept = isCompilationListKept;
        }

        /// <summary>
        /// Any thread. Waits until the domain's attempt to rebuild the compilation assembly list has
        /// ended. Empty when no list is kept then, or when the ledger names no assembly whose dll
        /// exists and whose compilation assembly the list holds.
        /// </summary>
        internal async Task<IReadOnlyList<string>> CollectAsync(CancellationToken ct)
        {
            // Why wait: this runs at the server start right after a domain load, before the rebuild
            // has usually ended, and returning empty then would leave the worker cold.
            // Why ConfigureAwait(false) and then a switch: a cancelled rebuild ends on a pool thread.
            await _compilationListReady.ConfigureAwait(false);
            // Why the main thread: the kept list is read only there.
            await MainThreadSwitcher.SwitchToMainThread(ct);
            // Why not ask Unity instead: GetAssemblies stops the main thread for hundreds of
            // milliseconds on a large project, more than this warm-up saves.
            if (!_isCompilationListKept())
            {
                return Array.Empty<string>();
            }

            IReadOnlyList<string> names = _readLedger(_projectRoot);
            CompiledAssemblyLayout layout = CompiledAssemblyLayout.Resolve(_projectRoot);
            int count = Math.Min(names.Count, HotReloadWarmUpContextSource.MaxWarmedTargets);
            for (int index = 0; index < count; index++)
            {
                string name = names[index];
                string fullTargetDllPath = Path.GetFullPath(layout.DllPath(name));
                // Renamed or removed since a run recorded it.
                if (!File.Exists(fullTargetDllPath))
                {
                    continue;
                }

                UnityCompilationAssembly assembly = _findCompilationAssembly(name);
                if (assembly == null || assembly.allReferences == null)
                {
                    continue;
                }

                // Read here: the pool thread must not touch the Unity object.
                string[] allReferences = assembly.allReferences;
                string scriptAssembliesDirectory = layout.CompiledAssembliesDirectory;
                // Why only the first usable name: the references bound as they are hardly differ
                // between assemblies, and the worker keeps every one it loads for its lifetime.
                // Why off the main thread: one File.Exists per reference, several hundred on a
                // large project.
                return await Task.Run(
                        () => ListReferencesBoundAsTheyAre(allReferences, fullTargetDllPath, scriptAssembliesDirectory),
                        ct)
                    .ConfigureAwait(false);
            }

            return Array.Empty<string>();
        }

        private IReadOnlyList<string> ListReferencesBoundAsTheyAre(
            string[] allReferences,
            string fullTargetDllPath,
            string scriptAssembliesDirectory)
        {
            List<string> paths = new List<string>();
            foreach (ShimCompileReference entry in HotReloadShimReferenceBuilder.ClassifyCompileReferences(
                         allReferences,
                         _projectRoot,
                         fullTargetDllPath,
                         scriptAssembliesDirectory))
            {
                if (!entry.UsesRewrittenCopy)
                {
                    paths.Add(entry.FullPath);
                }
            }

            return paths;
        }
    }
}
