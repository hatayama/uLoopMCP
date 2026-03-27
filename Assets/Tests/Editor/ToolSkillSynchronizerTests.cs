using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP
{
    [TestFixture]
    public class ToolSkillSynchronizerTests
    {
        private string _projectRoot;
        private string[] _nonExistentDirsBefore;

        [SetUp]
        public void SetUp()
        {
            _projectRoot = UnityMcpPathResolver.GetProjectRoot();

            _nonExistentDirsBefore = ToolSkillSynchronizer.SkillTargetDirs
                .Where(dir => !Directory.Exists(Path.Combine(_projectRoot, dir)))
                .ToArray();
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up any directories that were created during the test
            foreach (string dir in _nonExistentDirsBefore)
            {
                string fullPath = Path.Combine(_projectRoot, dir);
                if (Directory.Exists(fullPath))
                {
                    // Only delete if it was created by this test (didn't exist before)
                    string skillsPath = Path.Combine(fullPath, "skills");
                    if (Directory.Exists(skillsPath))
                    {
                        Directory.Delete(skillsPath, true);
                    }
                    // Remove the parent dir only if it's now empty
                    if (Directory.Exists(fullPath) && !Directory.EnumerateFileSystemEntries(fullPath).Any())
                    {
                        Directory.Delete(fullPath);
                    }
                }
            }
        }

        [Test]
        public async Task InstallSkillFiles_DoesNotCreateNonExistentTargetDirectories()
        {
            // Arrange: record which target directories don't exist
            UnityEngine.Debug.Assert(_nonExistentDirsBefore.Length > 0,
                "At least one target directory should not exist for this test to be meaningful");

            // Act
            await ToolSkillSynchronizer.InstallSkillFiles();

            // Assert: directories that didn't exist before should still not exist
            foreach (string dir in _nonExistentDirsBefore)
            {
                string fullPath = Path.Combine(_projectRoot, dir);
                Assert.IsFalse(Directory.Exists(fullPath),
                    $"Directory '{dir}' should not be created by InstallSkillFiles when '{dir}' did not exist");
            }
        }

        [Test]
        public void RemoveSkillFiles_DoesNotCreateNonExistentTargetDirectories()
        {
            // Arrange
            string testToolName = "compile";

            // Act
            ToolSkillSynchronizer.RemoveSkillFiles(testToolName);

            // Assert: directories that didn't exist before should still not exist
            foreach (string dir in _nonExistentDirsBefore)
            {
                string fullPath = Path.Combine(_projectRoot, dir);
                Assert.IsFalse(Directory.Exists(fullPath),
                    $"Directory '{dir}' should not be created by RemoveSkillFiles");
            }
        }

        [Test]
        public void IsSkillInstalled_DoesNotCreateNonExistentTargetDirectories()
        {
            // Arrange
            string testToolName = "compile";

            // Act
            ToolSkillSynchronizer.IsSkillInstalled(testToolName);

            // Assert: directories that didn't exist before should still not exist
            foreach (string dir in _nonExistentDirsBefore)
            {
                string fullPath = Path.Combine(_projectRoot, dir);
                Assert.IsFalse(Directory.Exists(fullPath),
                    $"Directory '{dir}' should not be created by IsSkillInstalled");
            }
        }
    }
}
