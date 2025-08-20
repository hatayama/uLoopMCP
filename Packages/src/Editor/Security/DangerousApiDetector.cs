#if ULOOPMCP_HAS_ROSLYN
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// 危険なAPIパターンを管理・検出するエンジン
    /// v4.1 名前空間チェック削除 - メソッドレベル制御に特化
    /// 設計ドキュメント参照: working-notes/2025-08-16_Restrictedモードユーザークラス実行機能_design.md
    /// 関連クラス: SecuritySyntaxWalker, SecurityValidator
    /// </summary>
    public class DangerousApiDetector
    {
        private readonly HashSet<string> dangerousTypes;
        private readonly Dictionary<string, List<string>> dangerousMembers;
        
        public DangerousApiDetector()
        {
            dangerousTypes = new();
            dangerousMembers = new();
            InitializeDangerousPatterns();
        }
        
        private void InitializeDangerousPatterns()
        {
            // v4.1: 名前空間チェックは削除（コンパイル時制限廃止のため）
            // メソッドレベルの細かい制御に集中
            
            // 危険な型
            // System.IO.FileとDirectoryは型全体ではなくメソッドレベルで制御
            // dangerousTypes.Add("System.IO.File");  // コメントアウト - メソッドレベル制御のみ
            // dangerousTypes.Add("System.IO.Directory");  // コメントアウト - メソッドレベル制御のみ
            dangerousTypes.Add("System.IO.FileInfo");
            dangerousTypes.Add("System.IO.DirectoryInfo");
            dangerousTypes.Add("System.IO.Path");
            // Stream系は特定メソッドのみ制限（型全体は許可）
            // FileStreamも型全体のブロックは避ける（File.Createが使えなくなるため）
            dangerousTypes.Add("System.Net.Http.HttpClient");
            dangerousTypes.Add("System.Net.WebClient");
            dangerousTypes.Add("System.Net.WebRequest");
            dangerousTypes.Add("System.Net.Sockets.Socket");
            dangerousTypes.Add("System.Net.Sockets.TcpClient");
            // System.Diagnostics.Process - メソッドレベルで制御（Start, Killのみ危険）
            dangerousTypes.Add("System.Diagnostics.ProcessStartInfo");
            // System.Reflection.Assembly - メソッドレベルで制御（Load系のみ危険）
            // System.Activator - メソッドレベルで制御（CreateComInstanceFromのみ危険）
            // System.Type - メンバーレベルで制御（InvokeMemberのみ危険）
            // System.Threading.Thread - メソッドレベルで制御（Abort, Suspend, Resumeのみ危険）
            dangerousTypes.Add("System.Threading.Tasks.Task");
            
            // 危険なメンバー（型ごと）
            dangerousMembers["System.IO.File"] = new() { 
                "Delete", "WriteAllText", "WriteAllBytes", "Replace"
                // Create, Copy, Move, ReadAllText, ReadAllBytes, Exists, Open系は比較的安全
            };
            
            dangerousMembers["System.IO.Directory"] = new() { 
                "Delete"
                // Create, GetFiles, GetDirectories, Move, Exists は比較的安全
            };
            
            // System.IO.Path は全て安全な補助機能なので除外
            
            dangerousMembers["System.Diagnostics.Process"] = new() { 
                "Start", "Kill"
                // GetProcesses, GetCurrentProcess等は情報取得のみで比較的安全
            };
            
            dangerousMembers["System.Reflection.Assembly"] = new() { 
                "Load", "LoadFrom", "LoadFile", "LoadWithPartialName"
                // GetExecutingAssembly等は情報取得のみで比較的安全
            };
            
            dangerousMembers["System.Type"] = new() { 
                "InvokeMember"  // 動的メソッド呼び出しは危険
                // GetType, GetMethod等は情報取得のみで比較的安全  
            };
            
            dangerousMembers["System.Activator"] = new() { 
                "CreateComInstanceFrom"  // COMオブジェクト作成は危険
                // CreateInstanceは用途次第だが許可
            };
            
            dangerousMembers["UnityEditor.AssetDatabase"] = new() { 
                "DeleteAsset"  // データ永久削除の危険性があるため制限
                // MoveAsset, CopyAsset, CreateAssetは比較的安全なので除外
            };
            
            dangerousMembers["UnityEditor.FileUtil"] = new() { 
                "DeleteFileOrDirectory"
                // CopyFileOrDirectory, MoveFileOrDirectoryは比較的安全
            };
            
            dangerousMembers["System.Environment"] = new() {
                "Exit", "FailFast"  // アプリケーション終了は危険
                // SetEnvironmentVariable, ExpandEnvironmentVariablesは比較的安全
            };
            
            dangerousMembers["System.Threading.Thread"] = new() {
                "Abort", "Suspend", "Resume"  // スレッド強制操作は危険
                // Start, Joinは比較的安全
            };
            
            // セキュリティ設定の変更を防ぐための追加制限
            // 型全体ではなくメソッドレベルで制御
            
            dangerousMembers["io.github.hatayama.uLoopMCP.DynamicCodeSecurityManager"] = new() {
                "InitializeFromSettings"  // セキュリティレベル変更は危険（CurrentLevelは読み取りのみなので許可）
            };
            
            dangerousMembers["io.github.hatayama.uLoopMCP.McpEditorSettings"] = new() {
                "SetDynamicCodeSecurityLevel", "GetDynamicCodeSecurityLevel"  // セキュリティレベル操作は危険
            };
            
            // 追加の危険な型（テストで期待されている）
            // System.Web関連（Unityでは通常利用不可だが念のため）
            dangerousTypes.Add("System.Web.HttpContext");
            dangerousTypes.Add("System.Web.HttpRequest");
            dangerousTypes.Add("System.Web.HttpResponse");
            
            // UnityEngine.Networking関連
            dangerousTypes.Add("UnityEngine.Networking.UnityWebRequest");
            dangerousTypes.Add("UnityEngine.Networking.NetworkTransport");
            
            // System.Data関連
            dangerousTypes.Add("System.Data.SqlClient.SqlConnection");
            dangerousTypes.Add("System.Data.SqlClient.SqlCommand");
            dangerousTypes.Add("System.Data.DataSet");
            
            // System.Runtime.Remoting関連
            dangerousTypes.Add("System.Runtime.Remoting.RemotingConfiguration");
            dangerousTypes.Add("System.Runtime.Remoting.RemotingServices");
            
            // System.Security.Cryptography関連（証明書操作）
            dangerousTypes.Add("System.Security.Cryptography.X509Certificates.X509Certificate");
            dangerousTypes.Add("System.Security.Cryptography.X509Certificates.X509Store");
        }
        
        public bool IsDangerousType(ITypeSymbol typeSymbol)
        {
            if (typeSymbol == null) return false;
            
            string fullTypeName = typeSymbol.ToDisplayString();
            return dangerousTypes.Contains(fullTypeName);
        }
        
        public bool IsDangerousApi(string fullApiName)
        {
            if (string.IsNullOrWhiteSpace(fullApiName)) return false;
            
            // まず危険な型自体かチェック（型名のみの場合）
            if (dangerousTypes.Contains(fullApiName))
            {
                return true;
            }
            
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
            
            // 危険な型のコンストラクタなど
            if (dangerousTypes.Contains(typeName))
            {
                return true;
            }
            
            // v4.1: 名前空間チェックは削除（メソッドレベル制御に集中）
            return false;
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
            return $"Dangerous patterns: {dangerousTypes.Count} types, " +
                   $"{dangerousMembers.Count} type-member mappings";
        }
    }
}
#endif