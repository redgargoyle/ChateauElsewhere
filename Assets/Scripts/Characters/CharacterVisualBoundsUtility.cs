using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class CharacterVisualBoundsUtility
{
    private const float MinScreenSize = 0.01f;
    private const float FitTolerancePixels = 0.75f;

    private static readonly string[] ForbiddenContainerNames =
    {
        "Canvas",
        "Canvas_Background",
        "Rooms",
        "People",
        "RoomContentGroup"
    };

    private static readonly string[] IgnoredVisualNameFragments =
    {
        "Shadow",
        "Bubble",
        "Speech",
        "Thought",
        "Cursor",
        "Marker",
        "Icon",
        "Tooltip",
        "Prompt",
        "Interact",
        "Highlight"
    };

    public readonly struct CharacterVisualTarget
    {
        public CharacterVisualTarget(
            Transform boundsRoot,
            Transform scaleRoot,
            Transform primaryVisual,
            Rect screenRect,
            float screenHeight,
            string diagnostic)
        {
            BoundsRoot = boundsRoot;
            ScaleRoot = scaleRoot;
            PrimaryVisual = primaryVisual;
            ScreenRect = screenRect;
            ScreenHeight = screenHeight;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public Transform BoundsRoot { get; }
        public Transform ScaleRoot { get; }
        public Transform PrimaryVisual { get; }
        public Rect ScreenRect { get; }
        public float ScreenHeight { get; }
        public string Diagnostic { get; }
    }

    public readonly struct CharacterVisualFitResult
    {
        public CharacterVisualFitResult(
            float beforeHeight,
            float targetHeight,
            float afterHeight,
            Vector3 beforeScale,
            Vector3 afterScale,
            Transform primaryVisual,
            bool usedFallback,
            string diagnostic)
        {
            BeforeHeight = beforeHeight;
            TargetHeight = targetHeight;
            AfterHeight = afterHeight;
            BeforeScale = beforeScale;
            AfterScale = afterScale;
            PrimaryVisual = primaryVisual;
            UsedFallback = usedFallback;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public float BeforeHeight { get; }
        public float TargetHeight { get; }
        public float AfterHeight { get; }
        public Vector3 BeforeScale { get; }
        public Vector3 AfterScale { get; }
        public Transform PrimaryVisual { get; }
        public bool UsedFallback { get; }
        public string Diagnostic { get; }
    }

    public static bool TryResolveCharacterVisualTarget(
        Transform candidateRoot,
        Camera camera,
        out CharacterVisualTarget target,
        bool includeInactive = false)
    {
        target = default;

        if (candidateRoot == null)
        {
            return false;
        }

        List<VisualCandidate> candidates = new List<VisualCandidate>();
        CollectRendererCandidates(candidateRoot, camera, includeInactive, candidates);
        CollectGraphicCandidates(candidateRoot, camera, includeInactive, candidates);

        bool usedRectTransformFallback = false;

        if (candidates.Count == 0)
        {
            CollectRectTransformFallbackCandidates(candidateRoot, camera, includeInactive, candidates);
            usedRectTransformFallback = candidates.Count > 0;
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        VisualCandidate primary = candidates[0];

        for (int i = 1; i < candidates.Count; i++)
        {
            if (candidates[i].Area > primary.Area)
            {
                primary = candidates[i];
            }
        }

        Transform boundsRoot = primary.Transform;
        Rect boundsRect = primary.ScreenRect;
        string diagnostic = primary.Source;

        if (candidates.Count > 1)
        {
            Transform commonRoot = GetLowestCommonAncestor(candidates);

            if (commonRoot != null && !LooksLikeForbiddenContainer(commonRoot))
            {
                boundsRoot = commonRoot;
                boundsRect = CombineRects(candidates);
                diagnostic = usedRectTransformFallback
                    ? $"RectTransform fallback on {candidates.Count} visual target(s)"
                    : $"Visible art target from {candidates.Count} renderer/graphic target(s)";
            }
            else
            {
                diagnostic = usedRectTransformFallback
                    ? "RectTransform fallback; broad common container ignored"
                    : "Visible art target; broad common container ignored";
            }
        }
        else if (usedRectTransformFallback)
        {
            diagnostic = $"RectTransform fallback: {primary.Transform.name}";
        }

        target = new CharacterVisualTarget(
            boundsRoot,
            boundsRoot,
            primary.Transform,
            boundsRect,
            boundsRect.height,
            diagnostic);
        return boundsRect.height > MinScreenSize;
    }

    public static bool TryGetScreenRect(
        Transform root,
        Camera camera,
        out Rect screenRect,
        bool includeInactive = false)
    {
        screenRect = default;

        if (!TryResolveCharacterVisualTarget(root, camera, out CharacterVisualTarget target, includeInactive))
        {
            return false;
        }

        screenRect = target.ScreenRect;
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

    public static bool TryApplyTargetScreenHeight(
        Transform scaleRoot,
        Transform boundsRoot,
        Camera camera,
        float targetHeight,
        out CharacterVisualFitResult result,
        bool includeInactive = false)
    {
        result = default;

        if (scaleRoot == null ||
            boundsRoot == null ||
            !IsFinite(targetHeight) ||
            targetHeight <= MinScreenSize)
        {
            result = new CharacterVisualFitResult(
                0f,
                targetHeight,
                0f,
                scaleRoot != null ? scaleRoot.localScale : Vector3.one,
                scaleRoot != null ? scaleRoot.localScale : Vector3.one,
                null,
                false,
                "Invalid scale root, bounds root, or target height");
            return false;
        }

        if (!TryResolveCharacterVisualTarget(boundsRoot, camera, out CharacterVisualTarget target, includeInactive))
        {
            result = new CharacterVisualFitResult(
                0f,
                targetHeight,
                0f,
                scaleRoot.localScale,
                scaleRoot.localScale,
                null,
                false,
                "No Renderer or Graphic visual bounds found");
            return false;
        }

        float beforeHeight = target.ScreenHeight;
        Vector3 rootBeforeScale = scaleRoot.localScale;

        if (beforeHeight <= MinScreenSize)
        {
            result = new CharacterVisualFitResult(
                beforeHeight,
                targetHeight,
                beforeHeight,
                rootBeforeScale,
                rootBeforeScale,
                target.PrimaryVisual,
                false,
                target.Diagnostic);
            return false;
        }

        if (Mathf.Abs(beforeHeight - targetHeight) <= Mathf.Max(FitTolerancePixels, targetHeight * 0.04f))
        {
            result = new CharacterVisualFitResult(
                beforeHeight,
                targetHeight,
                beforeHeight,
                rootBeforeScale,
                rootBeforeScale,
                target.PrimaryVisual,
                false,
                target.Diagnostic);
            return true;
        }

        if (!LooksLikeForbiddenContainer(scaleRoot))
        {
            float ratio = targetHeight / beforeHeight;

            if (IsFinite(ratio) && ratio > 0f)
            {
                scaleRoot.localScale = ScaleXY(rootBeforeScale, ratio);

                if (TryResolveCharacterVisualTarget(boundsRoot, camera, out CharacterVisualTarget afterTarget, includeInactive) &&
                    DidScaleMoveVisualHeight(beforeHeight, afterTarget.ScreenHeight, targetHeight, rootBeforeScale, scaleRoot.localScale))
                {
                    result = new CharacterVisualFitResult(
                        beforeHeight,
                        targetHeight,
                        afterTarget.ScreenHeight,
                        rootBeforeScale,
                        scaleRoot.localScale,
                        afterTarget.PrimaryVisual,
                        false,
                        afterTarget.Diagnostic);
                    return true;
                }
            }

            scaleRoot.localScale = rootBeforeScale;
        }

        Transform primaryVisual = target.PrimaryVisual;

        if (primaryVisual == null || primaryVisual == scaleRoot || LooksLikeForbiddenContainer(primaryVisual))
        {
            result = new CharacterVisualFitResult(
                beforeHeight,
                targetHeight,
                beforeHeight,
                rootBeforeScale,
                scaleRoot.localScale,
                primaryVisual,
                false,
                LooksLikeForbiddenContainer(scaleRoot)
                    ? "Scale root is a forbidden broad container"
                    : "Scaling root did not affect measured visual bounds");
            return false;
        }

        Vector3 primaryBeforeScale = primaryVisual.localScale;
        float primaryRatio = targetHeight / beforeHeight;

        if (!IsFinite(primaryRatio) || primaryRatio <= 0f)
        {
            result = new CharacterVisualFitResult(
                beforeHeight,
                targetHeight,
                beforeHeight,
                primaryBeforeScale,
                primaryBeforeScale,
                primaryVisual,
                true,
                "Invalid primary visual scale ratio");
            return false;
        }

        primaryVisual.localScale = ScaleXY(primaryBeforeScale, primaryRatio);

        if (TryResolveCharacterVisualTarget(boundsRoot, camera, out CharacterVisualTarget primaryAfterTarget, includeInactive) &&
            DidScaleMoveVisualHeight(beforeHeight, primaryAfterTarget.ScreenHeight, targetHeight, primaryBeforeScale, primaryVisual.localScale))
        {
            result = new CharacterVisualFitResult(
                beforeHeight,
                targetHeight,
                primaryAfterTarget.ScreenHeight,
                primaryBeforeScale,
                primaryVisual.localScale,
                primaryAfterTarget.PrimaryVisual,
                true,
                $"Used primary visual fallback. {primaryAfterTarget.Diagnostic}");
            return true;
        }

        primaryVisual.localScale = primaryBeforeScale;
        result = new CharacterVisualFitResult(
            beforeHeight,
            targetHeight,
            beforeHeight,
            primaryBeforeScale,
            primaryBeforeScale,
            primaryVisual,
            true,
            "Scaling root and primary visual did not affect measured visual bounds");
        return false;
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

        if (!TryApplyTargetScreenHeight(root, root, camera, targetScreenHeight, out CharacterVisualFitResult result))
        {
            nextLocalScale = root != null ? root.localScale : nextLocalScale;
            return false;
        }

        previousLocalScale = result.BeforeScale;
        nextLocalScale = result.AfterScale;
        return true;
    }

    public static bool LooksLikeForbiddenContainer(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        string name = target.name;

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (name.StartsWith("Room_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (int i = 0; i < ForbiddenContainerNames.Length; i++)
        {
            if (string.Equals(name, ForbiddenContainerNames[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool LooksLikeIgnoredVisual(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        string name = target.name;

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        for (int i = 0; i < IgnoredVisualNameFragments.Length; i++)
        {
            if (name.IndexOf(IgnoredVisualNameFragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void CollectRendererCandidates(
        Transform root,
        Camera camera,
        bool includeInactive,
        List<VisualCandidate> candidates)
    {
        if (camera == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null ||
                !renderer.enabled ||
                (!includeInactive && !renderer.gameObject.activeInHierarchy) ||
                LooksLikeIgnoredVisual(renderer.transform) ||
                LooksLikeForbiddenContainer(renderer.transform) ||
                !IsValidBounds(renderer.bounds) ||
                !TryGetWorldBoundsScreenRect(camera, renderer.bounds, out Rect screenRect))
            {
                continue;
            }

            candidates.Add(new VisualCandidate(renderer.transform, screenRect, "Renderer bounds"));
        }
    }

    private static void CollectGraphicCandidates(
        Transform root,
        Camera camera,
        bool includeInactive,
        List<VisualCandidate> candidates)
    {
        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(includeInactive);

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];

            if (graphic == null ||
                !graphic.enabled ||
                (!includeInactive && !graphic.gameObject.activeInHierarchy) ||
                graphic.color.a <= 0.001f ||
                graphic.rectTransform == null ||
                LooksLikeIgnoredVisual(graphic.transform) ||
                LooksLikeForbiddenContainer(graphic.transform) ||
                !TryGetRectTransformScreenRect(graphic.rectTransform, ResolveGraphicCamera(graphic, camera), out Rect screenRect))
            {
                continue;
            }

            candidates.Add(new VisualCandidate(graphic.transform, screenRect, "Graphic rect"));
        }
    }

    private static void CollectRectTransformFallbackCandidates(
        Transform root,
        Camera camera,
        bool includeInactive,
        List<VisualCandidate> candidates)
    {
        RectTransform[] rectTransforms = root.GetComponentsInChildren<RectTransform>(includeInactive);

        for (int i = 0; i < rectTransforms.Length; i++)
        {
            RectTransform rectTransform = rectTransforms[i];

            if (rectTransform == null ||
                (!includeInactive && !rectTransform.gameObject.activeInHierarchy) ||
                LooksLikeIgnoredVisual(rectTransform) ||
                LooksLikeForbiddenContainer(rectTransform) ||
                !TryGetRectTransformScreenRect(rectTransform, camera, out Rect screenRect))
            {
                continue;
            }

            candidates.Add(new VisualCandidate(rectTransform, screenRect, "RectTransform fallback"));
        }
    }

    private static Camera ResolveGraphicCamera(Graphic graphic, Camera suppliedCamera)
    {
        if (graphic == null)
        {
            return suppliedCamera;
        }

        Canvas canvas = graphic.canvas;

        if (canvas == null)
        {
            return suppliedCamera;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera != null ? canvas.worldCamera : suppliedCamera;
    }

    private static bool TryGetWorldBoundsScreenRect(Camera camera, Bounds bounds, out Rect screenRect)
    {
        screenRect = default;

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

        return TryBuildRect(hasRect, min, max, out screenRect);
    }

    private static bool TryGetRectTransformScreenRect(
        RectTransform rectTransform,
        Camera camera,
        out Rect screenRect)
    {
        screenRect = default;

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

        return TryBuildRect(hasRect, min, max, out screenRect);
    }

    private static bool TryBuildRect(bool hasRect, Vector2 min, Vector2 max, out Rect screenRect)
    {
        screenRect = default;

        if (!hasRect ||
            max.x - min.x <= MinScreenSize ||
            max.y - min.y <= MinScreenSize)
        {
            return false;
        }

        screenRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
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

    private static Rect CombineRects(List<VisualCandidate> candidates)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return default;
        }

        Vector2 min = new Vector2(candidates[0].ScreenRect.xMin, candidates[0].ScreenRect.yMin);
        Vector2 max = new Vector2(candidates[0].ScreenRect.xMax, candidates[0].ScreenRect.yMax);

        for (int i = 1; i < candidates.Count; i++)
        {
            Rect rect = candidates[i].ScreenRect;
            min = Vector2.Min(min, new Vector2(rect.xMin, rect.yMin));
            max = Vector2.Max(max, new Vector2(rect.xMax, rect.yMax));
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static Transform GetLowestCommonAncestor(List<VisualCandidate> candidates)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return null;
        }

        Transform common = candidates[0].Transform;

        for (int i = 1; i < candidates.Count && common != null; i++)
        {
            common = GetLowestCommonAncestor(common, candidates[i].Transform);
        }

        return common;
    }

    private static Transform GetLowestCommonAncestor(Transform first, Transform second)
    {
        if (first == null || second == null)
        {
            return null;
        }

        HashSet<Transform> ancestors = new HashSet<Transform>();
        Transform current = first;

        while (current != null)
        {
            ancestors.Add(current);
            current = current.parent;
        }

        current = second;

        while (current != null)
        {
            if (ancestors.Contains(current))
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }

    private static bool DidScaleMoveVisualHeight(
        float beforeHeight,
        float afterHeight,
        float targetHeight,
        Vector3 beforeScale,
        Vector3 afterScale)
    {
        if (afterHeight <= MinScreenSize || Approximately(beforeScale, afterScale))
        {
            return false;
        }

        if (Mathf.Abs(afterHeight - targetHeight) <= Mathf.Max(FitTolerancePixels, targetHeight * 0.04f))
        {
            return true;
        }

        if (targetHeight > beforeHeight)
        {
            return afterHeight > beforeHeight + FitTolerancePixels;
        }

        if (targetHeight < beforeHeight)
        {
            return afterHeight < beforeHeight - FitTolerancePixels;
        }

        return true;
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

    private static Vector3 ScaleXY(Vector3 scale, float ratio)
    {
        return new Vector3(
            ScaleSignedAxis(scale.x, ratio),
            ScaleSignedAxis(scale.y, ratio),
            scale.z);
    }

    private static float ScaleSignedAxis(float value, float ratio)
    {
        if (Mathf.Approximately(value, 0f))
        {
            return ratio;
        }

        return value * ratio;
    }

    private static bool Approximately(Vector3 left, Vector3 right)
    {
        return Mathf.Approximately(left.x, right.x) &&
            Mathf.Approximately(left.y, right.y) &&
            Mathf.Approximately(left.z, right.z);
    }

    private readonly struct VisualCandidate
    {
        public VisualCandidate(Transform transform, Rect screenRect, string source)
        {
            Transform = transform;
            ScreenRect = screenRect;
            Area = Mathf.Max(0f, screenRect.width) * Mathf.Max(0f, screenRect.height);
            Source = source ?? string.Empty;
        }

        public Transform Transform { get; }
        public Rect ScreenRect { get; }
        public float Area { get; }
        public string Source { get; }
    }
}
