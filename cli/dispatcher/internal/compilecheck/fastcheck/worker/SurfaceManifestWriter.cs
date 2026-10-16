using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace UnityCliLoop.CompileCheckWorker
{
    /// <summary>
    /// Writes what a reference assembly says about itself rather than about its types: its identity,
    /// the assemblies and modules it points at, the types it forwards and the resources it carries.
    /// The module version id is left out: a deterministic build derives it from the content, so it
    /// differs whenever anything does, including what a dependent never reads.
    /// </summary>
    internal static class SurfaceManifestWriter
    {
        // Every row here belongs to the assembly rather than to a type, so no name tells a change of it.
        internal static void Write(SurfaceMetadata metadata, SurfaceDescription description)
        {
            MetadataReader reader = metadata.Reader;
            WriteAssembly(metadata, description);
            ModuleDefinition module = reader.GetModuleDefinition();
            description.Add("module " + reader.GetString(module.Name), SurfaceOwner.Manifest);
            description.AddPrefixed("module ", metadata.AttributeLines(module.GetCustomAttributes()), SurfaceOwner.Manifest);
            description.AddPrefixed("", AssemblyReferenceLines(reader), SurfaceOwner.Manifest);
            description.AddPrefixed("", ModuleReferenceLines(reader), SurfaceOwner.Manifest);
            description.AddPrefixed("", FileLines(reader), SurfaceOwner.Manifest);
            description.AddPrefixed("", ExportedTypeLines(metadata), SurfaceOwner.Manifest);
            description.AddPrefixed("", ResourceLines(metadata), SurfaceOwner.Manifest);
        }

        internal static List<string> SecurityLines(MetadataReader reader, DeclarativeSecurityAttributeHandleCollection attributes)
        {
            List<string> entries = new List<string>();
            foreach (DeclarativeSecurityAttributeHandle handle in attributes)
            {
                DeclarativeSecurityAttribute attribute = reader.GetDeclarativeSecurityAttribute(handle);
                entries.Add("security " + attribute.Action + " " + SurfaceTypeNames.BlobHex(reader, attribute.PermissionSet));
            }
            entries.Sort(StringComparer.Ordinal);

            return entries;
        }

        private static void WriteAssembly(SurfaceMetadata metadata, SurfaceDescription description)
        {
            MetadataReader reader = metadata.Reader;
            if (!reader.IsAssembly)
            {
                description.Add("no assembly manifest", SurfaceOwner.Manifest);
                return;
            }

            AssemblyDefinition assembly = reader.GetAssemblyDefinition();
            description.Add("assembly " + reader.GetString(assembly.Name) + " " + assembly.Version
                + " culture=" + reader.GetString(assembly.Culture) + " flags=" + assembly.Flags
                + " key=" + SurfaceTypeNames.BlobHex(reader, assembly.PublicKey) + " hash=" + assembly.HashAlgorithm, SurfaceOwner.Manifest);
            description.AddPrefixed("assembly ", metadata.AttributeLines(assembly.GetCustomAttributes()), SurfaceOwner.Manifest);
            description.AddPrefixed("assembly ", SecurityLines(reader, assembly.GetDeclarativeSecurityAttributes()), SurfaceOwner.Manifest);
        }

        private static List<string> AssemblyReferenceLines(MetadataReader reader)
        {
            List<string> entries = new List<string>();
            foreach (AssemblyReferenceHandle handle in reader.AssemblyReferences)
            {
                AssemblyReference reference = reader.GetAssemblyReference(handle);
                entries.Add("assemblyref " + reader.GetString(reference.Name) + " " + reference.Version
                    + " culture=" + reader.GetString(reference.Culture) + " flags=" + reference.Flags
                    + " key=" + SurfaceTypeNames.BlobHex(reader, reference.PublicKeyOrToken));
            }
            entries.Sort(StringComparer.Ordinal);

            return entries;
        }

        private static List<string> ModuleReferenceLines(MetadataReader reader)
        {
            List<string> entries = new List<string>();
            int count = reader.GetTableRowCount(TableIndex.ModuleRef);
            for (int row = 1; row <= count; row++)
            {
                ModuleReference reference = reader.GetModuleReference(MetadataTokens.ModuleReferenceHandle(row));
                entries.Add("moduleref " + reader.GetString(reference.Name));
            }
            entries.Sort(StringComparer.Ordinal);

            return entries;
        }

        private static List<string> FileLines(MetadataReader reader)
        {
            List<string> entries = new List<string>();
            foreach (AssemblyFileHandle handle in reader.AssemblyFiles)
            {
                AssemblyFile file = reader.GetAssemblyFile(handle);
                entries.Add("file " + reader.GetString(file.Name) + " metadata=" + file.ContainsMetadata
                    + " hash=" + SurfaceTypeNames.BlobHex(reader, file.HashValue));
            }
            entries.Sort(StringComparer.Ordinal);

            return entries;
        }

        // Every forwarded or exported type with its attributes, as one entry each so the set compares
        // whatever order the table holds them in.
        private static List<string> ExportedTypeLines(SurfaceMetadata metadata)
        {
            MetadataReader reader = metadata.Reader;
            List<string> entries = new List<string>();
            foreach (ExportedTypeHandle handle in reader.ExportedTypes)
            {
                ExportedType type = reader.GetExportedType(handle);
                string entry = "exported " + ExportedTypeName(reader, type) + " flags=" + type.Attributes
                    + " in=" + ImplementationName(reader, type.Implementation);
                entries.Add(entry + " [" + string.Join("; ", metadata.AttributeLines(type.GetCustomAttributes())) + "]");
            }
            entries.Sort(StringComparer.Ordinal);

            return entries;
        }

        private static List<string> ResourceLines(SurfaceMetadata metadata)
        {
            MetadataReader reader = metadata.Reader;
            List<string> entries = new List<string>();
            foreach (ManifestResourceHandle handle in reader.ManifestResources)
            {
                ManifestResource resource = reader.GetManifestResource(handle);
                string place = resource.Implementation.IsNil
                    ? "embedded sha256=" + EmbeddedResourceHash(metadata.PEReader, resource.Offset)
                    : "in=" + ImplementationName(reader, resource.Implementation) + " offset=" + resource.Offset.ToString(CultureInfo.InvariantCulture);
                entries.Add("resource " + reader.GetString(resource.Name) + " flags=" + resource.Attributes + " " + place);
            }
            entries.Sort(StringComparer.Ordinal);

            return entries;
        }

        private static string ExportedTypeName(MetadataReader reader, ExportedType type)
        {
            string space = reader.GetString(type.Namespace);
            string name = space.Length == 0 ? reader.GetString(type.Name) : space + "." + reader.GetString(type.Name);
            if (type.Implementation.Kind == HandleKind.ExportedType)
            {
                return ExportedTypeName(reader, reader.GetExportedType((ExportedTypeHandle)type.Implementation)) + "/" + name;
            }

            return name;
        }

        private static string ImplementationName(MetadataReader reader, EntityHandle implementation)
        {
            switch (implementation.Kind)
            {
                case HandleKind.AssemblyReference:
                    return "[" + reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)implementation).Name) + "]";
                case HandleKind.AssemblyFile:
                    return "file " + reader.GetString(reader.GetAssemblyFile((AssemblyFileHandle)implementation).Name);
                case HandleKind.ExportedType:
                    return "exported " + ExportedTypeName(reader, reader.GetExportedType((ExportedTypeHandle)implementation));
                default:
                    throw new BadImageFormatException("an implementation is named by a " + implementation.Kind + " row");
            }
        }

        // An embedded resource sits in the resources directory as a 4-byte length and its bytes.
        private static string EmbeddedResourceHash(PEReader peReader, long offset)
        {
            CorHeader header = peReader.PEHeaders.CorHeader;
            if (header == null || header.ResourcesDirectory.Size == 0)
            {
                throw new BadImageFormatException("an embedded resource has no resources directory");
            }

            BlobReader blob = peReader.GetSectionData(header.ResourcesDirectory.RelativeVirtualAddress).GetReader();
            blob.Offset = checked((int)offset);
            int length = blob.ReadInt32();
            byte[] content = blob.ReadBytes(length);
            using SHA256 sha256 = SHA256.Create();

            return SurfaceTypeNames.BytesHex(sha256.ComputeHash(content));
        }
    }
}
