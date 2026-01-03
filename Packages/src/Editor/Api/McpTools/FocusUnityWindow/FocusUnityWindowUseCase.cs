using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Use case responsible for bringing the currently connected Unity Editor window to the foreground.
    /// </summary>
    public class FocusUnityWindowUseCase : AbstractUseCase<FocusUnityWindowSchema, FocusUnityWindowResponse>
    {
        /// <inheritdoc />
        public override Task<FocusUnityWindowResponse> ExecuteAsync(
            FocusUnityWindowSchema parameters,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

#if UNITY_EDITOR_WIN
            return FocusOnWindowsAsync();
#else
            return FocusWithEditorWindowApi();
#endif
        }

        /// <summary>
        /// Focus Unity Editor windows using EditorWindow.Focus() API.
        /// Works on macOS and Linux.
        /// </summary>
        private Task<FocusUnityWindowResponse> FocusWithEditorWindowApi()
        {
            EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();

            EditorWindow dockedWindow = windows.FirstOrDefault(w => w.docked);
            EditorWindow[] floatingWindows = windows.Where(w => !w.docked).ToArray();

            int focusedCount = 0;

            if (dockedWindow != null)
            {
                dockedWindow.Focus();
                focusedCount++;
            }

            foreach (EditorWindow window in floatingWindows)
            {
                window.Focus();
                focusedCount++;
            }

            FocusUnityWindowResponse response = new FocusUnityWindowResponse(
                $"Unity Editor windows are now frontmost. Focused {focusedCount} window(s).");

            return Task.FromResult(response);
        }

#if UNITY_EDITOR_WIN
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

        /// <summary>
        /// Brings the Unity Editor window to the front on Windows by minimizing then restoring,
        /// followed by SetForegroundWindow to push Z-order.
        /// </summary>
        private static Task<FocusUnityWindowResponse> FocusOnWindowsAsync()
        {
            IntPtr hWnd = ResolveUnityEditorWindowHandle();
            if (hWnd == IntPtr.Zero)
            {
                return Task.FromResult(new FocusUnityWindowResponse(
                    "Failed to bring Unity to front on Windows",
                    "Could not find Unity Editor window handle."));
            }

            ShowWindowAsync(hWnd, SW_MINIMIZE);
            ShowWindowAsync(hWnd, SW_RESTORE);

            bool foregrounded = SetForegroundWindow(hWnd);
            if (foregrounded)
            {
                return Task.FromResult(new FocusUnityWindowResponse(
                    "Unity window is now frontmost on Windows."));
            }

            return Task.FromResult(new FocusUnityWindowResponse(
                "Failed to bring Unity to front on Windows",
                "SetForegroundWindow returned false."));
        }

        /// <summary>
        /// Resolves the main Unity Editor HWND for the current process.
        /// Falls back to EnumWindows when MainWindowHandle is not available.
        /// </summary>
        private static IntPtr ResolveUnityEditorWindowHandle()
        {
            using (Process current = Process.GetCurrentProcess())
            {
                IntPtr handle = current.MainWindowHandle;
                if (handle != IntPtr.Zero)
                {
                    return handle;
                }

                IntPtr found = IntPtr.Zero;
                EnumWindows(
                    (hWnd, lParam) =>
                    {
                        GetWindowThreadProcessId(hWnd, out uint pid);
                        if (pid == (uint)current.Id && IsWindowVisible(hWnd))
                        {
                            found = hWnd;
                            return false;
                        }
                        return true;
                    },
                    IntPtr.Zero);

                return found;
            }
        }
#endif
    }
}
