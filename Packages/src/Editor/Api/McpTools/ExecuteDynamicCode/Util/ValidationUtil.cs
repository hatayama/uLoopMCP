// Validation helpers to assert required references and report issues
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public static class ValidationUtil
    {
        public static bool AssertNotNull(Object obj, string context, OperationSummary summary = null)
        {
            if (obj != null)
            {
                summary?.AddSuccess($"OK: {context}");
                return true;
            }
            summary?.AddFailure($"Missing: {context}");
            return false;
        }

        public static bool AssertFieldExists(Component component, string fieldName, OperationSummary summary = null)
        {
            bool exists = SerializedBindingUtil.FieldExists(component, fieldName);
            if (exists)
            {
                summary?.AddSuccess($"OK: Field exists {component.GetType().Name}.{fieldName}");
            }
            else
            {
                summary?.AddFailure($"Missing field {component.GetType().Name}.{fieldName}");
            }
            return exists;
        }

        public static void AppendSummaryToLogs(OperationSummary summary, List<string> logs)
        {
            if (summary == null || logs == null) return;
            logs.Add(summary.BuildReport());
        }
    }
}
#endif


