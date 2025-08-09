using System;
using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// コンパイラ設定

    /// 関連クラス: RoslynCompiler
    /// </summary>
    [Serializable]
    public class CompilerSettings
    {
        /// <summary>デフォルト名前空間</summary>
        public string DefaultNamespace { get; set; } = "uLoopMCP.Dynamic";

        /// <summary>デフォルトクラス名</summary>
        public string DefaultClassName { get; set; } = "DynamicCommand";

        /// <summary>自動using文追加</summary>
        public List<string> AutoUsings { get; set; } = new()
        {
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "UnityEngine",
            "UnityEditor"
        };

        /// <summary>追加参照アセンブリ</summary>
        public List<string> AdditionalReferences { get; set; } = new();

        /// <summary>警告をエラーとして扱う</summary>
        public bool TreatWarningsAsErrors { get; set; } = false;

        /// <summary>最適化を有効にする</summary>
        public bool EnableOptimization { get; set; } = false;

        /// <summary>デバッグ情報を含める</summary>
        public bool IncludeDebugInfo { get; set; } = true;

        /// <summary>コンパイルタイムアウト（秒）</summary>
        public int CompilationTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// デフォルト設定を取得
        /// </summary>
        public static CompilerSettings GetDefault()
        {
            return new CompilerSettings();
        }
    }
}