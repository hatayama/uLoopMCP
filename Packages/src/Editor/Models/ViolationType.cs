namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// 違反タイプの列挙（Roslyn詳細検証用）
    /// 設計ドキュメント参照: working-notes/2025-08-16_Restrictedモードユーザークラス実行機能_design.md
    /// 関連クラス: SecurityViolation, SecuritySyntaxWalker
    /// </summary>
    public enum ViolationType
    {
        DangerousApiCall,
        DangerousUsing,
        DangerousInheritance,
        UnauthorizedReflection,
        FileSystemAccess,
        NetworkAccess,
        ProcessManipulation,
        ThreadManipulation
    }
}