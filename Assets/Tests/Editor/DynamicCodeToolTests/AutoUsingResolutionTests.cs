#if ULOOPMCP_HAS_ROSLYN
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    public class AutoUsingResolutionTests
    {
        private RoslynCompiler _compiler;

        [SetUp]
        public void Setup()
        {
            _compiler = new RoslynCompiler(DynamicCodeSecurityLevel.FullAccess);
        }

        [TearDown]
        public void TearDown()
        {
            _compiler?.Dispose();
        }

        [Test]
        public void Should_NotAutoResolve_When_Vector3IsAmbiguous()
        {
            // Vector3 exists in both UnityEngine and System.Numerics
            string code = @"Vector3 pos = Vector3.zero;
return pos.ToString();";

            CompilationResult result = Compile(code);

            Assert.IsFalse(result.Success,
                "Compilation should fail when type has multiple namespace candidates");
            Assert.IsTrue(result.AmbiguousTypeCandidates.ContainsKey("Vector3"),
                "Should report Vector3 as ambiguous");
            List<string> candidates = result.AmbiguousTypeCandidates["Vector3"];
            Assert.That(candidates, Has.Member("UnityEngine"));
            Assert.That(candidates, Has.Member("System.Numerics"));
        }

        [Test]
        public void Should_AutoResolve_UnityEditor_When_EditorGUILayoutUsed()
        {
            string code = @"EditorWindow window = null;
return window?.ToString() ?? ""null"";";

            CompilationResult result = Compile(code);

            Assert.IsTrue(result.Success, $"Compilation should succeed. Errors: {FormatErrors(result)}");
            Assert.IsTrue(result.UpdatedCode.Contains("using UnityEditor;"),
                "Should auto-add 'using UnityEditor;'");
            Assert.IsEmpty(result.AmbiguousTypeCandidates,
                "Should have no ambiguous candidates when resolution succeeds");
        }

        [Test]
        public void Should_AutoResolve_SystemText_When_StringBuilderUsed()
        {
            string code = @"StringBuilder sb = new StringBuilder();
return sb.ToString();";

            CompilationResult result = Compile(code);

            Assert.IsTrue(result.Success, $"Compilation should succeed. Errors: {FormatErrors(result)}");
            Assert.IsTrue(result.UpdatedCode.Contains("using System.Text;"),
                "Should auto-add 'using System.Text;'");
        }

        [Test]
        public void Should_ReturnCandidates_When_MultipleNamespacesMatch()
        {
            // 'Object' exists in both System and UnityEngine -> ambiguous
            string code = @"Object obj = null;
return obj?.ToString() ?? ""null"";";

            CompilationResult result = Compile(code);

            // Should fail because 'Object' is ambiguous and cannot be auto-resolved
            Assert.IsFalse(result.Success,
                "Compilation should fail when type has multiple namespace candidates");
        }

        [Test]
        public void Should_ResolveMultipleUsings_InSingleCompilation()
        {
            string code = @"EditorWindow window = null;
StringBuilder sb = new StringBuilder();
return (window?.ToString() ?? ""null"") + sb.ToString();";

            CompilationResult result = Compile(code);

            Assert.IsTrue(result.Success, $"Compilation should succeed. Errors: {FormatErrors(result)}");
            Assert.IsTrue(result.UpdatedCode.Contains("using UnityEditor;"),
                "Should auto-add 'using UnityEditor;'");
            Assert.IsTrue(result.UpdatedCode.Contains("using System.Text;"),
                "Should auto-add 'using System.Text;'");
        }

        [Test]
        public void Should_NotAutoResolve_When_GenericListIsAmbiguous()
        {
            // List exists in both System.Collections.Generic and NUnit.Framework
            string code = @"List<int> numbers = new List<int> { 1, 2, 3 };
return numbers.Count;";

            CompilationResult result = Compile(code);

            Assert.IsFalse(result.Success,
                "Compilation should fail when type has multiple namespace candidates");
            Assert.IsTrue(result.AmbiguousTypeCandidates.ContainsKey("List"),
                "Should report List as ambiguous");
            List<string> candidates = result.AmbiguousTypeCandidates["List"];
            Assert.That(candidates, Has.Member("System.Collections.Generic"));
            Assert.That(candidates, Has.Member("NUnit.Framework"));
        }

        [Test]
        public void Should_NotRetry_When_TypeNotFoundAnywhere()
        {
            string code = @"CompletelyFakeNonExistentType x = null;
return x?.ToString() ?? ""null"";";

            CompilationResult result = Compile(code);

            Assert.IsFalse(result.Success,
                "Compilation should fail for types that don't exist in any assembly");
            Assert.IsTrue(result.Errors.Any(e => e.ErrorCode == "CS0246"),
                "Should report CS0246 for unresolvable type");
        }

        private CompilationResult Compile(string code)
        {
            CompilationRequest request = new()
            {
                Code = code,
                ClassName = "DynamicCommand",
                Namespace = "Dynamic",
                AdditionalReferences = new List<string>()
            };

            return _compiler.Compile(request);
        }

        private static string FormatErrors(CompilationResult result)
        {
            if (result.Errors == null || result.Errors.Count == 0) return "(no errors)";
            return string.Join("; ", result.Errors.Select(e => $"{e.ErrorCode}: {e.Message}"));
        }
    }
}
#endif
