using UnityEngine;

public enum CharacterWalkDirection
{
	Left,
	Right,
	Up,
	Down
}

public static class CharacterAnimatorDriver
{
	private static readonly int SpeedHash = Animator.StringToHash("Speed");
	private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
	private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
	private static readonly int IsWalkingUpHash = Animator.StringToHash("IsWalkingUp");
	private static readonly int IsWalkingDownHash = Animator.StringToHash("IsWalkingDown");
	private static readonly int IsWalkingLeftHash = Animator.StringToHash("IsWalkingLeft");
	private static readonly int IsWalkingRightHash = Animator.StringToHash("IsWalkingRight");
	private const float MovementEpsilon = 0.0001f;

	public readonly struct ParameterCache
	{
		private readonly bool hasSpeed;
		private readonly bool hasJumping;
		private readonly bool hasCrouching;
		private readonly bool hasWalkingUp;
		private readonly bool hasWalkingDown;
		private readonly bool hasWalkingLeft;
		private readonly bool hasWalkingRight;

		private ParameterCache(
			bool hasSpeed,
			bool hasJumping,
			bool hasCrouching,
			bool hasWalkingUp,
			bool hasWalkingDown,
			bool hasWalkingLeft,
			bool hasWalkingRight)
		{
			this.hasSpeed = hasSpeed;
			this.hasJumping = hasJumping;
			this.hasCrouching = hasCrouching;
			this.hasWalkingUp = hasWalkingUp;
			this.hasWalkingDown = hasWalkingDown;
			this.hasWalkingLeft = hasWalkingLeft;
			this.hasWalkingRight = hasWalkingRight;
		}

		public static ParameterCache FromAnimator(Animator animator)
		{
			if (animator == null || animator.runtimeAnimatorController == null)
				return default;

			bool hasSpeed = false;
			bool hasJumping = false;
			bool hasCrouching = false;
			bool hasWalkingUp = false;
			bool hasWalkingDown = false;
			bool hasWalkingLeft = false;
			bool hasWalkingRight = false;

			foreach (AnimatorControllerParameter parameter in animator.parameters)
			{
				if (parameter.nameHash == SpeedHash)
					hasSpeed = true;
				else if (parameter.nameHash == IsJumpingHash)
					hasJumping = true;
				else if (parameter.nameHash == IsCrouchingHash)
					hasCrouching = true;
				else if (parameter.nameHash == IsWalkingUpHash)
					hasWalkingUp = true;
				else if (parameter.nameHash == IsWalkingDownHash)
					hasWalkingDown = true;
				else if (parameter.nameHash == IsWalkingLeftHash)
					hasWalkingLeft = true;
				else if (parameter.nameHash == IsWalkingRightHash)
					hasWalkingRight = true;
			}

			return new ParameterCache(
				hasSpeed,
				hasJumping,
				hasCrouching,
				hasWalkingUp,
				hasWalkingDown,
				hasWalkingLeft,
				hasWalkingRight);
		}

		public void ApplyMovement(
			Animator animator,
			bool isWalking,
			CharacterWalkDirection direction,
			float walkingSpeed)
		{
			if (animator == null)
				return;

			SetFloat(animator, SpeedHash, isWalking ? walkingSpeed : 0f, hasSpeed);
			SetBool(animator, IsJumpingHash, false, hasJumping);
			SetBool(animator, IsCrouchingHash, false, hasCrouching);
			SetBool(animator, IsWalkingUpHash, isWalking && direction == CharacterWalkDirection.Up, hasWalkingUp);
			SetBool(animator, IsWalkingDownHash, isWalking && direction == CharacterWalkDirection.Down, hasWalkingDown);
			SetBool(animator, IsWalkingLeftHash, isWalking && direction == CharacterWalkDirection.Left, hasWalkingLeft);
			SetBool(animator, IsWalkingRightHash, isWalking && direction == CharacterWalkDirection.Right, hasWalkingRight);
		}

		private static void SetFloat(Animator animator, int parameterHash, float value, bool hasParameter)
		{
			if (hasParameter)
				animator.SetFloat(parameterHash, value);
		}

		private static void SetBool(Animator animator, int parameterHash, bool value, bool hasParameter)
		{
			if (hasParameter)
				animator.SetBool(parameterHash, value);
		}
	}

	public static CharacterWalkDirection DetermineDirection(
		Vector2 movement,
		CharacterWalkDirection fallbackDirection,
		float horizontalDirectionThreshold)
	{
		float horizontalMagnitude = Mathf.Abs(movement.x);
		float verticalMagnitude = Mathf.Abs(movement.y);
		float totalMagnitude = horizontalMagnitude + verticalMagnitude;

		if (totalMagnitude <= MovementEpsilon)
			return fallbackDirection;

		if (horizontalMagnitude / totalMagnitude >= Mathf.Clamp01(horizontalDirectionThreshold))
			return movement.x < 0f ? CharacterWalkDirection.Left : CharacterWalkDirection.Right;

		return movement.y >= 0f ? CharacterWalkDirection.Up : CharacterWalkDirection.Down;
	}
}
