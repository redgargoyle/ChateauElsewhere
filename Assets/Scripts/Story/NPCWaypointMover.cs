using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class NPCWaypointMover : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float stopDistance = 0.03f;
    [SerializeField] private bool preserveStartingZ = true;
    [SerializeField] private bool alignVisibleFeetToWaypoints;
    [SerializeField, Range(0.1f, 1f)] private float horizontalDirectionThreshold = 0.55f;
    [SerializeField] private RoomPersonWalker2D ambientWalker;
    [SerializeField] private ActorRoomState actorRoomState;
    [SerializeField] private Animator animator;
    [SerializeField] private GuestFootstepAudio footstepAudio;

    private Coroutine moveRoutine;
    private bool isMoving;
    private int speechPauseCount;
    private NPCWaypointMover movementPausePartner;
    private Transform activeTarget;
    private bool reachedActiveTarget;
    private bool movementPauseApplied;
    private bool restoreAmbientWalkerAfterSpeechPause;
    private CharacterAnimatorDriver.ParameterCache animatorParameters;
    private CharacterWalkDirection walkDirection = CharacterWalkDirection.Down;

    public bool IsMoving => isMoving;
    public bool IsSpeechPaused => speechPauseCount > 0;
    public bool IsMovementPaused => IsSpeechPaused ||
        (movementPausePartner != null && movementPausePartner.IsSpeechPaused);
    public bool AlignVisibleFeetToWaypoints
    {
        get => alignVisibleFeetToWaypoints;
        set => alignVisibleFeetToWaypoints = value;
    }
    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = Mathf.Max(0.01f, value);
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0.01f, moveSpeed);
        stopDistance = Mathf.Max(0.001f, stopDistance);
    }

    public Coroutine MoveTo(Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning($"NPCWaypointMover on '{name}' missing required waypoint target.", this);
            return null;
        }

        if (!isActiveAndEnabled)
        {
            activeTarget = target;
            reachedActiveTarget = false;
            ReleaseRoomStageBindingForTransformMotion();
            transform.position = GetTargetPosition(target);
            reachedActiveTarget = true;
            return null;
        }

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
        }

        ResolveReferences();
        moveRoutine = StartCoroutine(MoveToRoutine(target));
        return moveRoutine;
    }

    public IEnumerator MoveToRoutine(Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning($"NPCWaypointMover on '{name}' missing required waypoint target.", this);
            yield break;
        }

        ResolveReferences();
        activeTarget = target;
        reachedActiveTarget = false;

        if (ambientWalker != null)
        {
            ambientWalker.enabled = false;
        }

        isMoving = true;
        bool releasedRoomStageBinding = false;

        while (target != null)
        {
            Vector3 targetPosition = GetTargetPosition(target);

            if (Vector2.Distance(transform.position, targetPosition) <= stopDistance)
            {
                break;
            }

            if (TryApplyMovementPause())
            {
                yield return null;
                continue;
            }

            if (!releasedRoomStageBinding)
            {
                // A queued walk can remain speech-paused at its authored room
                // anchor for several frames. Keep that binding until this mover
                // actually takes ownership of the transform for its first step.
                ReleaseRoomStageBindingForTransformMotion();
                releasedRoomStageBinding = true;
            }

            Vector3 previousPosition = transform.position;
            Vector3 nextPosition = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime);
            transform.position = nextPosition;
            UpdateAnimator(nextPosition - previousPosition, true);
            yield return null;
        }

        if (target != null)
        {
            Vector3 targetPosition = GetTargetPosition(target);

            if (!releasedRoomStageBinding && transform.position != targetPosition)
            {
                ReleaseRoomStageBindingForTransformMotion();
            }

            transform.position = targetPosition;
        }

        UpdateAnimator(Vector2.zero, false);
        isMoving = false;
        moveRoutine = null;
        reachedActiveTarget = target != null;
    }

    public void StopMoving()
    {
        bool interruptedMove = isMoving;

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        isMoving = false;
        movementPauseApplied = false;

        if (interruptedMove)
        {
            reachedActiveTarget = false;
        }

        UpdateAnimator(Vector2.zero, false);
    }

    public bool HasReachedTarget(Transform target)
    {
        return target != null &&
            activeTarget == target &&
            reachedActiveTarget &&
            !isMoving;
    }

    public void SetMovementPausePartner(NPCWaypointMover partner)
    {
        movementPausePartner = partner != this ? partner : null;

        if (IsMovementPaused)
        {
            ApplyMovementPauseIdle();
        }
    }

    public void ClearMovementPausePartner(NPCWaypointMover expectedPartner = null)
    {
        if (expectedPartner != null && movementPausePartner != expectedPartner)
        {
            return;
        }

        movementPausePartner = null;
        ResumeMovementAfterPauseIfReady();
    }

    public void AcquireSpeechPause()
    {
        ResolveReferences();
        speechPauseCount++;

        if (speechPauseCount > 1)
        {
            return;
        }

        restoreAmbientWalkerAfterSpeechPause = ambientWalker != null && ambientWalker.enabled;

        if (restoreAmbientWalkerAfterSpeechPause)
        {
            ambientWalker.enabled = false;
        }

        ApplyMovementPauseIdle();
    }

    public void ReleaseSpeechPause()
    {
        if (speechPauseCount <= 0)
        {
            return;
        }

        speechPauseCount--;

        if (speechPauseCount > 0)
        {
            return;
        }

        bool shouldRestoreAmbientWalker = restoreAmbientWalkerAfterSpeechPause && !isMoving;
        restoreAmbientWalkerAfterSpeechPause = false;

        if (shouldRestoreAmbientWalker && ambientWalker != null)
        {
            ambientWalker.enabled = true;
        }

        ResumeMovementAfterPauseIfReady();
    }

    public void SetAmbientWalkerEnabled(bool value)
    {
        ResolveReferences();

        if (ambientWalker != null)
        {
            ambientWalker.enabled = value;
        }
    }

    private void ReleaseRoomStageBindingForTransformMotion()
    {
        ResolveReferences();

        if (actorRoomState != null)
        {
            actorRoomState.ClearRoomStagePointBinding();
        }
    }

    private Vector3 GetTargetPosition(Transform target)
    {
        Vector3 targetPosition = target.position;

        if (alignVisibleFeetToWaypoints &&
            CharacterFootPositionUtility.TryGetWorldPoint(gameObject, true, false, out Vector3 feetWorldPoint))
        {
            Vector3 feetOffset = feetWorldPoint - transform.position;
            targetPosition.x -= feetOffset.x;
            targetPosition.y -= feetOffset.y;
        }

        if (preserveStartingZ)
        {
            targetPosition.z = transform.position.z;
        }

        return targetPosition;
    }

    private void ResolveReferences()
    {
        if (ambientWalker == null)
        {
            ambientWalker = GetComponent<RoomPersonWalker2D>();
        }

        if (actorRoomState == null)
        {
            actorRoomState = GetComponent<ActorRoomState>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (footstepAudio == null)
        {
            footstepAudio = GetComponentInChildren<GuestFootstepAudio>(true);
        }

        animatorParameters = CharacterAnimatorDriver.ParameterCache.FromAnimator(animator);
    }

    private bool TryApplyMovementPause()
    {
        if (!IsMovementPaused)
        {
            ResumeMovementAfterPauseIfReady();
            return false;
        }

        ApplyMovementPauseIdle();
        return true;
    }

    private void ApplyMovementPauseIdle()
    {
        movementPauseApplied = true;
        footstepAudio?.StopWalking();
        UpdateAnimator(Vector2.zero, false);
    }

    private void ResumeMovementAfterPauseIfReady()
    {
        if (!movementPauseApplied || IsMovementPaused)
        {
            return;
        }

        movementPauseApplied = false;

        if (isMoving)
        {
            footstepAudio?.PlayWalking();
        }
    }

    private void UpdateAnimator(Vector2 movement, bool isWalking)
    {
        if (animator == null)
        {
            return;
        }

        if (movement.sqrMagnitude > 0.0001f)
        {
            walkDirection = CharacterAnimatorDriver.DetermineDirection(
                movement,
                walkDirection,
                horizontalDirectionThreshold);
        }

        animatorParameters.ApplyMovement(animator, isWalking, walkDirection, moveSpeed);
    }
}
