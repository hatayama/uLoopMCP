using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Use case responsible for bringing the currently connected Unity Editor window to the foreground.
    /// macOS leverages AppleScript via osascript, while Windows uses the Win32 user32.dll APIs.
    /// </summary>
    public class FocusUnityWindowUseCase : AbstractUseCase<FocusUnityWindowSchema, FocusUnityWindowResponse>
    {
        private const string MacNotSupportedMessage = "Failed to bring Unity to front on macOS";
        private const string WindowsNotSupportedMessage = "Failed to bring Unity to front on Windows";
        private const int SwRestore = 9;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

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

            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                return FocusOnMacAsync(cancellationToken);
            }

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return Task.FromResult(FocusOnWindows());
            }

            FocusUnityWindowResponse unsupportedResponse = new FocusUnityWindowResponse(
                "Focusing Unity window is only supported on macOS or Windows editors.",
                $"Unsupported platform: {Application.platform}");
            return Task.FromResult(unsupportedResponse);
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
                        return Task.FromResult(CreateFailureResponse(MacNotSupportedMessage, "Failed to start osascript process."));
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

                    return Task.FromResult(CreateFailureResponse(MacNotSupportedMessage, errorMessage.Trim()));
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult(CreateFailureResponse(MacNotSupportedMessage, ex.Message));
            }
        }

        private static FocusUnityWindowResponse FocusOnWindows()
        {
            try
            {
                Process currentProcess = Process.GetCurrentProcess();
                IntPtr windowHandle = currentProcess.MainWindowHandle;
                if (windowHandle == IntPtr.Zero)
                {
                    return CreateFailureResponse(WindowsNotSupportedMessage, "Unity main window handle is not available.");
                }

                bool isMinimized = IsIconic(windowHandle);
                if (isMinimized)
                {
                    ShowWindow(windowHandle, SwRestore);
                }

                bool broughtToFront = SetForegroundWindow(windowHandle);
                if (!broughtToFront)
                {
                    return CreateFailureResponse(WindowsNotSupportedMessage, "SetForegroundWindow returned false.");
                }

                return new FocusUnityWindowResponse("Unity window is now frontmost on Windows.");
            }
            catch (Exception ex)
            {
                return CreateFailureResponse(WindowsNotSupportedMessage, ex.Message);
            }
        }

        private static string BuildAppleScript(int pid)
        {
            return $"tell application \"System Events\" to set frontmost of (first process whose unix id is {pid}) to true";
        }

        private static FocusUnityWindowResponse CreateFailureResponse(string message, string error)
        {
            return new FocusUnityWindowResponse(message, error);
        }
    }
}

