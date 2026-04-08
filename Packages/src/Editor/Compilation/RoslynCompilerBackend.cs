using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor.Compilation;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    internal static class RoslynCompilerBackend
    {
        private sealed class OneShotCompileResult
        {
            public CompilerMessage[] CompilerMessages { get; }

            public bool ShouldFallback { get; }

            private OneShotCompileResult(CompilerMessage[] compilerMessages, bool shouldFallback)
            {
                CompilerMessages = compilerMessages;
                ShouldFallback = shouldFallback;
            }

            public static OneShotCompileResult Successful(CompilerMessage[] compilerMessages)
            {
                return new OneShotCompileResult(compilerMessages, false);
            }

            public static OneShotCompileResult Fallback()
            {
                return new OneShotCompileResult(null, true);
            }
        }

        public static async Task<CompilerMessage[]> CompileAsync(
            string sourcePath,
            string dllPath,
            List<string> references,
            ExternalCompilerPaths externalCompilerPaths,
            CancellationToken ct,
            Action markBuildStarted,
            Action markBuildFinished,
            Action incrementBuildCount)
        {
            string workerRequestFilePath = Path.ChangeExtension(sourcePath, ".worker");

            try
            {
                if (Application.platform != RuntimePlatform.WindowsEditor)
                {
                    WriteWorkerRequestFile(workerRequestFilePath, sourcePath, dllPath, references);

                    CompilerMessage[] workerMessages = SharedRoslynCompilerWorkerHost.TryCompile(
                        workerRequestFilePath,
                        externalCompilerPaths,
                        ct,
                        markBuildStarted,
                        markBuildFinished,
                        incrementBuildCount);
                    if (workerMessages != null)
                    {
                        return workerMessages;
                    }

                    DynamicCompilationHealthMonitor.ReportSharedWorkerFallback(
                        "worker_unavailable",
                        new
                        {
                            dotnet_host_path = externalCompilerPaths.DotnetHostPath,
                            compiler_dll_path = externalCompilerPaths.CompilerDllPath
                        });
                }
                else
                {
                    DynamicCompilationHealthMonitor.ReportSharedWorkerFallback(
                        "worker_unsupported_on_windows",
                        new
                        {
                            platform = Application.platform.ToString()
                        });
                }

                ct.ThrowIfCancellationRequested();
                OneShotCompileResult oneShotResult = await CompileWithOneShotAsync(
                    sourcePath,
                    dllPath,
                    references,
                    externalCompilerPaths,
                    ct,
                    markBuildStarted,
                    markBuildFinished,
                    incrementBuildCount).ConfigureAwait(false);
                if (oneShotResult.ShouldFallback)
                {
                    return await AssemblyBuilderFallbackCompilerBackend.CompileAsync(
                        sourcePath,
                        dllPath,
                        references,
                        ct,
                        markBuildStarted,
                        markBuildFinished,
                        incrementBuildCount).ConfigureAwait(false);
                }

                return oneShotResult.CompilerMessages;
            }
            finally
            {
                if (File.Exists(workerRequestFilePath))
                {
                    File.Delete(workerRequestFilePath);
                }
            }
        }

        private static async Task<OneShotCompileResult> CompileWithOneShotAsync(
            string sourcePath,
            string dllPath,
            List<string> references,
            ExternalCompilerPaths externalCompilerPaths,
            CancellationToken ct,
            Action markBuildStarted,
            Action markBuildFinished,
            Action incrementBuildCount)
        {
            string responseFilePath = Path.ChangeExtension(sourcePath, ".rsp");
            WriteCompilerResponseFile(responseFilePath, sourcePath, dllPath, references);

            try
            {
                incrementBuildCount();

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = externalCompilerPaths.DotnetHostPath,
                    Arguments = $"{QuoteCommandLineArgument(externalCompilerPaths.CompilerDllPath)} @{QuoteCommandLineArgument(responseFilePath)}",
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                markBuildStarted();

                try
                {
                    using Process process = ProcessStartHelper.TryStart(startInfo);
                    if (process == null)
                    {
                        DynamicCompilationHealthMonitor.ReportOneShotCompilerStartFailure(new
                        {
                            dotnet_host_path = externalCompilerPaths.DotnetHostPath,
                            compiler_dll_path = externalCompilerPaths.CompilerDllPath
                        });

                        return OneShotCompileResult.Fallback();
                    }

                    Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                    Task<string> stderrTask = process.StandardError.ReadToEndAsync();
                    await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
                    process.WaitForExit();
                    ct.ThrowIfCancellationRequested();

                    CompilerMessage[] compilerMessages = ExternalCompilerMessageParser.Parse(
                        stdoutTask.Result,
                        stderrTask.Result,
                        process.ExitCode);
                    if (ShouldRetryWithAssemblyBuilder(process.ExitCode, compilerMessages))
                    {
                        return OneShotCompileResult.Fallback();
                    }

                    return OneShotCompileResult.Successful(compilerMessages);
                }
                finally
                {
                    markBuildFinished();
                }
            }
            finally
            {
                if (File.Exists(responseFilePath))
                {
                    File.Delete(responseFilePath);
                }
            }
        }

        private static void WriteCompilerResponseFile(
            string responseFilePath,
            string sourcePath,
            string dllPath,
            IReadOnlyCollection<string> references)
        {
            List<string> lines = new List<string>
            {
                "-nologo",
                "-nostdlib+",
                "-target:library",
                "-optimize+",
                "-debug-",
                QuoteResponseFileArgument("-out:", dllPath)
            };

            foreach (string reference in references)
            {
                lines.Add(QuoteResponseFileArgument("-r:", reference));
            }

            lines.Add(QuoteResponseFilePath(sourcePath));
            File.WriteAllLines(responseFilePath, lines);
        }

        private static void WriteWorkerRequestFile(
            string requestFilePath,
            string sourcePath,
            string dllPath,
            IReadOnlyCollection<string> references)
        {
            List<string> lines = new List<string> { Path.GetFullPath(sourcePath), Path.GetFullPath(dllPath) };
            foreach (string reference in references)
            {
                lines.Add(Path.GetFullPath(reference));
            }

            File.WriteAllLines(Path.GetFullPath(requestFilePath), lines);
        }

        private static string QuoteResponseFileArgument(string prefix, string value)
        {
            return $"{prefix}{QuoteResponseFilePath(value)}";
        }

        private static string QuoteResponseFilePath(string path)
        {
            return $"\"{path}\"";
        }

        // Infrastructure-level failures (non-zero exit without file-specific diagnostics)
        // indicate the compiler itself broke, not the user's code.
        private static bool ShouldRetryWithAssemblyBuilder(
            int exitCode,
            IReadOnlyCollection<CompilerMessage> compilerMessages)
        {
            if (exitCode == 0)
            {
                return false;
            }

            foreach (CompilerMessage compilerMessage in compilerMessages)
            {
                if (compilerMessage.type == CompilerMessageType.Error &&
                    !string.IsNullOrWhiteSpace(compilerMessage.file))
                {
                    return false;
                }
            }

            return true;
        }

        private static string QuoteCommandLineArgument(string value)
        {
            return $"\"{value}\"";
        }
    }
}
