using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// SyntaxTree操作の拡張メソッドユーティリティ
    /// Unity AI AssistantのSyntaxTreeUtility.csに完全準拠
    /// 関連クラス: FixMissingUsings, CSharpFixProvider
    /// </summary>
    internal static class SyntaxTreeUtility
    {
        internal static SyntaxTree AddUsingDirective(this SyntaxTree syntaxTree, string namespaceToAdd)
        {
            CompilationUnitSyntax root = syntaxTree.GetRoot() as CompilationUnitSyntax;
            if (root == null) return syntaxTree;

            // 既存チェック
            bool usingExists = root.Usings.Any(u => u.Name?.ToString() == namespaceToAdd);
            if (usingExists)
                return syntaxTree;

            // using文作成
            UsingDirectiveSyntax usingDirective = SyntaxFactory
                .UsingDirective(SyntaxFactory.ParseName(namespaceToAdd))
                .NormalizeWhitespace()
                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

            CompilationUnitSyntax newRoot = root.AddUsings(usingDirective).NormalizeWhitespace();
            
            // Unity AI Assistant方式：CSharpSyntaxTree.Create使用
            return CSharpSyntaxTree.Create(newRoot);
        }

        internal static SyntaxTree RemoveUsingDirective(this SyntaxTree syntaxTree, string namespaceToRemove)
        {
            CompilationUnitSyntax root = syntaxTree.GetRoot() as CompilationUnitSyntax;
            if (root == null) return syntaxTree;

            UsingDirectiveSyntax usingToRemove = root.Usings
                .FirstOrDefault(u => u.Name?.ToString() == namespaceToRemove);
            
            if (usingToRemove != null)
            {
                CompilationUnitSyntax newRoot = root.RemoveNode(
                    usingToRemove, 
                    SyntaxRemoveOptions.KeepNoTrivia);
                return CSharpSyntaxTree.Create(newRoot.NormalizeWhitespace());
            }

            return syntaxTree;
        }
    }
}