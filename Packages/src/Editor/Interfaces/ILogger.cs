using System;
using System.Collections.Generic;
using UnityEngine;

namespace uLoopMCP.DynamicExecution
{
    /// <summary>
    /// ログ出力機能の抽象化インターフェース
    /// 設計ドキュメント: uLoopMCP_DynamicCodeExecution_Design.md
    /// 関連クラス: ConsoleLogger, TestLogger
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// 情報ログを出力する
        /// </summary>
        /// <param name="message">メッセージ</param>
        void LogInfo(string message);

        /// <summary>
        /// 警告ログを出力する
        /// </summary>
        /// <param name="message">メッセージ</param>
        void LogWarning(string message);

        /// <summary>
        /// エラーログを出力する
        /// </summary>
        /// <param name="message">メッセージ</param>
        void LogError(string message);

        /// <summary>
        /// 例外ログを出力する
        /// </summary>
        /// <param name="exception">例外</param>
        /// <param name="message">追加メッセージ</param>
        void LogException(Exception exception, string message = "");
    }

    /// <summary>
    /// Unityコンソール用ログ実装
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        public void LogInfo(string message)
        {
            Debug.Log($"[DynamicExecution] {message}");
        }

        public void LogWarning(string message)
        {
            Debug.LogWarning($"[DynamicExecution] {message}");
        }

        public void LogError(string message)
        {
            Debug.LogError($"[DynamicExecution] {message}");
        }

        public void LogException(Exception exception, string message = "")
        {
            var fullMessage = string.IsNullOrEmpty(message) 
                ? $"[DynamicExecution] Exception: {exception.Message}"
                : $"[DynamicExecution] {message}: {exception.Message}";
            Debug.LogError(fullMessage);
            Debug.LogException(exception);
        }
    }

    /// <summary>
    /// テスト用ログ実装
    /// </summary>
    public class TestLogger : ILogger
    {
        public List<string> InfoLogs { get; } = new();
        public List<string> WarningLogs { get; } = new();
        public List<string> ErrorLogs { get; } = new();
        public List<(Exception exception, string message)> ExceptionLogs { get; } = new();

        public void LogInfo(string message)
        {
            InfoLogs.Add(message);
        }

        public void LogWarning(string message)
        {
            WarningLogs.Add(message);
        }

        public void LogError(string message)
        {
            ErrorLogs.Add(message);
        }

        public void LogException(Exception exception, string message = "")
        {
            ExceptionLogs.Add((exception, message));
        }

        public void Clear()
        {
            InfoLogs.Clear();
            WarningLogs.Clear();
            ErrorLogs.Clear();
            ExceptionLogs.Clear();
        }
    }
}