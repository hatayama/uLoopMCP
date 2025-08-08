using io.github.hatayama.uLoopMCP;
using System.Collections.Generic;
using System;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// 動的コード実行ツールのレスポンス
    /// 設計ドキュメント: uLoopMCP_DynamicCodeExecution_Design.md
    /// 関連クラス: ExecuteDynamicCodeTool, ExecuteDynamicCodeSchema
    /// </summary>
    public class ExecuteDynamicCodeResponse : BaseToolResponse
    {
        /// <summary>実行成功フラグ</summary>
        public bool Success { get; set; }
        
        /// <summary>実行結果</summary>
        public string Result { get; set; }
        
        /// <summary>ログメッセージ</summary>
        public List<string> Logs { get; set; } = new();
        
        /// <summary>コンパイルエラー</summary>
        public List<CompilationErrorDto> CompilationErrors { get; set; } = new();
        
        /// <summary>エラーメッセージ（失敗時）</summary>
        public string ErrorMessage { get; set; }
        

    }
    
    /// <summary>
    /// コンパイルエラー情報のDTO
    /// </summary>
    public class CompilationErrorDto
    {
        /// <summary>エラーメッセージ</summary>
        public string Message { get; set; }
        
        /// <summary>行番号</summary>
        public int Line { get; set; }
        
        /// <summary>列番号</summary>
        public int Column { get; set; }
        
        /// <summary>エラーID</summary>
        public string Id { get; set; }
    }
}