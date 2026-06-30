using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class GuestRoomScaleMasterWindow : EditorWindow
{
    private const float MinMultiplier = 0.25f;
    private const float MaxMultiplier = 3f;

    private int selectedRoomIndex;
    private float selectedRoomMultiplier = 1f;
    private bool advancedFoldout;
    private float customFrontY;
    private float customFrontScale = 1f;
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
        GuestScaleParticipant[] guests = FindObjectsByType<GuestScaleParticipant>(FindObjectsInactive.Include);
        string[] rooms = BuildRoomOptions(calibration, butler);
        selectedRoomIndex = Mathf.Clamp(selectedRoomIndex, 0, Mathf.Max(0, rooms.Length - 1));
        string selectedRoom = rooms.Length > 0 ? rooms[selectedRoomIndex] : string.Empty;

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
            }

            EditorGUI.BeginChangeCheck();
            selectedRoomMultiplier = EditorGUILayout.Slider("Guest Size In This Room", selectedRoomMultiplier, MinMultiplier, MaxMultiplier);

            if (EditorGUI.EndChangeCheck() && calibration != null && !string.IsNullOrWhiteSpace(selectedRoom))
            {
                Undo.RecordObject(calibration, "Edit Guest Room Size");
                calibration.SetRoomMultiplier(selectedRoom, selectedRoomMultiplier);
                EditorUtility.SetDirty(calibration);
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

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Custom Front/Back Guest Curve", EditorStyles.boldLabel);
        customFrontY = EditorGUILayout.FloatField("Front Room Local Y", customFrontY);
        customFrontScale = EditorGUILayout.FloatField("Front Guest Scale", Mathf.Max(0.001f, customFrontScale));
        customBackY = EditorGUILayout.FloatField("Back Room Local Y", customBackY);
        customBackScale = EditorGUILayout.FloatField("Back Guest Scale", Mathf.Max(0.001f, customBackScale));

        if (GUILayout.Button("Save Custom Front/Back Guest Curve"))
        {
            GuestRoomScaleCalibration calibration = EnsureCalibration(FindButler());
            Undo.RecordObject(calibration, "Save Custom Guest Curve");
            calibration.SetFront(selectedRoom, customFrontY, customFrontScale);
            calibration.SetBack(selectedRoom, customBackY, customBackScale);
            EditorUtility.SetDirty(calibration);
            lastAction = $"Saved custom guest curve for {selectedRoom}.";
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
            GuestScaleParticipant[] guests = FindObjectsByType<GuestScaleParticipant>(FindObjectsInactive.Include);

            for (int i = 0; i < guests.Length; i++)
            {
                guests[i]?.RestoreCapturedBaseScale();
            }

            lastAction = "Restored captured base scales.";
        }
    }

    private void SetupGuestScaling()
    {
        PointClickPlayerMovement butler = FindButler();
        GuestRoomScaleCalibration calibration = EnsureCalibration(butler);
        GuestRoomScaleApplier applier = GuestRoomScaleApplier.EnsureInScene();
        applier.SetCalibration(calibration);
        int ensured = applier.EnsureParticipantsForSceneGuests();
        int applied = applier.RefreshAllNow();

        EditorUtility.SetDirty(calibration);
        EditorUtility.SetDirty(applier);
        EditorSceneManager.MarkAllScenesDirty();
        lastAction = $"Set up guest scaling. Ensured {ensured}, applied {applied}.";
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
        GuestScaleParticipant[] guests = FindObjectsByType<GuestScaleParticipant>(FindObjectsInactive.Include);

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
        GuestScaleParticipant[] guests = FindObjectsByType<GuestScaleParticipant>(FindObjectsInactive.Include);

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
