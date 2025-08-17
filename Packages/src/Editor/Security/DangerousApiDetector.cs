#if ULOOPMCP_HAS_ROSLYN
using Microsoft.CodeAnalysis;
#endif
using System;
using System.Collections.Generic;
using System.Linq;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// 危険なAPIパターンを管理・検出するエンジン
    /// 設計ドキュメント参照: working-notes/2025-08-16_Restrictedモードユーザークラス実行機能_design.md
    /// 関連クラス: SecuritySyntaxWalker, SecurityValidator
    /// </summary>
    public class DangerousApiDetector
    {
        private readonly HashSet<string> dangerousNamespaces;
        private readonly HashSet<string> dangerousTypes;
        private readonly Dictionary<string, List<string>> dangerousMembers;
        
        public DangerousApiDetector()
        {
            dangerousNamespaces = new();
            dangerousTypes = new();
            dangerousMembers = new();
            InitializeDangerousPatterns();
        }
        
        private void InitializeDangerousPatterns()
        {
            // 危険な名前空間
            dangerousNamespaces.Add("System.IO");
            dangerousNamespaces.Add("System.Net");
            dangerousNamespaces.Add("System.Net.Http");
            dangerousNamespaces.Add("System.Diagnostics");
            dangerousNamespaces.Add("System.Threading");
            dangerousNamespaces.Add("System.Reflection");
            dangerousNamespaces.Add("System.Runtime.InteropServices");
            dangerousNamespaces.Add("Microsoft.Win32");
            
            // 危険な型
            dangerousTypes.Add("System.IO.File");
            dangerousTypes.Add("System.IO.Directory");
            dangerousTypes.Add("System.IO.FileInfo");
            dangerousTypes.Add("System.IO.DirectoryInfo");
            dangerousTypes.Add("System.IO.Path");
            dangerousTypes.Add("System.IO.Stream");
            dangerousTypes.Add("System.IO.FileStream");
            dangerousTypes.Add("System.Net.Http.HttpClient");
            dangerousTypes.Add("System.Net.WebClient");
            dangerousTypes.Add("System.Net.WebRequest");
            dangerousTypes.Add("System.Net.Sockets.Socket");
            dangerousTypes.Add("System.Net.Sockets.TcpClient");
            dangerousTypes.Add("System.Diagnostics.Process");
            dangerousTypes.Add("System.Diagnostics.ProcessStartInfo");
            dangerousTypes.Add("System.Reflection.Assembly");
            dangerousTypes.Add("System.Activator");
            dangerousTypes.Add("System.Type");
            dangerousTypes.Add("System.Threading.Thread");
            dangerousTypes.Add("System.Threading.Tasks.Task");
            
            // 危険なメンバー（型ごと）
            dangerousMembers["System.IO.File"] = new() { 
                "Delete", "Create", "WriteAllText", "ReadAllText", "Copy", "Move", 
                "WriteAllBytes", "ReadAllBytes", "AppendAllText", "Exists", "Open",
                "OpenRead", "OpenWrite", "Replace"
            };
            
            dangerousMembers["System.IO.Directory"] = new() { 
                "Delete", "Create", "GetFiles", "GetDirectories", "Move",
                "Exists", "CreateDirectory", "GetFileSystemEntries"
            };
            
            dangerousMembers["System.IO.Path"] = new() {
                "GetFullPath", "GetTempPath", "GetTempFileName", "Combine"
            };
            
            dangerousMembers["System.Diagnostics.Process"] = new() { 
                "Start", "Kill", "GetProcesses", "GetCurrentProcess",
                "GetProcessById", "GetProcessesByName"
            };
            
            dangerousMembers["System.Reflection.Assembly"] = new() { 
                "Load", "LoadFrom", "LoadFile", "LoadWithPartialName",
                "GetExecutingAssembly", "GetCallingAssembly", "GetEntryAssembly"
            };
            
            dangerousMembers["System.Type"] = new() { 
                "GetType", "InvokeMember", "GetMethod", "GetProperty", "GetField"
            };
            
            dangerousMembers["System.Activator"] = new() { 
                "CreateInstance", "CreateInstanceFrom", "CreateComInstanceFrom"
            };
            
            dangerousMembers["UnityEditor.AssetDatabase"] = new() { 
                "DeleteAsset", "MoveAsset", "CopyAsset", "CreateAsset"
            };
            
            dangerousMembers["UnityEditor.FileUtil"] = new() { 
                "DeleteFileOrDirectory", "CopyFileOrDirectory", "MoveFileOrDirectory"
            };
            
            dangerousMembers["System.Environment"] = new() {
                "Exit", "FailFast", "SetEnvironmentVariable", "ExpandEnvironmentVariables"
            };
            
            dangerousMembers["System.Threading.Thread"] = new() {
                "Start", "Abort", "Suspend", "Resume", "Join"
            };
        }
        
        public bool IsDangerousNamespace(string namespaceName)
        {
            if (string.IsNullOrWhiteSpace(namespaceName)) return false;
            
            return dangerousNamespaces.Any(ns => 
                namespaceName.StartsWith(ns, StringComparison.OrdinalIgnoreCase));
        }
        
#if ULOOPMCP_HAS_ROSLYN
        public bool IsDangerousType(ITypeSymbol typeSymbol)
        {
            if (typeSymbol == null) return false;
            
            string fullTypeName = typeSymbol.ToDisplayString();
            return dangerousTypes.Contains(fullTypeName);
        }
#endif
        
        public bool IsDangerousApi(string fullApiName)
        {
            if (string.IsNullOrWhiteSpace(fullApiName)) return false;
            
            // APIフルネームを解析
            string[] parts = fullApiName.Split('.');
            if (parts.Length < 2) return false;
            
            // メンバー名とタイプ名を取得
            string memberName = parts[parts.Length - 1];
            string typeName = string.Join(".", parts.Take(parts.Length - 1));
            
            // 危険なメンバーかチェック
            if (dangerousMembers.ContainsKey(typeName))
            {
                return dangerousMembers[typeName].Contains(memberName);
            }
            
            // 危険な型自体かチェック
            if (dangerousTypes.Contains(typeName))
            {
                return true;
            }
            
            // 危険な名前空間内のAPIかチェック
            return dangerousNamespaces.Any(ns => fullApiName.StartsWith(ns, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// カスタム危険パターンを追加
        /// </summary>
        public void AddDangerousType(string typeName)
        {
            if (!string.IsNullOrWhiteSpace(typeName))
            {
                dangerousTypes.Add(typeName);
            }
        }
        
        /// <summary>
        /// カスタム危険メンバーを追加
        /// </summary>
        public void AddDangerousMember(string typeName, string memberName)
        {
            if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(memberName))
                return;
            
            if (!dangerousMembers.ContainsKey(typeName))
            {
                dangerousMembers[typeName] = new List<string>();
            }
            
            if (!dangerousMembers[typeName].Contains(memberName))
            {
                dangerousMembers[typeName].Add(memberName);
            }
        }
        
        /// <summary>
        /// 危険パターンのサマリーを取得（デバッグ用）
        /// </summary>
        public string GetPatternSummary()
        {
            return $"Dangerous patterns: {dangerousNamespaces.Count} namespaces, " +
                   $"{dangerousTypes.Count} types, {dangerousMembers.Count} type-member mappings";
        }
    }
}