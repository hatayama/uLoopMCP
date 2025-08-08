using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace io.github.hatayama.uLoopMCP.DynamicExecution
{
    /// <summary>
    /// Roslynを使用したC#動的コンパイル機能

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
                Assembly netStandardAssembly = Assembly.Load("netstandard");
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
                    string mscorlibPath = typeof(object).Assembly.Location;
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
            string[] runtimePaths = new[]
            {
                Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll"),
                Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Private.CoreLib.dll")
            };
            
            foreach (string path in runtimePaths)
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
                new { 
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
                new { 
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

        private string WrapCodeIfNeeded(string code, string namespaceName, string className)
        {
            // 既に名前空間やクラスが含まれているかチェック
            if (code.Contains("namespace ") || code.Contains("class "))
            {
                return code; // そのまま返す
            }
            
            // シンプルなメソッドや式の場合は、クラスでラップ
            StringBuilder wrappedCode = new StringBuilder();
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
                    LineNumber = d.Location.GetLineSpan().StartLinePosition.Line + 1,
                    ColumnNumber = d.Location.GetLineSpan().StartLinePosition.Character + 1
                })
                .ToList();
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