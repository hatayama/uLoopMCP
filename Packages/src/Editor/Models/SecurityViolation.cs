namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// セキュリティ違反
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
        /// <summary>禁止名前空間のusing宣言</summary>
        ForbiddenNamespace,

        /// <summary>危険なAPI呼び出し（メソッド、プロパティ等）</summary>
        DangerousApiCall,

        /// <summary>危険なクラスの継承</summary>
        DangerousInheritance,

        /// <summary>危険な型のインスタンス生成</summary>
        DangerousTypeCreation,

        /// <summary>リフレクション使用</summary>
        UnauthorizedReflection
    }
}