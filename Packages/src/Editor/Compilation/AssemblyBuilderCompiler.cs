using System;
using System.Collections.Generic;
using System.IO;
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
        private static readonly object AppDomainReferencesLock = new();
        private static string[] _cachedAppDomainReferences;
        private static int _cachedAssemblyCount = -1;

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

            CompilationResult cachedResult = _cacheManager.CheckCache(request);
            if (cachedResult != null)
            {
                return cachedResult;
            }

            // Source-level security scan before compilation to prevent dangerous code from executing
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
                        FailureReason = CompilationFailureReason.SecurityViolation
                    };
                }
            }

            string namespaceName = request.Namespace ?? DynamicCodeConstants.DEFAULT_NAMESPACE;
            string className = request.ClassName ?? DynamicCodeConstants.DEFAULT_CLASS_NAME;
            int id = Interlocked.Increment(ref _compileCounter);
            string uniqueName = $"{className}_{id}";
            string tempDir = Path.Combine("Temp", "uLoopMCPCompilation");
            string sourcePath = Path.Combine(tempDir, $"{uniqueName}.cs");
            string dllPath = Path.Combine(tempDir, $"{uniqueName}.dll");
            bool canDeleteTempFiles = true;

            Directory.CreateDirectory(tempDir);

            try
            {
                string wrappedCode = WrapCodeIfNeeded(request.Code, namespaceName, className);

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
                        UpdatedCode = request.Code
                    };
                }

                // Raw mode returns the original reference; script mode returns a new wrapped string
                bool isScriptMode = !ReferenceEquals(wrappedCode, request.Code);

                string originalWrappedCode = wrappedCode;
                bool preUsingAdded = false;
                if (isScriptMode)
                {
                    PreUsingResult preUsingResult = PreUsingResolver.Resolve(wrappedCode, AssemblyTypeIndex.Instance);
                    preUsingAdded = !ReferenceEquals(preUsingResult.UpdatedSource, wrappedCode);
                    wrappedCode = preUsingResult.UpdatedSource;
                }

                Task<CompilerMessage[]> BuildFunc(string sp, string dp, List<string> refs, CancellationToken token) =>
                    this.BuildAssemblyAsync(sp, dp, refs, token,
                        () => canDeleteTempFiles = false, () => canDeleteTempFiles = true);

                AutoUsingResolver resolver = new AutoUsingResolver();
                AutoUsingResult autoResult = await resolver.ResolveAsync(
                    sourcePath, dllPath, wrappedCode, request.AdditionalReferences, BuildFunc, ct);

                wrappedCode = autoResult.UpdatedSource;
                CompilerMessage[] messages = autoResult.Messages;

                List<CompilationError> errors = ExtractErrors(messages);
                List<string> warnings = ExtractWarnings(messages);

                // Pre-using can introduce ambiguity (CS0104) or wrong namespace (CS0234);
                // if that happened, retry with original source to check regression
                if (errors.Count > 0 && preUsingAdded && HasAmbiguityErrors(errors))
                {
                    AutoUsingResult rollbackResult = await resolver.ResolveAsync(
                        sourcePath, dllPath, originalWrappedCode, request.AdditionalReferences, BuildFunc, ct);

                    List<CompilationError> rollbackErrors = ExtractErrors(rollbackResult.Messages);
                    if (rollbackErrors.Count < errors.Count)
                    {
                        wrappedCode = rollbackResult.UpdatedSource;
                        messages = rollbackResult.Messages;
                        errors = rollbackErrors;
                        warnings = ExtractWarnings(messages);
                        autoResult = rollbackResult;
                    }
                }

                if (errors.Count > 0)
                {
                    return new CompilationResult
                    {
                        Success = false,
                        Errors = errors,
                        Warnings = warnings,
                        UpdatedCode = wrappedCode,
                        FailureReason = CompilationFailureReason.CompilationError,
                        AmbiguousTypeCandidates = autoResult.AmbiguousTypeCandidates
                    };
                }

                byte[] assemblyBytes = File.ReadAllBytes(dllPath);
                if (_securityLevel == DynamicCodeSecurityLevel.Restricted)
                {
                    SecurityValidationResult metadataSecurityResult = ValidateBeforeAssemblyLoad(assemblyBytes);
                    if (!metadataSecurityResult.IsValid)
                    {
                        return new CompilationResult
                        {
                            Success = false,
                            HasSecurityViolations = true,
                            SecurityViolations = metadataSecurityResult.Violations,
                            Warnings = warnings,
                            UpdatedCode = wrappedCode,
                            FailureReason = CompilationFailureReason.SecurityViolation,
                            AmbiguousTypeCandidates = autoResult.AmbiguousTypeCandidates
                        };
                    }
                }

                Assembly compiledAssembly = Assembly.Load(assemblyBytes);

                // Security validation via reflection after loading
                if (_securityLevel == DynamicCodeSecurityLevel.Restricted)
                {
                    IlSecurityValidator validator = new IlSecurityValidator();
                    SecurityValidationResult securityResult = validator.Validate(compiledAssembly);

                    if (!securityResult.IsValid)
                    {
                        return new CompilationResult
                        {
                            Success = false,
                            HasSecurityViolations = true,
                            SecurityViolations = securityResult.Violations,
                            Warnings = warnings,
                            UpdatedCode = wrappedCode,
                            FailureReason = CompilationFailureReason.SecurityViolation,
                            AmbiguousTypeCandidates = autoResult.AmbiguousTypeCandidates
                        };
                    }
                }

                CompilationResult result = new CompilationResult
                {
                    Success = true,
                    CompiledAssembly = compiledAssembly,
                    Warnings = warnings,
                    UpdatedCode = wrappedCode,
                    AmbiguousTypeCandidates = autoResult.AmbiguousTypeCandidates
                };

                _cacheManager.CacheResultIfSuccessful(result, request);
                return result;
            }
            finally
            {
                if (canDeleteTempFiles)
                {
                    // File.Delete is a no-op if the file does not exist (.NET behavior)
                    File.Delete(sourcePath);
                    File.Delete(dllPath);
                    File.Delete(Path.ChangeExtension(dllPath, ".pdb"));
                }
            }
        }

        private async Task<CompilerMessage[]> BuildAssemblyAsync(
            string sourcePath,
            string dllPath,
            List<string> additionalRefs,
            CancellationToken ct,
            Action markBuildStarted,
            Action markBuildFinished)
        {
            TaskCompletionSource<CompilerMessage[]> tcs = new();
            ct.ThrowIfCancellationRequested();
            LastBuildCount++;

            string[] references = CollectReferences(additionalRefs);

            AssemblyBuilder builder = new AssemblyBuilder(dllPath, sourcePath)
            {
                referencesOptions = ReferencesOptions.UseEngineModules,
                additionalReferences = references
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
            CompilerMessage[] compilerMessages = await tcs.Task.ConfigureAwait(false);
            markBuildFinished();
            ct.ThrowIfCancellationRequested();
            return compilerMessages;
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

        private string[] CollectReferences(List<string> additionalRefs)
        {
            string[] appDomainReferences = GetCachedAppDomainReferences();
            if (additionalRefs == null || additionalRefs.Count == 0)
            {
                return appDomainReferences;
            }

            return MergeReferencesByAssemblyName(appDomainReferences, additionalRefs);
        }

        internal static string[] MergeReferencesByAssemblyName(string[] baseReferences, List<string> additionalRefs)
        {
            Debug.Assert(baseReferences != null, "baseReferences must not be null");
            Debug.Assert(additionalRefs != null, "additionalRefs must not be null");

            HashSet<string> seenNames = new(StringComparer.OrdinalIgnoreCase);
            List<string> refs = new(baseReferences.Length + additionalRefs.Count);

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
            lock (AppDomainReferencesLock)
            {
                _cachedAppDomainReferences = null;
                _cachedAssemblyCount = -1;
            }
        }

        private static string[] GetCachedAppDomainReferences()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            lock (AppDomainReferencesLock)
            {
                if (_cachedAppDomainReferences != null && _cachedAssemblyCount == assemblies.Length)
                {
                    return _cachedAppDomainReferences;
                }

                // CS1703 prevention: deduplicate by assembly name, not file path.
                // The same assembly (e.g. System.Threading) can exist under both
                // MonoBleedingEdge and NetStandard directories with different paths.
                HashSet<string> seenNames = new(StringComparer.OrdinalIgnoreCase);
                List<string> refs = new();

                foreach (Assembly asm in assemblies)
                {
                    if (asm.IsDynamic)
                    {
                        continue;
                    }

                    string location;
                    try
                    {
                        location = asm.Location;
                    }
                    catch (NotSupportedException)
                    {
                        continue;
                    }

                    string assemblyName = asm.GetName().Name;
                    if (string.IsNullOrEmpty(location) || !File.Exists(location) || !seenNames.Add(assemblyName))
                    {
                        continue;
                    }

                    refs.Add(location);
                }

                _cachedAppDomainReferences = refs.ToArray();
                _cachedAssemblyCount = assemblies.Length;
                return _cachedAppDomainReferences;
            }
        }

        private string WrapCodeIfNeeded(string code, string namespaceName, string className)
        {
            return SourceShaper.WrapIfNeeded(code, namespaceName, className);
        }

        private static List<CompilationError> ExtractErrors(CompilerMessage[] messages)
        {
            List<CompilationError> errors = new();
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
            List<string> warnings = new();
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

    }
}
