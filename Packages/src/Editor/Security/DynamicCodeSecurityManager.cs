using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// ExecuteDynamicCodeToolのセキュリティ管理ユーティリティ
    /// 関連クラス: DynamicCodeSecurityLevel, AssemblyReferencePolicy, RoslynCompiler
    /// </summary>
    public static class DynamicCodeSecurityManager
    {
        /// <summary>
        /// 指定されたセキュリティレベルでコード実行が可能かチェック
        /// </summary>
        public static bool CanExecute(DynamicCodeSecurityLevel level)
        {
            switch (level)
            {
                case DynamicCodeSecurityLevel.Disabled:
                    // Level 0: 実行完全禁止
                    VibeLogger.LogWarning(
                        "security_execution_blocked",
                        "Execution blocked at Disabled security level",
                        new { level = level.ToString() },
                        correlationId: McpConstants.GenerateCorrelationId(),
                        humanNote: "Code execution prevented by security policy",
                        aiTodo: "Track execution attempts at disabled level"
                    );
                    return false;

                case DynamicCodeSecurityLevel.Restricted:
                case DynamicCodeSecurityLevel.FullAccess:
                    // Level 1, 2: 実行許可
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// セキュリティレベルに応じた許可されたアセンブリリストを取得
        /// </summary>
        public static IReadOnlyList<string> GetAllowedAssemblies(DynamicCodeSecurityLevel level)
        {
            return AssemblyReferencePolicy.GetAssemblies(level);
        }
    }
}