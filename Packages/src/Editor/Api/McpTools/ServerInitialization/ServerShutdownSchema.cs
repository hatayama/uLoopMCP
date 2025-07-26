namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Schema for server shutdown request
    /// </summary>
    public class ServerShutdownSchema : BaseToolSchema
    {
        /// <summary>
        /// Force shutdown flag
        /// </summary>
        public bool ForceShutdown { get; set; } = false;
    }
}