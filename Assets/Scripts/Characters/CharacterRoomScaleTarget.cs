using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum CharacterPose
{
    Auto = 0,
    Standing = 1,
    Seated = 2,
    Crouching = 3,
    Lying = 4
}

/// <summary>
/// Opts one Butler/Guest display into the single room-scale system. It contains no room curve data;
/// the catalog is the only source of room-dependent size. The target changes localScale only.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Character Room Scale Target")]
public sealed class CharacterRoomScaleTarget : MonoBehaviour
{
    public static event Action<CharacterRoomScaleTarget> RuntimeStateChanged;

    private static readonly HashSet<CharacterRoomScaleTarget> RegisteredTargets =
        new HashSet<CharacterRoomScaleTarget>();

    [SerializeField] private string characterId;
    [SerializeField] private string currentRoomId;
    [SerializeField] private string roomIdOverride;
    [SerializeField, HideInInspector] private string lastRoomResolutionSource;
    [SerializeField] private CharacterPose pose = CharacterPose.Auto;
    [SerializeField] private Transform scaleRoot;
    [SerializeField] private Transform bodyRoot;
    [SerializeField] private bool excludeFromRoomScaling;
    [SerializeField] private CharacterScaleProfile scaleProfile = CharacterScaleProfile.Auto;
    [SerializeField, Min(0.001f)] private float displaySizeMultiplier = 1f;
    [SerializeField] private Vector3 capturedBaseScale = Vector3.one;
    [SerializeField] private bool hasCapturedBaseScale;
    [NonSerialized] private int runtimeRevision;

    public string CharacterId => string.IsNullOrWhiteSpace(characterId) ? gameObject.name : characterId.Trim();
    public string CurrentRoomId => currentRoomId;
    public string RoomIdOverride => roomIdOverride;
    public string LastRoomResolutionSource => lastRoomResolutionSource;
    public CharacterPose Pose => pose;
    public Transform ScaleRoot => scaleRoot;
    public Transform BodyRoot => bodyRoot;
    public bool ExcludeFromRoomScaling => excludeFromRoomScaling;
    public CharacterScaleProfile ScaleProfile => scaleProfile;
    public CharacterScaleProfile ResolvedScaleProfile => ResolveScaleProfile();
    public float DisplaySizeMultiplier => Mathf.Max(0.001f, displaySizeMultiplier);
    public Vector3 CapturedBaseScale => capturedBaseScale;
    public bool HasCapturedBaseScale => hasCapturedBaseScale;
    public int RuntimeRevision => runtimeRevision;

    public static IEnumerable<CharacterRoomScaleTarget> Targets => RegisteredTargets;

    private void Reset()
    {
        characterId = gameObject.name;
        scaleProfile = ResolveAutomaticScaleProfile();
        ResolveScaleRoot();
        CaptureBaseScale(true);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        RegisteredTargets.Clear();
        RuntimeStateChanged = null;
    }

    private void OnEnable()
    {
        RegisteredTargets.Add(this);
        ResolveScaleRoot();
        CaptureBaseScale(false);
        NotifyRuntimeStateChanged();
    }

    private void OnDisable()
    {
        RegisteredTargets.Remove(this);
        NotifyRuntimeStateChanged();
    }

    private void OnDestroy()
    {
        RegisteredTargets.Remove(this);
        NotifyRuntimeStateChanged();
    }

    private void OnTransformParentChanged()
    {
        NotifyRuntimeStateChanged();
    }

    private void OnValidate()
    {
        displaySizeMultiplier = Mathf.Max(0.001f, displaySizeMultiplier);
        NotifyRuntimeStateChanged();
    }

    public void SetCharacterId(string value)
    {
        string cleanValue = string.IsNullOrWhiteSpace(value) ? gameObject.name : value.Trim();

        if (string.Equals(characterId, cleanValue, StringComparison.Ordinal))
        {
            return;
        }

        characterId = cleanValue;
        NotifyRuntimeStateChanged();
    }

    public void SetCurrentRoomId(string value)
    {
        string cleanValue = CharacterRoomScaleCatalog.CleanRoomId(value);

        if (string.Equals(currentRoomId, cleanValue, StringComparison.Ordinal))
        {
            return;
        }

        currentRoomId = cleanValue;
        NotifyRuntimeStateChanged();
    }

    public void ClearCurrentRoomId()
    {
        SetCurrentRoomId(string.Empty);
    }

    public void SetRoomIdOverride(string value)
    {
        string cleanValue = CharacterRoomScaleCatalog.CleanRoomId(value);

        if (string.Equals(roomIdOverride, cleanValue, StringComparison.Ordinal))
        {
            return;
        }

        roomIdOverride = cleanValue;
        NotifyRuntimeStateChanged();
    }

    public void SetPose(CharacterPose value)
    {
        if (pose == value)
        {
            return;
        }

        pose = value;
        NotifyRuntimeStateChanged();
    }

    public void SetScaleRoot(Transform value)
    {
        Transform resolved = IsUsableBodyTransform(value) ? value : transform;

        if (scaleRoot == resolved)
        {
            return;
        }

        scaleRoot = resolved;
        CaptureBaseScale(true);
        NotifyRuntimeStateChanged();
    }

    public void SetBodyRoot(Transform value)
    {
        Transform resolved = IsUsableBodyTransform(value) ? value : null;

        if (bodyRoot == resolved)
        {
            return;
        }

        bodyRoot = resolved;
        scaleRoot = null;
        ResolveScaleRoot();
        CaptureBaseScale(true);
        NotifyRuntimeStateChanged();
    }

    public void SetExcludedFromRoomScaling(bool value)
    {
        if (excludeFromRoomScaling == value)
        {
            return;
        }

        excludeFromRoomScaling = value;
        NotifyRuntimeStateChanged();
    }

    public void SetScaleProfile(CharacterScaleProfile value)
    {
        if (scaleProfile == value)
        {
            return;
        }

        scaleProfile = value;
        NotifyRuntimeStateChanged();
    }

    public void SetDisplaySizeMultiplier(float value)
    {
        float safeValue = Mathf.Max(0.001f, value);

        if (Mathf.Approximately(displaySizeMultiplier, safeValue))
        {
            return;
        }

        displaySizeMultiplier = safeValue;
        NotifyRuntimeStateChanged();
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
            Graphic graphic = walker.TargetGraphic != null
                ? walker.TargetGraphic
                : GetComponentInChildren<Graphic>(true);

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

    public CharacterScaleProfile ResolveScaleProfile()
    {
        return scaleProfile == CharacterScaleProfile.Auto
            ? ResolveAutomaticScaleProfile()
            : CharacterRoomScaleCatalog.ResolveConcreteProfile(scaleProfile);
    }

    public string ResolveRoomId()
    {
        // Keep room resolution tied to actual placement first. Serialized room fields are fallbacks;
        // they must never override a character that is visibly staged in a different room.
        if (TryResolveParentRoomId(out string parentRoomId))
        {
            lastRoomResolutionSource = "RoomContentGroup";
            return parentRoomId;
        }

        if (TryResolveProjectedCurrentRoomId(out string projectedCurrentRoomId))
        {
            lastRoomResolutionSource = "RoomProjectedEntity.CurrentVisualScaleRoomId";
            return projectedCurrentRoomId;
        }

        if (TryResolveProjectedProfileRoomId(out string projectedProfileRoomId))
        {
            lastRoomResolutionSource = "RoomProjectedEntity.RoomProfile";
            return projectedProfileRoomId;
        }

        if (TryResolveWalkerRoomId(out string walkerRoomId))
        {
            lastRoomResolutionSource = "RoomPersonWalker2D";
            return walkerRoomId;
        }

        if (TryResolveActorRoomId(out string actorRoomId))
        {
            lastRoomResolutionSource = "ActorRoomState";
            return actorRoomId;
        }

        if (TryResolveActiveNavigationRoomId(out string navigationRoomId))
        {
            lastRoomResolutionSource = "RoomNavigationManager";
            return navigationRoomId;
        }

        string cleanCurrentRoom = CharacterRoomScaleCatalog.CleanRoomId(currentRoomId);

        if (!string.IsNullOrWhiteSpace(cleanCurrentRoom))
        {
            lastRoomResolutionSource = "CurrentRoomId";
            return cleanCurrentRoom;
        }

        string cleanOverride = CharacterRoomScaleCatalog.CleanRoomId(roomIdOverride);

        if (!string.IsNullOrWhiteSpace(cleanOverride))
        {
            lastRoomResolutionSource = "RoomIdOverride";
            return cleanOverride;
        }

        if (ResolveScaleProfile() == CharacterScaleProfile.Guest &&
            TryInferAuthoredGuestRoomId(gameObject.name, out string inferredRoomId))
        {
            lastRoomResolutionSource = "GuestNameFallback";
            return inferredRoomId;
        }

        lastRoomResolutionSource = string.Empty;
        return string.Empty;
    }

    public bool TryResolveRoomScaleContext(out string roomId, out Vector2 roomLocalFootPoint)
    {
        return TryResolveRoomScaleContext(
            string.Empty,
            false,
            out roomId,
            out roomLocalFootPoint);
    }

    /// <summary>
    /// Resolves the character's visible foot point in the requested room coordinate space.
    /// The preferred-room path exists for the editor tool only; runtime uses actual placement.
    /// </summary>
    public bool TryResolveRoomScaleContext(
        string preferredRoomId,
        bool preferProvidedRoomId,
        out string roomId,
        out Vector2 roomLocalFootPoint)
    {
        string cleanPreferredRoomId = CharacterRoomScaleCatalog.CleanRoomId(preferredRoomId);
        roomId = preferProvidedRoomId && !string.IsNullOrWhiteSpace(cleanPreferredRoomId)
            ? cleanPreferredRoomId
            : ResolveRoomId();
        roomLocalFootPoint = default;

        PointClickPlayerMovement pointClickMovement = GetComponent<PointClickPlayerMovement>();

        if (ResolveScaleProfile() == CharacterScaleProfile.Butler &&
            pointClickMovement != null &&
            pointClickMovement.TryGetCharacterRoomScaleContext(
                roomId,
                out string movementRoomId,
                out roomLocalFootPoint))
        {
            if (!string.IsNullOrWhiteSpace(movementRoomId))
            {
                roomId = movementRoomId;
            }

            return !string.IsNullOrWhiteSpace(roomId);
        }

        RoomPersonWalker2D walker = ResolveWalker();

        if (walker != null &&
            (!preferProvidedRoomId ||
             walker.RoomProfile == null ||
             string.IsNullOrWhiteSpace(walker.RoomProfile.RoomId) ||
             CharacterRoomScaleCatalog.SameRoom(walker.RoomProfile.RoomId, roomId)))
        {
            roomLocalFootPoint = walker.CurrentPosition;
            return !string.IsNullOrWhiteSpace(roomId);
        }

        RoomProjectedEntity projectedEntity = ResolveProjectedEntity();

        if (projectedEntity != null &&
            (!preferProvidedRoomId || ProjectedEntityMatchesRoom(projectedEntity, roomId)))
        {
            roomLocalFootPoint = projectedEntity.RoomLocalFootPoint;
            return !string.IsNullOrWhiteSpace(roomId);
        }

        ActorRoomState actorState = ResolveActorRoomState();

        if (actorState != null &&
            actorState.TryGetRoomLocalFootPoint(out string actorRoomId, out Vector2 actorFootPoint) &&
            (string.IsNullOrWhiteSpace(roomId) || CharacterRoomScaleCatalog.SameRoom(actorRoomId, roomId)))
        {
            roomId = string.IsNullOrWhiteSpace(roomId) ? actorRoomId : roomId;
            roomLocalFootPoint = actorFootPoint;
            return !string.IsNullOrWhiteSpace(roomId);
        }

        Transform root = ResolveScaleRoot();
        RoomContentGroup parentRoom = root != null
            ? root.GetComponentInParent<RoomContentGroup>(true)
            : null;

        if (parentRoom != null &&
            (string.IsNullOrWhiteSpace(roomId) || CharacterRoomScaleCatalog.SameRoom(parentRoom.RoomName, roomId)))
        {
            Vector3 localPoint = parentRoom.transform.InverseTransformPoint(root.position);
            roomId = string.IsNullOrWhiteSpace(roomId) ? parentRoom.RoomName : roomId;
            roomLocalFootPoint = new Vector2(localPoint.x, localPoint.y);
            return !string.IsNullOrWhiteSpace(roomId);
        }

        if (TryFindRoomContent(roomId, out RoomContentGroup resolvedRoom) && root != null)
        {
            Vector3 localPoint = resolvedRoom.transform.InverseTransformPoint(root.position);
            roomLocalFootPoint = new Vector2(localPoint.x, localPoint.y);
            return true;
        }

        if (root is RectTransform rectTransform)
        {
            roomLocalFootPoint = rectTransform.anchoredPosition;
        }
        else if (root != null)
        {
            roomLocalFootPoint = new Vector2(root.localPosition.x, root.localPosition.y);
        }
        else
        {
            roomLocalFootPoint = new Vector2(transform.localPosition.x, transform.localPosition.y);
        }

        return !string.IsNullOrWhiteSpace(roomId);
    }

    public float ResolveRoomLocalY()
    {
        return TryResolveRoomScaleContext(out _, out Vector2 roomLocalFootPoint)
            ? roomLocalFootPoint.y
            : transform.localPosition.y;
    }

    public float ResolveRoomLocalY(string roomContext)
    {
        return TryResolveRoomScaleContext(
            roomContext,
            !Application.isPlaying,
            out _,
            out Vector2 roomLocalFootPoint)
            ? roomLocalFootPoint.y
            : transform.localPosition.y;
    }

    public void CaptureBaseScale(bool force = false)
    {
        if (!force && hasCapturedBaseScale)
        {
            return;
        }

        Transform root = ResolveScaleRoot();
        Vector3 resolvedBaseScale = root != null ? SanitizeScale(root.localScale) : Vector3.one;
        bool changed = !hasCapturedBaseScale || capturedBaseScale != resolvedBaseScale;
        capturedBaseScale = resolvedBaseScale;
        hasCapturedBaseScale = true;

        if (changed)
        {
            NotifyRuntimeStateChanged();
        }
    }

    public void RestoreCapturedBaseScale()
    {
        Transform root = ResolveScaleRoot();

        if (root == null || !hasCapturedBaseScale)
        {
            return;
        }

        root.localScale = capturedBaseScale;
        NotifyRuntimeStateChanged();
    }

    public bool ApplyFinalScale(float targetLocalScaleY)
    {
        if (excludeFromRoomScaling)
        {
            return false;
        }

        Transform root = ResolveScaleRoot();

        if (root == null)
        {
            return false;
        }

        if (!hasCapturedBaseScale)
        {
            CaptureBaseScale(true);
        }

        Vector3 reference = SanitizeScale(capturedBaseScale);
        Vector3 current = SanitizeScale(root.localScale);
        float referenceY = Mathf.Max(0.001f, Mathf.Abs(reference.y));
        float xOverY = Mathf.Abs(reference.x) / referenceY;
        float safeTargetY = Mathf.Max(0.001f, Mathf.Abs(targetLocalScaleY));
        float xSign = Mathf.Sign(Mathf.Approximately(current.x, 0f) ? reference.x : current.x);
        float ySign = Mathf.Sign(Mathf.Approximately(current.y, 0f) ? reference.y : current.y);
        Vector3 targetScale = new Vector3(
            xSign * xOverY * safeTargetY,
            ySign * safeTargetY,
            current.z);
        bool changed = (root.localScale - targetScale).sqrMagnitude > 0.000001f;

        if (!changed)
        {
            return false;
        }

        root.localScale = targetScale;
        return true;
    }

    public bool OwnsScaleTransform(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        Transform root = ResolveScaleRoot();

        if (root == null)
        {
            return false;
        }

        if (candidate == root)
        {
            return true;
        }

        // Scale writers inside this character's own hierarchy are blocked. A room-stage or other
        // external ancestor is deliberately not treated as owned, so unrelated systems keep working.
        bool candidateIsInsideCharacter = candidate == transform || candidate.IsChildOf(transform);
        return candidateIsInsideCharacter &&
            (root.IsChildOf(candidate) || candidate.IsChildOf(root));
    }

    public static CharacterRoomScaleTarget FindForTransform(Transform candidate)
    {
        if (candidate == null)
        {
            return null;
        }

        CharacterRoomScaleTarget[] parentTargets =
            candidate.GetComponentsInParent<CharacterRoomScaleTarget>(true);

        for (int i = 0; i < parentTargets.Length; i++)
        {
            CharacterRoomScaleTarget target = parentTargets[i];

            if (target != null && target.OwnsScaleTransform(candidate))
            {
                return target;
            }
        }

        CharacterRoomScaleTarget[] childTargets =
            candidate.GetComponentsInChildren<CharacterRoomScaleTarget>(true);

        for (int i = 0; i < childTargets.Length; i++)
        {
            CharacterRoomScaleTarget target = childTargets[i];

            if (target != null && target.OwnsScaleTransform(candidate))
            {
                return target;
            }
        }

        return null;
    }

    public static bool OwnsScaleFor(Transform candidate)
    {
        CharacterRoomScaleTarget target = FindForTransform(candidate);
        return target != null && !target.ExcludeFromRoomScaling;
    }

    public static bool LooksLikeGuest(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        string value = candidate.name ?? string.Empty;
        return value.IndexOf("guest", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static Vector3 SanitizeScale(Vector3 scale)
    {
        return new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
            Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
            Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);
    }

    private void NotifyRuntimeStateChanged()
    {
        unchecked
        {
            runtimeRevision++;
        }

        RuntimeStateChanged?.Invoke(this);
    }

    private CharacterScaleProfile ResolveAutomaticScaleProfile()
    {
        return LooksLikeGuest(gameObject)
            ? CharacterScaleProfile.Guest
            : CharacterScaleProfile.Butler;
    }

    private bool TryResolveActorRoomId(out string roomId)
    {
        roomId = string.Empty;
        ActorRoomState actorState = ResolveActorRoomState();

        if (actorState == null || string.IsNullOrWhiteSpace(actorState.CurrentRoomId))
        {
            return false;
        }

        roomId = CharacterRoomScaleCatalog.CleanRoomId(actorState.CurrentRoomId);
        return !string.IsNullOrWhiteSpace(roomId);
    }

    private bool TryResolveProjectedCurrentRoomId(out string roomId)
    {
        roomId = string.Empty;
        RoomProjectedEntity projected = ResolveProjectedEntity();

        if (projected == null ||
            !projected.IsProjectionActive ||
            string.IsNullOrWhiteSpace(projected.CurrentVisualScaleRoomId))
        {
            return false;
        }

        roomId = CharacterRoomScaleCatalog.CleanRoomId(projected.CurrentVisualScaleRoomId);
        return !string.IsNullOrWhiteSpace(roomId);
    }

    private bool TryResolveProjectedProfileRoomId(out string roomId)
    {
        roomId = string.Empty;
        RoomProjectedEntity projected = ResolveProjectedEntity();

        if (projected == null ||
            projected.RoomProfile == null ||
            string.IsNullOrWhiteSpace(projected.RoomProfile.RoomId))
        {
            return false;
        }

        roomId = CharacterRoomScaleCatalog.CleanRoomId(projected.RoomProfile.RoomId);
        return !string.IsNullOrWhiteSpace(roomId);
    }

    private bool TryResolveParentRoomId(out string roomId)
    {
        roomId = string.Empty;
        RoomContentGroup room = GetComponentInParent<RoomContentGroup>(true);

        if (room == null || string.IsNullOrWhiteSpace(room.RoomName))
        {
            return false;
        }

        roomId = CharacterRoomScaleCatalog.CleanRoomId(room.RoomName);
        return true;
    }

    private bool TryResolveWalkerRoomId(out string roomId)
    {
        roomId = string.Empty;
        RoomPersonWalker2D walker = ResolveWalker();

        if (walker == null || walker.RoomProfile == null || string.IsNullOrWhiteSpace(walker.RoomProfile.RoomId))
        {
            return false;
        }

        roomId = CharacterRoomScaleCatalog.CleanRoomId(walker.RoomProfile.RoomId);
        return !string.IsNullOrWhiteSpace(roomId);
    }

    private bool TryResolveActiveNavigationRoomId(out string roomId)
    {
        roomId = string.Empty;

        if (!Application.isPlaying || !gameObject.activeInHierarchy)
        {
            return false;
        }

        if (ResolveScaleProfile() == CharacterScaleProfile.Guest && !IsActiveVisibleCharacter())
        {
            return false;
        }

        RoomNavigationManager navigation = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Exclude);

        if (navigation == null || string.IsNullOrWhiteSpace(navigation.CurrentRoom))
        {
            return false;
        }

        roomId = CharacterRoomScaleCatalog.CleanRoomId(navigation.CurrentRoom);
        return !string.IsNullOrWhiteSpace(roomId);
    }

    private ActorRoomState ResolveActorRoomState()
    {
        ActorRoomState actorState = GetComponent<ActorRoomState>();
        actorState ??= GetComponentInParent<ActorRoomState>(true);
        actorState ??= GetComponentInChildren<ActorRoomState>(true);
        return actorState;
    }

    private RoomProjectedEntity ResolveProjectedEntity()
    {
        RoomProjectedEntity projected = GetComponent<RoomProjectedEntity>();
        projected ??= GetComponentInParent<RoomProjectedEntity>(true);
        projected ??= GetComponentInChildren<RoomProjectedEntity>(true);
        return projected;
    }

    private RoomPersonWalker2D ResolveWalker()
    {
        RoomPersonWalker2D walker = GetComponent<RoomPersonWalker2D>();
        walker ??= GetComponentInParent<RoomPersonWalker2D>(true);
        walker ??= GetComponentInChildren<RoomPersonWalker2D>(true);
        return walker;
    }

    private bool IsActiveVisibleCharacter()
    {
        if (excludeFromRoomScaling || !gameObject.activeInHierarchy)
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

    private static bool ProjectedEntityMatchesRoom(RoomProjectedEntity projected, string roomId)
    {
        if (projected == null || string.IsNullOrWhiteSpace(roomId))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(projected.CurrentVisualScaleRoomId) &&
            CharacterRoomScaleCatalog.SameRoom(projected.CurrentVisualScaleRoomId, roomId))
        {
            return true;
        }

        return projected.RoomProfile != null &&
            CharacterRoomScaleCatalog.SameRoom(projected.RoomProfile.RoomId, roomId);
    }

    private static bool TryInferAuthoredGuestRoomId(string guestName, out string roomId)
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

        int guestIndex = value.IndexOf("Guest", StringComparison.OrdinalIgnoreCase);

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

    private static bool TryFindRoomContent(string roomId, out RoomContentGroup roomContent)
    {
        roomContent = null;

        if (string.IsNullOrWhiteSpace(roomId))
        {
            return false;
        }

        RoomContentGroup[] rooms = Resources.FindObjectsOfTypeAll<RoomContentGroup>();

        for (int i = 0; i < rooms.Length; i++)
        {
            RoomContentGroup candidate = rooms[i];

            if (candidate != null &&
                candidate.gameObject != null &&
                candidate.gameObject.scene.IsValid() &&
                CharacterRoomScaleCatalog.SameRoom(candidate.RoomName, roomId))
            {
                roomContent = candidate;
                return true;
            }
        }

        return false;
    }

    public static bool NameLooksExcludedFromBodyScale(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] excludedTokens =
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

        for (int i = 0; i < excludedTokens.Length; i++)
        {
            if (value.IndexOf(excludedTokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUsableBodyTransform(Transform candidate)
    {
        return candidate != null &&
            candidate.gameObject != null &&
            candidate.gameObject.scene.IsValid() &&
            !NameLooksExcludedFromBodyScale(candidate.name);
    }
}
