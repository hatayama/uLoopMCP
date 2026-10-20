using NUnit.Framework;
using UnityEditor;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies how the simulated device rotation is chosen from the Game view's shape and the
    /// Player Settings orientation rules.
    /// </summary>
    public sealed class SimulatorRotationDecisionTests
    {
        private static OrientationPolicy AutoRotation(
            bool portrait,
            bool portraitUpsideDown,
            bool landscapeLeft,
            bool landscapeRight)
        {
            return new OrientationPolicy(
                UIOrientation.AutoRotation,
                portrait,
                portraitUpsideDown,
                landscapeLeft,
                landscapeRight);
        }

        private static OrientationPolicy Fixed(UIOrientation orientation)
        {
            // The allowed flags are deliberately all true: a fixed orientation must ignore them.
            return new OrientationPolicy(orientation, true, true, true, true);
        }

        [Test]
        public void Decide_LandscapeViewWithEveryOrientationAllowed_RotatesToLandscapeLeft()
        {
            // Verifies a wide Game view picks Unity's default landscape (270 = LandscapeLeft).
            SimulatorRotationDecisionResult result =
                SimulatorRotationDecision.Decide(1920f, 1080f, AutoRotation(true, true, true, true));

            Assert.That(result.WantsLandscape, Is.True);
            Assert.That(result.RotationDegrees, Is.EqualTo(270));
            Assert.That(result.Warning, Is.Empty);
        }

        [Test]
        public void Decide_LandscapeViewWithOnlyLandscapeRightAllowed_RotatesToLandscapeRight()
        {
            // Verifies LandscapeRight (90) is used when LandscapeLeft is not allowed.
            SimulatorRotationDecisionResult result =
                SimulatorRotationDecision.Decide(1920f, 1080f, AutoRotation(true, true, false, true));

            Assert.That(result.RotationDegrees, Is.EqualTo(90));
            Assert.That(result.Warning, Is.Empty);
        }

        [Test]
        public void Decide_LandscapeViewWithOnlyPortraitAllowed_LeavesRotationUnchangedWithWarning()
        {
            // Verifies no rotation is applied when Player Settings allow no landscape orientation.
            SimulatorRotationDecisionResult result =
                SimulatorRotationDecision.Decide(1920f, 1080f, AutoRotation(true, true, false, false));

            Assert.That(result.WantsLandscape, Is.True);
            Assert.That(result.RotationDegrees, Is.Null);
            Assert.That(result.Warning, Does.Contain("landscape"));
        }

        [Test]
        public void Decide_LandscapeViewWithFixedLandscapeRight_RotatesToLandscapeRight()
        {
            // Verifies a fixed LandscapeRight orientation maps to 90 degrees.
            SimulatorRotationDecisionResult result =
                SimulatorRotationDecision.Decide(1920f, 1080f, Fixed(UIOrientation.LandscapeRight));

            Assert.That(result.RotationDegrees, Is.EqualTo(90));
            Assert.That(result.Warning, Is.Empty);
        }

        [Test]
        public void Decide_LandscapeViewWithFixedPortrait_LeavesRotationUnchangedWithWarning()
        {
            // Verifies a fixed Portrait orientation ignores the allowed flags and refuses landscape.
            SimulatorRotationDecisionResult result =
                SimulatorRotationDecision.Decide(1920f, 1080f, Fixed(UIOrientation.Portrait));

            Assert.That(result.RotationDegrees, Is.Null);
            Assert.That(result.Warning, Does.Contain("landscape"));
        }

        [Test]
        public void Decide_PortraitViewWithEveryOrientationAllowed_RotatesToPortrait()
        {
            // Verifies a tall Game view picks upright portrait (0 degrees).
            SimulatorRotationDecisionResult result =
                SimulatorRotationDecision.Decide(1080f, 1920f, AutoRotation(true, true, true, true));

            Assert.That(result.WantsLandscape, Is.False);
            Assert.That(result.RotationDegrees, Is.EqualTo(0));
            Assert.That(result.Warning, Is.Empty);
        }

        [Test]
        public void Decide_PortraitViewWithFixedLandscapeLeft_LeavesRotationUnchangedWithWarning()
        {
            // Verifies a fixed landscape orientation refuses portrait and says so.
            SimulatorRotationDecisionResult result =
                SimulatorRotationDecision.Decide(1080f, 1920f, Fixed(UIOrientation.LandscapeLeft));

            Assert.That(result.WantsLandscape, Is.False);
            Assert.That(result.RotationDegrees, Is.Null);
            Assert.That(result.Warning, Does.Contain("portrait"));
        }

        [Test]
        public void Decide_PortraitViewWithOnlyUpsideDownAllowed_RotatesToPortraitUpsideDown()
        {
            // Verifies PortraitUpsideDown (180) is used when upright portrait is not allowed.
            SimulatorRotationDecisionResult result =
                SimulatorRotationDecision.Decide(1080f, 1920f, AutoRotation(false, true, true, true));

            Assert.That(result.RotationDegrees, Is.EqualTo(180));
            Assert.That(result.Warning, Is.Empty);
        }

        [Test]
        public void Decide_SquareView_IsTreatedAsPortrait()
        {
            // Verifies a square Game view counts as portrait, so it rotates to 0 degrees.
            SimulatorRotationDecisionResult result =
                SimulatorRotationDecision.Decide(1000f, 1000f, AutoRotation(true, true, true, true));

            Assert.That(result.WantsLandscape, Is.False);
            Assert.That(result.RotationDegrees, Is.EqualTo(0));
        }
    }
}
