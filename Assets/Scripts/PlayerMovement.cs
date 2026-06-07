using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController2D))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
	private static readonly int SpeedHash = Animator.StringToHash("Speed");
	private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
	private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");

	public CharacterController2D controller;
	public Animator animator;

	public float runSpeed = 40f;

	float horizontalMove = 0f;
	bool jump = false;
	bool crouch = false;
	bool hasSpeedParameter = true;
	bool hasJumpingParameter = true;
	bool hasCrouchingParameter = true;

#if ENABLE_INPUT_SYSTEM
	Vector2 messageMove;
	bool messageCrouch;
	bool hasMessageInput;
#endif

	private void Awake()
	{
		EnsureReferences();
		CacheAnimatorParameters();
	}

	private void Reset()
	{
		EnsureReferences();
	}

	private void OnValidate()
	{
		EnsureReferences();
		CacheAnimatorParameters();
	}

	private void OnEnable()
	{
		EnsureReferences();

		if (controller != null)
		{
			controller.OnLandEvent.RemoveListener(OnLanding);
			controller.OnLandEvent.AddListener(OnLanding);
			controller.OnCrouchEvent.RemoveListener(OnCrouching);
			controller.OnCrouchEvent.AddListener(OnCrouching);
		}
	}

	private void OnDisable()
	{
		if (controller != null)
		{
			controller.OnLandEvent.RemoveListener(OnLanding);
			controller.OnCrouchEvent.RemoveListener(OnCrouching);
		}
	}

	// Update is called once per frame
	void Update ()
	{
		PlayerInputState input = ReadInput();

		horizontalMove = input.horizontal * runSpeed;
		crouch = input.crouchHeld;

		SetAnimatorFloat(SpeedHash, Mathf.Abs(horizontalMove), hasSpeedParameter);
		SetAnimatorBool(IsCrouchingHash, crouch, hasCrouchingParameter);

		if (input.jumpPressed)
		{
			jump = true;
			PreviewJumpAnimation();
		}
	}

#if ENABLE_INPUT_SYSTEM
	public void OnMove(InputValue value)
	{
		messageMove = value.Get<Vector2>();
		hasMessageInput = true;
	}

	public void OnJump(InputValue value)
	{
		if (value.Get<float>() > 0.5f)
		{
			jump = true;
			PreviewJumpAnimation();
		}
	}

	public void OnCrouch(InputValue value)
	{
		messageCrouch = value.Get<float>() > 0.5f;
		hasMessageInput = true;
	}
#endif

	public void OnLanding ()
	{
		SetAnimatorBool(IsJumpingHash, false, hasJumpingParameter);
	}

	public void OnCrouching (bool isCrouching)
	{
		crouch = isCrouching;
		SetAnimatorBool(IsCrouchingHash, isCrouching, hasCrouchingParameter);
	}

	void FixedUpdate ()
	{
		EnsureReferences();

		if (controller != null && controller.Move(horizontalMove * Time.fixedDeltaTime, crouch, jump))
			SetAnimatorBool(IsJumpingHash, true, hasJumpingParameter);

		jump = false;
	}

	private void EnsureReferences()
	{
		if (controller == null)
			TryGetComponent(out controller);

		if (animator == null)
			TryGetComponent(out animator);
	}

	private void CacheAnimatorParameters()
	{
		hasSpeedParameter = false;
		hasJumpingParameter = false;
		hasCrouchingParameter = false;

		if (animator == null || animator.runtimeAnimatorController == null || !animator.isInitialized)
			return;

		AnimatorControllerParameter[] parameters;
		try
		{
			parameters = animator.parameters;
		}
		catch (InvalidOperationException)
		{
			return;
		}

		foreach (AnimatorControllerParameter parameter in parameters)
		{
			if (parameter.nameHash == SpeedHash)
				hasSpeedParameter = true;
			else if (parameter.nameHash == IsJumpingHash)
				hasJumpingParameter = true;
			else if (parameter.nameHash == IsCrouchingHash)
				hasCrouchingParameter = true;
		}
	}

	private void PreviewJumpAnimation()
	{
		EnsureReferences();

		if (controller == null || controller.IsGrounded)
			SetAnimatorBool(IsJumpingHash, true, hasJumpingParameter);
	}

	private PlayerInputState ReadInput()
	{
		PlayerInputState input = new PlayerInputState();

#if ENABLE_LEGACY_INPUT_MANAGER
		input.horizontal = ReadLegacyAxis("Horizontal");
		input.jumpPressed = ReadLegacyButtonDown("Jump");
		input.crouchHeld = ReadLegacyCrouch();
#endif

#if ENABLE_INPUT_SYSTEM
		ReadInputSystem(ref input);

		if (hasMessageInput)
		{
			if (Mathf.Abs(messageMove.x) > Mathf.Abs(input.horizontal))
				input.horizontal = Mathf.Clamp(messageMove.x, -1f, 1f);

			input.crouchHeld |= messageCrouch;
		}
#endif

		return input;
	}

#if ENABLE_INPUT_SYSTEM
	private void ReadInputSystem(ref PlayerInputState input)
	{
		Keyboard keyboard = Keyboard.current;
		if (keyboard != null)
		{
			float keyboardMove = 0f;

			if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
				keyboardMove -= 1f;
			if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
				keyboardMove += 1f;

			if (Mathf.Abs(keyboardMove) > Mathf.Abs(input.horizontal))
				input.horizontal = keyboardMove;

			input.jumpPressed |= keyboard.spaceKey.wasPressedThisFrame;
			input.crouchHeld |= keyboard.cKey.isPressed || keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed || keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
		}

		Gamepad gamepad = Gamepad.current;
		if (gamepad != null)
		{
			float stickMove = gamepad.leftStick.x.ReadValue();
			if (Mathf.Abs(stickMove) > Mathf.Abs(input.horizontal))
				input.horizontal = stickMove;

			input.jumpPressed |= gamepad.buttonSouth.wasPressedThisFrame;
			input.crouchHeld |= gamepad.buttonEast.isPressed || gamepad.leftStick.down.isPressed || gamepad.dpad.down.isPressed;
		}
	}
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
	private static float ReadLegacyAxis(string axisName)
	{
		try
		{
			return Input.GetAxisRaw(axisName);
		}
		catch (ArgumentException)
		{
			return 0f;
		}
		catch (InvalidOperationException)
		{
			return 0f;
		}
	}

	private static bool ReadLegacyButtonDown(string buttonName)
	{
		try
		{
			return Input.GetButtonDown(buttonName);
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}

	private static bool ReadLegacyCrouch()
	{
		return Input.GetKey(KeyCode.C)
			|| Input.GetKey(KeyCode.S)
			|| Input.GetKey(KeyCode.DownArrow)
			|| Input.GetKey(KeyCode.LeftControl)
			|| Input.GetKey(KeyCode.RightControl);
	}
#endif

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

	private struct PlayerInputState
	{
		public float horizontal;
		public bool jumpPressed;
		public bool crouchHeld;
	}
}
