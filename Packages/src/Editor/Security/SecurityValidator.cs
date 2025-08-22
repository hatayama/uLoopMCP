#if ULOOPMCP_HAS_ROSLYN
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// セキュリティ検証の中核クラス
    /// Compilationオブジェクトを受け取り、SemanticModelを活用
    /// 関連クラス: SecuritySyntaxWalker, DangerousApiDetector, RoslynCompiler
    /// </summary>
    public class SecurityValidator
    {
        private readonly DynamicCodeSecurityLevel _securityLevel;
        
        public SecurityValidator(DynamicCodeSecurityLevel level)
        {
            _securityLevel = level;
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
            if (_securityLevel == DynamicCodeSecurityLevel.FullAccess)
            {
                return result;
            }
            
            // Level 0 (Disabled)は即座に拒否
            if (_securityLevel == DynamicCodeSecurityLevel.Disabled)
            {
                result.IsValid = false;
                result.Violations.Add(new SecurityViolation
                {
                    Type = SecurityViolationType.DangerousApiCall,
                    Description = "Code execution is disabled at current security level",
                    Message = "Code execution is disabled at current security level",
                    ApiName = "N/A"
                });
                return result;
            }
            
            // Level 1 (Restricted): 詳細検査を実行
            string correlationId = McpConstants.GenerateCorrelationId();
            
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
    }
}
#endif