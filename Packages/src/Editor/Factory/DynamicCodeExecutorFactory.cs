#if ULOOPMCP_HAS_ROSLYN
using io.github.hatayama.uLoopMCP;

namespace io.github.hatayama.uLoopMCP.Factory
{
    /// <summary>
    /// DynamicCodeExecutor生成ファクトリー
    /// v4.0 明示的セキュリティレベル指定対応
    /// 関連クラス: DynamicCodeExecutor, RoslynCompiler, SecurityValidator, CommandRunner
    /// </summary>
    public static class DynamicCodeExecutorFactory
    {
        /// <summary>
        /// 指定されたセキュリティレベルでDynamicCodeExecutorを作成
        /// </summary>
        public static IDynamicCodeExecutor Create(DynamicCodeSecurityLevel securityLevel)
        {
            string correlationId = McpConstants.GenerateCorrelationId();

            try
            {
                // コンパイラー初期化（明示的なセキュリティレベル指定）
                RoslynCompiler compiler = new(securityLevel);

                // セキュリティバリデーター初期化（明示的なセキュリティレベル指定）
                SecurityValidator validator = new(securityLevel);

                // コマンドランナー初期化
                CommandRunner runner = new();

                // 統合エグゼキューター作成
                DynamicCodeExecutor executor = new(compiler, validator, securityLevel, runner);

                VibeLogger.LogInfo(
                    "dynamic_executor_created",
                    $"DynamicCodeExecutor created with security level: {securityLevel}",
                    new
                    {
                        security_level = securityLevel.ToString(),
                        compiler_type = compiler.GetType().Name,
                        validator_type = validator.GetType().Name,
                        runner_type = runner.GetType().Name
                    },
                    correlationId,
                    "動的コード実行システム初期化完了",
                    "実行準備完了"
                );

                return executor;
            }
            catch (System.Exception ex)
            {
                VibeLogger.LogError(
                    "dynamic_executor_creation_failed",
                    "Failed to create DynamicCodeExecutor",
                    new
                    {
                        error_type = ex.GetType().Name,
                        error_message = ex.Message
                    },
                    correlationId,
                    "動的コード実行システム初期化失敗",
                    "依存関係の問題を調査"
                );

                throw;
            }
        }
    }
}
#endif