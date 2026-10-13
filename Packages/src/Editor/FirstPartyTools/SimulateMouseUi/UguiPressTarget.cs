#nullable enable
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
            Report = new MouseUiHitReport(resolvedTargets.Target?.name, null, null);
        }

        public MouseUiHitReport Report { get; }

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
