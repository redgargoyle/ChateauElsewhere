using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(20000)]
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/World Y Sort Sprite Renderer")]
public sealed class WorldYSortSpriteRenderer : MonoBehaviour
{
    [SerializeField] private string sortingLayerName = "People";
    [SerializeField] private int sortingOrderBase = 1000;
    [SerializeField] private float sortingOrderPerYUnit = 100f;
    [SerializeField] private int sortingOrderOffset;
    [SerializeField] private bool includeChildren = true;
    [SerializeField] private bool forcePivotSortPoint = true;
    [SerializeField] private bool sortSolidObstacleFromPhysicalBottom = true;
    [SerializeField] private bool forceBehindPlayerInsidePhysicalBounds;
    [SerializeField] private int behindPlayerSortingOffset = -1;
    [SerializeField] private Transform yReference;

    private SpriteRenderer[] spriteRenderers;
    private YSortSolidObstacle2D solidObstacle;
    private PointClickPlayerMovement player;
    private PointClickPlayerMovement actorSortingSource;
    private SpriteRenderer actorFootRenderer;
    private readonly Dictionary<SpriteRenderer, int> actorRendererOffsets = new Dictionary<SpriteRenderer, int>();

    public bool IsConfiguredForActor => actorSortingSource != null;
    public SpriteRenderer ActorFootRenderer => actorFootRenderer;

    private void Reset()
    {
        yReference = transform;
        RefreshRenderers();
        ResolveOptionalReferences();
        ApplySorting();
    }

    private void OnEnable()
    {
        RefreshRenderers();
        ResolveOptionalReferences();
        ApplySorting();
    }

    private void OnValidate()
    {
        RefreshRenderers();
        ResolveOptionalReferences();
        ApplySorting();
    }

    private void OnTransformChildrenChanged()
    {
        RefreshRenderers();
        ApplySorting();
    }

    private void LateUpdate()
    {
        ApplySorting();
    }

    public void ApplySorting()
    {
        ResolveOptionalReferences();

        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            RefreshRenderers();
        }

        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            return;
        }

        Transform reference = yReference != null ? yReference : transform;
        bool sortActorFromVisibleFeet = actorSortingSource != null;
        string layerName = sortActorFromVisibleFeet
            ? actorSortingSource.CurrentSortingLayerName
            : GetSortingLayerName(sortingLayerName);
        float sortingY = sortActorFromVisibleFeet ? GetActorFootY() : GetSortingY(reference);
        int sortingOrder = sortActorFromVisibleFeet
            ? actorSortingSource.GetSortingOrderForFootY(sortingY) + sortingOrderOffset
            : sortingOrderBase - Mathf.RoundToInt(sortingY * sortingOrderPerYUnit) + sortingOrderOffset;
        sortingOrder = ResolveOcclusionSafeSortingOrder(sortingOrder);

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];

            if (spriteRenderer == null)
            {
                continue;
            }

            spriteRenderer.sortingLayerName = layerName;
            spriteRenderer.sortingOrder = sortingOrder + GetActorRendererOffset(spriteRenderer);

            if (forcePivotSortPoint)
            {
                spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
            }
        }
    }

    public void ConfigureForActor(PointClickPlayerMovement sortingSource, SpriteRenderer footRenderer)
    {
        actorSortingSource = sortingSource;
        actorFootRenderer = footRenderer;
        RefreshActorSortingTargets();
        ApplySorting();
    }

    public void RefreshActorSortingTargets()
    {
        RefreshRenderers();
        actorRendererOffsets.Clear();

        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            return;
        }

        if (actorFootRenderer == null)
        {
            actorFootRenderer = spriteRenderers[0];
        }

        int referenceOrder = actorFootRenderer != null ? actorFootRenderer.sortingOrder : 0;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];

            if (spriteRenderer != null)
            {
                actorRendererOffsets[spriteRenderer] = spriteRenderer.sortingOrder - referenceOrder;
            }
        }
    }

    private void RefreshRenderers()
    {
        spriteRenderers = includeChildren
            ? GetComponentsInChildren<SpriteRenderer>(true)
            : GetComponents<SpriteRenderer>();
    }

    private float GetActorFootY()
    {
        if (actorFootRenderer != null && actorFootRenderer.sprite != null)
        {
            return actorFootRenderer.bounds.min.y;
        }

        float lowestVisibleY = float.PositiveInfinity;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];

            if (spriteRenderer != null && spriteRenderer.enabled && spriteRenderer.sprite != null)
            {
                lowestVisibleY = Mathf.Min(lowestVisibleY, spriteRenderer.bounds.min.y);
            }
        }

        Transform reference = yReference != null ? yReference : transform;
        return float.IsPositiveInfinity(lowestVisibleY) ? reference.position.y : lowestVisibleY;
    }

    private int GetActorRendererOffset(SpriteRenderer spriteRenderer)
    {
        return actorSortingSource != null &&
            spriteRenderer != null &&
            actorRendererOffsets.TryGetValue(spriteRenderer, out int offset)
                ? offset
                : 0;
    }

    private void ResolveOptionalReferences()
    {
        if (solidObstacle == null)
        {
            solidObstacle = GetComponent<YSortSolidObstacle2D>();
        }

        if (player == null)
        {
            player = FindAnyObjectByType<PointClickPlayerMovement>();
        }
    }

    private int ResolveOcclusionSafeSortingOrder(int defaultSortingOrder)
    {
        if (!forceBehindPlayerInsidePhysicalBounds)
        {
            return defaultSortingOrder;
        }

        ResolveOptionalReferences();

        if (solidObstacle == null ||
            player == null ||
            !solidObstacle.DisableOcclusionWhilePlayerInside ||
            !solidObstacle.ShouldDisableOcclusionForPoint(player.transform.position))
        {
            return defaultSortingOrder;
        }

        int highestOrderBehindPlayer = player.CurrentSortingOrder + behindPlayerSortingOffset;
        return Mathf.Min(defaultSortingOrder, highestOrderBehindPlayer);
    }

    private float GetSortingY(Transform reference)
    {
        ResolveOptionalReferences();

        if (sortSolidObstacleFromPhysicalBottom &&
            solidObstacle != null &&
            solidObstacle.TryGetPhysicalBounds(out Bounds physicalBounds))
        {
            return physicalBounds.min.y;
        }

        return reference.position.y;
    }

    private static string GetSortingLayerName(string requestedLayerName)
    {
        if (string.IsNullOrWhiteSpace(requestedLayerName))
        {
            return "Default";
        }

        if (string.Equals(requestedLayerName, "Default", System.StringComparison.OrdinalIgnoreCase) ||
            SortingLayer.NameToID(requestedLayerName) != 0)
        {
            return requestedLayerName;
        }

        return "Default";
    }
}
