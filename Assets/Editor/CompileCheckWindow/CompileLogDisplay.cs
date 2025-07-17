using UnityEditor.Compilation;
using System.Text;

namespace io.github.hatayama.uLoopMCP
{
    public class CompileLogDisplay : System.IDisposable
    {
        private StringBuilder logBuilder = new();

        public string LogText => logBuilder?.ToString() ?? "Compile results will be displayed here.";

        public void Clear()
        {
            if (logBuilder == null) return;
            logBuilder.Clear();
            logBuilder.AppendLine("Compile results will be displayed here.");
        }

        public void RestoreFromText(string text)
        {
            if (logBuilder == null) return;
            logBuilder.Clear();
            logBuilder.Append(text);
        }

        public void AppendStartMessage(string message)
        {
            if (logBuilder == null) return;
            logBuilder.AppendLine(message);
        }

        public void AppendAssemblyMessage(string assemblyName, CompilerMessage[] messages)
        {
            if (logBuilder == null) return;
            logBuilder.AppendLine($"Assembly [{assemblyName}] compilation finished.");

            foreach (CompilerMessage message in messages)
            {
                if (message.type == CompilerMessageType.Error)
                {
                    logBuilder.AppendLine($"  [Error] {message.message}");
                }
                else if (message.type == CompilerMessageType.Warning)
                {
                    logBuilder.AppendLine($"  [Warning] {message.message}");
                }
            }
        }

        public void AppendCompletionMessage(CompileResult result)
        {
            if (logBuilder == null) return;

            string resultMessage = result.Success ?
                "Compilation successful! No issues." :
                "Compilation failed! Please check the errors.";

            logBuilder.AppendLine();

            // Display for when no assemblies were processed (no changes)
            if (result.Messages.Length == 0)
            {
                logBuilder.AppendLine("No changes, so compilation was skipped.");
                logBuilder.AppendLine("But it finished without any problems!");
            }

            logBuilder.AppendLine("=== Compilation Finished ===");
            logBuilder.AppendLine($"Result: {resultMessage}");
            logBuilder.AppendLine($"Has Errors: {!result.Success}");
            logBuilder.AppendLine($"Completion Time: {result.CompletedAt:HH:mm:ss}");

            if (result.Messages.Length > 0)
            {
                logBuilder.AppendLine($"Errors: {result.ErrorCount}, Warnings: {result.WarningCount}");
            }
            else
            {
                logBuilder.AppendLine("Processed Assemblies: None (no changes)");
            }
        }

        public CompileLogDisplay()
        {
            Clear();
        }

        public void Dispose()
        {
            logBuilder?.Clear();
            logBuilder = null;
        }
    }
}