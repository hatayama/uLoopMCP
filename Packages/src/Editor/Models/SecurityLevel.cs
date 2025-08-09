namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// セキュリティレベル
    /// 
    /// 関連クラス: SecurityValidator
    /// </summary>
    public enum SecurityLevel
    {
        /// <summary>安全</summary>
        Safe,

        /// <summary>低</summary>
        Low,

        /// <summary>中</summary>
        Medium,

        /// <summary>高</summary>
        High,

        /// <summary>警告</summary>
        Warning,

        /// <summary>危険</summary>
        Dangerous,

        /// <summary>致命的</summary>
        Critical
    }
}