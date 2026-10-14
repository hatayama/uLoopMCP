#nullable enable
using UnityEngine;
using UnityEngine.EventSystems;

using io.github.hatayama.UnityCliLoop.Runtime;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Decides whether a click or long press goes to uGUI or to UI Toolkit. On success a target is always
    /// returned, an empty uGUI one when nothing is under the point; on failure only the response is.
    /// </summary>
    internal static class MouseUiPressTargetResolver
    {
        internal const string NoEventSystemMessage = "No EventSystem found in the scene. Ensure an EventSystem GameObject exists.";
        private const string UIToolkitPathHint =
            " UI Toolkit element paths are not accepted here; click the element at its SimX/SimY without --bypass-raycast.";

        internal static (IMouseUiPressTarget? Target, SimulateMouseUiResponse? Failure) Resolve(
            MouseUiSimulationCommand parameters,
            EventSystem? eventSystem,
            Vector2 inputPos,
            Vector2 screenPos,
            MouseAction action)
        {
            if (parameters.BypassRaycast)
            {
                return ResolveBypass(parameters, eventSystem, inputPos, screenPos, action);
            }

            // UI Toolkit is looked up at the requested point itself, which does not depend on Screen; the uGUI
            // side leaves out the bridge's panel hits, which do.
            UIToolkitPointerTarget? toolkit = UIToolkitScreenElements.FindPointerTarget(inputPos);
            ResolvedPointerTargets uguiResolved = ResolvedPointerTargets.Empty;
            PointerEventData? uguiPointerData = null;
            if (eventSystem != null)
            {
                uguiPointerData = MouseUiPointerTargetResolver.CreatePointerPressData(eventSystem, screenPos, parameters.Button);
                uguiResolved = MouseUiPointerTargetResolver.ResolvePressablePointerTargets(
                    parameters, eventSystem, inputPos, screenPos, uguiPointerData, action);
            }

            // A raycast hit without a handler still counts: real input stops at it and never reaches the panel.
            bool uguiHasRaycastHit = uguiResolved.RawTarget != null;
            if (toolkit == null && !uguiHasRaycastHit)
            {
                if (eventSystem == null)
                {
                    return (null, MouseUiSimulationResponseFactory.CreateFailure(
                        parameters,
                        NoEventSystemNorUIToolkitMessage(inputPos)));
                }

                return (new UguiPressTarget(ResolvedPointerTargets.Empty, uguiPointerData), null);
            }

            if (toolkit != null && (!uguiHasRaycastHit || !IsUguiHitInFront(uguiPointerData!, toolkit)))
            {
                return (new UIToolkitPressTarget(toolkit, inputPos, (int)parameters.Button), null);
            }

            return (new UguiPressTarget(uguiResolved, uguiPointerData), null);
        }

        private static (IMouseUiPressTarget? Target, SimulateMouseUiResponse? Failure) ResolveBypass(
            MouseUiSimulationCommand parameters,
            EventSystem? eventSystem,
            Vector2 inputPos,
            Vector2 screenPos,
            MouseAction action)
        {
            if (eventSystem == null)
            {
                return (null, MouseUiSimulationResponseFactory.CreateFailure(parameters, NoEventSystemMessage));
            }

            PointerEventData pointerData = MouseUiPointerTargetResolver.CreatePointerPressData(eventSystem, screenPos, parameters.Button);
            ResolvedPointerTargets resolved = MouseUiPointerTargetResolver.ResolvePressablePointerTargets(
                parameters, eventSystem, inputPos, screenPos, pointerData, action);
            SimulateMouseUiResponse? failure = resolved.FailureResponse;
            if (failure == null)
            {
                return (new UguiPressTarget(resolved, pointerData), null);
            }

            // Annotation paths of UI Toolkit controls contain '#', which no GameObject path lookup can match.
            if (failure.Message.EndsWith("was not found.", System.StringComparison.Ordinal) && parameters.TargetPath.Contains("#"))
            {
                failure.Message += UIToolkitPathHint;
            }

            return (null, failure);
        }

        internal static string NoEventSystemNorUIToolkitMessage(Vector2 inputPos)
        {
            return $"No EventSystem found in the scene, and no UI Toolkit element is at ({inputPos.x:F1}, {inputPos.y:F1}). Ensure an EventSystem GameObject exists for uGUI.";
        }

        // The press data carries the uGUI raycast hit, whose raycaster decides the order against the panel.
        private static bool IsUguiHitInFront(PointerEventData uguiPointerData, UIToolkitPointerTarget toolkit)
        {
            BaseRaycaster raycaster = uguiPointerData.pointerPressRaycast.module;
            return MouseUiHitPriority.IsUguiInFront(raycaster.sortOrderPriority, toolkit.SortingOrder);
        }
    }
}
