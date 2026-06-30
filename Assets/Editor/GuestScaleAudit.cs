using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class GuestScaleAudit
{
    public const string ReportPath = "Assets/Editor/Reports/GuestScaleAudit.md";

    [MenuItem("Tools/Characters/Guest Scale Audit")]
    public static void RunAndWriteReport()
    {
        string report = BuildReport();
        string directory = Path.GetDirectoryName(ReportPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(ReportPath, report);
        AssetDatabase.ImportAsset(ReportPath);
        Debug.Log($"Guest scale audit written to {ReportPath}");
    }

    public static string BuildReport()
    {
        AuditCounts counts = new AuditCounts();
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("# Guest Scale Audit");
        builder.AppendLine();

        AppendButlerCalibration(builder, ref counts);
        AppendChapterGuests(builder, ref counts);
        AppendWalkers(builder, ref counts);
        AppendProjectedEntities(builder, ref counts);
        AppendCoats(builder, ref counts);
        AppendScaleWriters(builder, ref counts);

        builder.Insert(0, BuildSummary(counts));
        return builder.ToString();
    }

    private static string BuildSummary(AuditCounts counts)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("# Summary");
        builder.AppendLine();
        builder.AppendLine($"Butler room calibrations found: {counts.ButlerRoomCalibrations}");
        builder.AppendLine($"Guest prefab instances found: {counts.GuestPrefabInstances}");
        builder.AppendLine($"RoomPersonWalker2D guests found: {counts.RoomPersonWalkerGuests}");
        builder.AppendLine($"RoomProjectedEntity FloorCharacters found: {counts.ProjectedFloorCharacters}");
        builder.AppendLine($"RoomProjectedEntity props/furniture found: {counts.ProjectedPropsFurniture}");
        builder.AppendLine($"Non-empty roomVisualScaleOverrides found: {counts.NonEmptyRoomVisualScaleOverrides}");
        builder.AppendLine($"Coat visuals found under guests: {counts.CoatVisualsUnderGuests}");
        builder.AppendLine($"Active guest scale writers found: {counts.ActiveGuestScaleWriters}");
        builder.AppendLine();
        return builder.ToString();
    }

    private static void AppendButlerCalibration(StringBuilder builder, ref AuditCounts counts)
    {
        builder.AppendLine("## Butler Room Calibration");
        PointClickPlayerMovement butler = FindButler();

        if (butler == null)
        {
            builder.AppendLine("- Butler object path: not found");
            builder.AppendLine();
            return;
        }

        builder.AppendLine($"- Butler object path: {GetPath(butler.transform)}");
        List<string> roomIds = new List<string>();
        butler.GetButlerScaleOverrideRoomIds(roomIds);
        counts.ButlerRoomCalibrations = roomIds.Count;
        builder.AppendLine($"- Total Butler room entries: {roomIds.Count}");

        for (int i = 0; i < roomIds.Count; i++)
        {
            if (!butler.TryGetButlerRoomScaleOverride(roomIds[i], out PointClickPlayerMovement.ButlerRoomScaleOverrideData data))
            {
                continue;
            }

            builder.AppendLine(
                $"- {data.RoomId}: frontY={data.FrontRoomLocalFootY:0.###}, frontScaleY={data.FrontFinalLocalScaleY:0.###}, " +
                $"backY={data.BackRoomLocalFootY:0.###}, backScaleY={data.BackFinalLocalScaleY:0.###}");
        }

        builder.AppendLine();
    }

    private static void AppendChapterGuests(StringBuilder builder, ref AuditCounts counts)
    {
        builder.AppendLine("## Chapter 1 Guests");
        GameObject[] guests = FindChapterGuestObjects();
        counts.GuestPrefabInstances = guests.Length;

        for (int i = 0; i < guests.Length; i++)
        {
            GameObject guest = guests[i];
            SpriteRenderer spriteRenderer = guest.GetComponentInChildren<SpriteRenderer>(true);
            PointClickPlayerMovement movement = guest.GetComponentInChildren<PointClickPlayerMovement>(true);
            NPCWaypointMover mover = guest.GetComponent<NPCWaypointMover>();
            ActorRoomState actorState = guest.GetComponent<ActorRoomState>();

            builder.AppendLine($"- {guest.name}: {GetPath(guest.transform)}");
            builder.AppendLine($"  - Prefab/source: {PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(guest)}");
            builder.AppendLine($"  - SpriteRenderer sprite: {(spriteRenderer != null && spriteRenderer.sprite != null ? spriteRenderer.sprite.name : "none")}");
            builder.AppendLine($"  - SpriteRenderer size: {(spriteRenderer != null ? spriteRenderer.bounds.size.ToString() : "none")}");
            builder.AppendLine($"  - Transform localScale: {guest.transform.localScale}");
            builder.AppendLine($"  - Active state: {guest.activeInHierarchy}");
            builder.AppendLine($"  - PointClickPlayerMovement exists: {movement != null}");
            builder.AppendLine($"  - PointClickPlayerMovement disabled for guest: {movement == null || !movement.enabled}");
            builder.AppendLine($"  - NPCWaypointMover exists: {mover != null}");
            builder.AppendLine($"  - ActorRoomState exists: {actorState != null}");
        }

        builder.AppendLine();
    }

    private static void AppendWalkers(StringBuilder builder, ref AuditCounts counts)
    {
        builder.AppendLine("## RoomPersonWalker2D Guests");
        RoomPersonWalker2D[] walkers = Resources.FindObjectsOfTypeAll<RoomPersonWalker2D>();

        for (int i = 0; i < walkers.Length; i++)
        {
            RoomPersonWalker2D walker = walkers[i];

            if (IsPersistentAsset(walker))
            {
                continue;
            }

            counts.RoomPersonWalkerGuests++;
            SerializedObject serializedWalker = new SerializedObject(walker);
            builder.AppendLine($"- {GetPath(walker.transform)}");
            builder.AppendLine($"  - targetGraphic path: {(walker.TargetGraphic != null ? GetPath(walker.TargetGraphic.transform) : "none")}");
            builder.AppendLine($"  - nearScale: {ReadFloat(serializedWalker, "nearScale"):0.###}");
            builder.AppendLine($"  - farScale: {ReadFloat(serializedWalker, "farScale"):0.###}");
            builder.AppendLine($"  - transform localScale: {walker.transform.localScale}");
            builder.AppendLine($"  - targetGraphic localScale: {(walker.TargetGraphic != null ? walker.TargetGraphic.rectTransform.localScale.ToString() : "none")}");
            builder.AppendLine($"  - room id: {ResolveRoomId(walker.transform)}");
        }

        builder.AppendLine();
    }

    private static void AppendProjectedEntities(StringBuilder builder, ref AuditCounts counts)
    {
        builder.AppendLine("## RoomProjectedEntity");
        RoomProjectedEntity[] entities = Resources.FindObjectsOfTypeAll<RoomProjectedEntity>();
        int total = 0;

        for (int i = 0; i < entities.Length; i++)
        {
            RoomProjectedEntity entity = entities[i];

            if (IsPersistentAsset(entity))
            {
                continue;
            }

            total++;
            SerializedObject serializedEntity = new SerializedObject(entity);
            int overrideCount = ReadArraySize(serializedEntity, "roomVisualScaleOverrides");
            bool useOverrides = ReadBool(serializedEntity, "useRoomVisualScaleOverrides");
            bool applyScale = ReadBool(serializedEntity, "applyScale");

            if (entity.Mode == RoomProjectedEntity.ProjectionMode.FloorCharacter)
            {
                counts.ProjectedFloorCharacters++;
            }
            else
            {
                counts.ProjectedPropsFurniture++;
            }

            if (overrideCount > 0)
            {
                counts.NonEmptyRoomVisualScaleOverrides++;
            }

            builder.AppendLine($"- {GetPath(entity.transform)}");
            builder.AppendLine($"  - projection mode: {entity.Mode}");
            builder.AppendLine($"  - FloorCharacter or prop/furniture: {(entity.Mode == RoomProjectedEntity.ProjectionMode.FloorCharacter ? "FloorCharacter" : "prop/furniture")}");
            builder.AppendLine($"  - visualRoot: {(entity.VisualRoot != null ? GetPath(entity.VisualRoot) : "none")}");
            builder.AppendLine($"  - applyScale: {applyScale}");
            builder.AppendLine($"  - useRoomVisualScaleOverrides: {useOverrides}");
            builder.AppendLine($"  - roomVisualScaleOverrides count: {overrideCount}");
        }

        builder.AppendLine($"- total components: {total}");
        builder.AppendLine();
    }

    private static void AppendCoats(StringBuilder builder, ref AuditCounts counts)
    {
        builder.AppendLine("## Coat Visuals Under Guests");
        GameObject[] guests = FindChapterGuestObjects();

        for (int i = 0; i < guests.Length; i++)
        {
            Transform[] children = guests[i].GetComponentsInChildren<Transform>(true);

            for (int childIndex = 0; childIndex < children.Length; childIndex++)
            {
                Transform child = children[childIndex];

                if (child == null || child == guests[i].transform || !GuestScaleParticipant.NameLooksExcludedFromBodyScale(child.name))
                {
                    continue;
                }

                if (!NameLooksLikeCoat(child.name))
                {
                    continue;
                }

                counts.CoatVisualsUnderGuests++;
                builder.AppendLine($"- {GetPath(child)}");
            }
        }

        builder.AppendLine();
    }

    private static void AppendScaleWriters(StringBuilder builder, ref AuditCounts counts)
    {
        builder.AppendLine("## Scale Writers");
        GuestRoomScaleApplier[] appliers = Resources.FindObjectsOfTypeAll<GuestRoomScaleApplier>();
#pragma warning disable CS0618
        GuestButlerScaleHarmonizer[] harmonizers = Resources.FindObjectsOfTypeAll<GuestButlerScaleHarmonizer>();
#pragma warning restore CS0618
        counts.ActiveGuestScaleWriters = CountSceneObjects(appliers);

        builder.AppendLine("- GuestRoomScaleApplier.RefreshAllNow");
        builder.AppendLine("- RoomProjectedEntity.ApplyProjectedScale: guarded when GuestScaleParticipant owns the visual root");
        builder.AppendLine("- RoomPersonWalker2D.ApplyVisuals: guarded when GuestScaleParticipant owns the walker graphic/root");
        builder.AppendLine("- ActorRoomState room-stage scale: guarded when GuestScaleParticipant is present");
        builder.AppendLine($"- Obsolete GuestButlerScaleHarmonizer components in scene: {CountSceneObjects(harmonizers)}");
        builder.AppendLine();
    }

    private static PointClickPlayerMovement FindButler()
    {
        PointClickPlayerMovement[] movements = Resources.FindObjectsOfTypeAll<PointClickPlayerMovement>();

        for (int i = 0; i < movements.Length; i++)
        {
            PointClickPlayerMovement movement = movements[i];

            if (movement == null || IsPersistentAsset(movement) || IsGuestName(movement.gameObject.name))
            {
                continue;
            }

            if (string.Equals(movement.gameObject.tag, "Player", StringComparison.OrdinalIgnoreCase) ||
                movement.gameObject.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
                movement.gameObject.name.IndexOf("Butler", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return movement;
            }
        }

        return movements.Length > 0 ? movements[0] : null;
    }

    private static GameObject[] FindChapterGuestObjects()
    {
        List<GameObject> guests = new List<GameObject>();
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];

            if (candidate == null || IsPersistentAsset(candidate) || !IsGuestName(candidate.name))
            {
                continue;
            }

            if (candidate.transform.parent != null && IsGuestName(candidate.transform.parent.name))
            {
                continue;
            }

            guests.Add(candidate);
        }

        guests.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));
        return guests.ToArray();
    }

    private static int CountSceneObjects<T>(T[] objects) where T : UnityEngine.Object
    {
        int count = 0;

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null && !IsPersistentAsset(objects[i]))
            {
                count++;
            }
        }

        return count;
    }

    private static string ResolveRoomId(Transform transform)
    {
        RoomContentGroup room = transform != null ? transform.GetComponentInParent<RoomContentGroup>(true) : null;
        return room != null ? room.RoomName : string.Empty;
    }

    private static bool ReadBool(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null && property.boolValue;
    }

    private static float ReadFloat(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.floatValue : 0f;
    }

    private static int ReadArraySize(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null && property.isArray ? property.arraySize : 0;
    }

    private static string GetPath(Transform transform)
    {
        if (transform == null)
        {
            return "none";
        }

        Stack<string> names = new Stack<string>();
        Transform current = transform;

        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private static bool NameLooksLikeCoat(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            (value.IndexOf("Coat", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("coatcutout", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("Jacket", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("Cloak", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("Shawl", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool IsGuestName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.IndexOf("Guest", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsPersistentAsset(UnityEngine.Object value)
    {
        return value == null || EditorUtility.IsPersistent(value);
    }

    private struct AuditCounts
    {
        public int ButlerRoomCalibrations;
        public int GuestPrefabInstances;
        public int RoomPersonWalkerGuests;
        public int ProjectedFloorCharacters;
        public int ProjectedPropsFurniture;
        public int NonEmptyRoomVisualScaleOverrides;
        public int CoatVisualsUnderGuests;
        public int ActiveGuestScaleWriters;
    }
}
