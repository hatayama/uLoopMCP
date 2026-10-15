using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata;

namespace UnityCliLoop.CompileCheckWorker
{
    /// <summary>
    /// A type read out of a signature: how it is written in a surface description, and whether it
    /// holds a position a nullable annotation can describe.
    /// </summary>
    internal sealed class SurfaceType
    {
        public SurfaceType(string name, bool hasNullablePosition, bool isNullableValueType)
        {
            Name = name;
            HasNullablePosition = hasNullablePosition;
            IsNullableValueType = isNullableValueType;
        }

        public string Name { get; }

        public bool HasNullablePosition { get; }

        // Set for System.Nullable`1 itself, so an instantiation over a type without a position has none.
        public bool IsNullableValueType { get; }
    }

    /// <summary>
    /// Decodes signatures into the text a surface description writes, naming every type rather than
    /// pointing at a metadata row, so two assemblies whose tables are numbered differently compare.
    /// </summary>
    internal sealed class SurfaceSignatureProvider : ISignatureTypeProvider<SurfaceType, object>
    {
        private const string NullableNamespace = "System";
        private const string NullableName = "Nullable`1";

        public SurfaceType GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            // Only these two primitives are reference types; every other one, and void, has no position.
            bool hasPosition = typeCode == PrimitiveTypeCode.String || typeCode == PrimitiveTypeCode.Object;

            return new SurfaceType(typeCode.ToString(), hasPosition, false);
        }

        public SurfaceType GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            TypeDefinition type = reader.GetTypeDefinition(handle);
            bool isNullable = type.GetDeclaringType().IsNil
                && reader.StringComparer.Equals(type.Namespace, NullableNamespace)
                && reader.StringComparer.Equals(type.Name, NullableName);

            return Named(SurfaceTypeNames.Definition(reader, handle), rawTypeKind, isNullable);
        }

        public SurfaceType GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            TypeReference type = reader.GetTypeReference(handle);
            bool isNullable = type.ResolutionScope.Kind != HandleKind.TypeReference
                && reader.StringComparer.Equals(type.Namespace, NullableNamespace)
                && reader.StringComparer.Equals(type.Name, NullableName);

            return Named(SurfaceTypeNames.Reference(reader, handle), rawTypeKind, isNullable);
        }

        public SurfaceType GetTypeFromSpecification(
            MetadataReader reader, object genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
        }

        public SurfaceType GetGenericInstantiation(SurfaceType genericType, ImmutableArray<SurfaceType> typeArguments)
        {
            string name = genericType.Name + "<" + string.Join(",", typeArguments.Select(argument => argument.Name)) + ">";
            // A Nullable<T> over a type without a position has none either; every other instantiation,
            // a generic value type included, carries the positions of its type arguments.
            bool positionFree = genericType.IsNullableValueType
                && typeArguments.Length == 1
                && !typeArguments[0].HasNullablePosition;

            return new SurfaceType(name, !positionFree, false);
        }

        public SurfaceType GetSZArrayType(SurfaceType elementType)
        {
            return new SurfaceType(elementType.Name + "[]", true, false);
        }

        public SurfaceType GetArrayType(SurfaceType elementType, ArrayShape shape)
        {
            string name = elementType.Name + "[" + shape.Rank.ToString(CultureInfo.InvariantCulture)
                + ";" + string.Join(",", shape.Sizes) + ";" + string.Join(",", shape.LowerBounds) + "]";

            return new SurfaceType(name, true, false);
        }

        public SurfaceType GetByReferenceType(SurfaceType elementType)
        {
            return new SurfaceType(elementType.Name + "&", elementType.HasNullablePosition, false);
        }

        public SurfaceType GetPointerType(SurfaceType elementType)
        {
            return new SurfaceType(elementType.Name + "*", true, false);
        }

        public SurfaceType GetPinnedType(SurfaceType elementType)
        {
            return new SurfaceType(elementType.Name + " pinned", elementType.HasNullablePosition, false);
        }

        public SurfaceType GetModifiedType(SurfaceType modifier, SurfaceType unmodifiedType, bool isRequired)
        {
            string name = unmodifiedType.Name + (isRequired ? " modreq(" : " modopt(") + modifier.Name + ")";

            return new SurfaceType(name, unmodifiedType.HasNullablePosition, false);
        }

        public SurfaceType GetFunctionPointerType(MethodSignature<SurfaceType> signature)
        {
            return new SurfaceType("method " + SurfaceTypeNames.Signature(signature), true, false);
        }

        public SurfaceType GetGenericTypeParameter(object genericContext, int index)
        {
            return new SurfaceType("!" + index.ToString(CultureInfo.InvariantCulture), true, false);
        }

        public SurfaceType GetGenericMethodParameter(object genericContext, int index)
        {
            return new SurfaceType("!!" + index.ToString(CultureInfo.InvariantCulture), true, false);
        }

        private static SurfaceType Named(string name, byte rawTypeKind, bool isNullable)
        {
            // A non-generic value type, an enum included, has no position of its own.
            bool isValueType = rawTypeKind == (byte)SignatureTypeKind.ValueType;

            return new SurfaceType((isValueType ? "valuetype " : "class ") + name, !isValueType, isValueType && isNullable);
        }
    }
}
