using System;
using System.Collections.Generic;

namespace uLoopMCP.DynamicExecution
{
    /// <summary>
    /// セキュリティポリシー設定
    /// 設計ドキュメント: uLoopMCP_DynamicCodeExecution_Design.md
    /// 関連クラス: SecurityValidator
    /// </summary>
    [Serializable]
    public class SecurityPolicy
    {
        /// <summary>禁止された名前空間一覧</summary>
        public List<string> ForbiddenNamespaces { get; set; } = new()
        {
            "System.Net",
            "System.Diagnostics", 
            "System.Runtime.InteropServices",
            "System.Reflection"
        };

        /// <summary>禁止されたメソッド一覧</summary>
        public List<string> ForbiddenMethods { get; set; } = new()
        {
            "UnityEditor.AssetDatabase.DeleteAsset",
            "UnityEditor.FileUtil.DeleteFileOrDirectory",
            "System.IO.File.Delete",
            "System.IO.Directory.Delete"
        };

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

        /// <summary>
        /// デフォルトセキュリティポリシーを取得
        /// </summary>
        public static SecurityPolicy GetDefault()
        {
            return new SecurityPolicy();
        }

        /// <summary>
        /// 厳格なセキュリティポリシーを取得
        /// </summary>
        public static SecurityPolicy GetStrict()
        {
            var policy = new SecurityPolicy();
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