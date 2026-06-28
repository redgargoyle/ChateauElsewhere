using UnityEngine;
using UnityEngine.UI;

public static class CharacterVisualBoundsUtility
{
    private const float MinScreenSize = 0.01f;
    private const float FitTolerancePixels = 0.5f;

    public static bool TryGetScreenRect(
        Transform root,
        Camera camera,
        out Rect screenRect,
        bool includeInactive = false)
    {
        screenRect = default;

        if (root == null || camera == null)
        {
            return false;
        }

        bool hasRect = false;
        Vector2 min = Vector2.zero;
        Vector2 max = Vector2.zero;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null ||
                !renderer.enabled ||
                (!includeInactive && !renderer.gameObject.activeInHierarchy) ||
                !IsValidBounds(renderer.bounds))
            {
                continue;
            }

            EncapsulateWorldBounds(camera, renderer.bounds, ref hasRect, ref min, ref max);
        }

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(includeInactive);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];

            if (graphic == null ||
                !graphic.enabled ||
                (!includeInactive && !graphic.gameObject.activeInHierarchy))
            {
                continue;
            }

            EncapsulateRectTransform(camera, graphic.rectTransform, ref hasRect, ref min, ref max);
        }

        RectTransform[] rectTransforms = root.GetComponentsInChildren<RectTransform>(includeInactive);
        for (int i = 0; i < rectTransforms.Length; i++)
        {
            RectTransform rectTransform = rectTransforms[i];

            if (rectTransform == null ||
                (!includeInactive && !rectTransform.gameObject.activeInHierarchy))
            {
                continue;
            }

            EncapsulateRectTransform(camera, rectTransform, ref hasRect, ref min, ref max);
        }

        if (!hasRect ||
            max.x - min.x <= MinScreenSize ||
            max.y - min.y <= MinScreenSize)
        {
            screenRect = default;
            return false;
        }

        screenRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }

    public static bool TryGetScreenHeight(
        Transform root,
        Camera camera,
        out float height,
        bool includeInactive = false)
    {
        height = 0f;

        if (!TryGetScreenRect(root, camera, out Rect rect, includeInactive))
        {
            return false;
        }

        height = rect.height;
        return height > MinScreenSize;
    }

    public static bool TryFitScreenHeight(
        Transform root,
        Camera camera,
        float targetScreenHeight,
        out Vector3 previousLocalScale,
        out Vector3 nextLocalScale,
        int maxPasses = 2)
    {
        previousLocalScale = root != null ? root.localScale : Vector3.one;
        nextLocalScale = previousLocalScale;

        if (root == null ||
            camera == null ||
            !IsFinite(targetScreenHeight) ||
            targetScreenHeight <= MinScreenSize)
        {
            return false;
        }

        int passes = Mathf.Clamp(maxPasses, 1, 4);

        for (int i = 0; i < passes; i++)
        {
            if (!TryGetScreenHeight(root, camera, out float currentHeight) ||
                currentHeight <= MinScreenSize)
            {
                nextLocalScale = root.localScale;
                return false;
            }

            float delta = Mathf.Abs(currentHeight - targetScreenHeight);
            if (delta <= FitTolerancePixels)
            {
                nextLocalScale = root.localScale;
                return true;
            }

            float ratio = targetScreenHeight / currentHeight;
            if (!IsFinite(ratio) || ratio <= 0f)
            {
                nextLocalScale = root.localScale;
                return false;
            }

            Vector3 currentScale = root.localScale;
            root.localScale = new Vector3(
                ScaleSignedAxis(currentScale.x, ratio),
                ScaleSignedAxis(currentScale.y, ratio),
                currentScale.z);
        }

        nextLocalScale = root.localScale;
        return TryGetScreenHeight(root, camera, out float finalHeight) &&
            Mathf.Abs(finalHeight - targetScreenHeight) <= Mathf.Max(FitTolerancePixels, targetScreenHeight * 0.02f);
    }

    private static void EncapsulateWorldBounds(
        Camera camera,
        Bounds bounds,
        ref bool hasRect,
        ref Vector2 min,
        ref Vector2 max)
    {
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
    }

    private static void EncapsulateRectTransform(
        Camera camera,
        RectTransform rectTransform,
        ref bool hasRect,
        ref Vector2 min,
        ref Vector2 max)
    {
        if (rectTransform == null ||
            rectTransform.rect.width <= MinScreenSize ||
            rectTransform.rect.height <= MinScreenSize)
        {
            return;
        }

        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        for (int i = 0; i < corners.Length; i++)
        {
            EncapsulateScreenPoint(RectTransformUtility.WorldToScreenPoint(camera, corners[i]), ref hasRect, ref min, ref max);
        }
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
}
