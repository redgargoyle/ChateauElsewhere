using System.Collections.Generic;
using UnityEngine;

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
        ResolveCalibration();

        if (calibration == null)
        {
            return 0;
        }

        participants.Clear();
        FindObjectsInactive inactiveMode = includeInactiveParticipants
            ? FindObjectsInactive.Include
            : FindObjectsInactive.Exclude;
        participants.AddRange(FindObjectsByType<GuestScaleParticipant>(inactiveMode));

        int applied = 0;

        for (int i = 0; i < participants.Count; i++)
        {
            if (RefreshParticipantNow(participants[i]))
            {
                applied++;
            }
        }

        return applied;
    }

    public bool RefreshParticipantNow(GuestScaleParticipant participant)
    {
        if (participant == null ||
            participant.ExcludeFromGuestScaling ||
            participant.IsButler)
        {
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

        float poseRatio = ResolvePoseRatio(participant, roomId);
        float finalMultiplier =
            Mathf.Max(0.001f, roomScale) *
            Mathf.Max(0.001f, poseRatio) *
            participant.ManualFineTuneMultiplier;

        participant.ApplyFinalScale(finalMultiplier);
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

            if (candidate == null || !LooksLikeChapterGuest(candidate.name))
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

    private float ResolvePoseRatio(GuestScaleParticipant participant, string roomId)
    {
        CharacterPose resolvedPose = participant.Pose;
        float fineTuneMultiplier = 1f;

        if (poseOverrideStore != null &&
            poseOverrideStore.TryGetOverride(
                roomId,
                participant.CharacterId,
                out CharacterPose overridePose,
                out float overridePoseRatio,
                out float overrideFineTuneMultiplier))
        {
            resolvedPose = overridePose;
            fineTuneMultiplier = Mathf.Max(0.001f, overrideFineTuneMultiplier);

            if (overridePoseRatio > 0f)
            {
                return SanitizePoseRatio(resolvedPose, overridePoseRatio) * fineTuneMultiplier;
            }
        }

        if (resolvedPose == CharacterPose.Auto)
        {
            ActorRoomState actorRoomState = participant.GetComponentInParent<ActorRoomState>(true);
            resolvedPose = actorRoomState != null && actorRoomState.IsSeated
                ? CharacterPose.Seated
                : CharacterPose.Standing;
        }

        float ratio = resolvedPose switch
        {
            CharacterPose.Seated => participant.SeatedRatioOverride > 0f ? participant.SeatedRatioOverride : 0.68f,
            CharacterPose.Crouching => 0.75f,
            CharacterPose.Lying => 0.45f,
            _ => 1f
        };

        return SanitizePoseRatio(resolvedPose, ratio) * fineTuneMultiplier;
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
        return clean.Contains("Guest ", System.StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("Guest", System.StringComparison.OrdinalIgnoreCase) ||
            clean.Contains("Walker GEH", System.StringComparison.OrdinalIgnoreCase);
    }
}
