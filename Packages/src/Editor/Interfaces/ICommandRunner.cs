using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace io.github.hatayama.uLoopMCP.DynamicExecution
{
    /// <summary>
    /// コンパイル済みコードの実行制御インターフェース
    /// 設計ドキュメント: uLoopMCP_DynamicCodeExecution_Design.md
    /// 関連クラス: CommandRunner, ExecutionContext, ExecutionResult
    /// </summary>
    public interface ICommandRunner
    {
        /// <summary>
        /// コンパイル済みコードを実行する
        /// </summary>
        /// <param name="context">実行コンテキスト</param>
        /// <returns>実行結果</returns>
        ExecutionResult Execute(ExecutionContext context);

        /// <summary>
        /// 実行中の処理をキャンセルする
        /// </summary>
        void Cancel();

        /// <summary>
        /// 実行中かどうか
        /// </summary>
        bool IsRunning { get; }
    }

    /// <summary>
    /// コマンド実行コンテキスト
    /// </summary>
    public class ExecutionContext
    {
        /// <summary>コンパイル済みアセンブリ</summary>
        public Assembly CompiledAssembly { get; set; }

        /// <summary>実行時パラメータ</summary>
        public Dictionary<string, object> Parameters { get; set; } = new();

        /// <summary>タイムアウト秒数</summary>
        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>キャンセレーショントークン</summary>
        public CancellationToken CancellationToken { get; set; }
    }

    /// <summary>
    /// 実行結果
    /// </summary>
    public class ExecutionResult
    {
        /// <summary>実行成功フラグ</summary>
        public bool Success { get; set; }

        /// <summary>実行結果文字列</summary>
        public string Result { get; set; } = "";

        /// <summary>戻り値</summary>
        public object ReturnValue { get; set; }

        /// <summary>ログメッセージ</summary>
        public List<string> Logs { get; set; } = new();

        /// <summary>エラーメッセージ（失敗時）</summary>
        public string ErrorMessage { get; set; } = "";

        /// <summary>発生した例外</summary>
        public Exception Exception { get; set; }

        /// <summary>実行時間</summary>
        public TimeSpan ExecutionTime { get; set; }
    }
}