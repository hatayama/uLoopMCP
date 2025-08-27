using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// MCP Dynamic C# Code Execution Tool
    /// Regenerates Executor only when security level changes, otherwise caches and reuses
    /// Related Classes: IDynamicCodeExecutor, DynamicCodeExecutorFactory
    /// </summary>
    [McpTool(Description = @"<tool>
<name>ExecuteDynamicCode</name>
<purpose>Automate Unity Editor operations programmatically - NOT for runtime game code</purpose>
<primary_use>Execute editor automation tasks that users would normally do manually in Unity Editor</primary_use>

<important_note>
  <editor_automation_only>
    This tool is designed for EDITOR AUTOMATION, not for writing runtime game logic.
    Use this to:
    • Create and configure GameObjects in the scene
    • Set up test environments  
    • Batch process assets
    • Automate repetitive Editor tasks
    • Prototype scene layouts quickly
    
    NOT for:
    • Writing gameplay code (use .cs files instead)
    • Creating runtime systems (use proper MonoBehaviours)
    • Implementing game features (belongs in source files)
  </editor_automation_only>
</important_note>

<critical_workflow>
  <monobehaviour_components>
    <requirement>Creating NEW MonoBehaviour components requires compilation</requirement>
    <steps>
      <step order=""1"">Use Write tool to create .cs file</step>
      <step order=""2"">Use mcp compile tool with ForceRecompile=false (MANDATORY - verify no errors)</step>
      <step order=""3"">Use this tool to attach: gameObject.AddComponent&lt;YourScript&gt;()</step>
    </steps>
    <compile_tool_usage>
      <correct>mcp__uLoopMCP__compile with ForceRecompile=false - Returns error/warning count</correct>
      <incorrect>ForceRecompile=true - Returns indeterminate result, cannot verify compilation</incorrect>
      <important>Always check ErrorCount=0 before proceeding to AddComponent</important>
    </compile_tool_usage>
    <common_failure>
      <symptom>Type.GetType(""YourScript, Assembly-CSharp"") returns null</symptom>
      <cause>Script not compiled into assembly</cause>
      <solution>Run mcp__uLoopMCP__compile with ForceRecompile=false and verify ErrorCount=0</solution>
    </common_failure>
  </monobehaviour_components>
</critical_workflow>

<valid_patterns>
  <pattern>GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube); return ""Created"";</pattern>
  <pattern>Camera.main.transform.position = Vector3.zero; return ""Done"";</pattern>
  <pattern>Material mat = new Material(Shader.Find(""Standard"")); return ""Ready"";</pattern>
</valid_patterns>

<invalid_patterns>
  <pattern type=""missing_return"">
    <code>GameObject.CreatePrimitive(PrimitiveType.Cube);</code>
    <reason>Must include return statement</reason>
  </pattern>
  <note>Cannot define classes, namespaces, or methods - only direct code execution</note>
</invalid_patterns>

<error_solutions>
  <error type=""component_not_found"">
    <solution>Verify compiled with compile tool first</solution>
  </error>
  <note>Use fully-qualified names for ambiguous types (e.g., UnityEngine.Object)</note>
</error_solutions>

<inspector_references>
  <critical>SerializeField references need SerializedObject for persistence</critical>
  <persistent_method>SerializedObject so = new SerializedObject(component); SerializedProperty prop = so.FindProperty(""fieldName""); prop.objectReferenceValue = value; so.ApplyModifiedProperties(); EditorUtility.SetDirty(component);</persistent_method>
  <note>Use field name (cameraHolder) not display name (Camera Holder) in FindProperty</note>
</inspector_references>

<preferred_utils>
  <item>SerializedBindingUtil: Persistent bindings for Object/Float/Int/Bool/Color/Vector3/List with automatic dirty handling</item>
  <item>ExpressionBindingUtil: Strongly-typed field selection via lambda expressions (compile-time name safety)</item>
  <item>FieldName: Extract field name string from lambda for logs and assertions</item>
  <item>PrefabEditUtil: Safe prefab load/edit/save/unload wrapper</item>
  <item>DirtyUtil: Mark objects and scenes dirty consistently</item>
  <item>ValidationUtil: Assert required references and append operation summary to logs</item>
  <item>DryRunContext & OperationSummary: Dry-run without state mutation and collect success/failure report</item>
</preferred_utils>

<usage_examples>
  <example title=""Expression-based binding (recommended)"">
    ExpressionBindingUtil.BindObject(playerController, c => c.cameraPivot, cameraGO, new DryRunContext(false), new OperationSummary());
  </example>
  <example title=""Numeric binding with expressions"">
    ExpressionBindingUtil.BindFloat(enemy, c => c.moveSpeed, 3.0f);
  </example>
  <example title=""Prefab edit wrapper"">
    PrefabEditUtil.WithLoadedPrefab(""Assets/Prefabs/Enemy.prefab"", root => {
      var comp = root.GetComponent<MyEnemy>();
      ExpressionBindingUtil.BindInt(comp, c => c.level, 2);
    });
  </example>
  <example title=""Validation and summary to logs"">
    var summary = new OperationSummary();
    ValidationUtil.AssertNotNull(cameraGO, ""Main Camera"", summary);
    UnityEngine.Debug.Log(summary.BuildReport());
  </example>
  <example title=""Dry-run (no state changes)"">
    var dry = new DryRunContext(true);
    var summary = new OperationSummary();
    SerializedBindingUtil.BindFloat(comp, ""speed"", 5f, dry, summary);
    UnityEngine.Debug.Log(summary.BuildReport());
  </example>
</usage_examples>

</tool>")]
    public class ExecuteDynamicCodeTool : AbstractUnityTool<ExecuteDynamicCodeSchema, ExecuteDynamicCodeResponse>
    {
        private IDynamicCodeExecutor _executor;
        private readonly ImprovedErrorHandler _errorHandler;
        private DynamicCodeSecurityLevel _currentSecurityLevel;
        
        public override string ToolName => "execute-dynamic-code";
        
        public ExecuteDynamicCodeTool()
        {
#if ULOOPMCP_HAS_ROSLYN
            _executor = null;
            _errorHandler = new ImprovedErrorHandler();
            // Set initial value to an invalid value (will always be recreated on the first request)
            _currentSecurityLevel = (DynamicCodeSecurityLevel)(-1);
            
            VibeLogger.LogInfo(
                "execute_dynamic_code_tool_initialized",
                "ExecuteDynamicCodeTool initialized with conditional caching",
                new { },
                correlationId: McpConstants.GenerateCorrelationId(),
                humanNote: "Tool initialized with executor caching for performance",
                aiTodo: "Monitor executor lifecycle and cache hit rate"
            );
#else
            // Null when Roslyn is disabled
            _executor = null;
            _errorHandler = null;
            _currentSecurityLevel = DynamicCodeSecurityLevel.Disabled;
#endif
        }
        
        protected override async Task<ExecuteDynamicCodeResponse> ExecuteAsync(
            ExecuteDynamicCodeSchema parameters, 
            CancellationToken cancellationToken)
        {
            string correlationId = McpConstants.GenerateCorrelationId();
            
            try
            {
#if ULOOPMCP_HAS_ROSLYN
                DynamicCodeSecurityLevel editorLevel = McpEditorSettings.GetDynamicCodeSecurityLevel();
                
                // Recreate Executor only when editor settings change (cache for performance)
                if (_executor == null || editorLevel != _currentSecurityLevel)
                {
                    string action = _executor == null ? "Creating" : "Recreating";
                    
                    VibeLogger.LogInfo(
                        "execute_dynamic_code_executor_init",
                        $"{action} executor with editor security level: {editorLevel}",
                        new { 
                            action = action.ToLower(),
                            oldLevel = _currentSecurityLevel.ToString(),
                            newLevel = editorLevel.ToString(),
                            source = "EditorSettings"  // From editor settings, not parameters
                        },
                        correlationId,
                        $"{action} executor from editor settings",
                        "Monitor executor lifecycle and security level changes"
                    );
                    
                    _currentSecurityLevel = editorLevel;
                    _executor = Factory.DynamicCodeExecutorFactory.Create(_currentSecurityLevel);
                }
#endif
                
                // Log execution start with VibeLogger
                VibeLogger.LogInfo(
                    "execute_dynamic_code_start",
                    "Dynamic code execution started",
                    new { 
                        correlationId,
                        codeLength = parameters.Code?.Length ?? 0,
                        compileOnly = parameters.CompileOnly,
                        parametersCount = parameters.Parameters?.Count ?? 0,
                        securityLevel = _currentSecurityLevel.ToString()
                    },
                    correlationId,
                    "Dynamic code execution request received",
                    "Monitor execution flow and performance"
                );
                
                // Level 0: Execution completely prohibited
                if (!DynamicCodeSecurityManager.CanExecute(_currentSecurityLevel))
                {
                    return new ExecuteDynamicCodeResponse
                    {
                        Success = false,
                        Error = "Code execution is disabled at current security level (Disabled)",
                        UpdatedCode = parameters.Code,
                        SecurityLevel = _currentSecurityLevel.ToString()
                    };
                }
                
                // Level 1: In Restricted mode, delegate to Roslyn-based validation
                // Remove regex-based checks, SecurityValidator of RoslynCompiler handles this
                // This allows proper handling of user-defined classes (Assembly-CSharp)
                
                // Retrieve code
                string originalCode = parameters.Code ?? "";

                // Convert to parameter array
                object[] parametersArray = null;
                if (parameters.Parameters != null && parameters.Parameters.Count > 0)
                {
                    parametersArray = parameters.Parameters.Values.ToArray();
                }
                
                // Code execution (RoslynCompiler performs diagnostic-driven modifications)
                ExecutionResult executionResult = await _executor.ExecuteCodeAsync(
                    originalCode, // Use original code (RoslynCompiler will perform modifications)
                    "DynamicCommand",
                    parametersArray,
                    cancellationToken,
                    parameters.CompileOnly
                );
                
                // Convert to response (use improved message on error)
                ExecuteDynamicCodeResponse toolResponse = ConvertExecutionResultToResponse(
                    executionResult, originalCode, correlationId);
                
                // Add security level
                toolResponse.SecurityLevel = _currentSecurityLevel.ToString();
                
                // Log execution completion with VibeLogger
                VibeLogger.LogInfo(
                    "execute_dynamic_code_complete",
                    "Dynamic code execution completed",
                    new { 
                        correlationId,
                        success = executionResult.Success,
                        executionTimeMs = executionResult.ExecutionTime.TotalMilliseconds,
                        logsCount = executionResult.Logs?.Count ?? 0,
                        result_length = executionResult.Result?.ToString().Length ?? 0
                    },
                    correlationId,
                    $"Execution completed: {(executionResult.Success ? "Success" : "Failed")}",
                    "Check execution results and performance metrics"
                );
                
                return toolResponse;
            }
            catch (Exception ex)
            {
                // Log error with VibeLogger
                VibeLogger.LogError(
                    "execute_dynamic_code_error",
                    "Dynamic code execution failed with exception",
                    new { 
                        correlationId,
                        error = ex.Message,
                        stackTrace = ex.StackTrace
                    },
                    correlationId,
                    "Unexpected error during dynamic code execution",
                    "Investigate error cause and improve error handling"
                );
                
                return CreateErrorResponse(ex.Message);
            }
        }
        
        /// <summary>
        /// Convert ExecutionResponse to ExecuteDynamicCodeResponse
        /// </summary>
        private ExecuteDynamicCodeResponse ConvertExecutionResultToResponse(
            ExecutionResult result, string originalCode, string correlationId)
        {
            ExecuteDynamicCodeResponse response = new ExecuteDynamicCodeResponse
            {
                Success = result.Success,
                Result = result.Result?.ToString() ?? "",
                Logs = result.Logs ?? new List<string>(),
                CompilationErrors = new List<CompilationErrorDto>(), // Cannot be retrieved from ExecutionResult
                ErrorMessage = result.ErrorMessage ?? "",
                ExecutionTimeMs = (long)result.ExecutionTime.TotalMilliseconds
            };

            // Use improved message on error
            if (!result.Success)
            {
                // In case of compilation error, retrieve error information from Logs
                string actualErrorMessage = result.ErrorMessage ?? "";
                if (result.Logs?.Any() == true)
                {
                    actualErrorMessage = string.Join(" ", result.Logs);
                }
                
                EnhancedErrorResponse enhancedError = 
                    _errorHandler.ProcessError(result, originalCode);
                
                // Replace with a more understandable error message
                response.ErrorMessage = enhancedError.FriendlyMessage;
                
                // Add additional information to Logs
                if (response.Logs == null) response.Logs = new List<string>();
                
                // Keep the original error as well
                response.Logs.Add($"Original Error: {actualErrorMessage}");
                
                if (!string.IsNullOrEmpty(enhancedError.Explanation))
                {
                    response.Logs.Add($"Explanation: {enhancedError.Explanation}");
                }
                
                if (!string.IsNullOrEmpty(enhancedError.Example))
                {
                    response.Logs.Add($"Example: {enhancedError.Example}");
                }
                
                if (enhancedError.SuggestedSolutions?.Any() == true)
                {
                    response.Logs.Add("Solutions:");
                    foreach (string solution in enhancedError.SuggestedSolutions)
                    {
                        response.Logs.Add($"- {solution}");
                    }
                }
                
                if (enhancedError.LearningTips?.Any() == true)
                {
                    response.Logs.Add("Tips:");
                    foreach (string tip in enhancedError.LearningTips)
                    {
                        response.Logs.Add($"- {tip}");
                    }
                }
                
                VibeLogger.LogInfo(
                    "enhanced_error_response",
                    "Enhanced error message generated",
                    new
                    {
                        original_error = actualErrorMessage,
                        friendly_message = enhancedError.FriendlyMessage,
                        severity = enhancedError.Severity.ToString(),
                        solutions_count = enhancedError.SuggestedSolutions?.Count ?? 0
                    },
                    correlationId,
                    "Error message enhanced with user-friendly explanation",
                    "Monitor error message effectiveness"
                );
            }

            // Add error information when an exception occurs
            if (result.Exception != null)
            {
                if (response.Logs == null) response.Logs = new List<string>();
                response.Logs.Add($"Exception: {result.Exception.Message}");
                if (!string.IsNullOrEmpty(result.Exception.StackTrace))
                {
                    response.Logs.Add($"Stack Trace: {result.Exception.StackTrace}");
                }
            }

            return response;
        }
        
        /// <summary>
        /// Create error response
        /// </summary>
        private ExecuteDynamicCodeResponse CreateErrorResponse(string errorMessage)
        {
            return new ExecuteDynamicCodeResponse
            {
                Success = false,
                Result = "",
                Logs = new List<string>(),
                CompilationErrors = new List<CompilationErrorDto>(),
                ErrorMessage = errorMessage ?? "Unknown error occurred",
                ExecutionTimeMs = 0
            };
        }
    }
}