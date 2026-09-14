using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Gives all actor artwork one external depth point at the canonical floor.
/// Sprite pivots remain free to serve animation; body and carried props sort together.
/// Runs after normal sorting and before explicit seated furniture exceptions.
/// </summary>
[DefaultExecutionOrder(20500)]
[DisallowMultipleComponent]
public sealed class CharacterDepthGroup : MonoBehaviour
{
    private CharacterAnimationDisplay display;
    private CharacterFloorReference floor;
    private SpriteRenderer body;
    private SortingGroup depthGroup;

    public SortingGroup Group => depthGroup;

    public static CharacterDepthGroup EnsureForActor(CharacterAnimationDisplay display)
    {
        if (display == null || !display.HasValidDisplayRoot()) return null;
        CharacterDepthGroup depth = display.GetComponent<CharacterDepthGroup>();
        if (depth == null) depth = display.gameObject.AddComponent<CharacterDepthGroup>();
        depth.display = display;
        return depth;
    }

    private void LateUpdate() => RefreshDepth();

    public void RefreshDepth()
    {
        if (display == null) display = GetComponent<CharacterAnimationDisplay>();
        if (display == null || !display.HasValidDisplayRoot()) return;
        Transform visual = display.AnimationDisplay;
        if (body == null || !body.transform.IsChildOf(visual) && body.transform != visual)
        {
            CharacterAnimationPresenter presenter = GetComponent<CharacterAnimationPresenter>();
            SpriteRenderer presentedBody = presenter != null ? presenter.BodyRenderer : null;
            body = presentedBody != null &&
                (presentedBody.transform == visual || presentedBody.transform.IsChildOf(visual))
                ? presentedBody : visual.GetComponentInChildren<SpriteRenderer>(true);
        }
        if (body == null) return;
        if (floor == null) floor = CharacterFloorReference.EnsureForActor(gameObject, body);
        if (floor == null || !floor.TryGetWorldPoint(out Vector3 floorPoint)) return;

        if (depthGroup == null)
        {
            GameObject wrapper = new GameObject("ActorDepth");
            wrapper.transform.SetParent(visual.parent, false);
            wrapper.transform.SetSiblingIndex(visual.GetSiblingIndex());
            depthGroup = wrapper.AddComponent<SortingGroup>();
            // Nested under a seated override when one is active, never escape it.
            depthGroup.sortAtRoot = false;
            visual.SetParent(wrapper.transform, true);
        }

        // Move only the sorting origin. Preserve the visible pose and its local
        // scale so CharacterAnimationDisplay remains the sole size authority.
        Vector3 visualPosition = visual.position;
        depthGroup.transform.position = floorPoint;
        visual.position = visualPosition;
        depthGroup.sortingLayerID = body.sortingLayerID;
        depthGroup.sortingOrder = body.sortingOrder;
        depthGroup.enabled = true;
    }

    private void OnDisable()
    {
        if (depthGroup != null) depthGroup.enabled = false;
    }
}
