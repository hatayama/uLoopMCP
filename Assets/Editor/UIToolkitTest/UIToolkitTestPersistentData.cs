using UnityEngine;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// ScriptableSingleton for persistent data storage
    /// This data persists across:
    /// - Domain Reload
    /// - Unity Editor restart
    /// - Window close/reopen
    /// - Project reopen
    /// </summary>
    [FilePath("UserSettings/UIToolkitTestPersistentData.asset", FilePathAttribute.Location.ProjectFolder)]
    public class UIToolkitTestPersistentData : ScriptableSingleton<UIToolkitTestPersistentData>
    {
        [SerializeField]
        private string _persistentText = "ScriptableSingleton Initial Text";
        
        [SerializeField]
        private int _persistentCounter = 0;
        
        [SerializeField]
        private bool _persistentFlag = false;
        
        /// <summary>
        /// Persistent text that survives everything
        /// </summary>
        public string PersistentText
        {
            get => _persistentText;
            set
            {
                if (_persistentText != value)
                {
                    _persistentText = value;
                    Save();
                }
            }
        }
        
        /// <summary>
        /// Persistent counter
        /// </summary>
        public int PersistentCounter
        {
            get => _persistentCounter;
            set
            {
                if (_persistentCounter != value)
                {
                    _persistentCounter = value;
                    Save();
                }
            }
        }
        
        /// <summary>
        /// Persistent flag
        /// </summary>
        public bool PersistentFlag
        {
            get => _persistentFlag;
            set
            {
                if (_persistentFlag != value)
                {
                    _persistentFlag = value;
                    Save();
                }
            }
        }
        
        /// <summary>
        /// Save changes to disk
        /// </summary>
        private void Save()
        {
            Save(true);
        }
        
        /// <summary>
        /// Reset all persistent data
        /// </summary>
        public void ResetData()
        {
            _persistentText = "ScriptableSingleton Initial Text";
            _persistentCounter = 0;
            _persistentFlag = false;
            Save();
        }
        
        /// <summary>
        /// Get a summary of persistent data
        /// </summary>
        public string GetDataSummary()
        {
            return $"=== ScriptableSingleton Data ===\n" +
                   $"Persistent Text: {_persistentText}\n" +
                   $"Persistent Counter: {_persistentCounter}\n" +
                   $"Persistent Flag: {_persistentFlag}\n" +
                   $"File Path: {GetFilePath()}";
        }
    }
}