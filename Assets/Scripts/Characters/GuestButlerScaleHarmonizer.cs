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
        bool proofMode = proofModeActive && !Mathf.Approximately(debugGuestScaleMultiplier, 1f);

        if (!Application.isPlaying && !previewInEditMode && !requestedFromTool)
        {
            return GuestScaleApplySummary.Empty;
        }

        PointClickPlayerMovement source = ResolveButlerScaleSource();

        if (source == null && !proofMode)
        {
            if (logSummary)
            {
                Debug.LogWarning("[GuestButlerScale] No Butler scale source found. Run Tools > Characters > Human Scale Audit.", this);
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

            GuestScaleApplySummary noCameraSummary = new GuestScaleApplySummary(source != null ? source.name : name);
            noCameraSummary.FitFailed++;
            return noCameraSummary;
        }

        GuestScaleApplySummary summary = new GuestScaleApplySummary(source != null ? source.name : name);
        List<GuestScaleTarget> targets = BuildGuestScaleTargets(source, camera, ref summary);
        float standingHumanReferenceScreenHeight = 0f;
        string butlerReferenceDiagnostic = string.Empty;
        bool hasButlerReferenceHeight = source != null &&
            source.TryGetButlerHumanScaleReference(
                camera,
                out standingHumanReferenceScreenHeight,
                out butlerReferenceDiagnostic);

        for (int i = 0; i < targets.Count; i++)
        {
            ApplyToTarget(
                targets[i],
                source,
                camera,
                hasButlerReferenceHeight,
                standingHumanReferenceScreenHeight,
                butlerReferenceDiagnostic,
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

        if (camera == null)
        {
            return;
        }

        PointClickPlayerMovement source = ResolveButlerScaleSource();
        GuestScaleApplySummary ignoredSummary = GuestScaleApplySummary.Empty;
        List<GuestScaleTarget> targets = BuildGuestScaleTargets(source, camera, ref ignoredSummary);

        for (int i = 0; i < targets.Count; i++)
        {
            GuestScaleTarget target = targets[i];

            if (target.ScaleRoot == null)
            {
                continue;
            }

            bool hasHeight = CharacterVisualBoundsUtility.TryResolveCharacterVisualTarget(
                target.BoundsRoot,
                camera,
                out CharacterVisualBoundsUtility.CharacterVisualTarget visualTarget,
                includeInactive: !Application.isPlaying && includeInactiveInEditMode);
            Transform primaryVisual = hasHeight ? visualTarget.PrimaryVisual : target.PrimaryVisual;
            proofBaselines[target.ScaleRoot] = new ProofBaseline(
                target.ScaleRoot,
                target.ScaleRoot.localScale,
                primaryVisual,
                primaryVisual != null ? primaryVisual.localScale : Vector3.one,
                hasHeight ? visualTarget.ScreenHeight : 0f,
                hasHeight);
        }
    }

    public void RestoreProofBaselines()
    {
        foreach (KeyValuePair<Transform, ProofBaseline> pair in proofBaselines)
        {
            ProofBaseline baseline = pair.Value;

            if (baseline.ScaleRoot != null)
            {
                baseline.ScaleRoot.localScale = baseline.ScaleRootLocalScale;
            }

            if (baseline.PrimaryVisual != null && baseline.PrimaryVisual != baseline.ScaleRoot)
            {
                baseline.PrimaryVisual.localScale = baseline.PrimaryVisualLocalScale;
            }
        }
    }

    private void ApplyToTarget(
        GuestScaleTarget target,
        PointClickPlayerMovement source,
        Camera camera,
        bool hasButlerReferenceHeight,
        float standingHumanReferenceScreenHeight,
        string butlerReferenceDiagnostic,
        bool proofMode,
        ref GuestScaleApplySummary summary)
    {
        if (target.ScaleRoot == null || target.BoundsRoot == null)
        {
            summary.FitFailed++;
            summary.AddProofFailure(target.Path, "Scale root or bounds root missing");
            return;
        }

        WarnIfScaleWasOverwritten(target);
        PrepareControllerForFinalHumanScale(target, source);

        bool includeInactive = !Application.isPlaying && includeInactiveInEditMode;

        if (!CharacterVisualBoundsUtility.TryResolveCharacterVisualTarget(
            target.BoundsRoot,
            camera,
            out CharacterVisualBoundsUtility.CharacterVisualTarget visualTarget,
            includeInactive))
        {
            summary.NoVisualBounds++;
            summary.AddProofFailure(target.Path, "No visible Renderer or Graphic found");
            return;
        }

        PointClickPlayerMovement.ButlerCharacterScaleSample sample = default;
        bool hasSample = source != null &&
            !string.IsNullOrWhiteSpace(target.RoomId) &&
            source.TryEvaluateButlerCharacterScale(
                target.RoomId,
                target.RoomLocalFootPoint,
                out sample);

        if (!hasSample)
        {
            summary.MissingCalibration++;

            if (!proofMode &&
                source != null &&
                source.TryEvaluateCurrentButlerCharacterScale(out sample))
            {
                hasSample = true;
            }
        }

        if (!TryGetTargetScreenHeight(
            target,
            hasSample,
            sample,
            hasButlerReferenceHeight,
            standingHumanReferenceScreenHeight,
            butlerReferenceDiagnostic,
            proofMode,
            visualTarget.ScreenHeight,
            out float targetHeight,
            out string failureReason))
        {
            summary.FitFailed++;
            summary.AddProofFailure(target.Path, failureReason);
            return;
        }

        Transform scaleRoot = target.ScaleRoot;
        Transform boundsRoot = target.BoundsRoot;

        if (CharacterVisualBoundsUtility.LooksLikeForbiddenContainer(scaleRoot) &&
            visualTarget.PrimaryVisual != null)
        {
            scaleRoot = visualTarget.PrimaryVisual;
        }

        bool fitSucceeded = CharacterVisualBoundsUtility.TryApplyTargetScreenHeight(
            scaleRoot,
            boundsRoot,
            camera,
            targetHeight,
            out CharacterVisualBoundsUtility.CharacterVisualFitResult fitResult,
            includeInactive);

        if (!fitSucceeded)
        {
            summary.FitFailed++;
            summary.AddProofFailure(target.Path, fitResult.Diagnostic);
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

        summary.IncludeVisualHeight(fitResult.AfterHeight);
        lastFittedLocalScales[scaleRoot] = scaleRoot.localScale;
    }

    private bool TryGetTargetScreenHeight(
        GuestScaleTarget target,
        bool hasSample,
        PointClickPlayerMovement.ButlerCharacterScaleSample sample,
        bool hasButlerReferenceHeight,
        float standingHumanReferenceScreenHeight,
        string butlerReferenceDiagnostic,
        bool proofMode,
        float currentScreenHeight,
        out float targetHeight,
        out string failureReason)
    {
        targetHeight = 0f;
        failureReason = string.Empty;

        if (proofMode)
        {
            if (target.ScaleRoot != null &&
                proofBaselines.TryGetValue(target.ScaleRoot, out ProofBaseline baseline) &&
                baseline.HasScreenHeight)
            {
                targetHeight = baseline.ScreenHeight * Mathf.Max(0.001f, debugGuestScaleMultiplier);
                return targetHeight > 0.01f;
            }

            targetHeight = currentScreenHeight * Mathf.Max(0.001f, debugGuestScaleMultiplier);
            failureReason = "Proof baseline missing; used current visible height";
            return targetHeight > 0.01f;
        }

        if (!hasSample)
        {
            failureReason = "No complete Butler calibration for this room";
            return false;
        }

        if (!hasButlerReferenceHeight)
        {
            failureReason = string.IsNullOrWhiteSpace(butlerReferenceDiagnostic)
                ? "No Butler reference visual height"
                : butlerReferenceDiagnostic;
            return false;
        }

        float relativeHeight = Mathf.Clamp(
            Mathf.Max(0.001f, defaultGuestRelativeHeightMultiplier),
            0.25f,
            3f);
        float poseHeight = Mathf.Clamp(target.PoseHeightMultiplier, 0.25f, 1.25f);
        targetHeight =
            standingHumanReferenceScreenHeight *
            Mathf.Max(0.001f, sample.NormalizedScale) *
            poseHeight *
            relativeHeight *
            Mathf.Max(0.001f, debugGuestScaleMultiplier);
        return targetHeight > 0.01f;
    }

    private void PrepareControllerForFinalHumanScale(GuestScaleTarget target, PointClickPlayerMovement source)
    {
        if (target.ProjectedEntity != null)
        {
            target.ProjectedEntity.SetButlerScaleSource(source, false);
            target.ProjectedEntity.SetButlerCharacterScaleRulesEnabled(true, false);
            target.ProjectedEntity.SetIgnoreRoomVisualScaleOverridesWhenUsingButlerRules(true, false);
            target.ProjectedEntity.SetIgnoreVisualProfileHeightMultiplierWhenUsingButlerRules(true, false);
            target.ProjectedEntity.SetRoomVisualScaleOverridesEnabled(false, false);

            if (!Application.isPlaying)
            {
                target.ProjectedEntity.ApplyProjection();
            }

            return;
        }

        if (target.Walker != null)
        {
            target.Walker.SetButlerScaleSource(source, false);
            target.Walker.SetButlerCharacterScaleRulesEnabled(true, false);
            target.Walker.SetPreserveAuthoredLocalScaleWhenUsingButlerRules(true, false);

            if (!Application.isPlaying)
            {
                target.Walker.RefreshDepthVisualsNow();
            }
        }
    }

    private List<GuestScaleTarget> BuildGuestScaleTargets(
        PointClickPlayerMovement source,
        Camera camera,
        ref GuestScaleApplySummary summary)
    {
        List<GuestScaleTarget> targets = new List<GuestScaleTarget>();
        HashSet<Transform> claimedScaleRoots = new HashSet<Transform>();
        HashSet<Transform> claimedPrimaryVisuals = new HashSet<Transform>();
        bool includeInactive = !Application.isPlaying && includeInactiveInEditMode;

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

                ResolveProjectedVisualRoots(
                    entity,
                    camera,
                    includeInactive,
                    out Transform boundsRoot,
                    out Transform scaleRoot,
                    out Transform primaryVisual,
                    out string diagnostic);

                AddTarget(
                    targets,
                    claimedScaleRoots,
                    claimedPrimaryVisuals,
                    new GuestScaleTarget(
                        boundsRoot,
                        scaleRoot,
                        primaryVisual,
                        entity,
                        null,
                        null,
                        walker != null ? "RoomProjectedEntity + RoomPersonWalker2D" : "RoomProjectedEntity",
                        roomId,
                        footPoint,
                        entity.GetGuestRelativeHeightMultiplier(),
                        entity.GetGuestPoseHeightMultiplier(),
                        entity.IsGuestSeated(),
                        diagnostic),
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

                ResolveWalkerVisualRoots(
                    walker,
                    camera,
                    includeInactive,
                    out Transform boundsRoot,
                    out Transform scaleRoot,
                    out Transform primaryVisual,
                    out string diagnostic);

                AddTarget(
                    targets,
                    claimedScaleRoots,
                    claimedPrimaryVisuals,
                    new GuestScaleTarget(
                        boundsRoot,
                        scaleRoot,
                        primaryVisual,
                        null,
                        walker,
                        null,
                        projection != null ? "RoomProjectedEntity + RoomPersonWalker2D" : "RoomPersonWalker2D",
                        roomId,
                        footPoint,
                        walker.GetGuestRelativeHeightMultiplier(),
                        walker.GetGuestPoseHeightMultiplier(),
                        walker.IsGuestSeated(),
                        diagnostic),
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

                if (ActorContainsProjectionOrWalker(actor))
                {
                    summary.Skipped++;
                    continue;
                }

                if (!actor.TryResolveGuestRoomAndFootPoint(out string roomId, out Vector2 footPoint))
                {
                    summary.Skipped++;
                    continue;
                }

                ResolveActorVisualRoots(
                    actor,
                    camera,
                    includeInactive,
                    out Transform boundsRoot,
                    out Transform scaleRoot,
                    out Transform primaryVisual,
                    out string diagnostic);

                AddTarget(
                    targets,
                    claimedScaleRoots,
                    claimedPrimaryVisuals,
                    new GuestScaleTarget(
                        boundsRoot,
                        scaleRoot,
                        primaryVisual,
                        null,
                        null,
                        actor,
                        "ActorRoomState",
                        roomId,
                        footPoint,
                        actor.GetGuestRelativeHeightMultiplier(),
                        actor.GetGuestPoseHeightMultiplier(),
                        actor.IsSeated,
                        diagnostic),
                    source,
                    ref summary);
            }
        }

        return targets;
    }

    private void ResolveProjectedVisualRoots(
        RoomProjectedEntity entity,
        Camera camera,
        bool includeInactive,
        out Transform boundsRoot,
        out Transform scaleRoot,
        out Transform primaryVisual,
        out string diagnostic)
    {
        boundsRoot = entity != null ? entity.VisualRoot : null;
        scaleRoot = boundsRoot;
        primaryVisual = null;
        diagnostic = string.Empty;

        if (boundsRoot != null &&
            CharacterVisualBoundsUtility.TryResolveCharacterVisualTarget(boundsRoot, camera, out CharacterVisualBoundsUtility.CharacterVisualTarget target, includeInactive))
        {
            boundsRoot = target.BoundsRoot;
            primaryVisual = target.PrimaryVisual;
            diagnostic = target.Diagnostic;

            if (CharacterVisualBoundsUtility.LooksLikeForbiddenContainer(scaleRoot))
            {
                scaleRoot = primaryVisual;
            }
        }
    }

    private void ResolveWalkerVisualRoots(
        RoomPersonWalker2D walker,
        Camera camera,
        bool includeInactive,
        out Transform boundsRoot,
        out Transform scaleRoot,
        out Transform primaryVisual,
        out string diagnostic)
    {
        boundsRoot = walker != null ? walker.transform : null;
        scaleRoot = boundsRoot;
        primaryVisual = null;
        diagnostic = string.Empty;

        if (walker != null && walker.TargetGraphic != null)
        {
            boundsRoot = walker.TargetGraphic.transform;
            scaleRoot = walker.TargetGraphic.rectTransform;
            primaryVisual = walker.TargetGraphic.transform;
            diagnostic = "RoomPersonWalker2D targetGraphic";
            return;
        }

        if (boundsRoot != null &&
            CharacterVisualBoundsUtility.TryResolveCharacterVisualTarget(boundsRoot, camera, out CharacterVisualBoundsUtility.CharacterVisualTarget target, includeInactive))
        {
            boundsRoot = target.BoundsRoot;
            scaleRoot = target.PrimaryVisual != null ? target.PrimaryVisual : target.BoundsRoot;
            primaryVisual = target.PrimaryVisual;
            diagnostic = target.Diagnostic;
        }
    }

    private void ResolveActorVisualRoots(
        ActorRoomState actor,
        Camera camera,
        bool includeInactive,
        out Transform boundsRoot,
        out Transform scaleRoot,
        out Transform primaryVisual,
        out string diagnostic)
    {
        boundsRoot = actor != null ? actor.GetGuestScaleRoot() : null;
        scaleRoot = boundsRoot;
        primaryVisual = null;
        diagnostic = string.Empty;

        if (boundsRoot != null &&
            CharacterVisualBoundsUtility.TryResolveCharacterVisualTarget(boundsRoot, camera, out CharacterVisualBoundsUtility.CharacterVisualTarget target, includeInactive))
        {
            boundsRoot = target.BoundsRoot;
            primaryVisual = target.PrimaryVisual;
            scaleRoot = target.PrimaryVisual != null ? target.PrimaryVisual : target.BoundsRoot;

            if (target.PrimaryVisual == actor.transform && !CharacterVisualBoundsUtility.LooksLikeForbiddenContainer(actor.transform))
            {
                scaleRoot = actor.transform;
            }

            diagnostic = target.Diagnostic;
        }
    }

    private void AddTarget(
        List<GuestScaleTarget> targets,
        HashSet<Transform> claimedScaleRoots,
        HashSet<Transform> claimedPrimaryVisuals,
        GuestScaleTarget target,
        PointClickPlayerMovement source,
        ref GuestScaleApplySummary summary)
    {
        Transform duplicateKey = target.ScaleRoot != null ? target.ScaleRoot : target.BoundsRoot;

        if (duplicateKey == null)
        {
            summary.Skipped++;
            return;
        }

        if (skipButlerObject &&
            (IsButlerObjectOrChild(duplicateKey, source) ||
            IsButlerObjectOrChild(target.PrimaryVisual, source)))
        {
            summary.Skipped++;
            return;
        }

        if (claimedScaleRoots.Contains(duplicateKey))
        {
            summary.Skipped++;
            return;
        }

        if (target.PrimaryVisual != null && claimedPrimaryVisuals.Contains(target.PrimaryVisual))
        {
            summary.Skipped++;
            return;
        }

        claimedScaleRoots.Add(duplicateKey);

        if (target.PrimaryVisual != null)
        {
            claimedPrimaryVisuals.Add(target.PrimaryVisual);
        }

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
            Debug.LogWarning("[GuestButlerScale] No guests were scaled. Run Tools > Characters > Human Scale Audit.", this);
            return;
        }

        Debug.Log(
            $"[GuestButlerScale] scaled {summary.Scaled} guests ({summary.ProjectedFound} RoomProjectedEntity, {summary.WalkersFound} walkers, {summary.ActorStatesFound} actor states), {summary.Skipped} skipped, {summary.MissingCalibration} missing calibration, {summary.NoVisualBounds} no visual bounds, {summary.FitFailed} fit failed, source={summary.SourceName}, scale range={summary.MinScale:0.###}-{summary.MaxScale:0.###}, visual height range={summary.MinVisualHeight:0.#}-{summary.MaxVisualHeight:0.#} px",
            this);
    }

    private void WarnIfScaleWasOverwritten(GuestScaleTarget target)
    {
        if (!logSummary || target.ScaleRoot == null)
        {
            return;
        }

        if (!lastFittedLocalScales.TryGetValue(target.ScaleRoot, out Vector3 lastScale) ||
            Approximately(lastScale, target.ScaleRoot.localScale) ||
            scaleOverwriteWarnings.Contains(target.ScaleRoot))
        {
            return;
        }

        scaleOverwriteWarnings.Add(target.ScaleRoot);
        Debug.LogWarning(
            $"[GuestButlerScale] Another script changed guest scale before final harmonizer pass: {target.Path}",
            target.ScaleRoot);
    }

    private bool IsLoadedSceneObject(Component component)
    {
        return component != null &&
            component.gameObject != null &&
            component.gameObject.scene.IsValid() &&
            component.gameObject.scene.isLoaded;
    }

    private static bool ActorContainsProjectionOrWalker(ActorRoomState actor)
    {
        if (actor == null)
        {
            return false;
        }

        RoomProjectedEntity projection = actor.GetComponentInChildren<RoomProjectedEntity>(true);

        if (projection != null && projection.IsProjectionActive)
        {
            return true;
        }

        RoomPersonWalker2D walker = actor.GetComponentInChildren<RoomPersonWalker2D>(true);
        return walker != null;
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
        public ProofBaseline(
            Transform scaleRoot,
            Vector3 scaleRootLocalScale,
            Transform primaryVisual,
            Vector3 primaryVisualLocalScale,
            float screenHeight,
            bool hasScreenHeight)
        {
            ScaleRoot = scaleRoot;
            ScaleRootLocalScale = scaleRootLocalScale;
            PrimaryVisual = primaryVisual;
            PrimaryVisualLocalScale = primaryVisualLocalScale;
            ScreenHeight = screenHeight;
            HasScreenHeight = hasScreenHeight;
        }

        public Transform ScaleRoot { get; }
        public Vector3 ScaleRootLocalScale { get; }
        public Transform PrimaryVisual { get; }
        public Vector3 PrimaryVisualLocalScale { get; }
        public float ScreenHeight { get; }
        public bool HasScreenHeight { get; }
    }

    private readonly struct GuestScaleTarget
    {
        public GuestScaleTarget(
            Transform boundsRoot,
            Transform scaleRoot,
            Transform primaryVisual,
            RoomProjectedEntity projectedEntity,
            RoomPersonWalker2D walker,
            ActorRoomState actor,
            string controllerType,
            string roomId,
            Vector2 roomLocalFootPoint,
            float relativeHeightMultiplier,
            float poseHeightMultiplier,
            bool isSeated,
            string visualDiagnostic)
        {
            BoundsRoot = boundsRoot;
            ScaleRoot = scaleRoot;
            PrimaryVisual = primaryVisual;
            ProjectedEntity = projectedEntity;
            Walker = walker;
            Actor = actor;
            ControllerType = controllerType;
            RoomId = string.IsNullOrWhiteSpace(roomId) ? string.Empty : roomId.Trim();
            RoomLocalFootPoint = roomLocalFootPoint;
            RelativeHeightMultiplier = Mathf.Clamp(relativeHeightMultiplier, 0.5f, 1.5f);
            PoseHeightMultiplier = Mathf.Clamp(poseHeightMultiplier <= 0f ? 1f : poseHeightMultiplier, 0.25f, 1.25f);
            IsSeated = isSeated;
            VisualDiagnostic = visualDiagnostic ?? string.Empty;
            Path = GetObjectPath(scaleRoot != null ? scaleRoot : boundsRoot);
        }

        public Transform BoundsRoot { get; }
        public Transform ScaleRoot { get; }
        public Transform PrimaryVisual { get; }
        public RoomProjectedEntity ProjectedEntity { get; }
        public RoomPersonWalker2D Walker { get; }
        public ActorRoomState Actor { get; }
        public string ControllerType { get; }
        public string RoomId { get; }
        public Vector2 RoomLocalFootPoint { get; }
        public float RelativeHeightMultiplier { get; }
        public float PoseHeightMultiplier { get; }
        public bool IsSeated { get; }
        public string VisualDiagnostic { get; }
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
