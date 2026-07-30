using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(21000)]
[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Dining Room Seated Guest Occlusion Exception")]
public sealed class DiningRoomSeatedGuestOcclusionException : MonoBehaviour
{
    private struct RendererSortingState
    {
        public int LayerId;
        public int Order;
        public SpriteSortPoint SortPoint;
    }

    private const string InvalidOrderMessage = "Dining seat occlusion order invalid. Move chair/table sort anchors or split chair art.";

    [SerializeField] private ActorRoomState actorState;
    [SerializeField] private string diningRoomName = "Dining Room";
    [SerializeField] private string butlerExclusionObjectName = "Butler";
    [SerializeField] private RoomAnchor assignedSeat;
    [SerializeField] private GameObject assignedChair;
    [SerializeField] private SpriteRenderer assignedChairRenderer;
    [SerializeField] private SpriteRenderer frontOccluderRenderer;
    [SerializeField] private SpriteRenderer diningTableRenderer;

    private bool frontOccluderOnly;
    private bool actorBehindOccluder;
    private SortingGroup sortingGroup;
    private bool createdSortingGroup;
    private bool capturedOriginalSortingGroupState;
    private bool originalSortingGroupEnabled;
    private string originalSortingLayerName;
    private int originalSortingOrder;
    private readonly Dictionary<SpriteRenderer, RendererSortingState> actorRendererStates =
        new Dictionary<SpriteRenderer, RendererSortingState>();
    private readonly List<SpriteRenderer> activeActorRenderers = new List<SpriteRenderer>();
    private bool capturedFrontOccluderState;
    private int originalFrontOccluderLayerId;
    private int originalFrontOccluderOrder;
    private SpriteSortPoint originalFrontOccluderSortPoint;
    private bool appliedException;
    private bool loggedInvalidOrder;

    public bool IsExceptionActive => appliedException;
    public RoomAnchor AssignedSeat => assignedSeat;
    public GameObject AssignedChair => assignedChair;
    public SpriteRenderer FrontOccluderRenderer => frontOccluderRenderer;

    private void Awake()
    {
        ResolveActorState();
    }

    private void OnEnable()
    {
        ResolveActorState();
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
        ActivateForSeat(
            targetActorState,
            seatAnchor,
            chairObject,
            chairRenderer,
            null,
            tableRenderer,
            targetDiningRoomName,
            targetButlerExclusionObjectName);
    }

    public void ActivateForDiningSeat(
        ActorRoomState targetActorState,
        RoomAnchor seatAnchor,
        GameObject chairObject,
        SpriteRenderer chairRenderer,
        SpriteRenderer targetFrontOccluderRenderer,
        SpriteRenderer tableRenderer,
        string targetDiningRoomName,
        string targetButlerExclusionObjectName)
    {
        ActivateForSeat(
            targetActorState,
            seatAnchor,
            chairObject,
            chairRenderer,
            targetFrontOccluderRenderer,
            tableRenderer,
            targetDiningRoomName,
            targetButlerExclusionObjectName);
    }

    public void ActivateForSeat(
        ActorRoomState targetActorState,
        RoomAnchor seatAnchor,
        GameObject chairObject,
        SpriteRenderer chairRenderer,
        SpriteRenderer tableRenderer,
        string targetRoomName,
        string targetButlerExclusionObjectName)
    {
        ActivateForSeat(
            targetActorState,
            seatAnchor,
            chairObject,
            chairRenderer,
            null,
            tableRenderer,
            targetRoomName,
            targetButlerExclusionObjectName);
    }

    public void ActivateForSeat(
        ActorRoomState targetActorState,
        RoomAnchor seatAnchor,
        GameObject chairObject,
        SpriteRenderer chairRenderer,
        SpriteRenderer targetFrontOccluderRenderer,
        SpriteRenderer tableRenderer,
        string targetRoomName,
        string targetButlerExclusionObjectName)
    {
        RestoreNormalSorting();
        actorState = targetActorState != null ? targetActorState : actorState;
        assignedSeat = seatAnchor;
        assignedChair = chairObject;
        assignedChairRenderer = chairRenderer;
        frontOccluderRenderer = targetFrontOccluderRenderer;
        diningTableRenderer = tableRenderer;
        frontOccluderOnly = false;
        actorBehindOccluder = false;
        diningRoomName = string.IsNullOrWhiteSpace(targetRoomName) ? "Dining Room" : targetRoomName.Trim();
        butlerExclusionObjectName = string.IsNullOrWhiteSpace(targetButlerExclusionObjectName)
            ? "Butler"
            : targetButlerExclusionObjectName.Trim();
        loggedInvalidOrder = false;
        ApplyOrRestore();
    }

    public void ActivateFrontOccluderOnly(
        ActorRoomState targetActorState,
        RoomAnchor seatAnchor,
        SpriteRenderer targetFrontOccluderRenderer,
        string targetRoomName,
        string targetButlerExclusionObjectName)
    {
        RestoreNormalSorting();
        actorState = targetActorState != null ? targetActorState : actorState;
        assignedSeat = seatAnchor;
        assignedChair = null;
        assignedChairRenderer = null;
        frontOccluderRenderer = targetFrontOccluderRenderer;
        diningTableRenderer = null;
        frontOccluderOnly = true;
        actorBehindOccluder = false;
        diningRoomName = string.IsNullOrWhiteSpace(targetRoomName) ? "Drawing Room" : targetRoomName.Trim();
        butlerExclusionObjectName = string.IsNullOrWhiteSpace(targetButlerExclusionObjectName)
            ? "Butler"
            : targetButlerExclusionObjectName.Trim();
        loggedInvalidOrder = false;
        ApplyOrRestore();
    }

    public void ActivateBehindOccluder(
        ActorRoomState targetActorState,
        RoomAnchor seatAnchor,
        SpriteRenderer targetOccluderRenderer,
        SpriteRenderer targetFrontOccluderRenderer,
        string targetRoomName,
        string targetButlerExclusionObjectName)
    {
        RestoreNormalSorting();
        actorState = targetActorState != null ? targetActorState : actorState;
        assignedSeat = seatAnchor;
        assignedChair = targetOccluderRenderer != null ? targetOccluderRenderer.gameObject : null;
        assignedChairRenderer = targetOccluderRenderer;
        frontOccluderRenderer = targetFrontOccluderRenderer;
        diningTableRenderer = null;
        frontOccluderOnly = false;
        actorBehindOccluder = true;
        diningRoomName = string.IsNullOrWhiteSpace(targetRoomName) ? "Drawing Room" : targetRoomName.Trim();
        butlerExclusionObjectName = string.IsNullOrWhiteSpace(targetButlerExclusionObjectName)
            ? "Butler"
            : targetButlerExclusionObjectName.Trim();
        loggedInvalidOrder = false;
        ApplyOrRestore();
    }

    public void DeactivateForDiningSeat()
    {
        DeactivateForSeat();
    }

    public void DeactivateForSeat()
    {
        RestoreNormalSorting();
        assignedSeat = null;
        assignedChair = null;
        assignedChairRenderer = null;
        frontOccluderRenderer = null;
        diningTableRenderer = null;
        frontOccluderOnly = false;
        actorBehindOccluder = false;
        loggedInvalidOrder = false;
    }

    public void ApplyOcclusionNow()
    {
        ApplyOrRestore();
    }

    private void ApplyOrRestore()
    {
        if (!ShouldApplyException())
        {
            RestoreNormalSorting();
            return;
        }

        if (actorBehindOccluder)
        {
            ApplyActorBehindOccluder();
            return;
        }

        if (frontOccluderOnly)
        {
            ApplyFrontOccluderOnly();
            return;
        }

        int tableOrder = diningTableRenderer.sortingOrder;
        int chairOrder = assignedChairRenderer.sortingOrder;

        if (frontOccluderRenderer != null)
        {
            CaptureFrontOccluderStateIfNeeded();
            frontOccluderRenderer.sortingLayerName = diningTableRenderer.sortingLayerName;
            frontOccluderRenderer.sortingOrder = tableOrder;
        }

        int guestOrder = tableOrder - 1;

        if (guestOrder <= chairOrder)
        {
            if (!loggedInvalidOrder)
            {
                Debug.LogError(
                    $"{InvalidOrderMessage} seat={assignedSeat.name} chair={assignedChair.name} " +
                    $"chairOrder={chairOrder} guestOrder={guestOrder} " +
                    $"frontOrder={(frontOccluderRenderer != null ? frontOccluderRenderer.sortingOrder : tableOrder)} " +
                    $"tableOrder={tableOrder}",
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

        targetGroup.enabled = true;
        targetGroup.sortingLayerName = diningTableRenderer.sortingLayerName;
        targetGroup.sortingOrder = guestOrder;
        appliedException = true;
    }

    private bool ShouldApplyException()
    {
        ResolveActorState();

        if (actorState == null || assignedSeat == null)
        {
            return false;
        }

        if (IsButlerActor(actorState))
        {
            return false;
        }

        bool actorCanUseException = actorState.IsSeated &&
            actorState.IsVisibleInCurrentRoom &&
            SameRoom(actorState.CurrentRoomId, diningRoomName) &&
            SameRoom(assignedSeat.RoomId, diningRoomName);

        if (!actorCanUseException)
        {
            return false;
        }

        if (actorBehindOccluder)
        {
            return assignedChairRenderer != null;
        }

        return frontOccluderOnly
            ? frontOccluderRenderer != null
            : assignedChair != null && assignedChairRenderer != null && diningTableRenderer != null;
    }

    private void ApplyActorBehindOccluder()
    {
        DisableActorRootSortingGroupForDirectRendererOverride();

        SpriteRenderer[] actorRenderers = actorState.GetComponentsInChildren<SpriteRenderer>(true);
        activeActorRenderers.Clear();

        for (int i = 0; i < actorRenderers.Length; i++)
        {
            SpriteRenderer candidate = actorRenderers[i];

            if (candidate != null &&
                candidate.enabled &&
                candidate.gameObject.activeInHierarchy)
            {
                activeActorRenderers.Add(candidate);
            }
        }

        if (activeActorRenderers.Count == 0)
        {
            RestoreNormalSorting();
            return;
        }

        activeActorRenderers.Sort(CompareRendererBackToFront);

        int totalOrderSpan = 0;

        for (int i = 1; i < activeActorRenderers.Count; i++)
        {
            totalOrderSpan += GetNormalizedOrderStep(
                activeActorRenderers[i - 1],
                activeActorRenderers[i]);
        }

        int targetFrontmostActorOrder = assignedChairRenderer.sortingOrder - 1;
        int relativeOrder = -totalOrderSpan;

        for (int i = 0; i < activeActorRenderers.Count; i++)
        {
            SpriteRenderer actorRenderer = activeActorRenderers[i];
            int nextOrderStep = i + 1 < activeActorRenderers.Count
                ? GetNormalizedOrderStep(actorRenderer, activeActorRenderers[i + 1])
                : 0;

            CaptureActorRendererStateIfNeeded(actorRenderer);
            actorRenderer.sortingLayerID = assignedChairRenderer.sortingLayerID;
            actorRenderer.sortingOrder = targetFrontmostActorOrder + relativeOrder;
            actorRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
            relativeOrder += nextOrderStep;
        }

        if (frontOccluderRenderer != null)
        {
            CaptureFrontOccluderStateIfNeeded();
            frontOccluderRenderer.sortingLayerID = assignedChairRenderer.sortingLayerID;
            frontOccluderRenderer.sortingOrder = assignedChairRenderer.sortingOrder + 1;
            frontOccluderRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        }

        appliedException = true;
    }

    private void ApplyFrontOccluderOnly()
    {
        SpriteRenderer[] actorRenderers = actorState.GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer frontmostActorRenderer = null;

        for (int i = 0; i < actorRenderers.Length; i++)
        {
            SpriteRenderer candidate = actorRenderers[i];

            if (candidate != null &&
                candidate.enabled &&
                candidate.gameObject.activeInHierarchy &&
                IsRendererInFront(candidate, frontmostActorRenderer))
            {
                frontmostActorRenderer = candidate;
            }
        }

        if (frontmostActorRenderer == null)
        {
            RestoreNormalSorting();
            return;
        }

        CaptureFrontOccluderStateIfNeeded();
        frontOccluderRenderer.sortingLayerID = frontmostActorRenderer.sortingLayerID;
        frontOccluderRenderer.sortingOrder = frontmostActorRenderer.sortingOrder + 1;
        frontOccluderRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        appliedException = true;
    }

    private SortingGroup EnsureSortingGroup()
    {
        if (sortingGroup != null)
        {
            CaptureOriginalSortingGroupStateIfNeeded();
            return sortingGroup;
        }

        Transform groupRoot = actorState != null ? actorState.transform : transform;

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
            actorRendererStates.Count == 0 &&
            !capturedFrontOccluderState &&
            !capturedOriginalSortingGroupState)
        {
            return;
        }

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

        capturedOriginalSortingGroupState = false;
        RestoreActorRendererSorting();
        RestoreFrontOccluderSorting();
        appliedException = false;
    }

    private void CaptureActorRendererStateIfNeeded(SpriteRenderer actorRenderer)
    {
        if (actorRenderer == null || actorRendererStates.ContainsKey(actorRenderer))
        {
            return;
        }

        actorRendererStates.Add(
            actorRenderer,
            new RendererSortingState
            {
                LayerId = actorRenderer.sortingLayerID,
                Order = actorRenderer.sortingOrder,
                SortPoint = actorRenderer.spriteSortPoint
            });
    }

    private void RestoreActorRendererSorting()
    {
        foreach (KeyValuePair<SpriteRenderer, RendererSortingState> entry in actorRendererStates)
        {
            SpriteRenderer actorRenderer = entry.Key;

            if (actorRenderer == null)
            {
                continue;
            }

            actorRenderer.sortingLayerID = entry.Value.LayerId;
            actorRenderer.sortingOrder = entry.Value.Order;
            actorRenderer.spriteSortPoint = entry.Value.SortPoint;
        }

        actorRendererStates.Clear();
    }

    private void CaptureFrontOccluderStateIfNeeded()
    {
        if (capturedFrontOccluderState || frontOccluderRenderer == null)
        {
            return;
        }

        originalFrontOccluderLayerId = frontOccluderRenderer.sortingLayerID;
        originalFrontOccluderOrder = frontOccluderRenderer.sortingOrder;
        originalFrontOccluderSortPoint = frontOccluderRenderer.spriteSortPoint;
        capturedFrontOccluderState = true;
    }

    private void RestoreFrontOccluderSorting()
    {
        if (!capturedFrontOccluderState)
        {
            return;
        }

        if (frontOccluderRenderer != null)
        {
            frontOccluderRenderer.sortingLayerID = originalFrontOccluderLayerId;
            frontOccluderRenderer.sortingOrder = originalFrontOccluderOrder;
            frontOccluderRenderer.spriteSortPoint = originalFrontOccluderSortPoint;
        }

        capturedFrontOccluderState = false;
    }

    private void ResolveActorState()
    {
        if (actorState == null)
        {
            actorState = GetComponent<ActorRoomState>() ?? GetComponentInParent<ActorRoomState>();
        }
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

    private static bool IsRendererInFront(SpriteRenderer candidate, SpriteRenderer currentFrontmost)
    {
        if (candidate == null)
        {
            return false;
        }

        if (currentFrontmost == null)
        {
            return true;
        }

        int candidateLayerValue = SortingLayer.GetLayerValueFromID(candidate.sortingLayerID);
        int currentLayerValue = SortingLayer.GetLayerValueFromID(currentFrontmost.sortingLayerID);
        return candidateLayerValue > currentLayerValue ||
            (candidateLayerValue == currentLayerValue &&
            candidate.sortingOrder > currentFrontmost.sortingOrder);
    }

    private static int CompareRendererBackToFront(SpriteRenderer left, SpriteRenderer right)
    {
        int leftLayerValue = SortingLayer.GetLayerValueFromID(left.sortingLayerID);
        int rightLayerValue = SortingLayer.GetLayerValueFromID(right.sortingLayerID);
        int layerComparison = leftLayerValue.CompareTo(rightLayerValue);

        if (layerComparison != 0)
        {
            return layerComparison;
        }

        int orderComparison = left.sortingOrder.CompareTo(right.sortingOrder);
        return orderComparison != 0
            ? orderComparison
            : left.GetInstanceID().CompareTo(right.GetInstanceID());
    }

    private static int GetNormalizedOrderStep(SpriteRenderer behind, SpriteRenderer inFront)
    {
        return behind.sortingLayerID == inFront.sortingLayerID
            ? Mathf.Max(1, inFront.sortingOrder - behind.sortingOrder)
            : 1;
    }

    private void DisableActorRootSortingGroupForDirectRendererOverride()
    {
        if (sortingGroup == null)
        {
            sortingGroup = actorState.GetComponent<SortingGroup>();
        }

        if (sortingGroup == null)
        {
            return;
        }

        CaptureOriginalSortingGroupStateIfNeeded();
        sortingGroup.enabled = false;
    }

}
