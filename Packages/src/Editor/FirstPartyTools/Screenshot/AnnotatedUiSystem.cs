namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Names the UI system an annotated element came from, as reported in its UiSystem field.
    /// PhysicsCollider elements leave the field empty.
    /// </summary>
    internal static class AnnotatedUiSystem
    {
        internal const string UGUI = "UGUI";
        internal const string UIToolkit = "UIToolkit";
    }
}
