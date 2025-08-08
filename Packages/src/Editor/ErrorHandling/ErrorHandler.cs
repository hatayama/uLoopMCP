using System;
using System.Collections.Generic;
using System.Text;

namespace uLoopMCP.DynamicExecution
{
    /// <summary>
    /// エラーハンドリング統合機能
    /// 設計ドキュメント: uLoopMCP_DynamicCodeExecution_Design.md
    /// 関連クラス: DynamicCodeExecutor
    /// </summary>
    public static class ErrorHandler
    {
        /// <summary>
        /// 例外を詳細なエラー情報に変換
        /// </summary>
        /// <param name="exception">変換する例外</param>
        /// <param name="context">エラーコンテキスト</param>
        /// <returns>詳細なエラー情報</returns>
        public static DetailedErrorInfo ConvertToDetailedError(Exception exception, string context = "")
        {
            var errorInfo = new DetailedErrorInfo
            {
                ErrorType = exception.GetType().Name,
                Message = exception.Message,
                Context = context,
                Timestamp = DateTime.Now,
                StackTrace = exception.StackTrace ?? ""
            };

            // 特定の例外タイプに応じた追加処理
            switch (exception)
            {
                case CompilationFailedException compEx:
                    errorInfo.Category = ErrorCategory.Compilation;
                    errorInfo.Details.Add("Error Count", compEx.Errors?.Length.ToString() ?? "0");
                    break;

                case SecurityViolationException secEx:
                    errorInfo.Category = ErrorCategory.Security;
                    errorInfo.Details.Add("Violation Type", secEx.ViolationType.ToString());
                    errorInfo.Details.Add("Violation Count", secEx.Violations?.Length.ToString() ?? "0");
                    break;

                case ExecutionTimeoutException timeoutEx:
                    errorInfo.Category = ErrorCategory.Timeout;
                    errorInfo.Details.Add("Timeout Seconds", timeoutEx.TimeoutSeconds.ToString());
                    break;

                case UnsafeCodeException unsafeEx:
                    errorInfo.Category = ErrorCategory.UnsafeCode;
                    errorInfo.Details.Add("Unsafe Operation", unsafeEx.UnsafeOperation);
                    break;

                default:
                    errorInfo.Category = ErrorCategory.Runtime;
                    break;
            }

            // 内部例外の処理
            if (exception.InnerException != null)
            {
                errorInfo.InnerError = ConvertToDetailedError(exception.InnerException, "Inner Exception");
            }

            return errorInfo;
        }

        /// <summary>
        /// ユーザーフレンドリーなエラーメッセージを生成
        /// </summary>
        /// <param name="exception">例外</param>
        /// <param name="includeDetails">詳細情報を含めるか</param>
        /// <returns>ユーザーフレンドリーなメッセージ</returns>
        public static string CreateUserFriendlyMessage(Exception exception, bool includeDetails = false)
        {
            var sb = new StringBuilder();

            switch (exception)
            {
                case CompilationFailedException compEx:
                    sb.AppendLine("コンパイルエラーが発生しました。");
                    sb.AppendLine("以下のエラーを修正してください:");
                    if (compEx.Errors != null)
                    {
                        foreach (var error in compEx.Errors)
                        {
                            sb.AppendLine($"- 行 {error.LineNumber}: {error.Message}");
                        }
                    }
                    break;

                case SecurityViolationException secEx:
                    sb.AppendLine("セキュリティ制限に違反するコードが検出されました。");
                    sb.AppendLine($"違反タイプ: {secEx.ViolationType}");
                    if (includeDetails && secEx.Violations != null)
                    {
                        foreach (var violation in secEx.Violations)
                        {
                            sb.AppendLine($"- 行 {violation.LineNumber}: {violation.Description}");
                        }
                    }
                    break;

                case ExecutionTimeoutException timeoutEx:
                    sb.AppendLine($"実行がタイムアウトしました（{timeoutEx.TimeoutSeconds}秒）。");
                    sb.AppendLine("コードの実行時間を短縮するか、タイムアウト時間を延長してください。");
                    break;

                case UnsafeCodeException unsafeEx:
                    sb.AppendLine("危険なコードの実行が検出されました。");
                    sb.AppendLine($"検出された操作: {unsafeEx.UnsafeOperation}");
                    break;

                default:
                    sb.AppendLine("実行中にエラーが発生しました。");
                    sb.AppendLine($"エラー: {exception.Message}");
                    break;
            }

            if (includeDetails && !string.IsNullOrEmpty(exception.StackTrace))
            {
                sb.AppendLine();
                sb.AppendLine("詳細情報:");
                sb.AppendLine(exception.StackTrace);
            }

            return sb.ToString();
        }

        /// <summary>
        /// スタックトレースをクリーンアップ（無関係な部分を除去）
        /// </summary>
        /// <param name="stackTrace">元のスタックトレース</param>
        /// <returns>クリーンアップされたスタックトレース</returns>
        public static string CleanupStackTrace(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
                return "";

            var lines = stackTrace.Split('\n');
            var cleanLines = new List<string>();

            foreach (var line in lines)
            {
                // システム内部のスタックフレームを除外
                if (line.Contains("System.") && 
                    (line.Contains("Reflection") || line.Contains("Runtime")))
                    continue;

                if (line.Contains("Microsoft.CodeAnalysis"))
                    continue;

                cleanLines.Add(line.Trim());
            }

            return string.Join("\n", cleanLines);
        }
    }

    /// <summary>
    /// 詳細エラー情報
    /// </summary>
    public class DetailedErrorInfo
    {
        /// <summary>エラータイプ</summary>
        public string ErrorType { get; set; } = "";

        /// <summary>エラーメッセージ</summary>
        public string Message { get; set; } = "";

        /// <summary>エラーカテゴリ</summary>
        public ErrorCategory Category { get; set; }

        /// <summary>コンテキスト</summary>
        public string Context { get; set; } = "";

        /// <summary>発生時刻</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>スタックトレース</summary>
        public string StackTrace { get; set; } = "";

        /// <summary>追加詳細情報</summary>
        public Dictionary<string, string> Details { get; set; } = new();

        /// <summary>内部エラー</summary>
        public DetailedErrorInfo InnerError { get; set; }
    }

    /// <summary>
    /// エラーカテゴリ
    /// </summary>
    public enum ErrorCategory
    {
        /// <summary>コンパイルエラー</summary>
        Compilation,

        /// <summary>セキュリティエラー</summary>
        Security,

        /// <summary>実行時エラー</summary>
        Runtime,

        /// <summary>タイムアウトエラー</summary>
        Timeout,

        /// <summary>危険コードエラー</summary>
        UnsafeCode,

        /// <summary>システムエラー</summary>
        System,

        /// <summary>不明</summary>
        Unknown
    }
}