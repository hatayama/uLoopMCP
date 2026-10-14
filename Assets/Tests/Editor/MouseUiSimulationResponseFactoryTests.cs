#nullable enable
using NUnit.Framework;
using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.Runtime;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Characterizes wire-visible mouse UI response construction before factory extraction.
    /// </summary>
    [TestFixture]
    public sealed class MouseUiSimulationResponseFactoryTests
    {
        /// <summary>
        /// Verifies generic validation failures preserve the command action and supplied message.
        /// </summary>
        [Test]
        public void CreateFailure_WithCommandAndMessage_MapsFailureResponse()
        {
            MouseUiSimulationCommand command = CreateCommand(new SimulateMouseUiSchema
            {
                Action = UnityCliLoopMouseUiAction.LongPress
            });

            SimulateMouseUiResponse response =
                MouseUiSimulationResponseFactory.CreateFailure(command, "Validation failed.");

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Validation failed."));
            Assert.That(response.Action, Is.EqualTo(MouseAction.LongPress.ToString()));
        }

        /// <summary>
        /// Verifies frame timeout responses preserve the hit and complete drag coordinates.
        /// </summary>
        [Test]
        public void CreateFrameTimeoutResult_WithEndPosition_MapsAllCoordinates()
        {
            Vector2 position = new(10.25f, 20.75f);
            Vector2 endPosition = new(30.5f, 40.25f);

            SimulateMouseUiResponse response = MouseUiSimulationResponseFactory.CreateFrameTimeoutResult(
                MouseAction.Drag,
                position,
                endPosition,
                "DragTarget");

            Assert.That(response.Success, Is.False);
            Assert.That(
                response.Message,
                Is.EqualTo(
                    $"Timed out after {UnityCliLoopConstants.EDITOR_FRAME_WAIT_TIMEOUT_MS}ms while waiting for an editor frame."));
            Assert.That(response.Action, Is.EqualTo(MouseAction.Drag.ToString()));
            Assert.That(response.HitGameObjectName, Is.EqualTo("DragTarget"));
            Assert.That(response.PositionX, Is.EqualTo(position.x));
            Assert.That(response.PositionY, Is.EqualTo(position.y));
            Assert.That(response.EndPositionX, Is.EqualTo(endPosition.x));
            Assert.That(response.EndPositionY, Is.EqualTo(endPosition.y));
        }

        /// <summary>
        /// Verifies frame timeout responses keep optional hit and end coordinates absent.
        /// </summary>
        [Test]
        public void CreateFrameTimeoutResult_WithoutEndPosition_LeavesOptionalFieldsNull()
        {
            Vector2 position = new(10.25f, 20.75f);

            SimulateMouseUiResponse response = MouseUiSimulationResponseFactory.CreateFrameTimeoutResult(
                MouseAction.Click,
                position,
                null,
                null);

            Assert.That(response.Action, Is.EqualTo(MouseAction.Click.ToString()));
            Assert.That(response.HitGameObjectName, Is.Null);
            Assert.That(response.EndPositionX, Is.Null);
            Assert.That(response.EndPositionY, Is.Null);
        }

        /// <summary>
        /// Verifies click results retain normal, bypass, and no-hit message contracts.
        /// </summary>
        [TestCase(false, true, "ClickTarget", "", "Clicked 'ClickTarget' at (10.3, 20.8)")]
        [TestCase(true, true, "ClickTarget", "Canvas/ClickTarget", "Bypass-clicked 'ClickTarget' at (10.3, 20.8) via 'Canvas/ClickTarget'")]
        [TestCase(false, false, null, "", "Clicked at (10.3, 20.8) - no UI element hit")]
        public void CreateClickResult_WithHitMode_MapsMessageAndFields(
            bool bypassRaycast,
            bool hitTarget,
            string? targetName,
            string targetPath,
            string expectedMessage)
        {
            MouseUiSimulationCommand command = CreateCommand(new SimulateMouseUiSchema
            {
                Action = UnityCliLoopMouseUiAction.Click,
                BypassRaycast = bypassRaycast,
                TargetPath = targetPath
            });
            Vector2 position = new(10.25f, 20.75f);

            SimulateMouseUiResponse response =
                MouseUiSimulationResponseFactory.CreateClickResult(command, position, CreateHit(hitTarget, targetName));

            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Is.EqualTo(expectedMessage));
            Assert.That(response.Action, Is.EqualTo(MouseAction.Click.ToString()));
            Assert.That(response.HitGameObjectName, Is.EqualTo(targetName));
            Assert.That(response.PositionX, Is.EqualTo(position.x));
            Assert.That(response.PositionY, Is.EqualTo(position.y));
        }

        /// <summary>
        /// Verifies long-press results retain normal, bypass, and no-hit message contracts.
        /// </summary>
        [TestCase(false, true, "PressTarget", "", "Long-pressed 'PressTarget' at (10.3, 20.8) for 1.3s")]
        [TestCase(true, true, "PressTarget", "Canvas/PressTarget", "Bypass-long-pressed 'PressTarget' at (10.3, 20.8) via 'Canvas/PressTarget' for 1.3s")]
        [TestCase(false, false, null, "", "Long-pressed at (10.3, 20.8) for 1.3s - no UI element hit")]
        public void CreateLongPressResult_WithHitMode_MapsMessageAndFields(
            bool bypassRaycast,
            bool hitTarget,
            string? targetName,
            string targetPath,
            string expectedMessage)
        {
            MouseUiSimulationCommand command = CreateCommand(new SimulateMouseUiSchema
            {
                Action = UnityCliLoopMouseUiAction.LongPress,
                BypassRaycast = bypassRaycast,
                TargetPath = targetPath,
                Duration = 1.25f
            });
            Vector2 position = new(10.25f, 20.75f);

            SimulateMouseUiResponse response =
                MouseUiSimulationResponseFactory.CreateLongPressResult(command, position, CreateHit(hitTarget, targetName));

            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Is.EqualTo(expectedMessage));
            Assert.That(response.Action, Is.EqualTo(MouseAction.LongPress.ToString()));
            Assert.That(response.HitGameObjectName, Is.EqualTo(targetName));
            Assert.That(response.PositionX, Is.EqualTo(position.x));
            Assert.That(response.PositionY, Is.EqualTo(position.y));
        }

        /// <summary>
        /// Verifies click and long-press results carry the hit's UI system and path, and name UI Toolkit hits as
        /// such in the message.
        /// </summary>
        [Test]
        public void CreatePressResults_WithUIToolkitHit_MapUiSystemAndPath()
        {
            MouseUiHitReport hit = new MouseUiHitReport("play", "Menu/Document#play", "UIToolkit");
            Vector2 position = new(200f, 125f);

            SimulateMouseUiResponse click = MouseUiSimulationResponseFactory.CreateClickResult(
                CreateCommand(new SimulateMouseUiSchema { Action = UnityCliLoopMouseUiAction.Click }), position, hit);
            SimulateMouseUiResponse longPress = MouseUiSimulationResponseFactory.CreateLongPressResult(
                CreateCommand(new SimulateMouseUiSchema { Action = UnityCliLoopMouseUiAction.LongPress, Duration = 0.5f }), position, hit);

            Assert.That(click.Message, Is.EqualTo("Clicked UI Toolkit element 'play' at (200.0, 125.0)"));
            Assert.That(longPress.Message, Is.EqualTo("Long-pressed UI Toolkit element 'play' at (200.0, 125.0) for 0.5s"));
            foreach (SimulateMouseUiResponse response in new[] { click, longPress })
            {
                Assert.That(response.HitGameObjectName, Is.EqualTo("play"));
                Assert.That(response.UiSystem, Is.EqualTo("UIToolkit"));
                Assert.That(response.HitPath, Is.EqualTo("Menu/Document#play"));
            }
        }

        /// <summary>
        /// Verifies a uGUI hit keeps its message and carries its UI system and path, and no hit leaves both null.
        /// </summary>
        [Test]
        public void CreateClickResult_WithUguiHitOrNoHit_MapsUiSystemAndPath()
        {
            MouseUiSimulationCommand command = CreateCommand(new SimulateMouseUiSchema { Action = UnityCliLoopMouseUiAction.Click });
            Vector2 position = new(10f, 20f);

            SimulateMouseUiResponse ugui = MouseUiSimulationResponseFactory.CreateClickResult(
                command, position, new MouseUiHitReport("Ok", "Canvas/Ok", "UGUI"));
            SimulateMouseUiResponse none = MouseUiSimulationResponseFactory.CreateClickResult(
                command, position, MouseUiHitReport.None);

            Assert.That(ugui.Message, Is.EqualTo("Clicked 'Ok' at (10.0, 20.0)"));
            Assert.That(ugui.UiSystem, Is.EqualTo("UGUI"));
            Assert.That(ugui.HitPath, Is.EqualTo("Canvas/Ok"));
            Assert.That(none.UiSystem, Is.Null);
            Assert.That(none.HitPath, Is.Null);
        }

        /// <summary>
        /// Verifies one-shot drag results retain normal and bypass message contracts.
        /// </summary>
        [TestCase(false, "", "Dragged 'DragTarget' from (10.3, 20.8) to (30.5, 40.3) at 125 px/s")]
        [TestCase(true, "Canvas/DragTarget", "Bypass-dragged 'DragTarget' from (10.3, 20.8) to (30.5, 40.3) via 'Canvas/DragTarget' at 125 px/s")]
        public void CreateDragResult_WithBypassMode_MapsMessageAndFields(
            bool bypassRaycast,
            string targetPath,
            string expectedMessage)
        {
            MouseUiSimulationCommand command = CreateCommand(new SimulateMouseUiSchema
            {
                Action = UnityCliLoopMouseUiAction.Drag,
                BypassRaycast = bypassRaycast,
                TargetPath = targetPath,
                DragSpeed = 125f
            });
            Vector2 startPosition = new(10.25f, 20.75f);
            Vector2 endPosition = new(30.5f, 40.25f);

            SimulateMouseUiResponse response = MouseUiSimulationResponseFactory.CreateDragResult(
                command,
                startPosition,
                endPosition,
                new MouseUiHitReport("DragTarget", "Canvas/DragTarget", "UGUI"));

            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Is.EqualTo(expectedMessage));
            Assert.That(response.Action, Is.EqualTo(MouseAction.Drag.ToString()));
            Assert.That(response.HitGameObjectName, Is.EqualTo("DragTarget"));
            Assert.That(response.UiSystem, Is.EqualTo("UGUI"));
            Assert.That(response.HitPath, Is.EqualTo("Canvas/DragTarget"));
            Assert.That(response.PositionX, Is.EqualTo(startPosition.x));
            Assert.That(response.PositionY, Is.EqualTo(startPosition.y));
            Assert.That(response.EndPositionX, Is.EqualTo(endPosition.x));
            Assert.That(response.EndPositionY, Is.EqualTo(endPosition.y));
        }

        /// <summary>
        /// Verifies drag-end results preserve the final position and speed message.
        /// </summary>
        [Test]
        public void CreateDragEndResult_WithFinalPosition_MapsMessageAndFields()
        {
            MouseUiSimulationCommand command = CreateCommand(new SimulateMouseUiSchema
            {
                Action = UnityCliLoopMouseUiAction.DragEnd,
                DragSpeed = 125f
            });
            Vector2 endPosition = new(30.5f, 40.25f);

            SimulateMouseUiResponse response = MouseUiSimulationResponseFactory.CreateDragEndResult(
                command,
                endPosition,
                new MouseUiHitReport("DragTarget", "Canvas/DragTarget", "UGUI"));

            Assert.That(response.Success, Is.True);
            Assert.That(
                response.Message,
                Is.EqualTo("Drag ended on 'DragTarget' at (30.5, 40.3) at 125 px/s"));
            Assert.That(response.UiSystem, Is.EqualTo("UGUI"));
            Assert.That(response.HitPath, Is.EqualTo("Canvas/DragTarget"));
            Assert.That(response.Action, Is.EqualTo(MouseAction.DragEnd.ToString()));
            Assert.That(response.HitGameObjectName, Is.EqualTo("DragTarget"));
            Assert.That(response.PositionX, Is.EqualTo(endPosition.x));
            Assert.That(response.PositionY, Is.EqualTo(endPosition.y));
        }

        /// <summary>
        /// Verifies drag results name a UI Toolkit start element as such and carry its UI system and path.
        /// </summary>
        [Test]
        public void CreateDragResults_WithUIToolkitHit_NameUIToolkitElement()
        {
            MouseUiHitReport hit = new MouseUiHitReport("volume", "Menu/Document#volume", "UIToolkit");
            Vector2 start = new(100f, 200f);
            Vector2 end = new(300f, 200f);

            SimulateMouseUiResponse drag = MouseUiSimulationResponseFactory.CreateDragResult(
                CreateCommand(new SimulateMouseUiSchema { Action = UnityCliLoopMouseUiAction.Drag, DragSpeed = 100f }), start, end, hit);
            SimulateMouseUiResponse dragEnd = MouseUiSimulationResponseFactory.CreateDragEndResult(
                CreateCommand(new SimulateMouseUiSchema { Action = UnityCliLoopMouseUiAction.DragEnd, DragSpeed = 100f }), end, hit);

            Assert.That(drag.Message, Is.EqualTo("Dragged UI Toolkit element 'volume' from (100.0, 200.0) to (300.0, 200.0) at 100 px/s"));
            Assert.That(dragEnd.Message, Is.EqualTo("Drag ended on UI Toolkit element 'volume' at (300.0, 200.0) at 100 px/s"));
            foreach (SimulateMouseUiResponse response in new[] { drag, dragEnd })
            {
                Assert.That(response.UiSystem, Is.EqualTo("UIToolkit"));
                Assert.That(response.HitPath, Is.EqualTo("Menu/Document#volume"));
            }
        }

        private static MouseUiSimulationCommand CreateCommand(SimulateMouseUiSchema schema)
        {
            (MouseUiSimulationCommand? command, string? errorMessage) =
                MouseUiSimulationCommand.TryFromSchema(schema);
            Assert.That(errorMessage, Is.Null);
            Assert.That(command, Is.Not.Null);
            return command!;
        }

        private static MouseUiHitReport CreateHit(bool hitTarget, string? targetName)
        {
            return hitTarget ? new MouseUiHitReport(targetName, null, null) : MouseUiHitReport.None;
        }
    }
}
