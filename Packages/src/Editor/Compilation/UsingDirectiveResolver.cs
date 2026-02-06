#if ULOOPMCP_HAS_ROSLYN
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Resolves missing using directives by searching the compilation's GlobalNamespace
    /// for types matching unresolved identifiers from CS0103/CS0246 diagnostics.
    /// Related classes: RoslynCompiler (consumer via ApplyDiagnosticFixes)
    /// </summary>
    public class UsingDirectiveResolver
    {
        private static readonly Regex TypeNamePattern = new Regex(@"'([^']+)'", RegexOptions.Compiled);

        private readonly Dictionary<string, List<string>> _typeNameToNamespacesCache = new();

        public void ClearCache()
        {
            _typeNameToNamespacesCache.Clear();
        }

        /// <summary>
        /// Resolve unresolved types from CS0103/CS0246 diagnostics by searching the compilation's symbol table.
        /// </summary>
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
                Debug.Assert(diagnostic.Id == "CS0103" || diagnostic.Id == "CS0246",
                    $"Expected CS0103 or CS0246 but got {diagnostic.Id}");

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

        /// <summary>
        /// Extract the unresolved type name from a CS0103/CS0246 diagnostic message.
        /// CS0103: "The name 'Mathf' does not exist in the current context"
        /// CS0246: "The type or namespace name 'Vector3' could not be found ..."
        /// </summary>
        public string ExtractTypeNameFromDiagnostic(Diagnostic diagnostic)
        {
            Debug.Assert(diagnostic != null, "diagnostic must not be null");

            string message = diagnostic.GetMessage();
            Match match = TypeNamePattern.Match(message);
            if (!match.Success) return null;

            string rawName = match.Groups[1].Value;

            // Strip generic arity suffix (e.g. "List<>" -> "List")
            int genericIndex = rawName.IndexOf('<');
            if (genericIndex > 0)
            {
                rawName = rawName.Substring(0, genericIndex);
            }

            return rawName;
        }

        /// <summary>
        /// Search the compilation's GlobalNamespace recursively for all namespaces containing a type with the given name.
        /// Results are cached per instance to avoid repeated walks for the same type name.
        /// </summary>
        public List<string> FindNamespacesForType(CSharpCompilation compilation, string typeName)
        {
            Debug.Assert(compilation != null, "compilation must not be null");
            Debug.Assert(!string.IsNullOrEmpty(typeName), "typeName must not be null or empty");

            if (_typeNameToNamespacesCache.TryGetValue(typeName, out List<string> cached))
            {
                return cached;
            }

            List<string> results = new();
            SearchNamespace(compilation.GlobalNamespace, typeName, results);

            List<string> distinctResults = results.Distinct().ToList();
            _typeNameToNamespacesCache[typeName] = distinctResults;
            return distinctResults;
        }

        private void SearchNamespace(
            INamespaceSymbol namespaceSymbol,
            string typeName,
            List<string> results)
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

    /// <summary>
    /// Result of attempting to resolve a single unresolved type name to a using directive.
    /// </summary>
    public class UsingResolutionResult
    {
        public string TypeName { get; set; }
        public List<string> CandidateNamespaces { get; set; } = new();
        public bool IsUnique => CandidateNamespaces.Count == 1;
        public bool HasCandidates => CandidateNamespaces.Count > 0;
    }
}
#endif
