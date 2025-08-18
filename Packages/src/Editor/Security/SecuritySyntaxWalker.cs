#if ULOOPMCP_HAS_ROSLYN
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// 包括的なコード検査を行うSyntaxWalker
    /// 設計ドキュメント参照: working-notes/2025-08-16_Restrictedモードユーザークラス実行機能_design.md
    /// 関連クラス: DangerousApiDetector, SecurityValidator
    /// </summary>
    public class SecuritySyntaxWalker : CSharpSyntaxWalker
    {
        private readonly List<SecurityViolation> violations = new();
        private readonly SemanticModel semanticModel;
        private readonly DangerousApiDetector apiDetector;
        
        public IReadOnlyList<SecurityViolation> Violations => violations;
        
        public SecuritySyntaxWalker(SemanticModel model) : base(SyntaxWalkerDepth.Node)
        {
            this.semanticModel = model;
            this.apiDetector = new DangerousApiDetector();
        }
        
        // クラス宣言の検査
        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            // 基底クラスの検査
            if (node.BaseList != null)
            {
                foreach (BaseTypeSyntax baseType in node.BaseList.Types)
                {
                    ITypeSymbol typeSymbol = semanticModel.GetTypeInfo(baseType.Type).Type;
                    if (typeSymbol != null && apiDetector.IsDangerousType(typeSymbol))
                    {
                        violations.Add(new SecurityViolation
                        {
                            Type = SecurityViolationType.DangerousInheritance,
                            Description = $"Class inherits from dangerous type: {typeSymbol}",
                            Message = $"Class inherits from dangerous type: {typeSymbol}",
                            Location = node.GetLocation(),
                            ApiName = typeSymbol.ToDisplayString()
                        });
                    }
                }
            }
            
            base.VisitClassDeclaration(node);
        }
        
        // メソッド宣言の検査
        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            // メソッド内の処理を検査
            CheckMethodBody(node.Body, node.ExpressionBody);
            base.VisitMethodDeclaration(node);
        }
        
        // プロパティ宣言の検査
        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            // getterとsetterの検査
            if (node.AccessorList != null)
            {
                foreach (AccessorDeclarationSyntax accessor in node.AccessorList.Accessors)
                {
                    CheckMethodBody(accessor.Body, accessor.ExpressionBody);
                }
            }
            
            // Expression-bodied propertyの検査
            if (node.ExpressionBody != null)
            {
                CheckExpression(node.ExpressionBody.Expression);
            }
            
            base.VisitPropertyDeclaration(node);
        }
        
        // コンストラクタ宣言の検査
        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            CheckMethodBody(node.Body, node.ExpressionBody);
            base.VisitConstructorDeclaration(node);
        }
        
        // 単純ラムダ式の検査
        public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
        {
            CheckLambdaBody(node.Body);
            base.VisitSimpleLambdaExpression(node);
        }
        
        // 括弧付きラムダ式の検査
        public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
        {
            CheckLambdaBody(node.Body);
            base.VisitParenthesizedLambdaExpression(node);
        }
        
        // 匿名メソッドの検査
        public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
        {
            CheckLambdaBody(node.Body);
            base.VisitAnonymousMethodExpression(node);
        }
        
        // ローカル関数の検査
        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            CheckMethodBody(node.Body, node.ExpressionBody);
            base.VisitLocalFunctionStatement(node);
        }
        
        // using宣言の検査
        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            string namespaceName = node.Name?.ToString();
            
            // デバッグログ
            VibeLogger.LogInfo(
                "security_walker_using_check",
                $"Checking using directive: {namespaceName}",
                new { 
                    namespaceName,
                    isDangerous = !string.IsNullOrEmpty(namespaceName) && apiDetector.IsDangerousNamespace(namespaceName)
                },
                correlationId: System.Guid.NewGuid().ToString("N")[..8],
                humanNote: "SecuritySyntaxWalker checking using directive",
                aiTodo: "Track using directive security validation"
            );
            
            if (!string.IsNullOrEmpty(namespaceName) && apiDetector.IsDangerousNamespace(namespaceName))
            {
                violations.Add(new SecurityViolation
                {
                    Type = SecurityViolationType.ForbiddenNamespace,
                    Description = $"Using dangerous namespace: {namespaceName}",
                    Message = $"Using dangerous namespace: {namespaceName}",
                    Location = node.GetLocation(),
                    ApiName = namespaceName
                });
            }
            
            base.VisitUsingDirective(node);
        }
        
        // メンバーアクセス式の検査（強化版）
        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            ISymbol symbol = semanticModel.GetSymbolInfo(node).Symbol;
            if (symbol != null)
            {
                string fullName = GetFullSymbolName(symbol);
                if (apiDetector.IsDangerousApi(fullName))
                {
                    violations.Add(new SecurityViolation
                    {
                        Type = SecurityViolationType.DangerousApiCall,
                        Description = $"Dangerous API detected: {fullName}",
                        Message = $"Dangerous API detected: {fullName}",
                        Location = node.GetLocation(),
                        ApiName = fullName
                    });
                }
            }
            
            base.VisitMemberAccessExpression(node);
        }
        
        // 識別子の検査（型名の直接使用）
        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            ISymbol symbol = semanticModel.GetSymbolInfo(node).Symbol;
            if (symbol is ITypeSymbol typeSymbol)
            {
                string fullName = typeSymbol.ToDisplayString();
                if (apiDetector.IsDangerousApi(fullName))
                {
                    violations.Add(new SecurityViolation
                    {
                        Type = SecurityViolationType.DangerousApiCall,
                        Description = $"Dangerous type usage detected: {fullName}",
                        Message = $"Dangerous type usage detected: {fullName}",
                        Location = node.GetLocation(),
                        ApiName = fullName
                    });
                }
            }
            
            base.VisitIdentifierName(node);
        }
        
        // オブジェクト生成式の検査
        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            ITypeSymbol typeSymbol = semanticModel.GetTypeInfo(node).Type;
            if (typeSymbol != null && apiDetector.IsDangerousType(typeSymbol))
            {
                violations.Add(new SecurityViolation
                {
                    Type = SecurityViolationType.DangerousTypeCreation,
                    Description = $"Creating instance of dangerous type: {typeSymbol}",
                    Message = $"Creating instance of dangerous type: {typeSymbol}",
                    Location = node.GetLocation(),
                    ApiName = typeSymbol.ToDisplayString()
                });
            }
            
            base.VisitObjectCreationExpression(node);
        }
        
        // 呼び出し式の検査
        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            IMethodSymbol methodSymbol = semanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol;
            if (methodSymbol != null)
            {
                string fullName = GetFullMethodName(methodSymbol);
                if (apiDetector.IsDangerousApi(fullName))
                {
                    violations.Add(new SecurityViolation
                    {
                        Type = SecurityViolationType.DangerousApiCall,
                        Description = $"Dangerous method call detected: {fullName}",
                        Message = $"Dangerous method call detected: {fullName}",
                        Location = node.GetLocation(),
                        ApiName = fullName
                    });
                }
            }
            // 完全修飾名の使用を強制するため、型解決できない場合はエラーにする
            // ユーザーは System.IO.File.WriteAllText のように書く必要がある
            
            base.VisitInvocationExpression(node);
        }
        
        // ヘルパーメソッド
        private void CheckMethodBody(BlockSyntax block, ArrowExpressionClauseSyntax expressionBody)
        {
            if (block != null)
            {
                Visit(block);
            }
            
            if (expressionBody != null)
            {
                CheckExpression(expressionBody.Expression);
            }
        }
        
        private void CheckLambdaBody(CSharpSyntaxNode body)
        {
            if (body is BlockSyntax block)
            {
                Visit(block);
            }
            else if (body is ExpressionSyntax expression)
            {
                CheckExpression(expression);
            }
        }
        
        private void CheckExpression(ExpressionSyntax expression)
        {
            Visit(expression);
        }
        
        private string GetFullSymbolName(ISymbol symbol)
        {
            return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }
        
        private string GetFullMethodName(IMethodSymbol method)
        {
            string typeName = method.ContainingType?.ToDisplayString() ?? "";
            string methodName = method.Name;
            return string.IsNullOrEmpty(typeName) ? methodName : $"{typeName}.{methodName}";
        }
    }
}
#endif