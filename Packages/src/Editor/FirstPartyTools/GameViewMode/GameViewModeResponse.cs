using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Reports which Play Mode view was showing before and after a game-view-mode request.
    /// </summary>
    public sealed class GameViewModeResponse : UnityCliLoopToolResponse
    {
        /// <summary>
        /// View before the request: "GameView", "Simulator", "None", or "Other".
        /// </summary>
        public string PreviousView { get; set; } = "";

        /// <summary>
        /// View after the request, using the same values as <see cref="PreviousView"/>.
        /// </summary>
        public string View { get; set; } = "";

        /// <summary>
        /// True when the request swapped the tab, opened a view, or changed the shown device.
        /// </summary>
        public bool Changed { get; set; }

        /// <summary>
        /// Orientation the Device Simulator renders after the request. Present only when the
        /// current view is the Simulator and the orientation could be read.
        /// </summary>
        public string SimulatorOrientation { get; set; }

        public bool ShouldSerializeSimulatorOrientation()
        {
            return !string.IsNullOrEmpty(SimulatorOrientation);
        }

        /// <summary>
        /// Name of the device the Simulator shows after the request. Present only when the request
        /// succeeded and the current view is the Simulator.
        /// </summary>
        public string Device { get; set; }

        public bool ShouldSerializeDevice()
        {
            return !string.IsNullOrEmpty(Device);
        }

        /// <summary>
        /// Device names: every device with --list-devices, or the candidates (or every device) when
        /// --device matched several devices or none. Absent otherwise.
        /// </summary>
        public string[] AvailableDevices { get; set; }

        public bool ShouldSerializeAvailableDevices()
        {
            return AvailableDevices != null;
        }

        public string Message { get; set; } = "";

        public string Warning { get; set; } = "";

        public bool ShouldSerializeWarning()
        {
            return !string.IsNullOrEmpty(Warning);
        }
    }
}
