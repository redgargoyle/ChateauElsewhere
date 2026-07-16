using System;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Room Projected Entity")]
public sealed class RoomProjectedEntity : MonoBehaviour
{
    public enum ProjectionMode
    {
        FloorCharacter = 0,
        FloorProp = 1,
        WallProp = 2,
        FurnitureSurfaceProp = 3,
        ForegroundOccluder = 4
    }

    [SerializeField] private RoomPerspectiveProfile roomProfile;
    [SerializeField] private ProjectionMode projectionMode = ProjectionMode.FloorCharacter;
    [SerializeField] private Vector2 roomLocalFootPoint;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool applyPosition = true;
    [SerializeField] private bool applyScale = true;
    [SerializeField] private bool applyTint = true;
    [SerializeField] private bool applySorting = true;
    [SerializeField] private bool includeInactiveRenderers = true;
    [SerializeField] private bool requireActorRoomMatch = true;
    [SerializeField] private int sortingOffset;
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private Transform contactShadowRoot;
    [SerializeField] private SpriteRenderer contactShadowRenderer;
    [SerializeField] private Graphic contactShadowGraphic;
    [SerializeField, HideInInspector] private Vector3 propProjectionBaseScale = Vector3.one;
    [SerializeField, HideInInspector] private bool hasPropProjectionBaseScale;

    private RectTransform rectTransform;
    private SpriteRenderer[] spriteRenderers = Array.Empty<SpriteRenderer>();
    private Graphic[] graphics = Array.Empty<Graphic>();
    private Color[] spriteRendererBaseColors = Array.Empty<Color>();
    private Color[] graphicBaseColors = Array.Empty<Color>();
    private ActorRoomState actorRoomState;
    private float currentPropProjectionScale = 1f;
    private float currentRoomStageScaleMultiplier = 1f;
    private int currentSortingOrder;
    private bool hasRoomStageScaleReference;
    private float roomStageScaleReference = 1f;
    private string roomStageScaleReferenceRoom = string.Empty;
    private bool projectedSortingSuppressed;

    public RoomPerspectiveProfile RoomProfile => roomProfile;
    public ProjectionMode Mode => projectionMode;
    public Vector2 RoomLocalFootPoint => roomLocalFootPoint;
    public Transform VisualRoot => visualRoot != null ? visualRoot : transform;
    public bool HasUsableProfile => roomProfile != null;
    public bool IsProjectionActive => ShouldApplyProjection();
    public bool OwnsProjectedPosition => applyPosition && IsProjectionActive;
    public bool OwnsProjectedSorting => applySorting && !projectedSortingSuppressed && IsProjectionActive;
    public float CurrentPropProjectionScale => currentPropProjectionScale;
    public float CurrentRoomStageScaleMultiplier => currentRoomStageScaleMultiplier;
    public int CurrentSortingOrder => currentSortingOrder;
    public bool IsProjectedSortingSuppressed => projectedSortingSuppressed;

    private void Reset()
    {
        visualRoot = transform;
        rectTransform = transform as RectTransform;
        roomLocalFootPoint = rectTransform != null
            ? rectTransform.anchoredPosition
            : new Vector2(transform.localPosition.x, transform.localPosition.y);
        ResolveReferences();
        CapturePropProjectionBaseScale(true);
        RefreshVisualTargets();
    }

    private void Awake()
    {
        ResolveReferences();
        CapturePropProjectionBaseScale(false);
        RefreshVisualTargets();
        ApplyProjection();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CapturePropProjectionBaseScale(false);
        RefreshVisualTargets();
        ApplyProjection();
    }

    private void OnValidate()
    {
        ResolveReferences();
        CapturePropProjectionBaseScale(false);
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

    public void SetVisualRoot(Transform root)
    {
        visualRoot = root != null ? root : transform;
        hasPropProjectionBaseScale = false;
        CapturePropProjectionBaseScale(true);
        RefreshVisualTargets();
        ApplyProjection();
    }

    public void SetProjectedSortingSuppressed(bool value)
    {
        if (projectedSortingSuppressed == value)
        {
            return;
        }

        projectedSortingSuppressed = value;

        if (!projectedSortingSuppressed)
        {
            ApplyProjection();
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
        CapturePropProjectionBaseScale(false);

        if (!ShouldApplyProjection())
        {
            ClearRoomStageScaleReference();
            return;
        }

        currentPropProjectionScale = GetPropProjectionScale();
        currentRoomStageScaleMultiplier = GetRoomStageScaleMultiplier();
        currentSortingOrder = roomProfile.GetSortingOrder(roomLocalFootPoint, sortingOffset);

        if (applyPosition)
        {
            ApplyProjectedPosition();
        }

        if (ShouldApplyPropProjectionScale())
        {
            ApplyProjectedPropScale();
        }

        if (applyTint)
        {
            ApplyProjectedTint();
        }

        if (applySorting && !projectedSortingSuppressed)
        {
            ApplyProjectedSorting();
        }

        ApplyContactShadow();
    }

    private float GetPropProjectionScale()
    {
        return roomProfile != null ? roomProfile.GetScale(roomLocalFootPoint) : 1f;
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

    private void CapturePropProjectionBaseScale(bool force)
    {
        if (!IsPropProjectionMode())
        {
            return;
        }

        if (!force && hasPropProjectionBaseScale)
        {
            return;
        }

        Transform targetRoot = VisualRoot;
        propProjectionBaseScale = targetRoot != null
            ? SanitizeScale(targetRoot.localScale)
            : Vector3.one;
        hasPropProjectionBaseScale = true;
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

    private bool ShouldApplyPropProjectionScale()
    {
        return applyScale && IsPropProjectionMode();
    }

    private bool IsPropProjectionMode()
    {
        return projectionMode == ProjectionMode.FloorProp ||
            projectionMode == ProjectionMode.WallProp ||
            projectionMode == ProjectionMode.FurnitureSurfaceProp ||
            projectionMode == ProjectionMode.ForegroundOccluder;
    }

    private void ApplyProjectedPropScale()
    {
        Transform targetRoot = VisualRoot;
        if (targetRoot == null)
        {
            return;
        }

        CapturePropProjectionBaseScale(false);
        float appliedScale = currentPropProjectionScale * currentRoomStageScaleMultiplier;
        Vector3 baseScale = hasPropProjectionBaseScale ? propProjectionBaseScale : Vector3.one;
        targetRoot.localScale = new Vector3(
            baseScale.x * appliedScale,
            baseScale.y * appliedScale,
            baseScale.z);
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

            spriteRenderer.sortingLayerName = layerName;
            spriteRenderer.sortingOrder = GetSortingOrder();
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
            contactShadowRenderer.sortingOrder = GetSortingOrder(-2);
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
        if (!hasRoomStageScaleReference || !SameRoom(roomStageScaleReferenceRoom, roomKey))
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
        return targetRoot != null && targetRoot.GetComponentInParent<RoomContentGroup>(true) != null;
    }

    private string GetCurrentProjectionRoomKey()
    {
        if (actorRoomState != null && !string.IsNullOrWhiteSpace(actorRoomState.CurrentRoomId))
        {
            return actorRoomState.CurrentRoomId;
        }

        if (roomProfile != null && !string.IsNullOrWhiteSpace(roomProfile.RoomId))
        {
            return roomProfile.RoomId;
        }

        RoomContentGroup parentRoom = GetComponentInParent<RoomContentGroup>(true);
        return parentRoom != null && !string.IsNullOrWhiteSpace(parentRoom.RoomName)
            ? parentRoom.RoomName
            : string.Empty;
    }

    private void ClearRoomStageScaleReference()
    {
        currentRoomStageScaleMultiplier = 1f;
        hasRoomStageScaleReference = false;
        roomStageScaleReference = 1f;
        roomStageScaleReferenceRoom = string.Empty;
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
}
