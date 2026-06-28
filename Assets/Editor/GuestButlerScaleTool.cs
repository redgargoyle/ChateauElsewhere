using System;
using System.Collections.Generic;
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
        using (new EditorGUI.DisabledScope(selectedButler == null))
        {
            if (GUILayout.Button("Enable Butler Scaling On All Guests"))
            {
                EnableButlerScalingOnAllGuests();
            }

            if (GUILayout.Button("Bypass Old Room Visual Scale Overrides For All Guests"))
            {
                int changed = BypassOldRoomVisualScaleOverrides();
                lastStatus = $"Bypassed old room visual scale overrides on {changed} projected guest(s).";
            }

            if (GUILayout.Button("Refresh Guest Scaling Now"))
            {
                GuestButlerScaleHarmonizer harmonizer = EnsureHarmonizer();
                GuestButlerScaleHarmonizer.GuestScaleApplySummary summary = harmonizer != null
                    ? harmonizer.RefreshNow()
                    : GuestButlerScaleHarmonizer.GuestScaleApplySummary.Empty;
                lastStatus = $"Refreshed guest scaling. Scaled {summary.Scaled}, missing calibration {summary.MissingCalibration}, skipped {summary.Skipped}.";
            }
        }

        if (GUILayout.Button("Save Scene"))
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
            if (GUILayout.Button("PROOF: Make All Guest Butler Scales 50% For 2 Seconds"))
            {
                RunProof(0.5f);
            }

            if (GUILayout.Button("PROOF: Make All Guest Butler Scales 150% For 2 Seconds"))
            {
                RunProof(1.5f);
            }

            if (GUILayout.Button("Restore Real Butler Scaling"))
            {
                RestoreProof();
            }
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
            EditorGUILayout.LabelField("Room Id", row.RoomId);
            EditorGUILayout.LabelField("Room-Local Foot Y", row.RoomLocalFootY);
            EditorGUILayout.LabelField("Using Butler Rules?", YesNo(row.UsingButlerRules));
            EditorGUILayout.LabelField("Butler Scale Source Found?", YesNo(row.ButlerScaleSourceFound));
            EditorGUILayout.LabelField("Normalized Butler Scale", row.NormalizedButlerScale);
            EditorGUILayout.LabelField("Final Current localScale", row.FinalLocalScale);
            EditorGUILayout.LabelField("Hidden Old Room Visual Override Active?", YesNo(row.HiddenOldOverrideActive));

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

        Dictionary<Component, Vector3> before = CaptureProofGuestScales();
        harmonizer.SetDebugGuestScaleMultiplier(multiplier);
        GuestButlerScaleHarmonizer.GuestScaleApplySummary summary = harmonizer.RefreshNow();
        LogUnchangedProofGuests(before, multiplier);
        activeProofHarmonizer = harmonizer;
        proofRestoreAt = EditorApplication.timeSinceStartup + 2.0;
        EditorApplication.update -= ProofUpdate;
        EditorApplication.update += ProofUpdate;
        lastStatus = $"Proof multiplier {multiplier:0.##} applied to {summary.Scaled} guest scale writer(s).";
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

    private Dictionary<Component, Vector3> CaptureProofGuestScales()
    {
        Dictionary<Component, Vector3> result = new Dictionary<Component, Vector3>();

        RoomProjectedEntity[] entities = FindObjectsByType<RoomProjectedEntity>(FindObjectsInactive.Include);
        for (int i = 0; i < entities.Length; i++)
        {
            RoomProjectedEntity entity = entities[i];

            if (IsLoadedSceneObject(entity) &&
                entity.Mode == RoomProjectedEntity.ProjectionMode.FloorCharacter &&
                !IsButlerObjectOrChild(entity.transform) &&
                entity.TryGetButlerCharacterScaleSample(out _))
            {
                result[entity] = entity.VisualRoot != null ? entity.VisualRoot.localScale : entity.transform.localScale;
            }
        }

        RoomPersonWalker2D[] walkers = FindObjectsByType<RoomPersonWalker2D>(FindObjectsInactive.Include);
        for (int i = 0; i < walkers.Length; i++)
        {
            RoomPersonWalker2D walker = walkers[i];

            if (IsLoadedSceneObject(walker) &&
                !IsButlerObjectOrChild(walker.transform) &&
                walker.TryGetButlerCharacterScaleSample(out _))
            {
                result[walker] = walker.transform.localScale;
            }
        }

        ActorRoomState[] actors = FindObjectsByType<ActorRoomState>(FindObjectsInactive.Include);
        for (int i = 0; i < actors.Length; i++)
        {
            ActorRoomState actor = actors[i];

            if (IsLoadedSceneObject(actor) &&
                !IsButlerObjectOrChild(actor.transform) &&
                LooksLikeGuestActor(actor) &&
                actor.TryGetButlerCharacterScaleSample(selectedButler, out _))
            {
                result[actor] = actor.transform.localScale;
            }
        }

        return result;
    }

    private void LogUnchangedProofGuests(Dictionary<Component, Vector3> before, float multiplier)
    {
        List<string> unchanged = new List<string>();

        foreach (KeyValuePair<Component, Vector3> pair in before)
        {
            Component component = pair.Key;

            if (component == null)
            {
                continue;
            }

            Vector3 currentScale = component is RoomProjectedEntity entity && entity.VisualRoot != null
                ? entity.VisualRoot.localScale
                : component.transform.localScale;

            if (Approximately(pair.Value, currentScale))
            {
                unchanged.Add(GetObjectPath(component.transform));
            }
        }

        if (unchanged.Count <= 0)
        {
            return;
        }

        Debug.LogError(
            $"[GuestButlerScale] Proof multiplier {multiplier:0.##} did not change {unchanged.Count} guest scale(s): {string.Join(", ", unchanged)}",
            selectedButler);
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
        bool hasSample = entity.TryGetButlerCharacterScaleSample(out PointClickPlayerMovement.ButlerCharacterScaleSample sample);
        string warning = string.Empty;

        if (selectedButler == null)
        {
            warning = AppendWarning(warning, "No Butler source");
        }

        if (!hasSample)
        {
            warning = AppendWarning(warning, "No complete Butler calibration for this room");
        }

        if (entity.Mode != RoomProjectedEntity.ProjectionMode.FloorCharacter)
        {
            warning = AppendWarning(warning, "RoomProjectedEntity is not FloorCharacter");
        }

        if (overrideRooms.Count > 0 && !entity.IgnoreRoomVisualScaleOverridesWhenUsingButlerRules)
        {
            warning = AppendWarning(warning, "RoomProjectedEntity has roomVisualScaleOverrides and bypass is off");
        }

        RoomPersonWalker2D walker = entity.GetComponent<RoomPersonWalker2D>();

        if (walker != null && !entity.IsProjectionActive)
        {
            warning = AppendWarning(warning, "Object has both RoomProjectedEntity and RoomPersonWalker2D but projection not active");
        }

        return new GuestAuditRow(
            GetObjectPath(entity.transform),
            walker != null ? "RoomProjectedEntity + RoomPersonWalker2D" : "RoomProjectedEntity",
            ResolveProjectedRoomId(entity),
            entity.RoomLocalFootPoint.y.ToString("0.###"),
            entity.IsUsingButlerCharacterScaleRules,
            entity.ButlerScaleSource != null || selectedButler != null,
            hasSample ? sample.NormalizedScale.ToString("0.###") : "-",
            entity.VisualRoot != null ? entity.VisualRoot.localScale.ToString() : entity.transform.localScale.ToString(),
            overrideRooms.Count > 0 && entity.UsesRoomVisualScaleOverrides,
            warning);
    }

    private GuestAuditRow BuildWalkerAuditRow(RoomPersonWalker2D walker)
    {
        bool hasSample = walker.TryGetButlerCharacterScaleSample(out PointClickPlayerMovement.ButlerCharacterScaleSample sample);
        string roomId = ResolveWalkerRoomId(walker);
        string warning = string.Empty;

        if (selectedButler == null)
        {
            warning = AppendWarning(warning, "No Butler source");
        }

        if (string.IsNullOrWhiteSpace(roomId))
        {
            warning = AppendWarning(warning, "RoomPersonWalker2D has no room id");
        }

        if (!hasSample)
        {
            warning = AppendWarning(warning, "No complete Butler calibration for this room");
        }

        RoomProjectedEntity projection = walker.GetComponent<RoomProjectedEntity>();

        if (projection != null && !projection.IsProjectionActive)
        {
            warning = AppendWarning(warning, "Object has both RoomProjectedEntity and RoomPersonWalker2D but projection not active");
        }

        return new GuestAuditRow(
            GetObjectPath(walker.transform),
            projection != null ? "RoomProjectedEntity + RoomPersonWalker2D" : "RoomPersonWalker2D",
            roomId,
            walker.CurrentPosition.y.ToString("0.###"),
            walker.IsUsingButlerCharacterScaleRules,
            walker.ButlerScaleSource != null || selectedButler != null,
            hasSample ? sample.NormalizedScale.ToString("0.###") : "-",
            walker.transform.localScale.ToString(),
            false,
            warning);
    }

    private GuestAuditRow BuildActorAuditRow(ActorRoomState actor)
    {
        bool hasSample = actor.TryGetButlerCharacterScaleSample(selectedButler, out PointClickPlayerMovement.ButlerCharacterScaleSample sample);
        string roomId = actor.CurrentRoomId;
        string warning = string.Empty;

        if (selectedButler == null)
        {
            warning = AppendWarning(warning, "No Butler source");
        }

        if (string.IsNullOrWhiteSpace(roomId))
        {
            warning = AppendWarning(warning, "ActorRoomState has no current room id");
        }

        if (!hasSample)
        {
            warning = AppendWarning(warning, "No complete Butler calibration or room-local foot point for this actor");
        }

        RoomProjectedEntity projection = actor.Projection;

        if (projection != null && projection.IsProjectionActive)
        {
            warning = AppendWarning(warning, "Actor has active RoomProjectedEntity; projection should own scale");
        }

        return new GuestAuditRow(
            GetObjectPath(actor.transform),
            "ActorRoomState",
            roomId,
            hasSample ? sample.RoomLocalFootPoint.y.ToString("0.###") : "-",
            actor.IsUsingButlerCharacterScaleRules,
            selectedButler != null,
            hasSample ? sample.NormalizedScale.ToString("0.###") : "-",
            actor.transform.localScale.ToString(),
            false,
            warning);
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

    private static string YesNo(bool value)
    {
        return value ? "yes" : "no";
    }

    private readonly struct GuestAuditRow
    {
        public GuestAuditRow(
            string objectPath,
            string controllerType,
            string roomId,
            string roomLocalFootY,
            bool usingButlerRules,
            bool butlerScaleSourceFound,
            string normalizedButlerScale,
            string finalLocalScale,
            bool hiddenOldOverrideActive,
            string warning)
        {
            ObjectPath = objectPath;
            ControllerType = controllerType;
            RoomId = roomId;
            RoomLocalFootY = roomLocalFootY;
            UsingButlerRules = usingButlerRules;
            ButlerScaleSourceFound = butlerScaleSourceFound;
            NormalizedButlerScale = normalizedButlerScale;
            FinalLocalScale = finalLocalScale;
            HiddenOldOverrideActive = hiddenOldOverrideActive;
            Warning = warning;
        }

        public string ObjectPath { get; }
        public string ControllerType { get; }
        public string RoomId { get; }
        public string RoomLocalFootY { get; }
        public bool UsingButlerRules { get; }
        public bool ButlerScaleSourceFound { get; }
        public string NormalizedButlerScale { get; }
        public string FinalLocalScale { get; }
        public bool HiddenOldOverrideActive { get; }
        public string Warning { get; }
    }
}
