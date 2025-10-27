using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Service for retrieving Unity Hierarchy information
    /// Reusable logic separated from command implementation
    /// </summary>
    public class HierarchyService
    {
        /// <summary>
        /// Get all hierarchy nodes based on options
        /// </summary>
        public List<HierarchyNode> GetHierarchyNodes(HierarchyOptions options)
        {
            List<HierarchyNode> nodes = new List<HierarchyNode>();
            GameObject[] rootObjects = GetRootGameObjects(options.RootPath);
            
            foreach (GameObject root in rootObjects)
            {
                if (!options.IncludeInactive && !root.activeInHierarchy)
                    continue;
                    
                TraverseHierarchy(root, null, 0, options, nodes);
            }
            
            return nodes;
        }
        
        /// <summary>
        /// Get current scene context information
        /// </summary>
        public HierarchyContext GetCurrentContext()
        {
            string sceneType = "editor";
            string sceneName = "Unknown";
            
            // Check if in Prefab Edit Mode
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                sceneType = "prefab";
                sceneName = prefabStage.assetPath;
            }
            else if (Application.isPlaying)
            {
                sceneType = "runtime";
                sceneName = BuildSceneNameSummary();
            }
            else
            {
                sceneType = "editor";
                sceneName = BuildSceneNameSummary();
            }
            
            return new HierarchyContext(sceneType, sceneName, 0, 0);
        }

        private string BuildSceneNameSummary()
        {
            int count = SceneManager.sceneCount;
            if (count <= 0)
            {
                return string.Empty;
            }

            System.Collections.Generic.List<string> names = new System.Collections.Generic.List<string>();
            for (int i = 0; i < count; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.isLoaded)
                {
                    names.Add(s.name);
                }
            }

            if (names.Count <= 1)
            {
                return names.Count == 1 ? names[0] : string.Empty;
            }

            string joined = string.Join(", ", names.ToArray());
            return $"Multiple({names.Count}): {joined}";
        }
        
        private GameObject[] GetRootGameObjects(string rootPath)
        {
            // Check if in Prefab Edit Mode
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                GameObject prefabRoot = prefabStage.prefabContentsRoot;
                if (!string.IsNullOrEmpty(rootPath))
                {
                    if (prefabRoot.name == rootPath)
                    {
                        return new[] { prefabRoot };
                    }

                    string localPath = NormalizeRootRelativePath(rootPath, prefabRoot.name);
                    Transform found = string.IsNullOrEmpty(localPath)
                        ? prefabRoot.transform
                        : prefabRoot.transform.Find(localPath);
                    if (found != null)
                    {
                        return new[] { found.gameObject };
                    }

                    return System.Array.Empty<GameObject>();
                }
                return new[] { prefabRoot };
            }
            
            // Normal scene mode: iterate all loaded scenes (additive included)
            List<GameObject> results = new List<GameObject>();

            int sceneCount = SceneManager.sceneCount;
            if (!string.IsNullOrEmpty(rootPath))
            {
                for (int i = 0; i < sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded)
                    {
                        continue;
                    }

                    GameObject[] roots = scene.GetRootGameObjects();
                    foreach (GameObject root in roots)
                    {
                        if (root.name == rootPath)
                        {
                            results.Add(root);
                            continue;
                        }

                        string localPath = NormalizeRootRelativePath(rootPath, root.name);
                        Transform found = string.IsNullOrEmpty(localPath)
                            ? root.transform
                            : root.transform.Find(localPath);
                        if (found != null)
                        {
                            results.Add(found.gameObject);
                        }
                    }
                }
                return results.ToArray();
            }

            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                results.AddRange(roots);
            }

            return results.ToArray();
        }

        private static string NormalizeRootRelativePath(string rootPath, string rootName)
        {
            if (string.IsNullOrEmpty(rootPath))
            {
                return string.Empty;
            }

            string trimmed = rootPath.TrimStart('/');
            if (string.IsNullOrEmpty(trimmed))
            {
                return string.Empty;
            }

            if (trimmed.StartsWith(rootName + "/"))
            {
                return trimmed.Substring(rootName.Length + 1);
            }

            if (trimmed == rootName)
            {
                return string.Empty;
            }

            return trimmed;
        }
        
        private void TraverseHierarchy(GameObject obj, int? parentId, int depth, HierarchyOptions options, List<HierarchyNode> nodes)
        {
            // Check depth limit
            if (options.MaxDepth >= 0 && depth > options.MaxDepth)
                return;
                
            // Get components
            string[] componentNames = new string[0];
            if (options.IncludeComponents)
            {
                Component[] components = obj.GetComponents<Component>();
                componentNames = components
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name)
                    .ToArray();
            }
            
            // Create node
            HierarchyNode node = new HierarchyNode(
                id: obj.GetInstanceID(),
                name: obj.name,
                parent: parentId,
                depth: depth,
                isActive: obj.activeSelf,
                components: componentNames,
                sceneName: obj.scene.name,
                siblingIndex: obj.transform.GetSiblingIndex(),
                tag: obj.tag,
                layer: obj.layer
            );
            
            nodes.Add(node);
            
            // Traverse children
            int currentId = obj.GetInstanceID();
            foreach (Transform child in obj.transform)
            {
                if (!options.IncludeInactive && !child.gameObject.activeInHierarchy)
                    continue;
                    
                TraverseHierarchy(child.gameObject, currentId, depth + 1, options, nodes);
            }
        }
    }
}