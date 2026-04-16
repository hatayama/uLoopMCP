using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    [TestFixture]
    public class ServerStartingLockServiceTests
    {
        [Test]
        public void DeleteOwnedLockFile_WhenOwnershipTokenIsMissing_ShouldPreserveExistingLockFile()
        {
            string lockFilePath = GetLockFilePath();
            bool hadExistingLockFile = File.Exists(lockFilePath);
            string previousLockFileContents = hadExistingLockFile ? File.ReadAllText(lockFilePath) : null;
            string createdToken = ServerStartingLockService.CreateLockFile();

            try
            {
                ServerStartingLockService.DeleteOwnedLockFile(null);

                Assert.That(File.Exists(lockFilePath), Is.True);
                Assert.That(File.ReadAllText(lockFilePath), Is.EqualTo(createdToken));
            }
            finally
            {
                RestoreLockFile(lockFilePath, hadExistingLockFile, previousLockFileContents);
            }
        }

        [Test]
        public void DeleteOwnedLockFile_WhenOwnershipTokenMatches_ShouldDeleteExistingLockFile()
        {
            string lockFilePath = GetLockFilePath();
            bool hadExistingLockFile = File.Exists(lockFilePath);
            string previousLockFileContents = hadExistingLockFile ? File.ReadAllText(lockFilePath) : null;
            string createdToken = ServerStartingLockService.CreateLockFile();

            try
            {
                ServerStartingLockService.DeleteOwnedLockFile(createdToken);

                Assert.That(File.Exists(lockFilePath), Is.False);
            }
            finally
            {
                RestoreLockFile(lockFilePath, hadExistingLockFile, previousLockFileContents);
            }
        }

        private static string GetLockFilePath()
        {
            return Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Temp", "serverstarting.lock"));
        }

        private static void RestoreLockFile(
            string lockFilePath,
            bool hadExistingLockFile,
            string previousLockFileContents)
        {
            if (hadExistingLockFile)
            {
                File.WriteAllText(lockFilePath, previousLockFileContents);
                return;
            }

            if (File.Exists(lockFilePath))
            {
                File.Delete(lockFilePath);
            }
        }
    }
}
