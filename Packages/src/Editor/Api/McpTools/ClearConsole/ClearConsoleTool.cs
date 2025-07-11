using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// ClearConsole tool handler - Type-safe implementation using Schema and Response
    /// Clears Unity console logs for clean development workflow
    /// Related classes:
    /// - ConsoleUtility: Service layer for console operations
    /// - ClearConsoleSchema: Type-safe parameter schema
    /// - ClearConsoleResponse: Type-safe response structure
    /// </summary>
    [McpTool(Description = "Clear Unity console logs")]
    public class ClearConsoleTool : AbstractUnityTool<ClearConsoleSchema, ClearConsoleResponse>
    {
        public override string ToolName => "clear-console";

        /// <summary>
        /// Execute console clear tool
        /// </summary>
        /// <param name="parameters">Type-safe parameters</param>
        /// <param name="cancellationToken">Cancellation token for timeout control</param>
        /// <returns>Clear operation result</returns>
        protected override Task<ClearConsoleResponse> ExecuteAsync(ClearConsoleSchema parameters, CancellationToken cancellationToken)
        {
            // Get current log counts before clearing
            ConsoleUtility.GetConsoleLogCounts(out int errorCount, out int warningCount, out int logCount);
            int totalLogCount = errorCount + warningCount + logCount;
            
            ClearedLogCounts clearedCounts = new ClearedLogCounts(errorCount, warningCount, logCount);

            // Perform console clear operation
            ConsoleUtility.ClearConsole();

            // Add confirmation message if requested
            if (parameters.AddConfirmationMessage)
            {
                Debug.Log("=== Console cleared via MCP tool ===");
            }

            // Create success response
            string message = totalLogCount > 0 
                ? $"Successfully cleared {totalLogCount} console logs (Errors: {errorCount}, Warnings: {warningCount}, Logs: {logCount})"
                : "Console was already empty";

            ClearConsoleResponse response = new ClearConsoleResponse(
                success: true,
                clearedLogCount: totalLogCount,
                clearedCounts: clearedCounts,
                message: message
            );

            return Task.FromResult(response);
        }
    }
} 