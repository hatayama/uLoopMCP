namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Port validation utility for MCP configuration
    /// </summary>
    public static class McpPortValidator
    {
        private const int MinPort = 1;
        private const int MaxPort = 65535;
        private const int ReservedPortThreshold = 1024;
        private static readonly int[] CommonPorts = { 80, 443, 21, 22, 23, 25, 53, 110, 143, 993, 995, 3389 };

        /// <summary>
        /// Validates port number and logs warnings for potential issues
        /// </summary>
        /// <param name="port">Port number to validate</param>
        /// <param name="context">Additional context for logging (optional)</param>
        /// <returns>True if port is valid, false if port is outside valid range</returns>
        public static bool ValidatePort(int port, string context = "")
        {
            if (port <= 0 || port > MaxPort)
            {
                return false;
            }

            string contextSuffix = string.IsNullOrEmpty(context) ? "" : $" {context}";

            // Skip warnings for UI changes
            bool isUIChange = !string.IsNullOrEmpty(context) && context.Contains("UI port change");
            
            if (port < ReservedPortThreshold && !isUIChange)
            {
                UnityEngine.Debug.LogWarning($"Port {port} is below reserved port threshold ({ReservedPortThreshold}), but allowing for development{contextSuffix}");
                // Allow for development purposes, just log warning
            }
            
            if (System.Array.IndexOf(CommonPorts, port) != -1 && !isUIChange)
            {
                UnityEngine.Debug.LogWarning($"Port {port} is a common system port, but allowing for development{contextSuffix}");
                // Allow for development purposes, just log warning
            }

            return true;
        }
    }
}