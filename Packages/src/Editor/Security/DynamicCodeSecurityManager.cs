using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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

        // 危険なAPIパターン定義（Level 1で検出）
        private static readonly List<Regex> DangerousApiPatterns = new()
        {
            // ファイルシステム操作
            new Regex(@"\bSystem\.IO\.", RegexOptions.Compiled),
            new Regex(@"\bFile\.", RegexOptions.Compiled),
            new Regex(@"\bDirectory\.", RegexOptions.Compiled),
            new Regex(@"\bPath\.", RegexOptions.Compiled),
            new Regex(@"\bFileInfo\b", RegexOptions.Compiled),
            new Regex(@"\bDirectoryInfo\b", RegexOptions.Compiled),
            
            // ネットワーク操作
            new Regex(@"\bSystem\.Net\.", RegexOptions.Compiled),
            new Regex(@"\bHttpClient\b", RegexOptions.Compiled),
            new Regex(@"\bWebClient\b", RegexOptions.Compiled),
            new Regex(@"\bWebRequest\b", RegexOptions.Compiled),
            new Regex(@"\bTcpClient\b", RegexOptions.Compiled),
            new Regex(@"\bSocket\b", RegexOptions.Compiled),
            
            // リフレクション操作
            new Regex(@"\bSystem\.Reflection\.", RegexOptions.Compiled),
            new Regex(@"\bType\.GetType\b", RegexOptions.Compiled),
            new Regex(@"\bAssembly\.Load", RegexOptions.Compiled),
            new Regex(@"\bActivator\.CreateInstance", RegexOptions.Compiled),
            new Regex(@"\bMethodInfo\b", RegexOptions.Compiled),
            new Regex(@"\bFieldInfo\b", RegexOptions.Compiled),
            
            // プロセス操作
            new Regex(@"\bProcess\.", RegexOptions.Compiled),
            new Regex(@"\bProcessStartInfo\b", RegexOptions.Compiled),
            new Regex(@"\bEnvironment\.", RegexOptions.Compiled),
            
            // レジストリ操作
            new Regex(@"\bRegistry\.", RegexOptions.Compiled),
            new Regex(@"\bMicrosoft\.Win32\.", RegexOptions.Compiled)
        };

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
        /// コードに危険なAPIが含まれているかチェック（Level 1用）
        /// </summary>
        public static bool ContainsDangerousApi(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;

            foreach (Regex pattern in DangerousApiPatterns)
            {
                if (pattern.IsMatch(code))
                {
                    VibeLogger.LogWarning(
                        "dangerous_api_detected",
                        $"Dangerous API pattern detected: {pattern}",
                        new { 
                            pattern = pattern.ToString(),
                            codeLength = code.Length 
                        },
                        correlationId: Guid.NewGuid().ToString("N")[..8],
                        humanNote: "Potentially dangerous API usage detected",
                        aiTodo: "Review API usage patterns for security"
                    );
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 現在のセキュリティレベルでコードが実行可能かチェック
        /// </summary>
        public static bool IsCodeAllowedForCurrentLevel(string code)
        {
            // Level 0: 実行完全禁止
            if (_currentLevel == DynamicCodeSecurityLevel.Disabled)
            {
                return false;
            }

            // Level 1: 危険APIチェック
            if (_currentLevel == DynamicCodeSecurityLevel.Restricted)
            {
                return !ContainsDangerousApi(code);
            }

            // Level 2: 全て許可
            return true;
        }

        /// <summary>
        /// セキュリティレベルを設定から初期化
        /// </summary>
        internal static void InitializeFromSettings(DynamicCodeSecurityLevel level)
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