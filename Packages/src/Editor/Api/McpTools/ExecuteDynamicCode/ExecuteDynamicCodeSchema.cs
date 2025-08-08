using io.github.hatayama.uLoopMCP;
using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// 動的コード実行ツールのパラメータスキーマ

    /// 関連クラス: ExecuteDynamicCodeTool, ExecuteDynamicCodeResponse
    /// </summary>
    public class ExecuteDynamicCodeSchema : BaseToolSchema
    {
        /// <summary>実行するC#コード</summary>
        public string Code { get; set; } = "";
        
        /// <summary>実行時パラメータ</summary>
        public Dictionary<string, object> Parameters { get; set; } = new();
        
        /// <summary>コンパイルのみ実行（実行はしない）</summary>
        public bool CompileOnly { get; set; } = false;
    }
}