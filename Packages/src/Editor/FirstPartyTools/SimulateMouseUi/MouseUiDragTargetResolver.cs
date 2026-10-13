#nullable enable
using UnityEngine;
using UnityEngine.EventSystems;

using io.github.hatayama.UnityCliLoop.Runtime;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Decides what a Drag or DragStart starts on: a uGUI drag handler, a UI Toolkit control, or nothing. The
    /// choice between uGUI and UI Toolkit follows the same rule as clicks.
    /// </summary>
    internal static class MouseUiDragTargetResolver
    {
        internal static (MouseUiDragStartTarget Start, SimulateMouseUiResponse? FailureResponse) Resolve(
            MouseUiSimulationCommand parameters,
            EventSystem? eventSystem,
            MouseAction action,
            Vector2 inputPosition,
            Vector2 screenPosition)
        {
            if (parameters.BypassRaycast)
            {
                return ResolveBypass(parameters, eventSystem, action, inputPosition);
            }

            // As with clicks, UI Toolkit is looked up at the requested point itself and the uGUI side leaves out
            // the bridge's panel hits, which depend on Screen.
            UIToolkitPointerTarget? toolkit = UIToolkitScreenElements.FindPointerTarget(inputPosition);
            RaycastResult? hit = eventSystem != null
                ? UiRaycastHelper.RaycastUISkippingUIToolkitPanels(screenPosition, eventSystem)
                : null;
            if (toolkit == null && hit == null)
            {
                return eventSystem == null
                    ? (MouseUiDragStartTarget.None, MouseUiSimulationResponseFactory.CreateFailure(
                        parameters,
                        MouseUiPressTargetResolver.NoEventSystemNorUIToolkitMessage(inputPosition)))
                    : (MouseUiDragStartTarget.None, null);
            }

            if (toolkit != null &&
                (hit == null || !MouseUiHitPriority.IsUguiInFront(hit.Value.module.sortOrderPriority, toolkit.SortingOrder)))
            {
                return (MouseUiDragStartTarget.UIToolkit(toolkit), null);
            }

            // Execute dispatches only to the exact target; resolve the actual drag handler up the hierarchy.
            GameObject? target = ExecuteEvents.GetEventHandler<IDragHandler>(hit!.Value.gameObject);
            return (MouseUiDragStartTarget.Ugui(eventSystem!, hit.Value, target), null);
        }

        /// <summary>
        /// Refuses a drop target for a drag on a UI Toolkit control, which has no EventSystem drop handling.
        /// </summary>
        internal static SimulateMouseUiResponse? RefuseDropTargetOnUIToolkit(
            MouseUiSimulationCommand parameters,
            string? uiToolkitElementName)
        {
            if (uiToolkitElementName == null || string.IsNullOrEmpty(parameters.DropTargetPath))
            {
                return null;
            }

            return MouseUiSimulationResponseFactory.CreateFailure(
                parameters,
                $"DropTargetPath applies to uGUI drags only; this drag starts on UI Toolkit element '{uiToolkitElementName}'.");
        }

        private static (MouseUiDragStartTarget Start, SimulateMouseUiResponse? FailureResponse) ResolveBypass(
            MouseUiSimulationCommand parameters,
            EventSystem? eventSystem,
            MouseAction action,
            Vector2 inputPosition)
        {
            if (eventSystem == null)
            {
                return (MouseUiDragStartTarget.None, MouseUiSimulationResponseFactory.CreateFailure(
                    parameters, MouseUiPressTargetResolver.NoEventSystemMessage));
            }

            (GameObject? Target, SimulateMouseUiResponse? FailureResponse) resolution =
                MouseUiPointerTargetResolver.ResolveGameObjectPath(
                    parameters.TargetPath,
                    "TargetPath",
                    action,
                    inputPosition);
            if (resolution.FailureResponse != null)
            {
                return (MouseUiDragStartTarget.None, resolution.FailureResponse);
            }

            GameObject rawTarget = resolution.Target!;
            RaycastResult startRaycast = MouseUiPointerTargetResolver.CreateDirectRaycastResult(rawTarget);
            GameObject? target = ExecuteEvents.GetEventHandler<IDragHandler>(rawTarget);
            return (MouseUiDragStartTarget.Ugui(eventSystem, startRaycast, target), null);
        }
    }
}
