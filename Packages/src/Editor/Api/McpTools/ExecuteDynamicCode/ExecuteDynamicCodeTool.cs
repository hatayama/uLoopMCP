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
    [McpTool(Description = @"Execute Unity C# code directly - no class/namespace needed!

CORRECT Examples:
- GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube); return ""Cube created"";  
- for(int x=0; x<10; x++) for(int z=0; z<10; z++) { /* create grid */ } return ""Grid done"";
- Camera.main.transform.position = new Vector3(5, 8, -8); return ""Camera positioned"";
- Material mat = new Material(Shader.Find(""Standard"")); mat.color = Color.red; return ""Material ready"";

AVOID These Patterns:
- Don't write: namespace MyNamespace { class MyClass { ... } }
- Don't write: public static void MyMethod() { ... }
- Don't forget: return statement at the end (return ""message"";)
- Don't use: Object.method() - use UnityEngine.Object.method() if ambiguous

Common Fixes:
- Missing return? Add: return ""Task completed"";
- Object ambiguous? Use: UnityEngine.Object.FindObjectsOfType<GameObject>()
- Top-level error? Just write the code, no class wrapper needed!

Perfect for: GameObject creation, scene manipulation, editor tools, batch operations.

## Using Statements - How It Works:

**AI-Generated Code**: If your code includes using statements, they will be:
- Automatically extracted from your code
- Moved outside the class wrapper to proper location
- Example: ""using UnityEngine; var obj = new GameObject();"" works correctly

**Simple Code (Recommended)**: Just write the logic without using statements
- Use fully-qualified names: UnityEngine.GameObject instead of GameObject
- Or let the system handle namespace resolution

**Note**: This tool extracts and relocates using statements from AI-generated code.
It does NOT auto-add missing using statements. If you get ""type not found"" errors,
either use fully-qualified names or include the necessary using statements in your code.")]
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