using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class GuestButlerScaleHarmonizer : MonoBehaviour
{
    [SerializeField] private PointClickPlayerMovement butlerScaleSource;
    [SerializeField] private bool applyInPlayMode = true;
    [SerializeField] private bool previewInEditMode = true;
    [SerializeField] private bool includeInactiveInEditMode = true;
    [SerializeField] private bool applyToRoomProjectedEntities = true;
    [SerializeField] private bool applyToRoomPersonWalkers = true;
    [SerializeField] private bool applyToActorRoomStates = true;
    [SerializeField] private bool skipButlerObject = true;
    [SerializeField] private bool logSummary;
    [SerializeField] private float defaultGuestRelativeHeightMultiplier = 1f;
    [SerializeField, HideInInspector] private float debugGuestScaleMultiplier = 1f;

    private readonly Dictionary<Transform, ProofBaseline> proofBaselines = new Dictionary<Transform, ProofBaseline>();
    private readonly Dictionary<Transform, Vector3> lastFittedLocalScales = new Dictionary<Transform, Vector3>();
    private readonly HashSet<Transform> scaleOverwriteWarnings = new HashSet<Transform>();
    private bool proofModeActive;
    private string lastSummaryKey = string.Empty;

    public PointClickPlayerMovement ButlerScaleSource => ResolveButlerScaleSource();
    public float DebugGuestScaleMultiplier => debugGuestScaleMultiplier;
    public bool ProofModeActive => proofModeActive;

    private void Awake()
    {
        ResolveButlerScaleSource();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !applyInPlayMode)
        {
            return;
        }

        ApplyGuestScaling(false);
    }

    public void SetButlerScaleSource(PointClickPlayerMovement source)
    {
        butlerScaleSource = source;
    }

    public void SetDebugGuestScaleMultiplier(float multiplier)
    {
        float safeMultiplier = Mathf.Max(0.001f, multiplier);

        if (Mathf.Approximately(safeMultiplier, 1f))
        {
            RestoreProofBaselines();
            debugGuestScaleMultiplier = 1f;
            proofModeActive = false;
            proofBaselines.Clear();
            return;
        }

        if (proofModeActive)
        {
            RestoreProofBaselines();
        }

        debugGuestScaleMultiplier = safeMultiplier;
        BeginProofMode();
    }

    public void RestoreRealButlerScaling()
    {
        RestoreProofBaselines();
        debugGuestScaleMultiplier = 1f;
        proofModeActive = false;
        proofBaselines.Clear();
        RefreshNow();
    }

    public GuestScaleApplySummary RefreshNow()
    {
        return ApplyGuestScaling(true);
    }

    public GuestScaleApplySummary ApplyGuestScaling(bool requestedFromTool)
    {
        if (!Application.isPlaying && !previewInEditMode && !requestedFromTool)
        {
            return GuestScaleApplySummary.Empty;
        }

        PointClickPlayerMovement source = ResolveButlerScaleSource();

        if (source == null)
        {
            if (logSummary)
            {
                Debug.LogError("[GuestButlerScale] No Butler scale source found. Run Tools > Characters > Apply Butler Scaling To Guests > Audit.", this);
            }

            return GuestScaleApplySummary.Empty;
        }

        Camera camera = ResolveCamera();

        if (camera == null)
        {
            if (logSummary)
            {
                Debug.LogError("[GuestButlerScale] No camera found for visual-height fitting.", this);
            }

            GuestScaleApplySummary noCameraSummary = new GuestScaleApplySummary(source.name);
            noCameraSummary.FitFailed++;
            return noCameraSummary;
        }

        GuestScaleApplySummary summary = new GuestScaleApplySummary(source.name);
        List<GuestScaleTarget> targets = BuildGuestScaleTargets(source, ref summary);
        bool hasButlerReferenceHeight = source.TryGetButlerReferenceScreenHeightForRoomScale(camera, out float butlerReferenceScreenHeight);
        bool proofMode = proofModeActive && !Mathf.Approximately(debugGuestScaleMultiplier, 1f);

        for (int i = 0; i < targets.Count; i++)
        {
            ApplyToTarget(
                targets[i],
                source,
                camera,
                hasButlerReferenceHeight,
                butlerReferenceScreenHeight,
                proofMode,
                ref summary);
        }

        if (logSummary)
        {
            LogSummary(summary);
        }

        return summary;
    }

    private void BeginProofMode()
    {
        proofModeActive = true;
        proofBaselines.Clear();

        Camera camera = ResolveCamera();
        PointClickPlayerMovement source = ResolveButlerScaleSource();

        if (camera == null)
        {
            return;
        }

        GuestScaleApplySummary ignoredSummary = GuestScaleApplySummary.Empty;
        List<GuestScaleTarget> targets = BuildGuestScaleTargets(source, ref ignoredSummary);

        for (int i = 0; i < targets.Count; i++)
        {
            Transform root = targets[i].Root;

            if (root == null)
            {
                continue;
            }

            bool hasHeight = CharacterVisualBoundsUtility.TryGetScreenHeight(
                root,
                camera,
                out float screenHeight,
                includeInactive: !Application.isPlaying && includeInactiveInEditMode);
            proofBaselines[root] = new ProofBaseline(root, root.localScale, screenHeight, hasHeight);
        }
    }

    private void RestoreProofBaselines()
    {
        foreach (KeyValuePair<Transform, ProofBaseline> pair in proofBaselines)
        {
            ProofBaseline baseline = pair.Value;

            if (baseline.Root != null)
            {
                baseline.Root.localScale = baseline.LocalScale;
            }
        }
    }

    private void ApplyToTarget(
        GuestScaleTarget target,
        PointClickPlayerMovement source,
        Camera camera,
        bool hasButlerReferenceHeight,
        float butlerReferenceScreenHeight,
        bool proofMode,
        ref GuestScaleApplySummary summary)
    {
        if (target.Root == null)
        {
            summary.FitFailed++;
            summary.AddProofFailure(target.Path, "Root missing");
            return;
        }

        WarnIfScaleWasOverwritten(target);

        PointClickPlayerMovement.ButlerCharacterScaleSample sample = default;
        bool hasSample = source != null &&
            !string.IsNullOrWhiteSpace(target.RoomId) &&
            source.TryEvaluateButlerCharacterScale(
                target.RoomId,
                target.RoomLocalFootPoint,
                out sample);

        if (hasSample)
        {
            ApplyControllerScaleSample(target, source);
        }
        else
        {
            summary.MissingCalibration++;

            if (!proofMode)
            {
                return;
            }
        }

        bool includeInactive = !Application.isPlaying && includeInactiveInEditMode;

        if (!CharacterVisualBoundsUtility.TryGetScreenHeight(target.Root, camera, out float beforeHeight, includeInactive))
        {
            summary.NoVisualBounds++;
            summary.AddProofFailure(target.Path, "No visual bounds found");
            return;
        }

        bool hasTargetHeight = TryGetTargetScreenHeight(
            target,
            hasSample,
            sample,
            hasButlerReferenceHeight,
            butlerReferenceScreenHeight,
            proofMode,
            beforeHeight,
            out float targetHeight,
            out string failureReason);

        if (!hasTargetHeight)
        {
            summary.FitFailed++;
            summary.AddProofFailure(target.Path, failureReason);
            return;
        }

        bool fitSucceeded = CharacterVisualBoundsUtility.TryFitScreenHeight(
            target.Root,
            camera,
            targetHeight,
            out Vector3 previousScale,
            out Vector3 nextScale,
            2);
        bool hasAfterHeight = CharacterVisualBoundsUtility.TryGetScreenHeight(target.Root, camera, out float afterHeight, includeInactive);
        bool changed = !Approximately(previousScale, nextScale) ||
            (hasAfterHeight && Mathf.Abs(afterHeight - targetHeight) <= Mathf.Max(0.75f, targetHeight * 0.03f));

        if (!fitSucceeded || !changed)
        {
            summary.FitFailed++;
            summary.AddProofFailure(target.Path, fitSucceeded ? "Scale did not change" : "Fitter failed");
            return;
        }

        summary.Scaled++;

        if (hasSample)
        {
            summary.UsingButlerRules++;
            summary.IncludeScale(sample.NormalizedScale * Mathf.Max(0.001f, debugGuestScaleMultiplier));
        }
        else
        {
            summary.IncludeScale(debugGuestScaleMultiplier);
        }

        summary.IncludeVisualHeight(afterHeight);
        lastFittedLocalScales[target.Root] = target.Root.localScale;
    }

    private bool TryGetTargetScreenHeight(
        GuestScaleTarget target,
        bool hasSample,
        PointClickPlayerMovement.ButlerCharacterScaleSample sample,
        bool hasButlerReferenceHeight,
        float butlerReferenceScreenHeight,
        bool proofMode,
        float currentScreenHeight,
        out float targetHeight,
        out string failureReason)
    {
        targetHeight = 0f;
        failureReason = string.Empty;

        if (hasSample && hasButlerReferenceHeight)
        {
            float relativeHeight = Mathf.Clamp(target.RelativeHeightMultiplier * Mathf.Max(0.001f, defaultGuestRelativeHeightMultiplier), 0.25f, 3f);
            targetHeight =
                butlerReferenceScreenHeight *
                Mathf.Max(0.001f, sample.NormalizedScale) *
                relativeHeight *
                Mathf.Max(0.001f, debugGuestScaleMultiplier);
            return targetHeight > 0.01f;
        }

        if (!proofMode)
        {
            failureReason = hasSample
                ? "No Butler reference visual height"
                : "No complete Butler calibration for this room";
            return false;
        }

        if (target.Root != null &&
            proofBaselines.TryGetValue(target.Root, out ProofBaseline baseline) &&
            baseline.HasScreenHeight)
        {
            targetHeight = baseline.ScreenHeight * Mathf.Max(0.001f, debugGuestScaleMultiplier);
            return targetHeight > 0.01f;
        }

        targetHeight = currentScreenHeight * Mathf.Max(0.001f, debugGuestScaleMultiplier);
        failureReason = "Proof baseline missing";
        return targetHeight > 0.01f;
    }

    private void ApplyControllerScaleSample(GuestScaleTarget target, PointClickPlayerMovement source)
    {
        if (target.ProjectedEntity != null)
        {
            target.ProjectedEntity.SetButlerScaleSource(source, false);
            target.ProjectedEntity.SetButlerCharacterScaleRulesEnabled(true, false);
            target.ProjectedEntity.SetIgnoreRoomVisualScaleOverridesWhenUsingButlerRules(true, false);
            target.ProjectedEntity.ApplyButlerCharacterScaleNow(source);
            return;
        }

        if (target.Walker != null)
        {
            target.Walker.SetButlerScaleSource(source, false);
            target.Walker.SetButlerCharacterScaleRulesEnabled(true, false);
            target.Walker.SetPreserveAuthoredLocalScaleWhenUsingButlerRules(true, false);
            target.Walker.ApplyButlerCharacterScaleNow(source);
            return;
        }

        if (target.Actor != null)
        {
            target.Actor.ApplyButlerCharacterScaleNow(source);
        }
    }

    private List<GuestScaleTarget> BuildGuestScaleTargets(
        PointClickPlayerMovement source,
        ref GuestScaleApplySummary summary)
    {
        List<GuestScaleTarget> targets = new List<GuestScaleTarget>();
        HashSet<Transform> claimedRoots = new HashSet<Transform>();

        if (applyToRoomProjectedEntities)
        {
            RoomProjectedEntity[] entities = FindObjectsByType<RoomProjectedEntity>(GetFindObjectsInactiveMode());

            for (int i = 0; i < entities.Length; i++)
            {
                RoomProjectedEntity entity = entities[i];

                if (!IsLoadedSceneObject(entity))
                {
                    continue;
                }

                summary.ProjectedFound++;

                if (skipButlerObject && IsButlerObjectOrChild(entity.transform, source))
                {
                    summary.Skipped++;
                    continue;
                }

                RoomPersonWalker2D walker = entity.GetComponent<RoomPersonWalker2D>();

                if (entity.Mode != RoomProjectedEntity.ProjectionMode.FloorCharacter ||
                    (walker != null && !entity.IsProjectionActive))
                {
                    summary.Skipped++;
                    continue;
                }

                if (!entity.TryResolveGuestRoomAndFootPoint(out string roomId, out Vector2 footPoint))
                {
                    summary.Skipped++;
                    continue;
                }

                AddTarget(
                    targets,
                    claimedRoots,
                    new GuestScaleTarget(
                        entity.GetGuestScaleRoot(),
                        entity,
                        null,
                        null,
                        "RoomProjectedEntity",
                        roomId,
                        footPoint,
                        entity.GetGuestRelativeHeightMultiplier()),
                    source,
                    ref summary);
            }
        }

        if (applyToRoomPersonWalkers)
        {
            RoomPersonWalker2D[] walkers = FindObjectsByType<RoomPersonWalker2D>(GetFindObjectsInactiveMode());

            for (int i = 0; i < walkers.Length; i++)
            {
                RoomPersonWalker2D walker = walkers[i];

                if (!IsLoadedSceneObject(walker))
                {
                    continue;
                }

                summary.WalkersFound++;

                if (skipButlerObject && IsButlerObjectOrChild(walker.transform, source))
                {
                    summary.Skipped++;
                    continue;
                }

                RoomProjectedEntity projection = walker.GetComponent<RoomProjectedEntity>();

                if (projection != null &&
                    projection.Mode == RoomProjectedEntity.ProjectionMode.FloorCharacter &&
                    projection.IsProjectionActive)
                {
                    summary.Skipped++;
                    continue;
                }

                if (!walker.TryResolveGuestRoomAndFootPoint(out string roomId, out Vector2 footPoint))
                {
                    summary.Skipped++;
                    continue;
                }

                AddTarget(
                    targets,
                    claimedRoots,
                    new GuestScaleTarget(
                        walker.GetGuestScaleRoot(),
                        null,
                        walker,
                        null,
                        "RoomPersonWalker2D",
                        roomId,
                        footPoint,
                        walker.GetGuestRelativeHeightMultiplier()),
                    source,
                    ref summary);
            }
        }

        if (applyToActorRoomStates)
        {
            ActorRoomState[] actors = FindObjectsByType<ActorRoomState>(GetFindObjectsInactiveMode());

            for (int i = 0; i < actors.Length; i++)
            {
                ActorRoomState actor = actors[i];

                if (!IsLoadedSceneObject(actor))
                {
                    continue;
                }

                summary.ActorStatesFound++;

                if (skipButlerObject && IsButlerObjectOrChild(actor.transform, source))
                {
                    summary.Skipped++;
                    continue;
                }

                if (!LooksLikeGuestActor(actor))
                {
                    summary.Skipped++;
                    continue;
                }

                RoomProjectedEntity projection = actor.Projection;

                if (projection != null && projection.IsProjectionActive)
                {
                    summary.Skipped++;
                    continue;
                }

                if (!actor.TryResolveGuestRoomAndFootPoint(out string roomId, out Vector2 footPoint))
                {
                    summary.Skipped++;
                    continue;
                }

                AddTarget(
                    targets,
                    claimedRoots,
                    new GuestScaleTarget(
                        actor.GetGuestScaleRoot(),
                        null,
                        null,
                        actor,
                        "ActorRoomState",
                        roomId,
                        footPoint,
                        actor.GetGuestRelativeHeightMultiplier()),
                    source,
                    ref summary);
            }
        }

        return targets;
    }

    private void AddTarget(
        List<GuestScaleTarget> targets,
        HashSet<Transform> claimedRoots,
        GuestScaleTarget target,
        PointClickPlayerMovement source,
        ref GuestScaleApplySummary summary)
    {
        if (target.Root == null)
        {
            summary.Skipped++;
            return;
        }

        if (skipButlerObject && IsButlerObjectOrChild(target.Root, source))
        {
            summary.Skipped++;
            return;
        }

        if (claimedRoots.Contains(target.Root))
        {
            summary.Skipped++;
            return;
        }

        claimedRoots.Add(target.Root);
        targets.Add(target);
    }

    private FindObjectsInactive GetFindObjectsInactiveMode()
    {
        return Application.isPlaying || !includeInactiveInEditMode
            ? FindObjectsInactive.Exclude
            : FindObjectsInactive.Include;
    }

    private PointClickPlayerMovement ResolveButlerScaleSource()
    {
        if (butlerScaleSource != null)
        {
            return butlerScaleSource;
        }

        butlerScaleSource = GetComponent<PointClickPlayerMovement>();

        if (butlerScaleSource != null)
        {
            return butlerScaleSource;
        }

        PointClickPlayerMovement tagged = null;
        PointClickPlayerMovement named = null;
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

            bool active = candidate.gameObject.activeInHierarchy;

            if (active)
            {
                firstActive ??= candidate;

                if (string.Equals(candidate.gameObject.tag, "Player", StringComparison.OrdinalIgnoreCase))
                {
                    tagged ??= candidate;
                }

                if (NameLooksLikePlayerOrButler(candidate.name) ||
                    NameLooksLikePlayerOrButler(candidate.gameObject.name))
                {
                    named ??= candidate;
                }
            }
            else if (!Application.isPlaying)
            {
                firstInactive ??= candidate;
            }
        }

        butlerScaleSource =
            tagged != null
                ? tagged
                : named != null
                    ? named
                    : firstActive != null
                        ? firstActive
                        : firstInactive;
        return butlerScaleSource;
    }

    private Camera ResolveCamera()
    {
        if (Camera.main != null)
        {
            return Camera.main;
        }

        return FindAnyObjectByType<Camera>(FindObjectsInactive.Exclude);
    }

    private void LogSummary(GuestScaleApplySummary summary)
    {
        string key = $"{summary.SourceName}|{summary.ProjectedFound}|{summary.WalkersFound}|{summary.ActorStatesFound}|{summary.Scaled}|{summary.MissingCalibration}|{summary.NoVisualBounds}|{summary.FitFailed}|{summary.MinScale:0.###}|{summary.MaxScale:0.###}";

        if (key == lastSummaryKey)
        {
            return;
        }

        lastSummaryKey = key;

        if (summary.Scaled <= 0)
        {
            Debug.LogError("[GuestButlerScale] No guests were scaled. Run Tools > Characters > Apply Butler Scaling To Guests > Audit.", this);
            return;
        }

        Debug.Log(
            $"[GuestButlerScale] scaled {summary.Scaled} guests ({summary.ProjectedFound} RoomProjectedEntity, {summary.WalkersFound} walkers, {summary.ActorStatesFound} actor states), {summary.Skipped} skipped, {summary.MissingCalibration} missing calibration, {summary.NoVisualBounds} no visual bounds, {summary.FitFailed} fit failed, source={summary.SourceName}, scale range={summary.MinScale:0.###}-{summary.MaxScale:0.###}, visual height range={summary.MinVisualHeight:0.#}-{summary.MaxVisualHeight:0.#} px",
            this);
    }

    private void WarnIfScaleWasOverwritten(GuestScaleTarget target)
    {
        if (!logSummary || target.Root == null)
        {
            return;
        }

        if (!lastFittedLocalScales.TryGetValue(target.Root, out Vector3 lastScale) ||
            Approximately(lastScale, target.Root.localScale) ||
            scaleOverwriteWarnings.Contains(target.Root))
        {
            return;
        }

        scaleOverwriteWarnings.Add(target.Root);
        Debug.LogWarning(
            $"[GuestButlerScale] Another script changed guest scale after harmonizer: {target.Path}",
            target.Root);
    }

    private bool IsLoadedSceneObject(Component component)
    {
        return component != null &&
            component.gameObject != null &&
            component.gameObject.scene.IsValid() &&
            component.gameObject.scene.isLoaded;
    }

    private static bool IsButlerObjectOrChild(Transform target, PointClickPlayerMovement source)
    {
        return target != null &&
            source != null &&
            source.transform != null &&
            (target == source.transform || target.IsChildOf(source.transform));
    }

    private static bool Approximately(Vector3 left, Vector3 right)
    {
        return Mathf.Approximately(left.x, right.x) &&
            Mathf.Approximately(left.y, right.y) &&
            Mathf.Approximately(left.z, right.z);
    }

    private static string GetObjectPath(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        string path = target.name;
        Transform current = target.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private static bool NameLooksLikePlayerOrButler(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            (value.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("Butler", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool LooksLikeGuestActor(ActorRoomState actor)
    {
        return actor != null &&
            (ContainsGuest(actor.ActorId) ||
            ContainsGuest(actor.name) ||
            ContainsGuest(actor.gameObject.name));
    }

    private static bool ContainsGuest(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.IndexOf("Guest", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private readonly struct ProofBaseline
    {
        public ProofBaseline(Transform root, Vector3 localScale, float screenHeight, bool hasScreenHeight)
        {
            Root = root;
            LocalScale = localScale;
            ScreenHeight = screenHeight;
            HasScreenHeight = hasScreenHeight;
        }

        public Transform Root { get; }
        public Vector3 LocalScale { get; }
        public float ScreenHeight { get; }
        public bool HasScreenHeight { get; }
    }

    private readonly struct GuestScaleTarget
    {
        public GuestScaleTarget(
            Transform root,
            RoomProjectedEntity projectedEntity,
            RoomPersonWalker2D walker,
            ActorRoomState actor,
            string controllerType,
            string roomId,
            Vector2 roomLocalFootPoint,
            float relativeHeightMultiplier)
        {
            Root = root;
            ProjectedEntity = projectedEntity;
            Walker = walker;
            Actor = actor;
            ControllerType = controllerType;
            RoomId = string.IsNullOrWhiteSpace(roomId) ? string.Empty : roomId.Trim();
            RoomLocalFootPoint = roomLocalFootPoint;
            RelativeHeightMultiplier = Mathf.Clamp(relativeHeightMultiplier, 0.5f, 1.5f);
            Path = GetObjectPath(root);
        }

        public Transform Root { get; }
        public RoomProjectedEntity ProjectedEntity { get; }
        public RoomPersonWalker2D Walker { get; }
        public ActorRoomState Actor { get; }
        public string ControllerType { get; }
        public string RoomId { get; }
        public Vector2 RoomLocalFootPoint { get; }
        public float RelativeHeightMultiplier { get; }
        public string Path { get; }
    }

    public struct GuestScaleApplySummary
    {
        public static readonly GuestScaleApplySummary Empty = new GuestScaleApplySummary(string.Empty);

        public GuestScaleApplySummary(string sourceName)
        {
            SourceName = sourceName;
            ProjectedFound = 0;
            WalkersFound = 0;
            ActorStatesFound = 0;
            UsingButlerRules = 0;
            Skipped = 0;
            MissingCalibration = 0;
            NoVisualBounds = 0;
            FitFailed = 0;
            Scaled = 0;
            MinScale = float.PositiveInfinity;
            MaxScale = 0f;
            MinVisualHeight = float.PositiveInfinity;
            MaxVisualHeight = 0f;
            ProofFailures = new List<string>();
        }

        public string SourceName;
        public int ProjectedFound;
        public int WalkersFound;
        public int ActorStatesFound;
        public int UsingButlerRules;
        public int Skipped;
        public int MissingCalibration;
        public int NoVisualBounds;
        public int FitFailed;
        public int Scaled;
        public float MinScale;
        public float MaxScale;
        public float MinVisualHeight;
        public float MaxVisualHeight;
        public List<string> ProofFailures;

        public void IncludeScale(float scale)
        {
            MinScale = Mathf.Min(MinScale, scale);
            MaxScale = Mathf.Max(MaxScale, scale);
        }

        public void IncludeVisualHeight(float height)
        {
            MinVisualHeight = Mathf.Min(MinVisualHeight, height);
            MaxVisualHeight = Mathf.Max(MaxVisualHeight, height);
        }

        public void AddProofFailure(string path, string reason)
        {
            if (ProofFailures == null)
            {
                ProofFailures = new List<string>();
            }

            ProofFailures.Add($"{path}: {reason}");
        }
    }
}
