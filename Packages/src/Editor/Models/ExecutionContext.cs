using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// 実行コンテキスト
    /// 
    /// 関連クラス: CommandRunner
    /// </summary>
    public class ExecutionContext
    {
        /// <summary>コンパイル済みアセンブリ</summary>
        public Assembly CompiledAssembly { get; set; }

        /// <summary>パラメーター</summary>
        public Dictionary<string, object> Parameters { get; set; } = new();

        /// <summary>キャンセレーショントークン</summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>タイムアウト秒数</summary>
        public int TimeoutSeconds { get; set; } = 60;
    }
}