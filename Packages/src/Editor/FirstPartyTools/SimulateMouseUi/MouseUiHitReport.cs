#nullable enable

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// What a simulated press hit, captured as plain strings so responses never read UnityEngine objects or
    /// UI Toolkit elements after an await.
    /// </summary>
    internal sealed class MouseUiHitReport
    {
        internal MouseUiHitReport(string? name, string? path, string? uiSystem)
        {
            Name = name;
            Path = path;
            UiSystem = uiSystem;
        }

        internal static MouseUiHitReport None { get; } = new MouseUiHitReport(null, null, null);

        // Null means nothing was hit; the wire contract of HitGameObjectName depends on that.
        internal string? Name { get; }
        internal string? Path { get; }
        internal string? UiSystem { get; }
        internal bool HasHit => Name != null;
    }
}
