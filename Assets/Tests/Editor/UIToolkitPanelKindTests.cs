using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies how panels are told apart as world-space or screen-space.
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
    }
}
