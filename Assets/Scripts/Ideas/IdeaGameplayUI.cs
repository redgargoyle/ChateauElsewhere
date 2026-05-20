using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

[DefaultExecutionOrder(-60)]
public class IdeaGameplayUI : MonoBehaviour
{
    private sealed class IdeaRow
    {
        public string IdeaId;
        public TextMeshProUGUI StatusText;
    }

    private const int SortingOrder = 5000;
    private const string TutorialIdeaId = "inheritance";

    [SerializeField] private Vector2 referenceResolution = new Vector2(1366f, 768f);
    [SerializeField] private bool showTutorialOnNewGame = true;

    private readonly List<IdeaRow> ideaRows = new List<IdeaRow>();
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    private IdeaManager ideaManager;
    private Canvas canvas;
    private GameObject panelObject;
    private GameObject tutorialObject;
    private TextMeshProUGUI activeIdeaText;
    private TextMeshProUGUI panelStatusText;
    private TextMeshProUGUI selectionText;
    private TextMeshProUGUI placementStatusText;
    private TextMeshProUGUI tutorialTitleText;
    private TextMeshProUGUI tutorialBodyText;
    private TextMeshProUGUI tutorialPrimaryText;
    private TextMeshProUGUI tutorialSecondaryText;
    private Button tutorialPrimaryButton;
    private Button tutorialSecondaryButton;
    private bool placementMode;
    private int placedItemCount;
    private int tutorialStep = -1;

    private Color PanelColor => new Color(0.045f, 0.05f, 0.055f, 0.92f);
    private Color SoftPanelColor => new Color(0.08f, 0.085f, 0.09f, 0.88f);
    private Color TextColor => new Color(0.92f, 0.9f, 0.84f, 1f);
    private Color MutedTextColor => new Color(0.68f, 0.67f, 0.62f, 1f);
    private Color AccentColor => new Color(0.68f, 0.52f, 0.25f, 1f);
    private Color BlueAccentColor => new Color(0.25f, 0.58f, 0.72f, 1f);
    private Color RedAccentColor => new Color(0.56f, 0.16f, 0.18f, 1f);

    private void Awake()
    {
        ideaManager = IdeaManager.GetOrCreate();
        EnsureEventSystem();
        BuildUi();
    }

    private void OnEnable()
    {
        IdeaManager.AnyCurrentIdeaChanged += HandleCurrentIdeaChanged;
        IdeaWorldObject.SelectedObjectChanged += HandleSelectedObjectChanged;
        DoorTriggerNavigation.HoveredTriggerChanged += HandleHoveredDoorChanged;
    }

    private void Start()
    {
        RefreshIdeaUi();
        RefreshSelection(null);
        SetPanelOpen(false);

        if (showTutorialOnNewGame && IdeaGameFlow.ConsumeTutorialRequest())
        {
            StartTutorial();
        }
    }

    private void Update()
    {
        if (!placementMode || !Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (IsPointerOverThisUi(Input.mousePosition))
        {
            return;
        }

        PlaceItem(Input.mousePosition);
    }

    private void OnDisable()
    {
        IdeaManager.AnyCurrentIdeaChanged -= HandleCurrentIdeaChanged;
        IdeaWorldObject.SelectedObjectChanged -= HandleSelectedObjectChanged;
        DoorTriggerNavigation.HoveredTriggerChanged -= HandleHoveredDoorChanged;
    }

    private void BuildUi()
    {
        canvas = GetComponent<Canvas>();

        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler canvasScaler = GetComponent<CanvasScaler>();

        if (canvasScaler == null)
        {
            canvasScaler = gameObject.AddComponent<CanvasScaler>();
        }

        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = referenceResolution;
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        RectTransform root = transform as RectTransform;

        if (root != null)
        {
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.localScale = Vector3.one;
        }

        BuildWorldTint();
        BuildTopStatus();
        BuildIdeasPanel();
        BuildSelectionBar();
        BuildTutorial();
    }

    private void BuildWorldTint()
    {
        RectTransform tintRect = CreateRect("IdeaWorldTint", transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image tintImage = tintRect.gameObject.AddComponent<Image>();
        tintImage.color = Color.clear;
        tintImage.raycastTarget = false;

        tintRect.gameObject.AddComponent<IdeaWorldTint>();
        tintRect.SetAsFirstSibling();
    }

    private void BuildTopStatus()
    {
        RectTransform topBar = CreatePanel("Panel_IdeaStatus", transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(430f, 44f), SoftPanelColor);
        CreateButton(topBar, "Button_OpenIdeas", "Ideas", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(88f, 30f), AccentColor, TogglePanel);
        activeIdeaText = CreateText(topBar, "Text_ActiveIdea", "No active Idea", 17f, TextColor, TextAlignmentOptions.MidlineLeft);
        SetRect(activeIdeaText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(108f, 0f), new Vector2(-118f, 32f));
    }

    private void BuildIdeasPanel()
    {
        RectTransform panel = CreatePanel("Panel_Ideas", transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(390f, 620f), PanelColor);
        panelObject = panel.gameObject;

        TextMeshProUGUI title = CreateText(panel, "Text_IdeasTitle", "Ideas", 26f, TextColor, TextAlignmentOptions.MidlineLeft);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(-78f, 38f));

        CreateButton(panel, "Button_CloseIdeas", "Close", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(66f, 30f), SoftPanelColor, () => SetPanelOpen(false));

        panelStatusText = CreateText(panel, "Text_IdeaPanelStatus", string.Empty, 15f, MutedTextColor, TextAlignmentOptions.TopLeft);
        SetRect(panelStatusText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -62f), new Vector2(-36f, 44f));

        float y = -120f;
        IReadOnlyList<IdeaDefinition> ideas = ideaManager.Ideas;

        for (int i = 0; i < ideas.Count; i++)
        {
            IdeaDefinition idea = ideas[i];

            if (idea == null || idea.IsElsewhere)
            {
                continue;
            }

            BuildIdeaRow(panel, idea, y);
            y -= 100f;
        }

        BuildElsewhereRow(panel, y - 2f);
        BuildPlacementRow(panel);
    }

    private void BuildIdeaRow(RectTransform panel, IdeaDefinition idea, float y)
    {
        RectTransform row = CreatePanel($"Row_{idea.Id}", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, y), new Vector2(-36f, 86f), new Color(0.12f, 0.125f, 0.13f, 0.78f));

        TextMeshProUGUI nameText = CreateText(row, $"Text_{idea.Id}_Name", idea.DisplayName, 17f, TextColor, TextAlignmentOptions.TopLeft);
        SetRect(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(12f, -9f), new Vector2(-110f, 24f));

        TextMeshProUGUI premiseText = CreateText(row, $"Text_{idea.Id}_Premise", idea.Premise, 12f, MutedTextColor, TextAlignmentOptions.TopLeft);
        premiseText.enableWordWrapping = true;
        SetRect(premiseText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(12f, -32f), new Vector2(-110f, 48f));

        string capturedIdeaId = idea.Id;
        CreateButton(row, $"Button_{idea.Id}_Explore", "Explore", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(82f, 30f), BlueAccentColor, () => ideaManager.ExploreIdea(capturedIdeaId));

        TextMeshProUGUI statusText = CreateText(row, $"Text_{idea.Id}_Status", string.Empty, 12f, AccentColor, TextAlignmentOptions.BottomRight);
        SetRect(statusText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 8f), new Vector2(88f, 24f));

        ideaRows.Add(new IdeaRow { IdeaId = idea.Id, StatusText = statusText });
    }

    private void BuildElsewhereRow(RectTransform panel, float y)
    {
        RectTransform row = CreatePanel("Row_Elsewhere", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, y), new Vector2(-36f, 76f), new Color(0.1f, 0.09f, 0.13f, 0.78f));

        TextMeshProUGUI nameText = CreateText(row, "Text_Elsewhere_Name", "Elsewhere", 17f, TextColor, TextAlignmentOptions.TopLeft);
        SetRect(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(12f, -9f), new Vector2(-112f, 24f));

        TextMeshProUGUI premiseText = CreateText(row, "Text_Elsewhere_Premise", "The Odd Place. A clean exit when an Idea starts to close around you.", 12f, MutedTextColor, TextAlignmentOptions.TopLeft);
        premiseText.enableWordWrapping = true;
        SetRect(premiseText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(12f, -32f), new Vector2(-112f, 36f));

        CreateButton(row, "Button_EnterElsewhere", "Enter", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(82f, 30f), RedAccentColor, () => ideaManager.EnterElsewhere());
        CreateButton(row, "Button_ClearIdea", "Clear", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 10f), new Vector2(82f, 30f), SoftPanelColor, () => ideaManager.ClearIdea());
    }

    private void BuildPlacementRow(RectTransform panel)
    {
        RectTransform row = CreatePanel("Panel_Placement", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(18f, 18f), new Vector2(-36f, 96f), new Color(0.105f, 0.105f, 0.1f, 0.82f));

        TextMeshProUGUI label = CreateText(row, "Text_PlacementTitle", "World Notes", 17f, TextColor, TextAlignmentOptions.TopLeft);
        SetRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(12f, -10f), new Vector2(-24f, 24f));

        placementStatusText = CreateText(row, "Text_PlacementStatus", "Placement is off.", 12f, MutedTextColor, TextAlignmentOptions.TopLeft);
        SetRect(placementStatusText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(12f, -36f), new Vector2(-24f, 22f));

        CreateButton(row, "Button_PlaceIdeaMarker", "Place Marker", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(12f, 12f), new Vector2(120f, 30f), BlueAccentColor, () => SetPlacementMode(true));
        CreateButton(row, "Button_CancelPlacement", "Cancel", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(140f, 12f), new Vector2(84f, 30f), SoftPanelColor, () => SetPlacementMode(false));
    }

    private void BuildSelectionBar()
    {
        RectTransform bar = CreatePanel("Panel_Selection", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(620f, 42f), SoftPanelColor);
        selectionText = CreateText(bar, "Text_Selection", "Selected: None", 15f, TextColor, TextAlignmentOptions.MidlineLeft);
        SetRect(selectionText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(14f, 0f), new Vector2(-28f, -12f));
    }

    private void BuildTutorial()
    {
        RectTransform blocker = CreateRect("Panel_IdeaTutorial", transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image blockerImage = blocker.gameObject.AddComponent<Image>();
        blockerImage.color = new Color(0f, 0f, 0f, 0.48f);
        tutorialObject = blocker.gameObject;

        RectTransform panel = CreatePanel("Panel_IdeaTutorialCard", blocker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 276f), PanelColor);

        tutorialTitleText = CreateText(panel, "Text_TutorialTitle", string.Empty, 25f, TextColor, TextAlignmentOptions.TopLeft);
        SetRect(tutorialTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(24f, -22f), new Vector2(-48f, 42f));

        tutorialBodyText = CreateText(panel, "Text_TutorialBody", string.Empty, 16f, MutedTextColor, TextAlignmentOptions.TopLeft);
        tutorialBodyText.enableWordWrapping = true;
        SetRect(tutorialBodyText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(24f, -82f), new Vector2(-48f, 112f));

        tutorialPrimaryButton = CreateButton(panel, "Button_TutorialPrimary", "Next", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(128f, 34f), BlueAccentColor, AdvanceTutorial);
        tutorialPrimaryText = tutorialPrimaryButton.GetComponentInChildren<TextMeshProUGUI>();

        tutorialSecondaryButton = CreateButton(panel, "Button_TutorialSecondary", "Skip", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-162f, 24f), new Vector2(104f, 34f), SoftPanelColor, SkipTutorial);
        tutorialSecondaryText = tutorialSecondaryButton.GetComponentInChildren<TextMeshProUGUI>();
        tutorialObject.SetActive(false);
    }

    private void StartTutorial()
    {
        tutorialStep = 0;
        ideaManager.ExploreIdea(TutorialIdeaId);
        tutorialObject.SetActive(true);
        SetPanelOpen(false);
        RefreshTutorial();
    }

    private void AdvanceTutorial()
    {
        if (tutorialStep == 1)
        {
            SetPanelOpen(true);
        }
        else if (tutorialStep == 2)
        {
            ideaManager.EnterElsewhere();
        }
        else if (tutorialStep >= 3)
        {
            ideaManager.ClearIdea();
            EndTutorial();
            return;
        }

        tutorialStep++;
        RefreshTutorial();
    }

    private void EndTutorial()
    {
        tutorialStep = -1;
        SetPanelOpen(false);

        if (tutorialObject != null)
        {
            tutorialObject.SetActive(false);
        }
    }

    private void SkipTutorial()
    {
        ideaManager.ClearIdea();
        EndTutorial();
    }

    private void RefreshTutorial()
    {
        if (tutorialStep == 0)
        {
            tutorialTitleText.text = "An Idea Has Started";
            tutorialBodyText.text = "The house is now reading itself through The Inherited Shape. The active Idea appears in the corner.";
            SetTutorialButtons("Next", "Skip", true);
        }
        else if (tutorialStep == 1)
        {
            tutorialTitleText.text = "The Ideas Menu";
            tutorialBodyText.text = "This menu keeps the current Idea visible and lets you explore another one when the house offers it.";
            SetTutorialButtons("Open", "Skip", true);
        }
        else if (tutorialStep == 2)
        {
            tutorialTitleText.text = "Elsewhere";
            tutorialBodyText.text = "Elsewhere is The Odd Place. It is the way out when an Idea starts feeling too complete.";
            SetTutorialButtons("Enter", "Skip", true);
        }
        else
        {
            tutorialTitleText.text = "Back To The House";
            tutorialBodyText.text = "You can open Ideas any time, see what is active, select objects, and place simple notes in the room.";
            SetTutorialButtons("Begin", string.Empty, false);
        }
    }

    private void SetTutorialButtons(string primaryText, string secondaryText, bool showSecondary)
    {
        tutorialPrimaryText.text = primaryText;
        tutorialSecondaryText.text = secondaryText;
        tutorialSecondaryButton.gameObject.SetActive(showSecondary);
    }

    private void HandleCurrentIdeaChanged(IdeaDefinition idea)
    {
        RefreshIdeaUi();
    }

    private void HandleSelectedObjectChanged(IdeaWorldObject selectedObject)
    {
        RefreshSelection(selectedObject);
    }

    private void HandleHoveredDoorChanged(DoorTriggerNavigation trigger)
    {
        if (trigger == null)
        {
            RefreshSelection(IdeaWorldObject.SelectedObject);
            return;
        }

        string interactionLabel = trigger.InteractionLabel;
        string doorName = string.IsNullOrWhiteSpace(trigger.DoorName) ? trigger.name : trigger.DoorName;
        string destination = string.IsNullOrWhiteSpace(trigger.DestinationRoom) ? "unknown" : trigger.DestinationRoom;
        selectionText.text = $"{interactionLabel}: {doorName} -> {destination}";
    }

    private void RefreshIdeaUi()
    {
        IdeaDefinition currentIdea = ideaManager != null ? ideaManager.CurrentIdea : null;
        string activeText = currentIdea == null
            ? "No active Idea"
            : currentIdea.IsElsewhere ? $"Elsewhere: {currentIdea.DisplayName}" : $"Active: {currentIdea.DisplayName}";

        if (activeIdeaText != null)
        {
            activeIdeaText.text = activeText;
        }

        if (panelStatusText != null)
        {
            panelStatusText.text = currentIdea == null
                ? "The house is in its neutral shape."
                : currentIdea.Premise;
        }

        string currentIdeaId = currentIdea != null ? currentIdea.Id : string.Empty;

        for (int i = 0; i < ideaRows.Count; i++)
        {
            IdeaRow row = ideaRows[i];
            row.StatusText.text = row.IdeaId == currentIdeaId ? "Active" : string.Empty;
        }
    }

    private void RefreshSelection(IdeaWorldObject selectedObject)
    {
        if (selectionText == null)
        {
            return;
        }

        if (selectedObject == null)
        {
            selectionText.text = placementMode ? "Placement: click the room to place a marker." : "Selected: None";
            return;
        }

        string description = selectedObject.Description;
        selectionText.text = string.IsNullOrWhiteSpace(description)
            ? $"Selected: {selectedObject.DisplayName}"
            : $"Selected: {selectedObject.DisplayName} - {description}";
    }

    private void SetPanelOpen(bool isOpen)
    {
        if (panelObject != null)
        {
            panelObject.SetActive(isOpen);
        }
    }

    private void TogglePanel()
    {
        if (panelObject != null)
        {
            SetPanelOpen(!panelObject.activeSelf);
        }
    }

    private void SetPlacementMode(bool isOn)
    {
        placementMode = isOn;

        if (placementStatusText != null)
        {
            placementStatusText.text = placementMode ? "Click the room to place a marker." : "Placement is off.";
        }

        RefreshSelection(IdeaWorldObject.SelectedObject);
    }

    private void PlaceItem(Vector2 screenPoint)
    {
        RectTransform placementRoot = FindPlacementRoot();

        if (placementRoot == null)
        {
            Debug.LogWarning("Could not place an Idea marker because no active room RectTransform was found.", this);
            SetPlacementMode(false);
            return;
        }

        Camera eventCamera = GetEventCameraFor(placementRoot);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(placementRoot, screenPoint, eventCamera, out Vector2 localPoint))
        {
            return;
        }

        IdeaDefinition currentIdea = ideaManager != null ? ideaManager.CurrentIdea : null;
        placedItemCount++;

        GameObject item = new GameObject($"Placed_Idea_Marker_{placedItemCount}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(IdeaWorldObject));
        RectTransform itemRect = item.transform as RectTransform;
        itemRect.SetParent(placementRoot, false);
        itemRect.anchorMin = new Vector2(0.5f, 0.5f);
        itemRect.anchorMax = new Vector2(0.5f, 0.5f);
        itemRect.pivot = new Vector2(0.5f, 0.5f);
        itemRect.anchoredPosition = localPoint;
        itemRect.sizeDelta = new Vector2(34f, 34f);
        itemRect.localScale = Vector3.one;
        item.transform.SetAsLastSibling();

        Image image = item.GetComponent<Image>();
        image.color = currentIdea != null ? WithAlpha(currentIdea.Tint, 0.92f) : WithAlpha(AccentColor, 0.92f);
        image.raycastTarget = true;

        TextMeshProUGUI markerText = CreateText(itemRect, "Text_Marker", "I", 18f, Color.white, TextAlignmentOptions.Center);
        markerText.fontStyle = FontStyles.Bold;
        SetRect(markerText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        string ideaName = currentIdea != null ? currentIdea.DisplayName : "Neutral";
        IdeaWorldObject worldObject = item.GetComponent<IdeaWorldObject>();
        worldObject.Configure($"Marker {placedItemCount}", $"Placed during {ideaName}.");
        worldObject.Select();
        SetPlacementMode(false);
    }

    private RectTransform FindPlacementRoot()
    {
        RoomNavigationManager navigationManager = Object.FindObjectOfType<RoomNavigationManager>(true);
        string currentRoom = navigationManager != null ? navigationManager.CurrentRoom : string.Empty;
        RoomContentGroup[] roomGroups = Object.FindObjectsOfType<RoomContentGroup>(true);
        RectTransform activeRoom = null;

        for (int i = 0; i < roomGroups.Length; i++)
        {
            RoomContentGroup roomGroup = roomGroups[i];

            if (roomGroup == null)
            {
                continue;
            }

            RectTransform rectTransform = roomGroup.transform as RectTransform;

            if (rectTransform == null)
            {
                continue;
            }

            if (roomGroup.gameObject.activeInHierarchy)
            {
                activeRoom = rectTransform;
            }

            if (!string.IsNullOrWhiteSpace(currentRoom) && string.Equals(roomGroup.RoomName, currentRoom, System.StringComparison.OrdinalIgnoreCase))
            {
                return rectTransform;
            }
        }

        return activeRoom;
    }

    private Camera GetEventCameraFor(RectTransform rectTransform)
    {
        Canvas targetCanvas = rectTransform.GetComponentInParent<Canvas>();

        if (targetCanvas == null || targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return targetCanvas.worldCamera;
    }

    private bool IsPointerOverThisUi(Vector2 screenPoint)
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null)
        {
            return false;
        }

        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = screenPoint
        };

        uiRaycastResults.Clear();
        eventSystem.RaycastAll(pointerData, uiRaycastResults);

        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            GameObject hitObject = uiRaycastResults[i].gameObject;

            if (hitObject != null && hitObject.transform.IsChildOf(transform))
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystemObject.transform.SetAsLastSibling();
    }

    private RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        RectTransform rectTransform = CreateRect(name, parent, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        Image image = rectTransform.gameObject.AddComponent<Image>();
        image.color = color;
        return rectTransform;
    }

    private RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rectTransform = gameObject.transform as RectTransform;
        rectTransform.SetParent(parent, false);
        SetRect(rectTransform, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        return rectTransform;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rectTransform = textObject.transform as RectTransform;
        rectTransform.SetParent(parent, false);
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        return label;
    }

    private Button CreateButton(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color, UnityAction action)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rectTransform = buttonObject.transform as RectTransform;
        rectTransform.SetParent(parent, false);
        SetRect(rectTransform, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.18f, 0.18f, 0.18f, 0.55f);
        button.colors = colors;

        TextMeshProUGUI label = CreateText(rectTransform, "Text", text, 14f, Color.white, TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return button;
    }

    private void SetRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;
        rectTransform.localScale = Vector3.one;
    }

    private Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
