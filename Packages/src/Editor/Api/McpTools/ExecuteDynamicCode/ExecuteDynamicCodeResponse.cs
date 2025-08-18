using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// 動的コード実行ツールのレスポンス

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
        
        /// <summary>現在のセキュリティレベル</summary>
        public string SecurityLevel { get; set; }
        
        /// <summary>エラーメッセージ（ErrorMessageのエイリアス）</summary>
        public string Error 
        { 
            get => ErrorMessage; 
            set => ErrorMessage = value; 
        }
        
        /// <summary>更新されたコード（修正適用後）</summary>
        public string UpdatedCode { get; set; }
        
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
        
        /// <summary>コンパイラーエラーコード（CS0103など）</summary>
        public string ErrorCode { get; set; }
    }
}