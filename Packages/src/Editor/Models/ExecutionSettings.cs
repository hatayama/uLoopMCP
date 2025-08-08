using System;

namespace io.github.hatayama.uLoopMCP.DynamicExecution
{
    /// <summary>
    /// 実行設定
    /// 設計ドキュメント: uLoopMCP_DynamicCodeExecution_Design.md
    /// 関連クラス: CommandRunner
    /// </summary>
    [Serializable]
    public class ExecutionSettings
    {
        /// <summary>デフォルトタイムアウト（秒）</summary>
        public int DefaultTimeoutSeconds { get; set; } = 60;

        /// <summary>最大タイムアウト（秒）</summary>
        public int MaxTimeoutSeconds { get; set; } = 300;

        /// <summary>メモリ監視を有効にする</summary>
        public bool EnableMemoryMonitoring { get; set; } = true;

        /// <summary>実行ログを詳細化</summary>
        public bool VerboseLogging { get; set; } = false;

        /// <summary>例外をキャッチして継続</summary>
        public bool ContinueOnException { get; set; } = false;

        /// <summary>実行前にガベージコレクションを実行</summary>
        public bool ForceGCBeforeExecution { get; set; } = false;

        /// <summary>
        /// デフォルト設定を取得
        /// </summary>
        public static ExecutionSettings GetDefault()
        {
            return new ExecutionSettings();
        }

        /// <summary>
        /// デバッグ用設定を取得
        /// </summary>
        public static ExecutionSettings GetDebug()
        {
            return new ExecutionSettings
            {
                VerboseLogging = true,
                EnableMemoryMonitoring = true,
                ContinueOnException = true
            };
        }
    }
}