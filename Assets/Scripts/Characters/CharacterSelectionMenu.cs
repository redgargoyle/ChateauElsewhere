using System;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public sealed class CharacterSelectionMenu : MonoBehaviour
{
	private const int WindowId = 0x445347;

	private static CharacterSelectionMenu activeMenu;

	[Serializable]
	private sealed class CharacterOption
	{
		public string displayName = string.Empty;
		public RuntimeAnimatorController animatorController = null;
		public Sprite initialSprite = null;
	}

	[SerializeField] private string playerObjectName = "Player";
	[SerializeField] private CharacterOption[] characterOptions = Array.Empty<CharacterOption>();
	[SerializeField] private bool showOnStart = true;
	[SerializeField] private float windowWidth = 420f;
	[SerializeField] private float windowHeight = 235f;
	[SerializeField] private Rect changeButtonRect = new Rect(16f, 16f, 170f, 34f);

	private Animator playerAnimator;
	private SpriteRenderer playerSpriteRenderer;
	private Rect windowRect;
	private bool menuVisible;
	private string selectedCharacterName = "Current";

	public static bool IsBlockingGameplayInput(Vector2 screenPosition)
	{
		if (activeMenu == null || !activeMenu.isActiveAndEnabled)
			return false;

		return activeMenu.IsBlockingScreenPosition(screenPosition);
	}

	private void Awake()
	{
		activeMenu = this;
		menuVisible = showOnStart;
		ResolvePlayerReferences();
		CenterWindow();
	}

	private void OnEnable()
	{
		activeMenu = this;
	}

	private void OnDisable()
	{
		if (activeMenu == this)
			activeMenu = null;
	}

	private void OnDestroy()
	{
		if (activeMenu == this)
			activeMenu = null;
	}

	private void OnGUI()
	{
		if (Event.current != null && Event.current.type == EventType.Layout)
			CenterWindow();

		if (!menuVisible)
		{
			if (GUI.Button(changeButtonRect, $"Character: {selectedCharacterName}"))
				menuVisible = true;

			return;
		}

		windowRect = GUI.ModalWindow(WindowId, windowRect, DrawCharacterWindow, "Animation Test Character");
	}

	private void DrawCharacterWindow(int windowId)
	{
		GUILayout.Space(8f);
		GUILayout.Label("Select the controllable character for sprite-sheet animation testing.");
		GUILayout.Space(8f);

		if (characterOptions == null || characterOptions.Length == 0)
		{
			GUILayout.Label("No character options are assigned.");
		}
		else
		{
			for (int i = 0; i < characterOptions.Length; i++)
			{
				CharacterOption option = characterOptions[i];
				string label = option != null && !string.IsNullOrWhiteSpace(option.displayName)
					? option.displayName
					: $"Character {i + 1}";

				if (GUILayout.Button(label, GUILayout.Height(34f)))
					SelectCharacter(i);
			}
		}

		GUILayout.FlexibleSpace();

		if (GUILayout.Button("Keep Current", GUILayout.Height(28f)))
			menuVisible = false;
	}

	private void SelectCharacter(int index)
	{
		if (characterOptions == null || index < 0 || index >= characterOptions.Length)
			return;

		CharacterOption option = characterOptions[index];
		if (option == null)
			return;

		ResolvePlayerReferences();

		if (playerSpriteRenderer != null && option.initialSprite != null)
			playerSpriteRenderer.sprite = option.initialSprite;

		if (playerAnimator != null && option.animatorController != null)
		{
			playerAnimator.runtimeAnimatorController = option.animatorController;
			playerAnimator.Rebind();
			playerAnimator.Update(0f);
		}
		else
		{
			Debug.LogWarning($"Character option '{option.displayName}' is missing an Animator controller or player Animator.", this);
		}

		selectedCharacterName = string.IsNullOrWhiteSpace(option.displayName)
			? $"Character {index + 1}"
			: option.displayName;
		menuVisible = false;
	}

	private void ResolvePlayerReferences()
	{
		if (playerAnimator != null && playerSpriteRenderer != null)
			return;

		GameObject playerObject = null;
		if (!string.IsNullOrWhiteSpace(playerObjectName))
			playerObject = GameObject.Find(playerObjectName);

		if (playerObject == null)
		{
			PointClickPlayerMovement playerMovement = FindAnyObjectByType<PointClickPlayerMovement>();
			if (playerMovement != null)
				playerObject = playerMovement.gameObject;
		}

		if (playerObject == null)
			return;

		if (playerAnimator == null)
			playerAnimator = playerObject.GetComponent<Animator>();

		if (playerSpriteRenderer == null)
			playerSpriteRenderer = playerObject.GetComponentInChildren<SpriteRenderer>();
	}

	private void CenterWindow()
	{
		float width = Mathf.Max(260f, windowWidth);
		float height = Mathf.Max(180f, windowHeight);
		windowRect = new Rect(
			(Screen.width - width) * 0.5f,
			(Screen.height - height) * 0.5f,
			width,
			height);
	}

	private bool IsBlockingScreenPosition(Vector2 screenPosition)
	{
		if (menuVisible)
			return true;

		Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
		return changeButtonRect.Contains(guiPosition);
	}
}
