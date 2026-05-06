using System;

namespace io.github.hatayama.UnityCliLoop
{
    public static class ProjectRootIdentityValidator
    {
        public static ValidationResult Validate(
            string expectedProjectRoot,
            string actualProjectRoot)
        {
            if (string.IsNullOrWhiteSpace(expectedProjectRoot))
            {
                return ValidationResult.Failure("Invalid x-uloop metadata: expectedProjectRoot is required.");
            }

            if (string.IsNullOrWhiteSpace(actualProjectRoot))
            {
                return ValidationResult.Failure("Fast project validation is unavailable. Restart Unity CLI Loop and retry.");
            }

            if (!string.Equals(expectedProjectRoot, actualProjectRoot, StringComparison.Ordinal))
            {
                return ValidationResult.Failure("Connected Unity instance belongs to a different project.");
            }

            return ValidationResult.Success();
        }
    }
}
