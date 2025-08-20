using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// セキュリティレベルに基づくアセンブリ参照ポリシー
    /// v4.1 実行時チェック移行 - コンパイル時制限を廃止
    /// 
    /// 設計ドキュメント参照: working-notes/2025-08-10_ExecuteDynamicCodeセキュリティ制限機能_design.md
    /// 関連クラス: DynamicCodeSecurityLevel, DangerousApiDetector, RoslynCompiler
    /// </summary>
    public static class AssemblyReferencePolicy
    {
        // Level 1 (Restricted) で許可される安全なアセンブリプレフィックス
        private static readonly HashSet<string> RestrictedAllowedPrefixes = new()
        {
            // 基本.NET
            "mscorlib",
            "System",
            "System.Core",
            "System.Collections",
            "System.Linq",
            "System.Text",
            "System.Runtime",
            "netstandard",
            
            // Unity公式API
            "UnityEngine",
            "UnityEditor",
            "Unity.Mathematics",
            "Unity.Collections",
            "Unity.Burst",
            "Unity.TextMeshPro"
        };

        // Level 1 (Restricted) で禁止される危険なアセンブリプレフィックス
        // v4.1: コンパイル時の制限を廃止し、実行時チェックに移行
        // これにより「コンパイルは成功、実行時にブロック」が実現できる
        private static readonly HashSet<string> RestrictedForbiddenPrefixes = new()
        {
            // 現在は空（全アセンブリを参照可能にする）
            // セキュリティチェックはDangerousApiDetectorで実行時に行う
        };

        // Level 2 (FullAccess) では全アセンブリが利用可能
        private static readonly HashSet<string> AlwaysExcludedPrefixes = new()
        {
            // FullAccessレベルでは制限なし - 全てのアセンブリが利用可能
        };

        /// <summary>
        /// セキュリティレベルに応じたアセンブリ名リストを取得
        /// </summary>
        public static IReadOnlyList<string> GetAssemblies(DynamicCodeSecurityLevel level)
        {
            switch (level)
            {
                case DynamicCodeSecurityLevel.Disabled:
                    // Level 0: 何も追加しない
                    return new List<string>();

                case DynamicCodeSecurityLevel.Restricted:
                    // Level 1: 安全なアセンブリのみ
                    return GetRestrictedAssemblies();

                case DynamicCodeSecurityLevel.FullAccess:
                    // Level 2: 全アセンブリ（危険なものを除く）
                    return GetFullAccessAssemblies();

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
        /// 指定されたアセンブリがセキュリティレベルで許可されているかチェック
        /// </summary>
        public static bool IsAssemblyAllowed(string assemblyName, DynamicCodeSecurityLevel level)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                return false;

            // 常に除外されるアセンブリをチェック
            if (IsAlwaysExcluded(assemblyName))
                return false;

            switch (level)
            {
                case DynamicCodeSecurityLevel.Disabled:
                    // Level 0: 何も許可しない
                    return false;

                case DynamicCodeSecurityLevel.Restricted:
                    // Level 1: 安全なアセンブリのみ
                    return IsRestrictedAssemblyAllowed(assemblyName);

                case DynamicCodeSecurityLevel.FullAccess:
                    // Level 2: 危険なものを除いて全て許可
                    return !IsAlwaysExcluded(assemblyName);

                default:
                    return false;
            }
        }

        private static List<string> GetRestrictedAssemblies()
        {
            List<string> assemblies = new();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic || string.IsNullOrWhiteSpace(assembly.Location))
                    continue;

                string assemblyName = assembly.GetName().Name;
                
                // ユーザー定義アセンブリは動的判定で許可
                if (AssemblyClassifier.IsUserDefinedAssembly(assembly))
                {
                    assemblies.Add(assemblyName);
                    VibeLogger.LogInfo(
                        "assembly_allowed_user_defined",
                        $"User-defined assembly allowed in Restricted mode: {assemblyName}",
                        new { assemblyName, location = assembly.Location },
                        correlationId: McpConstants.GenerateCorrelationId(),
                        humanNote: "User assembly permitted for compilation",
                        aiTodo: "Track user assembly usage"
                    );
                }
                else if (IsRestrictedAssemblyAllowed(assemblyName))
                {
                    assemblies.Add(assemblyName);
                }
            }

            VibeLogger.LogInfo(
                "assembly_policy_restricted_list",
                "Generated restricted assembly list",
                new { count = assemblies.Count },
                correlationId: McpConstants.GenerateCorrelationId(),
                humanNote: "Restricted assembly list created",
                aiTodo: "Monitor assembly usage patterns"
            );

            return assemblies;
        }

        private static List<string> GetFullAccessAssemblies()
        {
            List<string> assemblies = new();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic || string.IsNullOrWhiteSpace(assembly.Location))
                    continue;

                string assemblyName = assembly.GetName().Name;
                if (!IsAlwaysExcluded(assemblyName))
                {
                    assemblies.Add(assemblyName);
                }
            }

            VibeLogger.LogInfo(
                "assembly_policy_full_access_list",
                "Generated full access assembly list",
                new { count = assemblies.Count },
                correlationId: McpConstants.GenerateCorrelationId(),
                humanNote: "Full access assembly list created",
                aiTodo: "Review security implications"
            );

            return assemblies;
        }

        private static bool IsRestrictedAssemblyAllowed(string assemblyName)
        {
            // ユーザー定義アセンブリは許可（名前ベースの簡易判定）
            if (AssemblyClassifier.IsUserDefinedAssemblyByName(assemblyName))
            {
                return true;
            }
            
            // 禁止リストチェック（優先）
            foreach (string forbidden in RestrictedForbiddenPrefixes)
            {
                if (assemblyName.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // 許可リストチェック
            foreach (string allowed in RestrictedAllowedPrefixes)
            {
                if (assemblyName.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Unity.で始まるアセンブリは基本的に許可
            if (assemblyName.StartsWith("Unity.", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // それ以外は拒否
            return false;
        }

        private static bool IsAlwaysExcluded(string assemblyName)
        {
            foreach (string excluded in AlwaysExcludedPrefixes)
            {
                if (assemblyName.StartsWith(excluded, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}