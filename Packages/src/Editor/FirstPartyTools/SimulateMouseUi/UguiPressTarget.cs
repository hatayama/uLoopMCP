#nullable enable
using UnityEngine;
using UnityEngine.EventSystems;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Delivers a click or long press to uGUI or physics targets through the EventSystem's handlers. With no
    /// resolved target it reports no hit and sends nothing.
    /// </summary>
    internal sealed class UguiPressTarget : IMouseUiPressTarget
    {
        private readonly ResolvedPointerTargets _resolvedTargets;
        private readonly PointerEventData? _pointerData;
        private readonly bool _shouldReleasePointer;

        internal UguiPressTarget(ResolvedPointerTargets resolvedTargets, PointerEventData? pointerData)
        {
            _resolvedTargets = resolvedTargets;
            _pointerData = pointerData;

            // Captured as plain values: Release can run from cleanup after a timeout, where checking the
            // UnityEngine.Object targets for null is not safe.
            bool shouldReleasePointer = resolvedTargets.RawTarget != null && resolvedTargets.Target != null;
            _shouldReleasePointer = shouldReleasePointer;
            Report = CreateReport(resolvedTargets.Target);
        }

        public MouseUiHitReport Report { get; }

        // Decided by whether the handler sits under a Canvas rather than by raycaster type, because a bypass
        // press has no raycaster and both paths must report alike.
        private static MouseUiHitReport CreateReport(GameObject? target)
        {
            if (target == null)
            {
                return MouseUiHitReport.None;
            }

            string uiSystem = target.GetComponentInParent<Canvas>() != null
                ? MouseUiHitSystem.UGUI
                : MouseUiHitSystem.NonUi;
            return new MouseUiHitReport(target.name, GameObjectPathUtility.GetFullPath(target), uiSystem);
        }

        public void Click()
        {
            if (_resolvedTargets.RawTarget == null)
            {
                return;
            }

            if (_resolvedTargets.PressTarget != null)
            {
                ExecuteEvents.ExecuteHierarchy(
                    _resolvedTargets.RawTarget,
                    _pointerData,
                    ExecuteEvents.pointerDownHandler);
            }

            if (_resolvedTargets.Target != null)
            {
                ExecuteEvents.Execute(
                    _resolvedTargets.Target,
                    _pointerData,
                    ExecuteEvents.pointerUpHandler);
            }

            if (_resolvedTargets.ClickTarget != null)
            {
                ExecuteEvents.Execute(
                    _resolvedTargets.ClickTarget,
                    _pointerData,
                    ExecuteEvents.pointerClickHandler);
            }
        }

        public void PressDown()
        {
            if (_resolvedTargets.RawTarget == null || _resolvedTargets.Target == null)
            {
                return;
            }

            ExecuteEvents.ExecuteHierarchy(
                _resolvedTargets.RawTarget,
                _pointerData,
                ExecuteEvents.pointerDownHandler);
        }

        public void Release()
        {
            if (!_shouldReleasePointer)
            {
                return;
            }

            ExecuteEvents.Execute(_resolvedTargets.Target!, _pointerData, ExecuteEvents.pointerUpHandler);
        }
    }
}
