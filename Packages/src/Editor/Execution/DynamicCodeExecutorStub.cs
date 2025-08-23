using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Stub implementation for DynamicCodeExecutor when Roslyn is disabled
    /// Minimal implementation that always returns a 'Roslyn Required' error
    /// Related classes: IDynamicCodeExecutor, DynamicCodeExecutor, DynamicCodeExecutorFactory
    /// </summary>
    public class DynamicCodeExecutorStub : IDynamicCodeExecutor
    {
        private readonly ExecutionStatistics _statistics;

        public DynamicCodeExecutorStub()
        {
            _statistics = new ExecutionStatistics();
        }

        /// <summary>Execute code (always returns a Roslyn required error)</summary>
        public ExecutionResult ExecuteCode(
            string code,
            string className = DynamicCodeConstants.DEFAULT_CLASS_NAME,
            object[] parameters = null,
            CancellationToken cancellationToken = default,
            bool compileOnly = false)
        {
            return CreateRoslynRequiredResult();
        }

        /// <summary>Execute code asynchronously (always returns a Roslyn required error)</summary>
        public async Task<ExecutionResult> ExecuteCodeAsync(
            string code,
            string className = DynamicCodeConstants.DEFAULT_CLASS_NAME,
            object[] parameters = null,
            CancellationToken cancellationToken = default,
            bool compileOnly = false)
        {
            return await Task.FromResult(CreateRoslynRequiredResult());
        }

        /// <summary>Retrieve execution statistics</summary>
        public ExecutionStatistics GetStatistics()
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

        private ExecutionResult CreateRoslynRequiredResult()
        {
            return new ExecutionResult
            {
                Success = false,
                ErrorMessage = $"{McpConstants.ERROR_ROSLYN_REQUIRED}: {McpConstants.ERROR_MESSAGE_ROSLYN_REQUIRED}",
                ExecutionTime = TimeSpan.Zero,
                Statistics = _statistics
            };
        }
    }
}