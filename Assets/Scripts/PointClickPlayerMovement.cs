using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PointClickPlayerMovement : MonoBehaviour
{
	private static readonly int SpeedHash = Animator.StringToHash("Speed");
	private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
	private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
	private static readonly int IsWalkingUpHash = Animator.StringToHash("IsWalkingUp");
	private static readonly int IsWalkingDownHash = Animator.StringToHash("IsWalkingDown");
	private static readonly int IsWalkingLeftHash = Animator.StringToHash("IsWalkingLeft");
	private static readonly int IsWalkingRightHash = Animator.StringToHash("IsWalkingRight");
	private const float MovementEpsilon = 0.0001f;

	[SerializeField] private string walkableFloorName = "PlayerBoundary_Entrance";
	[SerializeField] private Collider2D walkableFloor;
	[SerializeField] private float moveSpeed = 3.2f;
	[SerializeField] private float stopDistance = 0.04f;
	[SerializeField, Range(0.5f, 1f)] private float horizontalDirectionThreshold = 0.58f;
	[SerializeField] private bool allowMovementWithoutWalkableFloor;
	[SerializeField] private string playerSortingLayerName = "People";
	[SerializeField] private int playerSortingOrderBase = 1000;
	[SerializeField] private float playerSortingOrderPerYUnit = 100f;
	[SerializeField] private float nearY = -4.25f;
	[SerializeField] private float farY = -2.25f;
	[SerializeField] private float nearScale = 0.85f;
	[SerializeField] private float farScale = 0.48f;
	[SerializeField] private float runningAnimationSpeed = 40f;
	[SerializeField] private bool disablePlatformMovement = true;

	private enum WalkDirection
	{
		Left,
		Right,
		Up,
		Down
	}

	private Rigidbody2D body;
	private Animator animator;
	private SpriteRenderer spriteRenderer;
	private SpriteRenderer[] spriteRenderers;
	private Vector2 destination;
	private WalkDirection walkDirection = WalkDirection.Right;
	private bool hasDestination;
	private bool isReady;
	private bool isWalking;
	private bool hasSpeedParameter = true;
	private bool hasJumpingParameter = true;
	private bool hasCrouchingParameter = true;
	private bool hasWalkingUpParameter = true;
	private bool hasWalkingDownParameter = true;
	private bool hasWalkingLeftParameter = true;
	private bool hasWalkingRightParameter = true;

	private void Awake()
	{
		CacheReferences();
		CacheAnimatorParameters();
	}

	private void Start()
	{
		FindWalkableFloor();

		if (walkableFloor == null && !allowMovementWithoutWalkableFloor)
		{
			enabled = false;
			return;
		}

		ConfigurePointAndClickMovement();
	}

	private void Update()
	{
		if (!isReady)
			return;

		if (TryGetFloorClick(out Vector2 clickPosition))
			SetDestination(clickPosition);

		UpdateAnimator();
	}

	private void FixedUpdate()
	{
		if (!isReady)
			return;

		MoveTowardDestination();
		ApplyPerspectiveScale();
		ApplyPlayerSorting();
	}

	private void CacheReferences()
	{
		if (body == null)
			TryGetComponent(out body);

		if (animator == null)
			TryGetComponent(out animator);

		if (spriteRenderer == null)
		{
			if (!TryGetComponent(out spriteRenderer))
				spriteRenderer = GetComponentInChildren<SpriteRenderer>();
		}

		if (spriteRenderers == null || spriteRenderers.Length == 0)
			spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
	}

	private void CacheAnimatorParameters()
	{
		if (animator == null || animator.runtimeAnimatorController == null)
			return;

		hasSpeedParameter = false;
		hasJumpingParameter = false;
		hasCrouchingParameter = false;
		hasWalkingUpParameter = false;
		hasWalkingDownParameter = false;
		hasWalkingLeftParameter = false;
		hasWalkingRightParameter = false;

		foreach (AnimatorControllerParameter parameter in animator.parameters)
		{
			if (parameter.nameHash == SpeedHash)
				hasSpeedParameter = true;
			else if (parameter.nameHash == IsJumpingHash)
				hasJumpingParameter = true;
			else if (parameter.nameHash == IsCrouchingHash)
				hasCrouchingParameter = true;
			else if (parameter.nameHash == IsWalkingUpHash)
				hasWalkingUpParameter = true;
			else if (parameter.nameHash == IsWalkingDownHash)
				hasWalkingDownParameter = true;
			else if (parameter.nameHash == IsWalkingLeftHash)
				hasWalkingLeftParameter = true;
			else if (parameter.nameHash == IsWalkingRightHash)
				hasWalkingRightParameter = true;
		}
	}

	private void FindWalkableFloor()
	{
		if (walkableFloor != null)
			return;

		GameObject floorObject = GameObject.Find(walkableFloorName);
		if (floorObject != null)
			walkableFloor = floorObject.GetComponent<Collider2D>();

		if (walkableFloor == null)
			walkableFloor = FindPlayerBoundaryCollider();
	}

	private void ConfigurePointAndClickMovement()
	{
		if (disablePlatformMovement)
		{
			if (TryGetComponent(out PlayerMovement platformMovement))
				platformMovement.enabled = false;

			if (TryGetComponent(out CharacterController2D platformController))
				platformController.enabled = false;
		}

		body.bodyType = RigidbodyType2D.Kinematic;
		body.gravityScale = 0f;
		body.freezeRotation = true;
		body.linearVelocity = Vector2.zero;
		body.angularVelocity = 0f;

		Vector2 startPosition = ClampToWalkableArea(body.position);
		body.position = startPosition;
		destination = startPosition;
		walkDirection = WalkDirection.Right;
		isReady = true;
		isWalking = false;

		SetAnimatorBool(IsJumpingHash, false, hasJumpingParameter);
		SetAnimatorBool(IsCrouchingHash, false, hasCrouchingParameter);
		SetAnimatorBool(IsWalkingUpHash, false, hasWalkingUpParameter);
		SetAnimatorBool(IsWalkingDownHash, false, hasWalkingDownParameter);
		SetAnimatorBool(IsWalkingLeftHash, false, hasWalkingLeftParameter);
		SetAnimatorBool(IsWalkingRightHash, false, hasWalkingRightParameter);
		ApplySpriteMirror();
		ApplyPerspectiveScale();
		ApplyPlayerSorting();
	}

	private bool TryGetFloorClick(out Vector2 clickPosition)
	{
		clickPosition = Vector2.zero;

		if (!TryGetPrimaryPointerDown(out Vector2 screenPosition))
			return false;

		Camera mainCamera = Camera.main;
		if (mainCamera == null)
			return false;

		Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenPosition);
		clickPosition = worldPosition;

		if (IsPickupObjectAtPoint(clickPosition))
			return false;

		return IsPointWalkable(clickPosition);
	}

	private static bool TryGetPrimaryPointerDown(out Vector2 screenPosition)
	{
		screenPosition = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
		Mouse mouse = Mouse.current;
		if (mouse != null && mouse.leftButton.wasPressedThisFrame)
		{
			screenPosition = mouse.position.ReadValue();
			return true;
		}
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
		try
		{
			if (!Input.GetMouseButtonDown(0))
				return false;

			screenPosition = Input.mousePosition;
			return true;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
#else
		return false;
#endif
	}

	private static bool IsPickupObjectAtPoint(Vector2 point)
	{
		Collider2D[] hits = Physics2D.OverlapPointAll(point);
		for (int i = 0; i < hits.Length; i++)
		{
			if (hits[i] != null && hits[i].GetComponentInParent<PickupObject>() != null)
				return true;
		}

		return false;
	}

	private void SetDestination(Vector2 clickPosition)
	{
		destination = clickPosition;
		Vector2 movement = destination - body.position;
		hasDestination = movement.magnitude > stopDistance;
		if (hasDestination)
			UpdateWalkDirection(movement);

		isWalking = hasDestination;
	}

	private void MoveTowardDestination()
	{
		if (!hasDestination)
		{
			isWalking = false;
			ApplySpriteMirror();
			return;
		}

		Vector2 currentPosition = body.position;
		Vector2 nextPosition = Vector2.MoveTowards(currentPosition, destination, moveSpeed * Time.fixedDeltaTime);
		Vector2 movement = nextPosition - currentPosition;

		if (!IsPointWalkable(nextPosition))
		{
			hasDestination = false;
			isWalking = false;
			ApplySpriteMirror();
			return;
		}

		body.MovePosition(nextPosition);
		UpdateWalkDirection(movement);

		if (Vector2.Distance(nextPosition, destination) <= stopDistance)
		{
			body.MovePosition(destination);
			hasDestination = false;
			isWalking = false;
			ApplySpriteMirror();
		}
		else
		{
			isWalking = true;
		}
	}

	private void ApplyPerspectiveScale()
	{
		float depth = Mathf.InverseLerp(nearY, farY, transform.position.y);
		float scale = Mathf.Lerp(nearScale, farScale, depth);
		transform.localScale = new Vector3(scale, scale, transform.localScale.z);
	}

	private void UpdateAnimator()
	{
		SetAnimatorFloat(SpeedHash, isWalking ? runningAnimationSpeed : 0f, hasSpeedParameter);
		SetAnimatorBool(IsJumpingHash, false, hasJumpingParameter);
		SetAnimatorBool(IsCrouchingHash, false, hasCrouchingParameter);
		SetAnimatorBool(IsWalkingUpHash, isWalking && walkDirection == WalkDirection.Up, hasWalkingUpParameter);
		SetAnimatorBool(IsWalkingDownHash, isWalking && walkDirection == WalkDirection.Down, hasWalkingDownParameter);
		SetAnimatorBool(IsWalkingLeftHash, isWalking && walkDirection == WalkDirection.Left, hasWalkingLeftParameter);
		SetAnimatorBool(IsWalkingRightHash, isWalking && walkDirection == WalkDirection.Right, hasWalkingRightParameter);
		ApplySpriteMirror();
	}

	private void UpdateWalkDirection(Vector2 movement)
	{
		if (movement.sqrMagnitude <= MovementEpsilon)
			return;

		walkDirection = DetermineWalkDirection(movement);
	}

	private WalkDirection DetermineWalkDirection(Vector2 movement)
	{
		float horizontalMagnitude = Mathf.Abs(movement.x);
		float verticalMagnitude = Mathf.Abs(movement.y);
		float totalMagnitude = horizontalMagnitude + verticalMagnitude;

		if (totalMagnitude <= MovementEpsilon)
			return walkDirection;

		if (horizontalMagnitude / totalMagnitude >= horizontalDirectionThreshold)
			return movement.x < 0f ? WalkDirection.Left : WalkDirection.Right;

		return movement.y >= 0f ? WalkDirection.Up : WalkDirection.Down;
	}

	private void ApplySpriteMirror()
	{
		if (spriteRenderer != null)
			spriteRenderer.flipX = false;
	}

	private bool IsPointWalkable(Vector2 point)
	{
		return walkableFloor == null || walkableFloor.OverlapPoint(point);
	}

	private Vector2 ClampToWalkableArea(Vector2 point)
	{
		if (walkableFloor == null || walkableFloor.OverlapPoint(point))
			return point;

		return walkableFloor.ClosestPoint(point);
	}

	private static Collider2D FindPlayerBoundaryCollider()
	{
#if UNITY_2023_1_OR_NEWER
		Collider2D[] colliders = FindObjectsByType<Collider2D>();
#else
		Collider2D[] colliders = FindObjectsOfType<Collider2D>();
#endif

		for (int i = 0; i < colliders.Length; i++)
		{
			Collider2D candidate = colliders[i];
			if (candidate == null || !candidate.enabled)
				continue;

			if (candidate.name.StartsWith("PlayerBoundary", StringComparison.OrdinalIgnoreCase))
				return candidate;
		}

		return null;
	}

	private void ApplyPlayerSorting()
	{
		if (spriteRenderers == null || spriteRenderers.Length == 0)
			return;

		string sortingLayerName = GetSortingLayerName(playerSortingLayerName);
		int sortingOrder = playerSortingOrderBase - Mathf.RoundToInt(transform.position.y * playerSortingOrderPerYUnit);

		for (int i = 0; i < spriteRenderers.Length; i++)
		{
			SpriteRenderer targetRenderer = spriteRenderers[i];
			if (targetRenderer == null)
				continue;

			targetRenderer.sortingLayerName = sortingLayerName;
			targetRenderer.sortingOrder = sortingOrder;
		}
	}

	private static string GetSortingLayerName(string requestedLayerName)
	{
		if (string.IsNullOrWhiteSpace(requestedLayerName))
			return "Default";

		if (string.Equals(requestedLayerName, "Default", StringComparison.OrdinalIgnoreCase) ||
			SortingLayer.NameToID(requestedLayerName) != 0)
			return requestedLayerName;

		return "Default";
	}

	private void SetAnimatorFloat(int parameterHash, float value, bool hasParameter)
	{
		if (animator != null && hasParameter)
			animator.SetFloat(parameterHash, value);
	}

	private void SetAnimatorBool(int parameterHash, bool value, bool hasParameter)
	{
		if (animator != null && hasParameter)
			animator.SetBool(parameterHash, value);
	}
}
