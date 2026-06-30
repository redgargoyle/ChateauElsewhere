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
    [SerializeField, HideInInspector] private List<GuestRoomScaleMultiplier> roomGuestScaleMultipliers = new List<GuestRoomScaleMultiplier>();
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

    public float GetRoomGuestScaleMultiplier(string roomId)
    {
        int index = GetRoomGuestScaleMultiplierIndex(roomId);
        return index >= 0
            ? SanitizeMultiplier(roomGuestScaleMultipliers[index].Multiplier)
            : 1f;
    }

    public void SetRoomGuestScaleMultiplier(string roomId, float multiplier)
    {
        string cleanRoomId = CleanRoomId(roomId);

        if (string.IsNullOrWhiteSpace(cleanRoomId))
        {
            return;
        }

        if (roomGuestScaleMultipliers == null)
        {
            roomGuestScaleMultipliers = new List<GuestRoomScaleMultiplier>();
        }

        GuestRoomScaleMultiplier roomMultiplier = new GuestRoomScaleMultiplier(
            cleanRoomId,
            SanitizeMultiplier(multiplier));
        int index = GetRoomGuestScaleMultiplierIndex(cleanRoomId);

        if (index >= 0)
        {
            roomGuestScaleMultipliers[index] = roomMultiplier;
        }
        else
        {
            roomGuestScaleMultipliers.Add(roomMultiplier);
        }
    }

    public bool RemoveRoomGuestScaleMultiplier(string roomId)
    {
        int index = GetRoomGuestScaleMultiplierIndex(roomId);

        if (index < 0)
        {
            return false;
        }

        roomGuestScaleMultipliers.RemoveAt(index);
        return true;
    }

    public void GetGuestScaleMultiplierRoomIds(List<string> results)
    {
        if (results == null || roomGuestScaleMultipliers == null)
        {
            return;
        }

        for (int i = 0; i < roomGuestScaleMultipliers.Count; i++)
        {
            string roomId = roomGuestScaleMultipliers[i].RoomId;

            if (!string.IsNullOrWhiteSpace(roomId) && !ContainsRoomId(results, roomId))
            {
                results.Add(roomId);
            }
        }
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
                Debug.LogError("[GuestButlerScale] No Butler scale source found. Open Tools > Characters > Guest Room Scale and click Find Scene Player.", this);
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

        if (applyToActorRoomStates)
        {
            ApplyToActorRoomStates(source, ref summary);
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
                float multiplier = GetCombinedGuestScaleMultiplier(sample);
                entity.ApplyButlerCharacterScaleNow(source, multiplier);
                summary.Scaled++;
                summary.UsingButlerRules++;
                summary.IncludeScale(sample.ButlerFinalLocalScaleY * multiplier);
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
                float multiplier = GetCombinedGuestScaleMultiplier(sample);
                walker.ApplyButlerCharacterScaleNow(source, multiplier);
                summary.Scaled++;
                summary.UsingButlerRules++;
                summary.IncludeScale(sample.ButlerFinalLocalScaleY * multiplier);
            }
            else
            {
                summary.MissingCalibration++;
            }
        }
    }

    private void ApplyToActorRoomStates(PointClickPlayerMovement source, ref GuestScaleApplySummary summary)
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

            if (actor.TryGetButlerCharacterScaleSample(source, out PointClickPlayerMovement.ButlerCharacterScaleSample sample))
            {
                float multiplier = GetCombinedGuestScaleMultiplier(sample);

                if (actor.ApplyButlerCharacterScaleNow(source, multiplier))
                {
                    summary.Scaled++;
                    summary.UsingButlerRules++;
                    summary.IncludeScale(sample.ButlerFinalLocalScaleY * multiplier);
                }
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

    private float GetCombinedGuestScaleMultiplier(PointClickPlayerMovement.ButlerCharacterScaleSample sample)
    {
        return SanitizeMultiplier(debugGuestScaleMultiplier) *
            GetRoomGuestScaleMultiplier(sample.RoomId);
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
        string key = $"{summary.SourceName}|{summary.ProjectedFound}|{summary.WalkersFound}|{summary.ActorStatesFound}|{summary.Scaled}|{summary.MissingCalibration}|{summary.MinScale:0.###}|{summary.MaxScale:0.###}";

        if (key == lastSummaryKey)
        {
            return;
        }

        lastSummaryKey = key;

        if (summary.Scaled <= 0)
        {
            Debug.LogError("[GuestButlerScale] No guests were scaled. Open Tools > Characters > Guest Room Scale and click Auto Setup + Apply Now.", this);
            return;
        }

        Debug.Log(
            $"[GuestButlerScale] scaled {summary.Scaled} guests ({summary.ProjectedFound} RoomProjectedEntity, {summary.WalkersFound} walkers, {summary.ActorStatesFound} actor states), {summary.Skipped} skipped, {summary.MissingCalibration} missing calibration, source={summary.SourceName}, scale range={summary.MinScale:0.###}-{summary.MaxScale:0.###}",
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
            value.IndexOf("Guest", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private int GetRoomGuestScaleMultiplierIndex(string roomId)
    {
        if (roomGuestScaleMultipliers == null || string.IsNullOrWhiteSpace(roomId))
        {
            return -1;
        }

        string cleanRoomId = CleanRoomId(roomId);

        for (int i = 0; i < roomGuestScaleMultipliers.Count; i++)
        {
            if (SameRoom(roomGuestScaleMultipliers[i].RoomId, cleanRoomId))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool ContainsRoomId(List<string> roomIds, string roomId)
    {
        if (roomIds == null || string.IsNullOrWhiteSpace(roomId))
        {
            return false;
        }

        for (int i = 0; i < roomIds.Count; i++)
        {
            if (SameRoom(roomIds[i], roomId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SameRoom(string left, string right)
    {
        return string.Equals(NormalizeRoomName(left), NormalizeRoomName(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRoomName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim()
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty);
    }

    private static string CleanRoomId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static float SanitizeMultiplier(float multiplier)
    {
        return Mathf.Max(0.001f, multiplier);
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
            Scaled = 0;
            MinScale = float.PositiveInfinity;
            MaxScale = 0f;
        }

        public string SourceName;
        public int ProjectedFound;
        public int WalkersFound;
        public int ActorStatesFound;
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

    [Serializable]
    private sealed class GuestRoomScaleMultiplier
    {
        [SerializeField] private string roomId;
        [SerializeField] private float multiplier = 1f;

        public GuestRoomScaleMultiplier(string roomId, float multiplier)
        {
            this.roomId = CleanRoomId(roomId);
            this.multiplier = SanitizeMultiplier(multiplier);
        }

        public string RoomId => roomId;
        public float Multiplier => SanitizeMultiplier(multiplier);
    }
}
