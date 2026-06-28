using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class CharacterScalingAuditWindow : EditorWindow
{
    private Vector2 scroll;
    private string report = string.Empty;

    [MenuItem("Tools/Characters/Audit Character Scaling")]
    public static void Open()
    {
        CharacterScalingAuditWindow window = GetWindow<CharacterScalingAuditWindow>("Character Scaling Audit");
        window.RefreshReport();
    }

    private void OnEnable()
    {
        RefreshReport();
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh Audit"))
            {
                RefreshReport();
            }

            if (GUILayout.Button("Copy Report"))
            {
                EditorGUIUtility.systemCopyBuffer = report;
            }
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    internal void RefreshReport()
    {
        report = BuildReport();
    }

    internal static string BuildReport()
    {
        StringBuilder builder = new StringBuilder();
        Dictionary<RoomPerspectiveProfile, int> profileUseCounts = new Dictionary<RoomPerspectiveProfile, int>();
        HashSet<RoomPerspectiveProfile> loadedProfiles = CollectLoadedRoomProfiles();

        builder.AppendLine("Character Scaling Audit");
        builder.AppendLine();
        AppendPointClickMovements(builder, profileUseCounts);
        AppendProjectedEntities(builder, profileUseCounts);
        AppendRoomPersonWalkers(builder, profileUseCounts);
        AppendUnusedProfileWarnings(builder, loadedProfiles, profileUseCounts);

        return builder.ToString();
    }

    private static void AppendPointClickMovements(StringBuilder builder, Dictionary<RoomPerspectiveProfile, int> profileUseCounts)
    {
        builder.AppendLine("PointClickPlayerMovement");
        PointClickPlayerMovement[] movements = Resources.FindObjectsOfTypeAll<PointClickPlayerMovement>();

        for (int i = 0; i < movements.Length; i++)
        {
            PointClickPlayerMovement movement = movements[i];

            if (!IsLoadedSceneObject(movement))
            {
                continue;
            }

            movement.TryGetCurrentRoomPerspectiveProfileForAudit(out RoomPerspectiveProfile profile);
            string currentRoom = movement.CurrentButlerScaleRoomId;
            Vector2 roomLocalFootPoint = Vector2.zero;
            float profileScale = 1f;

            if (movement.TryGetButlerCalibrationContext(currentRoom, !Application.isPlaying, out string contextRoom, out Vector2 footPoint))
            {
                currentRoom = contextRoom;
                roomLocalFootPoint = footPoint;
            }

            if (profile != null)
            {
                profileScale = profile.GetScale(roomLocalFootPoint);
                IncrementProfileUse(profileUseCounts, profile);
            }

            builder.AppendLine($"- {GetObjectPath(movement.transform)}");
            builder.AppendLine($"  current room: {currentRoom}");
            builder.AppendLine($"  has RoomPerspectiveProfile?: {YesNo(profile != null)}");
            builder.AppendLine($"  profile name/id: {GetProfileLabel(profile)}");
            builder.AppendLine($"  current room-local foot Y: {roomLocalFootPoint.y:0.###}");
            builder.AppendLine($"  profile scale here: {profileScale:0.###}");
            builder.AppendLine($"  currentRoomStageScaleRatio: {movement.CurrentRoomStageScaleRatio:0.###}");
            builder.AppendLine($"  current transform.localScale: {movement.transform.localScale}");

            if (profile == null)
            {
                builder.AppendLine("  WARNING: Butler has no RoomPerspectiveProfile.");
            }

            if (movement.UsesButlerRoomScaleOverrides &&
                movement.HasCompleteButlerRoomScaleOverride(currentRoom))
            {
                builder.AppendLine("  WARNING: Butler has custom final local scale override active. Runtime scale should now come from RoomPerspectiveProfile.");
            }

            if (!movement.AppliesPerspectiveScale)
            {
                builder.AppendLine("  WARNING: Butler is not multiplying by currentRoomStageScaleRatio because perspective scaling is disabled.");
            }
        }

        builder.AppendLine();
    }

    private static void AppendProjectedEntities(StringBuilder builder, Dictionary<RoomPerspectiveProfile, int> profileUseCounts)
    {
        builder.AppendLine("RoomProjectedEntity");
        RoomProjectedEntity[] entities = Resources.FindObjectsOfTypeAll<RoomProjectedEntity>();

        for (int i = 0; i < entities.Length; i++)
        {
            RoomProjectedEntity entity = entities[i];

            if (!IsLoadedSceneObject(entity))
            {
                continue;
            }

            RoomPerspectiveProfile profile = entity.RoomProfile;
            float profileScale = profile != null ? profile.GetScale(entity.RoomLocalFootPoint) : 1f;
            float heightMultiplier = entity.VisualProfile != null ? entity.VisualProfile.HeightScaleMultiplier : 1f;

            if (profile != null)
            {
                IncrementProfileUse(profileUseCounts, profile);
            }

            builder.AppendLine($"- {GetObjectPath(entity.transform)}");
            builder.AppendLine($"  projection mode: {entity.Mode}");
            builder.AppendLine($"  roomProfile assigned?: {YesNo(profile != null)}");
            builder.AppendLine($"  room id: {(profile != null ? profile.RoomId : string.Empty)}");
            builder.AppendLine($"  roomLocalFootPoint.y: {entity.RoomLocalFootPoint.y:0.###}");
            builder.AppendLine($"  profile scale here: {profileScale:0.###}");
            builder.AppendLine($"  visualProfile height multiplier: {heightMultiplier:0.###}");
            builder.AppendLine($"  current visual root localScale: {(entity.VisualRoot != null ? entity.VisualRoot.localScale : Vector3.one)}");
            builder.AppendLine($"  useRoomVisualScaleOverrides: {YesNo(entity.UsesRoomVisualScaleOverrides)}");

            if (entity.Mode == RoomProjectedEntity.ProjectionMode.FloorCharacter)
            {
                if (profile == null)
                {
                    builder.AppendLine("  WARNING: Guest has no RoomPerspectiveProfile.");
                }

                if (!entity.ApplyScale)
                {
                    builder.AppendLine("  WARNING: Guest is not using RoomPerspectiveProfile scale.");
                }

                if (entity.UsesRoomVisualScaleOverrides)
                {
                    builder.AppendLine("  WARNING: Guest has old room visual overrides enabled.");
                }
            }
        }

        builder.AppendLine();
    }

    private static void AppendRoomPersonWalkers(StringBuilder builder, Dictionary<RoomPerspectiveProfile, int> profileUseCounts)
    {
        builder.AppendLine("RoomPersonWalker2D");
        RoomPersonWalker2D[] walkers = Resources.FindObjectsOfTypeAll<RoomPersonWalker2D>();

        for (int i = 0; i < walkers.Length; i++)
        {
            RoomPersonWalker2D walker = walkers[i];

            if (!IsLoadedSceneObject(walker))
            {
                continue;
            }

            RoomPerspectiveProfile profile = walker.RoomProfile;
            RectTransform rectTransform = walker.transform as RectTransform;
            float profileScale = profile != null ? profile.GetScale(walker.CurrentPosition) : 1f;

            if (profile != null)
            {
                IncrementProfileUse(profileUseCounts, profile);
            }

            builder.AppendLine($"- {GetObjectPath(walker.transform)}");
            builder.AppendLine($"  roomProfile assigned?: {YesNo(profile != null)}");
            builder.AppendLine($"  useRoomPerspectiveProfileScale: {YesNo(walker.UsesRoomPerspectiveProfileScale)}");
            builder.AppendLine($"  current position Y: {walker.CurrentPosition.y:0.###}");
            builder.AppendLine($"  profile scale here: {profileScale:0.###}");
            builder.AppendLine($"  current RectTransform.localScale: {(rectTransform != null ? rectTransform.localScale : walker.transform.localScale)}");

            if (profile == null)
            {
                builder.AppendLine("  WARNING: Guest has no RoomPerspectiveProfile.");
            }

            if (!walker.UsesRoomPerspectiveProfileScale)
            {
                builder.AppendLine("  WARNING: Guest is not using RoomPerspectiveProfile scale.");
            }
        }

        builder.AppendLine();
    }

    private static void AppendUnusedProfileWarnings(
        StringBuilder builder,
        HashSet<RoomPerspectiveProfile> loadedProfiles,
        Dictionary<RoomPerspectiveProfile, int> profileUseCounts)
    {
        builder.AppendLine("Profile Warnings");

        foreach (RoomPerspectiveProfile profile in loadedProfiles)
        {
            if (profile == null || profileUseCounts.ContainsKey(profile))
            {
                continue;
            }

            builder.AppendLine($"- WARNING: Profile exists but no character uses it: {GetProfileLabel(profile)}");
        }
    }

    private static HashSet<RoomPerspectiveProfile> CollectLoadedRoomProfiles()
    {
        HashSet<RoomPerspectiveProfile> profiles = new HashSet<RoomPerspectiveProfile>();
        RoomContentGroup[] rooms = Resources.FindObjectsOfTypeAll<RoomContentGroup>();

        for (int i = 0; i < rooms.Length; i++)
        {
            RoomContentGroup room = rooms[i];

            if (IsLoadedSceneObject(room) && room.PerspectiveProfile != null)
            {
                profiles.Add(room.PerspectiveProfile);
            }
        }

        return profiles;
    }

    private static void IncrementProfileUse(Dictionary<RoomPerspectiveProfile, int> profileUseCounts, RoomPerspectiveProfile profile)
    {
        if (profile == null)
        {
            return;
        }

        profileUseCounts.TryGetValue(profile, out int count);
        profileUseCounts[profile] = count + 1;
    }

    private static bool IsLoadedSceneObject(Component component)
    {
        return component != null &&
            component.gameObject != null &&
            component.gameObject.scene.IsValid() &&
            component.gameObject.scene.isLoaded &&
            !EditorUtility.IsPersistent(component);
    }

    private static string GetProfileLabel(RoomPerspectiveProfile profile)
    {
        return profile == null ? string.Empty : $"{profile.name} / {profile.RoomId}";
    }

    private static string GetObjectPath(Transform transform)
    {
        if (transform == null)
        {
            return string.Empty;
        }

        string path = transform.name;
        Transform current = transform.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private static string YesNo(bool value)
    {
        return value ? "yes" : "no";
    }
}
