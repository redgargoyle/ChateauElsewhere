using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class ButlerRoomScaleCalibrationWindow : EditorWindow
{
    private const string InstructionText =
        "Use this in EDIT MODE. For each room: move the Butler to the FRONT, adjust Preview Butler Size Here, click SAVE FRONT. Then move him to the BACK, adjust Preview Butler Size Here, click SAVE BACK. Do not edit the Butler Transform scale manually.";
    private const string PlayModeWarning = "Stop Play Mode to save calibration.";
    private const string NoSafePlayerMessage =
        "No safe player object found. Drag the scene object named 'player' into the Butler / Player Object field.";
    private const string GuestSelectionWarning =
        "Warning: selected object looks like a guest. The actual Butler/player should be the scene object named 'player'. Drag 'player' into this field before saving calibration.";
    private const string GuestProjectionWarning =
        "Warning: selected object has guest projection components. Confirm this is not a guest before saving.";
    private const string UnexpectedPlayerWarning =
        "Warning: selected object does not look like the main player. Expected object name: player.";

    private PointClickPlayerMovement selectedButler;
    private int selectedRoomIndex;
    private Vector2 scroll;
    private bool advancedFoldout;
    private bool candidatesFoldout;
    private float previewSize = 1f;
    private string lastStatus = string.Empty;

    [MenuItem("Tools/Butler/Room Scale Calibration")]
    public static void Open()
    {
        GetWindow<ButlerRoomScaleCalibrationWindow>("Butler Room Scale");
    }

    private void OnEnable()
    {
        SetSelectedButler(FindScenePlayer(), "Ready.");
    }

    private void OnGUI()
    {
        PlayerCandidateInfo[] candidates = BuildPlayerCandidateInfos();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Butler Room Scale", EditorStyles.boldLabel);

        DrawTopControls(candidates);
        DrawCandidateFoldout(candidates);

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox(PlayModeWarning, MessageType.Warning);
        }

        if (selectedButler == null)
        {
            EditorGUILayout.HelpBox(NoSafePlayerMessage, MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        DrawSelectedPlayerWarnings(selectedButler);

        string[] rooms = BuildRoomOptions(selectedButler);

        if (rooms.Length == 0)
        {
            EditorGUILayout.HelpBox("No scene RoomContentGroup rooms were found.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        string selectedRoom = ResolveSelectedRoom(selectedButler, rooms, ref selectedRoomIndex);
        DrawRoomSelection(selectedButler, rooms, ref selectedRoomIndex, ref selectedRoom);

        EditorGUILayout.HelpBox(InstructionText, MessageType.Info);
        DrawStatus(selectedButler, selectedRoom);
        DrawPrimaryWorkflow(selectedButler, selectedRoom);
        DrawAdvancedTools(selectedButler, selectedRoom, rooms);

        EditorGUILayout.EndScrollView();
    }

    private void DrawTopControls(PlayerCandidateInfo[] candidates)
    {
        EditorGUI.BeginChangeCheck();
        PointClickPlayerMovement nextButler = (PointClickPlayerMovement)EditorGUILayout.ObjectField(
            "Butler / Player Object",
            selectedButler,
            typeof(PointClickPlayerMovement),
            true);

        if (EditorGUI.EndChangeCheck())
        {
            SetSelectedButler(nextButler, "Selected Butler / Player Object.");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Find Scene Player"))
            {
                SetSelectedButler(FindScenePlayer(candidates), "Found scene player.");

                if (selectedButler != null)
                {
                    Selection.activeGameObject = selectedButler.gameObject;
                    EditorGUIUtility.PingObject(selectedButler);
                }
            }

            using (new EditorGUI.DisabledScope(selectedButler == null || Application.isPlaying))
            {
                if (GUILayout.Button("Save Scene"))
                {
                    SaveButlerScene(selectedButler);
                    lastStatus = "Scene saved.";
                }
            }
        }
    }

    private static void DrawRoomSelection(
        PointClickPlayerMovement movement,
        string[] rooms,
        ref int selectedIndex,
        ref string selectedRoom)
    {
        EditorGUI.BeginChangeCheck();
        selectedIndex = EditorGUILayout.Popup("Current Room", selectedIndex, rooms);

        if (EditorGUI.EndChangeCheck())
        {
            selectedRoom = rooms[selectedIndex];
            RecordMovementAndTransform(movement, "Select Butler Scale Room");
            movement.SetEditorSelectedButlerScaleRoomId(selectedRoom);
            movement.RefreshPerspectiveScaleNow(true);
            MarkDirty(movement);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Previous Room"))
            {
                selectedIndex = (selectedIndex - 1 + rooms.Length) % rooms.Length;
                selectedRoom = rooms[selectedIndex];
                RecordMovementAndTransform(movement, "Select Previous Butler Scale Room");
                movement.SetEditorSelectedButlerScaleRoomId(selectedRoom);
                movement.RefreshPerspectiveScaleNow(true);
                MarkDirty(movement);
            }

            if (GUILayout.Button("Next Room"))
            {
                selectedIndex = (selectedIndex + 1) % rooms.Length;
                selectedRoom = rooms[selectedIndex];
                RecordMovementAndTransform(movement, "Select Next Butler Scale Room");
                movement.SetEditorSelectedButlerScaleRoomId(selectedRoom);
                movement.RefreshPerspectiveScaleNow(true);
                MarkDirty(movement);
            }

            if (GUILayout.Button("Ping RoomContentGroup"))
            {
                PingRoom(selectedRoom);
            }
        }
    }

    private void DrawStatus(PointClickPlayerMovement movement, string selectedRoom)
    {
        movement.TryGetButlerCalibrationContext(selectedRoom, true, out _, out Vector2 footPoint);
        bool hasOverride = movement.TryGetButlerRoomScaleOverride(selectedRoom, out PointClickPlayerMovement.ButlerRoomScaleOverrideData data);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Selected Room", selectedRoom);
            EditorGUILayout.FloatField("Current Room-Local Foot Y", footPoint.y);
            EditorGUILayout.Vector3Field("Current Transform localScale", movement.transform.localScale);
            EditorGUILayout.Vector3Field("Butler Base Local Scale", movement.ButlerCalibrationBaseLocalScale);
            EditorGUILayout.TextField("Calibration Status", GetCalibrationStatus(hasOverride, data));
        }

        if (!string.IsNullOrWhiteSpace(lastStatus))
        {
            EditorGUILayout.HelpBox(lastStatus, MessageType.None);
        }
    }

    private void DrawPrimaryWorkflow(PointClickPlayerMovement movement, string selectedRoom)
    {
        bool safeForSaving = IsSafePlayerObjectForSaving(movement);
        bool canSave = !Application.isPlaying && safeForSaving;

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            previewSize = DrawPreviewSizeControl(previewSize, out bool previewChanged);

            if (previewChanged)
            {
                PreviewCurrentSize(movement);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("STEP 1 - FRONT / closest to camera", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!canSave))
            {
                if (GUILayout.Button("SAVE FRONT: Current Position + Current Size"))
                {
                    SaveEndpoint(movement, selectedRoom, true);
                }
            }

            EditorGUILayout.LabelField("STEP 2 - BACK / farthest from camera", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!canSave))
            {
                if (GUILayout.Button("SAVE BACK: Current Position + Current Size"))
                {
                    SaveEndpoint(movement, selectedRoom, false);
                }
            }

            EditorGUILayout.LabelField("STEP 3 - TEST", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!CanTestSavedScaling(movement, selectedRoom)))
            {
                if (GUILayout.Button("TEST SAVED SCALING AT CURRENT POSITION"))
                {
                    TestSavedScaling(movement, selectedRoom);
                }
            }

            if (GUILayout.Button("RESTORE BASE SIZE PREVIEW"))
            {
                RecordMovementAndTransform(movement, "Restore Butler Base Size Preview");
                movement.RestoreButlerCalibrationBaseScalePreview();
                previewSize = 1f;
                MarkTransformDirty(movement);
                lastStatus = "Restored Butler preview to the stored base size.";
            }
        }
    }

    private void DrawAdvancedTools(PointClickPlayerMovement movement, string selectedRoom, string[] rooms)
    {
        advancedFoldout = EditorGUILayout.Foldout(advancedFoldout, "Advanced / Reset Tools", true);

        if (!advancedFoldout)
        {
            return;
        }

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("RESET THIS ROOM TO OLD DEFAULT SCALE VALUES") &&
                EditorUtility.DisplayDialog(
                    "Reset Butler Room Scale",
                    $"This overwrites the saved FRONT and BACK calibration for {selectedRoom}. Continue?",
                    "Continue",
                    "Cancel"))
            {
                RecordMovementAndTransform(movement, "Reset Butler Room Scale To Old Defaults");
                movement.InitializeButlerScaleOverrideForRoomFromCurrentPerspective(selectedRoom, false);
                MarkDirty(movement);
                lastStatus = $"Reset {selectedRoom} to old default scale values.";
            }

            if (GUILayout.Button("DELETE THIS ROOM'S BUTLER CALIBRATION") &&
                EditorUtility.DisplayDialog(
                    "Delete Butler Room Scale",
                    $"Delete the saved Butler calibration for {selectedRoom}?",
                    "Delete",
                    "Cancel"))
            {
                RecordMovementAndTransform(movement, "Delete Butler Room Scale");
                movement.RemoveButlerScaleOverrideForRoom(selectedRoom, false);
                MarkDirty(movement);
                lastStatus = $"Deleted Butler calibration for {selectedRoom}.";
            }

            if (GUILayout.Button("CAPTURE CURRENT TRANSFORM AS BUTLER BASE SCALE") &&
                EditorUtility.DisplayDialog(
                    "Capture Butler Base Scale",
                    "This replaces the stable Butler base scale with the current Transform localScale. Use only to fix setup issues. Continue?",
                    "Capture",
                    "Cancel"))
            {
                RecordMovementAndTransform(movement, "Capture Butler Base Scale");
                movement.CaptureCurrentTransformAsButlerCalibrationBaseScale();
                previewSize = 1f;
                MarkDirty(movement);
                lastStatus = "Captured current Transform localScale as Butler base scale.";
            }

            if (GUILayout.Button("INITIALIZE MISSING ROOMS FROM OLD DEFAULTS") &&
                EditorUtility.DisplayDialog(
                    "Initialize Missing Butler Room Scales",
                    "This creates old-default Butler calibration only for rooms with no saved Butler calibration. Existing rooms are not overwritten. Continue?",
                    "Initialize Missing",
                    "Cancel"))
            {
                InitializeMissingRooms(movement, rooms);
            }
        }
    }

    private static float DrawPreviewSizeControl(float currentValue, out bool changed)
    {
        changed = false;
        float nextValue = Mathf.Max(0.001f, currentValue);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel("Preview Butler Size Here");

            EditorGUI.BeginChangeCheck();
            nextValue = GUILayout.HorizontalSlider(nextValue, 0.20f, 2.50f);
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

    private void SaveEndpoint(PointClickPlayerMovement movement, string selectedRoom, bool front)
    {
        movement.EnsureButlerCalibrationBaseScale();

        if (!movement.TryGetButlerCalibrationContext(selectedRoom, true, out string roomId, out Vector2 footPoint))
        {
            lastStatus = "Could not resolve the current room-local foot point.";
            return;
        }

        RecordMovementAndTransform(movement, front ? "Save Butler Front Scale" : "Save Butler Back Scale");

        if (front)
        {
            movement.SetButlerFrontScaleForRoom(roomId, footPoint.y, previewSize, false);
            lastStatus = $"Saved FRONT for {roomId}: Y {footPoint.y:0.###}, Size {previewSize:0.###}.";
        }
        else
        {
            movement.SetButlerBackScaleForRoom(roomId, footPoint.y, previewSize, false);
            lastStatus = $"Saved BACK for {roomId}: Y {footPoint.y:0.###}, Size {previewSize:0.###}.";
        }

        MarkDirty(movement);
    }

    private void PreviewCurrentSize(PointClickPlayerMovement movement)
    {
        RecordMovementAndTransform(movement, "Preview Butler Size Here");
        movement.ApplyButlerScalePreview(previewSize);
        MarkTransformDirty(movement);
        lastStatus = $"Previewing Butler size {previewSize:0.###}. This has not saved FRONT or BACK.";
    }

    private void TestSavedScaling(PointClickPlayerMovement movement, string selectedRoom)
    {
        if (!movement.TryGetButlerCalibrationContext(selectedRoom, true, out string roomId, out Vector2 footPoint) ||
            !movement.TryEvaluateButlerRoomScale(roomId, footPoint.y, out float calibratedScale))
        {
            lastStatus = "Saved FRONT and BACK are required before testing room scaling.";
            return;
        }

        previewSize = calibratedScale;
        RecordMovementAndTransform(movement, "Test Saved Butler Scaling");
        movement.ApplyButlerScalePreview(previewSize);
        MarkTransformDirty(movement);
        lastStatus = $"Tested saved scaling at Y {footPoint.y:0.###}: Size {previewSize:0.###}.";
    }

    private static bool CanTestSavedScaling(PointClickPlayerMovement movement, string selectedRoom)
    {
        return !Application.isPlaying &&
            movement != null &&
            movement.TryGetButlerCalibrationContext(selectedRoom, true, out string roomId, out Vector2 footPoint) &&
            movement.TryEvaluateButlerRoomScale(roomId, footPoint.y, out _);
    }

    private void InitializeMissingRooms(PointClickPlayerMovement movement, string[] rooms)
    {
        int initializedCount = 0;
        RecordMovementAndTransform(movement, "Initialize Missing Butler Room Scales");

        for (int i = 0; i < rooms.Length; i++)
        {
            string room = rooms[i];

            if (movement.TryGetButlerRoomScaleOverride(room, out _))
            {
                continue;
            }

            if (movement.InitializeButlerScaleOverrideForRoomFromCurrentPerspective(room, false))
            {
                initializedCount++;
            }
        }

        if (initializedCount > 0)
        {
            MarkDirty(movement);
        }

        lastStatus = $"Initialized {initializedCount} missing rooms from old default scale values.";
    }

    private void SetSelectedButler(PointClickPlayerMovement movement, string status)
    {
        selectedButler = movement;

        if (selectedButler == null)
        {
            return;
        }

        selectedButler.EnsureButlerCalibrationBaseScale();
        previewSize = selectedButler.CaptureCurrentButlerPreviewScale();
        lastStatus = status;
    }

    internal static PointClickPlayerMovement FindScenePlayer()
    {
        return FindScenePlayer(BuildPlayerCandidateInfos());
    }

    private static PointClickPlayerMovement FindScenePlayer(PlayerCandidateInfo[] candidates)
    {
        PlayerCandidateInfo bestCandidate = null;

        for (int i = 0; i < candidates.Length; i++)
        {
            PlayerCandidateInfo candidate = candidates[i];

            if (!candidate.AutoSelectable)
            {
                continue;
            }

            if (bestCandidate == null || candidate.Priority < bestCandidate.Priority)
            {
                bestCandidate = candidate;
            }
        }

        return bestCandidate != null ? bestCandidate.Movement : null;
    }

    private void DrawCandidateFoldout(PlayerCandidateInfo[] candidates)
    {
        candidatesFoldout = EditorGUILayout.Foldout(candidatesFoldout, "Detected PointClickPlayerMovement Objects", true);

        if (!candidatesFoldout)
        {
            return;
        }

        if (candidates == null || candidates.Length == 0)
        {
            EditorGUILayout.LabelField("None", EditorStyles.miniLabel);
            return;
        }

        PointClickPlayerMovement preferred = FindScenePlayer(candidates);

        for (int i = 0; i < candidates.Length; i++)
        {
            PlayerCandidateInfo candidate = candidates[i];
            bool selected = selectedButler != null && candidate.Movement == selectedButler;
            bool preferredCandidate = preferred != null && candidate.Movement == preferred;
            string status = !candidate.AutoSelectable
                ? $"rejected: {candidate.RejectionReason}"
                : selected && preferredCandidate
                    ? "selected, preferred"
                    : selected
                        ? "selected"
                        : preferredCandidate
                            ? "preferred"
                            : "candidate";

            EditorGUILayout.LabelField(
                $"{candidate.ObjectName} | scene: {candidate.SceneName} | active: {candidate.IsActive} | tag: {candidate.Tag} | Guest name: {YesNo(candidate.NameContainsGuest)} | RoomProjectedEntity: {YesNo(candidate.HasRoomProjectedEntity)} | {status}",
                EditorStyles.miniLabel);
        }
    }

    private static PlayerCandidateInfo[] BuildPlayerCandidateInfos()
    {
        PointClickPlayerMovement selected = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInParent<PointClickPlayerMovement>(true)
            : null;
        PointClickPlayerMovement[] movements = Resources.FindObjectsOfTypeAll<PointClickPlayerMovement>();
        List<PlayerCandidateInfo> candidates = new List<PlayerCandidateInfo>();

        for (int i = 0; i < movements.Length; i++)
        {
            PointClickPlayerMovement movement = movements[i];

            if (movement == null)
            {
                continue;
            }

            candidates.Add(new PlayerCandidateInfo(movement, movement == selected));
        }

        candidates.Sort((left, right) =>
        {
            int priorityComparison = left.Priority.CompareTo(right.Priority);
            return priorityComparison != 0
                ? priorityComparison
                : string.Compare(left.ObjectName, right.ObjectName, StringComparison.OrdinalIgnoreCase);
        });

        return candidates.ToArray();
    }

    internal static bool IsSafePlayerObjectForSaving(PointClickPlayerMovement movement)
    {
        return movement != null && new PlayerCandidateInfo(movement, false).SafeForSaving;
    }

    private static void DrawSelectedPlayerWarnings(PointClickPlayerMovement movement)
    {
        PlayerCandidateInfo info = new PlayerCandidateInfo(movement, false);

        if (info.NameContainsGuest)
        {
            EditorGUILayout.HelpBox(GuestSelectionWarning, MessageType.Warning);
        }

        if (info.HasRoomProjectedEntity)
        {
            EditorGUILayout.HelpBox(GuestProjectionWarning, MessageType.Warning);
        }

        if (!info.LooksLikePlayer)
        {
            EditorGUILayout.HelpBox(UnexpectedPlayerWarning, MessageType.Warning);
        }
    }

    private static string GetCalibrationStatus(bool hasOverride, PointClickPlayerMovement.ButlerRoomScaleOverrideData data)
    {
        if (!hasOverride)
        {
            return "Not started";
        }

        if (data.IsComplete)
        {
            return "Complete";
        }

        if (data.HasFront)
        {
            return "Front saved only";
        }

        if (data.HasBack)
        {
            return "Back saved only";
        }

        return "Not started";
    }

    private static string ResolveSelectedRoom(PointClickPlayerMovement movement, string[] rooms, ref int selectedIndex)
    {
        string selectedRoom = movement.EditorSelectedButlerScaleRoomId;

        if (string.IsNullOrWhiteSpace(selectedRoom))
        {
            selectedRoom = movement.CurrentButlerScaleRoomId;
        }

        for (int i = 0; i < rooms.Length; i++)
        {
            if (SameRoom(rooms[i], selectedRoom))
            {
                selectedIndex = i;
                return rooms[i];
            }
        }

        selectedIndex = Mathf.Clamp(selectedIndex, 0, rooms.Length - 1);
        return rooms[selectedIndex];
    }

    private static string[] BuildRoomOptions(PointClickPlayerMovement movement)
    {
        List<string> rooms = new List<string>();

        AddRoom(rooms, movement.EditorSelectedButlerScaleRoomId);
        AddRoom(rooms, movement.CurrentButlerScaleRoomId);
        AddRoom(rooms, movement.CurrentRoomPerspectiveProfileRoomId);

        RoomNavigationManager navigationManager = FindSceneNavigationManager();
        AddRoom(rooms, navigationManager != null ? navigationManager.CurrentRoom : string.Empty);
        movement.GetButlerScaleOverrideRoomIds(rooms);

        RoomContentGroup[] roomContentGroups = Resources.FindObjectsOfTypeAll<RoomContentGroup>();

        for (int i = 0; i < roomContentGroups.Length; i++)
        {
            RoomContentGroup roomContentGroup = roomContentGroups[i];

            if (roomContentGroup != null &&
                roomContentGroup.gameObject != null &&
                roomContentGroup.gameObject.scene.IsValid())
            {
                AddRoom(rooms, roomContentGroup.RoomName);
            }
        }

        rooms.Sort(StringComparer.OrdinalIgnoreCase);
        return rooms.ToArray();
    }

    private static RoomNavigationManager FindSceneNavigationManager()
    {
        RoomNavigationManager[] navigationManagers = Resources.FindObjectsOfTypeAll<RoomNavigationManager>();

        for (int i = 0; i < navigationManagers.Length; i++)
        {
            RoomNavigationManager navigationManager = navigationManagers[i];

            if (navigationManager != null &&
                navigationManager.gameObject != null &&
                navigationManager.gameObject.scene.IsValid())
            {
                return navigationManager;
            }
        }

        return null;
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

    private static void PingRoom(string roomId)
    {
        RoomContentGroup[] roomContentGroups = Resources.FindObjectsOfTypeAll<RoomContentGroup>();

        for (int i = 0; i < roomContentGroups.Length; i++)
        {
            RoomContentGroup roomContentGroup = roomContentGroups[i];

            if (roomContentGroup == null || !SameRoom(roomContentGroup.RoomName, roomId))
            {
                continue;
            }

            Selection.activeGameObject = roomContentGroup.gameObject;
            EditorGUIUtility.PingObject(roomContentGroup.gameObject);
            return;
        }
    }

    private static void SaveButlerScene(PointClickPlayerMovement movement)
    {
        if (movement == null || Application.isPlaying || !movement.gameObject.scene.IsValid())
        {
            return;
        }

        EditorSceneManager.MarkSceneDirty(movement.gameObject.scene);
        EditorSceneManager.SaveScene(movement.gameObject.scene);
    }

    private static void RecordMovementAndTransform(PointClickPlayerMovement movement, string actionName)
    {
        if (movement == null)
        {
            return;
        }

        Undo.RecordObject(movement, actionName);
        Undo.RecordObject(movement.transform, actionName);
    }

    private static void MarkDirty(PointClickPlayerMovement movement)
    {
        if (movement == null || Application.isPlaying)
        {
            return;
        }

        EditorUtility.SetDirty(movement);
        MarkTransformDirty(movement);
    }

    private static void MarkTransformDirty(PointClickPlayerMovement movement)
    {
        if (movement == null || Application.isPlaying)
        {
            return;
        }

        EditorUtility.SetDirty(movement.transform);

        if (movement.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(movement.gameObject.scene);
        }
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

    private static string YesNo(bool value)
    {
        return value ? "Yes" : "No";
    }

    private static bool Contains(string value, string token)
    {
        return value != null && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetTagSafely(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return string.Empty;
        }

        try
        {
            return gameObject.tag;
        }
        catch (UnityException)
        {
            return string.Empty;
        }
    }

    private sealed class PlayerCandidateInfo
    {
        public PlayerCandidateInfo(PointClickPlayerMovement movement, bool isCurrentSelection)
        {
            Movement = movement;
            GameObject gameObject = movement != null ? movement.gameObject : null;
            ObjectName = gameObject != null ? gameObject.name : string.Empty;
            SceneName = gameObject != null && gameObject.scene.IsValid() ? gameObject.scene.name : "<no scene>";
            IsActive = gameObject != null && gameObject.activeInHierarchy;
            Tag = GetTagSafely(gameObject);
            IsCurrentSelection = isCurrentSelection;
            IsPersistentAsset = movement == null ||
                gameObject == null ||
                EditorUtility.IsPersistent(movement) ||
                EditorUtility.IsPersistent(gameObject);
            IsLoadedSceneObject = gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded && !IsPersistentAsset;
            NameContainsGuest = Contains(ObjectName, "Guest");
            HasRoomProjectedEntity = movement != null &&
                (movement.GetComponentInParent<RoomProjectedEntity>(true) != null ||
                    movement.GetComponentInChildren<RoomProjectedEntity>(true) != null);
            ExactNamePlayer = string.Equals(ObjectName, "player", StringComparison.OrdinalIgnoreCase);
            TaggedPlayer = string.Equals(Tag, "Player", StringComparison.OrdinalIgnoreCase);
            NameContainsButler = Contains(ObjectName, "Butler");
            NameContainsPlayer = Contains(ObjectName, "Player");
            LooksLikePlayer = ExactNamePlayer || TaggedPlayer || NameContainsButler || NameContainsPlayer;
            SafeForSaving = IsLoadedSceneObject && !NameContainsGuest && !HasRoomProjectedEntity && LooksLikePlayer;
            AutoSelectable = IsLoadedSceneObject &&
                !NameContainsGuest &&
                !HasRoomProjectedEntity &&
                (IsCurrentSelection || LooksLikePlayer);
            Priority = CalculatePriority();
            RejectionReason = CalculateRejectionReason();
        }

        public PointClickPlayerMovement Movement { get; }
        public string ObjectName { get; }
        public string SceneName { get; }
        public bool IsActive { get; }
        public string Tag { get; }
        public bool IsCurrentSelection { get; }
        public bool IsPersistentAsset { get; }
        public bool IsLoadedSceneObject { get; }
        public bool NameContainsGuest { get; }
        public bool HasRoomProjectedEntity { get; }
        public bool ExactNamePlayer { get; }
        public bool TaggedPlayer { get; }
        public bool NameContainsButler { get; }
        public bool NameContainsPlayer { get; }
        public bool LooksLikePlayer { get; }
        public bool SafeForSaving { get; }
        public bool AutoSelectable { get; }
        public int Priority { get; }
        public string RejectionReason { get; }

        private int CalculatePriority()
        {
            if (!AutoSelectable)
            {
                return 1000;
            }

            if (IsCurrentSelection)
            {
                return 0;
            }

            if (ExactNamePlayer)
            {
                return 1;
            }

            if (TaggedPlayer)
            {
                return 2;
            }

            if (NameContainsButler)
            {
                return 3;
            }

            if (NameContainsPlayer)
            {
                return 4;
            }

            return 50;
        }

        private string CalculateRejectionReason()
        {
            if (AutoSelectable)
            {
                return string.Empty;
            }

            if (!IsLoadedSceneObject || IsPersistentAsset)
            {
                return "not a loaded scene object";
            }

            if (NameContainsGuest)
            {
                return "name contains Guest";
            }

            if (HasRoomProjectedEntity)
            {
                return "has RoomProjectedEntity";
            }

            return "does not look like player";
        }
    }
}
