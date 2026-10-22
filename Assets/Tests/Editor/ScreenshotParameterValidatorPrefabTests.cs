using NUnit.Framework;
using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies which screenshot parameter combinations are rejected for prefab captures before any asset is loaded.
    /// </summary>
    public sealed class ScreenshotParameterValidatorPrefabTests
    {
        private const string AnyPrefabPath = "Assets/Any.prefab";

        /// <summary>
        /// Verifies an explicit capture mode cannot be combined with a prefab path.
        /// </summary>
        [TestCase(CaptureMode.window, TestName = "ValidatePrefab_WithWindowCaptureMode_Throws")]
        [TestCase(CaptureMode.rendering, TestName = "ValidatePrefab_WithRenderingCaptureMode_Throws")]
        [TestCase(CaptureMode.GameView, TestName = "ValidatePrefab_WithGameViewCaptureMode_Throws")]
        public void ValidatePrefab_WithAnExplicitCaptureMode_Throws(CaptureMode captureMode)
        {
            ScreenshotSchema request = CreatePrefabRequest();
            request.CaptureMode = captureMode;

            AssertPrefabRejected(request, "CaptureMode");
        }

        /// <summary>
        /// Verifies the default auto capture mode is accepted for a prefab path.
        /// </summary>
        [Test]
        public void ValidatePrefab_WithAutoCaptureMode_Accepts()
        {
            ScreenshotSchema request = CreatePrefabRequest();

            Assert.DoesNotThrow(() => ScreenshotParameterValidator.ValidatePrefab(request));
        }

        /// <summary>
        /// Verifies AnnotateElements cannot be combined with a prefab path.
        /// </summary>
        [Test]
        public void ValidatePrefab_WithAnnotateElements_Throws()
        {
            ScreenshotSchema request = CreatePrefabRequest();
            request.AnnotateElements = true;

            AssertPrefabRejected(request, "AnnotateElements");
        }

        /// <summary>
        /// Verifies ElementsOnly cannot be combined with a prefab path.
        /// </summary>
        [Test]
        public void ValidatePrefab_WithElementsOnly_Throws()
        {
            ScreenshotSchema request = CreatePrefabRequest();
            request.ElementsOnly = true;

            AssertPrefabRejected(request, "ElementsOnly");
        }

        /// <summary>
        /// Verifies AnnotateRaycastGrid cannot be combined with a prefab path.
        /// </summary>
        [Test]
        public void ValidatePrefab_WithAnnotateRaycastGrid_Throws()
        {
            ScreenshotSchema request = CreatePrefabRequest();
            request.AnnotateRaycastGrid = true;

            AssertPrefabRejected(request, "AnnotateRaycastGrid");
        }

        /// <summary>
        /// Verifies RaycastLayerMask cannot be combined with a prefab path.
        /// </summary>
        [Test]
        public void ValidatePrefab_WithRaycastLayerMask_Throws()
        {
            ScreenshotSchema request = CreatePrefabRequest();
            request.RaycastLayerMask = "Default";

            AssertPrefabRejected(request, "RaycastLayerMask");
        }

        /// <summary>
        /// Verifies prefab resolution scales outside 0.1 to 1.0 are rejected and the bounds themselves are accepted.
        /// </summary>
        [TestCase(0.09f, false, TestName = "ValidatePrefab_WithAScaleBelowTheMinimum_Throws")]
        [TestCase(0.1f, true, TestName = "ValidatePrefab_WithTheMinimumScale_Accepts")]
        [TestCase(1.0f, true, TestName = "ValidatePrefab_WithTheMaximumScale_Accepts")]
        [TestCase(1.01f, false, TestName = "ValidatePrefab_WithAScaleAboveTheMaximum_Throws")]
        public void ValidatePrefab_ChecksTheResolutionScaleRange(float scale, bool accepted)
        {
            ScreenshotSchema request = CreatePrefabRequest();
            request.ResolutionScale = scale;

            if (accepted)
            {
                Assert.DoesNotThrow(() => ScreenshotParameterValidator.ValidatePrefab(request));
                return;
            }

            AssertPrefabRejected(request, "ResolutionScale must be between 0.1 and 1.0");
        }

        /// <summary>
        /// Verifies empty, #RRGGBB, and #RRGGBBAA background colors are accepted.
        /// </summary>
        [TestCase("", TestName = "ValidatePrefab_WithAnEmptyBackgroundColor_Accepts")]
        [TestCase("#336699", TestName = "ValidatePrefab_WithAnRgbBackgroundColor_Accepts")]
        [TestCase("#33669980", TestName = "ValidatePrefab_WithAnRgbaBackgroundColor_Accepts")]
        [TestCase("#00000000", TestName = "ValidatePrefab_WithATransparentBackgroundColor_Accepts")]
        public void ValidatePrefab_WithAWellFormedBackgroundColor_Accepts(string color)
        {
            ScreenshotSchema request = CreatePrefabRequest();
            request.PrefabBackgroundColor = color;

            Assert.DoesNotThrow(() => ScreenshotParameterValidator.ValidatePrefab(request));
        }

        /// <summary>
        /// Verifies background colors outside the #RRGGBB / #RRGGBBAA forms are rejected, including color names.
        /// </summary>
        [TestCase("#FFF", TestName = "ValidatePrefab_WithAShortHexBackgroundColor_Throws")]
        [TestCase("red", TestName = "ValidatePrefab_WithAColorNameBackgroundColor_Throws")]
        [TestCase("#GG0000", TestName = "ValidatePrefab_WithANonHexBackgroundColor_Throws")]
        [TestCase("336699", TestName = "ValidatePrefab_WithABackgroundColorMissingTheHash_Throws")]
        public void ValidatePrefab_WithAMalformedBackgroundColor_Throws(string color)
        {
            ScreenshotSchema request = CreatePrefabRequest();
            request.PrefabBackgroundColor = color;

            AssertPrefabRejected(request, "PrefabBackgroundColor");
        }

        /// <summary>
        /// Verifies WindowName and MatchMode are ignored for a prefab capture.
        /// </summary>
        [Test]
        public void ValidatePrefab_WithAWindowNameAndMatchMode_Accepts()
        {
            ScreenshotSchema request = CreatePrefabRequest();
            request.WindowName = "Scene";
            request.MatchMode = WindowMatchMode.contains;

            Assert.DoesNotThrow(() => ScreenshotParameterValidator.ValidatePrefab(request));
        }

        /// <summary>
        /// Verifies a background color without a prefab path is rejected on the non-prefab path.
        /// </summary>
        [Test]
        public void Validate_WithABackgroundColorAndNoPrefabPath_Throws()
        {
            ScreenshotSchema request = new ScreenshotSchema { PrefabBackgroundColor = "#336699" };

            UnityCliLoopToolParameterValidationException exception =
                Assert.Throws<UnityCliLoopToolParameterValidationException>(() => ScreenshotParameterValidator.Validate(request));
            Assert.That(exception.Message, Does.Contain("PrefabBackgroundColor requires PrefabPath"));
        }

        /// <summary>
        /// Verifies an empty background color resolves to the documented #808080 default.
        /// </summary>
        [Test]
        public void Resolve_WithAnEmptyValue_ReturnsTheDefaultGray()
        {
            Color color = PrefabBackgroundColorParser.Resolve("");

            AssertColor(new Color(0x80 / 255f, 0x80 / 255f, 0x80 / 255f, 1f), color);
        }

        /// <summary>
        /// Verifies a #RRGGBBAA value resolves to its channels including alpha.
        /// </summary>
        [Test]
        public void Resolve_WithAnRgbaValue_ReturnsItsChannels()
        {
            Color color = PrefabBackgroundColorParser.Resolve("#33669980");

            AssertColor(new Color(0x33 / 255f, 0x66 / 255f, 0x99 / 255f, 0x80 / 255f), color);
        }

        private static ScreenshotSchema CreatePrefabRequest()
        {
            return new ScreenshotSchema { PrefabPath = AnyPrefabPath };
        }

        private static void AssertPrefabRejected(ScreenshotSchema request, string expectedMessagePart)
        {
            UnityCliLoopToolParameterValidationException exception =
                Assert.Throws<UnityCliLoopToolParameterValidationException>(() => ScreenshotParameterValidator.ValidatePrefab(request));
            Assert.That(exception.Message, Does.Contain(expectedMessagePart));
        }

        private static void AssertColor(Color expected, Color actual)
        {
            const float tolerance = 0.5f / 255f;
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(tolerance));
        }
    }
}
