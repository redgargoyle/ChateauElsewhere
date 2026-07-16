using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public readonly struct GuestScaleApplyResult
{
    public GuestScaleApplyResult(int applied, int changed)
    {
        Applied = applied;
        Changed = changed;
    }

    public int Applied { get; }
    public int Changed { get; }
}

public readonly struct GuestScaleComputation
{
    public GuestScaleComputation(
        string roomId,
        float roomLocalY,
        float roomScale,
        float fineTune,
        float baseGuestScale,
        float roomStageZoomRatio,
        float inheritedRoomStageZoomRatio,
        float targetLocalScale,
        string roomScaleDiagnostic,
        string roomStageZoomDiagnostic,
        string inheritedRoomStageZoomDiagnostic)
    {
        RoomId = roomId;
        RoomLocalY = roomLocalY;
        RoomScale = roomScale;
        FineTune = fineTune;
        BaseGuestScale = baseGuestScale;
        RoomStageZoomRatio = roomStageZoomRatio;
        InheritedRoomStageZoomRatio = inheritedRoomStageZoomRatio;
        TargetLocalScale = targetLocalScale;
        RoomScaleDiagnostic = roomScaleDiagnostic;
        RoomStageZoomDiagnostic = roomStageZoomDiagnostic;
        InheritedRoomStageZoomDiagnostic = inheritedRoomStageZoomDiagnostic;
    }

    public string RoomId { get; }
    public float RoomLocalY { get; }
    public float RoomScale { get; }
    public float FineTune { get; }
    public float BaseGuestScale { get; }
    public float RoomStageZoomRatio { get; }
    public float InheritedRoomStageZoomRatio { get; }
    public float TargetLocalScale { get; }
    public string RoomScaleDiagnostic { get; }
    public string RoomStageZoomDiagnostic { get; }
    public string InheritedRoomStageZoomDiagnostic { get; }
}

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Guest Room Scale Applier")]
public sealed class GuestRoomScaleApplier : MonoBehaviour
{
    [SerializeField] private GuestRoomScaleCalibration calibration;
    [SerializeField] private bool includeInactiveParticipants = true;
    [SerializeField] private bool logScaleDiagnostics;

    private readonly List<GuestScaleParticipant> participants = new List<GuestScaleParticipant>();
    private readonly Dictionary<GuestScaleParticipant, RuntimeScaleSnapshot> runtimeSnapshots = new Dictionary<GuestScaleParticipant, RuntimeScaleSnapshot>();
    private bool runtimeParticipantCacheInitialized;
    private CameraManager runtimeCameraManager;
    private RoomNavigationManager runtimeNavigationManager;

    public GuestRoomScaleCalibration Calibration => calibration;
    public bool LogScaleDiagnostics
    {
        get => logScaleDiagnostics;
        set => logScaleDiagnostics = value;
    }

    private void OnEnable()
    {
        GuestScaleParticipant.RuntimeScaleStateChanged -= HandleRuntimeScaleStateChanged;
        GuestScaleParticipant.RuntimeScaleStateChanged += HandleRuntimeScaleStateChanged;
        runtimeParticipantCacheInitialized = false;
    }

    private void OnDisable()
    {
        GuestScaleParticipant.RuntimeScaleStateChanged -= HandleRuntimeScaleStateChanged;
    }

    private void LateUpdate()
    {
        if (Application.isPlaying)
        {
            RefreshRuntimeChangesNow();
        }
    }

    public void SetCalibration(GuestRoomScaleCalibration value)
    {
        calibration = value;
        runtimeSnapshots.Clear();
    }

    public static GuestRoomScaleApplier EnsureInScene()
    {
        GuestRoomScaleApplier existing = FindAnyObjectByType<GuestRoomScaleApplier>(FindObjectsInactive.Include);

        if (existing != null)
        {
            existing.ResolveCalibration();
            return existing;
        }

        GameObject applierObject = new GameObject("GuestRoomScaleApplier");
        GuestRoomScaleApplier created = applierObject.AddComponent<GuestRoomScaleApplier>();
        created.ResolveCalibration();
        return created;
    }

    public static GuestScaleParticipant EnsureParticipantForGuestObject(
        GameObject guestObject,
        string characterId = null,
        string roomId = null,
        CharacterPose pose = CharacterPose.Standing,
        bool roomIdIsCurrent = false)
    {
        if (guestObject == null)
        {
            return null;
        }

        GuestScaleParticipant participant = guestObject.GetComponent<GuestScaleParticipant>();

        if (participant == null)
        {
            participant = guestObject.AddComponent<GuestScaleParticipant>();
        }

        if (!string.IsNullOrWhiteSpace(characterId))
        {
            participant.SetCharacterId(characterId);
        }

        if (string.IsNullOrWhiteSpace(roomId) &&
            TryInferAuthoredSceneGuestRoomId(guestObject, out string inferredRoomId))
        {
            roomId = inferredRoomId;
        }

        if (!string.IsNullOrWhiteSpace(roomId))
        {
            if (roomIdIsCurrent)
            {
                participant.SetCurrentRoomId(roomId);
            }
            else
            {
                participant.SetRoomIdOverride(roomId);
            }
        }

        participant.SetPose(pose);
        participant.SetIsButler(false);
        participant.ResolveScaleRoot();
        participant.CaptureBaseScale(false);
        return participant;
    }

    public int RefreshAllNow()
    {
        return RefreshAllWithResultNow().Applied;
    }

    public GuestScaleApplyResult RefreshRuntimeChangesNow()
    {
        ResolveCalibration();

        if (calibration == null)
        {
            return new GuestScaleApplyResult(0, 0);
        }

        if (!runtimeParticipantCacheInitialized)
        {
            RefreshParticipantList();
        }

        ResolveRuntimeContext(
            out string navigationRoomId,
            out float roomStageScale,
            out int calibrationRevision,
            out int butlerScaleRevision);
        int applied = 0;
        int changed = 0;

        for (int i = participants.Count - 1; i >= 0; i--)
        {
            GuestScaleParticipant participant = participants[i];

            if (participant == null)
            {
                participants.RemoveAt(i);
                continue;
            }

            if (!participant.gameObject.activeInHierarchy)
            {
                continue;
            }

            RuntimeScaleSnapshot current = RuntimeScaleSnapshot.Capture(
                participant,
                navigationRoomId,
                roomStageScale,
                calibrationRevision,
                butlerScaleRevision);

            if (runtimeSnapshots.TryGetValue(participant, out RuntimeScaleSnapshot previous) && previous.Matches(current))
            {
                continue;
            }

            if (RefreshParticipantNow(participant, out bool participantChanged))
            {
                applied++;

                if (participantChanged)
                {
                    changed++;
                }
            }

            runtimeSnapshots[participant] = RuntimeScaleSnapshot.Capture(
                participant,
                navigationRoomId,
                roomStageScale,
                calibrationRevision,
                butlerScaleRevision);
        }

        return new GuestScaleApplyResult(applied, changed);
    }

    public GuestScaleApplyResult RefreshAllWithResultNow()
    {
        ResolveCalibration();

        if (calibration == null)
        {
            return new GuestScaleApplyResult(0, 0);
        }

        RefreshParticipantList();

        int applied = 0;
        int changed = 0;

        for (int i = 0; i < participants.Count; i++)
        {
            if (RefreshParticipantNow(participants[i], out bool participantChanged))
            {
                applied++;

                if (participantChanged)
                {
                    changed++;
                }
            }
        }

        return new GuestScaleApplyResult(applied, changed);
    }

    public GuestScaleApplyResult RefreshRoomNow(string roomId)
    {
        ResolveCalibration();

        if (calibration == null || string.IsNullOrWhiteSpace(roomId))
        {
            return new GuestScaleApplyResult(0, 0);
        }

        RefreshParticipantList();

        int applied = 0;
        int changed = 0;

        for (int i = 0; i < participants.Count; i++)
        {
            GuestScaleParticipant participant = participants[i];

            if (!ShouldApplyParticipantForRoomContext(participant, roomId))
            {
                continue;
            }

            if (RefreshParticipantNow(participant, out bool participantChanged, roomId))
            {
                applied++;

                if (participantChanged)
                {
                    changed++;
                }
            }
        }

        return new GuestScaleApplyResult(applied, changed);
    }

    public bool RefreshParticipantNow(GuestScaleParticipant participant)
    {
        return RefreshParticipantNow(participant, out _);
    }

    public bool RefreshParticipantNow(GuestScaleParticipant participant, out bool changed)
    {
        return RefreshParticipantNow(participant, out changed, string.Empty);
    }

    private bool RefreshParticipantNow(GuestScaleParticipant participant, out bool changed, string roomContext)
    {
        ResolveCalibration();

        if (!TryComputeParticipantScale(participant, roomContext, out GuestScaleComputation computation))
        {
            changed = false;
            return false;
        }

        changed = participant.ApplyFinalScale(computation.TargetLocalScale);

        if (logScaleDiagnostics)
        {
            Debug.Log(
                $"[GuestScale] {participant.CharacterId} room={computation.RoomId} base={computation.BaseGuestScale:0.####} zoomRatio={computation.RoomStageZoomRatio:0.####} inherited={computation.InheritedRoomStageZoomRatio:0.####} applied={computation.TargetLocalScale:0.####}",
                participant);
        }

        return true;
    }

    public bool TryComputeParticipantScale(GuestScaleParticipant participant, out GuestScaleComputation computation)
    {
        return TryComputeParticipantScale(participant, string.Empty, out computation);
    }

    public bool TryComputeParticipantScale(
        GuestScaleParticipant participant,
        string roomContext,
        out GuestScaleComputation computation)
    {
        ResolveCalibration();
        computation = default;

        if (participant == null ||
            IsGuestScaleInfrastructureObject(participant.gameObject) ||
            !IsManagedGuestParticipant(participant) ||
            participant.ExcludeFromGuestScaling ||
            participant.IsButler)
        {
            return false;
        }

        string roomId = string.IsNullOrWhiteSpace(roomContext)
            ? participant.ResolveRoomId()
            : participant.ResolveRoomIdForScaleContext(roomContext);
        float roomLocalY = participant.ResolveRoomLocalY(roomId);
        float roomScale = 1f;
        string roomScaleDiagnostic = "No guest room scale calibration.";

        if (calibration != null &&
            calibration.TryEvaluateGuestScale(roomId, roomLocalY, out float evaluatedScale, out _, out string evaluatedDiagnostic))
        {
            roomScale = evaluatedScale;
            roomScaleDiagnostic = evaluatedDiagnostic;
        }

        float fineTune = participant.ManualFineTuneMultiplier;
        float baseGuestScale =
            Mathf.Max(0.001f, roomScale) *
            Mathf.Max(0.001f, fineTune);

        float roomStageZoomRatio = 1f;
        string roomStageZoomDiagnostic = string.Empty;

        if (!GuestRoomStageScaleUtility.TryGetCurrentRoomStageZoomRatio(
            calibration,
            roomId,
            out roomStageZoomRatio,
            out roomStageZoomDiagnostic))
        {
            roomStageZoomRatio = 1f;
        }

        float inheritedRoomStageZoomRatio = 1f;
        string inheritedRoomStageZoomDiagnostic = string.Empty;

        if (!GuestRoomStageScaleUtility.TryGetInheritedRoomStageZoomRatio(
            participant,
            calibration,
            roomId,
            out inheritedRoomStageZoomRatio,
            out inheritedRoomStageZoomDiagnostic))
        {
            inheritedRoomStageZoomRatio = 1f;
        }

        float targetLocalScale = CalculateTargetLocalScale(
            baseGuestScale,
            roomStageZoomRatio,
            inheritedRoomStageZoomRatio);

        computation = new GuestScaleComputation(
            roomId,
            roomLocalY,
            roomScale,
            fineTune,
            baseGuestScale,
            roomStageZoomRatio,
            inheritedRoomStageZoomRatio,
            targetLocalScale,
            roomScaleDiagnostic,
            roomStageZoomDiagnostic,
            inheritedRoomStageZoomDiagnostic);
        return true;
    }

    public static bool ShouldApplyParticipantForRoomContext(
        GuestScaleParticipant participant,
        string roomId)
    {
        if (participant == null || string.IsNullOrWhiteSpace(roomId))
        {
            return false;
        }

        string resolvedRoomId = participant.ResolveRoomIdForScaleContext(roomId);
        return GuestRoomScaleCalibration.SameRoom(resolvedRoomId, roomId);
    }

    public static float CalculateTargetLocalScale(
        float baseGuestScale,
        float roomStageZoomRatio,
        float inheritedRoomStageZoomRatio)
    {
        return Mathf.Max(0.001f, baseGuestScale) *
            Mathf.Max(0.0001f, roomStageZoomRatio) /
            Mathf.Max(0.0001f, inheritedRoomStageZoomRatio);
    }

    public int EnsureParticipantsForSceneGuests()
    {
        int ensured = 0;
        HashSet<Transform> seenScaleRoots = new HashSet<Transform>();

        GameObject[] roots = FindObjectsByType<GameObject>(FindObjectsInactive.Include);

        for (int i = 0; i < roots.Length; i++)
        {
            GameObject candidate = roots[i];

            if (candidate == null ||
                IsGuestScaleInfrastructureObject(candidate) ||
                !LooksLikeChapterGuest(candidate.name))
            {
                continue;
            }

            GuestScaleParticipant participant = EnsureParticipantForGuestObject(candidate, candidate.name);
            Transform scaleRoot = participant != null ? participant.ResolveScaleRoot() : null;

            if (scaleRoot == null || seenScaleRoots.Contains(scaleRoot))
            {
                continue;
            }

            seenScaleRoots.Add(scaleRoot);
            ensured++;
        }

        RoomPersonWalker2D[] walkers = FindObjectsByType<RoomPersonWalker2D>(FindObjectsInactive.Include);

        for (int i = 0; i < walkers.Length; i++)
        {
            if (walkers[i] == null ||
                !LooksLikeChapterGuest(walkers[i].gameObject.name))
            {
                continue;
            }

            GuestScaleParticipant participant = EnsureParticipantForGuestObject(walkers[i].gameObject, walkers[i].name);
            Transform scaleRoot = participant != null ? participant.ResolveScaleRoot() : null;

            if (scaleRoot == null || seenScaleRoots.Contains(scaleRoot))
            {
                continue;
            }

            seenScaleRoots.Add(scaleRoot);
            ensured++;
        }

        RoomProjectedEntity[] projectedEntities = FindObjectsByType<RoomProjectedEntity>(FindObjectsInactive.Include);

        for (int i = 0; i < projectedEntities.Length; i++)
        {
            RoomProjectedEntity entity = projectedEntities[i];

            if (entity == null ||
                entity.Mode != RoomProjectedEntity.ProjectionMode.FloorCharacter ||
                !LooksLikeChapterGuest(entity.gameObject.name))
            {
                continue;
            }

            GuestScaleParticipant participant = EnsureParticipantForGuestObject(entity.gameObject, entity.name);
            Transform scaleRoot = participant != null ? participant.ResolveScaleRoot() : null;

            if (scaleRoot == null || seenScaleRoots.Contains(scaleRoot))
            {
                continue;
            }

            seenScaleRoots.Add(scaleRoot);
            ensured++;
        }

        return ensured;
    }

    public static bool TryInferAuthoredSceneGuestRoomId(GameObject guestObject, out string roomId)
    {
        roomId = string.Empty;

        if (guestObject == null)
        {
            return false;
        }

        if (TryResolveParentRoomId(guestObject, out roomId) ||
            TryResolveProjectedProfileRoomId(guestObject, out roomId) ||
            TryResolveWalkerRoomId(guestObject, out roomId) ||
            TryResolveActorRoomId(guestObject, out roomId) ||
            TryResolveActiveNavigationRoomId(guestObject, out roomId) ||
            TryResolveParticipantOverrideRoomId(guestObject, out roomId) ||
            TryInferChapterOneSceneGuestRoomId(guestObject.name, out roomId))
        {
            roomId = GuestRoomScaleCalibration.CleanRoomId(roomId);
            return !string.IsNullOrWhiteSpace(roomId);
        }

        return false;
    }

    public static bool TryInferChapterGuestNameRoomId(string guestName, out string roomId)
    {
        if (TryInferChapterOneSceneGuestRoomId(guestName, out roomId))
        {
            roomId = GuestRoomScaleCalibration.CleanRoomId(roomId);
            return !string.IsNullOrWhiteSpace(roomId);
        }

        return false;
    }

    private void ResolveCalibration()
    {
        if (calibration == null)
        {
            calibration = FindAnyObjectByType<GuestRoomScaleCalibration>(FindObjectsInactive.Include);
        }
    }

    private void RefreshParticipantList()
    {
        participants.Clear();
        FindObjectsInactive inactiveMode = includeInactiveParticipants
            ? FindObjectsInactive.Include
            : FindObjectsInactive.Exclude;
        participants.AddRange(FindObjectsByType<GuestScaleParticipant>(inactiveMode));
        runtimeParticipantCacheInitialized = true;
        PruneRuntimeSnapshots();
    }

    private void HandleRuntimeScaleStateChanged(GuestScaleParticipant participant)
    {
        if (participant == null)
        {
            runtimeParticipantCacheInitialized = false;
            return;
        }

        runtimeSnapshots.Remove(participant);

        if (!participants.Contains(participant))
        {
            participants.Add(participant);
        }
    }

    private void ResolveRuntimeContext(
        out string navigationRoomId,
        out float roomStageScale,
        out int calibrationRevision,
        out int butlerScaleRevision)
    {
        if (runtimeNavigationManager == null)
        {
            runtimeNavigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Exclude);
            runtimeNavigationManager ??= FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
        }

        navigationRoomId = runtimeNavigationManager != null
            ? runtimeNavigationManager.CurrentRoom ?? string.Empty
            : string.Empty;

        if (runtimeCameraManager == null)
        {
            runtimeCameraManager = FindAnyObjectByType<CameraManager>(FindObjectsInactive.Exclude);
            runtimeCameraManager ??= FindAnyObjectByType<CameraManager>(FindObjectsInactive.Include);
        }

        roomStageScale = 1f;

        if (runtimeCameraManager != null &&
            runtimeCameraManager.TryGetRoomStageScreenTransform(out _, out _, out float activeStageScale))
        {
            roomStageScale = GuestRoomStageScaleUtility.SanitizeRoomStageScale(activeStageScale);
        }

        calibrationRevision = calibration != null ? calibration.RuntimeScaleRevision : 0;
        PointClickPlayerMovement butlerScaleSource = calibration != null ? calibration.ButlerScaleSource : null;
        butlerScaleRevision = butlerScaleSource != null ? butlerScaleSource.ButlerScaleRevision : 0;
    }

    private void PruneRuntimeSnapshots()
    {
        if (runtimeSnapshots.Count == 0)
        {
            return;
        }

        List<GuestScaleParticipant> staleParticipants = null;

        foreach (KeyValuePair<GuestScaleParticipant, RuntimeScaleSnapshot> entry in runtimeSnapshots)
        {
            if (entry.Key != null && participants.Contains(entry.Key))
            {
                continue;
            }

            staleParticipants ??= new List<GuestScaleParticipant>();
            staleParticipants.Add(entry.Key);
        }

        if (staleParticipants == null)
        {
            return;
        }

        for (int i = 0; i < staleParticipants.Count; i++)
        {
            runtimeSnapshots.Remove(staleParticipants[i]);
        }
    }

    private readonly struct RuntimeScaleSnapshot
    {
        private const float VectorTolerance = 0.000001f;

        private RuntimeScaleSnapshot(
            Transform scaleRoot,
            Vector3 worldPosition,
            Vector3 localScale,
            Vector3 parentLossyScale,
            string currentRoomId,
            string roomIdOverride,
            CharacterPose pose,
            float fineTune,
            string navigationRoomId,
            float roomStageScale,
            int participantRevision,
            string resolvedRoomId,
            int calibrationRevision,
            int butlerScaleRevision)
        {
            ScaleRoot = scaleRoot;
            WorldPosition = worldPosition;
            LocalScale = localScale;
            ParentLossyScale = parentLossyScale;
            CurrentRoomId = currentRoomId;
            RoomIdOverride = roomIdOverride;
            Pose = pose;
            FineTune = fineTune;
            NavigationRoomId = navigationRoomId;
            RoomStageScale = roomStageScale;
            ParticipantRevision = participantRevision;
            ResolvedRoomId = resolvedRoomId;
            CalibrationRevision = calibrationRevision;
            ButlerScaleRevision = butlerScaleRevision;
        }

        private Transform ScaleRoot { get; }
        private Vector3 WorldPosition { get; }
        private Vector3 LocalScale { get; }
        private Vector3 ParentLossyScale { get; }
        private string CurrentRoomId { get; }
        private string RoomIdOverride { get; }
        private CharacterPose Pose { get; }
        private float FineTune { get; }
        private string NavigationRoomId { get; }
        private float RoomStageScale { get; }
        private int ParticipantRevision { get; }
        private string ResolvedRoomId { get; }
        private int CalibrationRevision { get; }
        private int ButlerScaleRevision { get; }

        public static RuntimeScaleSnapshot Capture(
            GuestScaleParticipant participant,
            string navigationRoomId,
            float roomStageScale,
            int calibrationRevision,
            int butlerScaleRevision)
        {
            Transform scaleRoot = participant != null ? participant.ResolveScaleRoot() : null;
            Transform parent = scaleRoot != null ? scaleRoot.parent : null;

            return new RuntimeScaleSnapshot(
                scaleRoot,
                scaleRoot != null ? scaleRoot.position : Vector3.zero,
                scaleRoot != null ? scaleRoot.localScale : Vector3.one,
                parent != null ? parent.lossyScale : Vector3.one,
                participant != null ? participant.CurrentRoomId : string.Empty,
                participant != null ? participant.RoomIdOverride : string.Empty,
                participant != null ? participant.Pose : CharacterPose.Auto,
                participant != null ? participant.ManualFineTuneMultiplier : 1f,
                navigationRoomId,
                roomStageScale,
                participant != null ? participant.RuntimeScaleRevision : 0,
                participant != null ? participant.ResolveRoomId() : string.Empty,
                calibrationRevision,
                butlerScaleRevision);
        }

        public bool Matches(RuntimeScaleSnapshot other)
        {
            return ScaleRoot == other.ScaleRoot &&
                (WorldPosition - other.WorldPosition).sqrMagnitude <= VectorTolerance &&
                (LocalScale - other.LocalScale).sqrMagnitude <= VectorTolerance &&
                (ParentLossyScale - other.ParentLossyScale).sqrMagnitude <= VectorTolerance &&
                string.Equals(CurrentRoomId, other.CurrentRoomId, System.StringComparison.Ordinal) &&
                string.Equals(RoomIdOverride, other.RoomIdOverride, System.StringComparison.Ordinal) &&
                Pose == other.Pose &&
                Mathf.Approximately(FineTune, other.FineTune) &&
                string.Equals(NavigationRoomId, other.NavigationRoomId, System.StringComparison.Ordinal) &&
                Mathf.Approximately(RoomStageScale, other.RoomStageScale) &&
                ParticipantRevision == other.ParticipantRevision &&
                string.Equals(ResolvedRoomId, other.ResolvedRoomId, System.StringComparison.Ordinal) &&
                CalibrationRevision == other.CalibrationRevision &&
                ButlerScaleRevision == other.ButlerScaleRevision;
        }
    }

    private static bool TryResolveParticipantOverrideRoomId(GameObject guestObject, out string roomId)
    {
        roomId = string.Empty;
        GuestScaleParticipant participant = guestObject.GetComponent<GuestScaleParticipant>();

        if (participant == null)
        {
            participant = guestObject.GetComponentInParent<GuestScaleParticipant>(true);
        }

        if (participant == null)
        {
            participant = guestObject.GetComponentInChildren<GuestScaleParticipant>(true);
        }

        if (participant == null || string.IsNullOrWhiteSpace(participant.RoomIdOverride))
        {
            return false;
        }

        roomId = participant.RoomIdOverride;
        return true;
    }

    private static bool TryResolveActorRoomId(GameObject guestObject, out string roomId)
    {
        roomId = string.Empty;
        ActorRoomState actorState = guestObject.GetComponent<ActorRoomState>();

        if (actorState == null)
        {
            actorState = guestObject.GetComponentInParent<ActorRoomState>(true);
        }

        if (actorState == null)
        {
            actorState = guestObject.GetComponentInChildren<ActorRoomState>(true);
        }

        if (actorState == null || string.IsNullOrWhiteSpace(actorState.CurrentRoomId))
        {
            return false;
        }

        roomId = actorState.CurrentRoomId;
        return true;
    }

    private static bool TryResolveWalkerRoomId(GameObject guestObject, out string roomId)
    {
        roomId = string.Empty;
        RoomPersonWalker2D walker = guestObject.GetComponent<RoomPersonWalker2D>();

        if (walker == null)
        {
            walker = guestObject.GetComponentInParent<RoomPersonWalker2D>(true);
        }

        if (walker == null)
        {
            walker = guestObject.GetComponentInChildren<RoomPersonWalker2D>(true);
        }

        if (walker != null && walker.RoomProfile != null && !string.IsNullOrWhiteSpace(walker.RoomProfile.RoomId))
        {
            roomId = walker.RoomProfile.RoomId;
            return true;
        }

        if (guestObject.name.Contains("Walker GEH", System.StringComparison.OrdinalIgnoreCase) ||
            guestObject.name.Contains("Walker_GEH", System.StringComparison.OrdinalIgnoreCase))
        {
            roomId = "Grand Entrance Hall";
            return true;
        }

        return false;
    }

    private static bool TryResolveProjectedProfileRoomId(GameObject guestObject, out string roomId)
    {
        roomId = string.Empty;
        RoomProjectedEntity projectedEntity = ResolveProjectedEntity(guestObject);

        if (projectedEntity == null)
        {
            return false;
        }

        if (projectedEntity.RoomProfile != null && !string.IsNullOrWhiteSpace(projectedEntity.RoomProfile.RoomId))
        {
            roomId = projectedEntity.RoomProfile.RoomId;
            return true;
        }

        return false;
    }

    private static RoomProjectedEntity ResolveProjectedEntity(GameObject guestObject)
    {
        if (guestObject == null)
        {
            return null;
        }

        RoomProjectedEntity projectedEntity = guestObject.GetComponent<RoomProjectedEntity>();

        if (projectedEntity == null)
        {
            projectedEntity = guestObject.GetComponentInParent<RoomProjectedEntity>(true);
        }

        if (projectedEntity == null)
        {
            projectedEntity = guestObject.GetComponentInChildren<RoomProjectedEntity>(true);
        }

        return projectedEntity;
    }

    private static bool TryResolveParentRoomId(GameObject guestObject, out string roomId)
    {
        roomId = string.Empty;
        RoomContentGroup roomContent = guestObject.GetComponentInParent<RoomContentGroup>(true);

        if (roomContent == null || string.IsNullOrWhiteSpace(roomContent.RoomName))
        {
            return false;
        }

        roomId = roomContent.RoomName;
        return true;
    }

    private static bool TryResolveActiveNavigationRoomId(GameObject guestObject, out string roomId)
    {
        roomId = string.Empty;

        if (guestObject == null || !Application.isPlaying || !IsActiveVisibleManagedChapterGuest(guestObject))
        {
            return false;
        }

        RoomNavigationManager navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Exclude);

        if (navigationManager == null || string.IsNullOrWhiteSpace(navigationManager.CurrentRoom))
        {
            return false;
        }

        roomId = navigationManager.CurrentRoom;
        return true;
    }

    private static bool IsActiveVisibleManagedChapterGuest(GameObject guestObject)
    {
        if (guestObject == null || !guestObject.activeInHierarchy)
        {
            return false;
        }

        GuestScaleParticipant participant = guestObject.GetComponent<GuestScaleParticipant>();

        if (participant == null)
        {
            participant = guestObject.GetComponentInParent<GuestScaleParticipant>(true);
        }

        if (participant == null)
        {
            participant = guestObject.GetComponentInChildren<GuestScaleParticipant>(true);
        }

        if (!IsManagedGuestParticipant(participant))
        {
            return false;
        }

        Renderer[] renderers = guestObject.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        Graphic[] graphics = guestObject.GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];

            if (graphic != null && graphic.enabled && graphic.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryInferChapterOneSceneGuestRoomId(string guestName, out string roomId)
    {
        roomId = string.Empty;

        if (!TryExtractGuestNumber(guestName, out int guestNumber))
        {
            return false;
        }

        if (guestNumber >= 1 && guestNumber <= 4)
        {
            roomId = "Grand Entrance Hall";
            return true;
        }

        if (guestNumber >= 5 && guestNumber <= 8)
        {
            roomId = "Drawing Room";
            return true;
        }

        return false;
    }

    private static bool TryExtractGuestNumber(string value, out int guestNumber)
    {
        guestNumber = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        int guestIndex = value.IndexOf("Guest", System.StringComparison.OrdinalIgnoreCase);

        if (guestIndex < 0)
        {
            return false;
        }

        int digitStart = guestIndex + "Guest".Length;

        while (digitStart < value.Length &&
            (char.IsWhiteSpace(value[digitStart]) ||
                value[digitStart] == '_' ||
                value[digitStart] == '-' ||
                value[digitStart] == '#'))
        {
            digitStart++;
        }

        int digitEnd = digitStart;

        while (digitEnd < value.Length && char.IsDigit(value[digitEnd]))
        {
            digitEnd++;
        }

        return digitEnd > digitStart &&
            int.TryParse(value.Substring(digitStart, digitEnd - digitStart), out guestNumber);
    }

    private static bool LooksLikeChapterGuest(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string clean = value.Replace("_", " ");
        return StartsWithGuestNumber(clean);
    }

    private static bool StartsWithGuestNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith("Guest", System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        for (int i = "Guest".Length; i < value.Length; i++)
        {
            if (char.IsWhiteSpace(value[i]))
            {
                continue;
            }

            return char.IsDigit(value[i]);
        }

        return false;
    }

    public static bool IsGuestScaleInfrastructureObject(GameObject candidate)
    {
        return candidate != null &&
            (candidate.GetComponent<GuestRoomScaleApplier>() != null ||
            candidate.GetComponent<GuestRoomScaleCalibration>() != null ||
            candidate.name.Contains("GuestRoomScale", System.StringComparison.OrdinalIgnoreCase) ||
            candidate.name.Contains("GuestScale", System.StringComparison.OrdinalIgnoreCase) ||
            candidate.name.Contains("GuestArrival", System.StringComparison.OrdinalIgnoreCase) ||
            candidate.name.Contains("GuestDrawingRoomDoorTarget", System.StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsManagedGuestParticipant(GuestScaleParticipant participant)
    {
        if (participant == null ||
            participant.gameObject == null ||
            IsGuestScaleInfrastructureObject(participant.gameObject) ||
            participant.ExcludeFromGuestScaling ||
            participant.IsButler)
        {
            return false;
        }

        return LooksLikeChapterGuest(participant.gameObject.name) ||
            LooksLikeChapterGuest(participant.CharacterId);
    }
}
