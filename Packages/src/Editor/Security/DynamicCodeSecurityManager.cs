using System;
using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// ExecuteDynamicCodeToolのセキュリティ管理
    /// v3.0 静的アセンブリ初期化戦略 - シンプルで予測可能な動作
    /// 関連クラス: DynamicCodeSecurityLevel, AssemblyReferencePolicy, RoslynCompiler
    /// </summary>
    public static class DynamicCodeSecurityManager
    {
        private static DynamicCodeSecurityLevel _currentLevel = DynamicCodeSecurityLevel.Restricted;

        /// <summary>
        /// セキュリティレベル変更イベント
        /// </summary>
        public static event Action<DynamicCodeSecurityLevel> SecurityLevelChanged;

        /// <summary>
        /// 現在のセキュリティレベル
        /// </summary>
        public static DynamicCodeSecurityLevel CurrentLevel
        {
            get => _currentLevel;
            internal set
            {
                if (_currentLevel != value)
                {
                    DynamicCodeSecurityLevel oldLevel = _currentLevel;
                    _currentLevel = value;

                    string correlationId = Guid.NewGuid().ToString("N")[..8];
                    VibeLogger.LogInfo(
                        "security_level_changed",
                        $"Security level changed from {oldLevel} to {value}",
                        new { 
                            oldLevel = oldLevel.ToString(), 
                            newLevel = value.ToString() 
                        },
                        correlationId,
                        "Security level updated",
                        "Monitor security level changes"
                    );

                    // イベント発火
                    SecurityLevelChanged?.Invoke(value);
                }
            }
        }

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
                        correlationId: Guid.NewGuid().ToString("N")[..8],
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
        /// 危険なAPIが含まれているか簡易チェック（Roslynがない環境用フォールバック）
        /// 通常はRoslynベースのSecurityValidatorを使用すること
        /// </summary>
        internal static bool ContainsDangerousApi(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;
            
            // 簡易的な危険API検出（Roslynがない場合のフォールバック）
            string[] dangerousPatterns = new[]
            {
                "System.IO", "File.", "Directory.", "Path.",
                "System.Net", "HttpClient", "WebClient",
                "System.Reflection", "Assembly.Load", "Activator.CreateInstance",
                "Process.", "Registry.", "Microsoft.Win32"
            };
            
            foreach (string pattern in dangerousPatterns)
            {
                if (code.Contains(pattern))
                    return true;
            }
            
            return false;
        }

        /// <summary>
        /// セキュリティレベルを設定から初期化（テスト用に公開）
        /// </summary>
        public static void InitializeFromSettings(DynamicCodeSecurityLevel level)
        {
            _currentLevel = level;
            
            VibeLogger.LogInfo(
                "security_manager_initialized",
                $"Security manager initialized with level: {level}",
                new { level = level.ToString() },
                correlationId: Guid.NewGuid().ToString("N")[..8],
                humanNote: "Security manager ready",
                aiTodo: "Monitor initialization patterns"
            );
        }
    }
}