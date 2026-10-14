using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityCliLoop.CompileCheckWorker
{
    /// <summary>
    /// Hashes the part of a source file that other files and assemblies can see, so an edit that
    /// keeps the hash only changed what runs inside bodies and initializers.
    /// </summary>
    internal static class DeclarationFingerprint
    {
        // The algorithm version. Bump it (fp2, ...) whenever the hashed text changes, so a baseline
        // fingerprinted by an older worker never matches by accident.
        private const string AlgorithmVersion = "fp1";

        private static readonly Regex WhitespaceRun = new Regex(@"\s+", RegexOptions.CultureInvariant);

        internal static string Compute(SyntaxTree tree)
        {
            SyntaxNode stripped = new BodyStripper().Visit(tree.GetRoot());
            StringBuilder builder = new StringBuilder(AlgorithmVersion).Append('\n');
            foreach (SyntaxToken token in stripped.DescendantTokens())
            {
                AppendDirectives(builder, token.LeadingTrivia);
                builder.Append(token.Text).Append(' ');
                AppendDirectives(builder, token.TrailingTrivia);
            }

            return Sha256Hex(Encoding.UTF8.GetBytes(builder.ToString()));
        }

        internal static string Sha256Hex(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                // Lowercase, because the Go side compares these against hex.EncodeToString.
                return string.Concat(sha256.ComputeHash(bytes).Select(b => b.ToString("x2")));
            }
        }

        private static void AppendDirectives(StringBuilder builder, SyntaxTriviaList trivia)
        {
            foreach (SyntaxTrivia item in trivia)
            {
                // Directives and the text they disable decide which declarations exist, so an edit
                // to them outside a body is a declaration edit.
                if (!item.IsDirective && !item.IsKind(SyntaxKind.DisabledTextTrivia))
                {
                    continue;
                }

                builder.Append(WhitespaceRun.Replace(item.ToString(), " ").Trim()).Append('\n');
            }
        }

        /// <summary>
        /// Replaces bodies and non-constant initializers with empty placeholders. A body is replaced,
        /// not removed, because whether a member has one (an auto property, a default interface
        /// method) is part of its declaration.
        /// </summary>
        private sealed class BodyStripper : CSharpSyntaxRewriter
        {
            public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                return StripMethodBody(node, base.VisitMethodDeclaration(node));
            }

            public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
            {
                // The constructor initializer (`: base(...)`) stays: it is outside the body.
                return StripMethodBody(node, base.VisitConstructorDeclaration(node));
            }

            public override SyntaxNode VisitDestructorDeclaration(DestructorDeclarationSyntax node)
            {
                return StripMethodBody(node, base.VisitDestructorDeclaration(node));
            }

            public override SyntaxNode VisitOperatorDeclaration(OperatorDeclarationSyntax node)
            {
                return StripMethodBody(node, base.VisitOperatorDeclaration(node));
            }

            public override SyntaxNode VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
            {
                return StripMethodBody(node, base.VisitConversionOperatorDeclaration(node));
            }

            public override SyntaxNode VisitAccessorDeclaration(AccessorDeclarationSyntax node)
            {
                AccessorDeclarationSyntax accessor = (AccessorDeclarationSyntax)base.VisitAccessorDeclaration(node);
                if (node.Body != null)
                {
                    accessor = accessor.WithBody(EmptyBody(node.Body));
                }

                if (node.ExpressionBody != null)
                {
                    accessor = accessor.WithExpressionBody(EmptyExpressionBody(node.ExpressionBody));
                }

                return accessor;
            }

            public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax node)
            {
                PropertyDeclarationSyntax property = (PropertyDeclarationSyntax)base.VisitPropertyDeclaration(node);
                if (node.ExpressionBody != null)
                {
                    property = property.WithExpressionBody(EmptyExpressionBody(node.ExpressionBody));
                }

                if (node.Initializer != null)
                {
                    property = property.WithInitializer(EmptyInitializer(node.Initializer));
                }

                return property;
            }

            public override SyntaxNode VisitIndexerDeclaration(IndexerDeclarationSyntax node)
            {
                IndexerDeclarationSyntax indexer = (IndexerDeclarationSyntax)base.VisitIndexerDeclaration(node);
                if (node.ExpressionBody != null)
                {
                    indexer = indexer.WithExpressionBody(EmptyExpressionBody(node.ExpressionBody));
                }

                return indexer;
            }

            public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
            {
                // A constant's value is part of its declaration: other files can fold it.
                if (node.Modifiers.Any(SyntaxKind.ConstKeyword))
                {
                    return node;
                }

                return node.ReplaceNodes(
                    node.Declaration.Variables.Where(variable => variable.Initializer != null).Select(variable => variable.Initializer),
                    (original, rewritten) => EmptyInitializer(original));
            }

            public override SyntaxNode VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
            {
                return node;
            }

            private static SyntaxNode StripMethodBody(BaseMethodDeclarationSyntax original, SyntaxNode visited)
            {
                // Local functions and lambdas live inside the body, so replacing the body covers them.
                BaseMethodDeclarationSyntax method = (BaseMethodDeclarationSyntax)visited;
                if (original.Body != null)
                {
                    method = method.WithBody(EmptyBody(original.Body));
                }

                if (original.ExpressionBody != null)
                {
                    method = method.WithExpressionBody(EmptyExpressionBody(original.ExpressionBody));
                }

                return method;
            }

            private static BlockSyntax EmptyBody(BlockSyntax original)
            {
                SyntaxToken openBrace = SyntaxFactory.Token(SyntaxKind.OpenBraceToken)
                    .WithTrailingTrivia(NullableDirectivesIn(original));
                return SyntaxFactory.Block().WithOpenBraceToken(openBrace);
            }

            private static ArrowExpressionClauseSyntax EmptyExpressionBody(ArrowExpressionClauseSyntax original)
            {
                return original.WithExpression(Placeholder(original));
            }

            private static EqualsValueClauseSyntax EmptyInitializer(EqualsValueClauseSyntax original)
            {
                return SyntaxFactory.EqualsValueClause(Placeholder(original));
            }

            private static IdentifierNameSyntax Placeholder(SyntaxNode original)
            {
                SyntaxToken identifier = SyntaxFactory.Identifier(
                    SyntaxFactory.TriviaList(),
                    "_",
                    NullableDirectivesIn(original));
                return SyntaxFactory.IdentifierName(identifier);
            }

            private static SyntaxTriviaList NullableDirectivesIn(SyntaxNode original)
            {
                // A #nullable inside a body still changes the nullable context of the declarations
                // after it, so it stays in the fingerprint at the position of the body it was in.
                IEnumerable<SyntaxTrivia> directives = original
                    .DescendantTrivia(descendIntoTrivia: true)
                    .Where(trivia => trivia.IsKind(SyntaxKind.NullableDirectiveTrivia));
                return SyntaxFactory.TriviaList(directives);
            }
        }
    }
}
