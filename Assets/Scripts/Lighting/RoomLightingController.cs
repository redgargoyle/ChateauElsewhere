using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
[DefaultExecutionOrder(-40)]
public sealed class RoomLightingController : MonoBehaviour
{
    private const string DefaultPresetResourcePath = "Lighting/RoomLightingPreset";
    private const string LightingRootName = "Lighting";
    private const string TrueParticleFireRootName = "TrueParticleFire";
    private const int HudSortingOrder = 7000;
    private static readonly string[] LightingPayloadRootNames = { LightingRootName, TrueParticleFireRootName };
    private static readonly string[] LooseLightParticleNameFragments = { "flame", "candle", "fire", "hearth" };

    [SerializeField] private string presetResourcePath = DefaultPresetResourcePath;
    [SerializeField] private RoomLightingPreset preset;
    [SerializeField] private KeyCode toggleKey = KeyCode.L;
    [SerializeField] private bool showHud = true;
    [SerializeField] private bool createMissingLightsFromPreset = true;
    [SerializeField] private bool syncExistingLightsFromPreset = true;
    [SerializeField] private bool removeStaleGeneratedFlameCores = true;
    [Header("Global Volume")]
    [SerializeField] private bool driveGlobalBloomIntensity = true;
    [SerializeField] private Volume globalVolume;
    [SerializeField] private float lightsOnBloomIntensity = 20f;
    [SerializeField] private float lightsOffBloomIntensity = 0f;

    private readonly List<RoomLightOverlay> overlays = new List<RoomLightOverlay>();
    private readonly List<GameObject> lightingStructureObjects = new List<GameObject>();
    private readonly Dictionary<GameObject, bool> lightingStructureActiveStates = new Dictionary<GameObject, bool>();

    private bool lightsOn = true;
    private float lightBlend = 1f;
    private TextMeshProUGUI hudText;
    private bool initialized;
    private Bloom globalBloom;
    private float lastAppliedBloomIntensity = float.NaN;

    public float LightBlend => lightBlend;

    private void Awake()
    {
        InitializeLighting();
    }

    private void OnEnable()
    {
        InitializeLighting();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall += InitializeLightingIfAlive;
        }
#endif
    }

    [ContextMenu("Create Missing Scene Lights From Preset")]
    public void CreateMissingSceneLightsFromPreset()
    {
        ResolvePreset();

        if (preset == null)
        {
            Debug.LogWarning("Room lighting could not load Resources/Lighting/RoomLightingPreset.asset.", this);
            return;
        }

        CreateMissingSceneLights();
        RefreshOverlayCache();
        ApplyLightingStructureActiveState();
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            lightBlend = 1f;
            ApplyLightBlendToOverlays();
            return;
        }
#endif

        if (Input.GetKeyDown(toggleKey))
        {
            ToggleLights();
        }

        float targetBlend = lightsOn ? 1f : 0f;
        float fadeSeconds = preset != null ? preset.ToggleFadeSeconds : 0.65f;
        lightBlend = Mathf.MoveTowards(lightBlend, targetBlend, Time.deltaTime / fadeSeconds);
        ApplyLightBlendToOverlays();
        ApplyGlobalBloomIntensity();
        RefreshHud();
    }

    public void ToggleLights()
    {
        if (lightsOn)
        {
            CaptureLightingStructureActiveStates();
        }

        lightsOn = !lightsOn;
        ApplyLightingStructureActiveState();
        ApplyGlobalBloomIntensity(true);
        RefreshHud();
    }

    private void InitializeLighting()
    {
        ResolvePreset();

        if (preset == null)
        {
            Debug.LogWarning("Room lighting could not load Resources/Lighting/RoomLightingPreset.asset.", this);
            enabled = false;
            return;
        }

        if (!initialized)
        {
            lightsOn = preset.StartLightsOn;
            lightBlend = lightsOn ? 1f : 0f;
            initialized = true;
        }

        if (createMissingLightsFromPreset)
        {
            CreateMissingSceneLights();
        }

        RefreshOverlayCache();
        ApplyLightBlendToOverlays();
        ApplyLightingStructureActiveState();
        ApplyGlobalBloomIntensity(true);

        if (Application.isPlaying && showHud)
        {
            BuildHud();
        }
    }

#if UNITY_EDITOR
    private void InitializeLightingIfAlive()
    {
        if (this == null || Application.isPlaying)
        {
            return;
        }

        InitializeLighting();
    }
#endif

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

    private void RefreshOverlayCache()
    {
        overlays.Clear();
        lightingStructureObjects.Clear();
        RoomContentGroup[] roomGroups = FindObjectsByType<RoomContentGroup>(FindObjectsInactive.Include);

        for (int i = 0; i < roomGroups.Length; i++)
        {
            RoomContentGroup roomGroup = roomGroups[i];

            if (roomGroup == null)
            {
                continue;
            }

            overlays.AddRange(roomGroup.GetComponentsInChildren<RoomLightOverlay>(true));
            CacheLightingStructureObjects(roomGroup.transform);
        }

        PruneLightingStructureActiveStates();

        if (Application.isPlaying && overlays.Count == 0)
        {
            Debug.LogWarning("Room lighting loaded, but no RoomLightOverlay objects exist in this scene.", this);
        }
    }

    private void CreateMissingSceneLights()
    {
        RoomContentGroup[] roomGroups = FindObjectsByType<RoomContentGroup>(FindObjectsInactive.Include);
        Dictionary<string, RoomContentGroup> roomsByName = new Dictionary<string, RoomContentGroup>(System.StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < roomGroups.Length; i++)
        {
            RoomContentGroup roomGroup = roomGroups[i];

            if (roomGroup != null && !string.IsNullOrWhiteSpace(roomGroup.RoomName))
            {
                roomsByName[roomGroup.RoomName] = roomGroup;
            }
        }

        IReadOnlyList<RoomLightDefinition> lights = preset != null ? preset.Lights : null;

        if (lights == null)
        {
            return;
        }

        Dictionary<string, HashSet<string>> presetSceneLightNamesByRoom = new Dictionary<string, HashSet<string>>(System.StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < lights.Count; i++)
        {
            RoomLightDefinition light = lights[i];

            if (light == null || string.IsNullOrWhiteSpace(light.roomName) || !roomsByName.TryGetValue(light.roomName.Trim(), out RoomContentGroup roomGroup))
            {
                continue;
            }

            if (!presetSceneLightNamesByRoom.TryGetValue(roomGroup.RoomName, out HashSet<string> presetSceneLightNames))
            {
                presetSceneLightNames = new HashSet<string>(System.StringComparer.Ordinal);
                presetSceneLightNamesByRoom[roomGroup.RoomName] = presetSceneLightNames;
            }

            presetSceneLightNames.Add(GetSceneLightName(light, i));
            CreateMissingSceneLight(roomGroup, light, i);
        }

        RemoveStaleGeneratedFlameCores(roomsByName, presetSceneLightNamesByRoom);
    }

    private void RemoveStaleGeneratedFlameCores(Dictionary<string, RoomContentGroup> roomsByName, Dictionary<string, HashSet<string>> presetSceneLightNamesByRoom)
    {
        if (!removeStaleGeneratedFlameCores || roomsByName == null || presetSceneLightNamesByRoom == null)
        {
            return;
        }

        foreach (KeyValuePair<string, RoomContentGroup> roomEntry in roomsByName)
        {
            RoomContentGroup roomGroup = roomEntry.Value;

            if (roomGroup == null || !presetSceneLightNamesByRoom.TryGetValue(roomGroup.RoomName, out HashSet<string> presetSceneLightNames))
            {
                continue;
            }

            Transform lightingRoot = roomGroup.transform.Find(LightingRootName);

            if (lightingRoot == null)
            {
                continue;
            }

            for (int i = lightingRoot.childCount - 1; i >= 0; i--)
            {
                GameObject childObject = lightingRoot.GetChild(i).gameObject;

                if (childObject == null || !IsGeneratedFlameCoreSceneLightName(childObject.name) || presetSceneLightNames.Contains(childObject.name))
                {
                    continue;
                }

                if (childObject.GetComponent<RoomLightOverlay>() == null)
                {
                    continue;
                }

                lightingStructureActiveStates.Remove(childObject);
                RemoveSceneObject(childObject);
            }
        }
    }

    private void CreateMissingSceneLight(RoomContentGroup roomGroup, RoomLightDefinition definition, int index)
    {
        Transform lightingRoot = FindOrCreateLightingRoot(roomGroup.transform);
        string lightName = GetSceneLightName(definition, index);
        Transform existing = lightingRoot.Find(lightName);

        if (existing != null && existing.TryGetComponent(out RoomLightOverlay existingOverlay))
        {
            if (syncExistingLightsFromPreset)
            {
                existingOverlay.ApplyDefinition(definition);
                MarkSceneDirtyIfEditing(existingOverlay.gameObject);
            }

            return;
        }

        GameObject lightObject;

        if (existing != null)
        {
            lightObject = existing.gameObject;
            EnsureSceneComponent<CanvasRenderer>(lightObject);
            EnsureSceneComponent<Image>(lightObject);
        }
        else
        {
            lightObject = CreateSceneGameObject(lightName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            lightObject.transform.SetParent(lightingRoot, false);
        }

        RoomLightOverlay overlay = EnsureSceneComponent<RoomLightOverlay>(lightObject);
        overlay.ApplyDefinition(definition);
        MarkSceneDirtyIfEditing(lightObject);
    }

    private static Transform FindOrCreateLightingRoot(Transform roomTransform)
    {
        Transform existing = roomTransform.Find(LightingRootName);

        if (existing != null)
        {
            return existing;
        }

        GameObject rootObject = CreateSceneGameObject(LightingRootName, typeof(RectTransform));
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

        MarkSceneDirtyIfEditing(rootObject);
        return root;
    }

    private void CacheLightingStructureObjects(Transform roomTransform)
    {
        Transform[] roomTransforms = roomTransform.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < roomTransforms.Length; i++)
        {
            Transform candidate = roomTransforms[i];

            if (candidate == null)
            {
                continue;
            }

            if (IsLightingPayloadRoot(candidate.name))
            {
                CacheLightingRootChildren(candidate);
                continue;
            }

            CacheLooseLightParticle(candidate, roomTransform);
        }
    }

    private void CacheLightingRootChildren(Transform lightingRoot)
    {
        for (int i = 0; i < lightingRoot.childCount; i++)
        {
            GameObject childObject = lightingRoot.GetChild(i).gameObject;

            CacheLightingStructureObject(childObject);
        }
    }

    private void CacheLooseLightParticle(Transform candidate, Transform roomTransform)
    {
        if (candidate == roomTransform || !LooksLikeLooseLightParticle(candidate.gameObject))
        {
            return;
        }

        CacheLightingStructureObject(candidate.gameObject);
    }

    private void CacheLightingStructureObject(GameObject lightingObject)
    {
        if (lightingObject == null)
        {
            return;
        }

        if (!lightingStructureObjects.Contains(lightingObject))
        {
            lightingStructureObjects.Add(lightingObject);
        }

        if (!lightingStructureActiveStates.ContainsKey(lightingObject))
        {
            lightingStructureActiveStates[lightingObject] = lightingObject.activeSelf;
        }
    }

    private static bool IsLightingPayloadRoot(string objectName)
    {
        for (int i = 0; i < LightingPayloadRootNames.Length; i++)
        {
            if (string.Equals(objectName, LightingPayloadRootNames[i], System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeLooseLightParticle(GameObject candidate)
    {
        if (candidate == null || candidate.GetComponent<ParticleSystem>() == null)
        {
            return false;
        }

        string objectName = candidate.name;

        for (int i = 0; i < LooseLightParticleNameFragments.Length; i++)
        {
            if (objectName.IndexOf(LooseLightParticleNameFragments[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private void CaptureLightingStructureActiveStates()
    {
        for (int i = 0; i < lightingStructureObjects.Count; i++)
        {
            GameObject lightingObject = lightingStructureObjects[i];

            if (lightingObject != null)
            {
                lightingStructureActiveStates[lightingObject] = lightingObject.activeSelf;
            }
        }
    }

    private void ApplyLightingStructureActiveState()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        for (int i = 0; i < lightingStructureObjects.Count; i++)
        {
            GameObject lightingObject = lightingStructureObjects[i];

            if (lightingObject == null)
            {
                continue;
            }

            if (!lightingStructureActiveStates.TryGetValue(lightingObject, out bool authoredActive))
            {
                authoredActive = lightingObject.activeSelf;
                lightingStructureActiveStates[lightingObject] = authoredActive;
            }

            bool targetActive = lightsOn && authoredActive;

            if (lightingObject.activeSelf != targetActive)
            {
                lightingObject.SetActive(targetActive);
            }
        }
    }

    private void PruneLightingStructureActiveStates()
    {
        List<GameObject> staleObjects = null;

        foreach (KeyValuePair<GameObject, bool> entry in lightingStructureActiveStates)
        {
            if (entry.Key == null || !lightingStructureObjects.Contains(entry.Key))
            {
                if (staleObjects == null)
                {
                    staleObjects = new List<GameObject>();
                }

                staleObjects.Add(entry.Key);
            }
        }

        if (staleObjects == null)
        {
            return;
        }

        for (int i = 0; i < staleObjects.Count; i++)
        {
            lightingStructureActiveStates.Remove(staleObjects[i]);
        }
    }

    private void BuildHud()
    {
        if (hudText != null)
        {
            return;
        }

        if (FindAnyObjectByType<EventSystem>() == null)
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

    private void ApplyLightBlendToOverlays()
    {
        for (int i = 0; i < overlays.Count; i++)
        {
            RoomLightOverlay overlay = overlays[i];

            if (overlay != null)
            {
                overlay.SetLightBlend(lightBlend);
            }
        }
    }

    private void ApplyGlobalBloomIntensity(bool force = false)
    {
        if (!Application.isPlaying || !driveGlobalBloomIntensity)
        {
            return;
        }

        if (!ResolveGlobalBloom())
        {
            return;
        }

        float targetIntensity = lightsOn ? lightsOnBloomIntensity : lightsOffBloomIntensity;

        if (!force && Mathf.Approximately(lastAppliedBloomIntensity, targetIntensity))
        {
            return;
        }

        globalBloom.active = true;
        globalBloom.intensity.overrideState = true;
        globalBloom.intensity.value = targetIntensity;
        lastAppliedBloomIntensity = targetIntensity;
    }

    private bool ResolveGlobalBloom()
    {
        if (globalBloom != null)
        {
            return true;
        }

        if (globalVolume == null)
        {
            Volume[] volumes = FindObjectsByType<Volume>(FindObjectsInactive.Include);

            for (int i = 0; i < volumes.Length; i++)
            {
                Volume volume = volumes[i];

                if (volume != null && volume.isGlobal)
                {
                    globalVolume = volume;
                    break;
                }
            }
        }

        if (globalVolume == null)
        {
            Debug.LogWarning("Room lighting could not find a Global Volume to drive bloom intensity.", this);
            return false;
        }

        VolumeProfile profile = globalVolume.profile;

        if (profile == null)
        {
            Debug.LogWarning("Room lighting found a Global Volume, but it has no profile.", globalVolume);
            return false;
        }

        if (!profile.TryGet(out globalBloom))
        {
            globalBloom = profile.Add<Bloom>(true);
        }

        return globalBloom != null;
    }

    private static string GetSceneLightName(RoomLightDefinition definition, int index)
    {
        return string.IsNullOrWhiteSpace(definition.lightName)
            ? $"RoomLight_{index:00}"
            : $"RoomLight_{definition.lightName.Trim().Replace(' ', '_')}";
    }

    private static bool IsGeneratedFlameCoreSceneLightName(string objectName)
    {
        return !string.IsNullOrWhiteSpace(objectName)
            && objectName.StartsWith("RoomLight_", System.StringComparison.Ordinal)
            && objectName.IndexOf("_Flame_Core", System.StringComparison.Ordinal) >= 0;
    }

    private static GameObject CreateSceneGameObject(string objectName, params System.Type[] components)
    {
        GameObject gameObject = new GameObject(objectName, components);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.RegisterCreatedObjectUndo(gameObject, $"Create {objectName}");
        }
#endif

        return gameObject;
    }

    private static T EnsureSceneComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();

        if (component != null)
        {
            return component;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            component = Undo.AddComponent<T>(target);
            MarkSceneDirtyIfEditing(target);
            return component;
        }
#endif

        return target.AddComponent<T>();
    }

    private static void RemoveSceneObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        target.SetActive(false);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.DestroyObjectImmediate(target);
            return;
        }
#endif

        Destroy(target);
    }

    private static void MarkSceneDirtyIfEditing(GameObject target)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && target != null && target.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(target.scene);
            EditorUtility.SetDirty(target);
        }
#endif
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

}
