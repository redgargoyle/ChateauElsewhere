using System;
using UnityEngine;
using UnityEngine.UI;

public enum CharacterPose
{
    Auto,
    Standing,
    Seated,
    Crouching,
    Lying
}

public readonly struct GuestRoomResolutionTrace
{
    public GuestRoomResolutionTrace(
        string characterId,
        string objectPath,
        bool activeInHierarchy,
        string currentRoomId,
        string actorRoomStateRoomId,
        string projectedCurrentVisualScaleRoomId,
        string projectedRoomProfileRoomId,
        string parentRoomContentRoomName,
        string walkerRoomProfileRoomId,
        string activeNavigationRoomId,
        string roomIdOverride,
        string authoredNameInferenceRoomId,
        string finalRoomId,
        string finalSource,
        bool includedInSelectedRoom,
        string exclusionReason)
    {
        CharacterId = characterId;
        ObjectPath = objectPath;
        ActiveInHierarchy = activeInHierarchy;
        CurrentRoomId = currentRoomId;
        ActorRoomStateRoomId = actorRoomStateRoomId;
        ProjectedCurrentVisualScaleRoomId = projectedCurrentVisualScaleRoomId;
        ProjectedRoomProfileRoomId = projectedRoomProfileRoomId;
        ParentRoomContentRoomName = parentRoomContentRoomName;
        WalkerRoomProfileRoomId = walkerRoomProfileRoomId;
        ActiveNavigationRoomId = activeNavigationRoomId;
        RoomIdOverride = roomIdOverride;
        AuthoredNameInferenceRoomId = authoredNameInferenceRoomId;
        FinalRoomId = finalRoomId;
        FinalSource = finalSource;
        IncludedInSelectedRoom = includedInSelectedRoom;
        ExclusionReason = exclusionReason;
    }

    public readonly string CharacterId;
    public readonly string ObjectPath;
    public readonly bool ActiveInHierarchy;
    public readonly string CurrentRoomId;
    public readonly string ActorRoomStateRoomId;
    public readonly string ProjectedCurrentVisualScaleRoomId;
    public readonly string ProjectedRoomProfileRoomId;
    public readonly string ParentRoomContentRoomName;
    public readonly string WalkerRoomProfileRoomId;
    public readonly string ActiveNavigationRoomId;
    public readonly string RoomIdOverride;
    public readonly string AuthoredNameInferenceRoomId;
    public readonly string FinalRoomId;
    public readonly string FinalSource;
    public readonly bool IncludedInSelectedRoom;
    public readonly string ExclusionReason;
}

[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Guest Scale Participant")]
public sealed class GuestScaleParticipant : MonoBehaviour
{
    [SerializeField] private string characterId;
    [SerializeField] private string currentRoomId;
    [SerializeField] private string roomIdOverride;
    [SerializeField, HideInInspector] private string lastRoomResolutionSource;
    [SerializeField] private CharacterPose pose = CharacterPose.Auto;
    [SerializeField] private Transform scaleRoot;
    [SerializeField] private Transform bodyRoot;
    [SerializeField] private bool excludeFromGuestScaling;
    [SerializeField] private bool isButler;
    [SerializeField, Min(0.001f)] private float manualFineTuneMultiplier = 1f;
    [SerializeField] private Vector3 capturedBaseScale = Vector3.one;
    [SerializeField] private bool hasCapturedBaseScale;

    public string CharacterId => string.IsNullOrWhiteSpace(characterId) ? gameObject.name : characterId.Trim();
    public string CurrentRoomId => currentRoomId;
    public string RoomIdOverride => roomIdOverride;
    public string LastRoomResolutionSource => lastRoomResolutionSource;
    public CharacterPose Pose => pose;
    public Transform ScaleRoot => scaleRoot;
    public Transform BodyRoot => bodyRoot;
    public bool ExcludeFromGuestScaling => excludeFromGuestScaling;
    public bool IsButler => isButler;
    public float ManualFineTuneMultiplier => Mathf.Max(0.001f, manualFineTuneMultiplier);
    public Vector3 CapturedBaseScale => capturedBaseScale;
    public bool HasCapturedBaseScale => hasCapturedBaseScale;

    private void Reset()
    {
        characterId = gameObject.name;
        isButler = LooksLikeButler(gameObject);
        ResolveScaleRoot();
        CaptureBaseScale(true);
    }

    private void OnValidate()
    {
        manualFineTuneMultiplier = Mathf.Max(0.001f, manualFineTuneMultiplier);
    }

    public void SetCharacterId(string value)
    {
        characterId = string.IsNullOrWhiteSpace(value) ? gameObject.name : value.Trim();
    }

    public void SetCurrentRoomId(string value)
    {
        currentRoomId = GuestRoomScaleCalibration.CleanRoomId(value);
    }

    public void ClearCurrentRoomId()
    {
        currentRoomId = string.Empty;
    }

    public void SetRoomIdOverride(string value)
    {
        roomIdOverride = GuestRoomScaleCalibration.CleanRoomId(value);
    }

    public void SetPose(CharacterPose value)
    {
        pose = value;
    }

    public void SetScaleRoot(Transform value)
    {
        scaleRoot = IsUsableBodyTransform(value) ? value : transform;
    }

    public void SetBodyRoot(Transform value)
    {
        bodyRoot = IsUsableBodyTransform(value) ? value : null;
    }

    public void SetExcludedFromGuestScaling(bool value)
    {
        excludeFromGuestScaling = value;
    }

    public void SetIsButler(bool value)
    {
        isButler = value;
    }

    public void SetManualFineTuneMultiplier(float value)
    {
        manualFineTuneMultiplier = Mathf.Max(0.001f, value);
    }

    public Transform ResolveScaleRoot()
    {
        if (IsUsableBodyTransform(scaleRoot))
        {
            return scaleRoot;
        }

        RoomPersonWalker2D walker = GetComponent<RoomPersonWalker2D>();

        if (walker != null)
        {
            Graphic graphic = walker.TargetGraphic != null ? walker.TargetGraphic : GetComponentInChildren<Graphic>(true);

            if (graphic != null && IsUsableBodyTransform(graphic.rectTransform))
            {
                scaleRoot = graphic.rectTransform;
                return scaleRoot;
            }
        }

        RoomProjectedEntity projectedEntity = GetComponent<RoomProjectedEntity>();

        if (projectedEntity != null &&
            projectedEntity.Mode == RoomProjectedEntity.ProjectionMode.FloorCharacter &&
            IsUsableBodyTransform(projectedEntity.VisualRoot))
        {
            scaleRoot = projectedEntity.VisualRoot;
            return scaleRoot;
        }

        if (IsUsableBodyTransform(bodyRoot))
        {
            scaleRoot = bodyRoot;
            return scaleRoot;
        }

        scaleRoot = transform;
        return scaleRoot;
    }

    public string ResolveRoomId()
    {
        return ResolveCurrentRoomId();
    }

    public string ResolveRoomIdForScaleContext(string selectedRoom)
    {
        string cleanSelectedRoom = GuestRoomScaleCalibration.CleanRoomId(selectedRoom);

        if (!Application.isPlaying &&
            !string.IsNullOrWhiteSpace(cleanSelectedRoom) &&
            IsActiveVisibleManagedChapterGuest())
        {
            lastRoomResolutionSource = "SelectedRoomManualContext";
            return cleanSelectedRoom;
        }

        return ResolveCurrentRoomId();
    }

    public string ResolveCurrentRoomId()
    {
        if (TryResolveCurrentRoomId(out string roomId, out string source))
        {
            lastRoomResolutionSource = source;
            return roomId;
        }

        lastRoomResolutionSource = string.Empty;
        return string.Empty;
    }

    public GuestRoomResolutionTrace BuildRoomResolutionTrace(string selectedRoom)
    {
        string cleanSelectedRoom = GuestRoomScaleCalibration.CleanRoomId(selectedRoom);
        string cleanCurrentRoomId = GuestRoomScaleCalibration.CleanRoomId(currentRoomId);
        bool hasActorRoom = TryResolveActorRoomId(out string actorRoomId);
        bool hasProjectedCurrentRoom = TryResolveProjectedCurrentVisualScaleRoomId(out string projectedCurrentRoomId);
        bool hasProjectedProfileRoom = TryResolveProjectedProfileRoomId(out string projectedProfileRoomId);
        bool hasParentRoom = TryResolveParentRoomId(out string parentRoomId);
        bool hasWalkerRoom = TryResolveWalkerRoomId(out string walkerRoomId);
        bool hasActiveNavigationRoom = TryResolveActiveNavigationRoomId(out string activeNavigationRoomId);
        string cleanOverrideRoomId = GuestRoomScaleCalibration.CleanRoomId(roomIdOverride);
        bool hasAuthoredNameRoom = GuestRoomScaleApplier.TryInferChapterGuestNameRoomId(gameObject.name, out string authoredNameRoomId);
        string finalRoomId = ResolveRoomIdForScaleContext(cleanSelectedRoom);
        bool included = !string.IsNullOrWhiteSpace(cleanSelectedRoom) &&
            GuestRoomScaleCalibration.SameRoom(finalRoomId, cleanSelectedRoom) &&
            GuestRoomScaleApplier.IsManagedGuestParticipant(this);
        string exclusionReason = BuildRoomFilterExclusionReason(cleanSelectedRoom, finalRoomId);

        return new GuestRoomResolutionTrace(
            CharacterId,
            GetTransformPath(transform),
            gameObject.activeInHierarchy,
            cleanCurrentRoomId,
            hasActorRoom ? actorRoomId : string.Empty,
            hasProjectedCurrentRoom ? projectedCurrentRoomId : string.Empty,
            hasProjectedProfileRoom ? projectedProfileRoomId : string.Empty,
            hasParentRoom ? parentRoomId : string.Empty,
            hasWalkerRoom ? walkerRoomId : string.Empty,
            hasActiveNavigationRoom ? activeNavigationRoomId : string.Empty,
            cleanOverrideRoomId,
            hasAuthoredNameRoom ? authoredNameRoomId : string.Empty,
            finalRoomId,
            lastRoomResolutionSource,
            included,
            included ? string.Empty : exclusionReason);
    }

    private bool TryResolveCurrentRoomId(out string roomId, out string source)
    {
        if (TryResolveParentRoomId(out roomId))
        {
            source = "ParentRoomContent";
            return true;
        }

        if (TryResolveProjectedCurrentVisualScaleRoomId(out roomId))
        {
            source = "ProjectedCurrentVisualScaleRoom";
            return true;
        }

        if (TryResolveProjectedProfileRoomId(out roomId))
        {
            source = "ProjectedRoomProfile";
            return true;
        }

        if (TryResolveWalkerRoomId(out roomId))
        {
            source = "WalkerRoomProfile";
            return true;
        }

        if (TryResolveActorRoomId(out roomId))
        {
            source = "ActorRoomState";
            return true;
        }

        if (TryResolveActiveNavigationRoomId(out roomId))
        {
            source = "ActiveNavigation";
            return true;
        }

        if (TryResolveExplicitCurrentRoomId(out roomId))
        {
            source = "CurrentRoomId";
            return true;
        }

        if (TryResolveOverrideRoomId(out roomId))
        {
            source = "RoomIdOverride";
            return true;
        }

        if (GuestRoomScaleApplier.TryInferChapterGuestNameRoomId(gameObject.name, out roomId))
        {
            source = "AuthoredNameInference";
            return true;
        }

        roomId = string.Empty;
        source = string.Empty;
        return false;
    }

    private bool TryResolveExplicitCurrentRoomId(out string roomId)
    {
        roomId = GuestRoomScaleCalibration.CleanRoomId(currentRoomId);
        return !string.IsNullOrWhiteSpace(roomId);
    }

    private bool TryResolveProjectedCurrentVisualScaleRoomId(out string roomId)
    {
        roomId = string.Empty;
        RoomProjectedEntity projectedEntity = ResolveProjectedEntity();

        if (projectedEntity == null ||
            !projectedEntity.IsProjectionActive ||
            string.IsNullOrWhiteSpace(projectedEntity.CurrentVisualScaleRoomId))
        {
            return false;
        }

        roomId = GuestRoomScaleCalibration.CleanRoomId(projectedEntity.CurrentVisualScaleRoomId);
        return !string.IsNullOrWhiteSpace(roomId);
    }

    private bool TryResolveProjectedProfileRoomId(out string roomId)
    {
        roomId = string.Empty;
        RoomProjectedEntity projectedEntity = ResolveProjectedEntity();

        if (projectedEntity != null &&
            projectedEntity.RoomProfile != null &&
            !string.IsNullOrWhiteSpace(projectedEntity.RoomProfile.RoomId))
        {
            roomId = GuestRoomScaleCalibration.CleanRoomId(projectedEntity.RoomProfile.RoomId);
            return !string.IsNullOrWhiteSpace(roomId);
        }

        return false;
    }

    private bool TryResolveActorRoomId(out string roomId)
    {
        roomId = string.Empty;
        ActorRoomState actorRoomState = GetComponent<ActorRoomState>();

        if (actorRoomState == null)
        {
            actorRoomState = GetComponentInParent<ActorRoomState>(true);
        }

        if (actorRoomState == null)
        {
            actorRoomState = GetComponentInChildren<ActorRoomState>(true);
        }

        if (actorRoomState != null && !string.IsNullOrWhiteSpace(actorRoomState.CurrentRoomId))
        {
            roomId = GuestRoomScaleCalibration.CleanRoomId(actorRoomState.CurrentRoomId);
            return true;
        }

        return false;
    }

    private bool TryResolveParentRoomId(out string roomId)
    {
        roomId = string.Empty;
        RoomContentGroup roomContent = GetComponentInParent<RoomContentGroup>(true);

        if (roomContent != null && !string.IsNullOrWhiteSpace(roomContent.RoomName))
        {
            roomId = GuestRoomScaleCalibration.CleanRoomId(roomContent.RoomName);
            return true;
        }

        return false;
    }

    private bool TryResolveWalkerRoomId(out string roomId)
    {
        roomId = string.Empty;
        RoomPersonWalker2D walker = GetComponent<RoomPersonWalker2D>();

        if (walker == null)
        {
            walker = GetComponentInParent<RoomPersonWalker2D>(true);
        }

        if (walker == null)
        {
            walker = GetComponentInChildren<RoomPersonWalker2D>(true);
        }

        if (walker != null && walker.RoomProfile != null && !string.IsNullOrWhiteSpace(walker.RoomProfile.RoomId))
        {
            roomId = GuestRoomScaleCalibration.CleanRoomId(walker.RoomProfile.RoomId);
            return true;
        }

        return false;
    }

    private bool TryResolveActiveNavigationRoomId(out string roomId)
    {
        roomId = string.Empty;

        if (!Application.isPlaying || !IsActiveVisibleManagedChapterGuest())
        {
            return false;
        }

        RoomNavigationManager navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Exclude);

        if (navigationManager == null || string.IsNullOrWhiteSpace(navigationManager.CurrentRoom))
        {
            return false;
        }

        roomId = GuestRoomScaleCalibration.CleanRoomId(navigationManager.CurrentRoom);
        return true;
    }

    private bool TryResolveOverrideRoomId(out string roomId)
    {
        roomId = string.Empty;

        if (string.IsNullOrWhiteSpace(roomIdOverride))
        {
            return false;
        }

        roomId = GuestRoomScaleCalibration.CleanRoomId(roomIdOverride);
        return true;
    }

    private RoomProjectedEntity ResolveProjectedEntity()
    {
        RoomProjectedEntity projectedEntity = GetComponent<RoomProjectedEntity>();

        if (projectedEntity == null)
        {
            projectedEntity = GetComponentInParent<RoomProjectedEntity>(true);
        }

        if (projectedEntity == null)
        {
            projectedEntity = GetComponentInChildren<RoomProjectedEntity>(true);
        }

        return projectedEntity;
    }

    private bool IsActiveVisibleManagedChapterGuest()
    {
        if (!gameObject.activeInHierarchy ||
            !GuestRoomScaleApplier.IsManagedGuestParticipant(this))
        {
            return false;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);

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

    private string BuildRoomFilterExclusionReason(string selectedRoom, string finalRoomId)
    {
        if (excludeFromGuestScaling)
        {
            return "Excluded from guest scaling.";
        }

        if (isButler)
        {
            return "Participant is marked as Butler.";
        }

        if (!GuestRoomScaleApplier.IsManagedGuestParticipant(this))
        {
            return "Not a managed chapter guest participant.";
        }

        if (string.IsNullOrWhiteSpace(selectedRoom))
        {
            return "No selected room.";
        }

        if (string.IsNullOrWhiteSpace(finalRoomId))
        {
            return "No room resolved.";
        }

        return $"Resolved to '{finalRoomId}', not selected room '{selectedRoom}'.";
    }

    private static string GetTransformPath(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        string path = target.name;
        Transform current = target.parent;

        while (current != null)
        {
            path = $"{current.name}/{path}";
            current = current.parent;
        }

        return path;
    }

    public float ResolveRoomLocalY()
    {
        return ResolveRoomLocalY(ResolveRoomId());
    }

    public float ResolveRoomLocalY(string roomContext)
    {
        RoomPersonWalker2D walker = GetComponent<RoomPersonWalker2D>();

        if (walker != null)
        {
            return walker.CurrentPosition.y;
        }

        RoomProjectedEntity projectedEntity = GetComponent<RoomProjectedEntity>();

        if (projectedEntity != null)
        {
            return projectedEntity.RoomLocalFootPoint.y;
        }

        Transform root = ResolveScaleRoot();
        RoomContentGroup roomContent = root != null ? root.GetComponentInParent<RoomContentGroup>(true) : null;

        if (roomContent != null)
        {
            Vector3 localPoint = roomContent.transform.InverseTransformPoint(root.position);
            return localPoint.y;
        }

        if (TryFindResolvedRoomContent(roomContext, out RoomContentGroup resolvedRoomContent))
        {
            Vector3 localPoint = resolvedRoomContent.transform.InverseTransformPoint(root.position);
            return localPoint.y;
        }

        if (root is RectTransform rectTransform)
        {
            return rectTransform.anchoredPosition.y;
        }

        return root != null ? root.localPosition.y : transform.localPosition.y;
    }

    public void CaptureBaseScale(bool force = false)
    {
        if (!force && hasCapturedBaseScale)
        {
            return;
        }

        Transform root = ResolveScaleRoot();
        capturedBaseScale = root != null ? SanitizeScale(root.localScale) : Vector3.one;
        hasCapturedBaseScale = true;
    }

    public void RestoreCapturedBaseScale()
    {
        Transform root = ResolveScaleRoot();

        if (root != null && hasCapturedBaseScale)
        {
            root.localScale = capturedBaseScale;
        }
    }

    public bool ApplyFinalScale(float targetLocalScaleY)
    {
        if (excludeFromGuestScaling || isButler)
        {
            return false;
        }

        Transform root = ResolveScaleRoot();

        if (root == null)
        {
            return false;
        }

        float safeTargetY = Mathf.Max(0.001f, targetLocalScaleY);
        Vector3 referenceScale = SanitizeScale(root.localScale);

        if (referenceScale == Vector3.one && hasCapturedBaseScale)
        {
            referenceScale = SanitizeScale(capturedBaseScale);
        }

        float baseY = Mathf.Abs(referenceScale.y) > 0.001f ? referenceScale.y : 1f;
        float signedTargetY = baseY < 0f ? -safeTargetY : safeTargetY;
        float aspectRatio = signedTargetY / baseY;
        Vector3 targetScale = new Vector3(
            referenceScale.x * aspectRatio,
            signedTargetY,
            referenceScale.z);
        bool changed = (root.localScale - targetScale).sqrMagnitude > 0.000001f;
        root.localScale = targetScale;
        return changed;
    }

    public static bool NameLooksExcludedFromBodyScale(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] tokens =
        {
            "coat",
            "coatcutout",
            "jacket",
            "cloak",
            "shawl",
            "speech",
            "thought",
            "bubble",
            "prompt",
            "highlight",
            "icon",
            "shadow",
            "cursor",
            "tooltip"
        };

        for (int i = 0; i < tokens.Length; i++)
        {
            if (value.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    internal static Vector3 SanitizeScale(Vector3 scale)
    {
        return new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
            Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
            Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);
    }

    private static bool IsUsableBodyTransform(Transform candidate)
    {
        return candidate != null && !NameLooksExcludedFromBodyScale(candidate.name);
    }

    private bool TryFindResolvedRoomContent(string roomId, out RoomContentGroup roomContent)
    {
        roomContent = null;

        if (string.IsNullOrWhiteSpace(roomId))
        {
            return false;
        }

        RoomContentGroup[] rooms = FindObjectsByType<RoomContentGroup>(FindObjectsInactive.Include);

        for (int i = 0; i < rooms.Length; i++)
        {
            RoomContentGroup candidate = rooms[i];

            if (candidate != null && GuestRoomScaleCalibration.SameRoom(candidate.RoomName, roomId))
            {
                roomContent = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeButler(GameObject candidate)
    {
        return candidate != null &&
            (candidate.GetComponent<PointClickPlayerMovement>() != null &&
                !candidate.name.Contains("Guest", StringComparison.OrdinalIgnoreCase) ||
            candidate.name.Contains("Butler", StringComparison.OrdinalIgnoreCase));
    }
}
