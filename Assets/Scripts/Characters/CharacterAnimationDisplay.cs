using UnityEngine;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Character Animation Display")]
public sealed class CharacterAnimationDisplay : MonoBehaviour
{
    [Header("Display Animations")]
    [Tooltip("Dedicated visual child containing the Animator and character renderers. The actor/movement root is never scaled.")]
    [SerializeField] private Transform animationDisplay;

    [Header("Character Scale Catalog")]
    [SerializeField] private CharacterScaleCatalog catalog;
    [SerializeField] private ActorRoomState actorRoomState;
    [SerializeField] private PointClickPlayerMovement pointClickMovement;
    [SerializeField] private RoomNavigationManager navigationManager;

    private string lastConfigurationWarning;
    private CharacterScaleRoom resolvedScaleRoom;
    private string resolvedScaleRoomKey;

    public Transform AnimationDisplay => animationDisplay;
    public CharacterScaleCatalog Catalog => catalog;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void LateUpdate()
    {
        TryApplyCurrentRoomScale();
    }

    public void Configure(Transform displayRoot, CharacterScaleCatalog scaleCatalog = null)
    {
        animationDisplay = displayRoot;

        if (scaleCatalog != null)
        {
            catalog = scaleCatalog;
        }

        ResolveReferences();
    }

    public bool TryApplyCurrentRoomScale()
    {
        ResolveReferences();
        string roomName = ResolveRoomName();
        return TryApplyScaleForRoom(roomName);
    }

    public bool TryApplyScaleForRoom(string roomName)
    {
        // This can be called synchronously as a room-stage binding is created,
        // before this component's next LateUpdate. Refresh the actor state so
        // the authored bound foot point is available for the first scale pass.
        ResolveReferences();

        if (!HasValidDisplayRoot())
        {
            WarnOnce("CharacterAnimationDisplay requires a dedicated child AnimationDisplay; the actor root will not be scaled.");
            return false;
        }

        if (catalog == null || !TryEvaluateScale(roomName, out float scale))
        {
            return false;
        }

        Vector3 requestedScale = new Vector3(scale, scale, 1f);

        if (animationDisplay.localScale != requestedScale)
        {
            // This is the sole Butler/guest body-size write in runtime code.
            // animationDisplay is a visual-only child, never the movement root.
            animationDisplay.localScale = requestedScale;
        }

        lastConfigurationWarning = string.Empty;
        return true;
    }

    public bool HasValidDisplayRoot()
    {
        return animationDisplay != null &&
            animationDisplay != transform &&
            animationDisplay.IsChildOf(transform);
    }

    private bool TryEvaluateScale(string roomName, out float scale)
    {
        scale = 1f;

        if (!TryResolveScaleRoom(roomName, out CharacterScaleRoom scaleRoom))
        {
            return false;
        }

        Vector3 footWorldPosition = ResolveFootWorldPosition();

        return scaleRoom.TryGetCharacterRoomY(footWorldPosition, out float roomY) &&
            catalog.TryEvaluateScaleAtRoomY(
                roomName,
                roomY,
                scaleRoom.CurrentStageScale,
                out scale);
    }

    private bool TryResolveScaleRoom(string roomName, out CharacterScaleRoom scaleRoom)
    {
        string requestedKey = CharacterScaleCatalog.NormalizeRoomName(roomName);

        if (resolvedScaleRoom != null &&
            string.Equals(resolvedScaleRoomKey, requestedKey, System.StringComparison.Ordinal))
        {
            scaleRoom = resolvedScaleRoom;
            return true;
        }

        resolvedScaleRoom = null;
        resolvedScaleRoomKey = string.Empty;

        if (string.IsNullOrEmpty(requestedKey))
        {
            scaleRoom = null;
            return false;
        }

        CharacterScaleRoom[] roomStages = FindObjectsByType<CharacterScaleRoom>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < roomStages.Length; i++)
        {
            CharacterScaleRoom candidate = roomStages[i];

            if (candidate != null &&
                CharacterScaleCatalog.NormalizeRoomName(candidate.RoomName) == requestedKey)
            {
                resolvedScaleRoom = candidate;
                resolvedScaleRoomKey = requestedKey;
                scaleRoom = candidate;
                return true;
            }
        }

        scaleRoom = null;
        return false;
    }

    private Vector3 ResolveFootWorldPosition()
    {
        // PointClickPlayerMovement's logical position is the Butler's visible-foot
        // point; the actor root is deliberately offset when a sprite pivot requires it.
        if (pointClickMovement != null &&
            pointClickMovement.TryGetWorldPointFromLogicalPosition(
                pointClickMovement.LogicalPosition,
                out Vector2 logicalFootWorldPoint))
        {
            return new Vector3(logicalFootWorldPoint.x, logicalFootWorldPoint.y, transform.position.z);
        }

        if (animationDisplay != null &&
            CharacterFootPositionUtility.TryGetWorldPoint(
                animationDisplay.gameObject,
                false,
                true,
                out Vector3 visibleFootWorldPoint))
        {
            return visibleFootWorldPoint;
        }

        return transform.position;
    }

    public static CharacterAnimationDisplay EnsureForActor(
        GameObject actorRoot,
        CharacterScaleCatalog scaleCatalog = null)
    {
        if (actorRoot == null)
        {
            return null;
        }

        CharacterAnimationDisplay display = actorRoot.GetComponent<CharacterAnimationDisplay>();

        if (display != null)
        {
            return display;
        }

        Transform displayRoot = actorRoot.transform.Find("AnimationDisplay");

        if (displayRoot == null)
        {
            SpriteRenderer renderer = actorRoot.GetComponentInChildren<SpriteRenderer>(true);

            if (renderer != null && renderer.transform != actorRoot.transform)
            {
                displayRoot = renderer.transform;
            }
        }

        if (displayRoot == null || displayRoot == actorRoot.transform)
        {
            Debug.LogError(
                $"Actor '{actorRoot.name}' needs a dedicated AnimationDisplay child before character scale can be enabled.",
                actorRoot);
            return null;
        }

        display = actorRoot.AddComponent<CharacterAnimationDisplay>();
        display.Configure(displayRoot, scaleCatalog != null ? scaleCatalog : CharacterScaleCatalog.LoadDefault());
        return display;
    }

    private void ResolveReferences()
    {
        if (animationDisplay == null)
        {
            animationDisplay = transform.Find("AnimationDisplay");
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

        if (catalog == null)
        {
            catalog = CharacterScaleCatalog.LoadDefault();
        }
    }

    private string ResolveRoomName()
    {
        if (actorRoomState != null && !string.IsNullOrWhiteSpace(actorRoomState.CurrentRoomId))
        {
            return actorRoomState.CurrentRoomId;
        }

        RoomContentGroup parentRoom = GetComponentInParent<RoomContentGroup>(true);

        if (parentRoom != null)
        {
            return parentRoom.RoomName;
        }

        return navigationManager != null ? navigationManager.CurrentRoom : string.Empty;
    }

    private void WarnOnce(string warning)
    {
        if (string.Equals(lastConfigurationWarning, warning, System.StringComparison.Ordinal))
        {
            return;
        }

        lastConfigurationWarning = warning;
        Debug.LogError(warning, this);
    }
}
