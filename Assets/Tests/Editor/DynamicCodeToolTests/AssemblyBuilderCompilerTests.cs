using NUnit.Framework;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    [TestFixture]
    public class AssemblyBuilderCompilerTests
    {
        private IPreloadAssemblySecurityValidator _previousValidator;

        [SetUp]
        public void SetUp()
        {
            _previousValidator = PreloadAssemblySecurityValidatorRegistry.SwapValidatorForTests(new SystemReflectionMetadataPreloadValidator());
        }

        [TearDown]
        public void TearDown()
        {
            PreloadAssemblySecurityValidatorRegistry.SwapValidatorForTests(_previousValidator);
        }

        [Test]
        public async Task CompileAsync_WhenAssemblyModeIsSelectiveReference_ShouldIgnoreLegacyModeAndSucceed()
        {
            AssemblyBuilderCompiler compiler = new AssemblyBuilderCompiler(DynamicCodeSecurityLevel.Restricted);
            CompilationRequest request = new CompilationRequest
            {
                Code = "return 1 + 2;",
                ClassName = "SelectiveReferenceCompatibilityCommand",
                Namespace = "TestNamespace",
                AssemblyMode = AssemblyLoadingMode.SelectiveReference
            };

            CompilationResult result = await compiler.CompileAsync(request, CancellationToken.None);

            Assert.IsTrue(result.Success, result.Errors != null && result.Errors.Count > 0 ? result.Errors[0].Message : "Compilation should succeed");
            Assert.IsNotNull(result.CompiledAssembly);
        }

        [Test]
        public void Compile_WhenCalledSynchronously_ShouldThrowNotSupportedException()
        {
            AssemblyBuilderCompiler compiler = new AssemblyBuilderCompiler(DynamicCodeSecurityLevel.Restricted);
            CompilationRequest request = new CompilationRequest
            {
                Code = "return 1 + 2;",
                ClassName = "SyncCompileCommand",
                Namespace = "TestNamespace"
            };

            Assert.Throws<System.NotSupportedException>(() => compiler.Compile(request));
        }

        [Test]
        public async Task CompileAsync_Restricted_WhenModuleInitializerExists_ShouldReturnSecurityViolationBeforeLoad()
        {
            AssemblyBuilderCompiler compiler = new AssemblyBuilderCompiler(DynamicCodeSecurityLevel.Restricted);
            CompilationRequest request = new CompilationRequest
            {
                Code = @"
                    using System.Runtime.CompilerServices;

                    public static class DangerousModuleInitializer
                    {
                        [ModuleInitializer]
                        public static void Initialize()
                        {
                            UnityEngine.Debug.Log(""should not run"");
                        }
                    }
                ",
                ClassName = "DangerousModuleInitializerCommand",
                Namespace = "TestNamespace"
            };

            CompilationResult result = await compiler.CompileAsync(request, CancellationToken.None);

            Assert.IsFalse(result.Success, "ModuleInitializer should fail in Restricted mode");
            Assert.IsTrue(result.HasSecurityViolations, "ModuleInitializer should be reported as a security violation");
        }

        [Test]
        public async Task CompileAsync_Restricted_WhenSafeReflectionIsUsed_ShouldSucceed()
        {
            AssemblyBuilderCompiler compiler = new AssemblyBuilderCompiler(DynamicCodeSecurityLevel.Restricted);
            CompilationRequest request = new CompilationRequest
            {
                Code = @"
                    System.Reflection.MethodInfo method = typeof(UnityEngine.Mathf).GetMethod(""Max"",
                        new System.Type[] { typeof(float), typeof(float) });
                    object result = method.Invoke(null, new object[] { 3f, 7f });
                    return result;
                ",
                ClassName = "SafeReflectionCommand",
                Namespace = "TestNamespace"
            };

            CompilationResult result = await compiler.CompileAsync(request, CancellationToken.None);

            Assert.IsTrue(result.Success, result.Errors != null && result.Errors.Count > 0 ? result.Errors[0].Message : "Safe reflection should compile");
            Assert.IsFalse(result.HasSecurityViolations, "Safe reflection should not be flagged");
        }

        [Test]
        public async Task CompileAsync_WhenInterpolatedHoleContainsNestedStringLiteral_ShouldSucceed()
        {
            AssemblyBuilderCompiler compiler = new AssemblyBuilderCompiler(DynamicCodeSecurityLevel.Restricted);
            CompilationRequest request = new CompilationRequest
            {
                Code = @"
                    string message = $""x{string.Concat(""}"", ""z"")}y"";
                    return message;
                ",
                ClassName = "NestedInterpolationLiteralCommand",
                Namespace = "TestNamespace"
            };

            CompilationResult result = await compiler.CompileAsync(request, CancellationToken.None);

            Assert.IsTrue(result.Success, result.Errors != null && result.Errors.Count > 0 ? result.Errors[0].Message : "Interpolated string with nested literals should compile");
            Assert.IsNotNull(result.CompiledAssembly);
        }

        [Test]
        public void CompileAsync_WhenCanceledBeforeBuild_ShouldThrowOperationCanceledException()
        {
            AssemblyBuilderCompiler compiler = new AssemblyBuilderCompiler(DynamicCodeSecurityLevel.Restricted);
            CompilationRequest request = new CompilationRequest
            {
                Code = "return 1 + 2;",
                ClassName = "CanceledBeforeBuildCommand",
                Namespace = "TestNamespace"
            };

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(async () => await compiler.CompileAsync(request, cts.Token));
        }

        [Test]
        public async Task CompileAsync_Restricted_WhenRegistryValidatorRejects_ShouldReturnSecurityViolation()
        {
            PreloadAssemblySecurityValidatorRegistry.SwapValidatorForTests(new RejectingPreloadAssemblySecurityValidator());

            AssemblyBuilderCompiler compiler = new AssemblyBuilderCompiler(DynamicCodeSecurityLevel.Restricted);
            CompilationRequest request = new CompilationRequest
            {
                Code = "return 42;",
                ClassName = "RegistryValidatorCommand",
                Namespace = "TestNamespace"
            };

            CompilationResult result = await compiler.CompileAsync(request, CancellationToken.None);

            Assert.IsFalse(result.Success, "Injected registry validator should block assembly load");
            Assert.IsTrue(result.HasSecurityViolations, "Injected registry validator should surface a security violation");
            Assert.That(result.SecurityViolations, Has.Count.EqualTo(1));
            Assert.That(result.SecurityViolations[0].ApiName, Is.EqualTo("Injected.Validator"));
        }

        [Test]
        public async Task CompileAsync_Restricted_WhenRegistryHasNoValidator_ShouldUseMetadataValidatorFallback()
        {
            PreloadAssemblySecurityValidatorRegistry.SwapValidatorForTests(null);

            AssemblyBuilderCompiler compiler = new AssemblyBuilderCompiler(DynamicCodeSecurityLevel.Restricted);
            CompilationRequest request = new CompilationRequest
            {
                Code = @"
                    System.Type type = typeof(System.IO.FileInfo);
                    return type.Name;
                ",
                ClassName = "FallbackMetadataValidatorCommand",
                Namespace = "TestNamespace"
            };

            CompilationResult result = await compiler.CompileAsync(request, CancellationToken.None);

            Assert.IsFalse(result.Success, "Fallback metadata validator should block dangerous type references");
            Assert.IsTrue(result.HasSecurityViolations, "Fallback metadata validator should report a security violation");
            Assert.That(
                result.SecurityViolations.Exists(violation =>
                    violation.Location == "metadata" &&
                    violation.ApiName == "System.IO.FileInfo"),
                Is.True,
                "The rejection should come from the metadata fallback validator");
        }

        private sealed class RejectingPreloadAssemblySecurityValidator : IPreloadAssemblySecurityValidator
        {
            public SecurityValidationResult Validate(byte[] assemblyBytes)
            {
                return new SecurityValidationResult
                {
                    IsValid = false,
                    Violations = new List<SecurityViolation>
                    {
                        new SecurityViolation
                        {
                            Type = SecurityViolationType.DangerousApiCall,
                            ApiName = "Injected.Validator",
                            Message = "Injected validator rejected the assembly",
                            Description = "The test validator forces the compiler to use the registry result.",
                            Location = "test"
                        }
                    }
                };
            }
        }
    }
}
