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
    private bool warnedMissingAnimatorActions;

    public ActorRoomState ActorState => actorState;

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        StopEatingAndIdle();
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

    public void PrepareSeatedIdle()
    {
        ResolveReferences();

        if (actorState != null)
        {
            actorState.SetAvailableInCurrentChapter(true);
            actorState.SetVisibleByChapterState(true);
            actorState.SetInteractable(false);
            actorState.SetSeated(true);
            actorState.ApplyState();
        }

        SetBoolIfPresent(eatingBool, false);
        SetBoolIfPresent(talkingBool, false);
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
            WarnMissingAnimatorActionsOnce();
            return;
        }

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
            actorState.SetInteractable(false);
            actorState.ApplyState();
        }
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
    }

    private static bool IsTalkAction(string actionName)
    {
        return !string.IsNullOrWhiteSpace(actionName) &&
            actionName.IndexOf("Talk", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
