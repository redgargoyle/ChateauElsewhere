using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class NPCWaypointMover : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float stopDistance = 0.03f;
    [SerializeField] private bool preserveStartingZ = true;
    [SerializeField] private RoomPersonWalker2D ambientWalker;

    private Coroutine moveRoutine;
    private bool isMoving;

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
            transform.position = GetTargetPosition(target);
            return null;
        }

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
        }

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

        isMoving = true;
        Vector3 targetPosition = GetTargetPosition(target);

        while (Vector2.Distance(transform.position, targetPosition) > stopDistance)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = targetPosition;
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
    }
}
