using UnityEditor.Compilation;
using System.Text;

namespace io.github.hatayama.uLoopMCP
{
    public class CompileLogDisplay : System.IDisposable
    {
        private StringBuilder _logBuilder = new();

        public string LogText => _logBuilder?.ToString() ?? "Compile results will be displayed here.";

        public void Clear()
        {
            if (_logBuilder == null) return;
            _logBuilder.Clear();
            _logBuilder.AppendLine("Compile results will be displayed here.");
        }

        public void RestoreFromText(string text)
        {
            if (_logBuilder == null) return;
            _logBuilder.Clear();
            _logBuilder.Append(text);
        }

        public void AppendStartMessage(string message)
        {
            if (_logBuilder == null) return;
            _logBuilder.AppendLine(message);
        }

        public void AppendAssemblyMessage(string assemblyName, CompilerMessage[] messages)
        {
            if (_logBuilder == null) return;
            _logBuilder.AppendLine($"Assembly [{assemblyName}] compilation finished.");

            foreach (CompilerMessage message in messages)
            {
                if (message.type == CompilerMessageType.Error)
                {
                    _logBuilder.AppendLine($"  [Error] {message.message}");
                }
                else if (message.type == CompilerMessageType.Warning)
                {
                    _logBuilder.AppendLine($"  [Warning] {message.message}");
                }
            }
        }

        public void AppendCompletionMessage(CompileResult result)
        {
            if (_logBuilder == null) return;

            string resultMessage = result.Success switch
            {
                true => "Compilation successful! No issues.",
                false => "Compilation failed! Please check the errors.",
                null => "Compilation status is indeterminate. Use get-logs tool to check results."
            };

            _logBuilder.AppendLine();

            // Display for when no assemblies were processed (no changes)
            if (result.Messages.Length == 0)
            {
                _logBuilder.AppendLine("No changes, so compilation was skipped.");
                _logBuilder.AppendLine("But it finished without any problems!");
            }

            _logBuilder.AppendLine("=== Compilation Finished ===");
            _logBuilder.AppendLine($"Result: {resultMessage}");
            _logBuilder.AppendLine($"Has Errors: {!result.Success}");
            _logBuilder.AppendLine($"Completion Time: {result.CompletedAt:HH:mm:ss}");

            if (result.Messages.Length > 0)
            {
                _logBuilder.AppendLine($"Errors: {result.ErrorCount}, Warnings: {result.WarningCount}");
            }
            else
            {
                _logBuilder.AppendLine("Processed Assemblies: None (no changes)");
            }
        }

        public CompileLogDisplay()
        {
            Clear();
        }

        public void Dispose()
        {
            _logBuilder?.Clear();
            _logBuilder = null;
        }
    }
}