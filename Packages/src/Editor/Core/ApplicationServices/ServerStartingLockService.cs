using System;
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
        public static string CreateLockFile()
        {
            string lockPath = LockFilePath;
            string tempDir = Path.GetDirectoryName(lockPath);

            if (!Directory.Exists(tempDir))
            {
                return null;
            }

            string ownershipToken = System.Guid.NewGuid().ToString("N");
            File.WriteAllText(lockPath, ownershipToken);
            return ownershipToken;
        }

        /// <summary>
        /// Delete lock file. Called when server startup completes or on crash recovery.
        /// </summary>
        public static void DeleteLockFile(string ownershipToken = null)
        {
            string lockPath = LockFilePath;
            if (File.Exists(lockPath))
            {
                if (!string.IsNullOrEmpty(ownershipToken))
                {
                    string existingOwnershipToken = TryReadOwnershipToken(lockPath);
                    if (!string.Equals(existingOwnershipToken, ownershipToken, System.StringComparison.Ordinal))
                    {
                        return;
                    }
                }

                TryDeleteLockFile(lockPath);
            }
        }

        private static string TryReadOwnershipToken(string lockPath)
        {
            try
            {
                return File.ReadAllText(lockPath);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static void TryDeleteLockFile(string lockPath)
        {
            try
            {
                File.Delete(lockPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
