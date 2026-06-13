using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("ChataeuChatilly/Characters/World Y Sort Sprite Renderer")]
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
    private RoomProjectedEntity roomProjection;
    private YSortSolidObstacle2D solidObstacle;
    private PointClickPlayerMovement player;

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

        if (roomProjection != null && roomProjection.IsProjectionActive)
        {
            return;
        }

        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            RefreshRenderers();
        }

        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            return;
        }

        Transform reference = yReference != null ? yReference : transform;
        string layerName = GetSortingLayerName(sortingLayerName);
        float sortingY = GetSortingY(reference);
        int sortingOrder = sortingOrderBase - Mathf.RoundToInt(sortingY * sortingOrderPerYUnit) + sortingOrderOffset;
        sortingOrder = ResolveOcclusionSafeSortingOrder(sortingOrder);

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];

            if (spriteRenderer == null)
            {
                continue;
            }

            spriteRenderer.sortingLayerName = layerName;
            spriteRenderer.sortingOrder = sortingOrder;

            if (forcePivotSortPoint)
            {
                spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
            }
        }
    }

    private void RefreshRenderers()
    {
        spriteRenderers = includeChildren
            ? GetComponentsInChildren<SpriteRenderer>(true)
            : GetComponents<SpriteRenderer>();
    }

    private void ResolveOptionalReferences()
    {
        if (roomProjection == null)
        {
            roomProjection = GetComponentInParent<RoomProjectedEntity>();
        }

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
