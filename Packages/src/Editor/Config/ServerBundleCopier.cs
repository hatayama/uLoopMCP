using System.IO;
using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Copies server.bundle.js to a fixed location in Library folder.
    /// This prevents mcp.json from needing updates when uLoopMCP package version changes.
    /// </summary>
    [InitializeOnLoad]
    public static class ServerBundleCopier
    {
        private const int FileCompareBufferSize = 81920;

        static ServerBundleCopier()
        {
            InitializeOnLoadTiming.Measure(
                "ServerBundleCopier",
                EnsureServerBundleCopied);
        }

        /// <summary>
        /// Ensures server.bundle.js is copied to the fixed Library path.
        /// Called automatically on Unity startup and can be called manually if needed.
        /// </summary>
        public static void EnsureServerBundleCopied()
        {
            string sourcePath = GetSourceServerBundlePath();
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                return;
            }

            string destPath = GetFixedServerBundlePath();
            CopyServerBundleWhenChanged(sourcePath, destPath);
        }

        internal static bool CopyServerBundleWhenChanged(string sourcePath, string destinationPath)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(sourcePath), "sourcePath must not be empty");
            Debug.Assert(!string.IsNullOrWhiteSpace(destinationPath), "destinationPath must not be empty");
            Debug.Assert(File.Exists(sourcePath), "sourcePath must exist");

            string destDir = Path.GetDirectoryName(destinationPath);

            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            if (!ShouldCopyServerBundle(sourcePath, destinationPath))
            {
                return false;
            }

            File.Copy(sourcePath, destinationPath, overwrite: true);
            File.SetLastWriteTimeUtc(destinationPath, File.GetLastWriteTimeUtc(sourcePath));
            return true;
        }

        private static bool ShouldCopyServerBundle(string sourcePath, string destinationPath)
        {
            if (!File.Exists(destinationPath))
            {
                return true;
            }

            FileInfo sourceInfo = new FileInfo(sourcePath);
            FileInfo destinationInfo = new FileInfo(destinationPath);
            if (sourceInfo.Length != destinationInfo.Length)
            {
                return true;
            }

            if (sourceInfo.LastWriteTimeUtc == destinationInfo.LastWriteTimeUtc)
            {
                return false;
            }

            if (!HasSameContent(sourcePath, destinationPath))
            {
                return true;
            }

            File.SetLastWriteTimeUtc(destinationPath, sourceInfo.LastWriteTimeUtc);
            return false;
        }

        private static bool HasSameContent(string sourcePath, string destinationPath)
        {
            using (FileStream sourceStream = File.OpenRead(sourcePath))
            using (FileStream destinationStream = File.OpenRead(destinationPath))
            {
                byte[] sourceBuffer = new byte[FileCompareBufferSize];
                byte[] destinationBuffer = new byte[FileCompareBufferSize];

                while (true)
                {
                    int sourceRead = sourceStream.Read(sourceBuffer, 0, sourceBuffer.Length);
                    int destinationRead = destinationStream.Read(destinationBuffer, 0, destinationBuffer.Length);

                    if (sourceRead != destinationRead)
                    {
                        return false;
                    }

                    if (sourceRead == 0)
                    {
                        return true;
                    }

                    for (int i = 0; i < sourceRead; i++)
                    {
                        if (sourceBuffer[i] != destinationBuffer[i])
                        {
                            return false;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the fixed server bundle path in Library folder.
        /// </summary>
        public static string GetFixedServerBundlePath()
        {
            string projectRoot = UnityMcpPathResolver.GetProjectRoot();
            return Path.Combine(
                projectRoot,
                McpConstants.LIBRARY_DIR,
                McpConstants.ULOOPMCP_DIR,
                McpConstants.SERVER_BUNDLE_FILE
            );
        }

        /// <summary>
        /// Gets the source path of server.bundle.js from local development, submodule, or PackageCache.
        /// </summary>
        private static string GetSourceServerBundlePath()
        {
            string projectRoot = UnityMcpPathResolver.GetProjectRoot();

            // 1. Check local development path (Packages/src)
            string localPath = Path.Combine(
                projectRoot,
                McpConstants.PACKAGES_DIR,
                McpConstants.SRC_DIR,
                McpConstants.TYPESCRIPT_SERVER_DIR,
                McpConstants.DIST_DIR,
                McpConstants.SERVER_BUNDLE_FILE
            );
            if (File.Exists(localPath))
            {
                return localPath;
            }

            // 2. Check package path via Unity Package Manager API (supports submodules)
            string packagePath = McpConstants.PackageResolvedPath;
            if (!string.IsNullOrEmpty(packagePath))
            {
                string serverPath = Path.Combine(
                    packagePath,
                    McpConstants.TYPESCRIPT_SERVER_DIR,
                    McpConstants.DIST_DIR,
                    McpConstants.SERVER_BUNDLE_FILE
                );
                if (File.Exists(serverPath))
                {
                    return serverPath;
                }
            }

            // 3. Search in PackageCache
            string packageCacheDir = Path.Combine(
                projectRoot,
                McpConstants.LIBRARY_DIR,
                McpConstants.PACKAGE_CACHE_DIR
            );
            if (Directory.Exists(packageCacheDir))
            {
                string[] packageDirs = Directory.GetDirectories(packageCacheDir, McpConstants.PackageNamePattern);
                foreach (string packageDir in packageDirs)
                {
                    string serverPath = Path.Combine(
                        packageDir,
                        McpConstants.TYPESCRIPT_SERVER_DIR,
                        McpConstants.DIST_DIR,
                        McpConstants.SERVER_BUNDLE_FILE
                    );
                    if (File.Exists(serverPath))
                    {
                        return serverPath;
                    }
                }
            }

            return null;
        }
    }
}
