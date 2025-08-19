namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// ExecuteDynamicCodeToolのセキュリティレベル定義
    /// v3.0 静的アセンブリ初期化戦略対応
    /// 
    /// 設計ドキュメント参照: working-notes/2025-08-10_ExecuteDynamicCodeセキュリティ制限機能_design.md
    /// 関連クラス: DynamicCodeSecurityManager, AssemblyReferencePolicy, RoslynCompiler
    /// </summary>
    public enum DynamicCodeSecurityLevel
    {
        /// <summary>
        /// Level 0: 完全無効化
        /// - アセンブリを何も追加しない（コンパイラ提供の最小限参照のみ）
        /// - コンパイルは可能だが実行は拒否される
        /// - 用途: プロダクション環境、セキュリティ最優先
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Level 1: 制限付き実行（推奨）
        /// - 基本.NETアセンブリ（mscorlib, System, netstandard）
        /// - Unity公式APIアセンブリ（UnityEngine, UnityEditor等）
        /// - 危険なAPI（System.IO、System.Net.Http、リフレクション等）はブロック
        /// - 用途: 通常のUnity開発、安全性重視
        /// </summary>
        Restricted = 1,

        /// <summary>
        /// Level 2: フルアクセス
        /// - 全アセンブリが利用可能（制限なし）
        /// - System.Reflection.Emit、System.CodeDomも含む
        /// - 用途: 高度な開発、システム統合、デバッグ、動的コード生成
        /// - 警告: セキュリティリスクあり - 信頼できるコードのみで使用
        /// </summary>
        FullAccess = 2
    }
}