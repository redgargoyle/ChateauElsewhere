using TMPro;
using UnityEngine;

public class DoorPromptSequenceController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private DoorCameraSequence sequence;
    [SerializeField] private string sequenceResourcePath = "Navigation/DoorCameraSequence";

    [Header("References")]
    [SerializeField] private RoomNavigationManager navigationManager;
    [SerializeField] private Canvas canvas;
    [SerializeField] private TMP_Text promptText;

    [Header("Prompt")]
    [SerializeField] private string promptObjectName = "Text_OpenDoorPrompt";
    [SerializeField] private Vector2 promptSize = new Vector2(420f, 90f);
    [SerializeField] private float fontSize = 36f;
    [SerializeField] private Color promptColor = Color.white;
    [SerializeField] private bool showOnlyWhenSequenceCanAdvance = true;

    private bool subscribed;
    private bool subscribedToDoorHover;

    private void Awake()
    {
        ResolveReferences();
        EnsurePromptText();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsurePromptText();
        Subscribe();
        RefreshPrompt();
    }

    private void Start()
    {
        RefreshPrompt();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (promptText == null)
        {
            EnsurePromptText();
        }

        RefreshPrompt();
    }

    private void Subscribe()
    {
        if (!subscribed && navigationManager != null)
        {
            navigationManager.OnCurrentRoomChanged.AddListener(HandleRoomChanged);
            subscribed = true;
        }

        if (!subscribedToDoorHover)
        {
            DoorTriggerNavigation.HoveredTriggerChanged += HandleHoveredTriggerChanged;
            subscribedToDoorHover = true;
        }
    }

    private void Unsubscribe()
    {
        if (subscribed && navigationManager != null)
        {
            navigationManager.OnCurrentRoomChanged.RemoveListener(HandleRoomChanged);
            subscribed = false;
        }

        if (subscribedToDoorHover)
        {
            DoorTriggerNavigation.HoveredTriggerChanged -= HandleHoveredTriggerChanged;
            subscribedToDoorHover = false;
        }
    }

    private void HandleRoomChanged(string roomName)
    {
        RefreshPrompt();
    }

    private void HandleHoveredTriggerChanged(DoorTriggerNavigation doorTrigger)
    {
        RefreshPrompt();
    }

    private void ResolveReferences()
    {
        if (sequence == null && !string.IsNullOrWhiteSpace(sequenceResourcePath))
        {
            sequence = Resources.Load<DoorCameraSequence>(sequenceResourcePath);
        }

        if (navigationManager == null)
        {
            navigationManager = FindObjectOfType<RoomNavigationManager>();
        }

        if (canvas == null)
        {
            canvas = FindPreferredCanvas();
        }
    }

    private void EnsurePromptText()
    {
        if (promptText == null)
        {
            promptText = FindNamedPrompt();
        }

        if (promptText == null)
        {
            CreatePromptText();
        }

        ConfigurePromptText();
    }

    private TMP_Text FindNamedPrompt()
    {
        TMP_Text[] texts = FindObjectsOfType<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];

            if (text != null && text.name == promptObjectName)
            {
                return text;
            }
        }

        return null;
    }

    private void CreatePromptText()
    {
        if (canvas == null)
        {
            return;
        }

        GameObject promptObject = new GameObject(promptObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        promptObject.transform.SetParent(canvas.transform, false);
        promptText = promptObject.GetComponent<TMP_Text>();
    }

    private void ConfigurePromptText()
    {
        if (promptText == null)
        {
            return;
        }

        RectTransform rectTransform = promptText.transform as RectTransform;

        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = promptSize;
            rectTransform.localScale = Vector3.one;
        }

        promptText.text = sequence != null ? sequence.PromptText : "Open Door";
        promptText.fontSize = fontSize;
        promptText.color = promptColor;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.raycastTarget = false;
        promptText.gameObject.transform.SetAsLastSibling();
    }

    private void RefreshPrompt()
    {
        if (promptText == null)
        {
            return;
        }

        DoorTriggerNavigation hoveredTrigger = DoorTriggerNavigation.HoveredTrigger;
        bool shouldShow = hoveredTrigger != null && hoveredTrigger.isActiveAndEnabled;

        if (shouldShow && showOnlyWhenSequenceCanAdvance)
        {
            shouldShow = sequence != null &&
                navigationManager != null &&
                sequence.ContainsRoom(navigationManager.CurrentRoom);
        }

        promptText.text = sequence != null ? sequence.PromptText : "Open Door";
        promptText.gameObject.SetActive(shouldShow);
        promptText.transform.SetAsLastSibling();
    }

    private Canvas FindPreferredCanvas()
    {
        GameObject backgroundCanvas = GameObject.Find("Canvas_Background");

        if (backgroundCanvas != null && backgroundCanvas.TryGetComponent(out Canvas foundCanvas))
        {
            return foundCanvas;
        }

        return FindObjectOfType<Canvas>();
    }
}
