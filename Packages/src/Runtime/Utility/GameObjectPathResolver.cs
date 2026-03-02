using UnityEngine;
using UnityEngine.SceneManagement;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Resolves GameObjects by hierarchy path strings with [n] sibling index support.
    /// Shared across tools that need path-based object lookup (TapObjectTool, InputTextTool, etc.).
    /// </summary>
    public static class GameObjectPathResolver
    {
        public static GameObject FindByPath(string path)
        {
            string[] parts = path.Split('/');
            if (parts.Length == 0) return null;

            ParsePathSegment(parts[0], out string rootName, out int rootIndex);
            GameObject current = FindRootByName(rootName, rootIndex);
            if (current == null) return null;

            for (int i = 1; i < parts.Length; i++)
            {
                ParsePathSegment(parts[i], out string childName, out int childIndex);
                current = FindChild(current.transform, childName, childIndex);
                if (current == null) return null;
            }

            return current;
        }

        private static void ParsePathSegment(string segment, out string name, out int siblingIndex)
        {
            siblingIndex = 0;
            int bracketStart = segment.IndexOf('[');
            if (bracketStart >= 0 && segment.EndsWith("]"))
            {
                name = segment.Substring(0, bracketStart);
                string indexStr = segment.Substring(bracketStart + 1, segment.Length - bracketStart - 2);
                int.TryParse(indexStr, out siblingIndex);
            }
            else
            {
                name = segment;
            }
        }

        private static GameObject FindRootByName(string name, int nthMatch)
        {
            int matchCount = 0;
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name == name)
                    {
                        if (matchCount == nthMatch) return root;
                        matchCount++;
                    }
                }
            }
            return null;
        }

        private static GameObject FindChild(Transform parent, string name, int nthMatch)
        {
            int matchCount = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                {
                    if (matchCount == nthMatch) return child.gameObject;
                    matchCount++;
                }
            }
            return null;
        }
    }
}
