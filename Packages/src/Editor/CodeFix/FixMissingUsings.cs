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
    internal class FixMissingUsings : CSharpFixProvider
    {
        private static readonly string[] DiagnosticIds = { "CS0246", "CS1061" };

        private readonly Dictionary<string, string[]> NamespaceKeywords = new()
        {
            { "System.Linq", new[] { "Where", "Select", "OrderBy", "Concat", "Any", "First", "Last", "ToList", "ToArray" } },
            { "System.Collections.Generic", new[] { "List<", "Dictionary<", "HashSet<", "Queue<", "Stack<" } },
            { "UnityEngine", new[] { "GameObject", "Transform", "Component", "MonoBehaviour", "ScriptableObject", "Vector3", "Quaternion", "Color" } },
            { "UnityEditor", new[] { "EditorWindow", "EditorGUI", "AssetDatabase", "Selection", "EditorUtility", "EditorApplication" } },
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
            
            // キーワードベースの判定
            foreach (string[] keywords in NamespaceKeywords.Values)
            {
                if (keywords.Any(keyword => message.Contains($"'{keyword}'")))
                    return true;
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
            
            // 正しい名前空間を追加
            foreach (KeyValuePair<string, string[]> namespaceKeywords in NamespaceKeywords)
            {
                string[] keywords = namespaceKeywords.Value;
                if (keywords.Any(keyword => message.Contains($"'{keyword}'")))
                {
                    return tree.AddUsingDirective(namespaceKeywords.Key);
                }
            }

            // 間違った名前空間を修正
            foreach (KeyValuePair<string, string> wrongNamespace in WrongNamespaces)
            {
                if (message.Contains($"'{wrongNamespace.Value}'"))
                {
                    SyntaxTree cleanTree = tree.RemoveUsingDirective(wrongNamespace.Value);
                    return cleanTree.AddUsingDirective(wrongNamespace.Key);
                }
            }

            return tree;
        }
    }
}