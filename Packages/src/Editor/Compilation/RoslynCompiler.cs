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
    /// v3.0 静的アセンブリ初期化戦略対応
    /// 関連クラス: CompilationRequest, CompilationResult, DynamicCodeSecurityManager
    /// </summary>
    public class RoslynCompiler : IDisposable
    {
        private readonly List<MetadataReference> _defaultReferences = new();
        private readonly Dictionary<string, Assembly> _compilationCache = new();
        private readonly Dictionary<DynamicCodeSecurityLevel, List<MetadataReference>> _referenceCache = new();
        private DynamicCodeSecurityLevel _currentSecurityLevel;
        private List<MetadataReference> _currentReferences;
        private bool _disposed;

        // FixProviderリスト（現在は空）
        // 完全修飾名の使用を推奨するため、using自動追加は無効化
        private static readonly List<CSharpFixProvider> FixProviders = new()
        {
        };

        public RoslynCompiler()
        {
            _currentSecurityLevel = DynamicCodeSecurityManager.CurrentLevel;
            InitializeReferencesForLevel(_currentSecurityLevel);
            
            // セキュリティレベル変更イベントを監視
            DynamicCodeSecurityManager.SecurityLevelChanged += HandleSecurityLevelChanged;
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                // イベント登録解除
                DynamicCodeSecurityManager.SecurityLevelChanged -= HandleSecurityLevelChanged;
                
                // キャッシュクリア
                _referenceCache.Clear();
                _compilationCache.Clear();
                _defaultReferences.Clear();
                
                _disposed = true;
            }
        }

        /// <summary>
        /// セキュリティレベル変更ハンドラ
        /// </summary>
        private void HandleSecurityLevelChanged(DynamicCodeSecurityLevel newLevel)
        {
            string correlationId = Guid.NewGuid().ToString("N")[..8];
            
            VibeLogger.LogInfo(
                "roslyn_compiler_security_level_change",
                $"Handling security level change to: {newLevel}",
                new { 
                    oldLevel = _currentSecurityLevel.ToString(), 
                    newLevel = newLevel.ToString() 
                },
                correlationId,
                "Reinitializing compiler for new security level",
                "Monitor compiler reinitialization performance"
            );
            
            InitializeReferencesForLevel(newLevel);
            ClearCompilationCache();
        }
        
        /// <summary>
        /// セキュリティレベルに応じたアセンブリ参照を初期化
        /// </summary>
        public void InitializeReferencesForLevel(DynamicCodeSecurityLevel level)
        {
            string correlationId = Guid.NewGuid().ToString("N")[..8];
            _currentSecurityLevel = level;
            
            // キャッシュチェック
            if (_referenceCache.TryGetValue(level, out List<MetadataReference> cachedReferences))
            {
                _currentReferences = cachedReferences;
                _defaultReferences.Clear();
                _defaultReferences.AddRange(cachedReferences);
                
                VibeLogger.LogInfo(
                    "roslyn_compiler_references_from_cache",
                    "Using cached references for security level",
                    new { 
                        level = level.ToString(),
                        referenceCount = cachedReferences.Count 
                    },
                    correlationId,
                    "References loaded from cache",
                    "Monitor cache hit rate"
                );
                return;
            }
            
            // 新規参照構築
            _defaultReferences.Clear();
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
            
            // キャッシュに保存
            _referenceCache[level] = newReferences;
            _currentReferences = newReferences;
            
            VibeLogger.LogInfo(
                "roslyn_compiler_references_initialized",
                "RoslynCompiler references initialized for security level",
                new { 
                    level = level.ToString(),
                    referenceCount = newReferences.Count,
                    assemblyCount = allowedAssemblies.Count
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
            _compilationCache.Clear();
            
            VibeLogger.LogInfo(
                "roslyn_compilation_cache_cleared",
                "Compilation cache cleared due to security level change",
                new { cacheSize = 0 },
                correlationId: Guid.NewGuid().ToString("N")[..8],
                humanNote: "Cache cleared for security consistency",
                aiTodo: "Monitor cache clear frequency"
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

            // セキュリティ検証を最初に実行（Level 1 Restrictedモードの場合）
            // エラーの有無に関わらず、危険なAPIの使用を検出
            if (_currentSecurityLevel == DynamicCodeSecurityLevel.Restricted)
            {
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
            }

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
            if (usingStatements.Count > 0)
            {
                VibeLogger.LogInfo(
                    "wrap_code_extracted_usings",
                    $"Extracted {usingStatements.Count} using statements from AI-generated code",
                    new { 
                        usingCount = usingStatements.Count,
                        usings = usingStatements.ToArray(),
                        className = className
                    },
                    correlationId: Guid.NewGuid().ToString("N")[..8],
                    humanNote: "AI-provided using statements preserved and relocated",
                    aiTodo: "Monitor using statement extraction patterns"
                );
            }

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
#endif