using System.Collections.Generic;
using System.Reflection;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// コンパイル結果
    /// 
    /// 関連クラス: RoslynCompiler
    /// </summary>
    public class CompilationResult
    {
        /// <summary>コンパイル成功</summary>
        public bool Success { get; set; }

        /// <summary>コンパイル済みアセンブリ</summary>
        public Assembly CompiledAssembly { get; set; }

        /// <summary>エラー</summary>
        public List<CompilationError> Errors { get; set; } = new();

        /// <summary>警告</summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>更新されたコード</summary>
        public string UpdatedCode { get; set; }
    }
}