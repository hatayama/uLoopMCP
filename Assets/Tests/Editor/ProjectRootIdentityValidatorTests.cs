using NUnit.Framework;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    [TestFixture]
    public sealed class ProjectRootIdentityValidatorTests
    {
        [Test]
        public void Validate_WhenExpectedProjectRootIsMissing_ReturnsInvalidResult()
        {
            // Verifies that missing expected project roots are rejected before request execution.
            ValidationResult result = ProjectRootIdentityValidator.Validate(string.Empty, "/project");

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("expectedProjectRoot is required"));
        }

        [Test]
        public void Validate_WhenActualProjectRootIsUnavailable_ReturnsInvalidResult()
        {
            // Verifies that unavailable actual project roots fail closed instead of allowing execution.
            ValidationResult result = ProjectRootIdentityValidator.Validate("/project", string.Empty);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Fast project validation is unavailable"));
        }

        [Test]
        public void Validate_WhenProjectRootDiffers_ReturnsInvalidResult()
        {
            // Verifies that a CLI request for another project cannot execute against this Unity instance.
            ValidationResult result = ProjectRootIdentityValidator.Validate("/project-a", "/project-b");

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("different project"));
        }

        [Test]
        public void Validate_WhenProjectRootMatches_ReturnsValidResult()
        {
            // Verifies that matching project identity allows request execution to continue.
            ValidationResult result = ProjectRootIdentityValidator.Validate("/project", "/project");

            Assert.That(result.IsValid, Is.True);
        }
    }
}
