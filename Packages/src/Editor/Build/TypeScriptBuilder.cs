using UnityEngine;
using System.Collections.Generic;
using System.IO;
using SystemDiagnostics = System.Diagnostics;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Class responsible for TypeScript server build processing
    /// </summary>
    public class TypeScriptBuilder
    {
        // npm command constants
        private const string NPM_COMMAND_CI = "ci";
        private const string NPM_COMMAND_BUILD_BUNDLE = "run build:bundle";
        private const string NPM_COMMAND_INSTALL = "install";
        
        // Common Node.js paths
        private static readonly string[] COMMON_NODE_PATHS = {
            "/usr/local/bin",
            "/opt/homebrew/bin",
            "/usr/bin"
        };
        
        // Possible npm paths
        private static readonly string[] POSSIBLE_NPM_PATHS = {
            "/usr/local/bin/npm", // When installed via Homebrew
            "/opt/homebrew/bin/npm", // Homebrew on Apple Silicon Mac
            "/usr/bin/npm" // System installation
        };
        
        /// <summary>
        /// Callback for build completion
        /// </summary>
        /// <param name="success">Whether the build was successful</param>
        /// <param name="output">Build output</param>
        /// <param name="error">Error output</param>
        public delegate void BuildCompleteCallback(bool success, string output, string error);

        /// <summary>
        /// Build the TypeScript server
        /// </summary>
        /// <param name="onComplete">Callback for build completion</param>
        public void BuildTypeScriptServer(BuildCompleteCallback onComplete = null)
        {
            string packageBasePath = UnityMcpPathResolver.GetPackageBasePath();
            if (string.IsNullOrEmpty(packageBasePath))
            {
                Debug.LogError("Package base path not found. Cannot build TypeScript server.");
                onComplete?.Invoke(false, "", "Package base path not found. Cannot build TypeScript server.");
                return;
            }
            
            string typeScriptDir = Path.Combine(packageBasePath, McpConstants.TYPESCRIPT_SERVER_DIR);
            if (!Directory.Exists(typeScriptDir))
            {
                Debug.LogError($"TypeScript directory not found: {typeScriptDir}");
                onComplete?.Invoke(false, "", $"TypeScript directory not found: {typeScriptDir}");
                return;
            }
            
            // Get npm path
            string npmPath = GetNpmPath();
            if (string.IsNullOrEmpty(npmPath))
            {
                Debug.LogError("npm command not found. Please make sure Node.js and npm are installed.");
                onComplete?.Invoke(false, "", "npm command not found");
                return;
            }
            
            Debug.Log($"Building TypeScript server in: {typeScriptDir}");
            Debug.Log($"Using npm at: {npmPath}");
            
            // Run npm ci (strict installation from package-lock.json)
            RunCommand(npmPath, NPM_COMMAND_CI, typeScriptDir);
            
            // Run bundle build with esbuild
            RunCommand(npmPath, NPM_COMMAND_BUILD_BUNDLE, typeScriptDir);
            
            Debug.Log("TypeScript server build completed.");
            onComplete?.Invoke(true, "", "TypeScript server build completed.");
        }

        /// <summary>
        /// Execute npm install
        /// </summary>
        /// <param name="npmPath">Path to npm</param>
        /// <param name="workingDirectory">Working directory</param>
        /// <returns>Whether it was successful</returns>
        private bool RunNpmInstall(string npmPath, string workingDirectory)
        {
            Debug.Log("Running npm install...");
            
            SystemDiagnostics.ProcessStartInfo startInfo = new SystemDiagnostics.ProcessStartInfo
            {
                FileName = npmPath,
                Arguments = NPM_COMMAND_INSTALL,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            // Set PATH environment variable
            SetupEnvironmentPath(startInfo, npmPath);
            
            try
            {
                using (SystemDiagnostics.Process process = SystemDiagnostics.Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        Debug.LogError("Failed to start npm install process");
                        return false;
                    }
                    
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    
                    process.WaitForExit();
                    
                    if (process.ExitCode == 0)
                    {
                        Debug.Log("npm install completed successfully!");
                        if (!string.IsNullOrEmpty(output))
                        {
                            Debug.Log($"Install output:\n{output}");
                        }
                        return true;
                    }
                    else
                    {
                        Debug.LogError($"npm install failed with exit code {process.ExitCode}");
                        if (!string.IsNullOrEmpty(error))
                        {
                            Debug.LogError($"Install error:\n{error}");
                        }
                        if (!string.IsNullOrEmpty(output))
                        {
                            Debug.LogError($"Install output:\n{output}");
                        }
                        return false;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to start npm install process: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get npm path (macOS compatible)
        /// </summary>
        private string GetNpmPath()
        {
            Debug.Log("Searching for npm command...");
            
            // First, search for npm using which command
            try
            {
                SystemDiagnostics.ProcessStartInfo whichInfo = new SystemDiagnostics.ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "npm",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using (SystemDiagnostics.Process whichProcess = SystemDiagnostics.Process.Start(whichInfo))
                {
                    if (whichProcess == null)
                    {
                        Debug.LogError("Failed to start 'which npm' process");
                        return string.Empty;
                    }
                    
                    string whichOutput = whichProcess.StandardOutput.ReadToEnd().Trim();
                    string whichError = whichProcess.StandardError.ReadToEnd().Trim();
                    whichProcess.WaitForExit();
                    
                    Debug.Log($"which npm output: '{whichOutput}', error: '{whichError}', exit code: {whichProcess.ExitCode}");
                    
                    if (whichProcess.ExitCode == 0 && !string.IsNullOrEmpty(whichOutput))
                    {
                        Debug.Log($"Found npm via which command: {whichOutput}");
                        return whichOutput;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to run which command for npm detection: {ex.Message}");
                // This is expected to fail on some systems, so we continue with fallback detection
            }
            
            // If not found with which, try common paths
            List<string> possiblePaths = new List<string>(POSSIBLE_NPM_PATHS);
            possiblePaths.Add(Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".nvm/versions/node/*/bin/npm")); // nvm
            
            foreach (string path in possiblePaths)
            {
                if (path.Contains("*"))
                {
                    // For nvm paths, expand and search
                    string nvmBasePath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".nvm/versions/node");
                    if (Directory.Exists(nvmBasePath))
                    {
                        string[] nodeDirs = Directory.GetDirectories(nvmBasePath);
                        foreach (string nodeDir in nodeDirs)
                        {
                            string nvmNpmPath = Path.Combine(nodeDir, "bin", "npm");
                            if (File.Exists(nvmNpmPath))
                            {
                                return nvmNpmPath;
                            }
                        }
                    }
                }
                else
                {
                    Debug.Log($"Checking npm path: {path}");
                    // Check path directly
                    if (File.Exists(path))
                    {
                        Debug.Log($"Found npm at: {path}");
                        return path;
                    }
                }
            }
            
            Debug.LogError("npm command not found in any of the expected locations");
            return null;
        }

        /// <summary>
        /// Set PATH environment variable (so node command can be found)
        /// </summary>
        private void SetupEnvironmentPath(SystemDiagnostics.ProcessStartInfo startInfo, string npmPath)
        {
            // Infer node path from npm directory
            string npmDir = Path.GetDirectoryName(npmPath);
            
            // Get current PATH environment variable
            string currentPath = System.Environment.GetEnvironmentVariable("PATH") ?? "";
            
            // List of paths to add
            List<string> additionalPaths = new List<string>();
            
            // Add same directory as npm
            if (!string.IsNullOrEmpty(npmDir))
            {
                additionalPaths.Add(npmDir);
            }
            
            // Add common Node.js paths
            
            foreach (string path in COMMON_NODE_PATHS)
            {
                if (Directory.Exists(path) && !additionalPaths.Contains(path))
                {
                    additionalPaths.Add(path);
                }
            }
            
            // Also add nvm paths
            string nvmBasePath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".nvm/versions/node");
            if (Directory.Exists(nvmBasePath))
            {
                string[] nodeDirs = Directory.GetDirectories(nvmBasePath);
                foreach (string nodeDir in nodeDirs)
                {
                    string binPath = Path.Combine(nodeDir, "bin");
                    if (Directory.Exists(binPath) && !additionalPaths.Contains(binPath))
                    {
                        additionalPaths.Add(binPath);
                    }
                }
            }
            
            // Build new PATH
            string newPath = string.Join(":", additionalPaths) + ":" + currentPath;
            
            // Set environment variables
            startInfo.EnvironmentVariables["PATH"] = newPath;
            
            Debug.Log($"Updated PATH for npm process: {newPath}");
        }

        private void RunCommand(string command, string arguments, string workingDirectory)
        {
            // Security: Validate command and arguments
            if (string.IsNullOrEmpty(command) || string.IsNullOrEmpty(arguments) || string.IsNullOrEmpty(workingDirectory))
            {
                throw new System.ArgumentException("Command, arguments, and working directory cannot be null or empty");
            }
            
            // Security: Only allow predefined npm commands
            if (!IsValidNpmCommand(arguments))
            {
                throw new System.ArgumentException($"Invalid npm command: {arguments}. Only predefined commands are allowed.");
            }
            
            // Security: Validate working directory path
            if (!IsValidWorkingDirectory(workingDirectory))
            {
                throw new System.ArgumentException($"Invalid working directory: {workingDirectory}");
            }
            
            Debug.Log($"Running command: {command} {arguments} in directory: {workingDirectory}");
            
            SystemDiagnostics.ProcessStartInfo startInfo = new SystemDiagnostics.ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            // Set PATH environment variable
            SetupEnvironmentPath(startInfo, GetNpmPath());
            
            try
            {
                using (SystemDiagnostics.Process process = SystemDiagnostics.Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        Debug.LogError($"Failed to start {command} {arguments} process");
                        return;
                    }
                    
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    
                    process.WaitForExit();
                    
                    bool success = process.ExitCode == 0;
                    
                    if (success)
                    {
                        Debug.Log($"{command} {arguments} completed successfully!");
                        if (!string.IsNullOrEmpty(output))
                        {
                            Debug.Log($"Output:\n{output}");
                        }
                    }
                    else
                    {
                        Debug.LogError($"{command} {arguments} failed with exit code {process.ExitCode}");
                        if (!string.IsNullOrEmpty(error))
                        {
                            Debug.LogError($"Error:\n{error}");
                        }
                        if (!string.IsNullOrEmpty(output))
                        {
                            Debug.LogError($"Output:\n{output}");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                string errorMsg = $"Failed to run command: {command} {arguments}: {ex.Message}";
                Debug.LogError(errorMsg);
                Debug.LogError("Make sure npm is installed and available in PATH");
                
                // This is a critical failure for TypeScript server building
                throw new System.InvalidOperationException(
                    $"TypeScript build command failed: {command} {arguments}. " +
                    "Ensure Node.js and npm are properly installed and accessible from Unity Editor.", ex);
            }
        }
        
        /// <summary>
        /// Security: Validate npm command arguments
        /// </summary>
        private bool IsValidNpmCommand(string arguments)
        {
            // Only allow predefined npm commands
            return arguments == NPM_COMMAND_CI || 
                   arguments == NPM_COMMAND_BUILD_BUNDLE || 
                   arguments == NPM_COMMAND_INSTALL;
        }
        
        /// <summary>
        /// Security: Validate working directory path
        /// </summary>
        private bool IsValidWorkingDirectory(string workingDirectory)
        {
            try
            {
                // Normalize the path to prevent path traversal attacks
                string normalizedPath = Path.GetFullPath(workingDirectory);
                
                // Check if the path exists
                if (!Directory.Exists(normalizedPath))
                {
                    return false;
                }
                
                // Ensure the path is within the package directory
                string packageBasePath = UnityMcpPathResolver.GetPackageBasePath();
                if (string.IsNullOrEmpty(packageBasePath))
                {
                    return false;
                }
                
                string normalizedPackagePath = Path.GetFullPath(packageBasePath);
                return normalizedPath.StartsWith(normalizedPackagePath, System.StringComparison.OrdinalIgnoreCase);
            }
            catch (System.Exception)
            {
                return false;
            }
        }
    }
} 