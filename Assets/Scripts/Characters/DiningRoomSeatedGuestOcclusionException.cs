using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Dining Room Seated Guest Occlusion Exception")]
public sealed class DiningRoomSeatedGuestOcclusionException : MonoBehaviour
{
    private const string InvalidOrderMessage = "Dining seat occlusion order invalid. Move chair/table sort anchors or split chair art.";

    [SerializeField] private ActorRoomState actorState;
    [SerializeField] private string diningRoomName = "Dining Room";
    [SerializeField] private string butlerExclusionObjectName = "Butler";
    [SerializeField] private RoomAnchor assignedSeat;
    [SerializeField] private GameObject assignedChair;
    [SerializeField] private SpriteRenderer assignedChairRenderer;
    [SerializeField] private SpriteRenderer diningTableRenderer;

    private RoomProjectedEntity roomProjection;
    private SortingGroup sortingGroup;
    private bool createdSortingGroup;
    private bool capturedOriginalSortingGroupState;
    private bool originalSortingGroupEnabled;
    private string originalSortingLayerName;
    private int originalSortingOrder;
    private bool appliedException;
    private bool loggedInvalidOrder;

    public bool IsExceptionActive => appliedException;
    public RoomAnchor AssignedSeat => assignedSeat;
    public GameObject AssignedChair => assignedChair;

    private void Awake()
    {
        ResolveActorState();
        ResolveProjection();
    }

    private void OnEnable()
    {
        ResolveActorState();
        ResolveProjection();
        ApplyOrRestore();
    }

    private void LateUpdate()
    {
        ApplyOrRestore();
    }

    private void OnDisable()
    {
        RestoreNormalSorting();
    }

    public void ActivateForDiningSeat(
        ActorRoomState targetActorState,
        RoomAnchor seatAnchor,
        GameObject chairObject,
        SpriteRenderer chairRenderer,
        SpriteRenderer tableRenderer,
        string targetDiningRoomName,
        string targetButlerExclusionObjectName)
    {
        actorState = targetActorState != null ? targetActorState : actorState;
        assignedSeat = seatAnchor;
        assignedChair = chairObject;
        assignedChairRenderer = chairRenderer;
        diningTableRenderer = tableRenderer;
        diningRoomName = string.IsNullOrWhiteSpace(targetDiningRoomName) ? "Dining Room" : targetDiningRoomName.Trim();
        butlerExclusionObjectName = string.IsNullOrWhiteSpace(targetButlerExclusionObjectName)
            ? "Butler"
            : targetButlerExclusionObjectName.Trim();
        loggedInvalidOrder = false;
        ResolveProjection();
        ApplyOrRestore();
    }

    public void DeactivateForDiningSeat()
    {
        assignedSeat = null;
        assignedChair = null;
        assignedChairRenderer = null;
        diningTableRenderer = null;
        loggedInvalidOrder = false;
        RestoreNormalSorting();
    }

    private void ApplyOrRestore()
    {
        if (!ShouldApplyException())
        {
            RestoreNormalSorting();
            return;
        }

        int tableOrder = diningTableRenderer.sortingOrder;
        int chairOrder = assignedChairRenderer.sortingOrder;
        int guestOrder = tableOrder - 1;

        if (guestOrder <= chairOrder)
        {
            if (!loggedInvalidOrder)
            {
                Debug.LogError(
                    $"{InvalidOrderMessage} seat={assignedSeat.name} chair={assignedChair.name} " +
                    $"chairOrder={chairOrder} guestOrder={guestOrder} tableOrder={tableOrder}",
                    this);
                loggedInvalidOrder = true;
            }

            RestoreNormalSorting();
            return;
        }

        SortingGroup targetGroup = EnsureSortingGroup();

        if (targetGroup == null)
        {
            RestoreNormalSorting();
            return;
        }

        ResolveProjection();
        roomProjection?.SetProjectedSortingSuppressed(true);

        targetGroup.enabled = true;
        targetGroup.sortingLayerName = diningTableRenderer.sortingLayerName;
        targetGroup.sortingOrder = guestOrder;
        appliedException = true;
    }

    private bool ShouldApplyException()
    {
        ResolveActorState();

        if (actorState == null ||
            assignedSeat == null ||
            assignedChair == null ||
            assignedChairRenderer == null ||
            diningTableRenderer == null)
        {
            return false;
        }

        if (IsButlerActor(actorState))
        {
            return false;
        }

        return actorState.IsSeated &&
            actorState.IsVisibleInCurrentRoom &&
            SameRoom(actorState.CurrentRoomId, diningRoomName) &&
            SameRoom(assignedSeat.RoomId, diningRoomName);
    }

    private SortingGroup EnsureSortingGroup()
    {
        if (sortingGroup != null)
        {
            CaptureOriginalSortingGroupStateIfNeeded();
            return sortingGroup;
        }

        ResolveProjection();
        Transform groupRoot = roomProjection != null && roomProjection.VisualRoot != null
            ? roomProjection.VisualRoot
            : actorState != null
                ? actorState.transform
                : transform;

        sortingGroup = groupRoot.GetComponent<SortingGroup>();

        if (sortingGroup == null)
        {
            sortingGroup = groupRoot.gameObject.AddComponent<SortingGroup>();
            createdSortingGroup = true;
        }

        CaptureOriginalSortingGroupStateIfNeeded();
        return sortingGroup;
    }

    private void CaptureOriginalSortingGroupStateIfNeeded()
    {
        if (capturedOriginalSortingGroupState || sortingGroup == null)
        {
            return;
        }

        originalSortingGroupEnabled = sortingGroup.enabled;
        originalSortingLayerName = sortingGroup.sortingLayerName;
        originalSortingOrder = sortingGroup.sortingOrder;
        capturedOriginalSortingGroupState = true;
    }

    private void RestoreNormalSorting()
    {
        if (!appliedException &&
            (sortingGroup == null || !createdSortingGroup || !sortingGroup.enabled))
        {
            return;
        }

        ResolveProjection();
        roomProjection?.SetProjectedSortingSuppressed(false);

        if (sortingGroup != null)
        {
            if (createdSortingGroup)
            {
                sortingGroup.enabled = false;
            }
            else if (capturedOriginalSortingGroupState)
            {
                sortingGroup.enabled = originalSortingGroupEnabled;
                sortingGroup.sortingLayerName = originalSortingLayerName;
                sortingGroup.sortingOrder = originalSortingOrder;
            }
        }

        appliedException = false;
    }

    private void ResolveActorState()
    {
        if (actorState == null)
        {
            actorState = GetComponent<ActorRoomState>() ?? GetComponentInParent<ActorRoomState>();
        }
    }

    private void ResolveProjection()
    {
        if (roomProjection != null)
        {
            return;
        }

        if (actorState != null && actorState.Projection != null)
        {
            roomProjection = actorState.Projection;
            return;
        }

        roomProjection = GetComponentInChildren<RoomProjectedEntity>(true) ??
            GetComponentInParent<RoomProjectedEntity>(true);
    }

    private bool IsButlerActor(ActorRoomState targetActorState)
    {
        if (targetActorState == null)
        {
            return false;
        }

        return ContainsOrdinalIgnoreCase(targetActorState.ActorId, butlerExclusionObjectName) ||
            ContainsOrdinalIgnoreCase(targetActorState.name, butlerExclusionObjectName) ||
            ContainsOrdinalIgnoreCase(gameObject.name, butlerExclusionObjectName);
    }

    private static bool SameRoom(string left, string right)
    {
        return string.Equals(Clean(left), Clean(right), System.StringComparison.OrdinalIgnoreCase);
    }

    private static string Clean(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static bool ContainsOrdinalIgnoreCase(string value, string fragment)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            !string.IsNullOrWhiteSpace(fragment) &&
            value.IndexOf(fragment, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
