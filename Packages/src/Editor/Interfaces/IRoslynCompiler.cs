using System;
using System.Collections.Generic;
using System.Reflection;

namespace uLoopMCP.DynamicExecution
{
    /// <summary>
    /// Roslynを使用した動的C#コンパイル機能のインターフェース
    /// 設計ドキュメント: uLoopMCP_DynamicCodeExecution_Design.md
    /// 関連クラス: RoslynCompiler, CompilationRequest, CompilationResult
    /// </summary>
    public interface IRoslynCompiler
    {
        /// <summary>
        /// C#コードをコンパイルして動的アセンブリを生成する
        /// </summary>
        /// <param name="request">コンパイルリクエスト</param>
        /// <returns>コンパイル結果</returns>
        CompilationResult Compile(CompilationRequest request);

        /// <summary>
        /// 参照アセンブリを初期化する
        /// </summary>
        void InitializeReferences();

        /// <summary>
        /// コンパイルキャッシュをクリアする
        /// </summary>
        void ClearCache();
    }

    /// <summary>
    /// コンパイルリクエストのデータモデル
    /// </summary>
    public class CompilationRequest
    {
        /// <summary>コンパイルするC#コード</summary>
        public string Code { get; set; } = "";

        /// <summary>生成するクラス名</summary>
        public string ClassName { get; set; } = "DynamicCommand";

        /// <summary>名前空間</summary>
        public string Namespace { get; set; } = "uLoopMCP.Dynamic";

        /// <summary>追加の参照アセンブリ</summary>
        public List<string> AdditionalReferences { get; set; } = new();
    }

    /// <summary>
    /// コンパイル結果のデータモデル
    /// </summary>
    public class CompilationResult
    {
        /// <summary>コンパイル成功フラグ</summary>
        public bool Success { get; set; }

        /// <summary>コンパイル済みアセンブリ</summary>
        public Assembly CompiledAssembly { get; set; }

        /// <summary>自動修復後の更新コード</summary>
        public string UpdatedCode { get; set; } = "";

        /// <summary>コンパイルエラー</summary>
        public List<CompilationError> Errors { get; set; } = new();

        /// <summary>警告メッセージ</summary>
        public List<string> Warnings { get; set; } = new();
    }
}