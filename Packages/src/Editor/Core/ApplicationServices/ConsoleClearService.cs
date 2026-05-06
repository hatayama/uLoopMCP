using UnityEngine;

namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Application-owned bridge from the public host-service contract to Unity Console mutation.
    /// </summary>
    public sealed class ConsoleClearService : IUnityCliLoopConsoleClearService
    {
        public UnityCliLoopConsoleClearResult Clear(bool addConfirmationMessage)
        {
            ConsoleUtility.GetConsoleLogCounts(out int errorCount, out int warningCount, out int logCount);
            int totalLogCount = errorCount + warningCount + logCount;
            UnityCliLoopConsoleClearCounts clearedCounts = new UnityCliLoopConsoleClearCounts(
                errorCount,
                warningCount,
                logCount);

            ConsoleUtility.ClearConsole();

            if (addConfirmationMessage)
            {
                Debug.Log("=== Console cleared via Unity CLI Loop ===");
            }

            string message = totalLogCount > 0
                ? $"Successfully cleared {totalLogCount} console logs (Errors: {errorCount}, Warnings: {warningCount}, Logs: {logCount})"
                : "Console was already empty";

            return new UnityCliLoopConsoleClearResult(
                success: true,
                clearedLogCount: totalLogCount,
                clearedCounts: clearedCounts,
                message: message);
        }
    }
}
