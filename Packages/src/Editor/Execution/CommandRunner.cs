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
    /// Controls the execution of compiled code.
    /// 
    /// Related Classes: ExecutionContext, ExecutionResult
    /// </summary>
    public class CommandRunner
    {
        private bool _isRunning = false;
        private CancellationTokenSource _cancellationTokenSource;

        public bool IsRunning => _isRunning;

        private static void LogExecutionStart(ExecutionContext context, string correlationId)
        {
        }

        private static void LogExecutionComplete(ExecutionResult result, string correlationId)
        {
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
                
                // Configure timeout
                using CancellationTokenSource combinedCts = CreateCombinedCancellationTokenSource(context);

                // Execute
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
                ExecutionTime = TimeSpan.Zero, // Will be set by the caller
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
                // No parameters
                return executeMethod.Invoke(instance, null);
            }
            else if (methodParameters.Length == 1 && methodParameters[0].ParameterType == typeof(Dictionary<string, object>))
            {
                // Parameter dictionary available
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
                // Find executable type from assembly
                (Type targetType, MethodInfo executeMethod) = FindExecuteMethod(context.CompiledAssembly);
                
                if (targetType == null || executeMethod == null)
                {
                    return CreateErrorResult(
                        McpConstants.ERROR_MESSAGE_NO_EXECUTE_METHOD,
                        new List<string> { "Assembly types checked but no Execute method found" });
                }

                // Create instance
                object instance = CreateInstance(targetType);
                if (instance == null)
                {
                    return CreateErrorResult(McpConstants.ERROR_MESSAGE_FAILED_TO_CREATE_INSTANCE);
                }

                // Check cancellation
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Execute method
                    object executionResult = InvokeExecuteMethod(executeMethod, instance, context.Parameters);
                    
                    // Convert result to string
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
                // Retrieve actual exception
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