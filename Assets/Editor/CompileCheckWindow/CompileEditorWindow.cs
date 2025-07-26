using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    public class CompileEditorWindow : EditorWindow
    {
        private CompileController _compileController;
        private CompileLogDisplay _logDisplay;
        private Vector2 _scrollPosition;
        private bool _forceRecompile = false;

        // Note: Compile window data is now managed via McpSessionManager

        [MenuItem("uLoopMCP/Windows/Compile Tool")]
        public static void ShowWindow()
        {
            CompileEditorWindow window = GetWindow<CompileEditorWindow>();
            window.titleContent = new GUIContent("Compile Tool");
            window.Show();
        }

        private void OnEnable()
        {
            // Create instances only if they don't exist yet
            if (_compileController == null || _logDisplay == null)
            {
                _compileController = new CompileController();
                _logDisplay = new CompileLogDisplay();

                // Restore persisted logs from McpSessionManager
                bool hasPersistedData = McpEditorSettings.GetCompileWindowHasData();
                if (hasPersistedData)
                {
                    string persistentLogText = McpEditorSettings.GetCompileWindowLogText();
                    _logDisplay.RestoreFromText(persistentLogText);
                }

                // Subscribe to events
                _compileController.OnCompileStarted += _logDisplay.AppendStartMessage;
                _compileController.OnAssemblyCompiled += _logDisplay.AppendAssemblyMessage;
                _compileController.OnCompileCompleted += OnCompileCompleted;
            }
            else
            {
                // If an instance already exists, re-subscribe as the event subscription might have been lost
                if (!_compileController.IsCompiling)
                {
                    _compileController.OnCompileStarted += _logDisplay.AppendStartMessage;
                    _compileController.OnAssemblyCompiled += _logDisplay.AppendAssemblyMessage;
                    _compileController.OnCompileCompleted += OnCompileCompleted;
                }
            }
        }

        private void OnDisable()
        {
            DisposeInstances();

            // Set to null only on OnDisable for a complete cleanup
            _compileController = null;
            _logDisplay = null;
        }

        private void DisposeInstances()
        {
            if (_compileController != null)
            {
                // Unsubscribe from events
                if (_logDisplay != null)
                {
                    _compileController.OnCompileStarted -= _logDisplay.AppendStartMessage;
                    _compileController.OnAssemblyCompiled -= _logDisplay.AppendAssemblyMessage;
                }
                _compileController.OnCompileCompleted -= OnCompileCompleted;

                _compileController.Dispose();
                // Set to null only on OnDisable
            }

            if (_logDisplay != null)
            {
                _logDisplay.Dispose();
                // Set to null only on OnDisable
            }
        }

        private void OnGUI()
        {
            if (_compileController == null || _logDisplay == null) return;

            GUILayout.Label("Unity Compile Tool", EditorStyles.boldLabel);

            // Force recompile option
            _forceRecompile = EditorGUILayout.Toggle("Force Recompile", _forceRecompile);
            GUILayout.Space(5);

            EditorGUI.BeginDisabledGroup(_compileController.IsCompiling);
            string buttonText = _compileController.IsCompiling ? "Compiling..." :
                               (_forceRecompile ? "Run Force Compile" : "Run Compile");
            if (GUILayout.Button(buttonText, GUILayout.Height(30)))
            {
                // Execute compilation using async/await
                ExecuteCompileAsync().Forget();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(5);

            // Clear button
            if (GUILayout.Button("Clear Log", GUILayout.Height(25)))
            {
                ClearLog();
            }

            GUILayout.Space(10);

            GUILayout.Label("Compilation Result:", EditorStyles.boldLabel);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(300));
            EditorGUILayout.TextArea(_logDisplay.LogText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            DrawMessageDetails();
        }

        private async Task ExecuteCompileAsync()
        {
            CompileResult result = await _compileController.TryCompileAsync(_forceRecompile);
            
            // Output result to log (for debugging)
            UnityEngine.Debug.Log($"Compilation finished: Success={result.Success}, Errors={result.error.Length}, Warnings={result.warning.Length}");
        }

        private void OnCompileCompleted(CompileResult result)
        {
            _logDisplay.AppendCompletionMessage(result);

            // Persist log to McpSessionManager
            McpEditorSettings.SetCompileWindowLogText(_logDisplay.LogText);
            McpEditorSettings.SetCompileWindowHasData(true);

            Repaint();
        }

        private void DrawMessageDetails()
        {
            var messages = _compileController.CompileMessages;
            if (messages.Count > 0)
            {
                GUILayout.Space(10);
                GUILayout.Label($"Error/Warning Details ({messages.Count} items):", EditorStyles.boldLabel);

                foreach (CompilerMessage message in messages)
                {
                    GUIStyle style = message.type == CompilerMessageType.Error ?
                        EditorStyles.helpBox : EditorStyles.helpBox;

                    string prefix = message.type == CompilerMessageType.Error ? "[Error]" : "[Warning]";
                    EditorGUILayout.LabelField($"{prefix} {message.message}", style);
                }
            }
        }

        private void ClearLog()
        {
            _logDisplay.Clear();
            _compileController.ClearMessages();

            // Also clear McpSessionManager data
            McpEditorSettings.ClearCompileWindowData();

            Repaint();
        }
    }
}