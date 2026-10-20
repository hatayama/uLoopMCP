using UnityEditor;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Snapshot of the Player Settings orientation rules the simulated screen honors.
    /// </summary>
    internal readonly struct OrientationPolicy
    {
        public OrientationPolicy(
            UIOrientation defaultOrientation,
            bool allowPortrait,
            bool allowPortraitUpsideDown,
            bool allowLandscapeLeft,
            bool allowLandscapeRight)
        {
            DefaultOrientation = defaultOrientation;
            AllowPortrait = allowPortrait;
            AllowPortraitUpsideDown = allowPortraitUpsideDown;
            AllowLandscapeLeft = allowLandscapeLeft;
            AllowLandscapeRight = allowLandscapeRight;
        }

        public UIOrientation DefaultOrientation { get; }
        public bool AllowPortrait { get; }
        public bool AllowPortraitUpsideDown { get; }
        public bool AllowLandscapeLeft { get; }
        public bool AllowLandscapeRight { get; }
    }

    /// <summary>
    /// Outcome of matching the simulated device rotation to the Game view's shape.
    /// </summary>
    internal readonly struct SimulatorRotationDecisionResult
    {
        public SimulatorRotationDecisionResult(bool wantsLandscape, int? rotationDegrees, string warning)
        {
            WantsLandscape = wantsLandscape;
            RotationDegrees = rotationDegrees;
            Warning = warning;
        }

        /// <summary>True when the Game view is wider than tall.</summary>
        public bool WantsLandscape { get; }

        /// <summary>Device rotation to apply, or null when the wanted orientation cannot be rendered.</summary>
        public int? RotationDegrees { get; }

        /// <summary>Empty when the rotation can be applied.</summary>
        public string Warning { get; }
    }

    /// <summary>
    /// Chooses the device rotation that makes the Device Simulator render in the same orientation
    /// as the Game view it replaces.
    /// </summary>
    internal static class SimulatorRotationDecision
    {
        // Device rotation in degrees as the Simulator counts it: 0 is upright, 90 is LandscapeRight,
        // 180 is upside down, 270 is LandscapeLeft (Unity's default landscape).
        private const int UprightDegrees = 0;
        private const int LandscapeRightDegrees = 90;
        private const int PortraitUpsideDownDegrees = 180;
        private const int LandscapeLeftDegrees = 270;

        internal static SimulatorRotationDecisionResult Decide(float width, float height, OrientationPolicy policy)
        {
            // A square view counts as portrait so that a device never rotates for an ambiguous shape.
            bool wantsLandscape = width > height;
            int? degrees = wantsLandscape ? LandscapeDegrees(policy) : PortraitDegrees(policy);
            if (degrees.HasValue)
            {
                return new SimulatorRotationDecisionResult(wantsLandscape, degrees, string.Empty);
            }

            string wanted = wantsLandscape ? "landscape" : "portrait";
            string warning =
                $"The Game view is {wanted} but Player Settings allow no {wanted} orientation; " +
                "the Simulator keeps its current rotation.";
            return new SimulatorRotationDecisionResult(wantsLandscape, null, warning);
        }

        private static int? LandscapeDegrees(OrientationPolicy policy)
        {
            switch (policy.DefaultOrientation)
            {
                case UIOrientation.LandscapeLeft:
                    return LandscapeLeftDegrees;
                case UIOrientation.LandscapeRight:
                    return LandscapeRightDegrees;
                case UIOrientation.AutoRotation:
                    if (policy.AllowLandscapeLeft)
                    {
                        return LandscapeLeftDegrees;
                    }

                    return policy.AllowLandscapeRight ? LandscapeRightDegrees : (int?)null;
                default:
                    return null;
            }
        }

        private static int? PortraitDegrees(OrientationPolicy policy)
        {
            switch (policy.DefaultOrientation)
            {
                case UIOrientation.Portrait:
                    return UprightDegrees;
                case UIOrientation.PortraitUpsideDown:
                    return PortraitUpsideDownDegrees;
                case UIOrientation.AutoRotation:
                    if (policy.AllowPortrait)
                    {
                        return UprightDegrees;
                    }

                    return policy.AllowPortraitUpsideDown ? PortraitUpsideDownDegrees : (int?)null;
                default:
                    return null;
            }
        }
    }
}
