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

    private struct SortingGroupState
    {
        public bool Enabled;
        public int LayerId;
        public int Order;
        public bool SortAtRoot;
    }

    private readonly struct EffectiveSortKey
    {
        public EffectiveSortKey(int layerValue, int order)
        {
            LayerValue = layerValue;
            Order = order;
        }

        public int LayerValue { get; }
        public int Order { get; }
    }

    private const string InvalidOrderMessage = "Dining seat occlusion order invalid. Move chair/table sort anchors or split chair art.";

    [SerializeField] private ActorRoomState actorState;
    [SerializeField] private ActorRoomState preservedBehindActorState;
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
    private readonly Dictionary<SortingGroup, SortingGroupState> actorSortingGroupStates =
        new Dictionary<SortingGroup, SortingGroupState>();
    private readonly List<SpriteRenderer> activeActorRenderers = new List<SpriteRenderer>();
    private readonly Dictionary<SpriteRenderer, RendererSortingState> preservedBehindRendererStates =
        new Dictionary<SpriteRenderer, RendererSortingState>();
    private readonly Dictionary<SortingGroup, SortingGroupState> preservedBehindSortingGroupStates =
        new Dictionary<SortingGroup, SortingGroupState>();
    private readonly List<SpriteRenderer> preservedBehindActiveRenderers = new List<SpriteRenderer>();
    private readonly List<EffectiveSortKey> leftEffectiveSortPath = new List<EffectiveSortKey>();
    private readonly List<EffectiveSortKey> rightEffectiveSortPath = new List<EffectiveSortKey>();
    private readonly List<SortingGroupState> leftSortingGroupChain = new List<SortingGroupState>();
    private readonly List<SortingGroupState> rightSortingGroupChain = new List<SortingGroupState>();
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

    private void OnDestroy()
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
        preservedBehindActorState = null;
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
        preservedBehindActorState = null;
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
        ActivateBehindOccluder(
            targetActorState,
            null,
            seatAnchor,
            targetOccluderRenderer,
            targetFrontOccluderRenderer,
            targetRoomName,
            targetButlerExclusionObjectName);
    }

    public void ActivateBehindOccluder(
        ActorRoomState targetActorState,
        ActorRoomState targetPreservedBehindActorState,
        RoomAnchor seatAnchor,
        SpriteRenderer targetOccluderRenderer,
        SpriteRenderer targetFrontOccluderRenderer,
        string targetRoomName,
        string targetButlerExclusionObjectName)
    {
        RestoreNormalSorting();
        actorState = targetActorState != null ? targetActorState : actorState;
        preservedBehindActorState = targetPreservedBehindActorState;
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
        preservedBehindActorState = null;
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
            ReleaseNormalSortingToWorldY();
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
        int targetFrontmostActorOrder = assignedChairRenderer.sortingOrder - 1;

        if (!ApplyActorImmediatelyBehindOrder(
            actorState,
            activeActorRenderers,
            actorRendererStates,
            actorSortingGroupStates,
            targetFrontmostActorOrder,
            out int targetBackmostActorOrder))
        {
            RestoreNormalSorting();
            return;
        }

        if (CanApplyToPreservedBehindActor())
        {
            if (!ApplyActorImmediatelyBehindOrder(
                preservedBehindActorState,
                preservedBehindActiveRenderers,
                preservedBehindRendererStates,
                preservedBehindSortingGroupStates,
                targetBackmostActorOrder - 1,
                out _))
            {
                ReleasePreservedBehindActorToWorldYSorting();
            }
        }
        else
        {
            ReleasePreservedBehindActorToWorldYSorting();
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

    private bool ApplyActorImmediatelyBehindOrder(
        ActorRoomState targetActorState,
        List<SpriteRenderer> targetActiveRenderers,
        Dictionary<SpriteRenderer, RendererSortingState> targetRendererStates,
        Dictionary<SortingGroup, SortingGroupState> targetSortingGroupStates,
        int targetFrontmostOrder,
        out int targetBackmostOrder)
    {
        targetBackmostOrder = targetFrontmostOrder;
        targetActiveRenderers.Clear();

        if (targetActorState == null)
        {
            return false;
        }

        SpriteRenderer[] actorRenderers =
            targetActorState.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < actorRenderers.Length; i++)
        {
            SpriteRenderer candidate = actorRenderers[i];

            if (candidate != null &&
                candidate.enabled &&
                candidate.gameObject.activeInHierarchy)
            {
                targetActiveRenderers.Add(candidate);
            }
        }

        if (targetActiveRenderers.Count == 0)
        {
            return false;
        }

        targetActiveRenderers.Sort(
            (left, right) => CompareRendererBackToFront(
                left,
                right,
                targetActorState,
                targetSortingGroupStates));
        CaptureAndDisableActorLocalSortingGroups(
            targetActorState,
            targetActiveRenderers,
            targetSortingGroupStates);

        targetBackmostOrder = targetFrontmostOrder - (targetActiveRenderers.Count - 1);

        for (int i = 0; i < targetActiveRenderers.Count; i++)
        {
            SpriteRenderer actorRenderer = targetActiveRenderers[i];

            CaptureActorRendererStateIfNeeded(actorRenderer, targetRendererStates);
            actorRenderer.sortingLayerID = assignedChairRenderer.sortingLayerID;
            actorRenderer.sortingOrder = targetBackmostOrder + i;
            actorRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        }

        return true;
    }

    private bool CanApplyToPreservedBehindActor()
    {
        return preservedBehindActorState != null &&
            preservedBehindActorState != actorState &&
            !IsButlerActor(preservedBehindActorState) &&
            preservedBehindActorState.IsSeated &&
            preservedBehindActorState.IsVisibleInCurrentRoom &&
            SameRoom(preservedBehindActorState.CurrentRoomId, diningRoomName);
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
            actorSortingGroupStates.Count == 0 &&
            preservedBehindRendererStates.Count == 0 &&
            preservedBehindSortingGroupStates.Count == 0 &&
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
        RestoreActorRendererSorting(actorRendererStates);
        RestoreActorSortingGroups(actorSortingGroupStates);
        RestorePreservedBehindActorSorting();
        RestoreFrontOccluderSorting();
        appliedException = false;
    }

    private void ReleaseNormalSortingToWorldY()
    {
        bool releasedActorState =
            actorRendererStates.Count > 0 ||
            actorSortingGroupStates.Count > 0;
        bool releasedPreservedBehindState =
            preservedBehindRendererStates.Count > 0 ||
            preservedBehindSortingGroupStates.Count > 0;
        RestoreNormalSorting();

        if (releasedActorState)
        {
            ReapplyWorldYSorting(actorState);
        }

        if (releasedPreservedBehindState)
        {
            ReapplyWorldYSorting(preservedBehindActorState);
        }
    }

    private static void CaptureActorRendererStateIfNeeded(
        SpriteRenderer actorRenderer,
        Dictionary<SpriteRenderer, RendererSortingState> rendererStates)
    {
        if (actorRenderer == null || rendererStates.ContainsKey(actorRenderer))
        {
            return;
        }

        rendererStates.Add(
            actorRenderer,
            new RendererSortingState
            {
                LayerId = actorRenderer.sortingLayerID,
                Order = actorRenderer.sortingOrder,
                SortPoint = actorRenderer.spriteSortPoint
            });
    }

    private static void RestoreActorRendererSorting(
        Dictionary<SpriteRenderer, RendererSortingState> rendererStates)
    {
        foreach (KeyValuePair<SpriteRenderer, RendererSortingState> entry in rendererStates)
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

        rendererStates.Clear();
    }

    private static void CaptureAndDisableActorLocalSortingGroups(
        ActorRoomState targetActorState,
        List<SpriteRenderer> targetActiveRenderers,
        Dictionary<SortingGroup, SortingGroupState> sortingGroupStates)
    {
        if (targetActorState == null)
        {
            return;
        }

        Transform actorRoot = targetActorState.transform;

        for (int rendererIndex = 0; rendererIndex < targetActiveRenderers.Count; rendererIndex++)
        {
            SpriteRenderer actorRenderer = targetActiveRenderers[rendererIndex];

            if (actorRenderer == null)
            {
                continue;
            }

            for (Transform cursor = actorRenderer.transform;
                cursor != null && (cursor == actorRoot || cursor.IsChildOf(actorRoot));
                cursor = cursor.parent)
            {
                SortingGroup[] groups = cursor.GetComponents<SortingGroup>();

                for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
                {
                    SortingGroup group = groups[groupIndex];

                    if (group == null)
                    {
                        continue;
                    }

                    if (sortingGroupStates.ContainsKey(group))
                    {
                        group.enabled = false;
                        continue;
                    }

                    if (!group.enabled)
                    {
                        continue;
                    }

                    sortingGroupStates.Add(
                        group,
                        new SortingGroupState
                        {
                            Enabled = true,
                            LayerId = group.sortingLayerID,
                            Order = group.sortingOrder,
                            SortAtRoot = group.sortAtRoot
                        });
                    group.enabled = false;
                }
            }
        }
    }

    private static void RestoreActorSortingGroups(
        Dictionary<SortingGroup, SortingGroupState> sortingGroupStates)
    {
        foreach (KeyValuePair<SortingGroup, SortingGroupState> entry in sortingGroupStates)
        {
            SortingGroup group = entry.Key;

            if (group == null)
            {
                continue;
            }

            SortingGroupState state = entry.Value;
            group.sortingLayerID = state.LayerId;
            group.sortingOrder = state.Order;
            group.sortAtRoot = state.SortAtRoot;
            group.enabled = state.Enabled;
        }

        sortingGroupStates.Clear();
    }

    private void RestorePreservedBehindActorSorting()
    {
        RestoreActorRendererSorting(preservedBehindRendererStates);
        RestoreActorSortingGroups(preservedBehindSortingGroupStates);
    }

    private void ReleasePreservedBehindActorToWorldYSorting()
    {
        bool releasedCapturedState =
            preservedBehindRendererStates.Count > 0 ||
            preservedBehindSortingGroupStates.Count > 0;
        RestorePreservedBehindActorSorting();

        if (!releasedCapturedState || preservedBehindActorState == null)
        {
            return;
        }

        ReapplyWorldYSorting(preservedBehindActorState);
    }

    private static void ReapplyWorldYSorting(ActorRoomState targetActorState)
    {
        WorldYSortSpriteRenderer worldYSorter = targetActorState != null
            ? targetActorState.GetComponent<WorldYSortSpriteRenderer>()
            : null;

        if (worldYSorter != null &&
            worldYSorter.isActiveAndEnabled &&
            worldYSorter.IsConfiguredForActor)
        {
            worldYSorter.ApplySorting();
        }
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

    private int CompareRendererBackToFront(
        SpriteRenderer left,
        SpriteRenderer right,
        ActorRoomState targetActorState,
        Dictionary<SortingGroup, SortingGroupState> sortingGroupStates)
    {
        BuildEffectiveSortPath(
            left,
            targetActorState,
            sortingGroupStates,
            leftEffectiveSortPath,
            leftSortingGroupChain);
        BuildEffectiveSortPath(
            right,
            targetActorState,
            sortingGroupStates,
            rightEffectiveSortPath,
            rightSortingGroupChain);
        int sharedLength = Mathf.Min(leftEffectiveSortPath.Count, rightEffectiveSortPath.Count);

        for (int i = 0; i < sharedLength; i++)
        {
            int layerComparison =
                leftEffectiveSortPath[i].LayerValue.CompareTo(rightEffectiveSortPath[i].LayerValue);

            if (layerComparison != 0)
            {
                return layerComparison;
            }

            int orderComparison =
                leftEffectiveSortPath[i].Order.CompareTo(rightEffectiveSortPath[i].Order);

            if (orderComparison != 0)
            {
                return orderComparison;
            }
        }

        int pathLengthComparison = leftEffectiveSortPath.Count.CompareTo(rightEffectiveSortPath.Count);
        return pathLengthComparison != 0
            ? pathLengthComparison
            : left.GetInstanceID().CompareTo(right.GetInstanceID());
    }

    private void BuildEffectiveSortPath(
        SpriteRenderer renderer,
        ActorRoomState targetActorState,
        Dictionary<SortingGroup, SortingGroupState> sortingGroupStates,
        List<EffectiveSortKey> destination,
        List<SortingGroupState> groupChain)
    {
        destination.Clear();
        groupChain.Clear();

        if (renderer == null || targetActorState == null)
        {
            return;
        }

        Transform actorRoot = targetActorState.transform;

        for (Transform cursor = renderer.transform;
            cursor != null && (cursor == actorRoot || cursor.IsChildOf(actorRoot));
            cursor = cursor.parent)
        {
            SortingGroup[] groups = cursor.GetComponents<SortingGroup>();

            for (int i = 0; i < groups.Length; i++)
            {
                SortingGroup group = groups[i];

                if (group == null)
                {
                    continue;
                }

                if (group.enabled)
                {
                    groupChain.Add(
                        new SortingGroupState
                        {
                            Enabled = true,
                            LayerId = group.sortingLayerID,
                            Order = group.sortingOrder,
                            SortAtRoot = group.sortAtRoot
                        });
                }
                else if (sortingGroupStates.TryGetValue(
                    group,
                    out SortingGroupState capturedState) &&
                    capturedState.Enabled)
                {
                    groupChain.Add(capturedState);
                }
            }
        }

        for (int i = groupChain.Count - 1; i >= 0; i--)
        {
            SortingGroupState group = groupChain[i];

            if (group.SortAtRoot)
            {
                destination.Clear();
            }

            destination.Add(
                new EffectiveSortKey(
                    SortingLayer.GetLayerValueFromID(group.LayerId),
                    group.Order));
        }

        destination.Add(
            new EffectiveSortKey(
                SortingLayer.GetLayerValueFromID(renderer.sortingLayerID),
                renderer.sortingOrder));
    }

}
