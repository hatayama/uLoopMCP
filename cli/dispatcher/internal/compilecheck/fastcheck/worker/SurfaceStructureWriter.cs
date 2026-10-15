using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection.Metadata;

namespace UnityCliLoop.CompileCheckWorker
{
    /// <summary>
    /// Writes every type and member of a reference assembly a dependent can see, line by line, naming
    /// what each row points at instead of writing its row number. Types and members keep the order of
    /// their tables, so a reordering reads as a change; what hangs off one row and has no order in C# -
    /// attributes, interfaces, constraints, explicit implementations - is sorted. The compiler's
    /// nullable attributes are left to SurfaceNullabilityWriter.
    /// </summary>
    internal static class SurfaceStructureWriter
    {
        internal static void Write(SurfaceMetadata metadata, List<string> lines)
        {
            SurfaceManifestWriter.Write(metadata, lines);
            foreach (TypeDefinitionHandle handle in metadata.Reader.TypeDefinitions)
            {
                if (!metadata.IsLeftOut(handle))
                {
                    WriteType(metadata, handle, lines);
                }
            }
        }

        private static void WriteType(SurfaceMetadata metadata, TypeDefinitionHandle handle, List<string> lines)
        {
            MetadataReader reader = metadata.Reader;
            TypeDefinition type = reader.GetTypeDefinition(handle);
            string name = SurfaceTypeNames.Definition(reader, handle);
            TypeLayout layout = type.GetLayout();
            lines.Add("type " + name + " flags=" + type.Attributes + " base=" + metadata.TypeName(type.BaseType)
                + " layout=" + layout.PackingSize.ToString(CultureInfo.InvariantCulture) + "," + layout.Size.ToString(CultureInfo.InvariantCulture));
            WriteGenericParameters(metadata, type.GetGenericParameters(), name, lines);
            SurfaceManifestWriter.AddPrefixed(lines, name + " ", InterfaceEntries(metadata, type));
            SurfaceManifestWriter.AddPrefixed(lines, name + " ", SurfaceManifestWriter.SecurityLines(reader, type.GetDeclarativeSecurityAttributes()));
            SurfaceManifestWriter.AddPrefixed(lines, name + " ", metadata.AttributeLines(type.GetCustomAttributes()));
            SurfaceManifestWriter.AddPrefixed(lines, name + " ", ImplementationEntries(metadata, type));
            foreach (FieldDefinitionHandle field in type.GetFields())
            {
                WriteField(metadata, name, field, lines);
            }
            foreach (MethodDefinitionHandle method in type.GetMethods())
            {
                WriteMethod(metadata, method, lines);
            }
            foreach (PropertyDefinitionHandle property in type.GetProperties())
            {
                WriteProperty(metadata, name, property, lines);
            }
            foreach (EventDefinitionHandle eventHandle in type.GetEvents())
            {
                WriteEvent(metadata, name, eventHandle, lines);
            }
        }

        private static List<string> InterfaceEntries(SurfaceMetadata metadata, TypeDefinition type)
        {
            List<string> entries = new List<string>();
            foreach (InterfaceImplementationHandle handle in type.GetInterfaceImplementations())
            {
                InterfaceImplementation implementation = metadata.Reader.GetInterfaceImplementation(handle);
                entries.Add("implements " + metadata.TypeName(implementation.Interface)
                    + " [" + string.Join("; ", metadata.AttributeLines(implementation.GetCustomAttributes())) + "]");
            }
            entries.Sort(StringComparer.Ordinal);

            return entries;
        }

        private static List<string> ImplementationEntries(SurfaceMetadata metadata, TypeDefinition type)
        {
            List<string> entries = new List<string>();
            foreach (MethodImplementationHandle handle in type.GetMethodImplementations())
            {
                MethodImplementation implementation = metadata.Reader.GetMethodImplementation(handle);
                entries.Add("methodimpl " + SurfaceTypeNames.Member(metadata.Reader, metadata.Provider, implementation.MethodBody)
                    + " overrides " + SurfaceTypeNames.Member(metadata.Reader, metadata.Provider, implementation.MethodDeclaration));
            }
            entries.Sort(StringComparer.Ordinal);

            return entries;
        }

        private static void WriteGenericParameters(
            SurfaceMetadata metadata, GenericParameterHandleCollection parameters, string owner, List<string> lines)
        {
            MetadataReader reader = metadata.Reader;
            foreach (GenericParameterHandle handle in parameters)
            {
                GenericParameter parameter = reader.GetGenericParameter(handle);
                string name = owner + " !" + parameter.Index.ToString(CultureInfo.InvariantCulture);
                lines.Add(name + " " + reader.GetString(parameter.Name) + " flags=" + parameter.Attributes);
                SurfaceManifestWriter.AddPrefixed(lines, name + " ", metadata.AttributeLines(parameter.GetCustomAttributes()));
                List<string> constraints = new List<string>();
                foreach (GenericParameterConstraintHandle constraintHandle in parameter.GetConstraints())
                {
                    GenericParameterConstraint constraint = reader.GetGenericParameterConstraint(constraintHandle);
                    constraints.Add("constraint " + metadata.TypeName(constraint.Type)
                        + " [" + string.Join("; ", metadata.AttributeLines(constraint.GetCustomAttributes())) + "]");
                }
                constraints.Sort(StringComparer.Ordinal);
                SurfaceManifestWriter.AddPrefixed(lines, name + " ", constraints);
            }
        }

        private static void WriteField(SurfaceMetadata metadata, string typeName, FieldDefinitionHandle handle, List<string> lines)
        {
            MetadataReader reader = metadata.Reader;
            FieldDefinition field = reader.GetFieldDefinition(handle);
            string name = "field " + typeName + "::" + reader.GetString(field.Name);
            lines.Add(name + " flags=" + field.Attributes + " type=" + field.DecodeSignature(metadata.Provider, null).Name
                + " const=" + ConstantText(reader, field.GetDefaultValue())
                + " marshal=" + SurfaceTypeNames.BlobHex(reader, field.GetMarshallingDescriptor())
                + " offset=" + field.GetOffset().ToString(CultureInfo.InvariantCulture)
                + " rva=" + (field.GetRelativeVirtualAddress() != 0));
            SurfaceManifestWriter.AddPrefixed(lines, name + " ", metadata.AttributeLines(field.GetCustomAttributes()));
        }

        private static void WriteMethod(SurfaceMetadata metadata, MethodDefinitionHandle handle, List<string> lines)
        {
            MetadataReader reader = metadata.Reader;
            MethodDefinition method = reader.GetMethodDefinition(handle);
            string name = "method " + metadata.MethodName(handle);
            lines.Add(name + " flags=" + method.Attributes + " impl=" + method.ImplAttributes + ImportText(reader, method.GetImport()));
            WriteGenericParameters(metadata, method.GetGenericParameters(), name, lines);
            foreach (ParameterHandle parameterHandle in method.GetParameters())
            {
                lines.Add(name + " " + ParameterLine(metadata, reader.GetParameter(parameterHandle)));
            }
            SurfaceManifestWriter.AddPrefixed(lines, name + " ", SurfaceManifestWriter.SecurityLines(reader, method.GetDeclarativeSecurityAttributes()));
            SurfaceManifestWriter.AddPrefixed(lines, name + " ", metadata.AttributeLines(method.GetCustomAttributes()));
        }

        // One parameter row, the return value's included. Why a return-value row that only carries a
        // nullable attribute is kept: a method's nullable context follows its own signature alone, so
        // whether that row exists never moves with members a dependent cannot see.
        private static string ParameterLine(SurfaceMetadata metadata, Parameter parameter)
        {
            MetadataReader reader = metadata.Reader;
            List<string> attributes = metadata.AttributeLines(parameter.GetCustomAttributes());

            return "param #" + parameter.SequenceNumber.ToString(CultureInfo.InvariantCulture)
                + " " + (parameter.Name.IsNil ? "" : reader.GetString(parameter.Name))
                + " flags=" + parameter.Attributes
                + " const=" + ConstantText(reader, parameter.GetDefaultValue())
                + " marshal=" + SurfaceTypeNames.BlobHex(reader, parameter.GetMarshallingDescriptor())
                + " [" + string.Join("; ", attributes) + "]";
        }

        private static void WriteProperty(SurfaceMetadata metadata, string typeName, PropertyDefinitionHandle handle, List<string> lines)
        {
            MetadataReader reader = metadata.Reader;
            PropertyDefinition property = reader.GetPropertyDefinition(handle);
            PropertyAccessors accessors = property.GetAccessors();
            string name = "property " + typeName + "::" + reader.GetString(property.Name);
            lines.Add(name + " flags=" + property.Attributes
                + " sig=" + SurfaceTypeNames.Signature(property.DecodeSignature(metadata.Provider, null))
                + " const=" + ConstantText(reader, property.GetDefaultValue())
                + " get=" + AccessorName(metadata, accessors.Getter) + " set=" + AccessorName(metadata, accessors.Setter)
                + " other=[" + string.Join("; ", OtherAccessorNames(metadata, accessors.Others)) + "]");
            SurfaceManifestWriter.AddPrefixed(lines, name + " ", metadata.AttributeLines(property.GetCustomAttributes()));
        }

        private static void WriteEvent(SurfaceMetadata metadata, string typeName, EventDefinitionHandle handle, List<string> lines)
        {
            MetadataReader reader = metadata.Reader;
            EventDefinition eventDefinition = reader.GetEventDefinition(handle);
            EventAccessors accessors = eventDefinition.GetAccessors();
            string name = "event " + typeName + "::" + reader.GetString(eventDefinition.Name);
            lines.Add(name + " flags=" + eventDefinition.Attributes + " type=" + metadata.TypeName(eventDefinition.Type)
                + " add=" + AccessorName(metadata, accessors.Adder) + " remove=" + AccessorName(metadata, accessors.Remover)
                + " raise=" + AccessorName(metadata, accessors.Raiser)
                + " other=[" + string.Join("; ", OtherAccessorNames(metadata, accessors.Others)) + "]");
            SurfaceManifestWriter.AddPrefixed(lines, name + " ", metadata.AttributeLines(eventDefinition.GetCustomAttributes()));
        }

        private static string AccessorName(SurfaceMetadata metadata, MethodDefinitionHandle handle)
        {
            return handle.IsNil ? "-" : metadata.MethodName(handle);
        }

        private static List<string> OtherAccessorNames(SurfaceMetadata metadata, System.Collections.Immutable.ImmutableArray<MethodDefinitionHandle> others)
        {
            List<string> names = new List<string>();
            foreach (MethodDefinitionHandle other in others)
            {
                names.Add(metadata.MethodName(other));
            }
            names.Sort(StringComparer.Ordinal);

            return names;
        }

        private static string ImportText(MetadataReader reader, MethodImport import)
        {
            if (import.Module.IsNil)
            {
                return "";
            }

            return " import=" + reader.GetString(reader.GetModuleReference(import.Module).Name)
                + "!" + reader.GetString(import.Name) + " " + import.Attributes;
        }

        private static string ConstantText(MetadataReader reader, ConstantHandle handle)
        {
            if (handle.IsNil)
            {
                return "-";
            }

            Constant constant = reader.GetConstant(handle);

            return constant.TypeCode + ":" + SurfaceTypeNames.BlobHex(reader, constant.Value);
        }
    }
}
