using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// One assembly of a rebuilt compilation assembly list, before the Unity objects are made.
    /// Project references are named, precompiled ones are paths as Unity lists them.
    /// </summary>
    internal sealed class HotReloadRebuiltAssemblyEntry
    {
        internal HotReloadRebuiltAssemblyEntry(
            string name,
            string outputPath,
            string[] sourceFiles,
            string[] defines,
            List<string> projectReferences,
            List<string> compiledReferences)
        {
            Name = name;
            OutputPath = outputPath;
            SourceFiles = sourceFiles;
            Defines = defines;
            ProjectReferences = projectReferences;
            CompiledReferences = compiledReferences;
        }

        internal string Name { get; }

        internal string OutputPath { get; }

        internal string[] SourceFiles { get; }

        internal string[] Defines { get; }

        internal List<string> ProjectReferences { get; }

        internal List<string> CompiledReferences { get; }
    }

    /// <summary>
    /// Makes the entries of a rebuilt list from one saved list: as saved for an assembly whose dll
    /// did not change, or from the PDB of one that was compiled again. A PDB-made entry keeps the
    /// saved order of every element it still has and appends new ones in the PDB's order, because
    /// the PDB lists them in the compiler's order, which is not Unity's. Touches only files, so it
    /// can run on a pool thread.
    /// </summary>
    internal sealed class HotReloadCompilationAssemblyRebuilder
    {
        private const string ReferenceAssemblySuffix = ".ref.dll";
        private const string AssemblySuffix = ".dll";

        private readonly HotReloadSavedCompilationList _saved;
        private readonly HotReloadSnapshotCaptureInputs _inputs;
        private readonly HashSet<string> _savedNames = new HashSet<string>(StringComparer.Ordinal);
        private readonly HotReloadSavedReferenceResolver _resolver;
        private readonly StringComparer _pathComparer;

        internal HotReloadCompilationAssemblyRebuilder(HotReloadSavedCompilationList saved, HotReloadSnapshotCaptureInputs inputs)
        {
            Debug.Assert(saved != null && saved.IsComplete(), "saved must be a complete list.");
            Debug.Assert(inputs != null, "inputs must not be null.");

            _saved = saved;
            _inputs = inputs;
            foreach (HotReloadSavedCompilationAssembly assembly in saved.assemblies)
            {
                _savedNames.Add(assembly.name);
            }

            _pathComparer = inputs.PathComparison == StringComparison.OrdinalIgnoreCase
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            _resolver = new HotReloadSavedReferenceResolver(saved.references, inputs.ProjectRoot, _pathComparer);
        }

        internal HotReloadRebuiltAssemblyEntry FromSaved(HotReloadSavedCompilationAssembly assembly)
        {
            List<string> compiledReferences = new List<string>(assembly.compiledAssemblyReferenceIndexes.Length);
            foreach (int index in assembly.compiledAssemblyReferenceIndexes)
            {
                compiledReferences.Add(_saved.references[index].path);
            }

            return new HotReloadRebuiltAssemblyEntry(
                assembly.name,
                assembly.outputPath,
                assembly.sourceFiles,
                assembly.defines,
                new List<string>(assembly.assemblyReferences),
                compiledReferences);
        }

        /// <summary>
        /// Makes the entry of a compiled-again assembly from its PDB. Returns null and the entry, or
        /// the reason no entry can be trusted: a PDB of another build, sources that cannot be shown
        /// to be every source the compiler was given, or a reference that does not resolve to a file
        /// of the recorded MVID. A PDB that a rewrite left without some documents still gives an
        /// entry when the compiler's own PDB of the same build under Library/Bee lists every source.
        /// </summary>
        internal string TryFromPdb(
            HotReloadSavedCompilationAssembly assembly,
            HotReloadCaptureAssemblyCandidate candidate,
            out HotReloadRebuiltAssemblyEntry entry)
        {
            Debug.Assert(assembly != null, "assembly must not be null.");
            Debug.Assert(candidate != null, "candidate must not be null.");

            entry = null;
            HotReloadPdbCompilationInfo info = HotReloadPdbCompilationInfo.Read(candidate.PdbPath);
            // Why checked before anything is read from it: a compile that is replacing the files can
            // leave a PDB of another build beside the dll.
            if (info.PdbGuid != HotReloadPdbDocumentIndex.ReadCodeViewGuid(candidate.DllPath))
            {
                return HotReloadConstants.CompilationListReasonPdbMismatch;
            }

            // Why the capture's reader decides: it alone judges whether the sources are every
            // source the compiler was given, reading the compiler's own PDB when a rewrite after the
            // compile (an IL post-processor does this) dropped the documents no sequence point refers
            // to. A PDB without the compiler's count cannot show it is whole, and one with extra
            // #line documents is refused as well. Read before the references so this reason still
            // comes before theirs.
            HotReloadCaptureAssembly compiled = HotReloadCaptureAssemblyEnumerator.ReadSources(candidate, _inputs);
            if (!compiled.HasEverySource)
            {
                return HotReloadConstants.CompilationListReasonPdbDocumentsIncomplete;
            }

            List<string> projectReferences = new List<string>();
            Dictionary<string, string> compiledPathByFileName = new Dictionary<string, string>(_pathComparer);
            foreach (HotReloadPdbReferencedAssembly reference in info.References)
            {
                string referencedName = AssemblyNameOf(reference.FileName);
                if (_savedNames.Contains(referencedName))
                {
                    projectReferences.Add(referencedName);
                    continue;
                }

                string failure = _resolver.TryResolve(reference, out string path);
                if (failure != null)
                {
                    return failure;
                }

                compiledPathByFileName[reference.FileName] = path;
            }

            entry = new HotReloadRebuiltAssemblyEntry(
                assembly.name,
                assembly.outputPath,
                InSavedOrder(assembly.sourceFiles, compiled.SourceAssetPaths, _pathComparer).ToArray(),
                ToArray(info.Defines),
                InSavedOrder(assembly.assemblyReferences, projectReferences, StringComparer.Ordinal),
                CompiledReferencesInSavedOrder(assembly, compiledPathByFileName));
            return null;
        }

        // Unity lists a precompiled reference once per path, sometimes the same path twice and
        // sometimes two paths of one file name, while the PDB records one file name once. So every
        // saved path whose file name the PDB still records is kept as saved.
        private List<string> CompiledReferencesInSavedOrder(
            HotReloadSavedCompilationAssembly assembly,
            Dictionary<string, string> compiledPathByFileName)
        {
            List<string> ordered = new List<string>(compiledPathByFileName.Count);
            HashSet<string> savedFileNames = new HashSet<string>(_pathComparer);
            foreach (int index in assembly.compiledAssemblyReferenceIndexes)
            {
                string path = _saved.references[index].path;
                string fileName = Path.GetFileName(path);
                savedFileNames.Add(fileName);
                if (compiledPathByFileName.ContainsKey(fileName))
                {
                    ordered.Add(path);
                }
            }

            foreach (KeyValuePair<string, string> pair in compiledPathByFileName)
            {
                if (!savedFileNames.Contains(pair.Key))
                {
                    ordered.Add(pair.Value);
                }
            }

            return ordered;
        }

        private static List<string> InSavedOrder(string[] saved, IReadOnlyList<string> current, StringComparer comparer)
        {
            HashSet<string> currentSet = new HashSet<string>(current, comparer);
            HashSet<string> savedSet = new HashSet<string>(saved, comparer);
            List<string> ordered = new List<string>(current.Count);
            foreach (string element in saved)
            {
                if (currentSet.Contains(element))
                {
                    ordered.Add(element);
                }
            }

            foreach (string element in current)
            {
                if (!savedSet.Contains(element))
                {
                    ordered.Add(element);
                }
            }

            return ordered;
        }

        private static string[] ToArray(IReadOnlyList<string> values)
        {
            string[] array = new string[values.Count];
            for (int index = 0; index < array.Length; index++)
            {
                array[index] = values[index];
            }

            return array;
        }

        // A project assembly is referenced through its reference assembly (X.ref.dll), a
        // precompiled one through its dll.
        private static string AssemblyNameOf(string referenceFileName)
        {
            if (referenceFileName.EndsWith(ReferenceAssemblySuffix, StringComparison.OrdinalIgnoreCase))
            {
                return referenceFileName.Substring(0, referenceFileName.Length - ReferenceAssemblySuffix.Length);
            }

            if (referenceFileName.EndsWith(AssemblySuffix, StringComparison.OrdinalIgnoreCase))
            {
                return referenceFileName.Substring(0, referenceFileName.Length - AssemblySuffix.Length);
            }

            return referenceFileName;
        }
    }

    /// <summary>
    /// Finds the saved reference path for a precompiled reference a PDB names by file name and
    /// MVID, reading each candidate file's MVID at most once.
    /// </summary>
    internal sealed class HotReloadSavedReferenceResolver
    {
        private readonly List<HotReloadSavedReference> _references;
        private readonly string _projectRoot;
        private readonly Dictionary<string, List<int>> _indexesByFileName;
        private readonly Dictionary<int, Guid?> _currentMvidByIndex = new Dictionary<int, Guid?>();

        internal HotReloadSavedReferenceResolver(List<HotReloadSavedReference> references, string projectRoot, StringComparer pathComparer)
        {
            _references = references;
            _projectRoot = projectRoot;
            _indexesByFileName = new Dictionary<string, List<int>>(pathComparer);
            for (int index = 0; index < references.Count; index++)
            {
                string fileName = Path.GetFileName(references[index].path);
                if (!_indexesByFileName.TryGetValue(fileName, out List<int> indexes))
                {
                    indexes = new List<int>();
                    _indexesByFileName.Add(fileName, indexes);
                }

                indexes.Add(index);
            }
        }

        /// <summary>
        /// Returns null and the saved path when a saved path with this file name holds a dll of
        /// this MVID now; otherwise the reason there is none.
        /// </summary>
        internal string TryResolve(HotReloadPdbReferencedAssembly reference, out string path)
        {
            path = null;
            if (!_indexesByFileName.TryGetValue(reference.FileName, out List<int> indexes))
            {
                return HotReloadConstants.CompilationListReasonUnresolvedReference;
            }

            bool anyExists = false;
            foreach (int index in indexes)
            {
                Guid? mvid = CurrentMvid(index);
                if (mvid == null)
                {
                    continue;
                }

                anyExists = true;
                if (mvid.Value == reference.Mvid)
                {
                    path = _references[index].path;
                    return null;
                }
            }

            return anyExists
                ? HotReloadConstants.CompilationListReasonReferenceChanged
                : HotReloadConstants.CompilationListReasonReferenceMissing;
        }

        // Null when the file is gone. Why the saved MVID is trusted for an unchanged stamp:
        // reading every reference's metadata again would cost what the saved list saves.
        private Guid? CurrentMvid(int index)
        {
            if (_currentMvidByIndex.TryGetValue(index, out Guid? known))
            {
                return known;
            }

            HotReloadSavedReference saved = _references[index];
            string fullPath = Path.GetFullPath(Path.Combine(_projectRoot, saved.path));
            HotReloadSavedFileStamp stamp = HotReloadSavedFileStamp.ReadOrNull(fullPath);
            Guid? mvid = null;
            if (stamp != null)
            {
                mvid = stamp.SameAs(saved.stamp)
                    ? Guid.ParseExact(saved.mvid, "N")
                    : HotReloadAssemblyMetadataMvid.Read(fullPath);
            }

            _currentMvidByIndex.Add(index, mvid);
            return mvid;
        }
    }
}
