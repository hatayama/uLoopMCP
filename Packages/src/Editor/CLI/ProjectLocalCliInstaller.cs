using System.Diagnostics;
using System.IO;

using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Places the package-owned TypeScript CLI bundle into each Unity project so the global npm command can stay a dispatcher.
    /// </summary>
    public static class ProjectLocalCliInstaller
    {
        private const int CHMOD_TIMEOUT_MS = 5000;

        public static CliInstallResult InstallProjectLocalCli(string projectRoot)
        {
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty");

            string sourceBundlePath = GetProjectCliBundlePath();
            return InstallProjectLocalCliFromBundle(sourceBundlePath, projectRoot);
        }

        internal static CliInstallResult InstallProjectLocalCliFromBundle(
            string sourceBundlePath,
            string projectRoot)
        {
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(sourceBundlePath), "sourceBundlePath must not be null or empty");
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty");

            if (!File.Exists(sourceBundlePath))
            {
                return new CliInstallResult(false, $"Project-local CLI bundle was not found: {sourceBundlePath}");
            }

            string projectLocalBinDir = GetProjectLocalBinDir(projectRoot);
            Directory.CreateDirectory(projectLocalBinDir);

            string projectLocalCliPath = GetProjectLocalCliPath(projectRoot);
            File.Copy(sourceBundlePath, projectLocalCliPath, overwrite: true);
            File.WriteAllText(
                GetProjectLocalWindowsCommandPath(projectRoot),
                BuildWindowsCommandContent());

            return MakeProjectLocalCliExecutable(projectLocalCliPath);
        }

        internal static string GetProjectCliBundlePath()
        {
            return Path.Combine(
                McpConstants.PackageResolvedPath,
                CliConstants.CLI_PACKAGE_DIR_NAME,
                CliConstants.DIST_DIR_NAME,
                CliConstants.PROJECT_CLI_BUNDLE_FILE_NAME);
        }

        internal static string GetProjectLocalCliPath(string projectRoot)
        {
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty");

            return Path.Combine(
                GetProjectLocalBinDir(projectRoot),
                CliConstants.PROJECT_LOCAL_EXECUTABLE_NAME);
        }

        internal static string GetProjectLocalWindowsCommandPath(string projectRoot)
        {
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty");

            return Path.Combine(
                GetProjectLocalBinDir(projectRoot),
                CliConstants.PROJECT_LOCAL_WINDOWS_COMMAND_NAME);
        }

        private static string GetProjectLocalBinDir(string projectRoot)
        {
            return Path.Combine(
                projectRoot,
                McpConstants.ULOOP_DIR,
                CliConstants.PROJECT_LOCAL_BIN_DIR_NAME);
        }

        private static string BuildWindowsCommandContent()
        {
            return "@echo off\r\nnode \"%~dp0\\uloop\" %*\r\n";
        }

        private static CliInstallResult MakeProjectLocalCliExecutable(string projectLocalCliPath)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return new CliInstallResult(true, "");
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "/bin/chmod",
                Arguments = $"+x {QuoteProcessArgument(projectLocalCliPath)}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Process process = ProcessStartHelper.TryStart(startInfo);
            if (process == null)
            {
                return new CliInstallResult(false, "Failed to start chmod process");
            }

            bool exited = process.WaitForExit(CHMOD_TIMEOUT_MS);
            if (!exited)
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }

                process.Dispose();
                return new CliInstallResult(false, "Making project-local CLI executable timed out");
            }

            process.WaitForExit();
            string errorOutput = process.StandardError.ReadToEnd();
            bool success = process.ExitCode == 0;
            process.Dispose();

            return new CliInstallResult(success, errorOutput);
        }

        private static string QuoteProcessArgument(string value)
        {
            UnityEngine.Debug.Assert(value != null, "value must not be null");
            return $"\"{value.Replace("\"", "\\\"")}\"";
        }
    }
}
