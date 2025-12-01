namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Reason for server shutdown, sent to TypeScript MCP server for message differentiation.
    /// Values must match TypeScript ServerShutdownReason constants.
    /// </summary>
    public enum ServerShutdownReason
    {
        /// <summary>
        /// Server is shutting down due to domain reload (compilation).
        /// TypeScript side should show "waiting" message.
        /// </summary>
        DomainReload,

        /// <summary>
        /// Server is shutting down due to editor quit.
        /// TypeScript side should show "server not running" error.
        /// </summary>
        EditorQuit
    }
}
