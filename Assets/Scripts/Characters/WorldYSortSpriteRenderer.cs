using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(20000)]
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/World Y Sort Sprite Renderer")]
public sealed class WorldYSortSpriteRenderer : MonoBehaviour
{
    private const float ActorTieYThreshold = 0.001f;
    private static readonly HashSet<WorldYSortSpriteRenderer> ActiveSorters = new HashSet<WorldYSortSpriteRenderer>();
    private static readonly List<WorldYSortSpriteRenderer> ActorTieCandidates = new List<WorldYSortSpriteRenderer>();

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
    private CharacterFloorReference actorFloorReference;
    private readonly Dictionary<SpriteRenderer, int> actorRendererOffsets = new Dictionary<SpriteRenderer, int>();

    public bool IsConfiguredForActor => actorSortingSource != null;
    public SpriteRenderer ActorFootRenderer => actorFootRenderer;
    public CharacterFloorReference ActorFloorReference => actorFloorReference;
    public float CurrentActorSortingY { get; private set; }
    public int CurrentBaseSortingOrder { get; private set; }
    public int CurrentTieBreakOffset { get; private set; }

    private void Reset()
    {
        yReference = transform;
        RefreshRenderers();
        ResolveOptionalReferences();
        ApplySorting();
    }

    private void OnEnable()
    {
        ActiveSorters.Add(this);
        RefreshRenderers();
        ResolveOptionalReferences();
        ApplySorting();
    }

    private void OnDisable()
    {
        ActiveSorters.Remove(this);
    }

    private void OnDestroy()
    {
        ActiveSorters.Remove(this);
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

        CurrentActorSortingY = sortActorFromVisibleFeet ? sortingY : 0f;
        CurrentBaseSortingOrder = sortingOrder;
        CurrentTieBreakOffset = sortActorFromVisibleFeet
            ? ResolveActorTieBreakOffset(sortingY, sortingOrder, layerName)
            : 0;
        sortingOrder += CurrentTieBreakOffset;
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
        actorFloorReference = CharacterFloorReference.EnsureForActor(gameObject, actorFootRenderer);
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
        if (actorFloorReference == null || !actorFloorReference.IsInitialized)
        {
            actorFloorReference = CharacterFloorReference.EnsureForActor(gameObject, actorFootRenderer);
        }

        if (actorFloorReference != null &&
            actorFloorReference.TryGetWorldPoint(out Vector3 stableFloorWorldPoint))
        {
            return stableFloorWorldPoint.y;
        }

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

    private int ResolveActorTieBreakOffset(float sortingY, int baseSortingOrder, string layerName)
    {
        ActorTieCandidates.Clear();

        foreach (WorldYSortSpriteRenderer candidate in ActiveSorters)
        {
            if (candidate == null ||
                !candidate.isActiveAndEnabled ||
                candidate.actorSortingSource != actorSortingSource ||
                !candidate.HasEnabledActorRenderer())
            {
                continue;
            }

            string candidateLayerName = candidate.actorSortingSource.CurrentSortingLayerName;
            float candidateY = candidate.GetActorFootY();
            int candidateBaseOrder = candidate.actorSortingSource.GetSortingOrderForFootY(candidateY) +
                candidate.sortingOrderOffset;

            if (candidateBaseOrder == baseSortingOrder &&
                Mathf.Abs(candidateY - sortingY) <= ActorTieYThreshold &&
                string.Equals(candidateLayerName, layerName, System.StringComparison.Ordinal))
            {
                ActorTieCandidates.Add(candidate);
            }
        }

        if (ActorTieCandidates.Count <= 1)
        {
            return 0;
        }

        ActorTieCandidates.Sort(CompareActorTieKeys);
        return Mathf.Max(0, ActorTieCandidates.IndexOf(this));
    }

    private bool HasEnabledActorRenderer()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            RefreshRenderers();
        }

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];

            if (spriteRenderer != null &&
                spriteRenderer.enabled &&
                spriteRenderer.gameObject.activeInHierarchy &&
                spriteRenderer.sprite != null)
            {
                return true;
            }
        }

        return false;
    }

    private static int CompareActorTieKeys(
        WorldYSortSpriteRenderer left,
        WorldYSortSpriteRenderer right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return -1;
        }

        if (right == null)
        {
            return 1;
        }

        int keyComparison = string.CompareOrdinal(left.GetActorTieKey(), right.GetActorTieKey());

        if (keyComparison != 0)
        {
            return keyComparison;
        }

        // Actor ids/names are expected to be unique. The object hash is only a
        // last-resort, per-session stable fallback for misconfigured duplicates.
        return left.GetHashCode().CompareTo(right.GetHashCode());
    }

    private string GetActorTieKey()
    {
        ActorRoomState actorRoomState = GetComponent<ActorRoomState>();
        string actorId = actorRoomState != null ? actorRoomState.ActorId : string.Empty;

        if (!string.IsNullOrWhiteSpace(actorId))
        {
            return actorId.Trim();
        }

        return gameObject.name;
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

        if (actorFloorReference == null)
        {
            actorFloorReference = GetComponent<CharacterFloorReference>();
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
