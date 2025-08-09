using System;
using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// 動的コード実行結果
    /// 
    /// 関連クラス: DynamicCodeExecutor, CommandRunner
    /// </summary>
    public class ExecutionResult
    {
        /// <summary>実行成功</summary>
        public bool Success { get; set; }

        /// <summary>実行結果</summary>
        public object Result { get; set; }

        /// <summary>エラーメッセージ</summary>
        public string ErrorMessage { get; set; }

        /// <summary>例外</summary>
        public Exception Exception { get; set; }

        /// <summary>実行時間</summary>
        public TimeSpan ExecutionTime { get; set; }

        /// <summary>ログ</summary>
        public List<string> Logs { get; set; } = new();
    }
}