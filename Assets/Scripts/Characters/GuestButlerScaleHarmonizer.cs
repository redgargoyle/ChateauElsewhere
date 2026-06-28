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
    [SerializeField] private bool skipButlerObject = true;
    [SerializeField] private bool logSummary;
    [SerializeField, HideInInspector] private float debugGuestScaleMultiplier = 1f;

    private string lastSummaryKey = string.Empty;

    public PointClickPlayerMovement ButlerScaleSource => ResolveButlerScaleSource();
    public float DebugGuestScaleMultiplier => debugGuestScaleMultiplier;

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
        debugGuestScaleMultiplier = Mathf.Max(0.001f, multiplier);
    }

    public void RestoreRealButlerScaling()
    {
        debugGuestScaleMultiplier = 1f;
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

        GuestScaleApplySummary summary = new GuestScaleApplySummary(source.name);

        if (applyToRoomProjectedEntities)
        {
            ApplyToProjectedEntities(source, ref summary);
        }

        if (applyToRoomPersonWalkers)
        {
            ApplyToWalkers(source, ref summary);
        }

        if (logSummary)
        {
            LogSummary(summary);
        }

        return summary;
    }

    private void ApplyToProjectedEntities(PointClickPlayerMovement source, ref GuestScaleApplySummary summary)
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

            if (entity.Mode != RoomProjectedEntity.ProjectionMode.FloorCharacter)
            {
                summary.Skipped++;
                continue;
            }

            entity.SetButlerScaleSource(source, false);
            entity.SetButlerCharacterScaleRulesEnabled(true, false);
            entity.SetIgnoreRoomVisualScaleOverridesWhenUsingButlerRules(true, false);

            if (entity.TryGetButlerCharacterScaleSample(out PointClickPlayerMovement.ButlerCharacterScaleSample sample))
            {
                entity.ApplyButlerCharacterScaleNow(source, debugGuestScaleMultiplier);
                summary.Scaled++;
                summary.UsingButlerRules++;
                summary.IncludeScale(sample.NormalizedScale * debugGuestScaleMultiplier);
            }
            else
            {
                summary.MissingCalibration++;
            }
        }
    }

    private void ApplyToWalkers(PointClickPlayerMovement source, ref GuestScaleApplySummary summary)
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

            walker.SetButlerScaleSource(source, false);
            walker.SetButlerCharacterScaleRulesEnabled(true, false);
            walker.SetPreserveAuthoredLocalScaleWhenUsingButlerRules(true, false);

            if (walker.TryGetButlerCharacterScaleSample(out PointClickPlayerMovement.ButlerCharacterScaleSample sample))
            {
                walker.ApplyButlerCharacterScaleNow(source, debugGuestScaleMultiplier);
                summary.Scaled++;
                summary.UsingButlerRules++;
                summary.IncludeScale(sample.NormalizedScale * debugGuestScaleMultiplier);
            }
            else
            {
                summary.MissingCalibration++;
            }
        }
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

                if (string.Equals(candidate.gameObject.tag, "Player", System.StringComparison.OrdinalIgnoreCase))
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

    private void LogSummary(GuestScaleApplySummary summary)
    {
        string key = $"{summary.SourceName}|{summary.ProjectedFound}|{summary.WalkersFound}|{summary.Scaled}|{summary.MissingCalibration}|{summary.MinScale:0.###}|{summary.MaxScale:0.###}";

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
            $"[GuestButlerScale] scaled {summary.Scaled} guests ({summary.ProjectedFound} RoomProjectedEntity, {summary.WalkersFound} walkers), {summary.Skipped} skipped, {summary.MissingCalibration} missing calibration, source={summary.SourceName}, scale range={summary.MinScale:0.###}-{summary.MaxScale:0.###}",
            this);
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

    private static bool NameLooksLikePlayerOrButler(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            (value.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("Butler", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    public struct GuestScaleApplySummary
    {
        public static readonly GuestScaleApplySummary Empty = new GuestScaleApplySummary(string.Empty);

        public GuestScaleApplySummary(string sourceName)
        {
            SourceName = sourceName;
            ProjectedFound = 0;
            WalkersFound = 0;
            UsingButlerRules = 0;
            Skipped = 0;
            MissingCalibration = 0;
            Scaled = 0;
            MinScale = float.PositiveInfinity;
            MaxScale = 0f;
        }

        public string SourceName;
        public int ProjectedFound;
        public int WalkersFound;
        public int UsingButlerRules;
        public int Skipped;
        public int MissingCalibration;
        public int Scaled;
        public float MinScale;
        public float MaxScale;

        public void IncludeScale(float scale)
        {
            MinScale = Mathf.Min(MinScale, scale);
            MaxScale = Mathf.Max(MaxScale, scale);
        }
    }
}
