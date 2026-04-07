using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using Assembly = System.Reflection.Assembly;
using Debug = UnityEngine.Debug;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// IDynamicCompilationService implementation using Unity's built-in AssemblyBuilder API.
    /// Replaces RoslynCompiler by delegating compilation to Unity's internal C# compiler.
    /// </summary>
    public sealed class AssemblyBuilderCompiler : IDynamicCompilationService, IDisposable
    {
        private static int _compileCounter;
        private static readonly object ReferenceCatalogLock = new();
        private static readonly Regex ExternalCompilerDiagnosticRegex = new(
            @"^(?<file>.+)\((?<line>\d+),(?<column>\d+)\): (?<severity>error|warning) (?<code>[A-Z]+\d+): (?<message>.+)$",
            RegexOptions.Compiled);
        private static readonly string[] BaseReferenceAssemblyNames =
        {
            "mscorlib",
            "netstandard",
            "System",
            "System.Core",
            "System.Runtime",
            "System.Collections",
            "System.Net.Http",
            "System.Threading",
            "System.Threading.Tasks",
            "UnityEngine.CoreModule",
            "UnityEditor.CoreModule",
        };
        private const string SharedCompilerWorkerResultPrefix = "__ULOOP_RESULT__";
        private const string SharedCompilerWorkerEndMarker = "__ULOOP_END__";
        private const string SharedCompilerWorkerQuitCommand = "__QUIT__";
        private const string RoslynWorkerSourceFileName = "RoslynCompilerWorker.cs";
        private const string RoslynWorkerAssemblyFileName = "RoslynCompilerWorker.dll";
        private const string RoslynWorkerCompileResponseFileName = "RoslynCompilerWorker.rsp";
        private const int SharedCompilerWorkerResponseTimeoutMilliseconds = 30000;
        private const int AutoPrewarmDelayFrameCount = 5;
        private const string AutoPrewarmCode = "return null;";
        private const string AutoPrewarmClassName = "DynamicCodeAutoPrewarmCommand";
        private static readonly object SharedCompilerWorkerLock = new();
        private static readonly object AutoPrewarmLock = new();
        private static System.Diagnostics.Process _sharedCompilerWorkerProcess;
        private static Dictionary<string, string> _cachedAssemblyLocationsByName;
        private static int _cachedAssemblyCount = -1;
        private static Task _autoPrewarmTask;
        private static bool _hasCompletedAutoPrewarm;

        private readonly DynamicCodeSecurityLevel _securityLevel;
        private readonly CompilationCacheManager _cacheManager = new();
        private bool _disposed;

        /// <summary>
        /// Number of AssemblyBuilder.Build() invocations during the last CompileAsync call.
        /// Used by tests to verify that PreUsingResolver reduces compilation retry count.
        /// </summary>
        internal int LastBuildCount { get; private set; }

        public AssemblyBuilderCompiler(DynamicCodeSecurityLevel securityLevel)
        {
            _securityLevel = securityLevel;
        }

        internal static void RequestAutoPrewarm()
        {
            RequestAutoPrewarmAsync().Forget();
        }

        internal static Task RequestAutoPrewarmAsync()
        {
            lock (AutoPrewarmLock)
            {
                if (_hasCompletedAutoPrewarm)
                {
                    return Task.CompletedTask;
                }

                if (_autoPrewarmTask != null && !_autoPrewarmTask.IsCompleted)
                {
                    return _autoPrewarmTask;
                }

                _autoPrewarmTask = AutoPrewarmAsyncCore();
                return _autoPrewarmTask;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cacheManager.ClearCache();
                _disposed = true;
            }
        }

        public CompilationResult Compile(CompilationRequest request)
        {
            throw new NotSupportedException("Compile blocks Unity's main thread. Use CompileAsync instead.");
        }

        public async Task<CompilationResult> CompileAsync(CompilationRequest request, CancellationToken ct = default)
        {
            Debug.Assert(request != null, "request must not be null");
            Debug.Assert(!string.IsNullOrWhiteSpace(request.Code), "request.Code must not be empty");
            ct.ThrowIfCancellationRequested();
            LastBuildCount = 0;

            string namespaceName = request.Namespace ?? DynamicCodeConstants.DEFAULT_NAMESPACE;
            string className = request.ClassName ?? DynamicCodeConstants.DEFAULT_CLASS_NAME;
            PreparedDynamicCode preparedCode = DynamicCodeSourcePreparer.Prepare(
                request.Code,
                namespaceName,
                className);
            CompilationRequest normalizedRequest = CreateNormalizedRequest(request, preparedCode.PreparedSource, className, namespaceName);

            CompilationResult cachedResult = _cacheManager.CheckCache(normalizedRequest);
            if (cachedResult != null)
            {
                cachedResult.UpdatedCode = preparedCode.PreparedSource;
                return cachedResult;
            }

            if (_securityLevel == DynamicCodeSecurityLevel.Restricted)
            {
                SecurityValidationResult sourceSecurityResult = SourceSecurityScanner.Scan(request.Code);
                if (!sourceSecurityResult.IsValid)
                {
                    return new CompilationResult
                    {
                        Success = false,
                        HasSecurityViolations = true,
                        SecurityViolations = sourceSecurityResult.Violations,
                        UpdatedCode = request.Code,
                        FailureReason = CompilationFailureReason.SecurityViolation,
                        Timings = CreateTimings(0, 0, 0)
                    };
                }
            }

            int id = Interlocked.Increment(ref _compileCounter);
            string uniqueName = $"{className}_{id}";
            string tempDir = Path.Combine("Temp", "uLoopMCPCompilation");
            string sourcePath = Path.Combine(tempDir, $"{uniqueName}.cs");
            string dllPath = Path.Combine(tempDir, $"{uniqueName}.dll");
            bool canDeleteTempFiles = true;
            double referenceResolutionMilliseconds = 0;
            double buildMilliseconds = 0;

            Directory.CreateDirectory(tempDir);

            try
            {
                string wrappedCode = preparedCode.PreparedSource;

                if (wrappedCode == null)
                {
                    return new CompilationResult
                    {
                        Success = false,
                        Errors = new List<CompilationError>
                        {
                            new CompilationError
                            {
                                Message = "Mixed top-level statements and type declarations are not supported. Use a full class definition instead.",
                                ErrorCode = "MIXED_MODE",
                                Line = 0,
                                Column = 0
                            }
                        },
                        FailureReason = CompilationFailureReason.CompilationError,
                        UpdatedCode = request.Code,
                        Timings = CreateTimings(referenceResolutionMilliseconds, buildMilliseconds, 0)
                    };
                }

                bool isScriptMode = preparedCode.IsScriptMode;
                string originalWrappedCode = wrappedCode;
                bool preUsingAdded = false;
                PreUsingResult preUsingResult = null;

                Stopwatch initialReferenceResolutionStopwatch = Stopwatch.StartNew();
                List<string> initialReferences;
                if (isScriptMode)
                {
                    preUsingResult = PreUsingResolver.Resolve(wrappedCode, AssemblyTypeIndex.Instance);
                    preUsingAdded = !ReferenceEquals(preUsingResult.UpdatedSource, wrappedCode);
                    wrappedCode = preUsingResult.UpdatedSource;
                    initialReferences = BuildReferenceSet(
                        request.AdditionalReferences,
                        preUsingResult.AddedAssemblyReferences);
                }
                else
                {
                    initialReferences = BuildReferenceSet(request.AdditionalReferences, null);
                }
                initialReferenceResolutionStopwatch.Stop();
                referenceResolutionMilliseconds += initialReferenceResolutionStopwatch.Elapsed.TotalMilliseconds;

                async Task<CompilerMessage[]> BuildFunc(
                    string resolvedSourcePath,
                    string resolvedDllPath,
                    List<string> resolvedReferences,
                    CancellationToken cancellationToken)
                {
                    Stopwatch buildStopwatch = Stopwatch.StartNew();
                    CompilerMessage[] compilerMessages = await this.BuildAssemblyAsync(
                        resolvedSourcePath,
                        resolvedDllPath,
                        resolvedReferences,
                        cancellationToken,
                        () => canDeleteTempFiles = false,
                        () => canDeleteTempFiles = true);
                    buildStopwatch.Stop();
                    buildMilliseconds += buildStopwatch.Elapsed.TotalMilliseconds;
                    return compilerMessages;
                }

                AutoUsingResolver resolver = new AutoUsingResolver();
                AutoUsingResult autoResult = await resolver.ResolveAsync(
                    sourcePath,
                    dllPath,
                    wrappedCode,
                    initialReferences,
                    BuildFunc,
                    ct);
                referenceResolutionMilliseconds += autoResult.ReferenceResolutionMilliseconds;

                wrappedCode = autoResult.UpdatedSource;
                CompilerMessage[] messages = autoResult.Messages;
                List<CompilationError> errors = ExtractErrors(messages);
                List<string> warnings = ExtractWarnings(messages);

                bool preUsingRolledBack = false;
                if (errors.Count > 0 && preUsingAdded && HasAmbiguityErrors(errors))
                {
                    Stopwatch rollbackReferenceResolutionStopwatch = Stopwatch.StartNew();
                    List<string> rollbackReferences = BuildReferenceSet(request.AdditionalReferences, null);
                    rollbackReferenceResolutionStopwatch.Stop();
                    referenceResolutionMilliseconds += rollbackReferenceResolutionStopwatch.Elapsed.TotalMilliseconds;

                    AutoUsingResult rollbackResult = await resolver.ResolveAsync(
                        sourcePath,
                        dllPath,
                        originalWrappedCode,
                        rollbackReferences,
                        BuildFunc,
                        ct);
                    referenceResolutionMilliseconds += rollbackResult.ReferenceResolutionMilliseconds;

                    List<CompilationError> rollbackErrors = ExtractErrors(rollbackResult.Messages);
                    if (rollbackErrors.Count < errors.Count)
                    {
                        wrappedCode = rollbackResult.UpdatedSource;
                        messages = rollbackResult.Messages;
                        errors = rollbackErrors;
                        warnings = ExtractWarnings(messages);
                        autoResult = rollbackResult;
                        preUsingRolledBack = true;
                    }
                }

                List<string> autoInjectedNamespaces = MergeAutoInjectedNamespaces(
                    preUsingRolledBack,
                    preUsingResult,
                    autoResult);

                if (errors.Count > 0)
                {
                    return new CompilationResult
                    {
                        Success = false,
                        Errors = errors,
                        Warnings = warnings,
                        UpdatedCode = wrappedCode,
                        FailureReason = CompilationFailureReason.CompilationError,
                        AmbiguousTypeCandidates = autoResult.AmbiguousTypeCandidates,
                        AutoInjectedNamespaces = autoInjectedNamespaces,
                        Timings = CreateTimings(referenceResolutionMilliseconds, buildMilliseconds, 0)
                    };
                }

                byte[] assemblyBytes = File.ReadAllBytes(dllPath);
                Stopwatch assemblyLoadStopwatch = Stopwatch.StartNew();
                if (_securityLevel == DynamicCodeSecurityLevel.Restricted)
                {
                    SecurityValidationResult metadataSecurityResult = ValidateBeforeAssemblyLoad(assemblyBytes);
                    if (!metadataSecurityResult.IsValid)
                    {
                        assemblyLoadStopwatch.Stop();
                        return new CompilationResult
                        {
                            Success = false,
                            HasSecurityViolations = true,
                            SecurityViolations = metadataSecurityResult.Violations,
                            Warnings = warnings,
                            UpdatedCode = wrappedCode,
                            FailureReason = CompilationFailureReason.SecurityViolation,
                            AmbiguousTypeCandidates = autoResult.AmbiguousTypeCandidates,
                            AutoInjectedNamespaces = autoInjectedNamespaces,
                            Timings = CreateTimings(
                                referenceResolutionMilliseconds,
                                buildMilliseconds,
                                assemblyLoadStopwatch.Elapsed.TotalMilliseconds)
                        };
                    }
                }

                Assembly compiledAssembly = Assembly.Load(assemblyBytes);

                if (_securityLevel == DynamicCodeSecurityLevel.Restricted)
                {
                    IlSecurityValidator validator = new IlSecurityValidator();
                    SecurityValidationResult securityResult = validator.Validate(compiledAssembly);

                    if (!securityResult.IsValid)
                    {
                        assemblyLoadStopwatch.Stop();
                        return new CompilationResult
                        {
                            Success = false,
                            HasSecurityViolations = true,
                            SecurityViolations = securityResult.Violations,
                            Warnings = warnings,
                            UpdatedCode = wrappedCode,
                            FailureReason = CompilationFailureReason.SecurityViolation,
                            AmbiguousTypeCandidates = autoResult.AmbiguousTypeCandidates,
                            AutoInjectedNamespaces = autoInjectedNamespaces,
                            Timings = CreateTimings(
                                referenceResolutionMilliseconds,
                                buildMilliseconds,
                                assemblyLoadStopwatch.Elapsed.TotalMilliseconds)
                        };
                    }
                }
                assemblyLoadStopwatch.Stop();

                CompilationResult result = new CompilationResult
                {
                    Success = true,
                    CompiledAssembly = compiledAssembly,
                    Warnings = warnings,
                    UpdatedCode = wrappedCode,
                    AmbiguousTypeCandidates = autoResult.AmbiguousTypeCandidates,
                    AutoInjectedNamespaces = autoInjectedNamespaces,
                    Timings = CreateTimings(
                        referenceResolutionMilliseconds,
                        buildMilliseconds,
                        assemblyLoadStopwatch.Elapsed.TotalMilliseconds)
                };

                _cacheManager.CacheResultIfSuccessful(result, normalizedRequest);
                return result;
            }
            finally
            {
                if (canDeleteTempFiles)
                {
                    File.Delete(sourcePath);
                    File.Delete(dllPath);
                    File.Delete(Path.ChangeExtension(dllPath, ".pdb"));
                }
            }
        }

        private async Task<CompilerMessage[]> BuildAssemblyAsync(
            string sourcePath,
            string dllPath,
            List<string> references,
            CancellationToken ct,
            Action markBuildStarted,
            Action markBuildFinished)
        {
            ExternalCompilerPaths externalCompilerPaths = ResolveExternalCompilerPaths();
            if (externalCompilerPaths != null)
            {
                return await this.BuildAssemblyWithExternalCompilerAsync(
                    sourcePath,
                    dllPath,
                    references,
                    externalCompilerPaths,
                    ct,
                    markBuildStarted,
                    markBuildFinished);
            }

            return await this.BuildAssemblyWithAssemblyBuilderAsync(
                sourcePath,
                dllPath,
                references,
                ct,
                markBuildStarted,
                markBuildFinished);
        }

        private static async Task AutoPrewarmAsyncCore()
        {
            if (ResolveExternalCompilerPaths() == null)
            {
                lock (AutoPrewarmLock)
                {
                    _hasCompletedAutoPrewarm = true;
                }

                return;
            }

            await EditorDelay.DelayFrame(AutoPrewarmDelayFrameCount, CancellationToken.None);

            using IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.Restricted);
            ExecutionResult result = await executor.ExecuteCodeAsync(
                AutoPrewarmCode,
                AutoPrewarmClassName,
                null,
                CancellationToken.None,
                false);

            if (!result.Success)
            {
                return;
            }

            lock (AutoPrewarmLock)
            {
                _hasCompletedAutoPrewarm = true;
            }
        }

        private Task<CompilerMessage[]> BuildAssemblyWithExternalCompilerAsync(
            string sourcePath,
            string dllPath,
            List<string> references,
            ExternalCompilerPaths externalCompilerPaths,
            CancellationToken ct,
            Action markBuildStarted,
            Action markBuildFinished)
        {
            string responseFilePath = Path.ChangeExtension(sourcePath, ".rsp");
            string workerRequestFilePath = Path.ChangeExtension(sourcePath, ".worker");
            WriteCompilerResponseFile(responseFilePath, sourcePath, dllPath, references);
            WriteSharedCompilerWorkerRequestFile(workerRequestFilePath, sourcePath, dllPath, references);

            try
            {
                CompilerMessage[] workerMessages = TryBuildAssemblyWithSharedCompilerWorker(
                    workerRequestFilePath,
                    externalCompilerPaths,
                    ct,
                    markBuildStarted,
                    markBuildFinished);
                if (workerMessages != null)
                {
                    return Task.FromResult(workerMessages);
                }

                ct.ThrowIfCancellationRequested();
                LastBuildCount++;

                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
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
                using System.Diagnostics.Process process = ProcessStartHelper.TryStart(startInfo);
                if (process == null)
                {
                    markBuildFinished();
                    return Task.FromResult(new CompilerMessage[]
                    {
                        new CompilerMessage
                        {
                            type = CompilerMessageType.Error,
                            message = "External C# compiler failed to start"
                        }
                    });
                }

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                markBuildFinished();
                ct.ThrowIfCancellationRequested();

                return Task.FromResult(ParseExternalCompilerMessages(stdout, stderr, process.ExitCode));
            }
            finally
            {
                if (File.Exists(responseFilePath))
                {
                    File.Delete(responseFilePath);
                }

                if (File.Exists(workerRequestFilePath))
                {
                    File.Delete(workerRequestFilePath);
                }
            }
        }

        private CompilerMessage[] TryBuildAssemblyWithSharedCompilerWorker(
            string responseFilePath,
            ExternalCompilerPaths externalCompilerPaths,
            CancellationToken ct,
            Action markBuildStarted,
            Action markBuildFinished)
        {
            if (UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WindowsEditor)
            {
                return null;
            }

            lock (SharedCompilerWorkerLock)
            {
                EnsureSharedCompilerWorkerStarted(externalCompilerPaths);
                if (_sharedCompilerWorkerProcess == null || _sharedCompilerWorkerProcess.HasExited)
                {
                    return null;
                }

                ct.ThrowIfCancellationRequested();
                LastBuildCount++;
                markBuildStarted();
                try
                {
                    _sharedCompilerWorkerProcess.StandardInput.WriteLine(Path.GetFullPath(responseFilePath));
                    _sharedCompilerWorkerProcess.StandardInput.Flush();

                    string header = ReadSharedCompilerWorkerLine(_sharedCompilerWorkerProcess.StandardOutput, ct);
                    if (string.IsNullOrEmpty(header))
                    {
                        ShutdownSharedCompilerWorker();
                        return null;
                    }

                    List<string> outputLines = new List<string>();
                    while (true)
                    {
                        string line = ReadSharedCompilerWorkerLine(_sharedCompilerWorkerProcess.StandardOutput, ct);
                        if (line == null)
                        {
                            ShutdownSharedCompilerWorker();
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
                        ShutdownSharedCompilerWorker();
                        return null;
                    }

                    string statusText = header.Substring(SharedCompilerWorkerResultPrefix.Length).Trim();
                    int exitCode = int.Parse(statusText);
                    string combinedOutput = string.Join("\n", outputLines);
                    return ParseExternalCompilerMessages(combinedOutput, string.Empty, exitCode);
                }
                finally
                {
                    markBuildFinished();
                }
            }
        }

        private async Task<CompilerMessage[]> BuildAssemblyWithAssemblyBuilderAsync(
            string sourcePath,
            string dllPath,
            List<string> references,
            CancellationToken ct,
            Action markBuildStarted,
            Action markBuildFinished)
        {
            TaskCompletionSource<CompilerMessage[]> tcs = new();
            ct.ThrowIfCancellationRequested();
            LastBuildCount++;

            string[] referenceArray = references != null
                ? MergeReferencesByAssemblyName(Array.Empty<string>(), references)
                : Array.Empty<string>();

            AssemblyBuilder builder = new AssemblyBuilder(dllPath, sourcePath)
            {
                referencesOptions = ReferencesOptions.UseEngineModules,
                additionalReferences = referenceArray
            };

            builder.buildFinished += (string assemblyPath, CompilerMessage[] compilerMessages) =>
            {
                tcs.TrySetResult(compilerMessages);
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
            CompilerMessage[] messages = await tcs.Task.ConfigureAwait(false);
            markBuildFinished();
            ct.ThrowIfCancellationRequested();
            return messages;
        }

        private static ExternalCompilerPaths ResolveExternalCompilerPaths()
        {
            string editorPath = EditorApplication.applicationPath;
            if (string.IsNullOrEmpty(editorPath))
            {
                return null;
            }

            string contentsPath = editorPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(editorPath, "Contents")
                : Path.GetDirectoryName(Path.GetDirectoryName(editorPath));
            if (string.IsNullOrEmpty(contentsPath))
            {
                return null;
            }

            string dotnetHostFileName = UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WindowsEditor
                ? "dotnet.exe"
                : "dotnet";
            string dotnetHostPath = Path.Combine(contentsPath, "NetCoreRuntime", dotnetHostFileName);
            string compilerDllPath = Path.Combine(contentsPath, "DotNetSdkRoslyn", "csc.dll");
            string compilerRuntimeConfigPath = Path.Combine(contentsPath, "DotNetSdkRoslyn", "csc.runtimeconfig.json");
            string compilerDepsFilePath = Path.Combine(contentsPath, "DotNetSdkRoslyn", "csc.deps.json");
            string codeAnalysisDllPath = Path.Combine(contentsPath, "DotNetSdkRoslyn", "Microsoft.CodeAnalysis.dll");
            string codeAnalysisCSharpDllPath = Path.Combine(contentsPath, "DotNetSdkRoslyn", "Microsoft.CodeAnalysis.CSharp.dll");
            string netCoreRuntimeSharedRootPath = Path.Combine(contentsPath, "NetCoreRuntime", "shared", "Microsoft.NETCore.App");
            string[] runtimeDirectories = Directory.Exists(netCoreRuntimeSharedRootPath)
                ? Directory.GetDirectories(netCoreRuntimeSharedRootPath)
                : Array.Empty<string>();
            string netCoreRuntimeSharedDirectoryPath = runtimeDirectories.Length > 0
                ? runtimeDirectories[0]
                : null;

            if (!File.Exists(dotnetHostPath)
                || !File.Exists(compilerDllPath)
                || !File.Exists(compilerRuntimeConfigPath)
                || !File.Exists(compilerDepsFilePath)
                || !File.Exists(codeAnalysisDllPath)
                || !File.Exists(codeAnalysisCSharpDllPath)
                || string.IsNullOrEmpty(netCoreRuntimeSharedDirectoryPath))
            {
                return null;
            }

            return new ExternalCompilerPaths(
                contentsPath,
                dotnetHostPath,
                compilerDllPath,
                compilerRuntimeConfigPath,
                compilerDepsFilePath,
                codeAnalysisDllPath,
                codeAnalysisCSharpDllPath,
                netCoreRuntimeSharedDirectoryPath);
        }

        [InitializeOnLoadMethod]
        private static void RegisterSharedCompilerWorkerLifecycle()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ShutdownSharedCompilerWorkerForReload;
            AssemblyReloadEvents.beforeAssemblyReload += ShutdownSharedCompilerWorkerForReload;
        }

        private static void EnsureSharedCompilerWorkerStarted(ExternalCompilerPaths externalCompilerPaths)
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
            string workerSource = CreateSharedCompilerWorkerProgramSource();

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
                CompilerMessage[] buildMessages = BuildSharedCompilerWorkerAssembly(
                    externalCompilerPaths,
                    workerSourcePath,
                    workerAssemblyPath,
                    workerCompileResponseFilePath);
                if (HasErrorMessages(buildMessages))
                {
                    return;
                }
            }

            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
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
        }

        private static string CreateSharedCompilerWorkerProgramSource()
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

        private static CompilerMessage[] BuildSharedCompilerWorkerAssembly(
            ExternalCompilerPaths externalCompilerPaths,
            string workerSourcePath,
            string workerAssemblyPath,
            string workerCompileResponseFilePath)
        {
            WriteWorkerCompilerResponseFile(
                workerCompileResponseFilePath,
                workerSourcePath,
                workerAssemblyPath,
                BuildSharedCompilerWorkerReferenceSet(externalCompilerPaths));

            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = externalCompilerPaths.DotnetHostPath,
                Arguments = $"{QuoteCommandLineArgument(externalCompilerPaths.CompilerDllPath)} @{QuoteCommandLineArgument(workerCompileResponseFilePath)}",
                WorkingDirectory = Path.GetDirectoryName(workerSourcePath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using System.Diagnostics.Process process = ProcessStartHelper.TryStart(startInfo);
            if (process == null)
            {
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
            return ParseExternalCompilerMessages(stdout, stderr, process.ExitCode);
        }

        private static List<string> BuildSharedCompilerWorkerReferenceSet(ExternalCompilerPaths externalCompilerPaths)
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

            AddIfExists(
                references,
                Path.Combine(sharedRuntimeDirectoryPath, "System.Collections.Immutable.dll"));
            AddIfExists(
                references,
                Path.Combine(sharedRuntimeDirectoryPath, "System.Reflection.Metadata.dll"));
            AddIfExists(
                references,
                Path.Combine(sharedRuntimeDirectoryPath, "System.Runtime.CompilerServices.Unsafe.dll"));
            AddIfExists(
                references,
                Path.Combine(sharedRuntimeDirectoryPath, "System.Memory.dll"));
            AddIfExists(
                references,
                Path.Combine(sharedRuntimeDirectoryPath, "System.Buffers.dll"));
            AddIfExists(
                references,
                Path.Combine(sharedRuntimeDirectoryPath, "System.Threading.Tasks.Extensions.dll"));

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

        private static void WriteSharedCompilerWorkerRequestFile(
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

        private static string ReadSharedCompilerWorkerLine(
            StreamReader reader,
            CancellationToken ct)
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

        private static bool HasErrorMessages(IReadOnlyCollection<CompilerMessage> messages)
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

        private static void ShutdownSharedCompilerWorkerForReload()
        {
            ShutdownSharedCompilerWorker();
        }

        private static void ShutdownSharedCompilerWorker()
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

        private static CompilerMessage[] ParseExternalCompilerMessages(
            string stdout,
            string stderr,
            int exitCode)
        {
            List<CompilerMessage> messages = new List<CompilerMessage>();
            AddParsedExternalCompilerMessages(messages, stdout);
            AddParsedExternalCompilerMessages(messages, stderr);

            if (messages.Count > 0)
            {
                return messages.ToArray();
            }

            if (exitCode == 0)
            {
                return Array.Empty<CompilerMessage>();
            }

            string combinedOutput = CombineCompilerOutput(stdout, stderr);
            return new CompilerMessage[]
            {
                new CompilerMessage
                {
                    type = CompilerMessageType.Error,
                    message = string.IsNullOrWhiteSpace(combinedOutput)
                        ? "External C# compiler failed without diagnostics"
                        : combinedOutput
                }
            };
        }

        private static void AddParsedExternalCompilerMessages(
            List<CompilerMessage> messages,
            string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return;
            }

            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                Match match = ExternalCompilerDiagnosticRegex.Match(line.Trim());
                if (!match.Success)
                {
                    continue;
                }

                messages.Add(new CompilerMessage
                {
                    type = match.Groups["severity"].Value == "warning"
                        ? CompilerMessageType.Warning
                        : CompilerMessageType.Error,
                    message = $"{match.Groups["code"].Value}: {match.Groups["message"].Value}",
                    file = match.Groups["file"].Value,
                    line = int.Parse(match.Groups["line"].Value),
                    column = int.Parse(match.Groups["column"].Value)
                });
            }
        }

        private static string CombineCompilerOutput(string stdout, string stderr)
        {
            if (string.IsNullOrWhiteSpace(stdout))
            {
                return stderr?.Trim();
            }

            if (string.IsNullOrWhiteSpace(stderr))
            {
                return stdout.Trim();
            }

            return $"{stdout.Trim()}\n{stderr.Trim()}";
        }

        private static SecurityValidationResult ValidateBeforeAssemblyLoad(byte[] assemblyBytes)
        {
            Debug.Assert(assemblyBytes != null, "assemblyBytes must not be null");

            IPreloadAssemblySecurityValidator registeredValidator;
            if (PreloadAssemblySecurityValidatorRegistry.TryGetValidator(out registeredValidator))
            {
                return registeredValidator.Validate(assemblyBytes);
            }

            SystemReflectionMetadataPreloadValidator fallbackValidator = new SystemReflectionMetadataPreloadValidator();
            return fallbackValidator.Validate(assemblyBytes);
        }

        private List<string> BuildReferenceSet(
            List<string> additionalReferences,
            IReadOnlyCollection<string> resolvedAssemblyReferences)
        {
            Dictionary<string, string> assemblyLocationsByName = GetCachedAssemblyLocationsByName();
            List<string> baseReferences = new List<string>();
            ExternalCompilerPaths externalCompilerPaths = ResolveExternalCompilerPaths();

            foreach (string assemblyName in BaseReferenceAssemblyNames)
            {
                string preferredReferencePath = GetPreferredBaseReferencePath(externalCompilerPaths, assemblyName);
                if (!string.IsNullOrEmpty(preferredReferencePath) && File.Exists(preferredReferencePath))
                {
                    baseReferences.Add(preferredReferencePath);
                    continue;
                }

                if (assemblyLocationsByName.TryGetValue(assemblyName, out string loadedAssemblyPath))
                {
                    baseReferences.Add(loadedAssemblyPath);
                }
            }

            List<string> mergedAdditionalReferences = new List<string>();
            AddExistingReferences(mergedAdditionalReferences, additionalReferences);
            AddExistingReferences(mergedAdditionalReferences, resolvedAssemblyReferences);

            string[] mergedReferences = MergeReferencesByAssemblyName(
                baseReferences.ToArray(),
                mergedAdditionalReferences);

            return new List<string>(mergedReferences);
        }

        internal static string[] MergeReferencesByAssemblyName(string[] baseReferences, List<string> additionalRefs)
        {
            Debug.Assert(baseReferences != null, "baseReferences must not be null");
            Debug.Assert(additionalRefs != null, "additionalRefs must not be null");

            HashSet<string> seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> refs = new List<string>(baseReferences.Length + additionalRefs.Count);

            foreach (string baseRef in baseReferences)
            {
                string name = Path.GetFileNameWithoutExtension(baseRef);
                if (seenNames.Add(name))
                {
                    refs.Add(baseRef);
                }
            }

            foreach (string refPath in additionalRefs)
            {
                if (string.IsNullOrEmpty(refPath) || !File.Exists(refPath))
                {
                    continue;
                }

                string name = Path.GetFileNameWithoutExtension(refPath);
                if (seenNames.Add(name))
                {
                    refs.Add(refPath);
                }
            }

            return refs.ToArray();
        }

        [InitializeOnLoadMethod]
        private static void InvalidateReferenceCacheOnDomainReload()
        {
            lock (ReferenceCatalogLock)
            {
                _cachedAssemblyLocationsByName = null;
                _cachedAssemblyCount = -1;
            }
        }

        private static Dictionary<string, string> GetCachedAssemblyLocationsByName()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            lock (ReferenceCatalogLock)
            {
                if (_cachedAssemblyLocationsByName != null && _cachedAssemblyCount == assemblies.Length)
                {
                    return _cachedAssemblyLocationsByName;
                }

                Dictionary<string, string> assemblyLocationsByName =
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (Assembly assembly in assemblies)
                {
                    if (assembly.IsDynamic)
                    {
                        continue;
                    }

                    string location;
                    try
                    {
                        location = assembly.Location;
                    }
                    catch (NotSupportedException)
                    {
                        continue;
                    }

                    string assemblyName = assembly.GetName().Name;
                    if (string.IsNullOrEmpty(location) ||
                        !File.Exists(location) ||
                        string.IsNullOrEmpty(assemblyName) ||
                        assemblyLocationsByName.ContainsKey(assemblyName))
                    {
                        continue;
                    }

                    assemblyLocationsByName.Add(assemblyName, location);
                }

                _cachedAssemblyLocationsByName = assemblyLocationsByName;
                _cachedAssemblyCount = assemblies.Length;
                return _cachedAssemblyLocationsByName;
            }
        }

        private static string GetPreferredBaseReferencePath(
            ExternalCompilerPaths externalCompilerPaths,
            string assemblyName)
        {
            if (externalCompilerPaths == null)
            {
                return null;
            }

            string contentsPath = externalCompilerPaths.EditorContentsPath;
            switch (assemblyName)
            {
                case "mscorlib":
                case "netstandard":
                case "System":
                case "System.Core":
                case "System.Net.Http":
                    return Path.Combine(contentsPath, "UnityReferenceAssemblies", "unity-4.8-api", $"{assemblyName}.dll");
                case "System.Runtime":
                case "System.Collections":
                case "System.Threading":
                case "System.Threading.Tasks":
                    return Path.Combine(contentsPath, "UnityReferenceAssemblies", "unity-4.8-api", "Facades", $"{assemblyName}.dll");
                case "UnityEngine":
                case "UnityEditor":
                    return Path.Combine(contentsPath, "Managed", $"{assemblyName}.dll");
                case "UnityEngine.CoreModule":
                case "UnityEditor.CoreModule":
                    return Path.Combine(contentsPath, "Managed", "UnityEngine", $"{assemblyName}.dll");
                default:
                    return null;
            }
        }

        private static void AddExistingReferences(
            List<string> destination,
            IReadOnlyCollection<string> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (string reference in source)
            {
                if (string.IsNullOrEmpty(reference) || !File.Exists(reference))
                {
                    continue;
                }

                destination.Add(reference);
            }
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

        private static CompilationRequest CreateNormalizedRequest(
            CompilationRequest request,
            string preparedSource,
            string className,
            string namespaceName)
        {
            return new CompilationRequest
            {
                Code = preparedSource ?? request.Code,
                ClassName = className,
                Namespace = namespaceName,
                AdditionalReferences = request.AdditionalReferences != null
                    ? new List<string>(request.AdditionalReferences)
                    : new List<string>(),
                AssemblyMode = request.AssemblyMode
            };
        }

        private static List<CompilationError> ExtractErrors(CompilerMessage[] messages)
        {
            List<CompilationError> errors = new List<CompilationError>();
            foreach (CompilerMessage msg in messages)
            {
                if (msg.type == CompilerMessageType.Error)
                {
                    errors.Add(new CompilationError
                    {
                        Message = msg.message,
                        ErrorCode = ExtractErrorCode(msg.message),
                        Line = msg.line,
                        Column = msg.column
                    });
                }
            }

            return errors;
        }

        private static List<string> ExtractWarnings(CompilerMessage[] messages)
        {
            List<string> warnings = new List<string>();
            foreach (CompilerMessage msg in messages)
            {
                if (msg.type == CompilerMessageType.Warning)
                {
                    warnings.Add(msg.message);
                }
            }

            return warnings;
        }

        private static string ExtractErrorCode(string message)
        {
            int csIndex = message.IndexOf("CS", StringComparison.Ordinal);
            if (csIndex >= 0 && csIndex + 6 <= message.Length)
            {
                string candidate = message.Substring(csIndex, 6);
                if (candidate.Length == 6 && candidate[2] >= '0' && candidate[2] <= '9')
                {
                    return candidate;
                }
            }

            return "UNKNOWN";
        }

        private static bool HasAmbiguityErrors(List<CompilationError> errors)
        {
            foreach (CompilationError error in errors)
            {
                if (error.ErrorCode == "CS0104" || error.ErrorCode == "CS0234")
                {
                    return true;
                }
            }

            return false;
        }

        private static List<string> MergeAutoInjectedNamespaces(
            bool preUsingRolledBack,
            PreUsingResult preUsingResult,
            AutoUsingResult autoResult)
        {
            List<string> merged = new List<string>();

            if (!preUsingRolledBack && preUsingResult != null && preUsingResult.AddedNamespaces.Count > 0)
            {
                foreach (string ns in preUsingResult.AddedNamespaces)
                {
                    merged.Add(ns);
                }
            }

            foreach (string ns in autoResult.AddedNamespaces)
            {
                if (!merged.Contains(ns))
                {
                    merged.Add(ns);
                }
            }

            return merged;
        }

        private static List<string> CreateTimings(
            double referenceResolutionMilliseconds,
            double buildMilliseconds,
            double assemblyLoadMilliseconds)
        {
            return new List<string>
            {
                $"[Perf] ReferenceResolution: {referenceResolutionMilliseconds:F1}ms",
                $"[Perf] Build: {buildMilliseconds:F1}ms",
                $"[Perf] AssemblyLoad: {assemblyLoadMilliseconds:F1}ms"
            };
        }

        private sealed class ExternalCompilerPaths
        {
            public string EditorContentsPath { get; }
            public string DotnetHostPath { get; }
            public string CompilerDllPath { get; }
            public string CompilerRuntimeConfigPath { get; }
            public string CompilerDepsFilePath { get; }
            public string CodeAnalysisDllPath { get; }
            public string CodeAnalysisCSharpDllPath { get; }
            public string NetCoreRuntimeSharedDirectoryPath { get; }

            public ExternalCompilerPaths(
                string editorContentsPath,
                string dotnetHostPath,
                string compilerDllPath,
                string compilerRuntimeConfigPath,
                string compilerDepsFilePath,
                string codeAnalysisDllPath,
                string codeAnalysisCSharpDllPath,
                string netCoreRuntimeSharedDirectoryPath)
            {
                EditorContentsPath = editorContentsPath;
                DotnetHostPath = dotnetHostPath;
                CompilerDllPath = compilerDllPath;
                CompilerRuntimeConfigPath = compilerRuntimeConfigPath;
                CompilerDepsFilePath = compilerDepsFilePath;
                CodeAnalysisDllPath = codeAnalysisDllPath;
                CodeAnalysisCSharpDllPath = codeAnalysisCSharpDllPath;
                NetCoreRuntimeSharedDirectoryPath = netCoreRuntimeSharedDirectoryPath;
            }
        }
    }
}
