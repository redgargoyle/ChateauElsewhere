using UnityEngine;
using UnityEngine.UI;

public static class CharacterVisualBoundsUtility
{
    private const float MinScreenSize = 0.01f;
    private const float FitTolerancePixels = 0.5f;

    public struct VisualBoundsOptions
    {
        public bool includeInactive;
        public bool includeContainerRectTransforms;
        public bool preferGraphicsAndRenderersOnly;

        public static VisualBoundsOptions Default => new VisualBoundsOptions
        {
            includeInactive = false,
            includeContainerRectTransforms = false,
            preferGraphicsAndRenderersOnly = true
        };
    }

    public readonly struct VisualFitResult
    {
        public VisualFitResult(
            bool success,
            bool usedFallbackDirectScale,
            float beforeHeight,
            float targetHeight,
            float afterHeight,
            Vector3 beforeScale,
            Vector3 afterScale,
            string scaleRootPath,
            string boundsRootPath,
            string primaryVisualPath,
            string diagnostic)
        {
            Success = success;
            UsedFallbackDirectScale = usedFallbackDirectScale;
            BeforeHeight = beforeHeight;
            TargetHeight = targetHeight;
            AfterHeight = afterHeight;
            BeforeScale = beforeScale;
            AfterScale = afterScale;
            ScaleRootPath = scaleRootPath;
            BoundsRootPath = boundsRootPath;
            PrimaryVisualPath = primaryVisualPath;
            Diagnostic = diagnostic;
        }

        public bool Success { get; }
        public bool UsedFallbackDirectScale { get; }
        public float BeforeHeight { get; }
        public float TargetHeight { get; }
        public float AfterHeight { get; }
        public Vector3 BeforeScale { get; }
        public Vector3 AfterScale { get; }
        public string ScaleRootPath { get; }
        public string BoundsRootPath { get; }
        public string PrimaryVisualPath { get; }
        public string Diagnostic { get; }
    }

    public static bool TryGetScreenRect(
        Transform root,
        Camera camera,
        out Rect screenRect,
        bool includeInactive = false)
    {
        VisualBoundsOptions options = VisualBoundsOptions.Default;
        options.includeInactive = includeInactive;
        return TryGetVisualScreenRect(root, camera, out screenRect, out _, out _, options);
    }

    public static bool TryGetScreenRect(
        Transform root,
        Camera camera,
        out Rect screenRect,
        VisualBoundsOptions options)
    {
        return TryGetVisualScreenRect(root, camera, out screenRect, out _, out _, options);
    }

    public static bool TryGetScreenHeight(
        Transform root,
        Camera camera,
        out float height,
        bool includeInactive = false)
    {
        VisualBoundsOptions options = VisualBoundsOptions.Default;
        options.includeInactive = includeInactive;
        return TryGetScreenHeight(root, camera, out height, options);
    }

    public static bool TryGetScreenHeight(
        Transform root,
        Camera camera,
        out float height,
        VisualBoundsOptions options)
    {
        height = 0f;

        if (!TryGetVisualScreenRect(root, camera, out Rect rect, out _, out _, options))
        {
            return false;
        }

        height = rect.height;
        return height > MinScreenSize;
    }

    public static bool TryGetVisualScreenRect(
        Transform root,
        Camera camera,
        out Rect screenRect,
        out Transform primaryVisualTransform,
        out string diagnostic,
        VisualBoundsOptions options)
    {
        screenRect = default;
        primaryVisualTransform = null;
        diagnostic = string.Empty;

        if (root == null)
        {
            diagnostic = "Root missing";
            return false;
        }

        bool hasRect = false;
        bool hasArtRect = false;
        Vector2 min = Vector2.zero;
        Vector2 max = Vector2.zero;
        float primaryHeight = 0f;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(options.includeInactive);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null ||
                !renderer.enabled ||
                (!options.includeInactive && !renderer.gameObject.activeInHierarchy) ||
                !IsValidBounds(renderer.bounds) ||
                camera == null)
            {
                continue;
            }

            if (!TryGetWorldBoundsScreenRect(camera, renderer.bounds, out Rect rendererRect))
            {
                continue;
            }

            EncapsulateRect(rendererRect, ref hasRect, ref min, ref max);
            hasArtRect = true;
            SetPrimaryIfLarger(renderer.transform, rendererRect, ref primaryVisualTransform, ref primaryHeight);
        }

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(options.includeInactive);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];

            if (graphic == null ||
                !graphic.enabled ||
                (!options.includeInactive && !graphic.gameObject.activeInHierarchy) ||
                graphic.rectTransform == null)
            {
                continue;
            }

            if (!TryGetRectTransformScreenRect(graphic.rectTransform, ResolveGraphicCamera(graphic, camera), out Rect graphicRect))
            {
                continue;
            }

            EncapsulateRect(graphicRect, ref hasRect, ref min, ref max);
            hasArtRect = true;
            SetPrimaryIfLarger(graphic.transform, graphicRect, ref primaryVisualTransform, ref primaryHeight);
        }

        if (!hasArtRect &&
            options.includeContainerRectTransforms &&
            !options.preferGraphicsAndRenderersOnly)
        {
            RectTransform[] rectTransforms = root.GetComponentsInChildren<RectTransform>(options.includeInactive);
            for (int i = 0; i < rectTransforms.Length; i++)
            {
                RectTransform rectTransform = rectTransforms[i];

                if (rectTransform == null ||
                    LooksLikeForbiddenContainer(rectTransform) ||
                    (!options.includeInactive && !rectTransform.gameObject.activeInHierarchy) ||
                    !TryGetRectTransformScreenRect(rectTransform, ResolveRectTransformCamera(rectTransform, camera), out Rect rectTransformRect))
                {
                    continue;
                }

                EncapsulateRect(rectTransformRect, ref hasRect, ref min, ref max);
                SetPrimaryIfLarger(rectTransform, rectTransformRect, ref primaryVisualTransform, ref primaryHeight);
            }

            if (hasRect)
            {
                diagnostic = "RectTransform fallback";
            }
        }

        if (!hasRect ||
            max.x - min.x <= MinScreenSize ||
            max.y - min.y <= MinScreenSize)
        {
            screenRect = default;
            diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                ? "No visible Renderer or Graphic bounds found"
                : diagnostic;
            return false;
        }

        screenRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        diagnostic = string.IsNullOrWhiteSpace(diagnostic)
            ? $"Measured visible art via {GetPath(primaryVisualTransform)}"
            : diagnostic;
        return true;
    }

    public static bool TryFitScreenHeight(
        Transform root,
        Camera camera,
        float targetScreenHeight,
        out Vector3 previousLocalScale,
        out Vector3 nextLocalScale,
        int maxPasses = 2)
    {
        bool success = TryScaleTransformForScreenHeight(
            root,
            root,
            camera,
            targetScreenHeight,
            out VisualFitResult result,
            VisualBoundsOptions.Default,
            maxPasses);
        previousLocalScale = result.BeforeScale;
        nextLocalScale = result.AfterScale;
        return success;
    }

    public static bool TryScaleTransformForScreenHeight(
        Transform scaleRoot,
        Transform boundsRoot,
        Camera camera,
        float targetScreenHeight,
        out VisualFitResult result,
        VisualBoundsOptions options,
        int maxPasses = 2)
    {
        Vector3 beforeScale = scaleRoot != null ? scaleRoot.localScale : Vector3.one;
        result = new VisualFitResult(
            false,
            false,
            0f,
            targetScreenHeight,
            0f,
            beforeScale,
            beforeScale,
            GetPath(scaleRoot),
            GetPath(boundsRoot),
            string.Empty,
            string.Empty);

        if (scaleRoot == null)
        {
            result = BuildFitResult(false, false, 0f, targetScreenHeight, 0f, beforeScale, beforeScale, scaleRoot, boundsRoot, null, "Scale root missing");
            return false;
        }

        if (boundsRoot == null)
        {
            result = BuildFitResult(false, false, 0f, targetScreenHeight, 0f, beforeScale, scaleRoot.localScale, scaleRoot, boundsRoot, null, "Bounds root missing");
            return false;
        }

        if (!IsFinite(targetScreenHeight) || targetScreenHeight <= MinScreenSize)
        {
            result = BuildFitResult(false, false, 0f, targetScreenHeight, 0f, beforeScale, scaleRoot.localScale, scaleRoot, boundsRoot, null, "Invalid target height");
            return false;
        }

        if (!TryGetVisualScreenRect(boundsRoot, camera, out Rect beforeRect, out Transform primaryVisual, out string diagnostic, options))
        {
            result = BuildFitResult(false, false, 0f, targetScreenHeight, 0f, beforeScale, scaleRoot.localScale, scaleRoot, boundsRoot, primaryVisual, diagnostic);
            return false;
        }

        float beforeHeight = beforeRect.height;
        int passes = Mathf.Clamp(maxPasses, 1, 4);

        for (int i = 0; i < passes; i++)
        {
            if (!TryGetScreenHeight(boundsRoot, camera, out float currentHeight, options) ||
                currentHeight <= MinScreenSize)
            {
                break;
            }

            if (Mathf.Abs(currentHeight - targetScreenHeight) <= FitTolerancePixels)
            {
                result = BuildFitResult(true, false, beforeHeight, targetScreenHeight, currentHeight, beforeScale, scaleRoot.localScale, scaleRoot, boundsRoot, primaryVisual, diagnostic);
                return true;
            }

            float ratio = targetScreenHeight / currentHeight;

            if (!TryScaleXY(scaleRoot, ratio))
            {
                result = BuildFitResult(false, false, beforeHeight, targetScreenHeight, currentHeight, beforeScale, scaleRoot.localScale, scaleRoot, boundsRoot, primaryVisual, "Exact fit produced invalid scale");
                return false;
            }
        }

        if (TryGetScreenHeight(boundsRoot, camera, out float exactAfterHeight, options) &&
            Mathf.Abs(exactAfterHeight - targetScreenHeight) <= Mathf.Max(FitTolerancePixels, targetScreenHeight * 0.02f))
        {
            result = BuildFitResult(true, false, beforeHeight, targetScreenHeight, exactAfterHeight, beforeScale, scaleRoot.localScale, scaleRoot, boundsRoot, primaryVisual, diagnostic);
            return true;
        }

        scaleRoot.localScale = beforeScale;
        float fallbackRatio = targetScreenHeight / Mathf.Max(MinScreenSize, beforeHeight);

        if (!TryScaleXY(scaleRoot, fallbackRatio))
        {
            result = BuildFitResult(false, true, beforeHeight, targetScreenHeight, exactAfterHeight, beforeScale, scaleRoot.localScale, scaleRoot, boundsRoot, primaryVisual, "Fallback direct scale produced invalid scale");
            return false;
        }

        TryGetScreenHeight(boundsRoot, camera, out float fallbackAfterHeight, options);
        bool movedTowardTarget = MovedTowardTarget(beforeHeight, fallbackAfterHeight, targetScreenHeight);
        bool changedScale = !Approximately(beforeScale, scaleRoot.localScale);
        bool success = changedScale && movedTowardTarget;
        result = BuildFitResult(
            success,
            true,
            beforeHeight,
            targetScreenHeight,
            fallbackAfterHeight,
            beforeScale,
            scaleRoot.localScale,
            scaleRoot,
            boundsRoot,
            primaryVisual,
            success ? "Fallback direct visual scale" : "Fallback direct visual scale did not move height toward target");
        return success;
    }

    private static VisualFitResult BuildFitResult(
        bool success,
        bool usedFallbackDirectScale,
        float beforeHeight,
        float targetHeight,
        float afterHeight,
        Vector3 beforeScale,
        Vector3 afterScale,
        Transform scaleRoot,
        Transform boundsRoot,
        Transform primaryVisual,
        string diagnostic)
    {
        return new VisualFitResult(
            success,
            usedFallbackDirectScale,
            beforeHeight,
            targetHeight,
            afterHeight,
            beforeScale,
            afterScale,
            GetPath(scaleRoot),
            GetPath(boundsRoot),
            GetPath(primaryVisual),
            diagnostic);
    }

    private static Camera ResolveGraphicCamera(Graphic graphic, Camera fallbackCamera)
    {
        if (graphic == null)
        {
            return fallbackCamera;
        }

        Canvas canvas = graphic.canvas != null
            ? graphic.canvas
            : graphic.GetComponentInParent<Canvas>(true);
        return ResolveCanvasCamera(canvas, fallbackCamera);
    }

    private static Camera ResolveRectTransformCamera(RectTransform rectTransform, Camera fallbackCamera)
    {
        Canvas canvas = rectTransform != null ? rectTransform.GetComponentInParent<Canvas>(true) : null;
        return ResolveCanvasCamera(canvas, fallbackCamera);
    }

    private static Camera ResolveCanvasCamera(Canvas canvas, Camera fallbackCamera)
    {
        if (canvas == null)
        {
            return fallbackCamera;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera != null ? canvas.worldCamera : fallbackCamera;
    }

    private static bool TryGetWorldBoundsScreenRect(Camera camera, Bounds bounds, out Rect rect)
    {
        rect = default;

        if (camera == null || !IsValidBounds(bounds))
        {
            return false;
        }

        bool hasRect = false;
        Vector2 min = Vector2.zero;
        Vector2 max = Vector2.zero;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        EncapsulateScreenPoint(camera.WorldToScreenPoint(center + new Vector3(-extents.x, -extents.y, -extents.z)), ref hasRect, ref min, ref max);
        EncapsulateScreenPoint(camera.WorldToScreenPoint(center + new Vector3(-extents.x, -extents.y, extents.z)), ref hasRect, ref min, ref max);
        EncapsulateScreenPoint(camera.WorldToScreenPoint(center + new Vector3(-extents.x, extents.y, -extents.z)), ref hasRect, ref min, ref max);
        EncapsulateScreenPoint(camera.WorldToScreenPoint(center + new Vector3(-extents.x, extents.y, extents.z)), ref hasRect, ref min, ref max);
        EncapsulateScreenPoint(camera.WorldToScreenPoint(center + new Vector3(extents.x, -extents.y, -extents.z)), ref hasRect, ref min, ref max);
        EncapsulateScreenPoint(camera.WorldToScreenPoint(center + new Vector3(extents.x, -extents.y, extents.z)), ref hasRect, ref min, ref max);
        EncapsulateScreenPoint(camera.WorldToScreenPoint(center + new Vector3(extents.x, extents.y, -extents.z)), ref hasRect, ref min, ref max);
        EncapsulateScreenPoint(camera.WorldToScreenPoint(center + new Vector3(extents.x, extents.y, extents.z)), ref hasRect, ref min, ref max);

        if (!hasRect || max.x - min.x <= MinScreenSize || max.y - min.y <= MinScreenSize)
        {
            return false;
        }

        rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }

    private static bool TryGetRectTransformScreenRect(RectTransform rectTransform, Camera camera, out Rect rect)
    {
        rect = default;

        if (rectTransform == null ||
            rectTransform.rect.width <= MinScreenSize ||
            rectTransform.rect.height <= MinScreenSize)
        {
            return false;
        }

        bool hasRect = false;
        Vector2 min = Vector2.zero;
        Vector2 max = Vector2.zero;
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        for (int i = 0; i < corners.Length; i++)
        {
            EncapsulateScreenPoint(RectTransformUtility.WorldToScreenPoint(camera, corners[i]), ref hasRect, ref min, ref max);
        }

        if (!hasRect || max.x - min.x <= MinScreenSize || max.y - min.y <= MinScreenSize)
        {
            return false;
        }

        rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }

    private static void EncapsulateRect(Rect rect, ref bool hasRect, ref Vector2 min, ref Vector2 max)
    {
        if (rect.width <= MinScreenSize || rect.height <= MinScreenSize)
        {
            return;
        }

        Vector2 rectMin = rect.min;
        Vector2 rectMax = rect.max;

        if (!hasRect)
        {
            min = rectMin;
            max = rectMax;
            hasRect = true;
            return;
        }

        min = Vector2.Min(min, rectMin);
        max = Vector2.Max(max, rectMax);
    }

    private static void SetPrimaryIfLarger(
        Transform candidate,
        Rect rect,
        ref Transform primaryVisualTransform,
        ref float primaryHeight)
    {
        if (candidate == null ||
            LooksLikeForbiddenContainer(candidate) ||
            rect.height <= primaryHeight)
        {
            return;
        }

        primaryVisualTransform = candidate;
        primaryHeight = rect.height;
    }

    private static void EncapsulateScreenPoint(
        Vector3 screenPoint,
        ref bool hasRect,
        ref Vector2 min,
        ref Vector2 max)
    {
        if (!IsFinite(screenPoint.x) ||
            !IsFinite(screenPoint.y) ||
            screenPoint.z < 0f)
        {
            return;
        }

        Vector2 point = new Vector2(screenPoint.x, screenPoint.y);

        if (!hasRect)
        {
            min = point;
            max = point;
            hasRect = true;
            return;
        }

        min = Vector2.Min(min, point);
        max = Vector2.Max(max, point);
    }

    private static void EncapsulateScreenPoint(
        Vector2 screenPoint,
        ref bool hasRect,
        ref Vector2 min,
        ref Vector2 max)
    {
        EncapsulateScreenPoint(new Vector3(screenPoint.x, screenPoint.y, 1f), ref hasRect, ref min, ref max);
    }

    private static bool TryScaleXY(Transform target, float ratio)
    {
        if (target == null || !IsFinite(ratio) || ratio <= 0f)
        {
            return false;
        }

        Vector3 currentScale = target.localScale;
        Vector3 nextScale = new Vector3(
            ScaleSignedAxis(currentScale.x, ratio),
            ScaleSignedAxis(currentScale.y, ratio),
            currentScale.z);

        if (!IsFinite(nextScale.x) || !IsFinite(nextScale.y) || !IsFinite(nextScale.z))
        {
            return false;
        }

        target.localScale = nextScale;
        return true;
    }

    private static bool MovedTowardTarget(float beforeHeight, float afterHeight, float targetHeight)
    {
        if (!IsFinite(afterHeight) || afterHeight <= MinScreenSize)
        {
            return false;
        }

        return Mathf.Abs(afterHeight - targetHeight) < Mathf.Abs(beforeHeight - targetHeight);
    }

    private static bool Approximately(Vector3 left, Vector3 right)
    {
        return Mathf.Approximately(left.x, right.x) &&
            Mathf.Approximately(left.y, right.y) &&
            Mathf.Approximately(left.z, right.z);
    }

    private static bool LooksLikeForbiddenContainer(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        string name = target.name;
        return string.Equals(name, "Canvas", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Canvas_Background", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Rooms", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "People", System.StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Room_", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidBounds(Bounds bounds)
    {
        Vector3 size = bounds.size;
        return IsFinite(size.x) &&
            IsFinite(size.y) &&
            IsFinite(size.z) &&
            (size.x > MinScreenSize || size.y > MinScreenSize || size.z > MinScreenSize);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static float ScaleSignedAxis(float value, float ratio)
    {
        if (Mathf.Approximately(value, 0f))
        {
            return ratio;
        }

        return value * ratio;
    }

    private static string GetPath(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        string path = target.name;
        Transform current = target.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
