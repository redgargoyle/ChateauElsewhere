using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Depth Pose Sorting Override")]
public sealed class DepthPoseSortingOverride : MonoBehaviour
{
    [SerializeField] private bool overrideActive;
    [SerializeField] private Transform depthAnchor;
    [SerializeField] private YSortOcclusionFootprint2D depthLine;
    [SerializeField] private int relativeSortingOffset;

    public bool OverrideActive => overrideActive;
    public Transform DepthAnchor => depthAnchor;
    public YSortOcclusionFootprint2D DepthLine => depthLine;
    public int RelativeSortingOffset => relativeSortingOffset;

    public int ApplyRelativeOffset(int baseSortingOrder)
    {
        return overrideActive ? baseSortingOrder + relativeSortingOffset : baseSortingOrder;
    }

    public bool TryGetDepthAnchorPoint(out Vector2 worldPoint)
    {
        if (!overrideActive || depthAnchor == null)
        {
            worldPoint = Vector2.zero;
            return false;
        }

        worldPoint = depthAnchor.position;
        return true;
    }
}
