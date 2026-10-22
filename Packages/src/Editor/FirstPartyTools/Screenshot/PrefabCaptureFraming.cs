using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// What a prefab screenshot is rendered as, which decides its image size and camera.
    /// </summary>
    internal enum PrefabCaptureKind
    {
        UiPart,
        ScreenUi,
        Object3D,
        Flat2D
    }

    /// <summary>
    /// The classification and image size decided for one prefab capture.
    /// </summary>
    internal readonly struct PrefabCaptureFrame
    {
        public PrefabCaptureKind Kind { get; }
        public Vector2Int ImageSize { get; }

        public PrefabCaptureFrame(PrefabCaptureKind kind, Vector2Int imageSize)
        {
            if (imageSize.x < 1 || imageSize.y < 1)
            {
                throw new ArgumentException($"Image size must be at least 1x1, got: {imageSize}", nameof(imageSize));
            }

            Kind = kind;
            ImageSize = imageSize;
        }
    }

    /// <summary>
    /// Classifies a prefab instance in a preview scene and points the capture camera at it.
    /// </summary>
    internal static class PrefabCaptureFraming
    {
        private const float WorldUiCameraDistance = 100f;
        private const float ObjectFieldOfView = 30f;
        private static readonly Quaternion ObjectViewRotation = Quaternion.Euler(20f, -30f, 0f);
        private static readonly Quaternion AddedLightRotation = Quaternion.Euler(50f, -30f, 0f);
        private const float ObjectFitMargin = 1.1f;
        private const float FlatFitMargin = 1.05f;
        private const float FlatDepthRatio = 0.001f;
        private const float FlatCameraDistance = 10f;

        /// <summary>
        /// Rearranges the instance and camera for rendering and returns what was decided; rejects prefabs with nothing to capture.
        /// </summary>
        internal static PrefabCaptureFrame Arrange(GameObject instanceRoot, Camera camera, Vector2Int gameViewSize)
        {
            if (instanceRoot == null)
            {
                throw new ArgumentNullException(nameof(instanceRoot));
            }

            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            if (gameViewSize.x < 1 || gameViewSize.y < 1)
            {
                throw new ArgumentException($"Game View size must be at least 1x1, got: {gameViewSize}", nameof(gameViewSize));
            }

            // A camera bound to another scene would render the user's scene instead of the instance.
            if (camera.scene != instanceRoot.scene)
            {
                throw new ArgumentException("Camera must render the scene that holds the prefab instance", nameof(camera));
            }

            // A prefab saved inactive still has content worth capturing; children keep their saved state.
            if (!instanceRoot.activeSelf)
            {
                instanceRoot.SetActive(true);
            }

            // Screen UI is checked first so a RectTransform root with a screen-space Canvas keeps its in-game layout.
            List<Canvas> screenCanvases = CollectTopCanvases(instanceRoot, false);
            if (screenCanvases.Count > 0)
            {
                return ArrangeScreenUi(screenCanvases, camera, gameViewSize);
            }

            if (instanceRoot.transform is RectTransform)
            {
                return ArrangeUiPart(instanceRoot, camera, gameViewSize);
            }

            return ArrangeObject(instanceRoot, camera, gameViewSize);
        }

        private static PrefabCaptureFrame ArrangeScreenUi(List<Canvas> screenCanvases, Camera camera, Vector2Int gameViewSize)
        {
            foreach (Canvas canvas in screenCanvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }

            camera.orthographic = true;
            camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 10f;
            return new PrefabCaptureFrame(PrefabCaptureKind.ScreenUi, gameViewSize);
        }

        // Returns only outermost Canvases. Nested canvases follow their root canvas and Unity ignores
        // renderMode/worldCamera assignments on them; skipping them keeps only the canvases that decide how the UI renders.
        private static List<Canvas> CollectTopCanvases(GameObject root, bool includeWorldSpace)
        {
            List<Canvas> canvases = new();
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(false))
            {
                if (!canvas.enabled)
                {
                    continue;
                }

                if (HasCanvasAncestorWithin(canvas.transform, root.transform))
                {
                    continue;
                }

                bool isWorld = canvas.renderMode == RenderMode.WorldSpace;
                if (isWorld != includeWorldSpace)
                {
                    continue;
                }

                canvases.Add(canvas);
            }

            return canvases;
        }

        private static bool HasCanvasAncestorWithin(Transform transform, Transform root)
        {
            if (transform == root)
            {
                return false;
            }

            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            {
                if (parent.GetComponent<Canvas>() != null)
                {
                    return true;
                }

                if (parent == root)
                {
                    break;
                }
            }

            return false;
        }

        private static PrefabCaptureFrame ArrangeUiPart(GameObject root, Camera camera, Vector2Int gameViewSize)
        {
            GameObject wrapper = new("PrefabCaptureCanvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(wrapper, root.scene);
            Canvas wrapperCanvas = wrapper.GetComponent<Canvas>();
            wrapperCanvas.renderMode = RenderMode.WorldSpace;
            wrapperCanvas.worldCamera = camera;
            RectTransform wrapperRect = (RectTransform)wrapper.transform;
            // A stretched UI root spreads to the Game View size, as it would under a full-screen Canvas.
            wrapperRect.sizeDelta = gameViewSize;
            wrapperRect.position = Vector3.zero;
            root.transform.SetParent(wrapper.transform, false);
            // LayoutGroup and ContentSizeFitter sizes must settle before the rects are measured.
            Canvas.ForceUpdateCanvases();

            Rect union = UnionOfWorldRects(root);
            Vector3 rootScale = root.transform.lossyScale;
            float unitsPerPixelX = Mathf.Abs(rootScale.x);
            float unitsPerPixelY = Mathf.Abs(rootScale.y);
            // World corners include the root's scale, so pixels are counted in UI units (the size at scale 1).
            // A 100x100 world-space Canvas saved at localScale 0.01 is 1x1 in the world but captured at 100x100 px.
            if (unitsPerPixelX <= 0f || unitsPerPixelY <= 0f)
            {
                throw new UnityCliLoopToolParameterValidationException("Prefab has no visible UI area to capture");
            }

            Vector2 pixelSize = new(union.width / unitsPerPixelX, union.height / unitsPerPixelY);
            if (pixelSize.x < 1f || pixelSize.y < 1f)
            {
                throw new UnityCliLoopToolParameterValidationException("Prefab has no visible UI area to capture");
            }

            camera.orthographic = true;
            camera.orthographicSize = union.height / 2f;
            camera.transform.SetPositionAndRotation(
                new Vector3(union.center.x, union.center.y, -WorldUiCameraDistance),
                Quaternion.identity);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = WorldUiCameraDistance * 2f;
            return new PrefabCaptureFrame(
                PrefabCaptureKind.UiPart,
                new Vector2Int(Mathf.CeilToInt(pixelSize.x), Mathf.CeilToInt(pixelSize.y)));
        }

        private static Rect UnionOfWorldRects(GameObject root)
        {
            Vector3[] corners = new Vector3[4];
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(false))
            {
                rect.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    minX = Mathf.Min(minX, corner.x);
                    minY = Mathf.Min(minY, corner.y);
                    maxX = Mathf.Max(maxX, corner.x);
                    maxY = Mathf.Max(maxY, corner.y);
                }
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static PrefabCaptureFrame ArrangeObject(GameObject root, Camera camera, Vector2Int gameViewSize)
        {
            Bounds? bounds = CollectRenderableBounds(root);
            if (bounds == null)
            {
                throw new UnityCliLoopToolParameterValidationException("Prefab has no Renderer or UI to capture");
            }

            if (!HasEnabledLight(root))
            {
                AddDirectionalLight(root.scene);
            }

            float aspect = gameViewSize.x / (float)gameViewSize.y;
            Bounds b = bounds.Value;
            // Things without depth, such as sprites, are framed straight on so they are not flattened by an angled view.
            if (b.size.z <= FlatDepthRatio * Mathf.Max(b.size.x, b.size.y))
            {
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(b.size.y / 2f, b.size.x / (2f * aspect)) * FlatFitMargin;
                camera.transform.SetPositionAndRotation(b.center - Vector3.forward * FlatCameraDistance, Quaternion.identity);
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = FlatCameraDistance * 2f;
                return new PrefabCaptureFrame(PrefabCaptureKind.Flat2D, gameViewSize);
            }

            float radius = b.extents.magnitude;
            float vertical = ObjectFieldOfView * Mathf.Deg2Rad;
            float horizontal = 2f * Mathf.Atan(Mathf.Tan(vertical / 2f) * aspect);
            float distance = radius / Mathf.Sin(Mathf.Min(vertical, horizontal) / 2f) * ObjectFitMargin;
            Vector3 direction = ObjectViewRotation * Vector3.forward;
            camera.orthographic = false;
            camera.fieldOfView = ObjectFieldOfView;
            camera.transform.SetPositionAndRotation(b.center - direction * distance, Quaternion.LookRotation(direction));
            camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
            camera.farClipPlane = distance + radius * 2f;
            return new PrefabCaptureFrame(PrefabCaptureKind.Object3D, gameViewSize);
        }

        private static Bounds? CollectRenderableBounds(GameObject root)
        {
            Bounds? bounds = null;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(false))
            {
                if (!renderer.enabled)
                {
                    continue;
                }

                bounds = Encapsulate(bounds, renderer.bounds);
            }

            Vector3[] corners = new Vector3[4];
            foreach (Canvas canvas in CollectTopCanvases(root, true))
            {
                ((RectTransform)canvas.transform).GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    bounds = Encapsulate(bounds, new Bounds(corner, Vector3.zero));
                }
            }

            return bounds;
        }

        private static Bounds Encapsulate(Bounds? current, Bounds addition)
        {
            if (current == null)
            {
                return addition;
            }

            Bounds result = current.Value;
            result.Encapsulate(addition);
            return result;
        }

        private static bool HasEnabledLight(GameObject root)
        {
            foreach (Light light in root.GetComponentsInChildren<Light>(false))
            {
                if (light.enabled)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddDirectionalLight(Scene scene)
        {
            GameObject lightObject = new("PrefabCaptureLight");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            lightObject.transform.rotation = AddedLightRotation;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
        }
    }
}
