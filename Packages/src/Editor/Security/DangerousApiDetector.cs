#if ULOOPMCP_HAS_ROSLYN
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// 危険なAPIパターンを管理・検出するエンジン
    /// 関連クラス: SecuritySyntaxWalker, SecurityValidator
    /// </summary>
    public class DangerousApiDetector
    {
        private static readonly HashSet<string> dangerousTypes = new()
        {
            // System.IO関連（FileInfoやDirectoryInfoは型全体が危険）
            "System.IO.FileInfo",
            "System.IO.DirectoryInfo",
            "System.IO.Path",
            
            // ネットワーク関連
            "System.Net.Http.HttpClient",
            "System.Net.WebClient",
            "System.Net.WebRequest",
            "System.Net.Sockets.Socket",
            "System.Net.Sockets.TcpClient",
            
            // プロセス関連
            "System.Diagnostics.ProcessStartInfo",
            
            // タスク関連
            "System.Threading.Tasks.Task",
            
            // Web関連（Unityでは通常利用不可だが念のため）
            "System.Web.HttpContext",
            "System.Web.HttpRequest",
            "System.Web.HttpResponse",
            
            // UnityEngine.Networking関連
            "UnityEngine.Networking.UnityWebRequest",
            "UnityEngine.Networking.NetworkTransport",
            
            // System.Data関連
            "System.Data.SqlClient.SqlConnection",
            "System.Data.SqlClient.SqlCommand",
            "System.Data.DataSet",
            
            // System.Runtime.Remoting関連
            "System.Runtime.Remoting.RemotingConfiguration",
            "System.Runtime.Remoting.RemotingServices",
            
            // System.Security.Cryptography関連（証明書操作）
            "System.Security.Cryptography.X509Certificates.X509Certificate",
            "System.Security.Cryptography.X509Certificates.X509Store"
        };
        
        // 危険なメンバー（型ごと）を定数的に宣言
        private static readonly Dictionary<string, List<string>> dangerousMembers = new()
        {
            // System.IO.File - 削除や書き込み系は危険
            ["System.IO.File"] = new() 
            { 
                "Delete", "WriteAllText", "WriteAllBytes", "Replace"
                // Create, Copy, Move, ReadAllText, ReadAllBytes, Exists, Open系は比較的安全
            },
            
            // System.IO.Directory - 削除は危険
            ["System.IO.Directory"] = new() 
            { 
                "Delete"
                // Create, GetFiles, GetDirectories, Move, Exists は比較的安全
            },
            
            // System.Diagnostics.Process - 起動と強制終了は危険
            ["System.Diagnostics.Process"] = new() 
            { 
                "Start", "Kill"
                // GetProcesses, GetCurrentProcess等は情報取得のみで比較的安全
            },
            
            // System.Reflection.Assembly - 動的ロードは危険
            ["System.Reflection.Assembly"] = new() 
            { 
                "Load", "LoadFrom", "LoadFile", "LoadWithPartialName"
                // GetExecutingAssembly等は情報取得のみで比較的安全
            },
            
            // System.Type - 動的メソッド呼び出しは危険
            ["System.Type"] = new() 
            { 
                "InvokeMember"
                // GetType, GetMethod等は情報取得のみで比較的安全
            },
            
            // System.Activator - COMオブジェクト作成は危険
            ["System.Activator"] = new() 
            { 
                "CreateComInstanceFrom"
                // CreateInstanceは用途次第だが許可
            },
            
            // UnityEditor.AssetDatabase - データ永久削除は危険
            ["UnityEditor.AssetDatabase"] = new() 
            { 
                "DeleteAsset"
                // MoveAsset, CopyAsset, CreateAssetは比較的安全なので除外
            },
            
            // UnityEditor.FileUtil - ファイル削除は危険
            ["UnityEditor.FileUtil"] = new() 
            { 
                "DeleteFileOrDirectory"
                // CopyFileOrDirectory, MoveFileOrDirectoryは比較的安全
            },
            
            // System.Environment - アプリケーション終了は危険
            ["System.Environment"] = new() 
            {
                "Exit", "FailFast"
                // SetEnvironmentVariable, ExpandEnvironmentVariablesは比較的安全
            },
            
            // System.Threading.Thread - スレッド強制操作は危険
            ["System.Threading.Thread"] = new() 
            {
                "Abort", "Suspend", "Resume"
                // Start, Joinは比較的安全
            },
            
            // セキュリティ設定の変更を防ぐための追加制限
            ["io.github.hatayama.uLoopMCP.DynamicCodeSecurityManager"] = new() 
            {
                "InitializeFromSettings"  // セキュリティレベル変更は危険（CurrentLevelは読み取りのみなので許可）
            },
            
            ["io.github.hatayama.uLoopMCP.McpEditorSettings"] = new() 
            {
                "SetDynamicCodeSecurityLevel"  // セキュリティレベル操作は危険
            }
        };
        
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
            
            return false;
        }
    }
}
#endif