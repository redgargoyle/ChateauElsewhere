using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NPCWaypointMover : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float stopDistance = 0.03f;
    [SerializeField] private bool preserveStartingZ = true;
    [SerializeField] private bool alignVisibleFeetToWaypoints;
    [SerializeField] private bool constrainToPlayerFloorBoundary = true;
    [SerializeField, Range(0.1f, 1f)] private float horizontalDirectionThreshold = 0.55f;
    [SerializeField] private RoomPersonWalker2D ambientWalker;
    [SerializeField] private ActorRoomState actorRoomState;
    [SerializeField] private CharacterAnimationPresenter animationPresenter;
    [SerializeField] private Animator animator;
    [SerializeField] private GuestFootstepAudio footstepAudio;

    private Coroutine moveRoutine;
    private bool isMoving;
    private int speechPauseCount;
    private NPCWaypointMover movementPausePartner;
    private Transform activeTarget;
    private bool reachedActiveTarget;
    private bool lastMoveFailed;
    private bool movementPauseApplied;
    private bool restoreAmbientWalkerAfterSpeechPause;
    private CharacterAnimatorDriver.ParameterCache animatorParameters;
    private CharacterWalkDirection walkDirection = CharacterWalkDirection.Down;
    private bool hasAnimationDirectionOverride;
    private CharacterWalkDirection animationDirectionOverride = CharacterWalkDirection.Down;
    private readonly List<Vector2> floorRouteLogicalPoints = new List<Vector2>();
    private PointClickPlayerMovement floorRoutePlanner;
    private Vector2 currentFloorRouteLogicalPosition;
    private Vector2 floorRouteRequestedTargetLogicalPosition;
    private int floorRouteIndex;

    public bool IsMoving => isMoving;
    public bool LastMoveFailed => lastMoveFailed;
    public bool IsSpeechPaused => speechPauseCount > 0;
    public bool IsMovementPaused => IsSpeechPaused ||
        (movementPausePartner != null && movementPausePartner.IsSpeechPaused);
    public bool AlignVisibleFeetToWaypoints
    {
        get => alignVisibleFeetToWaypoints;
        set => alignVisibleFeetToWaypoints = value;
    }
    public bool ConstrainToPlayerFloorBoundary
    {
        get => constrainToPlayerFloorBoundary;
        set => constrainToPlayerFloorBoundary = value;
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
        return MoveTo(target, false, CharacterWalkDirection.Down);
    }

    public Coroutine MoveTo(Transform target, CharacterWalkDirection animationDirection)
    {
        return MoveTo(target, true, animationDirection);
    }

    private Coroutine MoveTo(
        Transform target,
        bool overrideAnimationDirection,
        CharacterWalkDirection animationDirection)
    {
        SetAnimationDirectionOverride(overrideAnimationDirection, animationDirection);

        if (target == null)
        {
            Debug.LogWarning($"NPCWaypointMover on '{name}' missing required waypoint target.", this);
            return null;
        }

        if (!isActiveAndEnabled)
        {
            activeTarget = target;
            reachedActiveTarget = false;
            lastMoveFailed = false;
            ResolveReferences();
            ResetFloorRoute();

            if (!constrainToPlayerFloorBoundary)
            {
                ReleaseRoomStageBindingForTransformMotion();
                transform.position = GetTargetPosition(target);
                reachedActiveTarget = true;
            }
            else if (TryPrepareFloorRoute(target))
            {
                ReleaseRoomStageBindingForTransformMotion();
                reachedActiveTarget = TryApplyFloorLogicalPoint(
                    floorRouteLogicalPoints[floorRouteLogicalPoints.Count - 1]);
                lastMoveFailed = !reachedActiveTarget;

                if (reachedActiveTarget)
                {
                    BindCurrentFloorPointToRoomStage(target);
                }
            }
            else
            {
                lastMoveFailed = true;
                WarnFloorRouteFailure(target);
            }

            return null;
        }

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
        }

        ResolveReferences();
        moveRoutine = StartCoroutine(MoveToRoutineInternal(target));
        return moveRoutine;
    }

    public IEnumerator MoveToRoutine(Transform target)
    {
        SetAnimationDirectionOverride(false, CharacterWalkDirection.Down);
        return MoveToRoutineInternal(target);
    }

    public IEnumerator MoveToRoutine(Transform target, CharacterWalkDirection animationDirection)
    {
        SetAnimationDirectionOverride(true, animationDirection);
        return MoveToRoutineInternal(target);
    }

    private IEnumerator MoveToRoutineInternal(Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning($"NPCWaypointMover on '{name}' missing required waypoint target.", this);
            yield break;
        }

        ResolveReferences();
        ResetFloorRoute();
        activeTarget = target;
        reachedActiveTarget = false;
        lastMoveFailed = false;

        if (ambientWalker != null)
        {
            ambientWalker.enabled = false;
        }

        isMoving = true;
        bool releasedRoomStageBinding = false;
        bool floorRoutePrepared = false;
        bool floorRouteFailed = false;
        bool floorRoutingRequired = constrainToPlayerFloorBoundary;

        while (target != null)
        {
            if (TryApplyMovementPause())
            {
                yield return null;
                continue;
            }

            if (!floorRoutePrepared)
            {
                floorRoutePrepared = true;

                if (floorRoutingRequired && !TryPrepareFloorRoute(target))
                {
                    floorRouteFailed = true;
                    WarnFloorRouteFailure(target);
                    break;
                }
            }

            if (floorRoutingRequired)
            {
                if (!TryRefreshFloorRouteForLiveTarget(target))
                {
                    floorRouteFailed = true;
                    WarnFloorRouteFailure(target);
                    break;
                }

                if (floorRouteIndex >= floorRouteLogicalPoints.Count)
                {
                    break;
                }

                if (!releasedRoomStageBinding)
                {
                    // A queued walk can remain speech-paused at its authored room
                    // anchor for several frames. Keep that binding until this mover
                    // actually takes ownership of the transform for its first step.
                    ReleaseRoomStageBindingForTransformMotion();
                    releasedRoomStageBinding = true;
                }

                Vector3 previousFloorPosition = transform.position;

                if (!TryAdvanceFloorRoute(moveSpeed * Time.deltaTime, out bool reachedFloorRouteEnd))
                {
                    floorRouteFailed = true;
                    WarnFloorRouteFailure(target);
                    break;
                }

                UpdateAnimator(transform.position - previousFloorPosition, true);

                if (reachedFloorRouteEnd)
                {
                    break;
                }

                yield return null;
                continue;
            }

            // Only explicit opt-outs keep the legacy transform path. Gameplay
            // defaults to the active Butler planner and fails closed without it.
            Vector3 targetPosition = GetTargetPosition(target);

            if (Vector2.Distance(transform.position, targetPosition) <= stopDistance)
            {
                break;
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

        if (target != null && !floorRouteFailed && !floorRoutingRequired)
        {
            Vector3 targetPosition = GetTargetPosition(target);

            if (!releasedRoomStageBinding && transform.position != targetPosition)
            {
                ReleaseRoomStageBindingForTransformMotion();
            }

            transform.position = targetPosition;
        }

        if (floorRoutingRequired && releasedRoomStageBinding)
        {
            BindCurrentFloorPointToRoomStage(target);
        }

        UpdateAnimator(Vector2.zero, false);
        isMoving = false;
        moveRoutine = null;
        lastMoveFailed = floorRouteFailed;
        reachedActiveTarget = target != null && !floorRouteFailed;
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
        ResetFloorRoute();

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

    private bool TryPrepareFloorRoute(Transform target)
    {
        floorRoutePlanner = PointClickPlayerMovement.FindActiveRoutePlanner();
        return floorRoutePlanner != null && TryBuildFloorRoute(target);
    }

    private bool TryBuildFloorRoute(Transform target)
    {
        floorRouteLogicalPoints.Clear();
        floorRouteIndex = 0;

        if (floorRoutePlanner == null || target == null)
        {
            return false;
        }

        Vector2 currentFloorWorldPoint = GetCurrentFloorWorldPoint();

        if (!floorRoutePlanner.TryGetLogicalPositionFromWorldPoint(
                currentFloorWorldPoint,
                true,
                currentFloorWorldPoint,
                out Vector2 startLogicalPosition) ||
            !floorRoutePlanner.TryGetWorldPointFromLogicalPosition(startLogicalPosition, out Vector2 clampedStartWorldPoint) ||
            !TryResolveFloorRouteTarget(
                target,
                currentFloorWorldPoint,
                out Vector2 targetLogicalPosition,
                out Vector2 targetWorldPoint))
        {
            return false;
        }

        if (!floorRoutePlanner.TryBuildReachableLogicalPath(
                clampedStartWorldPoint,
                targetWorldPoint,
                true,
                floorRouteLogicalPoints))
        {
            return false;
        }

        currentFloorRouteLogicalPosition = startLogicalPosition;
        floorRouteRequestedTargetLogicalPosition = targetLogicalPosition;
        ApplyFloorWorldPoint(clampedStartWorldPoint);
        return true;
    }

    private bool TryResolveFloorRouteTarget(
        Transform target,
        Vector2 currentFloorWorldPoint,
        out Vector2 targetLogicalPosition,
        out Vector2 targetWorldPoint)
    {
        targetLogicalPosition = Vector2.zero;
        targetWorldPoint = Vector2.zero;

        if (floorRoutePlanner == null || target == null)
        {
            return false;
        }

        DoorTriggerNavigation door = target.GetComponent<DoorTriggerNavigation>();

        if (door != null)
        {
            return door.TryFindArrivalDestination(floorRoutePlanner, out targetLogicalPosition) &&
                floorRoutePlanner.TryGetWorldPointFromLogicalPosition(targetLogicalPosition, out targetWorldPoint);
        }

        return floorRoutePlanner.TryGetLogicalPositionFromWorldPoint(
                target.position,
                true,
                currentFloorWorldPoint,
                out targetLogicalPosition) &&
            floorRoutePlanner.TryGetWorldPointFromLogicalPosition(targetLogicalPosition, out targetWorldPoint);
    }

    private bool TryRefreshFloorRouteForLiveTarget(Transform target)
    {
        // Door approach points already live in the room's stable logical space.
        // Camera pan and zoom remap that route every frame without rebuilding it.
        if (target != null && target.GetComponent<DoorTriggerNavigation>() != null)
        {
            return true;
        }

        if (!floorRoutePlanner.TryGetWorldPointFromLogicalPosition(
                currentFloorRouteLogicalPosition,
                out Vector2 currentFloorWorldPoint) ||
            !TryResolveFloorRouteTarget(
                target,
                currentFloorWorldPoint,
                out Vector2 liveTargetLogicalPosition,
                out _))
        {
            return false;
        }

        if (Vector2.Distance(liveTargetLogicalPosition, floorRouteRequestedTargetLogicalPosition) <= stopDistance)
        {
            return true;
        }

        return TryBuildFloorRoute(target);
    }

    private bool TryAdvanceFloorRoute(float maxDistance, out bool reachedRouteEnd)
    {
        reachedRouteEnd = false;

        if (floorRoutePlanner == null)
        {
            return false;
        }

        while (floorRouteIndex < floorRouteLogicalPoints.Count &&
            Vector2.Distance(
                currentFloorRouteLogicalPosition,
                floorRouteLogicalPoints[floorRouteIndex]) <= stopDistance)
        {
            currentFloorRouteLogicalPosition = floorRouteLogicalPoints[floorRouteIndex];
            floorRouteIndex++;
        }

        if (floorRouteIndex >= floorRouteLogicalPoints.Count)
        {
            reachedRouteEnd = true;
            return TryApplyFloorLogicalPoint(currentFloorRouteLogicalPosition);
        }

        Vector2 routePoint = floorRouteLogicalPoints[floorRouteIndex];
        currentFloorRouteLogicalPosition = floorRoutePlanner.MoveLogicalPointToward(
            currentFloorRouteLogicalPosition,
            routePoint,
            Mathf.Max(0f, maxDistance));

        if (!floorRoutePlanner.TryGetWorldPointFromLogicalPosition(
                currentFloorRouteLogicalPosition,
                out Vector2 nextFloorWorldPoint))
        {
            return false;
        }

        ApplyFloorWorldPoint(nextFloorWorldPoint);

        if (Vector2.Distance(currentFloorRouteLogicalPosition, routePoint) <= stopDistance)
        {
            currentFloorRouteLogicalPosition = routePoint;
            floorRouteIndex++;

            if (!TryApplyFloorLogicalPoint(currentFloorRouteLogicalPosition))
            {
                return false;
            }
        }

        reachedRouteEnd = floorRouteIndex >= floorRouteLogicalPoints.Count;
        return true;
    }

    private bool TryApplyFloorLogicalPoint(Vector2 logicalPoint)
    {
        if (floorRoutePlanner == null ||
            !floorRoutePlanner.TryGetWorldPointFromLogicalPosition(
                logicalPoint,
                out Vector2 floorWorldPoint))
        {
            return false;
        }

        currentFloorRouteLogicalPosition = logicalPoint;
        ApplyFloorWorldPoint(floorWorldPoint);
        return true;
    }

    private Vector2 GetCurrentFloorWorldPoint()
    {
        if (CharacterFootPositionUtility.TryGetWorldPoint(
                gameObject,
                true,
                false,
                out Vector3 feetWorldPoint))
        {
            return feetWorldPoint;
        }

        return transform.position;
    }

    private void ApplyFloorWorldPoint(Vector2 floorWorldPoint)
    {
        Vector2 currentFloorWorldPoint = GetCurrentFloorWorldPoint();
        Vector2 floorOffset = currentFloorWorldPoint - (Vector2)transform.position;
        float targetZ = transform.position.z;
        transform.position = new Vector3(
            floorWorldPoint.x - floorOffset.x,
            floorWorldPoint.y - floorOffset.y,
            targetZ);
    }

    private void WarnFloorRouteFailure(Transform target)
    {
        Debug.LogWarning(
            $"NPCWaypointMover on '{name}' could not build a floor-bound route to " +
            $"'{(target != null ? target.name : "missing target")}'. The guest will remain on the current floor point.",
            this);
    }

    private void BindCurrentFloorPointToRoomStage(Transform target)
    {
        if (actorRoomState == null || target == null)
        {
            return;
        }

        actorRoomState.BindCurrentWorldFootPointToRoomStage(target);
    }

    private void ResetFloorRoute()
    {
        floorRouteLogicalPoints.Clear();
        floorRoutePlanner = null;
        currentFloorRouteLogicalPosition = Vector2.zero;
        floorRouteRequestedTargetLogicalPosition = Vector2.zero;
        floorRouteIndex = 0;
    }

    private void SetAnimationDirectionOverride(
        bool hasOverride,
        CharacterWalkDirection direction)
    {
        hasAnimationDirectionOverride = hasOverride;
        animationDirectionOverride = direction;
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

        if (animationPresenter == null)
        {
            animationPresenter = GetComponent<CharacterAnimationPresenter>();
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
        if (animationPresenter != null)
        {
            if (hasAnimationDirectionOverride)
            {
                if (isWalking)
                {
                    animationPresenter.BeginWalk(animationDirectionOverride, moveSpeed);
                }
                else
                {
                    animationPresenter.StopWalk(animationDirectionOverride);
                }
            }
            else
            {
                animationPresenter.ApplyMovement(
                    movement,
                    isWalking,
                    moveSpeed,
                    horizontalDirectionThreshold);
            }

            return;
        }

        if (animator == null)
        {
            return;
        }

        if (hasAnimationDirectionOverride)
        {
            walkDirection = animationDirectionOverride;
        }
        else if (movement.sqrMagnitude > 0.0001f)
        {
            walkDirection = CharacterAnimatorDriver.DetermineDirection(
                movement,
                walkDirection,
                horizontalDirectionThreshold);
        }

        animatorParameters.ApplyMovement(animator, isWalking, walkDirection, moveSpeed);
    }
}
