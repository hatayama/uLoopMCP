using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.InternalAPIBridge;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies the Switch Game View use case against a fake Editor port, without a DockArea.
    /// </summary>
    public sealed class SwitchGameViewUseCaseTests
    {
        private static readonly Vector2 LandscapeSize = new(1920f, 1080f);
        private static readonly Vector2 PortraitSize = new(1080f, 1920f);

        private static OrientationPolicy EverythingAllowed()
        {
            return new OrientationPolicy(UIOrientation.AutoRotation, true, true, true, true);
        }

        private static SwitchGameViewSchema Request(SwitchGameViewTarget? to)
        {
            return new SwitchGameViewSchema { To = to };
        }

        [Test]
        public void Execute_WithoutTargetOnGameView_ReportsWithoutChanging()
        {
            // Verifies omitting --to only reports the current Game view and touches nothing.
            FakeGameViewSwitchPort port = new() { Kind = PlayModeViewKind.GameView };

            SwitchGameViewResponse response = new SwitchGameViewUseCase(port).Execute(Request(null));

            Assert.That(port.CountCalls("SwapMainViewTo"), Is.Zero);
            Assert.That(port.CountCalls("OpenView"), Is.Zero);
            Assert.That(port.CountCalls("TrySetSimulatorRotation"), Is.Zero);
            Assert.That(response.Success, Is.True);
            Assert.That(response.Changed, Is.False);
            Assert.That(response.PreviousView, Is.EqualTo("GameView"));
            Assert.That(response.View, Is.EqualTo("GameView"));
            Assert.That(JObject.FromObject(response)["SimulatorOrientation"], Is.Null);
        }

        [Test]
        public void Execute_WithoutTargetOnSimulator_ReportsRenderedOrientation()
        {
            // Verifies the status form includes the Simulator's rendered orientation.
            FakeGameViewSwitchPort port = new() { Kind = PlayModeViewKind.Simulator, Rendered = "Portrait" };

            SwitchGameViewResponse response = new SwitchGameViewUseCase(port).Execute(Request(null));

            Assert.That(response.Changed, Is.False);
            Assert.That(response.View, Is.EqualTo("Simulator"));
            Assert.That(response.SimulatorOrientation, Is.EqualTo("Portrait"));
        }

        [Test]
        public void Execute_WithoutTargetAndNoView_ReportsNone()
        {
            // Verifies the status form reports "None" when no Play Mode view is open.
            FakeGameViewSwitchPort port = new() { Kind = PlayModeViewKind.None };

            SwitchGameViewResponse response = new SwitchGameViewUseCase(port).Execute(Request(null));

            Assert.That(response.View, Is.EqualTo("None"));
            Assert.That(response.Changed, Is.False);
        }

        [Test]
        public void Execute_SimulatorFromLandscapeGameView_SwapsAfterReadingSizeAndRotatesToLandscapeLeft()
        {
            // Verifies the Game view size is read before the swap and the device is rotated to 270.
            FakeGameViewSwitchPort port = new()
            {
                Kind = PlayModeViewKind.GameView,
                TargetSize = LandscapeSize,
                Policy = EverythingAllowed(),
                Rendered = "Portrait",
            };

            SwitchGameViewResponse response =
                new SwitchGameViewUseCase(port).Execute(Request(SwitchGameViewTarget.Simulator));

            Assert.That(port.IndexOfCall("GetMainViewTargetSize"), Is.LessThan(port.IndexOfCall("SwapMainViewTo:Simulator")));
            Assert.That(port.CountCalls("SwapMainViewTo"), Is.EqualTo(1));
            Assert.That(port.Calls, Does.Contain("TrySetSimulatorRotation:270"));
            Assert.That(port.CountCalls("TrySetSimulatorRotation"), Is.EqualTo(1));
            Assert.That(response.Changed, Is.True);
            Assert.That(response.PreviousView, Is.EqualTo("GameView"));
            Assert.That(response.View, Is.EqualTo("Simulator"));
            Assert.That(response.SimulatorOrientation, Is.EqualTo("LandscapeLeft"));
            Assert.That(response.Message, Does.Contain("rotated the device to landscape"));
            Assert.That(response.Warning, Is.Empty);
        }

        [Test]
        public void Execute_SimulatorFromPortraitGameView_RotatesToPortrait()
        {
            // Verifies a tall Game view rotates the device upright (0 degrees).
            FakeGameViewSwitchPort port = new()
            {
                Kind = PlayModeViewKind.GameView,
                TargetSize = PortraitSize,
                Policy = EverythingAllowed(),
                Rendered = "LandscapeLeft",
            };

            SwitchGameViewResponse response =
                new SwitchGameViewUseCase(port).Execute(Request(SwitchGameViewTarget.Simulator));

            Assert.That(port.Calls, Does.Contain("TrySetSimulatorRotation:0"));
            Assert.That(response.SimulatorOrientation, Is.EqualTo("Portrait"));
        }

        [Test]
        public void Execute_SimulatorWhenLandscapeIsNotRenderable_SwapsWithoutRotatingAndWarns()
        {
            // Verifies a portrait-only project gets the Simulator but no rotation, plus a warning.
            FakeGameViewSwitchPort port = new()
            {
                Kind = PlayModeViewKind.Other,
                TargetSize = LandscapeSize,
                Policy = new OrientationPolicy(UIOrientation.Portrait, true, true, true, true),
                Rendered = "Portrait",
            };

            SwitchGameViewResponse response =
                new SwitchGameViewUseCase(port).Execute(Request(SwitchGameViewTarget.Simulator));

            Assert.That(port.CountCalls("SwapMainViewTo"), Is.EqualTo(1));
            Assert.That(port.CountCalls("TrySetSimulatorRotation"), Is.Zero);
            Assert.That(response.Warning, Does.Contain("landscape"));
            Assert.That(response.SimulatorOrientation, Is.EqualTo("Portrait"));
            Assert.That(response.Message, Does.Not.Contain("rotated"));
            Assert.That(response.Changed, Is.True);
        }

        [Test]
        public void Execute_SimulatorWhenRotationCannotBeApplied_WarnsWithoutClaimingRotation()
        {
            // Verifies a failed rotation is reported as such, without the device-support warning.
            FakeGameViewSwitchPort port = new()
            {
                Kind = PlayModeViewKind.GameView,
                TargetSize = LandscapeSize,
                Policy = EverythingAllowed(),
                RotationApplies = false,
                Rendered = "Portrait",
            };

            SwitchGameViewResponse response =
                new SwitchGameViewUseCase(port).Execute(Request(SwitchGameViewTarget.Simulator));

            Assert.That(port.Calls, Does.Contain("TrySetSimulatorRotation:270"));
            Assert.That(response.Warning, Does.Contain("could not be applied"));
            Assert.That(response.Warning, Does.Not.Contain("may not support"));
            Assert.That(response.Message, Does.Not.Contain("rotated"));
            Assert.That(response.SimulatorOrientation, Is.EqualTo("Portrait"));
            Assert.That(response.Changed, Is.True);
            Assert.That(response.View, Is.EqualTo("Simulator"));
        }

        [Test]
        public void Execute_SimulatorWhenDeviceIgnoresRotation_WarnsAboutDeviceSupport()
        {
            // Verifies a rotation the selected device does not render produces the support warning.
            FakeGameViewSwitchPort port = new()
            {
                Kind = PlayModeViewKind.GameView,
                TargetSize = LandscapeSize,
                Policy = EverythingAllowed(),
                RotationApplies = true,
                RenderedOverride = "Portrait",
            };

            SwitchGameViewResponse response =
                new SwitchGameViewUseCase(port).Execute(Request(SwitchGameViewTarget.Simulator));

            Assert.That(response.Warning, Does.Contain("may not support"));
            Assert.That(response.SimulatorOrientation, Is.EqualTo("Portrait"));
        }

        [Test]
        public void Execute_SimulatorWhenAlreadySimulator_ChangesNothing()
        {
            // Verifies asking for the Simulator while it is showing swaps and rotates nothing.
            FakeGameViewSwitchPort port = new() { Kind = PlayModeViewKind.Simulator, Rendered = "LandscapeLeft" };

            SwitchGameViewResponse response =
                new SwitchGameViewUseCase(port).Execute(Request(SwitchGameViewTarget.Simulator));

            Assert.That(port.CountCalls("SwapMainViewTo"), Is.Zero);
            Assert.That(port.CountCalls("TrySetSimulatorRotation"), Is.Zero);
            Assert.That(response.Changed, Is.False);
            Assert.That(response.Message, Does.Contain("already"));
            Assert.That(response.SimulatorOrientation, Is.EqualTo("LandscapeLeft"));
        }

        [Test]
        public void Execute_SimulatorWhenNoViewIsOpen_OpensSimulatorWithoutRotating()
        {
            // Verifies a missing Play Mode view is replaced by a new Simulator window, rotation untouched.
            FakeGameViewSwitchPort port = new() { Kind = PlayModeViewKind.None };

            SwitchGameViewResponse response =
                new SwitchGameViewUseCase(port).Execute(Request(SwitchGameViewTarget.Simulator));

            Assert.That(port.CountCalls("OpenView:Simulator"), Is.EqualTo(1));
            Assert.That(port.CountCalls("SwapMainViewTo"), Is.Zero);
            Assert.That(port.CountCalls("TrySetSimulatorRotation"), Is.Zero);
            Assert.That(response.Changed, Is.True);
            Assert.That(response.PreviousView, Is.EqualTo("None"));
            Assert.That(response.View, Is.EqualTo("Simulator"));
        }

        [Test]
        public void Execute_SimulatorWhenSimulatorIsUnavailable_FailsBeforeTouchingTheView()
        {
            // Verifies an Editor without the Device Simulator gets a failed response and no swap.
            FakeGameViewSwitchPort port = new() { Kind = PlayModeViewKind.GameView, SimulatorAvailable = false };

            SwitchGameViewResponse response =
                new SwitchGameViewUseCase(port).Execute(Request(SwitchGameViewTarget.Simulator));

            Assert.That(response.Success, Is.False);
            Assert.That(port.CountCalls("SwapMainViewTo"), Is.Zero);
            Assert.That(port.CountCalls("OpenView"), Is.Zero);
            Assert.That(response.View, Is.EqualTo("GameView"));
            Assert.That(response.Message, Does.Contain("Device Simulator"));
        }

        [Test]
        public void Execute_GameViewFromSimulator_SwapsBackWithoutOrientation()
        {
            // Verifies switching back needs no size read and reports no Simulator orientation.
            FakeGameViewSwitchPort port = new() { Kind = PlayModeViewKind.Simulator };

            SwitchGameViewResponse response =
                new SwitchGameViewUseCase(port).Execute(Request(SwitchGameViewTarget.GameView));

            Assert.That(port.CountCalls("SwapMainViewTo:GameView"), Is.EqualTo(1));
            Assert.That(port.CountCalls("GetMainViewTargetSize"), Is.Zero);
            Assert.That(response.Changed, Is.True);
            Assert.That(response.View, Is.EqualTo("GameView"));
            Assert.That(JObject.FromObject(response)["SimulatorOrientation"], Is.Null);
        }

        [Test]
        public void Execute_GameViewWhenAlreadyGameView_ChangesNothing()
        {
            // Verifies asking for the Game view while it is showing is a no-op.
            FakeGameViewSwitchPort port = new() { Kind = PlayModeViewKind.GameView };

            SwitchGameViewResponse response =
                new SwitchGameViewUseCase(port).Execute(Request(SwitchGameViewTarget.GameView));

            Assert.That(port.CountCalls("SwapMainViewTo"), Is.Zero);
            Assert.That(port.CountCalls("OpenView"), Is.Zero);
            Assert.That(port.CountCalls("TrySetSimulatorRotation"), Is.Zero);
            Assert.That(response.Changed, Is.False);
        }

        [Test]
        public void Execute_GameViewWhenNoViewIsOpen_OpensGameView()
        {
            // Verifies a missing Play Mode view is replaced by a new Game view window.
            FakeGameViewSwitchPort port = new() { Kind = PlayModeViewKind.None };

            SwitchGameViewResponse response =
                new SwitchGameViewUseCase(port).Execute(Request(SwitchGameViewTarget.GameView));

            Assert.That(port.CountCalls("OpenView:GameView"), Is.EqualTo(1));
            Assert.That(response.Changed, Is.True);
            Assert.That(response.View, Is.EqualTo("GameView"));
        }

        [Test]
        public void Execute_WhenSwapThrows_PropagatesWithoutRotating()
        {
            // Verifies a swap failure surfaces as the tool error and no rotation is attempted.
            FakeGameViewSwitchPort port = new()
            {
                Kind = PlayModeViewKind.GameView,
                TargetSize = LandscapeSize,
                SwapException = new InvalidOperationException("did not switch"),
            };

            Assert.That(
                () => new SwitchGameViewUseCase(port).Execute(Request(SwitchGameViewTarget.Simulator)),
                Throws.InvalidOperationException);
            Assert.That(port.CountCalls("TrySetSimulatorRotation"), Is.Zero);
        }

        private sealed class FakeGameViewSwitchPort : IGameViewSwitchPort
        {
            public PlayModeViewKind Kind { get; set; } = PlayModeViewKind.GameView;
            public bool SimulatorAvailable { get; set; } = true;
            public Vector2 TargetSize { get; set; } = LandscapeSize;
            public OrientationPolicy Policy { get; set; } = EverythingAllowed();
            public bool RotationApplies { get; set; } = true;
            public string Rendered { get; set; } = "Portrait";
            public string RenderedOverride { get; set; }
            public Exception SwapException { get; set; }
            public List<string> Calls { get; } = new();

            public int CountCalls(string prefix)
            {
                return Calls.Count(call => call.StartsWith(prefix, StringComparison.Ordinal));
            }

            public int IndexOfCall(string name)
            {
                return Calls.IndexOf(name);
            }

            public PlayModeViewKind GetMainViewKind()
            {
                Calls.Add("GetMainViewKind");
                return Kind;
            }

            public bool IsSimulatorAvailable()
            {
                Calls.Add("IsSimulatorAvailable");
                return SimulatorAvailable;
            }

            public Vector2 GetMainViewTargetSize()
            {
                Calls.Add("GetMainViewTargetSize");
                return TargetSize;
            }

            public void SwapMainViewTo(PlayModeViewKind target)
            {
                Calls.Add("SwapMainViewTo:" + target);
                if (SwapException != null)
                {
                    throw SwapException;
                }

                Kind = target;
            }

            public void OpenView(PlayModeViewKind target)
            {
                Calls.Add("OpenView:" + target);
                Kind = target;
            }

            public OrientationPolicy ReadOrientationPolicy()
            {
                Calls.Add("ReadOrientationPolicy");
                return Policy;
            }

            // Mirrors the real bridge: a successful rotation is visible in the rendered orientation.
            public bool TrySetSimulatorRotation(int degrees)
            {
                Calls.Add("TrySetSimulatorRotation:" + degrees);
                if (!RotationApplies)
                {
                    return false;
                }

                Rendered = OrientationFor(degrees);
                return true;
            }

            public string GetSimulatorRenderedOrientation()
            {
                Calls.Add("GetSimulatorRenderedOrientation");
                return RenderedOverride ?? Rendered;
            }

            private static string OrientationFor(int degrees)
            {
                switch (degrees)
                {
                    case 0:
                        return "Portrait";
                    case 90:
                        return "LandscapeRight";
                    case 180:
                        return "PortraitUpsideDown";
                    case 270:
                        return "LandscapeLeft";
                    default:
                        throw new ArgumentOutOfRangeException(nameof(degrees), degrees, "Unexpected rotation");
                }
            }
        }
    }
}
