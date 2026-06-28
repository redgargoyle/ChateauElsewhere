using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class ButlerRoomScaleCalibrationWindow : EditorWindow
{
    private const string PlayModeWarning = "Stop Play Mode before saving room profile calibration.";
    private const string NoPlayerWarning = "No safe player object found. Drag the scene object named 'player' into the Player / Butler Object field.";
    private const string OldCalibrationWarning =
        "Old Butler calibration data used final local scale and may need manual review. The new source of truth is RoomPerspectiveProfile.";

    private PointClickPlayerMovement selectedButler;
    private RoomPerspectiveProfile selectedProfile;
    private int selectedRoomIndex;
    private float previewProfileScale = 1f;
    private Vector2 scroll;
    private bool livePreview = true;
    private bool advancedFoldout;
    private string lastStatus = string.Empty;

    private RoomPerspectiveProfile proofProfile;
    private float proofFrontY;
    private float proofBackY;
    private float proofFrontScale = 1f;
    private float proofBackScale = 1f;
    private bool hasProofSnapshot;

    [MenuItem("Tools/Characters/Room Character Scale Calibration")]
    public static void Open()
    {
        GetWindow<ButlerRoomScaleCalibrationWindow>("Room Character Scale");
    }

    [MenuItem("Tools/Butler/Room Scale Calibration")]
    public static void OpenLegacyMenu()
    {
        Open();
    }

    private void OnEnable()
    {
        SetSelectedButler(FindScenePlayer(), "Ready.");
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Room Character Scale", EditorStyles.boldLabel);

        DrawTopControls();

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox(PlayModeWarning, MessageType.Warning);
        }

        if (selectedButler == null)
        {
            EditorGUILayout.HelpBox(NoPlayerWarning, MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        string[] rooms = BuildRoomOptions();

        if (rooms.Length == 0)
        {
            EditorGUILayout.HelpBox("No loaded scene RoomContentGroup rooms were found.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        string selectedRoom = ResolveSelectedRoom(rooms);
        DrawRoomSelection(rooms, ref selectedRoom);

        RoomContentGroup roomContent = FindRoomContent(selectedRoom);
        DrawProfileSelection(roomContent);

        if (selectedProfile == null)
        {
            EditorGUILayout.HelpBox("The selected room does not have a RoomPerspectiveProfile assigned.", MessageType.Warning);
            DrawApplyProfileTools();
            DrawAdvancedTools(selectedRoom);
            EditorGUILayout.EndScrollView();
            return;
        }

        if (!TryGetCurrentRoomLocalFootPoint(selectedRoom, out Vector2 roomLocalFootPoint))
        {
            roomLocalFootPoint = Vector2.zero;
        }

        DrawStatus(selectedRoom, roomLocalFootPoint);
        DrawPrimaryWorkflow(selectedRoom, roomLocalFootPoint);
        DrawApplyProfileTools();
        DrawAdvancedTools(selectedRoom);

        EditorGUILayout.EndScrollView();
    }

    private void DrawTopControls()
    {
        EditorGUI.BeginChangeCheck();
        PointClickPlayerMovement nextButler = (PointClickPlayerMovement)EditorGUILayout.ObjectField(
            "Player / Butler Object",
            selectedButler,
            typeof(PointClickPlayerMovement),
            true);

        if (EditorGUI.EndChangeCheck())
        {
            SetSelectedButler(nextButler, "Selected Player / Butler Object.");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Find Scene Player"))
            {
                SetSelectedButler(FindScenePlayer(), "Found scene player.");

                if (selectedButler != null)
                {
                    Selection.activeGameObject = selectedButler.gameObject;
                    EditorGUIUtility.PingObject(selectedButler);
                }
            }

            if (GUILayout.Button("Audit Character Scaling"))
            {
                CharacterScalingAuditWindow.Open();
            }
        }
    }

    private void DrawRoomSelection(string[] rooms, ref string selectedRoom)
    {
        EditorGUI.BeginChangeCheck();
        selectedRoomIndex = EditorGUILayout.Popup("Selected Room", selectedRoomIndex, rooms);

        if (EditorGUI.EndChangeCheck())
        {
            selectedRoom = rooms[selectedRoomIndex];
            selectedButler.SetEditorSelectedButlerScaleRoomId(selectedRoom);
            selectedProfile = FindProfileForRoom(selectedRoom);
            SyncPreviewScaleFromProfile(selectedRoom);
            selectedButler.RefreshPerspectiveScaleNow(true);
            MarkDirty(selectedButler);
        }
    }

    private void DrawProfileSelection(RoomContentGroup roomContent)
    {
        EditorGUI.BeginChangeCheck();
        RoomPerspectiveProfile nextProfile = (RoomPerspectiveProfile)EditorGUILayout.ObjectField(
            "Selected RoomPerspectiveProfile",
            selectedProfile,
            typeof(RoomPerspectiveProfile),
            false);

        if (EditorGUI.EndChangeCheck())
        {
            selectedProfile = nextProfile;

            if (roomContent != null && !Application.isPlaying)
            {
                Undo.RecordObject(roomContent, "Assign Room Perspective Profile");
                roomContent.SetPerspectiveProfile(selectedProfile);
                MarkDirty(roomContent);
            }
        }
    }

    private void DrawStatus(string selectedRoom, Vector2 roomLocalFootPoint)
    {
        float currentProfileScale = selectedProfile.GetScale(roomLocalFootPoint);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Selected Room", selectedRoom);
            EditorGUILayout.FloatField("Current Room-Local Foot Y", roomLocalFootPoint.y);
            EditorGUILayout.FloatField("Current Profile Scale Here", currentProfileScale);
            EditorGUILayout.FloatField("Preview Room Character Scale Here", previewProfileScale);
            EditorGUILayout.FloatField("Current Room Stage Scale Ratio", selectedButler.CurrentRoomStageScaleRatio);
            EditorGUILayout.Vector3Field("Current Transform localScale", selectedButler.transform.localScale);
        }

        if (!string.IsNullOrWhiteSpace(lastStatus))
        {
            EditorGUILayout.HelpBox(lastStatus, MessageType.None);
        }
    }

    private void DrawPrimaryWorkflow(string selectedRoom, Vector2 roomLocalFootPoint)
    {
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            previewProfileScale = DrawPreviewProfileScaleControl(previewProfileScale, out bool previewChanged);
            livePreview = EditorGUILayout.Toggle("Live Preview", livePreview);

            if (previewChanged && livePreview)
            {
                PreviewProfileScaleOnButler();
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("STEP 1 - FRONT / closest to camera", EditorStyles.boldLabel);

            if (GUILayout.Button("SAVE FRONT TO ROOM PROFILE: Current Position + Preview Scale"))
            {
                SaveProfileEndpoint(selectedRoom, roomLocalFootPoint.y, true);
            }

            EditorGUILayout.LabelField("STEP 2 - BACK / farthest from camera", EditorStyles.boldLabel);

            if (GUILayout.Button("SAVE BACK TO ROOM PROFILE: Current Position + Preview Scale"))
            {
                SaveProfileEndpoint(selectedRoom, roomLocalFootPoint.y, false);
            }

            EditorGUILayout.LabelField("STEP 3 - REFRESH / TEST", EditorStyles.boldLabel);

            if (GUILayout.Button("REFRESH ALL CHARACTERS USING THIS ROOM PROFILE"))
            {
                int refreshedCount = RefreshAllCharactersUsingProfile(selectedProfile, true);
                lastStatus = $"Refreshed {refreshedCount} character(s) using {selectedProfile.name}.";
            }

            if (GUILayout.Button("PREVIEW PROFILE SCALE AT CURRENT POSITION"))
            {
                previewProfileScale = selectedProfile.GetScale(roomLocalFootPoint);
                PreviewProfileScaleOnButler();
                lastStatus = $"Previewing profile scale {previewProfileScale:0.###} at Y={roomLocalFootPoint.y:0.###}.";
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("SAVE PROFILE ASSET"))
            {
                EditorUtility.SetDirty(selectedProfile);
                AssetDatabase.SaveAssets();
                lastStatus = $"Saved profile asset {selectedProfile.name}.";
            }

            if (GUILayout.Button("SAVE SCENE"))
            {
                EditorSceneManager.SaveOpenScenes();
                lastStatus = "Saved open scene(s).";
            }
        }
    }

    private void DrawApplyProfileTools()
    {
        CountAssignedRoomPeople(out int projectedWithProfiles, out int projectedCount, out int walkersWithProfiles, out int walkerCount);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Apply Profile Scaling To All Characters In Loaded Scenes", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.LabelField("Floor characters with room profiles:", $"{projectedWithProfiles} / {projectedCount}");
            EditorGUILayout.LabelField("Room walkers using room profiles:", $"{walkersWithProfiles} / {walkerCount}");
        }

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("ASSIGN ROOM PROFILES TO ALL ROOM PEOPLE"))
            {
                AssignRoomProfilesToAllRoomPeople(out int projectedChanged, out int walkersChanged);
                lastStatus = $"Assigned room profiles to {projectedChanged} projected floor character(s) and {walkersChanged} walker(s).";
            }
        }
    }

    private void DrawAdvancedTools(string selectedRoom)
    {
        advancedFoldout = EditorGUILayout.Foldout(advancedFoldout, "Advanced", true);

        if (!advancedFoldout)
        {
            return;
        }

        EditorGUILayout.HelpBox(OldCalibrationWarning, MessageType.Info);

        using (new EditorGUI.DisabledScope(Application.isPlaying || selectedProfile == null || selectedButler == null))
        {
            if (GUILayout.Button("MIGRATE OLD BUTLER CALIBRATION INTO ROOM PROFILE"))
            {
                MigrateOldButlerCalibration(selectedRoom);
            }

            if (GUILayout.Button("DISABLE OLD PER-ROOM GUEST VISUAL SCALE OVERRIDES"))
            {
                int changedCount = SetOldGuestVisualScaleOverrides(false);
                lastStatus = $"Disabled old per-room guest visual scale overrides on {changedCount} floor character(s).";
            }

            if (GUILayout.Button("ENABLE OLD PER-ROOM GUEST VISUAL SCALE OVERRIDES"))
            {
                int changedCount = SetOldGuestVisualScaleOverrides(true);
                lastStatus = $"Enabled old per-room guest visual scale overrides on {changedCount} floor character(s).";
            }

            if (GUILayout.Button("PROOF TEST: Temporarily Double This Room Profile Scale") &&
                EditorUtility.DisplayDialog(
                    "Proof Test Room Profile Scale",
                    "This temporarily doubles the selected profile's front/back scale and refreshes characters in loaded scenes. Continue?",
                    "Double Temporarily",
                    "Cancel"))
            {
                RunProofDoubleScale();
            }

            using (new EditorGUI.DisabledScope(!hasProofSnapshot))
            {
                if (GUILayout.Button("UNDO PROOF TEST / RESTORE PROFILE SCALE"))
                {
                    RestoreProofScale();
                }
            }
        }
    }

    private static float DrawPreviewProfileScaleControl(float currentValue, out bool changed)
    {
        changed = false;
        float nextValue = Mathf.Max(0.001f, currentValue);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel("Preview Room Character Scale Here");

            EditorGUI.BeginChangeCheck();
            nextValue = GUILayout.HorizontalSlider(nextValue, 0.10f, 3.00f);
            if (EditorGUI.EndChangeCheck())
            {
                changed = true;
            }

            EditorGUI.BeginChangeCheck();
            nextValue = EditorGUILayout.FloatField(nextValue, GUILayout.Width(72f));
            if (EditorGUI.EndChangeCheck())
            {
                changed = true;
            }

            if (GUILayout.Button("-0.05", GUILayout.Width(52f)))
            {
                nextValue -= 0.05f;
                changed = true;
            }

            if (GUILayout.Button("+0.05", GUILayout.Width(52f)))
            {
                nextValue += 0.05f;
                changed = true;
            }
        }

        return Mathf.Max(0.001f, nextValue);
    }

    private void SaveProfileEndpoint(string selectedRoom, float roomLocalFootY, bool front)
    {
        if (selectedProfile == null)
        {
            lastStatus = "No RoomPerspectiveProfile is selected.";
            return;
        }

        Undo.RecordObject(selectedProfile, front ? "Save Front Room Character Scale" : "Save Back Room Character Scale");

        float frontY = front ? roomLocalFootY : selectedProfile.NearFootY;
        float frontScale = front ? previewProfileScale : selectedProfile.NearScale;
        float backY = front ? selectedProfile.FarFootY : roomLocalFootY;
        float backScale = front ? selectedProfile.FarScale : previewProfileScale;

        selectedProfile.SetCharacterScaleCalibration(frontY, frontScale, backY, backScale);
        EditorUtility.SetDirty(selectedProfile);
        int refreshedCount = RefreshAllCharactersUsingProfile(selectedProfile, true);
        PreviewProfileScaleOnButler();
        lastStatus = $"Saved {(front ? "FRONT" : "BACK")} to {selectedProfile.name} for {selectedRoom}: Y={roomLocalFootY:0.###}, scale={previewProfileScale:0.###}. Refreshed {refreshedCount} character(s).";
    }

    private void PreviewProfileScaleOnButler()
    {
        if (selectedButler == null)
        {
            return;
        }

        Undo.RecordObject(selectedButler.transform, "Preview Room Character Scale");
        selectedButler.EnsureButlerCalibrationBaseScale();
        Vector3 referenceScale = selectedButler.ButlerCalibrationBaseLocalScale;
        float roomStageScale = Mathf.Max(0.001f, selectedButler.CurrentRoomStageScaleRatio);
        float scale = Mathf.Max(0.001f, previewProfileScale) * roomStageScale;
        selectedButler.transform.localScale = new Vector3(
            referenceScale.x * scale,
            referenceScale.y * scale,
            referenceScale.z);
        MarkTransformDirty(selectedButler);
    }

    private void MigrateOldButlerCalibration(string selectedRoom)
    {
        if (!selectedButler.TryGetButlerRoomScaleOverride(selectedRoom, out PointClickPlayerMovement.ButlerRoomScaleOverrideData data) ||
            !data.IsComplete)
        {
            lastStatus = $"No complete old Butler calibration exists for {selectedRoom}.";
            return;
        }

        Undo.RecordObject(selectedProfile, "Migrate Old Butler Calibration Into Room Profile");
        float referenceY = Mathf.Max(0.001f, Mathf.Abs(selectedButler.ButlerCalibrationBaseLocalScale.y));
        float frontScale = Mathf.Max(0.001f, data.FrontFinalLocalScaleY / referenceY);
        float backScale = Mathf.Max(0.001f, data.BackFinalLocalScaleY / referenceY);
        selectedProfile.SetCharacterScaleCalibration(
            data.FrontRoomLocalFootY,
            frontScale,
            data.BackRoomLocalFootY,
            backScale);
        EditorUtility.SetDirty(selectedProfile);
        previewProfileScale = frontScale;
        int refreshedCount = RefreshAllCharactersUsingProfile(selectedProfile, true);
        lastStatus = $"Migrated old Butler calibration into {selectedProfile.name}. Review manually. Refreshed {refreshedCount} character(s).";
    }

    private void RunProofDoubleScale()
    {
        if (selectedProfile == null)
        {
            return;
        }

        proofProfile = selectedProfile;
        proofFrontY = selectedProfile.NearFootY;
        proofBackY = selectedProfile.FarFootY;
        proofFrontScale = selectedProfile.NearScale;
        proofBackScale = selectedProfile.FarScale;
        hasProofSnapshot = true;

        Undo.RecordObject(selectedProfile, "Proof Double Room Profile Scale");
        selectedProfile.SetCharacterScaleCalibration(
            proofFrontY,
            proofFrontScale * 2f,
            proofBackY,
            proofBackScale * 2f);
        EditorUtility.SetDirty(selectedProfile);
        int refreshedCount = RefreshAllCharactersUsingProfile(selectedProfile, true);
        lastStatus = $"Proof test doubled {selectedProfile.name}. Refreshed {refreshedCount} character(s). Use UNDO PROOF TEST before saving.";
    }

    private void RestoreProofScale()
    {
        if (!hasProofSnapshot || proofProfile == null)
        {
            return;
        }

        Undo.RecordObject(proofProfile, "Restore Proof Room Profile Scale");
        proofProfile.SetCharacterScaleCalibration(proofFrontY, proofFrontScale, proofBackY, proofBackScale);
        EditorUtility.SetDirty(proofProfile);
        int refreshedCount = RefreshAllCharactersUsingProfile(proofProfile, true);
        hasProofSnapshot = false;
        lastStatus = $"Restored proof test scale for {proofProfile.name}. Refreshed {refreshedCount} character(s).";
    }

    private static int RefreshAllCharactersUsingProfile(RoomPerspectiveProfile profile, bool recordUndo)
    {
        if (profile == null)
        {
            return 0;
        }

        int refreshedCount = 0;
        RoomProjectedEntity[] entities = Resources.FindObjectsOfTypeAll<RoomProjectedEntity>();

        for (int i = 0; i < entities.Length; i++)
        {
            RoomProjectedEntity entity = entities[i];

            if (!IsLoadedSceneObject(entity) || entity.RoomProfile != profile)
            {
                continue;
            }

            if (recordUndo)
            {
                Undo.RecordObject(entity, "Refresh Room Profile Projection");
                if (entity.VisualRoot != null)
                {
                    Undo.RecordObject(entity.VisualRoot, "Refresh Room Profile Projection");
                }
            }

            entity.RefreshVisualTargets();
            entity.ApplyProjection();
            MarkProjectedEntityDirty(entity);
            refreshedCount++;
        }

        RoomPersonWalker2D[] walkers = Resources.FindObjectsOfTypeAll<RoomPersonWalker2D>();

        for (int i = 0; i < walkers.Length; i++)
        {
            RoomPersonWalker2D walker = walkers[i];

            if (!IsLoadedSceneObject(walker) || !walker.UsesPerspectiveProfile(profile))
            {
                continue;
            }

            if (recordUndo)
            {
                Undo.RecordObject(walker, "Refresh Room Profile Walker");
                Undo.RecordObject(walker.transform, "Refresh Room Profile Walker");
            }

            walker.RefreshDepthVisualsNow();
            MarkDirty(walker);
            refreshedCount++;
        }

        PointClickPlayerMovement[] movements = Resources.FindObjectsOfTypeAll<PointClickPlayerMovement>();

        for (int i = 0; i < movements.Length; i++)
        {
            PointClickPlayerMovement movement = movements[i];

            if (!IsLoadedSceneObject(movement) || !movement.UsesPerspectiveProfile(profile))
            {
                continue;
            }

            if (recordUndo)
            {
                Undo.RecordObject(movement.transform, "Refresh Room Profile Player");
            }

            movement.RefreshPerspectiveScaleNow(true);
            MarkDirty(movement);
            refreshedCount++;
        }

        SceneView.RepaintAll();
        return refreshedCount;
    }

    private static void AssignRoomProfilesToAllRoomPeople(out int projectedChanged, out int walkersChanged)
    {
        projectedChanged = 0;
        walkersChanged = 0;

        RoomProjectedEntity[] entities = Resources.FindObjectsOfTypeAll<RoomProjectedEntity>();

        for (int i = 0; i < entities.Length; i++)
        {
            RoomProjectedEntity entity = entities[i];

            if (!IsLoadedSceneObject(entity) || entity.Mode != RoomProjectedEntity.ProjectionMode.FloorCharacter)
            {
                continue;
            }

            RoomContentGroup roomContent = entity.GetComponentInParent<RoomContentGroup>(true);
            RoomPerspectiveProfile profile = roomContent != null ? roomContent.PerspectiveProfile : entity.RoomProfile;

            if (profile == null)
            {
                continue;
            }

            Undo.RecordObject(entity, "Assign Room Profile To Floor Character");
            entity.SetRoomProfile(profile);
            entity.SetApplyScale(true, false);
            entity.SetIgnoreRoomVisualScaleOverridesWhenUsingSharedCharacterScale(true, false);
            entity.ApplyProjection();
            MarkProjectedEntityDirty(entity);
            projectedChanged++;
        }

        RoomPersonWalker2D[] walkers = Resources.FindObjectsOfTypeAll<RoomPersonWalker2D>();

        for (int i = 0; i < walkers.Length; i++)
        {
            RoomPersonWalker2D walker = walkers[i];

            if (!IsLoadedSceneObject(walker))
            {
                continue;
            }

            RoomContentGroup roomContent = walker.GetComponentInParent<RoomContentGroup>(true);

            if (roomContent == null || roomContent.PerspectiveProfile == null)
            {
                continue;
            }

            Undo.RecordObject(walker, "Assign Room Profile To Walker");
            walker.SetRoomPerspectiveProfile(roomContent.PerspectiveProfile, false);
            walker.SetRoomPerspectiveProfileScaleEnabled(true, false);
            walker.RefreshDepthVisualsNow();
            MarkDirty(walker);
            walkersChanged++;
        }
    }

    private static int SetOldGuestVisualScaleOverrides(bool enabled)
    {
        int changedCount = 0;
        RoomProjectedEntity[] entities = Resources.FindObjectsOfTypeAll<RoomProjectedEntity>();

        for (int i = 0; i < entities.Length; i++)
        {
            RoomProjectedEntity entity = entities[i];

            if (!IsLoadedSceneObject(entity) || entity.Mode != RoomProjectedEntity.ProjectionMode.FloorCharacter)
            {
                continue;
            }

            Undo.RecordObject(entity, enabled ? "Enable Old Guest Visual Scale Overrides" : "Disable Old Guest Visual Scale Overrides");
            entity.SetRoomVisualScaleOverridesEnabled(enabled, false);
            entity.ApplyProjection();
            MarkProjectedEntityDirty(entity);
            changedCount++;
        }

        return changedCount;
    }

    private static void CountAssignedRoomPeople(
        out int projectedWithProfiles,
        out int projectedCount,
        out int walkersWithProfiles,
        out int walkerCount)
    {
        projectedWithProfiles = 0;
        projectedCount = 0;
        walkersWithProfiles = 0;
        walkerCount = 0;

        RoomProjectedEntity[] entities = Resources.FindObjectsOfTypeAll<RoomProjectedEntity>();

        for (int i = 0; i < entities.Length; i++)
        {
            RoomProjectedEntity entity = entities[i];

            if (!IsLoadedSceneObject(entity) || entity.Mode != RoomProjectedEntity.ProjectionMode.FloorCharacter)
            {
                continue;
            }

            projectedCount++;

            if (entity.RoomProfile != null && entity.ApplyScale)
            {
                projectedWithProfiles++;
            }
        }

        RoomPersonWalker2D[] walkers = Resources.FindObjectsOfTypeAll<RoomPersonWalker2D>();

        for (int i = 0; i < walkers.Length; i++)
        {
            RoomPersonWalker2D walker = walkers[i];

            if (!IsLoadedSceneObject(walker))
            {
                continue;
            }

            walkerCount++;

            if (walker.RoomProfile != null && walker.UsesRoomPerspectiveProfileScale)
            {
                walkersWithProfiles++;
            }
        }
    }

    private void SetSelectedButler(PointClickPlayerMovement movement, string status)
    {
        selectedButler = movement;
        lastStatus = status;

        if (selectedButler == null)
        {
            return;
        }

        selectedButler.EnsureButlerCalibrationBaseScale();
        selectedButler.SetEditorSelectedButlerScaleRoomId(selectedButler.CurrentButlerScaleRoomId);
        selectedProfile = FindProfileForRoom(selectedButler.CurrentButlerScaleRoomId);
        SyncPreviewScaleFromProfile(selectedButler.CurrentButlerScaleRoomId);
    }

    private void SyncPreviewScaleFromProfile(string selectedRoom)
    {
        if (selectedProfile == null || !TryGetCurrentRoomLocalFootPoint(selectedRoom, out Vector2 footPoint))
        {
            previewProfileScale = 1f;
            return;
        }

        previewProfileScale = selectedProfile.GetScale(footPoint);
    }

    private bool TryGetCurrentRoomLocalFootPoint(string selectedRoom, out Vector2 roomLocalFootPoint)
    {
        roomLocalFootPoint = Vector2.zero;
        return selectedButler != null &&
            selectedButler.TryGetButlerCalibrationContext(selectedRoom, true, out _, out roomLocalFootPoint);
    }

    internal static PointClickPlayerMovement FindScenePlayer()
    {
        PointClickPlayerMovement[] movements = Resources.FindObjectsOfTypeAll<PointClickPlayerMovement>();
        PointClickPlayerMovement fallback = null;

        for (int i = 0; i < movements.Length; i++)
        {
            PointClickPlayerMovement movement = movements[i];

            if (!IsLoadedSceneObject(movement))
            {
                continue;
            }

            fallback ??= movement;

            if (string.Equals(movement.gameObject.name, "player", StringComparison.OrdinalIgnoreCase))
            {
                return movement;
            }
        }

        return fallback;
    }

    private string ResolveSelectedRoom(string[] rooms)
    {
        string selectedRoom = selectedButler != null ? selectedButler.EditorSelectedButlerScaleRoomId : string.Empty;

        if (string.IsNullOrWhiteSpace(selectedRoom) && selectedButler != null)
        {
            selectedRoom = selectedButler.CurrentButlerScaleRoomId;
        }

        for (int i = 0; i < rooms.Length; i++)
        {
            if (SameRoom(rooms[i], selectedRoom))
            {
                selectedRoomIndex = i;
                selectedProfile = FindProfileForRoom(rooms[i]);
                return rooms[i];
            }
        }

        selectedRoomIndex = Mathf.Clamp(selectedRoomIndex, 0, rooms.Length - 1);
        selectedProfile = FindProfileForRoom(rooms[selectedRoomIndex]);
        return rooms[selectedRoomIndex];
    }

    private static string[] BuildRoomOptions()
    {
        List<string> rooms = new List<string>();
        RoomContentGroup[] roomContentGroups = Resources.FindObjectsOfTypeAll<RoomContentGroup>();

        for (int i = 0; i < roomContentGroups.Length; i++)
        {
            RoomContentGroup roomContentGroup = roomContentGroups[i];

            if (IsLoadedSceneObject(roomContentGroup))
            {
                AddRoom(rooms, roomContentGroup.RoomName);
            }
        }

        rooms.Sort(StringComparer.OrdinalIgnoreCase);
        return rooms.ToArray();
    }

    private static RoomPerspectiveProfile FindProfileForRoom(string roomId)
    {
        RoomContentGroup roomContent = FindRoomContent(roomId);
        return roomContent != null ? roomContent.PerspectiveProfile : null;
    }

    private static RoomContentGroup FindRoomContent(string roomId)
    {
        RoomContentGroup[] roomContentGroups = Resources.FindObjectsOfTypeAll<RoomContentGroup>();

        for (int i = 0; i < roomContentGroups.Length; i++)
        {
            RoomContentGroup roomContentGroup = roomContentGroups[i];

            if (IsLoadedSceneObject(roomContentGroup) && SameRoom(roomContentGroup.RoomName, roomId))
            {
                return roomContentGroup;
            }
        }

        return null;
    }

    private static bool IsLoadedSceneObject(Component component)
    {
        return component != null &&
            component.gameObject != null &&
            component.gameObject.scene.IsValid() &&
            component.gameObject.scene.isLoaded &&
            !EditorUtility.IsPersistent(component);
    }

    private static void MarkProjectedEntityDirty(RoomProjectedEntity entity)
    {
        if (entity == null)
        {
            return;
        }

        MarkDirty(entity);

        if (entity.VisualRoot != null)
        {
            EditorUtility.SetDirty(entity.VisualRoot);
        }
    }

    private static void MarkTransformDirty(PointClickPlayerMovement movement)
    {
        if (movement == null)
        {
            return;
        }

        EditorUtility.SetDirty(movement.transform);

        if (!Application.isPlaying && movement.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(movement.gameObject.scene);
        }
    }

    private static void MarkDirty(Component component)
    {
        if (component == null)
        {
            return;
        }

        EditorUtility.SetDirty(component);

        if (!Application.isPlaying && component.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
        }
    }

    private static void AddRoom(List<string> rooms, string roomId)
    {
        if (rooms == null || string.IsNullOrWhiteSpace(roomId))
        {
            return;
        }

        string cleanRoomId = roomId.Trim();

        for (int i = 0; i < rooms.Count; i++)
        {
            if (SameRoom(rooms[i], cleanRoomId))
            {
                return;
            }
        }

        rooms.Add(cleanRoomId);
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

        char[] normalized = new char[value.Length];
        int count = 0;

        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];

            if (!char.IsLetterOrDigit(character))
            {
                continue;
            }

            normalized[count] = char.ToLowerInvariant(character);
            count++;
        }

        return new string(normalized, 0, count);
    }
}
