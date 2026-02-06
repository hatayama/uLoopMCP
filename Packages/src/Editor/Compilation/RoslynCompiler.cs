#if ULOOPMCP_HAS_ROSLYN
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
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

            // Comprehensively collect Unity/.NET reference assemblies (independent of AppDomain)
            HashSet<string> addedPaths = new();

            // Track assembly identities to avoid CS1703 (multiple assemblies with equivalent identity)
            HashSet<string> addedAssemblyNames = new(StringComparer.OrdinalIgnoreCase);

            // 1. Collect Unity/.NET + ScriptAssemblies from Unity folders
            AddUnityReferenceAssemblies(references, addedPaths, addedAssemblyNames);

            // 2. Always include precompiled plugin assemblies that belong to the project or packages
            AddPrecompiledPluginAssemblies(references, addedPaths, addedAssemblyNames);

            // 3. Security level specific handling
            if (level == DynamicCodeSecurityLevel.FullAccess)
            {
                // In FullAccess, also include all currently loaded AppDomain assemblies for maximum compatibility
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
            }

            // 4. Also add current assembly (for uLoopMCP class access)
            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            if (!string.IsNullOrWhiteSpace(currentAssembly.Location))
            {
                if (addedPaths.Add(currentAssembly.Location))
                {
                    MetadataReference currentRef = MetadataReference.CreateFromFile(currentAssembly.Location);
                    references.Add(currentRef);
                    try
                    {
                        if (TryGetAssemblyIdentity(currentAssembly.Location, out string n, out var _))
                        {
                            addedAssemblyNames.Add(n);
                        }
                    }
                    catch { }
                }
            }

            _defaultReferences.Clear();
            _defaultReferences.AddRange(references);

            
        }

        /// <summary>
        /// Add precompiled plugin assemblies that are part of the current project or its packages.
        /// Includes assemblies under Assets, Packages, and Library/PackageCache.
        /// Skips unrelated system locations to avoid unintended exposure in Restricted mode.
        /// </summary>
        private void AddPrecompiledPluginAssemblies(List<MetadataReference> references, HashSet<string> addedPaths, HashSet<string> addedAssemblyNames = null)
        {
            try
            {
                string dataPath = UnityEngine.Application.dataPath; // {Project}/Assets
                string projectRoot = Path.GetFullPath(Path.Combine(dataPath, ".."));
                string assetsRoot = Path.GetFullPath(dataPath);
                string packagesRoot = Path.Combine(projectRoot, "Packages");
                string packageCacheRoot = Path.Combine(projectRoot, "Library", "PackageCache");

                string Normalize(string p) => Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                assetsRoot = Normalize(assetsRoot);
                packagesRoot = Normalize(packagesRoot);
                packageCacheRoot = Normalize(packageCacheRoot);

                IEnumerable<string> roots = new[] { assetsRoot, packagesRoot, packageCacheRoot }
                    .Where(root => !string.IsNullOrEmpty(root) && Directory.Exists(root));

                foreach (string root in roots)
                {
                    foreach (string dllPath in Directory.GetFiles(root, "*.dll", SearchOption.AllDirectories))
                    {
                        string fullPath = Normalize(dllPath);

                        if (!IsManagedAssembly(fullPath))
                        {
                            continue;
                        }

                        // Early skip for analyzer assemblies based on centralized rules
                        if (ReferenceExclusionRules.ShouldSkip(fullPath, null))
                        {
                            continue;
                        }

                        if (addedPaths.Add(fullPath))
                        {
                            // Avoid identity duplicates across sources (Managed vs Packages, etc.)
                            string asmName = null;
                            if (addedAssemblyNames != null && TryGetAssemblyIdentity(fullPath, out asmName, out Version _))
                            {
                                // Skip analyzer assemblies based on assembly identity
                                if (ReferenceExclusionRules.ShouldSkip(fullPath, asmName))
                                {
                                    continue;
                                }

                                if (!addedAssemblyNames.Add(asmName))
                                {
                                    continue;
                                }
                            }

                            MetadataReference reference = TryCreateReference(fullPath);
                            if (reference != null)
                            {
                                references.Add(reference);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                VibeLogger.LogWarning(
                    "roslyn_precompiled_refs_failed",
                    $"Failed to add precompiled plugin assemblies: {ex.Message}",
                    new { error = ex.Message },
                    McpConstants.GenerateCorrelationId(),
                    "Continuing without precompiled plugin references",
                    "Check Unity CompilationPipeline availability"
                );
            }
        }

        /// <summary>
        /// Comprehensively collect Unity reference assemblies
        /// Collect directly from Unity installation folders, independent of AppDomain
        /// </summary>
        private void AddUnityReferenceAssemblies(List<MetadataReference> references, HashSet<string> addedPaths, HashSet<string> addedAssemblyNames = null)
        {
            string correlationId = McpConstants.GenerateCorrelationId();

            try
            {
                string contentsPath = GetUnityContentsPath();

                List<string> searchPaths = BuildSearchPaths(contentsPath);

                int scannedCount = 0;
                int skippedNonManaged = 0;
                int identityFailures = 0;
                int duplicatesReplaced = 0;

                Dictionary<string, (Version Version, string Path)> bestByName = new Dictionary<string, (Version Version, string Path)>(StringComparer.OrdinalIgnoreCase);

                foreach (string dllPath in EnumerateDllPaths(searchPaths))
                {
                    scannedCount++;

                    if (!IsManagedAssembly(dllPath))
                    {
                        skippedNonManaged++;
                        continue;
                    }

                    if (!TryGetAssemblyIdentity(dllPath, out string assemblyName, out Version version))
                    {
                        identityFailures++;
                        continue;
                    }

                    // Skip analyzer assemblies (avoid type conflicts such as Vector3)
                    if (ReferenceExclusionRules.ShouldSkip(dllPath, assemblyName))
                    {
                        continue;
                    }

                    if (bestByName.TryGetValue(assemblyName, out var existing))
                    {
                        if (version != null && existing.Version != null && version.CompareTo(existing.Version) > 0)
                        {
                            bestByName[assemblyName] = (version, dllPath);
                            duplicatesReplaced++;
                        }
                        continue;
                    }

                    bestByName[assemblyName] = (version, dllPath);
                }

                foreach (string dllPath in bestByName.Values.Select(v => v.Path).OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase))
                {
                    if (addedPaths.Add(dllPath))
                    {
                        // Avoid identity duplicates (CS1703)
                        if (addedAssemblyNames != null && TryGetAssemblyIdentity(dllPath, out string asmName, out Version _))
                        {
                            if (ReferenceExclusionRules.ShouldSkip(dllPath, asmName))
                            {
                                continue;
                            }
                            if (!addedAssemblyNames.Add(asmName))
                            {
                                continue;
                            }
                        }

                        // Prefer newer CoreModule assemblies over legacy wrapper assemblies
                        string fileName = Path.GetFileNameWithoutExtension(dllPath);
                        if (string.Equals(fileName, "UnityEngine", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(fileName, "UnityEditor", StringComparison.OrdinalIgnoreCase))
                        {
                            // Skip legacy wrapper in favor of CoreModule variants
                            continue;
                        }

                        MetadataReference reference = TryCreateReference(dllPath);
                        if (reference != null)
                        {
                            references.Add(reference);
                        }
                    }
                }

                if (skippedNonManaged > 0 || identityFailures > 0 || duplicatesReplaced > 0)
                {
                    VibeLogger.LogWarning(
                        "roslyn_unity_references_summary",
                        "Unity reference scan summary",
                        new
                        {
                            scanned = scannedCount,
                            selected = bestByName.Count,
                            skippedNonManaged,
                            identityFailures,
                            duplicatesReplaced
                        },
                        correlationId,
                        "Aggregated reference scan summary",
                        "Individual failures suppressed to reduce noise"
                    );
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

        /// <summary>
        /// Get the base path for scripting-related assemblies.
        /// Unity 6.3+ moved assemblies to Contents/Resources/Scripting/, while earlier versions use Contents/ directly.
        /// See: https://github.com/hatayama/uLoopMCP/issues/370
        /// </summary>
        private string GetScriptingBasePath(string contentsPath)
        {
            string scriptingPath = Path.Combine(contentsPath, "Resources", "Scripting");
            if (Directory.Exists(scriptingPath))
            {
                return scriptingPath;
            }
            return contentsPath;
        }

        private List<string> BuildSearchPaths(string contentsPath)
        {
            List<string> searchPaths = new();

            // Unity 6.3+ uses Contents/Resources/Scripting/, earlier versions use Contents/ directly
            string scriptingBase = GetScriptingBasePath(contentsPath);

            // .NET Framework 4.x reference assemblies (use only when NetStandard 2.1 is unavailable)
            string monoApi = Path.Combine(scriptingBase, "MonoBleedingEdge", "lib", "mono", "4.7.1-api");
            string monoFacades = Path.Combine(monoApi, "Facades");
            string monoUnityjit = Path.Combine(scriptingBase, "MonoBleedingEdge", "lib", "mono", "unityjit-macos");

            // .NET Standard reference assemblies
            string netStandard21 = Path.Combine(scriptingBase, "NetStandard", "ref", "2.1.0");
            string netStandard20 = Path.Combine(scriptingBase, "NetStandard", "ref", "2.0.0");
            string netStandardCompat = Path.Combine(scriptingBase, "NetStandard", "compat", "shims", "net472");

            // Unity Managed assemblies
            string managed = Path.Combine(scriptingBase, "Managed");
            string unityEngine = Path.Combine(managed, "UnityEngine");
            string unityEditor = Path.Combine(managed, "UnityEditor");

            // Project assemblies
            string scriptAssemblies = Path.Combine(UnityEngine.Application.dataPath, "..", "Library", "ScriptAssemblies");

            // Use .NET Standard 2.0 + compat shims and also include mono 4.7.1 api/facades
            // This combination allows referencing both netstandard libraries and 4.x-targeted assemblies
            AddDirectoryIfExists(searchPaths, netStandard20);
            AddDirectoryIfExists(searchPaths, netStandardCompat);
            AddDirectoryIfExists(searchPaths, monoApi);
            AddDirectoryIfExists(searchPaths, monoFacades);
            AddDirectoryIfExists(searchPaths, monoUnityjit);
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
                    foreach (string dllPath in Directory.GetFiles(searchPath, "*.dll").OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase))
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

        private bool IsManagedAssembly(string dllPath)
        {
            try
            {
                using (FileStream stream = File.OpenRead(dllPath))
                using (PEReader peReader = new PEReader(stream))
                {
                    return peReader.HasMetadata;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool TryGetAssemblyIdentity(string dllPath, out string assemblyName, out Version version)
        {
            assemblyName = null;
            version = null;
            try
            {
                AssemblyName an = AssemblyName.GetAssemblyName(dllPath);
                assemblyName = an?.Name;
                version = an?.Version ?? new Version(0, 0, 0, 0);
                return !string.IsNullOrEmpty(assemblyName);
            }
            catch
            {
                return false;
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
                // Explicitly block compilation at Level 0 (Disabled)
                if (_currentSecurityLevel == DynamicCodeSecurityLevel.Disabled)
                {
                    VibeLogger.LogWarning(
                        "roslyn_compilation_blocked_level0",
                        "Compilation attempt blocked at security level 0",
                        new { level = _currentSecurityLevel.ToString() },
                        correlationId,
                        "Compilation disabled by security policy",
                        "Increase isolation level to 1 or higher to compile"
                    );

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
        /// Apply diagnostic-driven fixes (auto-resolve missing using directives for CS0103/CS0246 errors)
        /// </summary>
        private SyntaxTree ApplyDiagnosticFixes(CSharpCompilation compilation, CompilationContext context, string correlationId)
        {
            SyntaxTree currentTree = context.SyntaxTree;
            UsingDirectiveResolver resolver = new UsingDirectiveResolver();
            int maxAttempts = 3;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                compilation = compilation.ReplaceSyntaxTree(
                    compilation.SyntaxTrees.First(),
                    currentTree);

                Diagnostic[] diagnostics = compilation.GetDiagnostics()
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToArray();

                if (!diagnostics.Any()) break;

                Diagnostic[] unresolvedTypeDiagnostics = diagnostics
                    .Where(d => d.Id == "CS0103" || d.Id == "CS0246")
                    .ToArray();

                if (!unresolvedTypeDiagnostics.Any()) break;

                List<UsingResolutionResult> resolutions = resolver.ResolveUnresolvedTypes(
                    compilation, unresolvedTypeDiagnostics);

                HashSet<string> namespacesToAdd = new();
                foreach (UsingResolutionResult resolution in resolutions)
                {
                    if (resolution.IsUnique)
                    {
                        namespacesToAdd.Add(resolution.CandidateNamespaces[0]);
                    }
                    else if (resolution.CandidateNamespaces.Count > 1)
                    {
                        context.AmbiguousTypeCandidates[resolution.TypeName] =
                            new List<string>(resolution.CandidateNamespaces);

                        VibeLogger.LogWarning(
                            "roslyn_auto_using_ambiguous",
                            $"Ambiguous type '{resolution.TypeName}': multiple namespaces found",
                            new
                            {
                                typeName = resolution.TypeName,
                                candidates = string.Join(", ", resolution.CandidateNamespaces)
                            },
                            correlationId,
                            $"Use fully-qualified name for '{resolution.TypeName}'",
                            $"Candidates: {string.Join(", ", resolution.CandidateNamespaces)}"
                        );
                    }
                }

                if (namespacesToAdd.Count == 0) break;

                currentTree = AddUsingDirectives(currentTree, namespacesToAdd);

                VibeLogger.LogInfo(
                    "roslyn_auto_using_added",
                    "Auto-resolved using directives",
                    new { attempt, namespaces = string.Join(", ", namespacesToAdd) },
                    correlationId,
                    $"Auto-added {namespacesToAdd.Count} using directive(s)",
                    "Monitor auto-using resolution patterns"
                );
            }

            resolver.ClearCache();
            return currentTree;
        }

        private SyntaxTree AddUsingDirectives(SyntaxTree tree, IEnumerable<string> namespaces)
        {
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            HashSet<string> existingUsings = new(
                root.Usings.Select(u => u.Name.ToString()));

            List<UsingDirectiveSyntax> newUsings = new();
            foreach (string ns in namespaces)
            {
                if (!existingUsings.Contains(ns))
                {
                    UsingDirectiveSyntax usingDirective = SyntaxFactory.UsingDirective(
                        SyntaxFactory.ParseName(ns))
                        .NormalizeWhitespace()
                        .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
                    newUsings.Add(usingDirective);
                }
            }

            if (newUsings.Count == 0) return tree;

            CompilationUnitSyntax newRoot = root.AddUsings(newUsings.ToArray());
            return newRoot.SyntaxTree;
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
            SyntaxTree fixedTree = ApplyDiagnosticFixes(compilation, context, correlationId);

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
            result.AmbiguousTypeCandidates = context.AmbiguousTypeCandidates;

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

                bool hasTopLevelReturn = root.Members
                    .OfType<GlobalStatementSyntax>()
                    .SelectMany(gs => gs.Statement.DescendantNodesAndSelf().OfType<ReturnStatementSyntax>())
                    .Any(rs => !rs.Ancestors().Any(ancestor =>
                        ancestor is LocalFunctionStatementSyntax ||
                        ancestor is AnonymousFunctionExpressionSyntax));
                if (hasTopLevel && !hasTopLevelReturn)
                {
                    body = string.IsNullOrWhiteSpace(body)
                        ? "return null;"
                        : body + "\nreturn null;";
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
                wrappedCode.AppendLine("        public async System.Threading.Tasks.Task<object> ExecuteAsync(System.Collections.Generic.Dictionary<string, object> parameters = null, System.Threading.CancellationToken ct = default)");
                wrappedCode.AppendLine("        {");

                foreach (string line in body.Split(new char[] { '\n' }, StringSplitOptions.None))
                {
                    wrappedCode.AppendLine($"            {line}");
                }

                wrappedCode.AppendLine("        }");
                wrappedCode.AppendLine();
                wrappedCode.AppendLine("        public object Execute(System.Collections.Generic.Dictionary<string, object> parameters = null)");
                wrappedCode.AppendLine("        {");
                wrappedCode.AppendLine("            return ExecuteAsync(parameters, default).GetAwaiter().GetResult();");
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
        public Dictionary<string, List<string>> AmbiguousTypeCandidates { get; set; } = new();
    }
}
#endif