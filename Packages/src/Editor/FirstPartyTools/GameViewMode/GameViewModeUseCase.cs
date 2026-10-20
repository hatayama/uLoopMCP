using UnityEngine;

using io.github.hatayama.UnityCliLoop.InternalAPIBridge;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Decides whether the current Play Mode view tab must be swapped, swaps it, and rotates the
    /// simulated device to match the Game view's orientation.
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
            if (!parameters.To.HasValue)
            {
                return Report(current);
            }

            PlayModeViewKind target = ToKind(parameters.To.Value);
            if (current == target)
            {
                return AlreadyThere(current);
            }

            if (target == PlayModeViewKind.Simulator && !_port.IsSimulatorAvailable())
            {
                return SimulatorUnavailable(current);
            }

            if (current == PlayModeViewKind.None)
            {
                return Open(target);
            }

            if (target == PlayModeViewKind.Simulator)
            {
                return SwapToSimulator(current);
            }

            return SwapToGameView(current);
        }

        private GameViewModeResponse Report(PlayModeViewKind current)
        {
            string orientation = current == PlayModeViewKind.Simulator ? _port.GetSimulatorRenderedOrientation() : null;
            return Build(current, current, false, orientation, $"The current Play Mode view is {Label(current)}.", string.Empty);
        }

        private GameViewModeResponse AlreadyThere(PlayModeViewKind current)
        {
            string orientation = current == PlayModeViewKind.Simulator ? _port.GetSimulatorRenderedOrientation() : null;
            return Build(current, current, false, orientation, $"The Play Mode view is already {Label(current)}.", string.Empty);
        }

        private static GameViewModeResponse SimulatorUnavailable(PlayModeViewKind current)
        {
            GameViewModeResponse response = Build(
                current,
                current,
                false,
                null,
                "The Device Simulator is not available in this Editor; install the Device Simulator package or use Unity 2021.1 or newer.",
                string.Empty);
            response.Success = false;
            return response;
        }

        private GameViewModeResponse Open(PlayModeViewKind target)
        {
            // Opening a fresh view has no Game view shape to copy, so the Simulator keeps its own rotation.
            _port.OpenView(target);
            string orientation = target == PlayModeViewKind.Simulator ? _port.GetSimulatorRenderedOrientation() : null;
            return Build(
                PlayModeViewKind.None,
                target,
                true,
                orientation,
                $"No Play Mode view was open; opened {Label(target)}.",
                string.Empty);
        }

        private GameViewModeResponse SwapToGameView(PlayModeViewKind current)
        {
            _port.SwapMainViewTo(PlayModeViewKind.GameView);
            return Build(current, PlayModeViewKind.GameView, true, null, $"Switched {Label(current)} to the Game view.", string.Empty);
        }

        private GameViewModeResponse SwapToSimulator(PlayModeViewKind current)
        {
            // The size must be read before the swap: afterwards the main view is the Simulator, whose
            // rendering resolution is the device screen, not the shape the user was looking at.
            Vector2 size = _port.GetMainViewTargetSize();
            SimulatorRotationDecisionResult decision =
                SimulatorRotationDecision.Decide(size.x, size.y, _port.ReadOrientationPolicy());

            _port.SwapMainViewTo(PlayModeViewKind.Simulator);

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

            string rendered = _port.GetSimulatorRenderedOrientation();
            if (rotationApplied && rendered != null && IsLandscape(rendered) != decision.WantsLandscape)
            {
                warning = Join(warning, $"The Simulator renders {rendered} although the device was rotated; the selected device may not support that orientation.");
            }

            string wanted = decision.WantsLandscape ? "landscape" : "portrait";
            string message = rotationApplied
                ? $"Switched {Label(current)} to the Device Simulator and rotated the device to {wanted}."
                : $"Switched {Label(current)} to the Device Simulator.";
            return Build(current, PlayModeViewKind.Simulator, true, rendered, message, warning);
        }

        private static bool IsLandscape(string renderedOrientation)
        {
            return renderedOrientation.StartsWith("Landscape", System.StringComparison.Ordinal);
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

        private static GameViewModeResponse Build(
            PlayModeViewKind previous,
            PlayModeViewKind view,
            bool changed,
            string simulatorOrientation,
            string message,
            string warning)
        {
            return new GameViewModeResponse
            {
                PreviousView = previous.ToString(),
                View = view.ToString(),
                Changed = changed,
                SimulatorOrientation = simulatorOrientation,
                Message = message,
                Warning = warning ?? string.Empty,
            };
        }
    }
}
