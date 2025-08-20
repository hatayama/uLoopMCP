#if ULOOPMCP_HAS_ROSLYN
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
    /// v4.0 明示的セキュリティレベル注入対応
    /// 関連クラス: CompilationRequest, CompilationResult, DynamicCodeSecurityManager
    /// </summary>
    public class RoslynCompiler : IDisposable
    {
        private readonly CompilationCacheManager _cacheManager = new();
        private readonly List<MetadataReference> _defaultReferences = new();
        private readonly DynamicCodeSecurityLevel _currentSecurityLevel;
        private List<MetadataReference> _currentReferences;
        private bool _disposed;

        // FixProviderリスト（現在は空）
        // 完全修飾名の使用を推奨するため、using自動追加は無効化
        private static readonly List<CSharpFixProvider> FixProviders = new()
        {
        };

        public RoslynCompiler(DynamicCodeSecurityLevel securityLevel)
        {
            _currentSecurityLevel = securityLevel;
            InitializeReferencesForLevel(_currentSecurityLevel);
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                // キャッシュクリア
                _cacheManager.ClearReferenceCache();
                _cacheManager.ClearCache();
                _defaultReferences.Clear();
                
                _disposed = true;
            }
        }
        
        /// <summary>
        /// セキュリティレベルに応じたアセンブリ参照を初期化
        /// </summary>
        private void InitializeReferencesForLevel(DynamicCodeSecurityLevel level)
        {
            string correlationId = McpConstants.GenerateCorrelationId();
            
            // キャッシュから取得または新規作成
            List<MetadataReference> references = _cacheManager.GetOrCreateReferences(level, () =>
            {
                // 新規参照構築
                List<MetadataReference> newReferences = new();
            
            // セキュリティレベルに応じたアセンブリ取得
            IReadOnlyList<string> allowedAssemblies = DynamicCodeSecurityManager.GetAllowedAssemblies(level);
            
            foreach (string assemblyName in allowedAssemblies)
            {
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == assemblyName);
                
                if (assembly != null && !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
                {
                    MetadataReference reference = MetadataReference.CreateFromFile(assembly.Location);
                    newReferences.Add(reference);
                    _defaultReferences.Add(reference);
                }
            }
            
            // 現在のアセンブリも追加（uLoopMCPクラスアクセス用）
            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            if (!string.IsNullOrWhiteSpace(currentAssembly.Location))
            {
                MetadataReference currentRef = MetadataReference.CreateFromFile(currentAssembly.Location);
                newReferences.Add(currentRef);
                _defaultReferences.Add(currentRef);
            }
                
                return newReferences;
            });
            
            _currentReferences = references;
            _defaultReferences.Clear();
            _defaultReferences.AddRange(references);
            
            VibeLogger.LogInfo(
                "roslyn_compiler_references_initialized",
                "RoslynCompiler references initialized for security level",
                new { 
                    level = level.ToString(),
                    referenceCount = references.Count
                },
                correlationId,
                "Compiler references configured for security level",
                "Monitor assembly loading patterns"
            );
        }
        
        /// <summary>
        /// コンパイルキャッシュをクリア
        /// </summary>
        private void ClearCompilationCache()
        {
            _cacheManager.ClearCompilationCache();
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
            _cacheManager.ClearCache();
        }

        private string GenerateCorrelationId()
        {
            return McpConstants.GenerateCorrelationId();
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
            return _cacheManager.CheckCache(request);
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

        /// <summary>
        /// セキュリティ検証を実行（Restrictedモードの場合のみ）
        /// </summary>
        private CompilationResult ValidateSecurityIfNeeded(CompilationContext context, CSharpCompilationOptions compilationOptions, string correlationId)
        {
            if (_currentSecurityLevel != DynamicCodeSecurityLevel.Restricted)
            {
                return null; // セキュリティ検証不要
            }

            // セキュリティ検証用に一時的に全参照を含むコンパイレーションを作成
            // これにより、System.IOなどの危険な型もSemanticModelで解決可能になる
            List<MetadataReference> securityCheckReferences = new();
            
            // 全てのロード済みアセンブリから参照を作成（危険なものも含む）
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
                {
                    try
                    {
                        securityCheckReferences.Add(MetadataReference.CreateFromFile(assembly.Location));
                    }
                    catch
                    {
                        // 参照追加に失敗した場合は無視
                    }
                }
            }
            
            // セキュリティ検証用のコンパイレーション（全参照付き）
            CSharpCompilation securityCheckCompilation = CSharpCompilation.Create(
                $"SecurityCheck_{correlationId}",
                new[] { context.SyntaxTree },
                securityCheckReferences,
                compilationOptions
            );
            
            VibeLogger.LogInfo(
                "roslyn_security_check_compilation_created",
                "Created compilation with full references for security validation",
                new 
                { 
                    referenceCount = securityCheckReferences.Count,
                    originalReferenceCount = context.References.Count 
                },
                correlationId,
                "Security validation compilation prepared with all references",
                "This allows SemanticModel to resolve dangerous types for detection"
            );
            
            // デバッグログ：セキュリティ検証前のSyntaxTree確認
            string syntaxTreePreview = context.SyntaxTree.GetRoot().ToFullString();
            VibeLogger.LogInfo(
                "roslyn_security_validation_start",
                "Starting security validation with SyntaxTree",
                new 
                { 
                    treeLength = syntaxTreePreview.Length,
                    containsUsingIO = syntaxTreePreview.Contains("using System.IO"),
                    containsUsingHttp = syntaxTreePreview.Contains("using System.Net.Http"),
                    treePreview = syntaxTreePreview.Length > 500 ? syntaxTreePreview.Substring(0, 500) + "..." : syntaxTreePreview
                },
                correlationId,
                "Security validation starting with syntax tree inspection",
                "Check if using directives are present in the tree"
            );
            
            SecurityValidator validator = new(_currentSecurityLevel);
            SecurityValidationResult validationResult = validator.ValidateCompilation(securityCheckCompilation);
            
            if (!validationResult.IsValid)
            {
                // セキュリティ違反を検出した場合は即座にエラーとして返す
                VibeLogger.LogWarning(
                    "roslyn_security_validation_failed",
                    "Security validation failed during compilation",
                    new
                    {
                        violationCount = validationResult.Violations.Count,
                        violations = validationResult.Violations.Select(v => new
                        {
                            type = v.Type.ToString(),
                            api = v.ApiName,
                            message = v.Message
                        }).ToArray()
                    },
                    correlationId,
                    "Dangerous API usage detected in user code",
                    "Review and fix security violations"
                );
                
                return new CompilationResult
                {
                    Success = false,
                    UpdatedCode = context.WrappedCode,
                    Errors = new List<CompilationError>
                    {
                        new CompilationError
                        {
                            Message = validationResult.GetErrorSummary(),
                            ErrorCode = "SECURITY001",
                            Line = 0,
                            Column = 0
                        }
                    },
                    Warnings = new List<string>(),
                    HasSecurityViolations = true,
                    SecurityViolations = validationResult.Violations,
                    FailureReason = CompilationFailureReason.SecurityViolation
                };
            }

            return null; // セキュリティ検証成功
        }

        /// <summary>
        /// 診断駆動修正を適用
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

                // 動的アセンブリ追加機能は削除されました
                // using文の抽出により型解決が改善されたため不要になりました

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

            return currentTree;
        }

        /// <summary>
        /// コンパイル初期化
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
            // コンパイルオプションの準備
            CSharpCompilationOptions compilationOptions = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                allowUnsafe: false
            );

            // セキュリティ検証（Restrictedモードの場合）
            CompilationResult securityValidationResult = ValidateSecurityIfNeeded(context, compilationOptions, correlationId);
            if (securityValidationResult != null)
            {
                return securityValidationResult; // セキュリティ違反で早期リターン
            }

            // 基本のコンパイル作成
            CSharpCompilation compilation = CreateCompilation(
                $"DynamicAssembly_{correlationId}",
                context.SyntaxTree,
                context.References
            );

            // Unity AI Assistant方式の診断駆動修正
            SyntaxTree fixedTree = ApplyDiagnosticFixes(compilation, context.SyntaxTree, correlationId);
            
            // 修正されたTreeでコンテキストを更新
            context.SyntaxTree = fixedTree;
            context.WrappedCode = fixedTree.ToString();
            
            // 最終的なコンパイル
            compilation = compilation.ReplaceSyntaxTree(
                compilation.SyntaxTrees.First(),
                fixedTree
            );

            // アセンブリの出力と結果処理
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
            // 既に名前空間やクラスが含まれているかチェック
            if (code.Contains("namespace ") || code.Contains("class "))
            {
                return code; // そのまま返す
            }

            // AIが書いたコードからusing文を抽出
            List<string> usingStatements = new();
            List<string> codeLines = new();
            
            string[] lines = code.Split(new char[] { '\n' }, StringSplitOptions.None);
            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                
                // using文を検出（"using " で始まり ";" を含む）
                // コメント付きの場合も考慮
                if (trimmedLine.StartsWith("using ") && trimmedLine.Contains(";"))
                {
                    // セキュリティ検証用にusing文を保持
                    // ただし、RestrictedモードではWrapCodeIfNeeded後にセキュリティ検証で
                    // 危険な名前空間が検出されるので、ここでは単に抽出のみ
                    usingStatements.Add(trimmedLine);
                }
                else if (!string.IsNullOrWhiteSpace(trimmedLine))
                {
                    // using文以外のコード行
                    codeLines.Add(line);
                }
            }

            // クラスでラップ
            StringBuilder wrappedCode = new();
            
            // using文を最初に配置（AIが指定したものをそのまま使用）
            foreach (string usingStatement in usingStatements)
            {
                wrappedCode.AppendLine(usingStatement);
            }
            
            // using文があった場合は空行を追加
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

            // using文以外のコードを適切にインデント
            foreach (string line in codeLines)
            {
                wrappedCode.AppendLine($"            {line}");
            }

            wrappedCode.AppendLine("        }");
            wrappedCode.AppendLine("    }");
            wrappedCode.AppendLine("}");

            // ログ出力（デバッグ用）
            string wrappedCodeString = wrappedCode.ToString();
            if (usingStatements.Count > 0)
            {
                VibeLogger.LogInfo(
                    "wrap_code_extracted_usings",
                    $"Extracted {usingStatements.Count} using statements from AI-generated code",
                    new { 
                        usingCount = usingStatements.Count,
                        usings = usingStatements.ToArray(),
                        className = className,
                        wrappedCodePreview = wrappedCodeString.Length > 500 ? wrappedCodeString.Substring(0, 500) + "..." : wrappedCodeString
                    },
                    correlationId: McpConstants.GenerateCorrelationId(),
                    humanNote: "AI-provided using statements preserved and relocated",
                    aiTodo: "Monitor using statement extraction patterns"
                );
            }

            return wrappedCodeString;
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
                            Type = SecurityViolationType.DangerousApiCall,
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


        private string GenerateCacheKey(CompilationRequest request)
        {
            return _cacheManager.GenerateCacheKey(request);
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
#endif