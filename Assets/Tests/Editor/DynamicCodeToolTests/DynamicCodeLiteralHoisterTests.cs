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
    public class DynamicCodeLiteralHoisterTests
    {
        [Test]
        public void Rewrite_WhenInterpolatedHoleContainsNestedStringLiteral_ShouldKeepInterpolatedStringOpaque()
        {
            string source = @"return $""value: {int.Parse(""2"") + 1}"";";

            HoistedLiteralRewriteResult result = DynamicCodeLiteralHoister.Rewrite(source);

            Assert.That(result.RewrittenSource, Is.EqualTo(source));
            Assert.That(result.Bindings, Is.Empty);
            Assert.That(result.DeclarationLines, Is.Empty);
        }
    }
}
