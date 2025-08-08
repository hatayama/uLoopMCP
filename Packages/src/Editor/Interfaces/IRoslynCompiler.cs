using System;
using System.Collections.Generic;
using System.Reflection;

namespace io.github.hatayama.uLoopMCP.DynamicExecution
{
    /// <summary>
    /// Roslynを使用した動的C#コンパイル機能のインターフェース
    /// 設計ドキュメント: uLoopMCP_DynamicCodeExecution_Design.md
    /// 関連クラス: RoslynCompiler, CompilationRequest, CompilationResult
    /// </summary>
    public interface IRoslynCompiler
    {
        /// <summary>コード コンパイル</summary>
        CompilationResult Compile(CompilationRequest request);
        
        /// <summary>参照設定初期化</summary>
        void InitializeReferences();
        
        /// <summary>キャッシュクリア</summary>
        void ClearCache();
    }

    /// <summary>コンパイル要求</summary>
    public class CompilationRequest
    {
        /// <summary>コンパイルするC#コード</summary>
        public string Code { get; set; } = "";
        
        /// <summary>クラス名</summary>
        public string ClassName { get; set; } = "DynamicCommand";
        
        /// <summary>名前空間</summary>
        public string Namespace { get; set; } = "uLoopMCP.Dynamic";
        
        /// <summary>追加参照</summary>
        public List<string> AdditionalReferences { get; set; } = new();
    }

    /// <summary>コンパイル結果</summary>
    public class CompilationResult
    {
        /// <summary>コンパイル成功フラグ</summary>
        public bool Success { get; set; }
        
        /// <summary>コンパイル済みアセンブリ</summary>
        public Assembly CompiledAssembly { get; set; }
        
        /// <summary>更新されたコード（自動修正適用後）</summary>
        public string UpdatedCode { get; set; } = "";
        
        /// <summary>コンパイルエラー</summary>
        public List<CompilationError> Errors { get; set; } = new();
        
        /// <summary>警告</summary>
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>コンパイルエラー情報</summary>
    public class CompilationError
    {
        /// <summary>エラーメッセージ</summary>
        public string Message { get; set; } = "";

        /// <summary>行番号</summary>
        public int Line { get; set; }

        /// <summary>行番号（LineNumberエイリアス）</summary>
        public int LineNumber 
        { 
            get => Line; 
            set => Line = value; 
        }

        /// <summary>列番号</summary>
        public int Column { get; set; }

        /// <summary>列番号（ColumnNumberエイリアス）</summary>
        public int ColumnNumber 
        { 
            get => Column; 
            set => Column = value; 
        }

        /// <summary>エラーID</summary>
        public string Id { get; set; } = "";

        /// <summary>エラーID（ErrorCodeエイリアス）</summary>
        public string ErrorCode 
        { 
            get => Id; 
            set => Id = value; 
        }

        /// <summary>重要度レベル</summary>
        public string Severity { get; set; } = "";

        /// <summary>ファイルパス</summary>
        public string FilePath { get; set; } = "";
    }
}