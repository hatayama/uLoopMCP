using System;
using System.IO;
using UnityEngine;

namespace io.github.hatayama.UnityCliLoop
{
    // Reads the shared dynamic-code permission setting without depending on the platform settings layer.
    internal static class FirstPartyDynamicCodeSettings
    {
        public static DynamicCodeSecurityLevel GetDynamicCodeSecurityLevel()
        {
            string settingsFilePath = Path.Combine(
                UnityCliLoopPathResolver.GetProjectRoot(),
                UnityCliLoopConstants.ULOOP_DIR,
                UnityCliLoopConstants.ULOOP_SETTINGS_FILE_NAME);

            if (!File.Exists(settingsFilePath))
            {
                return DynamicCodeSecurityLevel.Restricted;
            }

            string json = File.ReadAllText(settingsFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return DynamicCodeSecurityLevel.Restricted;
            }

            DynamicCodePermissionSettings settings = JsonUtility.FromJson<DynamicCodePermissionSettings>(json);
            if (settings == null || !Enum.IsDefined(typeof(DynamicCodeSecurityLevel), settings.dynamicCodeSecurityLevel))
            {
                return DynamicCodeSecurityLevel.Restricted;
            }

            return (DynamicCodeSecurityLevel)settings.dynamicCodeSecurityLevel;
        }

        [Serializable]
        private sealed class DynamicCodePermissionSettings
        {
            public int dynamicCodeSecurityLevel = (int)DynamicCodeSecurityLevel.Restricted;
        }
    }
}
