namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// サーバー終了リクエストのスキーマ
    /// </summary>
    public class ServerShutdownSchema : BaseToolSchema
    {
        /// <summary>
        /// 強制終了フラグ
        /// </summary>
        public bool ForceShutdown { get; set; } = false;
    }
}