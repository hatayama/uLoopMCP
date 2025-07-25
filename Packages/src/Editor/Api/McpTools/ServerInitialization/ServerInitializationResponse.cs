namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// サーバー初期化レスポンス
    /// </summary>
    public class ServerInitializationResponse : BaseToolResponse
    {
        /// <summary>
        /// 処理が成功したかどうか
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 実際に使用されたサーバーポート
        /// </summary>
        public int ServerPort { get; set; }

        /// <summary>
        /// サーバーが正常に起動したかどうか
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// 結果メッセージ
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 作成されたサーバーインスタンス
        /// </summary>
        public McpBridgeServer ServerInstance { get; set; }
    }
}