using System;
using System.IO;
using System.Text.Json;
using NUnit.Framework;

namespace UnityCliLoop.CompileCheckWorker.UnitTests
{
    [TestFixture]
    public sealed class CompareSurfaceCommandTests
    {
        private const string TwoPrivateStrings = " private string pa = \"\"; private string pb = \"\"; ";
        private const string ThreePrivateNullableObjects = " private object? pa; private object? pb; private object? pc; ";
        private const string InternalsVisibleToDependent =
            "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"" + SurfaceFixture.DependentAssemblyName + "\")]\n";

        /// <summary>
        /// Verifies that adding private reference-type fields, which only moves the nullable attributes
        /// the compiler writes, keeps the surface.
        /// </summary>
        [Test]
        public void PrivateReferenceFieldsAddedKeepTheSurface()
        {
            SurfaceFixture.AssertSame(
                "public class Bag { public Bag(int seed) { } public int Count() => 0; }",
                "public class Bag { public Bag(int seed) { } public int Count() => 0;" + TwoPrivateStrings + "}",
                "class D { int F() => new Bag(1).Count(); }");
        }

        /// <summary>
        /// Verifies that adding a private generic method with an unmanaged constraint, which only adds an
        /// embedded attribute type, keeps the surface.
        /// </summary>
        [Test]
        public void PrivateUnmanagedGenericMethodAddedKeepsTheSurface()
        {
            SurfaceFixture.AssertSame(
                "public class C { public int X; }",
                "public class C { public int X; private static void G<T>() where T : unmanaged { } }",
                "class D { int F() => new C().X; }");
        }

        /// <summary>
        /// Verifies that annotating a public property as nullable changes the surface.
        /// </summary>
        [Test]
        public void PublicPropertyAnnotationChangeChangesTheSurface()
        {
            SurfaceFixture.AssertDifferent(
                "public class C { public string P { get; set; } = \"\"; }",
                "public class C { public string? P { get; set; } }",
                "class D { int F() => new C().P.Length; }");
        }

        /// <summary>
        /// Verifies that turning an oblivious public member into a not-annotated one changes the surface.
        /// </summary>
        [Test]
        public void ObliviousToNotAnnotatedChangesTheSurface()
        {
            SurfaceFixture.AssertDifferent(
                "public class C {\n#nullable disable\npublic string P { get; set; }\n#nullable restore\n}",
                "public class C { public string P { get; set; } = \"\"; }",
                "class D { void F() { new C().P = null; } }");
        }

        /// <summary>
        /// Verifies that relaxing a class constraint to a nullable one changes the surface.
        /// </summary>
        [Test]
        public void ConstraintAnnotationChangeChangesTheSurface()
        {
            SurfaceFixture.AssertDifferent(
                "public class G<T> where T : class { }",
                "public class G<T> where T : class? { }",
                "class D { G<string?>? F() => null; }");
        }

        /// <summary>
        /// Verifies that annotating a type argument of the base type changes the surface.
        /// </summary>
        [Test]
        public void BaseTypeArgumentAnnotationChangeChangesTheSurface()
        {
            SurfaceFixture.AssertDifferent(
                "public class C : System.Collections.Generic.List<string> { }",
                "public class C : System.Collections.Generic.List<string?> { }",
                "class D { int F() => new C()[0].Length; }");
        }

        /// <summary>
        /// Verifies that marking a public method obsolete as an error changes the surface.
        /// </summary>
        [Test]
        public void ObsoleteErrorAddedChangesTheSurface()
        {
            SurfaceFixture.AssertDifferent(
                "public class C { public void M() { } }",
                "public class C { [System.Obsolete(\"x\", true)] public void M() { } }",
                "class D { void F() { new C().M(); } }");
        }

        /// <summary>
        /// Verifies that changing the value of a public constant changes the surface.
        /// </summary>
        [Test]
        public void PublicConstantValueChangeChangesTheSurface()
        {
            SurfaceFixture.AssertDifferent(
                "public class C { public const int K = 1; }",
                "public class C { public const int K = 300; }",
                "class D { const byte B = C.K; }");
        }

        /// <summary>
        /// Verifies that adding a public method changes the surface.
        /// </summary>
        [Test]
        public void PublicMethodAddedChangesTheSurface()
        {
            SurfaceFixture.AssertDifferent(
                "public class C { public int X; }",
                "public class C { public int X; public void M() { } }",
                "class D { void F() { new C().M(); } }");
        }

        /// <summary>
        /// Verifies that adding a private reference-type field to a struct, which the reference assembly
        /// keeps and which makes the struct no longer unmanaged, changes the surface.
        /// </summary>
        [Test]
        public void StructPrivateReferenceFieldAddedChangesTheSurface()
        {
            SurfaceFixture.AssertDifferent(
                "public struct S { public int X; }",
                "public struct S { public int X; private string? p; }",
                "class D { unsafe void F() { S* p = null; } }");
        }

        /// <summary>
        /// Verifies that annotating an internal member a dependent sees through InternalsVisibleTo
        /// changes the surface.
        /// </summary>
        [Test]
        public void InternalAnnotationChangeChangesTheSurface()
        {
            SurfaceFixture.AssertDifferent(
                InternalsVisibleToDependent + "public class C { internal static string M() => \"\"; public int P; }",
                InternalsVisibleToDependent + "public class C { internal static string? M() => null; public int P; }",
                "class D { int F() => C.M().Length; }");
        }

        /// <summary>
        /// Verifies that turning IntPtr into nint, which differs only in an attribute of an embedded
        /// type, changes the surface.
        /// </summary>
        [Test]
        public void NativeIntegerChangeChangesTheSurface()
        {
            SurfaceFixture.AssertDifferent(
                "public class C { public static System.IntPtr M(System.IntPtr x) => x; }",
                "public class C { public static nint M(nint x) => x; }",
                "class D { long F() { return (long)(C.M(default) * 2); } }");
        }

        /// <summary>
        /// Verifies that annotating a type argument of the type containing a nested return type changes
        /// the surface.
        /// </summary>
        [Test]
        public void ContainingTypeArgumentAnnotationChangeChangesTheSurface()
        {
            SurfaceFixture.AssertDifferent(
                "using System.Collections.Generic;\npublic class C { public static Dictionary<string, string>.ValueCollection M() => null!; }",
                "using System.Collections.Generic;\npublic class C { public static Dictionary<string, string?>.ValueCollection M() => null!; }",
                "class D { int F() { int n = 0; foreach (string v in C.M()) n += v.Length; return n; } }");
        }

        /// <summary>
        /// Verifies that enabling nullable annotations on an interface with an unconstrained method type
        /// parameter changes the surface.
        /// </summary>
        [Test]
        public void ObliviousUnconstrainedTypeParameterToEnabledChangesTheSurface()
        {
            SurfaceFixture.AssertDifferent(
                "#nullable disable\npublic interface I { void M<T>(); }",
                "public interface I { void M<T>(); }",
                "class D : I { public void M<T>() where T : notnull { } }");
        }

        /// <summary>
        /// Verifies that adding an attribute to the return value of a public method changes the surface.
        /// </summary>
        [Test]
        public void ReturnAttributeAddedChangesTheSurface()
        {
            // .NET Standard 2.0 has no MaybeNullAttribute, so the library declares its own, the way
            // libraries targeting it do; the compiler reads the attribute by name.
            const string MaybeNull = "namespace System.Diagnostics.CodeAnalysis { [System.AttributeUsage(System.AttributeTargets.ReturnValue)] internal sealed class MaybeNullAttribute : System.Attribute { } }\n";
            SurfaceFixture.AssertDifferent(
                MaybeNull + "public class C { public static string M() => \"\"; }",
                MaybeNull + "public class C { [return: System.Diagnostics.CodeAnalysis.MaybeNull] public static string M() => \"\"; }",
                "class D { int F() => C.M().Length; }");
        }

        /// <summary>
        /// Verifies that members without a nullable position keep the surface when private fields move
        /// the context of their type.
        /// </summary>
        [Test]
        public void PositionFreeMembersKeepTheSurfaceWhenTheContextMoves()
        {
            SurfaceFixture.AssertSame(
                "public enum Kind { A, B }\npublic class C { public int? N { get; set; } public Kind K { get; set; } }",
                "public enum Kind { A, B }\npublic class C { public int? N { get; set; } public Kind K { get; set; }" + TwoPrivateStrings + "}",
                "class D { int F() { C c = new C(); return (c.N ?? 0) + (int)c.K; } }");
        }

        /// <summary>
        /// Verifies that annotating the element type of a returned array changes the surface.
        /// </summary>
        [Test]
        public void ArrayElementAnnotationChangeChangesTheSurface()
        {
            SurfaceFixture.AssertDifferent(
                "public class C { public static string[] M() => new string[0]; }",
                "public class C { public static string?[] M() => new string?[0]; }",
                "class D { int F() => C.M()[0].Length; }");
        }

        /// <summary>
        /// Verifies that a base type keeps its annotation when private fields move the context of the
        /// type deriving from it.
        /// </summary>
        [Test]
        public void BaseTypeKeepsItsAnnotationWhenTheTypeContextMoves()
        {
            SurfaceFixture.AssertSame(
                "public class C : System.Collections.Generic.List<int> { public int X; }",
                "public class C : System.Collections.Generic.List<int> { public int X;" + ThreePrivateNullableObjects + "}",
                "class D { int F() { C c = new C(); c.Add(1); return c.Count + c.X; } }");
        }

        /// <summary>
        /// Verifies that a file that is not an assembly is never called the same surface.
        /// </summary>
        [Test]
        public void UnreadableAssemblyIsNotTheSameSurface()
        {
            byte[] assembly = SurfaceFixture.EmitReferenceAssembly("public class C { }");

            CompareSurfaceResponse response = SurfaceFixture.Compare(assembly, new byte[] { 1, 2, 3 });

            Assert.That(response.Outcome, Is.EqualTo(CompareSurfaceResponse.DifferentOutcome));
            Assert.That(response.Detail, Does.StartWith("could not read: "));
        }

        /// <summary>
        /// Verifies that two assemblies built from the same source keep the surface.
        /// </summary>
        [Test]
        public void IdenticalAssembliesKeepTheSurface()
        {
            byte[] assembly = SurfaceFixture.EmitReferenceAssembly("public class C { public string? P { get; set; } }");

            CompareSurfaceResponse response = SurfaceFixture.Compare(assembly, (byte[])assembly.Clone());

            Assert.That(response.Outcome, Is.EqualTo(CompareSurfaceResponse.SameOutcome), response.Detail);
            Assert.That(response.Detail, Is.Empty);
        }

        /// <summary>
        /// Verifies that the worker's command dispatch runs a compare-surface request and writes its
        /// outcome to the response file.
        /// </summary>
        [Test]
        public void CompareSurfaceCommandIsDispatched()
        {
            string directory = Path.Combine(Path.GetTempPath(), "compile-check-dispatch-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string assemblyPath = Path.Combine(directory, "lib.ref.dll");
                File.WriteAllBytes(assemblyPath, SurfaceFixture.EmitReferenceAssembly("public class C { }"));
                string requestPath = Path.Combine(directory, "in.json");
                string responsePath = Path.Combine(directory, "out.json");
                File.WriteAllBytes(requestPath, JsonSerializer.SerializeToUtf8Bytes(new CompareSurfaceRequest
                {
                    ProducedPath = assemblyPath,
                    UnityBuildPath = assemblyPath,
                }));

                Program.RunCommand(Program.CompareSurfaceCommandName, requestPath, responsePath);

                CompareSurfaceResponse response = JsonSerializer.Deserialize<CompareSurfaceResponse>(File.ReadAllBytes(responsePath));
                Assert.That(response.Outcome, Is.EqualTo(CompareSurfaceResponse.SameOutcome));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// Verifies that annotating a type argument of a generic value type changes the surface.
        /// </summary>
        [Test]
        public void GenericValueTypeArgumentAnnotationChangeChangesTheSurface()
        {
            SurfaceFixture.AssertDifferent(
                "public class C { public System.Collections.Generic.KeyValuePair<int, string> P; }",
                "public class C { public System.Collections.Generic.KeyValuePair<int, string?> P; }",
                "class D { int F() => new C().P.Value.Length; }");
        }

        /// <summary>
        /// Verifies that adding a private method before a lambda, which only renumbers the cached
        /// delegate fields of the compiler's private closure class, keeps the surface.
        /// </summary>
        [Test]
        public void PrivateMemberAddedBeforeALambdaKeepsTheSurface()
        {
            string oldSource = "#nullable disable\npublic class C { public int Sum(int[] xs) => System.Linq.Enumerable.Sum(System.Linq.Enumerable.Select(xs, x => x * 2)); }";
            string newSource = "#nullable disable\npublic class C { private void Before() { } public int Sum(int[] xs) => System.Linq.Enumerable.Sum(System.Linq.Enumerable.Select(xs, x => x * 2)); }";

            AssertOnlyPrivateNestedTypesDiffer(oldSource, newSource);
            SurfaceFixture.AssertSame(oldSource, newSource, "class D { int F() => new C().Sum(new int[0]); }");
        }

        /// <summary>
        /// Verifies that making a public nested type private changes the surface.
        /// </summary>
        [Test]
        public void NestedTypeMadePrivateChangesTheSurface()
        {
            SurfaceFixture.AssertDifferent(
                "public class Outer { public class N { public int V; } }",
                "public class Outer { private class N { public int V; } }",
                "class D { int F() => new Outer.N().V; }");
        }

        /// <summary>
        /// Verifies that adding a private nested class nothing outside its type refers to keeps the
        /// surface.
        /// </summary>
        [Test]
        public void PrivateNestedTypeChangeKeepsTheSurface()
        {
            string oldSource = "public class Outer { public int V; }";
            string newSource = "public class Outer { public int V; private class P { public string S = \"\"; public int M() => 1; } }";

            AssertOnlyPrivateNestedTypesDiffer(oldSource, newSource);
            SurfaceFixture.AssertSame(oldSource, newSource, "class D { int F() => new Outer().V; }");
        }

        /// <summary>
        /// Verifies that a private nested type a public interface implementation names reaches the
        /// dependent, so dropping one of its interfaces changes the surface.
        /// </summary>
        [Test]
        public void PrivateNestedTypeInAnInterfaceChangesTheSurface()
        {
            const string Usings = "using System; using System.Collections; using System.Collections.Generic;\n";
            SurfaceFixture.AssertDifferent(
                Usings + "public class A : IEnumerable<A.X> { private class X : ICloneable { public object Clone() => this; } IEnumerator<X> IEnumerable<X>.GetEnumerator() => null!; IEnumerator IEnumerable.GetEnumerator() => null!; }",
                Usings + "public class A : IEnumerable<A.X> { private class X { public object Clone() => this; } IEnumerator<X> IEnumerable<X>.GetEnumerator() => null!; IEnumerator IEnumerable.GetEnumerator() => null!; }",
                "using System; using System.Collections.Generic;\nclass D { IEnumerable<ICloneable> F() => new A(); }");
        }

        /// <summary>
        /// Verifies that a private nested struct behind a private field of a public struct reaches the
        /// dependent, so giving it a reference-type field changes the surface.
        /// </summary>
        [Test]
        public void PrivateNestedStructBehindAFieldChangesTheSurface()
        {
            SurfaceFixture.AssertDifferent(
                "public struct S { private Inner _x; private struct Inner { private int _a; } }",
                "public struct S { private Inner _x; private struct Inner { private object? _a; } }",
                "unsafe class D { static void G<T>() where T : unmanaged { } void F() { S* p = null; G<S>(); } }");
        }

        /// <summary>
        /// Verifies that giving an empty private nested struct behind a private field a field changes
        /// the surface, since the dependent's definite assignment depends on it.
        /// </summary>
        [Test]
        public void PrivateNestedStructGainingAFieldChangesTheSurface()
        {
            SurfaceFixture.AssertDifferent(
                "public struct S { private Inner _x; private struct Inner { } }",
                "public struct S { private Inner _x; private struct Inner { private int _a; } }",
                "class D { object F() { S s; return s; } }");
        }

        /// <summary>
        /// Verifies that adding a private method before a coroutine, which only renumbers the compiler's
        /// state machine type the iterator attribute names, keeps the surface.
        /// </summary>
        [Test]
        public void PrivateMemberAddedBeforeAnIteratorKeepsTheSurface()
        {
            SurfaceFixture.AssertSame(
                "public class C { public System.Collections.IEnumerator Run() { yield return null; } }",
                "public class C { private void Before() { } public System.Collections.IEnumerator Run() { yield return null; } }",
                "class D { object F() => new C().Run(); }");
        }

        /// <summary>
        /// Verifies that adding a private method before an async method, which only renumbers the
        /// compiler's state machine type the async attribute names, keeps the surface.
        /// </summary>
        [Test]
        public void PrivateMemberAddedBeforeAnAsyncMethodKeepsTheSurface()
        {
            SurfaceFixture.AssertSame(
                "public class C { public async System.Threading.Tasks.Task M() { await System.Threading.Tasks.Task.Yield(); } }",
                "public class C { private void Before() { } public async System.Threading.Tasks.Task M() { await System.Threading.Tasks.Task.Yield(); } }",
                "class D { System.Threading.Tasks.Task F() => new C().M(); }");
        }

        /// <summary>
        /// Verifies that a private protected nested type reaches a derived dependent through
        /// InternalsVisibleTo, so removing its member changes the surface.
        /// </summary>
        [Test]
        public void PrivateProtectedNestedTypeChangeChangesTheSurface()
        {
            SurfaceFixture.AssertDifferent(
                InternalsVisibleToDependent + "public class Outer { private protected class PP { public int V; } }",
                InternalsVisibleToDependent + "public class Outer { private protected class PP { } }",
                "class D : Outer { int F() => new PP().V; }");
        }

        /// <summary>
        /// Verifies that sending the internals to another assembly changes the surface: what the
        /// assembly says about itself decides what a dependent may use.
        /// </summary>
        [Test]
        public void InternalsVisibleToTargetChangeChangesTheSurface()
        {
            SurfaceFixture.AssertDifferent(
                InternalsVisibleToDependent + "public class C { internal static int M() => 0; }",
                "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"Other\")]\npublic class C { internal static int M() => 0; }",
                "class D { int F() => C.M(); }");
        }

        /// <summary>
        /// Verifies that renaming a parameter changes the surface, since a dependent can pass it by name.
        /// </summary>
        [Test]
        public void ParameterRenameChangesTheSurface()
        {
            SurfaceFixture.AssertDifferent(
                "public class C { public static void M(int a) { } }",
                "public class C { public static void M(int b) { } }",
                "class D { void F() => C.M(a: 1); }");
        }

        private static void AssertOnlyPrivateNestedTypesDiffer(string oldSource, string newSource)
        {
            Assert.That(
                SurfaceFixture.PrivateNestedMemberNames(SurfaceFixture.EmitReferenceAssembly(newSource)),
                Is.Not.EqualTo(SurfaceFixture.PrivateNestedMemberNames(SurfaceFixture.EmitReferenceAssembly(oldSource))),
                "the private nested types of the two versions are the same, so they are not what this case changes");
        }
    }
}
