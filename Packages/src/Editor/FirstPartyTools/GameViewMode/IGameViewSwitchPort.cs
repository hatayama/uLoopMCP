using System.Collections.Generic;
using UnityEngine;

using io.github.hatayama.UnityCliLoop.InternalAPIBridge;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Editor-side operations the game-view-mode use case needs, kept behind a port so the
    /// decision logic can be exercised without a DockArea.
    /// </summary>
    internal interface IGameViewSwitchPort
    {
        PlayModeViewKind GetMainViewKind();

        bool IsSimulatorAvailable();

        /// <summary>Rendering resolution of the main view. Precondition: a main view exists.</summary>
        Vector2 GetMainViewTargetSize();

        /// <summary>
        /// Replaces the main view tab in place. Precondition: target is GameView or Simulator, a main
        /// view exists, and it is of a different kind. Afterwards the Simulator calls below address
        /// the new window.
        /// </summary>
        void SwapMainViewTo(PlayModeViewKind target);

        /// <summary>Opens a view of the target kind. Precondition: no main view exists.</summary>
        void OpenView(PlayModeViewKind target);

        OrientationPolicy ReadOrientationPolicy();

        /// <summary>
        /// Rotates the simulated device. Precondition: the current view is the Simulator. When this
        /// returns true the rendered orientation already reflects the rotation.
        /// </summary>
        bool TrySetSimulatorRotation(int degrees);

        /// <summary>Rendered orientation of the Simulator, or null when it cannot be read.</summary>
        string GetSimulatorRenderedOrientation();

        /// <summary>
        /// Friendly names of every device the Simulator can show, in the Simulator's own order.
        /// Precondition: the Simulator is available.
        /// </summary>
        IReadOnlyList<string> LoadSimulatorDeviceNames();

        /// <summary>
        /// Friendly name of the device the Simulator shows, or null when it cannot be read.
        /// Precondition: the current view is the Simulator.
        /// </summary>
        string GetSimulatorDeviceName();

        /// <summary>
        /// Shows the named device. Precondition: the current view is the Simulator and the name is
        /// one of <see cref="LoadSimulatorDeviceNames"/>. Afterwards the Simulator renders that
        /// device with its current rotation.
        /// </summary>
        void SetSimulatorDevice(string friendlyName);
    }
}
