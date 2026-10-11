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
        /// Returns the documents of the PDB in table order, after checking that the PDB belongs to
        /// the build of the dll. Throws InvalidOperationException for a PDB of another build.
        /// </summary>
        internal static List<HotReloadPdbDocument> Read(string dllPath, string pdbPath)
        {
            Debug.Assert(!string.IsNullOrEmpty(dllPath), "dllPath must not be null or empty.");
            Debug.Assert(!string.IsNullOrEmpty(pdbPath), "pdbPath must not be null or empty.");

            Guid codeViewGuid = HotReloadPdbDocumentIndex.ReadCodeViewGuid(dllPath);
            using FileStream pdbStream = File.Open(pdbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using MetadataReaderProvider provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
            MetadataReader reader = provider.GetMetadataReader();

            // Why checked here: a PDB of another build would list sources the dll was not compiled from.
            BlobContentId pdbId = new BlobContentId(reader.DebugMetadataHeader.Id);
            if (pdbId.Guid != codeViewGuid)
            {
                throw new InvalidOperationException(
                    "The PDB beside " + Path.GetFileName(dllPath) + " belongs to another build of it.");
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

            return documents;
        }
    }
}
