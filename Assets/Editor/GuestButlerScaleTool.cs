using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class GuestButlerScaleTool : EditorWindow
{
    private readonly List<string> roomIds = new List<string>();
    private PointClickPlayerMovement selectedButler;
    private string selectedRoomId = string.Empty;
    private float selectedRoomMultiplier = 1f;
    private string lastStatus = string.Empty;

    [MenuItem("Tools/Characters/Guest Room Scale")]
    public static void Open()
    {
        GetWindow<GuestButlerScaleTool>("Guest Room Scale");
    }

    private void OnEnable()
    {
        selectedButler = FindSceneButler();
        RefreshRoomSelection();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Guest Room Scale", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Default: guests use the Butler's saved room scaling. Adjust one room multiplier only when the guest art needs polishing.",
            MessageType.Info);

        DrawButlerControls();
        DrawRoomControls();
        DrawTestControls();

        if (!string.IsNullOrWhiteSpace(lastStatus))
        {
            EditorGUILayout.HelpBox(lastStatus, MessageType.None);
        }
    }

    private void DrawButlerControls()
    {
        EditorGUI.BeginChangeCheck();
        selectedButler = (PointClickPlayerMovement)EditorGUILayout.ObjectField(
            "Butler / Player",
            selectedButler,
            typeof(PointClickPlayerMovement),
            true);

        if (EditorGUI.EndChangeCheck())
        {
            RefreshRoomSelection();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Find Scene Player"))
            {
                selectedButler = FindSceneButler();
                RefreshRoomSelection();
                lastStatus = selectedButler != null ? $"Found {selectedButler.name}." : "No safe scene player found.";
            }

            using (new EditorGUI.DisabledScope(selectedButler == null))
            {
                if (GUILayout.Button("Auto Setup + Apply Now"))
                {
                    AutoSetupAndApply();
                }

                if (GUILayout.Button("Save Scene"))
                {
                    EditorSceneManager.SaveOpenScenes();
                    lastStatus = "Saved open scene(s).";
                }
            }
        }
    }

    private void DrawRoomControls()
    {
        using (new EditorGUI.DisabledScope(selectedButler == null))
        {
            RefreshRoomList();

            if (roomIds.Count <= 0)
            {
                EditorGUILayout.HelpBox("No room ids found. Select the scene player first.", MessageType.Warning);
                return;
            }

            int currentIndex = Mathf.Max(0, roomIds.FindIndex(room => SameRoom(room, selectedRoomId)));
            int nextIndex = EditorGUILayout.Popup("Room", currentIndex, roomIds.ToArray());

            if (nextIndex != currentIndex || !SameRoom(selectedRoomId, roomIds[nextIndex]))
            {
                selectedRoomId = roomIds[nextIndex];
                LoadSelectedRoomMultiplier();
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Guest Size In This Room", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                selectedRoomMultiplier = EditorGUILayout.Slider(selectedRoomMultiplier, 0.25f, 2.5f);
                selectedRoomMultiplier = Mathf.Max(0.001f, EditorGUILayout.FloatField(selectedRoomMultiplier, GUILayout.Width(70f)));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("-0.05", GUILayout.Width(70f)))
                {
                    selectedRoomMultiplier = Mathf.Max(0.001f, selectedRoomMultiplier - 0.05f);
                    GUI.changed = true;
                }

                if (GUILayout.Button("+0.05", GUILayout.Width(70f)))
                {
                    selectedRoomMultiplier += 0.05f;
                    GUI.changed = true;
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"Current multiplier: {GetHarmonizerMultiplier():0.###}", GUILayout.Width(170f));
            }

            if (EditorGUI.EndChangeCheck())
            {
                SaveSelectedRoomMultiplier(false);
                ApplyNow("Previewed");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save Guest Room Scale"))
                {
                    SaveSelectedRoomMultiplier(true);
                }

                if (GUILayout.Button("Reset Room To Butler Size"))
                {
                    selectedRoomMultiplier = 1f;
                    SaveSelectedRoomMultiplier(true);
                    ApplyNow("Reset");
                }

                if (GUILayout.Button("Apply To All Active Guests Now"))
                {
                    ApplyNow("Applied");
                }
            }
        }
    }

    private void DrawTestControls()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Quick Test", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(selectedButler == null))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Test 50%"))
            {
                RunProof(0.5f);
            }

            if (GUILayout.Button("Test 150%"))
            {
                RunProof(1.5f);
            }

            if (GUILayout.Button("Restore Real Butler Scaling"))
            {
                RestoreProof();
            }
        }
    }

    private void AutoSetupAndApply()
    {
        GuestButlerScaleHarmonizer harmonizer = EnsureHarmonizer();

        if (harmonizer == null)
        {
            lastStatus = "Could not create guest scale harmonizer.";
            return;
        }

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

            Undo.RecordObject(entity, "Auto Setup Guest Scale");
            entity.SetButlerCharacterScaleRulesEnabled(true, false);
            entity.SetButlerScaleSource(selectedButler, false);
            entity.SetIgnoreRoomVisualScaleOverridesWhenUsingButlerRules(true, false);
            EditorUtility.SetDirty(entity);
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

            Undo.RecordObject(walker, "Auto Setup Guest Scale");
            walker.SetButlerCharacterScaleRulesEnabled(true, false);
            walker.SetButlerScaleSource(selectedButler, false);
            walker.SetPreserveAuthoredLocalScaleWhenUsingButlerRules(true, false);
            EditorUtility.SetDirty(walker);
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

            Undo.RecordObject(actor, "Auto Setup Guest Scale");
            actor.SetScaleWithRoomStageMotion(true);
            EditorUtility.SetDirty(actor);
            actors++;
        }

        SaveSelectedRoomMultiplier(false);
        GuestButlerScaleHarmonizer.GuestScaleApplySummary summary = harmonizer.RefreshNow();
        MarkButlerSceneDirty();
        lastStatus = $"Auto setup complete. Configured {projected} projected, {walkers} walkers, {actors} actor states. Scaled {summary.Scaled}, missing calibration {summary.MissingCalibration}.";
    }

    private void SaveSelectedRoomMultiplier(bool showStatus)
    {
        GuestButlerScaleHarmonizer harmonizer = EnsureHarmonizer();

        if (harmonizer == null || string.IsNullOrWhiteSpace(selectedRoomId))
        {
            return;
        }

        Undo.RecordObject(harmonizer, "Save Guest Room Scale");
        harmonizer.SetRoomGuestScaleMultiplier(selectedRoomId, selectedRoomMultiplier);
        EditorUtility.SetDirty(harmonizer);
        MarkButlerSceneDirty();

        if (showStatus)
        {
            lastStatus = $"Saved {selectedRoomId} guest size multiplier: {selectedRoomMultiplier:0.###}.";
        }
    }

    private void ApplyNow(string verb)
    {
        GuestButlerScaleHarmonizer harmonizer = EnsureHarmonizer();

        if (harmonizer == null)
        {
            return;
        }

        GuestButlerScaleHarmonizer.GuestScaleApplySummary summary = harmonizer.RefreshNow();
        lastStatus = $"{verb} guest scaling. Scaled {summary.Scaled}, missing calibration {summary.MissingCalibration}, skipped {summary.Skipped}.";
    }

    private void RunProof(float multiplier)
    {
        GuestButlerScaleHarmonizer harmonizer = EnsureHarmonizer();

        if (harmonizer == null)
        {
            return;
        }

        harmonizer.SetDebugGuestScaleMultiplier(multiplier);
        GuestButlerScaleHarmonizer.GuestScaleApplySummary summary = harmonizer.RefreshNow();
        lastStatus = $"Test {multiplier:P0}: scaled {summary.Scaled} guest scale writer(s). Click Restore Real Butler Scaling when done.";
    }

    private void RestoreProof()
    {
        GuestButlerScaleHarmonizer harmonizer = EnsureHarmonizer();

        if (harmonizer == null)
        {
            return;
        }

        harmonizer.RestoreRealButlerScaling();
        lastStatus = "Restored real Butler scaling.";
    }

    private GuestButlerScaleHarmonizer EnsureHarmonizer()
    {
        if (selectedButler == null)
        {
            return null;
        }

        GuestButlerScaleHarmonizer harmonizer = selectedButler.GetComponent<GuestButlerScaleHarmonizer>();

        if (harmonizer == null)
        {
            Undo.RecordObject(selectedButler.gameObject, "Add Guest Scale Harmonizer");
            harmonizer = selectedButler.gameObject.AddComponent<GuestButlerScaleHarmonizer>();
            EditorUtility.SetDirty(selectedButler.gameObject);
            MarkButlerSceneDirty();
        }

        harmonizer.SetButlerScaleSource(selectedButler);
        return harmonizer;
    }

    private void RefreshRoomSelection()
    {
        RefreshRoomList();

        if (string.IsNullOrWhiteSpace(selectedRoomId) && roomIds.Count > 0)
        {
            selectedRoomId = roomIds[0];
        }

        LoadSelectedRoomMultiplier();
    }

    private void RefreshRoomList()
    {
        roomIds.Clear();

        if (selectedButler != null)
        {
            AddRoomId(selectedButler.CurrentButlerScaleRoomId);
            AddRoomId(selectedButler.CurrentRoomPerspectiveProfileRoomId);
            selectedButler.GetButlerScaleOverrideRoomIds(roomIds);

            GuestButlerScaleHarmonizer harmonizer = selectedButler.GetComponent<GuestButlerScaleHarmonizer>();
            if (harmonizer != null)
            {
                harmonizer.GetGuestScaleMultiplierRoomIds(roomIds);
            }
        }

        RoomNavigationManager navigation = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
        if (navigation != null)
        {
            AddRoomId(navigation.CurrentRoom);
        }

        RoomContentGroup[] roomGroups = Resources.FindObjectsOfTypeAll<RoomContentGroup>();
        for (int i = 0; i < roomGroups.Length; i++)
        {
            RoomContentGroup group = roomGroups[i];

            if (group != null &&
                group.gameObject != null &&
                group.gameObject.scene.IsValid() &&
                !string.IsNullOrWhiteSpace(group.RoomName))
            {
                AddRoomId(group.RoomName);
            }
        }

        roomIds.Sort(StringComparer.OrdinalIgnoreCase);
    }

    private void AddRoomId(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId) || ContainsRoomId(roomIds, roomId))
        {
            return;
        }

        roomIds.Add(roomId.Trim());
    }

    private void LoadSelectedRoomMultiplier()
    {
        GuestButlerScaleHarmonizer harmonizer = selectedButler != null
            ? selectedButler.GetComponent<GuestButlerScaleHarmonizer>()
            : null;

        selectedRoomMultiplier = harmonizer != null && !string.IsNullOrWhiteSpace(selectedRoomId)
            ? harmonizer.GetRoomGuestScaleMultiplier(selectedRoomId)
            : 1f;
    }

    private float GetHarmonizerMultiplier()
    {
        GuestButlerScaleHarmonizer harmonizer = selectedButler != null
            ? selectedButler.GetComponent<GuestButlerScaleHarmonizer>()
            : null;

        return harmonizer != null && !string.IsNullOrWhiteSpace(selectedRoomId)
            ? harmonizer.GetRoomGuestScaleMultiplier(selectedRoomId)
            : 1f;
    }

    private PointClickPlayerMovement FindSceneButler()
    {
        PointClickPlayerMovement tagged = null;
        PointClickPlayerMovement named = null;
        PointClickPlayerMovement firstSafe = null;
        PointClickPlayerMovement[] candidates = FindObjectsByType<PointClickPlayerMovement>(FindObjectsInactive.Include);

        for (int i = 0; i < candidates.Length; i++)
        {
            PointClickPlayerMovement candidate = candidates[i];

            if (!IsLoadedSceneObject(candidate) || LooksLikeGuest(candidate.transform))
            {
                continue;
            }

            firstSafe ??= candidate;

            if (string.Equals(candidate.gameObject.name, "player", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }

            if (string.Equals(candidate.gameObject.tag, "Player", StringComparison.OrdinalIgnoreCase))
            {
                tagged ??= candidate;
            }

            if (NameLooksLikePlayerOrButler(candidate.name) || NameLooksLikePlayerOrButler(candidate.gameObject.name))
            {
                named ??= candidate;
            }
        }

        return tagged != null ? tagged : named != null ? named : firstSafe;
    }

    private bool IsButlerObjectOrChild(Transform target)
    {
        return selectedButler != null &&
            target != null &&
            selectedButler.transform != null &&
            (target == selectedButler.transform || target.IsChildOf(selectedButler.transform));
    }

    private void MarkButlerSceneDirty()
    {
        if (selectedButler != null && selectedButler.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(selectedButler.gameObject.scene);
        }
    }

    private static bool IsLoadedSceneObject(Component component)
    {
        return component != null &&
            component.gameObject != null &&
            component.gameObject.scene.IsValid() &&
            component.gameObject.scene.isLoaded;
    }

    private static bool LooksLikeGuest(Transform target)
    {
        return target != null &&
            (ContainsGuest(target.name) ||
            target.GetComponentInParent<RoomProjectedEntity>(true) != null);
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

    private static bool NameLooksLikePlayerOrButler(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            (value.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("Butler", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool ContainsRoomId(List<string> values, string roomId)
    {
        if (values == null || string.IsNullOrWhiteSpace(roomId))
        {
            return false;
        }

        for (int i = 0; i < values.Count; i++)
        {
            if (SameRoom(values[i], roomId))
            {
                return true;
            }
        }

        return false;
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
