using UnityEngine;

/// <summary>
/// Identifies the dedicated child used by the character animation pipeline.
/// Display size is owned exclusively by CharacterDisplayScaleController.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Character Animation Display")]
public sealed class CharacterAnimationDisplay : MonoBehaviour
{
    [Tooltip("Dedicated visual child containing the Animator and character renderers. The actor/movement root is never scaled.")]
    [SerializeField] private Transform animationDisplay;

    public Transform AnimationDisplay => animationDisplay;

    private void Reset()
    {
        ResolveDisplayRoot();
    }

    private void OnValidate()
    {
        ResolveDisplayRoot();
    }

    public void Configure(Transform displayRoot)
    {
        animationDisplay = displayRoot;
    }

    public bool HasValidDisplayRoot()
    {
        return animationDisplay != null &&
            animationDisplay != transform &&
            animationDisplay.IsChildOf(transform);
    }

    public static CharacterAnimationDisplay EnsureForActor(GameObject actorRoot)
    {
        if (actorRoot == null)
        {
            return null;
        }

        CharacterAnimationDisplay display = actorRoot.GetComponent<CharacterAnimationDisplay>();

        if (display != null)
        {
            return display;
        }

        Transform displayRoot = actorRoot.transform.Find("AnimationDisplay");

        if (displayRoot == null)
        {
            SpriteRenderer renderer = actorRoot.GetComponentInChildren<SpriteRenderer>(true);

            if (renderer != null && renderer.transform != actorRoot.transform)
            {
                displayRoot = renderer.transform;
            }
        }

        if (displayRoot == null || displayRoot == actorRoot.transform)
        {
            Debug.LogError(
                $"Actor '{actorRoot.name}' needs a dedicated AnimationDisplay child before character animation can be enabled.",
                actorRoot);
            return null;
        }

        display = actorRoot.AddComponent<CharacterAnimationDisplay>();
        display.Configure(displayRoot);
        return display;
    }

    private void ResolveDisplayRoot()
    {
        if (animationDisplay == null)
        {
            animationDisplay = transform.Find("AnimationDisplay");
        }
    }
}
