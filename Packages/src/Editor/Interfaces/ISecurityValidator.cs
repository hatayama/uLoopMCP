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
        /// <summary>
        /// C#コードのセキュリティチェックを実行する
        /// </summary>
        /// <param name="code">検証するC#コード</param>
        /// <returns>検証結果</returns>
        ValidationResult ValidateCode(string code);

        /// <summary>
        /// セキュリティポリシーを読み込む
        /// </summary>
        /// <param name="jsonPath">セキュリティポリシーJSONファイルのパス</param>
        void LoadSecurityPolicy(string jsonPath);

        /// <summary>
        /// 指定したメソッドの使用が許可されているかチェックする
        /// </summary>
        /// <param name="methodSignature">メソッドシグネチャ</param>
        /// <returns>許可されている場合true</returns>
        bool IsMethodAllowed(string methodSignature);
    }

    /// <summary>
    /// セキュリティ検証結果のデータモデル
    /// </summary>
    public class ValidationResult
    {
        /// <summary>検証が有効（問題なし）かどうか</summary>
        public bool IsValid { get; set; }

        /// <summary>セキュリティ違反の詳細</summary>
        public List<SecurityViolation> Violations { get; set; } = new();

        /// <summary>リスクレベル</summary>
        public SecurityLevel RiskLevel { get; set; }
    }

    /// <summary>
    /// セキュリティ違反情報
    /// </summary>
    public class SecurityViolation
    {
        /// <summary>違反の種類</summary>
        public SecurityViolationType Type { get; set; }

        /// <summary>違反の詳細説明</summary>
        public string Description { get; set; } = "";

        /// <summary>違反が発生した行番号</summary>
        public int LineNumber { get; set; }

        /// <summary>違反を含むコードスニペット</summary>
        public string CodeSnippet { get; set; } = "";
    }

    /// <summary>
    /// セキュリティ違反の種類
    /// </summary>
    public enum SecurityViolationType
    {
        /// <summary>禁止された名前空間の使用</summary>
        ForbiddenNamespace,

        /// <summary>危険なメソッドの呼び出し</summary>
        DangerousMethodCall,

        /// <summary>禁止された型の使用</summary>
        ForbiddenType,

        /// <summary>リフレクションの不正使用</summary>
        IllegalReflection,

        /// <summary>ファイルシステムアクセス</summary>
        FileSystemAccess,

        /// <summary>ネットワークアクセス</summary>
        NetworkAccess
    }

    /// <summary>
    /// セキュリティリスクレベル
    /// </summary>
    public enum SecurityLevel
    {
        /// <summary>安全</summary>
        Safe,

        /// <summary>低リスク</summary>
        Low,

        /// <summary>中リスク</summary>
        Medium,

        /// <summary>高リスク</summary>
        High,

        /// <summary>危険</summary>
        Critical
    }
}