using System.Collections.Generic;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// What one warm-up loads: the project root, at least one target assembly, and whether the
    /// compilation assembly list was kept when the targets were chosen.
    /// </summary>
    internal sealed class HotReloadWarmUpContext
    {
        internal HotReloadWarmUpContext(
            string projectRoot,
            IReadOnlyList<HotReloadWarmUpTarget> targets,
            bool compilationListKept)
        {
            Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty.");
            Debug.Assert(targets != null && targets.Count > 0, "targets must hold at least one target.");
            ProjectRoot = projectRoot;
            Targets = targets;
            CompilationListKept = compilationListKept;
        }

        internal string ProjectRoot { get; }

        internal IReadOnlyList<HotReloadWarmUpTarget> Targets { get; }

        /// <summary>
        /// False when no compilation assembly list was kept: the items that look assemblies up in
        /// it are then skipped, so the warm-up never asks Unity for the list on the main thread.
        /// </summary>
        internal bool CompilationListKept { get; }
    }
}
