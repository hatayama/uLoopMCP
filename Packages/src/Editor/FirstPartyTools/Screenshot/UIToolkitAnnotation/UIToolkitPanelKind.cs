#nullable enable
using System.Reflection;
using UnityEngine.UIElements;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Tells world-space UI Toolkit panels apart from screen-space ones.
    /// </summary>
    internal static class UIToolkitPanelKind
    {
        private const string RENDER_MODE_PROPERTY = "renderMode";
        private const string WORLD_SPACE = "WorldSpace";

        // Reflection because renderMode is internal from 6000.0 to 6000.3.10 and public from 6000.3.11, and
        // version defines cannot tell patch versions apart. 2022.3 has no such property and no world-space panels.
        internal static bool IsWorldSpace(PanelSettings settings)
        {
            return HasWorldSpaceRenderMode(settings);
        }

        // Takes any object and looks the property up on its runtime type so tests can check the lookup and the
        // comparison with stand-in types: the project's Unity has no renderMode, and CI's Unity 6 legs skip
        // these tests.
        internal static bool HasWorldSpaceRenderMode(object settings)
        {
            PropertyInfo? property = settings.GetType().GetProperty(
                RENDER_MODE_PROPERTY,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null)
            {
                return false;
            }

            object? value = property.GetValue(settings);
            return value != null && value.ToString() == WORLD_SPACE;
        }
    }
}
