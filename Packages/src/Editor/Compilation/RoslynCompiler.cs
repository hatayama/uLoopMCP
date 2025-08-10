using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Roslynを使用したC#動的コンパイル機能
    /// 関連クラス: CompilationRequest, CompilationResult
    /// </summary>
    public class RoslynCompiler
    {
        private readonly List<MetadataReference> _defaultReferences = new();
        private readonly Dictionary<string, Assembly> _compilationCache = new();

        // キュレートされたアセンブリプレフィックス（Assembly-CSharp除外でセキュリティ強化）
        private static readonly string[] CuratedAssemblyPrefixes = { "UnityEngine", "UnityEditor", "Unity.", "netstandard" };

        // Unity AI Assistant方式のFixProviderリスト
        private static readonly List<CSharpFixProvider> FixProviders = new()
        {
            new FixMissingUsings()
        };

        public RoslynCompiler()
        {
            InitializeReferences();
        }

        public void InitializeReferences()
        {
            _defaultReferences.Clear();

            // .NET Standard/Core基本アセンブリ
            Assembly netStandardAssembly = Assembly.Load("netstandard");
            if (netStandardAssembly != null)
            {
                _defaultReferences.Add(MetadataReference.CreateFromFile(netStandardAssembly.Location));
            }

            // 基本的な.NETアセンブリを追加
            _defaultReferences.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            _defaultReferences.Add(MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location));
            _defaultReferences.Add(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location));
            _defaultReferences.Add(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location));

            // System.Runtimeを明示的に追加
            string[] runtimePaths = new[]
            {
                Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll"),
                Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Private.CoreLib.dll")
            };

            foreach (string path in runtimePaths)
            {
                if (File.Exists(path))
                {
                    _defaultReferences.Add(MetadataReference.CreateFromFile(path));
                }
            }

            // 現在のアセンブリも追加（uLoopMCP関連クラスにアクセスするため）
            _defaultReferences.Add(MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location));

            VibeLogger.LogInfo(
                "roslyn_compiler_initialize",
                "RoslynCompiler initialized with references",
                new { referenceCount = _defaultReferences.Count },
                correlationId: Guid.NewGuid().ToString("N")[..8],
                humanNote: "Roslyn compiler ready for dynamic code compilation",
                aiTodo: "Monitor compilation performance and reference resolution"
            );
        }

        public CompilationResult Compile(CompilationRequest request)
        {
            string correlationId = GenerateCorrelationId();

            try
            {
                LogCompilationStart(request, correlationId);

                // 早期return でネスト浅く
                CompilationResult cachedResult = CheckCache(request);
                if (cachedResult != null) return cachedResult;

                CompilationContext context = PrepareCompilation(request);
                CompilationResult result = ExecuteCompilation(context, correlationId);

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
            _compilationCache.Clear();
            VibeLogger.LogInfo(
                "roslyn_cache_cleared",
                "Compilation cache cleared",
                new { },
                correlationId: Guid.NewGuid().ToString("N")[..8],
                humanNote: "Compilation cache was cleared",
                aiTodo: "Monitor cache usage patterns"
            );
        }

        private string GenerateCorrelationId()
        {
            return Guid.NewGuid().ToString("N")[..8];
        }

        private void LogCompilationStart(CompilationRequest request, string correlationId)
        {
            VibeLogger.LogInfo(
                "roslyn_compile_start",
                "Starting Roslyn compilation",
                new
                {
                    codeLength = request.Code?.Length ?? 0,
                    className = request.ClassName,
                    @namespace = request.Namespace,
                    additionalRefsCount = request.AdditionalReferences?.Count ?? 0
                },
                correlationId,
                "Dynamic code compilation started",
                "Monitor compilation success rate and performance"
            );
        }

        private CompilationResult CheckCache(CompilationRequest request)
        {
            string cacheKey = GenerateCacheKey(request);
            if (_compilationCache.TryGetValue(cacheKey, out Assembly cachedAssembly))
            {
                return new CompilationResult
                {
                    Success = true,
                    CompiledAssembly = cachedAssembly,
                    UpdatedCode = request.Code
                };
            }

            return null;
        }

        private CompilationContext PrepareCompilation(CompilationRequest request)
        {
            string wrappedCode = WrapCodeIfNeeded(request.Code, request.Namespace, request.ClassName);
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(wrappedCode);

            // アセンブリ読み込みモードに応じて参照を準備
            // 注意: AllAssembliesモードでも、コンストラクタで既に全アセンブリが_defaultReferencesに追加済み
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

        private CompilationResult ExecuteCompilation(CompilationContext context, string correlationId)
        {
            CSharpCompilationOptions compilationOptions = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                allowUnsafe: false
            );

            CSharpCompilation compilation = CSharpCompilation.Create(
                $"DynamicAssembly_{correlationId}",
                new[] { context.SyntaxTree },
                context.References,
                compilationOptions
            );

            // Unity AI Assistant方式の診断駆動修正
            SyntaxTree currentTree = context.SyntaxTree;
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

                // Phase 1: 動的アセンブリ追加を試行
                bool addedAssembly = TryAddMissingAssemblies(diagnostics, correlationId);
                if (addedAssembly)
                {
                    // アセンブリが追加された場合はコンパイレーションを再構築
                    compilation = CSharpCompilation.Create(
                        $"DynamicAssembly_{correlationId}",
                        new[] { currentTree },
                        context.References,
                        compilationOptions
                    );
                    continue; // 次のループでエラーチェック
                }

                // Phase 2: 修正適用
                foreach (Diagnostic diagnostic in diagnostics)
                {
                    foreach (CSharpFixProvider provider in FixProviders)
                    {
                        if (provider.CanFix(diagnostic))
                        {
                            currentTree = provider.ApplyFix(currentTree, diagnostic);
                            VibeLogger.LogInfo(
                                "code_fix_applied",
                                $"Applied fix: {provider.GetType().Name}",
                                new { diagnosticId = diagnostic.Id },
                                correlationId,
                                "Code fix applied during compilation",
                                "Track fix effectiveness");
                            break;
                        }
                    }
                }
            }

            // 修正されたTreeでコンテキストを更新
            context.SyntaxTree = currentTree;
            context.WrappedCode = currentTree.ToString();
            compilation = compilation.ReplaceSyntaxTree(
                compilation.SyntaxTrees.First(),
                currentTree);

            using MemoryStream memoryStream = new MemoryStream();
            Microsoft.CodeAnalysis.Emit.EmitResult emitResult = compilation.Emit(memoryStream);

            return ProcessEmitResult(emitResult, memoryStream, context, correlationId);
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

                LogCompilationSuccess(assembly, correlationId);
            }
            else
            {
                result.Success = false;
                result.Errors = ConvertDiagnosticsToErrors(emitResult.Diagnostics);

                // セキュリティ違反を検出
                DetectSecurityViolations(result, emitResult.Diagnostics);

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

        private void LogCompilationSuccess(Assembly assembly, string correlationId)
        {
            VibeLogger.LogInfo(
                "roslyn_compile_success",
                "Compilation completed successfully",
                new
                {
                    assemblyName = assembly.FullName,
                    typeCount = assembly.GetTypes().Length
                },
                correlationId,
                "Dynamic code compilation succeeded",
                "Track compilation success patterns"
            );
        }

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
            if (result.Success)
            {
                string cacheKey = GenerateCacheKey(request);
                _compilationCache[cacheKey] = result.CompiledAssembly;
            }
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
            // 既に名前空間やクラスが含まれているかチェック
            if (code.Contains("namespace ") || code.Contains("class "))
            {
                return code; // そのまま返す
            }

            // シンプルなメソッドや式の場合は、クラスでラップ
            StringBuilder wrappedCode = new();
            // Unity AI Assistant準拠: using文はSyntaxTreeベースで後から追加
            // WrapCodeIfNeededで生成されるコードは完全修飾名を使用し、using文に依存しない
            wrappedCode.AppendLine($"namespace {namespaceName}");
            wrappedCode.AppendLine("{");
            wrappedCode.AppendLine($"    public class {className}");
            wrappedCode.AppendLine("    {");
            wrappedCode.AppendLine("        public object Execute(System.Collections.Generic.Dictionary<string, object> parameters = null)");
            wrappedCode.AppendLine("        {");

            // コードを適切にインデント
            string[] lines = code.Split(new char[] { '\n' }, StringSplitOptions.None);
            foreach (string line in lines)
            {
                wrappedCode.AppendLine($"            {line}");
            }

            wrappedCode.AppendLine("        }");
            wrappedCode.AppendLine("    }");
            wrappedCode.AppendLine("}");

            return wrappedCode.ToString();
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


        /// <summary>
        /// Unity AI Assistant準拠のキュレートされたアセンブリのみを追加
        /// </summary>
        private int AddCuratedAssemblies()
        {
            try
            {
                // Unity AI Assistant準拠のキュレートされたプレフィックス (Assembly-CSharpを除外してセキュリティ強化)

                int addedCount = 0;

                // キュレートされたアセンブリのみ追加（重複を避けるため明示的追加は削除）
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.IsDynamic || string.IsNullOrWhiteSpace(assembly.Location))
                        continue;

                    // キュレートされたプレフィックスかチェック
                    bool isCurated = false;
                    foreach (string prefix in CuratedAssemblyPrefixes)
                    {
                        if (assembly.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            isCurated = true;
                            break;
                        }
                    }

                    if (isCurated && TryAddAssemblyReference(assembly))
                    {
                        addedCount++;
                    }
                }

                return addedCount;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"RoslynCompiler: キュレートされたアセンブリ追加でエラー - {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 不足しているアセンブリを動的に追加を試行
        /// 
        /// TODO: 現在は不完全な実装
        /// - AppDomain.CurrentDomain.GetAssemblies()で既に読み込まれたアセンブリのみ検索
        /// - 独自DLL、NuGetパッケージ、外部ライブラリは発見できない  
        /// - ファイルシステムスキャン + Assembly.LoadFromによる動的読み込みが必要
        /// - 現状ではAdditionalReferencesによる手動指定を推奨
        /// - 実際のプロジェクトではほとんど使われていない
        /// </summary>
        private bool TryAddMissingAssemblies(IEnumerable<Diagnostic> diagnostics, string correlationId)
        {
            bool addedNewAssembly = false;

            foreach (Diagnostic diagnostic in diagnostics.Where(d => d.Id == "CS0246"))
            {
                string typeName = ExtractTypeNameFromDiagnostic(diagnostic);
                if (!string.IsNullOrEmpty(typeName))
                {
                    Assembly foundAssembly = FindAssemblyContainingType(typeName);
                    if (foundAssembly != null && TryAddAssemblyReference(foundAssembly))
                    {
                        addedNewAssembly = true;
                        VibeLogger.LogInfo(
                            "dynamic_assembly_added",
                            $"Dynamically added assembly for type: {typeName}",
                            new
                            {
                                typeName,
                                assemblyName = foundAssembly.FullName,
                                diagnosticId = diagnostic.Id
                            },
                            correlationId,
                            "Assembly dynamically added to resolve compilation error",
                            "Track dynamic assembly addition patterns"
                        );
                    }
                }
            }

            return addedNewAssembly;
        }

        private string ExtractTypeNameFromDiagnostic(Diagnostic diagnostic)
        {
            string message = diagnostic.GetMessage();
            VibeLogger.LogInfo(
                "diagnostic_message_analysis",
                $"Analyzing diagnostic message for type extraction",
                new
                {
                    diagnosticId = diagnostic.Id,
                    message = message,
                    severity = diagnostic.Severity.ToString()
                },
                null,
                "Diagnostic message being processed for type name extraction",
                "Monitor type extraction patterns for improvement"
            );

            // CS0246: The type or namespace name 'TypeName' could not be found
            if (diagnostic.Id == "CS0246")
            {
                int startIndex = message.IndexOf('\'') + 1;
                int endIndex = message.IndexOf('\'', startIndex);

                if (startIndex > 0 && endIndex > startIndex)
                {
                    return message.Substring(startIndex, endIndex - startIndex);
                }
            }

            // CS0103: The name 'TypeName' does not exist in the current context
            if (diagnostic.Id == "CS0103")
            {
                int startIndex = message.IndexOf('\'') + 1;
                int endIndex = message.IndexOf('\'', startIndex);

                if (startIndex > 0 && endIndex > startIndex)
                {
                    string typeName = message.Substring(startIndex, endIndex - startIndex);
                    // HttpClient, File など具体的な型名の場合
                    if (!typeName.Contains('.') && char.IsUpper(typeName[0]))
                    {
                        return typeName;
                    }
                }
            }

            // その他のパターンの場合もログ出力
            VibeLogger.LogWarning(
                "type_extraction_failed",
                "Could not extract type name from diagnostic",
                new
                {
                    diagnosticId = diagnostic.Id,
                    message = message
                },
                null,
                "Type name extraction failed for diagnostic message",
                "Review extraction patterns for completeness"
            );

            return null;
        }

        private Assembly FindAssemblyContainingType(string typeName)
        {
            // よく使われる型の名前空間マッピング
            Dictionary<string, string[]> commonTypeMapping = new Dictionary<string, string[]>
            {
                { "HttpClient", new[] { "System.Net.Http.HttpClient" } },
                { "File", new[] { "System.IO.File" } },
                { "Directory", new[] { "System.IO.Directory" } },
                { "Path", new[] { "System.IO.Path" } },
                { "WebClient", new[] { "System.Net.WebClient" } },
                { "List", new[] { "System.Collections.Generic.List`1" } },
                { "Dictionary", new[] { "System.Collections.Generic.Dictionary`2" } }
            };

            // 型名候補を準備
            List<string> candidateTypes = new List<string> { typeName };
            if (commonTypeMapping.ContainsKey(typeName))
            {
                candidateTypes.AddRange(commonTypeMapping[typeName]);
            }

            // AppDomain内の全アセンブリから型を検索
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic || string.IsNullOrWhiteSpace(assembly.Location))
                    continue;

                foreach (string candidate in candidateTypes)
                {
                    Type foundType = assembly.GetType(candidate) ??
                                     assembly.GetTypes().FirstOrDefault(t =>
                                         t.Name == candidate ||
                                         t.FullName == candidate ||
                                         t.Name == typeName ||
                                         t.FullName == typeName);

                    if (foundType != null)
                    {
                        VibeLogger.LogInfo(
                            "assembly_found_for_type",
                            $"Found assembly containing type: {typeName}",
                            new
                            {
                                typeName,
                                foundTypeName = foundType.FullName,
                                assemblyName = assembly.FullName,
                                assemblyLocation = assembly.Location
                            },
                            null,
                            "Assembly found for dynamic type resolution",
                            "Track successful type-to-assembly mappings"
                        );
                        return assembly;
                    }
                }
            }

            VibeLogger.LogWarning(
                "type_not_found_in_assemblies",
                $"Type {typeName} not found in any loaded assembly",
                new
                {
                    typeName,
                    searchedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                        .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
                        .Select(a => a.FullName)
                        .ToArray()
                },
                null,
                "Type could not be resolved to any assembly",
                "Review type resolution patterns"
            );

            return null;
        }

        /// <summary>
        /// 診断メッセージからセキュリティ違反を検出してCompilationResultに設定
        /// </summary>
        private void DetectSecurityViolations(CompilationResult result, IEnumerable<Diagnostic> diagnostics)
        {
            SecurityPolicy securityPolicy = SecurityPolicy.GetDefault();
            List<SecurityViolation> violations = new();

            foreach (Diagnostic diagnostic in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
            {
                string message = diagnostic.GetMessage();

                // 禁止された名前空間の使用を検出
                foreach (string forbiddenNamespace in securityPolicy.ForbiddenNamespaces)
                {
                    bool isViolation = false;
                    string detectionReason = "";

                    // パターン1: 直接的な名前空間エラー（"System.IO is a namespace but is used like a type"）
                    if (message.Contains($"'{forbiddenNamespace}'") &&
                        message.Contains("is a namespace but is used like a type"))
                    {
                        isViolation = true;
                        detectionReason = "Direct namespace usage error";
                    }

                    // パターン2: 子名前空間の使用エラー（"System.Net.Http is a namespace but is used like a type"）
                    else if (message.Contains("is a namespace but is used like a type"))
                    {
                        // System.Net.Http の場合、System.Net で検出
                        if (message.Contains($"'{forbiddenNamespace}."))
                        {
                            isViolation = true;
                            detectionReason = "Child namespace usage error";
                        }
                    }

                    // パターン3: 禁止名前空間に属する型の使用エラー（間接的検出）
                    // "HttpClient could not be found" で System.Net.Http.HttpClient を検出
                    else if (diagnostic.Id == "CS0246")
                    {
                        string context = diagnostic.Location.SourceTree?.ToString() ?? "";
                        if (context.Contains($"using {forbiddenNamespace}") ||
                            context.Contains($"using {forbiddenNamespace}."))
                        {
                            isViolation = true;
                            detectionReason = "Forbidden namespace type usage";
                        }
                    }

                    if (isViolation)
                    {
                        SecurityViolation violation = new SecurityViolation
                        {
                            Type = SecurityViolationType.ForbiddenNamespace,
                            Description = $"Forbidden namespace '{forbiddenNamespace}' was used but blocked by security policy ({detectionReason})",
                            LineNumber = diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1,
                            CodeSnippet = message
                        };
                        violations.Add(violation);

                        VibeLogger.LogWarning(
                            "security_violation_detected",
                            $"Security violation: forbidden namespace '{forbiddenNamespace}' detected in compilation",
                            new
                            {
                                forbiddenNamespace,
                                diagnosticId = diagnostic.Id,
                                diagnosticMessage = message,
                                lineNumber = violation.LineNumber,
                                detectionReason
                            },
                            null,
                            "Security policy violation detected during compilation",
                            "Track security violations for policy effectiveness analysis"
                        );
                        break; // 最初にマッチした禁止名前空間で終了
                    }
                }

                // 禁止されたメソッドの検出
                foreach (string forbiddenMethod in securityPolicy.ForbiddenMethods)
                {
                    if (message.Contains(forbiddenMethod))
                    {
                        SecurityViolation violation = new SecurityViolation
                        {
                            Type = SecurityViolationType.DangerousMethodCall,
                            Description = $"Forbidden method '{forbiddenMethod}' was detected",
                            LineNumber = diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1,
                            CodeSnippet = message
                        };
                        violations.Add(violation);
                    }
                }
            }

            if (violations.Count > 0)
            {
                result.HasSecurityViolations = true;
                result.SecurityViolations = violations;
                result.FailureReason = CompilationFailureReason.SecurityViolation;
            }
            else
            {
                result.FailureReason = CompilationFailureReason.CompilationError;
            }
        }


        private bool TryAddAssemblyReference(Assembly assembly)
        {
            _defaultReferences.Add(MetadataReference.CreateFromFile(assembly.Location));
            return true;
        }

        private string GenerateCacheKey(CompilationRequest request)
        {
            StringBuilder keyBuilder = new StringBuilder();
            keyBuilder.Append(request.Code);
            keyBuilder.Append(request.ClassName);
            keyBuilder.Append(request.Namespace);

            if (request.AdditionalReferences != null)
            {
                foreach (string reference in request.AdditionalReferences.OrderBy(r => r))
                {
                    keyBuilder.Append(reference);
                }
            }

            return Convert.ToBase64String(Encoding.UTF8.GetBytes(keyBuilder.ToString()));
        }
    }

    /// <summary>
    /// コンパイル処理のコンテキスト情報
    /// </summary>
    internal class CompilationContext
    {
        public string WrappedCode { get; set; }
        public SyntaxTree SyntaxTree { get; set; }
        public List<MetadataReference> References { get; set; }
    }
}