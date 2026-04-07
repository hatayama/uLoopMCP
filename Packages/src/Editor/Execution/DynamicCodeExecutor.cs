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
        private readonly IDynamicCompilationService _compiler;
        private readonly DynamicCodeSecurityLevel _securityLevel;
        private readonly CommandRunner _runner;
        private readonly ExecutionStatistics _statistics;
        private readonly object _statsLock = new();

        /// <summary>Constructor</summary>
        public DynamicCodeExecutor(
            IDynamicCompilationService compiler,
            DynamicCodeSecurityLevel securityLevel,
            CommandRunner runner)
        {
            _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
            _securityLevel = securityLevel;
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _statistics = new ExecutionStatistics();
        }

        /// <summary>Code Execution</summary>
        public ExecutionResult ExecuteCode(
            string code,
            string className = DynamicCodeConstants.DEFAULT_CLASS_NAME,
            object[] parameters = null,
            CancellationToken cancellationToken = default,
            bool compileOnly = false)
        {
            throw new NotSupportedException("ExecuteCode blocks Unity's main thread. Use ExecuteCodeAsync instead.");
        }

        private void LogExecutionStart(string className, object[] parameters, string code, bool compileOnly, string correlationId)
        {
        }

        private ExecutionResult HandleCompilationResult(CompilationResult compilationResult, Stopwatch stopwatch)
        {
            if (!compilationResult.Success)
            {
                if (compilationResult.HasSecurityViolations)
                {
                    return CreateFailureResult("Security violations detected",
                        stopwatch.Elapsed,
                        ConvertToSecurityViolations(compilationResult.SecurityViolations),
                        compilationResult.Timings);
                }
                return CreateCompilationFailureResult("Compilation error occurred",
                    stopwatch.Elapsed, compilationResult);
            }

            if (compilationResult.HasSecurityViolations)
            {
                return CreateFailureResult("Security violations detected (Dangerous API call)",
                    stopwatch.Elapsed,
                    compilationResult.SecurityViolations,
                    compilationResult.Timings);
            }

            return null; // Success
        }

        private ExecutionResult CreateCompileOnlySuccessResult(CompilationResult compilationResult, string correlationId, Stopwatch stopwatch)
        {
            return new ExecutionResult
            {
                Success = true,
                Result = null,
                ExecutionTime = stopwatch.Elapsed,
                Logs = new List<string> { "Code compiled successfully (no execution)" },
                AutoInjectedNamespaces = compilationResult.AutoInjectedNamespaces,
                Timings = new List<string>(compilationResult.Timings)
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
        public async Task<ExecutionResult> ExecuteCodeAsync(
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
                PreparedDynamicCode preparedCode = DynamicCodeSourcePreparer.Prepare(
                    code,
                    DynamicCodeConstants.DEFAULT_NAMESPACE,
                    className);

                // Unity Editor APIs (Undo, AssetDatabase) require the main thread, so do not use ConfigureAwait(false) here
                CompilationResult compilationResult = await CompileCodeAsync(code, className, correlationId, cancellationToken);
                ExecutionResult compilationErrorResult = HandleCompilationResult(compilationResult, stopwatch);
                if (compilationErrorResult != null) return compilationErrorResult;

                // Phase 3: Check Compile-Only Mode
                if (compileOnly)
                {
                    return CreateCompileOnlySuccessResult(compilationResult, correlationId, stopwatch);
                }

                // Phase 4: Execution (async via CommandRunner)
                ExecutionContext context = new ExecutionContext
                {
                    CompiledAssembly = compilationResult.CompiledAssembly,
                    Parameters = ConvertParametersToDict(parameters ?? new object[0], preparedCode.HoistedLiteralBindings),
                    CancellationToken = cancellationToken
                };

                Stopwatch executionStopwatch = Stopwatch.StartNew();
                ExecutionResult executionResult = await _runner.ExecuteAsync(context);
                executionStopwatch.Stop();
                executionResult.ExecutionTime = stopwatch.Elapsed;
                executionResult.AutoInjectedNamespaces = compilationResult.AutoInjectedNamespaces;
                executionResult.Timings = MergeTimings(
                    compilationResult.Timings,
                    executionResult.Timings,
                    $"[Perf] Execution: {executionStopwatch.Elapsed.TotalMilliseconds:F1}ms");
                UpdateStatistics(executionResult, stopwatch.Elapsed);
                return executionResult;
            }
            catch (Exception ex)
            {
                return HandleExecutionException(ex, correlationId, stopwatch);
            }
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

        public void Dispose()
        {
            if (_compiler is IDisposable disposableCompiler)
            {
                disposableCompiler.Dispose();
            }
        }

        private async Task<CompilationResult> CompileCodeAsync(string code, string className, string correlationId, CancellationToken ct)
        {
            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = className,
                Namespace = DynamicCodeConstants.DEFAULT_NAMESPACE
            };

            CompilationResult result = await _compiler.CompileAsync(request, ct);

            if (!result.Success)
            {
                lock (_statsLock)
                {
                    _statistics.CompilationErrors++;
                }
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
                Parameters = ConvertParametersToDict(parameters ?? new object[0], new List<HoistedLiteralBinding>()),
                CancellationToken = cancellationToken
            };
            return _runner.Execute(context);
        }

        private ExecutionResult CreateFailureResult(
            string message,
            TimeSpan executionTime,
            List<SecurityViolation> violations,
            List<string> timings)
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
                ExecutionTime = executionTime,
                Timings = timings != null ? new List<string>(timings) : new List<string>()
            };
        }

        private ExecutionResult CreateCompilationFailureResult(string message, TimeSpan executionTime,
            CompilationResult compilationResult)
        {
            return new ExecutionResult
            {
                Success = false,
                ErrorMessage = message,
                // Do not include raw per-error lines here; the tool layer will format DiagnosticsSummary
                Logs = new List<string>(),
                ExecutionTime = executionTime,
                CompilationErrors = compilationResult.Errors,
                UpdatedCode = compilationResult.UpdatedCode,
                AmbiguousTypeCandidates = compilationResult.AmbiguousTypeCandidates,
                AutoInjectedNamespaces = compilationResult.AutoInjectedNamespaces,
                Timings = new List<string>(compilationResult.Timings)
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
                ExecutionTime = executionTime,
                Timings = new List<string>()
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

        private Dictionary<string, object> ConvertParametersToDict(
            object[] parameters,
            IReadOnlyCollection<HoistedLiteralBinding> hoistedLiteralBindings)
        {
            Dictionary<string, object> dict = new();
            if (hoistedLiteralBindings != null)
            {
                foreach (HoistedLiteralBinding binding in hoistedLiteralBindings)
                {
                    dict[binding.ParameterName] = binding.Value;
                }
            }

            if (parameters != null)
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    dict[$"param{i}"] = parameters[i];
                }
            }
            return dict;
        }

        private List<string> MergeTimings(
            List<string> compilationTimings,
            List<string> executionTimings,
            string executionEntry)
        {
            List<string> mergedTimings = new List<string>();

            if (compilationTimings != null)
            {
                mergedTimings.AddRange(compilationTimings);
            }

            if (executionTimings != null)
            {
                mergedTimings.AddRange(executionTimings);
            }

            mergedTimings.Add(executionEntry);
            return mergedTimings;
        }
    }
}
