using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;

namespace UnityCliLoop.CompileCheckWorker
{
    /// <summary>
    /// Decides which owner each row of one type gets, so the structure and the nullability descriptions
    /// file the same member under the same kind. A row whose change a dependent can feel without writing
    /// the member's name gets a kind of its own; every row of a type the compiler binds by pattern gets
    /// PatternBoundType.
    /// </summary>
    internal sealed class SurfaceTypeOwners
    {
        private const string ConstructorName = ".ctor";
        private const string ExtensionAttributeName = "System.Runtime.CompilerServices.ExtensionAttribute";
        private const string ValueTypeName = "System.ValueType";
        private const string EnumTypeName = "System.Enum";

        private readonly SurfaceMetadata metadata;
        private readonly string typeName;
        private readonly bool patternBound;
        private readonly bool valueType;
        private readonly HashSet<MethodDefinitionHandle> indexerAccessors = new HashSet<MethodDefinitionHandle>();

        public SurfaceTypeOwners(SurfaceMetadata metadata, TypeDefinitionHandle handle)
        {
            this.metadata = metadata;
            MetadataReader reader = metadata.Reader;
            TypeDefinition type = reader.GetTypeDefinition(handle);
            typeName = SurfaceTypeNames.Definition(reader, handle);
            patternBound = metadata.IsPatternBoundType(handle);
            string baseType = metadata.NamespaceAndName(type.BaseType);
            valueType = baseType == ValueTypeName || baseType == EnumTypeName;
            foreach (PropertyDefinitionHandle propertyHandle in type.GetProperties())
            {
                if (IsIndexer(propertyHandle))
                {
                    AddAccessors(reader.GetPropertyDefinition(propertyHandle).GetAccessors());
                }
            }
        }

        public string TypeName => typeName;

        // The type's own row and what hangs off it without shaping it: attributes, security, explicit
        // implementations.
        public SurfaceOwner Type()
        {
            return patternBound ? SurfaceOwner.ForPatternBoundType(typeName) : SurfaceOwner.ForType(typeName);
        }

        // The base type, interfaces, and type parameters with their constraints.
        public SurfaceOwner Shape()
        {
            return patternBound ? SurfaceOwner.ForPatternBoundType(typeName) : SurfaceOwner.ForTypeShape(typeName);
        }

        public SurfaceOwner Method(MethodDefinitionHandle handle)
        {
            if (patternBound)
            {
                return SurfaceOwner.ForPatternBoundType(typeName);
            }

            MethodDefinition method = metadata.Reader.GetMethodDefinition(handle);
            string name = metadata.Reader.GetString(method.Name);
            if (indexerAccessors.Contains(handle))
            {
                return SurfaceOwner.ForIndexer(typeName, name);
            }
            if (name == ConstructorName && method.DecodeSignature(metadata.Provider, null).ParameterTypes.Length == 0)
            {
                return SurfaceOwner.ForParameterlessConstructor(typeName);
            }
            if (metadata.HasAttribute(method.GetCustomAttributes(), ExtensionAttributeName))
            {
                return SurfaceOwner.ForExtensionMethod(typeName, name);
            }
            if ((method.Attributes & MethodAttributes.Abstract) != 0)
            {
                return SurfaceOwner.ForAbstractMember(typeName, name);
            }

            return SurfaceOwner.ForMember(typeName, name);
        }

        public SurfaceOwner Field(FieldDefinitionHandle handle)
        {
            if (patternBound)
            {
                return SurfaceOwner.ForPatternBoundType(typeName);
            }

            FieldDefinition field = metadata.Reader.GetFieldDefinition(handle);
            string name = metadata.Reader.GetString(field.Name);
            if (valueType && (field.Attributes & FieldAttributes.Static) == 0)
            {
                return SurfaceOwner.ForValueTypeInstanceField(typeName, name);
            }

            return SurfaceOwner.ForMember(typeName, name);
        }

        public SurfaceOwner Property(PropertyDefinitionHandle handle)
        {
            if (patternBound)
            {
                return SurfaceOwner.ForPatternBoundType(typeName);
            }

            string name = metadata.Reader.GetString(metadata.Reader.GetPropertyDefinition(handle).Name);

            return IsIndexer(handle) ? SurfaceOwner.ForIndexer(typeName, name) : SurfaceOwner.ForMember(typeName, name);
        }

        public SurfaceOwner Event(EventDefinitionHandle handle)
        {
            if (patternBound)
            {
                return SurfaceOwner.ForPatternBoundType(typeName);
            }

            return SurfaceOwner.ForMember(typeName, metadata.Reader.GetString(metadata.Reader.GetEventDefinition(handle).Name));
        }

        // A property with parameters, whatever IndexerName called it.
        private bool IsIndexer(PropertyDefinitionHandle handle)
        {
            return metadata.Reader.GetPropertyDefinition(handle).DecodeSignature(metadata.Provider, null).ParameterTypes.Length > 0;
        }

        private void AddAccessors(PropertyAccessors accessors)
        {
            if (!accessors.Getter.IsNil)
            {
                indexerAccessors.Add(accessors.Getter);
            }
            if (!accessors.Setter.IsNil)
            {
                indexerAccessors.Add(accessors.Setter);
            }
            foreach (MethodDefinitionHandle other in accessors.Others)
            {
                indexerAccessors.Add(other);
            }
        }
    }
}
