using System;
using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// セキュリティポリシー設定

    /// 関連クラス: SecurityValidator
    /// </summary>
    [Serializable]
    public class SecurityPolicy
    {
        /// <summary>禁止された名前空間一覧</summary>
        public List<string> ForbiddenNamespaces { get; set; } = new();

        /// <summary>禁止されたメソッド一覧</summary>
        public List<string> ForbiddenMethods { get; set; } = new();

        /// <summary>禁止された型一覧</summary>
        public List<string> ForbiddenTypes { get; set; } = new();

        /// <summary>最大実行時間（秒）</summary>
        public int MaxExecutionTimeSeconds { get; set; } = 300;

        /// <summary>最大メモリ使用量（MB）</summary>
        public int MaxMemoryMB { get; set; } = 100;

        /// <summary>ファイルシステムアクセス許可</summary>
        public bool AllowFileSystemAccess { get; set; } = false;

        /// <summary>ネットワークアクセス許可</summary>
        public bool AllowNetworkAccess { get; set; } = false;

        /// <summary>リフレクション使用許可</summary>
        public bool AllowReflection { get; set; } = false;

        /// <summary>unsafe コード許可</summary>
        public bool AllowUnsafeCode { get; set; } = false;

        /// <summary>外部プロセス実行許可</summary>
        public bool AllowProcessExecution { get; set; } = false;

        /// <summary>最大コード長</summary>
        public int MaxCodeLength { get; set; } = 10000;

        /// <summary>
        /// デフォルトセキュリティポリシーを取得
        /// </summary>
        public static SecurityPolicy GetDefault()
        {
            return new SecurityPolicy
            {
                ForbiddenNamespaces = new List<string>
                {
                    "System.Net",
                    "System.Diagnostics", 
                    "System.Runtime.InteropServices",
                    "System.Reflection",
                    "System.IO",
                    "System.Threading",
                    "Microsoft.Win32"
                },
                ForbiddenMethods = new List<string>
                {
                    // Unity危険メソッド
                    "UnityEditor.AssetDatabase.DeleteAsset",
                    "UnityEditor.FileUtil.DeleteFileOrDirectory",
                    
                    // ファイルシステム操作
                    "System.IO.File.Delete",
                    "System.IO.File.WriteAllText",
                    "System.IO.Directory.Delete",
                    
                    // プロセス実行
                    "System.Diagnostics.Process.Start",
                    
                    // リフレクション/アセンブリ操作
                    "System.Reflection.Assembly.LoadFrom",
                    "System.Reflection.Assembly.LoadFile",
                    
                    // ネットワーク
                    "System.Net.WebClient",
                    "System.Net.Http.HttpClient",
                    
                    // 危険なシステム操作
                    "System.Runtime.InteropServices.Marshal"
                }
            };
        }

        /// <summary>
        /// 厳格なセキュリティポリシーを取得
        /// </summary>
        public static SecurityPolicy GetStrict()
        {
            SecurityPolicy policy = new SecurityPolicy();
            policy.ForbiddenNamespaces.AddRange(new[]
            {
                "System.IO",
                "System.Threading",
                "System.Net.Sockets",
                "System.Web"
            });
            policy.MaxExecutionTimeSeconds = 60;
            policy.MaxMemoryMB = 50;
            return policy;
        }
    }
}