using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace io.github.hatayama.uLoopMCP
{
    public class UIToolkitTestWindow : EditorWindow
    {
        // Persist state with SerializeField (Domain Reload support)
        [SerializeField]
        private string _customText = "Initial Text";
        
        [SerializeField]
        private int _textIndex = 0;
        
        // Non-serializable test data (old pattern - will be lost on domain reload)
        [SerializeField]
        private NonSerializableTest _nonSerializableTest = new();
        
        // Persistence comparison data
        [SerializeField]
        private int _serializeFieldCounter = 0;
        
        // UI Elements references
        private Label _textLabel;
        private Label _complexDataLabel;
        private Label _nonSerializableLabel;
        
        // New labels for persistence comparison
        private Label _serializeFieldLabel;
        private Label _nonSerializedLabel;
        private Label _scriptableSingletonLabel;
        
        // Non-serialized data (for comparison)
        private string _nonSerializedText = "Non-Serialized Initial Text";
        private int _nonSerializedCounter = 0;
        
        // UXML and USS assets
        private VisualTreeAsset _visualTreeAsset;
        private StyleSheet _styleSheet;
        
        private readonly string[] _textOptions = new[]
        {
            "Initial Text",
            "Text 1: UI Toolkit Verification",
            "Text 2: Label Switching Test",
            "Text 3: MenuItem Operation Check"
        };

        [MenuItem("uLoopMCP/Windows/UI Toolkit Test Window")]
        public static void ShowWindow()
        {
            // Use GetWindow with utility:false and showImmediately:false to reuse existing instance
            UIToolkitTestWindow window = GetWindow<UIToolkitTestWindow>(false, "UI Toolkit Test", true);
            window.Show();
        }

        private void OnEnable()
        {
            // Save instance reference to ensure data persistence
            if (_instance == null)
                _instance = this;
        }

        private void OnDisable()
        {
            // Keep reference when window is closed
            if (_instance == this)
                _instance = null;
        }

        private static UIToolkitTestWindow _instance;

        private void CreateGUI()
        {
            // Load UXML
            string uxmlPath = "Assets/Editor/UIToolkitTest/UIToolkitTestWindow.uxml";
            _visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (_visualTreeAsset == null)
            {
                Debug.LogError($"[UIToolkitTest] Failed to load UXML from: {uxmlPath}");
                return;
            }

            // Load USS
            string ussPath = "Assets/Editor/UIToolkitTest/UIToolkitTestWindow.uss";
            _styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
            if (_styleSheet == null)
            {
                Debug.LogError($"[UIToolkitTest] Failed to load USS from: {ussPath}");
                return;
            }

            // Clone the visual tree from UXML
            VisualElement root = _visualTreeAsset.CloneTree();
            rootVisualElement.Add(root);
            
            // Apply the stylesheet
            root.styleSheets.Add(_styleSheet);

            // Get references to UI elements
            _textLabel = root.Q<Label>("text-label");
            _complexDataLabel = root.Q<Label>("complex-data-label");
            _nonSerializableLabel = root.Q<Label>("non-serializable-label");
            
            // Get references to persistence comparison labels
            _serializeFieldLabel = root.Q<Label>("serialize-field-label");
            _nonSerializedLabel = root.Q<Label>("non-serialized-label");
            _scriptableSingletonLabel = root.Q<Label>("scriptable-singleton-label");

            // Set initial values
            if (_textLabel != null)
                _textLabel.text = _customText;

            // Bind button callbacks
            Button switchTextButton = root.Q<Button>("switch-text-button");
            if (switchTextButton != null)
                switchTextButton.clicked += SwitchText;

            Button updateComplexDataButton = root.Q<Button>("update-complex-data-button");
            if (updateComplexDataButton != null)
                updateComplexDataButton.clicked += UpdateComplexDataDisplay;

            Button randomizeDataButton = root.Q<Button>("randomize-data-button");
            if (randomizeDataButton != null)
                randomizeDataButton.clicked += RandomizeComplexData;

            Button updateNonSerializableButton = root.Q<Button>("update-non-serializable-button");
            if (updateNonSerializableButton != null)
                updateNonSerializableButton.clicked += UpdateNonSerializableData;

            // Bind persistence comparison buttons
            Button incrementCountersButton = root.Q<Button>("increment-counters-button");
            if (incrementCountersButton != null)
                incrementCountersButton.clicked += IncrementAllCounters;

            Button updateTextsButton = root.Q<Button>("update-texts-button");
            if (updateTextsButton != null)
                updateTextsButton.clicked += UpdateAllTexts;

            Button resetSingletonButton = root.Q<Button>("reset-singleton-button");
            if (resetSingletonButton != null)
                resetSingletonButton.clicked += ResetScriptableSingleton;

            // Update initial display
            UpdateComplexDataDisplay();
            UpdateNonSerializableDisplay();
            UpdatePersistenceComparison();
        }

        // MenuItem callbacks for static methods
        [MenuItem("uLoopMCP/UI Toolkit Test/Switch Text")]
        private static void SwitchTextMenuItem()
        {
            UIToolkitTestWindow window = GetWindow<UIToolkitTestWindow>(false);
            if (window != null)
            {
                window.SwitchText();
            }
        }

        [MenuItem("uLoopMCP/UI Toolkit Test/Set Text 1")]
        private static void SetText1()
        {
            UIToolkitTestWindow window = GetWindow<UIToolkitTestWindow>(false);
            if (window != null)
            {
                window.SetText(1);
            }
        }

        [MenuItem("uLoopMCP/UI Toolkit Test/Set Text 2")]
        private static void SetText2()
        {
            UIToolkitTestWindow window = GetWindow<UIToolkitTestWindow>(false);
            if (window != null)
            {
                window.SetText(2);
            }
        }

        [MenuItem("uLoopMCP/UI Toolkit Test/Set Text 3")]
        private static void SetText3()
        {
            UIToolkitTestWindow window = GetWindow<UIToolkitTestWindow>(false);
            if (window != null)
            {
                window.SetText(3);
            }
        }

        [MenuItem("uLoopMCP/UI Toolkit Test/Reset Text")]
        private static void ResetText()
        {
            UIToolkitTestWindow window = GetWindow<UIToolkitTestWindow>(false);
            if (window != null)
            {
                window.SetText(0);
            }
        }

        // Switch text
        private void SwitchText()
        {
            _textIndex = (_textIndex + 1) % _textOptions.Length;
            UpdateTextLabel(_textOptions[_textIndex]);
        }

        // Set text by index
        private void SetText(int index)
        {
            if (index >= 0 && index < _textOptions.Length)
            {
                _textIndex = index;
                UpdateTextLabel(_textOptions[_textIndex]);
            }
        }

        // Update label text
        private void UpdateTextLabel(string newText)
        {
            _customText = newText;
            if (_textLabel != null)
            {
                _textLabel.text = _customText;
                Debug.Log($"[UIToolkitTest] Text switched: {_customText}");
            }
        }

        // Static method called from MCP
        public static bool SetTextFromMCP(string text, bool autoOpenWindow, out bool windowOpened)
        {
            windowOpened = false;
            
            // Search for existing window (doesn't bring Unity to foreground)
            UIToolkitTestWindow window = null;
            UIToolkitTestWindow[] windows = Resources.FindObjectsOfTypeAll<UIToolkitTestWindow>();
            if (windows.Length > 0)
            {
                window = windows[0];
            }
            
            // If window is not open
            if (window == null && autoOpenWindow)
            {
                // Use CreateWindow instead of GetWindow
                window = CreateWindow<UIToolkitTestWindow>();
                window.titleContent = new GUIContent("UI Toolkit Test");
                window.Show();
                windowOpened = true;
            }
            
            // If window is null, fail
            if (window == null)
            {
                return false;
            }
            
            // Set text directly
            window.SetCustomText(text);
            return true;
        }

        // Update complex data display
        private void UpdateComplexDataDisplay()
        {
            if (_complexDataLabel != null)
            {
                ComplexPersistentData complexData = ComplexPersistentData.instance;
                _complexDataLabel.text = complexData.GetDataSummary();
            }
        }

        // Randomize complex data
        private void RandomizeComplexData()
        {
            ComplexPersistentData complexData = ComplexPersistentData.instance;
            complexData.RandomizeData();
            UpdateComplexDataDisplay();
            Debug.Log("[UIToolkitTest] Complex data randomized");
        }

        // Update non-serializable data display
        private void UpdateNonSerializableDisplay()
        {
            if (_nonSerializableLabel != null && _nonSerializableTest != null)
            {
                _nonSerializableLabel.text = _nonSerializableTest.GetNonSerializedStatus();
            }
        }

        // Update non-serializable data
        private void UpdateNonSerializableData()
        {
            if (_nonSerializableTest != null)
            {
                // Set value to non-serialized field
                _nonSerializableTest.NonSerializedString = $"Update time: {System.DateTime.Now:HH:mm:ss}";
                
                // Update static field
                NonSerializableTest.StaticString = $"Static update: {UnityEngine.Random.Range(1, 100)}";
                
                // Add data to Dictionary
                if (_nonSerializableTest.DictionaryData == null)
                {
                    _nonSerializableTest.DictionaryData = new System.Collections.Generic.Dictionary<string, int>();
                }
                _nonSerializableTest.DictionaryData[$"Key{_nonSerializableTest.DictionaryData.Count}"] = UnityEngine.Random.Range(1, 100);
                
                UpdateNonSerializableDisplay();
                Debug.Log("[UIToolkitTest] Non-serializable data updated (will be lost on Domain Reload)");
            }
        }

        // Update persistence comparison display
        private void UpdatePersistenceComparison()
        {
            // Update SerializeField label
            if (_serializeFieldLabel != null)
            {
                _serializeFieldLabel.text = $"Counter: {_serializeFieldCounter}\nText: {_customText}";
            }

            // Update Non-Serialized label
            if (_nonSerializedLabel != null)
            {
                _nonSerializedLabel.text = $"Counter: {_nonSerializedCounter}\nText: {_nonSerializedText}";
            }

            // Update ScriptableSingleton label
            if (_scriptableSingletonLabel != null)
            {
                UIToolkitTestPersistentData persistentData = UIToolkitTestPersistentData.instance;
                _scriptableSingletonLabel.text = $"Counter: {persistentData.PersistentCounter}\nText: {persistentData.PersistentText}";
            }
        }

        // Increment all counters
        private void IncrementAllCounters()
        {
            // Increment SerializeField counter
            _serializeFieldCounter++;

            // Increment Non-Serialized counter
            _nonSerializedCounter++;

            // Increment ScriptableSingleton counter
            UIToolkitTestPersistentData persistentData = UIToolkitTestPersistentData.instance;
            persistentData.PersistentCounter++;

            // Update display
            UpdatePersistenceComparison();
            
            Debug.Log("[UIToolkitTest] All counters incremented");
        }

        // Update all texts
        private void UpdateAllTexts()
        {
            string timestamp = System.DateTime.Now.ToString("HH:mm:ss");

            // Update SerializeField text
            _customText = $"SerializeField Updated: {timestamp}";
            if (_textLabel != null)
                _textLabel.text = _customText;

            // Update Non-Serialized text
            _nonSerializedText = $"Non-Serialized Updated: {timestamp}";

            // Update ScriptableSingleton text
            UIToolkitTestPersistentData persistentData = UIToolkitTestPersistentData.instance;
            persistentData.PersistentText = $"ScriptableSingleton Updated: {timestamp}";

            // Update display
            UpdatePersistenceComparison();
            
            Debug.Log($"[UIToolkitTest] All texts updated at {timestamp}");
        }

        // Reset ScriptableSingleton data
        private void ResetScriptableSingleton()
        {
            UIToolkitTestPersistentData persistentData = UIToolkitTestPersistentData.instance;
            persistentData.ResetData();
            
            // Update display
            UpdatePersistenceComparison();
            
            Debug.Log("[UIToolkitTest] ScriptableSingleton data reset");
        }

        // Private method to set custom text
        private void SetCustomText(string text)
        {
            UpdateTextLabel(text);
            Debug.Log($"[UIToolkitTest] Text set from MCP: {text}");
        }
    }
}