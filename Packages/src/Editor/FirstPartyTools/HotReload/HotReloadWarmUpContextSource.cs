using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Chooses what the warm-up loads: the most recent assemblies in the project's ledger whose
    /// compiled dll and PDB exist, with the dlls that reference each of them. The referencing dlls
    /// are read only from a kept compilation assembly list; without one each target has none.
    /// </summary>
    internal sealed class HotReloadWarmUpContextSource : IHotReloadWarmUpContextSource
    {
        // Why only the most recent few: the assembly edited last is the one most likely edited
        // next, and each target adds a full read of its dll to the warm-up.
        internal const int MaxWarmedTargets = 8;

        private readonly string _projectRoot;
        private readonly IHotReloadEditorStateSnapshotCapture _editorState;
        private readonly Func<string, IReadOnlyList<string>> _collectReferencingDllPaths;
        private readonly Func<bool> _isCompilationListKept;

        internal HotReloadWarmUpContextSource(
            string projectRoot,
            IHotReloadEditorStateSnapshotCapture editorState,
            Func<string, IReadOnlyList<string>> collectReferencingDllPaths,
            Func<bool> isCompilationListKept)
        {
            Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty.");
            Debug.Assert(editorState != null, "editorState must not be null.");
            Debug.Assert(collectReferencingDllPaths != null, "collectReferencingDllPaths must not be null.");
            Debug.Assert(isCompilationListKept != null, "isCompilationListKept must not be null.");
            _projectRoot = projectRoot;
            _editorState = editorState;
            _collectReferencingDllPaths = collectReferencingDllPaths;
            _isCompilationListKept = isCompilationListKept;
        }

        /// <summary>
        /// Main thread only. Skips while the Editor compiles or imports: the dlls are being
        /// rewritten then, and the compilation pipeline lists no assembly. A failed last compile
        /// does not skip: the loaded dlls are the last good build, which a run reads too.
        /// </summary>
        public HotReloadWarmUpCapture Capture()
        {
            HotReloadEditorStateSnapshot state = _editorState.CaptureCurrent();
            if (state.IsCompiling)
            {
                return HotReloadWarmUpCapture.Skipped(HotReloadWarmUpCapture.SkipReasonCompiling);
            }

            if (state.IsUpdating)
            {
                return HotReloadWarmUpCapture.Skipped(HotReloadWarmUpCapture.SkipReasonUpdating);
            }

            IReadOnlyList<string> names = HotReloadWarmUpTargetLedger.Read(_projectRoot);
            if (names.Count == 0)
            {
                return HotReloadWarmUpCapture.Skipped(HotReloadWarmUpCapture.SkipReasonNoTargets);
            }

            // Read once: the items decide from the same answer the referencing dlls came from.
            bool compilationListKept = _isCompilationListKept();
            List<HotReloadWarmUpTarget> targets = CollectTargets(names, compilationListKept);
            if (targets.Count == 0)
            {
                return HotReloadWarmUpCapture.Skipped(HotReloadWarmUpCapture.SkipReasonNoCompiledAssembly);
            }

            return HotReloadWarmUpCapture.Ready(new HotReloadWarmUpContext(_projectRoot, targets, compilationListKept));
        }

        private List<HotReloadWarmUpTarget> CollectTargets(IReadOnlyList<string> names, bool compilationListKept)
        {
            CompiledAssemblyLayout layout = CompiledAssemblyLayout.Resolve(_projectRoot);
            List<HotReloadWarmUpTarget> targets = new List<HotReloadWarmUpTarget>();
            int count = Math.Min(names.Count, MaxWarmedTargets);
            for (int index = 0; index < count; index++)
            {
                string name = names[index];
                string dllPath = layout.DllPath(name);
                string pdbPath = Path.ChangeExtension(dllPath, ".pdb");
                // Why skip rather than fail: an assembly renamed or removed since a run recorded it
                // has nothing to load, and the ledger keeps the name harmlessly.
                if (!File.Exists(dllPath) || !File.Exists(pdbPath))
                {
                    continue;
                }

                // Why not collected without a kept list: the referencing assemblies come from the
                // list, and asking Unity for it here would stop the main thread.
                IReadOnlyList<string> referencingDllPaths = compilationListKept
                    ? _collectReferencingDllPaths(name)
                    : Array.Empty<string>();
                targets.Add(new HotReloadWarmUpTarget(name, dllPath, pdbPath, referencingDllPaths));
            }

            return targets;
        }
    }
}
