using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private const string ERROR_UNSUPPORTED_SELECTIVE_REFERENCE = "UNSUPPORTED_MODE";
        private const string ERROR_MESSAGE_UNSUPPORTED_SELECTIVE_REFERENCE =
            "AssemblyBuilder backend does not support SelectiveReference mode";

        private static int _compileCounter;

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

            if (_securityLevel == DynamicCodeSecurityLevel.Disabled)
            {
                return CreateDisabledResult(request);
            }

            if (request.AssemblyMode == AssemblyLoadingMode.SelectiveReference)
            {
                return CreateUnsupportedModeResult(request);
            }

            CompilationResult cachedResult = _cacheManager.CheckCache(request);
            if (cachedResult != null)
            {
                return cachedResult;
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
                File.WriteAllText(sourcePath, wrappedCode);

                CompilerMessage[] messages = await BuildAssemblyAsync(sourcePath, dllPath, request.AdditionalReferences, ct).ConfigureAwait(false);

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
                        FailureReason = CompilationFailureReason.CompilationError
                    };
                }

                byte[] assemblyBytes = File.ReadAllBytes(dllPath);
                Assembly compiledAssembly = Assembly.Load(assemblyBytes);

                CompilationResult result = new CompilationResult
                {
                    Success = true,
                    CompiledAssembly = compiledAssembly,
                    Warnings = warnings,
                    UpdatedCode = wrappedCode
                };

                _cacheManager.CacheResultIfSuccessful(result, request);
                return result;
            }
            finally
            {
                // File.Delete is a no-op if the file does not exist (.NET behavior)
                File.Delete(sourcePath);
                File.Delete(dllPath);
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
            Debug.Assert(started, "AssemblyBuilder.Build() must return true to indicate compilation started");

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
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            List<string> refs = new();

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;

                // Collectible assemblies and in-memory assemblies throw NotSupportedException on Location access
                string location;
                try
                {
                    location = asm.Location;
                }
                catch (NotSupportedException)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(location) || !File.Exists(location)) continue;
                if (!seen.Add(Path.GetFileName(location))) continue;

                refs.Add(location);
            }

            if (additionalRefs != null)
            {
                foreach (string refPath in additionalRefs)
                {
                    if (!string.IsNullOrEmpty(refPath) && File.Exists(refPath) && seen.Add(Path.GetFileName(refPath)))
                    {
                        refs.Add(refPath);
                    }
                }
            }

            return refs.ToArray();
        }

        /// <summary>
        /// Minimal code wrapping for Phase 1. Will be replaced by SourceShaper in Phase 2.
        /// </summary>
        private string WrapCodeIfNeeded(string code, string namespaceName, string className)
        {
            // TODO: Replace with SourceShaper in Phase 2
            string trimmed = code.TrimStart();

            // If user provided a full namespace or class definition, pass through
            if (trimmed.StartsWith("namespace ") || trimmed.StartsWith("public class ") ||
                trimmed.StartsWith("internal class ") || trimmed.StartsWith("class "))
            {
                return code;
            }

            // Otherwise, wrap as top-level statements
            return $@"#pragma warning disable CS0162
#pragma warning disable CS1998
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace {namespaceName}
{{
    public class {className}
    {{
        public async System.Threading.Tasks.Task<object> ExecuteAsync(
            System.Collections.Generic.Dictionary<string, object> parameters = null,
            System.Threading.CancellationToken ct = default)
        {{
#line 1 ""user-snippet.cs""
            {code}
#line default
#line hidden
        }}

        public object Execute(
            System.Collections.Generic.Dictionary<string, object> parameters = null)
        {{
            return ExecuteAsync(parameters, default).GetAwaiter().GetResult();
        }}
    }}
}}";
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

        private static CompilationResult CreateDisabledResult(CompilationRequest request)
        {
            return new CompilationResult
            {
                Success = false,
                Errors = new List<CompilationError>
                {
                    new CompilationError
                    {
                        Message = McpConstants.ERROR_MESSAGE_COMPILATION_DISABLED_LEVEL0,
                        ErrorCode = McpConstants.ERROR_COMPILATION_DISABLED_LEVEL0,
                        Line = 0,
                        Column = 0
                    }
                },
                FailureReason = CompilationFailureReason.CompilationError,
                UpdatedCode = request.Code
            };
        }

        private static CompilationResult CreateUnsupportedModeResult(CompilationRequest request)
        {
            return new CompilationResult
            {
                Success = false,
                Errors = new List<CompilationError>
                {
                    new CompilationError
                    {
                        Message = ERROR_MESSAGE_UNSUPPORTED_SELECTIVE_REFERENCE,
                        ErrorCode = ERROR_UNSUPPORTED_SELECTIVE_REFERENCE,
                        Line = 0,
                        Column = 0
                    }
                },
                FailureReason = CompilationFailureReason.CompilationError,
                UpdatedCode = request.Code
            };
        }
    }
}
