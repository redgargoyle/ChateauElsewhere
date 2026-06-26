using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class ButlerRoomScaleCalibrationWindow : EditorWindow
{
    private const string WorkflowHelp =
        "Use EDIT MODE to save calibration. Pick a room, move the player object to the FRONT of the room, adjust Front Scale, then click Save FRONT. Move the player object to the BACK of the room, adjust Back Scale, then click Save BACK. Preview buttons only change the visible size temporarily; Save buttons store the room calibration.";
    private const string PlayModeWarning =
        "PLAY MODE: preview only. Calibration changes will not be saved to the scene. Exit Play Mode to save FRONT/BACK room scale.";
    private const string NoSafePlayerMessage =
        "No safe player object found. Drag the scene object named 'player' into the Butler / Player Object field.";
    private const string GuestSelectionWarning =
        "Warning: selected object looks like a guest. The actual Butler/player should be the scene object named 'player'. Drag 'player' into this field before saving calibration.";
    private const string GuestProjectionWarning =
        "Warning: selected object has guest projection components. Confirm this is not a guest before saving.";
    private const string UnexpectedPlayerWarning =
        "Warning: selected object does not look like the main player. Expected object name: player.";
    private const string MatchingEndpointWarning =
        "Front and Back positions are the same. Move the player to the front and save FRONT, then move to the back and save BACK.";

    private PointClickPlayerMovement selectedButler;
    private int selectedRoomIndex;
    private Vector2 scroll;
    private bool candidateFoldout;
    private Dictionary<string, float> draftScales = new Dictionary<string, float>();

    [MenuItem("Tools/Butler/Room Scale Calibration")]
    public static void Open()
    {
        GetWindow<ButlerRoomScaleCalibrationWindow>("Butler Scale");
    }

    private void OnEnable()
    {
        selectedButler = FindScenePlayer();
    }

    private void OnGUI()
    {
        if (draftScales == null)
        {
            draftScales = new Dictionary<string, float>();
        }

        PlayerCandidateInfo[] candidates = BuildPlayerCandidateInfos();

        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Butler Room Scale Calibration", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(WorkflowHelp, MessageType.Info);

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox(PlayModeWarning, MessageType.Warning);
        }

        selectedButler = (PointClickPlayerMovement)EditorGUILayout.ObjectField(
            "Butler / Player Object",
            selectedButler,
            typeof(PointClickPlayerMovement),
            true);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Find Scene Player"))
            {
                selectedButler = FindScenePlayer();

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
                }
            }
        }

        DrawDetectedPlayerCandidates(candidates, selectedButler);

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
        DrawButlerSummaryAndControls(selectedButler, selectedRoom, IsSafePlayerObjectForSaving(selectedButler), draftScales);

        EditorGUILayout.EndScrollView();
    }

    private static void DrawRoomSelection(
        PointClickPlayerMovement movement,
        string[] rooms,
        ref int selectedIndex,
        ref string selectedRoom)
    {
        EditorGUI.BeginChangeCheck();
        selectedIndex = EditorGUILayout.Popup("Room", selectedIndex, rooms);

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

    private static void DrawButlerSummaryAndControls(
        PointClickPlayerMovement movement,
        string selectedRoom,
        bool safePlayerForSaving,
        Dictionary<string, float> draftScales)
    {
        movement.TryGetCurrentButlerRoomLocalFootPoint(out Vector2 footPoint);
        bool hasOverride = movement.TryGetButlerRoomScaleOverride(selectedRoom, out PointClickPlayerMovement.ButlerRoomScaleOverrideData data);
        float currentScale = movement.CaptureCurrentButlerScaleMultiplier();
        bool endpointsOverlap = HasMatchingSavedEndpoints(data);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Vector2Field("Current Room-Local Foot Point", footPoint);
            EditorGUILayout.Vector3Field("Current Transform localScale", movement.transform.localScale);
            EditorGUILayout.Toggle("Stored Override", hasOverride);
            EditorGUILayout.Toggle("Complete Override", hasOverride && data.IsComplete);
        }

        if (endpointsOverlap)
        {
            EditorGUILayout.HelpBox(MatchingEndpointWarning, MessageType.Warning);
        }

        bool canSaveCalibration = !Application.isPlaying && safePlayerForSaving;

        DrawEndpointControls(movement, selectedRoom, data, footPoint, currentScale, true, canSaveCalibration, draftScales);
        DrawEndpointControls(movement, selectedRoom, data, footPoint, currentScale, false, canSaveCalibration, draftScales);

        if (GUILayout.Button("Preview Saved Room Scaling Here"))
        {
            PreviewScale(movement, movement.GetButlerScaleForRoomAtY(selectedRoom, footPoint.y, currentScale), "Preview Butler Current Depth Scale");
        }

        using (new EditorGUI.DisabledScope(!canSaveCalibration))
        {
            if (GUILayout.Button("Initialize Room From Existing Perspective"))
            {
                RecordMovementAndTransform(movement, "Initialize Butler Room Scale");
                movement.InitializeButlerScaleOverrideForRoomFromCurrentPerspective(selectedRoom);
                ClearEndpointDrafts(draftScales, movement, selectedRoom);
                MarkDirty(movement);
            }
        }

        using (new EditorGUI.DisabledScope(!hasOverride || !canSaveCalibration))
        {
            if (GUILayout.Button("Clear Saved Scale For This Room"))
            {
                RecordMovementAndTransform(movement, "Remove Butler Room Scale");
                movement.RemoveButlerScaleOverrideForRoom(selectedRoom);
                ClearEndpointDrafts(draftScales, movement, selectedRoom);
                MarkDirty(movement);
            }
        }

        EditorGUILayout.HelpBox(
            "For each room, place the Butler near the front/closest part of the floor and tune Front Scale until he reads around 3/4 of a comparable door height or about 1.5x a chair. Then place him near the back/farthest part of the floor and tune Back Scale. The Butler interpolates between these two saved endpoints while walking.",
            MessageType.None);
    }

    private static void DrawEndpointControls(
        PointClickPlayerMovement movement,
        string selectedRoom,
        PointClickPlayerMovement.ButlerRoomScaleOverrideData data,
        Vector2 footPoint,
        float currentScale,
        bool front,
        bool canSaveCalibration,
        Dictionary<string, float> draftScales)
    {
        string label = front ? "Front" : "Back";
        bool hasEndpoint = front ? data.HasFront : data.HasBack;
        float endpointY = hasEndpoint ? (front ? data.FrontFootY : data.BackFootY) : footPoint.y;
        float storedScale = hasEndpoint ? (front ? data.FrontScale : data.BackScale) : currentScale;
        string draftKey = GetDraftScaleKey(movement, selectedRoom, front);
        float endpointScale = GetDraftScale(draftScales, draftKey, storedScale);
        string previewButtonLabel = front ? "Preview FRONT Size" : "Preview BACK Size";
        string saveButtonLabel = front ? "Save FRONT: Current Position + Scale" : "Save BACK: Current Position + Scale";

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField($"{label} saved", hasEndpoint ? "Yes" : "No");
            EditorGUILayout.FloatField($"{label} Y", endpointY);
            EditorGUILayout.FloatField($"{label} Scale", storedScale);
        }

        endpointScale = DrawScaleSliderNumericNudge($"{label} Scale", endpointScale, out bool scaleChanged);

        if (scaleChanged)
        {
            draftScales[draftKey] = endpointScale;
            PreviewScale(movement, endpointScale, $"Preview Butler {label} Scale");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(previewButtonLabel))
            {
                PreviewScale(movement, endpointScale, $"Preview Butler {label} Scale");
            }

            using (new EditorGUI.DisabledScope(!canSaveCalibration))
            {
                if (GUILayout.Button(saveButtonLabel))
                {
                    RecordMovementAndTransform(movement, $"Set Butler {label} Scale");

                    if (front)
                    {
                        movement.SetButlerFrontScaleForRoom(selectedRoom, footPoint.y, endpointScale);
                    }
                    else
                    {
                        movement.SetButlerBackScaleForRoom(selectedRoom, footPoint.y, endpointScale);
                    }

                    draftScales[draftKey] = endpointScale;
                    MarkDirty(movement);
                }
            }
        }
    }

    internal static float DrawScaleSliderNumericNudge(string label, float scale, out bool changed)
    {
        changed = false;
        float nextScale = Mathf.Max(0.001f, scale);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel(label);

            EditorGUI.BeginChangeCheck();
            nextScale = GUILayout.HorizontalSlider(nextScale, 0.25f, 2.5f);
            if (EditorGUI.EndChangeCheck())
            {
                changed = true;
            }

            EditorGUI.BeginChangeCheck();
            nextScale = EditorGUILayout.FloatField(nextScale, GUILayout.Width(72f));
            if (EditorGUI.EndChangeCheck())
            {
                changed = true;
            }

            if (GUILayout.Button("-0.05", GUILayout.Width(52f)))
            {
                nextScale -= 0.05f;
                changed = true;
            }

            if (GUILayout.Button("+0.05", GUILayout.Width(52f)))
            {
                nextScale += 0.05f;
                changed = true;
            }
        }

        return Mathf.Max(0.001f, nextScale);
    }

    private static float GetDraftScale(Dictionary<string, float> draftScales, string key, float fallbackScale)
    {
        return draftScales != null && draftScales.TryGetValue(key, out float draftScale)
            ? Mathf.Max(0.001f, draftScale)
            : Mathf.Max(0.001f, fallbackScale);
    }

    private static string GetDraftScaleKey(PointClickPlayerMovement movement, string roomId, bool front)
    {
        return $"{movement.GetEntityId()}:{NormalizeRoomName(roomId)}:{(front ? "front" : "back")}";
    }

    private static void ClearEndpointDrafts(Dictionary<string, float> draftScales, PointClickPlayerMovement movement, string roomId)
    {
        if (draftScales == null)
        {
            return;
        }

        draftScales.Remove(GetDraftScaleKey(movement, roomId, true));
        draftScales.Remove(GetDraftScaleKey(movement, roomId, false));
    }

    private static void PreviewScale(PointClickPlayerMovement movement, float scale, string actionName)
    {
        RecordMovementAndTransform(movement, actionName);
        movement.ApplyButlerScalePreview(scale);

        if (!Application.isPlaying)
        {
            MarkDirty(movement);
        }
    }

    private static bool HasMatchingSavedEndpoints(PointClickPlayerMovement.ButlerRoomScaleOverrideData data)
    {
        return data.HasFront &&
            data.HasBack &&
            Mathf.Abs(data.FrontFootY - data.BackFootY) < PointClickPlayerMovement.ButlerRoomScaleEndpointEpsilon;
    }

    internal static PointClickPlayerMovement FindScenePlayer()
    {
        PlayerCandidateInfo[] candidates = BuildPlayerCandidateInfos();
        PlayerCandidateInfo bestCandidate = FindBestPlayerCandidate(candidates);
        return bestCandidate != null ? bestCandidate.Movement : null;
    }

    internal static PlayerCandidateInfo[] BuildPlayerCandidateInfos()
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

        candidates.Sort(ComparePlayerCandidatesForDebug);
        return candidates.ToArray();
    }

    private static int ComparePlayerCandidatesForDebug(PlayerCandidateInfo left, PlayerCandidateInfo right)
    {
        int priorityComparison = left.Priority.CompareTo(right.Priority);

        if (priorityComparison != 0)
        {
            return priorityComparison;
        }

        return string.Compare(left.ObjectName, right.ObjectName, StringComparison.OrdinalIgnoreCase);
    }

    private static PlayerCandidateInfo FindBestPlayerCandidate(PlayerCandidateInfo[] candidates)
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

        return bestCandidate;
    }

    private void DrawDetectedPlayerCandidates(PlayerCandidateInfo[] candidates, PointClickPlayerMovement selected)
    {
        candidateFoldout = EditorGUILayout.Foldout(candidateFoldout, "Detected PointClickPlayerMovement Objects", true);

        if (!candidateFoldout)
        {
            return;
        }

        if (candidates == null || candidates.Length == 0)
        {
            EditorGUILayout.LabelField("None", EditorStyles.miniLabel);
            return;
        }

        PlayerCandidateInfo preferredCandidate = FindBestPlayerCandidate(candidates);
        PointClickPlayerMovement preferred = preferredCandidate != null ? preferredCandidate.Movement : null;

        for (int i = 0; i < candidates.Length; i++)
        {
            PlayerCandidateInfo candidate = candidates[i];
            bool isSelected = selected != null && candidate.Movement == selected;
            bool isPreferred = preferred != null && candidate.Movement == preferred;
            string status = GetCandidateStatus(candidate, isSelected, isPreferred);

            EditorGUILayout.LabelField(
                $"{candidate.ObjectName} | scene: {candidate.SceneName} | active: {candidate.IsActive} | tag: {candidate.Tag} | Guest name: {YesNo(candidate.NameContainsGuest)} | RoomProjectedEntity: {YesNo(candidate.HasRoomProjectedEntity)} | {status}",
                EditorStyles.miniLabel);
        }
    }

    private static string GetCandidateStatus(PlayerCandidateInfo candidate, bool isSelected, bool isPreferred)
    {
        if (!candidate.AutoSelectable)
        {
            return $"rejected: {candidate.RejectionReason}";
        }

        if (isSelected && isPreferred)
        {
            return "selected, preferred";
        }

        if (isSelected)
        {
            return "selected";
        }

        if (isPreferred)
        {
            return "preferred";
        }

        return "candidate";
    }

    private static string YesNo(bool value)
    {
        return value ? "Yes" : "No";
    }

    internal static bool IsSafePlayerObjectForSaving(PointClickPlayerMovement movement)
    {
        return movement != null && new PlayerCandidateInfo(movement, false).SafeForSaving;
    }

    internal static void DrawSelectedPlayerWarnings(PointClickPlayerMovement movement)
    {
        if (movement == null)
        {
            return;
        }

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

    internal sealed class PlayerCandidateInfo
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
        Undo.RecordObject(movement, actionName);

        if (movement != null)
        {
            Undo.RecordObject(movement.transform, actionName);
        }
    }

    private static void MarkDirty(PointClickPlayerMovement movement)
    {
        if (movement == null || Application.isPlaying)
        {
            return;
        }

        EditorUtility.SetDirty(movement);
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
}
