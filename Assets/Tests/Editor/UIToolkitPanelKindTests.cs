using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies how panels are told apart as world-space or screen-space. The project's Unity version has no
    /// render mode on PanelSettings, so stand-in types exercise the property lookup and the value comparison.
    /// </summary>
    public sealed class UIToolkitPanelKindTests
    {
        /// <summary>
        /// Verifies default panel settings are not world-space, including on versions without a render mode
        /// property at all.
        /// </summary>
        [Test]
        public void IsWorldSpace_WithDefaultPanelSettings_ShouldReturnFalse()
        {
            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();

            bool isWorldSpace = UIToolkitPanelKind.IsWorldSpace(settings);

            Object.DestroyImmediate(settings);
            Assert.That(isWorldSpace, Is.False);
        }

        /// <summary>
        /// Verifies an internal render mode set to WorldSpace is detected, as on the Unity 6 versions where the
        /// property is not public.
        /// </summary>
        [Test]
        public void HasWorldSpaceRenderMode_WithInternalWorldSpace_ShouldReturnTrue()
        {
            InternalRenderModeSettings settings = new InternalRenderModeSettings(StandInRenderMode.WorldSpace);

            Assert.That(UIToolkitPanelKind.HasWorldSpaceRenderMode(settings), Is.True);
        }

        /// <summary>
        /// Verifies a public render mode set to a screen-space value is not treated as world-space.
        /// </summary>
        [Test]
        public void HasWorldSpaceRenderMode_WithPublicScreenSpaceOverlay_ShouldReturnFalse()
        {
            PublicRenderModeSettings settings = new PublicRenderModeSettings(StandInRenderMode.ScreenSpaceOverlay);

            Assert.That(UIToolkitPanelKind.HasWorldSpaceRenderMode(settings), Is.False);
        }

        /// <summary>
        /// Verifies an object without a render mode property is not treated as world-space.
        /// </summary>
        [Test]
        public void HasWorldSpaceRenderMode_WithoutRenderMode_ShouldReturnFalse()
        {
            Assert.That(UIToolkitPanelKind.HasWorldSpaceRenderMode(new object()), Is.False);
        }

        private enum StandInRenderMode
        {
            ScreenSpaceOverlay,
            WorldSpace
        }

        private sealed class InternalRenderModeSettings
        {
            internal InternalRenderModeSettings(StandInRenderMode renderMode)
            {
                this.renderMode = renderMode;
            }

            // The lower-case name matches the Unity property the lookup searches for.
            internal StandInRenderMode renderMode { get; }
        }

        private sealed class PublicRenderModeSettings
        {
            public PublicRenderModeSettings(StandInRenderMode renderMode)
            {
                this.renderMode = renderMode;
            }

            public StandInRenderMode renderMode { get; }
        }
    }
}
