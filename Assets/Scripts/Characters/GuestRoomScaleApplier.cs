using System.Collections.Generic;
using UnityEngine;

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

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Guest Room Scale Applier")]
public sealed class GuestRoomScaleApplier : MonoBehaviour
{
    [SerializeField] private GuestRoomScaleCalibration calibration;
    [SerializeField] private GuestPoseScaleOverrideStore poseOverrideStore;
    [SerializeField] private bool includeInactiveParticipants = true;

    private readonly List<GuestScaleParticipant> participants = new List<GuestScaleParticipant>();

    public GuestRoomScaleCalibration Calibration => calibration;

    private void LateUpdate()
    {
        if (Application.isPlaying)
        {
            RefreshAllNow();
        }
    }

    public void SetCalibration(GuestRoomScaleCalibration value)
    {
        calibration = value;
    }

    public void SetPoseOverrideStore(GuestPoseScaleOverrideStore value)
    {
        poseOverrideStore = value;
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
        CharacterPose pose = CharacterPose.Standing)
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

        if (!string.IsNullOrWhiteSpace(roomId))
        {
            participant.SetRoomIdOverride(roomId);
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

            if (participant == null || !GuestRoomScaleCalibration.SameRoom(participant.ResolveRoomId(), roomId))
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
        }

        return new GuestScaleApplyResult(applied, changed);
    }

    public bool RefreshParticipantNow(GuestScaleParticipant participant)
    {
        return RefreshParticipantNow(participant, out _);
    }

    public bool RefreshParticipantNow(GuestScaleParticipant participant, out bool changed)
    {
        if (participant == null ||
            IsGuestScaleInfrastructureObject(participant.gameObject) ||
            participant.ExcludeFromGuestScaling ||
            participant.IsButler)
        {
            changed = false;
            return false;
        }

        string roomId = participant.ResolveRoomId();
        float roomLocalY = participant.ResolveRoomLocalY();

        float roomScale = 1f;

        if (calibration != null &&
            calibration.TryEvaluateGuestScale(roomId, roomLocalY, out float evaluatedScale, out _, out _))
        {
            roomScale = evaluatedScale;
        }

        float finalMultiplier =
            Mathf.Max(0.001f, roomScale) *
            ResolveExplicitPoseRatio(participant, roomId) *
            participant.ManualFineTuneMultiplier;

        changed = participant.ApplyFinalScale(finalMultiplier);
        return true;
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
            if (walkers[i] == null)
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

            if (entity == null || entity.Mode != RoomProjectedEntity.ProjectionMode.FloorCharacter)
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

    private float ResolveExplicitPoseRatio(GuestScaleParticipant participant, string roomId)
    {
        float fineTuneMultiplier = 1f;

        if (poseOverrideStore != null &&
            poseOverrideStore.TryGetOverride(
                roomId,
                participant.CharacterId,
                out CharacterPose overridePose,
                out float overridePoseRatio,
                out float overrideFineTuneMultiplier))
        {
            fineTuneMultiplier = Mathf.Max(0.001f, overrideFineTuneMultiplier);

            if (overridePoseRatio > 0f)
            {
                return SanitizePoseRatio(overridePose, overridePoseRatio) * fineTuneMultiplier;
            }
        }

        return fineTuneMultiplier;
    }

    private void ResolveCalibration()
    {
        if (calibration == null)
        {
            calibration = FindAnyObjectByType<GuestRoomScaleCalibration>(FindObjectsInactive.Include);
        }

        if (poseOverrideStore == null)
        {
            poseOverrideStore = FindAnyObjectByType<GuestPoseScaleOverrideStore>(FindObjectsInactive.Include);
        }
    }

    private void RefreshParticipantList()
    {
        participants.Clear();
        FindObjectsInactive inactiveMode = includeInactiveParticipants
            ? FindObjectsInactive.Include
            : FindObjectsInactive.Exclude;
        participants.AddRange(FindObjectsByType<GuestScaleParticipant>(inactiveMode));
    }

    private static float SanitizePoseRatio(CharacterPose pose, float ratio)
    {
        if (pose == CharacterPose.Seated)
        {
            return Mathf.Clamp(ratio, 0.55f, 0.8f);
        }

        return Mathf.Max(0.001f, ratio);
    }

    private static bool LooksLikeChapterGuest(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string clean = value.Replace("_", " ");
        return StartsWithGuestNumber(clean) ||
            clean.Contains("Walker GEH", System.StringComparison.OrdinalIgnoreCase);
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
            candidate.GetComponent<GuestPoseScaleOverrideStore>() != null ||
            candidate.name.Contains("GuestRoomScale", System.StringComparison.OrdinalIgnoreCase) ||
            candidate.name.Contains("GuestScale", System.StringComparison.OrdinalIgnoreCase) ||
            candidate.name.Contains("GuestArrival", System.StringComparison.OrdinalIgnoreCase) ||
            candidate.name.Contains("GuestDrawingRoomDoorTarget", System.StringComparison.OrdinalIgnoreCase));
    }
}
