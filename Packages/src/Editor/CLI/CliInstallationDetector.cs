using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public static class CliInstallationDetector
    {
        private const int PROCESS_TIMEOUT_MS = 5000;
        private const double CACHE_TTL_SECONDS = 5.0;

        // Empty string = checked but not installed, non-empty = version, null = not yet checked
        private static string _cachedCliVersion;
        private static bool _cacheInitialized;
        private static double _cliCacheTime;

        public static bool IsCliInstalled()
        {
            string version = GetCliVersion();
            return version != null;
        }

        public static string GetCliVersion()
        {
            double now = EditorTimestamp.Now();
            if (_cacheInitialized && (now - _cliCacheTime) < CACHE_TTL_SECONDS)
            {
                return _cachedCliVersion;
            }

            _cachedCliVersion = DetectCliVersion();
            _cacheInitialized = true;
            _cliCacheTime = now;
            return _cachedCliVersion;
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

        public static void InvalidateCache()
        {
            _cachedCliVersion = null;
            _cacheInitialized = false;
        }

        private static string DetectCliVersion()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = CliConstants.EXECUTABLE_NAME,
                Arguments = CliConstants.VERSION_FLAG,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            NodeEnvironmentResolver.SetupEnvironmentPath(startInfo, NodeEnvironmentResolver.FindNodePath());

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    return null;
                }

                string output = process.StandardOutput.ReadToEnd().Trim();
                process.StandardError.ReadToEnd();

                if (!process.WaitForExit(PROCESS_TIMEOUT_MS))
                {
                    process.Kill();
                    return null;
                }

                if (process.ExitCode != 0 || string.IsNullOrEmpty(output))
                {
                    return null;
                }

                return output;
            }
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
