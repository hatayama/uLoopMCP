namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// サーバー初期化リクエストのスキーマ
    /// </summary>
    public class ServerInitializationSchema : BaseToolSchema
    {
        /// <summary>
        /// 起動するポート番号（-1でデフォルト使用）
        /// </summary>
        public int Port { get; set; } = -1;
    }
}