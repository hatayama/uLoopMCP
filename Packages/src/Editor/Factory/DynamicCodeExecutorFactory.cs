#if ULOOPMCP_HAS_ROSLYN
using io.github.hatayama.uLoopMCP;

namespace io.github.hatayama.uLoopMCP.Factory
{
    /// <summary>
    /// DynamicCodeExecutor生成ファクトリー

    /// 関連クラス: DynamicCodeExecutor, RoslynCompiler, SecurityValidator, CommandRunner
    /// </summary>
    public static class DynamicCodeExecutorFactory
    {
        /// <summary>デフォルト設定でDynamicCodeExecutorを作成</summary>
        public static IDynamicCodeExecutor CreateDefault()
        {
            string correlationId = System.Guid.NewGuid().ToString("N")[..8];

            try
            {
                // コンパイラー初期化（現在のセキュリティレベルで自動初期化される）
                RoslynCompiler compiler = new RoslynCompiler();

                // セキュリティバリデーター初期化（現在のセキュリティレベル使用）
                SecurityValidator validator = new SecurityValidator(DynamicCodeSecurityManager.CurrentLevel);

                // コマンドランナー初期化
                CommandRunner runner = new CommandRunner();

                // 統合エグゼキューター作成
                DynamicCodeExecutor executor = new DynamicCodeExecutor(compiler, validator, runner);

                VibeLogger.LogInfo(
                    "dynamic_executor_created",
                    "DynamicCodeExecutor created with default settings",
                    new
                    {
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

        /// <summary>厳格なセキュリティ設定でDynamicCodeExecutorを作成</summary>
        public static IDynamicCodeExecutor CreateStrict()
        {
            string correlationId = System.Guid.NewGuid().ToString("N")[..8];

            try
            {
                RoslynCompiler compiler = new RoslynCompiler();
                SecurityValidator validator = new SecurityValidator(DynamicCodeSecurityManager.CurrentLevel);
                CommandRunner runner = new CommandRunner();

                DynamicCodeExecutor executor = new DynamicCodeExecutor(compiler, validator, runner);

                VibeLogger.LogInfo(
                    "dynamic_executor_created_strict",
                    "DynamicCodeExecutor created with strict security settings",
                    new
                    {
                        compiler_type = compiler.GetType().Name,
                        validator_type = validator.GetType().Name,
                        runner_type = runner.GetType().Name
                    },
                    correlationId,
                    "厳格セキュリティの動的コード実行システム初期化完了",
                    "セキュリティ設定の確認"
                );

                return executor;
            }
            catch (System.Exception ex)
            {
                VibeLogger.LogError(
                    "dynamic_executor_creation_failed_strict",
                    "Failed to create strict DynamicCodeExecutor",
                    new
                    {
                        error_type = ex.GetType().Name,
                        error_message = ex.Message
                    },
                    correlationId,
                    "厳格セキュリティ動的コード実行システム初期化失敗",
                    "セキュリティ設定との互換性を調査"
                );

                throw;
            }
        }
    }
}
#endif