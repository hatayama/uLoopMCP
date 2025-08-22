using System;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// 動的コード実行統合機能のインターフェース

    /// 関連クラス: DynamicCodeExecutor, RoslynCompiler, SecurityValidator, CommandRunner
    /// </summary>
    public interface IDynamicCodeExecutor
    {
        /// <summary>コード実行</summary>
        ExecutionResult ExecuteCode(
            string code,
            string className = DynamicCodeConstants.DEFAULT_CLASS_NAME,
            object[] parameters = null,
            CancellationToken cancellationToken = default,
            bool compileOnly = false
        );

        /// <summary>非同期コード実行</summary>
        System.Threading.Tasks.Task<ExecutionResult> ExecuteCodeAsync(
            string code,
            string className = DynamicCodeConstants.DEFAULT_CLASS_NAME, 
            object[] parameters = null,
            CancellationToken cancellationToken = default,
            bool compileOnly = false
        );



        /// <summary>実行統計取得</summary>
        ExecutionStatistics GetStatistics();
    }

    /// <summary>実行統計</summary>
    public class ExecutionStatistics
    {
        /// <summary>総実行回数</summary>
        public int TotalExecutions { get; set; }

        /// <summary>成功実行回数</summary>
        public int SuccessfulExecutions { get; set; }

        /// <summary>失敗実行回数</summary>
        public int FailedExecutions { get; set; }

        /// <summary>平均実行時間</summary>
        public TimeSpan AverageExecutionTime { get; set; }

        /// <summary>セキュリティ違反回数</summary>
        public int SecurityViolations { get; set; }

        /// <summary>コンパイルエラー回数</summary>
        public int CompilationErrors { get; set; }
    }
}