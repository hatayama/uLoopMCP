using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Reads the documents of every source an assembly was compiled from out of the compiler's own
    /// dll and PDB, which Unity's build leaves under Library/Bee/artifacts/&lt;dag&gt;/, for when a tool
    /// that rewrote the PDB beside the dll dropped documents. Does not call Unity, so it runs on a
    /// pool thread.
    /// </summary>
    internal static class HotReloadCompilerOutputDocuments
    {
        private const string BeeDirectoryName = "Bee";
        private const string ArtifactsDirectoryName = "artifacts";
        private const string DagSearchPattern = "*.dag";
        private const string PdbExtension = ".pdb";

        /// <summary>
        /// Returns, in table order, the documents of the compiler's PDB for the dll under
        /// Library/ScriptAssemblies, when a compiler dll with the same MVID exists, its PDB belongs to
        /// it, and that PDB has one document per source the compiler recorded. Returns null otherwise.
        /// IO failures are thrown as they are.
        /// </summary>
        internal static List<HotReloadPdbDocument> TryReadEveryDocument(string dllPath)
        {
            Debug.Assert(!string.IsNullOrEmpty(dllPath), "dllPath must not be null or empty.");

            // Why beside the dll and not under the project root: a Virtual Player reads the main
            // project's Library/ScriptAssemblies, whose build left Bee next to it, and a dll copied
            // elsewhere has no compiler output of its own to stand in for its PDB.
            string libraryDirectory = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(dllPath)));
            string artifactsDirectory = Path.Combine(libraryDirectory, BeeDirectoryName, ArtifactsDirectoryName);
            if (!Directory.Exists(artifactsDirectory))
            {
                return null;
            }

            string fileName = Path.GetFileName(dllPath);
            Guid mvid = HotReloadAssemblyMetadataMvid.Read(dllPath);
            // Why every dag, matched by MVID: the dag's name changes with the Unity version and the
            // code optimization, and a player build leaves dags of its own beside the Editor's.
            // Sorted so the order the dags are tried in does not depend on the file system.
            string[] dagDirectories = Directory.GetDirectories(artifactsDirectory, DagSearchPattern);
            Array.Sort(dagDirectories, StringComparer.Ordinal);
            foreach (string dagDirectory in dagDirectories)
            {
                List<HotReloadPdbDocument> documents = TryReadFromDag(dagDirectory, fileName, mvid);
                if (documents != null)
                {
                    return documents;
                }
            }

            return null;
        }

        private static List<HotReloadPdbDocument> TryReadFromDag(string dagDirectory, string fileName, Guid mvid)
        {
            string compilerDllPath = Path.Combine(dagDirectory, fileName);
            string compilerPdbPath = Path.ChangeExtension(compilerDllPath, PdbExtension);
            if (!File.Exists(compilerDllPath) || !File.Exists(compilerPdbPath))
            {
                return null;
            }

            // Why the MVID: an IL post-processor keeps it while it changes the PDB id, and Unity
            // compiles deterministically, so the same MVID means the same compiler inputs.
            if (HotReloadAssemblyMetadataMvid.Read(compilerDllPath) != mvid)
            {
                return null;
            }

            List<HotReloadPdbDocument> documents = HotReloadPdbDocumentTable.TryRead(
                compilerDllPath, compilerPdbPath, out int? sourceFileCount);
            if (documents == null)
            {
                return null;
            }

            // Why the count again: a compiler PDB that also lacks documents, or records no count,
            // cannot vouch for every source.
            return sourceFileCount == documents.Count ? documents : null;
        }
    }
}
