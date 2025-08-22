using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using io.github.hatayama.uLoopMCP;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// コンパイル済みコードの実行制御

    /// 関連クラス: ExecutionContext, ExecutionResult
    /// </summary>
    public class CommandRunner
    {
        private bool _isRunning = false;
        private CancellationTokenSource _cancellationTokenSource;

        public bool IsRunning => _isRunning;

        private static void LogExecutionStart(ExecutionContext context, string correlationId)
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
        }

        private static void LogExecutionComplete(ExecutionResult result, string correlationId)
        {
            VibeLogger.LogInfo(
                "command_execution_complete",
                "Command execution completed",
                new { 
                    success = result.Success,
                    executionTimeMs = result.ExecutionTime.TotalMilliseconds,
                    resultLength = result.Result?.ToString()?.Length ?? 0,
                    logCount = result.Logs?.Count ?? 0
                },
                correlationId,
                $"Execution completed: {(result.Success ? "SUCCESS" : "FAILED")}",
                "Track execution success patterns and performance"
            );
        }

        private static void LogExecutionError(Exception ex, string correlationId)
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
        }

        private CancellationTokenSource CreateCombinedCancellationTokenSource(ExecutionContext context)
        {
            CancellationTokenSource timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(context.TimeoutSeconds));
            return CancellationTokenSource.CreateLinkedTokenSource(
                _cancellationTokenSource.Token,
                timeoutCts.Token,
                context.CancellationToken
            );
        }

        public ExecutionResult Execute(ExecutionContext context)
        {
            string correlationId = McpConstants.GenerateCorrelationId();
            
            if (_isRunning)
            {
                return CreateErrorResult(McpConstants.ERROR_MESSAGE_EXECUTION_IN_PROGRESS);
            }
            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                LogExecutionStart(context, correlationId);

                DateTime startTime = DateTime.Now;
                
                // タイムアウト設定
                using CancellationTokenSource combinedCts = CreateCombinedCancellationTokenSource(context);

                // 実行
                ExecutionResult result = ExecuteInternal(context, combinedCts.Token, correlationId);
                
                DateTime endTime = DateTime.Now;
                result.ExecutionTime = endTime - startTime;

                LogExecutionComplete(result, correlationId);

                return result;
            }
            catch (OperationCanceledException)
            {
                ExecutionResult cancelResult = CreateErrorResult(
                    McpConstants.ERROR_MESSAGE_EXECUTION_CANCELLED,
                    new List<string> { "Execution cancelled due to timeout" });
                cancelResult.ExecutionTime = TimeSpan.FromSeconds(context.TimeoutSeconds);
                return cancelResult;
            }
            catch (Exception ex)
            {
                LogExecutionError(ex, correlationId);

                return CreateErrorResult(
                    ex.Message,
                    new List<string> { $"Exception: {ex.Message}" });
            }
            finally
            {
                _isRunning = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        public void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }

        private static ExecutionResult CreateErrorResult(string errorMessage, List<string> logs = null)
        {
            return new ExecutionResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                ExecutionTime = TimeSpan.Zero,
                Logs = logs ?? new List<string>()
            };
        }

        private static ExecutionResult CreateSuccessResult(string result, List<string> logs = null)
        {
            return new ExecutionResult
            {
                Success = true,
                Result = result,
                ExecutionTime = TimeSpan.Zero, // 呼び出し元で設定される
                Logs = logs ?? new List<string> { "Execution completed successfully" }
            };
        }

        private static (Type targetType, MethodInfo executeMethod) FindExecuteMethod(Assembly assembly)
        {
            Type[] types = assembly.GetTypes();
            
            foreach (Type type in types)
            {
                MethodInfo method = type.GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance);
                if (method != null)
                {
                    return (type, method);
                }
            }
            
            return (null, null);
        }

        private static object CreateInstance(Type targetType)
        {
            return Activator.CreateInstance(targetType);
        }

        private static object InvokeExecuteMethod(MethodInfo executeMethod, object instance, Dictionary<string, object> parameters)
        {
            System.Reflection.ParameterInfo[] methodParameters = executeMethod.GetParameters();
            
            if (methodParameters.Length == 0)
            {
                // パラメータなし
                return executeMethod.Invoke(instance, null);
            }
            else if (methodParameters.Length == 1 && methodParameters[0].ParameterType == typeof(Dictionary<string, object>))
            {
                // パラメータ辞書あり
                return executeMethod.Invoke(instance, new object[] { parameters });
            }
            else
            {
                throw new NotSupportedException("Expected Execute() or Execute(Dictionary<string, object> parameters)");
            }
        }

        private ExecutionResult ExecuteInternal(ExecutionContext context, CancellationToken cancellationToken, string correlationId)
        {
            if (context.CompiledAssembly == null)
            {
                return CreateErrorResult(McpConstants.ERROR_MESSAGE_NO_COMPILED_ASSEMBLY);
            }

            try
            {
                // アセンブリから実行可能な型を探す
                (Type targetType, MethodInfo executeMethod) = FindExecuteMethod(context.CompiledAssembly);
                
                if (targetType == null || executeMethod == null)
                {
                    return CreateErrorResult(
                        McpConstants.ERROR_MESSAGE_NO_EXECUTE_METHOD,
                        new List<string> { "Assembly types checked but no Execute method found" });
                }

                // インスタンス作成
                object instance = CreateInstance(targetType);
                if (instance == null)
                {
                    return CreateErrorResult(McpConstants.ERROR_MESSAGE_FAILED_TO_CREATE_INSTANCE);
                }

                // キャンセレーションチェック
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // メソッド実行
                    object executionResult = InvokeExecuteMethod(executeMethod, instance, context.Parameters);
                    
                    // 結果を文字列に変換
                    string resultString = executionResult?.ToString() ?? "";
                    
                    return CreateSuccessResult(resultString);
                }
                catch (NotSupportedException ex)
                {
                    return CreateErrorResult(
                        McpConstants.ERROR_MESSAGE_UNSUPPORTED_SIGNATURE,
                        new List<string> { ex.Message });
                }
            }
            catch (TargetInvocationException ex)
            {
                // 実際の例外を取得
                Exception innerException = ex.InnerException ?? ex;
                
                return CreateErrorResult(
                    innerException.Message,
                    new List<string> 
                    { 
                        $"Target invocation exception: {innerException.Message}",
                        $"Stack trace: {innerException.StackTrace}"
                    });
            }
            catch (Exception ex)
            {
                return CreateErrorResult(
                    ex.Message,
                    new List<string> 
                    { 
                        $"Execution exception: {ex.Message}",
                        $"Stack trace: {ex.StackTrace}"
                    });
            }
        }
    }
}