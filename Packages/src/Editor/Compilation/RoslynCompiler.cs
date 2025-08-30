#if ULOOPMCP_HAS_ROSLYN
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Dynamic C# compilation functionality using Roslyn
    /// Related Classes: CompilationRequest, CompilationResult, DynamicCodeSecurityManager
    /// </summary>
    public class RoslynCompiler : IDisposable
    {
        // Compilation result cache management (avoiding recompilation of the same code)
        private readonly CompilationCacheManager _cacheManager = new();
        private readonly List<MetadataReference> _defaultReferences = new();
        private readonly DynamicCodeSecurityLevel _currentSecurityLevel;
        private readonly SecurityValidator _securityValidator;
        private bool _disposed;


        public RoslynCompiler(DynamicCodeSecurityLevel securityLevel)
        {
            _currentSecurityLevel = securityLevel;
            _securityValidator = new SecurityValidator(securityLevel);
            InitializeReferencesForLevel(_currentSecurityLevel);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Clear cache
                _cacheManager.ClearReferenceCache();
                _cacheManager.ClearCache();
                _defaultReferences.Clear();

                _disposed = true;
            }
        }

        /// <summary>
        /// Initialize assembly references according to security level.
        /// This method constructs the MetadataReference set used for compilation.
        /// Note: AssemblyReferencePolicy is not consulted here; safety in Restricted mode
        /// is enforced post-compilation by SecurityValidator/DangerousApiDetector.
        /// </summary>
        private void InitializeReferencesForLevel(DynamicCodeSecurityLevel level)
        {
            string correlationId = McpConstants.GenerateCorrelationId();

            // Invalidate reference assembly list cache:
            // - Create a fresh reference list each time (approximately 144ms)
            // - Reason: To reflect the latest Unity assembly state
            // - Note: This is separate from the compilation result cache (CompilationCacheManager)

            List<MetadataReference> references = new();

            if (level == DynamicCodeSecurityLevel.Disabled)
            {
                // Do not add references at the Disabled level
                _defaultReferences.Clear();
                return;
            }

            // Comprehensively collect Unity reference assemblies (independent of AppDomain)
            HashSet<string> addedPaths = new();

            // 1. Collect from Unity reference assembly folders
            AddUnityReferenceAssemblies(references, addedPaths);

            // 2. Also add currently loaded assemblies
            // (Complement assemblies dynamically loaded or plugins not in Unity folders)
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
                {
                    if (addedPaths.Add(assembly.Location))
                    {
                        MetadataReference reference = MetadataReference.CreateFromFile(assembly.Location);
                        references.Add(reference);
                    }
                }
            }

            // 3. Also add current assembly (for uLoopMCP class access)
            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            if (!string.IsNullOrWhiteSpace(currentAssembly.Location))
            {
                if (addedPaths.Add(currentAssembly.Location))
                {
                    MetadataReference currentRef = MetadataReference.CreateFromFile(currentAssembly.Location);
                    references.Add(currentRef);
                }
            }

            _defaultReferences.Clear();
            _defaultReferences.AddRange(references);

            
        }

        /// <summary>
        /// Comprehensively collect Unity reference assemblies
        /// Collect directly from Unity installation folders, independent of AppDomain
        /// </summary>
        private void AddUnityReferenceAssemblies(List<MetadataReference> references, HashSet<string> addedPaths)
        {
            string correlationId = McpConstants.GenerateCorrelationId();

            try
            {
                string contentsPath = GetUnityContentsPath();

                List<string> searchPaths = BuildSearchPaths(contentsPath);

                foreach (string dllPath in EnumerateDllPaths(searchPaths))
                {
                    if (addedPaths.Add(dllPath))
                    {
                        MetadataReference reference = TryCreateReference(dllPath);
                        if (reference != null)
                        {
                            references.Add(reference);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                VibeLogger.LogWarning(
                    "roslyn_unity_references_failed",
                    $"Failed to add Unity reference assemblies: {ex.Message}",
                    new { error = ex.Message },
                    correlationId,
                    "Falling back to AppDomain assemblies only",
                    "Review Unity installation path detection"
                );
            }
        }

        private string GetUnityContentsPath()
        {
            string unityPath = UnityEditor.EditorApplication.applicationPath;
            string contentsPath = string.Empty;

#if UNITY_EDITOR_OSX
            // macOS: Unity.app/Contents
            // Unity.app is an application bundle, so directly add Contents
            contentsPath = Path.Combine(unityPath, "Contents");
#elif UNITY_EDITOR_WIN
            // Windows: Editor/Data
            contentsPath = Path.Combine(
                Path.GetDirectoryName(unityPath),
                "Data"
            );
#elif UNITY_EDITOR_LINUX
            // Linux: Editor/Data
            contentsPath = Path.Combine(
                Path.GetDirectoryName(unityPath),
                "Data"
            );
#endif

            return contentsPath;
        }

        private List<string> BuildSearchPaths(string contentsPath)
        {
            List<string> searchPaths = new();

            // .NET Framework 4.x reference assemblies
            string monoApi = Path.Combine(contentsPath, "MonoBleedingEdge", "lib", "mono", "4.7.1-api");
            string monoFacades = Path.Combine(monoApi, "Facades");
            string monoUnityjit = Path.Combine(contentsPath, "MonoBleedingEdge", "lib", "mono", "unityjit-macos");

            // .NET Standard reference assemblies
            string netStandard21 = Path.Combine(contentsPath, "NetStandard", "ref", "2.1.0");
            string netStandard20 = Path.Combine(contentsPath, "NetStandard", "ref", "2.0.0");
            string netStandardCompat = Path.Combine(contentsPath, "NetStandard", "compat", "shims", "net472");

            // Unity Managed assemblies
            string managed = Path.Combine(contentsPath, "Managed");
            string unityEngine = Path.Combine(managed, "UnityEngine");
            string unityEditor = Path.Combine(managed, "UnityEditor");

            // Project assemblies
            string scriptAssemblies = Path.Combine(UnityEngine.Application.dataPath, "..", "Library", "ScriptAssemblies");

            // Add existing directories
            AddDirectoryIfExists(searchPaths, monoApi);
            AddDirectoryIfExists(searchPaths, monoFacades);
            AddDirectoryIfExists(searchPaths, monoUnityjit);
            AddDirectoryIfExists(searchPaths, netStandard21);
            AddDirectoryIfExists(searchPaths, netStandard20);
            AddDirectoryIfExists(searchPaths, netStandardCompat);
            AddDirectoryIfExists(searchPaths, managed);
            AddDirectoryIfExists(searchPaths, unityEngine);
            AddDirectoryIfExists(searchPaths, unityEditor);
            AddDirectoryIfExists(searchPaths, scriptAssemblies);

            return searchPaths;
        }

        private IEnumerable<string> EnumerateDllPaths(IEnumerable<string> searchPaths)
        {
            foreach (string searchPath in searchPaths)
            {
                if (Directory.Exists(searchPath))
                {
                    foreach (string dllPath in Directory.GetFiles(searchPath, "*.dll"))
                    {
                        yield return dllPath;
                    }
                }
            }
        }

        private MetadataReference TryCreateReference(string dllPath)
        {
            try
            {
                return MetadataReference.CreateFromFile(dllPath);
            }
            catch
            {
                // Skip unloadable DLLs (native DLLs etc.)
                // This is normal behavior (native DLLs cannot be loaded)
                return null;
            }
        }

        /// <summary>
        /// Add to list only if directory exists
        /// </summary>
        private void AddDirectoryIfExists(List<string> list, string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                list.Add(path);
            }
        }


        public CompilationResult Compile(CompilationRequest request)
        {
            string correlationId = GenerateCorrelationId();

            try
            {
                LogCompilationStart(request, correlationId);

                // Check compilation result cache (do not recompile the same code)
                CompilationResult cachedResult = CheckCache(request);
                if (cachedResult != null) return cachedResult;  // Cache hit

                CompilationContext context = PrepareCompilation(request);
                CompilationResult result = ExecuteCompilation(context, correlationId);

                // Save successful compilation result to cache
                CacheResultIfSuccessful(request, result);

                return result;
            }
            catch (Exception ex)
            {
                return HandleCompilationException(ex, request, correlationId);
            }
        }

        public void ClearCache()
        {
            _cacheManager.ClearCache();
        }

        private string GenerateCorrelationId()
        {
            return McpConstants.GenerateCorrelationId();
        }

        private void LogCompilationStart(CompilationRequest request, string correlationId)
        {
            
        }

        private CompilationResult CheckCache(CompilationRequest request)
        {
            return _cacheManager.CheckCache(request);
        }

        private CompilationContext PrepareCompilation(CompilationRequest request)
        {
            string wrappedCode = WrapCodeIfNeeded(request.Code, request.Namespace, request.ClassName);
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(wrappedCode);

            // Prepare references according to assembly loading mode
            // Note: Even in AllAssemblies mode, all assemblies have already been added to _defaultReferences in the constructor
            List<MetadataReference> references = PrepareReferences(request.AdditionalReferences);

            return new CompilationContext
            {
                WrappedCode = wrappedCode,
                SyntaxTree = syntaxTree,
                References = references
            };
        }

        private List<MetadataReference> PrepareReferences(List<string> additionalReferences)
        {
            List<MetadataReference> references = new List<MetadataReference>(_defaultReferences);

            foreach (string additionalRef in additionalReferences ?? new List<string>())
            {
                if (File.Exists(additionalRef))
                {
                    references.Add(MetadataReference.CreateFromFile(additionalRef));
                }
            }

            return references;
        }

        /// <summary>
        /// Apply diagnostic-driven fixes
        /// </summary>
        private SyntaxTree ApplyDiagnosticFixes(CSharpCompilation compilation, SyntaxTree syntaxTree, string correlationId)
        {
            SyntaxTree currentTree = syntaxTree;
            bool hasError = true;
            int maxAttempts = 3;

            for (int attempt = 0; attempt < maxAttempts && hasError; attempt++)
            {
                compilation = compilation.ReplaceSyntaxTree(
                    compilation.SyntaxTrees.First(),
                    currentTree);

                Diagnostic[] diagnostics = compilation.GetDiagnostics()
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToArray();

                hasError = diagnostics.Any();
                if (!hasError) break;
            }

            return currentTree;
        }

        /// <summary>
        /// Initialize compilation
        /// </summary>
        private CSharpCompilation CreateCompilation(string assemblyName, SyntaxTree syntaxTree, IEnumerable<MetadataReference> references)
        {
            CSharpCompilationOptions compilationOptions = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                allowUnsafe: false
            );

            return CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                references,
                compilationOptions
            );
        }

        private CompilationResult ExecuteCompilation(CompilationContext context, string correlationId)
        {
            // Create basic compilation
            CSharpCompilation compilation = CreateCompilation(
                $"DynamicAssembly_{correlationId}",
                context.SyntaxTree,
                context.References
            );

            // Diagnostic-driven fixes in Unity AI Assistant style
            SyntaxTree fixedTree = ApplyDiagnosticFixes(compilation, context.SyntaxTree, correlationId);

            // Update context with corrected Tree
            context.SyntaxTree = fixedTree;
            context.WrappedCode = fixedTree.ToString();

            // Final compilation
            compilation = compilation.ReplaceSyntaxTree(
                compilation.SyntaxTrees.First(),
                fixedTree
            );

            // Output assembly and process results
            using MemoryStream memoryStream = new MemoryStream();
            Microsoft.CodeAnalysis.Emit.EmitResult emitResult = compilation.Emit(memoryStream);

            // Security check (only in Restricted mode)
            CompilationResult result = ProcessEmitResult(emitResult, memoryStream, context, correlationId);

            // Perform security verification only on successful compilation
            if (result.Success && _currentSecurityLevel == DynamicCodeSecurityLevel.Restricted)
            {
                SecurityValidationResult validationResult = _securityValidator.ValidateCompilation(compilation);
                if (!validationResult.IsValid)
                {
                    result.HasSecurityViolations = true;
                    result.SecurityViolations = validationResult.Violations;

                    VibeLogger.LogWarning(
                        "security_violations_detected",
                        "Security violations detected in compiled code",
                        new
                        {
                            violationCount = validationResult.Violations.Count,
                            violations = validationResult.Violations.Select(v => new
                            {
                                type = v.Type.ToString(),
                                api = v.ApiName,
                                description = v.Description
                            })
                        },
                        correlationId,
                        "Detected dangerous API calls",
                        "Will be blocked at runtime"
                    );
                }
            }

            return result;
        }

        private CompilationResult ProcessEmitResult(
            Microsoft.CodeAnalysis.Emit.EmitResult emitResult,
            MemoryStream memoryStream,
            CompilationContext context,
            string correlationId)
        {
            CompilationResult result = new CompilationResult
            {
                UpdatedCode = context.WrappedCode,
                Warnings = CollectWarnings(emitResult.Diagnostics)
            };

            if (emitResult.Success)
            {
                memoryStream.Seek(0, SeekOrigin.Begin);
                Assembly assembly = Assembly.Load(memoryStream.ToArray());

                result.Success = true;
                result.CompiledAssembly = assembly;

                
            }
            else
            {
                result.Success = false;
                result.Errors = ConvertDiagnosticsToErrors(emitResult.Diagnostics);
                LogCompilationFailure(result, correlationId);
            }

            return result;
        }

        private List<string> CollectWarnings(IEnumerable<Diagnostic> diagnostics)
        {
            return diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Warning)
                .Select(d => d.ToString())
                .ToList();
        }

        private void LogCompilationSuccess(Assembly assembly, string correlationId) { }

        private void LogCompilationFailure(CompilationResult result, string correlationId)
        {
            VibeLogger.LogWarning(
                "roslyn_compile_failure",
                "Compilation failed with errors",
                new
                {
                    errorCount = result.Errors.Count,
                    errors = result.Errors.Select(e => e.Message).ToArray()
                },
                correlationId,
                "Dynamic code compilation failed",
                "Analyze common compilation errors for auto-fix patterns"
            );
        }

        private void CacheResultIfSuccessful(CompilationRequest request, CompilationResult result)
        {
            _cacheManager.CacheResultIfSuccessful(result, request);
        }

        private CompilationResult HandleCompilationException(Exception ex, CompilationRequest request, string correlationId)
        {
            VibeLogger.LogError(
                "roslyn_compile_exception",
                "Exception during Roslyn compilation",
                new
                {
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                },
                correlationId,
                "Unexpected compilation error occurred",
                "Investigate and handle compilation exceptions"
            );

            return new CompilationResult
            {
                Success = false,
                UpdatedCode = request.Code,
                Errors = new List<CompilationError>
                {
                    new CompilationError()
                    {
                        Message = $"Compilation exception: {ex.Message}",
                        ErrorCode = "INTERNAL_ERROR",
                        Line = 0,
                        Column = 0
                    }
                }
            };
        }

        private string WrapCodeIfNeeded(string code, string namespaceName, string className)
        {
            // Use Roslyn to detect actual structure instead of naive string checks
            CompilationUnitSyntax root = null;
            try
            {
                SyntaxTree rawTree = CSharpSyntaxTree.ParseText(code);
                root = rawTree.GetCompilationUnitRoot();
            }
            catch
            {
                // Fall back to legacy behavior if parsing fails
                root = null;
            }

            if (root != null)
            {
                bool hasNamespace = root.Members.Any(m => m is NamespaceDeclarationSyntax || m is FileScopedNamespaceDeclarationSyntax);
                bool hasType = root.Members.Any(m => m is BaseTypeDeclarationSyntax);
                bool hasTopLevel = root.Members.Any(m => m is GlobalStatementSyntax);

                // If user already provided a proper namespace/type and no top-level statements, return as-is
                if ((hasNamespace || hasType) && !hasTopLevel)
                {
                    return code;
                }

                // Extract using directives via AST
                List<string> usingStatements = root.Usings
                    .Select(u => u.ToString().TrimEnd())
                    .ToList();

                // Build method body from top-level statements when available
                string body;
                if (hasTopLevel)
                {
                    IEnumerable<string> stmtTexts = root.Members
                        .OfType<GlobalStatementSyntax>()
                        .Select(gs => gs.Statement.ToString().TrimEnd());
                    body = string.Join("\n", stmtTexts);
                }
                else
                {
                    // Fallback: remove using lines from original and use the rest as body
                    string[] lines = code.Split(new char[] { '\n' }, StringSplitOptions.None);
                    List<string> filtered = new();
                    foreach (string line in lines)
                    {
                        string trimmed = line.TrimStart();
                        if (!(trimmed.StartsWith("using ") && trimmed.Contains(";")))
                        {
                            filtered.Add(line);
                        }
                    }
                    body = string.Join("\n", filtered);
                }

                // Compose wrapped code
                StringBuilder wrappedCode = new();

                foreach (string usingStatement in usingStatements)
                {
                    wrappedCode.AppendLine(usingStatement);
                }
                if (usingStatements.Count > 0)
                {
                    wrappedCode.AppendLine();
                }

                wrappedCode.AppendLine($"namespace {namespaceName}");
                wrappedCode.AppendLine("{");
                wrappedCode.AppendLine($"    public class {className}");
                wrappedCode.AppendLine("    {");
                wrappedCode.AppendLine("        public object Execute(System.Collections.Generic.Dictionary<string, object> parameters = null)");
                wrappedCode.AppendLine("        {");

                foreach (string line in body.Split(new char[] { '\n' }, StringSplitOptions.None))
                {
                    wrappedCode.AppendLine($"            {line}");
                }

                wrappedCode.AppendLine("        }");
                wrappedCode.AppendLine("    }");
                wrappedCode.AppendLine("}");

                string wrappedCodeString = wrappedCode.ToString();
                

                return wrappedCodeString;
            }

            // No legacy string-based fallback: if AST parsing fails, return original code
            // and let compilation diagnostics surface the issue clearly.
            return code;
        }

        private List<CompilationError> ConvertDiagnosticsToErrors(IEnumerable<Diagnostic> diagnostics)
        {
            return diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => new CompilationError
                {
                    Message = d.GetMessage(),
                    ErrorCode = d.Id,
                    Line = d.Location.GetLineSpan().StartLinePosition.Line + 1,
                    Column = d.Location.GetLineSpan().StartLinePosition.Character + 1
                })
                .ToList();
        }
    }

    /// <summary>
    /// Context information for compilation processing
    /// </summary>
    internal class CompilationContext
    {
        public string WrappedCode { get; set; }
        public SyntaxTree SyntaxTree { get; set; }
        public List<MetadataReference> References { get; set; }
    }
}
#endif