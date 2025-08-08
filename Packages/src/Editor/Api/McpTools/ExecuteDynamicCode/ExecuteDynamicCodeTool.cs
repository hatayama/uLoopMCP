using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using io.github.hatayama.uLoopMCP;
using io.github.hatayama.uLoopMCP.DynamicExecution;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// MCP 動的C#コード実行ツール（薄いラッパー）

    /// 関連クラス: IDynamicCodeExecutor
    /// </summary>
    [McpTool(Description = "Execute C# code dynamically with security validation and timeout control")]
    public class ExecuteDynamicCodeTool : AbstractUnityTool<ExecuteDynamicCodeSchema, ExecuteDynamicCodeResponse>
    {
        private readonly global::io.github.hatayama.uLoopMCP.DynamicExecution.IDynamicCodeExecutor _executor;
        
        public override string ToolName => "execute-dynamic-code";
        
        public ExecuteDynamicCodeTool()
        {
            // 実際のDynamicCodeExecutor実装を使用
            _executor = global::io.github.hatayama.uLoopMCP.Factory.DynamicCodeExecutorFactory.CreateDefault();
        }
        
        /// <summary>
        /// テスト用コンストラクタ（依存性注入対応）
        /// </summary>
        public ExecuteDynamicCodeTool(global::io.github.hatayama.uLoopMCP.DynamicExecution.IDynamicCodeExecutor executor)
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
                
                // パラメータ配列に変換
                object[] parametersArray = null;
                if (parameters.Parameters != null && parameters.Parameters.Count > 0)
                {
                    parametersArray = parameters.Parameters.Values.ToArray();
                }
                
                // 実行（ExecuteCodeAsyncを使用）
                ExecutionResult executionResult = await _executor.ExecuteCodeAsync(
                    parameters.Code ?? "",
                    "DynamicCommand",
                    parametersArray,
                    cancellationToken
                );
                
                // レスポンスに変換
                ExecuteDynamicCodeResponse toolResponse = ConvertExecutionResultToResponse(executionResult, correlationId);
                
                // VibeLoggerで実行完了をログ
                VibeLogger.LogInfo(
                    "execute_dynamic_code_complete",
                    "Dynamic code execution completed",
                    new { 
                        correlationId,
                        success = executionResult.Success,
                        executionTimeMs = executionResult.ExecutionTime.TotalMilliseconds,
                        logsCount = executionResult.Logs?.Count ?? 0,
                        result_length = executionResult.Result?.Length ?? 0
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
        private ExecuteDynamicCodeResponse ConvertExecutionResultToResponse(ExecutionResult result, string correlationId)
        {
            ExecuteDynamicCodeResponse response = new ExecuteDynamicCodeResponse
            {
                Success = result.Success,
                Result = result.Result ?? "",
                Logs = result.Logs ?? new List<string>(),
                CompilationErrors = new List<CompilationErrorDto>(), // ExecutionResultからは取得不可
                ErrorMessage = result.ErrorMessage ?? "",
                ExecutionTimeMs = (long)result.ExecutionTime.TotalMilliseconds
            };

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
    
    // スタブ実装 - 一旦コンパイルを通すため
    public interface IDynamicCodeExecutor
    {
        Task<ExecutionResponse> ExecuteAsync(ExecutionRequest request);
    }
    
    public class ExecutionRequest
    {
        public string Code { get; set; } = "";
        public int TimeoutSeconds { get; set; } = 60;
        public bool CompileOnly { get; set; } = false;
        public Dictionary<string, object> Parameters { get; set; } = new();
    }
    
    public class ExecutionResponse
    {
        public bool Success { get; set; }
        public string Result { get; set; } = "";
        public List<string> Logs { get; set; } = new();
        public List<CompilationError> CompilationErrors { get; set; } = new();
        public string ErrorMessage { get; set; }
        public TimeSpan ExecutionTime { get; set; }
    }
    
    public class CompilationError
    {
        public string Message { get; set; } = "";
        public int Line { get; set; }
        public int Column { get; set; }
        public string ErrorCode { get; set; } = "";
    }

}