using System;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata;

namespace UnityCliLoop.CompileCheckWorker
{
    /// <summary>
    /// Writes the names a surface description uses for types, members and signatures. Nested types
    /// are joined with '/', the metadata form, everywhere except in ReflectionName.
    /// </summary>
    internal static class SurfaceTypeNames
    {
        private const string MetadataNestingSeparator = "/";
        private const string ReflectionNestingSeparator = "+";

        internal static string Definition(MetadataReader reader, TypeDefinitionHandle handle)
        {
            return DefinitionName(reader, handle, MetadataNestingSeparator);
        }

        // The name typeof writes into an attribute value for a type of the same assembly.
        internal static string ReflectionName(MetadataReader reader, TypeDefinitionHandle handle)
        {
            return DefinitionName(reader, handle, ReflectionNestingSeparator);
        }

        internal static string Reference(MetadataReader reader, TypeReferenceHandle handle)
        {
            TypeReference type = reader.GetTypeReference(handle);
            string name = QualifiedName(reader, type.Namespace, type.Name);
            EntityHandle scope = type.ResolutionScope;
            switch (scope.Kind)
            {
                case HandleKind.TypeReference:
                    return Reference(reader, (TypeReferenceHandle)scope) + MetadataNestingSeparator + reader.GetString(type.Name);
                case HandleKind.AssemblyReference:
                    return "[" + reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)scope).Name) + "]" + name;
                case HandleKind.ModuleReference:
                    return "[.module " + reader.GetString(reader.GetModuleReference((ModuleReferenceHandle)scope).Name) + "]" + name;
                case HandleKind.ModuleDefinition:
                    return "[.this]" + name;
                default:
                    return "[*]" + name;
            }
        }

        // A type a row points at by handle: a base type, an interface, a constraint, an event type.
        internal static string Entity(MetadataReader reader, SurfaceSignatureProvider provider, EntityHandle handle)
        {
            if (handle.IsNil)
            {
                return "(none)";
            }

            switch (handle.Kind)
            {
                case HandleKind.TypeDefinition:
                    return Definition(reader, (TypeDefinitionHandle)handle);
                case HandleKind.TypeReference:
                    return Reference(reader, (TypeReferenceHandle)handle);
                case HandleKind.TypeSpecification:
                    return reader.GetTypeSpecification((TypeSpecificationHandle)handle).DecodeSignature(provider, null).Name;
                default:
                    throw new BadImageFormatException("a type is named by a " + handle.Kind + " row");
            }
        }

        internal static string Method(MetadataReader reader, SurfaceSignatureProvider provider, MethodDefinitionHandle handle)
        {
            MethodDefinition method = reader.GetMethodDefinition(handle);

            return Definition(reader, method.GetDeclaringType()) + "::" + reader.GetString(method.Name)
                + Signature(method.DecodeSignature(provider, null));
        }

        // A method or field another row points at, by definition or by reference.
        internal static string Member(MetadataReader reader, SurfaceSignatureProvider provider, EntityHandle handle)
        {
            if (handle.Kind == HandleKind.MethodDefinition)
            {
                return Method(reader, provider, (MethodDefinitionHandle)handle);
            }
            if (handle.Kind != HandleKind.MemberReference)
            {
                throw new BadImageFormatException("a member is named by a " + handle.Kind + " row");
            }

            MemberReference member = reader.GetMemberReference((MemberReferenceHandle)handle);
            string name = MemberParent(reader, provider, member.Parent) + "::" + reader.GetString(member.Name);
            if (member.GetKind() == MemberReferenceKind.Field)
            {
                return name + " field " + member.DecodeFieldSignature(provider, null).Name;
            }

            return name + Signature(member.DecodeMethodSignature(provider, null));
        }

        internal static string Signature(MethodSignature<SurfaceType> signature)
        {
            string text = "`" + signature.GenericParameterCount.ToString(CultureInfo.InvariantCulture)
                + " " + signature.Header.RawValue.ToString("X2", CultureInfo.InvariantCulture)
                + "(" + string.Join(",", signature.ParameterTypes.Select(type => type.Name)) + ")";
            // A vararg signature says where its fixed parameters end.
            if (signature.RequiredParameterCount != signature.ParameterTypes.Length)
            {
                text += " required=" + signature.RequiredParameterCount.ToString(CultureInfo.InvariantCulture);
            }

            return text + "->" + signature.ReturnType.Name;
        }

        internal static string BytesHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes);
        }

        internal static string BlobHex(MetadataReader reader, BlobHandle handle)
        {
            return handle.IsNil ? "" : BitConverter.ToString(reader.GetBlobBytes(handle));
        }

        private static string MemberParent(MetadataReader reader, SurfaceSignatureProvider provider, EntityHandle parent)
        {
            switch (parent.Kind)
            {
                case HandleKind.MethodDefinition:
                    return Method(reader, provider, (MethodDefinitionHandle)parent);
                case HandleKind.ModuleReference:
                    return "[.module " + reader.GetString(reader.GetModuleReference((ModuleReferenceHandle)parent).Name) + "]";
                default:
                    return Entity(reader, provider, parent);
            }
        }

        private static string DefinitionName(MetadataReader reader, TypeDefinitionHandle handle, string separator)
        {
            TypeDefinition type = reader.GetTypeDefinition(handle);
            TypeDefinitionHandle declaring = type.GetDeclaringType();
            if (!declaring.IsNil)
            {
                return DefinitionName(reader, declaring, separator) + separator + reader.GetString(type.Name);
            }

            return QualifiedName(reader, type.Namespace, type.Name);
        }

        private static string QualifiedName(MetadataReader reader, StringHandle space, StringHandle name)
        {
            string spaceText = reader.GetString(space);

            return spaceText.Length == 0 ? reader.GetString(name) : spaceText + "." + reader.GetString(name);
        }
    }
}
