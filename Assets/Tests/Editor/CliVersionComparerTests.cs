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
    public class CliVersionComparerTests
    {
        [TestCase("3.0.0-beta.0", "3.0.0-beta.0", true)]
        [TestCase("3.0.0-beta.1", "3.0.0-beta.0", true)]
        [TestCase("3.0.0", "3.0.0-beta.0", true)]
        [TestCase("v3.0.0-beta.0", "3.0.0-beta.0", true)]
        [TestCase("3.0.0-beta.0", "3.0.0-beta.1", false)]
        [TestCase("3.0.0-beta.0", "3.0.0", false)]
        public void IsVersionGreaterThanOrEqual_ReturnsExpectedResult(
            string installedVersion,
            string requiredVersion,
            bool expected)
        {
            bool result = CliVersionComparer.IsVersionGreaterThanOrEqual(
                installedVersion,
                requiredVersion);

            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase("3.0.0-beta.0", "3.0.0-beta.1", true)]
        [TestCase("3.0.0-beta.0", "3.0.0", true)]
        [TestCase("3.0.0", "3.0.0-beta.0", false)]
        public void IsVersionLessThan_ReturnsExpectedResult(
            string leftVersion,
            string rightVersion,
            bool expected)
        {
            bool result = CliVersionComparer.IsVersionLessThan(leftVersion, rightVersion);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void IsVersionGreaterThanOrEqual_WhenVersionIsInvalid_ReturnsFalse()
        {
            bool result = CliVersionComparer.IsVersionGreaterThanOrEqual(
                "3.0.0-beta.0",
                "not-a-version");

            Assert.That(result, Is.False);
        }
    }
}
