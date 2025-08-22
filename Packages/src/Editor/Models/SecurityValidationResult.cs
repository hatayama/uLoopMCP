using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        
        /// <summary>コンパイルエラー（拡張）</summary>
        public List<string> CompilationErrors { get; set; } = new();
        
        /// <summary>
        /// エラーサマリーを取得
        /// </summary>
        public string GetErrorSummary()
        {
            if (IsValid) return "No security violations detected.";
            
            StringBuilder sb = new();
            sb.AppendLine($"Security validation failed with {Violations.Count} violation(s):");
            
            foreach (SecurityViolation violation in Violations)
            {
                sb.AppendLine($"  - [{violation.Type}] {violation.Message}");
                if (!string.IsNullOrEmpty(violation.ApiName))
                {
                    sb.AppendLine($"    API: {violation.ApiName}");
                }
            }
            
            if (CompilationErrors?.Any() == true)
            {
                sb.AppendLine($"Compilation errors: {CompilationErrors.Count}");
                foreach (string error in CompilationErrors)
                {
                    sb.AppendLine($"  - {error}");
                }
            }
            
            return sb.ToString();
        }
    }
}