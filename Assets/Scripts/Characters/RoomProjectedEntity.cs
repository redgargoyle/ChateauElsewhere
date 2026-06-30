using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Room Projected Entity")]
public sealed class RoomProjectedEntity : MonoBehaviour
{
    public enum ProjectionMode
    {
        FloorCharacter,
        FloorProp,
        WallProp,
        FurnitureSurfaceProp,
        ForegroundOccluder
    }

    [SerializeField] private RoomPerspectiveProfile roomProfile;
    [SerializeField] private CharacterVisualProfile visualProfile;
    [SerializeField] private ProjectionMode projectionMode = ProjectionMode.FloorCharacter;
    [SerializeField] private Vector2 roomLocalFootPoint;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool applyPosition = true;
    [SerializeField] private bool applyScale = true;
    [SerializeField] private bool applyTint = true;
    [SerializeField] private bool applySorting = true;
    [SerializeField] private bool includeInactiveRenderers = true;
    [SerializeField] private bool requireActorRoomMatch = true;
    [SerializeField] private bool normalizeLogicalRootScale = true;
    [SerializeField] private Vector3 logicalRootScale = Vector3.one;
    [SerializeField] private int sortingOffset;
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private Transform contactShadowRoot;
    [SerializeField] private SpriteRenderer contactShadowRenderer;
    [SerializeField] private Graphic contactShadowGraphic;
    [SerializeField] private bool useButlerCharacterScaleRules = true;
    [SerializeField] private PointClickPlayerMovement butlerScaleSource;
    [SerializeField] private bool ignoreRoomVisualScaleOverridesWhenUsingButlerRules = true;
    [SerializeField] private bool useButlerRulesOnlyForFloorCharacters = true;
    [SerializeField] private bool useRoomVisualScaleOverrides = true;
    [SerializeField, HideInInspector] private string editorSelectedVisualScaleRoomId = string.Empty;
    [SerializeField, HideInInspector] private List<RoomVisualScaleOverride> roomVisualScaleOverrides = new List<RoomVisualScaleOverride>();

    private RectTransform rectTransform;
    private SpriteRenderer[] spriteRenderers = Array.Empty<SpriteRenderer>();
    private Graphic[] graphics = Array.Empty<Graphic>();
    private Color[] spriteRendererBaseColors = Array.Empty<Color>();
    private Color[] graphicBaseColors = Array.Empty<Color>();
    private Transform cachedVisualRoot;
    private ActorRoomState actorRoomState;
    [SerializeField, HideInInspector] private Vector3 authoredVisualRootScale = Vector3.one;
    [SerializeField, HideInInspector] private bool hasAuthoredVisualRootScale;
    private float currentScale = 1f;
    private float currentRoomStageScaleMultiplier = 1f;
    private int currentSortingOrder;
    private bool isUsingButlerCharacterScaleRules;
    private float currentButlerCharacterScale = 1f;
    private float currentButlerCharacterFinalLocalScaleY = 1f;
    private float currentButlerCharacterDepth01;
    private string currentButlerCharacterScaleSource = string.Empty;
    private bool hasRoomStageScaleReference;
    private float roomStageScaleReference = 1f;
    private string roomStageScaleReferenceRoom = string.Empty;

    public RoomPerspectiveProfile RoomProfile => roomProfile;
    public CharacterVisualProfile VisualProfile => visualProfile;
    public ProjectionMode Mode => projectionMode;
    public Vector2 RoomLocalFootPoint => roomLocalFootPoint;
    public Transform VisualRoot => visualRoot != null ? visualRoot : transform;
    public bool HasUsableProfile => roomProfile != null;
    public bool IsProjectionActive => ShouldApplyProjection();
    public float CurrentScale => currentScale;
    public float CurrentRoomStageScaleMultiplier => currentRoomStageScaleMultiplier;
    public int CurrentSortingOrder => currentSortingOrder;
    public bool UseButlerCharacterScaleRules => useButlerCharacterScaleRules;
    public PointClickPlayerMovement ButlerScaleSource => butlerScaleSource;
    public bool IgnoreRoomVisualScaleOverridesWhenUsingButlerRules => ignoreRoomVisualScaleOverridesWhenUsingButlerRules;
    public bool UseButlerRulesOnlyForFloorCharacters => useButlerRulesOnlyForFloorCharacters;
    public bool IsUsingButlerCharacterScaleRules => isUsingButlerCharacterScaleRules;
    public float CurrentButlerCharacterScale => currentButlerCharacterScale;
    public float CurrentButlerCharacterDepth01 => currentButlerCharacterDepth01;
    public string CurrentButlerCharacterScaleSource => currentButlerCharacterScaleSource;
    public bool UsesRoomVisualScaleOverrides => useRoomVisualScaleOverrides;
    public string EditorSelectedVisualScaleRoomId => editorSelectedVisualScaleRoomId;
    public string CurrentVisualScaleRoomId => GetCurrentVisualScaleRoomKey();

    private void Reset()
    {
        visualRoot = transform;
        rectTransform = transform as RectTransform;
        roomLocalFootPoint = rectTransform != null
            ? rectTransform.anchoredPosition
            : new Vector2(transform.localPosition.x, transform.localPosition.y);
        ResolveReferences();
        CaptureAuthoredVisualScale(true);
        RefreshVisualTargets();
    }

    private void Awake()
    {
        ResolveReferences();
        CaptureAuthoredVisualScale(false);
        RefreshVisualTargets();
        ApplyProjection();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CaptureAuthoredVisualScale(false);
        RefreshVisualTargets();
        ApplyProjection();
    }

    private void OnValidate()
    {
        logicalRootScale = new Vector3(
            Mathf.Approximately(logicalRootScale.x, 0f) ? 1f : logicalRootScale.x,
            Mathf.Approximately(logicalRootScale.y, 0f) ? 1f : logicalRootScale.y,
            Mathf.Approximately(logicalRootScale.z, 0f) ? 1f : logicalRootScale.z);
        ResolveReferences();
        CaptureAuthoredVisualScale(false);
        RefreshVisualTargets();
        ApplyProjection();
    }

    private void OnTransformChildrenChanged()
    {
        RefreshVisualTargets();
        ApplyProjection();
    }

    private void LateUpdate()
    {
        if (Application.isPlaying)
        {
            ApplyProjection();
        }
    }

    private void OnDisable()
    {
        ClearRoomStageScaleReference();
    }

    public void SetRoomProfile(RoomPerspectiveProfile profile)
    {
        roomProfile = profile;
        ClearRoomStageScaleReference();
        ApplyProjection();
    }

    public void SetVisualProfile(CharacterVisualProfile profile)
    {
        visualProfile = profile;
        ApplyProjection();
    }

    public void SetVisualRoot(Transform root)
    {
        visualRoot = root != null ? root : transform;
        CaptureAuthoredVisualScale(true);
        RefreshVisualTargets();
        ApplyProjection();
    }

    public void SetButlerCharacterScaleRulesEnabled(bool value, bool applyImmediately = true)
    {
        useButlerCharacterScaleRules = value;

        if (applyImmediately)
        {
            ApplyProjection();
        }
    }

    public void SetButlerScaleSource(PointClickPlayerMovement source, bool applyImmediately = true)
    {
        butlerScaleSource = source;

        if (applyImmediately)
        {
            ApplyProjection();
        }
    }

    public void SetIgnoreRoomVisualScaleOverridesWhenUsingButlerRules(bool value, bool applyImmediately = true)
    {
        ignoreRoomVisualScaleOverridesWhenUsingButlerRules = value;

        if (applyImmediately)
        {
            ApplyProjection();
        }
    }

    public void SetUseButlerRulesOnlyForFloorCharacters(bool value, bool applyImmediately = true)
    {
        useButlerRulesOnlyForFloorCharacters = value;

        if (applyImmediately)
        {
            ApplyProjection();
        }
    }

    public void SetRoomVisualScaleOverridesEnabled(bool value, bool applyImmediately = true)
    {
        useRoomVisualScaleOverrides = value;

        if (applyImmediately)
        {
            ApplyProjection();
        }
    }

    public void SetEditorSelectedVisualScaleRoomId(string roomId)
    {
        editorSelectedVisualScaleRoomId = CleanRoomId(roomId);
    }

    public Vector3 GetVisualRootScaleForRoom(string roomId)
    {
        return TryGetVisualRootScaleForRoom(roomId, out Vector3 scale)
            ? scale
            : GetDefaultAuthoredVisualRootScale();
    }

    public bool TryGetVisualRootScaleForRoom(string roomId, out Vector3 scale)
    {
        scale = Vector3.one;

        if (!useRoomVisualScaleOverrides ||
            string.IsNullOrWhiteSpace(roomId) ||
            roomVisualScaleOverrides == null)
        {
            return false;
        }

        string cleanRoomId = CleanRoomId(roomId);

        for (int i = 0; i < roomVisualScaleOverrides.Count; i++)
        {
            RoomVisualScaleOverride roomScale = roomVisualScaleOverrides[i];

            if (roomScale.Matches(cleanRoomId))
            {
                scale = roomScale.VisualRootScale;
                return true;
            }
        }

        return false;
    }

    public bool HasVisualRootScaleForRoom(string roomId)
    {
        return TryGetVisualRootScaleForRoom(roomId, out _);
    }

    public void SetVisualRootScaleForRoom(string roomId, Vector3 scale, bool applyImmediately = true)
    {
        string cleanRoomId = CleanRoomId(roomId);

        if (string.IsNullOrWhiteSpace(cleanRoomId))
        {
            return;
        }

        if (roomVisualScaleOverrides == null)
        {
            roomVisualScaleOverrides = new List<RoomVisualScaleOverride>();
        }

        scale = SanitizeScale(scale);
        int existingIndex = GetVisualScaleOverrideIndex(cleanRoomId);

        if (existingIndex >= 0)
        {
            roomVisualScaleOverrides[existingIndex] = new RoomVisualScaleOverride(cleanRoomId, scale);
        }
        else
        {
            roomVisualScaleOverrides.Add(new RoomVisualScaleOverride(cleanRoomId, scale));
        }

        useRoomVisualScaleOverrides = true;
        SetEditorSelectedVisualScaleRoomId(cleanRoomId);

        if (applyImmediately)
        {
            ApplyProjection();
        }
    }

    public bool RemoveVisualRootScaleForRoom(string roomId, bool applyImmediately = true)
    {
        string cleanRoomId = CleanRoomId(roomId);
        int existingIndex = GetVisualScaleOverrideIndex(cleanRoomId);

        if (existingIndex < 0)
        {
            return false;
        }

        roomVisualScaleOverrides.RemoveAt(existingIndex);

        if (applyImmediately)
        {
            ApplyProjection();
        }

        return true;
    }

    public Vector3 CaptureCurrentVisualRootScaleForRoom(string roomId, bool removeProjectionMultiplier = true)
    {
        Transform targetRoot = VisualRoot;
        Vector3 currentVisualRootScale = targetRoot != null
            ? SanitizeScale(targetRoot.localScale)
            : GetDefaultAuthoredVisualRootScale();

        if (removeProjectionMultiplier && applyScale && ShouldApplyProjection())
        {
            float projectionMultiplier = Mathf.Max(
                0.001f,
                Mathf.Max(0.001f, currentScale > 0f ? currentScale : GetProjectedScale()) *
                Mathf.Max(0.001f, currentRoomStageScaleMultiplier > 0f ? currentRoomStageScaleMultiplier : 1f));
            currentVisualRootScale = new Vector3(
                currentVisualRootScale.x / projectionMultiplier,
                currentVisualRootScale.y / projectionMultiplier,
                currentVisualRootScale.z);
        }

        SetVisualRootScaleForRoom(roomId, currentVisualRootScale);
        return currentVisualRootScale;
    }

    public void GetVisualScaleOverrideRoomIds(List<string> roomIds)
    {
        if (roomIds == null || roomVisualScaleOverrides == null)
        {
            return;
        }

        for (int i = 0; i < roomVisualScaleOverrides.Count; i++)
        {
            string roomId = roomVisualScaleOverrides[i].RoomId;

            if (!string.IsNullOrWhiteSpace(roomId) && !ContainsRoomId(roomIds, roomId))
            {
                roomIds.Add(roomId);
            }
        }
    }

    public void SetRoomLocalFootPoint(Vector2 footPoint, bool applyImmediately = true)
    {
        roomLocalFootPoint = footPoint;

        if (applyImmediately)
        {
            ApplyProjection();
        }
    }

    public bool TrySetRoomLocalFootPointFromTarget(Transform target, bool applyImmediately = true)
    {
        UseProfileFromRoomTargetIfNeeded(target);

        if (!CanProjectTarget(target) ||
            !TryGetRoomLocalFootPointForTarget(target, out Vector2 footPoint))
        {
            return false;
        }

        SetRoomLocalFootPoint(footPoint, applyImmediately);
        return true;
    }

    public void UseProfileFromRoomTarget(Transform target)
    {
        UseProfileFromRoomTargetIfNeeded(target);
    }

    public bool CanProjectTarget(Transform target)
    {
        if (roomProfile == null)
        {
            return false;
        }

        if (target == null || string.IsNullOrWhiteSpace(roomProfile.RoomId))
        {
            return true;
        }

        RoomContentGroup targetRoom = target.GetComponentInParent<RoomContentGroup>(true);
        return targetRoom == null || SameRoom(targetRoom.RoomName, roomProfile.RoomId);
    }

    public bool TryGetRoomLocalFootPointForTarget(Transform target, out Vector2 footPoint)
    {
        footPoint = Vector2.zero;

        if (target == null)
        {
            return false;
        }

        RoomContentGroup targetRoom = target.GetComponentInParent<RoomContentGroup>(true);
        RectTransform targetRoomStage = targetRoom != null ? targetRoom.transform as RectTransform : null;

        if (targetRoomStage != null)
        {
            Vector3 localPoint = targetRoomStage.InverseTransformPoint(target.position);
            footPoint = new Vector2(localPoint.x, localPoint.y);
            return true;
        }

        if (target is RectTransform targetRectTransform)
        {
            footPoint = targetRectTransform.anchoredPosition;
            return true;
        }

        RoomContentGroup ownRoom = GetComponentInParent<RoomContentGroup>(true);
        RectTransform ownRoomStage = ownRoom != null ? ownRoom.transform as RectTransform : null;

        if (ownRoomStage != null)
        {
            Vector3 localPoint = ownRoomStage.InverseTransformPoint(target.position);
            footPoint = new Vector2(localPoint.x, localPoint.y);
            return true;
        }

        footPoint = new Vector2(target.localPosition.x, target.localPosition.y);
        return true;
    }

    public void RefreshVisualTargets()
    {
        Transform targetRoot = VisualRoot;
        spriteRenderers = includeInactiveRenderers
            ? targetRoot.GetComponentsInChildren<SpriteRenderer>(true)
            : targetRoot.GetComponentsInChildren<SpriteRenderer>();
        graphics = includeInactiveRenderers
            ? targetRoot.GetComponentsInChildren<Graphic>(true)
            : targetRoot.GetComponentsInChildren<Graphic>();

        spriteRendererBaseColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            spriteRendererBaseColors[i] = spriteRenderers[i] != null ? spriteRenderers[i].color : Color.white;
        }

        graphicBaseColors = new Color[graphics.Length];
        for (int i = 0; i < graphics.Length; i++)
        {
            graphicBaseColors[i] = graphics[i] != null ? graphics[i].color : Color.white;
        }
    }

    public void ApplyProjection()
    {
        ResolveReferences();
        CaptureAuthoredVisualScale(false);

        if (!ShouldApplyProjection())
        {
            ClearButlerCharacterScaleDebug();
            ClearRoomStageScaleReference();
            return;
        }

        currentScale = GetProjectedScale();
        currentRoomStageScaleMultiplier = GetRoomStageScaleMultiplier();
        currentSortingOrder = roomProfile.GetSortingOrder(roomLocalFootPoint, sortingOffset);

        if (normalizeLogicalRootScale && VisualRoot != transform)
        {
            transform.localScale = logicalRootScale;
        }

        if (applyPosition)
        {
            ApplyProjectedPosition();
        }

        if (applyScale)
        {
            ApplyProjectedScale();
        }

        if (applyTint)
        {
            ApplyProjectedTint();
        }

        if (applySorting)
        {
            ApplyProjectedSorting();
        }

        ApplyContactShadow();
    }

    public float GetProjectedScale()
    {
        float profileScale;

        if (TryGetButlerCharacterScaleForThisEntity(out PointClickPlayerMovement.ButlerCharacterScaleSample sample))
        {
            profileScale = sample.NormalizedScale;
            isUsingButlerCharacterScaleRules = true;
            currentButlerCharacterScale = sample.NormalizedScale;
            currentButlerCharacterFinalLocalScaleY = sample.ButlerFinalLocalScaleY;
            currentButlerCharacterDepth01 = sample.Depth01;
            currentButlerCharacterScaleSource = sample.Source;
        }
        else
        {
            profileScale = roomProfile != null ? roomProfile.GetScale(roomLocalFootPoint) : 1f;
            ClearButlerCharacterScaleDebug();
        }

        float visualScale = visualProfile != null ? visualProfile.HeightScaleMultiplier : 1f;
        return Mathf.Max(0.001f, profileScale * visualScale);
    }

    public int GetSortingOrder(int localOffset = 0)
    {
        return roomProfile != null
            ? roomProfile.GetSortingOrder(roomLocalFootPoint, sortingOffset + localOffset)
            : sortingOffset + localOffset;
    }

    public string GetSortingLayerName()
    {
        return roomProfile != null ? roomProfile.SortingLayerName : "Default";
    }

    private void ResolveReferences()
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (cameraManager == null)
        {
            cameraManager = FindAnyObjectByType<CameraManager>(FindObjectsInactive.Include);
        }

        if (actorRoomState == null)
        {
            actorRoomState = GetComponentInParent<ActorRoomState>();
        }
    }

    private void CaptureAuthoredVisualScale(bool force)
    {
        Transform targetRoot = VisualRoot;
        Vector3 currentVisualRootScale = targetRoot != null
            ? SanitizeScale(targetRoot.localScale)
            : Vector3.one;
        if (!force && hasAuthoredVisualRootScale)
        {
            cachedVisualRoot = targetRoot;
            return;
        }

        cachedVisualRoot = targetRoot;
        authoredVisualRootScale = currentVisualRootScale;
        hasAuthoredVisualRootScale = true;
    }

    private void ApplyProjectedPosition()
    {
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = roomLocalFootPoint;
            return;
        }

        RoomContentGroup roomContent = GetComponentInParent<RoomContentGroup>(true);
        RectTransform roomStage = roomContent != null ? roomContent.transform as RectTransform : null;

        if (roomStage != null)
        {
            Vector3 localPosition = transform.localPosition;
            localPosition.x = roomLocalFootPoint.x;
            localPosition.y = roomLocalFootPoint.y;
            transform.localPosition = localPosition;
            return;
        }

        if (TryApplyWorldPositionFromActiveRoomStage())
        {
            return;
        }

        Vector3 fallbackLocalPosition = transform.localPosition;
        fallbackLocalPosition.x = roomLocalFootPoint.x;
        fallbackLocalPosition.y = roomLocalFootPoint.y;
        transform.localPosition = fallbackLocalPosition;
    }

    private bool TryApplyWorldPositionFromActiveRoomStage()
    {
        if (cameraManager == null)
        {
            return false;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return false;
        }

        float depth = transform.position.z - mainCamera.transform.position.z;
        if (depth <= 0.01f)
        {
            depth = Mathf.Abs(depth);
        }

        if (depth <= 0.01f)
        {
            depth = 10f;
        }

        if (!cameraManager.TryGetActiveRoomStageWorldPoint(roomLocalFootPoint, depth, out Vector3 worldPoint))
        {
            return false;
        }

        worldPoint.z = transform.position.z;
        transform.position = worldPoint;
        return true;
    }

    private void ApplyProjectedScale()
    {
        Transform targetRoot = VisualRoot;

        if (targetRoot == null)
        {
            return;
        }

        Vector3 baseScale = isUsingButlerCharacterScaleRules && ignoreRoomVisualScaleOverridesWhenUsingButlerRules
            ? GetDefaultAuthoredVisualRootScale()
            : GetAuthoredVisualRootScaleForCurrentRoom();

        if (isUsingButlerCharacterScaleRules)
        {
            targetRoot.localScale = BuildFinalLocalScaleFromReference(
                baseScale,
                currentButlerCharacterFinalLocalScaleY * currentRoomStageScaleMultiplier);
            return;
        }

        float appliedScale = currentScale * currentRoomStageScaleMultiplier;
        Vector3 projectedScale = new Vector3(
            baseScale.x * appliedScale,
            baseScale.y * appliedScale,
            baseScale.z);

        targetRoot.localScale = projectedScale;
    }

    public void ApplyButlerCharacterScaleNow(PointClickPlayerMovement source = null)
    {
        ApplyButlerCharacterScaleNow(source, 1f);
    }

    public void ApplyButlerCharacterScaleNow(PointClickPlayerMovement source, float debugScaleMultiplier)
    {
        if (source != null)
        {
            butlerScaleSource = source;
        }

        ResolveReferences();
        CaptureAuthoredVisualScale(false);
        RefreshVisualTargets();
        ApplyProjection();

        if (!TryGetButlerCharacterScaleForThisEntity(out PointClickPlayerMovement.ButlerCharacterScaleSample sample))
        {
            ClearButlerCharacterScaleDebug();
            return;
        }

        ForceApplyButlerCharacterScale(sample, debugScaleMultiplier);
    }

    public bool TryGetButlerCharacterScaleSample(out PointClickPlayerMovement.ButlerCharacterScaleSample sample)
    {
        return TryGetButlerCharacterScaleForThisEntity(out sample);
    }

    private void ForceApplyButlerCharacterScale(
        PointClickPlayerMovement.ButlerCharacterScaleSample sample,
        float debugScaleMultiplier)
    {
        Transform targetRoot = VisualRoot;

        if (targetRoot == null)
        {
            return;
        }

        isUsingButlerCharacterScaleRules = true;
        currentButlerCharacterScale = sample.NormalizedScale;
        currentButlerCharacterFinalLocalScaleY = sample.ButlerFinalLocalScaleY;
        currentButlerCharacterDepth01 = sample.Depth01;
        currentButlerCharacterScaleSource = sample.Source;

        float finalLocalScaleY =
            Mathf.Max(0.001f, sample.ButlerFinalLocalScaleY) *
            Mathf.Max(0.001f, debugScaleMultiplier) *
            Mathf.Max(0.001f, currentRoomStageScaleMultiplier > 0f ? currentRoomStageScaleMultiplier : GetRoomStageScaleMultiplier());
        Vector3 baseScale = ignoreRoomVisualScaleOverridesWhenUsingButlerRules
            ? GetDefaultAuthoredVisualRootScale()
            : GetAuthoredVisualRootScaleForCurrentRoom();

        targetRoot.localScale = BuildFinalLocalScaleFromReference(baseScale, finalLocalScaleY);
        currentScale = Mathf.Max(0.001f, sample.NormalizedScale);
    }

    private void ApplyProjectedTint()
    {
        Color tint = roomProfile.GetTint(roomLocalFootPoint);

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
            {
                continue;
            }

            Color baseColor = i < spriteRendererBaseColors.Length ? spriteRendererBaseColors[i] : Color.white;
            spriteRenderers[i].color = MultiplyColor(baseColor, tint);
        }

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] == null)
            {
                continue;
            }

            Color baseColor = i < graphicBaseColors.Length ? graphicBaseColors[i] : Color.white;
            graphics[i].color = MultiplyColor(baseColor, tint);
        }
    }

    private void ApplyProjectedSorting()
    {
        string layerName = GetSortingLayerName();

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];

            if (spriteRenderer == null)
            {
                continue;
            }

            int localOffset = visualProfile != null
                ? visualProfile.GetSortingOffsetForRenderer(spriteRenderer.transform)
                : 0;
            spriteRenderer.sortingLayerName = layerName;
            spriteRenderer.sortingOrder = GetSortingOrder(localOffset);
            spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        }
    }

    private void ApplyContactShadow()
    {
        if (roomProfile == null || contactShadowRoot == null)
        {
            return;
        }

        float shadowScale = roomProfile.GetShadowScale(roomLocalFootPoint) * currentRoomStageScaleMultiplier;
        contactShadowRoot.localScale = new Vector3(shadowScale, shadowScale, contactShadowRoot.localScale.z);
        float opacity = roomProfile.GetShadowOpacity(roomLocalFootPoint);

        if (contactShadowRenderer != null)
        {
            Color color = contactShadowRenderer.color;
            color.a = opacity;
            contactShadowRenderer.color = color;
            contactShadowRenderer.sortingLayerName = GetSortingLayerName();
            contactShadowRenderer.sortingOrder = GetSortingOrder(visualProfile != null ? visualProfile.ShadowSortingOffset : -2);
        }

        if (contactShadowGraphic != null)
        {
            Color color = contactShadowGraphic.color;
            color.a = opacity;
            contactShadowGraphic.color = color;
        }
    }

    private void UseProfileFromRoomTargetIfNeeded(Transform target)
    {
        if (roomProfile != null || target == null)
        {
            return;
        }

        RoomContentGroup roomContent = target.GetComponentInParent<RoomContentGroup>(true);

        if (roomContent != null && roomContent.TryGetPerspectiveProfile(out RoomPerspectiveProfile profile))
        {
            roomProfile = profile;
        }
    }

    private bool ShouldApplyProjection()
    {
        if (roomProfile == null)
        {
            return false;
        }

        if (!requireActorRoomMatch)
        {
            return true;
        }

        if (actorRoomState == null)
        {
            actorRoomState = GetComponentInParent<ActorRoomState>();
        }

        if (actorRoomState == null ||
            string.IsNullOrWhiteSpace(actorRoomState.CurrentRoomId) ||
            string.IsNullOrWhiteSpace(roomProfile.RoomId))
        {
            return true;
        }

        return SameRoom(actorRoomState.CurrentRoomId, roomProfile.RoomId);
    }

    private Vector3 GetAuthoredVisualRootScaleForCurrentRoom()
    {
        string roomKey = GetCurrentVisualScaleRoomKey();
        return TryGetVisualRootScaleForRoom(roomKey, out Vector3 roomScale)
            ? roomScale
            : GetDefaultAuthoredVisualRootScale();
    }

    private Vector3 GetDefaultAuthoredVisualRootScale()
    {
        return hasAuthoredVisualRootScale ? authoredVisualRootScale : Vector3.one;
    }

    private string GetCurrentVisualScaleRoomKey()
    {
        if (!Application.isPlaying && !string.IsNullOrWhiteSpace(editorSelectedVisualScaleRoomId))
        {
            return editorSelectedVisualScaleRoomId;
        }

        return GetCurrentProjectionRoomKey();
    }

    private float GetRoomStageScaleMultiplier()
    {
        if (IsAlreadyOwnedByRoomStage())
        {
            ClearRoomStageScaleReference();
            return 1f;
        }

        if (cameraManager == null)
        {
            cameraManager = FindAnyObjectByType<CameraManager>(FindObjectsInactive.Include);
        }

        Camera mainCamera = Camera.main;
        if (cameraManager == null || mainCamera == null)
        {
            ClearRoomStageScaleReference();
            return 1f;
        }

        float depth = transform.position.z - mainCamera.transform.position.z;
        if (depth <= 0.01f)
        {
            depth = Mathf.Abs(depth);
        }

        if (depth <= 0.01f)
        {
            depth = 10f;
        }

        if (!cameraManager.TryGetActiveRoomStageWorldPoint(
            roomLocalFootPoint,
            depth,
            out _,
            out float roomStageScale))
        {
            ClearRoomStageScaleReference();
            return 1f;
        }

        string roomKey = GetCurrentProjectionRoomKey();
        if (!hasRoomStageScaleReference ||
            !SameRoom(roomStageScaleReferenceRoom, roomKey))
        {
            roomStageScaleReference = Mathf.Max(0.0001f, roomStageScale);
            roomStageScaleReferenceRoom = roomKey;
            hasRoomStageScaleReference = true;
        }

        return roomStageScale / Mathf.Max(0.0001f, roomStageScaleReference);
    }

    private bool IsAlreadyOwnedByRoomStage()
    {
        Transform targetRoot = VisualRoot;
        return targetRoot != null &&
            targetRoot.GetComponentInParent<RoomContentGroup>(true) != null;
    }

    private string GetCurrentProjectionRoomKey()
    {
        if (actorRoomState != null &&
            !string.IsNullOrWhiteSpace(actorRoomState.CurrentRoomId))
        {
            return actorRoomState.CurrentRoomId;
        }

        if (roomProfile != null && !string.IsNullOrWhiteSpace(roomProfile.RoomId))
        {
            return roomProfile.RoomId;
        }

        RoomContentGroup parentRoom = GetComponentInParent<RoomContentGroup>(true);
        if (parentRoom != null && !string.IsNullOrWhiteSpace(parentRoom.RoomName))
        {
            return parentRoom.RoomName;
        }

        return !string.IsNullOrWhiteSpace(editorSelectedVisualScaleRoomId)
            ? editorSelectedVisualScaleRoomId
            : string.Empty;
    }

    private bool TryGetButlerCharacterScaleForThisEntity(out PointClickPlayerMovement.ButlerCharacterScaleSample sample)
    {
        sample = default;

        if (!useButlerCharacterScaleRules ||
            (useButlerRulesOnlyForFloorCharacters && projectionMode != ProjectionMode.FloorCharacter))
        {
            return false;
        }

        string roomId = GetCurrentProjectionRoomKey();

        if (string.IsNullOrWhiteSpace(roomId))
        {
            return false;
        }

        PointClickPlayerMovement source = ResolveButlerScaleSource();
        return source != null &&
            source.TryEvaluateButlerCharacterScale(roomId, roomLocalFootPoint, out sample);
    }

    private PointClickPlayerMovement ResolveButlerScaleSource()
    {
        if (butlerScaleSource != null)
        {
            return butlerScaleSource;
        }

        PointClickPlayerMovement activeTaggedPlayer = null;
        PointClickPlayerMovement activeNamedPlayer = null;
        PointClickPlayerMovement firstActive = null;
        PointClickPlayerMovement firstInactive = null;
        PointClickPlayerMovement[] candidates = FindObjectsByType<PointClickPlayerMovement>(FindObjectsInactive.Include);

        for (int i = 0; i < candidates.Length; i++)
        {
            PointClickPlayerMovement candidate = candidates[i];

            if (candidate == null || candidate.gameObject == null)
            {
                continue;
            }

            bool isActive = candidate.gameObject.activeInHierarchy;

            if (isActive)
            {
                firstActive ??= candidate;

                if (string.Equals(candidate.gameObject.tag, "Player", StringComparison.OrdinalIgnoreCase))
                {
                    activeTaggedPlayer ??= candidate;
                }

                if (NameLooksLikePlayerOrButler(candidate.name) ||
                    NameLooksLikePlayerOrButler(candidate.gameObject.name))
                {
                    activeNamedPlayer ??= candidate;
                }
            }
            else if (!Application.isPlaying)
            {
                firstInactive ??= candidate;
            }
        }

        butlerScaleSource =
            activeTaggedPlayer != null
                ? activeTaggedPlayer
                : activeNamedPlayer != null
                    ? activeNamedPlayer
                    : firstActive != null
                        ? firstActive
                        : firstInactive;
        return butlerScaleSource;
    }

    private int GetVisualScaleOverrideIndex(string roomId)
    {
        if (roomVisualScaleOverrides == null || string.IsNullOrWhiteSpace(roomId))
        {
            return -1;
        }

        string cleanRoomId = CleanRoomId(roomId);

        for (int i = 0; i < roomVisualScaleOverrides.Count; i++)
        {
            if (roomVisualScaleOverrides[i].Matches(cleanRoomId))
            {
                return i;
            }
        }

        return -1;
    }

    private void ClearRoomStageScaleReference()
    {
        currentRoomStageScaleMultiplier = 1f;
        hasRoomStageScaleReference = false;
        roomStageScaleReference = 1f;
        roomStageScaleReferenceRoom = string.Empty;
    }

    private void ClearButlerCharacterScaleDebug()
    {
        isUsingButlerCharacterScaleRules = false;
        currentButlerCharacterScale = 1f;
        currentButlerCharacterFinalLocalScaleY = 1f;
        currentButlerCharacterDepth01 = 0f;
        currentButlerCharacterScaleSource = string.Empty;
    }

    private static Color MultiplyColor(Color baseColor, Color tint)
    {
        return new Color(
            baseColor.r * tint.r,
            baseColor.g * tint.g,
            baseColor.b * tint.b,
            baseColor.a * tint.a);
    }

    private static bool SameRoom(string left, string right)
    {
        return string.Equals(NormalizeRoomName(left), NormalizeRoomName(right), StringComparison.OrdinalIgnoreCase);
    }

    private static Vector3 SanitizeScale(Vector3 scale)
    {
        return new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
            Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
            Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);
    }

    private static Vector3 BuildFinalLocalScaleFromReference(Vector3 referenceScale, float finalLocalScaleY)
    {
        Vector3 safeReference = SanitizeScale(referenceScale);
        float safeFinalY = Mathf.Max(0.001f, Mathf.Abs(finalLocalScaleY));
        float referenceY = Mathf.Max(0.001f, Mathf.Abs(safeReference.y));
        float xOverY = safeReference.x / referenceY;
        float ySign = Mathf.Sign(safeReference.y);

        if (Mathf.Approximately(ySign, 0f))
        {
            ySign = 1f;
        }

        return new Vector3(
            xOverY * safeFinalY,
            ySign * safeFinalY,
            safeReference.z);
    }

    private static string NormalizeRoomName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim()
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty);
    }

    private static bool NameLooksLikePlayerOrButler(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            (value.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("Butler", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string CleanRoomId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static bool ContainsRoomId(List<string> roomIds, string roomId)
    {
        if (roomIds == null || string.IsNullOrWhiteSpace(roomId))
        {
            return false;
        }

        for (int i = 0; i < roomIds.Count; i++)
        {
            if (SameRoom(roomIds[i], roomId))
            {
                return true;
            }
        }

        return false;
    }

    [Serializable]
    private struct RoomVisualScaleOverride
    {
        [SerializeField] private string roomId;
        [SerializeField] private Vector3 visualRootScale;

        public RoomVisualScaleOverride(string roomId, Vector3 visualRootScale)
        {
            this.roomId = CleanRoomId(roomId);
            this.visualRootScale = SanitizeScale(visualRootScale);
        }

        public string RoomId => roomId;
        public Vector3 VisualRootScale => SanitizeScale(visualRootScale);

        public bool Matches(string otherRoomId)
        {
            return SameRoom(roomId, otherRoomId);
        }
    }
}
