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

    private RectTransform rectTransform;
    private SpriteRenderer[] spriteRenderers = Array.Empty<SpriteRenderer>();
    private Graphic[] graphics = Array.Empty<Graphic>();
    private Color[] spriteRendererBaseColors = Array.Empty<Color>();
    private Color[] graphicBaseColors = Array.Empty<Color>();
    private Transform cachedVisualRoot;
    private ActorRoomState actorRoomState;
    private Vector3 authoredVisualRootScale = Vector3.one;
    private bool hasAuthoredVisualRootScale;
    private float currentScale = 1f;
    private int currentSortingOrder;

    public RoomPerspectiveProfile RoomProfile => roomProfile;
    public CharacterVisualProfile VisualProfile => visualProfile;
    public ProjectionMode Mode => projectionMode;
    public Vector2 RoomLocalFootPoint => roomLocalFootPoint;
    public Transform VisualRoot => visualRoot != null ? visualRoot : transform;
    public bool HasUsableProfile => roomProfile != null;
    public bool IsProjectionActive => ShouldApplyProjection();
    public float CurrentScale => currentScale;
    public int CurrentSortingOrder => currentSortingOrder;

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

    public void SetRoomProfile(RoomPerspectiveProfile profile)
    {
        roomProfile = profile;
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

        if (!ShouldApplyProjection())
        {
            return;
        }

        currentScale = GetProjectedScale();
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
        float profileScale = roomProfile != null ? roomProfile.GetScale(roomLocalFootPoint) : 1f;
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

        if (!force &&
            hasAuthoredVisualRootScale &&
            cachedVisualRoot == targetRoot)
        {
            return;
        }

        cachedVisualRoot = targetRoot;
        authoredVisualRootScale = targetRoot != null ? targetRoot.localScale : Vector3.one;
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

        Vector3 baseScale = hasAuthoredVisualRootScale ? authoredVisualRootScale : Vector3.one;
        Vector3 projectedScale = new Vector3(
            baseScale.x * currentScale,
            baseScale.y * currentScale,
            baseScale.z);

        if (targetRoot == transform && normalizeLogicalRootScale)
        {
            projectedScale = new Vector3(
                logicalRootScale.x * currentScale,
                logicalRootScale.y * currentScale,
                logicalRootScale.z);
        }

        targetRoot.localScale = projectedScale;
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

        float shadowScale = roomProfile.GetShadowScale(roomLocalFootPoint);
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
