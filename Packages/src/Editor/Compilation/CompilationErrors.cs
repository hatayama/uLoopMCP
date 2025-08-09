using System.Collections.Generic;
using System.Text;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// コンパイルエラーの構造化管理クラス
    /// Unity AI Assistantの実装パターンを採用
    /// </summary>
    public class CompilationErrors
    {
        public class ErrorLog
        {
            public int Line { get; set; }
            public string Message { get; set; }
        }

        public List<ErrorLog> Errors { get; } = new();

        public void Add(string message, int line = -1)
        {
            Errors.Add(new ErrorLog { Line = line, Message = message });
        }

        public override string ToString()
        {
            StringBuilder errorLog = new();
            foreach (ErrorLog error in Errors)
            {
                errorLog.AppendLine($"- Error {error.Message} (Line: {error.Line + 1})");
            }
            return errorLog.ToString();
        }
    }
}