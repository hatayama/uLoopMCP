using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using io.github.hatayama.uLoopMCP;
using Newtonsoft.Json.Linq;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// execute-dynamic-codeツールのusing文自動追加機能テスト
    /// Unity AI Assistant準拠のSyntaxTreeベース実装の動作確認
    /// </summary>
    public class ExecuteDynamicCodeUsingStatementTests
    {
        private ExecuteDynamicCodeTool _tool;
        
        [SetUp]
        public void Setup()
        {
            _tool = new ExecuteDynamicCodeTool();
        }
        
        [Test]
        public async System.Threading.Tasks.Task TestAssetDatabaseUsage_ShouldAutoAddUnityEditorUsing()
        {
            // Arrange
            string code = @"
                string[] allAssets = AssetDatabase.FindAssets(""t:Texture2D"");
                Debug.Log($""Found {allAssets.Length} textures"");
                return $""Found {allAssets.Length} texture assets"";
            ";
            
            var parameters = new ExecuteDynamicCodeSchema
            {
                Code = code,
                CompileOnly = false
            };
            
            // Act
            JObject paramsJson = JObject.FromObject(parameters);
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(paramsJson);
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;
            
            // Assert
            Assert.IsNotNull(response, "Response should be ExecuteDynamicCodeResponse type");
            Assert.IsTrue(response.Success, $"Execution should succeed. Error: {response.ErrorMessage}");
            Assert.IsNotNull(response.Result, "Result should not be null");
            Assert.That(response.Result, Does.Contain("texture assets"), "Result should contain texture count");
            Assert.IsEmpty(response.CompilationErrors, "There should be no compilation errors");
        }
        
        [Test]
        public async System.Threading.Tasks.Task TestSelectionAPI_ShouldAutoAddUnityEditorUsing()
        {
            // Arrange
            string code = @"
                GameObject selected = Selection.activeGameObject;
                if (selected != null)
                {
                    return $""Selected: {selected.name}"";
                }
                else
                {
                    return ""No GameObject selected"";
                }
            ";
            
            var parameters = new ExecuteDynamicCodeSchema
            {
                Code = code,
                CompileOnly = false
            };
            
            // Act
            JObject paramsJson = JObject.FromObject(parameters);
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(paramsJson);
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;
            
            // Assert
            Assert.IsNotNull(response, "Response should be ExecuteDynamicCodeResponse type");
            Assert.IsTrue(response.Success, $"Execution should succeed. Error: {response.ErrorMessage}");
            Assert.IsNotNull(response.Result, "Result should not be null");
            Assert.That(response.Result, Does.Contain("No GameObject selected").Or.Contain("Selected:"));
            Assert.IsEmpty(response.CompilationErrors, "There should be no compilation errors");
        }
        
        [Test]
        public async System.Threading.Tasks.Task TestEditorApplicationBeep_ShouldAutoAddUnityEditorUsing()
        {
            // Arrange
            string code = @"
                EditorApplication.Beep();
                return ""Beep executed"";
            ";
            
            var parameters = new ExecuteDynamicCodeSchema
            {
                Code = code,
                CompileOnly = false
            };
            
            // Act
            JObject paramsJson = JObject.FromObject(parameters);
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(paramsJson);
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;
            
            // Assert
            Assert.IsNotNull(response, "Response should be ExecuteDynamicCodeResponse type");
            Assert.IsTrue(response.Success, $"Execution should succeed. Error: {response.ErrorMessage}");
            Assert.AreEqual("Beep executed", response.Result, "Result should match expected value");
            Assert.IsEmpty(response.CompilationErrors, "There should be no compilation errors");
        }
        
        [Test]
        public async System.Threading.Tasks.Task TestEditorUtilityDisplayDialog_ShouldAutoAddUnityEditorUsing()
        {
            // Arrange
            string code = @"
                bool result = EditorUtility.DisplayDialog(
                    ""Test Dialog"", 
                    ""This is a test"", 
                    ""OK"", 
                    ""Cancel"");
                return $""Dialog result: {result}"";
            ";
            
            var parameters = new ExecuteDynamicCodeSchema
            {
                Code = code,
                CompileOnly = true  // Compile only to avoid dialog popup during test
            };
            
            // Act
            JObject paramsJson = JObject.FromObject(parameters);
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(paramsJson);
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;
            
            // Assert
            Assert.IsNotNull(response, "Response should be ExecuteDynamicCodeResponse type");
            Assert.IsTrue(response.Success, $"Compilation should succeed. Error: {response.ErrorMessage}");
            Assert.IsEmpty(response.CompilationErrors, "There should be no compilation errors");
        }
        
        [Test]
        public async System.Threading.Tasks.Task TestMultipleUnityEditorAPIs_ShouldAutoAddUsing()
        {
            // Arrange
            string code = @"
                // Multiple UnityEditor APIs in one code
                string[] prefabs = AssetDatabase.FindAssets(""t:Prefab"");
                GameObject selected = Selection.activeGameObject;
                EditorApplication.Beep();
                
                return $""Prefabs: {prefabs.Length}, Selected: {selected?.name ?? ""none""}"";
            ";
            
            var parameters = new ExecuteDynamicCodeSchema
            {
                Code = code,
                CompileOnly = false
            };
            
            // Act
            JObject paramsJson = JObject.FromObject(parameters);
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(paramsJson);
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;
            
            // Assert
            Assert.IsNotNull(response, "Response should be ExecuteDynamicCodeResponse type");
            Assert.IsTrue(response.Success, $"Execution should succeed. Error: {response.ErrorMessage}");
            Assert.IsNotNull(response.Result, "Result should not be null");
            Assert.That(response.Result, Does.Contain("Prefabs:"), "Result should contain prefab count");
            Assert.That(response.Result, Does.Contain("Selected:"), "Result should contain selection info");
            Assert.IsEmpty(response.CompilationErrors, "There should be no compilation errors");
        }
        
        [Test]
        public async System.Threading.Tasks.Task TestLinqUsage_ShouldAutoAddSystemLinqUsing()
        {
            // Arrange
            string code = @"
                int[] numbers = { 1, 2, 3, 4, 5 };
                var filtered = numbers.Where(n => n > 2).Select(n => n * 2);
                return string.Join("", "", filtered);
            ";
            
            var parameters = new ExecuteDynamicCodeSchema
            {
                Code = code,
                CompileOnly = false
            };
            
            // Act
            JObject paramsJson = JObject.FromObject(parameters);
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(paramsJson);
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;
            
            // Assert
            Assert.IsNotNull(response, "Response should be ExecuteDynamicCodeResponse type");
            Assert.IsTrue(response.Success, $"Execution should succeed. Error: {response.ErrorMessage}");
            Assert.AreEqual("6, 8, 10", response.Result, "Result should be filtered and transformed numbers");
            Assert.IsEmpty(response.CompilationErrors, "There should be no compilation errors");
        }
        
        [Test]
        public async System.Threading.Tasks.Task TestGenericCollections_ShouldAutoAddSystemCollectionsGenericUsing()
        {
            // Arrange
            string code = @"
                List<string> items = new List<string> { ""apple"", ""banana"", ""cherry"" };
                Dictionary<int, string> dict = new Dictionary<int, string>
                {
                    { 1, ""one"" },
                    { 2, ""two"" }
                };
                return $""List count: {items.Count}, Dict count: {dict.Count}"";
            ";
            
            var parameters = new ExecuteDynamicCodeSchema
            {
                Code = code,
                CompileOnly = false
            };
            
            // Act
            JObject paramsJson = JObject.FromObject(parameters);
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(paramsJson);
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;
            
            // Assert
            Assert.IsNotNull(response, "Response should be ExecuteDynamicCodeResponse type");
            Assert.IsTrue(response.Success, $"Execution should succeed. Error: {response.ErrorMessage}");
            Assert.AreEqual("List count: 3, Dict count: 2", response.Result);
            Assert.IsEmpty(response.CompilationErrors, "There should be no compilation errors");
        }
        
        [Test]
        public async System.Threading.Tasks.Task TestFileOperations_ShouldAutoAddSystemIOUsing()
        {
            // Arrange
            string code = @"
                string projectPath = Directory.GetCurrentDirectory();
                bool exists = File.Exists(Path.Combine(projectPath, ""README.md""));
                return $""Project path: {projectPath}, README exists: {exists}"";
            ";
            
            var parameters = new ExecuteDynamicCodeSchema
            {
                Code = code,
                CompileOnly = false
            };
            
            // Act
            JObject paramsJson = JObject.FromObject(parameters);
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(paramsJson);
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;
            
            // Assert
            Assert.IsNotNull(response, "Response should be ExecuteDynamicCodeResponse type");
            Assert.IsTrue(response.Success, $"Execution should succeed. Error: {response.ErrorMessage}");
            Assert.That(response.Result, Does.Contain("Project path:"));
            Assert.That(response.Result, Does.Contain("README exists:"));
            Assert.IsEmpty(response.CompilationErrors, "There should be no compilation errors");
        }
        
        [Test]
        public async System.Threading.Tasks.Task TestComplexCodeWithMultipleNamespaces_ShouldAutoAddAllRequiredUsings()
        {
            // Arrange
            string code = @"
                // 複数の名前空間が必要な複雑なコード
                List<GameObject> objects = new List<GameObject>();
                for (int i = 0; i < 3; i++)
                {
                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    objects.Add(cube);
                }
                
                string[] assets = AssetDatabase.FindAssets(""t:Material"");
                var names = objects.Select(o => o.name).ToArray();
                
                return $""Created {objects.Count} objects, Found {assets.Length} materials, Names: {string.Join("", "", names)}"";
            ";
            
            var parameters = new ExecuteDynamicCodeSchema
            {
                Code = code,
                CompileOnly = false
            };
            
            // Act
            JObject paramsJson = JObject.FromObject(parameters);
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(paramsJson);
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;
            
            // Assert
            Assert.IsNotNull(response, "Response should be ExecuteDynamicCodeResponse type");
            Assert.IsTrue(response.Success, $"Execution should succeed. Error: {response.ErrorMessage}");
            Assert.That(response.Result, Does.Contain("Created 3 objects"));
            Assert.That(response.Result, Does.Contain("materials"));
            Assert.That(response.Result, Does.Contain("Names:"));
            Assert.IsEmpty(response.CompilationErrors, "There should be no compilation errors");
            
            // Clean up created objects
            JObject cleanupParams = JObject.FromObject(new ExecuteDynamicCodeSchema
            {
                Code = @"
                    GameObject[] cubes = GameObject.FindObjectsOfType<GameObject>()
                        .Where(o => o.name.Contains(""Cube"")).ToArray();
                    foreach (var cube in cubes)
                    {
                        GameObject.DestroyImmediate(cube);
                    }
                    return $""Cleaned up {cubes.Length} objects"";
                ",
                CompileOnly = false
            });
            await _tool.ExecuteAsync(cleanupParams);
        }
        
        [Test]
        public async System.Threading.Tasks.Task TestCompileOnlyMode_ShouldNotExecuteButShouldCompile()
        {
            // Arrange
            string code = @"
                AssetDatabase.Refresh();
                Selection.activeObject = null;
                EditorApplication.Beep();
                return ""This should compile but not execute"";
            ";
            
            var parameters = new ExecuteDynamicCodeSchema
            {
                Code = code,
                CompileOnly = true
            };
            
            // Act
            JObject paramsJson = JObject.FromObject(parameters);
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(paramsJson);
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;
            
            // Assert
            Assert.IsNotNull(response, "Response should be ExecuteDynamicCodeResponse type");
            Assert.IsTrue(response.Success, $"Compilation should succeed. Error: {response.ErrorMessage}");
            Assert.IsEmpty(response.CompilationErrors, "There should be no compilation errors");
            // CompileOnly mode should not return execution result
            Assert.That(response.Result, Is.Null.Or.Empty.Or.EqualTo("Compilation completed successfully"));
        }
    }
}