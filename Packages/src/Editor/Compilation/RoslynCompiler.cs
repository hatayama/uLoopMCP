using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using uLoopMCP.DynamicExecution;
using UnityEngine;
using io.github.hatayama.uLoopMCP;

namespace uLoopMCP.DynamicExecution
{
    /// <summary>
    /// Roslynを使用したC#動的コンパイル機能
    /// 設計ドキュメント: uLoopMCP_DynamicCodeExecution_Design.md
    /// 関連クラス: IRoslynCompiler, CompilationRequest, CompilationResult
    /// </summary>
    public class RoslynCompiler : IRoslynCompiler
    {
        private readonly List<MetadataReference> _defaultReferences = new();
        private readonly Dictionary<string, Assembly> _compilationCache = new();
        
        public RoslynCompiler()
        {
            InitializeReferences();
        }

        public void InitializeReferences()
        {
            _defaultReferences.Clear();
            
            try
            {
                // .NET Standard/Core基本アセンブリ
                var netStandardAssembly = Assembly.Load("netstandard");
                if (netStandardAssembly != null)
                {
                    _defaultReferences.Add(MetadataReference.CreateFromFile(netStandardAssembly.Location));
                }
            }
            catch
            {
                // netstandard見つからない場合はmscorlib/System.Runtimeで代替
                try
                {
                    var mscorlibPath = typeof(object).Assembly.Location;
                    _defaultReferences.Add(MetadataReference.CreateFromFile(mscorlibPath));
                }
                catch { }
            }
            
            // 基本的な.NETアセンブリを安全に追加
            try { _defaultReferences.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location)); } catch { }
            try { _defaultReferences.Add(MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location)); } catch { }
            try { _defaultReferences.Add(MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location)); } catch { }
            try { _defaultReferences.Add(MetadataReference.CreateFromFile(typeof(System.Console).Assembly.Location)); } catch { }
            
            // Unityアセンブリを追加
            try { _defaultReferences.Add(MetadataReference.CreateFromFile(typeof(UnityEngine.Debug).Assembly.Location)); } catch { }
            try { _defaultReferences.Add(MetadataReference.CreateFromFile(typeof(UnityEngine.GameObject).Assembly.Location)); } catch { }
            
            // System.Runtimeを明示的に追加
            var runtimePaths = new[]
            {
                Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll"),
                Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Private.CoreLib.dll")
            };
            
            foreach (var path in runtimePaths)
            {
                if (File.Exists(path))
                {
                    try { _defaultReferences.Add(MetadataReference.CreateFromFile(path)); } catch { }
                }
            }
            
            // 現在のアセンブリも追加（uLoopMCP関連クラスにアクセスするため）
            try { _defaultReferences.Add(MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location)); } catch { }
            
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
            string correlationId = Guid.NewGuid().ToString("N")[..8];
            
            try
            {
                VibeLogger.LogInfo(
                    "roslyn_compile_start",
                    "Starting Roslyn compilation",
                    new { 
                        codeLength = request.Code?.Length ?? 0,
                        className = request.ClassName,
                        @namespace = request.Namespace,
                        additionalRefsCount = request.AdditionalReferences?.Count ?? 0
                    },
                    correlationId,
                    "Dynamic code compilation started",
                    "Monitor compilation success rate and performance"
                );
                
                // キャッシュチェック
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
                
                // コードの前処理（必要に応じて名前空間とクラスでラップ）
                string wrappedCode = WrapCodeIfNeeded(request.Code, request.Namespace, request.ClassName);
                
                // Syntax Treeの作成
                var syntaxTree = CSharpSyntaxTree.ParseText(wrappedCode);
                
                // 参照の準備
                var references = new List<MetadataReference>(_defaultReferences);
                
                // 追加参照を処理
                foreach (var additionalRef in request.AdditionalReferences ?? new List<string>())
                {
                    if (File.Exists(additionalRef))
                    {
                        references.Add(MetadataReference.CreateFromFile(additionalRef));
                    }
                }
                
                // コンパイル設定
                var compilationOptions = new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    allowUnsafe: false // セキュリティのためunsafeコードは禁止
                );
                
                // コンパイル実行
                var compilation = CSharpCompilation.Create(
                    $"DynamicAssembly_{correlationId}",
                    new[] { syntaxTree },
                    references,
                    compilationOptions
                );
                
                // メモリにアセンブリを生成
                using var memoryStream = new MemoryStream();
                var emitResult = compilation.Emit(memoryStream);
                
                var result = new CompilationResult
                {
                    UpdatedCode = wrappedCode
                };
                
                if (emitResult.Success)
                {
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    var assembly = Assembly.Load(memoryStream.ToArray());
                    
                    result.Success = true;
                    result.CompiledAssembly = assembly;
                    
                    // キャッシュに保存
                    _compilationCache[cacheKey] = assembly;
                    
                    VibeLogger.LogInfo(
                        "roslyn_compile_success",
                        "Compilation completed successfully",
                        new { 
                            assemblyName = assembly.FullName,
                            typeCount = assembly.GetTypes().Length
                        },
                        correlationId,
                        "Dynamic code compilation succeeded",
                        "Track compilation success patterns"
                    );
                }
                else
                {
                    result.Success = false;
                    result.Errors = ConvertDiagnosticsToErrors(emitResult.Diagnostics);
                    
                    VibeLogger.LogWarning(
                        "roslyn_compile_failure",
                        "Compilation failed with errors",
                        new { 
                            errorCount = result.Errors.Count,
                            errors = result.Errors.Select(e => e.Message).ToArray()
                        },
                        correlationId,
                        "Dynamic code compilation failed",
                        "Analyze common compilation errors for auto-fix patterns"
                    );
                }
                
                // 警告も収集
                var warnings = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Warning)
                    .Select(d => d.ToString())
                    .ToList();
                result.Warnings = warnings;
                
                return result;
            }
            catch (Exception ex)
            {
                VibeLogger.LogError(
                    "roslyn_compile_exception",
                    "Exception during Roslyn compilation",
                    new { 
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
                        new()
                        {
                            Message = $"Compilation exception: {ex.Message}",
                            ErrorCode = "INTERNAL_ERROR",
                            LineNumber = 0,
                            ColumnNumber = 0
                        }
                    }
                };
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

        private string WrapCodeIfNeeded(string code, string namespaceName, string className)
        {
            // 既に名前空間やクラスが含まれているかチェック
            if (code.Contains("namespace ") || code.Contains("class "))
            {
                return code; // そのまま返す
            }
            
            // シンプルなメソッドや式の場合は、クラスでラップ
            var wrappedCode = new StringBuilder();
            wrappedCode.AppendLine("using System;");
            wrappedCode.AppendLine("using System.Collections.Generic;");
            wrappedCode.AppendLine("using System.Linq;");
            wrappedCode.AppendLine("using UnityEngine;");
            wrappedCode.AppendLine();
            wrappedCode.AppendLine($"namespace {namespaceName}");
            wrappedCode.AppendLine("{");
            wrappedCode.AppendLine($"    public class {className}");
            wrappedCode.AppendLine("    {");
            wrappedCode.AppendLine("        public object Execute(Dictionary<string, object> parameters = null)");
            wrappedCode.AppendLine("        {");
            
            // コードを適切にインデント
            var lines = code.Split('\n');
            foreach (var line in lines)
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
                    LineNumber = d.Location.GetLineSpan().StartLinePosition.Line + 1,
                    ColumnNumber = d.Location.GetLineSpan().StartLinePosition.Character + 1
                })
                .ToList();
        }

        private string GenerateCacheKey(CompilationRequest request)
        {
            var keyBuilder = new StringBuilder();
            keyBuilder.Append(request.Code);
            keyBuilder.Append(request.ClassName);
            keyBuilder.Append(request.Namespace);
            
            if (request.AdditionalReferences != null)
            {
                foreach (var reference in request.AdditionalReferences.OrderBy(r => r))
                {
                    keyBuilder.Append(reference);
                }
            }
            
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(keyBuilder.ToString()));
        }
    }
}