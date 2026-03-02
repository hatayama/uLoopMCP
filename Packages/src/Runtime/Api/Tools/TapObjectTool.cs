using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public sealed class TapObjectSchema : BaseToolSchema
    {
        public int ObjectId { get; set; } = -1;
        public string ObjectPath { get; set; }
    }

    public sealed class TapObjectResponse : BaseToolResponse
    {
        public bool Tapped { get; set; }
        public string TargetName { get; set; }
        public string Error { get; set; }
    }

    public sealed class TapObjectTool : AbstractDeviceTool<TapObjectSchema, TapObjectResponse>
    {
        public override string ToolName => "tap-object";

        private readonly Dictionary<int, GameObject> _objectIdMap;

        public TapObjectTool(Dictionary<int, GameObject> objectIdMap)
        {
            Debug.Assert(objectIdMap != null, "objectIdMap must not be null");
            _objectIdMap = objectIdMap;
        }

        protected override Task<TapObjectResponse> ExecuteAsync(TapObjectSchema parameters, CancellationToken ct)
        {
            if (!EventSystemHelper.IsEventSystemAvailable())
            {
                return Task.FromResult(new TapObjectResponse { Tapped = false, Error = "EventSystem not available" });
            }

            GameObject target = ResolveTarget(parameters);
            if (target == null)
            {
                return Task.FromResult(new TapObjectResponse { Tapped = false, Error = "Target not found" });
            }

            bool success = EventSystemHelper.SimulateClick(target);
            return Task.FromResult(new TapObjectResponse
            {
                Tapped = success,
                TargetName = target.name
            });
        }

        private GameObject ResolveTarget(TapObjectSchema parameters)
        {
            if (parameters.ObjectId >= 0)
            {
                _objectIdMap.TryGetValue(parameters.ObjectId, out GameObject go);
                if (go != null) return go;
            }

            if (!string.IsNullOrEmpty(parameters.ObjectPath))
            {
                return GameObjectPathResolver.FindByPath(parameters.ObjectPath);
            }

            return null;
        }
    }
}
