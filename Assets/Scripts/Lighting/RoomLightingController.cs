using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(-40)]
public sealed class RoomLightingController : MonoBehaviour
{
    private const string DefaultPresetResourcePath = "Lighting/RoomLightingPreset";
    private const int HudSortingOrder = 7000;

    [SerializeField] private string presetResourcePath = DefaultPresetResourcePath;
    [SerializeField] private RoomLightingPreset preset;
    [SerializeField] private KeyCode toggleKey = KeyCode.L;
    [SerializeField] private bool showHud = true;
    [SerializeField] private bool liveEditPreset = true;

    private readonly List<RoomLightOverlay> overlays = new List<RoomLightOverlay>();

    private Sprite softLightSprite;
    private bool lightsOn = true;
    private float lightBlend = 1f;
    private TextMeshProUGUI hudText;

    public float LightBlend => lightBlend;
    public bool LiveEditPreset => liveEditPreset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForGameplayRooms()
    {
        if (FindObjectOfType<RoomLightingController>() != null)
        {
            return;
        }

        if (FindObjectsOfType<RoomContentGroup>(true).Length == 0)
        {
            return;
        }

        GameObject controllerObject = new GameObject("RoomLightingController");
        controllerObject.AddComponent<RoomLightingController>();
    }

    private void Awake()
    {
        ResolvePreset();

        if (preset == null)
        {
            Debug.LogWarning("Room lighting could not load Resources/Lighting/RoomLightingPreset.asset.", this);
            enabled = false;
            return;
        }

        lightsOn = preset.StartLightsOn;
        lightBlend = lightsOn ? 1f : 0f;
        softLightSprite = CreateSoftLightSprite();
        BuildRoomLights();

        if (showHud)
        {
            BuildHud();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleLights();
        }

        float targetBlend = lightsOn ? 1f : 0f;
        float fadeSeconds = preset != null ? preset.ToggleFadeSeconds : 0.65f;
        lightBlend = Mathf.MoveTowards(lightBlend, targetBlend, Time.deltaTime / fadeSeconds);
        RefreshHud();
    }

    public void ToggleLights()
    {
        lightsOn = !lightsOn;
        RefreshHud();
    }

    private void ResolvePreset()
    {
        if (preset != null)
        {
            return;
        }

        string resourcePath = string.IsNullOrWhiteSpace(presetResourcePath)
            ? DefaultPresetResourcePath
            : presetResourcePath.Trim();

        preset = Resources.Load<RoomLightingPreset>(resourcePath);
    }

    private void BuildRoomLights()
    {
        overlays.Clear();

        RoomContentGroup[] roomGroups = FindObjectsOfType<RoomContentGroup>(true);
        Dictionary<string, RoomContentGroup> roomsByName = new Dictionary<string, RoomContentGroup>(System.StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < roomGroups.Length; i++)
        {
            RoomContentGroup roomGroup = roomGroups[i];

            if (roomGroup != null && !string.IsNullOrWhiteSpace(roomGroup.RoomName))
            {
                roomsByName[roomGroup.RoomName] = roomGroup;
            }
        }

        IReadOnlyList<RoomLightDefinition> lights = preset.Lights;

        for (int i = 0; i < lights.Count; i++)
        {
            RoomLightDefinition light = lights[i];

            if (light == null || string.IsNullOrWhiteSpace(light.roomName) || !roomsByName.TryGetValue(light.roomName.Trim(), out RoomContentGroup roomGroup))
            {
                continue;
            }

            RoomLightOverlay overlay = CreateOverlay(roomGroup, light, i);
            overlays.Add(overlay);
        }

        if (overlays.Count == 0)
        {
            Debug.LogWarning("Room lighting loaded, but no preset entries matched the room names in this scene.", this);
        }
    }

    private RoomLightOverlay CreateOverlay(RoomContentGroup roomGroup, RoomLightDefinition definition, int index)
    {
        Transform lightingRoot = FindOrCreateLightingRoot(roomGroup.transform);
        string lightName = string.IsNullOrWhiteSpace(definition.lightName) ? $"RoomLight_{index:00}" : $"RoomLight_{definition.lightName.Trim().Replace(' ', '_')}";

        GameObject lightObject = new GameObject(lightName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RoomLightOverlay));
        lightObject.transform.SetParent(lightingRoot, false);

        RoomLightOverlay overlay = lightObject.GetComponent<RoomLightOverlay>();
        overlay.Configure(definition, this, softLightSprite);
        return overlay;
    }

    private static Transform FindOrCreateLightingRoot(Transform roomTransform)
    {
        Transform existing = roomTransform.Find("Lighting");

        if (existing != null)
        {
            return existing;
        }

        GameObject rootObject = new GameObject("Lighting", typeof(RectTransform));
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.SetParent(roomTransform, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;

        Transform doors = roomTransform.Find("Doors");

        if (doors != null)
        {
            root.SetSiblingIndex(doors.GetSiblingIndex());
        }

        return root;
    }

    private void BuildHud()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystemObject.transform.SetParent(transform, false);
        }

        GameObject canvasObject = new GameObject("Canvas_RoomLightingHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = HudSortingOrder;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1366f, 768f);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        RectTransform buttonRect = CreateHudRect("Button_Lights", canvasRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(124f, 34f));
        Image buttonImage = buttonRect.gameObject.AddComponent<Image>();
        buttonImage.color = new Color(0.16f, 0.13f, 0.095f, 0.92f);

        Button hudButton = buttonRect.gameObject.AddComponent<Button>();
        hudButton.targetGraphic = buttonImage;
        hudButton.onClick.AddListener(ToggleLights);

        TextMeshProUGUI label = CreateHudText(buttonRect, "Text_Lights", 15f);
        hudText = label;
        RefreshHud();
    }

    private void RefreshHud()
    {
        if (hudText == null)
        {
            return;
        }

        hudText.text = lightsOn ? "Lights On" : "Lights Off";
    }

    private static RectTransform CreateHudRect(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject rectObject = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = rectObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static TextMeshProUGUI CreateHudText(Transform parent, string objectName, float fontSize)
    {
        RectTransform textRect = CreateHudRect(objectName, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.95f, 0.88f, 0.68f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private static Sprite CreateSoftLightSprite()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Generated_SoftRoomLight";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.12f, 1f, radius));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

}
