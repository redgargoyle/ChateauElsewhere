using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class GuestRoomScaleMasterWindow : EditorWindow
{
    public const string ActiveSelectionGuestOptionLabel = "Active Hierarchy Selection";
    public const string AllGuestsSelectionLabel = "All Guests In Selected Room";
    public const string AllGuestsInAllRoomsSelectionLabel = "All Guests In All Rooms";
    public const string ApplyManualSizeToAllGuestsButtonLabel = "APPLY MANUAL SIZE TO SELECTED ROOM";
    public const string ApplyManualSizeToAllRoomsButtonLabel = "APPLY MANUAL SIZE TO ALL ROOMS";

    private const float MinMultiplier = 0.25f;
    private const float MaxMultiplier = 3f;
    private const float MinManualGuestScale = 0.1f;
    private const float MaxManualGuestScale = 5f;
    private const int ActiveSelectionGuestOptionIndex = 0;
    private const int AllGuestsSelectionOptionIndex = 1;
    private const int AllGuestsInAllRoomsSelectionOptionIndex = 2;
    private const int FirstExplicitGuestOptionIndex = 3;
    private static readonly string[] ScaleModeOptions =
    {
        "Manual Size + Butler Depth"
    };
    private static readonly GuestRoomScaleMode[] ScaleModeValues =
    {
        GuestRoomScaleMode.ButlerCurve
    };

    internal enum GuestRoomScaleMode
    {
        ButlerCurve
    }

    private int selectedRoomIndex;
    private int selectedManualGuestOptionIndex;
    private string loadedRoomId = string.Empty;
    private string loadedManualGuestKey = string.Empty;
    private float selectedRoomMultiplier = 1f;
    private float manualGuestScale = 1f;
    private GuestRoomScaleMode selectedScaleMode = GuestRoomScaleMode.ButlerCurve;
    private bool advancedFoldout;
    private string lastAction = "Ready";

    [MenuItem("Tools/Characters/Guest Size Master")]
    public static void OpenWindow()
    {
        GetWindow<GuestRoomScaleMasterWindow>("Guest Size Master");
    }

    private readonly struct ManualGuestSelection
    {
        public ManualGuestSelection(GuestScaleParticipant selectedGuest, bool allGuests, bool allRooms, string selectionKey)
        {
            SelectedGuest = selectedGuest;
            AllGuests = allGuests;
            AllRooms = allRooms;
            SelectionKey = selectionKey;
        }

        public GuestScaleParticipant SelectedGuest { get; }
        public bool AllGuests { get; }
        public bool AllRooms { get; }
        public string SelectionKey { get; }
    }

    private void OnSelectionChange()
    {
        loadedManualGuestKey = string.Empty;
        Repaint();
    }

    private void OnGUI()
    {
        PointClickPlayerMovement butler = FindButler();
        GuestRoomScaleCalibration calibration = FindAnyObjectByType<GuestRoomScaleCalibration>(FindObjectsInactive.Include);
        GuestRoomScaleApplier applier = FindAnyObjectByType<GuestRoomScaleApplier>(FindObjectsInactive.Include);
        GuestScaleParticipant[] guests = FindGuestParticipants();
        string[] rooms = BuildRoomOptions(calibration, butler);
        string selectedRoom = ResolveSelectedRoom(butler, rooms, ref selectedRoomIndex);

        if (!GuestRoomScaleCalibration.SameRoom(loadedRoomId, selectedRoom))
        {
            selectedRoomMultiplier = GetRoomMultiplier(calibration, selectedRoom);
            selectedScaleMode = GetRoomScaleMode(calibration, selectedRoom);
            loadedRoomId = selectedRoom;
            loadedManualGuestKey = string.Empty;
            selectedManualGuestOptionIndex = ActiveSelectionGuestOptionIndex;
        }

        EditorGUILayout.LabelField("Guest Size Master", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Butler found: {(butler != null ? "yes" : "no")}");
        EditorGUILayout.LabelField($"GuestRoomScaleCalibration found: {(calibration != null ? "yes" : "no")}");
        EditorGUILayout.LabelField($"GuestRoomScaleApplier found: {(applier != null ? "yes" : "no")}");
        EditorGUILayout.LabelField($"Guests found: {guests.Length}");
        EditorGUILayout.LabelField($"Guests ready: {CountReadyGuests(guests)}");
        EditorGUILayout.LabelField($"Current selected room: {(string.IsNullOrWhiteSpace(selectedRoom) ? "none" : selectedRoom)}");
        bool hasCurrentRoomStageScale = GuestRoomStageScaleUtility.TryGetActiveRoomStageScale(out float currentRoomStageScale);
        float referenceRoomStageScale = 1f;
        bool hasReferenceRoomStageScale = calibration != null &&
            calibration.TryGetReferenceRoomStageScale(selectedRoom, out referenceRoomStageScale);
        referenceRoomStageScale = hasReferenceRoomStageScale ? referenceRoomStageScale : 1f;
        float roomStageZoomRatio = hasCurrentRoomStageScale
            ? GuestRoomStageScaleUtility.CalculateRoomStageZoomRatio(currentRoomStageScale, referenceRoomStageScale)
            : 1f;
        int selectedRoomGuestCount = CountGuestsInRoom(guests, selectedRoom);
        int activeVisibleGuestCount = CountActiveVisibleManagedGuests(guests);

        EditorGUILayout.LabelField($"Current room-stage scale: {FormatScaleStatus(hasCurrentRoomStageScale, currentRoomStageScale)}");
        EditorGUILayout.LabelField($"Saved reference room-stage scale: {FormatScaleStatus(hasReferenceRoomStageScale, referenceRoomStageScale)}");
        EditorGUILayout.LabelField($"Computed room-stage zoom ratio: {roomStageZoomRatio:0.####}");
        EditorGUILayout.LabelField($"Guests in selected room: {selectedRoomGuestCount}");
        if (!string.IsNullOrWhiteSpace(selectedRoom) && selectedRoomGuestCount < activeVisibleGuestCount)
        {
            EditorGUILayout.HelpBox(
                $"Only {selectedRoomGuestCount} of {activeVisibleGuestCount} managed visible guests resolve to {selectedRoom}. Use Explain Room Filtering.",
                MessageType.Warning);
        }
        EditorGUILayout.LabelField($"Last action: {lastAction}");
        EditorGUILayout.Space(8f);

        using (new EditorGUI.DisabledScope(rooms.Length == 0))
        {
            string previousSelectedRoom = selectedRoom;
            selectedRoom = DrawRoomSelection(butler, rooms, ref selectedRoomIndex, selectedRoom);

            if (!GuestRoomScaleCalibration.SameRoom(previousSelectedRoom, selectedRoom))
            {
                selectedRoomMultiplier = GetRoomMultiplier(calibration, selectedRoom);
                selectedScaleMode = GetRoomScaleMode(calibration, selectedRoom);
                loadedRoomId = selectedRoom;
                loadedManualGuestKey = string.Empty;
                selectedManualGuestOptionIndex = ActiveSelectionGuestOptionIndex;
            }

            selectedScaleMode = DrawScaleModeSelection(butler, calibration, selectedRoom, selectedScaleMode);
            DrawPrimaryScaleControl(selectedRoom);
        }

        EditorGUILayout.Space(8f);

        GuestScaleParticipant[] roomGuests = FindGuestsInRoom(guests, selectedRoom);
        ManualGuestSelection manualSelection = ResolveManualGuestSelection(selectedRoom, roomGuests);
        GuestScaleParticipant selectedGuest = manualSelection.SelectedGuest;

        if (!string.Equals(loadedManualGuestKey, manualSelection.SelectionKey, StringComparison.Ordinal))
        {
            manualGuestScale = manualSelection.AllGuests
                ? GetAverageGuestCurrentScale(manualSelection.AllRooms ? guests : roomGuests)
                : GetGuestCurrentScale(selectedGuest, calibration, selectedRoom);
            loadedManualGuestKey = manualSelection.SelectionKey;
        }

        if (GUILayout.Button("SET UP GUEST SCALING"))
        {
            SetupGuestScaling(selectedRoom);
        }

        if (GUILayout.Button("PREVIEW ROOM GUEST SIZE"))
        {
            PreviewSelectedRoom(selectedRoom);
        }

        if (GUILayout.Button("SAVE ROOM GUEST SIZE"))
        {
            SaveSelectedRoom(calibration, selectedRoom, manualSelection);
        }

        if (GUILayout.Button("SAVE SCENE"))
        {
            EditorSceneManager.SaveOpenScenes();
            lastAction = "Saved open scenes.";
        }

        DrawAdvanced(selectedRoom, roomGuests, manualSelection);
    }

    private static string DrawRoomSelection(
        PointClickPlayerMovement butler,
        string[] rooms,
        ref int selectedIndex,
        string selectedRoom)
    {
        if (rooms == null || rooms.Length == 0)
        {
            selectedIndex = 0;
            return string.Empty;
        }

        EditorGUI.BeginChangeCheck();
        selectedIndex = EditorGUILayout.Popup("Current Room", selectedIndex, rooms);

        if (EditorGUI.EndChangeCheck())
        {
            selectedRoom = rooms[selectedIndex];
            SelectGuestScaleRoom(butler, selectedRoom);
        }

        return selectedRoom;
    }

    private void DrawPrimaryScaleControl(string selectedRoom)
    {
        selectedScaleMode = GuestRoomScaleMode.ButlerCurve;
        EditorGUILayout.HelpBox(
            "Guests use this room size multiplier and always follow Butler room-depth scaling at their current room position.",
            MessageType.Info);
        EditorGUI.BeginChangeCheck();
        selectedRoomMultiplier = EditorGUILayout.Slider("Guest Size In This Room", selectedRoomMultiplier, MinMultiplier, MaxMultiplier);

        if (EditorGUI.EndChangeCheck() && !string.IsNullOrWhiteSpace(selectedRoom))
        {
            PreviewSelectedRoom(selectedRoom);
        }
    }

    private void StepSelectedRoom(int delta)
    {
        PointClickPlayerMovement butler = FindButler();
        GuestRoomScaleCalibration calibration = FindAnyObjectByType<GuestRoomScaleCalibration>(FindObjectsInactive.Include);
        string[] rooms = BuildRoomOptions(calibration, butler);

        if (rooms == null || rooms.Length == 0)
        {
            return;
        }

        selectedRoomIndex = (selectedRoomIndex + delta + rooms.Length) % rooms.Length;
        string selectedRoom = rooms[selectedRoomIndex];
        SelectGuestScaleRoom(butler, selectedRoom);
        selectedRoomMultiplier = GetRoomMultiplier(calibration, selectedRoom);
        selectedScaleMode = GetRoomScaleMode(calibration, selectedRoom);
        loadedRoomId = selectedRoom;
        loadedManualGuestKey = string.Empty;
        selectedManualGuestOptionIndex = ActiveSelectionGuestOptionIndex;
        Repaint();
    }

    private GuestRoomScaleMode DrawScaleModeSelection(
        PointClickPlayerMovement butler,
        GuestRoomScaleCalibration calibration,
        string selectedRoom,
        GuestRoomScaleMode currentMode)
    {
        EditorGUILayout.LabelField("Scale Rule", ScaleModeOptions[0]);
        SelectGuestScaleRoom(butler, selectedRoom);
        return ScaleModeValues[0];
    }

    private void DrawAdvanced(
        string selectedRoom,
        GuestScaleParticipant[] roomGuests,
        ManualGuestSelection manualSelection)
    {
        advancedFoldout = EditorGUILayout.Foldout(advancedFoldout, "Advanced / debugging", true);

        if (!advancedFoldout)
        {
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Previous Room"))
            {
                StepSelectedRoom(-1);
            }

            if (GUILayout.Button("Next Room"))
            {
                StepSelectedRoom(1);
            }

            if (GUILayout.Button("Ping RoomContentGroup"))
            {
                PingRoom(selectedRoom);
            }
        }

        EditorGUILayout.HelpBox(
            "Scale exceptions are disabled. Seated drawing-room and dining-room rules may change sorting/occlusion only.",
            MessageType.Info);

        if (GUILayout.Button("Run Guest Scale Audit"))
        {
            GuestScaleAudit.RunAndWriteReport();
            lastAction = "Audit written.";
        }

        if (GUILayout.Button("Log Guest Scale Diagnostics"))
        {
            LogGuestScaleDiagnostics(selectedRoom);
            lastAction = "Guest scale diagnostics logged.";
        }

        if (GUILayout.Button("EXPLAIN ROOM FILTERING FOR SELECTED ROOM"))
        {
            LogRoomFilteringExplanation(selectedRoom);
            lastAction = "Room filtering explanation logged.";
        }

        if (GUILayout.Button("Reset Selected Room Multiplier"))
        {
            GuestRoomScaleCalibration calibration = FindAnyObjectByType<GuestRoomScaleCalibration>(FindObjectsInactive.Include);

            if (calibration != null)
            {
                Undo.RecordObject(calibration, "Reset Guest Room Size");
                calibration.ClearFixedGuestScale(selectedRoom);
                calibration.ClearCustomCurve(selectedRoom);
                calibration.SetRoomMultiplier(selectedRoom, 1f);
                selectedRoomMultiplier = 1f;
                EditorUtility.SetDirty(calibration);
                lastAction = $"Reset {selectedRoom} multiplier.";
            }
        }

        if (GUILayout.Button("MATCH BUTLER SIZE IN ROOM"))
        {
            GuestRoomScaleCalibration calibration = FindAnyObjectByType<GuestRoomScaleCalibration>(FindObjectsInactive.Include);

            if (calibration != null)
            {
                Undo.RecordObject(calibration, "Match Butler Size In Room");
                calibration.ClearFixedGuestScale(selectedRoom);
                calibration.ClearCustomCurve(selectedRoom);
                calibration.SetRoomMultiplier(selectedRoom, 1f);
                EditorUtility.SetDirty(calibration);
            }

            selectedRoomMultiplier = 1f;
            GuestScaleApplyResult result = RefreshRoomWithUndo(selectedRoom, "Match Butler Size In Room");
            lastAction = $"Matched Butler size for {selectedRoom}; applied {result.Applied}, changed {result.Changed}.";
        }

        if (GUILayout.Button("APPLY TO ALL GUESTS IN ROOM"))
        {
            ApplySelectedRoom(selectedRoom);
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

    private void SetupGuestScaling(string selectedRoom)
    {
        PointClickPlayerMovement butler = FindButler();
        GuestRoomScaleCalibration calibration = EnsureCalibration(butler);
        GuestRoomScaleApplier applier = GuestRoomScaleApplier.EnsureInScene();
        applier.SetCalibration(calibration);
        int removed = RemoveInvalidParticipants();
        int ensured = applier.EnsureParticipantsForSceneGuests();
        int synced = 0;
        GuestScaleApplyResult result = applier.RefreshAllWithResultNow();

        EditorUtility.SetDirty(calibration);
        EditorUtility.SetDirty(applier);
        EditorSceneManager.MarkAllScenesDirty();
        lastAction = $"Set up guest scaling. Removed {removed}, ensured {ensured}, synced {synced}, applied {result.Applied}, changed {result.Changed}.";
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
        SaveRoomGuestSizeForCalibration(
            calibration,
            selectedRoom,
            selectedRoomMultiplier,
            manualGuestScale,
            saveManualScale: false,
            selectedScaleMode);
        applier.SetCalibration(calibration);

        List<Transform> scaleRoots = CollectParticipantScaleRoots(selectedRoom);
        RecordScaleRoots(scaleRoots, "Preview Room Guest Size");

        GuestScaleApplyResult result = applier.RefreshRoomNow(selectedRoom);
        MarkScaleRootsDirty(scaleRoots);
        EditorUtility.SetDirty(calibration);
        EditorUtility.SetDirty(applier);
        SceneView.RepaintAll();
        Repaint();
        float zoomRatio = GetRoomStageZoomRatio(calibration, selectedRoom);
        lastAction = $"Previewed {selectedRoom}; mode {FormatScaleMode(selectedScaleMode)}; value {selectedRoomMultiplier:0.###}; zoom {zoomRatio:0.####}; applied {result.Applied}, changed {result.Changed}.";
        Debug.Log($"[Guest Size Master] {lastAction}");
    }

    private void SaveSelectedRoom(
        GuestRoomScaleCalibration calibration,
        string selectedRoom,
        ManualGuestSelection manualSelection)
    {
        if (calibration == null || string.IsNullOrWhiteSpace(selectedRoom))
        {
            lastAction = "Save skipped: setup is incomplete.";
            return;
        }

        Undo.RecordObject(calibration, "Save Room Guest Size");
        bool saveManualScale = manualSelection.AllGuests && !manualSelection.AllRooms;
        SaveRoomGuestSizeForCalibration(
            calibration,
            selectedRoom,
            selectedRoomMultiplier,
            manualGuestScale,
            saveManualScale,
            selectedScaleMode);
        float referenceStageScale = SaveCurrentRoomStageReference(calibration, selectedRoom);
        GuestScaleApplyResult result = RefreshRoomWithUndo(selectedRoom, "Save Room Guest Size");
        EditorUtility.SetDirty(calibration);
        EditorSceneManager.MarkAllScenesDirty();
        lastAction = $"Saved guest size for {selectedRoom}; reference stage {referenceStageScale:0.####}; applied {result.Applied}, changed {result.Changed}.";
    }

    internal static void SaveRoomGuestSizeForCalibration(
        GuestRoomScaleCalibration calibration,
        string selectedRoom,
        float roomScale,
        float manualScale,
        bool saveManualScale,
        GuestRoomScaleMode scaleMode = GuestRoomScaleMode.ButlerCurve)
    {
        if (calibration == null || string.IsNullOrWhiteSpace(selectedRoom))
        {
            return;
        }

        GuestRoomScaleEntry entry = calibration.GetOrCreateRoom(selectedRoom);
        entry.useButlerRoomCurve = true;
        calibration.ClearFixedGuestScale(selectedRoom);
        calibration.ClearCustomCurve(selectedRoom);
        calibration.SetRoomMultiplier(selectedRoom, saveManualScale ? manualScale : roomScale);
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

        int synced = 0;
        List<Transform> scaleRoots = CollectParticipantScaleRoots(selectedRoom);
        RecordScaleRoots(scaleRoots, "Apply Room Guest Size");
        GuestScaleApplyResult result = applier.RefreshRoomNow(selectedRoom);
        MarkScaleRootsDirty(scaleRoots);
        EditorUtility.SetDirty(applier);
        SceneView.RepaintAll();
        Repaint();
        lastAction = $"Applied {selectedRoom} guest size to {result.Applied} guests; synced {synced}, changed {result.Changed}.";
        Debug.Log($"[Guest Size Master] {lastAction}");
    }

    private static List<Transform> CollectParticipantScaleRoots(string selectedRoom)
    {
        return CollectParticipantScaleRoots(FindGuestsInRoom(FindGuestParticipants(), selectedRoom));
    }

    private static List<Transform> CollectParticipantScaleRoots(GuestScaleParticipant[] guests)
    {
        List<Transform> scaleRoots = new List<Transform>();
        HashSet<Transform> seen = new HashSet<Transform>();

        for (int i = 0; guests != null && i < guests.Length; i++)
        {
            GuestScaleParticipant guest = guests[i];

            if (guest == null ||
                guest.ExcludeFromGuestScaling ||
                guest.IsButler ||
                !GuestRoomScaleApplier.IsManagedGuestParticipant(guest))
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

    private static void LogGuestScaleDiagnostics(string selectedRoom)
    {
        GuestRoomScaleApplier applier = FindAnyObjectByType<GuestRoomScaleApplier>(FindObjectsInactive.Include);
        GuestRoomScaleCalibration calibration = FindAnyObjectByType<GuestRoomScaleCalibration>(FindObjectsInactive.Include);

        if (applier == null)
        {
            Debug.LogWarning("[GuestScale Diagnostic] GuestRoomScaleApplier not found.");
            return;
        }

        if (calibration != null)
        {
            applier.SetCalibration(calibration);
        }

        GuestScaleParticipant[] guests = FindGuestParticipants();

        for (int i = 0; i < guests.Length; i++)
        {
            GuestScaleParticipant guest = guests[i];

            if (guest == null ||
                guest.ExcludeFromGuestScaling ||
                guest.IsButler ||
                !applier.TryComputeParticipantScale(guest, selectedRoom, out GuestScaleComputation computation))
            {
                continue;
            }

            Transform scaleRoot = guest.ResolveScaleRoot();
            bool hasRoomStage = GuestRoomStageScaleUtility.TryGetParticipantRoomStage(
                guest,
                computation.RoomId,
                out RoomContentGroup roomStage);
            bool hasCurrentStageScale = GuestRoomStageScaleUtility.TryGetActiveRoomStageScale(out float currentStageScale);
            float referenceStageScale = 1f;
            bool hasReferenceStageScale = calibration != null &&
                calibration.TryGetReferenceRoomStageScale(computation.RoomId, out referenceStageScale);
            referenceStageScale = hasReferenceStageScale ? referenceStageScale : 1f;
            bool underActiveRoomStage = hasRoomStage &&
                roomStage != null &&
                roomStage.gameObject.activeInHierarchy &&
                IsScaleNearActiveStage(roomStage.transform.lossyScale.x, currentStageScale, hasCurrentStageScale);
            bool hasPointClickPlayerMovement = HasPointClickPlayerMovement(guest);
            bool pointClickSkippedByGuestParticipant = hasPointClickPlayerMovement &&
                !guest.ExcludeFromGuestScaling &&
                !guest.IsButler;

            Debug.Log(
                "[GuestScale Diagnostic] " +
                $"guest={GetTransformPath(guest.transform)} " +
                $"room={computation.RoomId} " +
                $"roomLocalY={computation.RoomLocalY:0.###} " +
                $"scaleRoot={GetTransformPath(scaleRoot)} " +
                $"scaleRootLocalScale={FormatVector(scaleRoot != null ? scaleRoot.localScale : Vector3.one)} " +
                $"scaleRootLossyScale={FormatVector(scaleRoot != null ? scaleRoot.lossyScale : Vector3.one)} " +
                $"parentRoomContent={GetTransformPath(hasRoomStage && roomStage != null ? roomStage.transform : null)} " +
                $"underActiveRoomStage={underActiveRoomStage} " +
                $"currentRoomStageScale={FormatScaleStatus(hasCurrentStageScale, currentStageScale)} " +
                $"savedReferenceRoomStageScale={FormatScaleStatus(hasReferenceStageScale, referenceStageScale)} " +
                $"roomStageZoomRatio={computation.RoomStageZoomRatio:0.####} " +
                $"inheritedRoomStageZoomRatio={computation.InheritedRoomStageZoomRatio:0.####} " +
                $"baseGuestScale={computation.BaseGuestScale:0.####} " +
                $"targetLocalScale={computation.TargetLocalScale:0.####} " +
                $"pointClickPlayerMovementExists={hasPointClickPlayerMovement} " +
                $"pointClickSkippedByGuestParticipant={pointClickSkippedByGuestParticipant} " +
                $"roomScaleDiagnostic='{computation.RoomScaleDiagnostic}' " +
                $"roomStageDiagnostic='{computation.RoomStageZoomDiagnostic}' " +
                $"inheritedStageDiagnostic='{computation.InheritedRoomStageZoomDiagnostic}'",
                guest);
        }

        PointClickPlayerMovement butler = FindButler();

        if (butler != null)
        {
            bool hasCurrentStageScale = GuestRoomStageScaleUtility.TryGetActiveRoomStageScale(out float currentStageScale);
            bool hasButlerSample = butler.TryEvaluateCurrentButlerCharacterScale(out PointClickPlayerMovement.ButlerCharacterScaleSample sample);
            float zoomRatio = GetRoomStageZoomRatio(calibration, selectedRoom);
            Debug.Log(
                "[GuestScale Diagnostic] " +
                $"butler={GetTransformPath(butler.transform)} " +
                $"localScale={FormatVector(butler.transform.localScale)} " +
                $"currentRoomStageScale={FormatScaleStatus(hasCurrentStageScale, currentStageScale)} " +
                $"currentRoomStageZoomRatio={zoomRatio:0.####} " +
                $"butlerRoomScaleSample={(hasButlerSample ? sample.ButlerFinalLocalScaleY.ToString("0.####") : "not available")}",
                butler);
        }
    }

    private static void LogRoomFilteringExplanation(string selectedRoom)
    {
        GuestScaleParticipant[] guests = FindGuestParticipants();
        Array.Sort(
            guests,
            (left, right) => string.Compare(
                left != null ? left.CharacterId : string.Empty,
                right != null ? right.CharacterId : string.Empty,
                StringComparison.OrdinalIgnoreCase));

        int included = 0;

        for (int i = 0; i < guests.Length; i++)
        {
            GuestScaleParticipant guest = guests[i];

            if (guest == null)
            {
                continue;
            }

            GuestRoomResolutionTrace trace = guest.BuildRoomResolutionTrace(selectedRoom);

            if (trace.IncludedInSelectedRoom)
            {
                included++;
            }

            Debug.Log(
                "[GuestScale Room Filter] " +
                $"selectedRoom='{selectedRoom}' " +
                $"guest='{trace.CharacterId}' " +
                $"path='{trace.ObjectPath}' " +
                $"activeInHierarchy={trace.ActiveInHierarchy} " +
                $"currentRoomId='{trace.CurrentRoomId}' " +
                $"actorRoomState='{trace.ActorRoomStateRoomId}' " +
                $"projectedCurrentVisualScaleRoom='{trace.ProjectedCurrentVisualScaleRoomId}' " +
                $"projectedRoomProfile='{trace.ProjectedRoomProfileRoomId}' " +
                $"parentRoomContent='{trace.ParentRoomContentRoomName}' " +
                $"walkerRoomProfile='{trace.WalkerRoomProfileRoomId}' " +
                $"activeNavigationRoom='{trace.ActiveNavigationRoomId}' " +
                $"roomIdOverride='{trace.RoomIdOverride}' " +
                $"authoredNameInference='{trace.AuthoredNameInferenceRoomId}' " +
                $"finalRoom='{trace.FinalRoomId}' " +
                $"finalSource='{trace.FinalSource}' " +
                $"included={trace.IncludedInSelectedRoom} " +
                $"exclusionReason='{trace.ExclusionReason}'",
                guest);
        }

        Debug.Log(
            $"[GuestScale Room Filter] Summary selectedRoom='{selectedRoom}' included={included}/{guests.Length}.",
            FindAnyObjectByType<GuestRoomScaleApplier>(FindObjectsInactive.Include));
    }

    private static bool HasPointClickPlayerMovement(GuestScaleParticipant guest)
    {
        return guest != null &&
            (guest.GetComponent<PointClickPlayerMovement>() != null ||
            guest.GetComponentInParent<PointClickPlayerMovement>(true) != null ||
            guest.GetComponentInChildren<PointClickPlayerMovement>(true) != null);
    }

    private static bool IsScaleNearActiveStage(
        float candidateScale,
        float activeStageScale,
        bool hasActiveStageScale)
    {
        if (!hasActiveStageScale)
        {
            return false;
        }

        float safeActiveStageScale = Mathf.Max(0.0001f, activeStageScale);
        return Mathf.Abs(candidateScale - safeActiveStageScale) <= Mathf.Max(0.001f, safeActiveStageScale * 0.001f);
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:0.####}, {value.y:0.####}, {value.z:0.####})";
    }

    private static GuestScaleParticipant[] FindGuestParticipants()
    {
        GuestScaleParticipant[] allGuests = FindObjectsByType<GuestScaleParticipant>(FindObjectsInactive.Include);
        List<GuestScaleParticipant> guests = new List<GuestScaleParticipant>();

        for (int i = 0; i < allGuests.Length; i++)
        {
            GuestScaleParticipant guest = allGuests[i];

            if (GuestRoomScaleApplier.IsManagedGuestParticipant(guest))
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

            if (guest == null)
            {
                continue;
            }

            bool shouldRemove =
                GuestRoomScaleApplier.IsGuestScaleInfrastructureObject(guest.gameObject) ||
                (!guest.ExcludeFromGuestScaling &&
                    !guest.IsButler &&
                    !GuestRoomScaleApplier.IsManagedGuestParticipant(guest));

            if (shouldRemove)
            {
                Undo.DestroyObjectImmediate(guest);
                removed++;
            }
        }

        return removed;
    }

    private ManualGuestSelection ResolveManualGuestSelection(
        string selectedRoom,
        GuestScaleParticipant[] roomGuests)
    {
        int roomGuestCount = roomGuests != null ? roomGuests.Length : 0;
        selectedManualGuestOptionIndex = Mathf.Clamp(
            selectedManualGuestOptionIndex,
            0,
            FirstExplicitGuestOptionIndex + roomGuestCount - 1);

        string roomKey = GuestRoomScaleCalibration.NormalizeRoomName(selectedRoom);

        if (selectedManualGuestOptionIndex == AllGuestsSelectionOptionIndex)
        {
            return new ManualGuestSelection(null, true, false, $"{roomKey}|all-selected-room");
        }

        if (selectedManualGuestOptionIndex == AllGuestsInAllRoomsSelectionOptionIndex)
        {
            return new ManualGuestSelection(null, true, true, "all-rooms");
        }

        if (selectedManualGuestOptionIndex >= FirstExplicitGuestOptionIndex)
        {
            int guestIndex = selectedManualGuestOptionIndex - FirstExplicitGuestOptionIndex;
            GuestScaleParticipant explicitGuest = guestIndex >= 0 && guestIndex < roomGuestCount
                ? roomGuests[guestIndex]
                : null;

            if (explicitGuest != null)
            {
                return new ManualGuestSelection(
                    explicitGuest,
                    false,
                    false,
                    $"{roomKey}|explicit|{GetGuestSelectionKey(explicitGuest)}");
            }
        }

        GuestScaleParticipant selectedGuest = ResolveSelectedGuest(selectedRoom, roomGuests);
        return new ManualGuestSelection(
            selectedGuest,
            false,
            false,
            $"{roomKey}|active|{GetGuestSelectionKey(selectedGuest)}");
    }

    private static GuestScaleParticipant ResolveSelectedGuest(string selectedRoom)
    {
        return ResolveSelectedGuest(selectedRoom, FindGuestsInRoom(FindGuestParticipants(), selectedRoom));
    }

    private static GuestScaleParticipant ResolveSelectedGuest(
        string selectedRoom,
        GuestScaleParticipant[] roomGuests)
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

        for (int i = 0; roomGuests != null && i < roomGuests.Length; i++)
        {
            if (IsRoomGuest(roomGuests[i], selectedRoom))
            {
                return roomGuests[i];
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
            GuestRoomScaleApplier.IsManagedGuestParticipant(guest) &&
            GuestRoomScaleApplier.ShouldApplyParticipantForRoomContext(guest, selectedRoom);
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
                    selectedGuest.ResolveRoomLocalY(selectedRoom),
                    out float evaluatedScale,
                    out _,
                    out _))
            {
                return Mathf.Clamp(evaluatedScale, MinManualGuestScale, MaxManualGuestScale);
            }
        }

        return 1f;
    }

    private static float GetAverageGuestCurrentScale(GuestScaleParticipant[] roomGuests)
    {
        float sum = 0f;
        int count = 0;

        for (int i = 0; roomGuests != null && i < roomGuests.Length; i++)
        {
            Transform scaleRoot = roomGuests[i] != null ? roomGuests[i].ResolveScaleRoot() : null;

            if (scaleRoot == null)
            {
                continue;
            }

            sum += Mathf.Abs(scaleRoot.localScale.y);
            count++;
        }

        if (count == 0)
        {
            return 1f;
        }

        return Mathf.Clamp(sum / count, MinManualGuestScale, MaxManualGuestScale);
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

    private static string[] BuildManualGuestOptions(GuestScaleParticipant[] roomGuests)
    {
        int roomGuestCount = roomGuests != null ? roomGuests.Length : 0;
        string[] options = new string[FirstExplicitGuestOptionIndex + roomGuestCount];
        options[ActiveSelectionGuestOptionIndex] = ActiveSelectionGuestOptionLabel;
        options[AllGuestsSelectionOptionIndex] = AllGuestsSelectionLabel;
        options[AllGuestsInAllRoomsSelectionOptionIndex] = AllGuestsInAllRoomsSelectionLabel;

        for (int i = 0; i < roomGuestCount; i++)
        {
            options[FirstExplicitGuestOptionIndex + i] = FormatManualGuestOption(roomGuests[i]);
        }

        return options;
    }

    private static string FormatManualGuestOption(GuestScaleParticipant guest)
    {
        return guest != null ? guest.CharacterId : "Missing Guest";
    }

    private static GuestScaleParticipant[] FindGuestsInRoom(
        GuestScaleParticipant[] guests,
        string roomId)
    {
        List<GuestScaleParticipant> roomGuests = new List<GuestScaleParticipant>();

        for (int i = 0; guests != null && i < guests.Length; i++)
        {
            GuestScaleParticipant guest = guests[i];

            if (IsRoomGuest(guest, roomId))
            {
                roomGuests.Add(guest);
            }
        }

        roomGuests.Sort((left, right) => string.Compare(
            left != null ? left.CharacterId : string.Empty,
            right != null ? right.CharacterId : string.Empty,
            StringComparison.OrdinalIgnoreCase));
        return roomGuests.ToArray();
    }

    internal static string[] BuildRoomOptions(GuestRoomScaleCalibration calibration, PointClickPlayerMovement butler)
    {
        List<string> rooms = new List<string>();

        if (butler != null)
        {
            AddRoom(rooms, butler.EditorSelectedButlerScaleRoomId);
            AddRoom(rooms, butler.CurrentButlerScaleRoomId);
            AddRoom(rooms, butler.CurrentRoomPerspectiveProfileRoomId);
            RoomNavigationManager navigationManager = FindSceneNavigationManager();
            AddRoom(rooms, navigationManager != null ? navigationManager.CurrentRoom : string.Empty);
            butler.GetButlerScaleOverrideRoomIds(rooms);
        }

        if (calibration != null)
        {
            for (int i = 0; i < calibration.Rooms.Count; i++)
            {
                AddRoom(rooms, calibration.Rooms[i]?.roomId);
            }
        }

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

        if (rooms.Count == 0)
        {
            rooms.Add("Grand Entrance Hall");
        }

        rooms.Sort(StringComparer.OrdinalIgnoreCase);
        return rooms.ToArray();
    }

    internal static string ResolveSelectedRoom(
        PointClickPlayerMovement butler,
        string[] rooms,
        ref int selectedIndex)
    {
        if (rooms == null || rooms.Length == 0)
        {
            selectedIndex = 0;
            return string.Empty;
        }

        string selectedRoom = butler != null ? butler.EditorSelectedButlerScaleRoomId : string.Empty;

        if (string.IsNullOrWhiteSpace(selectedRoom) && butler != null)
        {
            selectedRoom = butler.CurrentButlerScaleRoomId;
        }

        for (int i = 0; i < rooms.Length; i++)
        {
            if (GuestRoomScaleCalibration.SameRoom(rooms[i], selectedRoom))
            {
                selectedIndex = i;
                return rooms[i];
            }
        }

        selectedIndex = Mathf.Clamp(selectedIndex, 0, rooms.Length - 1);
        return rooms[selectedIndex];
    }

    internal static void SelectGuestScaleRoom(PointClickPlayerMovement butler, string roomId)
    {
        if (butler == null || string.IsNullOrWhiteSpace(roomId))
        {
            return;
        }

        Undo.RecordObject(butler, "Select Guest Scale Room");
        Undo.RecordObject(butler.transform, "Select Guest Scale Room");
        butler.SetEditorSelectedButlerScaleRoomId(roomId);
        butler.RefreshPerspectiveScaleNow(true);
        EditorUtility.SetDirty(butler);
        EditorUtility.SetDirty(butler.transform);
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
            if (GuestRoomScaleCalibration.SameRoom(rooms[i], cleanRoomId))
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

            if (roomContentGroup == null ||
                !GuestRoomScaleCalibration.SameRoom(roomContentGroup.RoomName, roomId))
            {
                continue;
            }

            Selection.activeGameObject = roomContentGroup.gameObject;
            EditorGUIUtility.PingObject(roomContentGroup.gameObject);
            return;
        }
    }

    private static float GetRoomMultiplier(GuestRoomScaleCalibration calibration, string roomId)
    {
        if (calibration == null || !calibration.TryGetRoom(roomId, out GuestRoomScaleEntry entry))
        {
            return 1f;
        }

        return Mathf.Clamp(entry.roomGuestScaleMultiplier, MinMultiplier, MaxMultiplier);
    }

    private static GuestRoomScaleMode GetRoomScaleMode(GuestRoomScaleCalibration calibration, string roomId)
    {
        return GuestRoomScaleMode.ButlerCurve;
    }

    private static string FormatScaleMode(GuestRoomScaleMode mode)
    {
        return "Manual Size + Butler Depth";
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

    private static int CountActiveVisibleManagedGuests(GuestScaleParticipant[] guests)
    {
        int count = 0;

        for (int i = 0; i < guests.Length; i++)
        {
            GuestScaleParticipant guest = guests[i];

            if (guest != null &&
                GuestRoomScaleApplier.IsManagedGuestParticipant(guest) &&
                guest.gameObject.activeInHierarchy &&
                HasVisibleRendererOrGraphic(guest))
            {
                count++;
            }
        }

        return count;
    }

    internal static int SyncVisibleGuestsToSelectedRoomForManualEditing(
        GuestScaleParticipant[] guests,
        string selectedRoom)
    {
        string cleanSelectedRoom = GuestRoomScaleCalibration.CleanRoomId(selectedRoom);

        if (guests == null || string.IsNullOrWhiteSpace(cleanSelectedRoom))
        {
            return 0;
        }

        if (!CanSyncVisibleGuestsToSelectedRoom(cleanSelectedRoom))
        {
            return 0;
        }

        int synced = 0;

        for (int i = 0; i < guests.Length; i++)
        {
            GuestScaleParticipant guest = guests[i];

            if (!IsVisibleManagedGuestForManualEditing(guest) ||
                GuestRoomScaleCalibration.SameRoom(guest.CurrentRoomId, cleanSelectedRoom))
            {
                continue;
            }

            Undo.RecordObject(guest, "Assign Visible Guest Room");
            guest.SetCurrentRoomId(cleanSelectedRoom);
            EditorUtility.SetDirty(guest);
            synced++;
        }

        return synced;
    }

    private static bool CanSyncVisibleGuestsToSelectedRoom(string selectedRoom)
    {
        if (!Application.isPlaying)
        {
            return true;
        }

        return !TryGetActiveNavigationRoomId(out string activeRoomId) ||
            GuestRoomScaleCalibration.SameRoom(activeRoomId, selectedRoom);
    }

    private static bool TryGetActiveNavigationRoomId(out string roomId)
    {
        roomId = string.Empty;
        RoomNavigationManager navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Exclude);

        if (navigationManager == null || string.IsNullOrWhiteSpace(navigationManager.CurrentRoom))
        {
            return false;
        }

        roomId = GuestRoomScaleCalibration.CleanRoomId(navigationManager.CurrentRoom);
        return !string.IsNullOrWhiteSpace(roomId);
    }

    private static bool IsVisibleManagedGuestForManualEditing(GuestScaleParticipant guest)
    {
        return guest != null &&
            !guest.ExcludeFromGuestScaling &&
            !guest.IsButler &&
            !GuestRoomScaleApplier.IsGuestScaleInfrastructureObject(guest.gameObject) &&
            GuestRoomScaleApplier.IsManagedGuestParticipant(guest) &&
            guest.gameObject.activeInHierarchy &&
            HasVisibleRendererOrGraphic(guest);
    }

    private static bool HasVisibleRendererOrGraphic(GuestScaleParticipant guest)
    {
        Renderer[] renderers = guest.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        Graphic[] graphics = guest.GetComponentsInChildren<Graphic>(true);

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

    private static int CountGuestsInRoom(GuestScaleParticipant[] guests, string roomId)
    {
        int count = 0;

        for (int i = 0; i < guests.Length; i++)
        {
            GuestScaleParticipant guest = guests[i];

            if (IsRoomGuest(guest, roomId))
            {
                count++;
            }
        }

        return count;
    }

    private static string FormatScaleStatus(bool hasScale, float scale)
    {
        return hasScale ? scale.ToString("0.####") : "not available";
    }

    private static float SaveCurrentRoomStageReference(
        GuestRoomScaleCalibration calibration,
        string roomId)
    {
        float referenceStageScale = GuestRoomStageScaleUtility.TryGetActiveRoomStageScale(out float currentStageScale)
            ? currentStageScale
            : 1f;
        calibration.SetReferenceRoomStageScale(roomId, referenceStageScale);
        return referenceStageScale;
    }

    private static float GetRoomStageZoomRatio(
        GuestRoomScaleCalibration calibration,
        string roomId)
    {
        if (!GuestRoomStageScaleUtility.TryGetActiveRoomStageScale(out float currentStageScale))
        {
            return 1f;
        }

        float referenceStageScale = calibration != null &&
            calibration.TryGetReferenceRoomStageScale(roomId, out float savedReferenceScale)
                ? savedReferenceScale
                : 1f;
        return GuestRoomStageScaleUtility.CalculateRoomStageZoomRatio(
            currentStageScale,
            referenceStageScale);
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
