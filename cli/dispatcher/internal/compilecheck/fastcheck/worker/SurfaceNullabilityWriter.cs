using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata;

namespace UnityCliLoop.CompileCheckWorker
{
    /// <summary>
    /// Writes the nullable annotation of every row that can hold one, as one effective value per row.
    /// The compiler writes an annotation either on the row itself or, when it matches, only once as the
    /// context of the method or type around it, and that context follows every member of the type -
    /// private ones included. Resolving each row to the value that applies to it makes two assemblies
    /// whose contexts moved compare the same exactly when every annotation a dependent reads is the same.
    /// What the bytes of an annotation mean position by position is not decoded: equal bytes on the same
    /// row describe the same type.
    /// </summary>
    internal static class SurfaceNullabilityWriter
    {
        private const byte NoContext = 0;
        private const ushort AttributeBlobProlog = 1;
        private const int ReturnParameterSequence = 0;

        private static readonly HandleKind[] AnnotatedRowKinds =
        {
            HandleKind.TypeDefinition, HandleKind.InterfaceImplementation, HandleKind.GenericParameter,
            HandleKind.GenericParameterConstraint, HandleKind.FieldDefinition, HandleKind.PropertyDefinition,
            HandleKind.EventDefinition, HandleKind.Parameter,
        };

        private static readonly HandleKind[] ContextRowKinds = { HandleKind.TypeDefinition, HandleKind.MethodDefinition };

        internal static void Write(SurfaceMetadata metadata, List<string> lines)
        {
            RejectUnexpectedRows(metadata);
            foreach (TypeDefinitionHandle handle in metadata.Reader.TypeDefinitions)
            {
                if (!metadata.IsLeftOut(handle))
                {
                    WriteType(metadata, handle, lines);
                }
            }
        }

        // Why an attribute on any other kind of row stops the comparison: this description would not
        // read it, and an annotation it does not read must not pass for an unchanged one.
        private static void RejectUnexpectedRows(SurfaceMetadata metadata)
        {
            foreach (CustomAttributeHandle handle in metadata.Reader.CustomAttributes)
            {
                CustomAttribute attribute = metadata.Reader.GetCustomAttribute(handle);
                NullableAttributeKind kind = metadata.NullableKindOf(attribute);
                bool unexpected = (kind == NullableAttributeKind.Nullable && !AnnotatedRowKinds.Contains(attribute.Parent.Kind))
                    || (kind == NullableAttributeKind.Context && !ContextRowKinds.Contains(attribute.Parent.Kind));
                if (unexpected)
                {
                    throw new InvalidOperationException("a nullable attribute sits on a " + attribute.Parent.Kind + " row");
                }
            }
        }

        private static void WriteType(SurfaceMetadata metadata, TypeDefinitionHandle handle, List<string> lines)
        {
            MetadataReader reader = metadata.Reader;
            TypeDefinition type = reader.GetTypeDefinition(handle);
            string name = SurfaceTypeNames.Definition(reader, handle);
            byte typeContext = TypeContext(metadata, handle);
            // The annotation of the base type sits on the type's own row.
            if (!type.BaseType.IsNil)
            {
                lines.Add("base " + name + " " + Effective(metadata, type.GetCustomAttributes(), typeContext));
            }
            List<string> interfaces = new List<string>();
            foreach (InterfaceImplementationHandle interfaceHandle in type.GetInterfaceImplementations())
            {
                InterfaceImplementation implementation = reader.GetInterfaceImplementation(interfaceHandle);
                interfaces.Add("iface " + name + " " + metadata.TypeName(implementation.Interface)
                    + " " + Effective(metadata, implementation.GetCustomAttributes(), typeContext));
            }
            interfaces.Sort(StringComparer.Ordinal);
            lines.AddRange(interfaces);
            WriteGenericParameters(metadata, type.GetGenericParameters(), "gp " + name, typeContext, lines);
            WriteMembers(metadata, type, name, typeContext, lines);
        }

        private static void WriteMembers(SurfaceMetadata metadata, TypeDefinition type, string name, byte typeContext, List<string> lines)
        {
            MetadataReader reader = metadata.Reader;
            foreach (FieldDefinitionHandle fieldHandle in type.GetFields())
            {
                FieldDefinition field = reader.GetFieldDefinition(fieldHandle);
                if (field.DecodeSignature(metadata.Provider, null).HasNullablePosition)
                {
                    lines.Add("field " + name + "::" + reader.GetString(field.Name) + " " + Effective(metadata, field.GetCustomAttributes(), typeContext));
                }
            }
            foreach (PropertyDefinitionHandle propertyHandle in type.GetProperties())
            {
                PropertyDefinition property = reader.GetPropertyDefinition(propertyHandle);
                if (property.DecodeSignature(metadata.Provider, null).ReturnType.HasNullablePosition)
                {
                    lines.Add("property " + name + "::" + reader.GetString(property.Name) + " " + Effective(metadata, property.GetCustomAttributes(), typeContext));
                }
            }
            foreach (EventDefinitionHandle eventHandle in type.GetEvents())
            {
                EventDefinition eventDefinition = reader.GetEventDefinition(eventHandle);
                lines.Add("event " + name + "::" + reader.GetString(eventDefinition.Name) + " " + Effective(metadata, eventDefinition.GetCustomAttributes(), typeContext));
            }
            foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
            {
                WriteMethod(metadata, methodHandle, typeContext, lines);
            }
        }

        private static void WriteMethod(SurfaceMetadata metadata, MethodDefinitionHandle handle, byte typeContext, List<string> lines)
        {
            MetadataReader reader = metadata.Reader;
            MethodDefinition method = reader.GetMethodDefinition(handle);
            string name = metadata.MethodName(handle);
            byte? ownContext = OwnContext(metadata, method.GetCustomAttributes());
            byte context = ownContext ?? typeContext;
            WriteGenericParameters(metadata, method.GetGenericParameters(), "gp " + name, context, lines);

            Dictionary<int, Parameter> rows = new Dictionary<int, Parameter>();
            foreach (ParameterHandle parameterHandle in method.GetParameters())
            {
                Parameter parameter = reader.GetParameter(parameterHandle);
                rows[parameter.SequenceNumber] = parameter;
            }
            MethodSignature<SurfaceType> signature = method.DecodeSignature(metadata.Provider, null);
            if (signature.ReturnType.HasNullablePosition)
            {
                lines.Add("ret " + name + " " + Effective(metadata, RowAttributes(rows, ReturnParameterSequence), context));
            }
            for (int index = 0; index < signature.ParameterTypes.Length; index++)
            {
                if (signature.ParameterTypes[index].HasNullablePosition)
                {
                    int sequence = index + 1;
                    lines.Add("param " + name + "#" + sequence.ToString(CultureInfo.InvariantCulture)
                        + " " + Effective(metadata, RowAttributes(rows, sequence), context));
                }
            }
        }

        // A parameter without a row has no attribute of its own, the same as a row without one.
        private static IEnumerable<CustomAttributeHandle> RowAttributes(Dictionary<int, Parameter> rows, int sequence)
        {
            return rows.TryGetValue(sequence, out Parameter parameter)
                ? parameter.GetCustomAttributes()
                : Enumerable.Empty<CustomAttributeHandle>();
        }

        // Type parameters and their constraints always carry a position, whatever their constraints are.
        private static void WriteGenericParameters(
            SurfaceMetadata metadata, GenericParameterHandleCollection parameters, string owner, byte context, List<string> lines)
        {
            MetadataReader reader = metadata.Reader;
            foreach (GenericParameterHandle handle in parameters)
            {
                GenericParameter parameter = reader.GetGenericParameter(handle);
                string name = owner + "!" + parameter.Index.ToString(CultureInfo.InvariantCulture);
                lines.Add(name + " " + Effective(metadata, parameter.GetCustomAttributes(), context));
                List<string> constraints = new List<string>();
                foreach (GenericParameterConstraintHandle constraintHandle in parameter.GetConstraints())
                {
                    GenericParameterConstraint constraint = reader.GetGenericParameterConstraint(constraintHandle);
                    constraints.Add(name + " constraint " + metadata.TypeName(constraint.Type)
                        + " " + Effective(metadata, constraint.GetCustomAttributes(), context));
                }
                constraints.Sort(StringComparer.Ordinal);
                lines.AddRange(constraints);
            }
        }

        // The context a type's members fall back to: its own, else that of the type around it.
        private static byte TypeContext(SurfaceMetadata metadata, TypeDefinitionHandle handle)
        {
            for (TypeDefinitionHandle current = handle; !current.IsNil; current = metadata.Reader.GetTypeDefinition(current).GetDeclaringType())
            {
                byte? own = OwnContext(metadata, metadata.Reader.GetTypeDefinition(current).GetCustomAttributes());
                if (own.HasValue)
                {
                    return own.Value;
                }
            }

            return NoContext;
        }

        private static byte? OwnContext(SurfaceMetadata metadata, CustomAttributeHandleCollection attributes)
        {
            foreach (CustomAttributeHandle handle in attributes)
            {
                CustomAttribute attribute = metadata.Reader.GetCustomAttribute(handle);
                if (metadata.NullableKindOf(attribute) == NullableAttributeKind.Context)
                {
                    byte[] values = AttributeValues(metadata, attribute);
                    if (values.Length != 1)
                    {
                        throw new InvalidOperationException("a nullable context holds " + values.Length + " values");
                    }

                    return values[0];
                }
            }

            return null;
        }

        // The row's own annotation, else the context that applies to it. A uniform array reads as its
        // single value, since the compiler writes it either way.
        private static string Effective(SurfaceMetadata metadata, IEnumerable<CustomAttributeHandle> attributes, byte context)
        {
            byte[] own = null;
            foreach (CustomAttributeHandle handle in attributes)
            {
                CustomAttribute attribute = metadata.Reader.GetCustomAttribute(handle);
                if (metadata.NullableKindOf(attribute) != NullableAttributeKind.Nullable)
                {
                    continue;
                }
                if (own != null)
                {
                    throw new InvalidOperationException("a row carries two nullable attributes");
                }
                own = AttributeValues(metadata, attribute);
            }

            if (own == null)
            {
                return "u:" + context.ToString(CultureInfo.InvariantCulture);
            }
            if (own.Length > 0 && own.All(value => value == own[0]))
            {
                return "u:" + own[0].ToString(CultureInfo.InvariantCulture);
            }

            return "a:" + SurfaceTypeNames.BytesHex(own);
        }

        // Reads the single byte or byte array a nullable attribute was constructed with, and refuses any
        // other shape.
        private static byte[] AttributeValues(SurfaceMetadata metadata, CustomAttribute attribute)
        {
            MethodDefinition constructor = metadata.Reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);
            MethodSignature<SurfaceType> signature = constructor.DecodeSignature(metadata.Provider, null);
            BlobReader blob = metadata.Reader.GetBlobReader(attribute.Value);
            if (signature.ParameterTypes.Length != 1 || blob.ReadUInt16() != AttributeBlobProlog)
            {
                throw new InvalidOperationException("a nullable attribute has an unexpected constructor");
            }

            string parameterType = signature.ParameterTypes[0].Name;
            if (parameterType == PrimitiveTypeCode.Byte.ToString())
            {
                return new[] { blob.ReadByte() };
            }
            if (parameterType != PrimitiveTypeCode.Byte + "[]")
            {
                throw new InvalidOperationException("a nullable attribute is constructed from " + parameterType);
            }

            int count = blob.ReadInt32();
            if (count < 0)
            {
                throw new InvalidOperationException("a nullable attribute is constructed from a null array");
            }

            return blob.ReadBytes(count);
        }
    }
}
