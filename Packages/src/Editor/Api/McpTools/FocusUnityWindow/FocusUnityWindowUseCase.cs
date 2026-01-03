using System;
using System.Linq;
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
    }
}
