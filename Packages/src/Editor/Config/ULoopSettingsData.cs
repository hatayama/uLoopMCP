using System;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Security settings stored in .uloop/settings.permissions.json.
    /// This file can be git-tracked for team-wide security policy sharing.
    /// </summary>
    [Serializable]
    public record ULoopSettingsData
    {
        public bool enableTestsExecution = false;
        public bool allowMenuItemExecution = false;
        public bool allowThirdPartyTools = false;
        public int dynamicCodeSecurityLevel = (int)DynamicCodeSecurityLevel.Disabled;
    }
}
