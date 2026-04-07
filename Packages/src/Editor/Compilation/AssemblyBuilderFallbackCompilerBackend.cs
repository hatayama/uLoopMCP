using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor.Compilation;

namespace io.github.hatayama.uLoopMCP
{
    internal static class AssemblyBuilderFallbackCompilerBackend
    {
        public static async Task<CompilerMessage[]> CompileAsync(
            string sourcePath,
            string dllPath,
            List<string> references,
            CancellationToken ct,
            Action markBuildStarted,
            Action markBuildFinished,
            Action incrementBuildCount)
        {
            TaskCompletionSource<CompilerMessage[]> taskCompletionSource = new();
            ct.ThrowIfCancellationRequested();
            incrementBuildCount();

            string[] referenceArray = references != null
                ? DynamicReferenceSetBuilder.MergeReferencesByAssemblyName(Array.Empty<string>(), references)
                : Array.Empty<string>();

            AssemblyBuilder builder = new AssemblyBuilder(dllPath, sourcePath)
            {
                referencesOptions = ReferencesOptions.UseEngineModules,
                additionalReferences = referenceArray
            };

            builder.buildFinished += (string assemblyPath, CompilerMessage[] compilerMessages) =>
            {
                taskCompletionSource.TrySetResult(compilerMessages);
            };

            bool started = builder.Build();
            if (!started)
            {
                return new CompilerMessage[]
                {
                    new CompilerMessage
                    {
                        type = CompilerMessageType.Error,
                        message = "AssemblyBuilder.Build() failed to start compilation"
                    }
                };
            }

            markBuildStarted();
            CompilerMessage[] messages = await taskCompletionSource.Task.ConfigureAwait(false);
            markBuildFinished();
            ct.ThrowIfCancellationRequested();
            return messages;
        }
    }
}
