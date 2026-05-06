using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace io.github.hatayama.UnityCliLoop
{
    public static class FindGameObjectsTestMenu
    {
        [MenuItem("UnityCliLoop/Debug/FindGameObjects Tests/Test Camera Search")]
        public static async void TestFindGameObjectsCamera()
        {
            FindGameObjectsTool tool = CreateTool();
            
            JObject parameters = new JObject
            {
                ["RequiredComponents"] = new JArray { "Camera" },
                ["MaxResults"] = 1,
                ["IncludeInheritedProperties"] = true
            };
            
            try
            {
                UnityCliLoopToolResponse response = await tool.ExecuteAsync(parameters);
                
                if (response is FindGameObjectsResponse findResponse)
                {
                    Debug.Log($"Found {findResponse.totalFound} objects with Camera");
                    
                    foreach (FindGameObjectResult result in findResponse.results)
                    {
                        Debug.Log($"- {result.name}: {result.components.Length} components");
                        
                        foreach (ComponentInfo component in result.components)
                        {
                            if (component.type == "Camera")
                            {
                                Debug.Log($"  Camera: {component.properties?.Length ?? 0} properties");
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error: {ex.Message}");
            }
        }
        
        [MenuItem("UnityCliLoop/Debug/FindGameObjects Tests/Test Main Camera by Path")]
        public static async void TestFindMainCameraByPath()
        {
            Debug.Log("[FindGameObjectsTestMenu] Starting Main Camera path search test...");
            
            FindGameObjectsTool tool = CreateTool();
            
            // Search for Main Camera by path
            JObject parameters = new JObject
            {
                ["NamePattern"] = "Main Camera",
                ["SearchMode"] = "Path",
                ["MaxResults"] = 1
            };
            
            try
            {
                Debug.Log("[FindGameObjectsTestMenu] Executing search for Main Camera...");
                UnityCliLoopToolResponse response = await tool.ExecuteAsync(parameters);
                
                if (response is FindGameObjectsResponse findResponse)
                {
                    Debug.Log($"[FindGameObjectsTestMenu] Found {findResponse.totalFound} objects");
                    
                    foreach (FindGameObjectResult result in findResponse.results)
                    {
                        Debug.Log($"[FindGameObjectsTestMenu] - {result.name} at {result.path}");
                        Debug.Log($"[FindGameObjectsTestMenu]   Components: {result.components.Length}");
                        
                        foreach (ComponentInfo component in result.components)
                        {
                            Debug.Log($"[FindGameObjectsTestMenu]   - {component.type}: {component.properties?.Length ?? 0} properties");
                        }
                    }
                }
                else
                {
                    Debug.LogError("[FindGameObjectsTestMenu] Unexpected response type");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FindGameObjectsTestMenu] Error: {ex.Message}");
                Debug.LogError($"[FindGameObjectsTestMenu] StackTrace: {ex.StackTrace}");
            }
            
            Debug.Log("[FindGameObjectsTestMenu] Test completed");
        }

        private static FindGameObjectsTool CreateTool()
        {
            FindGameObjectsTool tool = new FindGameObjectsTool();
            tool.InitializeHostServices(new FindGameObjectsDebugHostServices());
            return tool;
        }

        private sealed class FindGameObjectsDebugHostServices : IUnityCliLoopToolHostServices
        {
            public IUnityCliLoopConsoleLogService ConsoleLogs => throw new System.NotSupportedException();
            public IUnityCliLoopConsoleClearService ConsoleClear => throw new System.NotSupportedException();
            public IUnityCliLoopCompilationService Compilation => throw new System.NotSupportedException();
            public IUnityCliLoopDynamicCodeExecutionService DynamicCodeExecution => throw new System.NotSupportedException();
            public IUnityCliLoopHierarchyService Hierarchy => throw new System.NotSupportedException();
            public IUnityCliLoopTestExecutionService TestExecution => throw new System.NotSupportedException();
            public IUnityCliLoopGameObjectSearchService GameObjectSearch { get; } =
                new FindGameObjectsUseCase(new GameObjectFinderService(), new ComponentSerializer());
            public IUnityCliLoopScreenshotService Screenshot => throw new System.NotSupportedException();
            public IUnityCliLoopRecordInputService RecordInput => throw new System.NotSupportedException();
            public IUnityCliLoopReplayInputService ReplayInput => throw new System.NotSupportedException();
            public IUnityCliLoopKeyboardSimulationService KeyboardSimulation => throw new System.NotSupportedException();
            public IUnityCliLoopMouseInputSimulationService MouseInputSimulation => throw new System.NotSupportedException();
        }
    }
}
