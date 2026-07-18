using UnityEngine;

/// <summary>
/// Stable actor-floor point used by movement, character scaling, and depth sorting.
/// The point is captured once in a visual transform's local space so display scaling
/// remains respected while sprite rect, pivot, and bounds changes cannot move the
/// actor between depth bands.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Character Floor Reference")]
public sealed class CharacterFloorReference : MonoBehaviour
{
    [SerializeField, HideInInspector] private Transform referenceTransform;
    [SerializeField, HideInInspector] private Vector3 localFloorPoint;
    [SerializeField, HideInInspector] private bool isInitialized;

    public bool IsInitialized => isInitialized;
    public Transform ReferenceTransform => referenceTransform;
    public Vector3 WorldPoint => GetReferenceTransform().TransformPoint(localFloorPoint);

    public bool TryGetWorldPoint(out Vector3 worldPoint)
    {
        worldPoint = isInitialized ? WorldPoint : Vector3.zero;
        return isInitialized;
    }

    public void CaptureWorldPoint(Vector3 worldPoint)
    {
        CaptureWorldPoint(worldPoint, GetReferenceTransform());
    }

    public void CaptureWorldPoint(Vector3 worldPoint, Transform visualReference)
    {
        referenceTransform = visualReference != null ? visualReference : transform;
        localFloorPoint = referenceTransform.InverseTransformPoint(worldPoint);
        isInitialized = true;
    }

    public bool TryCaptureVisibleFeet(
        GameObject visualRoot,
        SpriteRenderer preferredRenderer = null,
        bool ignoreCoatRenderers = true,
        bool includeInactiveRenderers = true)
    {
        if (preferredRenderer != null && preferredRenderer.sprite != null)
        {
            Bounds bounds = preferredRenderer.bounds;
            CaptureWorldPoint(
                new Vector3(bounds.center.x, bounds.min.y, bounds.center.z),
                preferredRenderer.transform);
            return true;
        }

        if (!CharacterFootPositionUtility.TryGetVisibleWorldPoint(
                visualRoot != null ? visualRoot : gameObject,
                ignoreCoatRenderers,
                includeInactiveRenderers,
                out Vector3 visibleFeetWorldPoint))
        {
            return false;
        }

        CaptureWorldPoint(visibleFeetWorldPoint, ResolveVisualReference(visualRoot));
        return true;
    }

    /// <summary>
    /// Translates the actor root so its canonical floor point reaches the requested
    /// world point. The actor's authored Z remains untouched.
    /// </summary>
    public void AlignActorToWorldPoint(Vector2 worldPoint)
    {
        if (!isInitialized)
        {
            CaptureWorldPoint(transform.position);
        }

        Vector3 currentWorldPoint = WorldPoint;
        Vector3 correction = new Vector3(
            worldPoint.x - currentWorldPoint.x,
            worldPoint.y - currentWorldPoint.y,
            0f);
        transform.position += correction;
    }

    public static CharacterFloorReference EnsureForActor(
        GameObject actorRoot,
        SpriteRenderer preferredRenderer = null)
    {
        if (actorRoot == null)
        {
            return null;
        }

        CharacterFloorReference floorReference = actorRoot.GetComponent<CharacterFloorReference>();

        if (floorReference == null)
        {
            floorReference = actorRoot.AddComponent<CharacterFloorReference>();
        }

        if (!floorReference.IsInitialized)
        {
            floorReference.TryCaptureVisibleFeet(actorRoot, preferredRenderer);
        }

        return floorReference;
    }

    private Transform GetReferenceTransform()
    {
        return referenceTransform != null ? referenceTransform : transform;
    }

    private Transform ResolveVisualReference(GameObject visualRoot)
    {
        GameObject requestedRoot = visualRoot != null ? visualRoot : gameObject;
        CharacterAnimationDisplay animationDisplay = requestedRoot.GetComponent<CharacterAnimationDisplay>();

        if (animationDisplay != null && animationDisplay.HasValidDisplayRoot())
        {
            return animationDisplay.AnimationDisplay;
        }

        SpriteRenderer[] renderers = requestedRoot.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];

            if (renderer != null && renderer.sprite != null)
            {
                return renderer.transform;
            }
        }

        return requestedRoot.transform;
    }
}
