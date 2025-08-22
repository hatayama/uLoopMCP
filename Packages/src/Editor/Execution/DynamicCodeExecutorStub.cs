using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Roslyn無効時のDynamicCodeExecutorスタブ実装
    /// 常にRoslyn必要エラーを返す最小実装
    /// 関連クラス: IDynamicCodeExecutor, DynamicCodeExecutor, DynamicCodeExecutorFactory
    /// </summary>
    public class DynamicCodeExecutorStub : IDynamicCodeExecutor
    {
        private readonly ExecutionStatistics _statistics;

        public DynamicCodeExecutorStub()
        {
            _statistics = new ExecutionStatistics();
        }

        /// <summary>コード実行（常にRoslyn必要エラーを返す）</summary>
        public ExecutionResult ExecuteCode(
            string code,
            string className = DynamicCodeConstants.DEFAULT_CLASS_NAME,
            object[] parameters = null,
            CancellationToken cancellationToken = default,
            bool compileOnly = false)
        {
            return CreateRoslynRequiredResult();
        }

        /// <summary>非同期コード実行（常にRoslyn必要エラーを返す）</summary>
        public async Task<ExecutionResult> ExecuteCodeAsync(
            string code,
            string className = DynamicCodeConstants.DEFAULT_CLASS_NAME,
            object[] parameters = null,
            CancellationToken cancellationToken = default,
            bool compileOnly = false)
        {
            return await Task.FromResult(CreateRoslynRequiredResult());
        }

        /// <summary>実行統計取得</summary>
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