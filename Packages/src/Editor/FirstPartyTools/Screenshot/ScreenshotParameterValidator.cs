using System;
using System.Collections.Generic;
using UnityEngine;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Validates ScreenshotSchema parameters before capture begins.
    /// </summary>
    internal static class ScreenshotParameterValidator
    {
        internal static void Validate(ScreenshotSchema request)
        {
            if (!string.IsNullOrEmpty(request.PrefabBackgroundColor))
            {
                throw new UnityCliLoopToolParameterValidationException("PrefabBackgroundColor requires PrefabPath");
            }

            if (request.CaptureMode != CaptureMode.rendering &&
                string.IsNullOrEmpty(request.WindowName))
            {
                throw new UnityCliLoopToolParameterValidationException("WindowName cannot be null or empty");
            }

            ValidateResolutionScale(request);

            // AnnotateElements, ElementsOnly, and AnnotateRaycastGrid rely on PlayMode rendering pipeline
            if (request.CaptureMode != CaptureMode.rendering)
            {
                if (request.AnnotateElements)
                {
                    throw new UnityCliLoopToolParameterValidationException("AnnotateElements is only supported when CaptureMode=rendering");
                }

                if (request.ElementsOnly)
                {
                    throw new UnityCliLoopToolParameterValidationException("ElementsOnly is only supported when CaptureMode=rendering");
                }

                if (request.AnnotateRaycastGrid)
                {
                    throw new UnityCliLoopToolParameterValidationException("AnnotateRaycastGrid is only supported when CaptureMode=rendering");
                }
            }

            if (request.ElementsOnly &&
                !request.AnnotateElements &&
                !request.AnnotateRaycastGrid)
            {
                throw new UnityCliLoopToolParameterValidationException(
                    "ElementsOnly requires AnnotateElements=true or AnnotateRaycastGrid=true");
            }

            RaycastLayerMaskResolution raycastLayerMaskResolution = ResolveRaycastLayerMask(request);
            if (raycastLayerMaskResolution.HasLayerNames && !request.AnnotateRaycastGrid)
            {
                throw new UnityCliLoopToolParameterValidationException(
                    "RaycastLayerMask requires AnnotateRaycastGrid=true");
            }

            if (!raycastLayerMaskResolution.IsValid)
            {
                throw new UnityCliLoopToolParameterValidationException(
                    CreateInvalidRaycastLayerMaskMessage(raycastLayerMaskResolution));
            }
        }

        /// <summary>
        /// Validates a prefab capture request; callers route here only when PrefabPath is set.
        /// </summary>
        internal static void ValidatePrefab(ScreenshotSchema request)
        {
            if (string.IsNullOrEmpty(request.PrefabPath))
            {
                throw new ArgumentException("ValidatePrefab requires PrefabPath", nameof(request));
            }

            if (request.CaptureMode != CaptureMode.auto)
            {
                throw new UnityCliLoopToolParameterValidationException(
                    "CaptureMode cannot be combined with PrefabPath; prefab screenshots always render the asset in a preview scene");
            }

            if (request.AnnotateElements)
            {
                throw new UnityCliLoopToolParameterValidationException("AnnotateElements cannot be combined with PrefabPath");
            }

            if (request.ElementsOnly)
            {
                throw new UnityCliLoopToolParameterValidationException("ElementsOnly cannot be combined with PrefabPath");
            }

            if (request.AnnotateRaycastGrid)
            {
                throw new UnityCliLoopToolParameterValidationException("AnnotateRaycastGrid cannot be combined with PrefabPath");
            }

            if (!string.IsNullOrEmpty(request.RaycastLayerMask))
            {
                throw new UnityCliLoopToolParameterValidationException("RaycastLayerMask cannot be combined with PrefabPath");
            }

            ValidateResolutionScale(request);

            if (!PrefabBackgroundColorParser.IsValid(request.PrefabBackgroundColor))
            {
                throw new UnityCliLoopToolParameterValidationException(
                    $"PrefabBackgroundColor must be #RRGGBB or #RRGGBBAA, got: {request.PrefabBackgroundColor}");
            }
        }

        private static void ValidateResolutionScale(ScreenshotSchema request)
        {
            if (request.ResolutionScale < 0.1f || request.ResolutionScale > 1.0f)
            {
                throw new UnityCliLoopToolParameterValidationException(
                    $"ResolutionScale must be between 0.1 and 1.0, got: {request.ResolutionScale}");
            }
        }

        internal static RaycastLayerMaskResolution ResolveRaycastLayerMask(ScreenshotSchema request)
        {
            string raycastLayerMask = request.RaycastLayerMask ?? "";
            return RaycastLayerMaskResolver.Resolve(
                raycastLayerMask,
                GetAvailableLayerDefinitions());
        }

        internal static List<RaycastLayerDefinition> GetAvailableLayerDefinitions()
        {
            List<RaycastLayerDefinition> layerDefinitions = new();
            for (int layerIndex = 0; layerIndex <= 31; layerIndex++)
            {
                string layerName = LayerMask.LayerToName(layerIndex);
                if (string.IsNullOrEmpty(layerName))
                {
                    continue;
                }

                layerDefinitions.Add(new RaycastLayerDefinition
                {
                    Name = layerName,
                    Index = layerIndex
                });
            }

            return layerDefinitions;
        }

        internal static string CreateInvalidRaycastLayerMaskMessage(
            RaycastLayerMaskResolution raycastLayerMaskResolution)
        {
            string invalidLayerNames = string.Join(", ", raycastLayerMaskResolution.InvalidLayerNames);
            string validLayerNames = string.Join(", ", raycastLayerMaskResolution.ValidLayerNames);
            if (string.IsNullOrEmpty(validLayerNames))
            {
                validLayerNames = "(none)";
            }

            return $"RaycastLayerMask contains unknown layer name(s): {invalidLayerNames}. Valid layers: {validLayerNames}";
        }
    }
}
