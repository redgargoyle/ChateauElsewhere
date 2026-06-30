using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class GuestRoomScaleMasterWindow : EditorWindow
{
    private const float MinMultiplier = 0.25f;
    private const float MaxMultiplier = 3f;
    private const float MinManualGuestScale = 0.1f;
    private const float MaxManualGuestScale = 5f;

    private int selectedRoomIndex;
    private string loadedRoomId = string.Empty;
    private string loadedManualGuestKey = string.Empty;
    private float selectedRoomMultiplier = 1f;
    private float manualGuestScale = 1f;
    private bool advancedFoldout;
    private bool customHasFront;
    private float customFrontY;
    private float customFrontScale = 1f;
    private bool customHasBack;
    private float customBackY;
    private float customBackScale = 1f;
    private string lastAction = "Ready";

    [MenuItem("Tools/Characters/Guest Size Master")]
    public static void OpenWindow()
    {
        GetWindow<GuestRoomScaleMasterWindow>("Guest Size Master");
    }

    private void OnGUI()
    {
        PointClickPlayerMovement butler = FindButler();
        GuestRoomScaleCalibration calibration = FindAnyObjectByType<GuestRoomScaleCalibration>(FindObjectsInactive.Include);
        GuestRoomScaleApplier applier = FindAnyObjectByType<GuestRoomScaleApplier>(FindObjectsInactive.Include);
        GuestScaleParticipant[] guests = FindGuestParticipants();
        string[] rooms = BuildRoomOptions(calibration, butler);
        selectedRoomIndex = Mathf.Clamp(selectedRoomIndex, 0, Mathf.Max(0, rooms.Length - 1));
        string selectedRoom = rooms.Length > 0 ? rooms[selectedRoomIndex] : string.Empty;

        if (!GuestRoomScaleCalibration.SameRoom(loadedRoomId, selectedRoom))
        {
            selectedRoomMultiplier = GetRoomMultiplier(calibration, selectedRoom);
            LoadCustomCurveFields(calibration, selectedRoom);
            loadedRoomId = selectedRoom;
            loadedManualGuestKey = string.Empty;
        }

        GuestScaleParticipant selectedGuest = ResolveSelectedGuest(selectedRoom);
        string selectedGuestKey = GetGuestSelectionKey(selectedGuest);

        if (!string.Equals(loadedManualGuestKey, selectedGuestKey, StringComparison.Ordinal))
        {
            manualGuestScale = GetGuestCurrentScale(selectedGuest, calibration, selectedRoom);
            loadedManualGuestKey = selectedGuestKey;
        }

        EditorGUILayout.LabelField("Guest Size Master", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Butler found: {(butler != null ? "yes" : "no")}");
        EditorGUILayout.LabelField($"GuestRoomScaleCalibration found: {(calibration != null ? "yes" : "no")}");
        EditorGUILayout.LabelField($"GuestRoomScaleApplier found: {(applier != null ? "yes" : "no")}");
        EditorGUILayout.LabelField($"Guests found: {guests.Length}");
        EditorGUILayout.LabelField($"Guests ready: {CountReadyGuests(guests)}");
        EditorGUILayout.LabelField($"Current selected room: {(string.IsNullOrWhiteSpace(selectedRoom) ? "none" : selectedRoom)}");
        EditorGUILayout.LabelField($"Last action: {lastAction}");
        EditorGUILayout.Space(8f);

        using (new EditorGUI.DisabledScope(rooms.Length == 0))
        {
            int nextRoomIndex = EditorGUILayout.Popup("Room", selectedRoomIndex, rooms);

            if (nextRoomIndex != selectedRoomIndex)
            {
                selectedRoomIndex = nextRoomIndex;
                selectedRoom = rooms[selectedRoomIndex];
                selectedRoomMultiplier = GetRoomMultiplier(calibration, selectedRoom);
                LoadCustomCurveFields(calibration, selectedRoom);
                loadedRoomId = selectedRoom;
                loadedManualGuestKey = string.Empty;
            }

            EditorGUI.BeginChangeCheck();
            selectedRoomMultiplier = EditorGUILayout.Slider("Guest Size In This Room", selectedRoomMultiplier, MinMultiplier, MaxMultiplier);

            if (EditorGUI.EndChangeCheck() && !string.IsNullOrWhiteSpace(selectedRoom))
            {
                PreviewSelectedRoom(selectedRoom);
            }
        }

        EditorGUILayout.Space(8f);

        if (GUILayout.Button("SET UP GUEST SCALING"))
        {
            SetupGuestScaling();
        }

        if (GUILayout.Button("PREVIEW ROOM GUEST SIZE"))
        {
            PreviewSelectedRoom(selectedRoom);
        }

        if (GUILayout.Button("MATCH BUTLER SIZE IN ROOM"))
        {
            selectedRoomMultiplier = 1f;
            PreviewSelectedRoom(selectedRoom);
        }

        DrawManualFrontBackGuestCurve(selectedRoom, selectedGuest);

        if (GUILayout.Button("SAVE ROOM GUEST SIZE"))
        {
            SaveSelectedRoom(calibration, selectedRoom);
        }

        if (GUILayout.Button("APPLY TO ALL GUESTS IN ROOM"))
        {
            ApplySelectedRoom(selectedRoom);
        }

        if (GUILayout.Button("SAVE SCENE"))
        {
            EditorSceneManager.SaveOpenScenes();
            lastAction = "Saved open scenes.";
        }

        DrawAdvanced(selectedRoom);
    }

    private void DrawAdvanced(string selectedRoom)
    {
        advancedFoldout = EditorGUILayout.Foldout(advancedFoldout, "Advanced", true);

        if (!advancedFoldout)
        {
            return;
        }

        if (GUILayout.Button("Run Guest Scale Audit"))
        {
            GuestScaleAudit.RunAndWriteReport();
            lastAction = "Audit written.";
        }

        if (GUILayout.Button("Reset Selected Room Multiplier"))
        {
            GuestRoomScaleCalibration calibration = FindAnyObjectByType<GuestRoomScaleCalibration>(FindObjectsInactive.Include);

            if (calibration != null)
            {
                Undo.RecordObject(calibration, "Reset Guest Room Size");
                calibration.SetRoomMultiplier(selectedRoom, 1f);
                selectedRoomMultiplier = 1f;
                EditorUtility.SetDirty(calibration);
                lastAction = $"Reset {selectedRoom} multiplier.";
            }
        }

        if (GUILayout.Button("Proof shrink guests"))
        {
            ApplyProofMultiplier(0.5f);
            lastAction = "Proof shrink applied.";
        }

        if (GUILayout.Button("Proof grow guests"))
        {
            ApplyProofMultiplier(1.5f);
            lastAction = "Proof grow applied.";
        }

        if (GUILayout.Button("Emergency restore captured base scales"))
        {
            GuestScaleParticipant[] guests = FindGuestParticipants();

            for (int i = 0; i < guests.Length; i++)
            {
                guests[i]?.RestoreCapturedBaseScale();
            }

            lastAction = "Restored captured base scales.";
        }
    }

    private void DrawManualFrontBackGuestCurve(string selectedRoom, GuestScaleParticipant selectedGuest)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Manual Front/Back Guest Curve", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Selected Guest", selectedGuest != null ? selectedGuest.CharacterId : "none");
        EditorGUILayout.LabelField("Selected Guest Y", selectedGuest != null ? selectedGuest.ResolveRoomLocalY().ToString("0.###") : "n/a");
        EditorGUILayout.LabelField("Saved Front", FormatManualCurvePoint(true));
        EditorGUILayout.LabelField("Saved Back", FormatManualCurvePoint(false));

        using (new EditorGUI.DisabledScope(selectedGuest == null))
        {
            EditorGUI.BeginChangeCheck();
            manualGuestScale = EditorGUILayout.Slider("Manual Guest Scale", manualGuestScale, MinManualGuestScale, MaxManualGuestScale);

            if (EditorGUI.EndChangeCheck())
            {
                PreviewSelectedGuestManualScale(selectedGuest);
            }

            if (GUILayout.Button("PREVIEW SELECTED GUEST SIZE"))
            {
                PreviewSelectedGuestManualScale(selectedGuest);
            }

            if (GUILayout.Button("SAVE FRONT FROM SELECTED GUEST"))
            {
                SaveManualCurvePoint(selectedRoom, selectedGuest, true);
            }

            if (GUILayout.Button("SAVE BACK FROM SELECTED GUEST"))
            {
                SaveManualCurvePoint(selectedRoom, selectedGuest, false);
            }
        }

        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(selectedRoom)))
        {
            if (GUILayout.Button("PREVIEW MANUAL CURVE IN ROOM"))
            {
                PreviewManualCurveInRoom(selectedRoom);
            }

            if (GUILayout.Button("CLEAR MANUAL CURVE"))
            {
                ClearManualCurve(selectedRoom);
            }
        }
    }

    private string FormatManualCurvePoint(bool front)
    {
        bool hasPoint = front ? customHasFront : customHasBack;

        if (!hasPoint)
        {
            return "not set";
        }

        float y = front ? customFrontY : customBackY;
        float scale = front ? customFrontScale : customBackScale;
        return $"Y {y:0.###}, scale {scale:0.###}";
    }

    private void PreviewSelectedGuestManualScale(GuestScaleParticipant selectedGuest)
    {
        if (selectedGuest == null)
        {
            lastAction = "Manual preview skipped: select a guest in the selected room.";
            return;
        }

        Transform scaleRoot = selectedGuest.ResolveScaleRoot();

        if (scaleRoot == null)
        {
            lastAction = "Manual preview skipped: selected guest has no scale root.";
            return;
        }

        Undo.RecordObject(scaleRoot, "Preview Selected Guest Size");
        bool changed = selectedGuest.ApplyFinalScale(manualGuestScale);
        EditorUtility.SetDirty(scaleRoot);
        SceneView.RepaintAll();
        Repaint();
        lastAction = $"Previewed {selectedGuest.CharacterId} at scale {manualGuestScale:0.###}; changed {(changed ? "yes" : "no")}.";
        Debug.Log($"[Guest Size Master] {lastAction}");
    }

    private void SaveManualCurvePoint(string selectedRoom, GuestScaleParticipant selectedGuest, bool front)
    {
        if (selectedGuest == null || string.IsNullOrWhiteSpace(selectedRoom))
        {
            lastAction = "Manual point save skipped: select a room guest first.";
            return;
        }

        GuestRoomScaleCalibration calibration = EnsureCalibration(FindButler());
        Undo.RecordObject(calibration, front ? "Save Guest Front Scale" : "Save Guest Back Scale");
        float roomLocalY = selectedGuest.ResolveRoomLocalY();

        if (front)
        {
            calibration.SetFront(selectedRoom, roomLocalY, manualGuestScale);
            customHasFront = true;
            customFrontY = roomLocalY;
            customFrontScale = manualGuestScale;
        }
        else
        {
            calibration.SetBack(selectedRoom, roomLocalY, manualGuestScale);
            customHasBack = true;
            customBackY = roomLocalY;
            customBackScale = manualGuestScale;
        }

        selectedRoomMultiplier = 1f;
        calibration.SetRoomMultiplier(selectedRoom, 1f);
        EditorUtility.SetDirty(calibration);

        if (HasCompleteManualCurve(calibration, selectedRoom))
        {
            GuestScaleApplyResult result = RefreshRoomWithUndo(selectedRoom, "Preview Manual Guest Curve");
            lastAction = $"Saved {(front ? "front" : "back")} guest scale for {selectedRoom}; applied {result.Applied}, changed {result.Changed}.";
        }
        else
        {
            PreviewSelectedGuestManualScale(selectedGuest);
            lastAction = $"Saved {(front ? "front" : "back")} guest scale for {selectedRoom}; save the other point next.";
        }

        Debug.Log($"[Guest Size Master] {lastAction}");
    }

    private void PreviewManualCurveInRoom(string selectedRoom)
    {
        GuestRoomScaleCalibration calibration = FindAnyObjectByType<GuestRoomScaleCalibration>(FindObjectsInactive.Include);

        if (calibration == null || !HasCompleteManualCurve(calibration, selectedRoom))
        {
            lastAction = "Manual curve preview skipped: save front and back first.";
            return;
        }

        GuestScaleApplyResult result = RefreshRoomWithUndo(selectedRoom, "Preview Manual Guest Curve");
        lastAction = $"Previewed manual guest curve for {selectedRoom}; applied {result.Applied}, changed {result.Changed}.";
        Debug.Log($"[Guest Size Master] {lastAction}");
    }

    private void ClearManualCurve(string selectedRoom)
    {
        GuestRoomScaleCalibration calibration = FindAnyObjectByType<GuestRoomScaleCalibration>(FindObjectsInactive.Include);

        if (calibration == null || string.IsNullOrWhiteSpace(selectedRoom))
        {
            lastAction = "Clear skipped: setup is incomplete.";
            return;
        }

        Undo.RecordObject(calibration, "Clear Manual Guest Curve");
        calibration.ClearCustomCurve(selectedRoom);
        LoadCustomCurveFields(calibration, selectedRoom);
        EditorUtility.SetDirty(calibration);
        PreviewSelectedRoom(selectedRoom);
        lastAction = $"Cleared manual guest curve for {selectedRoom}.";
        Debug.Log($"[Guest Size Master] {lastAction}");
    }

    private GuestScaleApplyResult RefreshRoomWithUndo(string selectedRoom, string undoName)
    {
        GuestRoomScaleApplier applier = FindAnyObjectByType<GuestRoomScaleApplier>(FindObjectsInactive.Include);

        if (applier == null)
        {
            return new GuestScaleApplyResult(0, 0);
        }

        GuestRoomScaleCalibration calibration = FindAnyObjectByType<GuestRoomScaleCalibration>(FindObjectsInactive.Include);

        if (calibration != null)
        {
            applier.SetCalibration(calibration);
        }

        List<Transform> scaleRoots = CollectParticipantScaleRoots(selectedRoom);
        RecordScaleRoots(scaleRoots, undoName);
        GuestScaleApplyResult result = applier.RefreshRoomNow(selectedRoom);
        MarkScaleRootsDirty(scaleRoots);
        EditorUtility.SetDirty(applier);
        SceneView.RepaintAll();
        Repaint();
        return result;
    }

    private void SetupGuestScaling()
    {
        PointClickPlayerMovement butler = FindButler();
        GuestRoomScaleCalibration calibration = EnsureCalibration(butler);
        GuestRoomScaleApplier applier = GuestRoomScaleApplier.EnsureInScene();
        applier.SetCalibration(calibration);
        int removed = RemoveInvalidParticipants();
        int ensured = applier.EnsureParticipantsForSceneGuests();
        GuestScaleApplyResult result = applier.RefreshAllWithResultNow();

        EditorUtility.SetDirty(calibration);
        EditorUtility.SetDirty(applier);
        EditorSceneManager.MarkAllScenesDirty();
        lastAction = $"Set up guest scaling. Removed {removed}, ensured {ensured}, applied {result.Applied}, changed {result.Changed}.";
        Debug.Log($"[Guest Size Master] {lastAction}");
    }

    private void PreviewSelectedRoom(string selectedRoom)
    {
        GuestRoomScaleCalibration calibration = FindAnyObjectByType<GuestRoomScaleCalibration>(FindObjectsInactive.Include);
        GuestRoomScaleApplier applier = FindAnyObjectByType<GuestRoomScaleApplier>(FindObjectsInactive.Include);

        if (calibration == null || applier == null || string.IsNullOrWhiteSpace(selectedRoom))
        {
            lastAction = "Preview skipped: setup is incomplete.";
            return;
        }

        Undo.RecordObject(calibration, "Preview Room Guest Size");
        calibration.SetRoomMultiplier(selectedRoom, selectedRoomMultiplier);
        applier.SetCalibration(calibration);

        List<Transform> scaleRoots = CollectParticipantScaleRoots(selectedRoom);
        RecordScaleRoots(scaleRoots, "Preview Room Guest Size");

        GuestScaleApplyResult result = applier.RefreshRoomNow(selectedRoom);
        MarkScaleRootsDirty(scaleRoots);
        EditorUtility.SetDirty(calibration);
        EditorUtility.SetDirty(applier);
        SceneView.RepaintAll();
        Repaint();
        lastAction = $"Previewed {selectedRoom}; applied {result.Applied}, changed {result.Changed}.";
        Debug.Log($"[Guest Size Master] {lastAction}");
    }

    private void SaveSelectedRoom(GuestRoomScaleCalibration calibration, string selectedRoom)
    {
        if (calibration == null || string.IsNullOrWhiteSpace(selectedRoom))
        {
            lastAction = "Save skipped: setup is incomplete.";
            return;
        }

        calibration.SetRoomMultiplier(selectedRoom, selectedRoomMultiplier);
        EditorUtility.SetDirty(calibration);
        EditorSceneManager.MarkAllScenesDirty();
        lastAction = $"Saved guest size for {selectedRoom}.";
    }

    private void ApplySelectedRoom(string selectedRoom)
    {
        GuestRoomScaleApplier applier = FindAnyObjectByType<GuestRoomScaleApplier>(FindObjectsInactive.Include);

        if (applier == null || string.IsNullOrWhiteSpace(selectedRoom))
        {
            lastAction = "Apply skipped: applier missing.";
            return;
        }

        GuestRoomScaleCalibration calibration = FindAnyObjectByType<GuestRoomScaleCalibration>(FindObjectsInactive.Include);

        if (calibration != null)
        {
            applier.SetCalibration(calibration);
        }

        List<Transform> scaleRoots = CollectParticipantScaleRoots(selectedRoom);
        RecordScaleRoots(scaleRoots, "Apply Room Guest Size");

        GuestScaleApplyResult result = applier.RefreshRoomNow(selectedRoom);
        MarkScaleRootsDirty(scaleRoots);
        EditorUtility.SetDirty(applier);
        SceneView.RepaintAll();
        Repaint();
        lastAction = $"Applied {selectedRoom} guest size to {result.Applied} guests; changed {result.Changed}.";
        Debug.Log($"[Guest Size Master] {lastAction}");
    }

    private static List<Transform> CollectParticipantScaleRoots(string selectedRoom)
    {
        List<Transform> scaleRoots = new List<Transform>();
        HashSet<Transform> seen = new HashSet<Transform>();
        GuestScaleParticipant[] guests = FindGuestParticipants();

        for (int i = 0; i < guests.Length; i++)
        {
            GuestScaleParticipant guest = guests[i];

            if (guest == null ||
                guest.ExcludeFromGuestScaling ||
                guest.IsButler ||
                !GuestRoomScaleCalibration.SameRoom(guest.ResolveRoomId(), selectedRoom))
            {
                continue;
            }

            Transform scaleRoot = guest.ResolveScaleRoot();

            if (scaleRoot != null && seen.Add(scaleRoot))
            {
                scaleRoots.Add(scaleRoot);
            }
        }

        return scaleRoots;
    }

    private static void RecordScaleRoots(List<Transform> scaleRoots, string undoName)
    {
        for (int i = 0; i < scaleRoots.Count; i++)
        {
            if (scaleRoots[i] != null)
            {
                Undo.RecordObject(scaleRoots[i], undoName);
            }
        }
    }

    private static void MarkScaleRootsDirty(List<Transform> scaleRoots)
    {
        for (int i = 0; i < scaleRoots.Count; i++)
        {
            if (scaleRoots[i] != null)
            {
                EditorUtility.SetDirty(scaleRoots[i]);
            }
        }
    }

    private static GuestRoomScaleCalibration EnsureCalibration(PointClickPlayerMovement butler)
    {
        GuestRoomScaleCalibration calibration = FindAnyObjectByType<GuestRoomScaleCalibration>(FindObjectsInactive.Include);

        if (calibration == null)
        {
            GameObject calibrationObject = new GameObject("GuestRoomScaleCalibration");
            calibration = calibrationObject.AddComponent<GuestRoomScaleCalibration>();
        }

        if (butler != null)
        {
            calibration.InitializeMissingRoomsFromButler(butler);
            calibration.SetButlerScaleSource(butler);
        }

        return calibration;
    }

    private static void ApplyProofMultiplier(float multiplier)
    {
        GuestScaleParticipant[] guests = FindGuestParticipants();

        for (int i = 0; i < guests.Length; i++)
        {
            GuestScaleParticipant guest = guests[i];

            if (guest == null || guest.ExcludeFromGuestScaling || guest.IsButler)
            {
                continue;
            }

            guest.CaptureBaseScale(false);
            guest.ApplyFinalScale(multiplier);
        }
    }

    private static GuestScaleParticipant[] FindGuestParticipants()
    {
        GuestScaleParticipant[] allGuests = FindObjectsByType<GuestScaleParticipant>(FindObjectsInactive.Include);
        List<GuestScaleParticipant> guests = new List<GuestScaleParticipant>();

        for (int i = 0; i < allGuests.Length; i++)
        {
            GuestScaleParticipant guest = allGuests[i];

            if (guest != null && !GuestRoomScaleApplier.IsGuestScaleInfrastructureObject(guest.gameObject))
            {
                guests.Add(guest);
            }
        }

        return guests.ToArray();
    }

    private static int RemoveInvalidParticipants()
    {
        int removed = 0;
        GuestScaleParticipant[] allGuests = FindObjectsByType<GuestScaleParticipant>(FindObjectsInactive.Include);

        for (int i = 0; i < allGuests.Length; i++)
        {
            GuestScaleParticipant guest = allGuests[i];

            if (guest == null || !GuestRoomScaleApplier.IsGuestScaleInfrastructureObject(guest.gameObject))
            {
                continue;
            }

            Undo.DestroyObjectImmediate(guest);
            removed++;
        }

        return removed;
    }

    private void LoadCustomCurveFields(GuestRoomScaleCalibration calibration, string selectedRoom)
    {
        if (calibration != null && calibration.TryGetRoom(selectedRoom, out GuestRoomScaleEntry entry))
        {
            customHasFront = entry.hasFront;
            customFrontY = entry.hasFront ? entry.frontRoomLocalY : 0f;
            customFrontScale = entry.hasFront ? entry.frontGuestScale : 1f;
            customHasBack = entry.hasBack;
            customBackY = entry.hasBack ? entry.backRoomLocalY : 0f;
            customBackScale = entry.hasBack ? entry.backGuestScale : 1f;
            return;
        }

        customHasFront = false;
        customFrontY = 0f;
        customFrontScale = 1f;
        customHasBack = false;
        customBackY = 0f;
        customBackScale = 1f;
    }

    private static bool HasCompleteManualCurve(GuestRoomScaleCalibration calibration, string selectedRoom)
    {
        return calibration != null &&
            calibration.TryGetRoom(selectedRoom, out GuestRoomScaleEntry entry) &&
            entry.useCustomGuestCurve &&
            entry.HasCompleteCustomCurve;
    }

    private static GuestScaleParticipant ResolveSelectedGuest(string selectedRoom)
    {
        Transform activeTransform = Selection.activeTransform;

        if (activeTransform != null)
        {
            GuestScaleParticipant selected = activeTransform.GetComponentInParent<GuestScaleParticipant>(true);

            if (selected == null)
            {
                selected = activeTransform.GetComponentInChildren<GuestScaleParticipant>(true);
            }

            if (IsRoomGuest(selected, selectedRoom))
            {
                return selected;
            }
        }

        GuestScaleParticipant[] guests = FindGuestParticipants();

        for (int i = 0; i < guests.Length; i++)
        {
            if (IsRoomGuest(guests[i], selectedRoom))
            {
                return guests[i];
            }
        }

        return null;
    }

    private static bool IsRoomGuest(GuestScaleParticipant guest, string selectedRoom)
    {
        return guest != null &&
            !guest.ExcludeFromGuestScaling &&
            !guest.IsButler &&
            !GuestRoomScaleApplier.IsGuestScaleInfrastructureObject(guest.gameObject) &&
            GuestRoomScaleCalibration.SameRoom(guest.ResolveRoomId(), selectedRoom);
    }

    private static float GetGuestCurrentScale(
        GuestScaleParticipant selectedGuest,
        GuestRoomScaleCalibration calibration,
        string selectedRoom)
    {
        if (selectedGuest != null)
        {
            Transform scaleRoot = selectedGuest.ResolveScaleRoot();

            if (scaleRoot != null)
            {
                return Mathf.Clamp(Mathf.Abs(scaleRoot.localScale.y), MinManualGuestScale, MaxManualGuestScale);
            }

            if (calibration != null &&
                calibration.TryEvaluateGuestScale(
                    selectedRoom,
                    selectedGuest.ResolveRoomLocalY(),
                    out float evaluatedScale,
                    out _,
                    out _))
            {
                return Mathf.Clamp(evaluatedScale, MinManualGuestScale, MaxManualGuestScale);
            }
        }

        return 1f;
    }

    private static string GetGuestSelectionKey(GuestScaleParticipant selectedGuest)
    {
        if (selectedGuest == null)
        {
            return string.Empty;
        }

        Transform scaleRoot = selectedGuest.ResolveScaleRoot();
        return GetTransformPath(scaleRoot != null ? scaleRoot : selectedGuest.transform);
    }

    private static string GetTransformPath(Transform transform)
    {
        if (transform == null)
        {
            return string.Empty;
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

    private static string[] BuildRoomOptions(GuestRoomScaleCalibration calibration, PointClickPlayerMovement butler)
    {
        List<string> rooms = new List<string>();

        if (calibration != null)
        {
            for (int i = 0; i < calibration.Rooms.Count; i++)
            {
                AddRoom(rooms, calibration.Rooms[i]?.roomId);
            }
        }

        if (butler != null)
        {
            List<string> butlerRooms = new List<string>();
            butler.GetButlerScaleOverrideRoomIds(butlerRooms);

            for (int i = 0; i < butlerRooms.Count; i++)
            {
                AddRoom(rooms, butlerRooms[i]);
            }
        }

        RoomContentGroup[] roomContentGroups = FindObjectsByType<RoomContentGroup>(FindObjectsInactive.Include);

        for (int i = 0; i < roomContentGroups.Length; i++)
        {
            AddRoom(rooms, roomContentGroups[i] != null ? roomContentGroups[i].RoomName : null);
        }

        if (rooms.Count == 0)
        {
            rooms.Add("Grand Entrance Hall");
        }

        return rooms.ToArray();
    }

    private static void AddRoom(List<string> rooms, string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            if (GuestRoomScaleCalibration.SameRoom(rooms[i], roomId))
            {
                return;
            }
        }

        rooms.Add(roomId.Trim());
    }

    private static float GetRoomMultiplier(GuestRoomScaleCalibration calibration, string roomId)
    {
        return calibration != null && calibration.TryGetRoom(roomId, out GuestRoomScaleEntry entry)
            ? entry.roomGuestScaleMultiplier
            : 1f;
    }

    private static int CountReadyGuests(GuestScaleParticipant[] guests)
    {
        int ready = 0;

        for (int i = 0; i < guests.Length; i++)
        {
            if (guests[i] != null && guests[i].HasCapturedBaseScale)
            {
                ready++;
            }
        }

        return ready;
    }

    private static PointClickPlayerMovement FindButler()
    {
        PointClickPlayerMovement[] movements = FindObjectsByType<PointClickPlayerMovement>(FindObjectsInactive.Include);

        for (int i = 0; i < movements.Length; i++)
        {
            PointClickPlayerMovement movement = movements[i];

            if (movement == null || movement.gameObject.name.IndexOf("Guest", StringComparison.OrdinalIgnoreCase) >= 0)
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
}
