#if ULOOPMCP_HAS_ROSLYN
using NUnit.Framework;
using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
	/// <summary>
	/// Tests to verify AST-based wrapping of top-level statements in RoslynCompiler.WrapCodeIfNeeded
	/// - Should not mis-detect keywords inside strings
	/// - Should wrap genuine top-level statements into Execute method
	/// </summary>
	public class WrapCodeTopLevelStatementsAstTests
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
		public void StringContainsKeywords_DoesNotBypassWrapping()
		{
			// Arrange: top-level statements present, but also a string containing 'class' and 'namespace'
			string aiGeneratedCode = @"string s = ""this mentions class and namespace""; return s;";

			CompilationRequest request = new()
			{
				Code = aiGeneratedCode,
				ClassName = "DynamicCommand",
				Namespace = "Dynamic",
				AdditionalReferences = new List<string>()
			};

			// Act
			CompilationResult result = _compiler.Compile(request);

			// Assert
			Assert.IsTrue(result.Success, $"Compilation failed: {result.Errors?.Count}");
			Assert.IsTrue(result.UpdatedCode.Contains("class DynamicCommand"), "Code should be wrapped in class");
			Assert.IsTrue(result.UpdatedCode.Contains("public object Execute"), "Execute method should be generated");
			Assert.IsTrue(result.UpdatedCode.Contains("this mentions class and namespace"), "String literal must remain intact");
		}

		[Test]
		public void PreWrappedClass_RemainsUnchanged_NoExtraWrapper()
		{
			// Arrange: Already a valid class/namespace, no top-level statements
			string aiGeneratedCode = @"namespace Dynamic { public class DynamicCommand { public object Execute(System.Collections.Generic.Dictionary<string, object> parameters = null) { return ""OK""; } } }";

			CompilationRequest request = new()
			{
				Code = aiGeneratedCode,
				ClassName = "DynamicCommand",
				Namespace = "Dynamic",
				AdditionalReferences = new List<string>()
			};

			// Act
			CompilationResult result = _compiler.Compile(request);

			// Assert: Should compile and not duplicate wrappers
			Assert.IsTrue(result.Success, $"Compilation failed: {result.Errors?.Count}");
			// Heuristic check: updated code should contain exactly one class declaration for DynamicCommand
			int occurrences = System.Text.RegularExpressions.Regex.Matches(result.UpdatedCode, "public class DynamicCommand").Count;
			Assert.AreEqual(1, occurrences, "Should not wrap already-wrapped code");
		}

		[Test]
		public void MixedUsingAndTopLevel_WrapsAndPreservesUsings()
		{
			string aiGeneratedCode = @"using UnityEngine; string name = ""Cube""; var go = new GameObject(name); return go.name;";

			CompilationRequest request = new()
			{
				Code = aiGeneratedCode,
				ClassName = "DynamicCommand",
				Namespace = "Dynamic",
				AdditionalReferences = new List<string>()
			};

			CompilationResult result = _compiler.Compile(request);

			Assert.IsTrue(result.Success, $"Compilation failed: {result.Errors?.Count}");
			Assert.IsTrue(result.UpdatedCode.Contains("using UnityEngine;"), "Using should be preserved at top");
			Assert.IsTrue(result.UpdatedCode.Contains("public class DynamicCommand"));
			Assert.IsTrue(result.UpdatedCode.Contains("public object Execute"));
		}
	}
}
#endif
