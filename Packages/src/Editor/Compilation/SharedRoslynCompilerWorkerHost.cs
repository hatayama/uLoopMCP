using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using Debug = UnityEngine.Debug;

namespace io.github.hatayama.uLoopMCP
{
    internal static class SharedRoslynCompilerWorkerHost
    {
        private const string SharedCompilerWorkerResultPrefix = "__ULOOP_RESULT__";
        private const string SharedCompilerWorkerEndMarker = "__ULOOP_END__";
        private const string SharedCompilerWorkerQuitCommand = "__QUIT__";
        private const string RoslynWorkerSourceFileName = "RoslynCompilerWorker.cs";
        private const string RoslynWorkerAssemblyFileName = "RoslynCompilerWorker.dll";
        private const string RoslynWorkerCompileResponseFileName = "RoslynCompilerWorker.rsp";
        private const int SharedCompilerWorkerResponseTimeoutMilliseconds = 30000;

        private static readonly object SharedCompilerWorkerLock = new();
        private static Process _sharedCompilerWorkerProcess;

        [InitializeOnLoadMethod]
        private static void RegisterLifecycle()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ShutdownForReload;
            AssemblyReloadEvents.beforeAssemblyReload += ShutdownForReload;
        }

        public static CompilerMessage[] TryCompile(
            string requestFilePath,
            ExternalCompilerPaths externalCompilerPaths,
            CancellationToken ct,
            Action markBuildStarted,
            Action markBuildFinished,
            Action incrementBuildCount)
        {
            if (UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WindowsEditor)
            {
                DynamicCompilationHealthMonitor.ReportSharedWorkerFallback(
                    "windows_platform",
                    new { platform = UnityEngine.Application.platform.ToString() });
                return null;
            }

            lock (SharedCompilerWorkerLock)
            {
                EnsureStarted(externalCompilerPaths);
                if (_sharedCompilerWorkerProcess == null || _sharedCompilerWorkerProcess.HasExited)
                {
                    DynamicCompilationHealthMonitor.ReportSharedWorkerFailure(
                        "worker_process_missing",
                        new { request_file_path = requestFilePath });
                    return null;
                }

                ct.ThrowIfCancellationRequested();
                incrementBuildCount();
                markBuildStarted();

                try
                {
                    _sharedCompilerWorkerProcess.StandardInput.WriteLine(Path.GetFullPath(requestFilePath));
                    _sharedCompilerWorkerProcess.StandardInput.Flush();

                    string header = ReadLine(_sharedCompilerWorkerProcess.StandardOutput, ct);
                    if (string.IsNullOrEmpty(header))
                    {
                        DynamicCompilationHealthMonitor.ReportSharedWorkerFailure(
                            "worker_empty_header",
                            new { request_file_path = requestFilePath });
                        Shutdown();
                        return null;
                    }

                    List<string> outputLines = new List<string>();
                    while (true)
                    {
                        string line = ReadLine(_sharedCompilerWorkerProcess.StandardOutput, ct);
                        if (line == null)
                        {
                            DynamicCompilationHealthMonitor.ReportSharedWorkerFailure(
                                "worker_missing_end_marker",
                                new { request_file_path = requestFilePath });
                            Shutdown();
                            return null;
                        }

                        if (line == SharedCompilerWorkerEndMarker)
                        {
                            break;
                        }

                        outputLines.Add(line);
                    }

                    ct.ThrowIfCancellationRequested();

                    if (!header.StartsWith(SharedCompilerWorkerResultPrefix, StringComparison.Ordinal))
                    {
                        DynamicCompilationHealthMonitor.ReportSharedWorkerFailure(
                            "worker_invalid_header",
                            new { header });
                        Shutdown();
                        return null;
                    }

                    string statusText = header.Substring(SharedCompilerWorkerResultPrefix.Length).Trim();
                    int exitCode = int.Parse(statusText);
                    string combinedOutput = string.Join("\n", outputLines);
                    return ExternalCompilerMessageParser.Parse(combinedOutput, string.Empty, exitCode);
                }
                finally
                {
                    markBuildFinished();
                }
            }
        }

        private static void EnsureStarted(ExternalCompilerPaths externalCompilerPaths)
        {
            if (_sharedCompilerWorkerProcess != null && !_sharedCompilerWorkerProcess.HasExited)
            {
                return;
            }

            string workerDirectoryPath = Path.Combine(Path.GetTempPath(), "uLoopMCPCompilation", "RoslynWorker");
            Directory.CreateDirectory(workerDirectoryPath);
            string workerSourcePath = Path.Combine(workerDirectoryPath, RoslynWorkerSourceFileName);
            string workerAssemblyPath = Path.Combine(workerDirectoryPath, RoslynWorkerAssemblyFileName);
            string workerCompileResponseFilePath = Path.Combine(workerDirectoryPath, RoslynWorkerCompileResponseFileName);
            string workerSource = CreateProgramSource();

            if (!File.Exists(workerSourcePath) || File.ReadAllText(workerSourcePath) != workerSource)
            {
                File.WriteAllText(workerSourcePath, workerSource);
                if (File.Exists(workerAssemblyPath))
                {
                    File.Delete(workerAssemblyPath);
                }
            }

            if (!File.Exists(workerAssemblyPath))
            {
                CompilerMessage[] buildMessages = BuildWorkerAssembly(
                    externalCompilerPaths,
                    workerSourcePath,
                    workerAssemblyPath,
                    workerCompileResponseFilePath);
                if (HasErrors(buildMessages))
                {
                    DynamicCompilationHealthMonitor.ReportSharedWorkerFailure(
                        "worker_build_failed",
                        new
                        {
                            first_error = FindFirstErrorMessage(buildMessages),
                            worker_source_path = workerSourcePath
                        });
                    return;
                }
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = externalCompilerPaths.DotnetHostPath,
                Arguments = "exec"
                    + " --runtimeconfig " + QuoteCommandLineArgument(externalCompilerPaths.CompilerRuntimeConfigPath)
                    + " --depsfile " + QuoteCommandLineArgument(externalCompilerPaths.CompilerDepsFilePath)
                    + " " + QuoteCommandLineArgument(workerAssemblyPath),
                WorkingDirectory = workerDirectoryPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                CreateNoWindow = true
            };

            _sharedCompilerWorkerProcess = ProcessStartHelper.TryStart(startInfo);
            if (_sharedCompilerWorkerProcess == null)
            {
                DynamicCompilationHealthMonitor.ReportSharedWorkerFailure(
                    "worker_start_failed",
                    new
                    {
                        dotnet_host_path = externalCompilerPaths.DotnetHostPath,
                        worker_assembly_path = workerAssemblyPath
                    });
            }
        }

        private static CompilerMessage[] BuildWorkerAssembly(
            ExternalCompilerPaths externalCompilerPaths,
            string workerSourcePath,
            string workerAssemblyPath,
            string workerCompileResponseFilePath)
        {
            WriteWorkerCompilerResponseFile(
                workerCompileResponseFilePath,
                workerSourcePath,
                workerAssemblyPath,
                BuildWorkerReferenceSet(externalCompilerPaths));

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = externalCompilerPaths.DotnetHostPath,
                Arguments = $"{QuoteCommandLineArgument(externalCompilerPaths.CompilerDllPath)} @{QuoteCommandLineArgument(workerCompileResponseFilePath)}",
                WorkingDirectory = Path.GetDirectoryName(workerSourcePath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using Process process = ProcessStartHelper.TryStart(startInfo);
            if (process == null)
            {
                DynamicCompilationHealthMonitor.ReportSharedWorkerFailure(
                    "worker_compiler_start_failed",
                    new
                    {
                        dotnet_host_path = externalCompilerPaths.DotnetHostPath,
                        compiler_dll_path = externalCompilerPaths.CompilerDllPath
                    });
                return new CompilerMessage[]
                {
                    new CompilerMessage
                    {
                        type = CompilerMessageType.Error,
                        message = "Persistent Roslyn worker compiler failed to start"
                    }
                };
            }

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return ExternalCompilerMessageParser.Parse(stdout, stderr, process.ExitCode);
        }

        private static List<string> BuildWorkerReferenceSet(ExternalCompilerPaths externalCompilerPaths)
        {
            string sharedRuntimeDirectoryPath = externalCompilerPaths.NetCoreRuntimeSharedDirectoryPath;
            List<string> references = new List<string>
            {
                Path.Combine(sharedRuntimeDirectoryPath, "System.Private.CoreLib.dll"),
                Path.Combine(sharedRuntimeDirectoryPath, "System.Runtime.dll"),
                Path.Combine(sharedRuntimeDirectoryPath, "System.Console.dll"),
                Path.Combine(sharedRuntimeDirectoryPath, "System.Collections.dll"),
                Path.Combine(sharedRuntimeDirectoryPath, "System.IO.dll"),
                Path.Combine(sharedRuntimeDirectoryPath, "System.Threading.dll"),
                Path.Combine(sharedRuntimeDirectoryPath, "System.Threading.Tasks.dll"),
                Path.Combine(sharedRuntimeDirectoryPath, "System.Text.Encoding.Extensions.dll"),
                Path.Combine(sharedRuntimeDirectoryPath, "System.Runtime.Extensions.dll"),
                Path.Combine(sharedRuntimeDirectoryPath, "netstandard.dll"),
                externalCompilerPaths.CodeAnalysisDllPath,
                externalCompilerPaths.CodeAnalysisCSharpDllPath
            };

            AddIfExists(references, Path.Combine(sharedRuntimeDirectoryPath, "System.Collections.Immutable.dll"));
            AddIfExists(references, Path.Combine(sharedRuntimeDirectoryPath, "System.Reflection.Metadata.dll"));
            AddIfExists(references, Path.Combine(sharedRuntimeDirectoryPath, "System.Runtime.CompilerServices.Unsafe.dll"));
            AddIfExists(references, Path.Combine(sharedRuntimeDirectoryPath, "System.Memory.dll"));
            AddIfExists(references, Path.Combine(sharedRuntimeDirectoryPath, "System.Buffers.dll"));
            AddIfExists(references, Path.Combine(sharedRuntimeDirectoryPath, "System.Threading.Tasks.Extensions.dll"));

            return references;
        }

        private static void WriteWorkerCompilerResponseFile(
            string responseFilePath,
            string sourcePath,
            string dllPath,
            IReadOnlyCollection<string> references)
        {
            List<string> lines = new List<string>
            {
                "-nologo",
                "-nostdlib+",
                "-target:exe",
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

        private static string ReadLine(StreamReader reader, CancellationToken ct)
        {
            Debug.Assert(reader != null, "reader must not be null");

            Task<string> readTask = Task.Run(() => reader.ReadLine());
            bool completed = readTask.Wait(SharedCompilerWorkerResponseTimeoutMilliseconds, ct);
            if (!completed)
            {
                return null;
            }

            return readTask.Result;
        }

        private static bool HasErrors(IReadOnlyCollection<CompilerMessage> messages)
        {
            foreach (CompilerMessage message in messages)
            {
                if (message.type == CompilerMessageType.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private static string FindFirstErrorMessage(IReadOnlyCollection<CompilerMessage> messages)
        {
            foreach (CompilerMessage message in messages)
            {
                if (message.type == CompilerMessageType.Error)
                {
                    return message.message;
                }
            }

            return string.Empty;
        }

        private static void ShutdownForReload()
        {
            Shutdown();
        }

        private static void Shutdown()
        {
            lock (SharedCompilerWorkerLock)
            {
                if (_sharedCompilerWorkerProcess == null)
                {
                    return;
                }

                if (!_sharedCompilerWorkerProcess.HasExited)
                {
                    _sharedCompilerWorkerProcess.StandardInput.WriteLine(SharedCompilerWorkerQuitCommand);
                    _sharedCompilerWorkerProcess.StandardInput.Flush();
                    _sharedCompilerWorkerProcess.WaitForExit(500);
                }

                if (!_sharedCompilerWorkerProcess.HasExited)
                {
                    _sharedCompilerWorkerProcess.Kill();
                    _sharedCompilerWorkerProcess.WaitForExit(500);
                }

                _sharedCompilerWorkerProcess.Dispose();
                _sharedCompilerWorkerProcess = null;
            }
        }

        private static string QuoteResponseFileArgument(string prefix, string value)
        {
            return $"{prefix}{QuoteResponseFilePath(value)}";
        }

        private static string QuoteResponseFilePath(string path)
        {
            return $"\"{path}\"";
        }

        private static string QuoteCommandLineArgument(string value)
        {
            return $"\"{value}\"";
        }

        private static void AddIfExists(
            List<string> destination,
            string referencePath)
        {
            if (string.IsNullOrEmpty(referencePath) || !File.Exists(referencePath))
            {
                return;
            }

            destination.Add(referencePath);
        }

        private static string CreateProgramSource()
        {
            return "using System;\n"
                + "using System.Collections.Generic;\n"
                + "using System.IO;\n"
                + "using System.Text;\n"
                + "using Microsoft.CodeAnalysis;\n"
                + "using Microsoft.CodeAnalysis.CSharp;\n"
                + "using Microsoft.CodeAnalysis.Emit;\n"
                + "using Microsoft.CodeAnalysis.Text;\n"
                + "\n"
                + "public static class Program\n"
                + "{\n"
                + "    private const string ResultPrefix = \"" + SharedCompilerWorkerResultPrefix + "\";\n"
                + "    private const string EndMarker = \"" + SharedCompilerWorkerEndMarker + "\";\n"
                + "    private const string QuitCommand = \"" + SharedCompilerWorkerQuitCommand + "\";\n"
                + "    private static readonly object SyncRoot = new object();\n"
                + "    private static readonly Dictionary<string, CachedReference> Cache = new Dictionary<string, CachedReference>(StringComparer.OrdinalIgnoreCase);\n"
                + "    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default;\n"
                + "    private static readonly CSharpCompilationOptions CompilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release);\n"
                + "\n"
                + "    public static int Main()\n"
                + "    {\n"
                + "        string requestPath;\n"
                + "        while ((requestPath = Console.ReadLine()) != null)\n"
                + "        {\n"
                + "            if (requestPath == QuitCommand)\n"
                + "            {\n"
                + "                return 0;\n"
                + "            }\n"
                + "\n"
                + "            int exitCode = Compile(requestPath);\n"
                + "            Console.WriteLine(ResultPrefix + \" \" + exitCode);\n"
                + "            Console.WriteLine(EndMarker);\n"
                + "            Console.Out.Flush();\n"
                + "        }\n"
                + "\n"
                + "        return 0;\n"
                + "    }\n"
                + "\n"
                + "    private static int Compile(string requestPath)\n"
                + "    {\n"
                + "        string[] requestLines = File.ReadAllLines(requestPath);\n"
                + "        string sourcePath = requestLines[0];\n"
                + "        string dllPath = requestLines[1];\n"
                + "        string source = File.ReadAllText(sourcePath, Encoding.UTF8);\n"
                + "        SourceText sourceText = SourceText.From(source, Encoding.UTF8);\n"
                + "        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceText, ParseOptions, sourcePath);\n"
                + "        List<MetadataReference> references = BuildReferences(requestLines);\n"
                + "        CSharpCompilation compilation = CSharpCompilation.Create(Path.GetFileNameWithoutExtension(dllPath), new[] { syntaxTree }, references, CompilationOptions);\n"
                + "        using (FileStream peStream = new FileStream(dllPath, FileMode.Create, FileAccess.Write, FileShare.Read))\n"
                + "        {\n"
                + "            EmitResult emitResult = compilation.Emit(peStream);\n"
                + "            int exitCode = 0;\n"
                + "            foreach (Diagnostic diagnostic in emitResult.Diagnostics)\n"
                + "            {\n"
                + "                if (diagnostic.Severity != DiagnosticSeverity.Error && diagnostic.Severity != DiagnosticSeverity.Warning)\n"
                + "                {\n"
                + "                    continue;\n"
                + "                }\n"
                + "\n"
                + "                if (diagnostic.Severity == DiagnosticSeverity.Error)\n"
                + "                {\n"
                + "                    exitCode = 1;\n"
                + "                }\n"
                + "\n"
                + "                FileLinePositionSpan lineSpan = diagnostic.Location != Location.None ? diagnostic.Location.GetMappedLineSpan() : default(FileLinePositionSpan);\n"
                + "                int line = lineSpan.StartLinePosition.Line + 1;\n"
                + "                int column = lineSpan.StartLinePosition.Character + 1;\n"
                + "                string file = string.IsNullOrEmpty(lineSpan.Path) ? sourcePath : lineSpan.Path;\n"
                + "                string severity = diagnostic.Severity == DiagnosticSeverity.Warning ? \"warning\" : \"error\";\n"
                + "                Console.WriteLine(file + \"(\" + line + \",\" + column + \"): \" + severity + \" \" + diagnostic.Id + \": \" + diagnostic.GetMessage());\n"
                + "            }\n"
                + "\n"
                + "            return exitCode;\n"
                + "        }\n"
                + "    }\n"
                + "\n"
                + "    private static List<MetadataReference> BuildReferences(string[] requestLines)\n"
                + "    {\n"
                + "        List<MetadataReference> references = new List<MetadataReference>(Math.Max(0, requestLines.Length - 2));\n"
                + "        lock (SyncRoot)\n"
                + "        {\n"
                + "            for (int i = 2; i < requestLines.Length; i++)\n"
                + "            {\n"
                + "                string referencePath = requestLines[i];\n"
                + "                if (string.IsNullOrWhiteSpace(referencePath) || !File.Exists(referencePath))\n"
                + "                {\n"
                + "                    continue;\n"
                + "                }\n"
                + "\n"
                + "                FileInfo info = new FileInfo(referencePath);\n"
                + "                long writeTicks = info.LastWriteTimeUtc.Ticks;\n"
                + "                long fileLength = info.Length;\n"
                + "                CachedReference cachedReference;\n"
                + "                if (Cache.TryGetValue(referencePath, out cachedReference)\n"
                + "                    && cachedReference.WriteTicks == writeTicks\n"
                + "                    && cachedReference.FileLength == fileLength)\n"
                + "                {\n"
                + "                    references.Add(cachedReference.Reference);\n"
                + "                    continue;\n"
                + "                }\n"
                + "\n"
                + "                PortableExecutableReference createdReference = MetadataReference.CreateFromFile(referencePath);\n"
                + "                Cache[referencePath] = new CachedReference(createdReference, writeTicks, fileLength);\n"
                + "                references.Add(createdReference);\n"
                + "            }\n"
                + "        }\n"
                + "\n"
                + "        return references;\n"
                + "    }\n"
                + "\n"
                + "    private sealed class CachedReference\n"
                + "    {\n"
                + "        public CachedReference(PortableExecutableReference reference, long writeTicks, long fileLength)\n"
                + "        {\n"
                + "            Reference = reference;\n"
                + "            WriteTicks = writeTicks;\n"
                + "            FileLength = fileLength;\n"
                + "        }\n"
                + "\n"
                + "        public PortableExecutableReference Reference { get; }\n"
                + "        public long WriteTicks { get; }\n"
                + "        public long FileLength { get; }\n"
                + "    }\n"
                + "}\n";
        }
    }
}
