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
            // 設定から現在のセキュリティレベルを取得
            _currentSecurityLevel = McpEditorSettings.GetDynamicCodeSecurityLevel();
            
            // セキュリティレベルを明示的に指定してExecutorを作成
            _executor = Factory.DynamicCodeExecutorFactory.Create(_currentSecurityLevel);
            _errorHandler = new ImprovedErrorHandler();
            
            VibeLogger.LogInfo(
                "execute_dynamic_code_tool_initialized",
                $"ExecuteDynamicCodeTool initialized with security level: {_currentSecurityLevel}",
                new { securityLevel = _currentSecurityLevel.ToString() },
                correlationId: McpConstants.GenerateCorrelationId(),
                humanNote: "Tool initialized with explicit security level",
                aiTodo: "Monitor security level consistency"
            );
#else
            // Roslyn無効時はnull（このツール自体が登録されないはず）
            _executor = null;
            _errorHandler = null;
            _currentSecurityLevel = DynamicCodeSecurityLevel.Disabled;
#endif
        }
        
        /// <summary>
        /// テスト用コンストラクタ（依存性注入対応）
        /// </summary>
        public ExecuteDynamicCodeTool(IDynamicCodeExecutor executor)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }
        
        protected override async Task<ExecuteDynamicCodeResponse> ExecuteAsync(
            ExecuteDynamicCodeSchema parameters, 
            CancellationToken cancellationToken)
        {
            string correlationId = McpConstants.GenerateCorrelationId();
            
            try
            {
#if ULOOPMCP_HAS_ROSLYN
                // セキュリティレベルが変更されている場合はExecutorを再作成
                DynamicCodeSecurityLevel currentLevel = McpEditorSettings.GetDynamicCodeSecurityLevel();
                if (currentLevel != _currentSecurityLevel)
                {
                    VibeLogger.LogInfo(
                        "execute_dynamic_code_recreating_executor",
                        $"Security level changed from {_currentSecurityLevel} to {currentLevel}, recreating executor",
                        new { 
                            oldLevel = _currentSecurityLevel.ToString(),
                            newLevel = currentLevel.ToString()
                        },
                        correlationId,
                        "Recreating executor due to security level change",
                        "Monitor security level change frequency"
                    );
                    
                    _currentSecurityLevel = currentLevel;
                    _executor = Factory.DynamicCodeExecutorFactory.Create(_currentSecurityLevel);
                }
#endif
                
                // VibeLoggerで実行開始をログ
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
                
                // Level 0: 実行完全禁止
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
                
                // Level 1: Restrictedモードでは、Roslynベースの検証に委譲
                // 正規表現ベースのチェックは削除し、RoslynCompilerのSecurityValidatorが処理する
                // これにより、ユーザー定義クラス（Assembly-CSharp）も適切に処理される
                
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
                
                EnhancedErrorResponse enhancedError = 
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