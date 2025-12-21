using System.IO;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Application service responsible for server starting lock file management.
    /// Single responsibility: Create/delete lock file during server startup for CLI detection.
    /// Related classes: CompilationLockService, DomainReloadDetectionService (similar patterns)
    /// Design reference: @Packages/docs/ARCHITECTURE_Unity.md - Application Service Layer (Single Function Implementation)
    /// </summary>
    public static class ServerStartingLockService
    {
        private const string LOCK_FILE_NAME = "serverstarting.lock";

        private static string LockFilePath => Path.Combine(UnityEngine.Application.dataPath, "..", "Temp", LOCK_FILE_NAME);

        /// <summary>
        /// Create lock file to signal server is starting.
        /// </summary>
        public static void CreateLockFile()
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
        /// Delete lock file. Called when server startup completes or on crash recovery.
        /// </summary>
        public static void DeleteLockFile()
        {
            string lockPath = LockFilePath;
            if (File.Exists(lockPath))
            {
                File.Delete(lockPath);
            }
        }
    }
}
