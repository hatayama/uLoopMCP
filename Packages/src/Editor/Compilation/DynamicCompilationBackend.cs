using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor.Compilation;

namespace io.github.hatayama.uLoopMCP
{
    internal sealed class DynamicCompilationBackend
    {
        public Task<CompilerMessage[]> CompileAsync(
            string sourcePath,
            string dllPath,
            List<string> references,
            ExternalCompilerPaths externalCompilerPaths,
            CancellationToken ct,
            Action markBuildStarted,
            Action markBuildFinished,
            Action incrementBuildCount)
        {
            if (externalCompilerPaths != null)
            {
                return RoslynCompilerBackend.CompileAsync(
                    sourcePath,
                    dllPath,
                    references,
                    externalCompilerPaths,
                    ct,
                    markBuildStarted,
                    markBuildFinished,
                    incrementBuildCount);
            }

            return AssemblyBuilderFallbackCompilerBackend.CompileAsync(
                sourcePath,
                dllPath,
                references,
                ct,
                markBuildStarted,
                markBuildFinished,
                incrementBuildCount);
        }
    }
}
