using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Complex data structure using ScriptableSingleton for persistence
    /// This demonstrates how complex nested data can be persisted
    /// </summary>
    [FilePath("UserSettings/ComplexPersistentData.asset", FilePathAttribute.Location.ProjectFolder)]
    public class ComplexPersistentData : ScriptableSingleton<ComplexPersistentData>
    {
        // Basic types
        [SerializeField] private string _userName = "Test User";
        [SerializeField] private int _level = 1;
        [SerializeField] private float _progress = 0.0f;
        [SerializeField] private bool _isActive = true;
        
        // Unity-specific types
        [SerializeField] private Vector3 _position = Vector3.zero;
        [SerializeField] private Color _themeColor = Color.blue;
        
        // Arrays and Lists
        [SerializeField] private string[] _tags = new string[] { "Tag1", "Tag2", "Tag3" };
        [SerializeField] private List<int> _scores = new List<int> { 100, 200, 300 };
        
        // Nested class
        [SerializeField] private NestedData _nestedData = new NestedData();
        
        // List with complex objects
        [SerializeField] private List<NestedData> _nestedDataList = new List<NestedData>
        {
            new NestedData { Name = "Nested1", Value = 10 },
            new NestedData { Name = "Nested2", Value = 20 }
        };
        
        // Properties (accessors)
        public string UserName
        {
            get => _userName;
            set
            {
                if (_userName != value)
                {
                    _userName = value;
                    Save();
                }
            }
        }
        
        public int Level
        {
            get => _level;
            set
            {
                if (_level != value)
                {
                    _level = value;
                    Save();
                }
            }
        }
        
        public float Progress
        {
            get => _progress;
            set
            {
                if (_progress != value)
                {
                    _progress = value;
                    Save();
                }
            }
        }
        
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    Save();
                }
            }
        }
        
        public Vector3 Position
        {
            get => _position;
            set
            {
                if (_position != value)
                {
                    _position = value;
                    Save();
                }
            }
        }
        
        public Color ThemeColor
        {
            get => _themeColor;
            set
            {
                if (_themeColor != value)
                {
                    _themeColor = value;
                    Save();
                }
            }
        }
        
        public string[] Tags => _tags;
        public List<int> Scores => _scores;
        public NestedData NestedData => _nestedData;
        public List<NestedData> NestedDataList => _nestedDataList;
        
        /// <summary>
        /// Save changes to disk
        /// </summary>
        private void Save()
        {
            Save(true);
        }
        
        /// <summary>
        /// Get data summary as string (for debugging)
        /// </summary>
        public string GetDataSummary()
        {
            string summary = $"=== ComplexPersistentData (ScriptableSingleton) ===\n";
            summary += $"UserName: {_userName}\n";
            summary += $"Level: {_level}\n";
            summary += $"Progress: {_progress:F2}\n";
            summary += $"IsActive: {_isActive}\n";
            summary += $"Position: {_position}\n";
            summary += $"ThemeColor: {_themeColor}\n";
            summary += $"Tags: [{string.Join(", ", _tags)}]\n";
            summary += $"Scores: [{string.Join(", ", _scores)}]\n";
            summary += $"NestedData: {_nestedData}\n";
            summary += $"NestedDataList Count: {_nestedDataList.Count}\n";
            foreach (var nested in _nestedDataList)
            {
                summary += $"  - {nested}\n";
            }
            summary += $"File Path: {GetFilePath()}";
            return summary;
        }
        
        /// <summary>
        /// Randomize data (for testing)
        /// </summary>
        public void RandomizeData()
        {
            _userName = $"User{UnityEngine.Random.Range(1, 100)}";
            _level = UnityEngine.Random.Range(1, 100);
            _progress = UnityEngine.Random.Range(0f, 100f);
            _isActive = UnityEngine.Random.Range(0, 2) == 1;
            _position = new Vector3(
                UnityEngine.Random.Range(-10f, 10f),
                UnityEngine.Random.Range(-10f, 10f),
                UnityEngine.Random.Range(-10f, 10f)
            );
            _themeColor = new Color(
                UnityEngine.Random.Range(0f, 1f),
                UnityEngine.Random.Range(0f, 1f),
                UnityEngine.Random.Range(0f, 1f)
            );
            
            // Randomize tags
            for (int i = 0; i < _tags.Length; i++)
            {
                _tags[i] = $"Tag{UnityEngine.Random.Range(1, 20)}";
            }
            
            // Randomize scores
            _scores.Clear();
            int scoreCount = UnityEngine.Random.Range(3, 8);
            for (int i = 0; i < scoreCount; i++)
            {
                _scores.Add(UnityEngine.Random.Range(0, 1000));
            }
            
            // Update nested data
            _nestedData.Name = $"Nested{UnityEngine.Random.Range(1, 50)}";
            _nestedData.Value = UnityEngine.Random.Range(0, 100);
            
            // Randomize nested data list
            _nestedDataList.Clear();
            int nestedCount = UnityEngine.Random.Range(2, 6);
            for (int i = 0; i < nestedCount; i++)
            {
                _nestedDataList.Add(new NestedData
                {
                    Name = $"ListItem{i + 1}",
                    Value = UnityEngine.Random.Range(0, 100)
                });
            }
            
            // Save after all changes
            Save();
        }
        
        /// <summary>
        /// Reset data to defaults
        /// </summary>
        public void ResetData()
        {
            _userName = "Test User";
            _level = 1;
            _progress = 0.0f;
            _isActive = true;
            _position = Vector3.zero;
            _themeColor = Color.blue;
            _tags = new string[] { "Tag1", "Tag2", "Tag3" };
            _scores = new List<int> { 100, 200, 300 };
            _nestedData = new NestedData();
            _nestedDataList = new List<NestedData>
            {
                new NestedData { Name = "Nested1", Value = 10 },
                new NestedData { Name = "Nested2", Value = 20 }
            };
            Save();
        }
    }
    
    /// <summary>
    /// Nested data class
    /// </summary>
    [Serializable]
    public class NestedData
    {
        [SerializeField] private string _name = "Nested Data";
        [SerializeField] private int _value = 0;
        [SerializeField] private DateTime _timestamp = DateTime.Now;
        
        public string Name
        {
            get => _name;
            set => _name = value;
        }
        
        public int Value
        {
            get => _value;
            set => _value = value;
        }
        
        public DateTime Timestamp
        {
            get => _timestamp;
            set => _timestamp = value;
        }
        
        public override string ToString()
        {
            return $"[{_name}: {_value}, Time: {_timestamp:HH:mm:ss}]";
        }
    }
}