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
    /// 設計ドキュメント: uLoopMCP_DynamicCodeExecution_Design.md  
    /// 関連クラス: IDynamicCodeExecutor
    /// </summary>
    [McpTool(Description = "Execute C# code dynamically with security validation and timeout control")]
    public class ExecuteDynamicCodeTool : AbstractUnityTool<ExecuteDynamicCodeSchema, ExecuteDynamicCodeResponse>
    {
        private readonly IDynamicCodeExecutor _executor;
        
        public override string ToolName => "execute-dynamic-code";
        
        public ExecuteDynamicCodeTool()
        {
            // スタブ実装 - 後でDynamicCodeExecutorFactoryを実装
            _executor = new StubDynamicCodeExecutor();
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
                        dryRun = parameters.DryRun,
                        parametersCount = parameters.Parameters?.Count ?? 0
                    },
                    correlationId,
                    "Dynamic code execution request received",
                    "Monitor execution flow and performance"
                );
                
                // ExecutionRequestに変換
                var request = new ExecutionRequest
                {
                    Code = parameters.Code ?? "",
                    TimeoutSeconds = parameters.TimeoutSeconds,
                    DryRun = parameters.DryRun,
                    Parameters = parameters.Parameters ?? new Dictionary<string, object>()
                };
                
                // 実行
                var response = await _executor.ExecuteAsync(request);
                
                // レスポンスに変換
                var toolResponse = ConvertToToolResponse(response, correlationId);
                
                // VibeLoggerで実行完了をログ
                VibeLogger.LogInfo(
                    "execute_dynamic_code_complete",
                    "Dynamic code execution completed",
                    new { 
                        correlationId,
                        success = response.Success,
                        executionTimeMs = response.ExecutionTime.TotalMilliseconds,
                        logsCount = response.Logs?.Count ?? 0,
                        errorsCount = response.CompilationErrors?.Count ?? 0
                    },
                    correlationId,
                    $"Execution completed: {(response.Success ? "Success" : "Failed")}",
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
        private ExecuteDynamicCodeResponse ConvertToToolResponse(ExecutionResponse response, string correlationId)
        {
            return new ExecuteDynamicCodeResponse
            {
                Success = response.Success,
                Result = response.Result ?? "",
                Logs = response.Logs ?? new List<string>(),
                CompilationErrors = ConvertCompilationErrors(response.CompilationErrors),
                ErrorMessage = response.ErrorMessage,
                ExecutionTimeMs = (long)response.ExecutionTime.TotalMilliseconds
            };
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
                Id = error.Id ?? ""
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
        public bool DryRun { get; set; } = false;
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
        public string Id { get; set; } = "";
    }
    
    public class StubDynamicCodeExecutor : IDynamicCodeExecutor
    {
        public async Task<ExecutionResponse> ExecuteAsync(ExecutionRequest request)
        {
            await Task.Delay(100); // 実行時間をシミュレート
            
            return new ExecutionResponse
            {
                Success = true,
                Result = $"Stub execution completed for code: {request.Code[..Math.Min(50, request.Code.Length)]}...",
                Logs = new List<string> { "Stub: Code validation passed", "Stub: Execution simulated" },
                CompilationErrors = new List<CompilationError>(),
                ErrorMessage = null,
                ExecutionTime = TimeSpan.FromMilliseconds(100)
            };
        }
    }
}