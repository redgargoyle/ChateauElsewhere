using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class GuestButlerScaleTool : EditorWindow
{
    private PointClickPlayerMovement selectedButler;
    private Vector2 scroll;
    private string lastStatus = string.Empty;
    private double proofRestoreAt;
    private GuestButlerScaleHarmonizer activeProofHarmonizer;

    [MenuItem("Tools/Characters/Apply Butler Scaling To Guests")]
    public static void Open()
    {
        GetWindow<GuestButlerScaleTool>("Guest Butler Scale");
    }

    [MenuItem("Tools/Characters/Human Scale Audit")]
    public static void OpenHumanScaleAudit()
    {
        Open();
    }

    private void OnEnable()
    {
        selectedButler = FindSceneButler();
    }

    private void OnDisable()
    {
        EditorApplication.update -= ProofUpdate;
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Guest Butler Scale", EditorStyles.boldLabel);

        DrawTopControls();
        DrawActions();
        DrawProofControls();
        DrawRoomCalibrationCoverage();
        DrawAuditTable();

        if (!string.IsNullOrWhiteSpace(lastStatus))
        {
            EditorGUILayout.HelpBox(lastStatus, MessageType.None);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawTopControls()
    {
        selectedButler = (PointClickPlayerMovement)EditorGUILayout.ObjectField(
            "Butler / Player",
            selectedButler,
            typeof(PointClickPlayerMovement),
            true);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Find Scene Butler"))
            {
                selectedButler = FindSceneButler();
                lastStatus = selectedButler != null ? $"Found {selectedButler.name}." : "No Butler/player found.";
            }

            using (new EditorGUI.DisabledScope(selectedButler == null))
            {
                if (GUILayout.Button("Add/Ensure GuestButlerScaleHarmonizer"))
                {
                    GuestButlerScaleHarmonizer harmonizer = EnsureHarmonizer();
                    lastStatus = harmonizer != null ? $"Ensured harmonizer on {selectedButler.name}." : "Could not add harmonizer.";
                }
            }
        }
    }

    private void DrawActions()
    {
        if (GUILayout.Button("Open Manual Guest Scale Calibration"))
        {
            GuestScaleCalibrationWindow.Open();
        }

        if (GUILayout.Button("Run Guest Scale Override Audit"))
        {
            GuestScaleOverrideAuditSummary audit = GuestScaleOverrideAudit.WriteReport();
            lastStatus = $"Wrote GuestScaleOverrideAudit.md. Overrides {audit.RoomProjectedEntityOverrideEntries}, non-1 profile multipliers {audit.NonOneCharacterHeightMultipliers}, custom walker scales {audit.CustomWalkerScaleEntries}.";
        }

        using (new EditorGUI.DisabledScope(selectedButler == null))
        {
            if (GUILayout.Button("Apply Saved Manual Guest Calibrations Now"))
            {
                GuestButlerScaleHarmonizer harmonizer = EnsureHarmonizer();
                EnsureCalibrationStore();
                GuestButlerScaleHarmonizer.GuestScaleApplySummary summary = harmonizer != null
                    ? harmonizer.RefreshNow()
                    : GuestButlerScaleHarmonizer.GuestScaleApplySummary.Empty;
                lastStatus = $"Applied saved manual guest calibrations. Scaled {summary.Scaled}, missing calibration {summary.MissingCalibration}, failed {summary.FitFailed + summary.NoVisualBounds}.";
            }

            if (GUILayout.Button("ENABLE FINAL HUMAN SCALE FOR ALL GUESTS"))
            {
                EnableButlerScalingOnAllGuests();
            }

            if (GUILayout.Button("PRINT SCALE WRITER AUDIT"))
            {
                PrintScaleWriterAudit();
            }

            if (GUILayout.Button("PRINT ALL GUEST SCALE WRITERS"))
            {
                PrintScaleWriterAudit();
            }

            if (GUILayout.Button("Bypass Old Room Visual Scale Overrides For All Guests"))
            {
                int changed = BypassOldRoomVisualScaleOverrides();
                lastStatus = $"Bypassed old room visual scale overrides on {changed} projected guest(s).";
            }

            if (GUILayout.Button("REFRESH FINAL HUMAN SCALE NOW"))
            {
                GuestButlerScaleHarmonizer harmonizer = EnsureHarmonizer();
                GuestButlerScaleHarmonizer.GuestScaleApplySummary summary = harmonizer != null
                    ? harmonizer.RefreshNow()
                    : GuestButlerScaleHarmonizer.GuestScaleApplySummary.Empty;
                lastStatus = $"Refreshed guest scaling. Scaled {summary.Scaled}, missing calibration {summary.MissingCalibration}, skipped {summary.Skipped}.";
            }

            if (GUILayout.Button("EMERGENCY: Restore Proof Baselines / Clamp Bad Guest Scales"))
            {
                EmergencyRestoreProofBaselinesAndClampBadGuestScales();
            }

            if (GUILayout.Button("Emergency Restore / Clamp Bad Guest Scales"))
            {
                EmergencyRestoreProofBaselinesAndClampBadGuestScales();
            }
        }

        if (GUILayout.Button("SAVE SCENE"))
        {
            EditorSceneManager.SaveOpenScenes();
            lastStatus = "Saved open scene(s).";
        }
    }

    private void DrawProofControls()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Proof Test", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(selectedButler == null))
        {
            if (GUILayout.Button("Proof 50% Using Manual Roots"))
            {
                RunProof(0.5f);
            }

            if (GUILayout.Button("Proof 150% Using Manual Roots"))
            {
                RunProof(1.5f);
            }

            if (GUILayout.Button("Restore Proof Baselines"))
            {
                RestoreProof();
            }

            if (GUILayout.Button("PROOF 50%"))
            {
                RunProof(0.5f);
            }

            if (GUILayout.Button("PROOF 150%"))
            {
                RunProof(1.5f);
            }

            if (GUILayout.Button("Restore Real Butler Scaling"))
            {
                RestoreProof();
            }
        }
    }

    private void DrawRoomCalibrationCoverage()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Room Calibration Coverage", EditorStyles.boldLabel);

        if (selectedButler == null)
        {
            EditorGUILayout.HelpBox("No Butler/player selected.", MessageType.Warning);
            return;
        }

        if (GUILayout.Button("Print Missing Guest Room Calibrations"))
        {
            PrintMissingGuestRoomCalibrations();
        }

        List<RoomCalibrationCoverageRow> rows = BuildRoomCalibrationCoverageRows();

        for (int i = 0; i < rows.Count; i++)
        {
            RoomCalibrationCoverageRow row = rows[i];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(row.RoomId, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Complete Butler Calibration?", YesNo(row.HasCompleteCalibration));
            EditorGUILayout.LabelField("Guest Count", row.GuestCount.ToString());
            EditorGUILayout.LabelField("Possible Matching Calibration Ids", row.PossibleMatches);

            if (!row.HasCompleteCalibration && row.GuestCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Missing Butler calibration for {row.RoomId}. {row.GuestCount} guest(s) in this room cannot use real Butler scale until this room is calibrated or aliased.",
                    MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }
    }

    private void DrawAuditTable()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Audit", EditorStyles.boldLabel);

        foreach (GuestAuditRow row in BuildAuditRows())
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(row.ObjectPath, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Controller Type", row.ControllerType);
            EditorGUILayout.LabelField("Visible Visual Root", row.VisibleVisualRoot);
            EditorGUILayout.LabelField("Bounds Root", row.BoundsRoot);
            EditorGUILayout.LabelField("Scale Root", row.ScaleRoot);
            EditorGUILayout.LabelField("Room Id", row.RoomId);
            EditorGUILayout.LabelField("Room-Local Foot Y", row.RoomLocalFootY);
            EditorGUILayout.LabelField("Is Seated?", YesNo(row.IsSeated));
            EditorGUILayout.LabelField("Using Butler Rules?", YesNo(row.UsingButlerRules));
            EditorGUILayout.LabelField("Butler Scale Source Found?", YesNo(row.ButlerScaleSourceFound));
            EditorGUILayout.LabelField("Has Butler Calibration For Room?", YesNo(row.HasButlerCalibrationForRoom));
            EditorGUILayout.LabelField("Normalized Butler Scale", row.NormalizedButlerScale);
            EditorGUILayout.LabelField("Current Visual Height px", row.CurrentVisualHeightPx);
            EditorGUILayout.LabelField("Target Visual Height px", row.TargetVisualHeightPx);
            EditorGUILayout.LabelField("Last Fit Result", row.LastFitResult);
            EditorGUILayout.LabelField("Final Current localScale", row.FinalLocalScale);
            EditorGUILayout.LabelField("Proof Can Force Scale?", YesNo(row.ProofCanForceScale));
            EditorGUILayout.LabelField("Hidden Old Room Visual Override Active?", YesNo(row.HiddenOldOverrideActive));
            EditorGUILayout.LabelField("Active Scale Writers", row.ActiveScaleWriters);

            if (!string.IsNullOrWhiteSpace(row.Warning))
            {
                EditorGUILayout.HelpBox(row.Warning, MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }
    }

    private void EnableButlerScalingOnAllGuests()
    {
        GuestButlerScaleHarmonizer harmonizer = EnsureHarmonizer();
        int projected = 0;
        int walkers = 0;
        int actors = 0;

        RoomProjectedEntity[] entities = FindObjectsByType<RoomProjectedEntity>(FindObjectsInactive.Include);
        for (int i = 0; i < entities.Length; i++)
        {
            RoomProjectedEntity entity = entities[i];

            if (!IsLoadedSceneObject(entity) ||
                entity.Mode != RoomProjectedEntity.ProjectionMode.FloorCharacter ||
                IsButlerObjectOrChild(entity.transform))
            {
                continue;
            }

            Undo.RecordObject(entity, "Enable Butler Scaling On Guest");
            entity.SetButlerCharacterScaleRulesEnabled(true, false);
            entity.SetButlerScaleSource(selectedButler, false);
            entity.SetIgnoreRoomVisualScaleOverridesWhenUsingButlerRules(true, false);
            entity.SetIgnoreVisualProfileHeightMultiplierWhenUsingButlerRules(true, false);
            entity.ApplyButlerCharacterScaleNow(selectedButler);
            MarkDirty(entity);
            projected++;
        }

        RoomPersonWalker2D[] walkerComponents = FindObjectsByType<RoomPersonWalker2D>(FindObjectsInactive.Include);
        for (int i = 0; i < walkerComponents.Length; i++)
        {
            RoomPersonWalker2D walker = walkerComponents[i];

            if (!IsLoadedSceneObject(walker) || IsButlerObjectOrChild(walker.transform))
            {
                continue;
            }

            Undo.RecordObject(walker, "Enable Butler Scaling On Walker");
            walker.SetButlerCharacterScaleRulesEnabled(true, false);
            walker.SetButlerScaleSource(selectedButler, false);
            walker.SetPreserveAuthoredLocalScaleWhenUsingButlerRules(true, false);
            walker.ApplyButlerCharacterScaleNow(selectedButler);
            MarkDirty(walker);
            walkers++;
        }

        ActorRoomState[] actorStates = FindObjectsByType<ActorRoomState>(FindObjectsInactive.Include);
        for (int i = 0; i < actorStates.Length; i++)
        {
            ActorRoomState actor = actorStates[i];

            if (!IsLoadedSceneObject(actor) ||
                IsButlerObjectOrChild(actor.transform) ||
                !LooksLikeGuestActor(actor))
            {
                continue;
            }

            Undo.RecordObject(actor, "Enable Butler Scaling On Actor");

            if (!Application.isPlaying)
            {
                actor.ResetAuthoredActorScaleForEditor();
            }

            actor.ApplyButlerCharacterScaleNow(selectedButler);
            MarkDirty(actor);
            actors++;
        }

        harmonizer?.RefreshNow();
        MarkSceneDirtyFromButler();
        lastStatus = $"Enabled Butler scaling on {projected} projected guest(s), {walkers} walker(s), and {actors} actor guest(s).";
    }

    private int BypassOldRoomVisualScaleOverrides()
    {
        int changed = 0;
        RoomProjectedEntity[] entities = FindObjectsByType<RoomProjectedEntity>(FindObjectsInactive.Include);

        for (int i = 0; i < entities.Length; i++)
        {
            RoomProjectedEntity entity = entities[i];

            if (!IsLoadedSceneObject(entity) ||
                entity.Mode != RoomProjectedEntity.ProjectionMode.FloorCharacter ||
                IsButlerObjectOrChild(entity.transform))
            {
                continue;
            }

            Undo.RecordObject(entity, "Bypass Old Room Visual Scale Overrides");
            entity.SetIgnoreRoomVisualScaleOverridesWhenUsingButlerRules(true);
            MarkDirty(entity);
            changed++;
        }

        MarkSceneDirtyFromButler();
        return changed;
    }

    private void RunProof(float multiplier)
    {
        GuestButlerScaleHarmonizer harmonizer = EnsureHarmonizer();

        if (harmonizer == null)
        {
            lastStatus = "No harmonizer available for proof test.";
            return;
        }

        EnsureCalibrationStore();
        harmonizer.SetDebugGuestScaleMultiplier(multiplier);
        GuestButlerScaleHarmonizer.GuestScaleApplySummary summary = harmonizer.RefreshNow();
        LogProofFailures(summary, multiplier);
        activeProofHarmonizer = harmonizer;
        proofRestoreAt = EditorApplication.timeSinceStartup + 2.0;
        EditorApplication.update -= ProofUpdate;
        EditorApplication.update += ProofUpdate;
        lastStatus = $"Proof multiplier {multiplier:0.##} fit {summary.Scaled} guest visual target(s), {summary.FitFailed + summary.NoVisualBounds} failed.";
    }

    private void RestoreProof()
    {
        if (activeProofHarmonizer == null)
        {
            activeProofHarmonizer = EnsureHarmonizer();
        }

        if (activeProofHarmonizer != null)
        {
            activeProofHarmonizer.RestoreRealButlerScaling();
        }

        EditorApplication.update -= ProofUpdate;
        lastStatus = "Restored real Butler scaling.";
    }

    private void ProofUpdate()
    {
        if (EditorApplication.timeSinceStartup < proofRestoreAt)
        {
            return;
        }

        RestoreProof();
        Repaint();
    }

    private void LogProofFailures(GuestButlerScaleHarmonizer.GuestScaleApplySummary summary, float multiplier)
    {
        if (summary.ProofFailures == null || summary.ProofFailures.Count <= 0)
        {
            return;
        }

        string failures = string.Join(", ", summary.ProofFailures);
        bool noVisibleTarget = failures.IndexOf("No visible Renderer or Graphic", StringComparison.OrdinalIgnoreCase) >= 0 ||
            failures.IndexOf("No visual bounds", StringComparison.OrdinalIgnoreCase) >= 0;
        string message = $"[GuestButlerScale] Proof multiplier {multiplier:0.##} could not fit {summary.ProofFailures.Count} guest visual target(s): {failures}";

        if (noVisibleTarget)
        {
            Debug.LogError(message, selectedButler);
            return;
        }

        Debug.LogWarning(message, selectedButler);
    }

    private void PrintScaleWriterAudit()
    {
        List<GuestAuditRow> rows = BuildAuditRows();

        if (rows.Count == 0)
        {
            Debug.LogWarning("[GuestButlerScale Audit] No guest-like scale targets found in loaded scenes.", selectedButler);
            lastStatus = "No guest-like scale targets found.";
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"[GuestButlerScale Audit] {rows.Count} guest-like scale target(s) in loaded scenes.");

        for (int i = 0; i < rows.Count; i++)
        {
            GuestAuditRow row = rows[i];
            builder.AppendLine();
            builder.AppendLine(row.ObjectPath);
            builder.AppendLine($"  controller: {row.ControllerType}");
            builder.AppendLine($"  visible visual root: {row.VisibleVisualRoot}");
            builder.AppendLine($"  bounds root: {row.BoundsRoot}");
            builder.AppendLine($"  scale root: {row.ScaleRoot}");
            builder.AppendLine($"  room: {row.RoomId}, footY: {row.RoomLocalFootY}, seated: {YesNo(row.IsSeated)}");
            builder.AppendLine($"  current height px: {row.CurrentVisualHeightPx}, target height px: {row.TargetVisualHeightPx}");
            builder.AppendLine($"  localScale: {row.FinalLocalScale}");
            builder.AppendLine($"  active scale writers: {row.ActiveScaleWriters}");

            if (!string.IsNullOrWhiteSpace(row.Warning))
            {
                builder.AppendLine($"  warning: {row.Warning}");
            }
        }

        Debug.Log(builder.ToString(), selectedButler);
        lastStatus = $"Printed scale writer audit for {rows.Count} guest-like target(s).";
    }

    private void EmergencyRestoreProofBaselinesAndClampBadGuestScales()
    {
        RestoreProof();

        int corrected = 0;
        HashSet<Transform> visited = new HashSet<Transform>();
        corrected += ClampBadGuestScales(FindObjectsByType<RoomProjectedEntity>(FindObjectsInactive.Include), visited);
        corrected += ClampBadGuestScales(FindObjectsByType<RoomPersonWalker2D>(FindObjectsInactive.Include), visited);
        corrected += ClampBadGuestScales(FindObjectsByType<ActorRoomState>(FindObjectsInactive.Include), visited);

        MarkSceneDirtyFromButler();
        lastStatus = $"Emergency cleanup restored proof baselines and corrected {corrected} bad guest scale root(s).";
    }

    private GuestButlerScaleHarmonizer EnsureHarmonizer()
    {
        if (selectedButler == null)
        {
            selectedButler = FindSceneButler();
        }

        if (selectedButler == null)
        {
            return null;
        }

        GuestButlerScaleHarmonizer harmonizer = selectedButler.GetComponent<GuestButlerScaleHarmonizer>();

        if (harmonizer == null)
        {
            Undo.RecordObject(selectedButler.gameObject, "Add Guest Butler Scale Harmonizer");
            harmonizer = selectedButler.gameObject.AddComponent<GuestButlerScaleHarmonizer>();
        }

        harmonizer.SetButlerScaleSource(selectedButler);
        MarkDirty(harmonizer);
        return harmonizer;
    }

    private GuestScaleCalibrationStore EnsureCalibrationStore()
    {
        if (selectedButler == null)
        {
            selectedButler = FindSceneButler();
        }

        if (selectedButler == null)
        {
            return null;
        }

        GuestScaleCalibrationStore store = selectedButler.GetComponent<GuestScaleCalibrationStore>();

        if (store == null)
        {
            store = Undo.AddComponent<GuestScaleCalibrationStore>(selectedButler.gameObject);
        }

        GuestButlerScaleHarmonizer harmonizer = EnsureHarmonizer();

        if (harmonizer != null)
        {
            Undo.RecordObject(harmonizer, "Assign Guest Scale Calibration Store");
            harmonizer.SetCalibrationStore(store);
            MarkDirty(harmonizer);
        }

        MarkDirty(store);
        return store;
    }

    private List<RoomCalibrationCoverageRow> BuildRoomCalibrationCoverageRows()
    {
        List<RoomCalibrationCoverageRow> rows = new List<RoomCalibrationCoverageRow>();
        Dictionary<string, int> guestCounts = BuildGuestCountsByRoom();
        RoomContentGroup[] rooms = FindObjectsByType<RoomContentGroup>(FindObjectsInactive.Include);

        for (int i = 0; i < rooms.Length; i++)
        {
            RoomContentGroup room = rooms[i];

            if (!IsLoadedSceneObject(room) || string.IsNullOrWhiteSpace(room.RoomName))
            {
                continue;
            }

            string roomId = room.RoomName.Trim();
            int guestCount = guestCounts.TryGetValue(NormalizeRoomName(roomId), out int count) ? count : 0;
            bool complete = selectedButler != null && selectedButler.HasCompleteButlerRoomScaleOverride(roomId);
            rows.Add(new RoomCalibrationCoverageRow(
                roomId,
                complete,
                guestCount,
                GetPossibleCalibrationMatches(roomId)));
        }

        rows.Sort((left, right) => string.Compare(left.RoomId, right.RoomId, StringComparison.OrdinalIgnoreCase));
        return rows;
    }

    private Dictionary<string, int> BuildGuestCountsByRoom()
    {
        Dictionary<string, int> counts = new Dictionary<string, int>();

        foreach (GuestAuditRow row in BuildAuditRows())
        {
            if (string.IsNullOrWhiteSpace(row.RoomId))
            {
                continue;
            }

            string key = NormalizeRoomName(row.RoomId);
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }

        return counts;
    }

    private void PrintMissingGuestRoomCalibrations()
    {
        List<RoomCalibrationCoverageRow> rows = BuildRoomCalibrationCoverageRows();

        for (int i = 0; i < rows.Count; i++)
        {
            RoomCalibrationCoverageRow row = rows[i];

            if (row.HasCompleteCalibration || row.GuestCount <= 0)
            {
                continue;
            }

            Debug.LogWarning(
                $"[GuestButlerScale] Missing Butler calibration for {row.RoomId}. {row.GuestCount} guests in this room cannot use real Butler scale until this room is calibrated or aliased. Possible matches: {row.PossibleMatches}",
                selectedButler);
        }

        lastStatus = "Printed missing guest room calibration report to the Console.";
    }

    private string GetPossibleCalibrationMatches(string roomId)
    {
        if (selectedButler == null || string.IsNullOrWhiteSpace(roomId))
        {
            return "-";
        }

        List<string> calibrationRooms = new List<string>();
        selectedButler.GetButlerScaleOverrideRoomIds(calibrationRooms);
        string normalizedRoom = NormalizeRoomName(roomId);
        List<string> matches = new List<string>();

        for (int i = 0; i < calibrationRooms.Count; i++)
        {
            string calibrationRoom = calibrationRooms[i];
            string normalizedCalibration = NormalizeRoomName(calibrationRoom);

            if (string.IsNullOrWhiteSpace(normalizedCalibration))
            {
                continue;
            }

            if (string.Equals(normalizedCalibration, normalizedRoom, StringComparison.OrdinalIgnoreCase) ||
                normalizedCalibration.Contains(normalizedRoom) ||
                normalizedRoom.Contains(normalizedCalibration))
            {
                matches.Add(calibrationRoom);
            }
        }

        return matches.Count > 0 ? string.Join(", ", matches) : "-";
    }

    private List<GuestAuditRow> BuildAuditRows()
    {
        List<GuestAuditRow> rows = new List<GuestAuditRow>();
        RoomProjectedEntity[] entities = FindObjectsByType<RoomProjectedEntity>(FindObjectsInactive.Include);

        for (int i = 0; i < entities.Length; i++)
        {
            RoomProjectedEntity entity = entities[i];

            if (!IsLoadedSceneObject(entity) || IsButlerObjectOrChild(entity.transform))
            {
                continue;
            }

            rows.Add(BuildProjectedAuditRow(entity));
        }

        RoomPersonWalker2D[] walkers = FindObjectsByType<RoomPersonWalker2D>(FindObjectsInactive.Include);

        for (int i = 0; i < walkers.Length; i++)
        {
            RoomPersonWalker2D walker = walkers[i];

            if (!IsLoadedSceneObject(walker) || IsButlerObjectOrChild(walker.transform))
            {
                continue;
            }

            rows.Add(BuildWalkerAuditRow(walker));
        }

        ActorRoomState[] actors = FindObjectsByType<ActorRoomState>(FindObjectsInactive.Include);

        for (int i = 0; i < actors.Length; i++)
        {
            ActorRoomState actor = actors[i];

            if (!IsLoadedSceneObject(actor) ||
                IsButlerObjectOrChild(actor.transform) ||
                !LooksLikeGuestActor(actor))
            {
                continue;
            }

            rows.Add(BuildActorAuditRow(actor));
        }

        rows.Sort((left, right) => string.Compare(left.ObjectPath, right.ObjectPath, StringComparison.OrdinalIgnoreCase));
        return rows;
    }

    private GuestAuditRow BuildProjectedAuditRow(RoomProjectedEntity entity)
    {
        List<string> overrideRooms = new List<string>();
        entity.GetVisualScaleOverrideRoomIds(overrideRooms);
        bool hasRoomAndFoot = entity.TryResolveGuestRoomAndFootPoint(out string roomId, out Vector2 footPoint);
        VisualAudit visualAudit = BuildVisualAudit(
            entity.GetGuestScaleRoot(),
            roomId,
            footPoint,
            entity.GetGuestRelativeHeightMultiplier(),
            entity.GetGuestPoseHeightMultiplier());
        string warning = string.Empty;

        if (selectedButler == null)
        {
            warning = AppendWarning(warning, "No Butler source");
        }

        if (!hasRoomAndFoot)
        {
            warning = AppendWarning(warning, "RoomProjectedEntity has no room id");
        }

        if (!visualAudit.HasButlerCalibrationForRoom)
        {
            warning = AppendWarning(warning, "No complete Butler calibration for this room");
        }

        if (!visualAudit.ProofCanForceScale)
        {
            warning = AppendWarning(warning, "No visual bounds found");
        }

        if (entity.Mode != RoomProjectedEntity.ProjectionMode.FloorCharacter)
        {
            warning = AppendWarning(warning, "RoomProjectedEntity is not FloorCharacter");
        }

        if (overrideRooms.Count > 0 && !entity.IgnoreRoomVisualScaleOverridesWhenUsingButlerRules)
        {
            warning = AppendWarning(warning, "RoomProjectedEntity has roomVisualScaleOverrides and bypass is off");
        }
        else if (overrideRooms.Count > 0)
        {
            warning = AppendWarning(warning, "Old room visual override exists but final visual fitter will override visible height");
        }

        if (!entity.IgnoreVisualProfileHeightMultiplierWhenUsingButlerRules && entity.VisualProfile != null)
        {
            warning = AppendWarning(warning, "CharacterVisualProfile.HeightScaleMultiplier can still affect Butler-rule projection before the final fitter");
        }

        RoomPersonWalker2D walker = entity.GetComponent<RoomPersonWalker2D>();

        if (walker != null && !entity.IsProjectionActive)
        {
            warning = AppendWarning(warning, "Object has both RoomProjectedEntity and RoomPersonWalker2D but projection not active");
        }

        return new GuestAuditRow(
            GetObjectPath(entity.transform),
            walker != null ? "RoomProjectedEntity + RoomPersonWalker2D" : "RoomProjectedEntity",
            visualAudit.VisibleVisualRoot,
            visualAudit.BoundsRoot,
            entity.GetGuestScaleRoot() != null ? GetObjectPath(entity.GetGuestScaleRoot()) : "-",
            roomId,
            hasRoomAndFoot ? footPoint.y.ToString("0.###") : "-",
            entity.IsGuestSeated(),
            entity.IsUsingButlerCharacterScaleRules,
            entity.ButlerScaleSource != null || selectedButler != null,
            visualAudit.HasButlerCalibrationForRoom,
            visualAudit.NormalizedButlerScale,
            visualAudit.CurrentVisualHeightPx,
            visualAudit.TargetVisualHeightPx,
            visualAudit.LastFitResult,
            entity.GetGuestScaleRoot() != null ? entity.GetGuestScaleRoot().localScale.ToString() : entity.transform.localScale.ToString(),
            visualAudit.ProofCanForceScale,
            overrideRooms.Count > 0 && entity.UsesRoomVisualScaleOverrides,
            BuildProjectedScaleWriterSummary(entity),
            warning);
    }

    private GuestAuditRow BuildWalkerAuditRow(RoomPersonWalker2D walker)
    {
        bool hasRoomAndFoot = walker.TryResolveGuestRoomAndFootPoint(out string roomId, out Vector2 footPoint);
        VisualAudit visualAudit = BuildVisualAudit(
            walker.GetGuestScaleRoot(),
            roomId,
            footPoint,
            walker.GetGuestRelativeHeightMultiplier(),
            walker.GetGuestPoseHeightMultiplier());
        string warning = string.Empty;

        if (selectedButler == null)
        {
            warning = AppendWarning(warning, "No Butler source");
        }

        if (!hasRoomAndFoot)
        {
            warning = AppendWarning(warning, "RoomPersonWalker2D has no room id");
        }

        if (!visualAudit.HasButlerCalibrationForRoom)
        {
            warning = AppendWarning(warning, "No complete Butler calibration for this room");
        }

        if (!visualAudit.ProofCanForceScale)
        {
            warning = AppendWarning(warning, "No visual bounds found");
        }

        RoomProjectedEntity projection = walker.GetComponent<RoomProjectedEntity>();

        if (projection != null && !projection.IsProjectionActive)
        {
            warning = AppendWarning(warning, "Object has both RoomProjectedEntity and RoomPersonWalker2D but projection not active");
        }

        return new GuestAuditRow(
            GetObjectPath(walker.transform),
            projection != null ? "RoomProjectedEntity + RoomPersonWalker2D" : "RoomPersonWalker2D",
            visualAudit.VisibleVisualRoot,
            visualAudit.BoundsRoot,
            walker.GetGuestScaleRoot() != null ? GetObjectPath(walker.GetGuestScaleRoot()) : "-",
            roomId,
            hasRoomAndFoot ? footPoint.y.ToString("0.###") : "-",
            walker.IsGuestSeated(),
            walker.IsUsingButlerCharacterScaleRules,
            walker.ButlerScaleSource != null || selectedButler != null,
            visualAudit.HasButlerCalibrationForRoom,
            visualAudit.NormalizedButlerScale,
            visualAudit.CurrentVisualHeightPx,
            visualAudit.TargetVisualHeightPx,
            visualAudit.LastFitResult,
            walker.GetGuestScaleRoot() != null ? walker.GetGuestScaleRoot().localScale.ToString() : walker.transform.localScale.ToString(),
            visualAudit.ProofCanForceScale,
            false,
            BuildWalkerScaleWriterSummary(walker),
            warning);
    }

    private GuestAuditRow BuildActorAuditRow(ActorRoomState actor)
    {
        bool hasRoomAndFoot = actor.TryResolveGuestRoomAndFootPoint(out string roomId, out Vector2 footPoint);
        VisualAudit visualAudit = BuildVisualAudit(
            actor.GetGuestScaleRoot(),
            roomId,
            footPoint,
            actor.GetGuestRelativeHeightMultiplier(),
            actor.GetGuestPoseHeightMultiplier());
        string warning = string.Empty;

        if (selectedButler == null)
        {
            warning = AppendWarning(warning, "No Butler source");
        }

        if (!hasRoomAndFoot)
        {
            warning = AppendWarning(warning, "ActorRoomState has no current room id");
        }

        if (!visualAudit.HasButlerCalibrationForRoom)
        {
            warning = AppendWarning(warning, "No complete Butler calibration or room-local foot point for this actor");
        }

        if (!visualAudit.ProofCanForceScale)
        {
            warning = AppendWarning(warning, "No visual bounds found");
        }

        RoomProjectedEntity projection = actor.Projection;

        if (projection != null && projection.IsProjectionActive)
        {
            warning = AppendWarning(warning, "Actor has active RoomProjectedEntity; projection should own scale");
        }

        return new GuestAuditRow(
            GetObjectPath(actor.transform),
            "ActorRoomState",
            visualAudit.VisibleVisualRoot,
            visualAudit.BoundsRoot,
            actor.GetGuestScaleRoot() != null ? GetObjectPath(actor.GetGuestScaleRoot()) : "-",
            roomId,
            hasRoomAndFoot ? footPoint.y.ToString("0.###") : "-",
            actor.IsSeated,
            actor.IsUsingButlerCharacterScaleRules,
            selectedButler != null,
            visualAudit.HasButlerCalibrationForRoom,
            visualAudit.NormalizedButlerScale,
            visualAudit.CurrentVisualHeightPx,
            visualAudit.TargetVisualHeightPx,
            visualAudit.LastFitResult,
            actor.GetGuestScaleRoot() != null ? actor.GetGuestScaleRoot().localScale.ToString() : actor.transform.localScale.ToString(),
            visualAudit.ProofCanForceScale,
            false,
            BuildActorScaleWriterSummary(actor),
            warning);
    }

    private VisualAudit BuildVisualAudit(
        Transform root,
        string roomId,
        Vector2 roomLocalFootPoint,
        float relativeHeightMultiplier,
        float poseHeightMultiplier)
    {
        Camera camera = ResolveCamera();
        float currentHeight = 0f;
        string visibleVisualRoot = "-";
        string boundsRoot = "-";
        string visualDiagnostic = string.Empty;
        CharacterVisualBoundsUtility.CharacterVisualTarget visualTarget = default;
        bool hasCurrentHeight = camera != null &&
            root != null &&
            CharacterVisualBoundsUtility.TryResolveCharacterVisualTarget(
                root,
                camera,
                out visualTarget,
                includeInactive: true);

        if (hasCurrentHeight)
        {
            currentHeight = visualTarget.ScreenHeight;
            visibleVisualRoot = visualTarget.PrimaryVisual != null ? GetObjectPath(visualTarget.PrimaryVisual) : "-";
            boundsRoot = visualTarget.BoundsRoot != null ? GetObjectPath(visualTarget.BoundsRoot) : "-";
            visualDiagnostic = visualTarget.Diagnostic;
        }

        PointClickPlayerMovement.ButlerCharacterScaleSample sample = default;
        bool hasSample = selectedButler != null &&
            !string.IsNullOrWhiteSpace(roomId) &&
            selectedButler.TryEvaluateButlerCharacterScale(
                roomId,
                roomLocalFootPoint,
                out sample);
        float butlerReferenceHeight = 0f;
        bool hasReferenceHeight = selectedButler != null &&
            camera != null &&
            selectedButler.TryGetButlerHumanScaleReference(camera, out butlerReferenceHeight, out _);
        bool hasTargetHeight = hasSample && hasReferenceHeight;
        float targetHeight = hasTargetHeight
            ? butlerReferenceHeight *
                Mathf.Max(0.001f, sample.NormalizedScale) *
                Mathf.Clamp(relativeHeightMultiplier, 0.5f, 1.5f) *
                Mathf.Clamp(poseHeightMultiplier, 0.25f, 1.25f)
            : 0f;
        string lastFitResult = hasTargetHeight
            ? string.IsNullOrWhiteSpace(visualDiagnostic) ? "Ready" : visualDiagnostic
            : hasSample
                ? "No Butler reference visual height"
                : "No complete Butler calibration for this room";

        if (!hasCurrentHeight)
        {
            lastFitResult = "No visual bounds found";
        }

        return new VisualAudit(
            hasSample,
            hasSample ? sample.NormalizedScale.ToString("0.###") : "-",
            hasCurrentHeight ? currentHeight.ToString("0.#") : "-",
            hasTargetHeight ? targetHeight.ToString("0.#") : "-",
            lastFitResult,
            hasCurrentHeight,
            visibleVisualRoot,
            boundsRoot);
    }

    private Camera ResolveCamera()
    {
        if (Camera.main != null)
        {
            return Camera.main;
        }

        return FindAnyObjectByType<Camera>(FindObjectsInactive.Exclude);
    }

    private static string BuildProjectedScaleWriterSummary(RoomProjectedEntity entity)
    {
        if (entity == null)
        {
            return "-";
        }

        List<string> writers = new List<string>();
        writers.Add($"RoomProjectedEntity applyScale={YesNo(GetSerializedBool(entity, "applyScale"))}");
        writers.Add($"useRoomVisualScaleOverrides={YesNo(entity.UsesRoomVisualScaleOverrides)}");
        writers.Add($"roomVisualScaleOverrides={GetSerializedArraySize(entity, "roomVisualScaleOverrides")}");
        writers.Add($"ignoreRoomOverridesWhenButler={YesNo(entity.IgnoreRoomVisualScaleOverridesWhenUsingButlerRules)}");
        writers.Add($"ignoreProfileHeightWhenButler={YesNo(entity.IgnoreVisualProfileHeightMultiplierWhenUsingButlerRules)}");

        if (entity.VisualProfile != null)
        {
            writers.Add($"CharacterVisualProfile.HeightScaleMultiplier={entity.VisualProfile.HeightScaleMultiplier:0.###}");
            writers.Add($"standingVisualHeight={entity.VisualProfile.StandingVisualHeight:0.#}");
            writers.Add($"sittingVisualHeight={entity.VisualProfile.SittingVisualHeight:0.#}");
        }

        string animatorCurves = FindAnimatorScaleCurveSummary(entity);

        if (!string.IsNullOrWhiteSpace(animatorCurves))
        {
            writers.Add(animatorCurves);
        }

        return string.Join("; ", writers);
    }

    private static string BuildWalkerScaleWriterSummary(RoomPersonWalker2D walker)
    {
        if (walker == null)
        {
            return "-";
        }

        List<string> writers = new List<string>();
        writers.Add($"RoomPersonWalker2D nearScale={GetSerializedFloat(walker, "nearScale"):0.###}");
        writers.Add($"farScale={GetSerializedFloat(walker, "farScale"):0.###}");
        writers.Add($"useRoomPerspectiveProfileScale={YesNo(GetSerializedBool(walker, "useRoomPerspectiveProfileScale"))}");
        writers.Add($"useButlerCharacterScaleRules={YesNo(walker.UseButlerCharacterScaleRules)}");
        writers.Add($"targetGraphic={FormatObjectName(walker.TargetGraphic)}");

        string animatorCurves = FindAnimatorScaleCurveSummary(walker);

        if (!string.IsNullOrWhiteSpace(animatorCurves))
        {
            writers.Add(animatorCurves);
        }

        return string.Join("; ", writers);
    }

    private static string BuildActorScaleWriterSummary(ActorRoomState actor)
    {
        if (actor == null)
        {
            return "-";
        }

        List<string> writers = new List<string>();
        writers.Add($"ActorRoomState scaleWithRoomStageMotion={YesNo(actor.ScaleWithRoomStageMotion)}");

        string animatorCurves = FindAnimatorScaleCurveSummary(actor);

        if (!string.IsNullOrWhiteSpace(animatorCurves))
        {
            writers.Add(animatorCurves);
        }

        return string.Join("; ", writers);
    }

    private int ClampBadGuestScales(RoomProjectedEntity[] entities, HashSet<Transform> visited)
    {
        int corrected = 0;

        for (int i = 0; i < entities.Length; i++)
        {
            RoomProjectedEntity entity = entities[i];

            if (!IsLoadedSceneObject(entity) || IsButlerObjectOrChild(entity.transform))
            {
                continue;
            }

            corrected += ClampBadScaleRoot(entity.GetGuestScaleRoot(), $"RoomProjectedEntity {GetObjectPath(entity.transform)}", visited);
        }

        return corrected;
    }

    private int ClampBadGuestScales(RoomPersonWalker2D[] walkers, HashSet<Transform> visited)
    {
        int corrected = 0;

        for (int i = 0; i < walkers.Length; i++)
        {
            RoomPersonWalker2D walker = walkers[i];

            if (!IsLoadedSceneObject(walker) || IsButlerObjectOrChild(walker.transform))
            {
                continue;
            }

            corrected += ClampBadScaleRoot(walker.GetGuestScaleRoot(), $"RoomPersonWalker2D {GetObjectPath(walker.transform)}", visited);
        }

        return corrected;
    }

    private int ClampBadGuestScales(ActorRoomState[] actors, HashSet<Transform> visited)
    {
        int corrected = 0;

        for (int i = 0; i < actors.Length; i++)
        {
            ActorRoomState actor = actors[i];

            if (!IsLoadedSceneObject(actor) ||
                IsButlerObjectOrChild(actor.transform) ||
                !LooksLikeGuestActor(actor))
            {
                continue;
            }

            corrected += ClampBadScaleRoot(actor.GetGuestScaleRoot(), $"ActorRoomState {GetObjectPath(actor.transform)}", visited);
        }

        return corrected;
    }

    private int ClampBadScaleRoot(Transform scaleRoot, string label, HashSet<Transform> visited)
    {
        if (scaleRoot == null || visited.Contains(scaleRoot))
        {
            return 0;
        }

        visited.Add(scaleRoot);
        Vector3 scale = scaleRoot.localScale;
        bool bad =
            Mathf.Abs(scale.x) > 20f ||
            Mathf.Abs(scale.y) > 20f ||
            Mathf.Abs(scale.x) < 0.01f ||
            Mathf.Abs(scale.y) < 0.01f;

        if (!bad)
        {
            return 0;
        }

        Undo.RecordObject(scaleRoot, "Clamp Bad Guest Scale");
        scaleRoot.localScale = new Vector3(1f, 1f, scale.z);
        EditorUtility.SetDirty(scaleRoot);

        if (scaleRoot.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scaleRoot.gameObject.scene);
        }

        Debug.LogWarning($"[GuestButlerScale Emergency] Reset bad scale on {label}: {scale} -> {scaleRoot.localScale}", scaleRoot);
        return 1;
    }

    private static bool GetSerializedBool(UnityEngine.Object target, string propertyName)
    {
        SerializedProperty property = new SerializedObject(target).FindProperty(propertyName);
        return property != null && property.propertyType == SerializedPropertyType.Boolean && property.boolValue;
    }

    private static float GetSerializedFloat(UnityEngine.Object target, string propertyName)
    {
        SerializedProperty property = new SerializedObject(target).FindProperty(propertyName);
        return property != null && property.propertyType == SerializedPropertyType.Float ? property.floatValue : 0f;
    }

    private static int GetSerializedArraySize(UnityEngine.Object target, string propertyName)
    {
        SerializedProperty property = new SerializedObject(target).FindProperty(propertyName);
        return property != null && property.isArray ? property.arraySize : 0;
    }

    private static string FindAnimatorScaleCurveSummary(Component component)
    {
        if (component == null)
        {
            return string.Empty;
        }

        Animator[] animators = component.GetComponentsInChildren<Animator>(true);
        int curveCount = 0;

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                continue;
            }

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;

            for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
            {
                AnimationClip clip = clips[clipIndex];

                if (clip == null)
                {
                    continue;
                }

                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);

                for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
                {
                    if (bindings[bindingIndex].propertyName.IndexOf("m_LocalScale", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        curveCount++;
                    }
                }
            }
        }

        return curveCount > 0 ? $"Animator localScale curves={curveCount}" : "Animator localScale curves=none";
    }

    private static string FormatObjectName(UnityEngine.Object target)
    {
        return target != null ? target.name : "-";
    }

    private string ResolveProjectedRoomId(RoomProjectedEntity entity)
    {
        ActorRoomState actorState = entity.GetComponentInParent<ActorRoomState>(true);

        if (actorState != null && !string.IsNullOrWhiteSpace(actorState.CurrentRoomId))
        {
            return actorState.CurrentRoomId;
        }

        if (entity.RoomProfile != null && !string.IsNullOrWhiteSpace(entity.RoomProfile.RoomId))
        {
            return entity.RoomProfile.RoomId;
        }

        RoomContentGroup room = entity.GetComponentInParent<RoomContentGroup>(true);
        return room != null ? room.RoomName : entity.EditorSelectedVisualScaleRoomId;
    }

    private string ResolveWalkerRoomId(RoomPersonWalker2D walker)
    {
        RoomContentGroup room = walker.GetComponentInParent<RoomContentGroup>(true);

        if (room != null && !string.IsNullOrWhiteSpace(room.RoomName))
        {
            return room.RoomName;
        }

        if (walker.RoomProfile != null && !string.IsNullOrWhiteSpace(walker.RoomProfile.RoomId))
        {
            return walker.RoomProfile.RoomId;
        }

        ActorRoomState actorState = walker.GetComponentInParent<ActorRoomState>(true);
        return actorState != null ? actorState.CurrentRoomId : string.Empty;
    }

    private PointClickPlayerMovement FindSceneButler()
    {
        PointClickPlayerMovement tagged = null;
        PointClickPlayerMovement named = null;
        PointClickPlayerMovement first = null;
        PointClickPlayerMovement[] candidates = FindObjectsByType<PointClickPlayerMovement>(FindObjectsInactive.Include);

        for (int i = 0; i < candidates.Length; i++)
        {
            PointClickPlayerMovement candidate = candidates[i];

            if (!IsLoadedSceneObject(candidate))
            {
                continue;
            }

            first ??= candidate;

            if (string.Equals(candidate.gameObject.tag, "Player", StringComparison.OrdinalIgnoreCase))
            {
                tagged ??= candidate;
            }

            if (NameLooksLikePlayerOrButler(candidate.name) ||
                NameLooksLikePlayerOrButler(candidate.gameObject.name))
            {
                named ??= candidate;
            }
        }

        return tagged != null ? tagged : named != null ? named : first;
    }

    private bool IsButlerObjectOrChild(Transform target)
    {
        return target != null &&
            selectedButler != null &&
            selectedButler.transform != null &&
            (target == selectedButler.transform || target.IsChildOf(selectedButler.transform));
    }

    private static bool IsLoadedSceneObject(Component component)
    {
        return component != null &&
            component.gameObject != null &&
            component.gameObject.scene.IsValid() &&
            component.gameObject.scene.isLoaded;
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

    private void MarkSceneDirtyFromButler()
    {
        if (selectedButler != null && selectedButler.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(selectedButler.gameObject.scene);
        }
    }

    private static string AppendWarning(string current, string warning)
    {
        return string.IsNullOrWhiteSpace(current) ? warning : $"{current}; {warning}";
    }

    private static bool Approximately(Vector3 left, Vector3 right)
    {
        return Mathf.Approximately(left.x, right.x) &&
            Mathf.Approximately(left.y, right.y) &&
            Mathf.Approximately(left.z, right.z);
    }

    private static string GetObjectPath(Transform target)
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

        return path;
    }

    private static bool NameLooksLikePlayerOrButler(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            (value.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("Butler", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool LooksLikeGuestActor(ActorRoomState actor)
    {
        return actor != null &&
            (ContainsGuest(actor.ActorId) ||
            ContainsGuest(actor.name) ||
            ContainsGuest(actor.gameObject.name));
    }

    private static bool ContainsGuest(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.IndexOf("Guest", StringComparison.OrdinalIgnoreCase) >= 0;
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
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static string YesNo(bool value)
    {
        return value ? "yes" : "no";
    }

    private readonly struct GuestAuditRow
    {
        public GuestAuditRow(
            string objectPath,
            string controllerType,
            string visibleVisualRoot,
            string boundsRoot,
            string scaleRoot,
            string roomId,
            string roomLocalFootY,
            bool isSeated,
            bool usingButlerRules,
            bool butlerScaleSourceFound,
            bool hasButlerCalibrationForRoom,
            string normalizedButlerScale,
            string currentVisualHeightPx,
            string targetVisualHeightPx,
            string lastFitResult,
            string finalLocalScale,
            bool proofCanForceScale,
            bool hiddenOldOverrideActive,
            string activeScaleWriters,
            string warning)
        {
            ObjectPath = objectPath;
            ControllerType = controllerType;
            VisibleVisualRoot = visibleVisualRoot;
            BoundsRoot = boundsRoot;
            ScaleRoot = scaleRoot;
            RoomId = roomId;
            RoomLocalFootY = roomLocalFootY;
            IsSeated = isSeated;
            UsingButlerRules = usingButlerRules;
            ButlerScaleSourceFound = butlerScaleSourceFound;
            HasButlerCalibrationForRoom = hasButlerCalibrationForRoom;
            NormalizedButlerScale = normalizedButlerScale;
            CurrentVisualHeightPx = currentVisualHeightPx;
            TargetVisualHeightPx = targetVisualHeightPx;
            LastFitResult = lastFitResult;
            FinalLocalScale = finalLocalScale;
            ProofCanForceScale = proofCanForceScale;
            HiddenOldOverrideActive = hiddenOldOverrideActive;
            ActiveScaleWriters = activeScaleWriters;
            Warning = warning;
        }

        public string ObjectPath { get; }
        public string ControllerType { get; }
        public string VisibleVisualRoot { get; }
        public string BoundsRoot { get; }
        public string ScaleRoot { get; }
        public string RoomId { get; }
        public string RoomLocalFootY { get; }
        public bool IsSeated { get; }
        public bool UsingButlerRules { get; }
        public bool ButlerScaleSourceFound { get; }
        public bool HasButlerCalibrationForRoom { get; }
        public string NormalizedButlerScale { get; }
        public string CurrentVisualHeightPx { get; }
        public string TargetVisualHeightPx { get; }
        public string LastFitResult { get; }
        public string FinalLocalScale { get; }
        public bool ProofCanForceScale { get; }
        public bool HiddenOldOverrideActive { get; }
        public string ActiveScaleWriters { get; }
        public string Warning { get; }
    }

    private readonly struct VisualAudit
    {
        public VisualAudit(
            bool hasButlerCalibrationForRoom,
            string normalizedButlerScale,
            string currentVisualHeightPx,
            string targetVisualHeightPx,
            string lastFitResult,
            bool proofCanForceScale,
            string visibleVisualRoot,
            string boundsRoot)
        {
            HasButlerCalibrationForRoom = hasButlerCalibrationForRoom;
            NormalizedButlerScale = normalizedButlerScale;
            CurrentVisualHeightPx = currentVisualHeightPx;
            TargetVisualHeightPx = targetVisualHeightPx;
            LastFitResult = lastFitResult;
            ProofCanForceScale = proofCanForceScale;
            VisibleVisualRoot = visibleVisualRoot;
            BoundsRoot = boundsRoot;
        }

        public bool HasButlerCalibrationForRoom { get; }
        public string NormalizedButlerScale { get; }
        public string CurrentVisualHeightPx { get; }
        public string TargetVisualHeightPx { get; }
        public string LastFitResult { get; }
        public bool ProofCanForceScale { get; }
        public string VisibleVisualRoot { get; }
        public string BoundsRoot { get; }
    }

    private readonly struct RoomCalibrationCoverageRow
    {
        public RoomCalibrationCoverageRow(
            string roomId,
            bool hasCompleteCalibration,
            int guestCount,
            string possibleMatches)
        {
            RoomId = roomId;
            HasCompleteCalibration = hasCompleteCalibration;
            GuestCount = guestCount;
            PossibleMatches = possibleMatches;
        }

        public string RoomId { get; }
        public bool HasCompleteCalibration { get; }
        public int GuestCount { get; }
        public string PossibleMatches { get; }
    }
}
