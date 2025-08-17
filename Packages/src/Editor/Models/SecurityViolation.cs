namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// セキュリティ違反
    /// 
    /// 関連クラス: SecurityValidator, SecuritySyntaxWalker
    /// </summary>
    public class SecurityViolation
    {
        /// <summary>違反タイプ</summary>
        public SecurityViolationType Type { get; set; }

        /// <summary>説明</summary>
        public string Description { get; set; }

        /// <summary>行番号</summary>
        public int LineNumber { get; set; }

        /// <summary>コードスニペット</summary>
        public string CodeSnippet { get; set; }
        
        // 拡張プロパティ（Roslyn検証用）
        /// <summary>違反タイプ（Roslyn）</summary>
        public ViolationType ViolationType { get; set; }
        
        /// <summary>メッセージ</summary>
        public string Message { get; set; }
        
        /// <summary>API名</summary>
        public string ApiName { get; set; }
        
#if ULOOPMCP_HAS_ROSLYN
        /// <summary>ロケーション</summary>
        public Microsoft.CodeAnalysis.Location Location { get; set; }
#else
        /// <summary>ロケーション（Roslyn無効時はobject）</summary>
        public object Location { get; set; }
#endif
    }

    /// <summary>
    /// セキュリティ違反タイプ
    /// </summary>
    public enum SecurityViolationType
    {
        /// <summary>禁止名前空間</summary>
        ForbiddenNamespace,

        /// <summary>危険なコード</summary>
        DangerousCode,

        /// <summary>危険なメソッド呼び出し</summary>
        DangerousMethodCall,

        /// <summary>安全でないコード</summary>
        UnsafeCode,

        /// <summary>プロセス実行</summary>
        ProcessExecution,

        /// <summary>ファイルアクセス</summary>
        FileAccess,

        /// <summary>ファイルシステムアクセス</summary>
        FileSystemAccess,

        /// <summary>ネットワークアクセス</summary>
        NetworkAccess,

        /// <summary>リフレクション使用</summary>
        ReflectionUsage
    }
}