using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class Chapter3LayeredDinnerBuilder : MonoBehaviour
{
    private const string ManifestResourcesPath = "Chapter3/Dining/LayeredDinnerManifest";
    private const string DinnerRootName = "Ch3_LayeredDinnerRoot";
    private const string SeatBaseRootName = "Ch3_SeatBaseLayers";
    private const string FoodStateRootName = "Ch3_FoodStateLayers";
    private const string SeatOverlayRootName = "Ch3_SeatEatingOverlays";

    [Header("References")]
    [SerializeField] private Chapter3LayeredDinnerAssetManifest manifest;

    [Header("Room")]
    [SerializeField] private string diningRoomId = "Dining Room";

    [Header("Placement")]
    [SerializeField] private Vector2 normalizedAnchor = new Vector2(0.5f, 0.46f);
    [SerializeField] private float normalizedWidth = 0.86f;
    [SerializeField] private Vector2 worldCenter = new Vector2(0f, -0.45f);
    [SerializeField] private float worldWidth = 9.6f;

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "People";
    [SerializeField] private int tableBackOrder = 9240;
    [SerializeField] private int seatBaseOrder = 9250;
    [SerializeField] private int tableTopPropsOrder = 9290;
    [SerializeField] private int tableFrontOverlayOrder = 9320;
    [SerializeField] private int foodOrder = 9340;
    [SerializeField] private int eatingOverlayOrder = 9360;

    private readonly List<Chapter3LayeredSeatAnimator> seatAnimators = new List<Chapter3LayeredSeatAnimator>();
    private Chapter3LayeredFoodState foodState;
    private Transform diningRoomRoot;

    public Transform DinnerRoot { get; private set; }
    public IReadOnlyList<Chapter3LayeredSeatAnimator> SeatAnimators => seatAnimators;
    public Chapter3LayeredFoodState FoodState => foodState;
    public bool HasValidManifest => manifest != null && manifest.Validate(out _);
    public string LastValidationMessage { get; private set; }

    public bool BuildOrRefresh()
    {
        seatAnimators.Clear();
        LoadManifestIfNeeded();

        if (manifest == null)
        {
            Debug.LogError("[Ch3Dining] Missing LayeredDinnerManifest at Resources/Chapter3/Dining/LayeredDinnerManifest.asset.", this);
            return false;
        }

        if (!manifest.Validate(out string validationMessage))
        {
            LastValidationMessage = validationMessage;
            Debug.LogError($"[Ch3Dining] Layered dinner manifest is invalid:\n{validationMessage}", this);
            return false;
        }

        LastValidationMessage = validationMessage;
        diningRoomRoot = ResolveDiningRoomRoot();

        if (diningRoomRoot == null)
        {
            Debug.LogError($"[Ch3Dining] Could not find Dining Room root for '{diningRoomId}'.", this);
            return false;
        }

        ClearFailedPreviousVisuals();
        bool useUi = ShouldUseUiLayers(diningRoomRoot);
        DinnerRoot = GetOrCreateChild(diningRoomRoot, DinnerRootName, useUi);
        DinnerRoot.gameObject.SetActive(true);

        if (useUi)
        {
            ConfigureUiDinnerRoot(DinnerRoot as RectTransform);
            BuildUiLayers();
        }
        else
        {
            ConfigureWorldDinnerRoot();
            BuildWorldLayers();
        }

        ShowDinner(true);
        return true;
    }

    public void ShowDinner(bool visible)
    {
        if (DinnerRoot != null && DinnerRoot.gameObject.activeSelf != visible)
        {
            DinnerRoot.gameObject.SetActive(visible);
        }
    }

    public void ClearFailedPreviousVisuals()
    {
        if (diningRoomRoot == null)
        {
            diningRoomRoot = ResolveDiningRoomRoot();
        }

        if (diningRoomRoot == null)
        {
            return;
        }

        string[] failedRootNames =
        {
            "Ch3_Dining_BackProps_Static",
            "Ch3_Dining_ForegroundOccluder_Static",
            "Ch3_Dining_FoodProps",
            "Ch3_DiningScene_Runtime",
            "Ch3_DinnerTableauRoot",
            "Ch3_TableForegroundFallback_Static",
            "Ch3_Dining_ForegroundFallback_Static"
        };

        Transform[] children = diningRoomRoot.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];

            if (child == null || child == diningRoomRoot || child.name == DinnerRootName || IsDiningSeatAnchor(child.name))
            {
                continue;
            }

            if (MatchesFailedRoot(child.name, failedRootNames))
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private void LoadManifestIfNeeded()
    {
        if (manifest == null)
        {
            manifest = Resources.Load<Chapter3LayeredDinnerAssetManifest>(ManifestResourcesPath);
        }

        if (manifest != null)
        {
            return;
        }

        TryEditorCreateManifest();
        manifest = Resources.Load<Chapter3LayeredDinnerAssetManifest>(ManifestResourcesPath);
    }

    private void TryEditorCreateManifest()
    {
#if UNITY_EDITOR
        try
        {
            System.Type validatorType = System.Type.GetType("Chapter3LayeredDinnerAssetValidator, Assembly-CSharp-Editor") ??
                System.Type.GetType("Chapter3LayeredDinnerAssetValidator");
            MethodInfo createMethod = validatorType?.GetMethod(
                "CreateOrUpdateLayeredDinnerManifest",
                BindingFlags.Public | BindingFlags.Static);
            createMethod?.Invoke(null, null);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[Ch3Dining] Could not auto-create layered dinner manifest: {exception.Message}", this);
        }
#endif
    }

    private void BuildUiLayers()
    {
        Image tableBack = CreateUiImageLayer(DinnerRoot, "Ch3_TableBack", manifest.tableBack, tableBackOrder);
        Transform seatBaseRoot = GetOrCreateChild(DinnerRoot, SeatBaseRootName, true);
        Image tableTopProps = CreateUiImageLayer(DinnerRoot, "Ch3_TableTopProps", manifest.tableTopProps, tableTopPropsOrder);
        Image tableFront = CreateUiImageLayer(DinnerRoot, "Ch3_TableFrontOverlay", manifest.tableFrontOverlay, tableFrontOverlayOrder);
        Transform foodRoot = GetOrCreateChild(DinnerRoot, FoodStateRootName, true);
        Transform overlayRoot = GetOrCreateChild(DinnerRoot, SeatOverlayRootName, true);

        DeactivateNestedLegacyLayerChildren(tableTopProps.transform);
        DeactivateNestedLegacyLayerChildren(tableFront.transform);
        tableBack.transform.SetSiblingIndex(0);
        seatBaseRoot.SetSiblingIndex(1);
        tableTopProps.transform.SetSiblingIndex(2);
        tableFront.transform.SetSiblingIndex(3);
        foodRoot.SetSiblingIndex(4);
        overlayRoot.SetSiblingIndex(5);

        Image covered = CreateUiImageLayer(foodRoot, "Ch3_CoveredDish", manifest.coveredDish, foodOrder);
        Image full = CreateUiImageLayer(foodRoot, "Ch3_FoodFull", manifest.foodFull, foodOrder);
        Image half = CreateUiImageLayer(foodRoot, "Ch3_FoodHalf", manifest.foodHalf, foodOrder);
        Image empty = CreateUiImageLayer(foodRoot, "Ch3_FoodEmpty", manifest.foodEmpty, foodOrder);

        _ = tableTopProps;
        _ = tableFront;
        ConfigureFoodState(foodRoot, covered, full, half, empty, null, null, null, null);

        for (int i = 0; i < 8; i++)
        {
            Chapter3LayeredDinnerAssetManifest.Chapter3SeatLayerSet seat = manifest.GetSeat(i);
            Image baseImage = CreateUiImageLayer(seatBaseRoot, $"Ch3_Seat{i + 1:00}_Base", FirstSprite(seat.idleFrames), seatBaseOrder + i);
            Image overlayImage = CreateUiImageLayer(overlayRoot, $"Ch3_Seat{i + 1:00}_Overlay", null, eatingOverlayOrder + i);
            Chapter3LayeredSeatAnimator animator = baseImage.gameObject.GetComponent<Chapter3LayeredSeatAnimator>();

            if (animator == null)
            {
                animator = baseImage.gameObject.AddComponent<Chapter3LayeredSeatAnimator>();
            }

            animator.Configure(i, seat, baseImage, null, overlayImage, null);
            seatAnimators.Add(animator);
        }
    }

    private void BuildWorldLayers()
    {
        SpriteRenderer tableBack = CreateWorldSpriteLayer(DinnerRoot, "Ch3_TableBack", manifest.tableBack, tableBackOrder);
        Transform seatBaseRoot = GetOrCreateChild(DinnerRoot, SeatBaseRootName, false);
        SpriteRenderer tableTopProps = CreateWorldSpriteLayer(DinnerRoot, "Ch3_TableTopProps", manifest.tableTopProps, tableTopPropsOrder);
        SpriteRenderer tableFront = CreateWorldSpriteLayer(DinnerRoot, "Ch3_TableFrontOverlay", manifest.tableFrontOverlay, tableFrontOverlayOrder);
        Transform foodRoot = GetOrCreateChild(DinnerRoot, FoodStateRootName, false);
        Transform overlayRoot = GetOrCreateChild(DinnerRoot, SeatOverlayRootName, false);

        DeactivateNestedLegacyLayerChildren(tableTopProps.transform);
        DeactivateNestedLegacyLayerChildren(tableFront.transform);
        tableBack.transform.SetSiblingIndex(0);
        seatBaseRoot.SetSiblingIndex(1);
        tableTopProps.transform.SetSiblingIndex(2);
        tableFront.transform.SetSiblingIndex(3);
        foodRoot.SetSiblingIndex(4);
        overlayRoot.SetSiblingIndex(5);

        SpriteRenderer covered = CreateWorldSpriteLayer(foodRoot, "Ch3_CoveredDish", manifest.coveredDish, foodOrder);
        SpriteRenderer full = CreateWorldSpriteLayer(foodRoot, "Ch3_FoodFull", manifest.foodFull, foodOrder);
        SpriteRenderer half = CreateWorldSpriteLayer(foodRoot, "Ch3_FoodHalf", manifest.foodHalf, foodOrder);
        SpriteRenderer empty = CreateWorldSpriteLayer(foodRoot, "Ch3_FoodEmpty", manifest.foodEmpty, foodOrder);

        _ = tableBack;
        _ = tableTopProps;
        _ = tableFront;
        ConfigureFoodState(foodRoot, null, null, null, null, covered, full, half, empty);

        for (int i = 0; i < 8; i++)
        {
            Chapter3LayeredDinnerAssetManifest.Chapter3SeatLayerSet seat = manifest.GetSeat(i);
            SpriteRenderer baseRenderer = CreateWorldSpriteLayer(seatBaseRoot, $"Ch3_Seat{i + 1:00}_Base", FirstSprite(seat.idleFrames), seatBaseOrder + i);
            SpriteRenderer overlayRenderer = CreateWorldSpriteLayer(overlayRoot, $"Ch3_Seat{i + 1:00}_Overlay", null, eatingOverlayOrder + i);
            Chapter3LayeredSeatAnimator animator = baseRenderer.gameObject.GetComponent<Chapter3LayeredSeatAnimator>();

            if (animator == null)
            {
                animator = baseRenderer.gameObject.AddComponent<Chapter3LayeredSeatAnimator>();
            }

            animator.Configure(i, seat, null, baseRenderer, null, overlayRenderer);
            seatAnimators.Add(animator);
        }
    }

    private void ConfigureFoodState(
        Transform foodRoot,
        Image covered,
        Image full,
        Image half,
        Image empty,
        SpriteRenderer coveredRenderer,
        SpriteRenderer fullRenderer,
        SpriteRenderer halfRenderer,
        SpriteRenderer emptyRenderer)
    {
        foodState = foodRoot.GetComponent<Chapter3LayeredFoodState>();

        if (foodState == null)
        {
            foodState = foodRoot.gameObject.AddComponent<Chapter3LayeredFoodState>();
        }

        foodState.Configure(covered, full, half, empty, coveredRenderer, fullRenderer, halfRenderer, emptyRenderer);
    }

    private Image CreateUiImageLayer(Transform parent, string objectName, Sprite sprite, int order)
    {
        RectTransform rect = GetOrCreateChild(parent, objectName, true) as RectTransform;
        Image image = rect.GetComponent<Image>();

        if (image == null)
        {
            image = rect.gameObject.AddComponent<Image>();
        }

        ConfigureFullRect(rect);
        image.sprite = sprite;
        image.enabled = sprite != null;
        image.color = Color.white;
        image.preserveAspect = false;
        image.raycastTarget = false;
        rect.SetSiblingIndex(Mathf.Max(0, order));
        return image;
    }

    private SpriteRenderer CreateWorldSpriteLayer(Transform parent, string objectName, Sprite sprite, int sortingOrder)
    {
        Transform layer = GetOrCreateChild(parent, objectName, false);
        SpriteRenderer renderer = layer.GetComponent<SpriteRenderer>();

        if (renderer == null)
        {
            renderer = layer.gameObject.AddComponent<SpriteRenderer>();
        }

        layer.localPosition = Vector3.zero;
        layer.localRotation = Quaternion.identity;
        layer.localScale = Vector3.one;
        renderer.sprite = sprite;
        renderer.enabled = sprite != null;
        renderer.color = Color.white;
        renderer.sortingLayerName = ResolveSortingLayerName();
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private void ConfigureUiDinnerRoot(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        RectTransform parentRect = rect.parent as RectTransform;
        float parentWidth = parentRect != null && parentRect.rect.width > 1f ? parentRect.rect.width : manifest.canvasSize.x;
        float width = parentWidth * Mathf.Clamp01(normalizedWidth);
        float height = width * manifest.canvasSize.y / Mathf.Max(1f, manifest.canvasSize.x);
        rect.anchorMin = normalizedAnchor;
        rect.anchorMax = normalizedAnchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private void ConfigureWorldDinnerRoot()
    {
        DinnerRoot.localPosition = new Vector3(worldCenter.x, worldCenter.y, 0f);
        DinnerRoot.localRotation = Quaternion.identity;
        float sourceWidth = manifest.tableBack != null && manifest.tableBack.bounds.size.x > 0f
            ? manifest.tableBack.bounds.size.x
            : manifest.canvasSize.x / 100f;
        float scale = Mathf.Max(0.01f, worldWidth) / Mathf.Max(0.01f, sourceWidth);
        DinnerRoot.localScale = Vector3.one * scale;
    }

    private static void ConfigureFullRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private Transform ResolveDiningRoomRoot()
    {
        RoomContentGroup[] roomGroups = FindObjectsByType<RoomContentGroup>(FindObjectsInactive.Include);

        for (int i = 0; i < roomGroups.Length; i++)
        {
            RoomContentGroup group = roomGroups[i];

            if (group != null &&
                string.Equals(group.RoomName, diningRoomId, System.StringComparison.OrdinalIgnoreCase))
            {
                return group.transform;
            }
        }

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];

            if (candidate != null &&
                !candidate.name.StartsWith("Ch3_", System.StringComparison.OrdinalIgnoreCase) &&
                candidate.name.IndexOf("Dining", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool ShouldUseUiLayers(Transform roomRoot)
    {
        if (roomRoot is RectTransform || roomRoot.GetComponentInParent<Canvas>(true) != null)
        {
            return true;
        }

        return roomRoot.GetComponentInChildren<Image>(true) != null ||
            roomRoot.GetComponentInChildren<RawImage>(true) != null;
    }

    private Transform GetOrCreateChild(Transform parent, string objectName, bool useRectTransform)
    {
        Transform existing = FindDirectChild(parent, objectName);

        if (existing != null)
        {
            if (useRectTransform && existing is not RectTransform)
            {
                DestroyObjectSafe(existing.gameObject);
            }
            else
            {
                existing.gameObject.SetActive(true);
                return existing;
            }
        }

        GameObject child = useRectTransform
            ? new GameObject(objectName, typeof(RectTransform))
            : new GameObject(objectName);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static Transform FindDirectChild(Transform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child != null &&
                string.Equals(child.name, objectName, System.StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static Sprite FirstSprite(Sprite[] frames)
    {
        if (frames == null)
        {
            return null;
        }

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null)
            {
                return frames[i];
            }
        }

        return null;
    }

    private string ResolveSortingLayerName()
    {
        if (!string.IsNullOrWhiteSpace(sortingLayerName) &&
            (string.Equals(sortingLayerName, "Default", System.StringComparison.OrdinalIgnoreCase) ||
                SortingLayer.NameToID(sortingLayerName) != 0))
        {
            return sortingLayerName.Trim();
        }

        return "Default";
    }

    private static bool MatchesFailedRoot(string objectName, string[] failedRootNames)
    {
        for (int i = 0; i < failedRootNames.Length; i++)
        {
            if (string.Equals(objectName, failedRootNames[i], System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return objectName.StartsWith("Ch3_", System.StringComparison.OrdinalIgnoreCase) &&
            (objectName.IndexOf("Primitive", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("Fallback", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("Beige", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool IsDiningSeatAnchor(string objectName)
    {
        return !string.IsNullOrWhiteSpace(objectName) &&
            objectName.StartsWith("Ch2_DiningSeat_", System.StringComparison.OrdinalIgnoreCase);
    }

    private static void DeactivateNestedLegacyLayerChildren(Transform layer)
    {
        if (layer == null)
        {
            return;
        }

        for (int i = 0; i < layer.childCount; i++)
        {
            Transform child = layer.GetChild(i);

            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private static void DestroyObjectSafe(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
