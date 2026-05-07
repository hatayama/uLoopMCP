namespace io.github.hatayama.UnityCliLoop
{
    // Port for validating Unity Editor state before server operations.
    public interface ISecurityValidationService
    {
        ValidationResult ValidateEditorState();
    }
}
