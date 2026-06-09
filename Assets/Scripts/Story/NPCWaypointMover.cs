using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class NPCWaypointMover : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float stopDistance = 0.03f;
    [SerializeField] private bool preserveStartingZ = true;
    [SerializeField, Range(0.1f, 1f)] private float horizontalDirectionThreshold = 0.55f;
    [SerializeField] private RoomPersonWalker2D ambientWalker;
    [SerializeField] private RoomProjectedEntity roomProjection;
    [SerializeField] private Animator animator;

    private Coroutine moveRoutine;
    private bool isMoving;
    private CharacterAnimatorDriver.ParameterCache animatorParameters;
    private CharacterWalkDirection walkDirection = CharacterWalkDirection.Down;

    public bool IsMoving => isMoving;
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
            if (!TryPlaceProjectedAtTarget(target))
            {
                transform.position = GetTargetPosition(target);
            }

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

        if (ambientWalker != null)
        {
            ambientWalker.enabled = false;
        }

        if (TryGetProjectedTarget(target, out Vector2 projectedTarget))
        {
            yield return MoveProjectedToRoutine(projectedTarget);
            yield break;
        }

        isMoving = true;
        Vector3 targetPosition = GetTargetPosition(target);

        while (Vector2.Distance(transform.position, targetPosition) > stopDistance)
        {
            Vector3 previousPosition = transform.position;
            Vector3 nextPosition = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime);
            transform.position = nextPosition;
            UpdateAnimator(nextPosition - previousPosition, true);
            yield return null;
        }

        transform.position = targetPosition;
        UpdateAnimator(Vector2.zero, false);
        isMoving = false;
        moveRoutine = null;
    }

    private IEnumerator MoveProjectedToRoutine(Vector2 targetFootPoint)
    {
        isMoving = true;

        while (roomProjection != null &&
            Vector2.Distance(roomProjection.RoomLocalFootPoint, targetFootPoint) > stopDistance)
        {
            Vector2 previousPosition = roomProjection.RoomLocalFootPoint;
            Vector2 nextPosition = Vector2.MoveTowards(
                previousPosition,
                targetFootPoint,
                moveSpeed * Time.deltaTime);
            roomProjection.SetRoomLocalFootPoint(nextPosition);
            UpdateAnimator(nextPosition - previousPosition, true);
            yield return null;
        }

        if (roomProjection != null)
        {
            roomProjection.SetRoomLocalFootPoint(targetFootPoint);
        }

        UpdateAnimator(Vector2.zero, false);
        isMoving = false;
        moveRoutine = null;
    }

    public void StopMoving()
    {
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        isMoving = false;
        UpdateAnimator(Vector2.zero, false);
    }

    public void SetAmbientWalkerEnabled(bool value)
    {
        ResolveReferences();

        if (ambientWalker != null)
        {
            ambientWalker.enabled = value;
        }
    }

    private Vector3 GetTargetPosition(Transform target)
    {
        Vector3 targetPosition = target.position;

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

        if (roomProjection == null)
        {
            roomProjection = GetComponentInChildren<RoomProjectedEntity>(true);
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        animatorParameters = CharacterAnimatorDriver.ParameterCache.FromAnimator(animator);
    }

    private bool TryPlaceProjectedAtTarget(Transform target)
    {
        ResolveReferences();

        if (roomProjection == null)
        {
            return false;
        }

        roomProjection.UseProfileFromRoomTarget(target);
        return roomProjection.IsProjectionActive &&
            roomProjection.CanProjectTarget(target) &&
            roomProjection.TrySetRoomLocalFootPointFromTarget(target);
    }

    private bool TryGetProjectedTarget(Transform target, out Vector2 targetFootPoint)
    {
        targetFootPoint = Vector2.zero;
        ResolveReferences();

        if (roomProjection == null)
        {
            return false;
        }

        roomProjection.UseProfileFromRoomTarget(target);
        return roomProjection.IsProjectionActive &&
            roomProjection.CanProjectTarget(target) &&
            roomProjection.TryGetRoomLocalFootPointForTarget(target, out targetFootPoint);
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
