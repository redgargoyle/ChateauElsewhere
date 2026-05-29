using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameClockHandsDisplay : MonoBehaviour
{
    [Header("Time")]
    [SerializeField] private ChapterClock chapterClock;
    [SerializeField, Range(0, 23)] private int fallbackHour = 17;
    [SerializeField, Range(0, 59)] private int fallbackMinute = 59;

    [Header("Clock Face")]
    [SerializeField] private Vector2 faceCenterLocal = new Vector2(2.33f, 12.4f);
    [SerializeField, Min(0.01f)] private float faceRadiusLocalX = 1.28f;
    [SerializeField, Min(0.01f)] private float faceRadiusLocalY = 1.28f;
    [SerializeField, Range(0.05f, 0.95f)] private float minuteHandRadius = 0.42f;
    [SerializeField, Range(0.05f, 0.95f)] private float hourHandRadius = 0.29f;
    [SerializeField, Min(1f)] private float minuteHandWidth = 6f;
    [SerializeField, Min(1f)] private float hourHandWidth = 10f;
    [SerializeField, Min(1f)] private float centerPinSize = 18f;

    [Header("Overlay")]
    [SerializeField] private bool startVisible;
    [SerializeField] private bool primaryOverlayClock;
    [SerializeField] private bool hideSourceWhenClosed;
    [SerializeField] private int canvasSortingOrder = 9050;
    [SerializeField] private Color handColor = new Color(0.03f, 0.012f, 0.004f, 1f);
    [SerializeField] private Color handHighlightColor = new Color(1f, 0.78f, 0.22f, 0.9f);
    [SerializeField] private Color ringColor = new Color(0.02f, 0.01f, 0f, 0.35f);
    [SerializeField] private Color pinColor = new Color(1f, 0.68f, 0.18f, 1f);

    private static readonly List<GameClockHandsDisplay> displays = new List<GameClockHandsDisplay>();
    private static GameClockHandsDisplay activeDisplay;
    private static GameClockHandsDisplay primaryDisplay;
    private static Sprite solidSprite;
    private static Sprite pinSprite;
    private static Sprite ringSprite;

    private SpriteRenderer sourceRenderer;
    private Camera worldCamera;
    private Canvas canvas;
    private RectTransform canvasRect;
    private RectTransform overlayRoot;
    private RectTransform hourHand;
    private RectTransform hourHighlight;
    private RectTransform minuteHand;
    private RectTransform minuteHighlight;
    private RectTransform centerPin;
    private bool visible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachToSceneClockSprites()
    {
        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];

            if (renderer == null || !LooksLikeGrandfatherClock(renderer))
            {
                continue;
            }

            if (renderer.GetComponent<GameClockHandsDisplay>() == null)
            {
                GameClockHandsDisplay display = renderer.gameObject.AddComponent<GameClockHandsDisplay>();

                if (LooksLikePreparedClockOverlay(renderer))
                {
                    display.primaryOverlayClock = true;
                    display.hideSourceWhenClosed = true;
                }
            }
        }
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (!displays.Contains(this))
        {
            displays.Add(this);
        }

        ResolveReferences();

        if (IsPrimaryOverlayClock())
        {
            primaryDisplay = this;
        }

        if (startVisible)
        {
            Show();
        }
        else if (hideSourceWhenClosed)
        {
            SetSourceVisible(false);
        }
    }

    private void OnDisable()
    {
        Hide();
        displays.Remove(this);

        if (primaryDisplay == this)
        {
            primaryDisplay = null;
        }
    }

    private void Update()
    {
        ResolveReferences();

        if (Input.GetMouseButtonDown(0) && PointerCanToggleClock() && IsPointerOverClock())
        {
            ToggleFromClick();
        }

        if (visible)
        {
            EnsureOverlay();
            RefreshOverlayPlacement();
            RefreshHands();
        }
    }

    private void OnValidate()
    {
        fallbackHour = Mathf.Clamp(fallbackHour, 0, 23);
        fallbackMinute = Mathf.Clamp(fallbackMinute, 0, 59);
        faceRadiusLocalX = Mathf.Max(0.01f, faceRadiusLocalX);
        faceRadiusLocalY = Mathf.Max(0.01f, faceRadiusLocalY);
        minuteHandWidth = Mathf.Max(1f, minuteHandWidth);
        hourHandWidth = Mathf.Max(1f, hourHandWidth);
        centerPinSize = Mathf.Max(1f, centerPinSize);
    }

    public void Toggle()
    {
        if (visible)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    public void Show()
    {
        HideOtherDisplays();
        visible = true;
        activeDisplay = this;
        SetSourceVisible(true);
        EnsureOverlay();
        overlayRoot.gameObject.SetActive(true);
        RefreshOverlayPlacement();
        RefreshHands();
    }

    public void Hide()
    {
        visible = false;

        if (activeDisplay == this)
        {
            activeDisplay = null;
        }

        if (overlayRoot != null)
        {
            overlayRoot.gameObject.SetActive(false);
        }

        if (hideSourceWhenClosed)
        {
            SetSourceVisible(false);
        }
    }

    public static bool TogglePrimaryClock()
    {
        GameClockHandsDisplay display = GetPrimaryDisplay();

        if (display == null)
        {
            return false;
        }

        display.Toggle();
        return true;
    }

    private void ResolveReferences()
    {
        if (chapterClock == null)
        {
            chapterClock = FindAnyObjectByType<ChapterClock>(FindObjectsInactive.Include);
        }

        if (sourceRenderer == null)
        {
            sourceRenderer = GetComponent<SpriteRenderer>();
        }

        if (worldCamera == null || !worldCamera.isActiveAndEnabled)
        {
            worldCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>(FindObjectsInactive.Exclude);
        }
    }

    private bool PointerCanToggleClock()
    {
        EventSystem eventSystem = EventSystem.current;
        return eventSystem == null || !eventSystem.IsPointerOverGameObject();
    }

    private bool IsPointerOverClock()
    {
        if (sourceRenderer == null || worldCamera == null)
        {
            return false;
        }

        if (hideSourceWhenClosed && !sourceRenderer.enabled)
        {
            return false;
        }

        Rect screenRect = GetRendererScreenRect();
        return screenRect.Contains(Input.mousePosition);
    }

    private void ToggleFromClick()
    {
        if (IsPrimaryOverlayClock())
        {
            Toggle();
            return;
        }

        if (TogglePrimaryClock())
        {
            return;
        }

        Toggle();
    }

    private Rect GetRendererScreenRect()
    {
        Bounds bounds = sourceRenderer.bounds;
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };

        Vector2 screenMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 screenMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 screenPoint = worldCamera.WorldToScreenPoint(corners[i]);
            screenMin = Vector2.Min(screenMin, screenPoint);
            screenMax = Vector2.Max(screenMax, screenPoint);
        }

        return Rect.MinMaxRect(screenMin.x, screenMin.y, screenMax.x, screenMax.y);
    }

    private void EnsureOverlay()
    {
        if (overlayRoot != null)
        {
            return;
        }

        canvas = GetOrCreateCanvas();
        canvasRect = canvas.transform as RectTransform;

        GameObject rootObject = new GameObject($"AnalogClockOverlay_{name}", typeof(RectTransform));
        overlayRoot = rootObject.GetComponent<RectTransform>();
        overlayRoot.SetParent(canvas.transform, false);
        overlayRoot.anchorMin = new Vector2(0.5f, 0.5f);
        overlayRoot.anchorMax = new Vector2(0.5f, 0.5f);
        overlayRoot.pivot = new Vector2(0.5f, 0.5f);

        Image ring = CreateImage("ClockFaceRing", overlayRoot, ringSprite ??= CreateRingSprite(), ringColor);
        RectTransform ringRect = ring.rectTransform;
        ringRect.anchorMin = Vector2.zero;
        ringRect.anchorMax = Vector2.one;
        ringRect.offsetMin = Vector2.zero;
        ringRect.offsetMax = Vector2.zero;

        hourHighlight = CreateHand("HourHandHighlight", handHighlightColor, hourHandWidth + 4f);
        hourHand = CreateHand("HourHand", handColor, hourHandWidth);
        minuteHighlight = CreateHand("MinuteHandHighlight", handHighlightColor, minuteHandWidth + 3f);
        minuteHand = CreateHand("MinuteHand", handColor, minuteHandWidth);
        centerPin = CreateCenterPin();
        overlayRoot.gameObject.SetActive(false);
    }

    private Canvas GetOrCreateCanvas()
    {
        const string canvasName = "Canvas_AnalogClockHands";
        GameObject canvasObject = GameObject.Find(canvasName);

        if (canvasObject == null)
        {
            canvasObject = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        }

        Canvas targetCanvas = canvasObject.GetComponent<Canvas>();
        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        targetCanvas.sortingOrder = canvasSortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2048f, 1024f);
        scaler.matchWidthOrHeight = 0.5f;

        return targetCanvas;
    }

    private RectTransform CreateHand(string objectName, Color color, float width)
    {
        Image image = CreateImage(objectName, overlayRoot, solidSprite ??= CreateSolidSprite(), color);
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, 80f);
        return rect;
    }

    private RectTransform CreateCenterPin()
    {
        Image image = CreateImage("ClockCenterPin", overlayRoot, pinSprite ??= CreateCircleSprite(), pinColor);
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(centerPinSize, centerPinSize);
        return rect;
    }

    private static Image CreateImage(string objectName, Transform parent, Sprite sprite, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void RefreshOverlayPlacement()
    {
        if (worldCamera == null || canvasRect == null)
        {
            return;
        }

        Vector3 faceWorld = transform.TransformPoint(new Vector3(faceCenterLocal.x, faceCenterLocal.y, 0f));
        Vector3 rightWorld = transform.TransformPoint(new Vector3(faceCenterLocal.x + faceRadiusLocalX, faceCenterLocal.y, 0f));
        Vector3 leftWorld = transform.TransformPoint(new Vector3(faceCenterLocal.x - faceRadiusLocalX, faceCenterLocal.y, 0f));
        Vector3 topWorld = transform.TransformPoint(new Vector3(faceCenterLocal.x, faceCenterLocal.y + faceRadiusLocalY, 0f));
        Vector3 bottomWorld = transform.TransformPoint(new Vector3(faceCenterLocal.x, faceCenterLocal.y - faceRadiusLocalY, 0f));

        Vector2 faceScreen = worldCamera.WorldToScreenPoint(faceWorld);
        Vector2 rightScreen = worldCamera.WorldToScreenPoint(rightWorld);
        Vector2 leftScreen = worldCamera.WorldToScreenPoint(leftWorld);
        Vector2 topScreen = worldCamera.WorldToScreenPoint(topWorld);
        Vector2 bottomScreen = worldCamera.WorldToScreenPoint(bottomWorld);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, faceScreen, null, out Vector2 localPoint);
        overlayRoot.anchoredPosition = localPoint;

        float width = Mathf.Max(48f, Vector2.Distance(leftScreen, rightScreen));
        float height = Mathf.Max(48f, Vector2.Distance(bottomScreen, topScreen));
        overlayRoot.sizeDelta = new Vector2(width, height);
    }

    private void RefreshHands()
    {
        if (hourHand == null || minuteHand == null)
        {
            return;
        }

        float totalMinutes = GetCurrentGameTotalMinutes();
        float minuteProgress = Mathf.Repeat(totalMinutes, 60f) / 60f;
        float hourProgress = Mathf.Repeat(totalMinutes, 720f) / 720f;
        float radius = Mathf.Min(overlayRoot.rect.width, overlayRoot.rect.height) * 0.5f;

        SetHand(hourHand, hourHighlight, hourProgress, radius * hourHandRadius);
        SetHand(minuteHand, minuteHighlight, minuteProgress, radius * minuteHandRadius);

        if (centerPin != null)
        {
            centerPin.sizeDelta = new Vector2(centerPinSize, centerPinSize);
        }
    }

    private float GetCurrentGameTotalMinutes()
    {
        if (chapterClock != null)
        {
            return chapterClock.StartTotalMinutes + chapterClock.ElapsedGameMinutes;
        }

        return fallbackHour * 60f + fallbackMinute;
    }

    private static void SetHand(RectTransform hand, RectTransform highlight, float progress, float length)
    {
        float angle = -Mathf.Repeat(progress, 1f) * 360f;
        Vector2 size = hand.sizeDelta;
        size.y = length;
        hand.sizeDelta = size;
        hand.localEulerAngles = new Vector3(0f, 0f, angle);

        if (highlight == null)
        {
            return;
        }

        Vector2 highlightSize = highlight.sizeDelta;
        highlightSize.y = length;
        highlight.sizeDelta = highlightSize;
        highlight.localEulerAngles = hand.localEulerAngles;
    }

    private void HideOtherDisplays()
    {
        for (int i = 0; i < displays.Count; i++)
        {
            GameClockHandsDisplay display = displays[i];

            if (display != null && display != this)
            {
                display.Hide();
            }
        }
    }

    private static bool LooksLikeGrandfatherClock(SpriteRenderer renderer)
    {
        string objectName = renderer.name.ToLowerInvariant();
        string spriteName = renderer.sprite != null ? renderer.sprite.name.ToLowerInvariant() : string.Empty;
        string textureName = renderer.sprite != null && renderer.sprite.texture != null
            ? renderer.sprite.texture.name.ToLowerInvariant()
            : string.Empty;

        return objectName.Contains("clockcutout") ||
            objectName.Contains("grandfatherclock") ||
            objectName.Contains("grandfather_clock") ||
            spriteName.Contains("clockcutout") ||
            spriteName.Contains("grandfather") ||
            textureName.Contains("clockcutout") ||
            textureName.Contains("grandfather");
    }

    private bool IsPrimaryOverlayClock()
    {
        return primaryOverlayClock || (sourceRenderer != null && LooksLikePreparedClockOverlay(sourceRenderer));
    }

    private void SetSourceVisible(bool value)
    {
        if (sourceRenderer != null)
        {
            sourceRenderer.enabled = value;
        }
    }

    private static GameClockHandsDisplay GetPrimaryDisplay()
    {
        if (primaryDisplay != null)
        {
            return primaryDisplay;
        }

        for (int i = 0; i < displays.Count; i++)
        {
            GameClockHandsDisplay display = displays[i];

            if (display != null && display.IsPrimaryOverlayClock())
            {
                primaryDisplay = display;
                return primaryDisplay;
            }
        }

        GameClockHandsDisplay[] allDisplays = FindObjectsByType<GameClockHandsDisplay>(FindObjectsInactive.Include);

        for (int i = 0; i < allDisplays.Length; i++)
        {
            GameClockHandsDisplay display = allDisplays[i];

            if (display != null && display.IsPrimaryOverlayClock())
            {
                primaryDisplay = display;
                return primaryDisplay;
            }
        }

        return null;
    }

    private static bool LooksLikePreparedClockOverlay(SpriteRenderer renderer)
    {
        string objectName = renderer != null ? renderer.name.ToLowerInvariant() : string.Empty;
        string spriteName = renderer != null && renderer.sprite != null ? renderer.sprite.name.ToLowerInvariant() : string.Empty;
        string textureName = renderer != null && renderer.sprite != null && renderer.sprite.texture != null
            ? renderer.sprite.texture.name.ToLowerInvariant()
            : string.Empty;

        return objectName.Contains("clockcutout") ||
            spriteName.Contains("clockcutout") ||
            textureName.Contains("clockcutout");
    }

    private static Sprite CreateSolidSprite()
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.name = "RuntimeClockHandSolid";
        texture.hideFlags = HideFlags.HideAndDontSave;
        return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }

    private static Sprite CreateCircleSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = Color.clear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x / (float)(size - 1)) * 2f - 1f;
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                float distance = Mathf.Sqrt(nx * nx + ny * ny);
                texture.SetPixel(x, y, distance <= 1f ? Color.white : clear);
            }
        }

        texture.Apply();
        texture.name = "RuntimeClockCenterPin";
        texture.hideFlags = HideFlags.HideAndDontSave;
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite CreateRingSprite()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = Color.clear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x / (float)(size - 1)) * 2f - 1f;
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                float distance = Mathf.Sqrt(nx * nx + ny * ny);
                bool inOuterRing = distance <= 1f && distance >= 0.88f;
                bool inInnerRing = distance <= 0.2f && distance >= 0.16f;
                texture.SetPixel(x, y, inOuterRing || inInnerRing ? Color.white : clear);
            }
        }

        texture.Apply();
        texture.name = "RuntimeClockFaceRing";
        texture.hideFlags = HideFlags.HideAndDontSave;
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
