#nullable enable

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// The UiSystem values simulate-mouse-ui reports, matching the ones screenshot annotations use.
    /// </summary>
    internal static class MouseUiHitSystem
    {
        internal const string UGUI = "UGUI";
        internal const string UIToolkit = "UIToolkit";

        // A target outside any Canvas, such as an object hit by a physics raycaster.
        internal const string NonUi = "";
    }
}
