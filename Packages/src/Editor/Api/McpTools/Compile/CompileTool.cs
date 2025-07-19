using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Compile tool handler - Type-safe implementation using Schema and Response
    /// Handles Unity project compilation with optional force recompile
    /// </summary>
    [McpTool(Description = "Execute Unity project compilation")]
    public class CompileTool : AbstractUnityTool<CompileSchema, CompileResponse>
    {
        public override string ToolName => "compile";

        /// <summary>
        /// Execute compile tool
        /// </summary>
        /// <param name="parameters">Type-safe parameters</param>
        /// <param name="cancellationToken">Cancellation token for timeout control</param>
        /// <returns>Compile result</returns>
        protected override async Task<CompileResponse> ExecuteAsync(CompileSchema parameters, CancellationToken cancellationToken)
        {
            // Check for cancellation before starting
            cancellationToken.ThrowIfCancellationRequested();
            
            // Type-safe parameter access - no more string parsing!
            bool forceRecompile = parameters.ForceRecompile;
            
            // Pre-compilation state check
            string stateError = ValidateCompilationState();
            if (!string.IsNullOrEmpty(stateError))
            {
                CompileIssue[] stateErrors = { new(stateError, "", 0) };
                return new CompileResponse(
                    success: false,
                    errorCount: 1,
                    warningCount: 0,
                    errors: stateErrors,
                    warnings: new CompileIssue[0]
                );
            }
            
            // Check for cancellation before compilation
            cancellationToken.ThrowIfCancellationRequested();
            
            // Execute compilation using CompileChecker
            using CompileController compileController = new CompileController();
            CompileResult result = await compileController.TryCompileAsync(forceRecompile);
            
            // Create type-safe response
            CompileIssue[] errors = result.error.Select(e => new CompileIssue(e.message, e.file, e.line)).ToArray();
            CompileIssue[] warnings = result.warning.Select(w => new CompileIssue(w.message, w.file, w.line)).ToArray();
            
            // Add special message for force compile
            string message = forceRecompile 
                ? "Force compilation executed. Error/warning messages are not included in this response due to domain reload timing. Use get-logs tool to retrieve compilation messages after execution."
                : null;
            
            return new CompileResponse(
                success: result.Success,
                errorCount: result.error.Length,
                warningCount: result.warning.Length,
                errors: errors,
                warnings: warnings,
                message: message
            );
        }
        
        /// <summary>
        /// Validate compilation state before attempting to compile
        /// </summary>
        /// <returns>Error message if compilation cannot proceed, empty string if OK</returns>
        private string ValidateCompilationState()
        {
            // Check if compilation is already in progress
            if (EditorApplication.isCompiling)
            {
                return "Compilation is already in progress. Please wait for the current compilation to finish.";
            }
            
            // Check if domain reload is in progress
            if (McpSessionManager.instance.IsDomainReloadInProgress)
            {
                return "Cannot compile while domain reload is in progress. Please wait for the domain reload to complete.";
            }
            
            // Check if editor is updating
            if (EditorApplication.isUpdating)
            {
                return "Cannot compile while editor is updating. Please wait for the update to complete.";
            }
            
            return string.Empty;
        }
    }
}