using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Application service responsible for managing Node.js TypeScript server process.
    /// Handles process lifecycle: startup, monitoring, and shutdown.
    ///
    /// Related classes:
    /// - McpServerStartupService: Coordinates with this service during server initialization
    /// - McpBridgeServer: TCP server that Node.js connects to
    /// </summary>
    public class NodeProcessService : IDisposable
    {
        private Process _nodeProcess;
        private bool _disposed;

        /// <summary>
        /// Gets a value indicating whether the Node.js process is running.
        /// </summary>
        public bool IsRunning => _nodeProcess != null && !_nodeProcess.HasExited;

        /// <summary>
        /// Gets the process ID of the Node.js process, or -1 if not running.
        /// </summary>
        public int ProcessId => _nodeProcess?.Id ?? -1;

        /// <summary>
        /// Starts the Node.js TypeScript server process with environment variables.
        /// </summary>
        /// <param name="tcpPort">TCP port for Unity communication</param>
        /// <param name="httpPort">HTTP port for MCP client connections</param>
        /// <returns>ServiceResult indicating success or failure</returns>
        public ServiceResult<int> StartProcess(int tcpPort, int httpPort)
        {
            if (IsRunning)
            {
                return ServiceResult<int>.SuccessResult(_nodeProcess.Id);
            }

            string serverBundlePath = GetServerBundlePath();
            if (string.IsNullOrEmpty(serverBundlePath))
            {
                return ServiceResult<int>.FailureResult("Could not find server.bundle.js");
            }

            if (!File.Exists(serverBundlePath))
            {
                return ServiceResult<int>.FailureResult($"Server bundle not found at: {serverBundlePath}");
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = $"\"{serverBundlePath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false,
                WorkingDirectory = Path.GetDirectoryName(serverBundlePath)
            };

            // Set environment variables for the Node.js process
            startInfo.EnvironmentVariables["UNITY_TCP_PORT"] = tcpPort.ToString();
            startInfo.EnvironmentVariables["MCP_HTTP_PORT"] = httpPort.ToString();

            _nodeProcess = new Process { StartInfo = startInfo };

            // Subscribe to output/error events for logging
            _nodeProcess.OutputDataReceived += OnOutputDataReceived;
            _nodeProcess.ErrorDataReceived += OnErrorDataReceived;
            _nodeProcess.EnableRaisingEvents = true;
            _nodeProcess.Exited += OnProcessExited;

            bool started = _nodeProcess.Start();
            if (!started)
            {
                return ServiceResult<int>.FailureResult("Failed to start Node.js process");
            }

            _nodeProcess.BeginOutputReadLine();
            _nodeProcess.BeginErrorReadLine();

            VibeLogger.LogInfo("node_process_started",
                $"pid={_nodeProcess.Id} tcp_port={tcpPort} http_port={httpPort}");

            return ServiceResult<int>.SuccessResult(_nodeProcess.Id);
        }

        /// <summary>
        /// Stops the Node.js process gracefully.
        /// </summary>
        /// <returns>ServiceResult indicating success or failure</returns>
        public ServiceResult<bool> StopProcess()
        {
            if (!IsRunning)
            {
                return ServiceResult<bool>.SuccessResult(true);
            }

            int pid = _nodeProcess.Id;

            // First try graceful shutdown
            _nodeProcess.CloseMainWindow();

            // Wait briefly for graceful shutdown
            bool exited = _nodeProcess.WaitForExit(3000);
            if (!exited)
            {
                // Force kill if graceful shutdown failed
                _nodeProcess.Kill();
                _nodeProcess.WaitForExit(1000);
            }

            VibeLogger.LogInfo("node_process_stopped", $"pid={pid}");

            CleanupProcess();
            return ServiceResult<bool>.SuccessResult(true);
        }

        /// <summary>
        /// Gets the path to the server bundle JavaScript file.
        /// </summary>
        /// <returns>Full path to server.bundle.js, or null if not found</returns>
        private string GetServerBundlePath()
        {
            // Get the package path
            string packagePath = GetPackagePath();
            if (string.IsNullOrEmpty(packagePath))
            {
                return null;
            }

            string bundlePath = Path.Combine(packagePath, "src", "TypeScriptServer~", "dist", "server.bundle.js");
            return Path.GetFullPath(bundlePath);
        }

        /// <summary>
        /// Gets the root path of the uLoopMCP package.
        /// </summary>
        /// <returns>Package root path, or null if not found</returns>
        private string GetPackagePath()
        {
            // Try to find package via Unity's package manager
            string[] possiblePaths = new[]
            {
                // In project Packages folder
                Path.Combine(Application.dataPath, "..", "Packages", "io.github.hatayama.uloopmcp"),
                // Development: directly in Packages folder
                Path.Combine(Application.dataPath, "..", "Packages"),
                // Cached package location
                Path.Combine(Application.dataPath, "..", "Library", "PackageCache")
            };

            foreach (string basePath in possiblePaths)
            {
                string fullPath = Path.GetFullPath(basePath);
                if (Directory.Exists(fullPath))
                {
                    // Check if this is the package root or need to search
                    string bundleCheck = Path.Combine(fullPath, "src", "TypeScriptServer~", "dist", "server.bundle.js");
                    if (File.Exists(bundleCheck))
                    {
                        return fullPath;
                    }

                    // Search for package in subdirectories (for PackageCache)
                    if (fullPath.Contains("PackageCache"))
                    {
                        string[] dirs = Directory.GetDirectories(fullPath, "io.github.hatayama.uloopmcp*");
                        foreach (string dir in dirs)
                        {
                            bundleCheck = Path.Combine(dir, "src", "TypeScriptServer~", "dist", "server.bundle.js");
                            if (File.Exists(bundleCheck))
                            {
                                return dir;
                            }
                        }
                    }
                }
            }

            // Fallback: use script location to find package
            string scriptPath = GetScriptPath();
            if (!string.IsNullOrEmpty(scriptPath))
            {
                // Navigate up from Editor/Core/ApplicationServices to package root
                string packageRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(scriptPath), "..", "..", ".."));
                string bundleCheck = Path.Combine(packageRoot, "src", "TypeScriptServer~", "dist", "server.bundle.js");
                if (File.Exists(bundleCheck))
                {
                    return packageRoot;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the path of this script file using CallerFilePath.
        /// </summary>
        private string GetScriptPath([System.Runtime.CompilerServices.CallerFilePath] string filePath = "")
        {
            return filePath;
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                VibeLogger.LogInfo("node_stdout", e.Data);
            }
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                VibeLogger.LogWarning("node_stderr", e.Data);
            }
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            int exitCode = _nodeProcess?.ExitCode ?? -1;
            VibeLogger.LogInfo("node_process_exited", $"exit_code={exitCode}");
            CleanupProcess();
        }

        private void CleanupProcess()
        {
            if (_nodeProcess != null)
            {
                _nodeProcess.OutputDataReceived -= OnOutputDataReceived;
                _nodeProcess.ErrorDataReceived -= OnErrorDataReceived;
                _nodeProcess.Exited -= OnProcessExited;
                _nodeProcess.Dispose();
                _nodeProcess = null;
            }
        }

        /// <summary>
        /// Disposes resources used by this service.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            StopProcess();
            _disposed = true;
        }
    }
}
