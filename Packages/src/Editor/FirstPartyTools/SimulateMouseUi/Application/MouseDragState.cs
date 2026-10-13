#nullable enable
using UnityEditor;

using io.github.hatayama.UnityCliLoop.Runtime;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Provides Mouse Drag State operations for its owning module.
    /// </summary>
    internal sealed class MouseDragStateService
    {
        // A destroyed target ends the drag without a command, as Unity's null check on it already did.
        internal bool IsDragging => Session != null && !Session.IsTargetDestroyed;
        internal IMouseUiDragSession? Session { get; set; }

        internal void RegisterPlayModeCallbacks()
        {
            // PlayMode exit leaves dangling references to destroyed GameObjects
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        internal void Clear()
        {
            Session = null;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Clear();
                SimulateMouseUiOverlayState.Clear();
            }
        }
    }

    /// <summary>
    /// Stores Mouse Drag state shared by the owning workflow.
    /// </summary>
    internal static class MouseDragState
    {
        private static readonly MouseDragStateService ServiceValue = new MouseDragStateService();

        internal static void InitializeForEditorStartup()
        {
            ServiceValue.RegisterPlayModeCallbacks();
        }

        internal static bool IsDragging => ServiceValue.IsDragging;

        internal static IMouseUiDragSession? Session
        {
            get => ServiceValue.Session;
            set => ServiceValue.Session = value;
        }

        internal static void Clear()
        {
            ServiceValue.Clear();
        }
    }
}
