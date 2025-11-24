#if UNITY_EDITOR_OSX || UNITY_EDITOR_WIN

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
    /// Available on macOS and Windows Editor builds.
    /// </summary>
    public class FocusUnityWindowUseCase : AbstractUseCase<FocusUnityWindowSchema, FocusUnityWindowResponse>
    {
        private const string MacFailureMessage = "Failed to bring Unity to front on macOS";
        private const string WindowsFailureMessage = "Failed to bring Unity to front on Windows";

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

#if UNITY_EDITOR_OSX
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                return FocusOnMacAsync(cancellationToken);
            }
#endif

#if UNITY_EDITOR_WIN
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return FocusOnWindowsAsync(cancellationToken);
            }
#endif

            FocusUnityWindowResponse unsupportedResponse = new FocusUnityWindowResponse(
                "Focusing Unity window is only supported on macOS or Windows Editor.",
                $"Unsupported platform: {Application.platform}");
            return Task.FromResult(unsupportedResponse);
        }

        /// <summary>
        /// Brings the Unity Editor window to the front on macOS by using AppleScript via osascript.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result describing success or failure.</returns>
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

        /// <summary>
        /// Builds the AppleScript to set frontmost for the given PID on macOS.
        /// </summary>
        /// <param name="pid">Process ID of Unity Editor.</param>
        /// <returns>AppleScript string.</returns>
        private static string BuildAppleScript(int pid)
        {
            return $"tell application \"System Events\" to set frontmost of (first process whose unix id is {pid}) to true";
        }

        /// <summary>
        /// Creates a macOS failure response with a consistent message header.
        /// </summary>
        /// <param name="error">Detailed error.</param>
        /// <returns>Failure response.</returns>
        private static FocusUnityWindowResponse CreateFailureResponse(string error)
        {
            return new FocusUnityWindowResponse(MacFailureMessage, error);
        }

#if UNITY_EDITOR_WIN
        /// <summary>
        /// Brings the Unity Editor window to the front on Windows by minimizing then restoring,
        /// followed by SetForegroundWindow to push Z-order.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result describing success or failure.</returns>
        private static Task<FocusUnityWindowResponse> FocusOnWindowsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IntPtr hWnd = ResolveUnityEditorWindowHandle();
            if (hWnd == IntPtr.Zero)
            {
                return Task.FromResult(CreateWindowsFailureResponse("Could not find Unity Editor window handle."));
            }

            // Minimize → small wait → restore → SetForegroundWindow
            ShowWindowAsync(hWnd, SW_MINIMIZE);
            ShowWindowAsync(hWnd, SW_RESTORE);

            bool foregrounded = SetForegroundWindow(hWnd);
            if (foregrounded)
            {
                return Task.FromResult(new FocusUnityWindowResponse("Unity window is now frontmost on Windows."));
            }

            return Task.FromResult(CreateWindowsFailureResponse("SetForegroundWindow returned false."));
        }

        /// <summary>
        /// Resolves the main Unity Editor HWND for the current process.
        /// Falls back to EnumWindows when MainWindowHandle is not available.
        /// </summary>
        /// <returns>HWND for Unity Editor main window, or IntPtr.Zero if not found.</returns>
        private static IntPtr ResolveUnityEditorWindowHandle()
        {
            Process current = Process.GetCurrentProcess();
            IntPtr handle = current.MainWindowHandle;
            if (handle != IntPtr.Zero)
            {
                return handle;
            }

            IntPtr found = IntPtr.Zero;
            bool EnumResult = EnumWindows(
                (hWnd, lParam) =>
                {
                    uint pid;
                    GetWindowThreadProcessId(hWnd, out pid);
                    if (pid == (uint)current.Id && IsWindowVisible(hWnd))
                    {
                        found = hWnd;
                        return false; // stop enumeration
                    }
                    return true; // continue
                },
                IntPtr.Zero);

            return found;
        }

        /// <summary>
        /// Creates a Windows failure response with a consistent message header.
        /// </summary>
        /// <param name="error">Detailed error.</param>
        /// <returns>Failure response.</returns>
        private static FocusUnityWindowResponse CreateWindowsFailureResponse(string error)
        {
            return new FocusUnityWindowResponse(WindowsFailureMessage, error);
        }

        // P/Invoke declarations and helpers for Windows
        private const int SW_MINIMIZE = 6;
        private const int SW_RESTORE = 9;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
#endif
    }
}

#endif
