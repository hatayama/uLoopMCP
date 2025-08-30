#if ULOOPMCP_HAS_ROSLYN
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Core class for security validation
    /// Receives a Compilation object and utilizes the SemanticModel
    /// Related classes: SecuritySyntaxWalker, DangerousApiDetector, RoslynCompiler
    /// </summary>
    public class SecurityValidator
    {
        private readonly DynamicCodeSecurityLevel _securityLevel;
        
        public SecurityValidator(DynamicCodeSecurityLevel level)
        {
            _securityLevel = level;
        }
        
        /// <summary>
        /// Validates a Compilation object (new method)
        /// </summary>
        public SecurityValidationResult ValidateCompilation(CSharpCompilation compilation)
        {
            SecurityValidationResult result = new()
            {
                IsValid = true,
                Violations = new List<SecurityViolation>(),
                CompilationErrors = new List<string>()
            };
            
            // Skip validation for Level 2 (FullAccess)
            if (_securityLevel == DynamicCodeSecurityLevel.FullAccess)
            {
                return result;
            }
            
            // Level 0 (Disabled): Do not perform static security rejection here.
            // Runtime guard in ExecuteDynamicCodeTool blocks execution; compile-only may still be allowed.
            
            // Level 1 (Restricted): Perform detailed inspection
            string correlationId = McpConstants.GenerateCorrelationId();
            
            // Inspect all SyntaxTrees
            foreach (SyntaxTree tree in compilation.SyntaxTrees)
            {
                SemanticModel semanticModel = compilation.GetSemanticModel(tree);
                SecuritySyntaxWalker walker = new(semanticModel);
                
                // Start scanning from root node
                SyntaxNode root = tree.GetRoot();
                walker.Visit(root);
                
                // Collect violations
                if (walker.Violations.Any())
                {
                    result.IsValid = false;
                    result.Violations.AddRange(walker.Violations);
                    
                    // Log output
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
            
            // Also check diagnostic information
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