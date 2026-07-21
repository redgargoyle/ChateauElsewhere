using System;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Display Scale/Character Display Scale Subject")]
public sealed class CharacterDisplayScaleSubject : MonoBehaviour, ICharacterDisplayScaleContext
{
    [Header("Display Scale Identity")]
    [SerializeField] private CharacterDisplayId characterId;
    [Tooltip("Dedicated visual/animation child. The actor gameplay root is never a valid scale target.")]
    [SerializeField] private Transform visualScaleRoot;
    [Tooltip("Optional custom context adapter. When empty, this subject reads existing Butler/Guest state directly.")]
    [SerializeField] private MonoBehaviour contextSource;

    [Header("Read-Only Runtime Context")]
    [SerializeField] private ActorRoomState actorRoomState;
    [SerializeField] private PointClickPlayerMovement pointClickMovement;
    [SerializeField] private RoomNavigationManager navigationManager;
    [SerializeField] private CharacterFloorReference floorReference;

    private RoomContentGroup resolvedRoom;
    private string resolvedRoomKey;
    private bool hasAuthoredBasis;
    private float authoredXSign = 1f;
    private float authoredYSign = 1f;
    private float authoredZ = 1f;

    public CharacterDisplayId CharacterId => characterId;
    public Transform VisualScaleRoot => visualScaleRoot;
    public string CurrentRoomId => ResolveRoomId();
    public float CurrentRoomLocalFootY =>
        TryGetCurrentRoomLocalFootPosition(out Vector2 roomLocalFootPosition)
            ? roomLocalFootPosition.y
            : float.NaN;
    public CharacterDisplayState CurrentDisplayState => ResolveDisplayState();

    private void Awake()
    {
        ResolveReferences();
        CaptureAuthoredBasis();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CaptureAuthoredBasis();

        if (Application.isPlaying && CharacterDisplayScaleController.ActiveController == null)
        {
            CharacterDisplayScaleBootstrap.EnsureControllerForLoadedSubjects();
        }

        CharacterDisplayScaleController.Register(this);
    }

    private void OnDisable()
    {
        CharacterDisplayScaleController.Unregister(this);
    }

    private void OnDestroy()
    {
        CharacterDisplayScaleController.Unregister(this);
    }

    private void Reset()
    {
        ResolveReferences();
        InferCharacterId();
        CaptureAuthoredBasis(true);
    }

    private void OnValidate()
    {
        ResolveReferences();
        CaptureAuthoredBasis(true);
    }

    public bool HasValidVisualScaleRoot()
    {
        return visualScaleRoot != null &&
            visualScaleRoot != transform &&
            visualScaleRoot.IsChildOf(transform);
    }

    public bool TryGetContext(out ICharacterDisplayScaleContext context)
    {
        // Chapter setup adds Guest room/floor components after inherited prefab
        // components have enabled, so read-only dependencies are refreshed lazily.
        ResolveReferences();

        if (contextSource != null)
        {
            context = contextSource as ICharacterDisplayScaleContext;

            if (context != null)
            {
                return true;
            }
        }

        context = this;
        return true;
    }

    public bool TryGetCurrentRoomLocalFootPosition(out Vector2 roomLocalFootPosition)
    {
        roomLocalFootPosition = Vector2.zero;
        string roomId = ResolveRoomId();

        if (!TryResolveRoom(roomId, out RoomContentGroup room) ||
            !TryResolveFootWorldPosition(out Vector3 footWorldPosition))
        {
            return false;
        }

        return TryConvertWorldPointToRoomLocal(room, footWorldPosition, out roomLocalFootPosition);
    }

    public Vector3 GetDeterministicScaleVector(float targetScale)
    {
        CaptureAuthoredBasis();
        float safeScale = IsFinite(targetScale) && targetScale > 0f ? targetScale : 1f;
        return new Vector3(authoredXSign * safeScale, authoredYSign * safeScale, authoredZ);
    }

    public static CharacterDisplayScaleSubject EnsureForActor(
        GameObject actorRoot,
        CharacterDisplayId id)
    {
        if (actorRoot == null)
        {
            return null;
        }

        CharacterDisplayScaleSubject subject =
            actorRoot.GetComponent<CharacterDisplayScaleSubject>();

        if (subject == null)
        {
            subject = actorRoot.AddComponent<CharacterDisplayScaleSubject>();
        }

        subject.characterId = id;
        subject.ResolveReferences();
        subject.CaptureAuthoredBasis(true);

        if (!subject.HasValidVisualScaleRoot())
        {
            Debug.LogError(
                $"Actor '{actorRoot.name}' needs a dedicated visual AnimationDisplay child before display scaling can be enabled.",
                actorRoot);
        }

        if (subject.isActiveAndEnabled)
        {
            if (Application.isPlaying && CharacterDisplayScaleController.ActiveController == null)
            {
                CharacterDisplayScaleBootstrap.EnsureControllerForLoadedSubjects();
            }

            CharacterDisplayScaleController.Register(subject);
        }

        return subject;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        CharacterDisplayId id,
        Transform scaleRoot,
        MonoBehaviour customContextSource = null)
    {
        characterId = id;
        visualScaleRoot = scaleRoot;
        contextSource = customContextSource;
        ResolveReferences();
        CaptureAuthoredBasis(true);
    }
#endif

    private void ResolveReferences()
    {
        if (visualScaleRoot == null)
        {
            CharacterAnimationDisplay animationDisplay = GetComponent<CharacterAnimationDisplay>();
            visualScaleRoot = animationDisplay != null
                ? animationDisplay.AnimationDisplay
                : transform.Find("AnimationDisplay");
        }

        if (actorRoomState == null)
        {
            actorRoomState = GetComponent<ActorRoomState>();
        }

        if (pointClickMovement == null)
        {
            pointClickMovement = GetComponent<PointClickPlayerMovement>();
        }

        if (navigationManager == null)
        {
            navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
        }

        if (floorReference == null)
        {
            floorReference = GetComponent<CharacterFloorReference>();
        }
    }

    private void InferCharacterId()
    {
        string candidate = actorRoomState != null ? actorRoomState.ActorId : name;

        if (Enum.TryParse(candidate, true, out CharacterDisplayId parsed))
        {
            characterId = parsed;
        }
    }

    private string ResolveRoomId()
    {
        if (actorRoomState != null)
        {
            return actorRoomState.CurrentRoomId;
        }

        RoomContentGroup parentRoom = GetComponentInParent<RoomContentGroup>(true);

        if (parentRoom != null && !string.IsNullOrWhiteSpace(parentRoom.RoomName))
        {
            return parentRoom.RoomName;
        }

        return navigationManager != null ? navigationManager.CurrentRoom : string.Empty;
    }

    private CharacterDisplayState ResolveDisplayState()
    {
        if (actorRoomState == null || !actorRoomState.IsSeated)
        {
            return CharacterDisplayState.Normal;
        }

        string normalizedRoomId = CharacterDisplayScaleCatalog.NormalizeRoomId(ResolveRoomId());

        if (normalizedRoomId == CharacterDisplayScaleCatalog.NormalizeRoomId(
                CharacterDisplayScaleCatalog.DrawingRoomId))
        {
            return CharacterDisplayState.DrawingRoomSeated;
        }

        if (normalizedRoomId == CharacterDisplayScaleCatalog.NormalizeRoomId(
                CharacterDisplayScaleCatalog.DiningRoomId))
        {
            return CharacterDisplayState.DiningRoomSeated;
        }

        return CharacterDisplayState.Normal;
    }

    private bool TryResolveFootWorldPosition(out Vector3 footWorldPosition)
    {
        footWorldPosition = Vector3.zero;

        // Guest prefab instances inherit PointClickPlayerMovement, but their
        // disabled/stale Butler logical position is not their floor point.
        if (actorRoomState != null)
        {
            return floorReference != null && floorReference.TryGetWorldPoint(out footWorldPosition);
        }

        if (characterId == CharacterDisplayId.Butler &&
            pointClickMovement != null &&
            pointClickMovement.isActiveAndEnabled &&
            pointClickMovement.TryGetCurrentFloorWorldPointReadOnly(out Vector2 butlerFloorPoint))
        {
            footWorldPosition = new Vector3(butlerFloorPoint.x, butlerFloorPoint.y, transform.position.z);
            return true;
        }

        return false;
    }

    private bool TryResolveRoom(string roomId, out RoomContentGroup room)
    {
        string roomKey = CharacterDisplayScaleCatalog.NormalizeRoomId(roomId);

        if (resolvedRoom != null && string.Equals(resolvedRoomKey, roomKey, StringComparison.Ordinal))
        {
            room = resolvedRoom;
            return true;
        }

        resolvedRoom = null;
        resolvedRoomKey = string.Empty;

        if (string.IsNullOrEmpty(roomKey))
        {
            room = null;
            return false;
        }

        RoomContentGroup[] rooms = FindObjectsByType<RoomContentGroup>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < rooms.Length; i++)
        {
            RoomContentGroup candidate = rooms[i];

            if (candidate != null &&
                CharacterDisplayScaleCatalog.NormalizeRoomId(candidate.RoomName) == roomKey)
            {
                resolvedRoom = candidate;
                resolvedRoomKey = roomKey;
                room = candidate;
                return true;
            }
        }

        room = null;
        return false;
    }

    private static bool TryConvertWorldPointToRoomLocal(
        RoomContentGroup room,
        Vector3 worldPoint,
        out Vector2 roomLocalPoint)
    {
        roomLocalPoint = Vector2.zero;

        if (room == null)
        {
            return false;
        }

        RectTransform roomRect = room.transform as RectTransform;
        Canvas canvas = roomRect != null ? roomRect.GetComponentInParent<Canvas>() : null;
        Camera worldCamera = Camera.main;

        if (roomRect != null &&
            canvas != null &&
            worldCamera != null &&
            worldCamera.pixelWidth > 1 &&
            worldCamera.pixelHeight > 1)
        {
            Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldPoint);

            if (screenPoint.z <= 0f)
            {
                return false;
            }

            Camera canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera != null
                    ? canvas.worldCamera
                    : worldCamera;

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                roomRect,
                screenPoint,
                canvasCamera,
                out roomLocalPoint);
        }

        roomLocalPoint = room.transform.InverseTransformPoint(worldPoint);
        return true;
    }

    private void CaptureAuthoredBasis(bool force = false)
    {
        if (hasAuthoredBasis && !force)
        {
            return;
        }

        if (visualScaleRoot == null)
        {
            return;
        }

        Vector3 authoredScale = visualScaleRoot.localScale;
        authoredXSign = authoredScale.x < 0f ? -1f : 1f;
        authoredYSign = authoredScale.y < 0f ? -1f : 1f;
        authoredZ = IsFinite(authoredScale.z) && !Mathf.Approximately(authoredScale.z, 0f)
            ? authoredScale.z
            : 1f;
        hasAuthoredBasis = true;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
