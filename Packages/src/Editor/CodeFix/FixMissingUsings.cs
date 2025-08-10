using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// 不足しているusing文の動的追加を行うFixProvider
    /// Unity AI AssistantのFixMissingImports.cs実装パターンに完全準拠
    /// 関連クラス: CSharpFixProvider, SyntaxTreeUtility
    /// </summary>
    public class FixMissingUsings : CSharpFixProvider
    {
        private readonly SecurityPolicy _securityPolicy;

        public FixMissingUsings()
        {
            _securityPolicy = SecurityPolicy.GetDefault();
        }

        private static readonly string[] DiagnosticIds = { "CS0246", "CS1061", "CS0103" };

        private readonly Dictionary<string, string[]> NamespaceKeywords = new()
        {
            { "System.Linq", new[] { "Where", "Select", "OrderBy", "Concat", "Any", "First", "Last", "ToList", "ToArray", "Take", "Skip", "Count", "Sum", "GroupBy", "Distinct" } },
            { "System.Collections.Generic", new[] { "List<>", "Dictionary<>", "HashSet<>", "Queue<>", "Stack<>" } },
            { "UnityEngine", new[] { "GameObject", "Transform", "Component", "MonoBehaviour", "ScriptableObject", "Vector3", "Quaternion", "Color", "Debug" } },
            { "UnityEditor", new[] { "EditorWindow", "EditorGUI", "AssetDatabase", "Selection", "EditorUtility", "EditorApplication", "Editor" } },
            { "UnityEngine.UI", new[] { "Button", "Text", "Image", "Canvas", "Toggle" } },
            { "System.IO", new[] { "File", "Directory", "Path", "StreamReader", "StreamWriter" } },
            { "System.Threading.Tasks", new[] { "Task", "async", "await" } }
        };

        // 間違った名前空間の修正
        private readonly Dictionary<string, string> WrongNamespaces = new()
        {
            { "Unity.Cinemachine", "Cinemachine" },
            { "UnityEngine.UIElements", "UnityEngine.UI" }
        };

        public override bool CanFix(Diagnostic diagnostic)
        {
            if (!DiagnosticIds.Contains(diagnostic.Id))
                return false;

            string message = diagnostic.GetMessage();
            
            VibeLogger.LogInfo(
                "fix_missing_usings_can_fix_check",
                $"Checking if diagnostic can be fixed: {diagnostic.Id}",
                new { 
                    diagnosticId = diagnostic.Id,
                    message = message
                },
                null,
                "FixMissingUsings診断チェック",
                "マッチングロジックの確認"
            );
            
            // Unity AI Assistant方式: 診断メッセージに 'keyword' の形で含まれるかチェック
            foreach (KeyValuePair<string, string[]> kvp in NamespaceKeywords)
            {
                foreach (string keyword in kvp.Value)
                {
                    if (message.Contains($"'{keyword}'") || message.Contains($"'{keyword} "))
                    {
                        VibeLogger.LogInfo(
                            "fix_missing_usings_match_found",
                            $"Found match for keyword '{keyword}' in namespace '{kvp.Key}'",
                            new { 
                                keyword = keyword,
                                namespaceToAdd = kvp.Key,
                                diagnosticId = diagnostic.Id,
                                message = message
                            },
                            null,
                            "FixMissingUsingsマッチング成功",
                            "修正対象の特定"
                        );
                        return true;
                    }
                }
            }

            // 間違った名前空間の判定
            foreach (KeyValuePair<string, string> wrongNamespace in WrongNamespaces)
            {
                if (message.Contains($"'{wrongNamespace.Value}'"))
                    return true;
            }

            return false;
        }

        public override SyntaxTree ApplyFix(SyntaxTree tree, Diagnostic diagnostic)
        {
            string message = diagnostic.GetMessage();
            
            // Unity AI Assistant方式: 診断メッセージベースでusing文追加
            foreach (KeyValuePair<string, string[]> namespaceKeywords in NamespaceKeywords)
            {
                string[] keywords = namespaceKeywords.Value;
                foreach (string keyword in keywords)
                {
                    if (message.Contains($"'{keyword}'") || message.Contains($"'{keyword} "))
                    {
                        string namespaceToAdd = namespaceKeywords.Key;
                        
                        // セキュリティチェック: 禁止された名前空間は追加しない
                        if (IsNamespaceForbidden(namespaceToAdd))
                        {
                            VibeLogger.LogWarning(
                                "using_statement_blocked_by_security",
                                $"Using statement for namespace '{namespaceToAdd}' blocked by security policy",
                                new { 
                                    namespaceToAdd,
                                    keyword,
                                    diagnosticId = diagnostic.Id,
                                    diagnosticMessage = message
                                },
                                null,
                                "Security policy prevented adding forbidden using statement",
                                "Track security policy enforcement in code fixes"
                            );
                            return tree; // 変更せずに返す
                        }
                        
                        return tree.AddUsingDirective(namespaceToAdd);
                    }
                }
            }

            // 間違った名前空間を修正
            foreach (KeyValuePair<string, string> wrongNamespace in WrongNamespaces)
            {
                if (message.Contains($"'{wrongNamespace.Value}'"))
                {
                    string namespaceToAdd = wrongNamespace.Key;
                    
                    // セキュリティチェック: 禁止された名前空間は追加しない
                    if (IsNamespaceForbidden(namespaceToAdd))
                    {
                        VibeLogger.LogWarning(
                            "using_statement_blocked_by_security",
                            $"Using statement for namespace '{namespaceToAdd}' blocked by security policy",
                            new { 
                                namespaceToAdd,
                                wrongNamespace = wrongNamespace.Value,
                                diagnosticId = diagnostic.Id,
                                diagnosticMessage = message
                            },
                            null,
                            "Security policy prevented adding forbidden using statement",
                            "Track security policy enforcement in code fixes"
                        );
                        return tree; // 変更せずに返す
                    }
                    
                    SyntaxTree cleanTree = tree.RemoveUsingDirective(wrongNamespace.Value);
                    return cleanTree.AddUsingDirective(namespaceToAdd);
                }
            }

            return tree;
        }

        /// <summary>
        /// 指定された名前空間が禁止されているかをチェック
        /// </summary>
        private bool IsNamespaceForbidden(string namespaceName)
        {
            // 完全一致チェック
            if (_securityPolicy.ForbiddenNamespaces.Contains(namespaceName))
            {
                return true;
            }
            
            // 部分一致チェック（System.Net は System.Net.Http もブロック）
            foreach (string forbiddenNamespace in _securityPolicy.ForbiddenNamespaces)
            {
                if (namespaceName.StartsWith(forbiddenNamespace + "."))
                {
                    return true;
                }
            }
            
            return false;
        }
    }
}