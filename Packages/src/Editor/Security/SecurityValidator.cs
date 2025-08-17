#if ULOOPMCP_HAS_ROSLYN
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// セキュリティ検証の中核クラス（改修版）
    /// Compilationオブジェクトを受け取り、SemanticModelを活用
    /// 設計ドキュメント参照: working-notes/2025-08-16_Restrictedモードユーザークラス実行機能_design.md
    /// 関連クラス: SecuritySyntaxWalker, DangerousApiDetector, RoslynCompiler
    /// </summary>
    public class SecurityValidator
    {
        private readonly DynamicCodeSecurityLevel securityLevel;
        
        public SecurityValidator(DynamicCodeSecurityLevel level)
        {
            this.securityLevel = level;
        }
        
        /// <summary>
        /// Compilationオブジェクトを受け取って検証（新規メソッド）
        /// </summary>
        public SecurityValidationResult ValidateCompilation(CSharpCompilation compilation)
        {
            SecurityValidationResult result = new()
            {
                IsValid = true,
                Violations = new List<SecurityViolation>(),
                CompilationErrors = new List<string>()
            };
            
            // Level 2 (FullAccess)は検証スキップ
            if (securityLevel == DynamicCodeSecurityLevel.FullAccess)
            {
                return result;
            }
            
            // Level 0 (Disabled)は即座に拒否
            if (securityLevel == DynamicCodeSecurityLevel.Disabled)
            {
                result.IsValid = false;
                result.Violations.Add(new SecurityViolation
                {
                    ViolationType = ViolationType.DangerousApiCall,
                    Message = "Code execution is disabled at current security level",
                    ApiName = "N/A"
                });
                return result;
            }
            
            // Level 1 (Restricted): 詳細検査を実行
            string correlationId = Guid.NewGuid().ToString("N")[..8];
            
            // 全てのSyntaxTreeを検査
            foreach (SyntaxTree tree in compilation.SyntaxTrees)
            {
                SemanticModel semanticModel = compilation.GetSemanticModel(tree);
                SecuritySyntaxWalker walker = new(semanticModel);
                
                // ルートノードから走査開始
                SyntaxNode root = tree.GetRoot();
                walker.Visit(root);
                
                // 違反を収集
                if (walker.Violations.Any())
                {
                    result.IsValid = false;
                    result.Violations.AddRange(walker.Violations);
                    
                    // ログ出力
                    foreach (SecurityViolation violation in walker.Violations)
                    {
                        VibeLogger.LogWarning(
                            "security_violation_detected",
                            violation.Message,
                            new
                            {
                                type = violation.Type.ToString(),
                                location = violation.Location?.ToString(),
                                apiName = violation.ApiName
                            },
                            correlationId,
                            "Security violation found during code analysis",
                            "Review and fix dangerous API usage"
                        );
                    }
                }
            }
            
            // 診断情報も確認
            ImmutableArray<Diagnostic> diagnostics = compilation.GetDiagnostics();
            foreach (Diagnostic diagnostic in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
            {
                result.CompilationErrors.Add(diagnostic.GetMessage());
            }
            
            return result;
        }
        
        /// <summary>
        /// 従来のコード文字列検証（後方互換性のため維持）
        /// </summary>
        public SecurityValidationResult ValidateCode(string code)
        {
            // SyntaxTreeを作成
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
            
            // 簡易Compilationを作成
            CSharpCompilation compilation = CreateSimpleCompilation(tree);
            
            // 新しいメソッドに委譲
            return ValidateCompilation(compilation);
        }
        
        private CSharpCompilation CreateSimpleCompilation(SyntaxTree tree)
        {
            // 基本的な参照のみで簡易コンパイル
            List<MetadataReference> references = new()
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location)
            };
            
            // Unity関連アセンブリも追加
            System.Reflection.Assembly unityEngine = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "UnityEngine");
            if (unityEngine != null && !string.IsNullOrEmpty(unityEngine.Location))
            {
                references.Add(MetadataReference.CreateFromFile(unityEngine.Location));
            }
            
            return CSharpCompilation.Create(
                "SecurityValidation",
                syntaxTrees: new[] { tree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
        }
    }
}
#endif