using UnityEditor;
using UnityEditor.Compilation;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// A class that asynchronously executes Unity's compilation process and monitors the results.
    /// It handles starting compilation, monitoring its progress, and retrieving the results.
    /// </summary>
    public class CompileController : IDisposable
    {
        private bool isCompiling = false;
        private List<CompilerMessage> compileMessages = new List<CompilerMessage>();
        private TaskCompletionSource<CompileResult> currentCompileTask;

        /// <summary>
        /// Event that occurs when compilation is complete.
        /// </summary>
        public event Action<CompileResult> OnCompileCompleted;
        
        /// <summary>
        /// Event that occurs when compilation starts.
        /// </summary>
        public event Action<string> OnCompileStarted;
        
        /// <summary>
        /// Event that occurs when assembly compilation is complete.
        /// </summary>
        public event Action<string, CompilerMessage[]> OnAssemblyCompiled;

        /// <summary>
        /// Gets whether a compilation is currently in progress.
        /// </summary>
        public bool IsCompiling => isCompiling;
        
        /// <summary>
        /// Gets the current list of compiler messages.
        /// </summary>
        public IReadOnlyList<CompilerMessage> CompileMessages => compileMessages.AsReadOnly();

        /// <summary>
        /// Executes compilation asynchronously.
        /// </summary>
        /// <param name="forceRecompile">Whether to force a recompile.</param>
        /// <returns>The compilation result.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the task is not found during compilation.</exception>
        public async Task<CompileResult> TryCompileAsync(bool forceRecompile = false)
        {
            if (isCompiling)
            {
                // If compilation is already in progress, wait for the current task.
                if (currentCompileTask != null)
                {
                    return await currentCompileTask.Task;
                }
                throw new InvalidOperationException("Compilation is in progress, but the task could not be found.");
            }

            isCompiling = true;
            compileMessages.Clear();
            currentCompileTask = new TaskCompletionSource<CompileResult>();

            // Execute asset refresh.
            AssetDatabase.Refresh();

            // Register events.
            CompilationPipeline.compilationFinished += HandleCompileFinished;
            CompilationPipeline.assemblyCompilationFinished += HandleAssemblyFinished;

            string startMessage = forceRecompile ? "Forced recompile started after asset refresh..." : "Compilation started after asset refresh...";
            OnCompileStarted?.Invoke(startMessage);

            if (forceRecompile)
            {
                CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
            }
            else
            {
                CompilationPipeline.RequestScriptCompilation();
            }

            return await currentCompileTask.Task;
        }

        /// <summary>
        /// Clears the compiler messages.
        /// </summary>
        public void ClearMessages()
        {
            compileMessages.Clear();
        }

        /// <summary>
        /// Handler for when compilation is complete.
        /// </summary>
        /// <param name="context">The compilation context.</param>
        private void HandleCompileFinished(object context)
        {
            // Unregister events.
            CompilationPipeline.compilationFinished -= HandleCompileFinished;
            CompilationPipeline.assemblyCompilationFinished -= HandleAssemblyFinished;

            isCompiling = false;

            CompileResult result = CreateCompileResult();
            OnCompileCompleted?.Invoke(result);

            // Set the result on the TaskCompletionSource.
            TaskCompletionSource<CompileResult> task = currentCompileTask;
            currentCompileTask = null;
            task?.SetResult(result);
        }

        /// <summary>
        /// Handler for when assembly compilation is complete.
        /// </summary>
        /// <param name="asmPath">The assembly path.</param>
        /// <param name="messages">The compiler messages.</param>
        private void HandleAssemblyFinished(string asmPath, CompilerMessage[] messages)
        {
            string assemblyName = System.IO.Path.GetFileName(asmPath);

            foreach (CompilerMessage message in messages)
            {
                compileMessages.Add(message);
            }

            OnAssemblyCompiled?.Invoke(assemblyName, messages);
        }

        /// <summary>
        /// Creates the compilation result.
        /// </summary>
        /// <returns>The compilation result.</returns>
        private CompileResult CreateCompileResult()
        {
            int errorCount = compileMessages.Count(m => m.type == CompilerMessageType.Error);
            int warningCount = compileMessages.Count(m => m.type == CompilerMessageType.Warning);

            CompilerMessage[] errors = compileMessages.Where(m => m.type == CompilerMessageType.Error).ToArray();
            CompilerMessage[] warnings = compileMessages.Where(m => m.type == CompilerMessageType.Warning).ToArray();

            return new CompileResult(
                success: errorCount == 0,
                errorCount: errorCount,
                warningCount: warningCount,
                completedAt: DateTime.Now,
                messages: compileMessages.ToArray(),
                errors: errors,
                warnings: warnings
            );
        }

        /// <summary>
        /// Cleans up resources.
        /// </summary>
        public void Cleanup()
        {
            // Unregister events just in case.
            CompilationPipeline.compilationFinished -= HandleCompileFinished;
            CompilationPipeline.assemblyCompilationFinished -= HandleAssemblyFinished;

            // If there is an incomplete task, cancel it.
            if (currentCompileTask != null && !currentCompileTask.Task.IsCompleted)
            {
                currentCompileTask.SetCanceled();
                currentCompileTask = null;
            }
        }

        /// <summary>
        /// Releases resources.
        /// </summary>
        public void Dispose()
        {
            Cleanup();
            compileMessages?.Clear();
            compileMessages = null;

            // Clear all events.
            OnCompileCompleted = null;
            OnCompileStarted = null;
            OnAssemblyCompiled = null;
        }
    }

    /// <summary>
    /// A class that represents the result of a compilation.
    /// Includes information on errors, warnings, and the completion time.
    /// </summary>
    public class CompileResult
    {
        /// <summary>
        /// Whether the compilation was successful.
        /// </summary>
        public bool Success { get; }
        
        /// <summary>
        /// The number of errors.
        /// </summary>
        public int ErrorCount { get; }
        
        /// <summary>
        /// The number of warnings.
        /// </summary>
        public int WarningCount { get; }
        
        /// <summary>
        /// The time of compilation completion.
        /// </summary>
        public DateTime CompletedAt { get; }
        
        /// <summary>
        /// All compiler messages.
        /// </summary>
        public CompilerMessage[] Messages { get; }
        
        /// <summary>
        /// Error messages only.
        /// </summary>
        public CompilerMessage[] Errors { get; }
        
        /// <summary>
        /// Warning messages only.
        /// </summary>
        public CompilerMessage[] Warnings { get; }

        /// <summary>
        /// Alias for error messages (for backward compatibility).
        /// </summary>
        public CompilerMessage[] error => Errors;
        
        /// <summary>
        /// Alias for warning messages (for backward compatibility).
        /// </summary>
        public CompilerMessage[] warning => Warnings;

        /// <summary>
        /// Initializes the compilation result.
        /// </summary>
        /// <param name="success">The compilation success flag.</param>
        /// <param name="errorCount">The number of errors.</param>
        /// <param name="warningCount">The number of warnings.</param>
        /// <param name="completedAt">The completion time.</param>
        /// <param name="messages">All messages.</param>
        /// <param name="errors">The error messages.</param>
        /// <param name="warnings">The warning messages.</param>
        public CompileResult(bool success, int errorCount, int warningCount, DateTime completedAt, CompilerMessage[] messages, CompilerMessage[] errors, CompilerMessage[] warnings)
        {
            Success = success;
            ErrorCount = errorCount;
            WarningCount = warningCount;
            CompletedAt = completedAt;
            Messages = messages;
            Errors = errors;
            Warnings = warnings;
        }
    }
}