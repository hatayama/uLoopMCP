using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Application
{
    // Port for validating Unity Editor state before server operations.
    public interface ISecurityValidationService
    {
        ValidationResult ValidateEditorState();
    }
}
