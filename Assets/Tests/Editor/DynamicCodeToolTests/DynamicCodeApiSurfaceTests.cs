using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.Application;
using io.github.hatayama.UnityCliLoop.CompositionRoot;
using io.github.hatayama.UnityCliLoop.Domain;
using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.Infrastructure;
using io.github.hatayama.UnityCliLoop.Presentation;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.DynamicCodeToolTests
{
    [TestFixture]
    public class DynamicCodeApiSurfaceTests
    {
        [Test]
        public void IDynamicCodeExecutor_ShouldExposeAsyncExecutionOnly()
        {
            Assert.That(typeof(IDynamicCodeExecutor).GetMethod("ExecuteCode"), Is.Null);
            Assert.That(typeof(IDynamicCodeExecutor).GetMethod("ExecuteCodeAsync"), Is.Not.Null);
        }

        [Test]
        public void IDynamicCompilationService_ShouldExposeAsyncCompilationOnly()
        {
            Assert.That(typeof(IDynamicCompilationService).GetMethod("Compile"), Is.Null);
            Assert.That(typeof(IDynamicCompilationService).GetMethod("CompileAsync"), Is.Not.Null);
        }
    }
}
