using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public static class CliInstaller
    {
        /// <summary>
        /// Windows: npm global prefix often points to admin-only directories (e.g. C:\Program Files\nodejs).
        /// Non-Windows: always returns true.
        /// </summary>
        public static bool CheckWindowsPermissions(
            string npmPath,
            string installTarget,
            out string globalPrefix,
            out string manualCommand)
        {
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(npmPath), "npmPath must not be null or empty");
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(installTarget), "installTarget must not be null or empty");

            globalPrefix = null;
            manualCommand = null;

            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                return true;
            }

            globalPrefix = NpmInstallDiagnostics.GetGlobalPrefix(npmPath);
            if (string.IsNullOrEmpty(globalPrefix) || NpmInstallDiagnostics.IsGlobalPrefixWritable(globalPrefix))
            {
                return true;
            }

            manualCommand = $"npm install -g {installTarget}";
            return false;
        }

        public static async Task<CliInstallResult> InstallAsync(string npmPath, string installTarget, string nodePath)
        {
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(npmPath), "npmPath must not be null or empty");
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(installTarget), "installTarget must not be null or empty");
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = npmPath,
                Arguments = $"install -g {installTarget}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            if (!string.IsNullOrEmpty(nodePath))
            {
                NodeEnvironmentResolver.SetupEnvironmentPath(startInfo, nodePath);
            }

            bool success = false;
            string errorOutput = "";

            await Task.Run(() =>
            {
                Process process = ProcessStartHelper.TryStart(startInfo);
                if (process == null)
                {
                    errorOutput = "Failed to start npm process";
                    return;
                }

                StringBuilder errorBuilder = new StringBuilder();
                process.OutputDataReceived += (s, e) => { };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(CliConstants.NPM_INSTALL_TIMEOUT_MS))
                {
                    if (!process.HasExited) process.Kill();
                    process.Dispose();
                    errorOutput = $"Installation timed out after {CliConstants.NPM_INSTALL_TIMEOUT_MS / 1000} seconds";
                    return;
                }

                // Parameterless WaitForExit flushes async output buffers
                process.WaitForExit();
                errorOutput = errorBuilder.ToString();
                success = process.ExitCode == 0;
                process.Dispose();
            });

            CliInstallationDetector.InvalidateCache();

            return new CliInstallResult(success, errorOutput);
        }
    }
}
