using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Y Sort Occlusion Footprint 2D")]
public sealed class YSortOcclusionFootprint2D : MonoBehaviour
{
    [SerializeField] private Collider2D footprintCollider;
    [SerializeField] private bool useDepthLine;
    [SerializeField] private Transform depthLineStart;
    [SerializeField] private Transform depthLineEnd;
    [SerializeField] [Min(0f)] private float influencePadding = 0.25f;
    [SerializeField] private bool affectPlayer = true;
    [SerializeField] private bool affectProjectedEntities;
    [SerializeField] [Min(1)] private int relativeOrderOffset = 1;

    public Collider2D FootprintCollider => footprintCollider;
    public bool UseDepthLine => useDepthLine;
    public bool AffectPlayer => affectPlayer;
    public bool AffectProjectedEntities => affectProjectedEntities;
    public int RelativeOrderOffset => Mathf.Max(1, relativeOrderOffset);

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        influencePadding = Mathf.Max(0f, influencePadding);
        relativeOrderOffset = Mathf.Max(1, relativeOrderOffset);
        ResolveReferences();
    }

    public void ConfigureDepthLine(Transform start, Transform end, float padding, int orderOffset)
    {
        depthLineStart = start;
        depthLineEnd = end;
        useDepthLine = start != null && end != null;
        influencePadding = Mathf.Max(0f, padding);
        relativeOrderOffset = Mathf.Max(1, orderOffset);
    }

    public bool TryGetDefaultBasePoint(out Vector2 worldPoint)
    {
        ResolveReferences();

        if (footprintCollider != null && footprintCollider.enabled)
        {
            Bounds bounds = footprintCollider.bounds;
            worldPoint = new Vector2(bounds.center.x, bounds.min.y);
            return true;
        }

        worldPoint = transform.position;
        return false;
    }

    public bool TryGetRelativeSortingOrderForActorFoot(
        Vector2 actorFootWorldPoint,
        int actorSortingOrder,
        int defaultSortingOrder,
        out int resolvedSortingOrder)
    {
        resolvedSortingOrder = defaultSortingOrder;

        if (!isActiveAndEnabled || !useDepthLine || depthLineStart == null || depthLineEnd == null)
        {
            return false;
        }

        if (!IsActorFootInfluenced(actorFootWorldPoint))
        {
            return false;
        }

        float comparableDepthY = GetDepthLineYAtWorldX(actorFootWorldPoint.x);

        if (actorFootWorldPoint.y <= comparableDepthY)
        {
            resolvedSortingOrder = actorSortingOrder - RelativeOrderOffset;
        }
        else
        {
            resolvedSortingOrder = actorSortingOrder + RelativeOrderOffset;
        }

        return true;
    }

    private bool IsActorFootInfluenced(Vector2 actorFootWorldPoint)
    {
        ResolveReferences();

        if (footprintCollider != null && footprintCollider.enabled)
        {
            if (footprintCollider.OverlapPoint(actorFootWorldPoint))
            {
                return true;
            }

            if (influencePadding <= 0f)
            {
                return false;
            }

            Vector2 closest = footprintCollider.ClosestPoint(actorFootWorldPoint);
            return Vector2.Distance(closest, actorFootWorldPoint) <= influencePadding;
        }

        Vector2 start = depthLineStart.position;
        Vector2 end = depthLineEnd.position;
        float minX = Mathf.Min(start.x, end.x) - influencePadding;
        float maxX = Mathf.Max(start.x, end.x) + influencePadding;
        return actorFootWorldPoint.x >= minX && actorFootWorldPoint.x <= maxX;
    }

    private float GetDepthLineYAtWorldX(float worldX)
    {
        Vector2 start = depthLineStart.position;
        Vector2 end = depthLineEnd.position;
        float t = Mathf.Approximately(start.x, end.x)
            ? 0f
            : Mathf.InverseLerp(start.x, end.x, worldX);
        return Vector2.Lerp(start, end, Mathf.Clamp01(t)).y;
    }

    private void ResolveReferences()
    {
        if (footprintCollider == null)
        {
            footprintCollider = GetComponent<Collider2D>();
        }
    }
}
