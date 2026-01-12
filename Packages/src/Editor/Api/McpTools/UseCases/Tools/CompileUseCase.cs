using System.Threading.Tasks;
using System.Threading;
using System.Linq;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Handles temporal cohesion for compilation processing
    /// Processing sequence: 1. Compilation state validation, 2. Compilation execution, 3. Result formatting
    /// Related classes: CompileTool, CompilationStateValidationService, CompilationExecutionService
    /// Design reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// </summary>
    public class CompileUseCase : AbstractUseCase<CompileSchema, CompileResponse>
    {
        /// <summary>
        /// Executes compilation processing
        /// </summary>
        /// <param name="parameters">Compilation parameters</param>
        /// <param name="cancellationToken">Cancellation control token</param>
        /// <returns>Compilation result</returns>
        public override async Task<CompileResponse> ExecuteAsync(CompileSchema parameters, CancellationToken cancellationToken)
        {
            // 1. Compilation state validation
            CompilationStateValidationService validationService = new();
            ValidationResult validation = validationService.ValidateCompilationState();
            
            if (!validation.IsValid)
            {
                return new CompileResponse(
                    success: false,
                    errorCount: 1,
                    warningCount: 0,
                    errors: new[] { new CompileIssue(validation.ErrorMessage, "", 0) },
                    warnings: new CompileIssue[0]
                );
            }
            
            // 2. Compilation execution
            cancellationToken.ThrowIfCancellationRequested();
            CompilationExecutionService executionService = new();
            CompileResult result = await executionService.ExecuteCompilationAsync(parameters.ForceRecompile, cancellationToken);
            
            // 3. Result formatting
            if (result.IsIndeterminate)
            {
                return new CompileResponse(
                    success: result.Success,
                    errorCount: null,
                    warningCount: null,
                    errors: null,
                    warnings: null,
                    message: result.Message ?? "Compilation status is indeterminate. Use get-logs tool to check results."
                );
            }
            
            CompileIssue[] errors = result.error?.Select(e => new CompileIssue(e.message, e.file, e.line)).ToArray();
            CompileIssue[] warnings = result.warning?.Select(w => new CompileIssue(w.message, w.file, w.line)).ToArray();
            
            return new CompileResponse(
                success: result.Success,
                errorCount: result.error?.Length ?? 0,
                warningCount: result.warning?.Length ?? 0,
                errors: errors,
                warnings: warnings
            );
        }
    }
}