// Editor utility for marking objects and scenes dirty consistently
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public static class DirtyUtil
    {
        public static void MarkObjectDirty(Object obj)
        {
            if (obj == null) return;
            EditorUtility.SetDirty(obj);
        }

        public static void MarkSceneDirtyOfObject(Object obj)
        {
            if (obj is Component c)
            {
                EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
            }
            else if (obj is GameObject go)
            {
                EditorSceneManager.MarkSceneDirty(go.scene);
            }
        }
    }
}
#endif


