#if ULOOPMCP_HAS_INPUT_SYSTEM
#nullable enable
using System.Collections;
using System.Threading.Tasks;
using io.github.hatayama.uLoopMCP;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public class SimulateKeyboardTests : InputTestFixture
    {
        private GameObject eventSystemGo = null!;
        private GameObject framePressObserverGo = null!;
        private SimulateKeyboardTool tool = null!;
        private SimulateKeyboardResponse lastResponse = null!;
        private Keyboard keyboard = null!;
        private FramePressObserver framePressObserver = null!;
        private FrameStateObserver frameStateObserver = null!;
        private InputSettings.UpdateMode originalUpdateMode;
        private float originalTimeScale;

        public override void Setup()
        {
            base.Setup();
            InputSettings settings = RequireInputSettings();
            originalUpdateMode = settings.updateMode;
            originalTimeScale = Time.timeScale;

            eventSystemGo = new GameObject("TestEventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            framePressObserverGo = new GameObject("FramePressObserver");
            framePressObserver = framePressObserverGo.AddComponent<FramePressObserver>();
            frameStateObserver = framePressObserverGo.AddComponent<FrameStateObserver>();

            tool = new SimulateKeyboardTool();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            InputSettings settings = RequireInputSettings();
            settings.updateMode = originalUpdateMode;
            Time.timeScale = originalTimeScale;
            KeyboardKeyState.ReleaseAllKeys();
            Object.Destroy(framePressObserverGo);
            Object.Destroy(eventSystemGo);
            base.TearDown();
        }

        #region Press Tests

        [UnityTest]
        public IEnumerator Press_Should_InjectKeyDownAndUp()
        {
            yield return null;

            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.Press.ToString(),
                ["key"] = "W"
            });

            Assert.IsTrue(lastResponse.Success);
            Assert.AreEqual("Press", lastResponse.Action);
            Assert.AreEqual("W", lastResponse.KeyName);
            // After press completes, key should be released
            Assert.IsFalse(keyboard[Key.W].isPressed, "Key should be released after press");
        }

        [UnityTest]
        public IEnumerator Press_WithDuration_Should_HoldKey()
        {
            yield return null;

            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.Press.ToString(),
                ["key"] = "Space",
                ["duration"] = 0.1f
            });

            Assert.IsTrue(lastResponse.Success);
            Assert.AreEqual("Space", lastResponse.KeyName);
        }

        [UnityTest]
        public IEnumerator Press_Space_Should_SetWasPressedThisFrame()
        {
            yield return null;

            framePressObserver.ResetCount();

            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.Press.ToString(),
                ["key"] = "Space",
                ["duration"] = 0.1f
            });

            Assert.Greater(framePressObserver.SpacePressedFrameCount, 0, "Space press should be visible via wasPressedThisFrame");
        }

        [UnityTest]
        public IEnumerator Press_WithoutDuration_Should_BehaveAsTap()
        {
            yield return null;

            framePressObserver.ResetCount();

            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.Press.ToString(),
                ["key"] = "Space"
            });

            Assert.Greater(framePressObserver.SpacePressedFrameCount, 0, "Zero-duration press should still be visible as a tap");
            Assert.IsFalse(keyboard[Key.Space].isPressed, "Zero-duration press should release the key after the tap");
        }

        [UnityTest]
        public IEnumerator Press_Should_KeepHeldOverlayKeys()
        {
            yield return null;

            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.KeyDown.ToString(),
                ["key"] = "LeftShift"
            });
            Assert.IsTrue(lastResponse.Success);

            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.Press.ToString(),
                ["key"] = "Space"
            });

            CollectionAssert.Contains(SimulateKeyboardOverlayState.HeldKeys, "LeftShift", "Press should not clear held-key overlay badges");
        }

        [UnityTest]
        public IEnumerator Press_Enter_Should_SetWasPressedThisFrame()
        {
            yield return null;

            framePressObserver.ResetCount();

            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.Press.ToString(),
                ["key"] = "Enter",
                ["duration"] = 0.1f
            });

            Assert.Greater(framePressObserver.EnterPressedFrameCount, 0, "Enter press should be visible via wasPressedThisFrame");
        }

        [UnityTest]
        public IEnumerator Press_InManualMode_Should_NotHang()
        {
            yield return null;

            InputSettings settings = RequireInputSettings();
            settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            framePressObserver.ResetCount();

            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.Press.ToString(),
                ["key"] = "Enter"
            });

            Assert.IsTrue(lastResponse.Success);
            Assert.Greater(framePressObserver.EnterPressedFrameCount, 0, "Manual-mode press should advance input and register the tap");
            Assert.IsFalse(keyboard[Key.Enter].isPressed, "Manual-mode press should release the key after the tap");
        }

        [UnityTest]
        public IEnumerator Press_InPausedFixedMode_Should_NotHang()
        {
            yield return null;

            InputSettings settings = RequireInputSettings();
            settings.updateMode = InputSettings.UpdateMode.ProcessEventsInFixedUpdate;
            Time.timeScale = 0f;
            framePressObserver.ResetCount();

            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.Press.ToString(),
                ["key"] = "Enter"
            });

            Assert.IsTrue(lastResponse.Success);
            Assert.IsFalse(keyboard[Key.Enter].isPressed, "Paused fixed-update press should release the key after the tap");
        }

        [UnityTest]
        public IEnumerator Press_WithInvalidKey_Should_ReturnFailure()
        {
            yield return null;

            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.Press.ToString(),
                ["key"] = "InvalidKeyName"
            });

            Assert.IsFalse(lastResponse.Success);
            StringAssert.Contains("Invalid key name", lastResponse.Message);
        }

        [UnityTest]
        public IEnumerator Press_WithEmptyKey_Should_ReturnFailure()
        {
            yield return null;

            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.Press.ToString(),
                ["key"] = ""
            });

            Assert.IsFalse(lastResponse.Success);
            StringAssert.Contains("Key parameter is required", lastResponse.Message);
        }

        #endregion

        #region KeyDown / KeyUp Tests

        [UnityTest]
        public IEnumerator KeyDown_Should_HoldKeyUntilKeyUp()
        {
            yield return null;

            frameStateObserver.ResetCounts();

            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.KeyDown.ToString(),
                ["key"] = "W"
            });

            Assert.IsTrue(lastResponse.Success);
            Assert.IsTrue(KeyboardKeyState.IsKeyHeld(Key.W), "Key should be held after KeyDown");
            Assert.Greater(frameStateObserver.WPressedUpdateCount, 0, "KeyDown should wait until Update observed the pressed key");

            frameStateObserver.ResetCounts();
            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.KeyUp.ToString(),
                ["key"] = "W"
            });

            Assert.IsTrue(lastResponse.Success);
            Assert.IsFalse(KeyboardKeyState.IsKeyHeld(Key.W), "Key should be released after KeyUp");
            Assert.Greater(frameStateObserver.WReleasedUpdateCount, 0, "KeyUp should wait until Update observed the released key");
        }

        [UnityTest]
        public IEnumerator KeyDown_WhenAlreadyHeld_Should_ReturnFailure()
        {
            yield return null;

            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.KeyDown.ToString(),
                ["key"] = "W"
            });
            Assert.IsTrue(lastResponse.Success);

            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.KeyDown.ToString(),
                ["key"] = "W"
            });
            Assert.IsFalse(lastResponse.Success);
            StringAssert.Contains("already held", lastResponse.Message);
        }

        [UnityTest]
        public IEnumerator KeyUp_WhenNotHeld_Should_ReturnFailure()
        {
            yield return null;

            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.KeyUp.ToString(),
                ["key"] = "W"
            });

            Assert.IsFalse(lastResponse.Success);
            StringAssert.Contains("not currently held", lastResponse.Message);
        }

        [UnityTest]
        public IEnumerator MultipleKeys_Should_SupportSimultaneousHold()
        {
            yield return null;

            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.KeyDown.ToString(),
                ["key"] = "LeftShift"
            });
            Assert.IsTrue(lastResponse.Success);

            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.KeyDown.ToString(),
                ["key"] = "W"
            });
            Assert.IsTrue(lastResponse.Success);

            Assert.IsTrue(KeyboardKeyState.IsKeyHeld(Key.LeftShift), "LeftShift should be held");
            Assert.IsTrue(KeyboardKeyState.IsKeyHeld(Key.W), "W should be held");

            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.KeyUp.ToString(),
                ["key"] = "W"
            });
            Assert.IsTrue(lastResponse.Success);
            Assert.IsTrue(KeyboardKeyState.IsKeyHeld(Key.LeftShift), "LeftShift should still be held");
            Assert.IsFalse(KeyboardKeyState.IsKeyHeld(Key.W), "W should be released");

            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.KeyUp.ToString(),
                ["key"] = "LeftShift"
            });
            Assert.IsTrue(lastResponse.Success);
        }

        #endregion

        #region State Management Tests

        [UnityTest]
        public IEnumerator ReleaseAllKeys_Should_ClearAllHeldKeys()
        {
            yield return null;

            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.KeyDown.ToString(),
                ["key"] = "W"
            });

            yield return RunTool(new JObject
            {
                ["action"] = KeyboardAction.KeyDown.ToString(),
                ["key"] = "LeftShift"
            });

            Assert.IsTrue(KeyboardKeyState.IsKeyHeld(Key.W));
            Assert.IsTrue(KeyboardKeyState.IsKeyHeld(Key.LeftShift));

            KeyboardKeyState.ReleaseAllKeys();

            Assert.IsFalse(KeyboardKeyState.IsKeyHeld(Key.W));
            Assert.IsFalse(KeyboardKeyState.IsKeyHeld(Key.LeftShift));
            Assert.AreEqual(0, KeyboardKeyState.HeldKeys.Count);
        }

        #endregion

        #region Helpers

        private IEnumerator RunTool(JObject parameters)
        {
            Task<BaseToolResponse> task = tool.ExecuteAsync(parameters);
            yield return WaitForTask(task);
            lastResponse = (SimulateKeyboardResponse)task.Result;
        }

        private static IEnumerator WaitForTask(Task task)
        {
            float timeoutAt = Time.realtimeSinceStartup + 5f;
            yield return new WaitUntil(() =>
                task.IsCompleted || Time.realtimeSinceStartup >= timeoutAt);
            Assert.IsTrue(task.IsCompleted, "Tool execution timed out.");
            Assert.IsFalse(task.IsFaulted, $"Tool execution should not fault: {task.Exception}");
        }

        private static InputSettings RequireInputSettings()
        {
            InputSettings? settings = InputSystem.settings;
            Debug.Assert(settings != null, "InputSystem.settings must be available in SimulateKeyboardTests");
            return settings!;
        }

        #endregion
    }

    public class FramePressObserver : MonoBehaviour
    {
        public int SpacePressedFrameCount { get; private set; }
        public int EnterPressedFrameCount { get; private set; }

        private void OnEnable()
        {
            InputSystem.onAfterUpdate += HandleAfterUpdate;
        }

        private void OnDisable()
        {
            InputSystem.onAfterUpdate -= HandleAfterUpdate;
        }

        // The tool follows the configured Input System update mode, so the
        // observer must sample wasPressedThisFrame from the same update loop.
        private void HandleAfterUpdate()
        {
            InputUpdateType expectedUpdateType = KeyboardInputUpdateTypeResolver.Resolve();
            InputUpdateType currentUpdateType = InputState.currentUpdateType;
            if (!KeyboardInputUpdateTypeResolver.IsMatch(currentUpdateType, expectedUpdateType))
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                SpacePressedFrameCount++;
            }

            if (keyboard.enterKey.wasPressedThisFrame)
            {
                EnterPressedFrameCount++;
            }
        }

        public void ResetCount()
        {
            SpacePressedFrameCount = 0;
            EnterPressedFrameCount = 0;
        }
    }

    public class FrameStateObserver : MonoBehaviour
    {
        public int WPressedUpdateCount { get; private set; }
        public int WReleasedUpdateCount { get; private set; }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.wKey.isPressed)
            {
                WPressedUpdateCount++;
                return;
            }

            WReleasedUpdateCount++;
        }

        public void ResetCounts()
        {
            WPressedUpdateCount = 0;
            WReleasedUpdateCount = 0;
        }
    }
}
#endif
