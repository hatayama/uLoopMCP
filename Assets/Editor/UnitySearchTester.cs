using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;
using io.github.hatayama.uLoopMCP;
using Newtonsoft.Json.Linq;

/// <summary>
/// Manual testing window for UnitySearchTool
/// Provides a simple UI to test various search scenarios
/// </summary>
public class UnitySearchTester : EditorWindow
{
    private string _searchQuery = "*.cs";
    private int _maxResults = 10;
    private bool _saveToFile = false;
    private string _lastResult = "";
    private Vector2 _scrollPosition;
    
    [MenuItem("uLoopMCP/Windows/Unity Search Tester")]
    public static void ShowWindow()
    {
        GetWindow<UnitySearchTester>("Unity Search Tester");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Unity Search Command Tester", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        // Input fields
        _searchQuery = EditorGUILayout.TextField("Search Query:", _searchQuery);
        _maxResults = EditorGUILayout.IntField("Max Results:", _maxResults);
        _saveToFile = EditorGUILayout.Toggle("Save to File:", _saveToFile);
        
        GUILayout.Space(10);
        
        // Test buttons
        if (GUILayout.Button("Test Basic Search"))
        {
            TestBasicSearchAsync().Forget();
        }
        
        if (GUILayout.Button("Test File Extension Filter"))
        {
            TestFileExtensionFilterAsync().Forget();
        }
        
        if (GUILayout.Button("Test Asset Type Filter"))
        {
            TestAssetTypeFilterAsync().Forget();
        }
        
        if (GUILayout.Button("Test Empty Query (Should Fail)"))
        {
            TestEmptyQueryAsync().Forget();
        }
        
        GUILayout.Space(10);
        
        // Results display
        GUILayout.Label("Last Result:", EditorStyles.boldLabel);
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));
        EditorGUILayout.TextArea(_lastResult, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }
    
    private async Task TestBasicSearchAsync()
    {
        UnitySearchTool tool = new UnitySearchTool();
        UnitySearchSchema schema = new UnitySearchSchema
        {
            SearchQuery = _searchQuery,
            MaxResults = _maxResults,
            SaveToFile = _saveToFile
        };
        
        await ExecuteTestAsync(tool, schema, "Basic Search");
    }
    
    private async Task TestFileExtensionFilterAsync()
    {
        UnitySearchTool tool = new UnitySearchTool();
        UnitySearchSchema schema = new UnitySearchSchema
        {
            SearchQuery = "*",
            FileExtensions = new string[] { "cs" },
            MaxResults = _maxResults,
            SaveToFile = _saveToFile
        };
        
        await ExecuteTestAsync(tool, schema, "File Extension Filter");
    }
    
    private async Task TestAssetTypeFilterAsync()
    {
        UnitySearchTool tool = new UnitySearchTool();
        UnitySearchSchema schema = new UnitySearchSchema
        {
            SearchQuery = "*",
            AssetTypes = new string[] { "MonoScript" },
            MaxResults = _maxResults,
            SaveToFile = _saveToFile
        };
        
        await ExecuteTestAsync(tool, schema, "Asset Type Filter");
    }
    
    private async Task TestEmptyQueryAsync()
    {
        UnitySearchTool tool = new UnitySearchTool();
        UnitySearchSchema schema = new UnitySearchSchema
        {
            SearchQuery = "",
            MaxResults = _maxResults
        };
        
        await ExecuteTestAsync(tool, schema, "Empty Query Test");
    }
    
    private async Task ExecuteTestAsync(UnitySearchTool tool, UnitySearchSchema schema, string testName)
    {
        try
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(schema);
            JToken token = JToken.Parse(json);
            
            BaseToolResponse baseResponse = await tool.ExecuteAsync(token);
            UnitySearchResponse response = baseResponse as UnitySearchResponse;
            
            if (response != null)
            {
                string result = $"=== {testName} ===\n";
                result += $"Success: {response.Success}\n";
                result += $"Total Count: {response.TotalCount}\n";
                result += $"Displayed Count: {response.DisplayedCount}\n";
                result += $"Search Duration: {response.SearchDurationMs}ms\n";
                result += $"Results Saved to File: {response.ResultsSavedToFile}\n";
                
                if (response.ResultsSavedToFile)
                {
                    result += $"File Path: {response.ResultsFilePath}\n";
                    result += $"Save Reason: {response.SaveToFileReason}\n";
                }
                
                if (!response.Success)
                {
                    result += $"Error: {response.ErrorMessage}\n";
                }
                
                if (!response.ResultsSavedToFile && response.Results != null)
                {
                    result += $"\nFirst few results:\n";
                    for (int i = 0; i < Mathf.Min(3, response.Results.Length); i++)
                    {
                        SearchResultItem item = response.Results[i];
                        result += $"- {item.Label} ({item.Type}) - {item.Path}\n";
                    }
                }
                
                _lastResult = result;
                Debug.Log($"Unity Search Test: {testName} completed - {(response.Success ? "SUCCESS" : "FAILED")}");
            }
            else
            {
                _lastResult = $"=== {testName} ===\nFailed to cast response to UnitySearchResponse";
                Debug.LogError($"Unity Search Test: {testName} - Failed to cast response");
            }
        }
        catch (System.Exception ex)
        {
            _lastResult = $"=== {testName} ===\nException: {ex.Message}\nStackTrace: {ex.StackTrace}";
            Debug.LogError($"Unity Search Test: {testName} - Exception: {ex.Message}");
        }
        
        Repaint();
    }
} 