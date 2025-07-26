using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Application service responsible for initialization notifications and dialogs.
    /// Handles user confirmations, error notifications, and status updates.
    /// 
    /// Related classes:
    /// - UnityEditor.EditorUtility: Provides dialog functionality
    /// - McpServerController: Uses this service for user interactions
    /// </summary>
    public class InitializationNotificationService
    {
        /// <summary>
        /// Shows an invalid port error dialog to the user.
        /// </summary>
        /// <param name="port">The invalid port number</param>
        /// <returns>Success indicator</returns>
        public ServiceResult<bool> ShowInvalidPortDialog(int port)
        {
            EditorUtility.DisplayDialog(
                "Invalid Port",
                $"Port {port} is not valid for server startup.\n\nPort must be 1024 or higher and not a reserved system port.",
                "OK"
            );
            return ServiceResult<bool>.SuccessResult(true);
        }

        /// <summary>
        /// Shows a configuration validation error dialog.
        /// </summary>
        /// <param name="errorMessage">The validation error message</param>
        /// <returns>Success indicator</returns>
        public ServiceResult<bool> ShowValidationErrorDialog(string errorMessage)
        {
            EditorUtility.DisplayDialog(
                "Server Configuration Error",
                $"Server configuration validation failed:\n\n{errorMessage}",
                "OK"
            );
            return ServiceResult<bool>.SuccessResult(true);
        }
    }
}