using UnityEngine;
using UnityEditor;

using io.github.hatayama.UnityCliLoop.Application;
using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.Runtime;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Dev
{
    public class LogGetterTestHelper
    {
        [MenuItem("UnityCliLoop/Debug/LogGetter Tests/Output Test Logs")]
        public static void OutputTestLogs()
        {
            Debug.Log("This is a normal log.");
            Debug.LogWarning("This is a warning log.");
            Debug.LogError("This is an error log.");
            
            Debug.Log("This is a log with a stack trace\nUnityEngine.Debug:Log(Object)\nLogGetterTestHelper:OutputTestLogs() (at Assets/Editor/LogGetter/LogGetterTestHelper.cs:12)");
            
            Debug.LogException(new System.Exception("This is a test exception."));
            
            Debug.Log("LogGetter test complete!");
            
            Debug.Log("Additional Log 1: Informational message");
            Debug.Log("Additional Log 2: Debug information");
            Debug.LogWarning("Additional Warning: Caution is necessary");
            Debug.LogError("Additional Error: A problem has occurred");
        }
    }
} 