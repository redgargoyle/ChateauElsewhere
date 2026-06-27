using UnityEngine;

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
    private RoomProjectedEntity roomProjection;
    private YSortSolidObstacle2D solidObstacle;
    private YSortOcclusionFootprint2D occlusionFootprint;
    private PointClickPlayerMovement player;
    private int currentSortingOrder;

    public int CurrentSortingOrder => currentSortingOrder;

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

        Vector2 worldBasePoint = GetSortingWorldPoint();
        string layerName;
        int sortingOrder;

        if (TryGetProfileSorting(worldBasePoint, out string profileLayerName, out int profileSortingOrder))
        {
            layerName = profileLayerName;
            sortingOrder = profileSortingOrder;
        }
        else
        {
            layerName = YAxisDepthSortUtility.GetSafeSortingLayerName(sortingLayerName);
            sortingOrder = YAxisDepthSortUtility.GetWorldSortingOrder(
                worldBasePoint.y,
                sortingOrderBase,
                sortingOrderPerYUnit,
                sortingOrderOffset);
        }

        sortingOrder = ResolveOcclusionSafeSortingOrder(sortingOrder);
        currentSortingOrder = sortingOrder;

        YAxisDepthSortUtility.ApplyToRenderers(spriteRenderers, layerName, sortingOrder, forcePivotSortPoint);
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

            if (solidObstacle == null)
            {
                solidObstacle = GetComponentInChildren<YSortSolidObstacle2D>(true);
            }
        }

        if (occlusionFootprint == null)
        {
            occlusionFootprint = GetComponent<YSortOcclusionFootprint2D>();

            if (occlusionFootprint == null)
            {
                occlusionFootprint = GetComponentInChildren<YSortOcclusionFootprint2D>(true);
            }
        }

        if (player == null)
        {
            player = FindAnyObjectByType<PointClickPlayerMovement>();
        }
    }

    private int ResolveOcclusionSafeSortingOrder(int defaultSortingOrder)
    {
        int resolvedSortingOrder = ResolveFootprintSortingOrder(defaultSortingOrder);

        if (!forceBehindPlayerInsidePhysicalBounds)
        {
            return resolvedSortingOrder;
        }

        ResolveOptionalReferences();

        if (solidObstacle == null ||
            player == null ||
            !solidObstacle.DisableOcclusionWhilePlayerInside ||
            !solidObstacle.ShouldDisableOcclusionForPoint(player.transform.position))
        {
            return resolvedSortingOrder;
        }

        int highestOrderBehindPlayer = player.CurrentSortingOrder + behindPlayerSortingOffset;
        return Mathf.Min(resolvedSortingOrder, highestOrderBehindPlayer);
    }

    private int ResolveFootprintSortingOrder(int defaultSortingOrder)
    {
        ResolveOptionalReferences();

        if (occlusionFootprint == null ||
            !occlusionFootprint.AffectPlayer ||
            player == null)
        {
            return defaultSortingOrder;
        }

        return occlusionFootprint.TryGetRelativeSortingOrderForActorFoot(
            player.transform.position,
            player.CurrentSortingOrder,
            defaultSortingOrder,
            out int footprintSortingOrder)
            ? footprintSortingOrder
            : defaultSortingOrder;
    }

    private Vector2 GetSortingWorldPoint()
    {
        ResolveOptionalReferences();

        if (occlusionFootprint != null && occlusionFootprint.TryGetDefaultBasePoint(out Vector2 footprintBasePoint))
        {
            return footprintBasePoint;
        }

        if (sortSolidObstacleFromPhysicalBottom &&
            solidObstacle != null &&
            solidObstacle.TryGetPhysicalBounds(out Bounds physicalBounds))
        {
            return new Vector2(physicalBounds.center.x, physicalBounds.min.y);
        }

        Transform reference = yReference != null ? yReference : transform;
        return reference.position;
    }

    private bool TryGetProfileSorting(Vector2 worldBasePoint, out string profileLayerName, out int profileSortingOrder)
    {
        profileLayerName = string.Empty;
        profileSortingOrder = 0;

        RoomContentGroup roomContent = GetComponentInParent<RoomContentGroup>(true);

        if (roomContent == null || !roomContent.TryGetPerspectiveProfile(out RoomPerspectiveProfile profile))
        {
            return false;
        }

        Vector3 localPoint = roomContent.transform.InverseTransformPoint(worldBasePoint);
        profileLayerName = profile.SortingLayerName;
        profileSortingOrder = YAxisDepthSortUtility.GetProfileSortingOrder(
            profile,
            new Vector2(localPoint.x, localPoint.y),
            sortingOrderOffset);
        return true;
    }
}
