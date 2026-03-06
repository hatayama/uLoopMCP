#if ULOOPMCP_HAS_ROSLYN
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// A SyntaxWalker that traverses the Roslyn syntax tree to detect security violations.
    /// Comprehensively examines dangerous API calls, type inheritance, instance creation, and more.
    /// Inherits from CSharpSyntaxWalker and implements Visit methods for various syntax elements.
    /// Records detected violations as SecurityViolation and reports them to the caller.
    /// Related classes: DangerousApiDetector (dangerous API detection), SecurityValidator (verification entry point)
    /// </summary>
    public class SecuritySyntaxWalker : CSharpSyntaxWalker
    {
        private readonly List<SecurityViolation> violations = new();
        private readonly SemanticModel semanticModel;
        private readonly DangerousApiDetector apiDetector;
        
        public IReadOnlyList<SecurityViolation> Violations => violations;
        
        public SecuritySyntaxWalker(SemanticModel model) : base(SyntaxWalkerDepth.Node)
        {
            semanticModel = model;
            apiDetector = new DangerousApiDetector();
        }
        
        /// <summary>
        /// Visits class declaration nodes to inspect security violations in inheritance relationships.
        /// Checks whether base classes or interfaces are dangerous types and
        /// records violations as DangerousInheritance type when found.
        /// </summary>
        /// <param name="node">The class declaration syntax node to be examined</param>
        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            // Inspect base class
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
        
        /// <summary>
        /// Visits method declaration nodes to inspect security violations within method bodies.
        /// Supports both block and expression-bodied forms, detecting
        /// dangerous APIs and types used within methods.
        /// </summary>
        /// <param name="node">The method declaration syntax node to be examined</param>
        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            // Inspect method body processing
            CheckMethodBody(node.Body, node.ExpressionBody);
            base.VisitMethodDeclaration(node);
        }
        
        /// <summary>
        /// Visits property declaration nodes to inspect security violations in getters and setters.
        /// Supports both traditional accessor forms and expression-bodied forms, detecting
        /// dangerous APIs and types used in property implementations.
        /// </summary>
        /// <param name="node">The property declaration syntax node to be examined</param>
        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            // Inspect getters and setters
            if (node.AccessorList != null)
            {
                foreach (AccessorDeclarationSyntax accessor in node.AccessorList.Accessors)
                {
                    CheckMethodBody(accessor.Body, accessor.ExpressionBody);
                }
            }
            
            // Inspect expression-bodied properties
            if (node.ExpressionBody != null)
            {
                CheckExpression(node.ExpressionBody.Expression);
            }
            
            base.VisitPropertyDeclaration(node);
        }
        
        /// <summary>
        /// Visits constructor declaration nodes to inspect security violations in initialization processes.
        /// Detects dangerous APIs and type instantiations used within constructor bodies,
        /// identifying security risks during object initialization.
        /// </summary>
        /// <param name="node">The constructor declaration syntax node to be examined</param>
        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            CheckMethodBody(node.Body, node.ExpressionBody);
            base.VisitConstructorDeclaration(node);
        }
        
        /// <summary>
        /// Visits simple lambda expression nodes (single parameter without parentheses) to inspect security violations.
        /// Detects dangerous APIs and types used within lambda bodies,
        /// identifying security risks in anonymous functions.
        /// </summary>
        /// <param name="node">The simple lambda expression syntax node to be examined</param>
        public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
        {
            CheckLambdaBody(node.Body);
            base.VisitSimpleLambdaExpression(node);
        }
        
        /// <summary>
        /// Visits parenthesized lambda expression nodes (multiple parameters or with type specification) to inspect security violations.
        /// Detects dangerous APIs and types used within lambda bodies,
        /// identifying security risks in delegates and event handlers.
        /// </summary>
        /// <param name="node">The parenthesized lambda expression syntax node to be examined</param>
        public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
        {
            CheckLambdaBody(node.Body);
            base.VisitParenthesizedLambdaExpression(node);
        }
        
        /// <summary>
        /// Visits anonymous method expression nodes (using delegate keyword) to inspect security violations.
        /// Detects dangerous APIs and types used within anonymous method bodies,
        /// identifying security risks in code executed as delegates.
        /// </summary>
        /// <param name="node">The anonymous method expression syntax node to be examined</param>
        public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
        {
            CheckLambdaBody(node.Body);
            base.VisitAnonymousMethodExpression(node);
        }
        
        /// <summary>
        /// Visits local function statement nodes (functions defined within methods) to inspect security violations.
        /// Detects dangerous APIs and types used within local function bodies,
        /// identifying security risks in functions defined and executed within methods.
        /// </summary>
        /// <param name="node">The local function statement syntax node to be examined</param>
        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            CheckMethodBody(node.Body, node.ExpressionBody);
            base.VisitLocalFunctionStatement(node);
        }
        
        /// <summary>
        /// Visits member access expression nodes (dot operator calls) to detect dangerous API calls.
        /// Examines access to properties, methods, and fields,
        /// matching dangerous API patterns in collaboration with DangerousApiDetector.
        /// Violations are recorded as DangerousApiCall type.
        /// </summary>
        /// <param name="node">The member access expression syntax node to be examined</param>
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
        
        /// <summary>
        /// Visits identifier name nodes (variable names, type names, method names) to detect direct references to dangerous types.
        /// Checks whether identifiers resolved as types are dangerous,
        /// identifying direct references to types like System.Diagnostics.Process.
        /// Violations are recorded as DangerousApiCall type.
        /// </summary>
        /// <param name="node">The identifier name syntax node to be examined</param>
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
        
        /// <summary>
        /// Visits object creation expression nodes (new operator) to detect instantiation of dangerous types.
        /// Checks whether the type being created is in the dangerous type list of DangerousApiDetector,
        /// identifying creation of dangerous objects like Process or WebClient.
        /// Violations are recorded as DangerousTypeCreation type.
        /// </summary>
        /// <param name="node">The object creation expression syntax node to be examined</param>
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
        
        /// <summary>
        /// Visits method invocation expression nodes to detect dangerous method executions.
        /// Analyzes the symbol information of invoked methods,
        /// identifying dangerous method calls like File.Delete or Process.Start.
        /// Violations are recorded as DangerousApiCall type.
        /// </summary>
        /// <param name="node">The invocation expression syntax node to be examined</param>
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
            
            base.VisitInvocationExpression(node);
        }

        /// <summary>
        /// Visits assignment expressions to detect dangerous property assignments
        /// (e.g., modifying GCSettings.LatencyMode).
        /// </summary>
        /// <param name="node">The assignment expression syntax node</param>
        public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            // Check left-hand side symbol (target being assigned to)
            ISymbol leftSymbol = semanticModel.GetSymbolInfo(node.Left).Symbol;
            if (leftSymbol is IPropertySymbol propertySymbol)
            {
                string containingTypeName = propertySymbol.ContainingType?.ToDisplayString();
                string propertyName = propertySymbol.Name;
                if (containingTypeName == "System.Runtime.GCSettings" && propertyName == "LatencyMode")
                {
                    string apiName = $"{containingTypeName}.{propertyName}";
                    violations.Add(new SecurityViolation
                    {
                        Type = SecurityViolationType.DangerousApiCall,
                        Description = $"Dangerous property assignment detected: {apiName}",
                        Message = $"Dangerous property assignment detected: {apiName}",
                        Location = node.GetLocation(),
                        ApiName = apiName
                    });
                }
            }

            base.VisitAssignmentExpression(node);
        }
        
        /// <summary>
        /// A common helper method for method body inspection that supports multiple formats.
        /// Handles both block-style (multiple statements enclosed in {}) and expression-bodied forms (=> expression).
        /// Used commonly from methods, constructors, property accessors, etc.
        /// </summary>
        /// <param name="block">Method body in block form (can be null)</param>
        /// <param name="expressionBody">Method body in expression-bodied form (can be null)</param>
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
        
        /// <summary>
        /// A common helper method for inspecting lambda and anonymous method bodies.
        /// Determines whether the lambda body is a block or a single expression,
        /// and calls the appropriate inspection method.
        /// Used commonly for simple lambdas, parenthesized lambdas, and anonymous methods.
        /// </summary>
        /// <param name="body">The lambda or anonymous method body (BlockSyntax or ExpressionSyntax)</param>
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
        
        /// <summary>
        /// A helper method for inspecting single expressions.
        /// Used when examining expression-bodied members or single-expression lambda bodies,
        /// invoking Visit methods to recursively traverse the syntax tree.
        /// </summary>
        /// <param name="expression">The expression syntax node to be examined</param>
        private void CheckExpression(ExpressionSyntax expression)
        {
            Visit(expression);
        }
        
        /// <summary>
        /// A helper method to retrieve the fully qualified symbol name.
        /// Returns a complete name including namespace (e.g., System.Diagnostics.Process)
        /// used for matching in DangerousApiDetector.
        /// Uses Roslyn's SymbolDisplayFormat.FullyQualifiedFormat to obtain the precise name.
        /// </summary>
        /// <param name="symbol">The symbol to retrieve the name for</param>
        /// <returns>Fully qualified name as a string</returns>
        private string GetFullSymbolName(ISymbol symbol)
        {
            return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }
        
        /// <summary>
        /// A helper method to retrieve the fully qualified method name (type.methodName).
        /// Combines the containing type name and method name (e.g., System.IO.File.Delete)
        /// used for matching dangerous method patterns in DangerousApiDetector.
        /// Returns only the method name if the containing type is unknown.
        /// </summary>
        /// <param name="method">The method symbol to retrieve the name for</param>
        /// <returns>A string in the format "type.methodName"</returns>
        private string GetFullMethodName(IMethodSymbol method)
        {
            string typeName = method.ContainingType?.ToDisplayString() ?? "";
            string methodName = method.Name;
            return string.IsNullOrEmpty(typeName) ? methodName : $"{typeName}.{methodName}";
        }
    }
}
#endif