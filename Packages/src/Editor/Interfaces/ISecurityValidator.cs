using System.Collections.Generic;

namespace uLoopMCP.DynamicExecution
{
    /// <summary>
    /// コードセキュリティ検証機能のインターフェース
    /// 設計ドキュメント: uLoopMCP_DynamicCodeExecution_Design.md
    /// 関連クラス: SecurityValidator, SecurityPolicy, ValidationResult
    /// </summary>
    public interface ISecurityValidator
    {
        /// <summary>コードセキュリティ検証</summary>
        ValidationResult ValidateCode(string code);
        
        /// <summary>セキュリティポリシー読み込み</summary>
        void LoadSecurityPolicy(string jsonPath);
        
        /// <summary>メソッド許可チェック</summary>
        bool IsMethodAllowed(string methodSignature);
    }

    /// <summary>検証結果</summary>
    public class ValidationResult
    {
        /// <summary>検証成功フラグ</summary>
        public bool IsValid { get; set; }
        
        /// <summary>セキュリティ違反リスト</summary>
        public List<SecurityViolation> Violations { get; set; } = new();
        
        /// <summary>リスクレベル</summary>
        public SecurityLevel RiskLevel { get; set; }
    }

    /// <summary>セキュリティ違反</summary>
    public class SecurityViolation
    {
        /// <summary>違反タイプ</summary>
        public SecurityViolationType Type { get; set; }
        
        /// <summary>違反説明</summary>
        public string Description { get; set; } = "";
        
        /// <summary>行番号</summary>
        public int LineNumber { get; set; }
        
        /// <summary>コードスニペット</summary>
        public string CodeSnippet { get; set; } = "";
    }

    /// <summary>セキュリティ違反タイプ</summary>
    public enum SecurityViolationType
    {
        /// <summary>危険なメソッド呼び出し</summary>
        DangerousMethodCall,
        
        /// <summary>ファイルシステムアクセス</summary>
        FileSystemAccess,
        
        /// <summary>ネットワークアクセス</summary>
        NetworkAccess,
        
        /// <summary>リフレクション使用</summary>
        ReflectionUsage,
        
        /// <summary>unsafe コード</summary>
        UnsafeCode,
        
        /// <summary>外部プロセス実行</summary>
        ProcessExecution,
        
        /// <summary>禁止名前空間</summary>
        ForbiddenNamespace
    }

    /// <summary>セキュリティレベル</summary>
    public enum SecurityLevel
    {
        /// <summary>安全</summary>
        Safe = 0,
        
        /// <summary>低リスク</summary>
        Low = 1,
        
        /// <summary>中リスク</summary>
        Medium = 2,
        
        /// <summary>高リスク</summary>
        High = 3,
        
        /// <summary>危険</summary>
        Critical = 4
    }
}