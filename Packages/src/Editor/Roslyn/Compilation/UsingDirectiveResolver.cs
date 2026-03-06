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
        private readonly Dictionary<string, List<string>> _typeNameMemberToNamespacesCache = new();

        public List<UsingResolutionResult> ResolveMissingSymbols(
            CSharpCompilation compilation,
            IEnumerable<Diagnostic> diagnostics)
        {
            Debug.Assert(compilation != null, "compilation must not be null");
            Debug.Assert(diagnostics != null, "diagnostics must not be null");

            List<Diagnostic> diagnosticList = diagnostics.ToList();
            List<UsingResolutionResult> results = new();
            HashSet<string> processedKeys = new();
            HashSet<string> unresolvedTypes = CollectUnresolvedTypes(diagnosticList);

            foreach (Diagnostic diagnostic in diagnosticList)
            {
                if (diagnostic.Id == "CS0246")
                {
                    string typeName = ExtractTypeNameFromDiagnostic(diagnostic);
                    if (string.IsNullOrEmpty(typeName))
                    {
                        continue;
                    }

                    string dedupKey = $"CS0246:{typeName}";
                    if (!processedKeys.Add(dedupKey))
                    {
                        continue;
                    }

                    List<string> candidateNamespaces = FindNamespacesForType(compilation, typeName);
                    results.Add(new UsingResolutionResult
                    {
                        TypeName = typeName,
                        CandidateNamespaces = candidateNamespaces
                    });
                    continue;
                }

                if (diagnostic.Id == "CS0103")
                {
                    if (!TryExtractCs0103TypeMemberCandidate(
                        compilation,
                        diagnostic,
                        out string typeName,
                        out string memberName))
                    {
                        continue;
                    }

                    if (unresolvedTypes.Contains(typeName))
                    {
                        continue;
                    }

                    string dedupKey = $"CS0103:{typeName}.{memberName}";
                    if (!processedKeys.Add(dedupKey))
                    {
                        continue;
                    }

                    List<string> candidateNamespaces = FindNamespacesForTypeWithStaticMember(
                        compilation,
                        typeName,
                        memberName);

                    results.Add(new UsingResolutionResult
                    {
                        TypeName = typeName,
                        CandidateNamespaces = candidateNamespaces
                    });
                }
            }

            return results;
        }

        private HashSet<string> CollectUnresolvedTypes(IEnumerable<Diagnostic> diagnostics)
        {
            HashSet<string> unresolvedTypes = new();

            foreach (Diagnostic diagnostic in diagnostics)
            {
                if (diagnostic.Id != "CS0246")
                {
                    continue;
                }

                string typeName = ExtractTypeNameFromDiagnostic(diagnostic);
                if (string.IsNullOrEmpty(typeName))
                {
                    continue;
                }

                unresolvedTypes.Add(typeName);
            }

            return unresolvedTypes;
        }

        public List<UsingResolutionResult> ResolveUnresolvedTypes(
            CSharpCompilation compilation,
            IEnumerable<Diagnostic> diagnostics)
        {
            return ResolveMissingSymbols(compilation, diagnostics);
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

        public List<string> FindNamespacesForTypeWithStaticMember(
            CSharpCompilation compilation,
            string typeName,
            string memberName)
        {
            Debug.Assert(compilation != null, "compilation must not be null");
            Debug.Assert(!string.IsNullOrEmpty(typeName), "typeName must not be null or empty");
            Debug.Assert(!string.IsNullOrEmpty(memberName), "memberName must not be null or empty");

            string cacheKey = $"{typeName}.{memberName}";
            if (_typeNameMemberToNamespacesCache.TryGetValue(cacheKey, out List<string> cached))
            {
                return cached;
            }

            HashSet<string> results = new();
            SearchNamespaceForTypeWithStaticMember(
                compilation.GlobalNamespace,
                typeName,
                memberName,
                results);

            List<string> resultList = results.ToList();
            _typeNameMemberToNamespacesCache[cacheKey] = resultList;
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

        private bool TryExtractCs0103TypeMemberCandidate(
            CSharpCompilation compilation,
            Diagnostic diagnostic,
            out string typeName,
            out string memberName)
        {
            Debug.Assert(compilation != null, "compilation must not be null");
            Debug.Assert(diagnostic != null, "diagnostic must not be null");

            typeName = null;
            memberName = null;

            if (!diagnostic.Location.IsInSource)
            {
                return false;
            }

            if (diagnostic.Location.SourceTree == null)
            {
                return false;
            }

            SyntaxNode root = diagnostic.Location.SourceTree.GetRoot();
            SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            if (node == null)
            {
                return false;
            }

            IdentifierNameSyntax identifierNode = node as IdentifierNameSyntax;
            if (identifierNode == null)
            {
                identifierNode = node.AncestorsAndSelf().OfType<IdentifierNameSyntax>().FirstOrDefault();
            }

            if (identifierNode == null)
            {
                return false;
            }

            MemberAccessExpressionSyntax memberAccess = identifierNode.Parent as MemberAccessExpressionSyntax;
            if (memberAccess == null)
            {
                return false;
            }

            if (memberAccess.Expression != identifierNode)
            {
                return false;
            }

            string extractedTypeName = NormalizeTypeName(identifierNode.Identifier.ValueText);
            if (string.IsNullOrEmpty(extractedTypeName))
            {
                return false;
            }

            if (!IsPascalCaseIdentifier(extractedTypeName))
            {
                return false;
            }

            string extractedMemberName = NormalizeTypeName(memberAccess.Name.Identifier.ValueText);
            if (string.IsNullOrEmpty(extractedMemberName))
            {
                return false;
            }

            SemanticModel semanticModel = compilation.GetSemanticModel(diagnostic.Location.SourceTree);
            int lookupPosition = diagnostic.Location.SourceSpan.Start;
            IEnumerable<ISymbol> visibleSymbols = semanticModel.LookupSymbols(lookupPosition, name: extractedTypeName);
            bool hasValueLikeSymbol = visibleSymbols.Any(IsValueLikeSymbol);
            if (hasValueLikeSymbol)
            {
                return false;
            }

            typeName = extractedTypeName;
            memberName = extractedMemberName;
            return true;
        }

        private void SearchNamespaceForTypeWithStaticMember(
            INamespaceSymbol namespaceSymbol,
            string typeName,
            string memberName,
            ICollection<string> results)
        {
            foreach (ISymbol member in namespaceSymbol.GetMembers(typeName))
            {
                INamedTypeSymbol typeSymbol = member as INamedTypeSymbol;
                if (typeSymbol == null || typeSymbol.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                if (!HasPublicStaticMember(typeSymbol, memberName))
                {
                    continue;
                }

                string namespaceName = typeSymbol.ContainingNamespace?.ToDisplayString();
                if (!string.IsNullOrEmpty(namespaceName) && namespaceName != "<global namespace>")
                {
                    results.Add(namespaceName);
                }
            }

            foreach (INamespaceSymbol nested in namespaceSymbol.GetNamespaceMembers())
            {
                SearchNamespaceForTypeWithStaticMember(nested, typeName, memberName, results);
            }
        }

        private static bool HasPublicStaticMember(INamedTypeSymbol typeSymbol, string memberName)
        {
            IEnumerable<ISymbol> members = typeSymbol.GetMembers(memberName);
            foreach (ISymbol member in members)
            {
                if (!member.IsStatic)
                {
                    continue;
                }

                if (member.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                IMethodSymbol methodSymbol = member as IMethodSymbol;
                if (methodSymbol != null && methodSymbol.MethodKind == MethodKind.StaticConstructor)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool IsPascalCaseIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return false;
            }

            char firstCharacter = identifier[0];
            return char.IsLetter(firstCharacter) && char.IsUpper(firstCharacter);
        }

        private static bool IsValueLikeSymbol(ISymbol symbol)
        {
            if (symbol == null)
            {
                return false;
            }

            return symbol.Kind == SymbolKind.Local
                || symbol.Kind == SymbolKind.Parameter
                || symbol.Kind == SymbolKind.Field
                || symbol.Kind == SymbolKind.Property
                || symbol.Kind == SymbolKind.Method
                || symbol.Kind == SymbolKind.Event
                || symbol.Kind == SymbolKind.RangeVariable
                || symbol.Kind == SymbolKind.Label;
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
