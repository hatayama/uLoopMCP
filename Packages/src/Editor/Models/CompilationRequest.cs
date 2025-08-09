using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// コンパイル要求
    /// 
    /// 関連クラス: RoslynCompiler
    /// </summary>
    public class CompilationRequest
    {
        /// <summary>コード</summary>
        public string Code { get; set; }

        /// <summary>クラス名</summary>
        public string ClassName { get; set; }

        /// <summary>名前空間</summary>
        public string Namespace { get; set; }

        /// <summary>追加参照</summary>
        public List<string> AdditionalReferences { get; set; } = new();
    }
}