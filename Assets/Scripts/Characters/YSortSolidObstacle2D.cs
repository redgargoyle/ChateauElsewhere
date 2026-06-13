using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[AddComponentMenu("ChataeuChatilly/Characters/Y Sort Solid Obstacle 2D")]
public sealed class YSortSolidObstacle2D : MonoBehaviour
{
    [SerializeField] private Collider2D physicalBounds;
    [SerializeField] private bool disableOcclusionWhilePlayerInside = true;
    [SerializeField] [Min(0f)] private float occlusionPadding = 0.28f;

    public bool DisableOcclusionWhilePlayerInside => disableOcclusionWhilePlayerInside;
    public float OcclusionPadding => occlusionPadding;

    private void Reset()
    {
        ResolveReferences();
        ConfigureCollider();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ConfigureCollider();
    }

    private void OnValidate()
    {
        ResolveReferences();
        ConfigureCollider();
    }

    public bool ShouldDisableOcclusionForPoint(Vector2 point)
    {
        return disableOcclusionWhilePlayerInside && ContainsPoint(point, occlusionPadding);
    }

    public bool ContainsPoint(Vector2 point, float padding)
    {
        ResolveReferences();

        if (physicalBounds == null || !physicalBounds.enabled || !isActiveAndEnabled)
        {
            return false;
        }

        if (physicalBounds.OverlapPoint(point))
        {
            return true;
        }

        if (padding <= 0f)
        {
            return false;
        }

        Vector2 closestPoint = physicalBounds.ClosestPoint(point);
        return Vector2.Distance(closestPoint, point) <= padding;
    }

    public bool TryGetPhysicalBounds(out Bounds bounds)
    {
        ResolveReferences();

        if (physicalBounds == null || !physicalBounds.enabled)
        {
            bounds = default;
            return false;
        }

        bounds = physicalBounds.bounds;
        return true;
    }

    private void ResolveReferences()
    {
        if (physicalBounds == null)
        {
            physicalBounds = GetComponent<Collider2D>();
        }
    }

    private void ConfigureCollider()
    {
        if (physicalBounds != null)
        {
            physicalBounds.isTrigger = true;
        }
    }
}
