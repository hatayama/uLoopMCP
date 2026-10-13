#nullable enable

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// The thing a click or long press is delivered to, whichever UI system it belongs to.
    /// </summary>
    internal interface IMouseUiPressTarget
    {
        // Captured when the target is created, so it stays valid after the target goes away.
        MouseUiHitReport Report { get; }

        void Click();

        void PressDown();

        // Safe to call from cleanup even if nothing was pressed or the target has gone away.
        void Release();
    }
}
