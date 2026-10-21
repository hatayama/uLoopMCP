using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using io.github.hatayama.UnityCliLoop.InternalAPIBridge;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Live Editor implementation of <see cref="IGameViewSwitchPort"/> on top of the internal bridge
    /// and public Editor APIs.
    /// </summary>
    internal sealed class EditorGameViewSwitchPort : IGameViewSwitchPort
    {
        // The window produced by the last swap or open. Right after a swap GetMainPlayModeView can
        // still name the old window, so Simulator calls go to this one when it exists.
        private EditorWindow _switchedWindow;

        public PlayModeViewKind GetMainViewKind()
        {
            return PlayModeViewSwitchBridge.GetMainViewKind();
        }

        public bool IsSimulatorAvailable()
        {
            return PlayModeViewSwitchBridge.IsSimulatorAvailable();
        }

        public Vector2 GetMainViewTargetSize()
        {
            PlayModeWindow.GetRenderingResolution(out uint width, out uint height);
            return new Vector2(width, height);
        }

        public void SwapMainViewTo(PlayModeViewKind target)
        {
            _switchedWindow = PlayModeViewSwitchBridge.SwapMainViewTo(target);
        }

        public void OpenView(PlayModeViewKind target)
        {
            _switchedWindow = PlayModeViewSwitchBridge.OpenView(target);
        }

        public OrientationPolicy ReadOrientationPolicy()
        {
            return new OrientationPolicy(
                PlayerSettings.defaultInterfaceOrientation,
                PlayerSettings.allowedAutorotateToPortrait,
                PlayerSettings.allowedAutorotateToPortraitUpsideDown,
                PlayerSettings.allowedAutorotateToLandscapeLeft,
                PlayerSettings.allowedAutorotateToLandscapeRight);
        }

        public bool TrySetSimulatorRotation(int degrees)
        {
            return PlayModeViewSwitchBridge.TrySetSimulatorRotation(CurrentSimulatorWindow(), degrees);
        }

        public string GetSimulatorRenderedOrientation()
        {
            return PlayModeViewSwitchBridge.GetSimulatorRenderedOrientation(CurrentSimulatorWindow());
        }

        public IReadOnlyList<string> LoadSimulatorDeviceNames()
        {
            return PlayModeViewSwitchBridge.LoadSimulatorDeviceNames();
        }

        public string GetSimulatorDeviceName()
        {
            return PlayModeViewSwitchBridge.GetSimulatorDeviceName(CurrentSimulatorWindow());
        }

        public void SetSimulatorDevice(string friendlyName)
        {
            PlayModeViewSwitchBridge.SetSimulatorDevice(CurrentSimulatorWindow(), friendlyName);
        }

        private EditorWindow CurrentSimulatorWindow()
        {
            if (_switchedWindow != null)
            {
                return _switchedWindow;
            }

            return PlayModeViewSwitchBridge.GetMainViewWindow();
        }
    }
}
