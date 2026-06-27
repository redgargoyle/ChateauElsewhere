using System.Collections.Generic;
using UnityEngine;

public static class YAxisDepthSortUtility
{
    public const string DefaultSortingLayerName = "People";
    public const int DefaultWorldSortingOrderBase = 1000;
    public const float DefaultWorldSortingOrderPerYUnit = 100f;

    public static int GetWorldSortingOrder(
        float worldY,
        int baseOrder = DefaultWorldSortingOrderBase,
        float orderPerYUnit = DefaultWorldSortingOrderPerYUnit,
        int offset = 0)
    {
        return baseOrder - Mathf.RoundToInt(worldY * orderPerYUnit) + offset;
    }

    public static int GetProfileSortingOrder(RoomPerspectiveProfile profile, Vector2 roomLocalFootPoint, int offset = 0)
    {
        return profile != null ? profile.GetSortingOrder(roomLocalFootPoint, offset) : offset;
    }

    public static string GetSafeSortingLayerName(
        string requestedLayerName,
        string fallback = DefaultSortingLayerName)
    {
        if (string.Equals(requestedLayerName, "Default", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Default";
        }

        if (!string.IsNullOrWhiteSpace(requestedLayerName) &&
            SortingLayer.NameToID(requestedLayerName) != 0)
        {
            return requestedLayerName;
        }

        if (string.Equals(fallback, "Default", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Default";
        }

        if (!string.IsNullOrWhiteSpace(fallback) &&
            SortingLayer.NameToID(fallback) != 0)
        {
            return fallback;
        }

        return "Default";
    }

    public static void ApplyToRenderer(
        SpriteRenderer renderer,
        string layerName,
        int sortingOrder,
        bool forcePivotSortPoint = true)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.sortingLayerName = GetSafeSortingLayerName(layerName);
        renderer.sortingOrder = sortingOrder;

        if (forcePivotSortPoint)
        {
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        }
    }

    public static void ApplyToRenderers(
        IEnumerable<SpriteRenderer> renderers,
        string layerName,
        int sortingOrder,
        bool forcePivotSortPoint = true)
    {
        if (renderers == null)
        {
            return;
        }

        foreach (SpriteRenderer renderer in renderers)
        {
            ApplyToRenderer(renderer, layerName, sortingOrder, forcePivotSortPoint);
        }
    }

    public static void ApplyToCanvas(Canvas canvas, string layerName, int sortingOrder)
    {
        if (canvas == null)
        {
            return;
        }

        canvas.overrideSorting = true;
        canvas.sortingLayerName = GetSafeSortingLayerName(layerName);
        canvas.sortingOrder = sortingOrder;
    }
}
