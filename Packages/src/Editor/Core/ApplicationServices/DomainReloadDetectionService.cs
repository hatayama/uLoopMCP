using System.IO;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Application service responsible for Domain Reload detection and state management
    /// Single responsibility: Domain Reload lifecycle management
    /// Related classes: McpSessionManager, McpServerController
    /// Design reference: @Packages/docs/ARCHITECTURE_Unity.md - Application Service Layer (Single Function Implementation)
    /// </summary>
    [InitializeOnLoad]
    public static class DomainReloadDetectionService
    {
        static DomainReloadDetectionService()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        private static void OnBeforeAssemblyReload()
        {
            UnityEngine.Debug.Log("[DomainReloadDetectionService] Before assembly reload, creating lock file");
            CreateLockFile();
        }

        private static void OnAfterAssemblyReload()
        {
            UnityEngine.Debug.Log("[DomainReloadDetectionService] After assembly reload, deleting lock file");
            DeleteLockFile();
        }

        private const string LOCK_FILE_NAME = "domainreload.lock";

        private static string LockFilePath => Path.Combine(UnityEngine.Application.dataPath, "..", "Temp", LOCK_FILE_NAME);

        /// <summary>
        /// Execute Domain Reload start processing
        /// </summary>
        /// <param name="correlationId">Tracking ID for related operations</param>
        /// <param name="serverIsRunning">Whether server is running</param>
        /// <param name="serverPort">Server port number</param>
        public static void StartDomainReload(string correlationId, bool serverIsRunning, int? serverPort)
        {
            // Create lock file for external process detection (e.g., CLI tools)
            CreateLockFile();

            // Set Domain Reload in progress flag
            McpEditorSettings.SetIsDomainReloadInProgress(true);

            // Save session state if server is running
            if (serverIsRunning && serverPort.HasValue)
            {
                McpEditorSettings.SetIsServerRunning(true);
                McpEditorSettings.SetServerPort(serverPort.Value);
                McpEditorSettings.SetIsAfterCompile(true);
                McpEditorSettings.SetIsReconnecting(true);
                McpEditorSettings.SetShowReconnectingUI(true);
                McpEditorSettings.SetShowPostCompileReconnectingUI(true);
            }

            // Log recording
            VibeLogger.LogInfo(
                "domain_reload_start",
                "Domain reload starting",
                new
                {
                    server_running = serverIsRunning,
                    server_port = serverPort
                },
                correlationId
            );
        }

        /// <summary>
        /// Execute Domain Reload completion processing
        /// </summary>
        /// <param name="correlationId">Tracking ID for related operations</param>
        public static void CompleteDomainReload(string correlationId)
        {
            // Delete lock file for external process detection
            DeleteLockFile();

            // Clear Domain Reload completion flag
            McpEditorSettings.ClearDomainReloadFlag();

            // Log recording
            VibeLogger.LogInfo(
                "domain_reload_complete",
                "Domain reload completed - starting server recovery process",
                new { session_server_port = McpEditorSettings.GetServerPort() },
                correlationId
            );
        }

        /// <summary>
        /// Check if currently in Domain Reload
        /// </summary>
        /// <returns>True if Domain Reload is in progress</returns>
        public static bool IsDomainReloadInProgress()
        {
            return McpEditorSettings.GetIsDomainReloadInProgress();
        }

        /// <summary>
        /// Check if reconnection UI display is required
        /// </summary>
        /// <returns>True if reconnection UI display is required</returns>
        public static bool ShouldShowReconnectingUI()
        {
            return McpEditorSettings.GetShowReconnectingUI();
        }

        /// <summary>
        /// Check if in after-compile state
        /// </summary>
        /// <returns>True if after compile</returns>
        public static bool IsAfterCompile()
        {
            return McpEditorSettings.GetIsAfterCompile();
        }

        private static void CreateLockFile()
        {
            string lockPath = LockFilePath;
            string tempDir = Path.GetDirectoryName(lockPath);

            if (!Directory.Exists(tempDir))
            {
                return;
            }

            File.WriteAllText(lockPath, System.DateTime.UtcNow.ToString("o"));
        }

        /// <summary>
        /// Delete lock file to signal Domain Reload completion.
        /// </summary>
        public static void DeleteLockFile()
        {
            string lockPath = LockFilePath;
            if (File.Exists(lockPath))
            {
                File.Delete(lockPath);
            }
        }

        /// <summary>
        /// Check if Domain Reload lock file exists.
        /// Used by external processes to detect Domain Reload state.
        /// </summary>
        /// <returns>True if lock file exists</returns>
        public static bool IsLockFilePresent()
        {
            return File.Exists(LockFilePath);
        }
    }
}