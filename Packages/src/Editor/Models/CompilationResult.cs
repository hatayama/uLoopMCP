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

        /// <summary>
        /// コンパイル用に整形されたコード
        /// （using文の抽出・移動、クラス/メソッドラップ適用後）
        /// </summary>
        public string UpdatedCode { get; set; }

        /// <summary>セキュリティ違反があった場合のフラグ</summary>
        public bool HasSecurityViolations { get; set; }

        /// <summary>セキュリティ違反の詳細</summary>
        public List<SecurityViolation> SecurityViolations { get; set; } = new();

        /// <summary>失敗の理由カテゴリ</summary>
        public CompilationFailureReason FailureReason { get; set; } = CompilationFailureReason.None;
    }

    /// <summary>
    /// コンパイル失敗の理由カテゴリ
    /// </summary>
    public enum CompilationFailureReason
    {
        /// <summary>失敗なし（成功）</summary>
        None,

        /// <summary>コンパイルエラー</summary>
        CompilationError,

        /// <summary>セキュリティ違反</summary>
        SecurityViolation,

        /// <summary>動的アセンブリ追加失敗</summary>
        DynamicAssemblyFailed,

        /// <summary>using文追加失敗</summary>
        UsingStatementFailed
    }
}