using System;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Handles temporal cohesion for compilation processing
    /// Processing sequence: 1. Play Mode preparation, 2. Compilation state validation, 3. Compilation execution, 4. Result formatting
    /// Related classes: CompileTool, PlayModeCompilationPreparationService, CompilationStateValidationService, CompilationExecutionService
    /// Design reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// </summary>
    public class CompileUseCase : AbstractUseCase<CompileSchema, CompileResponse>
    {
        private const int MAX_WAIT_MS = 5000;
        private const int POLL_INTERVAL_MS = 50;

        /// <summary>
        /// Executes compilation processing
        /// </summary>
        /// <param name="parameters">Compilation parameters</param>
        /// <param name="ct">Cancellation control token</param>
        /// <returns>Compilation result</returns>
        public override async Task<CompileResponse> ExecuteAsync(CompileSchema parameters, CancellationToken ct)
        {
            // 1. Play Mode preparation check
            PlayModeCompilationPreparationService preparationService = new();
            PreparationResult preparation = preparationService.DeterminePreparationAction();

            if (!preparation.CanProceed)
            {
                return new CompileResponse(
                    success: false,
                    errorCount: 1,
                    warningCount: 0,
                    errors: new[] { new CompileIssue(preparation.ErrorMessage, "", 0) },
                    warnings: Array.Empty<CompileIssue>()
                );
            }

            if (preparation.NeedsPlayModeStop)
            {
                preparationService.StopPlayMode();
                bool exited = await WaitForPlayModeExitAsync(ct);
                if (!exited)
                {
                    return new CompileResponse(
                        success: false,
                        errorCount: 1,
                        warningCount: 0,
                        errors: new[] { new CompileIssue("Play Mode did not exit within 5 seconds; compilation aborted.", "", 0) },
                        warnings: Array.Empty<CompileIssue>()
                    );
                }
            }

            // 2. Compilation state validation
            CompilationStateValidationService validationService = new();
            ValidationResult validation = validationService.ValidateCompilationState();
            
            if (!validation.IsValid)
            {
                return new CompileResponse(
                    success: false,
                    errorCount: 1,
                    warningCount: 0,
                    errors: new[] { new CompileIssue(validation.ErrorMessage, "", 0) },
                    warnings: Array.Empty<CompileIssue>()
                );
            }

            // 3. Compilation execution
            ct.ThrowIfCancellationRequested();
            CompilationExecutionService executionService = new();
            CompileResult result = await executionService.ExecuteCompilationAsync(parameters.ForceRecompile, ct);
            
            // 4. Result formatting
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

        private async Task<bool> WaitForPlayModeExitAsync(CancellationToken ct)
        {
            int waitedMs = 0;

            while (EditorApplication.isPlaying && waitedMs < MAX_WAIT_MS)
            {
                ct.ThrowIfCancellationRequested();
                await TimerDelay.Wait(POLL_INTERVAL_MS, ct);
                waitedMs += POLL_INTERVAL_MS;
            }

            return !EditorApplication.isPlaying;
        }
    }
}