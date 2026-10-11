using System;
using System.Collections.Generic;
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
        private const int MvidLength = 16;
        // Kind/flags byte, timestamp and image size that sit between the alias and the MVID.
        private const int BytesBeforeMvid = 1 + 4 + 4;

        public readonly IReadOnlyList<string> Defines;
        public readonly IReadOnlyList<HotReloadPdbReferencedAssembly> References;

        private HotReloadPdbCompilationInfo(
            IReadOnlyList<string> defines,
            IReadOnlyList<HotReloadPdbReferencedAssembly> references)
        {
            Defines = defines;
            References = references;
        }

        /// <summary>
        /// Reads the defines, in the order the compiler was given them, and the metadata references
        /// of the PDB. Throws InvalidOperationException when the PDB lacks either record or a record
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
            return new HotReloadPdbCompilationInfo(ParseDefines(options, pdbPath), ParseReferences(references, pdbPath));
        }

        private static byte[] ReadModuleRecord(MetadataReader reader, Guid kind, string pdbPath)
        {
            foreach (CustomDebugInformationHandle handle in reader.CustomDebugInformation)
            {
                CustomDebugInformation information = reader.GetCustomDebugInformation(handle);
                // Why the parent is checked: both records describe the whole compilation, so only
                // the module's copy is the one Roslyn writes for them.
                if (information.Parent != EntityHandle.ModuleDefinition || reader.GetGuid(information.Kind) != kind)
                {
                    continue;
                }

                return reader.GetBlobBytes(information.Value);
            }

            throw new InvalidOperationException(
                "The PDB " + Path.GetFileName(pdbPath) + " has no compilation record of kind " + kind + ".");
        }

        // The options blob is a sequence of null-terminated UTF-8 key and value pairs.
        private static IReadOnlyList<string> ParseDefines(byte[] blob, string pdbPath)
        {
            int position = 0;
            while (position < blob.Length)
            {
                string key = ReadNullTerminated(blob, ref position, pdbPath);
                string value = ReadNullTerminated(blob, ref position, pdbPath);
                if (key == DefineKey)
                {
                    return value.Length == 0 ? Array.Empty<string>() : value.Split(',');
                }
            }

            return Array.Empty<string>();
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
