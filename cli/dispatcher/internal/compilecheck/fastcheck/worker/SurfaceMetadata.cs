using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace UnityCliLoop.CompileCheckWorker
{
    /// <summary>
    /// Which nullable attribute an attribute row is, when it is one the compiler embedded.
    /// </summary>
    internal enum NullableAttributeKind
    {
        None,
        Nullable,
        Context,
    }

    /// <summary>
    /// One reference assembly as a surface description reads it: which types it leaves out, and how it
    /// writes names and attributes.
    /// </summary>
    internal sealed class SurfaceMetadata
    {
        private const string EmbeddedAttributeName = "Microsoft.CodeAnalysis.EmbeddedAttribute";
        private const string CompilerServicesNamespace = "System.Runtime.CompilerServices";
        private const string NullableAttributeName = CompilerServicesNamespace + ".NullableAttribute";
        private const string NullableContextAttributeName = CompilerServicesNamespace + ".NullableContextAttribute";
        private const string InterpolatedStringHandlerAttributeName = CompilerServicesNamespace + ".InterpolatedStringHandlerAttribute";
        private const string CollectionBuilderAttributeName = CompilerServicesNamespace + ".CollectionBuilderAttribute";
        private const ushort AttributeBlobProlog = 1;

        private static readonly string[] StateMachineAttributeNames =
        {
            "AsyncStateMachineAttribute",
            "IteratorStateMachineAttribute",
            "AsyncIteratorStateMachineAttribute",
        };

        private readonly HashSet<TypeDefinitionHandle> embeddedTypes;
        private readonly HashSet<TypeDefinitionHandle> hiddenNestedTypes;
        private readonly HashSet<string> hiddenNestedReflectionNames = new HashSet<string>(StringComparer.Ordinal);

        public SurfaceMetadata(PEReader peReader)
        {
            PEReader = peReader;
            Reader = peReader.GetMetadataReader();
            Provider = new SurfaceSignatureProvider();
            embeddedTypes = FindEmbeddedTypes(Reader);
            hiddenNestedTypes = SurfaceReachability.UnreferencedPrivateNestedTypes(Reader, embeddedTypes);
            foreach (TypeDefinitionHandle handle in hiddenNestedTypes)
            {
                hiddenNestedReflectionNames.Add(SurfaceTypeNames.ReflectionName(Reader, handle));
            }
        }

        public PEReader PEReader { get; }

        public MetadataReader Reader { get; }

        public SurfaceSignatureProvider Provider { get; }

        // A type no dependent can see: one the compiler embedded, or a private nested type nothing kept
        // refers to. Its members, parameters and attributes go with it.
        public bool IsLeftOut(TypeDefinitionHandle handle)
        {
            return embeddedTypes.Contains(handle) || hiddenNestedTypes.Contains(handle);
        }

        public string TypeName(EntityHandle handle)
        {
            return SurfaceTypeNames.Entity(Reader, Provider, handle);
        }

        public string MethodName(MethodDefinitionHandle handle)
        {
            return SurfaceTypeNames.Method(Reader, Provider, handle);
        }

        /// <summary>
        /// Says whether an attribute is the compiler's embedded NullableAttribute or
        /// NullableContextAttribute. One of the same name defined in another assembly is not.
        /// </summary>
        public NullableAttributeKind NullableKindOf(CustomAttribute attribute)
        {
            if (attribute.Constructor.Kind != HandleKind.MethodDefinition)
            {
                return NullableAttributeKind.None;
            }

            TypeDefinitionHandle declaring = Reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType();
            if (!embeddedTypes.Contains(declaring))
            {
                return NullableAttributeKind.None;
            }

            string name = SurfaceTypeNames.Definition(Reader, declaring);
            if (name == NullableAttributeName)
            {
                return NullableAttributeKind.Nullable;
            }

            return name == NullableContextAttributeName ? NullableAttributeKind.Context : NullableAttributeKind.None;
        }

        /// <summary>
        /// Writes the attributes of one row, each as its constructor and its value, sorted: the order of
        /// the table follows row numbers, which an embedded type added elsewhere can shift. The
        /// compiler's nullable attributes are left to the nullability description, and a state machine
        /// attribute naming a left-out type is left out with it.
        /// </summary>
        public List<string> AttributeLines(CustomAttributeHandleCollection attributes)
        {
            List<string> lines = new List<string>();
            foreach (CustomAttributeHandle handle in attributes)
            {
                CustomAttribute attribute = Reader.GetCustomAttribute(handle);
                if (NullableKindOf(attribute) != NullableAttributeKind.None || NamesHiddenStateMachine(attribute))
                {
                    continue;
                }
                lines.Add("@" + SurfaceTypeNames.Member(Reader, Provider, attribute.Constructor)
                    + " = " + SurfaceTypeNames.BlobHex(Reader, attribute.Value));
            }
            lines.Sort(StringComparer.Ordinal);

            return lines;
        }

        /// <summary>
        /// Says whether a row carries an attribute of the given namespace-qualified type, whether the type
        /// comes from another assembly or is defined in this one (as a polyfill would be).
        /// </summary>
        public bool HasAttribute(CustomAttributeHandleCollection attributes, string namespaceAndName)
        {
            foreach (CustomAttributeHandle handle in attributes)
            {
                if (AttributeTypeName(Reader.GetCustomAttribute(handle)) == namespaceAndName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Says whether the compiler binds the members of a type by pattern without a dependent writing
        /// their names: an interpolated string handler or a collection builder target.
        /// </summary>
        public bool IsPatternBoundType(TypeDefinitionHandle handle)
        {
            CustomAttributeHandleCollection attributes = Reader.GetTypeDefinition(handle).GetCustomAttributes();

            return HasAttribute(attributes, InterpolatedStringHandlerAttributeName)
                || HasAttribute(attributes, CollectionBuilderAttributeName);
        }

        /// <summary>
        /// The namespace-qualified name of a type defined in or referenced by this assembly, without the
        /// assembly it comes from; "" for no type and for any other row, such as a constructed generic type.
        /// </summary>
        public string NamespaceAndName(EntityHandle type)
        {
            if (type.IsNil)
            {
                return "";
            }

            switch (type.Kind)
            {
                case HandleKind.TypeDefinition:
                    return SurfaceTypeNames.Definition(Reader, (TypeDefinitionHandle)type);
                case HandleKind.TypeReference:
                    TypeReference reference = Reader.GetTypeReference((TypeReferenceHandle)type);
                    string space = Reader.GetString(reference.Namespace);
                    string name = Reader.GetString(reference.Name);

                    return space.Length == 0 ? name : space + "." + name;
                default:
                    return "";
            }
        }

        // The namespace-qualified name of an attribute's type, or "" when its constructor is not a method
        // of a type this reader can name.
        private string AttributeTypeName(CustomAttribute attribute)
        {
            EntityHandle type;
            if (attribute.Constructor.Kind == HandleKind.MethodDefinition)
            {
                type = Reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType();
            }
            else if (attribute.Constructor.Kind == HandleKind.MemberReference)
            {
                type = Reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent;
            }
            else
            {
                return "";
            }

            return NamespaceAndName(type);
        }

        private static HashSet<TypeDefinitionHandle> FindEmbeddedTypes(MetadataReader reader)
        {
            HashSet<TypeDefinitionHandle> embedded = new HashSet<TypeDefinitionHandle>();
            foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
            {
                foreach (CustomAttributeHandle attributeHandle in reader.GetTypeDefinition(handle).GetCustomAttributes())
                {
                    if (IsEmbeddedAttribute(reader, reader.GetCustomAttribute(attributeHandle)))
                    {
                        embedded.Add(handle);
                    }
                }
            }

            return embedded;
        }

        // [Microsoft.CodeAnalysis.Embedded] as the compiler writes it: a type defined in this very module.
        private static bool IsEmbeddedAttribute(MetadataReader reader, CustomAttribute attribute)
        {
            if (attribute.Constructor.Kind != HandleKind.MethodDefinition)
            {
                return false;
            }

            TypeDefinitionHandle declaring = reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType();

            return SurfaceTypeNames.Definition(reader, declaring) == EmbeddedAttributeName;
        }

        // An async or iterator attribute whose typeof names a state machine type left out of the
        // surface: its name carries the method's position in the type, so it moves whenever a member
        // is added before the method, while a dependent only ever sees the method itself.
        private bool NamesHiddenStateMachine(CustomAttribute attribute)
        {
            if (!IsStateMachineAttribute(attribute))
            {
                return false;
            }

            BlobReader blob = Reader.GetBlobReader(attribute.Value);
            if (blob.Length < 2 || blob.ReadUInt16() != AttributeBlobProlog)
            {
                return false;
            }
            string typeName = blob.ReadSerializedString();

            return typeName != null && hiddenNestedReflectionNames.Contains(typeName);
        }

        private bool IsStateMachineAttribute(CustomAttribute attribute)
        {
            if (attribute.Constructor.Kind != HandleKind.MemberReference)
            {
                return false;
            }

            EntityHandle parent = Reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent;
            if (parent.Kind != HandleKind.TypeReference)
            {
                return false;
            }

            TypeReference type = Reader.GetTypeReference((TypeReferenceHandle)parent);
            if (type.ResolutionScope.Kind != HandleKind.AssemblyReference
                || !Reader.StringComparer.Equals(type.Namespace, CompilerServicesNamespace))
            {
                return false;
            }

            return Array.Exists(StateMachineAttributeNames, name => Reader.StringComparer.Equals(type.Name, name));
        }
    }
}
