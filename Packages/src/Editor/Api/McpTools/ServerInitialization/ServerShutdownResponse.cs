namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// サーバー終了レスポンス
    /// </summary>
    public class ServerShutdownResponse : BaseToolResponse
    {
        /// <summary>
        /// 処理が成功したかどうか
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 結果メッセージ
        /// </summary>
        public string Message { get; set; }
    }
}