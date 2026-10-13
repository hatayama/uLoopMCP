using System;
using System.Collections.Generic;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Takes the per-file snapshot a group compares its apply against.
    /// </summary>
    internal static class HotReloadGroupStateSnapshot
    {
        /// <summary>
        /// Records on each file of the group what the domain holds for it before the group
        /// applies: active labels, added methods, forwarded Unity messages, and the verified
        /// source baseline (or why there is none). Call on the Unity main thread.
        /// </summary>
        internal static void Capture(
            HotReloadDomain domain,
            IHotReloadPlayModeQuery playMode,
            IReadOnlyList<HotReloadGroupFile> files)
        {
            foreach (HotReloadGroupFile file in files)
            {
                // Why snapshot at the group's apply entry: runs process groups sequentially, and
                // RevertUnchangedPatches / BeginFileGeneration mutate ledgers after the worker.
                // The worker itself does not.
                file.SnapshotLabels = HotReloadAppliedSourceLifecycle.CollectActiveLabelsForFile(
                    domain,
                    file.ProjectRelativePath);
                file.SnapshotAddedLabels = new HashSet<string>(
                    domain.ListActiveAddedMethodKeys(file.ProjectRelativePath),
                    StringComparer.Ordinal);
                file.SnapshotForwardedUnityMessageLabels = playMode.IsPlaying
                    ? HotReloadDeactivatedUnityMessageNote.CollectForwardedLabels(
                        domain.DescribeAddedMembersOfFile(file.ProjectRelativePath))
                    : new HashSet<string>(StringComparer.Ordinal);
                // Why projectRelativePath (not workerSourcePath): contentPathOverride E2E copies
                // live under Library/UloopHotReload/TestSources/ and are absent from the PDB
                // document list. Assembly resolution already computed the on-disk path.
                file.SnapshotSource = HotReloadSourceBaseline.LoadVerifiedSnapshotSource(
                    file.ProjectRelativePath,
                    file.TargetDllPath);
                file.SnapshotMissReason = file.SnapshotSource == null
                    ? HotReloadSourceBaseline.DescribeSnapshotMiss(
                        file.ProjectRelativePath,
                        file.TargetDllPath,
                        file.NewSourceMembershipEvidence)
                    : HotReloadSnapshotMissReason.None;
            }
        }
    }
}
