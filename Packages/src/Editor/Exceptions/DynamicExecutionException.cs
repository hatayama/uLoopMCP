using System;

namespace io.github.hatayama.uLoopMCP.DynamicExecution
{
    /// <summary>
    /// 動的コード実行機能の基底例外クラス
    /// 設計ドキュメント: uLoopMCP_DynamicCodeExecution_Design.md
    /// 関連クラス: CompilationFailedException, SecurityViolationException
    /// </summary>
    public class DynamicExecutionException : Exception
    {
        public DynamicExecutionException() : base()
        {
        }

        public DynamicExecutionException(string message) : base(message)
        {
        }

        public DynamicExecutionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// コンパイル失敗例外
    /// </summary>
    public class CompilationFailedException : DynamicExecutionException
    {
        /// <summary>コンパイルエラー詳細</summary>
        public CompilationError[] Errors { get; }

        public CompilationFailedException() : base()
        {
            Errors = new CompilationError[0];
        }

        public CompilationFailedException(string message) : base(message)
        {
            Errors = new CompilationError[0];
        }

        public CompilationFailedException(string message, CompilationError[] errors) : base(message)
        {
            Errors = errors ?? new CompilationError[0];
        }

        public CompilationFailedException(string message, Exception innerException) : base(message, innerException)
        {
            Errors = new CompilationError[0];
        }
    }

    /// <summary>
    /// セキュリティ違反例外
    /// </summary>
    public class SecurityViolationException : DynamicExecutionException
    {
        /// <summary>違反の種類</summary>
        public SecurityViolationType ViolationType { get; }

        /// <summary>違反詳細</summary>
        public SecurityViolation[] Violations { get; }

        public SecurityViolationException() : base()
        {
            ViolationType = SecurityViolationType.ForbiddenNamespace;
            Violations = new SecurityViolation[0];
        }

        public SecurityViolationException(string message, SecurityViolationType violationType) : base(message)
        {
            ViolationType = violationType;
            Violations = new SecurityViolation[0];
        }

        public SecurityViolationException(string message, SecurityViolation[] violations) : base(message)
        {
            ViolationType = violations?.Length > 0 ? violations[0].Type : SecurityViolationType.ForbiddenNamespace;
            Violations = violations ?? new SecurityViolation[0];
        }

        public SecurityViolationException(string message, Exception innerException) : base(message, innerException)
        {
            ViolationType = SecurityViolationType.ForbiddenNamespace;
            Violations = new SecurityViolation[0];
        }
    }

    /// <summary>
    /// 実行タイムアウト例外
    /// </summary>
    public class ExecutionTimeoutException : DynamicExecutionException
    {
        /// <summary>設定されたタイムアウト時間（秒）</summary>
        public int TimeoutSeconds { get; }

        public ExecutionTimeoutException() : base()
        {
            TimeoutSeconds = 0;
        }

        public ExecutionTimeoutException(string message, int timeoutSeconds) : base(message)
        {
            TimeoutSeconds = timeoutSeconds;
        }

        public ExecutionTimeoutException(string message, Exception innerException) : base(message, innerException)
        {
            TimeoutSeconds = 0;
        }
    }

    /// <summary>
    /// 危険コード実行例外
    /// </summary>
    public class UnsafeCodeException : DynamicExecutionException
    {
        /// <summary>検出された危険な操作</summary>
        public string UnsafeOperation { get; }

        public UnsafeCodeException() : base()
        {
            UnsafeOperation = "";
        }

        public UnsafeCodeException(string message, string unsafeOperation) : base(message)
        {
            UnsafeOperation = unsafeOperation ?? "";
        }

        public UnsafeCodeException(string message, Exception innerException) : base(message, innerException)
        {
            UnsafeOperation = "";
        }
    }
}