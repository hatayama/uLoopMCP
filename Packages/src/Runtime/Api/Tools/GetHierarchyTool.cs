using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace io.github.hatayama.uLoopMCP
{
    public sealed class GetHierarchySchema : BaseToolSchema
    {
        public int Depth { get; set; } = 3;
        public bool IncludeComponents { get; set; } = false;
    }

    public sealed class HierarchyNode
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool Active { get; set; }
        public string[] Components { get; set; }
        public HierarchyNode[] Children { get; set; }
    }

    public sealed class GetHierarchyResponse : BaseToolResponse
    {
        public HierarchyNode[] Scenes { get; set; }
    }

    public sealed class GetHierarchyTool : AbstractDeviceTool<GetHierarchySchema, GetHierarchyResponse>
    {
        public override string ToolName => "get-hierarchy";

        protected override Task<GetHierarchyResponse> ExecuteAsync(GetHierarchySchema parameters, CancellationToken ct)
        {
            List<HierarchyNode> sceneNodes = new();

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;

                List<HierarchyNode> rootNodes = new();
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    rootNodes.Add(BuildNode(root, parameters.Depth, parameters.IncludeComponents, ""));
                }

                sceneNodes.Add(new HierarchyNode
                {
                    Name = scene.name,
                    Path = scene.path,
                    Active = true,
                    Children = rootNodes.ToArray()
                });
            }

            GetHierarchyResponse response = new()
            {
                Scenes = sceneNodes.ToArray()
            };
            return Task.FromResult(response);
        }

        private static HierarchyNode BuildNode(GameObject go, int remainingDepth, bool includeComponents, string parentPath)
        {
            string currentPath = string.IsNullOrEmpty(parentPath) ? go.name : parentPath + "/" + go.name;

            string[] components = null;
            if (includeComponents)
            {
                Component[] comps = go.GetComponents<Component>();
                components = new string[comps.Length];
                for (int i = 0; i < comps.Length; i++)
                {
                    components[i] = comps[i] != null ? comps[i].GetType().Name : "null";
                }
            }

            HierarchyNode[] children = null;
            if (remainingDepth > 0 && go.transform.childCount > 0)
            {
                List<HierarchyNode> childNodes = new();
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    childNodes.Add(BuildNode(go.transform.GetChild(i).gameObject, remainingDepth - 1, includeComponents, currentPath));
                }
                children = childNodes.ToArray();
            }

            return new HierarchyNode
            {
                Name = go.name,
                Path = currentPath,
                Active = go.activeInHierarchy,
                Components = components,
                Children = children
            };
        }
    }
}
