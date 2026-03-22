#if ULOOPMCP_HAS_INPUT_SYSTEM
#nullable enable
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace io.github.hatayama.uLoopMCP
{
    public class InputRecorderWindow : EditorWindow
    {
        private const float REPAINT_INTERVAL = 0.1f;
        private const float FILE_LIST_REFRESH_INTERVAL = 2.0f;
        private const string RECORDING_EVENT_LOG = "recording-event-log.txt";
        private const string REPLAY_EVENT_LOG = "replay-event-log.txt";

        private static readonly GUILayoutOption BUTTON_HEIGHT = GUILayout.Height(30f);
        private static readonly GUILayoutOption BROWSE_BUTTON_WIDTH = GUILayout.Width(30f);
        private static readonly GUILayoutOption FILE_LIST_MAX_HEIGHT = GUILayout.MaxHeight(150f);
        private static readonly GUILayoutOption VERIFY_BUTTON_HEIGHT = GUILayout.Height(25f);

        private string _keyFilter = "";
        private string _outputPath = "";
        private bool _loop;
        private string _replayFilePath = "";

        private string _statusMessage = "Idle";
        private MessageType _statusType = MessageType.Info;
        private double _lastRepaintTime;
        private Vector2 _fileListScrollPosition;

        private string[]? _cachedFileList;
        private double _lastFileListRefreshTime;

        private string _verifyResult = "";
        private MessageType _verifyResultType = MessageType.Info;
        private Vector2 _verifyResultScrollPosition;

        [MenuItem("uLoopMCP/Windows/Input Recorder")]
        public static void ShowWindow()
        {
            InputRecorderWindow window = GetWindow<InputRecorderWindow>();
            window.titleContent = new GUIContent("Input Recorder");
            window.minSize = new Vector2(320f, 400f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void Update()
        {
            bool needsRepaint = InputRecorder.IsRecording || InputReplayer.IsReplaying;
            if (!needsRepaint)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup - _lastRepaintTime < REPAINT_INTERVAL)
            {
                return;
            }

            _lastRepaintTime = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private void OnGUI()
        {
            GUILayout.Label("Input Recorder", EditorStyles.boldLabel);
            GUILayout.Space(5f);

            DrawStatusSection();
            GUILayout.Space(10f);
            DrawRecordSection();
            GUILayout.Space(10f);
            DrawReplaySection();
            GUILayout.Space(10f);
            DrawVerificationSection();
            GUILayout.Space(10f);
            DrawRecordingFileList();
        }

        private void DrawStatusSection()
        {
            string statusText = BuildStatusText();
            EditorGUILayout.HelpBox(statusText, _statusType);
        }

        private string BuildStatusText()
        {
            if (InputRecorder.IsRecording)
            {
                return _statusMessage;
            }

            if (InputReplayer.IsReplaying)
            {
                int current = InputReplayer.CurrentFrame;
                int total = InputReplayer.TotalFrames;
                float progress = InputReplayer.Progress;
                return $"Replaying: {current} / {total} ({progress:P0})";
            }

            return _statusMessage;
        }

        private void DrawRecordSection()
        {
            GUILayout.Label("Record", EditorStyles.boldLabel);

            _keyFilter = EditorGUILayout.TextField("Key Filter", _keyFilter);
            _outputPath = EditorGUILayout.TextField("Output Path", _outputPath);

            GUILayout.Space(3f);

            EditorGUILayout.BeginHorizontal();

            bool isRecording = InputRecorder.IsRecording;
            bool isReplaying = InputReplayer.IsReplaying;
            bool isPlaying = EditorApplication.isPlaying;

            EditorGUI.BeginDisabledGroup(!isPlaying || isRecording || isReplaying);
            if (GUILayout.Button("\u25cf Record", BUTTON_HEIGHT))
            {
                StartRecording();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!isRecording);
            if (GUILayout.Button("\u25a0 Stop", BUTTON_HEIGHT))
            {
                StopRecording();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            if (!isPlaying)
            {
                EditorGUILayout.HelpBox("Enter PlayMode to record.", MessageType.Warning);
            }
        }

        private void DrawReplaySection()
        {
            GUILayout.Label("Replay", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            _replayFilePath = EditorGUILayout.TextField("Input File", _replayFilePath);
            if (GUILayout.Button("...", BROWSE_BUTTON_WIDTH))
            {
                string path = EditorUtility.OpenFilePanel("Select Recording", RecordInputConstants.DEFAULT_OUTPUT_DIR, "json");
                if (!string.IsNullOrEmpty(path))
                {
                    _replayFilePath = path;
                }
            }
            EditorGUILayout.EndHorizontal();

            _loop = EditorGUILayout.Toggle("Loop", _loop);

            GUILayout.Space(3f);

            EditorGUILayout.BeginHorizontal();

            bool isRecording = InputRecorder.IsRecording;
            bool isReplaying = InputReplayer.IsReplaying;
            bool isPlaying = EditorApplication.isPlaying;

            EditorGUI.BeginDisabledGroup(!isPlaying || isRecording || isReplaying);
            if (GUILayout.Button("\u25b6 Replay", BUTTON_HEIGHT))
            {
                StartReplay();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!isReplaying);
            if (GUILayout.Button("\u25a0 Stop", BUTTON_HEIGHT))
            {
                StopReplay();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            if (isReplaying)
            {
                float progress = InputReplayer.Progress;
                Rect rect = GUILayoutUtility.GetRect(0f, 18f, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(rect, progress, $"{InputReplayer.CurrentFrame} / {InputReplayer.TotalFrames}");
            }
        }

        private void DrawVerificationSection()
        {
            GUILayout.Label("Verification", EditorStyles.boldLabel);

            bool isPlaying = EditorApplication.isPlaying;
            MonoBehaviour? controller = isPlaying ? FindVerificationController() : null;
            bool hasController = controller != null;

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(!hasController);
            if (GUILayout.Button("Save Recording Log", VERIFY_BUTTON_HEIGHT))
            {
                SaveVerificationLog(RECORDING_EVENT_LOG);
            }

            if (GUILayout.Button("Save Replay Log", VERIFY_BUTTON_HEIGHT))
            {
                SaveVerificationLog(REPLAY_EVENT_LOG);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            bool hasBothLogs = File.Exists(GetVerificationLogPath(RECORDING_EVENT_LOG))
                            && File.Exists(GetVerificationLogPath(REPLAY_EVENT_LOG));

            EditorGUI.BeginDisabledGroup(!hasBothLogs);
            if (GUILayout.Button("\u2714 Compare Logs", VERIFY_BUTTON_HEIGHT))
            {
                CompareLogs();
            }
            EditorGUI.EndDisabledGroup();

            if (!hasController && isPlaying)
            {
                EditorGUILayout.HelpBox("InputReplayVerificationController not found in scene.", MessageType.Info);
            }

            if (!string.IsNullOrEmpty(_verifyResult))
            {
                _verifyResultScrollPosition = EditorGUILayout.BeginScrollView(_verifyResultScrollPosition, GUILayout.MaxHeight(120f));
                EditorGUILayout.HelpBox(_verifyResult, _verifyResultType);
                EditorGUILayout.EndScrollView();
            }
        }

        private void SaveVerificationLog(string fileName)
        {
            MonoBehaviour? controller = FindVerificationController();
            if (controller == null)
            {
                SetStatus("InputReplayVerificationController not found in scene.", MessageType.Error);
                return;
            }

            string path = GetVerificationLogPath(fileName);
            // SendMessage avoids a direct assembly reference to the test project
            controller.SendMessage("SaveLog", path);
            SetStatus($"Event log saved: {path}", MessageType.Info);
        }

        private void CompareLogs()
        {
            string recordingPath = GetVerificationLogPath(RECORDING_EVENT_LOG);
            string replayPath = GetVerificationLogPath(REPLAY_EVENT_LOG);

            string[] recordingLines = File.ReadAllLines(recordingPath);
            string[] replayLines = File.ReadAllLines(replayPath);

            if (recordingLines.Length == 0 && replayLines.Length == 0)
            {
                _verifyResult = "Both logs are empty.";
                _verifyResultType = MessageType.Warning;
                Repaint();
                return;
            }

            int maxLines = Mathf.Max(recordingLines.Length, replayLines.Length);
            int diffCount = 0;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            for (int i = 0; i < maxLines; i++)
            {
                string recordLine = i < recordingLines.Length ? recordingLines[i] : "(missing)";
                string replayLine = i < replayLines.Length ? replayLines[i] : "(missing)";

                if (recordLine != replayLine)
                {
                    diffCount++;
                    sb.AppendLine($"Line {i + 1}:");
                    sb.AppendLine($"  Rec: {recordLine}");
                    sb.AppendLine($"  Rep: {replayLine}");

                    if (diffCount >= 10)
                    {
                        sb.AppendLine($"... and more ({maxLines - i - 1} lines remaining)");
                        break;
                    }
                }
            }

            if (diffCount == 0)
            {
                _verifyResult = $"MATCH: {recordingLines.Length} lines identical. Replay is accurate.";
                _verifyResultType = MessageType.Info;
            }
            else
            {
                _verifyResult = $"MISMATCH: {diffCount}+ differences found (recording: {recordingLines.Length} lines, replay: {replayLines.Length} lines)\n\n{sb}";
                _verifyResultType = MessageType.Error;
            }

            Repaint();
        }

        private static string GetVerificationLogPath(string fileName)
        {
            return $"{RecordInputConstants.DEFAULT_OUTPUT_DIR}/{fileName}";
        }

        private static MonoBehaviour? FindVerificationController()
        {
            // Search by type name to avoid assembly reference to test project
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i].GetType().Name == "InputReplayVerificationController")
                {
                    return behaviours[i];
                }
            }
            return null;
        }

        private void DrawRecordingFileList()
        {
            GUILayout.Label("Recordings", EditorStyles.boldLabel);

            string[] files = GetCachedFileList();
            if (files.Length == 0)
            {
                EditorGUILayout.HelpBox("No recordings yet.", MessageType.Info);
                return;
            }

            _fileListScrollPosition = EditorGUILayout.BeginScrollView(_fileListScrollPosition, FILE_LIST_MAX_HEIGHT);

            for (int i = 0; i < files.Length; i++)
            {
                string fileName = Path.GetFileName(files[i]);
                if (GUILayout.Button(fileName, EditorStyles.linkLabel))
                {
                    _replayFilePath = files[i];
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private string[] GetCachedFileList()
        {
            double now = EditorApplication.timeSinceStartup;
            if (_cachedFileList != null && now - _lastFileListRefreshTime < FILE_LIST_REFRESH_INTERVAL)
            {
                return _cachedFileList;
            }

            _lastFileListRefreshTime = now;

            string outputDir = RecordInputConstants.DEFAULT_OUTPUT_DIR;
            if (!Directory.Exists(outputDir))
            {
                _cachedFileList = Array.Empty<string>();
                return _cachedFileList;
            }

            string[] files = Directory.GetFiles(outputDir, ReplayInputConstants.JSON_FILE_PATTERN);
            Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
            _cachedFileList = files;
            return _cachedFileList;
        }

        private void InvalidateFileListCache()
        {
            _cachedFileList = null;
        }

        private void StartRecording()
        {
            System.Collections.Generic.HashSet<Key>? keyFilter = InputRecordingFileHelper.ParseKeyFilter(_keyFilter);
            InputRecorder.StartRecording(keyFilter);

            string filterText = string.IsNullOrEmpty(_keyFilter) ? "all keys" : _keyFilter;
            SetStatus($"Recording... (filter: {filterText})", MessageType.Warning);
        }

        private void StopRecording()
        {
            InputRecordingData data = InputRecorder.StopRecording();

            string outputPath = InputRecordingFileHelper.ResolveOutputPath(_outputPath);
            InputRecordingFileHelper.Save(data, outputPath);

            InvalidateFileListCache();

            int eventCount = data.GetTotalEventCount();
            SetStatus($"Saved: {eventCount} events, {data.Metadata.TotalFrames} frames ({data.Metadata.DurationSeconds:F1}s)\n{outputPath}", MessageType.Info);
        }

        private void StartReplay()
        {
            string inputPath = InputRecordingFileHelper.ResolveLatestRecording(_replayFilePath);
            if (string.IsNullOrEmpty(inputPath))
            {
                SetStatus("No recording file found.", MessageType.Error);
                return;
            }

            if (!File.Exists(inputPath))
            {
                SetStatus($"File not found: {inputPath}", MessageType.Error);
                return;
            }

            InputRecordingData? data = InputRecordingFileHelper.Load(inputPath);
            if (data == null)
            {
                SetStatus($"Failed to parse: {inputPath}", MessageType.Error);
                return;
            }

            OverlayCanvasFactory.EnsureExists();
            InputReplayer.StartReplay(data, _loop, showOverlay: true);

            int eventCount = data.GetTotalEventCount();
            SetStatus($"Replaying: {eventCount} events, {data.Metadata.TotalFrames} frames", MessageType.Info);
        }

        private void StopReplay()
        {
            int stoppedFrame = InputReplayer.CurrentFrame;
            int totalFrames = InputReplayer.TotalFrames;
            InputReplayer.StopReplay();
            SetStatus($"Replay stopped at frame {stoppedFrame}/{totalFrames}", MessageType.Info);
        }

        private void SetStatus(string message, MessageType type)
        {
            _statusMessage = message;
            _statusType = type;
            Repaint();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                SetStatus("Idle", MessageType.Info);
            }
        }
    }
}
#endif
