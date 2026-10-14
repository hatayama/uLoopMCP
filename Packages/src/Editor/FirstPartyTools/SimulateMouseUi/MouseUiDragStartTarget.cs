#nullable enable
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// What a drag will start on, resolved before anything is sent so that refusals leave no button held.
    /// </summary>
    internal sealed class MouseUiDragStartTarget
    {
        private readonly EventSystem? _eventSystem;
        private readonly RaycastResult _startRaycast;
        private readonly GameObject? _uguiTarget;
        private readonly UIToolkitPointerTarget? _uiToolkitTarget;

        private MouseUiDragStartTarget(
            EventSystem? eventSystem,
            RaycastResult startRaycast,
            GameObject? uguiTarget,
            UIToolkitPointerTarget? uiToolkitTarget)
        {
            _eventSystem = eventSystem;
            _startRaycast = startRaycast;
            _uguiTarget = uguiTarget;
            _uiToolkitTarget = uiToolkitTarget;
        }

        internal static MouseUiDragStartTarget None { get; } = new MouseUiDragStartTarget(null, new RaycastResult(), null, null);

        /// <summary>A uGUI drag; a null target means the raycast hit has no drag handler.</summary>
        internal static MouseUiDragStartTarget Ugui(EventSystem eventSystem, RaycastResult startRaycast, GameObject? target)
        {
            return new MouseUiDragStartTarget(eventSystem, startRaycast, target, null);
        }

        internal static MouseUiDragStartTarget UIToolkit(UIToolkitPointerTarget target)
        {
            return new MouseUiDragStartTarget(null, new RaycastResult(), null, target);
        }

        internal bool HasTarget => _uiToolkitTarget != null || _uguiTarget != null;

        /// <summary>The raycast a uGUI drag presses on; empty for UI Toolkit and for no target.</summary>
        internal RaycastResult StartRaycast => _startRaycast;

        /// <summary>The uGUI drag handler, or null when the drag is not on uGUI or the hit has no handler.</summary>
        internal GameObject? UguiTarget => _uguiTarget;

        /// <summary>The UI Toolkit control's name, or null when the drag is not on UI Toolkit.</summary>
        internal string? UIToolkitElementName => _uiToolkitTarget?.Name;

        /// <summary>Presses on the target and returns the drag in progress.</summary>
        internal IMouseUiDragSession Begin(Vector2 inputStart, Vector2 screenStart)
        {
            if (_uiToolkitTarget != null)
            {
                return UIToolkitDragSession.Begin(_uiToolkitTarget, inputStart);
            }

            if (_uguiTarget == null)
            {
                throw new InvalidOperationException("A drag cannot begin without a target; check HasTarget first.");
            }

            return UguiDragSession.Begin(_eventSystem!, screenStart, _startRaycast, _uguiTarget);
        }
    }
}
