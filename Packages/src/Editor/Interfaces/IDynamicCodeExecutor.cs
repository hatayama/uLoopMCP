using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace uLoopMCP.DynamicExecution
{
    /// <summary>
    /// 動的コード実行機能のメインインターフェース（テスト用モック対応）
    /// 設計ドキュメント: uLoopMCP_DynamicCodeExecution_Design.md
    /// 関連クラス: DynamicCodeExecutor, ExecutionRequest, ExecutionResponse
    /// </summary>
    public interface IDynamicCodeExecutor
    {
        /// <summary>
        /// C#コードを非同期で実行する
        /// </summary>
        /// <param name="request">実行リクエスト</param>
        /// <returns>実行結果</returns>
        Task<ExecutionResponse> ExecuteAsync(ExecutionRequest request);

        /// <summary>
        /// コードの検証のみ実行（コンパイルチェックとセキュリティチェック）
        /// </summary>
        /// <param name="code">検証するC#コード</param>
        /// <returns>検証結果</returns>
        ExecutionResponse ValidateCode(string code);

        /// <summary>
        /// キャッシュをクリアする
        /// </summary>
        void ClearCache();
    }

    /// <summary>
    /// 実行リクエストのデータモデル
    /// </summary>
    public class ExecutionRequest
    {
        /// <summary>実行するC#コード</summary>
        public string Code { get; set; } = "";

        /// <summary>タイムアウト秒数（デフォルト60秒）</summary>
        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>ドライラン（コンパイルのみ実行）</summary>
        public bool DryRun { get; set; } = false;

        /// <summary>実行時パラメータ</summary>
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// 実行レスポンスのデータモデル
    /// </summary>
    public class ExecutionResponse
    {
        /// <summary>実行成功フラグ</summary>
        public bool Success { get; set; }

        /// <summary>実行結果</summary>
        public string Result { get; set; } = "";

        /// <summary>ログメッセージ</summary>
        public List<string> Logs { get; set; } = new();

        /// <summary>コンパイルエラー</summary>
        public List<CompilationError> CompilationErrors { get; set; } = new();

        /// <summary>エラーメッセージ</summary>
        public string ErrorMessage { get; set; } = "";

        /// <summary>実行時間</summary>
        public TimeSpan ExecutionTime { get; set; }
    }

    /// <summary>
    /// コンパイルエラー情報
    /// </summary>
    public class CompilationError
    {
        /// <summary>エラーメッセージ</summary>
        public string Message { get; set; } = "";

        /// <summary>行番号</summary>
        public int LineNumber { get; set; }

        /// <summary>列番号</summary>
        public int ColumnNumber { get; set; }

        /// <summary>エラーコード</summary>
        public string ErrorCode { get; set; } = "";

        /// <summary>重要度</summary>
        public ErrorSeverity Severity { get; set; }
    }

    /// <summary>
    /// エラー重要度
    /// </summary>
    public enum ErrorSeverity
    {
        Info,
        Warning,
        Error
    }
}