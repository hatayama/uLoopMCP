using System;
using System.Collections.Generic;
using System.Reflection;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// セキュリティレベルに基づくアセンブリ参照ポリシー
    /// 関連クラス: DynamicCodeSecurityLevel, DangerousApiDetector, RoslynCompiler
    /// 
    /// 設計方針:
    /// - Disabled: アセンブリ参照なし（コンパイル不可）
    /// - Restricted/FullAccess: 全アセンブリ参照可能（コンパイル可能）
    /// - セキュリティチェックは実行時にDangerousApiDetectorで行う
    /// </summary>
    public static class AssemblyReferencePolicy
    {
        /// <summary>
        /// セキュリティレベルに応じたアセンブリ名リストを取得
        /// </summary>
        public static IReadOnlyList<string> GetAssemblies(DynamicCodeSecurityLevel level)
        {
            switch (level)
            {
                case DynamicCodeSecurityLevel.Disabled:
                    // Level 0: 何も追加しない（コンパイル不可）
                    return new List<string>();

                case DynamicCodeSecurityLevel.Restricted:
                case DynamicCodeSecurityLevel.FullAccess:
                    // Level 1 & 2: 全アセンブリ参照可能
                    // セキュリティチェックは実行時に行う
                    return GetAllAvailableAssemblies();

                default:
                    VibeLogger.LogWarning(
                        "assembly_policy_unknown_level",
                        $"Unknown security level: {level}",
                        new { level = level.ToString() },
                        correlationId: McpConstants.GenerateCorrelationId(),
                        humanNote: "Unknown security level encountered",
                        aiTodo: "Review security level enum changes"
                    );
                    return new List<string>();
            }
        }

        /// <summary>
        /// 利用可能な全アセンブリを取得
        /// </summary>
        private static List<string> GetAllAvailableAssemblies()
        {
            List<string> assemblies = new();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // 動的アセンブリや場所が空のものは除外
                if (assembly.IsDynamic || string.IsNullOrWhiteSpace(assembly.Location))
                    continue;

                string assemblyName = assembly.GetName().Name;
                assemblies.Add(assemblyName);
            }

            VibeLogger.LogInfo(
                "assembly_policy_all_assemblies",
                "Generated all available assemblies list",
                new { count = assemblies.Count },
                correlationId: McpConstants.GenerateCorrelationId(),
                humanNote: "All assemblies made available for compilation",
                aiTodo: "Monitor assembly usage in dynamic code execution"
            );

            return assemblies;
        }

        /// <summary>
        /// 指定されたアセンブリがセキュリティレベルで許可されているかチェック
        /// （後方互換性のために維持）
        /// </summary>
        public static bool IsAssemblyAllowed(string assemblyName, DynamicCodeSecurityLevel level)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                return false;

            switch (level)
            {
                case DynamicCodeSecurityLevel.Disabled:
                    // Level 0: 何も許可しない
                    return false;

                case DynamicCodeSecurityLevel.Restricted:
                case DynamicCodeSecurityLevel.FullAccess:
                    // Level 1 & 2: 全て許可
                    return true;

                default:
                    return false;
            }
        }
    }
}