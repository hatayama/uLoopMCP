using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using io.github.hatayama.uLoopMCP;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// MCP 動的C#コード実行ツール（薄いラッパー）

    /// 関連クラス: IDynamicCodeExecutor
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
- Editor API missing? It auto-adds: using UnityEditor;
- Top-level error? Just write the code, no class wrapper needed!

Perfect for: GameObject creation, scene manipulation, editor tools, batch operations.

## Using Statements - Two Ways to Use:

1. **Simple Code (Recommended)**: Just write the logic, tool auto-wraps
   - var obj = new UnityEngine.GameObject(); return ""Created"";
   - Use fully-qualified names or let auto-fixing handle it

2. **Complete Class Definition**: Include full namespace + class structure  
   - using UnityEngine; namespace Test { public class MyClass { public object Execute() { ... } } }
   - Must include namespace, class, and Execute() method
   - Using statements go at the top, before namespace

AVOID: using statements in simple code - they'll be placed inside methods causing syntax errors\")]
    public class ExecuteDynamicCodeTool : AbstractUnityTool<ExecuteDynamicCodeSchema, ExecuteDynamicCodeResponse>
    {
        private readonly global::io.github.hatayama.uLoopMCP.IDynamicCodeExecutor _executor;
        private readonly global::io.github.hatayama.uLoopMCP.ImprovedErrorHandler _errorHandler;
        
        public override string ToolName => "execute-dynamic-code";
        
        public ExecuteDynamicCodeTool()
        {
            // 実際のDynamicCodeExecutor実装を使用
            _executor = global::io.github.hatayama.uLoopMCP.Factory.DynamicCodeExecutorFactory.CreateDefault();
            _errorHandler = new global::io.github.hatayama.uLoopMCP.ImprovedErrorHandler();
        }
        
        /// <summary>
        /// テスト用コンストラクタ（依存性注入対応）
        /// </summary>
        public ExecuteDynamicCodeTool(global::io.github.hatayama.uLoopMCP.IDynamicCodeExecutor executor)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }
        
        protected override async Task<ExecuteDynamicCodeResponse> ExecuteAsync(
            ExecuteDynamicCodeSchema parameters, 
            CancellationToken cancellationToken)
        {
            string correlationId = Guid.NewGuid().ToString("N")[..8];
            
            try
            {
                // VibeLoggerで実行開始をログ
                VibeLogger.LogInfo(
                    "execute_dynamic_code_start",
                    "Dynamic code execution started",
                    new { 
                        correlationId,
                        codeLength = parameters.Code?.Length ?? 0,
                        compileOnly = parameters.CompileOnly,
                        parametersCount = parameters.Parameters?.Count ?? 0
                    },
                    correlationId,
                    "Dynamic code execution request received",
                    "Monitor execution flow and performance"
                );
                
                // セキュリティチェック
                DynamicCodeSecurityLevel currentLevel = DynamicCodeSecurityManager.CurrentLevel;
                
                // Level 0: 実行完全禁止
                if (!DynamicCodeSecurityManager.CanExecute(currentLevel))
                {
                    return new ExecuteDynamicCodeResponse
                    {
                        Success = false,
                        Error = "Code execution is disabled at current security level (Disabled)",
                        UpdatedCode = parameters.Code,
                        SecurityLevel = currentLevel.ToString()
                    };
                }
                
                // Level 1: 危険APIチェック
                if (currentLevel == DynamicCodeSecurityLevel.Restricted)
                {
                    if (DynamicCodeSecurityManager.ContainsDangerousApi(parameters.Code))
                    {
                        return new ExecuteDynamicCodeResponse
                        {
                            Success = false,
                            Error = "Code contains dangerous APIs that are blocked at Restricted security level",
                            UpdatedCode = parameters.Code,
                            SecurityLevel = currentLevel.ToString()
                        };
                    }
                }
                
                // コード取得
                string originalCode = parameters.Code ?? "";

                // パラメータ配列に変換
                object[] parametersArray = null;
                if (parameters.Parameters != null && parameters.Parameters.Count > 0)
                {
                    parametersArray = parameters.Parameters.Values.ToArray();
                }
                
                // コード実行（RoslynCompilerが診断駆動修正を行う）
                ExecutionResult executionResult = await _executor.ExecuteCodeAsync(
                    originalCode, // オリジナルコードを使用（RoslynCompilerが修正を行う）
                    "DynamicCommand",
                    parametersArray,
                    cancellationToken,
                    parameters.CompileOnly
                );
                
                // レスポンスに変換（エラー時は改善されたメッセージを使用）
                ExecuteDynamicCodeResponse toolResponse = ConvertExecutionResultToResponse(
                    executionResult, originalCode, null, correlationId);
                
                // セキュリティレベルを追加
                toolResponse.SecurityLevel = currentLevel.ToString();
                
                // VibeLoggerで実行完了をログ
                VibeLogger.LogInfo(
                    "execute_dynamic_code_complete",
                    "Dynamic code execution completed",
                    new { 
                        correlationId,
                        success = executionResult.Success,
                        executionTimeMs = executionResult.ExecutionTime.TotalMilliseconds,
                        logsCount = executionResult.Logs?.Count ?? 0,
                        result_length = executionResult.Result?.ToString()?.Length ?? 0
                    },
                    correlationId,
                    $"Execution completed: {(executionResult.Success ? "Success" : "Failed")}",
                    "Check execution results and performance metrics"
                );
                
                return toolResponse;
            }
            catch (Exception ex)
            {
                // VibeLoggerでエラーをログ
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
                
                return CreateErrorResponse(ex.Message, correlationId);
            }
        }
        
        /// <summary>
        /// ExecutionResponseをExecuteDynamicCodeResponseに変換
        /// </summary>
        private ExecuteDynamicCodeResponse ConvertExecutionResultToResponse(
            ExecutionResult result, string originalCode, object fixResult, string correlationId)
        {
            ExecuteDynamicCodeResponse response = new ExecuteDynamicCodeResponse
            {
                Success = result.Success,
                Result = result.Result?.ToString() ?? "",
                Logs = result.Logs ?? new List<string>(),
                CompilationErrors = new List<CompilationErrorDto>(), // ExecutionResultからは取得不可
                ErrorMessage = result.ErrorMessage ?? "",
                ExecutionTimeMs = (long)result.ExecutionTime.TotalMilliseconds
            };

            // SmartCodeFixer削除済み - RoslynCompilerの診断駆動修正を使用

            // エラー時は改善されたメッセージを使用
            if (!result.Success)
            {
                // コンパイルエラーの場合、Logsからエラー情報を取得
                string actualErrorMessage = result.ErrorMessage ?? "";
                if (result.Logs?.Any() == true)
                {
                    actualErrorMessage = string.Join(" ", result.Logs);
                }
                
                global::io.github.hatayama.uLoopMCP.EnhancedErrorResponse enhancedError = 
                    _errorHandler.ProcessError(result, originalCode);
                
                // 分かりやすいエラーメッセージに置き換え
                response.ErrorMessage = enhancedError.FriendlyMessage;
                
                // 追加情報をLogsに追加
                if (response.Logs == null) response.Logs = new List<string>();
                
                // 元のエラーも残しておく
                response.Logs.Add($"元のエラー: {actualErrorMessage}");
                
                if (!string.IsNullOrEmpty(enhancedError.Explanation))
                {
                    response.Logs.Add($"説明: {enhancedError.Explanation}");
                }
                
                if (!string.IsNullOrEmpty(enhancedError.Example))
                {
                    response.Logs.Add($"例: {enhancedError.Example}");
                }
                
                if (enhancedError.SuggestedSolutions?.Any() == true)
                {
                    response.Logs.Add("解決策:");
                    foreach (string solution in enhancedError.SuggestedSolutions)
                    {
                        response.Logs.Add($"- {solution}");
                    }
                }
                
                if (enhancedError.LearningTips?.Any() == true)
                {
                    response.Logs.Add("ヒント:");
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

            // 例外が発生した場合のエラー情報追加
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
        /// コンパイルエラーをDTOに変換
        /// </summary>
        private List<CompilationErrorDto> ConvertCompilationErrors(List<CompilationError> errors)
        {
            if (errors == null) return new List<CompilationErrorDto>();
            
            return errors.Select(error => new CompilationErrorDto
            {
                Message = error.Message ?? "",
                Line = error.Line,
                Column = error.Column,
                ErrorCode = error.ErrorCode ?? ""
            }).ToList();
        }
        
        /// <summary>
        /// エラーレスポンス作成
        /// </summary>
        private ExecuteDynamicCodeResponse CreateErrorResponse(string errorMessage, string correlationId)
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