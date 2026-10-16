using System.Linq;
using System.Text;
using NUnit.Framework;

namespace UnityCliLoop.CompileCheckWorker.UnitTests
{
    [TestFixture]
    public sealed class ChangedSurfaceNamesTests
    {
        private const string Getter = "public static class F { public static T Get() => new T(); }\n";
        private const string StructGetter = "public static class F { public static S Get() => default; }\n";
        private const string Awaiter =
            "public sealed class Aw : System.Runtime.CompilerServices.INotifyCompletion { public bool IsCompleted => true;"
            + " public int GetResult() => 0; public void OnCompleted(System.Action a) { } }\n";
        private const string InterpolatedStringHandlerAttribute =
            "namespace System.Runtime.CompilerServices { [System.AttributeUsage(System.AttributeTargets.Struct)]"
            + " public sealed class InterpolatedStringHandlerAttribute : System.Attribute { } }\n";

        /// <summary>
        /// Verifies that adding a public method names its type and the method.
        /// </summary>
        [Test]
        public void MethodAddedNamesTheTypeAndTheMethod()
        {
            SurfaceFixture.AssertChangedNames(
                "public class C { }",
                "public class C { public void M() { } }",
                new[] { "C", "M" });
        }

        /// <summary>
        /// Verifies that a member change in a nested generic type names every level of the nesting and the
        /// member, and leaves out namespaces both versions have.
        /// </summary>
        [Test]
        public void NestedGenericMemberChangeNamesEachLevelButNotSharedNamespaces()
        {
            SurfaceFixture.AssertChangedNames(
                "namespace A.B { public class Outer<T> { public class Inner { public int P { get; set; } } } }",
                "namespace A.B { public class Outer<T> { public class Inner { public long P { get; set; } } } }",
                new[] { "Outer", "Inner", "P" });
        }

        /// <summary>
        /// Verifies that a type added in a new namespace names the type and the new namespace level, but
        /// not the level both versions have.
        /// </summary>
        [Test]
        public void TypeAddedInANewNamespaceNamesTheNewLevel()
        {
            SurfaceFixture.AssertChangedNames(
                "namespace A { public class Keep { } }",
                "namespace A { public class Keep { } } namespace A.New { public class T { } }",
                new[] { "New", "T" });
        }

        /// <summary>
        /// Verifies that removing a type names the type and its members.
        /// </summary>
        [Test]
        public void TypeRemovedNamesTheTypeAndItsMembers()
        {
            SurfaceFixture.AssertChangedNames(
                "public class Keep { } public class C { public int X; }",
                "public class Keep { }",
                new[] { "C", "X" });
        }

        /// <summary>
        /// Verifies that a change in an attribute type names it with and without the Attribute suffix.
        /// </summary>
        [Test]
        public void AttributeTypeChangeNamesItWithAndWithoutTheSuffix()
        {
            SurfaceFixture.AssertChangedNames(
                "public class FooAttribute : System.Attribute { }",
                "public class FooAttribute : System.Attribute { public int Level { get; set; } }",
                new[] { "Foo", "FooAttribute", "Level" });
        }

        /// <summary>
        /// Verifies that changing the parameters of a constructor names the type and target-typed new.
        /// </summary>
        [Test]
        public void ConstructorChangeNamesTheTypeAndTargetTypedNew()
        {
            SurfaceFixture.AssertChangedNames(
                "public class C { public C(int a) { } }",
                "public class C { public C(int a, int b) { } }",
                new[] { "C", "new(" });
        }

        /// <summary>
        /// Verifies that an accessor change names the property rather than the accessor method.
        /// </summary>
        [Test]
        public void AccessorChangeNamesThePropertyNotTheAccessor()
        {
            SurfaceFixture.AssertChangedNames(
                "public class C { public int P { get; } }",
                "public class C { public int P { get; set; } }",
                new[] { "C", "P" });
        }

        /// <summary>
        /// Verifies that a change on an explicit interface implementation names the member without its
        /// interface qualifier.
        /// </summary>
        [Test]
        public void ExplicitImplementationChangeNamesTheBareMember()
        {
            SurfaceFixture.AssertChangedNames(
                "namespace N { public interface IFoo { void Bar(); } } public class C : N.IFoo { void N.IFoo.Bar() { } }",
                "namespace N { public interface IFoo { void Bar(); } } public class C : N.IFoo { [System.Obsolete] void N.IFoo.Bar() { } }",
                new[] { "C", "Bar" });
        }

        /// <summary>
        /// Verifies that adding an operator cannot be told by names.
        /// </summary>
        [Test]
        public void OperatorAddedIsUnnamed()
        {
            SurfaceFixture.AssertUnnamed(
                "public class C { }",
                "public class C { public static C operator +(C a, C b) => a; }",
                "operator op_Addition");
        }

        /// <summary>
        /// Verifies that changing a base type cannot be told by names.
        /// </summary>
        [Test]
        public void BaseTypeChangeIsUnnamed()
        {
            SurfaceFixture.AssertUnnamed(
                "public class B { } public class C { }",
                "public class B { } public class C : B { }",
                "TypeShape of C");
        }

        /// <summary>
        /// Verifies that adding an interface to a class cannot be told by names.
        /// </summary>
        [Test]
        public void InterfaceAddedIsUnnamed()
        {
            SurfaceFixture.AssertUnnamed(
                "public interface I { } public class C { }",
                "public interface I { } public class C : I { }",
                "TypeShape of C");
        }

        /// <summary>
        /// Verifies that adding an abstract member to an abstract class cannot be told by names.
        /// </summary>
        [Test]
        public void AbstractMemberAddedIsUnnamed()
        {
            SurfaceFixture.AssertUnnamed(
                "public abstract class C { }",
                "public abstract class C { public abstract void M(); }",
                "abstract member added: C::M");
        }

        /// <summary>
        /// Verifies that adding a member without a body to an interface cannot be told by names.
        /// </summary>
        [Test]
        public void InterfaceMemberAddedIsUnnamed()
        {
            SurfaceFixture.AssertUnnamed(
                "public interface I { }",
                "public interface I { void M(); }",
                "abstract member added: I::M");
        }

        /// <summary>
        /// Verifies that adding a member that is not abstract to an interface, as one with a default body
        /// is, names the interface and the member: no implementer has to change for it. Built from rows
        /// because the .NET Standard 2.0 the fixture compiles against cannot hold a default body.
        /// </summary>
        [Test]
        public void InterfaceMemberWithABodyAddedNamesTheInterfaceAndTheMember()
        {
            SurfaceDescription unityBuild = new SurfaceDescription();
            unityBuild.Add("type I flags=Interface, Abstract", SurfaceOwner.ForType("I"));
            SurfaceDescription produced = new SurfaceDescription();
            produced.Add("type I flags=Interface, Abstract", SurfaceOwner.ForType("I"));
            produced.Add("method I::M`0 20()->Void flags=Public, Virtual", SurfaceOwner.ForMember("I", "M"));

            ChangedSurfaceNames names = ChangedSurfaceNames.Collect(produced, unityBuild);

            Assert.That(names.UnnamedReason, Is.Empty);
            Assert.That(names.Names, Is.EqualTo(new[] { "I", "M" }));
        }

        /// <summary>
        /// Verifies that removing an abstract member names the type and the member: no implementer has to
        /// change for it.
        /// </summary>
        [Test]
        public void AbstractMemberRemovedNamesTheTypeAndTheMember()
        {
            SurfaceFixture.AssertChangedNames(
                "public abstract class C { public abstract void M(); }",
                "public abstract class C { }",
                new[] { "C", "M" });
        }

        /// <summary>
        /// Verifies that adding an indexer cannot be told by names, through the default member attribute
        /// it puts on the type and through its own rows.
        /// </summary>
        [Test]
        public void IndexerAddedIsUnnamed()
        {
            SurfaceFixture.AssertUnnamed(
                "public class C { }",
                "public class C { public int this[int i] => i; }",
                "Type of C; Indexer of C");
        }

        /// <summary>
        /// Verifies that adding GetEnumerator, which foreach binds without writing it, cannot be told by
        /// names.
        /// </summary>
        [Test]
        public void GetEnumeratorAddedIsUnnamed()
        {
            SurfaceFixture.AssertUnnamed(
                "public class C { }",
                "public class C { public System.Collections.IEnumerator GetEnumerator() => null!; }",
                "pattern-bound member GetEnumerator");
        }

        /// <summary>
        /// Verifies that changing SetResult, which an async method builder binds without its name being
        /// written, cannot be told by names.
        /// </summary>
        [Test]
        public void SetResultChangeIsUnnamed()
        {
            SurfaceFixture.AssertUnnamed(
                "public class C { public void SetResult(int value) { } }",
                "public class C { public void SetResult(long value) { } }",
                "pattern-bound member SetResult");
        }

        /// <summary>
        /// Verifies that a change of a compiler-generated member, whose name no source writes, cannot be
        /// told by names.
        /// </summary>
        [Test]
        public void CompilerGeneratedMemberChangeIsUnnamed()
        {
            SurfaceDescription unityBuild = new SurfaceDescription();
            unityBuild.Add("method R::<Clone>$`0 20()->R flags=Public", SurfaceOwner.ForMember("R", "<Clone>$"));
            SurfaceDescription produced = new SurfaceDescription();
            produced.Add("method R::<Clone>$`0 20()->R flags=Public, Virtual", SurfaceOwner.ForMember("R", "<Clone>$"));

            ChangedSurfaceNames names = ChangedSurfaceNames.Collect(produced, unityBuild);

            Assert.That(names.UnnamedReason, Does.StartWith("compiler-generated member <Clone>$"));
            Assert.That(names.Names, Is.Empty);
        }

        /// <summary>
        /// Verifies that a record whose generated members change is never named, whatever reason comes
        /// first.
        /// </summary>
        [Test]
        public void RecordChangeIsUnnamed()
        {
            const string IsExternalInit = "namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }\n";
            SurfaceFixture.AssertUnnamed(
                IsExternalInit + "public record R { }",
                IsExternalInit + "public sealed record R { }",
                "");
        }

        /// <summary>
        /// Verifies that two descriptions holding the same rows in another order cannot be told by names.
        /// </summary>
        [Test]
        public void ReorderedMembersAreUnnamed()
        {
            SurfaceFixture.AssertUnnamed(
                "public class C { public void A() { } public void B() { } }",
                "public class C { public void B() { } public void A() { } }",
                "the descriptions differ only in order");
        }

        /// <summary>
        /// Verifies that a change naming more than the limit cannot be told by names.
        /// </summary>
        [Test]
        public void TooManyChangedNamesAreUnnamed()
        {
            StringBuilder fields = new StringBuilder();
            for (int index = 0; index < 2001; index++)
            {
                fields.Append("public int F").Append(index).Append("; ");
            }

            SurfaceFixture.AssertUnnamed(
                "public class C { }",
                "public class C { " + fields + "}",
                "too many changed names");
        }

        /// <summary>
        /// Verifies that a member change of an interpolated string handler type, which the compiler binds
        /// by pattern, cannot be told by names.
        /// </summary>
        [Test]
        public void InterpolatedStringHandlerMemberChangeIsUnnamed()
        {
            SurfaceFixture.AssertUnnamed(
                InterpolatedStringHandlerAttribute
                    + "[System.Runtime.CompilerServices.InterpolatedStringHandler] public struct H { public void Mark() { } }",
                InterpolatedStringHandlerAttribute
                    + "[System.Runtime.CompilerServices.InterpolatedStringHandler] public struct H { public void Mark(int level) { } }",
                "PatternBoundType of H");
        }

        /// <summary>
        /// Verifies that a dependent iterating a value it never names the type of is compiled when the
        /// enumerator that foreach binds changes.
        /// </summary>
        [Test]
        public void ForeachOverAnUnnamedTypeIsCompiled()
        {
            const string Enumerators =
                "public sealed class E { public object Current => null!; public bool MoveNext() => false; }"
                + " public sealed class E2 { public object Current => null!; }\n";
            SurfaceFixture.AssertDifferent(
                Enumerators + "public class T { public E GetEnumerator() => new E(); }\n" + Getter,
                Enumerators + "public class T { public E2 GetEnumerator() => new E2(); }\n" + Getter,
                "class D { void M() { foreach (object x in F.Get()) { } } }");
        }

        /// <summary>
        /// Verifies that a dependent awaiting a value it never names the type of is compiled when the
        /// awaiter that await binds changes.
        /// </summary>
        [Test]
        public void AwaitOnAnUnnamedTypeIsCompiled()
        {
            const string Awaiters = Awaiter
                + "public sealed class Aw2 : System.Runtime.CompilerServices.INotifyCompletion { public bool IsCompleted => true;"
                + " public string GetResult() => \"\"; public void OnCompleted(System.Action a) { } }\n";
            SurfaceFixture.AssertDifferent(
                Awaiters + "public class T { public Aw GetAwaiter() => new Aw(); }\n" + Getter,
                Awaiters + "public class T { public Aw2 GetAwaiter() => new Aw2(); }\n" + Getter,
                "class D { async System.Threading.Tasks.Task<int> M() { return await F.Get(); } }");
        }

        /// <summary>
        /// Verifies that a dependent adding two values it never names the type of is compiled when the
        /// operator it binds changes.
        /// </summary>
        [Test]
        public void OperatorOnAnUnnamedTypeIsCompiled()
        {
            SurfaceFixture.AssertDifferent(
                "public class T { public static T operator +(T a, T b) => a; }\n" + Getter,
                "public class T { public static T operator +(T a, int b) => a; }\n" + Getter,
                "class D { object M() => F.Get() + F.Get(); }");
        }

        /// <summary>
        /// Verifies that making a type abstract, which breaks a new() constraint a dependent satisfies
        /// without naming the type, cannot be told by names.
        /// </summary>
        [Test]
        public void TypeMadeAbstractIsUnnamed()
        {
            const string OldSource = "public class T { public T() { } }\n" + Getter;
            const string NewSource = "public abstract class T { public T() { } }\n"
                + "public static class F { public static T Get() => null!; }\n";
            SurfaceFixture.AssertDifferent(OldSource, NewSource,
                "class D { static void Use<U>(U u) where U : new() { } void M() { Use(F.Get()); } }");
            SurfaceFixture.AssertUnnamed(OldSource, NewSource, "Type of T");
        }

        /// <summary>
        /// Verifies that sealing a type, which breaks a cast a dependent writes without naming the type,
        /// cannot be told by names.
        /// </summary>
        [Test]
        public void TypeSealedIsUnnamed()
        {
            const string OldSource = "public class T { }\n" + Getter;
            const string NewSource = "public sealed class T { }\n" + Getter;
            SurfaceFixture.AssertDifferent(OldSource, NewSource,
                "class D { object M() => (System.IDisposable)F.Get(); }");
            SurfaceFixture.AssertUnnamed(OldSource, NewSource, "Type of T");
        }

        /// <summary>
        /// Verifies that turning a struct into a ref struct, which breaks boxing a dependent writes
        /// without naming the type, cannot be told by names.
        /// </summary>
        [Test]
        public void StructMadeRefStructIsUnnamed()
        {
            const string OldSource = "public struct S { }\n" + StructGetter;
            const string NewSource = "public ref struct S { }\n" + StructGetter;
            SurfaceFixture.AssertDifferent(OldSource, NewSource, "class D { object M() { object o = F.Get(); return o; } }");
            SurfaceFixture.AssertUnnamed(OldSource, NewSource, "Type of S");
        }

        /// <summary>
        /// Verifies that adding an instance field to a struct, which can break an unmanaged constraint a
        /// dependent satisfies without naming the type, cannot be told by names.
        /// </summary>
        [Test]
        public void StructInstanceFieldAddedIsUnnamed()
        {
            const string OldSource = "public struct S { public int A; }\n" + StructGetter;
            const string NewSource = "public struct S { public int A; public string? B; }\n" + StructGetter;
            SurfaceFixture.AssertDifferent(OldSource, NewSource,
                "class D { static void U<V>(V v) where V : unmanaged { } void M() { U(F.Get()); } }");
            SurfaceFixture.AssertUnnamed(OldSource, NewSource, "ValueTypeInstanceField of S");
        }

        /// <summary>
        /// Verifies that removing a parameterless constructor, which breaks a new() constraint a dependent
        /// satisfies without naming the type, cannot be told by names.
        /// </summary>
        [Test]
        public void ParameterlessConstructorRemovedIsUnnamed()
        {
            const string Maker = "public static class G { public static void Make<U>(U u) where U : new() { } }\n";
            const string OldSource = "public class T { public T() { } }\n" + Getter + Maker;
            const string NewSource = "public class T { public T(int x) { } }\n"
                + "public static class F { public static T Get() => new T(0); }\n" + Maker;
            SurfaceFixture.AssertDifferent(OldSource, NewSource, "class D { void M() { G.Make(F.Get()); } }");
            SurfaceFixture.AssertUnnamed(OldSource, NewSource, "ParameterlessConstructor of T");
        }

        /// <summary>
        /// Verifies that changing a renamed indexer, which a dependent uses without writing its name,
        /// cannot be told by names.
        /// </summary>
        [Test]
        public void RenamedIndexerChangeIsUnnamed()
        {
            const string OldSource =
                "public class T { [System.Runtime.CompilerServices.IndexerName(\"At\")] public int this[int i] => i; }\n" + Getter;
            const string NewSource =
                "public class T { [System.Runtime.CompilerServices.IndexerName(\"At\")] public string this[int i] => \"\"; }\n" + Getter;
            SurfaceFixture.AssertDifferent(OldSource, NewSource, "class D { int M() { int v = F.Get()[0]; return v; } }");
            SurfaceFixture.AssertUnnamed(OldSource, NewSource, "Indexer of T");
        }

        /// <summary>
        /// Verifies that an extension GetAwaiter added in a new static class, which makes a dependent's
        /// await bind without writing anything new, cannot be told by names.
        /// </summary>
        [Test]
        public void ExtensionGetAwaiterAddedIsUnnamed()
        {
            // An extension method both versions have, so the attribute it puts on the assembly is no change.
            const string OldSource = "public class T { }\n" + Getter + Awaiter
                + "public static class Old { public static int Twice(this int value) => value * 2; }\n";
            const string NewSource = OldSource + "public static class Ext { public static Aw GetAwaiter(this T t) => new Aw(); }\n";
            SurfaceFixture.AssertDifferent(OldSource, NewSource,
                "class D { async System.Threading.Tasks.Task<int> M() { return await F.Get(); } }");
            SurfaceFixture.AssertUnnamed(OldSource, NewSource, "pattern-bound member GetAwaiter");
        }

        /// <summary>
        /// Verifies that adding a property a dependent reaches through a value it never names the type of
        /// is named, and that the dependent writes one of the names.
        /// </summary>
        [Test]
        public void PropertyAddedIsNamedForADependentThatWritesIt()
        {
            const string OldSource = "public class T { }\n" + Getter;
            const string NewSource = "public class T { public int P { get; set; } }\n" + Getter;
            SurfaceFixture.AssertDifferent(OldSource, NewSource, "class D { int M() => F.Get().P; }");
            SurfaceFixture.AssertChangedNames(OldSource, NewSource, new[] { "T", "P" });
        }

        /// <summary>
        /// Verifies that the description carries the version that keeps the base type on its own row.
        /// </summary>
        [Test]
        public void DescriptionKeepsTheBaseTypeOnItsOwnRow()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "compile-check-describe-" + System.Guid.NewGuid().ToString("N") + ".dll");
            System.IO.File.WriteAllBytes(path, SurfaceFixture.EmitReferenceAssembly("public class B { } public class C : B { }"));
            try
            {
                string[] texts = CompareSurfaceCommand.Describe(path).Texts.ToArray();

                Assert.That(texts[0], Is.EqualTo("sv2"));
                Assert.That(texts.Where(text => text.StartsWith("type C ")).Single(), Does.Not.Contain("base="));
                Assert.That(texts, Does.Contain("C base B"));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
