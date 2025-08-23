#if ULOOPMCP_HAS_ROSLYN
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Integrated Dynamic Code Execution Implementation
    /// Related Classes: RoslynCompiler, SecurityValidator, CommandRunner
    /// </summary>
    public class DynamicCodeExecutor : IDynamicCodeExecutor
    {
        private readonly RoslynCompiler _compiler;
        private readonly DynamicCodeSecurityLevel _securityLevel;
        private readonly CommandRunner _runner;
        private readonly ExecutionStatistics _statistics;
        private readonly object _statsLock = new();

        /// <summary>Constructor</summary>
        public DynamicCodeExecutor(
            RoslynCompiler compiler,
            SecurityValidator validator,
            DynamicCodeSecurityLevel securityLevel,
            CommandRunner runner)
        {
            _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
            _securityLevel = securityLevel;
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _statistics = new ExecutionStatistics();

            VibeLogger.LogInfo(
                "dynamic_code_executor_initialized",
                "DynamicCodeExecutor initialized with dependencies",
                new
                {
                    compiler_type = compiler.GetType().Name,
                    validator_type = validator.GetType().Name,
                    runner_type = runner.GetType().Name
                },
                humanNote: "Initializing Integrated Dynamic Code Execution System",
                aiTodo: "Start Collecting Execution Statistics"
            );
        }

        /// <summary>Code Execution</summary>
        public ExecutionResult ExecuteCode(
            string code,
            string className = DynamicCodeConstants.DEFAULT_CLASS_NAME,
            object[] parameters = null,
            CancellationToken cancellationToken = default,
            bool compileOnly = false)
        {
            string correlationId = McpConstants.GenerateCorrelationId();
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                LogExecutionStart(className, parameters, code, compileOnly, correlationId);

                // Phase 1: Security Validation
                ExecutionResult securityResult = PerformSecurityValidation(code, correlationId, stopwatch);
                if (!securityResult.Success) return securityResult;

                // Phase 2: Compilation
                CompilationResult compilationResult = CompileCode(code, className, correlationId);
                ExecutionResult compilationErrorResult = HandleCompilationResult(compilationResult, stopwatch);
                if (compilationErrorResult != null) return compilationErrorResult;

                // Phase 3: Check Compile-Only Mode
                if (compileOnly)
                {
                    return CreateCompileOnlySuccessResult(compilationResult, correlationId, stopwatch);
                }

                // Phase 4: Execution
                ExecutionResult executionResult = PerformExecution(
                    compilationResult.CompiledAssembly,
                    className,
                    parameters,
                    correlationId,
                    cancellationToken,
                    stopwatch);

                LogExecutionComplete(executionResult, correlationId, stopwatch);
                return executionResult;
            }
            catch (Exception ex)
            {
                return HandleExecutionException(ex, correlationId, stopwatch);
            }
        }

        private void LogExecutionStart(string className, object[] parameters, string code, bool compileOnly, string correlationId)
        {
            VibeLogger.LogInfo(
                "execute_code_start",
                "Code execution started",
                new
                {
                    class_name = className,
                    parameter_count = parameters?.Length ?? 0,
                    code_length = code.Length,
                    compile_only = compileOnly
                },
                correlationId,
                "Dynamic Code Execution Started",
                "Tracking Execution Flow Steps"
            );
        }

        private ExecutionResult PerformSecurityValidation(string code, string correlationId, Stopwatch stopwatch)
        {
            SecurityValidationResult validationResult = ValidateCodeSecurity(code, correlationId);
            if (!validationResult.IsValid)
            {
                return CreateFailureResult("Security violations detected",
                    stopwatch.Elapsed, validationResult.Violations);
            }
            return new ExecutionResult { Success = true };
        }

        private ExecutionResult HandleCompilationResult(CompilationResult compilationResult, Stopwatch stopwatch)
        {
            if (!compilationResult.Success)
            {
                if (compilationResult.HasSecurityViolations)
                {
                    return CreateFailureResult("Security violations detected",
                        stopwatch.Elapsed, ConvertToSecurityViolations(compilationResult.SecurityViolations));
                }
                return CreateCompilationFailureResult("Compilation error occurred",
                    stopwatch.Elapsed, compilationResult.Errors);
            }

            if (compilationResult.HasSecurityViolations)
            {
                return CreateFailureResult("Security violations detected (Dangerous API call)",
                    stopwatch.Elapsed, compilationResult.SecurityViolations);
            }

            return null; // Success
        }

        private ExecutionResult CreateCompileOnlySuccessResult(CompilationResult compilationResult, string correlationId, Stopwatch stopwatch)
        {
            VibeLogger.LogInfo(
                "compile_only_complete",
                "Compilation completed successfully (compile-only mode)",
                new
                {
                    compile_time_ms = stopwatch.ElapsedMilliseconds,
                    assembly_name = compilationResult.CompiledAssembly?.FullName
                },
                correlationId,
                "Compile-Only Mode Completed",
                "Verifying Compilation Results"
            );

            return new ExecutionResult
            {
                Success = true,
                Result = null,
                ExecutionTime = stopwatch.Elapsed,
                Logs = new List<string> { "Code compiled successfully (no execution)" }
            };
        }

        private ExecutionResult PerformExecution(
            System.Reflection.Assembly assembly,
            string className,
            object[] parameters,
            string correlationId,
            CancellationToken cancellationToken,
            Stopwatch stopwatch)
        {
            ExecutionResult executionResult = ExecuteCompiledCode(
                assembly,
                className,
                parameters,
                correlationId,
                cancellationToken);

            executionResult.ExecutionTime = stopwatch.Elapsed;
            UpdateStatistics(executionResult, stopwatch.Elapsed);

            return executionResult;
        }

        private void LogExecutionComplete(ExecutionResult executionResult, string correlationId, Stopwatch stopwatch)
        {
            VibeLogger.LogInfo(
                "execute_code_complete",
                $"Code execution completed: {(executionResult.Success ? "Success" : "Failed")}",
                new
                {
                    success = executionResult.Success,
                    execution_time_ms = stopwatch.ElapsedMilliseconds,
                    result_length = executionResult.Result?.ToString()?.Length ?? 0
                },
                correlationId,
                "Dynamic Code Execution Completed",
                "Recording Execution Results and Performance"
            );
        }

        private ExecutionResult HandleExecutionException(Exception ex, string correlationId, Stopwatch stopwatch)
        {
            ExecutionResult result = CreateExceptionResult("An unexpected error occurred during execution",
                ex, stopwatch.Elapsed);
            UpdateStatistics(result, stopwatch.Elapsed);

            VibeLogger.LogError(
                "execute_code_exception",
                "Unexpected error during code execution",
                new
                {
                    exception_type = ex.GetType().Name,
                    execution_time_ms = stopwatch.ElapsedMilliseconds
                },
                correlationId,
                "Dynamic Code Execution Error",
                "Investigating Unexpected Exception"
            );

            return result;
        }

        /// <summary>Asynchronous Code Execution</summary>
#pragma warning disable CS1998 // Async method lacks 'await' operators (Occurs only when Roslyn is not available)
        public async Task<ExecutionResult> ExecuteCodeAsync(
            string code,
            string className = DynamicCodeConstants.DEFAULT_CLASS_NAME,
            object[] parameters = null,
            CancellationToken cancellationToken = default,
            bool compileOnly = false)
        {
#pragma warning restore CS1998
            // Runtime Security Check
            ExecutionResult securityCheckResult = PerformRuntimeSecurityCheck(code);
            if (!securityCheckResult.Success) return securityCheckResult;

            // Already switched to Main Thread via JsonRpcProcessor
            return await Task.FromResult(ExecuteCode(code, className, parameters, cancellationToken, compileOnly));
        }

        private ExecutionResult PerformRuntimeSecurityCheck(string code)
        {
            // Execution Disabled Check
            if (_securityLevel == DynamicCodeSecurityLevel.Disabled)
            {
                return CreateSecurityBlockedResult(
                    McpConstants.ERROR_EXECUTION_DISABLED,
                    McpConstants.ERROR_MESSAGE_EXECUTION_DISABLED);
            }

            // Block Security Level Change in Restricted Mode
            if (_securityLevel == DynamicCodeSecurityLevel.Restricted)
            {
                if (ContainsSecurityLevelChangeAttempt(code))
                {
                    return CreateSecurityBlockedResult(
                        McpConstants.ERROR_SECURITY_VIOLATION,
                        McpConstants.ERROR_MESSAGE_SECURITY_LEVEL_CHANGE_BLOCKED);
                }
            }

            return new ExecutionResult { Success = true };
        }

        private bool ContainsSecurityLevelChangeAttempt(string code)
        {
            return code.Contains("SetDynamicCodeSecurityLevel") ||
                   code.Contains("InitializeFromSettings") ||
                   code.Contains("McpEditorSettings.SetDynamicCodeSecurityLevel");
        }

        private ExecutionResult CreateSecurityBlockedResult(string errorCode, string errorMessage)
        {
            ExecutionStatistics stats;
            lock (_statsLock)
            {
                stats = new ExecutionStatistics
                {
                    TotalExecutions = _statistics.TotalExecutions,
                    SuccessfulExecutions = _statistics.SuccessfulExecutions,
                    FailedExecutions = _statistics.FailedExecutions,
                    AverageExecutionTime = _statistics.AverageExecutionTime,
                    SecurityViolations = _statistics.SecurityViolations,
                    CompilationErrors = _statistics.CompilationErrors
                };
            }

            return new ExecutionResult
            {
                Success = false,
                ErrorMessage = $"{errorCode}: {errorMessage}",
                ExecutionTime = TimeSpan.Zero,
                Result = null,
                Statistics = stats
            };
        }

        /// <summary>Get execution statistics</summary>
        public ExecutionStatistics GetStatistics()
        {
            lock (_statsLock)
            {
                return new ExecutionStatistics
                {
                    TotalExecutions = _statistics.TotalExecutions,
                    SuccessfulExecutions = _statistics.SuccessfulExecutions,
                    FailedExecutions = _statistics.FailedExecutions,
                    AverageExecutionTime = _statistics.AverageExecutionTime,
                    SecurityViolations = _statistics.SecurityViolations,
                    CompilationErrors = _statistics.CompilationErrors
                };
            }
        }

        // Private Helper Methods
        private SecurityValidationResult ValidateCodeSecurity(string code, string correlationId)
        {
            // In Restricted Mode, defer security validation to Roslyn
            // Perform only basic checks here (such as empty string check)
            if (string.IsNullOrWhiteSpace(code))
            {
                return new SecurityValidationResult
                {
                    IsValid = false,
                    Violations = new List<SecurityViolation>
                    {
                        new SecurityViolation
                        {
                            Type = SecurityViolationType.DangerousApiCall,
                            Description = "Code is empty",
                            LineNumber = 0,
                            CodeSnippet = string.Empty
                        }
                    }
                };
            }

            // Since detailed checks are performed by SecurityValidator during Roslyn compilation
            // Pass through with only basic validation here
            VibeLogger.LogInfo(
                "security_pre_validation_passed",
                "Pre-validation passed, detailed check will be done during compilation",
                new { code_length = code.Length },
                correlationId,
                "Pre-validation passed (details during compilation)",
                "Focusing on Roslyn validation during compilation"
            );

            return new SecurityValidationResult
            {
                IsValid = true,
                Violations = new List<SecurityViolation>()
            };
        }

        private CompilationResult CompileCode(string code, string className, string correlationId)
        {
            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = className,
                Namespace = DynamicCodeConstants.DEFAULT_NAMESPACE
            };

            CompilationResult result = _compiler.Compile(request);

            if (!result.Success)
            {
                lock (_statsLock)
                {
                    _statistics.CompilationErrors++;
                }

                VibeLogger.LogWarning(
                    "compilation_failed",
                    $"Code compilation failed with {result.Errors.Count} errors",
                    new
                    {
                        error_count = result.Errors.Count,
                        warning_count = result.Warnings.Count
                    },
                    correlationId,
                    "Compilation Failed",
                    "Analyzing Causes of Compilation Errors"
                );
            }

            return result;
        }

        private ExecutionResult ExecuteCompiledCode(
            System.Reflection.Assembly assembly, 
            string className,
            object[] parameters, 
            string correlationId,
            CancellationToken cancellationToken)
        {
            // Create ExecutionContext and call Execute method
            ExecutionContext context = new ExecutionContext
            {
                CompiledAssembly = assembly,
                Parameters = ConvertParametersToDict(parameters ?? new object[0]),
                CancellationToken = cancellationToken
            };
            return _runner.Execute(context);
        }

        private ExecutionResult CreateFailureResult(string message, TimeSpan executionTime, 
            List<SecurityViolation> violations)
        {
            List<string> violationMessages = new List<string>();
            foreach (SecurityViolation violation in violations)
            {
                // Preferentially use new properties (Message, ApiName)
                string violationMessage = !string.IsNullOrEmpty(violation.Message) 
                    ? violation.Message 
                    : violation.Description;
                    
                if (!string.IsNullOrEmpty(violation.ApiName))
                {
                    violationMessages.Add($"{violation.Type}: {violationMessage} (API: {violation.ApiName})");
                }
                else
                {
                    violationMessages.Add($"{violation.Type}: {violationMessage}");
                }
            }

            // Add details to error message
            if (violations.Count > 0)
            {
                message = $"{message} {string.Join(" ", violationMessages)}";
            }

            return new ExecutionResult
            {
                Success = false,
                ErrorMessage = message,
                Logs = violationMessages,
                ExecutionTime = executionTime
            };
        }

        private ExecutionResult CreateCompilationFailureResult(string message, TimeSpan executionTime,
            List<CompilationError> errors)
        {
            List<string> errorMessages = new List<string>();
            foreach (CompilationError error in errors)
            {
                errorMessages.Add($"Line {error.Line}: {error.Message}");
            }

            return new ExecutionResult
            {
                Success = false,
                ErrorMessage = message,
                Logs = errorMessages,
                ExecutionTime = executionTime
            };
        }

        /// <summary>
        /// Convert CompilationResult.SecurityViolations to SecurityValidator's SecurityViolation
        /// </summary>
        private List<SecurityViolation> ConvertToSecurityViolations(List<SecurityViolation> compilationSecurityViolations)
        {
            // Return as-is since it's the same type
            return compilationSecurityViolations ?? new List<SecurityViolation>();
        }

        private ExecutionResult CreateExceptionResult(string message, Exception ex, TimeSpan executionTime)
        {
            return new ExecutionResult
            {
                Success = false,
                ErrorMessage = message,
                Exception = ex,
                Logs = new List<string> { ex.ToString() },
                ExecutionTime = executionTime
            };
        }

        private void UpdateStatistics(ExecutionResult result, TimeSpan executionTime)
        {
            lock (_statsLock)
            {
                _statistics.TotalExecutions++;

                if (result.Success)
                {
                    _statistics.SuccessfulExecutions++;
                }
                else
                {
                    _statistics.FailedExecutions++;
                }

                // Update average execution time (simple moving average)
                double totalMs = _statistics.AverageExecutionTime.TotalMilliseconds * (_statistics.TotalExecutions - 1);
                totalMs += executionTime.TotalMilliseconds;
                _statistics.AverageExecutionTime = TimeSpan.FromMilliseconds(totalMs / _statistics.TotalExecutions);
            }
        }

        private Dictionary<string, object> ConvertParametersToDict(object[] parameters)
        {
            Dictionary<string, object> dict = new();
            if (parameters != null)
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    dict[$"param{i}"] = parameters[i];
                }
            }
            return dict;
        }
    }
}
#endif