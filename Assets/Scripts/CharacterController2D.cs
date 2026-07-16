using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody2D))]
public class CharacterController2D : MonoBehaviour
{
	[SerializeField] private float m_JumpForce = 400f;							// Amount of force added when the player jumps.
	[Range(0, 1)] [SerializeField] private float m_CrouchSpeed = .36f;			// Amount of maxSpeed applied to crouching movement. 1 = 100%
	[Range(0, .3f)] [SerializeField] private float m_MovementSmoothing = .05f;	// How much to smooth out the movement
	[SerializeField] private bool m_AirControl = false;							// Whether or not a player can steer while jumping;
	[SerializeField] private LayerMask m_WhatIsGround;							// A mask determining what is ground to the character
	[SerializeField] private Transform m_GroundCheck;							// A position marking where to check if the player is grounded.
	[SerializeField] private Transform m_CeilingCheck;							// A position marking where to check for ceilings
	[SerializeField] private Collider2D m_CrouchDisableCollider;				// A collider that will be disabled when crouching
	[SerializeField] private float m_GroundCheckDelayAfterJump = .08f;			// Prevents instant landing detection while the jump impulse is still near the floor.

	const float k_GroundedRadius = .2f; // Radius of the overlap circle to determine if grounded
	private bool m_Grounded;            // Whether or not the player is grounded.
	const float k_CeilingRadius = .2f; // Radius of the overlap circle to determine if the player can stand up
	private Rigidbody2D m_Rigidbody2D;
	private Collider2D m_Collider2D;
	private SpriteRenderer[] m_SpriteRenderers;
	private bool m_FacingRight = true;  // For determining which way the player is currently facing.
	private Vector3 m_Velocity = Vector3.zero;
	private float m_IgnoreGroundCheckTimer;

	[System.Serializable]
	public class BoolEvent : UnityEvent<bool> { }

	[Header("Events")]
	[Space]

	public UnityEvent OnLandEvent = new UnityEvent();
	public BoolEvent OnCrouchEvent = new BoolEvent();
	private bool m_wasCrouching = false;

	public bool IsGrounded => m_Grounded;

	private void Awake()
	{
		m_Rigidbody2D = GetComponent<Rigidbody2D>();
		if (m_Rigidbody2D == null)
			m_Rigidbody2D = gameObject.AddComponent<Rigidbody2D>();

		m_Rigidbody2D.freezeRotation = true;

		m_Collider2D = GetComponent<Collider2D>();
		if (m_Collider2D == null)
			m_Collider2D = gameObject.AddComponent<BoxCollider2D>();

		m_SpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

		if (OnLandEvent == null)
			OnLandEvent = new UnityEvent();

		if (OnCrouchEvent == null)
			OnCrouchEvent = new BoolEvent();
	}

	private void FixedUpdate()
	{
		if (m_IgnoreGroundCheckTimer > 0f)
		{
			m_IgnoreGroundCheckTimer -= Time.fixedDeltaTime;
			m_Grounded = false;
			return;
		}

		bool wasGrounded = m_Grounded;
		m_Grounded = false;

		// The player is grounded if a circlecast to the groundcheck position hits anything designated as ground
		// This can be done using layers instead but Sample Assets will not overwrite your project settings.
		Collider2D[] colliders = Physics2D.OverlapCircleAll(GetGroundCheckPosition(), k_GroundedRadius, GetGroundMask());
		for (int i = 0; i < colliders.Length; i++)
		{
			if (!IsOwnCollider(colliders[i]))
			{
				m_Grounded = true;
				break;
			}
		}

		if (m_Grounded && !wasGrounded)
			OnLandEvent.Invoke();
	}


	public bool Move(float move, bool crouch, bool jump)
	{
		// If crouching, check to see if the character can stand up
		if (!crouch)
		{
			// If the character has a ceiling preventing them from standing up, keep them crouching
			if (HasCeilingBlocker())
			{
				crouch = true;
			}
		}

		if (m_Rigidbody2D == null)
			return false;

		bool jumped = false;

		//only control the player if grounded or airControl is turned on
		if (m_Grounded || m_AirControl)
		{

			// If crouching
			if (crouch)
			{
				if (!m_wasCrouching)
				{
					m_wasCrouching = true;
					OnCrouchEvent.Invoke(true);
				}

				// Reduce the speed by the crouchSpeed multiplier
				move *= m_CrouchSpeed;

				// Disable one of the colliders when crouching
				if (m_CrouchDisableCollider != null)
					m_CrouchDisableCollider.enabled = false;
			} else
			{
				// Enable the collider when not crouching
				if (m_CrouchDisableCollider != null)
					m_CrouchDisableCollider.enabled = true;

				if (m_wasCrouching)
				{
					m_wasCrouching = false;
					OnCrouchEvent.Invoke(false);
				}
			}

			// Move the character by finding the target velocity
			Vector3 targetVelocity = new Vector2(move * 10f, m_Rigidbody2D.linearVelocity.y);
			// And then smoothing it out and applying it to the character
			m_Rigidbody2D.linearVelocity = Vector3.SmoothDamp(m_Rigidbody2D.linearVelocity, targetVelocity, ref m_Velocity, m_MovementSmoothing);

			// If the input is moving the player right and the player is facing left...
			if (move > 0 && !m_FacingRight)
			{
				// ... flip the player.
				Flip();
			}
			// Otherwise if the input is moving the player left and the player is facing right...
			else if (move < 0 && m_FacingRight)
			{
				// ... flip the player.
				Flip();
			}
		}
		// If the player should jump...
		if (m_Grounded && jump)
		{
			// Add a vertical force to the player.
			m_Grounded = false;
			m_IgnoreGroundCheckTimer = m_GroundCheckDelayAfterJump;
			m_Rigidbody2D.AddForce(new Vector2(0f, m_JumpForce));
			jumped = true;
		}

		return jumped;
	}

	private Vector2 GetGroundCheckPosition()
	{
		if (m_GroundCheck != null)
			return m_GroundCheck.position;

		if (m_Collider2D != null)
			return new Vector2(m_Collider2D.bounds.center.x, m_Collider2D.bounds.min.y);

		return (Vector2)transform.position + Vector2.down * 0.5f;
	}

	private Vector2 GetCeilingCheckPosition()
	{
		if (m_CeilingCheck != null)
			return m_CeilingCheck.position;

		if (m_Collider2D != null)
			return new Vector2(m_Collider2D.bounds.center.x, m_Collider2D.bounds.max.y);

		return (Vector2)transform.position + Vector2.up * 0.5f;
	}

	private int GetGroundMask()
	{
		return m_WhatIsGround.value == 0 ? Physics2D.AllLayers : m_WhatIsGround.value;
	}

	private bool HasCeilingBlocker()
	{
		Collider2D[] colliders = Physics2D.OverlapCircleAll(GetCeilingCheckPosition(), k_CeilingRadius, GetGroundMask());
		for (int i = 0; i < colliders.Length; i++)
		{
			if (!IsOwnCollider(colliders[i]))
				return true;
		}

		return false;
	}

	private bool IsOwnCollider(Collider2D collider)
	{
		return collider == null || collider.gameObject == gameObject || collider.transform.IsChildOf(transform);
	}


	private void Flip()
	{
		// Switch the way the player is labelled as facing.
		m_FacingRight = !m_FacingRight;

		// Mirroring is a renderer concern; CharacterAnimationDisplay alone owns body size.
		if (m_SpriteRenderers == null || m_SpriteRenderers.Length == 0)
			m_SpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

		for (int i = 0; i < m_SpriteRenderers.Length; i++)
		{
			SpriteRenderer targetRenderer = m_SpriteRenderers[i];
			if (targetRenderer != null)
				targetRenderer.flipX = !targetRenderer.flipX;
		}
	}
}
