using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using uLoopMCP.DynamicExecution;
using UnityEngine;
using io.github.hatayama.uLoopMCP;

namespace uLoopMCP.DynamicExecution
{
    /// <summary>
    /// コンパイル済みコードの実行制御
    /// 設計ドキュメント: uLoopMCP_DynamicCodeExecution_Design.md
    /// 関連クラス: ICommandRunner, ExecutionContext, ExecutionResult
    /// </summary>
    public class CommandRunner : ICommandRunner
    {
        private bool _isRunning = false;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly object _lockObject = new();

        public bool IsRunning 
        { 
            get 
            { 
                lock (_lockObject) { return _isRunning; } 
            } 
        }

        public ExecutionResult Execute(ExecutionContext context)
        {
            string correlationId = Guid.NewGuid().ToString("N")[..8];
            
            lock (_lockObject)
            {
                if (_isRunning)
                {
                    return new ExecutionResult
                    {
                        Success = false,
                        ErrorMessage = "Another execution is already in progress",
                        ExecutionTime = TimeSpan.Zero
                    };
                }
                _isRunning = true;
                _cancellationTokenSource = new CancellationTokenSource();
            }

            try
            {
                VibeLogger.LogInfo(
                    "command_execution_start",
                    "Command execution started",
                    new { 
                        assemblyName = context.CompiledAssembly?.FullName ?? "null",
                        timeoutSeconds = context.TimeoutSeconds,
                        parameterCount = context.Parameters?.Count ?? 0
                    },
                    correlationId,
                    "Starting dynamic command execution",
                    "Monitor execution performance and timeout behavior"
                );

                var startTime = DateTime.Now;
                
                // タイムアウト設定
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(context.TimeoutSeconds));
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    _cancellationTokenSource.Token, 
                    timeoutCts.Token,
                    context.CancellationToken
                );

                // 実行
                var result = ExecuteInternal(context, combinedCts.Token, correlationId);
                
                var endTime = DateTime.Now;
                result.ExecutionTime = endTime - startTime;

                VibeLogger.LogInfo(
                    "command_execution_complete",
                    "Command execution completed",
                    new { 
                        success = result.Success,
                        executionTimeMs = result.ExecutionTime.TotalMilliseconds,
                        resultLength = result.Result?.Length ?? 0,
                        logCount = result.Logs?.Count ?? 0
                    },
                    correlationId,
                    $"Execution completed: {(result.Success ? "SUCCESS" : "FAILED")}",
                    "Track execution success patterns and performance"
                );

                return result;
            }
            catch (OperationCanceledException)
            {
                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = "Execution was cancelled or timed out",
                    ExecutionTime = TimeSpan.FromSeconds(context.TimeoutSeconds),
                    Logs = new List<string> { "Execution cancelled due to timeout" }
                };
            }
            catch (Exception ex)
            {
                VibeLogger.LogError(
                    "command_execution_error",
                    "Command execution failed with exception",
                    new { 
                        error = ex.Message,
                        stackTrace = ex.StackTrace
                    },
                    correlationId,
                    "Unexpected execution error occurred",
                    "Investigate execution failures and improve error handling"
                );

                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ExecutionTime = TimeSpan.Zero,
                    Logs = new List<string> { $"Exception: {ex.Message}" }
                };
            }
            finally
            {
                lock (_lockObject)
                {
                    _isRunning = false;
                    _cancellationTokenSource?.Dispose();
                    _cancellationTokenSource = null;
                }
            }
        }

        public void Cancel()
        {
            lock (_lockObject)
            {
                _cancellationTokenSource?.Cancel();
            }
        }

        private ExecutionResult ExecuteInternal(ExecutionContext context, CancellationToken cancellationToken, string correlationId)
        {
            if (context.CompiledAssembly == null)
            {
                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = "No compiled assembly provided",
                    ExecutionTime = TimeSpan.Zero
                };
            }

            try
            {
                // アセンブリから実行可能な型を探す
                var types = context.CompiledAssembly.GetTypes();
                Type targetType = null;
                MethodInfo executeMethod = null;

                // Executeメソッドを持つ型を探す
                foreach (var type in types)
                {
                    var method = type.GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance);
                    if (method != null)
                    {
                        targetType = type;
                        executeMethod = method;
                        break;
                    }
                }

                if (targetType == null || executeMethod == null)
                {
                    return new ExecutionResult
                    {
                        Success = false,
                        ErrorMessage = "No Execute method found in compiled assembly",
                        ExecutionTime = TimeSpan.Zero,
                        Logs = new List<string> { "Assembly types checked but no Execute method found" }
                    };
                }

                // インスタンス作成
                var instance = Activator.CreateInstance(targetType);
                if (instance == null)
                {
                    return new ExecutionResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to create instance of target type",
                        ExecutionTime = TimeSpan.Zero
                    };
                }

                // キャンセレーションチェック
                cancellationToken.ThrowIfCancellationRequested();

                // メソッド実行
                object executionResult;
                var parameters = executeMethod.GetParameters();
                
                if (parameters.Length == 0)
                {
                    // パラメータなし
                    executionResult = executeMethod.Invoke(instance, null);
                }
                else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(Dictionary<string, object>))
                {
                    // パラメータ辞書あり
                    executionResult = executeMethod.Invoke(instance, new object[] { context.Parameters });
                }
                else
                {
                    return new ExecutionResult
                    {
                        Success = false,
                        ErrorMessage = "Execute method signature not supported",
                        ExecutionTime = TimeSpan.Zero,
                        Logs = new List<string> { "Expected Execute() or Execute(Dictionary<string, object> parameters)" }
                    };
                }

                // 結果を文字列に変換
                string resultString = executionResult?.ToString() ?? "";

                return new ExecutionResult
                {
                    Success = true,
                    Result = resultString,
                    ExecutionTime = TimeSpan.Zero, // 呼び出し元で設定される
                    Logs = new List<string> { "Execution completed successfully" }
                };
            }
            catch (TargetInvocationException ex)
            {
                // 実際の例外を取得
                var innerException = ex.InnerException ?? ex;
                
                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = innerException.Message,
                    ExecutionTime = TimeSpan.Zero,
                    Logs = new List<string> 
                    { 
                        $"Target invocation exception: {innerException.Message}",
                        $"Stack trace: {innerException.StackTrace}"
                    }
                };
            }
            catch (Exception ex)
            {
                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ExecutionTime = TimeSpan.Zero,
                    Logs = new List<string> 
                    { 
                        $"Execution exception: {ex.Message}",
                        $"Stack trace: {ex.StackTrace}"
                    }
                };
            }
        }
    }
}