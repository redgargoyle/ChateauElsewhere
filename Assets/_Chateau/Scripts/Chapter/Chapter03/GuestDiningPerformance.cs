using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GuestDiningPerformance : MonoBehaviour
{
    private const float FullCycle = 6.2831855f;

    [Header("References")]
    [SerializeField] private ActorRoomState actorState;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform proceduralMotionRoot;

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float minActionDelay = 1.5f;
    [SerializeField, Min(0.1f)] private float maxActionDelay = 4.5f;

    [Header("Animator Parameters")]
    [SerializeField] private string eatingBool = "IsEating";
    [SerializeField] private string talkingBool = "IsTalking";
    [SerializeField] private string[] randomActionTriggers = { "Eat", "Utensil", "HeadMove", "Talk" };

    [Header("Procedural Fallback Motion")]
    [SerializeField] private bool enableProceduralMotion = true;
    [SerializeField, Min(0f)] private float idleBobPixels = 0.9f;
    [SerializeField, Min(0f)] private float idleSwayPixels = 0.35f;
    [SerializeField, Min(0.1f)] private float idleCycleSeconds = 3.2f;
    [SerializeField, Min(0f)] private float idleLeanDegrees = 0.35f;
    [SerializeField, Min(0f)] private float eatingGesturePixels = 2.1f;
    [SerializeField, Min(0f)] private float eatingGestureLeanDegrees = 1.4f;
    [SerializeField, Min(0.1f)] private float eatingGestureDurationSeconds = 0.9f;
    [SerializeField, Min(0.0001f)] private float worldUnitsPerPixel = 0.01f;

    private Coroutine actionLoop;
    private Coroutine visualMotionLoop;
    private bool warnedMissingAnimationSetup;
    private bool isEatingMotionActive;
    private bool hasMotionBaseline;
    private RoomProjectedEntity motionProjection;
    private Vector2 baselineProjectedFootPoint;
    private RectTransform proceduralMotionRect;
    private Vector2 baselineAnchoredPosition;
    private Vector3 baselineLocalPosition;
    private Quaternion baselineLocalRotation = Quaternion.identity;
    private Vector3 baselineLocalScale = Vector3.one;

    public ActorRoomState ActorState => actorState;

    private void OnValidate()
    {
        minActionDelay = Mathf.Max(0.1f, minActionDelay);
        maxActionDelay = Mathf.Max(minActionDelay, maxActionDelay);
        idleBobPixels = Mathf.Max(0f, idleBobPixels);
        idleSwayPixels = Mathf.Max(0f, idleSwayPixels);
        idleCycleSeconds = Mathf.Max(0.1f, idleCycleSeconds);
        idleLeanDegrees = Mathf.Max(0f, idleLeanDegrees);
        eatingGesturePixels = Mathf.Max(0f, eatingGesturePixels);
        eatingGestureLeanDegrees = Mathf.Max(0f, eatingGestureLeanDegrees);
        eatingGestureDurationSeconds = Mathf.Max(0.1f, eatingGestureDurationSeconds);
        worldUnitsPerPixel = Mathf.Max(0.0001f, worldUnitsPerPixel);
    }

    private void OnDisable()
    {
        StopActionLoop();
        StopVisualMotionLoop(true);
    }

    public void Configure(ActorRoomState actor)
    {
        actorState = actor != null ? actor : actorState;
        ResolveReferences();
    }

    public void PrepareSeatedIdle()
    {
        ResolveReferences();
        StopActionLoop();
        SetAnimatorBool(eatingBool, false);
        SetAnimatorBool(talkingBool, false);
        ApplySeatedActorState();
        StartVisualMotionLoop(false);
    }

    public void BeginEating()
    {
        ResolveReferences();
        StopActionLoop();
        ApplySeatedActorState();

        bool hasEatingBool = SetAnimatorBool(eatingBool, true);
        bool hasAnyAction = hasEatingBool || HasAnimatorBool(talkingBool) || HasAnyAvailableTrigger();

        if (!hasAnyAction)
        {
            WarnMissingAnimationSetupOnce();
        }

        if (isActiveAndEnabled)
        {
            actionLoop = StartCoroutine(RunRandomActionLoop());
        }

        StartVisualMotionLoop(true);
    }

    public void StopEatingAndIdle()
    {
        ResolveReferences();
        StopActionLoop();
        SetAnimatorBool(eatingBool, false);
        SetAnimatorBool(talkingBool, false);
        ApplySeatedActorState();
        StartVisualMotionLoop(false);
    }

    private IEnumerator RunRandomActionLoop()
    {
        float initialDelay = Random.Range(0f, Mathf.Max(minActionDelay, maxActionDelay));

        if (initialDelay > 0f)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        while (true)
        {
            float delay = Random.Range(minActionDelay, Mathf.Max(minActionDelay, maxActionDelay));
            yield return new WaitForSeconds(delay);

            string action = ChooseAvailableAction();

            if (string.IsNullOrWhiteSpace(action) || IsIdleAction(action))
            {
                continue;
            }

            if (IsTalkAction(action) && !SetAnimatorTrigger(action))
            {
                yield return PulseTalkingBool();
                continue;
            }

            SetAnimatorTrigger(action);
        }
    }

    private IEnumerator PulseTalkingBool()
    {
        if (!SetAnimatorBool(talkingBool, true))
        {
            yield break;
        }

        yield return new WaitForSeconds(Random.Range(0.45f, 0.95f));
        SetAnimatorBool(talkingBool, false);
    }

    private string ChooseAvailableAction()
    {
        int triggerCount = randomActionTriggers != null ? randomActionTriggers.Length : 0;
        int choiceCount = triggerCount + 1;

        for (int guard = 0; guard < choiceCount * 2; guard++)
        {
            int choice = Random.Range(0, choiceCount);

            if (choice >= triggerCount)
            {
                return string.Empty;
            }

            string triggerName = randomActionTriggers[choice];

            if (string.IsNullOrWhiteSpace(triggerName) || IsIdleAction(triggerName))
            {
                return string.Empty;
            }

            if (HasAnimatorTrigger(triggerName) || (IsTalkAction(triggerName) && HasAnimatorBool(talkingBool)))
            {
                return triggerName.Trim();
            }
        }

        return string.Empty;
    }

    private void ApplySeatedActorState()
    {
        if (actorState == null)
        {
            return;
        }

        actorState.enabled = true;
        actorState.SetAvailableInCurrentChapter(true);
        actorState.SetVisibleByChapterState(true);
        actorState.SetInteractable(false);
        actorState.SetSeated(true);
        actorState.ApplyState();
    }

    private void StartVisualMotionLoop(bool eating)
    {
        isEatingMotionActive = eating;

        if (!enableProceduralMotion || !isActiveAndEnabled)
        {
            StopVisualMotionLoop(true);
            return;
        }

        ResolveMotionTarget();
        CaptureMotionBaselineIfNeeded();

        if (!HasMotionTarget())
        {
            return;
        }

        if (visualMotionLoop == null)
        {
            visualMotionLoop = StartCoroutine(RunProceduralVisualMotion());
        }
    }

    private IEnumerator RunProceduralVisualMotion()
    {
        float phase = Random.Range(0f, FullCycle);
        float nextGestureIn = Random.Range(minActionDelay, Mathf.Max(minActionDelay, maxActionDelay));
        float gestureTimer = 0f;
        float gestureDirection = 1f;
        Vector2 gestureVector = Vector2.zero;

        while (true)
        {
            float deltaTime = Time.deltaTime;
            phase = Mathf.Repeat(phase + (deltaTime / Mathf.Max(0.1f, idleCycleSeconds)) * FullCycle, FullCycle);

            Vector2 offset = new Vector2(
                Mathf.Sin(phase * 0.63f) * idleSwayPixels,
                Mathf.Sin(phase) * idleBobPixels);

            float lean = Mathf.Sin(phase * 0.47f) * idleLeanDegrees;

            if (isEatingMotionActive)
            {
                nextGestureIn -= deltaTime;

                if (gestureTimer <= 0f && nextGestureIn <= 0f)
                {
                    gestureTimer = eatingGestureDurationSeconds;
                    nextGestureIn = Random.Range(minActionDelay, Mathf.Max(minActionDelay, maxActionDelay));
                    gestureDirection = Random.value < 0.5f ? -1f : 1f;
                    gestureVector = new Vector2(
                        Random.Range(-0.35f, 0.35f) * eatingGesturePixels,
                        Random.Range(0.45f, 1f) * eatingGesturePixels);
                }

                if (gestureTimer > 0f)
                {
                    float gesture01 = 1f - Mathf.Clamp01(gestureTimer / eatingGestureDurationSeconds);
                    float pulse = Mathf.Sin(gesture01 * Mathf.PI);
                    offset += gestureVector * pulse;
                    lean += eatingGestureLeanDegrees * gestureDirection * pulse;
                    gestureTimer -= deltaTime;
                }
            }

            ApplyProceduralMotion(offset, lean);
            yield return null;
        }
    }

    private void ResolveMotionTarget()
    {
        if (actorState == null)
        {
            return;
        }

        motionProjection = actorState.Projection;

        if (proceduralMotionRoot == null)
        {
            if (motionProjection != null)
            {
                proceduralMotionRoot = motionProjection.VisualRoot;
            }
            else if (animator != null)
            {
                proceduralMotionRoot = animator.transform;
            }
            else
            {
                proceduralMotionRoot = actorState.transform;
            }
        }

        proceduralMotionRect = proceduralMotionRoot as RectTransform;
    }

    private void CaptureMotionBaselineIfNeeded()
    {
        if (hasMotionBaseline)
        {
            return;
        }

        if (motionProjection != null && motionProjection.IsProjectionActive)
        {
            baselineProjectedFootPoint = motionProjection.RoomLocalFootPoint;
        }

        if (proceduralMotionRect != null)
        {
            baselineAnchoredPosition = proceduralMotionRect.anchoredPosition;
        }

        if (proceduralMotionRoot != null)
        {
            baselineLocalPosition = proceduralMotionRoot.localPosition;
            baselineLocalRotation = proceduralMotionRoot.localRotation;
            baselineLocalScale = proceduralMotionRoot.localScale;
        }

        hasMotionBaseline = true;
    }

    private bool HasMotionTarget()
    {
        return motionProjection != null || proceduralMotionRoot != null || proceduralMotionRect != null;
    }

    private void ApplyProceduralMotion(Vector2 offsetPixels, float leanDegrees)
    {
        if (motionProjection != null && motionProjection.IsProjectionActive)
        {
            motionProjection.SetRoomLocalFootPoint(baselineProjectedFootPoint + offsetPixels);

            Transform visualRoot = proceduralMotionRoot != null ? proceduralMotionRoot : motionProjection.VisualRoot;
            if (visualRoot != null)
            {
                visualRoot.localRotation = baselineLocalRotation * Quaternion.Euler(0f, 0f, leanDegrees);
            }

            return;
        }

        if (proceduralMotionRect != null)
        {
            proceduralMotionRect.anchoredPosition = baselineAnchoredPosition + offsetPixels;
        }
        else if (proceduralMotionRoot != null)
        {
            proceduralMotionRoot.localPosition = baselineLocalPosition + new Vector3(
                offsetPixels.x * worldUnitsPerPixel,
                offsetPixels.y * worldUnitsPerPixel,
                0f);
        }

        if (proceduralMotionRoot != null)
        {
            proceduralMotionRoot.localRotation = baselineLocalRotation * Quaternion.Euler(0f, 0f, leanDegrees);
            proceduralMotionRoot.localScale = baselineLocalScale;
        }
    }

    private void StopVisualMotionLoop(bool restoreBaseline)
    {
        if (visualMotionLoop != null)
        {
            StopCoroutine(visualMotionLoop);
            visualMotionLoop = null;
        }

        if (restoreBaseline)
        {
            RestoreMotionBaseline();
        }
    }

    private void RestoreMotionBaseline()
    {
        if (!hasMotionBaseline)
        {
            return;
        }

        if (motionProjection != null && motionProjection.IsProjectionActive)
        {
            motionProjection.SetRoomLocalFootPoint(baselineProjectedFootPoint);
        }

        if (proceduralMotionRect != null)
        {
            proceduralMotionRect.anchoredPosition = baselineAnchoredPosition;
        }

        if (proceduralMotionRoot != null)
        {
            proceduralMotionRoot.localPosition = baselineLocalPosition;
            proceduralMotionRoot.localRotation = baselineLocalRotation;
            proceduralMotionRoot.localScale = baselineLocalScale;
        }

        hasMotionBaseline = false;
    }

    private void StopActionLoop()
    {
        if (actionLoop == null)
        {
            return;
        }

        StopCoroutine(actionLoop);
        actionLoop = null;
    }

    private void ResolveReferences()
    {
        if (actorState == null)
        {
            actorState = GetComponent<ActorRoomState>();
        }

        if (actorState == null)
        {
            actorState = GetComponentInParent<ActorRoomState>();
        }

        if (actorState == null)
        {
            actorState = GetComponentInChildren<ActorRoomState>(true);
        }

        if (animator == null)
        {
            GameObject actorObject = actorState != null ? actorState.gameObject : gameObject;
            animator = actorObject != null ? actorObject.GetComponentInChildren<Animator>(true) : null;
        }
    }

    private bool SetAnimatorBool(string parameterName, bool value)
    {
        if (!HasAnimatorBool(parameterName))
        {
            return false;
        }

        animator.SetBool(Animator.StringToHash(parameterName.Trim()), value);
        return true;
    }

    private bool SetAnimatorTrigger(string parameterName)
    {
        if (!HasAnimatorTrigger(parameterName))
        {
            return false;
        }

        animator.SetTrigger(Animator.StringToHash(parameterName.Trim()));
        return true;
    }

    private bool HasAnyAvailableTrigger()
    {
        if (randomActionTriggers == null)
        {
            return false;
        }

        for (int i = 0; i < randomActionTriggers.Length; i++)
        {
            if (HasAnimatorTrigger(randomActionTriggers[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasAnimatorBool(string parameterName)
    {
        return HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool);
    }

    private bool HasAnimatorTrigger(string parameterName)
    {
        return HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Trigger);
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null ||
            animator.runtimeAnimatorController == null ||
            !animator.isActiveAndEnabled ||
            string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        int hash = Animator.StringToHash(parameterName.Trim());
        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.nameHash == hash && parameter.type == parameterType)
            {
                return true;
            }
        }

        return false;
    }

    private void WarnMissingAnimationSetupOnce()
    {
        if (warnedMissingAnimationSetup)
        {
            return;
        }

        warnedMissingAnimationSetup = true;
        string actorName = actorState != null ? actorState.ActorId : name;
        Debug.LogWarning(
            $"Chapter 3 dining performance for '{actorName}' has no available eating/talking Animator parameters. " +
            "The guest will use procedural seated motion until eating parameters or triggers are added.",
            this);
    }

    private static bool IsTalkAction(string actionName)
    {
        return string.Equals(actionName?.Trim(), "Talk", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIdleAction(string actionName)
    {
        return string.Equals(actionName?.Trim(), "Idle", System.StringComparison.OrdinalIgnoreCase);
    }
}
