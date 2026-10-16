using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityCliLoop.CompileCheckWorker
{
    /// <summary>
    /// Tells what two different surfaces of one assembly changed, as the identifiers a dependent has to
    /// write in its source to be affected: type names, member names, and "new(" for a constructor a
    /// target-typed new can reach. A change a dependent can feel without writing any name - an operator,
    /// a base type, an abstract member added, a member C# binds by pattern - is unnamed instead, and an
    /// unnamed change carries no names at all, so a reader cannot narrow by them by mistake.
    /// </summary>
    internal sealed class ChangedSurfaceNames
    {
        // Beyond this many names, matching them against every dependent costs more than it saves.
        internal const int MaximumNames = 2000;
        private const int ReportedReasons = 5;
        private const int ManifestTextLength = 60;
        private const string ConstructorName = ".ctor";
        private const string TargetTypedNew = "new(";
        private const string AttributeSuffix = "Attribute";
        private const string OperatorPrefix = "op_";
        private static readonly string[] AccessorPrefixes = { "get_", "set_", "add_", "remove_", "raise_" };

        // Members C# binds without the dependent writing their names: foreach, await, using, collection
        // initializers, deconstruction, indexers, fixed, ranges and list patterns, LINQ query syntax,
        // delegate invocation, async method builders, interpolated string handlers, collection builders.
        private static readonly HashSet<string> BoundByPattern = new HashSet<string>(StringComparer.Ordinal)
        {
            "Add", "Dispose", "DisposeAsync", "GetEnumerator", "GetAsyncEnumerator", "MoveNext", "MoveNextAsync",
            "Current", "GetAwaiter", "GetResult", "IsCompleted", "OnCompleted", "UnsafeOnCompleted",
            "GetPinnableReference", "Slice", "Length", "Count", "Deconstruct", "Invoke", "Item", "Select",
            "SelectMany", "Where", "Join", "GroupJoin", "OrderBy", "OrderByDescending", "ThenBy",
            "ThenByDescending", "GroupBy", "Cast", "Create", "Start", "SetResult", "SetException",
            "SetStateMachine", "AwaitOnCompleted", "AwaitUnsafeOnCompleted", "Task", "AppendLiteral",
            "AppendFormatted",
        };

        private readonly SortedSet<string> names = new SortedSet<string>(StringComparer.Ordinal);
        // In the order first met, each once: one member's change shows on several rows.
        private readonly List<string> reasons = new List<string>();
        private readonly HashSet<string> reasonSet = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> producedTypes;
        private readonly HashSet<string> unityBuildTypes;
        private readonly HashSet<string> producedNamespaces;
        private readonly HashSet<string> unityBuildNamespaces;

        private ChangedSurfaceNames(SurfaceDescription produced, SurfaceDescription unityBuild)
        {
            producedTypes = TypeNames(produced);
            unityBuildTypes = TypeNames(unityBuild);
            producedNamespaces = NamespacePrefixes(producedTypes);
            unityBuildNamespaces = NamespacePrefixes(unityBuildTypes);
        }

        // Sorted ordinal, without duplicates; empty when the change is unnamed.
        public string[] Names { get; private set; } = Array.Empty<string>();

        // Why the change cannot be told by names; empty when it can.
        public string UnnamedReason { get; private set; } = "";

        /// <summary>
        /// Tells two descriptions apart by the rows whose text one holds more times than the other. Call
        /// it only for descriptions that differ.
        /// </summary>
        public static ChangedSurfaceNames Collect(SurfaceDescription produced, SurfaceDescription unityBuild)
        {
            ChangedSurfaceNames result = new ChangedSurfaceNames(produced, unityBuild);
            Dictionary<string, int> producedCounts = Counts(produced);
            Dictionary<string, int> unityBuildCounts = Counts(unityBuild);
            bool any = false;
            // Why rows are counted rather than walked in order: one member added shifts every row after it.
            foreach (SurfaceEntry entry in produced.Entries)
            {
                if (Count(producedCounts, entry.Text) != Count(unityBuildCounts, entry.Text))
                {
                    any = true;
                    result.Read(entry, true);
                }
            }
            foreach (SurfaceEntry entry in unityBuild.Entries)
            {
                if (Count(producedCounts, entry.Text) != Count(unityBuildCounts, entry.Text))
                {
                    any = true;
                    result.Read(entry, false);
                }
            }

            return result.Finish(any);
        }

        private ChangedSurfaceNames Finish(bool anyRowDiffers)
        {
            if (!anyRowDiffers)
            {
                UnnamedReason = "the descriptions differ only in order";
            }
            else if (names.Count > MaximumNames)
            {
                UnnamedReason = "too many changed names (" + names.Count + ")";
            }
            else if (reasons.Count > 0)
            {
                UnnamedReason = string.Join("; ", reasons.Take(ReportedReasons));
            }
            else
            {
                Names = names.ToArray();
            }

            return this;
        }

        private void Read(SurfaceEntry entry, bool producedSide)
        {
            SurfaceOwner owner = entry.Owner;
            if (owner.Kind == SurfaceOwnerKind.Manifest)
            {
                AddReason("manifest: " + (entry.Text.Length > ManifestTextLength ? entry.Text.Substring(0, ManifestTextLength) : entry.Text));
                return;
            }
            if (producedTypes.Contains(owner.TypeName) != unityBuildTypes.Contains(owner.TypeName))
            {
                ReadOneSidedType(owner);
                return;
            }

            switch (owner.Kind)
            {
                case SurfaceOwnerKind.AbstractMember when producedSide:
                    AddReason("abstract member added: " + owner.TypeName + "::" + owner.MemberName);
                    return;
                case SurfaceOwnerKind.Member:
                case SurfaceOwnerKind.AbstractMember:
                case SurfaceOwnerKind.ExtensionMethod:
                    AddTypeNames(owner.TypeName);
                    AddMemberName(owner.MemberName);
                    return;
                default:
                    // A row of a type both sides have that a dependent can feel without writing a name.
                    AddReason(owner.Kind + " of " + owner.TypeName);
                    return;
            }
        }

        // A type added, removed or renamed: a dependent reaches it only by writing its name or through
        // another member of the assembly, whose own row then changes too. An extension method is the
        // exception: it binds by pattern on a type it does not belong to.
        private void ReadOneSidedType(SurfaceOwner owner)
        {
            AddTypeNames(owner.TypeName);
            if (owner.Kind == SurfaceOwnerKind.ExtensionMethod)
            {
                AddMemberName(owner.MemberName);
                return;
            }
            if (owner.MemberName.Length == 0 || owner.MemberName.StartsWith(".", StringComparison.Ordinal))
            {
                return;
            }

            string member = NormalizeMember(owner.MemberName);
            if (IsCSharpIdentifier(member) && !member.StartsWith(OperatorPrefix, StringComparison.Ordinal))
            {
                names.Add(member);
            }
        }

        private void AddReason(string reason)
        {
            if (reasonSet.Add(reason))
            {
                reasons.Add(reason);
            }
        }

        private void AddMemberName(string member)
        {
            if (member == ConstructorName)
            {
                names.Add(TargetTypedNew);
                return;
            }
            if (member == ".cctor" || member == "Finalize")
            {
                return;
            }

            member = NormalizeMember(member);
            if (member.StartsWith(OperatorPrefix, StringComparison.Ordinal))
            {
                AddReason("operator " + member);
            }
            else if (!IsCSharpIdentifier(member))
            {
                AddReason("compiler-generated member " + member);
            }
            else if (BoundByPattern.Contains(member))
            {
                AddReason("pattern-bound member " + member);
            }
            else
            {
                names.Add(member);
            }
        }

        // "A.B.Outer`1/Inner" names Outer and Inner, and A or B only where one side lacks that namespace:
        // nearly every dependent writes a using for a namespace both sides have.
        private void AddTypeNames(string metadataName)
        {
            foreach (string nested in metadataName.Split('/'))
            {
                int arity = nested.IndexOf('`');
                string[] segments = (arity < 0 ? nested : nested.Substring(0, arity)).Split('.');
                string simple = segments[segments.Length - 1];
                names.Add(simple);
                if (simple.EndsWith(AttributeSuffix, StringComparison.Ordinal) && simple.Length > AttributeSuffix.Length)
                {
                    names.Add(simple.Substring(0, simple.Length - AttributeSuffix.Length));
                }
                for (int index = 0; index < segments.Length - 1; index++)
                {
                    string prefix = string.Join(".", segments, 0, index + 1);
                    if (producedNamespaces.Contains(prefix) != unityBuildNamespaces.Contains(prefix))
                    {
                        names.Add(segments[index]);
                    }
                }
            }
        }

        // An explicit implementation "Ns.IFoo.get_P" is written as P.
        private static string NormalizeMember(string member)
        {
            int dot = member.LastIndexOf('.');
            if (dot >= 0)
            {
                member = member.Substring(dot + 1);
            }
            foreach (string prefix in AccessorPrefixes)
            {
                if (member.StartsWith(prefix, StringComparison.Ordinal) && member.Length > prefix.Length)
                {
                    return member.Substring(prefix.Length);
                }
            }

            return member;
        }

        internal static bool IsCSharpIdentifier(string text)
        {
            if (text.Length == 0 || !(char.IsLetter(text[0]) || text[0] == '_'))
            {
                return false;
            }

            return text.All(character => char.IsLetterOrDigit(character) || character == '_');
        }

        private static HashSet<string> TypeNames(SurfaceDescription description)
        {
            HashSet<string> types = new HashSet<string>(StringComparer.Ordinal);
            foreach (SurfaceEntry entry in description.Entries)
            {
                if (!string.IsNullOrEmpty(entry.Owner.TypeName))
                {
                    types.Add(entry.Owner.TypeName);
                }
            }

            return types;
        }

        // The namespaces "A.B.C" and "A.B.C/D" sit in: "A" and "A.B".
        private static HashSet<string> NamespacePrefixes(HashSet<string> types)
        {
            HashSet<string> prefixes = new HashSet<string>(StringComparer.Ordinal);
            foreach (string type in types)
            {
                string outer = type.Split('/')[0];
                int arity = outer.IndexOf('`');
                string[] segments = (arity < 0 ? outer : outer.Substring(0, arity)).Split('.');
                for (int index = 0; index < segments.Length - 1; index++)
                {
                    prefixes.Add(string.Join(".", segments, 0, index + 1));
                }
            }

            return prefixes;
        }

        private static Dictionary<string, int> Counts(SurfaceDescription description)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string text in description.Texts)
            {
                counts[text] = Count(counts, text) + 1;
            }

            return counts;
        }

        private static int Count(Dictionary<string, int> counts, string text)
        {
            return counts.TryGetValue(text, out int count) ? count : 0;
        }
    }
}
