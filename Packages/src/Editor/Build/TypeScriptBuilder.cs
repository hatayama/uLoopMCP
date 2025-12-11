using UnityEngine;
using System.IO;
using SystemDiagnostics = System.Diagnostics;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Class responsible for TypeScript server build processing
    /// </summary>
    public class TypeScriptBuilder
    {
        private const string NPM_COMMAND_CI = "ci";
        private const string NPM_COMMAND_BUILD_BUNDLE = "run build:bundle";
        private const string NPM_COMMAND_INSTALL = "install";
        
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
        /// Get npm path using NodeEnvironmentResolver
        /// </summary>
        private string GetNpmPath()
        {
            Debug.Log("Searching for npm command...");

            string npmPath = NodeEnvironmentResolver.FindNpmPath();
            if (!string.IsNullOrEmpty(npmPath))
            {
                Debug.Log($"Found npm at: {npmPath}");
                return npmPath;
            }

            Debug.LogError("npm command not found in any of the expected locations");
            return null;
        }

        /// <summary>
        /// Set PATH environment variable (so node command can be found)
        /// </summary>
        private void SetupEnvironmentPath(SystemDiagnostics.ProcessStartInfo startInfo, string npmPath)
        {
            NodeEnvironmentResolver.SetupEnvironmentPath(startInfo, npmPath);
            Debug.Log($"Updated PATH for npm process");
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