using System.Diagnostics;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace io.github.hatayama.UnityCliLoop
{
    internal readonly struct CliInstallationDetection
    {
        public CliInstallationDetection(string version, string executablePath)
        {
            Version = version;
            ExecutablePath = executablePath;
        }

        public string Version { get; }
        public string ExecutablePath { get; }
    }

    /// <summary>
    /// Detects the installed CLI dispatcher and keeps the result in an instance-scoped editor cache.
    /// </summary>
    public sealed class CliInstallationDetector
    {
        private const int PROCESS_TIMEOUT_MS = 5000;

        private string _cachedCliVersion;
        private string _cachedCliExecutablePath;
        private bool _cacheInitialized;
        private bool _isRefreshing;

        public bool IsCliInstalled()
        {
            return GetCachedCliVersion() != null;
        }

        public string GetCachedCliVersion()
        {
            return _cacheInitialized ? _cachedCliVersion : null;
        }

        public string GetCachedCliExecutablePath()
        {
            return _cacheInitialized ? _cachedCliExecutablePath : null;
        }

        public bool IsCheckCompleted()
        {
            return _cacheInitialized;
        }

        public async Task RefreshCliVersionAsync(CancellationToken ct)
        {
            if (_cacheInitialized || _isRefreshing)
            {
                return;
            }

            _isRefreshing = true;
            try
            {
                CliInstallationDetection detection = await DetectCliInstallationAsync(ct);
                _cachedCliVersion = detection.Version;
                _cachedCliExecutablePath = detection.ExecutablePath;
                _cacheInitialized = true;
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        public async Task ForceRefreshCliVersionAsync(CancellationToken ct)
        {
            CliInstallationDetection detection = await DetectCliInstallationAsync(ct);
            _cachedCliVersion = detection.Version;
            _cachedCliExecutablePath = detection.ExecutablePath;
            _cacheInitialized = true;
        }

        public void InvalidateCache()
        {
            _cachedCliVersion = null;
            _cachedCliExecutablePath = null;
            _cacheInitialized = false;
            _isRefreshing = false;
        }

        private static Task<CliInstallationDetection> DetectCliInstallationAsync(CancellationToken ct)
        {
            RuntimePlatform platform = Application.platform;
            return Task.Run(() => DetectCliInstallationBlocking(platform, ct), ct);
        }

        internal static string DetectCliVersionBlocking(RuntimePlatform platform, CancellationToken ct)
        {
            return DetectCliInstallationBlocking(platform, ct).Version;
        }

        internal static CliInstallationDetection DetectCliInstallationBlocking(RuntimePlatform platform, CancellationToken ct)
        {
            string executablePath = NodeEnvironmentResolver.FindExecutablePathAtPlatform(
                CliConstants.EXECUTABLE_NAME,
                platform);
            // FindExecutablePath resolves .cmd shims on Windows via 'where' command
            string fileName = executablePath ?? CliConstants.EXECUTABLE_NAME;

            ProcessStartInfo startInfo = new()            {
                FileName = fileName,
                Arguments = CliConstants.VERSION_FLAG,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Process process = ProcessStartHelper.TryStart(startInfo);
            if (process == null)
            {
                return new CliInstallationDetection(null, executablePath);
            }

            StringBuilder outputBuilder = new();

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

            using CancellationTokenRegistration registration = ct.Register(() =>
            {
                try { process.Kill(); } catch (System.InvalidOperationException) { }
            });

            try
            {
                bool exited = process.WaitForExit(PROCESS_TIMEOUT_MS);

                if (!exited)
                {
                    try { process.Kill(); } catch (System.InvalidOperationException) { }
                    process.Dispose();
                    return new CliInstallationDetection(null, executablePath);
                }

                // Parameterless WaitForExit flushes async output buffers
                process.WaitForExit();

                string output = outputBuilder.ToString().Trim();
                bool failed = process.ExitCode != 0 || string.IsNullOrEmpty(output);
                process.Dispose();

                string version = failed ? null : output;
                return new CliInstallationDetection(version, executablePath);
            }
            catch (Exception ex)
            {
                process.Dispose();
                if (!ct.IsCancellationRequested)
                {
                    UnityEngine.Debug.LogWarning($"[UnityCliLoop] Failed to detect CLI version: {ex.Message}");
                }
                return new CliInstallationDetection(null, executablePath);
            }
        }
    }

    /// <summary>
    /// Checks whether project-local agent skill files have been installed for a target client.
    /// </summary>
    public sealed class SkillInstallationDetector
    {
        public bool AreSkillsInstalled(string targetDir)
        {
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(targetDir), "targetDir must not be null or empty");

            string projectRoot = UnityCliLoopPathResolver.GetProjectRoot();
            return AreSkillsInstalled(projectRoot, targetDir);
        }

        public bool AreSkillsInstalled(string targetDir, bool groupSkillsUnderUnityCliLoop)
        {
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(targetDir), "targetDir must not be null or empty");

            string projectRoot = UnityCliLoopPathResolver.GetProjectRoot();
            return AreSkillsInstalled(projectRoot, targetDir, groupSkillsUnderUnityCliLoop);
        }

        internal bool AreSkillsInstalled(string projectRoot, string targetDir)
        {
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty");
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(targetDir), "targetDir must not be null or empty");

            string targetRoot = Path.Combine(projectRoot, targetDir);
            return SkillInstallLayout.HasInstalledSkills(projectRoot, targetRoot);
        }

        internal bool AreSkillsInstalled(
            string projectRoot,
            string targetDir,
            bool groupSkillsUnderUnityCliLoop)
        {
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty");
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(targetDir), "targetDir must not be null or empty");

            string targetRoot = Path.Combine(projectRoot, targetDir);
            return SkillInstallLayout.HasInstalledSkills(projectRoot, targetRoot, groupSkillsUnderUnityCliLoop);
        }
    }

    /// <summary>
    /// Coordinates CLI setup workflows for editor UI without exposing installer details to presentation code.
    /// </summary>
    public sealed class CliSetupApplicationService
    {
        private readonly CliInstallationDetector _cliInstallationDetector;

        public CliSetupApplicationService(CliInstallationDetector cliInstallationDetector)
        {
            UnityEngine.Debug.Assert(cliInstallationDetector != null, "cliInstallationDetector must not be null");

            _cliInstallationDetector = cliInstallationDetector;
        }

        public bool IsCliCheckCompleted()
        {
            return _cliInstallationDetector.IsCheckCompleted();
        }

        public bool IsCliInstalled()
        {
            return _cliInstallationDetector.IsCliInstalled();
        }

        public string GetCachedCliVersion()
        {
            return _cliInstallationDetector.GetCachedCliVersion();
        }

        public string GetCachedCliExecutablePath()
        {
            return _cliInstallationDetector.GetCachedCliExecutablePath();
        }

        public Task RefreshCliVersionAsync(CancellationToken ct)
        {
            return _cliInstallationDetector.RefreshCliVersionAsync(ct);
        }

        public Task ForceRefreshCliVersionAsync(CancellationToken ct)
        {
            return _cliInstallationDetector.ForceRefreshCliVersionAsync(ct);
        }

        public void InvalidateCliCache()
        {
            _cliInstallationDetector.InvalidateCache();
        }

        public string GetRequiredDispatcherVersion(string packageVersion)
        {
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(packageVersion), "packageVersion must not be null or empty");

            string requiredDispatcherVersion = ProjectLocalCliInstaller.DetectBundledRequiredDispatcherVersion();
            return string.IsNullOrEmpty(requiredDispatcherVersion)
                ? packageVersion
                : requiredDispatcherVersion;
        }

        public CliInstallResult EnsureProjectLocalCliCurrent(string projectRoot, string packageVersion)
        {
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty");
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(packageVersion), "packageVersion must not be null or empty");

            return ProjectLocalCliAutoInstaller.EnsureProjectLocalCliCurrent(projectRoot, packageVersion);
        }

        public bool IsPackageOwnedCurrentUserInstallPath(
            string cliExecutablePath,
            RuntimePlatform platform)
        {
            return NativeCliInstaller.IsPackageOwnedCurrentUserInstallPath(cliExecutablePath, platform);
        }

        public bool IsCliVersionLessThan(string leftVersion, string rightVersion)
        {
            return CliVersionComparer.IsVersionLessThan(leftVersion, rightVersion);
        }

        public bool IsCliVersionGreaterThanOrEqual(string leftVersion, string rightVersion)
        {
            return CliVersionComparer.IsVersionGreaterThanOrEqual(leftVersion, rightVersion);
        }

        public Task<CliInstallResult> InstallGlobalCliAsync(
            RuntimePlatform platform,
            string packageVersion,
            CancellationToken ct)
        {
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(packageVersion), "packageVersion must not be null or empty");
            ct.ThrowIfCancellationRequested();

            return NativeCliInstaller.InstallAsync(platform, packageVersion);
        }

        public Task<CliInstallResult> UninstallGlobalCliAsync(RuntimePlatform platform, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            return NativeCliInstaller.UninstallAsync(platform);
        }

        public NativeCliInstallCommand GetGlobalCliInstallCommand(
            RuntimePlatform platform,
            string packageVersion,
            bool removeLegacyLaunchers)
        {
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(packageVersion), "packageVersion must not be null or empty");

            return NativeCliInstaller.GetInstallCommand(platform, packageVersion, removeLegacyLaunchers);
        }
    }

    /// <summary>
    /// Compatibility facade for editor setup UI workflows.
    /// </summary>
    public static class CliSetupApplicationFacade
    {
        private static CliSetupApplicationService ServiceValue;

        internal static void RegisterService(CliSetupApplicationService service)
        {
            UnityEngine.Debug.Assert(service != null, "service must not be null");

            ServiceValue = service ?? throw new ArgumentNullException(nameof(service));
        }

        private static CliSetupApplicationService GetService()
        {
            if (ServiceValue == null)
            {
                throw new InvalidOperationException("Unity CLI Loop CLI setup service is not registered.");
            }

            return ServiceValue;
        }

        public static bool IsCliCheckCompleted()
        {
            return GetService().IsCliCheckCompleted();
        }

        public static bool IsCliInstalled()
        {
            return GetService().IsCliInstalled();
        }

        public static string GetCachedCliVersion()
        {
            return GetService().GetCachedCliVersion();
        }

        public static string GetCachedCliExecutablePath()
        {
            return GetService().GetCachedCliExecutablePath();
        }

        public static Task RefreshCliVersionAsync(CancellationToken ct)
        {
            return GetService().RefreshCliVersionAsync(ct);
        }

        public static Task ForceRefreshCliVersionAsync(CancellationToken ct)
        {
            return GetService().ForceRefreshCliVersionAsync(ct);
        }

        public static void InvalidateCliCache()
        {
            GetService().InvalidateCliCache();
        }

        public static string GetRequiredDispatcherVersion(string packageVersion)
        {
            return GetService().GetRequiredDispatcherVersion(packageVersion);
        }

        public static CliInstallResult EnsureProjectLocalCliCurrent(string projectRoot, string packageVersion)
        {
            return GetService().EnsureProjectLocalCliCurrent(projectRoot, packageVersion);
        }

        public static bool IsPackageOwnedCurrentUserInstallPath(
            string cliExecutablePath,
            RuntimePlatform platform)
        {
            return GetService().IsPackageOwnedCurrentUserInstallPath(cliExecutablePath, platform);
        }

        public static bool IsCliVersionLessThan(string leftVersion, string rightVersion)
        {
            return GetService().IsCliVersionLessThan(leftVersion, rightVersion);
        }

        public static bool IsCliVersionGreaterThanOrEqual(string leftVersion, string rightVersion)
        {
            return GetService().IsCliVersionGreaterThanOrEqual(leftVersion, rightVersion);
        }

        public static Task<CliInstallResult> InstallGlobalCliAsync(
            RuntimePlatform platform,
            string packageVersion,
            CancellationToken ct)
        {
            return GetService().InstallGlobalCliAsync(platform, packageVersion, ct);
        }

        public static Task<CliInstallResult> UninstallGlobalCliAsync(RuntimePlatform platform, CancellationToken ct)
        {
            return GetService().UninstallGlobalCliAsync(platform, ct);
        }

        public static NativeCliInstallCommand GetGlobalCliInstallCommand(
            RuntimePlatform platform,
            string packageVersion,
            bool removeLegacyLaunchers)
        {
            return GetService().GetGlobalCliInstallCommand(platform, packageVersion, removeLegacyLaunchers);
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
