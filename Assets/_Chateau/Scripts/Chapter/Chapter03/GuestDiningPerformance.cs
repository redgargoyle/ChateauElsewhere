using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GuestDiningPerformance : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ActorRoomState actorState;
    [SerializeField] private Animator animator;

    [Header("Timing")]
    [SerializeField, Min(0.05f)] private float minActionDelay = 1.5f;
    [SerializeField, Min(0.05f)] private float maxActionDelay = 4.5f;
    [SerializeField, Min(0.05f)] private float talkingPulseSeconds = 0.85f;

    [Header("Procedural Fallback")]
    [SerializeField] private bool proceduralFallbackEnabled = true;
    [SerializeField, Min(0f)] private float seatedIdleBob = 0.01f;
    [SerializeField, Min(0f)] private float eatingBob = 0.025f;
    [SerializeField, Min(0f)] private float eatingLean = 0.018f;
    [SerializeField, Min(0f)] private float idleRotationDegrees = 0.25f;
    [SerializeField, Min(0f)] private float eatingRotationDegrees = 0.9f;

    [Header("Animator Parameters")]
    [SerializeField] private string eatingBool = "IsEating";
    [SerializeField] private string talkingBool = "IsTalking";
    [SerializeField]
    private string[] randomActionTriggers =
    {
        "Eat",
        "Utensil",
        "HeadMove",
        "Talk"
    };

    private Coroutine randomActionRoutine;
    private Coroutine proceduralMotionRoutine;
    private Transform proceduralVisualRoot;
    private Vector3 proceduralBaseLocalPosition;
    private Quaternion proceduralBaseLocalRotation;
    private bool hasProceduralBase;
    private bool dinnerInteractable;
    private float proceduralSeed;
    private bool warnedMissingAnimatorActions;

    public ActorRoomState ActorState => actorState;

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        StopEatingAndIdle();
        StopProceduralMotion(true);
    }

    public void AssignActorStateIfMissing(ActorRoomState actor)
    {
        if (actorState == null)
        {
            actorState = actor;
        }

        ResolveReferences();
    }

    public void ConfigureActionDelays(float minDelay, float maxDelay)
    {
        minActionDelay = Mathf.Max(0.05f, minDelay);
        maxActionDelay = Mathf.Max(minActionDelay, maxDelay);
    }

    public void SetDinnerInteractable(bool value)
    {
        dinnerInteractable = value;

        if (actorState != null)
        {
            actorState.SetInteractable(value);
            actorState.ApplyState();
        }
    }

    public void PrepareSeatedIdle()
    {
        ResolveReferences();

        if (actorState != null)
        {
            actorState.SetAvailableInCurrentChapter(true);
            actorState.SetVisibleByChapterState(true);
            actorState.SetInteractable(dinnerInteractable);
            actorState.SetSeated(true);
            actorState.ApplyState();
        }

        SetBoolIfPresent(eatingBool, false);
        SetBoolIfPresent(talkingBool, false);
        StartProceduralMotion(false);
    }

    public void BeginEating()
    {
        ResolveReferences();
        PrepareSeatedIdle();

        bool hasAnyAction = SetBoolIfPresent(eatingBool, true);
        hasAnyAction |= HasUsableTrigger();
        hasAnyAction |= HasBoolParameter(talkingBool);

        if (!hasAnyAction)
        {
            if (!proceduralFallbackEnabled)
            {
                WarnMissingAnimatorActionsOnce();
                return;
            }

            StartProceduralMotion(true);
            return;
        }

        StartProceduralMotion(true);

        if (randomActionRoutine != null)
        {
            StopCoroutine(randomActionRoutine);
        }

        randomActionRoutine = StartCoroutine(RandomActionLoop());
    }

    public void StopEatingAndIdle()
    {
        if (randomActionRoutine != null)
        {
            StopCoroutine(randomActionRoutine);
            randomActionRoutine = null;
        }

        ResolveReferences();
        SetBoolIfPresent(eatingBool, false);
        SetBoolIfPresent(talkingBool, false);

        if (actorState != null)
        {
            actorState.SetSeated(true);
            actorState.SetInteractable(dinnerInteractable);
            actorState.ApplyState();
        }

        StartProceduralMotion(false);
    }

    private IEnumerator ProceduralMotionLoop(bool eating)
    {
        Transform target = ResolveProceduralVisualRoot();

        if (target == null)
        {
            yield break;
        }

        proceduralVisualRoot = target;
        proceduralBaseLocalPosition = target.localPosition;
        proceduralBaseLocalRotation = target.localRotation;
        hasProceduralBase = true;

        float bobAmplitude = eating ? eatingBob : seatedIdleBob;
        float rotationAmplitude = eating ? eatingRotationDegrees : idleRotationDegrees;
        float leanAmplitude = eating ? eatingLean : 0f;

        while (true)
        {
            float t = Time.time + proceduralSeed;
            float slow = Mathf.Sin(t * 1.1f);
            float medium = Mathf.Sin(t * 1.75f + proceduralSeed * 0.37f);
            float bitePulse = eating ? Mathf.Max(0f, Mathf.Sin(t * 2.8f + proceduralSeed)) : 0f;

            Vector3 offset = new Vector3(
                medium * leanAmplitude,
                slow * bobAmplitude - bitePulse * eatingBob * 0.35f,
                0f);

            float rotation = medium * rotationAmplitude + bitePulse * rotationAmplitude * 0.35f;
            target.localPosition = proceduralBaseLocalPosition + offset;
            target.localRotation = proceduralBaseLocalRotation * Quaternion.Euler(0f, 0f, rotation);
            yield return null;
        }
    }

    private void StartProceduralMotion(bool eating)
    {
        if (!proceduralFallbackEnabled || !isActiveAndEnabled)
        {
            StopProceduralMotion(true);
            return;
        }

        StopProceduralMotion(true);
        proceduralMotionRoutine = StartCoroutine(ProceduralMotionLoop(eating));
    }

    private void StopProceduralMotion(bool resetToBase)
    {
        if (proceduralMotionRoutine != null)
        {
            StopCoroutine(proceduralMotionRoutine);
            proceduralMotionRoutine = null;
        }

        if (!resetToBase || !hasProceduralBase || proceduralVisualRoot == null)
        {
            return;
        }

        proceduralVisualRoot.localPosition = proceduralBaseLocalPosition;
        proceduralVisualRoot.localRotation = proceduralBaseLocalRotation;
        hasProceduralBase = false;
    }

    private Transform ResolveProceduralVisualRoot()
    {
        ResolveReferences();

        if (animator != null)
        {
            return animator.transform;
        }

        GameObject root = actorState != null ? actorState.gameObject : gameObject;
        Renderer[] renderers = root != null ? root.GetComponentsInChildren<Renderer>(true) : GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer != null && renderer.transform != null && !IsClickTarget(renderer.transform))
            {
                return renderer.transform;
            }
        }

        return root != null ? root.transform : transform;
    }

    private IEnumerator RandomActionLoop()
    {
        while (true)
        {
            float safeMin = Mathf.Max(0.05f, minActionDelay);
            float safeMax = Mathf.Max(safeMin, maxActionDelay);
            yield return new WaitForSeconds(Random.Range(safeMin, safeMax));
            yield return RunRandomActionOnce();
        }
    }

    private IEnumerator RunRandomActionOnce()
    {
        string triggerName = ChooseRandomTrigger();

        if (!string.IsNullOrWhiteSpace(triggerName) && SetTriggerIfPresent(triggerName))
        {
            yield break;
        }

        if (IsTalkAction(triggerName) && SetBoolIfPresent(talkingBool, true))
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, talkingPulseSeconds));
            SetBoolIfPresent(talkingBool, false);
            yield break;
        }

        if (SetBoolIfPresent(talkingBool, true))
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, talkingPulseSeconds));
            SetBoolIfPresent(talkingBool, false);
        }
    }

    private string ChooseRandomTrigger()
    {
        if (randomActionTriggers == null || randomActionTriggers.Length == 0)
        {
            return null;
        }

        return randomActionTriggers[Random.Range(0, randomActionTriggers.Length)];
    }

    private bool HasUsableTrigger()
    {
        if (randomActionTriggers == null)
        {
            return false;
        }

        for (int i = 0; i < randomActionTriggers.Length; i++)
        {
            if (HasTriggerParameter(randomActionTriggers[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool SetBoolIfPresent(string parameterName, bool value)
    {
        if (!HasBoolParameter(parameterName))
        {
            return false;
        }

        animator.SetBool(Animator.StringToHash(parameterName.Trim()), value);
        return true;
    }

    private bool SetTriggerIfPresent(string parameterName)
    {
        if (!HasTriggerParameter(parameterName))
        {
            return false;
        }

        animator.SetTrigger(Animator.StringToHash(parameterName.Trim()));
        return true;
    }

    private bool HasBoolParameter(string parameterName)
    {
        return HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool);
    }

    private bool HasTriggerParameter(string parameterName)
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

        int parameterHash = Animator.StringToHash(parameterName.Trim());
        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.nameHash == parameterHash && parameter.type == parameterType)
            {
                return true;
            }
        }

        return false;
    }

    private void WarnMissingAnimatorActionsOnce()
    {
        if (warnedMissingAnimatorActions)
        {
            return;
        }

        warnedMissingAnimatorActions = true;
        string actorName = actorState != null ? actorState.ActorId : name;
        Debug.LogWarning(
            $"Chapter 3 dining guest '{actorName}' has no usable eating/talking Animator parameters. " +
            "The guest will remain in seated idle until dining animation parameters are added.",
            this);
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

        if (animator == null && actorState != null)
        {
            animator = actorState.GetComponentInChildren<Animator>(true);
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (Mathf.Approximately(proceduralSeed, 0f))
        {
            string seedSource = actorState != null ? actorState.ActorId : name;
            proceduralSeed = Mathf.Abs(seedSource.GetHashCode() % 1000) * 0.01f + 0.01f;
        }
    }

    private static bool IsTalkAction(string actionName)
    {
        return !string.IsNullOrWhiteSpace(actionName) &&
            actionName.IndexOf("Talk", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsClickTarget(Transform candidate)
    {
        return candidate != null &&
            candidate.name.IndexOf("ClickTarget", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
