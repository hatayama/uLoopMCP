using System;
using System.IO;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.Tests.Editor
{
    public class ServerBundleCopierTests
    {
        private string _tempDirectory;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }

        [Test]
        public void CopyServerBundleWhenChanged_WhenSizeAndTimestampMatch_SkipsCopy()
        {
            string sourcePath = Path.Combine(_tempDirectory, "source.bundle.js");
            string destinationPath = Path.Combine(_tempDirectory, "Library", "server.bundle.js");
            DateTime sourceTimestamp = new DateTime(2026, 4, 24, 1, 2, 3, DateTimeKind.Utc);

            File.WriteAllText(sourcePath, "new bundle");
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
            File.WriteAllText(destinationPath, "old bundle");
            File.SetLastWriteTimeUtc(sourcePath, sourceTimestamp);
            File.SetLastWriteTimeUtc(destinationPath, sourceTimestamp);

            bool copied = ServerBundleCopier.CopyServerBundleWhenChanged(sourcePath, destinationPath);

            Assert.That(copied, Is.False);
            Assert.That(File.ReadAllText(destinationPath), Is.EqualTo("old bundle"));
            Assert.That(File.GetLastWriteTimeUtc(destinationPath), Is.EqualTo(sourceTimestamp));
        }

        [Test]
        public void CopyServerBundleWhenChanged_WhenTimestampDiffersWithSameLength_CopiesSource()
        {
            string sourcePath = Path.Combine(_tempDirectory, "source.bundle.js");
            string destinationPath = Path.Combine(_tempDirectory, "Library", "server.bundle.js");
            DateTime sourceTimestamp = new DateTime(2026, 4, 24, 1, 2, 3, DateTimeKind.Utc);

            File.WriteAllText(sourcePath, "new bundle");
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
            File.WriteAllText(destinationPath, "old bundle");
            File.SetLastWriteTimeUtc(sourcePath, sourceTimestamp);
            File.SetLastWriteTimeUtc(destinationPath, new DateTime(2026, 4, 24, 4, 5, 6, DateTimeKind.Utc));

            bool copied = ServerBundleCopier.CopyServerBundleWhenChanged(sourcePath, destinationPath);

            Assert.That(copied, Is.True);
            Assert.That(File.ReadAllText(destinationPath), Is.EqualTo("new bundle"));
            Assert.That(File.GetLastWriteTimeUtc(destinationPath), Is.EqualTo(sourceTimestamp));
        }

        [Test]
        public void CopyServerBundleWhenChanged_WhenSizeDiffers_CopiesSource()
        {
            string sourcePath = Path.Combine(_tempDirectory, "source.bundle.js");
            string destinationPath = Path.Combine(_tempDirectory, "Library", "server.bundle.js");

            File.WriteAllText(sourcePath, "new bundle");
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
            File.WriteAllText(destinationPath, "old");
            File.SetLastWriteTimeUtc(sourcePath, new DateTime(2026, 4, 24, 1, 2, 3, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(destinationPath, new DateTime(2026, 4, 24, 1, 2, 3, DateTimeKind.Utc));

            bool copied = ServerBundleCopier.CopyServerBundleWhenChanged(sourcePath, destinationPath);

            Assert.That(copied, Is.True);
            Assert.That(File.ReadAllText(destinationPath), Is.EqualTo("new bundle"));
        }

        [Test]
        public void CopyServerBundleWhenChanged_WhenDestinationMissing_CopiesSource()
        {
            string sourcePath = Path.Combine(_tempDirectory, "source.bundle.js");
            string destinationPath = Path.Combine(_tempDirectory, "Library", "server.bundle.js");

            File.WriteAllText(sourcePath, "new bundle");

            bool copied = ServerBundleCopier.CopyServerBundleWhenChanged(sourcePath, destinationPath);

            Assert.That(copied, Is.True);
            Assert.That(File.ReadAllText(destinationPath), Is.EqualTo("new bundle"));
        }
    }
}
