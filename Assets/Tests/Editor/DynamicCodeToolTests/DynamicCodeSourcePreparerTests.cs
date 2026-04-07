using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    [TestFixture]
    public class DynamicCodeSourcePreparerTests
    {
        [Test]
        public void Prepare_WhenOnlyLiteralValuesDiffer_ShouldGenerateSamePreparedSource()
        {
            PreparedDynamicCode first = DynamicCodeSourcePreparer.Prepare(
                "int benchNonce = 100; return benchNonce;",
                DynamicCodeConstants.DEFAULT_NAMESPACE,
                DynamicCodeConstants.DEFAULT_CLASS_NAME);
            PreparedDynamicCode second = DynamicCodeSourcePreparer.Prepare(
                "int benchNonce = 200; return benchNonce;",
                DynamicCodeConstants.DEFAULT_NAMESPACE,
                DynamicCodeConstants.DEFAULT_CLASS_NAME);

            Assert.AreEqual(first.PreparedSource, second.PreparedSource);
            Assert.AreEqual(1, first.HoistedLiteralBindings.Count);
            Assert.AreEqual(1, second.HoistedLiteralBindings.Count);
            Assert.AreEqual(100, first.HoistedLiteralBindings[0].Value);
            Assert.AreEqual(200, second.HoistedLiteralBindings[0].Value);
        }

        [Test]
        public void Prepare_WhenStringLiteralExists_ShouldEmitPreambleBeforeUserCodeMarker()
        {
            PreparedDynamicCode prepared = DynamicCodeSourcePreparer.Prepare(
                "return \"hello\";",
                DynamicCodeConstants.DEFAULT_NAMESPACE,
                DynamicCodeConstants.DEFAULT_CLASS_NAME);

            Assert.IsNotNull(prepared.PreparedSource);
            StringAssert.Contains("string __uloop_literal_0 = (string)parameters[\"__uloop_literal_0\"];", prepared.PreparedSource);
            Assert.Less(
                prepared.PreparedSource.IndexOf("string __uloop_literal_0 = (string)parameters[\"__uloop_literal_0\"];", System.StringComparison.Ordinal),
                prepared.PreparedSource.IndexOf(WrapperTemplate.UserCodeStartMarker, System.StringComparison.Ordinal));
        }

        [Test]
        public void Prepare_WhenInterpolatedStringExists_ShouldSkipLiteralHoisting()
        {
            PreparedDynamicCode prepared = DynamicCodeSourcePreparer.Prepare(
                "return $\"outer {$\"inner {1}\"}\";",
                DynamicCodeConstants.DEFAULT_NAMESPACE,
                DynamicCodeConstants.DEFAULT_CLASS_NAME);

            Assert.IsNotNull(prepared.PreparedSource);
            Assert.AreEqual(0, prepared.HoistedLiteralBindings.Count);
            StringAssert.Contains("return $\"outer {$\"inner {1}\"}\";", prepared.PreparedSource);
        }
    }
}
