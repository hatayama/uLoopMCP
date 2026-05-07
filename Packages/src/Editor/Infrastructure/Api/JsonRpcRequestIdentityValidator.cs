using io.github.hatayama.UnityCliLoop.Domain;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Infrastructure
{
    internal static class JsonRpcRequestIdentityValidator
    {
        public static void Validate(
            JsonRpcRequestUloopMetadata metadata,
            string actualProjectRoot)
        {
            if (metadata == null)
            {
                return;
            }

            ProjectRootIdentityValidationResult validation = ProjectRootIdentityValidator.Validate(
                metadata.ExpectedProjectRoot,
                actualProjectRoot);
            if (!validation.IsValid)
            {
                throw new UnityCliLoopToolParameterValidationException(validation.ErrorMessage);
            }
        }
    }
}
