using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace io.github.hatayama.uLoopMCP
{
    public sealed class InputTextSchema : BaseToolSchema
    {
        public string Text { get; set; }
        public string TargetObjectPath { get; set; }
    }

    public sealed class InputTextResponse : BaseToolResponse
    {
        public bool Success { get; set; }
        public string TargetName { get; set; }
        public string Error { get; set; }
    }

    public sealed class InputTextTool : AbstractDeviceTool<InputTextSchema, InputTextResponse>
    {
        public override string ToolName => "input-text";

        protected override Task<InputTextResponse> ExecuteAsync(InputTextSchema parameters, CancellationToken ct)
        {
            Debug.Assert(parameters.Text != null, "text must not be null");

            InputField target = ResolveInputField(parameters.TargetObjectPath);
            if (target == null)
            {
                return Task.FromResult(new InputTextResponse
                {
                    Success = false,
                    Error = string.IsNullOrEmpty(parameters.TargetObjectPath)
                        ? "No active InputField found in scene"
                        : $"InputField not found at path: {parameters.TargetObjectPath}"
                });
            }

            target.text = parameters.Text;
            target.onValueChanged?.Invoke(parameters.Text);
            target.onEndEdit?.Invoke(parameters.Text);

            return Task.FromResult(new InputTextResponse
            {
                Success = true,
                TargetName = target.gameObject.name
            });
        }

        private static InputField ResolveInputField(string objectPath)
        {
            if (!string.IsNullOrEmpty(objectPath))
            {
                GameObject go = GameObjectPathResolver.FindByPath(objectPath);
                if (go == null) return null;
                return go.GetComponent<InputField>();
            }

            return FindFirstActiveInputField();
        }

        private static InputField FindFirstActiveInputField()
        {
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    InputField field = root.GetComponentInChildren<InputField>(false);
                    if (field != null) return field;
                }
            }
            return null;
        }
    }
}
