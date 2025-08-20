using System;
using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// ExecuteDynamicCodeToolのセキュリティ管理ユーティリティ
    /// v4.0 ステートレス設計 - static変数を排除し、明示的な依存性注入
    /// 関連クラス: DynamicCodeSecurityLevel, AssemblyReferencePolicy, RoslynCompiler
    /// </summary>
    public static class DynamicCodeSecurityManager
    {
        // static変数を削除し、ステートレスなユーティリティクラスとして再設計
        // セキュリティレベルは各コンポーネントのコンストラクタで明示的に受け取る

        // 正規表現ベースの検出は廃止（Roslynベースに移行済み）
        // DangerousApiDetectorとSecurityValidatorを使用

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

        /// <summary>
        /// 現在のセキュリティレベルを取得（廃止予定 - v4.0）
        /// </summary>
        [Obsolete("Security level should be passed explicitly to each component")]
        public static DynamicCodeSecurityLevel CurrentLevel
        {
            get
            {
                // v4.0: static変数を削除したため、デフォルト値を返す
                // 実際のセキュリティレベルは各コンポーネントが保持
                VibeLogger.LogWarning(
                    "security_current_level_deprecated",
                    "CurrentLevel property accessed (deprecated)",
                    null,
                    correlationId: McpConstants.GenerateCorrelationId(),
                    humanNote: "Deprecated property accessed - returning default",
                    aiTodo: "Migrate to explicit level passing"
                );
                return DynamicCodeSecurityLevel.Disabled; // 安全なデフォルト値
            }
        }
        
        /// <summary>
        /// セキュリティレベルを設定から初期化（廃止予定 - v4.0）
        /// 後方互換性のため残してあるが、実際の状態変更は行わない
        /// </summary>
        [Obsolete("Use explicit security level passing to constructors instead")]
        public static void InitializeFromSettings(DynamicCodeSecurityLevel level)
        {
            // v4.0: static変数を削除したため、この メソッドは何もしない
            // 各コンポーネントのコンストラクタで明示的にレベルを渡すように変更
            
            VibeLogger.LogInfo(
                "security_manager_initialized_deprecated",
                $"InitializeFromSettings called (deprecated): {level}",
                new { level = level.ToString() },
                correlationId: McpConstants.GenerateCorrelationId(),
                humanNote: "Deprecated method called - no actual state change",
                aiTodo: "Migrate callers to use constructor injection"
            );
        }
    }
}