namespace UnityCliLoop.CompileCheckWorker
{
    /// <summary>
    /// What kind of row of a surface description a line is, as far as telling a change by names goes.
    /// </summary>
    internal enum SurfaceOwnerKind
    {
        // First, so a row added without an owner reads as one no name tells.
        Manifest,
        Type,
        TypeShape,
        PatternBoundType,
        ValueTypeInstanceField,
        ParameterlessConstructor,
        Indexer,
        AbstractMember,
        ExtensionMethod,
        Member,
    }

    /// <summary>
    /// The type and member a line of a surface description belongs to, and what kind of row it is.
    /// An empty type name is a row of the assembly itself; an empty member name is a row of the type.
    /// </summary>
    internal readonly struct SurfaceOwner
    {
        private SurfaceOwner(string typeName, string memberName, SurfaceOwnerKind kind)
        {
            TypeName = typeName;
            MemberName = memberName;
            Kind = kind;
        }

        public string TypeName { get; }

        public string MemberName { get; }

        public SurfaceOwnerKind Kind { get; }

        public static SurfaceOwner Manifest => new SurfaceOwner("", "", SurfaceOwnerKind.Manifest);

        public static SurfaceOwner ForType(string typeName)
        {
            return new SurfaceOwner(typeName, "", SurfaceOwnerKind.Type);
        }

        public static SurfaceOwner ForTypeShape(string typeName)
        {
            return new SurfaceOwner(typeName, "", SurfaceOwnerKind.TypeShape);
        }

        public static SurfaceOwner ForPatternBoundType(string typeName)
        {
            return new SurfaceOwner(typeName, "", SurfaceOwnerKind.PatternBoundType);
        }

        public static SurfaceOwner ForMember(string typeName, string memberName)
        {
            return new SurfaceOwner(typeName, memberName, SurfaceOwnerKind.Member);
        }

        public static SurfaceOwner ForAbstractMember(string typeName, string memberName)
        {
            return new SurfaceOwner(typeName, memberName, SurfaceOwnerKind.AbstractMember);
        }

        public static SurfaceOwner ForExtensionMethod(string typeName, string memberName)
        {
            return new SurfaceOwner(typeName, memberName, SurfaceOwnerKind.ExtensionMethod);
        }

        public static SurfaceOwner ForValueTypeInstanceField(string typeName, string memberName)
        {
            return new SurfaceOwner(typeName, memberName, SurfaceOwnerKind.ValueTypeInstanceField);
        }

        public static SurfaceOwner ForParameterlessConstructor(string typeName)
        {
            return new SurfaceOwner(typeName, ".ctor", SurfaceOwnerKind.ParameterlessConstructor);
        }

        public static SurfaceOwner ForIndexer(string typeName, string memberName)
        {
            return new SurfaceOwner(typeName, memberName, SurfaceOwnerKind.Indexer);
        }
    }
}
