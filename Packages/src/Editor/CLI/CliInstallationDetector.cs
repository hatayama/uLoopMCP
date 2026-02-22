using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public static class CliInstallationDetector
    {
        private const int PROCESS_TIMEOUT_MS = 5000;

        private static string _cachedCliVersion;
        private static bool _cacheInitialized;
        private static bool _isRefreshing;

        public static bool IsCliInstalled()
        {
            return GetCachedCliVersion() != null;
        }

        public static string GetCachedCliVersion()
        {
            return _cacheInitialized ? _cachedCliVersion : null;
        }

        public static bool IsCheckCompleted()
        {
            return _cacheInitialized;
        }

        public static async Task RefreshCliVersionAsync(CancellationToken ct)
        {
            if (_cacheInitialized || _isRefreshing)
            {
                return;
            }

            _isRefreshing = true;
            try
            {
                string version = await DetectCliVersionAsync(ct);
                _cachedCliVersion = version;
                _cacheInitialized = true;
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        public static bool AreSkillsInstalled(string target)
        {
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(target), "target must not be null or empty");

            string projectRoot = UnityMcpPathResolver.GetProjectRoot();
            string skillsDir = Path.Combine(projectRoot, $".{target}", "skills");

            if (!Directory.Exists(skillsDir))
            {
                return false;
            }

            string[] dirs = Directory.GetDirectories(skillsDir, "uloop-*");
            foreach (string dir in dirs)
            {
                string skillFile = Path.Combine(dir, "SKILL.md");
                if (File.Exists(skillFile))
                {
                    return true;
                }
            }

            return false;
        }

        public static async Task ForceRefreshCliVersionAsync(CancellationToken ct)
        {
            string version = await DetectCliVersionAsync(ct);
            _cachedCliVersion = version;
            _cacheInitialized = true;
        }

        public static void InvalidateCache()
        {
            _cachedCliVersion = null;
            _cacheInitialized = false;
            _isRefreshing = false;
        }

        private static Task<string> DetectCliVersionAsync(CancellationToken ct)
        {
            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();

            string executablePath = NodeEnvironmentResolver.FindExecutablePath(CliConstants.EXECUTABLE_NAME);
            // FindExecutablePath resolves .cmd shims on Windows via 'where' command
            string fileName = executablePath ?? CliConstants.EXECUTABLE_NAME;

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = CliConstants.VERSION_FLAG,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            NodeEnvironmentResolver.SetupEnvironmentPath(startInfo, NodeEnvironmentResolver.FindNodePath());

            Process process;
            try
            {
                process = Process.Start(startInfo);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // OS reports "command not found" as Win32Exception when executable is absent
                tcs.SetResult(null);
                return tcs.Task;
            }

            if (process == null)
            {
                tcs.SetResult(null);
                return tcs.Task;
            }

            StringBuilder outputBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.Append(e.Data);
                }
            };
            process.ErrorDataReceived += (sender, e) => { };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            Task.Run(() =>
            {
                bool exited = process.WaitForExit(PROCESS_TIMEOUT_MS);

                if (!exited)
                {
                    process.Kill();
                    process.Dispose();
                    tcs.TrySetResult(null);
                    return;
                }

                // Parameterless WaitForExit flushes async output buffers
                process.WaitForExit();

                string output = outputBuilder.ToString().Trim();
                bool failed = process.ExitCode != 0 || string.IsNullOrEmpty(output);
                process.Dispose();

                tcs.TrySetResult(failed ? null : output);
            }, ct);

            return tcs.Task;
        }
    }

    internal static class EditorTimestamp
    {
        public static double Now()
        {
            return UnityEditor.EditorApplication.timeSinceStartup;
        }
    }
}
