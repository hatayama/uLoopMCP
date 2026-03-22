#if ULOOPMCP_HAS_INPUT_SYSTEM
#nullable enable
using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    // Subscribes to InputRecorder/InputReplayer lifecycle events and drives the
    // verification controller accordingly. The Recordings EditorWindow (or CLI)
    // starts recording/replay; this bridge resets the controller so logging
    // stays in sync within the same frame.
    [InitializeOnLoad]
    internal static class InputReplayVerificationEditorBridge
    {
        static InputReplayVerificationEditorBridge()
        {
            InputRecorder.RecordingStarted -= OnRecordingStarted;
            InputRecorder.RecordingStarted += OnRecordingStarted;

            InputRecorder.RecordingStopped -= OnRecordingStopped;
            InputRecorder.RecordingStopped += OnRecordingStopped;

            InputReplayer.ReplayStarted -= OnReplayStarted;
            InputReplayer.ReplayStarted += OnReplayStarted;

            InputReplayer.ReplayCompleted -= OnReplayCompleted;
            InputReplayer.ReplayCompleted += OnReplayCompleted;
        }

        private static void OnRecordingStarted()
        {
            InputReplayVerificationController? controller = FindController();
            if (controller == null)
            {
                return;
            }

            controller.ActivateForExternalControl();
            Debug.Log("[VerificationBridge] Recording started, controller reset");
        }

        private static void OnRecordingStopped()
        {
            InputReplayVerificationController? controller = FindController();
            if (controller == null)
            {
                return;
            }

            controller.OnSaveRecordingLog();
            Debug.Log("[VerificationBridge] Recording stopped, log saved");
        }

        private static void OnReplayStarted()
        {
            InputReplayVerificationController? controller = FindController();
            if (controller == null)
            {
                return;
            }

            controller.ActivateForExternalReplay();
            Debug.Log("[VerificationBridge] Replay started, controller reset");
        }

        private static void OnReplayCompleted()
        {
            InputReplayVerificationController? controller = FindController();
            if (controller == null)
            {
                return;
            }

            controller.OnReplayCompleted();
            controller.OnSaveReplayLog();
            controller.OnCompareLogs();
            Debug.Log("[VerificationBridge] Replay completed, auto-verification done");
        }

        private static InputReplayVerificationController? FindController()
        {
            return Object.FindAnyObjectByType<InputReplayVerificationController>();
        }
    }
}
#endif
