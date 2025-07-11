using System.Threading.Tasks;
using System.Threading;
using System;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Debug tool that sleeps for specified duration to test timeout functionality
    /// Related classes:
    /// - SleepSchema: Input parameters including sleep duration and timeout
    /// - SleepResponse: Output containing results and timing information
    /// - AbstractUnityTool: Base class providing timeout infrastructure
    /// </summary>
    [McpTool(
        DisplayDevelopmentOnly = true,
        Description = "Debug tool that sleeps for specified duration to test timeout functionality"
    )]
    public class SleepTool : AbstractUnityTool<SleepSchema, SleepResponse>
    {
        public override string ToolName => "debug-sleep";
        
        protected override async Task<SleepResponse> ExecuteAsync(SleepSchema parameters, CancellationToken cancellationToken)
        {
            McpLogger.LogInfo($"[SleepTool] Starting sleep for {parameters.SleepSeconds} seconds (timeout: {parameters.TimeoutSeconds}s)");
            
            int actualSleepSeconds = 0;
            
            try
            {
                // Sleep 1 second at a time, checking for cancellation between each sleep
                for (int i = 0; i < parameters.SleepSeconds; i++)
                {
                    // Check for cancellation before each sleep iteration
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Sleep for 1 second with cancellation support
                    await Task.Delay(1000, cancellationToken);
                    
                    actualSleepSeconds = i + 1;
                    McpLogger.LogInfo($"[SleepTool] Slept {actualSleepSeconds}/{parameters.SleepSeconds} seconds");
                }
                
                McpLogger.LogInfo($"[SleepTool] Successfully completed sleep for {actualSleepSeconds} seconds");
                
                return new SleepResponse 
                { 
                    Message = $"Successfully slept for {actualSleepSeconds} seconds",
                    ActualSleepSeconds = actualSleepSeconds,
                    WasCancelled = false,
                    AppliedTimeoutSeconds = parameters.TimeoutSeconds
                };
            }
            catch (OperationCanceledException)
            {
                McpLogger.LogInfo($"[SleepTool] Operation cancelled after {actualSleepSeconds} seconds (timeout: {parameters.TimeoutSeconds}s)");
                
                // Even when cancelled, return a response with the information about what happened
                // The TimeoutException will be thrown by AbstractUnityTool, but we log the details here
                throw;
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"[SleepTool] Unexpected error: {ex.Message}");
                throw;
            }
        }
    }
}