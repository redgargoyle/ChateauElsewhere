using System;
using UnityEngine;
using UnityEngine.UI;

public static class CharacterFootPositionUtility
{
    public static bool TryGetWorldPoint(
        GameObject root,
        bool ignoreCoatRenderers,
        bool includeInactiveRenderers,
        out Vector3 feetWorldPoint)
    {
        feetWorldPoint = Vector3.zero;

        if (root == null)
        {
            return false;
        }

        CharacterFloorReference floorReference = root.GetComponent<CharacterFloorReference>();

        if (floorReference != null && floorReference.TryGetWorldPoint(out feetWorldPoint))
        {
            return true;
        }

        return TryGetVisibleWorldPoint(
            root,
            ignoreCoatRenderers,
            includeInactiveRenderers,
            out feetWorldPoint);
    }

    /// <summary>
    /// Measures the current rendered character bounds directly. Most gameplay callers
    /// should use <see cref="TryGetWorldPoint"/>, which returns an actor's canonical
    /// floor reference when one is available. This method exists for the one-time
    /// capture of that reference and for diagnostics that explicitly need visual bounds.
    /// </summary>
    public static bool TryGetVisibleWorldPoint(
        GameObject root,
        bool ignoreCoatRenderers,
        bool includeInactiveRenderers,
        out Vector3 feetWorldPoint)
    {
        feetWorldPoint = Vector3.zero;

        if (root == null)
        {
            return false;
        }

        Bounds combinedBounds = default;
        bool hasBounds = false;
        AccumulateRendererBounds(
            root,
            ignoreCoatRenderers,
            includeInactiveRenderers,
            ref combinedBounds,
            ref hasBounds);

        if (!hasBounds && ignoreCoatRenderers)
        {
            AccumulateRendererBounds(
                root,
                false,
                includeInactiveRenderers,
                ref combinedBounds,
                ref hasBounds);
        }

        AccumulateGraphicBounds(root, includeInactiveRenderers, ref combinedBounds, ref hasBounds);

        if (!hasBounds)
        {
            return false;
        }

        feetWorldPoint = new Vector3(combinedBounds.center.x, combinedBounds.min.y, combinedBounds.center.z);
        return true;
    }

    private static void AccumulateRendererBounds(
        GameObject root,
        bool ignoreCoatRenderers,
        bool includeInactiveRenderers,
        ref Bounds combinedBounds,
        ref bool hasBounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null ||
                (ignoreCoatRenderers && IsCoatVisualTransform(renderer.transform)) ||
                (!includeInactiveRenderers && (!renderer.enabled || !renderer.gameObject.activeInHierarchy)))
            {
                continue;
            }

            IncludeBounds(renderer.bounds, ref combinedBounds, ref hasBounds);
        }
    }

    private static void AccumulateGraphicBounds(
        GameObject root,
        bool includeInactiveRenderers,
        ref Bounds combinedBounds,
        ref bool hasBounds)
    {
        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        Vector3[] corners = new Vector3[4];

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];

            if (graphic == null ||
                graphic.rectTransform == null ||
                (!includeInactiveRenderers && (!graphic.enabled || !graphic.gameObject.activeInHierarchy)))
            {
                continue;
            }

            graphic.rectTransform.GetWorldCorners(corners);
            Bounds graphicBounds = new Bounds(corners[0], Vector3.zero);

            for (int cornerIndex = 1; cornerIndex < corners.Length; cornerIndex++)
            {
                graphicBounds.Encapsulate(corners[cornerIndex]);
            }

            IncludeBounds(graphicBounds, ref combinedBounds, ref hasBounds);
        }
    }

    private static void IncludeBounds(Bounds bounds, ref Bounds combinedBounds, ref bool hasBounds)
    {
        if (!hasBounds)
        {
            combinedBounds = bounds;
            hasBounds = true;
            return;
        }

        combinedBounds.Encapsulate(bounds);
    }

    private static bool IsCoatVisualTransform(Transform target)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            if (current.name.IndexOf("coat", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
