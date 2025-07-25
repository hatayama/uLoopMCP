namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Application service responsible for port allocation and conflict resolution.
    /// Handles port finding, validation, and conflict management.
    /// 
    /// Related classes:
    /// - NetworkUtility: Provides port availability checking
    /// - McpPortValidator: Validates port configurations
    /// - McpPortChangeUpdater: Updates configurations on port changes
    /// </summary>
    public class PortAllocationService
    {
        /// <summary>
        /// Finds an available port starting from the given port number.
        /// </summary>
        /// <param name="startPort">The starting port number to check</param>
        /// <returns>The first available port number</returns>
        public ServiceResult<int> FindAvailablePort(int startPort)
        {
            int availablePort = NetworkUtility.FindAvailablePort(startPort);
            return ServiceResult<int>.SuccessResult(availablePort);
        }

        /// <summary>
        /// Handles port conflict resolution with user confirmation.
        /// </summary>
        /// <param name="requestedPort">Originally requested port</param>
        /// <param name="availablePort">Available alternative port</param>
        /// <returns>True if user accepted the alternative port</returns>
        public ServiceResult<bool> HandlePortConflict(int requestedPort, int availablePort)
        {
            bool userConfirmed = UnityEditor.EditorUtility.DisplayDialog(
                "Port Conflict",
                $"Port {requestedPort} is already in use.\n\nWould you like to use port {availablePort} instead?",
                "OK",
                "Cancel"
            );

            if (userConfirmed)
            {
                McpPortChangeUpdater.UpdateAllConfigurationsForPortChange(availablePort, "Server port conflict resolution");
            }

            return ServiceResult<bool>.SuccessResult(userConfirmed);
        }
    }
}