namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// コンパイルエラー
    /// 
    /// 関連クラス: RoslynCompiler, CompilationResult
    /// </summary>
    public class CompilationError
    {
        /// <summary>行番号</summary>
        public int Line { get; set; }

        /// <summary>カラム番号</summary>
        public int Column { get; set; }

        /// <summary>エラーメッセージ</summary>
        public string Message { get; set; }

        /// <summary>エラーコード</summary>
        public string ErrorCode { get; set; }
    }
}