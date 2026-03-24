using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor.Compilation;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Resolves missing using directives by retrying compilation with candidate namespaces.
    /// Parses CS0246/CS0103 from CompilerMessage and looks up types via AssemblyTypeIndex.
    /// </summary>
    internal sealed class AutoUsingResolver
    {
        private const int MaxRetries = 3;

        public async Task<AutoUsingResult> ResolveAsync(
            string sourcePath,
            string dllPath,
            string originalSource,
            List<string> additionalReferences,
            System.Func<string, string, List<string>, CancellationToken, Task<CompilerMessage[]>> buildFunc,
            CancellationToken ct)
        {
            Debug.Assert(originalSource != null, "originalSource must not be null");

            string currentSource = originalSource;
            Dictionary<string, List<string>> ambiguousCandidates = new();
            HashSet<string> addedNamespaces = new(System.StringComparer.Ordinal);

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                File.WriteAllText(sourcePath, currentSource);
                // AssemblyBuilder retries must stay on Unity's main thread because the compiler API is not thread-safe.
                CompilerMessage[] messages = await buildFunc(sourcePath, dllPath, additionalReferences, ct);

                List<string> unresolvedTypes = ExtractUnresolvedTypes(messages);
                if (unresolvedTypes.Count == 0)
                {
                    return new AutoUsingResult(currentSource, messages, ambiguousCandidates);
                }

                HashSet<string> namespacesToAdd = new(System.StringComparer.Ordinal);
                AssemblyTypeIndex index = AssemblyTypeIndex.Instance;

                foreach (string typeName in unresolvedTypes)
                {
                    if (string.IsNullOrEmpty(typeName)) continue;

                    List<string> candidates = index.FindNamespacesForType(typeName);

                    if (candidates.Count == 1 && addedNamespaces.Add(candidates[0]))
                    {
                        namespacesToAdd.Add(candidates[0]);
                    }
                    else if (candidates.Count > 1)
                    {
                        ambiguousCandidates[typeName] = candidates;
                    }
                }

                if (namespacesToAdd.Count == 0) break;

                currentSource = InsertUsingDirectives(currentSource, namespacesToAdd);
            }

            // Final compilation with all added usings
            File.WriteAllText(sourcePath, currentSource);
            CompilerMessage[] finalMessages = await buildFunc(sourcePath, dllPath, additionalReferences, ct);
            return new AutoUsingResult(currentSource, finalMessages, ambiguousCandidates);
        }

        private static List<string> ExtractUnresolvedTypes(CompilerMessage[] messages)
        {
            List<string> types = new();
            foreach (CompilerMessage msg in messages)
            {
                if (msg.type != CompilerMessageType.Error) continue;

                // CS0246: The type or namespace name 'X' could not be found
                // CS0103: The name 'X' does not exist in the current context
                if (!msg.message.Contains("CS0246") && !msg.message.Contains("CS0103")) continue;

                string typeName = CompilationDiagnosticMessageParser.ExtractTypeNameFromMessage(msg.message);
                if (!string.IsNullOrEmpty(typeName))
                {
                    types.Add(typeName);
                }
            }
            return types;
        }

        private static string InsertUsingDirectives(string source, IEnumerable<string> namespaces)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string ns in namespaces)
            {
                sb.AppendLine($"using {ns};");
            }
            sb.Append(source);
            return sb.ToString();
        }
    }

    internal sealed class AutoUsingResult
    {
        public string UpdatedSource { get; }
        public CompilerMessage[] Messages { get; }
        public Dictionary<string, List<string>> AmbiguousTypeCandidates { get; }

        public AutoUsingResult(
            string updatedSource,
            CompilerMessage[] messages,
            Dictionary<string, List<string>> ambiguousTypeCandidates)
        {
            UpdatedSource = updatedSource;
            Messages = messages;
            AmbiguousTypeCandidates = ambiguousTypeCandidates;
        }
    }
}
