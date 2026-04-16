using System;
using System.IO;
using System.Threading;

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
        private const int FILE_OPERATION_RETRY_COUNT = 3;
        private const int FILE_OPERATION_RETRY_DELAY_MILLISECONDS = 50;

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

        public static void DeleteOwnedLockFile(string ownershipToken)
        {
            if (string.IsNullOrEmpty(ownershipToken))
            {
                return;
            }

            DeleteLockFile(ownershipToken);
        }

        private static string TryReadOwnershipToken(string lockPath)
        {
            for (int attempt = 0; attempt < FILE_OPERATION_RETRY_COUNT; attempt++)
            {
                try
                {
                    return File.ReadAllText(lockPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                if (attempt < FILE_OPERATION_RETRY_COUNT - 1)
                {
                    Thread.Sleep(FILE_OPERATION_RETRY_DELAY_MILLISECONDS);
                }
            }

            VibeLogger.LogWarning("server_starting_lock_read_failed", $"Failed to read ownership token: {lockPath}");
            return null;
        }

        private static void TryDeleteLockFile(string lockPath)
        {
            for (int attempt = 0; attempt < FILE_OPERATION_RETRY_COUNT; attempt++)
            {
                try
                {
                    File.Delete(lockPath);
                    if (!File.Exists(lockPath))
                    {
                        return;
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                if (attempt < FILE_OPERATION_RETRY_COUNT - 1)
                {
                    Thread.Sleep(FILE_OPERATION_RETRY_DELAY_MILLISECONDS);
                }
            }

            VibeLogger.LogWarning("server_starting_lock_delete_failed", $"Failed to delete lock file: {lockPath}");
        }
    }
}
