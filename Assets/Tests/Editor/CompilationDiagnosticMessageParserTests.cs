using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.Application;
using io.github.hatayama.UnityCliLoop.CompositionRoot;
using io.github.hatayama.UnityCliLoop.Domain;
using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.Infrastructure;
using io.github.hatayama.UnityCliLoop.Presentation;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    public class CompilationDiagnosticMessageParserTests
    {
        [Test]
        public void ExtractTypeNameFromMessage_ReturnsNull_WhenMessageIsNull()
        {
            string result = CompilationDiagnosticMessageParser.ExtractTypeNameFromMessage(null);

            Assert.That(result, Is.Null);
        }
    }
}
