// Editor utility for safe, persistent SerializedObject bindings
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Helper methods to bind serialized fields in a robust and persistent way.
    /// - Uses SerializedObject/SerializedProperty
    /// - Applies and marks objects/scenes dirty
    /// - Optional dry-run and operation summaries
    /// </summary>
    public static class SerializedBindingUtil
    {
        public static bool BindObject(Component component, string fieldName, Object value, DryRunContext ctx = null, OperationSummary summary = null)
        {
            if (!TryGetSerialized(component, fieldName, out var so, out var prop, out string error))
            {
                summary?.AddFailure($"BindObject failed: {error}");
                return false;
            }

            if (prop.propertyType != SerializedPropertyType.ObjectReference)
            {
                summary?.AddFailure($"BindObject failed: Field '{fieldName}' is not an ObjectReference on {component.GetType().Name}");
                return false;
            }

            if (DryRunContext.IsActive(ctx))
            {
                ctx.Log($"[DRY-RUN] {component.name}.{fieldName} <- {(value == null ? "null" : value.name)}");
                summary?.AddSuccess($"Dry-run bound {component.name}.{fieldName}");
                return true;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
            DirtyUtil.MarkObjectDirty(component);
            DirtyUtil.MarkSceneDirtyOfObject(component);
            summary?.AddSuccess($"Bound {component.name}.{fieldName}");
            return true;
        }

        public static bool BindFloat(Component component, string fieldName, float value, DryRunContext ctx = null, OperationSummary summary = null)
        {
            if (!TryGetSerialized(component, fieldName, out var so, out var prop, out string error))
            {
                summary?.AddFailure($"BindFloat failed: {error}");
                return false;
            }

            if (prop.propertyType != SerializedPropertyType.Float)
            {
                summary?.AddFailure($"BindFloat failed: Field '{fieldName}' is not a Float on {component.GetType().Name}");
                return false;
            }

            if (DryRunContext.IsActive(ctx))
            {
                ctx.Log($"[DRY-RUN] {component.name}.{fieldName} <- {value}");
                summary?.AddSuccess($"Dry-run bound {component.name}.{fieldName}");
                return true;
            }

            prop.floatValue = value;
            so.ApplyModifiedProperties();
            DirtyUtil.MarkObjectDirty(component);
            DirtyUtil.MarkSceneDirtyOfObject(component);
            summary?.AddSuccess($"Bound {component.name}.{fieldName}");
            return true;
        }

        public static bool BindInt(Component component, string fieldName, int value, DryRunContext ctx = null, OperationSummary summary = null)
        {
            if (!TryGetSerialized(component, fieldName, out var so, out var prop, out string error))
            {
                summary?.AddFailure($"BindInt failed: {error}");
                return false;
            }

            if (prop.propertyType != SerializedPropertyType.Integer)
            {
                summary?.AddFailure($"BindInt failed: Field '{fieldName}' is not an Integer on {component.GetType().Name}");
                return false;
            }

            if (DryRunContext.IsActive(ctx))
            {
                ctx.Log($"[DRY-RUN] {component.name}.{fieldName} <- {value}");
                summary?.AddSuccess($"Dry-run bound {component.name}.{fieldName}");
                return true;
            }

            prop.intValue = value;
            so.ApplyModifiedProperties();
            DirtyUtil.MarkObjectDirty(component);
            DirtyUtil.MarkSceneDirtyOfObject(component);
            summary?.AddSuccess($"Bound {component.name}.{fieldName}");
            return true;
        }

        public static bool BindBool(Component component, string fieldName, bool value, DryRunContext ctx = null, OperationSummary summary = null)
        {
            if (!TryGetSerialized(component, fieldName, out var so, out var prop, out string error))
            {
                summary?.AddFailure($"BindBool failed: {error}");
                return false;
            }

            if (prop.propertyType != SerializedPropertyType.Boolean)
            {
                summary?.AddFailure($"BindBool failed: Field '{fieldName}' is not a Boolean on {component.GetType().Name}");
                return false;
            }

            if (DryRunContext.IsActive(ctx))
            {
                ctx.Log($"[DRY-RUN] {component.name}.{fieldName} <- {value}");
                summary?.AddSuccess($"Dry-run bound {component.name}.{fieldName}");
                return true;
            }

            prop.boolValue = value;
            so.ApplyModifiedProperties();
            DirtyUtil.MarkObjectDirty(component);
            DirtyUtil.MarkSceneDirtyOfObject(component);
            summary?.AddSuccess($"Bound {component.name}.{fieldName}");
            return true;
        }

        public static bool BindColor(Component component, string fieldName, Color value, DryRunContext ctx = null, OperationSummary summary = null)
        {
            if (!TryGetSerialized(component, fieldName, out var so, out var prop, out string error))
            {
                summary?.AddFailure($"BindColor failed: {error}");
                return false;
            }

            if (prop.propertyType != SerializedPropertyType.Color)
            {
                summary?.AddFailure($"BindColor failed: Field '{fieldName}' is not a Color on {component.GetType().Name}");
                return false;
            }

            if (DryRunContext.IsActive(ctx))
            {
                ctx.Log($"[DRY-RUN] {component.name}.{fieldName} <- {value}");
                summary?.AddSuccess($"Dry-run bound {component.name}.{fieldName}");
                return true;
            }

            prop.colorValue = value;
            so.ApplyModifiedProperties();
            DirtyUtil.MarkObjectDirty(component);
            DirtyUtil.MarkSceneDirtyOfObject(component);
            summary?.AddSuccess($"Bound {component.name}.{fieldName}");
            return true;
        }

        public static bool BindVector3(Component component, string fieldName, Vector3 value, DryRunContext ctx = null, OperationSummary summary = null)
        {
            if (!TryGetSerialized(component, fieldName, out var so, out var prop, out string error))
            {
                summary?.AddFailure($"BindVector3 failed: {error}");
                return false;
            }

            if (prop.propertyType != SerializedPropertyType.Vector3)
            {
                summary?.AddFailure($"BindVector3 failed: Field '{fieldName}' is not a Vector3 on {component.GetType().Name}");
                return false;
            }

            if (DryRunContext.IsActive(ctx))
            {
                ctx.Log($"[DRY-RUN] {component.name}.{fieldName} <- {value}");
                summary?.AddSuccess($"Dry-run bound {component.name}.{fieldName}");
                return true;
            }

            prop.vector3Value = value;
            so.ApplyModifiedProperties();
            DirtyUtil.MarkObjectDirty(component);
            DirtyUtil.MarkSceneDirtyOfObject(component);
            summary?.AddSuccess($"Bound {component.name}.{fieldName}");
            return true;
        }

        public static bool BindObjectList(Component component, string fieldName, IList<Object> values, DryRunContext ctx = null, OperationSummary summary = null)
        {
            if (!TryGetSerialized(component, fieldName, out var so, out var prop, out string error))
            {
                summary?.AddFailure($"BindObjectList failed: {error}");
                return false;
            }

            if (!prop.isArray)
            {
                summary?.AddFailure($"BindObjectList failed: Field '{fieldName}' is not an array/list on {component.GetType().Name}");
                return false;
            }

            if (DryRunContext.IsActive(ctx))
            {
                ctx.Log($"[DRY-RUN] {component.name}.{fieldName} <- List({(values == null ? 0 : values.Count)})");
                summary?.AddSuccess($"Dry-run bound list {component.name}.{fieldName}");
                return true;
            }

            prop.ClearArray();
            if (values != null)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    prop.InsertArrayElementAtIndex(i);
                    var element = prop.GetArrayElementAtIndex(i);
                    if (element.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        element.objectReferenceValue = values[i];
                    }
                }
            }

            so.ApplyModifiedProperties();
            DirtyUtil.MarkObjectDirty(component);
            DirtyUtil.MarkSceneDirtyOfObject(component);
            summary?.AddSuccess($"Bound list {component.name}.{fieldName}");
            return true;
        }

        public static bool FieldExists(Component component, string fieldName)
        {
            return TryGetSerialized(component, fieldName, out _, out var prop, out _) && prop != null;
        }

        private static bool TryGetSerialized(Component component, string fieldName, out SerializedObject so, out SerializedProperty prop, out string error)
        {
            so = null;
            prop = null;
            error = null;

            if (component == null)
            {
                error = "Component is null";
                return false;
            }
            if (string.IsNullOrEmpty(fieldName))
            {
                error = "Field name is null or empty";
                return false;
            }

            so = new SerializedObject(component);
            prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                error = $"Field '{fieldName}' not found on {component.GetType().Name}";
                return false;
            }
            return true;
        }
    }
}
#endif


