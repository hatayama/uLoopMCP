using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Tells whether a PDB names a source document for every top-level type of its dll. A PDB that
    /// was rewritten after the compile (an IL post-processor does this) keeps only the documents a
    /// sequence point refers to, so a file that holds only an interface or an enum disappears from
    /// it. Safe to call from a pool thread; the only Unity API it uses is Debug.Assert.
    /// </summary>
    internal static class HotReloadPdbTypeDocumentCoverage
    {
        // Roslyn's custom debug information that names the documents of a type without method bodies
        // (Portable PDB spec, "Type Definition Documents").
        private static readonly Guid TypeDefinitionDocumentsKind = new Guid("932E74BC-DBA9-4478-8D46-0F32A7BAB3D3");
        private const string CompilerGeneratedAttributeName = "CompilerGeneratedAttribute";

        /// <summary>
        /// True when some top-level type of the dll has no document in the PDB, neither through a
        /// sequence point of one of its methods (nested types included) nor through its type
        /// definition documents. Compiler-generated types, whose names start with "&lt;" or which
        /// carry CompilerGeneratedAttribute, have no source and are not counted.
        /// </summary>
        internal static bool HasTypeWithoutDocument(string dllPath, string pdbPath)
        {
            Debug.Assert(!string.IsNullOrEmpty(dllPath), "dllPath must not be null or empty.");
            Debug.Assert(!string.IsNullOrEmpty(pdbPath), "pdbPath must not be null or empty.");

            using FileStream dllStream = File.Open(dllPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using PEReader peReader = new PEReader(dllStream);
            MetadataReader dll = peReader.GetMetadataReader();
            using FileStream pdbStream = File.Open(pdbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using MetadataReaderProvider provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
            MetadataReader pdb = provider.GetMetadataReader();

            HashSet<TypeDefinitionHandle> documented = DocumentedTopLevelTypes(dll, pdb);
            foreach (TypeDefinitionHandle handle in dll.TypeDefinitions)
            {
                TypeDefinition type = dll.GetTypeDefinition(handle);
                if (!type.GetDeclaringType().IsNil || documented.Contains(handle) || IsCompilerGenerated(dll, type))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        // The PDB's method rows and type rows share the row numbers of the dll's.
        private static HashSet<TypeDefinitionHandle> DocumentedTopLevelTypes(MetadataReader dll, MetadataReader pdb)
        {
            HashSet<TypeDefinitionHandle> documented = new HashSet<TypeDefinitionHandle>();
            foreach (MethodDefinitionHandle method in dll.MethodDefinitions)
            {
                MethodDebugInformation information = pdb.GetMethodDebugInformation(method);
                if (information.Document.IsNil && information.SequencePointsBlob.IsNil)
                {
                    continue;
                }

                documented.Add(TopLevelOf(dll, dll.GetMethodDefinition(method).GetDeclaringType()));
            }

            foreach (CustomDebugInformationHandle handle in pdb.CustomDebugInformation)
            {
                CustomDebugInformation information = pdb.GetCustomDebugInformation(handle);
                if (information.Parent.Kind != HandleKind.TypeDefinition
                    || pdb.GetGuid(information.Kind) != TypeDefinitionDocumentsKind)
                {
                    continue;
                }

                documented.Add(TopLevelOf(dll, (TypeDefinitionHandle)information.Parent));
            }

            return documented;
        }

        private static TypeDefinitionHandle TopLevelOf(MetadataReader dll, TypeDefinitionHandle handle)
        {
            TypeDefinitionHandle declaring = dll.GetTypeDefinition(handle).GetDeclaringType();
            while (!declaring.IsNil)
            {
                handle = declaring;
                declaring = dll.GetTypeDefinition(handle).GetDeclaringType();
            }

            return handle;
        }

        private static bool IsCompilerGenerated(MetadataReader dll, TypeDefinition type)
        {
            if (dll.GetString(type.Name).StartsWith("<", StringComparison.Ordinal))
            {
                return true;
            }

            foreach (CustomAttributeHandle handle in type.GetCustomAttributes())
            {
                EntityHandle constructor = dll.GetCustomAttribute(handle).Constructor;
                if (constructor.Kind != HandleKind.MemberReference)
                {
                    continue;
                }

                EntityHandle parent = dll.GetMemberReference((MemberReferenceHandle)constructor).Parent;
                if (parent.Kind == HandleKind.TypeReference
                    && dll.GetString(dll.GetTypeReference((TypeReferenceHandle)parent).Name) == CompilerGeneratedAttributeName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
