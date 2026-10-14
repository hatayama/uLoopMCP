using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace UnityCliLoop.CompileCheckWorker.UnitTests
{
    [TestFixture]
    public sealed class DeclarationFingerprintTests
    {
        /// <summary>
        /// Verifies that editing a statement inside a method body keeps the fingerprint.
        /// </summary>
        [Test]
        public void BodyEditKeepsFingerprint()
        {
            AssertSame(
                "class C { int M() { int a = 1; return a; } }",
                "class C { int M() { int b = 2; System.Console.WriteLine(b); return b; } }");
        }

        /// <summary>
        /// Verifies that editing the expression of an expression-bodied method keeps the fingerprint.
        /// </summary>
        [Test]
        public void ExpressionBodyEditKeepsFingerprint()
        {
            AssertSame("class C { int M() => 1; }", "class C { int M() => 2 + 3; }");
        }

        /// <summary>
        /// Verifies that editing an accessor body keeps the fingerprint.
        /// </summary>
        [Test]
        public void AccessorBodyEditKeepsFingerprint()
        {
            AssertSame(
                "class C { int p; int P { get { return p; } set { p = value; } } }",
                "class C { int p; int P { get { return p + 1; } set { p = value * 2; } } }");
        }

        /// <summary>
        /// Verifies that editing the initializer of a non-constant field or property keeps the fingerprint.
        /// </summary>
        [Test]
        public void NonConstInitializerEditKeepsFingerprint()
        {
            AssertSame(
                "class C { int x = 1, y = 2; static readonly string s = \"a\"; int P { get; } = 3; }",
                "class C { int x = 10, y = 20; static readonly string s = \"b\"; int P { get; } = 30; }");
        }

        /// <summary>
        /// Verifies that comments, whitespace, and CRLF versus LF line endings do not change the fingerprint.
        /// </summary>
        [Test]
        public void TriviaOnlyEditKeepsFingerprint()
        {
            AssertSame(
                "class C\r\n{\r\n    // old comment\r\n    void M() { }\r\n}\r\n",
                "class C\n{\n  /* new comment */\n\n  void   M()   {  }\n}\n");
        }

        /// <summary>
        /// Verifies that editing the inside of a #if region within a body keeps the fingerprint, also when
        /// the body holds a #nullable directive that the fingerprint keeps.
        /// </summary>
        [Test]
        public void DirectiveInsideBodyEditKeepsFingerprint()
        {
            AssertSame(
                "class C { void M() {\n#nullable enable\n#if FOO\nint a = 1;\n#else\nint b = 2;\n#endif\n} }",
                "class C { void M() {\n#nullable enable\n#if FOO\nstring changed = null;\n#else\nint other = 3;\n#endif\n} }");
        }

        /// <summary>
        /// Verifies that adding a #nullable directive inside a body changes the fingerprint, because it
        /// changes the nullable context of the declarations after it.
        /// </summary>
        [Test]
        public void NullableDirectiveInsideBodyChangesFingerprint()
        {
            AssertDifferent(
                "class C { void M() { } string N() => null; }",
                "class C { void M() {\n#nullable enable\n} string N() => null; }");
        }

        /// <summary>
        /// Verifies that moving a #nullable directive from one body to a later one changes the fingerprint,
        /// because the declarations between the two bodies change nullable context.
        /// </summary>
        [Test]
        public void NullableDirectiveMovedBetweenBodiesChangesFingerprint()
        {
            AssertDifferent(
                "class C { void A() {\n#nullable enable\n} string Between() => null; void B() { } }",
                "class C { void A() { } string Between() => null; void B() {\n#nullable enable\n} }");
        }

        /// <summary>
        /// Verifies that turning a computed accessor into an auto accessor changes the fingerprint.
        /// </summary>
        [Test]
        public void AccessorBodyPresenceChangesFingerprint()
        {
            AssertDifferent("class C { int P { get { return 1; } } }", "class C { int P { get; } }");
        }

        /// <summary>
        /// Verifies that adding or removing a method body (a default interface method versus an abstract one)
        /// changes the fingerprint.
        /// </summary>
        [Test]
        public void MethodBodyPresenceChangesFingerprint()
        {
            AssertDifferent("interface I { void M() { } }", "interface I { void M(); }");
        }

        /// <summary>
        /// Verifies that adding or removing a field initializer changes the fingerprint.
        /// </summary>
        [Test]
        public void InitializerPresenceChangesFingerprint()
        {
            AssertDifferent("class C { int x; }", "class C { int x = 1; }");
        }

        /// <summary>
        /// Verifies that changing the value of a constant changes the fingerprint.
        /// </summary>
        [Test]
        public void ConstValueEditChangesFingerprint()
        {
            AssertDifferent("class C { const int X = 1; }", "class C { const int X = 2; }");
        }

        /// <summary>
        /// Verifies that adding, removing, or renaming a method changes the fingerprint.
        /// </summary>
        [Test]
        public void MemberAddRemoveRenameChangesFingerprint()
        {
            const string original = "class C { void M() { } }";
            AssertDifferent(original, "class C { void M() { } void N() { } }");
            AssertDifferent(original, "class C { }");
            AssertDifferent(original, "class C { void Renamed() { } }");
        }

        /// <summary>
        /// Verifies that changing a parameter type, a parameter default, or a modifier such as async changes
        /// the fingerprint.
        /// </summary>
        [Test]
        public void SignatureEditChangesFingerprint()
        {
            const string original = "class C { void M(int a = 1) { } }";
            AssertDifferent(original, "class C { void M(long a = 1) { } }");
            AssertDifferent(original, "class C { void M(int a = 2) { } }");
            AssertDifferent(original, "class C { async void M(int a = 1) { } }");
        }

        /// <summary>
        /// Verifies that adding an attribute or changing an attribute argument changes the fingerprint.
        /// </summary>
        [Test]
        public void AttributeEditChangesFingerprint()
        {
            const string original = "class C { [System.Obsolete(\"a\")] void M() { } }";
            AssertDifferent(original, "class C { [System.Obsolete(\"b\")] void M() { } }");
            AssertDifferent(original, "class C { [System.Obsolete(\"a\")][System.Serializable] void M() { } }");
        }

        /// <summary>
        /// Verifies that adding a using directive changes the fingerprint.
        /// </summary>
        [Test]
        public void UsingEditChangesFingerprint()
        {
            AssertDifferent("class C { }", "using System;\nclass C { }");
        }

        /// <summary>
        /// Verifies that adding a #nullable directive outside any body changes the fingerprint.
        /// </summary>
        [Test]
        public void NullableDirectiveOutsideBodyChangesFingerprint()
        {
            AssertDifferent("class C { string M() => null; }", "#nullable disable\nclass C { string M() => null; }");
        }

        /// <summary>
        /// Verifies that changing the arguments of a constructor initializer changes the fingerprint.
        /// </summary>
        [Test]
        public void ConstructorInitializerEditChangesFingerprint()
        {
            AssertDifferent(
                "class B { public B(int x) { } } class C : B { C() : base(1) { } }",
                "class B { public B(int x) { } } class C : B { C() : base(2) { } }");
        }

        /// <summary>
        /// Verifies that changing the value of an enum member changes the fingerprint.
        /// </summary>
        [Test]
        public void EnumValueEditChangesFingerprint()
        {
            AssertDifferent("enum E { A = 1 }", "enum E { A = 2 }");
        }

        /// <summary>
        /// Verifies that editing a field or property initializer of a partial type, or of a type nested in
        /// one, changes the fingerprint, because the nullable analysis of a constructor in another file of
        /// that type reads it.
        /// </summary>
        [TestCase("static partial class C { static int seed = 1; }", "static partial class C { static int seed = 2; }")]
        [TestCase("partial class C { int guard = 1; }", "partial class C { int guard = Stop(); }")]
        [TestCase("partial class C { int P { get; } = 1; }", "partial class C { int P { get; } = 2; }")]
        [TestCase("partial class Outer { class C { int x = 1; } }", "partial class Outer { class C { int x = 2; } }")]
        public void PartialTypeInitializerEditChangesFingerprint(string before, string after)
        {
            AssertDifferent(before, after);
        }

        private static void AssertSame(string before, string after)
        {
            Assert.That(Fingerprint(after), Is.EqualTo(Fingerprint(before)));
        }

        private static void AssertDifferent(string before, string after)
        {
            Assert.That(Fingerprint(after), Is.Not.EqualTo(Fingerprint(before)));
        }

        private static string Fingerprint(string source)
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
            return DeclarationFingerprint.Compute(tree);
        }
    }
}
