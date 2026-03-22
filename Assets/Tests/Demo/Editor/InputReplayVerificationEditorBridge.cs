#if ULOOPMCP_HAS_INPUT_SYSTEM
#nullable enable
using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    // Detects InputRecorder/InputReplayer state transitions and drives the
    // verification controller accordingly. The Recordings EditorWindow (or CLI)
    // starts recording/replay; this bridge resets the controller so logging
    // stays in sync.
    [InitializeOnLoad]
    internal static class InputReplayVerificationEditorBridge
    {
        private static bool _prevIsRecording;
        private static bool _prevIsReplaying;

        static InputReplayVerificationEditorBridge()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            InputReplayer.ReplayCompleted -= OnReplayCompleted;
            InputReplayer.ReplayCompleted += OnReplayCompleted;
        }

        private static void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
            {
                _prevIsRecording = false;
                _prevIsReplaying = false;
                return;
            }

            bool isRecording = InputRecorder.IsRecording;
            bool isReplaying = InputReplayer.IsReplaying;

            if (isRecording && !_prevIsRecording)
            {
                OnRecordingStarted();
            }
            else if (!isRecording && _prevIsRecording)
            {
                OnRecordingStopped();
            }

            if (isReplaying && !_prevIsReplaying)
            {
                OnReplayStarted();
            }

            _prevIsRecording = isRecording;
            _prevIsReplaying = isReplaying;
        }

        private static void OnRecordingStarted()
        {
            InputReplayVerificationController? controller = FindController();
            if (controller == null)
            {
                return;
            }

            controller.ActivateForExternalControl();
            Debug.Log("[VerificationBridge] Recording detected, controller reset");
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
            Debug.Log("[VerificationBridge] Replay detected, controller reset");
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
