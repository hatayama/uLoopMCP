#if ULOOPMCP_HAS_ROSLYN
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace io.github.hatayama.uLoopMCP
{
    public class UsingDirectiveResolver
    {
        private static readonly Regex TypeNamePattern = new Regex(@"['""]([^'""]+)['""]", RegexOptions.Compiled);

        private readonly Dictionary<string, List<string>> _typeNameToNamespacesCache = new();

        public List<UsingResolutionResult> ResolveUnresolvedTypes(
            CSharpCompilation compilation,
            IEnumerable<Diagnostic> diagnostics)
        {
            Debug.Assert(compilation != null, "compilation must not be null");
            Debug.Assert(diagnostics != null, "diagnostics must not be null");

            List<UsingResolutionResult> results = new();
            HashSet<string> processedTypeNames = new();

            foreach (Diagnostic diagnostic in diagnostics)
            {
                Debug.Assert(diagnostic.Id == "CS0246",
                    $"Expected CS0246 but got {diagnostic.Id}");

                string typeName = ExtractTypeNameFromDiagnostic(diagnostic);
                if (string.IsNullOrEmpty(typeName)) continue;
                if (!processedTypeNames.Add(typeName)) continue;

                List<string> candidateNamespaces = FindNamespacesForType(compilation, typeName);

                results.Add(new UsingResolutionResult
                {
                    TypeName = typeName,
                    CandidateNamespaces = candidateNamespaces
                });
            }

            return results;
        }

        // Prefers syntax-level extraction over message parsing to avoid locale/format dependency
        public string ExtractTypeNameFromDiagnostic(Diagnostic diagnostic)
        {
            Debug.Assert(diagnostic != null, "diagnostic must not be null");

            string typeNameFromLocation = ExtractTypeNameFromLocation(diagnostic);
            if (!string.IsNullOrEmpty(typeNameFromLocation))
            {
                return typeNameFromLocation;
            }

            string message = diagnostic.GetMessage();
            Match match = TypeNamePattern.Match(message);
            if (!match.Success) return null;

            return NormalizeTypeName(match.Groups[1].Value);
        }

        private string ExtractTypeNameFromLocation(Diagnostic diagnostic)
        {
            if (!diagnostic.Location.IsInSource) return null;
            if (diagnostic.Location.SourceTree == null) return null;

            SyntaxNode root = diagnostic.Location.SourceTree.GetRoot();
            SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            if (node == null) return null;

            if (node is QualifiedNameSyntax qualifiedName)
            {
                node = qualifiedName.Right;
            }

            if (node is IdentifierNameSyntax identifierName)
            {
                return NormalizeTypeName(identifierName.Identifier.ValueText);
            }

            if (node is GenericNameSyntax genericName)
            {
                return NormalizeTypeName(genericName.Identifier.ValueText);
            }

            SyntaxToken token = root.FindToken(diagnostic.Location.SourceSpan.Start);
            if (token.IsKind(SyntaxKind.IdentifierToken))
            {
                return NormalizeTypeName(token.ValueText);
            }

            return null;
        }

        public static string ExtractTypeNameFromMessage(string message)
        {
            Match match = TypeNamePattern.Match(message);
            if (!match.Success) return null;

            return NormalizeTypeName(match.Groups[1].Value);
        }

        private static string NormalizeTypeName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return null;
            string normalized = rawName.Trim();

            // Strip generic arity suffix (e.g. "List<>" -> "List")
            int genericIndex = normalized.IndexOf('<');
            if (genericIndex > 0)
            {
                normalized = normalized.Substring(0, genericIndex);
            }

            return normalized;
        }

        public List<string> FindNamespacesForType(CSharpCompilation compilation, string typeName)
        {
            Debug.Assert(compilation != null, "compilation must not be null");
            Debug.Assert(!string.IsNullOrEmpty(typeName), "typeName must not be null or empty");

            if (_typeNameToNamespacesCache.TryGetValue(typeName, out List<string> cached))
            {
                return cached;
            }

            HashSet<string> results = new();
            SearchNamespace(compilation.GlobalNamespace, typeName, results);

            List<string> resultList = results.ToList();
            _typeNameToNamespacesCache[typeName] = resultList;
            return resultList;
        }

        private void SearchNamespace(
            INamespaceSymbol namespaceSymbol,
            string typeName,
            ICollection<string> results)
        {
            // INamespaceSymbol.GetMembers(name) is a hash-based O(1) lookup
            foreach (ISymbol member in namespaceSymbol.GetMembers(typeName))
            {
                if (member is INamedTypeSymbol typeSymbol && typeSymbol.DeclaredAccessibility == Accessibility.Public)
                {
                    string namespaceName = typeSymbol.ContainingNamespace?.ToDisplayString();
                    if (!string.IsNullOrEmpty(namespaceName) && namespaceName != "<global namespace>")
                    {
                        results.Add(namespaceName);
                    }
                }
            }

            foreach (INamespaceSymbol nested in namespaceSymbol.GetNamespaceMembers())
            {
                SearchNamespace(nested, typeName, results);
            }
        }
    }

    public class UsingResolutionResult
    {
        public string TypeName { get; set; }
        public List<string> CandidateNamespaces { get; set; } = new();
        public bool IsUnique => CandidateNamespaces.Count == 1;
    }
}
#endif
