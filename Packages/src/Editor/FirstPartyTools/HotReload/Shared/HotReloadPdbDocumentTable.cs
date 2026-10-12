using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Reads every row of a portable PDB's document table: each source the compiler was given,
    /// including files without a method body, which HotReloadPdbDocumentIndex leaves out because
    /// no sequence point refers to them.
    /// </summary>
    internal static class HotReloadPdbDocumentTable
    {
        /// <summary>
        /// Returns the documents of the PDB in table order and, in sourceFileCount, the number of
        /// sources the compiler recorded (null when it did not), after checking that the PDB belongs
        /// to the build of the dll. Throws InvalidOperationException for a PDB of another build.
        /// </summary>
        internal static List<HotReloadPdbDocument> Read(string dllPath, string pdbPath, out int? sourceFileCount)
        {
            List<HotReloadPdbDocument> documents = TryRead(dllPath, pdbPath, out sourceFileCount);
            if (documents == null)
            {
                throw new InvalidOperationException(
                    "The PDB beside " + Path.GetFileName(dllPath) + " belongs to another build of it.");
            }

            return documents;
        }

        /// <summary>
        /// Same as Read, but returns null instead of throwing when the PDB belongs to another build
        /// of the dll.
        /// </summary>
        internal static List<HotReloadPdbDocument> TryRead(string dllPath, string pdbPath, out int? sourceFileCount)
        {
            Debug.Assert(!string.IsNullOrEmpty(dllPath), "dllPath must not be null or empty.");
            Debug.Assert(!string.IsNullOrEmpty(pdbPath), "pdbPath must not be null or empty.");

            sourceFileCount = null;
            Guid codeViewGuid = HotReloadPdbDocumentIndex.ReadCodeViewGuid(dllPath);
            using FileStream pdbStream = File.Open(pdbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using MetadataReaderProvider provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
            MetadataReader reader = provider.GetMetadataReader();

            // Why checked here: a PDB of another build would list sources the dll was not compiled from.
            BlobContentId pdbId = new BlobContentId(reader.DebugMetadataHeader.Id);
            if (pdbId.Guid != codeViewGuid)
            {
                return null;
            }

            List<HotReloadPdbDocument> documents = new List<HotReloadPdbDocument>(reader.Documents.Count);
            foreach (DocumentHandle handle in reader.Documents)
            {
                Document document = reader.GetDocument(handle);
                documents.Add(new HotReloadPdbDocument(
                    reader.GetString(document.Name),
                    HotReloadPdbDocumentIndex.ToHashAlgorithm(reader.GetGuid(document.HashAlgorithm)),
                    reader.GetBlobBytes(document.Hash)));
            }

            // Why from the same reader: the PDB is then opened once for both.
            sourceFileCount = HotReloadPdbCompilationInfo.ReadSourceFileCount(reader, pdbPath);
            return documents;
        }
    }
}
