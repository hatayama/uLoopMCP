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
        internal static void Write(SurfaceMetadata metadata, SurfaceDescription description)
        {
            SurfaceManifestWriter.Write(metadata, description);
            foreach (TypeDefinitionHandle handle in metadata.Reader.TypeDefinitions)
            {
                if (!metadata.IsLeftOut(handle))
                {
                    WriteType(metadata, handle, description);
                }
            }
        }

        // Why the base type has a row of its own: a change of it alone then reads as a change of the
        // type's shape, apart from its flags and layout.
        private static void WriteType(SurfaceMetadata metadata, TypeDefinitionHandle handle, SurfaceDescription description)
        {
            MetadataReader reader = metadata.Reader;
            TypeDefinition type = reader.GetTypeDefinition(handle);
            SurfaceTypeOwners owners = new SurfaceTypeOwners(metadata, handle);
            string name = owners.TypeName;
            TypeLayout layout = type.GetLayout();
            description.Add("type " + name + " flags=" + type.Attributes
                + " layout=" + layout.PackingSize.ToString(CultureInfo.InvariantCulture) + "," + layout.Size.ToString(CultureInfo.InvariantCulture),
                owners.Type());
            description.Add(name + " base " + metadata.TypeName(type.BaseType), owners.Shape());
            WriteGenericParameters(metadata, type.GetGenericParameters(), name, owners.Shape(), description);
            description.AddPrefixed(name + " ", InterfaceEntries(metadata, type), owners.Shape());
            description.AddPrefixed(name + " ", SurfaceManifestWriter.SecurityLines(reader, type.GetDeclarativeSecurityAttributes()), owners.Type());
            description.AddPrefixed(name + " ", metadata.AttributeLines(type.GetCustomAttributes()), owners.Type());
            description.AddPrefixed(name + " ", ImplementationEntries(metadata, type), owners.Type());
            foreach (FieldDefinitionHandle field in type.GetFields())
            {
                WriteField(metadata, name, field, owners.Field(field), description);
            }
            foreach (MethodDefinitionHandle method in type.GetMethods())
            {
                WriteMethod(metadata, method, owners.Method(method), description);
            }
            foreach (PropertyDefinitionHandle property in type.GetProperties())
            {
                WriteProperty(metadata, name, property, owners.Property(property), description);
            }
            foreach (EventDefinitionHandle eventHandle in type.GetEvents())
            {
                WriteEvent(metadata, name, eventHandle, owners.Event(eventHandle), description);
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

        private static void WriteGenericParameters(SurfaceMetadata metadata, GenericParameterHandleCollection parameters,
            string owner, SurfaceOwner rowOwner, SurfaceDescription description)
        {
            MetadataReader reader = metadata.Reader;
            foreach (GenericParameterHandle handle in parameters)
            {
                GenericParameter parameter = reader.GetGenericParameter(handle);
                string name = owner + " !" + parameter.Index.ToString(CultureInfo.InvariantCulture);
                description.Add(name + " " + reader.GetString(parameter.Name) + " flags=" + parameter.Attributes, rowOwner);
                description.AddPrefixed(name + " ", metadata.AttributeLines(parameter.GetCustomAttributes()), rowOwner);
                List<string> constraints = new List<string>();
                foreach (GenericParameterConstraintHandle constraintHandle in parameter.GetConstraints())
                {
                    GenericParameterConstraint constraint = reader.GetGenericParameterConstraint(constraintHandle);
                    constraints.Add("constraint " + metadata.TypeName(constraint.Type)
                        + " [" + string.Join("; ", metadata.AttributeLines(constraint.GetCustomAttributes())) + "]");
                }
                constraints.Sort(StringComparer.Ordinal);
                description.AddPrefixed(name + " ", constraints, rowOwner);
            }
        }

        private static void WriteField(
            SurfaceMetadata metadata, string typeName, FieldDefinitionHandle handle, SurfaceOwner owner, SurfaceDescription description)
        {
            MetadataReader reader = metadata.Reader;
            FieldDefinition field = reader.GetFieldDefinition(handle);
            string name = "field " + typeName + "::" + reader.GetString(field.Name);
            description.Add(name + " flags=" + field.Attributes + " type=" + field.DecodeSignature(metadata.Provider, null).Name
                + " const=" + ConstantText(reader, field.GetDefaultValue())
                + " marshal=" + SurfaceTypeNames.BlobHex(reader, field.GetMarshallingDescriptor())
                + " offset=" + field.GetOffset().ToString(CultureInfo.InvariantCulture)
                + " rva=" + (field.GetRelativeVirtualAddress() != 0), owner);
            description.AddPrefixed(name + " ", metadata.AttributeLines(field.GetCustomAttributes()), owner);
        }

        private static void WriteMethod(SurfaceMetadata metadata, MethodDefinitionHandle handle, SurfaceOwner owner, SurfaceDescription description)
        {
            MetadataReader reader = metadata.Reader;
            MethodDefinition method = reader.GetMethodDefinition(handle);
            string name = "method " + metadata.MethodName(handle);
            description.Add(name + " flags=" + method.Attributes + " impl=" + method.ImplAttributes + ImportText(reader, method.GetImport()), owner);
            WriteGenericParameters(metadata, method.GetGenericParameters(), name, owner, description);
            foreach (ParameterHandle parameterHandle in method.GetParameters())
            {
                description.Add(name + " " + ParameterLine(metadata, reader.GetParameter(parameterHandle)), owner);
            }
            description.AddPrefixed(name + " ", SurfaceManifestWriter.SecurityLines(reader, method.GetDeclarativeSecurityAttributes()), owner);
            description.AddPrefixed(name + " ", metadata.AttributeLines(method.GetCustomAttributes()), owner);
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

        private static void WriteProperty(
            SurfaceMetadata metadata, string typeName, PropertyDefinitionHandle handle, SurfaceOwner owner, SurfaceDescription description)
        {
            MetadataReader reader = metadata.Reader;
            PropertyDefinition property = reader.GetPropertyDefinition(handle);
            PropertyAccessors accessors = property.GetAccessors();
            string name = "property " + typeName + "::" + reader.GetString(property.Name);
            description.Add(name + " flags=" + property.Attributes
                + " sig=" + SurfaceTypeNames.Signature(property.DecodeSignature(metadata.Provider, null))
                + " const=" + ConstantText(reader, property.GetDefaultValue())
                + " get=" + AccessorName(metadata, accessors.Getter) + " set=" + AccessorName(metadata, accessors.Setter)
                + " other=[" + string.Join("; ", OtherAccessorNames(metadata, accessors.Others)) + "]", owner);
            description.AddPrefixed(name + " ", metadata.AttributeLines(property.GetCustomAttributes()), owner);
        }

        private static void WriteEvent(
            SurfaceMetadata metadata, string typeName, EventDefinitionHandle handle, SurfaceOwner owner, SurfaceDescription description)
        {
            MetadataReader reader = metadata.Reader;
            EventDefinition eventDefinition = reader.GetEventDefinition(handle);
            EventAccessors accessors = eventDefinition.GetAccessors();
            string name = "event " + typeName + "::" + reader.GetString(eventDefinition.Name);
            description.Add(name + " flags=" + eventDefinition.Attributes + " type=" + metadata.TypeName(eventDefinition.Type)
                + " add=" + AccessorName(metadata, accessors.Adder) + " remove=" + AccessorName(metadata, accessors.Remover)
                + " raise=" + AccessorName(metadata, accessors.Raiser)
                + " other=[" + string.Join("; ", OtherAccessorNames(metadata, accessors.Others)) + "]", owner);
            description.AddPrefixed(name + " ", metadata.AttributeLines(eventDefinition.GetCustomAttributes()), owner);
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
