using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    [TestFixture]
    public class AssemblyBuilderCompilerTests
    {
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
    }
}
