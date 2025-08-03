using System;
using System.Collections.Generic;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Example of non-serializable data (for testing)
    /// This demonstrates which fields are NOT preserved during Domain Reload
    /// </summary>
    [Serializable]
    public class NonSerializableTest
    {
        // These are not serialized
        [NonSerialized] public string NonSerializedString = "This will not be saved";
        public static string StaticString = "Static is also not saved";
        public event Action SomeEvent; // Events are also not saved
        public Dictionary<string, int> DictionaryData = new Dictionary<string, int>(); // Dictionary is not supported by default
        
        // Serialized field
        [SerializeField] private string _savedData = "This will be saved";
        
        public string SavedData => _savedData;
        
        public string GetNonSerializedStatus()
        {
            return $"NonSerializedString: {NonSerializedString ?? "null"}\n" +
                   $"StaticString: {StaticString ?? "null"}\n" +
                   $"DictionaryData Count: {DictionaryData?.Count ?? 0}\n" +
                   $"SavedData: {_savedData}";
        }
    }
}