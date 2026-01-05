using UnityEngine;
using UnityEditor;
using System.IO;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Utility for testing the path resolution feature.
    /// </summary>
    public static class PathResolverTest
    {
        [MenuItem("uLoopMCP/Tools/Path Resolver/Test TypeScript Server Path")]
        public static void TestTypeScriptServerPath()
        {
            
            string serverPath = UnityMcpPathResolver.GetTypeScriptServerPath();
            
            if (File.Exists(serverPath))
            {
                
                // Also display the file size
                FileInfo fileInfo = new FileInfo(serverPath);
            }
            else
            {
            }
            
            // Also display detailed information of the search target path
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            
            if (projectRoot == null)
            {
                return;
            }
            
            
            // Check local path
            string localPath = Path.Combine(projectRoot, McpConstants.PACKAGES_DIR, McpConstants.SRC_DIR, McpConstants.TYPESCRIPT_SERVER_DIR, McpConstants.DIST_DIR, McpConstants.SERVER_BUNDLE_FILE);
            
            // Check Package Cache
            string packageCacheDir = Path.Combine(projectRoot, McpConstants.LIBRARY_DIR, McpConstants.PACKAGE_CACHE_DIR);
            
            if (Directory.Exists(packageCacheDir))
            {
                string[] packageDirs = Directory.GetDirectories(packageCacheDir, McpConstants.PackageNamePattern);
                
                foreach (string packageDir in packageDirs)
                {
                    string serverPathInCache = Path.Combine(packageDir, McpConstants.TYPESCRIPT_SERVER_DIR, McpConstants.DIST_DIR, McpConstants.SERVER_BUNDLE_FILE);
                }
            }
            
        }
        
        [MenuItem("uLoopMCP/Tools/Path Resolver/Force Update MCP Config")]
        public static void ForceUpdateMcpConfig()
        {
            
            int port = 7400;
            McpConfigRepository repository = new();
            McpConfigService configService = new(repository, McpEditorType.Cursor);
            configService.AutoConfigure(port);
            
        }

        [MenuItem("uLoopMCP/Tools/Path Resolver/Test Path Resolver")]
        public static void TestPathResolver()
        {
            Debug.Log("=== Path Resolver Test ===");
            
            string serverPath = UnityMcpPathResolver.GetTypeScriptServerPath();
            Debug.Log($"TypeScript Server Path: {serverPath}");
            
            if (string.IsNullOrEmpty(serverPath))
            {
                Debug.LogError("TypeScript server path is empty!");
                return;
            }
            
            if (System.IO.File.Exists(serverPath))
            {
                Debug.Log("✓ TypeScript server file exists");
            }
            else
            {
                Debug.LogError("✗ TypeScript server file not found");
            }
            
            string packageBasePath = UnityMcpPathResolver.GetPackageBasePath();
            Debug.Log($"Package Base Path: {packageBasePath}");
            
            string configPath = UnityMcpPathResolver.GetMcpConfigPath();
            Debug.Log($"MCP Config Path: {configPath}");
        }
    }
} 