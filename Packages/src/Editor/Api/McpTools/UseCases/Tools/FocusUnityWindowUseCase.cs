#if UNITY_EDITOR_OSX

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Use case responsible for bringing the currently connected Unity Editor window to the foreground.
    /// Available only on macOS Editor builds.
    /// </summary>
    public class FocusUnityWindowUseCase : AbstractUseCase<FocusUnityWindowSchema, FocusUnityWindowResponse>
    {
        private const string MacFailureMessage = "Failed to bring Unity to front on macOS";

        /// <inheritdoc />
        public override Task<FocusUnityWindowResponse> ExecuteAsync(
            FocusUnityWindowSchema parameters,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (Application.platform != RuntimePlatform.OSXEditor)
            {
                FocusUnityWindowResponse unsupportedResponse = new FocusUnityWindowResponse(
                    "Focusing Unity window is only supported on macOS Editor.",
                    $"Unsupported platform: {Application.platform}");
                return Task.FromResult(unsupportedResponse);
            }

            return FocusOnMacAsync(cancellationToken);
        }

        private static Task<FocusUnityWindowResponse> FocusOnMacAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                Process currentProcess = Process.GetCurrentProcess();
                string script = BuildAppleScript(currentProcess.Id);

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/osascript",
                    Arguments = $"-e '{script}'",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process osascriptProcess = Process.Start(startInfo))
                {
                    if (osascriptProcess == null)
                    {
                        return Task.FromResult(CreateFailureResponse("Failed to start osascript process."));
                    }

                    osascriptProcess.WaitForExit();
                    bool succeeded = osascriptProcess.ExitCode == 0;
                    if (succeeded)
                    {
                        return Task.FromResult(new FocusUnityWindowResponse("Unity window is now frontmost on macOS."));
                    }

                    string errorMessage = osascriptProcess.StandardError.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(errorMessage))
                    {
                        errorMessage = $"osascript exited with code {osascriptProcess.ExitCode}.";
                    }

                    return Task.FromResult(CreateFailureResponse(errorMessage.Trim()));
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult(CreateFailureResponse(ex.Message));
            }
        }

        private static string BuildAppleScript(int pid)
        {
            return $"tell application \"System Events\" to set frontmost of (first process whose unix id is {pid}) to true";
        }

        private static FocusUnityWindowResponse CreateFailureResponse(string error)
        {
            return new FocusUnityWindowResponse(MacFailureMessage, error);
        }
    }
}

#endif
