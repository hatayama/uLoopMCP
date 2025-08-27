// Prefab editing wrapper utilities
#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public static class PrefabEditUtil
    {
        /// <summary>
        /// Load, modify, and save a prefab from a project path.
        /// Ensures save/unload and marks assets dirty.
        /// </summary>
        public static void WithLoadedPrefab(string prefabAssetPath, Action<GameObject> editAction, DryRunContext ctx = null, OperationSummary summary = null)
        {
            if (string.IsNullOrEmpty(prefabAssetPath))
            {
                summary?.AddFailure("WithLoadedPrefab failed: prefabAssetPath is null or empty");
                return;
            }

            if (DryRunContext.IsActive(ctx))
            {
                ctx.Log($"[DRY-RUN] Load prefab: {prefabAssetPath}");
                summary?.AddSuccess($"Dry-run edited prefab: {prefabAssetPath}");
                return;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabAssetPath);
            if (prefabRoot == null)
            {
                summary?.AddFailure($"WithLoadedPrefab failed: Could not load {prefabAssetPath}");
                return;
            }

            try
            {
                editAction?.Invoke(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabAssetPath);
                EditorUtility.SetDirty(prefabRoot);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }
}
#endif


