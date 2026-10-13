#nullable enable
using System;
using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Inverts a panel's screen-to-panel function so panel-space rectangles can be reported in screen pixels.
    /// Only functions that scale and offset each axis independently can be inverted.
    /// </summary>
    internal sealed class UIToolkitScreenMapping
    {
        private const float SAMPLE = 1000f;
        private const float PROBE_X_RATIO = 0.37f;
        private const float PROBE_Y_RATIO = 0.61f;
        private const float MIN_SCALE = 1e-6f;
        private const float LINEARITY_TOLERANCE = 0.01f;

        private readonly Vector2 _origin;
        private readonly float _scaleX;
        private readonly float _scaleY;

        private UIToolkitScreenMapping(Vector2 origin, float scaleX, float scaleY)
        {
            _origin = origin;
            _scaleX = scaleX;
            _scaleY = scaleY;
        }

        // Unity offers no panel-to-screen API, and a panel may replace the screen-to-panel function with any
        // function, so the inverse is derived by sampling it. Returns null when the function is not invertible
        // per axis (non-linear, sheared, rotated, swapped, mirrored, or non-finite), because no screen rectangle would
        // then match what is drawn.
        internal static UIToolkitScreenMapping? TryCreate(Func<Vector2, Vector2> screenTopLeftToPanel)
        {
            if (screenTopLeftToPanel == null)
            {
                throw new ArgumentNullException(nameof(screenTopLeftToPanel));
            }

            Vector2 origin = screenTopLeftToPanel(Vector2.zero);
            Vector2 unitX = screenTopLeftToPanel(new Vector2(SAMPLE, 0f));
            Vector2 unitY = screenTopLeftToPanel(new Vector2(0f, SAMPLE));
            Vector2 probe = new Vector2(SAMPLE * PROBE_X_RATIO, SAMPLE * PROBE_Y_RATIO);
            Vector2 check = screenTopLeftToPanel(probe);
            if (!IsFinite(origin) || !IsFinite(unitX) || !IsFinite(unitY) || !IsFinite(check))
            {
                return null;
            }

            float scaleX = (unitX.x - origin.x) / SAMPLE;
            float scaleY = (unitY.y - origin.y) / SAMPLE;
            // A mirrored axis (negative scale) is rejected too: its inverse would swap the bounds' minimum and
            // maximum.
            if (scaleX < MIN_SCALE || scaleY < MIN_SCALE)
            {
                return null;
            }

            // Shear and rotation put part of one axis into the other, so the fourth point misses the
            // per-axis prediction; no dedicated check is needed for them.
            Vector2 predicted = new Vector2(origin.x + scaleX * probe.x, origin.y + scaleY * probe.y);
            if (Mathf.Abs(predicted.x - check.x) > LINEARITY_TOLERANCE
                || Mathf.Abs(predicted.y - check.y) > LINEARITY_TOLERANCE)
            {
                return null;
            }

            return new UIToolkitScreenMapping(origin, scaleX, scaleY);
        }

        internal Vector2 PanelToScreenTopLeft(Vector2 panelPoint)
        {
            return new Vector2((panelPoint.x - _origin.x) / _scaleX, (panelPoint.y - _origin.y) / _scaleY);
        }

        private static bool IsFinite(Vector2 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y);
        }
    }
}
