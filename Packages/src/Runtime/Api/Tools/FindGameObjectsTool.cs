using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace io.github.hatayama.uLoopMCP
{
    public sealed class FindGameObjectsSchema : BaseToolSchema
    {
        public string Name { get; set; }
        public string Tag { get; set; }
        public string ComponentType { get; set; }
        public bool ActiveOnly { get; set; } = true;
    }

    public sealed class GameObjectInfo
    {
        public int ObjectId { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string Tag { get; set; }
        public bool ActiveSelf { get; set; }
        public bool ActiveInHierarchy { get; set; }
        public string[] Components { get; set; }
    }

    public sealed class FindGameObjectsResponse : BaseToolResponse
    {
        public GameObjectInfo[] Objects { get; set; }
        public int Count { get; set; }
    }

    public sealed class FindGameObjectsTool : AbstractDeviceTool<FindGameObjectsSchema, FindGameObjectsResponse>
    {
        public override string ToolName => "find-game-objects";

        // Session-scoped object ID mapping
        private readonly Dictionary<int, GameObject> _objectIdMap;
        private int _nextObjectId;

        public FindGameObjectsTool(Dictionary<int, GameObject> objectIdMap)
        {
            Debug.Assert(objectIdMap != null, "objectIdMap must not be null");
            _objectIdMap = objectIdMap;
        }

        protected override Task<FindGameObjectsResponse> ExecuteAsync(FindGameObjectsSchema parameters, CancellationToken ct)
        {
            List<GameObjectInfo> results = new();

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    CollectMatching(root, parameters, results);
                }
            }

            FindGameObjectsResponse response = new()
            {
                Objects = results.ToArray(),
                Count = results.Count
            };
            return Task.FromResult(response);
        }

        private void CollectMatching(GameObject go, FindGameObjectsSchema filter, List<GameObjectInfo> results)
        {
            if (IsMatch(go, filter))
            {
                results.Add(CreateInfo(go));
            }

            for (int i = 0; i < go.transform.childCount; i++)
            {
                CollectMatching(go.transform.GetChild(i).gameObject, filter, results);
            }
        }

        private static bool IsMatch(GameObject go, FindGameObjectsSchema filter)
        {
            if (filter.ActiveOnly && !go.activeInHierarchy) return false;

            if (!string.IsNullOrEmpty(filter.Name) && !go.name.Contains(filter.Name)) return false;

            if (!string.IsNullOrEmpty(filter.Tag))
            {
                if (!go.CompareTag(filter.Tag)) return false;
            }

            if (!string.IsNullOrEmpty(filter.ComponentType))
            {
                Component comp = go.GetComponent(filter.ComponentType);
                if (comp == null) return false;
            }

            return true;
        }

        private GameObjectInfo CreateInfo(GameObject go)
        {
            int objectId = _nextObjectId++;
            _objectIdMap[objectId] = go;

            Component[] components = go.GetComponents<Component>();
            string[] componentNames = new string[components.Length];
            for (int i = 0; i < components.Length; i++)
            {
                componentNames[i] = components[i] != null ? components[i].GetType().Name : "null";
            }

            return new GameObjectInfo
            {
                ObjectId = objectId,
                Name = go.name,
                Path = GetHierarchyPath(go),
                Tag = go.tag,
                ActiveSelf = go.activeSelf,
                ActiveInHierarchy = go.activeInHierarchy,
                Components = componentNames
            };
        }

        private static string GetHierarchyPath(GameObject go)
        {
            string path = go.name;
            Transform parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
