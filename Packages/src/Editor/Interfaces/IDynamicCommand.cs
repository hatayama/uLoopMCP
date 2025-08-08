using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace io.github.hatayama.uLoopMCP.DynamicExecution
{
    /// <summary>
    /// 動的実行コマンドのインターフェース
    /// 設計ドキュメント: uLoopMCP_DynamicCodeExecution_Design.md
    /// 関連クラス: CommandContext
    /// </summary>
    public interface IDynamicCommand
    {
        /// <summary>
        /// コマンドを実行する
        /// </summary>
        /// <param name="context">実行コンテキスト</param>
        void Execute(CommandContext context);

        /// <summary>
        /// コマンドの説明を取得する
        /// </summary>
        /// <returns>説明文</returns>
        string GetDescription();
    }

    /// <summary>
    /// コマンド実行時のコンテキスト情報
    /// </summary>
    public class CommandContext
    {
        /// <summary>実行時パラメータ</summary>
        public Dictionary<string, object> Parameters { get; set; } = new();

        /// <summary>ログ出力アクション</summary>
        public Action<string> Log { get; set; }

        /// <summary>警告ログ出力アクション</summary>
        public Action<string> LogWarning { get; set; }

        /// <summary>エラーログ出力アクション</summary>
        public Action<string> LogError { get; set; }

        /// <summary>キャンセレーショントークン</summary>
        public CancellationToken CancellationToken { get; set; }

        // Unity特化の情報
        /// <summary>現在のシーン</summary>
        public Scene CurrentScene { get; set; }

        /// <summary>選択中のオブジェクト</summary>
        public List<GameObject> SelectedObjects { get; set; } = new();
    }
}