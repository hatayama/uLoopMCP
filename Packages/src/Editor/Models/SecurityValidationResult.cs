using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// セキュリティ検証結果
    /// 
    /// 関連クラス: SecurityValidator
    /// </summary>
    public class SecurityValidationResult
    {
        /// <summary>検証有効</summary>
        public bool IsValid { get; set; }

        /// <summary>違反リスト</summary>
        public List<SecurityViolation> Violations { get; set; } = new();

        /// <summary>リスクレベル</summary>
        public SecurityLevel RiskLevel { get; set; }
    }
}