using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection.Metadata;
using System.Text;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// One metadata reference the compiler was given, as its PDB records it. For an assembly of the
    /// project the file is its reference assembly (X.ref.dll), so the MVID is that of the reference
    /// assembly, not of the dll under Library/ScriptAssemblies.
    /// </summary>
    internal readonly struct HotReloadPdbReferencedAssembly
    {
        public readonly string FileName;
        public readonly Guid Mvid;

        public HotReloadPdbReferencedAssembly(string fileName, Guid mvid)
        {
            FileName = fileName;
            Mvid = mvid;
        }
    }

    /// <summary>
    /// The compiler inputs a portable PDB records besides its sources: the preprocessor defines and
    /// the metadata references. Safe to call from a pool thread; the only Unity API it uses is
    /// Debug.Assert.
    /// </summary>
    internal sealed class HotReloadPdbCompilationInfo
    {
        // Roslyn's custom debug information kinds (Portable PDB spec, "Compilation Options" and
        // "Compilation Metadata References").
        private static readonly Guid CompilationOptionsKind = new Guid("B5FEEC05-8CD0-4A83-96DA-466284BB4BD8");
        private static readonly Guid MetadataReferencesKind = new Guid("7E4D4708-096E-4C5C-AEDA-CB10BA6A740D");
        private const string DefineKey = "define";
        private const string SourceFileCountKey = "source-file-count";
        private const int MvidLength = 16;
        // Kind/flags byte, timestamp and image size that sit between the alias and the MVID.
        private const int BytesBeforeMvid = 1 + 4 + 4;

        public readonly IReadOnlyList<string> Defines;
        public readonly IReadOnlyList<HotReloadPdbReferencedAssembly> References;

        // The GUID of the PDB id, which the dll's CodeView entry names when both come from one build.
        public readonly Guid PdbGuid;

        // How many syntax trees the compiler emitted, generator output included, or null when the
        // PDB does not record it. The compiler writes one document per tree, and a tool that
        // rewrites the PDB keeps this record but can drop documents.
        public readonly int? SourceFileCount;

        // The rows of the PDB's document table.
        public readonly int DocumentCount;

        private HotReloadPdbCompilationInfo(
            IReadOnlyList<string> defines,
            IReadOnlyList<HotReloadPdbReferencedAssembly> references,
            Guid pdbGuid,
            int? sourceFileCount,
            int documentCount)
        {
            Defines = defines;
            References = references;
            PdbGuid = pdbGuid;
            SourceFileCount = sourceFileCount;
            DocumentCount = documentCount;
        }

        /// <summary>
        /// Reads the defines, in the order the compiler was given them, the compiler's source count,
        /// the document count, the metadata references and the id GUID of the PDB. Throws InvalidOperationException when the PDB lacks either record or a record
        /// is malformed.
        /// </summary>
        internal static HotReloadPdbCompilationInfo Read(string pdbPath)
        {
            Debug.Assert(!string.IsNullOrEmpty(pdbPath), "pdbPath must not be null or empty.");

            using FileStream pdbStream = File.Open(pdbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using MetadataReaderProvider provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
            MetadataReader reader = provider.GetMetadataReader();

            byte[] options = ReadModuleRecord(reader, CompilationOptionsKind, pdbPath);
            byte[] references = ReadModuleRecord(reader, MetadataReferencesKind, pdbPath);
            Dictionary<string, string> optionValues = ParseOptions(options, pdbPath);
            return new HotReloadPdbCompilationInfo(
                ToDefines(optionValues),
                ParseReferences(references, pdbPath),
                new BlobContentId(reader.DebugMetadataHeader.Id).Guid,
                ToSourceFileCount(optionValues),
                reader.Documents.Count);
        }

        /// <summary>
        /// Reads the compiler's source count from an open PDB, or null when the PDB has no
        /// compilation options record or the record does not hold the count. Throws
        /// InvalidOperationException when the record is malformed.
        /// </summary>
        internal static int? ReadSourceFileCount(MetadataReader reader, string pdbPath)
        {
            Debug.Assert(reader != null, "reader must not be null.");

            byte[] options = TryReadModuleRecord(reader, CompilationOptionsKind);
            return options == null ? null : ToSourceFileCount(ParseOptions(options, pdbPath));
        }

        private static byte[] ReadModuleRecord(MetadataReader reader, Guid kind, string pdbPath)
        {
            byte[] record = TryReadModuleRecord(reader, kind);
            if (record == null)
            {
                throw new InvalidOperationException(
                    "The PDB " + Path.GetFileName(pdbPath) + " has no compilation record of kind " + kind + ".");
            }

            return record;
        }

        private static byte[] TryReadModuleRecord(MetadataReader reader, Guid kind)
        {
            // Why only the module's rows: both records describe the whole compilation, so Roslyn
            // attaches them to the module.
            foreach (CustomDebugInformationHandle handle in reader.GetCustomDebugInformation(EntityHandle.ModuleDefinition))
            {
                CustomDebugInformation information = reader.GetCustomDebugInformation(handle);
                if (reader.GetGuid(information.Kind) != kind)
                {
                    continue;
                }

                return reader.GetBlobBytes(information.Value);
            }

            return null;
        }

        // The options blob is a sequence of null-terminated UTF-8 key and value pairs.
        private static Dictionary<string, string> ParseOptions(byte[] blob, string pdbPath)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            int position = 0;
            while (position < blob.Length)
            {
                string key = ReadNullTerminated(blob, ref position, pdbPath);
                string value = ReadNullTerminated(blob, ref position, pdbPath);
                if (!values.ContainsKey(key))
                {
                    values.Add(key, value);
                }
            }

            return values;
        }

        private static IReadOnlyList<string> ToDefines(Dictionary<string, string> optionValues)
        {
            if (!optionValues.TryGetValue(DefineKey, out string value) || value.Length == 0)
            {
                return Array.Empty<string>();
            }

            return value.Split(',');
        }

        private static int? ToSourceFileCount(Dictionary<string, string> optionValues)
        {
            if (!optionValues.TryGetValue(SourceFileCountKey, out string value)
                || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int count))
            {
                return null;
            }

            return count;
        }

        // Each reference is: file name and extern aliases (both null-terminated), a kind/flags
        // byte, a uint32 timestamp, a uint32 image size and the 16-byte MVID.
        private static IReadOnlyList<HotReloadPdbReferencedAssembly> ParseReferences(byte[] blob, string pdbPath)
        {
            List<HotReloadPdbReferencedAssembly> references = new List<HotReloadPdbReferencedAssembly>();
            int position = 0;
            while (position < blob.Length)
            {
                string fileName = ReadNullTerminated(blob, ref position, pdbPath);
                ReadNullTerminated(blob, ref position, pdbPath);
                position += BytesBeforeMvid;
                if (position + MvidLength > blob.Length)
                {
                    throw MalformedRecord(pdbPath);
                }

                references.Add(new HotReloadPdbReferencedAssembly(fileName, new Guid(Slice(blob, position, MvidLength))));
                position += MvidLength;
            }

            return references;
        }

        private static string ReadNullTerminated(byte[] blob, ref int position, string pdbPath)
        {
            int terminator = Array.IndexOf(blob, (byte)0, position);
            if (terminator < 0)
            {
                throw MalformedRecord(pdbPath);
            }

            string text = Encoding.UTF8.GetString(blob, position, terminator - position);
            position = terminator + 1;
            return text;
        }

        private static byte[] Slice(byte[] blob, int start, int length)
        {
            byte[] slice = new byte[length];
            Buffer.BlockCopy(blob, start, slice, 0, length);
            return slice;
        }

        private static InvalidOperationException MalformedRecord(string pdbPath)
        {
            return new InvalidOperationException(
                "A compilation record in the PDB " + Path.GetFileName(pdbPath) + " is malformed.");
        }
    }
}
