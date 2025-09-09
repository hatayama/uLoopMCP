namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Abstraction for MCP editor configuration services.
    /// Provides uniform API for JSON- and TOML-based editors.
    /// </summary>
    public interface IMcpConfigService
    {
        bool IsConfigured();
        bool IsUpdateNeeded(int port);
        void AutoConfigure(int port);
        int GetConfiguredPort();
        void UpdateDevelopmentSettings(int port, bool developmentMode, bool enableMcpLogs);
    }
}


