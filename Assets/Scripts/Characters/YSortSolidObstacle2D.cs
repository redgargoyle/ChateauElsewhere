using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[AddComponentMenu("Dreadforge/Characters/Y Sort Solid Obstacle 2D")]
public sealed class YSortSolidObstacle2D : MonoBehaviour
{
    [SerializeField] private Collider2D physicalBounds;
    [SerializeField] private bool blockPlayerMovement = true;
    [SerializeField] [Min(0f)] private float movementPadding = 0.02f;
    [SerializeField] private bool disableOcclusionWhilePlayerInside = true;
    [SerializeField] [Min(0f)] private float occlusionPadding = 0.28f;

    public bool BlockPlayerMovement => blockPlayerMovement;
    public float MovementPadding => movementPadding;
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

    public bool BlocksMovementAtPoint(Vector2 point, float extraPadding = 0f)
    {
        return blockPlayerMovement && ContainsPoint(point, movementPadding + Mathf.Max(0f, extraPadding));
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

    public bool TryGetMovementBounds(float extraPadding, out Bounds bounds)
    {
        if (!blockPlayerMovement || !TryGetPhysicalBounds(out bounds))
        {
            bounds = default;
            return false;
        }

        float padding = movementPadding + Mathf.Max(0f, extraPadding);
        bounds.Expand(new Vector3(padding * 2f, padding * 2f, 0f));
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
