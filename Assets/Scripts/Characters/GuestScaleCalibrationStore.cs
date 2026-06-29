using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GuestPose
{
    Standing,
    Seated,
    Lying,
    Crouching,
    Auto
}

[Serializable]
public sealed class GuestScaleCalibrationEntry
{
    public bool enabled = true;

    public string displayName;
    public string roomId;
    public string guestKey;

    public GuestPose pose;
    public Transform scaleRoot;
    public Transform boundsRoot;

    public RoomProjectedEntity projectedEntity;
    public RoomPersonWalker2D walker;
    public ActorRoomState actor;

    public float heightRatioToButlerStanding = 1f;
    public float manualFineTuneMultiplier = 1f;

    public Vector3 capturedScaleRootBaseScale = Vector3.one;
    public bool hasCapturedScaleRootBaseScale;
}

[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Guest Scale Calibration Store")]
public sealed class GuestScaleCalibrationStore : MonoBehaviour
{
    private const float DefaultStandingRatio = 1f;
    private const float DefaultSeatedRatio = 0.68f;
    private const float DefaultCrouchingRatio = 0.75f;
    private const float DefaultLyingRatio = 0.45f;

    [SerializeField] private List<GuestScaleCalibrationEntry> entries = new List<GuestScaleCalibrationEntry>();

    private static readonly string[][] KnownGuestAliases =
    {
        new[] { "guest1", "guest01", "guest_1", "guest_01", "lady", "missisoldewren", "miss_isolde_wren" },
        new[] { "guest2", "guest02", "guest_2", "guest_02", "butlerguest", "professorlucienvale", "professor_lucien_vale" },
        new[] { "guest3", "guest03", "guest_3", "guest_03", "misterflorianknell", "mister_florian_knell" },
        new[] { "guest4", "guest04", "guest_4", "guest_04", "countesselowendusk", "countess_elowen_dusk" },
        new[] { "guest5", "guest05", "guest_5", "guest_05", "baronhectorglass", "baron_hector_glass" },
        new[] { "guest6", "guest06", "guest_6", "guest_06", "ladysabinemarrow", "lady_sabine_marrow" },
        new[] { "guest7", "guest07", "guest_7", "guest_07", "lordambroseveil", "lord_ambrose_veil" },
        new[] { "guest8", "guest08", "guest_8", "guest_08", "madamecoraliethread", "madame_coralie_thread" }
    };

    public IReadOnlyList<GuestScaleCalibrationEntry> GetAllEntries()
    {
        EnsureEntriesList();
        return entries;
    }

    public bool TryGetCalibrationForGuest(
        Component guestComponent,
        string roomId,
        Transform scaleRoot,
        out GuestScaleCalibrationEntry entry)
    {
        ResolveGuestComponents(
            guestComponent,
            out RoomProjectedEntity projectedEntity,
            out RoomPersonWalker2D walker,
            out ActorRoomState actor);
        return TryGetCalibrationForGuest(projectedEntity, walker, actor, roomId, scaleRoot, out entry);
    }

    public bool TryGetCalibrationForGuest(
        RoomProjectedEntity projectedEntity,
        RoomPersonWalker2D walker,
        ActorRoomState actor,
        string roomId,
        Transform scaleRoot,
        out GuestScaleCalibrationEntry entry)
    {
        EnsureEntriesList();
        entry = null;

        if (TryFindByDirectReference(projectedEntity, walker, actor, roomId, out entry))
        {
            return true;
        }

        if (scaleRoot != null && TryFindByScaleRoot(scaleRoot, roomId, out entry))
        {
            return true;
        }

        if (actor != null && TryFindByActorId(actor, roomId, out entry))
        {
            return true;
        }

        if (TryFindByStablePath(projectedEntity, walker, actor, scaleRoot, roomId, out entry))
        {
            return true;
        }

        return false;
    }

    public GuestScaleCalibrationEntry GetOrCreateEntry(
        Component guestComponent,
        string roomId,
        Transform scaleRoot)
    {
        ResolveGuestComponents(
            guestComponent,
            out RoomProjectedEntity projectedEntity,
            out RoomPersonWalker2D walker,
            out ActorRoomState actor);
        return GetOrCreateEntry(projectedEntity, walker, actor, roomId, scaleRoot);
    }

    public GuestScaleCalibrationEntry GetOrCreateEntry(
        RoomProjectedEntity projectedEntity,
        RoomPersonWalker2D walker,
        ActorRoomState actor,
        string roomId,
        Transform scaleRoot)
    {
        if (TryGetCalibrationForGuest(projectedEntity, walker, actor, roomId, scaleRoot, out GuestScaleCalibrationEntry existing))
        {
            return existing;
        }

        EnsureEntriesList();
        GuestScaleCalibrationEntry entry = new GuestScaleCalibrationEntry();
        PopulateIdentity(entry, projectedEntity, walker, actor, roomId, scaleRoot);
        entries.Add(entry);
        return entry;
    }

    public GuestScaleCalibrationEntry GetOrCreateEntry(
        string guestKey,
        string displayName,
        string roomId)
    {
        EnsureEntriesList();
        string cleanGuestKey = CleanGuestKey(guestKey);
        string cleanRoomId = CleanRoomId(roomId);

        for (int i = 0; i < entries.Count; i++)
        {
            GuestScaleCalibrationEntry entry = entries[i];

            if (entry != null &&
                IsUsableEntryForRoom(entry, cleanRoomId) &&
                SameGuestIdentity(entry.guestKey, cleanGuestKey, entry.displayName, displayName))
            {
                return entry;
            }
        }

        GuestScaleCalibrationEntry created = new GuestScaleCalibrationEntry
        {
            displayName = string.IsNullOrWhiteSpace(displayName) ? cleanGuestKey : displayName.Trim(),
            roomId = cleanRoomId,
            guestKey = cleanGuestKey,
            pose = GuestPose.Auto,
            heightRatioToButlerStanding = 1f,
            manualFineTuneMultiplier = 1f
        };
        entries.Add(created);
        return created;
    }

    public void SetCalibrationForGuest(GuestScaleCalibrationEntry sourceEntry)
    {
        if (sourceEntry == null)
        {
            return;
        }

        GuestScaleCalibrationEntry target = GetOrCreateEntry(
            sourceEntry.projectedEntity,
            sourceEntry.walker,
            sourceEntry.actor,
            sourceEntry.roomId,
            sourceEntry.scaleRoot);
        CopyEntry(sourceEntry, target);
    }

    public GuestScaleCalibrationEntry SetCalibrationForGuest(
        Component guestComponent,
        string roomId,
        GuestPose pose,
        Transform scaleRoot,
        Transform boundsRoot,
        float heightRatioToButlerStanding,
        float manualFineTuneMultiplier)
    {
        ResolveGuestComponents(
            guestComponent,
            out RoomProjectedEntity projectedEntity,
            out RoomPersonWalker2D walker,
            out ActorRoomState actor);
        GuestScaleCalibrationEntry entry = GetOrCreateEntry(projectedEntity, walker, actor, roomId, scaleRoot);
        entry.enabled = true;
        entry.pose = pose;
        entry.scaleRoot = scaleRoot;
        entry.boundsRoot = boundsRoot != null ? boundsRoot : scaleRoot;
        entry.projectedEntity = projectedEntity != null ? projectedEntity : entry.projectedEntity;
        entry.walker = walker != null ? walker : entry.walker;
        entry.actor = actor != null ? actor : entry.actor;
        entry.roomId = CleanRoomId(roomId);
        entry.heightRatioToButlerStanding = SanitizeRatio(heightRatioToButlerStanding);
        entry.manualFineTuneMultiplier = SanitizeFineTune(manualFineTuneMultiplier);
        entry.guestKey = BuildGuestKey(projectedEntity, walker, actor, scaleRoot);

        if (string.IsNullOrWhiteSpace(entry.displayName))
        {
            entry.displayName = BuildDisplayName(projectedEntity, walker, actor, scaleRoot);
        }

        return entry;
    }

    public GuestScaleCalibrationEntry SetCalibrationForGuest(
        string guestKey,
        string displayName,
        string roomId,
        GuestPose pose,
        Transform scaleRoot,
        Transform boundsRoot,
        float heightRatioToButlerStanding,
        float manualFineTuneMultiplier)
    {
        GuestScaleCalibrationEntry entry = GetOrCreateEntry(guestKey, displayName, roomId);
        entry.enabled = true;
        entry.displayName = string.IsNullOrWhiteSpace(displayName) ? entry.displayName : displayName.Trim();
        entry.roomId = CleanRoomId(roomId);
        entry.guestKey = CleanGuestKey(guestKey);
        entry.pose = pose;
        entry.scaleRoot = scaleRoot;
        entry.boundsRoot = boundsRoot != null ? boundsRoot : scaleRoot;
        entry.heightRatioToButlerStanding = SanitizeRatio(heightRatioToButlerStanding);
        entry.manualFineTuneMultiplier = SanitizeFineTune(manualFineTuneMultiplier);
        return entry;
    }

    public bool RemoveCalibrationForGuest(
        Component guestComponent,
        string roomId,
        Transform scaleRoot)
    {
        ResolveGuestComponents(
            guestComponent,
            out RoomProjectedEntity projectedEntity,
            out RoomPersonWalker2D walker,
            out ActorRoomState actor);
        return RemoveCalibrationForGuest(projectedEntity, walker, actor, roomId, scaleRoot);
    }

    public bool RemoveCalibrationForGuest(
        RoomProjectedEntity projectedEntity,
        RoomPersonWalker2D walker,
        ActorRoomState actor,
        string roomId,
        Transform scaleRoot)
    {
        EnsureEntriesList();

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            GuestScaleCalibrationEntry entry = entries[i];

            if (EntryMatches(entry, projectedEntity, walker, actor, roomId, scaleRoot))
            {
                entries.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public bool RemoveCalibrationForGuest(string guestKey, string displayName, string roomId)
    {
        EnsureEntriesList();
        string cleanGuestKey = CleanGuestKey(guestKey);

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            GuestScaleCalibrationEntry entry = entries[i];

            if (entry != null &&
                IsUsableEntryForRoom(entry, roomId) &&
                SameGuestIdentity(entry.guestKey, cleanGuestKey, entry.displayName, displayName))
            {
                entries.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public bool CaptureBaseScale(GuestScaleCalibrationEntry entry)
    {
        if (entry == null || entry.scaleRoot == null)
        {
            return false;
        }

        entry.capturedScaleRootBaseScale = SanitizeScale(entry.scaleRoot.localScale);
        entry.hasCapturedScaleRootBaseScale = true;
        return true;
    }

    public bool CaptureBaseScale(
        Component guestComponent,
        string roomId,
        Transform scaleRoot)
    {
        if (!TryGetCalibrationForGuest(guestComponent, roomId, scaleRoot, out GuestScaleCalibrationEntry entry))
        {
            entry = GetOrCreateEntry(guestComponent, roomId, scaleRoot);
        }

        if (entry.scaleRoot == null)
        {
            entry.scaleRoot = scaleRoot;
        }

        return CaptureBaseScale(entry);
    }

    public bool RestoreBaseScale(GuestScaleCalibrationEntry entry)
    {
        if (entry == null || entry.scaleRoot == null || !entry.hasCapturedScaleRootBaseScale)
        {
            return false;
        }

        entry.scaleRoot.localScale = SanitizeScale(entry.capturedScaleRootBaseScale);
        return true;
    }

    public bool RestoreBaseScale(
        Component guestComponent,
        string roomId,
        Transform scaleRoot)
    {
        return TryGetCalibrationForGuest(guestComponent, roomId, scaleRoot, out GuestScaleCalibrationEntry entry) &&
            RestoreBaseScale(entry);
    }

    public static float GetDefaultHeightRatioForPose(GuestPose pose)
    {
        return pose switch
        {
            GuestPose.Seated => DefaultSeatedRatio,
            GuestPose.Crouching => DefaultCrouchingRatio,
            GuestPose.Lying => DefaultLyingRatio,
            _ => DefaultStandingRatio
        };
    }

    public static float SanitizeRatio(float value)
    {
        return Mathf.Clamp(value <= 0f ? 1f : value, 0.3f, 1.2f);
    }

    public static float SanitizeFineTune(float value)
    {
        return Mathf.Clamp(value <= 0f ? 1f : value, 0.5f, 1.5f);
    }

    public static string GetStableSceneObjectPath(Transform target)
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

        Scene scene = target.gameObject.scene;
        string sceneKey = scene.IsValid()
            ? string.IsNullOrWhiteSpace(scene.path) ? scene.name : scene.path
            : "unsaved-scene";
        return $"{sceneKey}:{path}";
    }

    public static string BuildGuestKey(
        RoomProjectedEntity projectedEntity,
        RoomPersonWalker2D walker,
        ActorRoomState actor,
        Transform scaleRoot)
    {
        if (actor != null && !string.IsNullOrWhiteSpace(actor.ActorId))
        {
            return $"actor:{actor.ActorId.Trim()}";
        }

        if (projectedEntity != null)
        {
            return $"projected:{GetStableSceneObjectPath(projectedEntity.transform)}";
        }

        if (walker != null)
        {
            return $"walker:{GetStableSceneObjectPath(walker.transform)}";
        }

        return scaleRoot != null
            ? $"root:{GetStableSceneObjectPath(scaleRoot)}"
            : string.Empty;
    }

    private void PopulateIdentity(
        GuestScaleCalibrationEntry entry,
        RoomProjectedEntity projectedEntity,
        RoomPersonWalker2D walker,
        ActorRoomState actor,
        string roomId,
        Transform scaleRoot)
    {
        entry.displayName = BuildDisplayName(projectedEntity, walker, actor, scaleRoot);
        entry.roomId = CleanRoomId(roomId);
        entry.guestKey = BuildGuestKey(projectedEntity, walker, actor, scaleRoot);
        entry.projectedEntity = projectedEntity;
        entry.walker = walker;
        entry.actor = actor;
        entry.scaleRoot = scaleRoot;
        entry.boundsRoot = scaleRoot;
        entry.pose = actor != null && actor.IsSeated ? GuestPose.Seated : GuestPose.Auto;
        entry.heightRatioToButlerStanding = GetDefaultHeightRatioForPose(entry.pose);
        entry.manualFineTuneMultiplier = 1f;
    }

    private static void CopyEntry(GuestScaleCalibrationEntry source, GuestScaleCalibrationEntry target)
    {
        target.enabled = source.enabled;
        target.displayName = source.displayName;
        target.roomId = CleanRoomId(source.roomId);
        target.guestKey = source.guestKey;
        target.pose = source.pose;
        target.scaleRoot = source.scaleRoot;
        target.boundsRoot = source.boundsRoot;
        target.projectedEntity = source.projectedEntity;
        target.walker = source.walker;
        target.actor = source.actor;
        target.heightRatioToButlerStanding = SanitizeRatio(source.heightRatioToButlerStanding);
        target.manualFineTuneMultiplier = SanitizeFineTune(source.manualFineTuneMultiplier);
        target.capturedScaleRootBaseScale = SanitizeScale(source.capturedScaleRootBaseScale);
        target.hasCapturedScaleRootBaseScale = source.hasCapturedScaleRootBaseScale;
    }

    private bool TryFindByDirectReference(
        RoomProjectedEntity projectedEntity,
        RoomPersonWalker2D walker,
        ActorRoomState actor,
        string roomId,
        out GuestScaleCalibrationEntry match)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            GuestScaleCalibrationEntry entry = entries[i];

            if (!IsUsableEntryForRoom(entry, roomId))
            {
                continue;
            }

            if ((projectedEntity != null && entry.projectedEntity == projectedEntity) ||
                (walker != null && entry.walker == walker) ||
                (actor != null && entry.actor == actor))
            {
                match = entry;
                return true;
            }
        }

        match = null;
        return false;
    }

    private bool TryFindByScaleRoot(
        Transform scaleRoot,
        string roomId,
        out GuestScaleCalibrationEntry match)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            GuestScaleCalibrationEntry entry = entries[i];

            if (IsUsableEntryForRoom(entry, roomId) && entry.scaleRoot == scaleRoot)
            {
                match = entry;
                return true;
            }
        }

        match = null;
        return false;
    }

    private bool TryFindByActorId(
        ActorRoomState actor,
        string roomId,
        out GuestScaleCalibrationEntry match)
    {
        string actorKey = actor != null && !string.IsNullOrWhiteSpace(actor.ActorId)
            ? $"actor:{actor.ActorId.Trim()}"
            : string.Empty;

        for (int i = 0; i < entries.Count; i++)
        {
            GuestScaleCalibrationEntry entry = entries[i];

            if (!IsUsableEntryForRoom(entry, roomId))
            {
                continue;
            }

            if ((entry.actor != null && SameActorId(entry.actor, actor)) ||
                (!string.IsNullOrWhiteSpace(actorKey) &&
                SameGuestIdentity(entry.guestKey, actorKey, entry.displayName, actor.ActorId)))
            {
                match = entry;
                return true;
            }
        }

        match = null;
        return false;
    }

    private bool TryFindByStablePath(
        RoomProjectedEntity projectedEntity,
        RoomPersonWalker2D walker,
        ActorRoomState actor,
        Transform scaleRoot,
        string roomId,
        out GuestScaleCalibrationEntry match)
    {
        string projectedKey = projectedEntity != null ? $"projected:{GetStableSceneObjectPath(projectedEntity.transform)}" : string.Empty;
        string walkerKey = walker != null ? $"walker:{GetStableSceneObjectPath(walker.transform)}" : string.Empty;
        string actorKey = actor != null && !string.IsNullOrWhiteSpace(actor.ActorId) ? $"actor:{actor.ActorId.Trim()}" : string.Empty;
        string rootKey = scaleRoot != null ? $"root:{GetStableSceneObjectPath(scaleRoot)}" : string.Empty;

        for (int i = 0; i < entries.Count; i++)
        {
            GuestScaleCalibrationEntry entry = entries[i];

            if (!IsUsableEntryForRoom(entry, roomId) || string.IsNullOrWhiteSpace(entry.guestKey))
            {
                continue;
            }

            if (SameGuestIdentity(entry.guestKey, projectedKey, entry.displayName, string.Empty) ||
                SameGuestIdentity(entry.guestKey, walkerKey, entry.displayName, string.Empty) ||
                SameGuestIdentity(entry.guestKey, actorKey, entry.displayName, actor != null ? actor.ActorId : string.Empty) ||
                SameGuestIdentity(entry.guestKey, rootKey, entry.displayName, string.Empty))
            {
                match = entry;
                return true;
            }
        }

        match = null;
        return false;
    }

    private bool EntryMatches(
        GuestScaleCalibrationEntry entry,
        RoomProjectedEntity projectedEntity,
        RoomPersonWalker2D walker,
        ActorRoomState actor,
        string roomId,
        Transform scaleRoot)
    {
        return IsUsableEntryForRoom(entry, roomId) &&
            ((projectedEntity != null && entry.projectedEntity == projectedEntity) ||
            (walker != null && entry.walker == walker) ||
            (actor != null && entry.actor == actor) ||
            (scaleRoot != null && entry.scaleRoot == scaleRoot) ||
            (actor != null && SameActorId(entry.actor, actor)) ||
            SameGuestIdentity(entry.guestKey, BuildGuestKey(projectedEntity, walker, actor, scaleRoot), entry.displayName, actor != null ? actor.ActorId : string.Empty));
    }

    private static bool IsUsableEntryForRoom(GuestScaleCalibrationEntry entry, string roomId)
    {
        if (entry == null || !entry.enabled)
        {
            return false;
        }

        return SameRoom(entry.roomId, roomId) ||
            string.IsNullOrWhiteSpace(entry.roomId) ||
            string.IsNullOrWhiteSpace(roomId);
    }

    private static void ResolveGuestComponents(
        Component guestComponent,
        out RoomProjectedEntity projectedEntity,
        out RoomPersonWalker2D walker,
        out ActorRoomState actor)
    {
        projectedEntity = guestComponent as RoomProjectedEntity;
        walker = guestComponent as RoomPersonWalker2D;
        actor = guestComponent as ActorRoomState;

        if (guestComponent == null)
        {
            return;
        }

        projectedEntity ??= guestComponent.GetComponent<RoomProjectedEntity>();
        walker ??= guestComponent.GetComponent<RoomPersonWalker2D>();
        actor ??= guestComponent.GetComponentInParent<ActorRoomState>(true);
    }

    private static string BuildDisplayName(
        RoomProjectedEntity projectedEntity,
        RoomPersonWalker2D walker,
        ActorRoomState actor,
        Transform scaleRoot)
    {
        if (actor != null && !string.IsNullOrWhiteSpace(actor.ActorId))
        {
            return actor.ActorId;
        }

        if (projectedEntity != null)
        {
            return projectedEntity.name;
        }

        if (walker != null)
        {
            return walker.name;
        }

        return scaleRoot != null ? scaleRoot.name : "Guest";
    }

    private void EnsureEntriesList()
    {
        entries ??= new List<GuestScaleCalibrationEntry>();
    }

    private static bool SameActorId(ActorRoomState left, ActorRoomState right)
    {
        return left != null &&
            right != null &&
            string.Equals(left.ActorId, right.ActorId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameRoom(string left, string right)
    {
        return string.Equals(NormalizeRoomName(left), NormalizeRoomName(right), StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameKey(string left, string right)
    {
        return !string.IsNullOrWhiteSpace(left) &&
            !string.IsNullOrWhiteSpace(right) &&
            string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameGuestIdentity(string leftKey, string rightKey, string leftDisplayName, string rightDisplayName)
    {
        if (SameKey(leftKey, rightKey))
        {
            return true;
        }

        string left = NormalizeGuestIdentity(RemoveIdentityPrefix(leftKey));
        string right = NormalizeGuestIdentity(RemoveIdentityPrefix(rightKey));

        if (!string.IsNullOrWhiteSpace(left) &&
            !string.IsNullOrWhiteSpace(right) &&
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (KnownGuestAliasMatch(left, right))
        {
            return true;
        }

        string leftName = NormalizeGuestIdentity(leftDisplayName);
        string rightName = NormalizeGuestIdentity(rightDisplayName);

        if (KnownGuestAliasMatch(left, rightName) ||
            KnownGuestAliasMatch(leftName, right) ||
            KnownGuestAliasMatch(leftName, rightName))
        {
            return true;
        }

        return false;
    }

    private static bool KnownGuestAliasMatch(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        for (int groupIndex = 0; groupIndex < KnownGuestAliases.Length; groupIndex++)
        {
            bool hasLeft = false;
            bool hasRight = false;
            string[] aliases = KnownGuestAliases[groupIndex];

            for (int aliasIndex = 0; aliasIndex < aliases.Length; aliasIndex++)
            {
                string alias = NormalizeGuestIdentity(aliases[aliasIndex]);
                hasLeft |= string.Equals(left, alias, StringComparison.OrdinalIgnoreCase);
                hasRight |= string.Equals(right, alias, StringComparison.OrdinalIgnoreCase);
            }

            if (hasLeft && hasRight)
            {
                return true;
            }
        }

        return false;
    }

    private static string RemoveIdentityPrefix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        int prefixIndex = value.IndexOf(':');
        return prefixIndex >= 0 ? value.Substring(prefixIndex + 1) : value;
    }

    private static string CleanGuestKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeGuestIdentity(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.Trim()
            .ToLowerInvariant()
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("'", string.Empty)
            .Replace(":", string.Empty);

        if (normalized.StartsWith("actor", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring("actor".Length);
        }

        if (normalized.StartsWith("guest0", StringComparison.OrdinalIgnoreCase))
        {
            int index = "guest".Length;

            while (index < normalized.Length - 1 && normalized[index] == '0')
            {
                normalized = normalized.Remove(index, 1);
            }
        }

        return normalized;
    }

    private static string CleanRoomId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
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

    private static Vector3 SanitizeScale(Vector3 scale)
    {
        return new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
            Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
            Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);
    }
}
