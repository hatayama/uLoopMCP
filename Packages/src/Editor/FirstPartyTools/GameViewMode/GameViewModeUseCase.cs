using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using io.github.hatayama.UnityCliLoop.InternalAPIBridge;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Decides whether the current Play Mode view tab must be swapped, swaps it, shows the requested
    /// device in the Simulator, and rotates it to match the Game view's orientation.
    /// </summary>
    internal sealed class GameViewModeUseCase
    {
        private readonly IGameViewSwitchPort _port;

        public GameViewModeUseCase(IGameViewSwitchPort port)
        {
            Debug.Assert(port != null, "port must not be null");
            _port = port;
        }

        public GameViewModeResponse Execute(GameViewModeSchema parameters)
        {
            Debug.Assert(parameters != null, "parameters must not be null");

            PlayModeViewKind current = _port.GetMainViewKind();
            string device = parameters.Device?.Trim();
            GameViewModeResponse rejected = Reject(current, parameters.To, device);
            if (rejected != null)
            {
                return rejected;
            }

            PlayModeViewKind? target = ResolveTarget(parameters.To, device);
            bool needsSimulator = target == PlayModeViewKind.Simulator || parameters.ListDevices;
            if (needsSimulator && !_port.IsSimulatorAvailable())
            {
                return SimulatorUnavailable(current);
            }

            // The name is resolved before any view changes so that an ambiguous or unknown name
            // leaves the Editor exactly as it was.
            IReadOnlyList<string> names = device != null || parameters.ListDevices ? _port.LoadSimulatorDeviceNames() : null;
            string selected = null;
            if (device != null)
            {
                SimulatorDeviceSelectionResult match = SimulatorDeviceSelection.Resolve(device, names);
                if (match.Kind != SimulatorDeviceMatchKind.Selected)
                {
                    return DeviceNotResolved(current, device, match, names);
                }

                selected = match.DeviceName;
            }

            GameViewModeResponse response = Dispatch(current, target, selected);
            if (parameters.ListDevices)
            {
                response.AvailableDevices = names.ToArray();
            }

            return response;
        }

        private static GameViewModeResponse Reject(PlayModeViewKind current, GameViewModeTarget? to, string device)
        {
            if (device == null)
            {
                return null;
            }

            if (device.Length == 0)
            {
                return Fail(current, "Pass a device name with --device, for example --device \"iPhone 12\".");
            }

            if (to == GameViewModeTarget.GameView)
            {
                return Fail(current, "--device selects a Device Simulator device; drop it or pass --to Simulator.");
            }

            return null;
        }

        private static PlayModeViewKind? ResolveTarget(GameViewModeTarget? to, string device)
        {
            if (to.HasValue)
            {
                return ToKind(to.Value);
            }

            if (device != null)
            {
                return PlayModeViewKind.Simulator;
            }

            return null;
        }

        private GameViewModeResponse Dispatch(PlayModeViewKind current, PlayModeViewKind? target, string selected)
        {
            if (!target.HasValue)
            {
                return Report(current);
            }

            if (current == target.Value && selected == null)
            {
                return AlreadyThere(current);
            }

            if (current == PlayModeViewKind.None)
            {
                return Open(target.Value, selected);
            }

            if (target.Value == PlayModeViewKind.GameView)
            {
                return SwapToGameView(current);
            }

            if (current == PlayModeViewKind.Simulator)
            {
                return SelectDeviceOnly(selected);
            }

            return SwapToSimulator(current, selected);
        }

        private GameViewModeResponse Report(PlayModeViewKind current)
        {
            return Build(current, current, false, ReadSimulator(current), $"The current Play Mode view is {Label(current)}.", string.Empty);
        }

        private GameViewModeResponse AlreadyThere(PlayModeViewKind current)
        {
            return Build(current, current, false, ReadSimulator(current), $"The Play Mode view is already {Label(current)}.", string.Empty);
        }

        private static GameViewModeResponse SimulatorUnavailable(PlayModeViewKind current)
        {
            return Fail(
                current,
                "The Device Simulator is not available in this Editor; install the Device Simulator package or use Unity 2021.1 or newer.");
        }

        private static GameViewModeResponse DeviceNotResolved(
            PlayModeViewKind current,
            string query,
            SimulatorDeviceSelectionResult match,
            IReadOnlyList<string> names)
        {
            bool ambiguous = match.Kind == SimulatorDeviceMatchKind.Ambiguous;
            string message = ambiguous
                ? $"\"{query}\" matches {match.Candidates.Count} devices; pass a more specific name."
                : $"No device matches \"{query}\"; pass one of the available device names.";
            GameViewModeResponse response = Fail(current, message);
            response.AvailableDevices = (ambiguous ? match.Candidates : names).ToArray();
            return response;
        }

        private GameViewModeResponse Open(PlayModeViewKind target, string selected)
        {
            // Opening a fresh view has no Game view shape to copy, so the Simulator keeps its own rotation.
            _port.OpenView(target);
            if (target == PlayModeViewKind.Simulator)
            {
                SelectDevice(selected);
            }

            return Build(
                PlayModeViewKind.None,
                target,
                true,
                ReadSimulator(target),
                $"No Play Mode view was open; opened {Label(target)}." + ShowsSuffix(selected),
                string.Empty);
        }

        private GameViewModeResponse SwapToGameView(PlayModeViewKind current)
        {
            _port.SwapMainViewTo(PlayModeViewKind.GameView);
            return Build(current, PlayModeViewKind.GameView, true, SimulatorState.None, $"Switched {Label(current)} to the Game view.", string.Empty);
        }

        private GameViewModeResponse SelectDeviceOnly(string selected)
        {
            bool changed = SelectDevice(selected);
            string message = changed
                ? $"Selected {selected} in the Device Simulator."
                : $"The Device Simulator already shows {selected}.";
            return Build(PlayModeViewKind.Simulator, PlayModeViewKind.Simulator, changed, ReadSimulator(PlayModeViewKind.Simulator), message, string.Empty);
        }

        private GameViewModeResponse SwapToSimulator(PlayModeViewKind current, string selected)
        {
            // The size must be read before the swap: afterwards the main view is the Simulator, whose
            // rendering resolution is the device screen, not the shape the user was looking at.
            Vector2 size = _port.GetMainViewTargetSize();
            SimulatorRotationDecisionResult decision =
                SimulatorRotationDecision.Decide(size.x, size.y, _port.ReadOrientationPolicy());

            _port.SwapMainViewTo(PlayModeViewKind.Simulator);
            // The device goes first: showing a device rebuilds the screen simulation, which would make
            // an orientation read before it stale.
            SelectDevice(selected);

            string warning = decision.Warning;
            bool rotationApplied = false;
            if (decision.RotationDegrees.HasValue)
            {
                rotationApplied = _port.TrySetSimulatorRotation(decision.RotationDegrees.Value);
                if (!rotationApplied)
                {
                    warning = Join(warning, "The Simulator rotation could not be applied in this Unity version; rotate it from the Simulator toolbar.");
                }
            }

            SimulatorState state = ReadSimulator(PlayModeViewKind.Simulator);
            if (rotationApplied && state.Orientation != null && IsLandscape(state.Orientation) != decision.WantsLandscape)
            {
                warning = Join(warning, $"The Simulator renders {state.Orientation} although the device was rotated; the selected device may not support that orientation.");
            }

            string wanted = decision.WantsLandscape ? "landscape" : "portrait";
            string message = rotationApplied
                ? $"Switched {Label(current)} to the Device Simulator and rotated the device to {wanted}."
                : $"Switched {Label(current)} to the Device Simulator.";
            return Build(current, PlayModeViewKind.Simulator, true, state, message + ShowsSuffix(selected), warning);
        }

        // Precondition: the current view is the Simulator. Returns true when the shown device changed.
        private bool SelectDevice(string selected)
        {
            if (selected == null)
            {
                return false;
            }

            string before = _port.GetSimulatorDeviceName();
            if (string.Equals(before, selected, StringComparison.Ordinal))
            {
                return false;
            }

            _port.SetSimulatorDevice(selected);
            return true;
        }

        private SimulatorState ReadSimulator(PlayModeViewKind view)
        {
            if (view != PlayModeViewKind.Simulator)
            {
                return SimulatorState.None;
            }

            return new SimulatorState(_port.GetSimulatorRenderedOrientation(), _port.GetSimulatorDeviceName());
        }

        private static string ShowsSuffix(string selected)
        {
            return selected == null ? string.Empty : $" The Simulator shows {selected}.";
        }

        private static bool IsLandscape(string renderedOrientation)
        {
            return renderedOrientation.StartsWith("Landscape", StringComparison.Ordinal);
        }

        private static string Join(string first, string second)
        {
            if (string.IsNullOrEmpty(first))
            {
                return second;
            }

            return first + " " + second;
        }

        private static PlayModeViewKind ToKind(GameViewModeTarget target)
        {
            return target == GameViewModeTarget.Simulator ? PlayModeViewKind.Simulator : PlayModeViewKind.GameView;
        }

        private static string Label(PlayModeViewKind kind)
        {
            switch (kind)
            {
                case PlayModeViewKind.GameView:
                    return "the Game view";
                case PlayModeViewKind.Simulator:
                    return "the Device Simulator";
                case PlayModeViewKind.None:
                    return "none";
                default:
                    return "another Play Mode view";
            }
        }

        private static GameViewModeResponse Fail(PlayModeViewKind current, string message)
        {
            GameViewModeResponse response = Build(current, current, false, SimulatorState.None, message, string.Empty);
            response.Success = false;
            return response;
        }

        private static GameViewModeResponse Build(
            PlayModeViewKind previous,
            PlayModeViewKind view,
            bool changed,
            SimulatorState simulator,
            string message,
            string warning)
        {
            return new GameViewModeResponse
            {
                PreviousView = previous.ToString(),
                View = view.ToString(),
                Changed = changed,
                SimulatorOrientation = simulator.Orientation,
                Device = simulator.Device,
                Message = message,
                Warning = warning ?? string.Empty,
            };
        }

        /// <summary>
        /// What the Simulator shows once a request is done; both null when the view is not the Simulator.
        /// </summary>
        private readonly struct SimulatorState
        {
            public SimulatorState(string orientation, string device)
            {
                Orientation = orientation;
                Device = device;
            }

            public string Orientation { get; }
            public string Device { get; }

            public static SimulatorState None => new SimulatorState(null, null);
        }
    }
}
