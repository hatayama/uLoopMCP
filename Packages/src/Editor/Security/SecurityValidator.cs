#if ULOOPMCP_HAS_ROSLYN
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using io.github.hatayama.uLoopMCP;

namespace io.github.hatayama.uLoopMCP
{
#if ULOOPMCP_HAS_ROSLYN
    /// <summary>
    /// コードセキュリティ検証機能
    /// 関連クラス: SecurityPolicy
    /// </summary>
    public class SecurityValidator
    {
        private SecurityPolicy _policy;

        public SecurityValidator()
        {
            _policy = SecurityPolicy.GetDefault();
        }

        public SecurityValidator(SecurityPolicy policy)
        {
            _policy = policy ?? SecurityPolicy.GetDefault();
        }

        public SecurityValidationResult ValidateCode(string code)
        {
            string correlationId = Guid.NewGuid().ToString("N")[..8];
            
            try
            {
                VibeLogger.LogInfo(
                    "security_validation_start",
                    "Security validation started",
                    new { 
                        codeLength = code?.Length ?? 0,
                        policyStrictness = _policy.MaxCodeLength,
                        forbiddenMethodsCount = _policy.ForbiddenMethods.Count,
                        forbiddenNamespacesCount = _policy.ForbiddenNamespaces.Count
                    },
                    correlationId,
                    "Starting security validation of dynamic code",
                    "Monitor security violation patterns"
                );

                SecurityValidationResult result = new SecurityValidationResult { IsValid = true };

                if (string.IsNullOrWhiteSpace(code))
                {
                    return result; // 空のコードは安全
                }

                // コード長チェック
                if (code.Length > _policy.MaxCodeLength)
                {
                    result.IsValid = false;
                    result.RiskLevel = SecurityLevel.Medium;
                    result.Violations.Add(new SecurityViolation
                    {
                        Type = SecurityViolationType.DangerousMethodCall,
                        Description = $"Code length {code.Length} exceeds maximum allowed {_policy.MaxCodeLength}",
                        LineNumber = 0,
                        CodeSnippet = code.Substring(0, Math.Min(100, code.Length))
                    });
                }

                // Syntax Tree解析による詳細チェック
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code);
                SyntaxNode root = syntaxTree.GetRoot();
                
                SecuritySyntaxWalker walker = new SecuritySyntaxWalker(_policy);
                walker.Visit(root);
                
                result.Violations.AddRange(walker.Violations);
                if (walker.Violations.Count > 0)
                {
                    result.IsValid = false;
                    result.RiskLevel = DetermineRiskLevel(walker.Violations);
                }

                VibeLogger.LogInfo(
                    "security_validation_complete",
                    "Security validation completed",
                    new { 
                        isValid = result.IsValid,
                        violationCount = result.Violations.Count,
                        riskLevel = result.RiskLevel.ToString()
                    },
                    correlationId,
                    $"Validation completed: {(result.IsValid ? "SAFE" : "VIOLATIONS FOUND")}",
                    "Track security violation patterns for policy improvement"
                );

                return result;
            }
            catch (Exception ex)
            {
                VibeLogger.LogError(
                    "security_validation_error",
                    "Security validation failed with exception",
                    new { 
                        error = ex.Message,
                        stackTrace = ex.StackTrace
                    },
                    correlationId,
                    "Security validation encountered an error",
                    "Investigate validation failures"
                );

                return new SecurityValidationResult
                {
                    IsValid = false,
                    RiskLevel = SecurityLevel.Critical,
                    Violations = new List<SecurityViolation>
                    {
                        new()
                        {
                            Type = SecurityViolationType.DangerousMethodCall,
                            Description = $"Security validation failed: {ex.Message}",
                            LineNumber = 0,
                            CodeSnippet = code.Substring(0, Math.Min(100, code.Length))
                        }
                    }
                };
            }
        }

        public void LoadSecurityPolicy(string jsonPath)
        {
            try
            {
                if (!File.Exists(jsonPath))
                {
                    Debug.LogWarning($"Security policy file not found: {jsonPath}");
                    return;
                }

                string jsonContent = File.ReadAllText(jsonPath);
                SecurityPolicy policy = JsonConvert.DeserializeObject<SecurityPolicy>(jsonContent);
                if (policy != null)
                {
                    _policy = policy;
                    Debug.Log($"Security policy loaded from: {jsonPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load security policy: {ex.Message}");
            }
        }

        public bool IsMethodAllowed(string methodSignature)
        {
            if (string.IsNullOrWhiteSpace(methodSignature))
                return true;

            // SecurityPolicyの禁止メソッドチェック
            if (_policy.ForbiddenMethods.Any(forbidden => methodSignature.Contains(forbidden)))
                return false;

            // SecurityPolicyの禁止名前空間チェック  
            if (_policy.ForbiddenNamespaces.Any(ns => methodSignature.StartsWith(ns)))
                return false;

            return true;
        }

        private SecurityLevel DetermineRiskLevel(List<SecurityViolation> violations)
        {
            if (!violations.Any()) return SecurityLevel.Safe;

            SecurityLevel maxRisk = SecurityLevel.Safe;
            
            foreach (SecurityViolation violation in violations)
            {
                SecurityLevel riskLevel = violation.Type switch
                {
                    SecurityViolationType.UnsafeCode => SecurityLevel.Critical,
                    SecurityViolationType.ProcessExecution => SecurityLevel.Critical,
                    SecurityViolationType.FileSystemAccess => SecurityLevel.High,
                    SecurityViolationType.NetworkAccess => SecurityLevel.High,
                    SecurityViolationType.ReflectionUsage => SecurityLevel.Medium,
                    SecurityViolationType.DangerousMethodCall => SecurityLevel.Medium,
                    SecurityViolationType.ForbiddenNamespace => SecurityLevel.Low,
                    _ => SecurityLevel.Low
                };

                if (riskLevel > maxRisk)
                    maxRisk = riskLevel;
            }

            return maxRisk;
        }
    }

    /// <summary>
    /// セキュリティ検証用Syntax Walker
    /// </summary>
    internal class SecuritySyntaxWalker : CSharpSyntaxWalker
    {
        private readonly SecurityPolicy _policy;
        
        public List<SecurityViolation> Violations { get; } = new();

        public SecuritySyntaxWalker(SecurityPolicy policy)
        {
            _policy = policy;
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            string methodCall = node.ToString();
            
            // SecurityPolicyの禁止メソッド呼び出しチェック
            foreach (string forbidden in _policy.ForbiddenMethods)
            {
                if (methodCall.Contains(forbidden))
                {
                    Violations.Add(new SecurityViolation
                    {
                        Type = SecurityViolationType.DangerousMethodCall,
                        Description = $"Forbidden method call detected: {forbidden}",
                        LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        CodeSnippet = methodCall
                    });
                }
            }

            base.VisitInvocationExpression(node);
        }

        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            string namespaceName = node.Name?.ToString();
            
            if (namespaceName != null && _policy.ForbiddenNamespaces.Contains(namespaceName))
            {
                Violations.Add(new SecurityViolation
                {
                    Type = SecurityViolationType.ForbiddenNamespace,
                    Description = $"Forbidden namespace: {namespaceName}",
                    LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    CodeSnippet = node.ToString()
                });
            }

            base.VisitUsingDirective(node);
        }

        public override void VisitUnsafeStatement(UnsafeStatementSyntax node)
        {
            if (!_policy.AllowUnsafeCode)
            {
                Violations.Add(new SecurityViolation
                {
                    Type = SecurityViolationType.UnsafeCode,
                    Description = "Unsafe code is not allowed",
                    LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    CodeSnippet = node.ToString()
                });
            }

            base.VisitUnsafeStatement(node);
        }
    }
#endif
}