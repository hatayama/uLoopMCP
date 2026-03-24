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
            return CompileAsync(request, CancellationToken.None).GetAwaiter().GetResult();
        }

        public async Task<CompilationResult> CompileAsync(CompilationRequest request, CancellationToken ct = default)
        {
            Debug.Assert(request != null, "request must not be null");
            Debug.Assert(!string.IsNullOrWhiteSpace(request.Code), "request.Code must not be empty");

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

                // Auto-using resolution: compile, detect CS0246/CS0103, add usings, retry
                AutoUsingResolver resolver = new AutoUsingResolver();
                AutoUsingResult autoResult = await resolver.ResolveAsync(
                    sourcePath, dllPath, wrappedCode, request.AdditionalReferences,
                    BuildAssemblyAsync, ct).ConfigureAwait(false);

                wrappedCode = autoResult.UpdatedSource;
                CompilerMessage[] messages = autoResult.Messages;

                List<CompilationError> errors = ExtractErrors(messages);
                List<string> warnings = ExtractWarnings(messages);

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
                    PreloadMetadataSecurityValidator metadataValidator = new PreloadMetadataSecurityValidator();
                    SecurityValidationResult metadataSecurityResult = metadataValidator.Validate(assemblyBytes);

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
                // File.Delete is a no-op if the file does not exist (.NET behavior)
                File.Delete(sourcePath);
                File.Delete(dllPath);
                File.Delete(Path.ChangeExtension(dllPath, ".pdb"));
            }
        }

        private async Task<CompilerMessage[]> BuildAssemblyAsync(
            string sourcePath,
            string dllPath,
            List<string> additionalRefs,
            CancellationToken ct)
        {
            TaskCompletionSource<CompilerMessage[]> tcs = new();

            using CancellationTokenRegistration registration = ct.Register(() =>
                tcs.TrySetCanceled(ct));

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

            return await tcs.Task.ConfigureAwait(false);
        }

        private string[] CollectReferences(List<string> additionalRefs)
        {
            string[] appDomainReferences = GetCachedAppDomainReferences();
            if (additionalRefs == null || additionalRefs.Count == 0)
            {
                return appDomainReferences;
            }

            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            List<string> refs = new(appDomainReferences.Length + additionalRefs.Count);

            foreach (string appDomainReference in appDomainReferences)
            {
                if (seen.Add(appDomainReference))
                {
                    refs.Add(appDomainReference);
                }
            }

            if (additionalRefs != null)
            {
                foreach (string refPath in additionalRefs)
                {
                    if (!string.IsNullOrEmpty(refPath) && File.Exists(refPath) && seen.Add(refPath))
                    {
                        refs.Add(refPath);
                    }
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

                HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
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

                    if (string.IsNullOrEmpty(location) || !File.Exists(location) || !seen.Add(location))
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
            // Extract CS#### pattern from compiler message
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

    }
}
