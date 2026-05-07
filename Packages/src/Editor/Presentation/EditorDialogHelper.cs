using UnityEditor;

using io.github.hatayama.UnityCliLoop.Application;
using io.github.hatayama.UnityCliLoop.Domain;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Presentation
{
    public static class EditorDialogHelper
    {
        public static void ShowSkillsInstalledDialog()
        {
#if UNITY_6000_3_OR_NEWER
            EditorDialog.DisplayAlertDialog(
                "Skills Installed",
                "Skills have been installed successfully.",
                "OK",
                DialogIconType.Info);
#else
            EditorUtility.DisplayDialog(
                "Skills Installed",
                "Skills have been installed successfully.",
                "OK");
#endif
        }
    }
}
