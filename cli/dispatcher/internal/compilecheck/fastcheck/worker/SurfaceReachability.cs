using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;

namespace UnityCliLoop.CompileCheckWorker
{
    /// <summary>
    /// Finds the private nested types of a reference assembly that nothing a dependent can see refers
    /// to: the compiler's closure, async and iterator types, and private helpers. A dependent cannot
    /// use such a type, so whether it exists never decides whether a dependent compiles; it only
    /// decides which error a dependent that already names it reports (CS0122 rather than CS0246 or
    /// CS0426). One a kept row names - an interface a public type implements over it, the type of a
    /// private field a struct keeps - does reach it, and stays.
    /// </summary>
    internal sealed class SurfaceReachability : ISignatureTypeProvider<object, object>
    {
        private readonly HashSet<TypeDefinitionHandle> referenced = new HashSet<TypeDefinitionHandle>();

        private SurfaceReachability()
        {
        }

        /// <summary>
        /// Lists every type left out of the surface for being private-nested, itself or through a type
        /// around it, without any kept row referring to it or to a type around it.
        /// </summary>
        internal static HashSet<TypeDefinitionHandle> UnreferencedPrivateNestedTypes(
            MetadataReader reader, HashSet<TypeDefinitionHandle> embeddedTypes)
        {
            SurfaceReachability reachability = new SurfaceReachability();
            // Why to a fixpoint: a private nested type a kept row names is kept whole, and what its own
            // members name is then kept in turn.
            int before = -1;
            while (reachability.referenced.Count != before)
            {
                before = reachability.referenced.Count;
                foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
                {
                    if (!embeddedTypes.Contains(handle) && reachability.IsKept(reader, handle))
                    {
                        reachability.VisitType(reader, handle);
                    }
                }
            }

            HashSet<TypeDefinitionHandle> unreferenced = new HashSet<TypeDefinitionHandle>();
            foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
            {
                if (!reachability.IsKept(reader, handle))
                {
                    unreferenced.Add(handle);
                }
            }

            return unreferenced;
        }

        public object GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            for (TypeDefinitionHandle current = handle; !current.IsNil; current = reader.GetTypeDefinition(current).GetDeclaringType())
            {
                if (IsPrivateNested(reader, current))
                {
                    referenced.Add(current);
                }
            }

            return null;
        }

        public object GetTypeFromSpecification(MetadataReader reader, object genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
        }

        public object GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => null;

        public object GetPrimitiveType(PrimitiveTypeCode typeCode) => null;

        public object GetGenericInstantiation(object genericType, ImmutableArray<object> typeArguments) => null;

        public object GetSZArrayType(object elementType) => null;

        public object GetArrayType(object elementType, ArrayShape shape) => null;

        public object GetByReferenceType(object elementType) => null;

        public object GetPointerType(object elementType) => null;

        public object GetPinnedType(object elementType) => null;

        public object GetModifiedType(object modifier, object unmodifiedType, bool isRequired) => null;

        public object GetFunctionPointerType(MethodSignature<object> signature) => null;

        public object GetGenericTypeParameter(object genericContext, int index) => null;

        public object GetGenericMethodParameter(object genericContext, int index) => null;

        private static bool IsPrivateNested(MetadataReader reader, TypeDefinitionHandle handle)
        {
            return (reader.GetTypeDefinition(handle).Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.NestedPrivate;
        }

        // Kept when the type or a type around it is referenced, which keeps everything inside it, and
        // otherwise unless the type or a type around it is private-nested.
        private bool IsKept(MetadataReader reader, TypeDefinitionHandle handle)
        {
            bool underPrivateNested = false;
            for (TypeDefinitionHandle current = handle; !current.IsNil; current = reader.GetTypeDefinition(current).GetDeclaringType())
            {
                if (referenced.Contains(current))
                {
                    return true;
                }
                underPrivateNested |= IsPrivateNested(reader, current);
            }

            return !underPrivateNested;
        }

        private void VisitType(MetadataReader reader, TypeDefinitionHandle handle)
        {
            TypeDefinition type = reader.GetTypeDefinition(handle);
            VisitEntity(reader, type.BaseType);
            foreach (InterfaceImplementationHandle implementation in type.GetInterfaceImplementations())
            {
                VisitEntity(reader, reader.GetInterfaceImplementation(implementation).Interface);
            }
            VisitConstraints(reader, type.GetGenericParameters());
            foreach (MethodImplementationHandle implementation in type.GetMethodImplementations())
            {
                VisitMember(reader, reader.GetMethodImplementation(implementation).MethodDeclaration);
            }
            foreach (FieldDefinitionHandle field in type.GetFields())
            {
                reader.GetFieldDefinition(field).DecodeSignature(this, null);
            }
            foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
            {
                MethodDefinition method = reader.GetMethodDefinition(methodHandle);
                method.DecodeSignature(this, null);
                VisitConstraints(reader, method.GetGenericParameters());
            }
            foreach (PropertyDefinitionHandle property in type.GetProperties())
            {
                reader.GetPropertyDefinition(property).DecodeSignature(this, null);
            }
            foreach (EventDefinitionHandle eventHandle in type.GetEvents())
            {
                VisitEntity(reader, reader.GetEventDefinition(eventHandle).Type);
            }
        }

        private void VisitConstraints(MetadataReader reader, GenericParameterHandleCollection parameters)
        {
            foreach (GenericParameterHandle parameter in parameters)
            {
                foreach (GenericParameterConstraintHandle constraint in reader.GetGenericParameter(parameter).GetConstraints())
                {
                    VisitEntity(reader, reader.GetGenericParameterConstraint(constraint).Type);
                }
            }
        }

        // The interface method an implementation declares, which can sit on a private nested interface.
        private void VisitMember(MetadataReader reader, EntityHandle member)
        {
            if (member.Kind == HandleKind.MemberReference)
            {
                VisitEntity(reader, reader.GetMemberReference((MemberReferenceHandle)member).Parent);
            }
            if (member.Kind == HandleKind.MethodDefinition)
            {
                GetTypeFromDefinition(reader, reader.GetMethodDefinition((MethodDefinitionHandle)member).GetDeclaringType(), 0);
            }
        }

        private void VisitEntity(MetadataReader reader, EntityHandle handle)
        {
            if (handle.Kind == HandleKind.TypeDefinition)
            {
                GetTypeFromDefinition(reader, (TypeDefinitionHandle)handle, 0);
            }
            if (handle.Kind == HandleKind.TypeSpecification)
            {
                reader.GetTypeSpecification((TypeSpecificationHandle)handle).DecodeSignature(this, null);
            }
        }
    }
}
