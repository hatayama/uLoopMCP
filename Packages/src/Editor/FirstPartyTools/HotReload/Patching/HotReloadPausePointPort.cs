using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Answers the pause-point tool's questions about one hot-reload domain. The composition root
    /// publishes an instance of this for the domain it installs, so an answer can never come from
    /// a domain that is no longer installed.
    /// </summary>
    internal sealed class HotReloadPausePointPort : IHotReloadPausePointPort
    {
        private readonly HotReloadDomain _domain;
        private readonly Func<string, string> _readSourceContentHashOrNull;
        private readonly Action _ensureSnapshotCaptured;
        private readonly Func<bool> _isCaptureInFlight;

        // Why the hash reader is passed in: the hasher lives in the apply pipeline's assembly,
        // which this one may not reference, and the check must hash the way the worker did.
        // Why the capture is passed in: the gate that captures the source snapshot belongs to the
        // services the composition root installs, which this assembly does not reference, and the
        // in-flight check is passed in for the same reason.
        public HotReloadPausePointPort(
            HotReloadDomain domain,
            Func<string, string> readSourceContentHashOrNull,
            Action ensureSnapshotCaptured,
            Func<bool> isCaptureInFlight)
        {
            Debug.Assert(ensureSnapshotCaptured != null, "ensureSnapshotCaptured must not be null.");
            Debug.Assert(isCaptureInFlight != null, "isCaptureInFlight must not be null.");

            _domain = domain;
            _readSourceContentHashOrNull = readSourceContentHashOrNull;
            _ensureSnapshotCaptured = ensureSnapshotCaptured;
            _isCaptureInFlight = isCaptureInFlight;
        }

        public MethodBase GetActiveShimForMethod(MethodBase method)
        {
            return _domain.FindGenerationForMethod(method)?.FindPatchShim(method);
        }

        public HotReloadShimFileLookup GetShimLookupForFile(string file)
        {
            return _domain.LookupShimsForFile(file);
        }

        public HotReloadAddedMethodAtLine FindAddedMethodContainingLine(string file, int line)
        {
            return _domain.FindAddedMethodContainingLine(file, line);
        }

        // Why the domain picks the path to read: it is the path the transform worker hashed, which
        // differs from the argument when a reload was run from an edited copy.
        public bool HasShimSourceChangedOnDisk(string file)
        {
            return _domain.HasShimSourceChangedOnDisk(file, _readSourceContentHashOrNull);
        }

        public string GetVerifiedSnapshotSource(string projectRelativeFile, string dllPath)
        {
            if (string.IsNullOrEmpty(projectRelativeFile) || string.IsNullOrEmpty(dllPath))
            {
                return null;
            }

            // Why the live file is tried while a capture runs: a pause point re-armed on the first
            // update tick after a reload would otherwise wait for the whole capture. A live file the
            // PDB confirms holds the bytes the compiler read, which is what the snapshot will hold.
            // Why only while a capture runs: otherwise the snapshot is ready or about to be captured
            // here, and reading it keeps the answer unchanged from before the capture moved off the
            // main thread.
            if (_isCaptureInFlight())
            {
                string liveSource = HotReloadSourceBaseline.LoadVerifiedLiveSourceOrNull(projectRelativeFile, dllPath);
                if (liveSource != null)
                {
                    return liveSource;
                }
            }

            // Why: with both tools disabled at domain load nothing was captured, and a pause point
            // is the first reader once the pause-point tool is enabled.
            _ensureSnapshotCaptured();
            return HotReloadSourceBaseline.LoadVerifiedSnapshotSource(projectRelativeFile, dllPath);
        }

        public IReadOnlyList<LocalBuilder> GetTransplantLocals(MethodBase method)
        {
            return _domain.FindGenerationForMethod(method)?.FindTransplantLocals(method);
        }

        public int GetTransplantPreambleLength(MethodBase method)
        {
            return _domain.FindGenerationForMethod(method)?.FindTransplantPreambleLength(method) ?? 0;
        }

        public IReadOnlyList<string> GetAddedFieldsForType(string typeFullName)
        {
            return _domain.GetAddedFieldsForType(typeFullName);
        }

        // Why the same path matching as the shim lookup: the pause point tool passes whatever
        // path the caller typed, and a descriptor only ever records the project-relative one.
        public bool IsIntroducedTypeSourceFile(string file)
        {
            if (string.IsNullOrEmpty(file))
            {
                return false;
            }

            IReadOnlyList<HotReloadIntroducedTypeDescriptor> descriptors =
                _domain.IntroducedTypes.DescribeActive();
            for (int index = 0; index < descriptors.Count; index++)
            {
                string ownerProjectRelativePath = descriptors[index].OwnerProjectRelativePath;
                if (string.IsNullOrEmpty(ownerProjectRelativePath))
                {
                    continue;
                }

                if (HotReloadSourcePathNormalizer.PathsReferToSameFile(file, ownerProjectRelativePath))
                {
                    return true;
                }
            }

            return false;
        }

        // Why the recorded path is hashed rather than the argument: it is the copy the transform
        // worker read, which differs from the argument when a reload was run from an edited copy.
        // Why an unreadable file counts as unchanged: the shim-source check treats it the same way,
        // so the two answers never disagree about one file.
        public HotReloadLatestFileReload GetLatestReloadOfFile(string file)
        {
            HotReloadAppliedSourceRecord record = _domain.AppliedSources.FindRecordForRequestedPath(file);
            if (record == null)
            {
                return null;
            }

            string currentHash = _readSourceContentHashOrNull(record.SourcePath);
            bool fileChangedSince = currentHash != null
                && !string.Equals(currentHash, record.Hash, StringComparison.Ordinal);
            return new HotReloadLatestFileReload(fileChangedSince, record.UnappliedRows);
        }

        // Why the label match is exact: the label built from the MethodBase spells its parameter
        // types the way the worker row does (List`1<System.Int32>), so a method whose row is
        // absent finds none rather than a guessed one.
        public HotReloadUnappliedRow FindUnappliedRowForMethod(string file, MethodBase method)
        {
            if (method == null || method.DeclaringType == null)
            {
                return null;
            }

            HotReloadLatestFileReload latest = GetLatestReloadOfFile(file);
            if (latest == null || latest.FileChangedSince)
            {
                return null;
            }

            string label = HotReloadMethodKeys.FormatMethodLabel(method);
            foreach (HotReloadUnappliedRow row in latest.UnappliedRows)
            {
                if (string.Equals(row.Label, label, StringComparison.Ordinal))
                {
                    return row;
                }
            }

            return null;
        }
    }
}
