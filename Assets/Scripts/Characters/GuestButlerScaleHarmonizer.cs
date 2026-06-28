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

    public int EmergencyClampHugeGuestScales(float maxAbsScale = 20f)
    {
        Camera camera = ResolveCamera();
        PointClickPlayerMovement source = ResolveButlerScaleSource();
        GuestScaleApplySummary ignoredSummary = GuestScaleApplySummary.Empty;
        List<GuestScaleTarget> targets = BuildGuestScaleTargets(source, camera, ref ignoredSummary);
        int clamped = 0;

        for (int i = 0; i < targets.Count; i++)
        {
            clamped += ClampHugeScale(targets[i].ScaleRoot, maxAbsScale);

            if (targets[i].PrimaryVisualRoot != targets[i].ScaleRoot)
            {
                clamped += ClampHugeScale(targets[i].PrimaryVisualRoot, maxAbsScale);
            }
        }

        return clamped;
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
        List<GuestScaleTarget> targets = BuildGuestScaleTargets(source, camera, ref summary);
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
        List<GuestScaleTarget> targets = BuildGuestScaleTargets(source, camera, ref ignoredSummary);
        CharacterVisualBoundsUtility.VisualBoundsOptions options = GetBoundsOptions();

        for (int i = 0; i < targets.Count; i++)
        {
            GuestScaleTarget target = targets[i];
            Transform key = target.ScaleRoot != null ? target.ScaleRoot : target.PrimaryVisualRoot;

            if (key == null)
            {
                continue;
            }

            bool hasHeight = CharacterVisualBoundsUtility.TryGetVisualScreenRect(
                target.BoundsRoot,
                camera,
                out Rect screenRect,
                out Transform primaryVisual,
                out _,
                options);
            Transform primary = primaryVisual != null ? primaryVisual : target.PrimaryVisualRoot;
            proofBaselines[key] = new ProofBaseline(
                target.ScaleRoot,
                target.ScaleRoot != null ? target.ScaleRoot.localScale : Vector3.one,
                primary,
                primary != null ? primary.localScale : Vector3.one,
                hasHeight ? screenRect.height : 0f,
                hasHeight);
        }
    }

    private void RestoreProofBaselines()
    {
        foreach (KeyValuePair<Transform, ProofBaseline> pair in proofBaselines)
        {
            ProofBaseline baseline = pair.Value;

            if (baseline.ScaleRoot != null)
            {
                baseline.ScaleRoot.localScale = baseline.ScaleRootLocalScale;
            }

            if (baseline.PrimaryVisualRoot != null && baseline.PrimaryVisualRoot != baseline.ScaleRoot)
            {
                baseline.PrimaryVisualRoot.localScale = baseline.PrimaryVisualLocalScale;
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
        if (target.ScaleRoot == null && target.PrimaryVisualRoot == null)
        {
            summary.FitFailed++;
            summary.AddProofFailure(target.Path, target.BuildDiagnostic("No visual scale root found"));
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

        CharacterVisualBoundsUtility.VisualBoundsOptions options = GetBoundsOptions();

        if (!CharacterVisualBoundsUtility.TryGetVisualScreenRect(
                target.BoundsRoot,
                camera,
                out Rect currentRect,
                out Transform measuredPrimary,
                out string measureDiagnostic,
                options))
        {
            if (proofMode && target.PrimaryVisualRoot != null && ApplyProofDirectScale(target))
            {
                summary.Scaled++;
                summary.IncludeScale(debugGuestScaleMultiplier);
                summary.AddFitDiagnosticText(target.Path, target.BuildDiagnostic("Fallback direct visual scale after missing measured bounds"));
                return;
            }

            summary.NoVisualBounds++;
            summary.AddProofFailure(target.Path, target.BuildDiagnostic(measureDiagnostic));
            return;
        }

        GuestScaleTarget measuredTarget = target.WithPrimaryVisual(measuredPrimary);

        if (!TryGetTargetScreenHeight(
                measuredTarget,
                hasSample,
                sample,
                hasButlerReferenceHeight,
                butlerReferenceScreenHeight,
                proofMode,
                currentRect.height,
                out float targetHeight,
                out string failureReason))
        {
            summary.FitFailed++;
            summary.AddProofFailure(target.Path, measuredTarget.BuildDiagnostic(failureReason));
            return;
        }

        CharacterVisualBoundsUtility.VisualFitResult fitResult;
        bool fitSucceeded = TryFitTarget(measuredTarget, camera, targetHeight, options, out fitResult);

        if (!fitSucceeded && proofMode && ApplyProofDirectScale(measuredTarget))
        {
            summary.Scaled++;
            summary.IncludeScale(debugGuestScaleMultiplier);
            summary.IncludeVisualHeight(fitResult.AfterHeight);
            summary.AddFitDiagnosticText(measuredTarget.Path, measuredTarget.BuildDiagnostic("Fallback direct visual scale after fitter miss", fitResult));
            return;
        }

        if (!fitSucceeded)
        {
            summary.FitFailed++;
            summary.AddProofFailure(measuredTarget.Path, measuredTarget.BuildDiagnostic(fitResult.Diagnostic, fitResult));
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
        Transform fittedScaleRoot = measuredTarget.ScaleRoot != null
            ? measuredTarget.ScaleRoot
            : measuredTarget.PrimaryVisualRoot;

        if (fittedScaleRoot != null)
        {
            lastFittedLocalScales[fittedScaleRoot] = fittedScaleRoot.localScale;
        }

        summary.AddFitDiagnostic(measuredTarget.Path, fitResult);
    }

    private bool TryFitTarget(
        GuestScaleTarget target,
        Camera camera,
        float targetHeight,
        CharacterVisualBoundsUtility.VisualBoundsOptions options,
        out CharacterVisualBoundsUtility.VisualFitResult result)
    {
        Transform scaleRoot = target.ScaleRoot != null ? target.ScaleRoot : target.PrimaryVisualRoot;

        if (LooksLikeForbiddenScaleRoot(scaleRoot) && target.PrimaryVisualRoot != null)
        {
            scaleRoot = target.PrimaryVisualRoot;
        }

        if (CharacterVisualBoundsUtility.TryScaleTransformForScreenHeight(
                scaleRoot,
                target.BoundsRoot,
                camera,
                targetHeight,
                out result,
                options,
                2))
        {
            return true;
        }

        if (target.PrimaryVisualRoot != null && target.PrimaryVisualRoot != scaleRoot)
        {
            return CharacterVisualBoundsUtility.TryScaleTransformForScreenHeight(
                target.PrimaryVisualRoot,
                target.BoundsRoot,
                camera,
                targetHeight,
                out result,
                options,
                2);
        }

        return false;
    }

    private bool ApplyProofDirectScale(GuestScaleTarget target)
    {
        Transform key = target.ScaleRoot != null ? target.ScaleRoot : target.PrimaryVisualRoot;
        Transform scaleRoot = target.PrimaryVisualRoot != null && (target.ScaleRoot == null || LooksLikeForbiddenScaleRoot(target.ScaleRoot))
            ? target.PrimaryVisualRoot
            : target.ScaleRoot;

        if (scaleRoot == null)
        {
            return false;
        }

        if (proofBaselines.TryGetValue(key, out ProofBaseline baseline))
        {
            if (scaleRoot == baseline.PrimaryVisualRoot)
            {
                scaleRoot.localScale = ScaleBaseline(baseline.PrimaryVisualLocalScale, debugGuestScaleMultiplier);
                return !Approximately(scaleRoot.localScale, baseline.PrimaryVisualLocalScale);
            }

            scaleRoot.localScale = ScaleBaseline(baseline.ScaleRootLocalScale, debugGuestScaleMultiplier);
            return !Approximately(scaleRoot.localScale, baseline.ScaleRootLocalScale);
        }

        Vector3 previous = scaleRoot.localScale;
        scaleRoot.localScale = ScaleBaseline(previous, debugGuestScaleMultiplier);
        return !Approximately(previous, scaleRoot.localScale);
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

        Transform key = target.ScaleRoot != null ? target.ScaleRoot : target.PrimaryVisualRoot;

        if (key != null &&
            proofBaselines.TryGetValue(key, out ProofBaseline baseline) &&
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
        Camera camera,
        ref GuestScaleApplySummary summary)
    {
        List<GuestScaleTarget> targets = new List<GuestScaleTarget>();
        HashSet<Transform> claimedRoots = new HashSet<Transform>();
        CharacterVisualBoundsUtility.VisualBoundsOptions options = GetBoundsOptions();

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
                    CreateTarget(
                        entity.GetGuestScaleRoot(),
                        entity.GetGuestBoundsRoot(),
                        entity.GetGuestScaleRoot(),
                        entity,
                        null,
                        null,
                        "RoomProjectedEntity",
                        roomId,
                        footPoint,
                        entity.GetGuestRelativeHeightMultiplier(),
                        camera,
                        options),
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
                    CreateTarget(
                        walker.GetGuestScaleRoot(),
                        walker.GetGuestBoundsRoot(),
                        walker.GetGuestScaleRoot(),
                        null,
                        walker,
                        null,
                        "RoomPersonWalker2D",
                        roomId,
                        footPoint,
                        walker.GetGuestRelativeHeightMultiplier(),
                        camera,
                        options),
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

                if (!LooksLikeGuestActor(actor) || actor.HasGuestScaleControllerChild())
                {
                    summary.Skipped++;
                    continue;
                }

                if (!actor.TryResolveGuestRoomAndFootPoint(out string roomId, out Vector2 footPoint))
                {
                    summary.Skipped++;
                    continue;
                }

                Transform actorRoot = actor.GetGuestScaleRoot();
                AddTarget(
                    targets,
                    claimedRoots,
                    CreateTarget(
                        actorRoot,
                        actor.GetGuestBoundsRoot(),
                        actorRoot,
                        null,
                        null,
                        actor,
                        "ActorRoomState",
                        roomId,
                        footPoint,
                        actor.GetGuestRelativeHeightMultiplier(),
                        camera,
                        options),
                    source,
                    ref summary);
            }
        }

        return targets;
    }

    private GuestScaleTarget CreateTarget(
        Transform scaleRoot,
        Transform boundsRoot,
        Transform defaultPrimaryRoot,
        RoomProjectedEntity projectedEntity,
        RoomPersonWalker2D walker,
        ActorRoomState actor,
        string controllerType,
        string roomId,
        Vector2 roomLocalFootPoint,
        float relativeHeightMultiplier,
        Camera camera,
        CharacterVisualBoundsUtility.VisualBoundsOptions options)
    {
        Transform safeBoundsRoot = boundsRoot != null ? boundsRoot : scaleRoot;
        Transform primaryVisual = null;
        string visualDiagnostic = string.Empty;

        if (safeBoundsRoot != null &&
            CharacterVisualBoundsUtility.TryGetVisualScreenRect(
                safeBoundsRoot,
                camera,
                out _,
                out Transform measuredPrimary,
                out visualDiagnostic,
                options) &&
            measuredPrimary != null)
        {
            primaryVisual = measuredPrimary;
        }

        Transform safeScaleRoot = scaleRoot != null ? scaleRoot : primaryVisual != null ? primaryVisual : defaultPrimaryRoot;

        if (LooksLikeForbiddenScaleRoot(safeScaleRoot) && primaryVisual != null)
        {
            safeScaleRoot = primaryVisual;
        }

        return new GuestScaleTarget(
            safeScaleRoot,
            safeBoundsRoot,
            primaryVisual,
            projectedEntity,
            walker,
            actor,
            controllerType,
            roomId,
            roomLocalFootPoint,
            relativeHeightMultiplier,
            visualDiagnostic);
    }

    private void AddTarget(
        List<GuestScaleTarget> targets,
        HashSet<Transform> claimedRoots,
        GuestScaleTarget target,
        PointClickPlayerMovement source,
        ref GuestScaleApplySummary summary)
    {
        if (target.BoundsRoot == null && target.ScaleRoot == null && target.PrimaryVisualRoot == null)
        {
            summary.Skipped++;
            return;
        }

        if ((skipButlerObject && IsButlerObjectOrChild(target.ScaleRoot, source)) ||
            (skipButlerObject && IsButlerObjectOrChild(target.BoundsRoot, source)) ||
            (skipButlerObject && IsButlerObjectOrChild(target.PrimaryVisualRoot, source)))
        {
            summary.Skipped++;
            return;
        }

        if ((target.ScaleRoot != null && claimedRoots.Contains(target.ScaleRoot)) ||
            (target.BoundsRoot != null && claimedRoots.Contains(target.BoundsRoot)) ||
            (target.PrimaryVisualRoot != null && claimedRoots.Contains(target.PrimaryVisualRoot)))
        {
            summary.Skipped++;
            return;
        }

        if (target.ScaleRoot != null)
        {
            claimedRoots.Add(target.ScaleRoot);
        }

        if (target.BoundsRoot != null)
        {
            claimedRoots.Add(target.BoundsRoot);
        }

        if (target.PrimaryVisualRoot != null)
        {
            claimedRoots.Add(target.PrimaryVisualRoot);
        }

        targets.Add(target);
    }

    private CharacterVisualBoundsUtility.VisualBoundsOptions GetBoundsOptions()
    {
        CharacterVisualBoundsUtility.VisualBoundsOptions options = CharacterVisualBoundsUtility.VisualBoundsOptions.Default;
        options.includeInactive = !Application.isPlaying && includeInactiveInEditMode;
        return options;
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
            $"[GuestButlerScale] Another script changed guest scale after harmonizer: {target.Path}",
            target.ScaleRoot);
    }

    private static int ClampHugeScale(Transform target, float maxAbsScale)
    {
        if (target == null)
        {
            return 0;
        }

        Vector3 scale = target.localScale;

        if (Mathf.Abs(scale.x) <= maxAbsScale && Mathf.Abs(scale.y) <= maxAbsScale)
        {
            return 0;
        }

        target.localScale = new Vector3(
            Mathf.Sign(Mathf.Approximately(scale.x, 0f) ? 1f : scale.x),
            Mathf.Sign(Mathf.Approximately(scale.y, 0f) ? 1f : scale.y),
            scale.z);
        Debug.LogWarning($"[GuestButlerScale] Clamped huge guest scale on {GetObjectPath(target)} from {scale} to {target.localScale}.", target);
        return 1;
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

    private static Vector3 ScaleBaseline(Vector3 baseline, float multiplier)
    {
        float safeMultiplier = Mathf.Max(0.001f, multiplier);
        return new Vector3(
            baseline.x * safeMultiplier,
            baseline.y * safeMultiplier,
            baseline.z);
    }

    private static bool LooksLikeForbiddenScaleRoot(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        string name = target.name;
        return string.Equals(name, "Canvas", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Canvas_Background", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Rooms", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "People", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Room_", StringComparison.OrdinalIgnoreCase) ||
            target.GetComponent<RoomContentGroup>() != null;
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
            Transform primaryVisualRoot,
            Vector3 primaryVisualLocalScale,
            float screenHeight,
            bool hasScreenHeight)
        {
            ScaleRoot = scaleRoot;
            ScaleRootLocalScale = scaleRootLocalScale;
            PrimaryVisualRoot = primaryVisualRoot;
            PrimaryVisualLocalScale = primaryVisualLocalScale;
            ScreenHeight = screenHeight;
            HasScreenHeight = hasScreenHeight;
        }

        public Transform ScaleRoot { get; }
        public Vector3 ScaleRootLocalScale { get; }
        public Transform PrimaryVisualRoot { get; }
        public Vector3 PrimaryVisualLocalScale { get; }
        public float ScreenHeight { get; }
        public bool HasScreenHeight { get; }
    }

    private readonly struct GuestScaleTarget
    {
        public GuestScaleTarget(
            Transform scaleRoot,
            Transform boundsRoot,
            Transform primaryVisualRoot,
            RoomProjectedEntity projectedEntity,
            RoomPersonWalker2D walker,
            ActorRoomState actor,
            string controllerType,
            string roomId,
            Vector2 roomLocalFootPoint,
            float relativeHeightMultiplier,
            string visualDiagnostic)
        {
            ScaleRoot = scaleRoot;
            BoundsRoot = boundsRoot;
            PrimaryVisualRoot = primaryVisualRoot;
            ProjectedEntity = projectedEntity;
            Walker = walker;
            Actor = actor;
            ControllerType = controllerType;
            RoomId = string.IsNullOrWhiteSpace(roomId) ? string.Empty : roomId.Trim();
            RoomLocalFootPoint = roomLocalFootPoint;
            RelativeHeightMultiplier = Mathf.Clamp(relativeHeightMultiplier, 0.5f, 1.5f);
            VisualDiagnostic = visualDiagnostic;
            Path = GetObjectPath(scaleRoot != null ? scaleRoot : boundsRoot != null ? boundsRoot : primaryVisualRoot);
        }

        public Transform ScaleRoot { get; }
        public Transform BoundsRoot { get; }
        public Transform PrimaryVisualRoot { get; }
        public RoomProjectedEntity ProjectedEntity { get; }
        public RoomPersonWalker2D Walker { get; }
        public ActorRoomState Actor { get; }
        public string ControllerType { get; }
        public string RoomId { get; }
        public Vector2 RoomLocalFootPoint { get; }
        public float RelativeHeightMultiplier { get; }
        public string VisualDiagnostic { get; }
        public string Path { get; }

        public GuestScaleTarget WithPrimaryVisual(Transform primaryVisualRoot)
        {
            return new GuestScaleTarget(
                ScaleRoot,
                BoundsRoot,
                primaryVisualRoot != null ? primaryVisualRoot : PrimaryVisualRoot,
                ProjectedEntity,
                Walker,
                Actor,
                ControllerType,
                RoomId,
                RoomLocalFootPoint,
                RelativeHeightMultiplier,
                VisualDiagnostic);
        }

        public string BuildDiagnostic(string reason)
        {
            return $"{reason}; controller={ControllerType}; boundsRoot={GetObjectPath(BoundsRoot)}; scaleRoot={GetObjectPath(ScaleRoot)}; primaryVisual={GetObjectPath(PrimaryVisualRoot)}; room={RoomId}; initialDiagnostic={VisualDiagnostic}";
        }

        public string BuildDiagnostic(string reason, CharacterVisualBoundsUtility.VisualFitResult result)
        {
            return $"{BuildDiagnostic(reason)}; before={result.BeforeHeight:0.##}; target={result.TargetHeight:0.##}; after={result.AfterHeight:0.##}; fit={result.Diagnostic}; fitScaleRoot={result.ScaleRootPath}; fitBoundsRoot={result.BoundsRootPath}; fitPrimary={result.PrimaryVisualPath}";
        }
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
            FitDiagnostics = new List<string>();
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
        public List<string> FitDiagnostics;

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

        public void AddFitDiagnostic(string path, CharacterVisualBoundsUtility.VisualFitResult result)
        {
            if (FitDiagnostics == null)
            {
                FitDiagnostics = new List<string>();
            }

            FitDiagnostics.Add(
                $"{path}: {(result.UsedFallbackDirectScale ? "Fallback direct visual scale" : "Exact visual fit")} before={result.BeforeHeight:0.##} target={result.TargetHeight:0.##} after={result.AfterHeight:0.##} scaleRoot={result.ScaleRootPath} boundsRoot={result.BoundsRootPath} primaryVisual={result.PrimaryVisualPath} diagnostic={result.Diagnostic}");
        }

        public void AddFitDiagnosticText(string path, string diagnostic)
        {
            if (FitDiagnostics == null)
            {
                FitDiagnostics = new List<string>();
            }

            FitDiagnostics.Add($"{path}: {diagnostic}");
        }
    }
}
