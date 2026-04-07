using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Dynamic compilation service for execute-dynamic-code.
    /// Keeps the orchestration flow in one place and delegates low-level compiler concerns to helpers.
    /// </summary>
    public sealed class DynamicCodeCompiler : IDynamicCompilationService, IDisposable
    {
        private static int _compileCounter;

        private readonly DynamicCodeSecurityLevel _securityLevel;
        private readonly CompilationCacheManager _cacheManager = new();
        private bool _disposed;

        internal int LastBuildCount { get; private set; }

        public DynamicCodeCompiler(DynamicCodeSecurityLevel securityLevel)
        {
            _securityLevel = securityLevel;
        }

        internal static void RequestAutoPrewarm()
        {
            DynamicCodePrewarmService.Request();
        }

        internal static Task RequestAutoPrewarmAsync()
        {
            return DynamicCodePrewarmService.RequestAsync();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _cacheManager.ClearCache();
            _disposed = true;
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
            CompilationRequest normalizedRequest = CreateNormalizedRequest(
                request,
                preparedCode.PreparedSource,
                className,
                namespaceName);

            CompilationResult cachedResult = TryGetCachedResult(normalizedRequest, preparedCode);
            if (cachedResult != null)
            {
                return cachedResult;
            }

            CompilationResult sourceSecurityFailure = CreateSourceSecurityFailure(request.Code);
            if (sourceSecurityFailure != null)
            {
                return sourceSecurityFailure;
            }

            ExternalCompilerPaths externalCompilerPaths = ExternalCompilerPathResolver.Resolve();
            string tempDirectoryPath = Path.Combine("Temp", "uLoopMCPCompilation");
            string uniqueName = $"{className}_{Interlocked.Increment(ref _compileCounter)}";
            string sourcePath = Path.Combine(tempDirectoryPath, $"{uniqueName}.cs");
            string dllPath = Path.Combine(tempDirectoryPath, $"{uniqueName}.dll");
            bool canDeleteTempFiles = true;
            double referenceResolutionMilliseconds = 0;
            double buildMilliseconds = 0;

            Directory.CreateDirectory(tempDirectoryPath);

            try
            {
                string wrappedCode = preparedCode.PreparedSource;
                if (wrappedCode == null)
                {
                    return CreateMixedModeFailureResult(request.Code, referenceResolutionMilliseconds, buildMilliseconds);
                }

                bool isScriptMode = preparedCode.IsScriptMode;
                string originalWrappedCode = wrappedCode;
                bool preUsingAdded = false;
                PreUsingResult preUsingResult = null;

                Stopwatch initialReferenceResolutionStopwatch = Stopwatch.StartNew();
                List<string> initialReferences = BuildInitialReferences(
                    request,
                    isScriptMode,
                    externalCompilerPaths,
                    ref wrappedCode,
                    ref preUsingAdded,
                    ref preUsingResult);
                initialReferenceResolutionStopwatch.Stop();
                referenceResolutionMilliseconds += initialReferenceResolutionStopwatch.Elapsed.TotalMilliseconds;

                async Task<UnityEditor.Compilation.CompilerMessage[]> BuildFunc(
                    string resolvedSourcePath,
                    string resolvedDllPath,
                    List<string> resolvedReferences,
                    CancellationToken cancellationToken)
                {
                    Stopwatch buildStopwatch = Stopwatch.StartNew();
                    UnityEditor.Compilation.CompilerMessage[] compilerMessages = await BuildAssemblyAsync(
                        resolvedSourcePath,
                        resolvedDllPath,
                        resolvedReferences,
                        externalCompilerPaths,
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
                UnityEditor.Compilation.CompilerMessage[] messages = autoResult.Messages;
                CompilerDiagnostics diagnostics = CompilerDiagnostics.FromMessages(messages);

                bool preUsingRolledBack = false;
                if (diagnostics.Errors.Count > 0 && preUsingAdded && diagnostics.HasAmbiguityErrors)
                {
                    Stopwatch rollbackReferenceResolutionStopwatch = Stopwatch.StartNew();
                    List<string> rollbackReferences = DynamicReferenceSetBuilder.BuildReferenceSet(
                        request.AdditionalReferences,
                        null,
                        externalCompilerPaths);
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

                    CompilerDiagnostics rollbackDiagnostics = CompilerDiagnostics.FromMessages(rollbackResult.Messages);
                    if (rollbackDiagnostics.Errors.Count < diagnostics.Errors.Count)
                    {
                        wrappedCode = rollbackResult.UpdatedSource;
                        messages = rollbackResult.Messages;
                        diagnostics = rollbackDiagnostics;
                        autoResult = rollbackResult;
                        preUsingRolledBack = true;
                    }
                }

                List<string> autoInjectedNamespaces = MergeAutoInjectedNamespaces(
                    preUsingRolledBack,
                    preUsingResult,
                    autoResult);

                if (diagnostics.Errors.Count > 0)
                {
                    return new CompilationResult
                    {
                        Success = false,
                        Errors = diagnostics.Errors,
                        Warnings = diagnostics.Warnings,
                        UpdatedCode = wrappedCode,
                        FailureReason = CompilationFailureReason.CompilationError,
                        AmbiguousTypeCandidates = autoResult.AmbiguousTypeCandidates,
                        AutoInjectedNamespaces = autoInjectedNamespaces,
                        Timings = DynamicCompilationTimingFormatter.CreateCompilationTimings(
                            referenceResolutionMilliseconds,
                            buildMilliseconds,
                            0)
                    };
                }

                byte[] assemblyBytes = File.ReadAllBytes(dllPath);
                CompiledAssemblyLoadResult assemblyLoadResult = CompiledAssemblyLoader.Load(
                    _securityLevel,
                    assemblyBytes);
                if (!assemblyLoadResult.Success)
                {
                    return CreateAssemblySecurityFailure(
                        assemblyLoadResult.SecurityViolations,
                        diagnostics.Warnings,
                        wrappedCode,
                        autoResult,
                        autoInjectedNamespaces,
                        referenceResolutionMilliseconds,
                        buildMilliseconds,
                        assemblyLoadResult.AssemblyLoadMilliseconds);
                }

                CompilationResult result = new CompilationResult
                {
                    Success = true,
                    CompiledAssembly = assemblyLoadResult.CompiledAssembly,
                    Warnings = diagnostics.Warnings,
                    UpdatedCode = wrappedCode,
                    AmbiguousTypeCandidates = autoResult.AmbiguousTypeCandidates,
                    AutoInjectedNamespaces = autoInjectedNamespaces,
                    Timings = DynamicCompilationTimingFormatter.CreateCompilationTimings(
                        referenceResolutionMilliseconds,
                        buildMilliseconds,
                        assemblyLoadResult.AssemblyLoadMilliseconds)
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

        internal static string[] MergeReferencesByAssemblyName(string[] baseReferences, List<string> additionalReferences)
        {
            return DynamicReferenceSetBuilder.MergeReferencesByAssemblyName(baseReferences, additionalReferences);
        }

        private CompilationResult TryGetCachedResult(
            CompilationRequest normalizedRequest,
            PreparedDynamicCode preparedCode)
        {
            CompilationResult cachedResult = _cacheManager.CheckCache(normalizedRequest);
            if (cachedResult == null)
            {
                return null;
            }

            cachedResult.UpdatedCode = preparedCode.PreparedSource;
            return cachedResult;
        }

        private CompilationResult CreateSourceSecurityFailure(string originalCode)
        {
            if (_securityLevel != DynamicCodeSecurityLevel.Restricted)
            {
                return null;
            }

            SecurityValidationResult sourceSecurityResult = SourceSecurityScanner.Scan(originalCode);
            if (sourceSecurityResult.IsValid)
            {
                return null;
            }

            return new CompilationResult
            {
                Success = false,
                HasSecurityViolations = true,
                SecurityViolations = sourceSecurityResult.Violations,
                UpdatedCode = originalCode,
                FailureReason = CompilationFailureReason.SecurityViolation,
                Timings = DynamicCompilationTimingFormatter.CreateCompilationTimings(0, 0, 0)
            };
        }

        private static CompilationResult CreateMixedModeFailureResult(
            string originalCode,
            double referenceResolutionMilliseconds,
            double buildMilliseconds)
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
                UpdatedCode = originalCode,
                Timings = DynamicCompilationTimingFormatter.CreateCompilationTimings(
                    referenceResolutionMilliseconds,
                    buildMilliseconds,
                    0)
            };
        }

        private List<string> BuildInitialReferences(
            CompilationRequest request,
            bool isScriptMode,
            ExternalCompilerPaths externalCompilerPaths,
            ref string wrappedCode,
            ref bool preUsingAdded,
            ref PreUsingResult preUsingResult)
        {
            if (!isScriptMode)
            {
                return DynamicReferenceSetBuilder.BuildReferenceSet(
                    request.AdditionalReferences,
                    null,
                    externalCompilerPaths);
            }

            preUsingResult = PreUsingResolver.Resolve(wrappedCode, AssemblyTypeIndex.Instance);
            preUsingAdded = !ReferenceEquals(preUsingResult.UpdatedSource, wrappedCode);
            wrappedCode = preUsingResult.UpdatedSource;
            return DynamicReferenceSetBuilder.BuildReferenceSet(
                request.AdditionalReferences,
                preUsingResult.AddedAssemblyReferences,
                externalCompilerPaths);
        }

        private async Task<UnityEditor.Compilation.CompilerMessage[]> BuildAssemblyAsync(
            string sourcePath,
            string dllPath,
            List<string> references,
            ExternalCompilerPaths externalCompilerPaths,
            CancellationToken ct,
            Action markBuildStarted,
            Action markBuildFinished)
        {
            if (externalCompilerPaths != null)
            {
                return await RoslynCompilerBackend.CompileAsync(
                    sourcePath,
                    dllPath,
                    references,
                    externalCompilerPaths,
                    ct,
                    markBuildStarted,
                    markBuildFinished,
                    IncrementBuildCount);
            }

            return await AssemblyBuilderFallbackCompilerBackend.CompileAsync(
                sourcePath,
                dllPath,
                references,
                ct,
                markBuildStarted,
                markBuildFinished,
                IncrementBuildCount);
        }

        private void IncrementBuildCount()
        {
            LastBuildCount++;
        }

        private static CompilationResult CreateAssemblySecurityFailure(
            List<SecurityViolation> securityViolations,
            List<string> warnings,
            string updatedCode,
            AutoUsingResult autoResult,
            List<string> autoInjectedNamespaces,
            double referenceResolutionMilliseconds,
            double buildMilliseconds,
            double assemblyLoadMilliseconds)
        {
            return new CompilationResult
            {
                Success = false,
                HasSecurityViolations = true,
                SecurityViolations = securityViolations,
                Warnings = warnings,
                UpdatedCode = updatedCode,
                FailureReason = CompilationFailureReason.SecurityViolation,
                AmbiguousTypeCandidates = autoResult.AmbiguousTypeCandidates,
                AutoInjectedNamespaces = autoInjectedNamespaces,
                Timings = DynamicCompilationTimingFormatter.CreateCompilationTimings(
                    referenceResolutionMilliseconds,
                    buildMilliseconds,
                    assemblyLoadMilliseconds)
            };
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

        private static List<string> MergeAutoInjectedNamespaces(
            bool preUsingRolledBack,
            PreUsingResult preUsingResult,
            AutoUsingResult autoResult)
        {
            List<string> mergedNamespaces = new List<string>();

            if (!preUsingRolledBack && preUsingResult != null && preUsingResult.AddedNamespaces.Count > 0)
            {
                foreach (string namespaceName in preUsingResult.AddedNamespaces)
                {
                    mergedNamespaces.Add(namespaceName);
                }
            }

            foreach (string namespaceName in autoResult.AddedNamespaces)
            {
                if (!mergedNamespaces.Contains(namespaceName))
                {
                    mergedNamespaces.Add(namespaceName);
                }
            }

            return mergedNamespaces;
        }
    }
}
