using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Character Animation Presenter")]
public sealed class CharacterAnimationPresenter : MonoBehaviour
{
    [SerializeField] private CharacterAnimationDisplay animationDisplay;
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private CharacterWalkDirection facingDirection = CharacterWalkDirection.Down;
    [SerializeField] private bool seated;

    private CharacterAnimatorDriver.ParameterCache animatorParameters;
    private bool currentIsWalking;
    private float currentAnimationSpeed;

    public Animator Animator => animator;
    public SpriteRenderer BodyRenderer => bodyRenderer;
    public CharacterWalkDirection FacingDirection => facingDirection;
    public bool IsSeated => seated;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    public static CharacterAnimationPresenter EnsureForActor(GameObject actorRoot)
    {
        if (actorRoot == null)
        {
            return null;
        }

        CharacterAnimationPresenter presenter = actorRoot.GetComponent<CharacterAnimationPresenter>();

        if (presenter == null)
        {
            presenter = actorRoot.AddComponent<CharacterAnimationPresenter>();
        }

        presenter.ResolveReferences();
        return presenter;
    }

    public static CharacterAnimationPresenter FindForActor(GameObject actorRoot)
    {
        return actorRoot != null ? actorRoot.GetComponent<CharacterAnimationPresenter>() : null;
    }

    public bool ApplyMovement(
        Vector2 movement,
        bool isWalking,
        float animationSpeed,
        float horizontalDirectionThreshold)
    {
        if (movement.sqrMagnitude > 0.0001f)
        {
            facingDirection = CharacterAnimatorDriver.DetermineDirection(
                movement,
                facingDirection,
                horizontalDirectionThreshold);
        }

        return ApplyAnimatorState(isWalking, animationSpeed);
    }

    public bool BeginWalk(CharacterWalkDirection direction, float animationSpeed)
    {
        facingDirection = direction;
        return ApplyAnimatorState(true, animationSpeed);
    }

    public bool StopWalk(CharacterWalkDirection direction)
    {
        facingDirection = direction;
        return ApplyAnimatorState(false, 0f);
    }

    public bool SetSeated(bool value)
    {
        seated = value;
        return ApplyAnimatorState(currentIsWalking, currentAnimationSpeed);
    }

    public bool ResetAnimatorToAuthoredState()
    {
        ResolveReferences();

        if (!CanUseAnimator())
        {
            return false;
        }

        animator.Rebind();
        animator.Update(0f);
        CacheAnimatorParameters();
        currentIsWalking = false;
        currentAnimationSpeed = 0f;
        return ApplyAnimatorState(false, 0f);
    }

    public void SetAnimatorSpeed(float speed)
    {
        ResolveReferences();

        if (animator != null)
        {
            animator.speed = Mathf.Max(0.1f, speed);
        }
    }

    public void SetAnimatorEnabled(bool value)
    {
        ResolveReferences();

        if (animator != null)
        {
            animator.enabled = value;
        }
    }

    public bool TrySetBodySprite(Sprite sprite)
    {
        ResolveReferences();

        if (sprite == null || bodyRenderer == null)
        {
            return false;
        }

        bodyRenderer.sprite = sprite;
        return true;
    }

    private bool ApplyAnimatorState(bool isWalking, float animationSpeed)
    {
        ResolveReferences();

        if (!CanUseAnimator())
        {
            return false;
        }

        if (!animator.enabled)
        {
            animator.enabled = true;
        }

        currentIsWalking = isWalking;
        currentAnimationSpeed = animationSpeed;
        animatorParameters.ApplyMovement(
            animator,
            isWalking,
            facingDirection,
            animationSpeed,
            seated && !isWalking);
        return true;
    }

    private bool CanUseAnimator()
    {
        return animator != null && animator.runtimeAnimatorController != null;
    }

    private void ResolveReferences()
    {
        if (animationDisplay == null)
        {
            animationDisplay = GetComponent<CharacterAnimationDisplay>();
        }

        Transform displayRoot = animationDisplay != null
            ? animationDisplay.AnimationDisplay
            : transform.Find("AnimationDisplay");

        if (displayRoot != null)
        {
            Animator displayAnimator = displayRoot.GetComponent<Animator>();

            if (displayAnimator != null)
            {
                animator = displayAnimator;
            }

            SpriteRenderer displayRenderer = displayRoot.GetComponent<SpriteRenderer>();

            if (displayRenderer != null)
            {
                bodyRenderer = displayRenderer;
            }
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (bodyRenderer == null)
        {
            bodyRenderer = FindBodyRenderer();
        }

        CacheAnimatorParameters();
    }

    private void CacheAnimatorParameters()
    {
        animatorParameters = CharacterAnimatorDriver.ParameterCache.FromAnimator(animator);
    }

    private SpriteRenderer FindBodyRenderer()
    {
        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();

        if (rootRenderer != null && !IsIgnoredVisualTransform(rootRenderer.transform))
        {
            return rootRenderer;
        }

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];

            if (renderer != null && !IsIgnoredVisualTransform(renderer.transform))
            {
                return renderer;
            }
        }

        return null;
    }

    private static bool IsIgnoredVisualTransform(Transform target)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            if (!string.IsNullOrWhiteSpace(current.name) &&
                current.name.IndexOf("coat", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
