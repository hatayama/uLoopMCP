using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// A compiled assembly found on disk with a PDB beside it, before its sources are read.
    /// </summary>
    internal sealed class HotReloadCaptureAssemblyCandidate
    {
        internal HotReloadCaptureAssemblyCandidate(string name, string dllPath, string pdbPath)
        {
            Debug.Assert(!string.IsNullOrEmpty(name), "name must not be null or empty.");
            Debug.Assert(!string.IsNullOrEmpty(dllPath), "dllPath must not be null or empty.");
            Debug.Assert(!string.IsNullOrEmpty(pdbPath), "pdbPath must not be null or empty.");

            Name = name;
            DllPath = dllPath;
            PdbPath = pdbPath;
        }

        internal string Name { get; }

        internal string DllPath { get; }

        internal string PdbPath { get; }
    }

    /// <summary>
    /// Lists the project's compiled assemblies from the dlls and PDBs on disk, and reads an
    /// assembly's sources from its PDB, instead of asking Unity's compilation pipeline, which only
    /// answers on the main thread. Asks Unity nothing, so it can run on any thread.
    /// </summary>
    internal static class HotReloadCaptureAssemblyEnumerator
    {
        private const string DllSearchPattern = "*.dll";

        /// <summary>
        /// Returns every compiled dll that has a PDB beside it, in ordinal order of the file name.
        /// A dll without a PDB is left out, because no snapshot can be checked against it. Returns
        /// an empty list when the compiled assemblies directory does not exist.
        /// </summary>
        internal static IReadOnlyList<HotReloadCaptureAssemblyCandidate> ListCompiled(string projectRoot)
        {
            Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty.");

            CompiledAssemblyLayout layout = CompiledAssemblyLayout.Resolve(projectRoot);
            string directory = layout.CompiledAssembliesDirectory;
            if (!Directory.Exists(directory))
            {
                return Array.Empty<HotReloadCaptureAssemblyCandidate>();
            }

            string[] dllPaths = Directory.GetFiles(directory, DllSearchPattern);
            Array.Sort(dllPaths, StringComparer.Ordinal);
            List<HotReloadCaptureAssemblyCandidate> candidates = new List<HotReloadCaptureAssemblyCandidate>(dllPaths.Length);
            foreach (string dllPath in dllPaths)
            {
                string name = Path.GetFileNameWithoutExtension(dllPath);
                string pdbPath = layout.PdbPath(name);
                if (!File.Exists(pdbPath))
                {
                    continue;
                }

                candidates.Add(new HotReloadCaptureAssemblyCandidate(name, dllPath, pdbPath));
            }

            return candidates;
        }

        /// <summary>
        /// Reads the sources of one compiled assembly from its PDB's document table, named by their
        /// asset paths, and whether it is a read-only package's. When the PDB beside the dll has
        /// fewer documents than the sources the compiler recorded, as after a tool rewrote it, the
        /// documents are read from the compiler's own PDB under Library/Bee if one of the same build
        /// lists every source. The result tells whether it has every source and whether it came from
        /// the compiler's PDB. Throws InvalidOperationException when the PDB beside the dll belongs to
        /// another build of it, as while a compile replaces them, or its compilation options record
        /// cannot be read.
        /// </summary>
        internal static HotReloadCaptureAssembly ReadSources(
            HotReloadCaptureAssemblyCandidate candidate,
            HotReloadSnapshotCaptureInputs inputs)
        {
            Debug.Assert(candidate != null, "candidate must not be null.");
            Debug.Assert(inputs != null, "inputs must not be null.");

            List<HotReloadPdbDocument> documents = HotReloadPdbDocumentTable.Read(
                candidate.DllPath, candidate.PdbPath, out int? sourceFileCount);
            bool hasEverySource = sourceFileCount == documents.Count;
            bool fromCompilerOutput = false;
            // Why the compiler's own output: a tool that rewrites the PDB drops the documents no
            // sequence point refers to but keeps the count, and the readers that compare a source
            // with its snapshot byte for byte take a file without a snapshot as unchanged, so a
            // file without a method body needs a snapshot too. A PDB without the count, or with more
            // documents than the count (#line), does not come here and keeps what it lists.
            if (sourceFileCount.HasValue && documents.Count < sourceFileCount.Value)
            {
                List<HotReloadPdbDocument> compiled = HotReloadCompilerOutputDocuments.TryReadEveryDocument(candidate.DllPath);
                if (compiled != null)
                {
                    documents = compiled;
                    hasEverySource = true;
                    fromCompilerOutput = true;
                }
            }

            // Why the main project's root: a Virtual Player reads the main project's assemblies,
            // whose PDB names sources from the main project's compile.
            HotReloadCompiledSources sources = HotReloadCompiledSourceMap.FromDocuments(
                documents,
                CompiledAssemblyLayout.Resolve(inputs.ProjectRoot).MainProjectRoot,
                inputs.ProjectRoot,
                inputs.PackageRoots,
                inputs.PathComparison);
            return new HotReloadCaptureAssembly(
                candidate.Name,
                sources.AssetPaths,
                sources.IsImmutablePackageAssembly,
                hasEverySource,
                fromCompilerOutput);
        }
    }
}
