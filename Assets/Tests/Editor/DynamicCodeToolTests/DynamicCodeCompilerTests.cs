using NUnit.Framework;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    [TestFixture]
    public class DynamicCodeCompilerTests
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
            DynamicCodeCompiler compiler = new DynamicCodeCompiler(DynamicCodeSecurityLevel.Restricted);
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
            DynamicCodeCompiler compiler = new DynamicCodeCompiler(DynamicCodeSecurityLevel.Restricted);
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
            DynamicCodeCompiler compiler = new DynamicCodeCompiler(DynamicCodeSecurityLevel.Restricted);
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
            DynamicCodeCompiler compiler = new DynamicCodeCompiler(DynamicCodeSecurityLevel.Restricted);
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
            DynamicCodeCompiler compiler = new DynamicCodeCompiler(DynamicCodeSecurityLevel.Restricted);
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
        public async Task CompileAsync_WhenInterpolationHoleContainsCollectionInitializer_ShouldSucceed()
        {
            DynamicCodeCompiler compiler = new DynamicCodeCompiler(DynamicCodeSecurityLevel.Restricted);
            CompilationRequest request = new CompilationRequest
            {
                Code = @"
                    string message = $""x{new int[] { 1, 2, 3 }.Length}y"";
                    return message;
                ",
                ClassName = "InterpolationCollectionInitializerCommand",
                Namespace = "TestNamespace"
            };

            CompilationResult result = await compiler.CompileAsync(request, CancellationToken.None);

            Assert.IsTrue(result.Success, result.Errors != null && result.Errors.Count > 0 ? result.Errors[0].Message : "Interpolated string with collection initializers should compile");
            Assert.IsNotNull(result.CompiledAssembly);
        }

        [Test]
        public async Task CompileAsync_WhenTypeRequiresMissingUsing_ShouldInjectNamespaceAndSucceed()
        {
            DynamicCodeCompiler compiler = new DynamicCodeCompiler(DynamicCodeSecurityLevel.Restricted);
            CompilationRequest request = new CompilationRequest
            {
                Code = @"
                    StringBuilder builder = new StringBuilder();
                    builder.Append(""ok"");
                    return builder.ToString();
                ",
                ClassName = "MissingUsingCommand",
                Namespace = "TestNamespace"
            };

            CompilationResult result = await compiler.CompileAsync(request, CancellationToken.None);

            Assert.IsTrue(result.Success, result.Errors != null && result.Errors.Count > 0 ? result.Errors[0].Message : "Missing using should be resolved automatically");
            StringAssert.Contains("using System.Text;", result.UpdatedCode);
        }

        [Test]
        public async Task CompileAsync_WhenOnlyLiteralValuesDiffer_ShouldReuseCompiledAssembly()
        {
            DynamicCodeCompiler compiler = new DynamicCodeCompiler(DynamicCodeSecurityLevel.Restricted);
            CompilationRequest firstRequest = new CompilationRequest
            {
                Code = "int benchNonce = 100; return benchNonce;",
                ClassName = "LiteralReuseCommand",
                Namespace = "TestNamespace"
            };
            CompilationRequest secondRequest = new CompilationRequest
            {
                Code = "int benchNonce = 200; return benchNonce;",
                ClassName = "LiteralReuseCommand",
                Namespace = "TestNamespace"
            };

            CompilationResult firstResult = await compiler.CompileAsync(firstRequest, CancellationToken.None);
            CompilationResult secondResult = await compiler.CompileAsync(secondRequest, CancellationToken.None);

            Assert.IsTrue(firstResult.Success, firstResult.Errors != null && firstResult.Errors.Count > 0 ? firstResult.Errors[0].Message : "First compilation should succeed");
            Assert.IsTrue(secondResult.Success, secondResult.Errors != null && secondResult.Errors.Count > 0 ? secondResult.Errors[0].Message : "Second compilation should succeed");
            Assert.AreSame(firstResult.CompiledAssembly, secondResult.CompiledAssembly);
        }

        [Test]
        public async Task CompileAsync_WhenCustomAsmdefTypeIsReferenced_ShouldAddAssemblyReferenceAndSucceed()
        {
            DynamicCodeCompiler compiler = new DynamicCodeCompiler(DynamicCodeSecurityLevel.Restricted);
            CompilationRequest request = new CompilationRequest
            {
                Code = @"
                    DynamicAssemblyTest test = new DynamicAssemblyTest();
                    return test.HelloWorld();
                ",
                ClassName = "CustomAsmdefAssemblyReferenceCommand",
                Namespace = "TestNamespace"
            };

            CompilationResult result = await compiler.CompileAsync(request, CancellationToken.None);

            Assert.IsTrue(result.Success, result.Errors != null && result.Errors.Count > 0 ? result.Errors[0].Message : "Custom asmdef type should compile");
            StringAssert.Contains("using io.github.hatayama.uLoopMCP;", result.UpdatedCode);
        }

        [Test]
        public async Task CompileAsync_WhenFullyQualifiedCustomAsmdefTypeIsReferenced_ShouldSucceed()
        {
            DynamicCodeCompiler compiler = new DynamicCodeCompiler(DynamicCodeSecurityLevel.Restricted);
            CompilationRequest request = new CompilationRequest
            {
                Code = @"
                    io.github.hatayama.uLoopMCP.DynamicAssemblyTest test = new io.github.hatayama.uLoopMCP.DynamicAssemblyTest();
                    return test.HelloWorld();
                ",
                ClassName = "FullyQualifiedCustomAsmdefReferenceCommand",
                Namespace = "TestNamespace"
            };

            CompilationResult result = await compiler.CompileAsync(request, CancellationToken.None);

            Assert.IsTrue(result.Success, result.Errors != null && result.Errors.Count > 0 ? result.Errors[0].Message : "Fully-qualified custom asmdef type should compile");
        }

        [Test]
        public void CompileAsync_WhenCanceledBeforeBuild_ShouldThrowOperationCanceledException()
        {
            DynamicCodeCompiler compiler = new DynamicCodeCompiler(DynamicCodeSecurityLevel.Restricted);
            CompilationRequest request = new CompilationRequest
            {
                Code = "return 1 + 2;",
                ClassName = "CanceledBeforeBuildCommand",
                Namespace = "TestNamespace"
            };

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.That(
                async () => await compiler.CompileAsync(request, cts.Token),
                Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public async Task CompileAsync_Restricted_WhenRegistryValidatorRejects_ShouldReturnSecurityViolation()
        {
            PreloadAssemblySecurityValidatorRegistry.SwapValidatorForTests(new RejectingPreloadAssemblySecurityValidator());

            DynamicCodeCompiler compiler = new DynamicCodeCompiler(DynamicCodeSecurityLevel.Restricted);
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

            DynamicCodeCompiler compiler = new DynamicCodeCompiler(DynamicCodeSecurityLevel.Restricted);
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

        [Test]
        public async Task RequestAutoPrewarmAsync_WhenCalledRepeatedly_ShouldReuseSameTask()
        {
            Task firstTask = DynamicCodeCompiler.RequestAutoPrewarmAsync();
            Task secondTask = DynamicCodeCompiler.RequestAutoPrewarmAsync();

            Assert.AreSame(firstTask, secondTask);
            await firstTask;
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
