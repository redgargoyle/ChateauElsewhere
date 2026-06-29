using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class GuestScaleCalibrationWindow : EditorWindow
{
    private const string HelpText = "Use the Butler calibration as the room/depth source. Pick a guest, choose the actual visible character art as the scale root if auto detection is wrong, adjust the height ratio/fine tune until the guest visually matches the Butler, then save.";

    private PointClickPlayerMovement selectedButler;
    private GuestScaleCalibrationStore calibrationStore;
    private readonly List<GuestCandidate> candidates = new List<GuestCandidate>();
    private readonly List<string> roomNames = new List<string>();
    private Vector2 scroll;
    private int selectedRoomIndex;
    private int selectedGuestIndex;
    private GuestPose selectedPose = GuestPose.Auto;
    private float heightRatioToButlerStanding = 1f;
    private float manualFineTuneMultiplier = 1f;
    private Transform selectedScaleRoot;
    private Transform selectedBoundsRoot;
    private string lastStatus = string.Empty;

    private static readonly KnownGuestDefinition[] KnownChapterGuests =
    {
        new KnownGuestDefinition("actor:guest_1", "Guest 01", "Miss Isolde Wren", new[] { "Guest1", "Guest 1", "Guest01", "Lady", "Miss Isolde Wren" }),
        new KnownGuestDefinition("actor:guest_2", "Guest 02", "Professor Lucien Vale", new[] { "Guest2", "Guest 2", "Guest02", "ButlerGuest", "Butler Guest", "Professor Lucien Vale" }),
        new KnownGuestDefinition("actor:guest_3", "Guest 03", "Mister Florian Knell", new[] { "Guest3", "Guest 3", "Guest03", "Mister Florian Knell" }),
        new KnownGuestDefinition("actor:guest_4", "Guest 04", "Countess Elowen Dusk", new[] { "Guest4", "Guest 4", "Guest04", "Countess Elowen Dusk" }),
        new KnownGuestDefinition("actor:guest_5", "Guest 05", "Baron Hector Glass", new[] { "Guest5", "Guest 5", "Guest05", "Baron Hector Glass" }),
        new KnownGuestDefinition("actor:guest_6", "Guest 06", "Lady Sabine Marrow", new[] { "Guest6", "Guest 6", "Guest06", "Lady Sabine Marrow" }),
        new KnownGuestDefinition("actor:guest_7", "Guest 07", "Lord Ambrose Veil", new[] { "Guest7", "Guest 7", "Guest07", "Lord Ambrose Veil" }),
        new KnownGuestDefinition("actor:guest_8", "Guest 08", "Madame Coralie Thread", new[] { "Guest8", "Guest 8", "Guest08", "Madame Coralie Thread" })
    };

    [MenuItem("Tools/Characters/Manual Guest Scale Calibration")]
    public static void Open()
    {
        GetWindow<GuestScaleCalibrationWindow>("Guest Scale Calibration");
    }

    private void OnEnable()
    {
        selectedButler = FindSceneButler();
        calibrationStore = FindCalibrationStore(selectedButler);
        RefreshGuestList();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Guest Scale Calibration", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(HelpText, MessageType.Info);

        DrawTopControls();
        DrawGuestSelection();
        DrawGuestDetails();
        DrawManualControls();
        DrawRootControls();
        DrawWarnings();

        if (!string.IsNullOrWhiteSpace(lastStatus))
        {
            EditorGUILayout.HelpBox(lastStatus, MessageType.None);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawTopControls()
    {
        EditorGUILayout.Space(6f);
        selectedButler = (PointClickPlayerMovement)EditorGUILayout.ObjectField(
            "Butler / Player",
            selectedButler,
            typeof(PointClickPlayerMovement),
            true);
        calibrationStore = (GuestScaleCalibrationStore)EditorGUILayout.ObjectField(
            "GuestScaleCalibrationStore",
            calibrationStore,
            typeof(GuestScaleCalibrationStore),
            true);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Find Scene Butler"))
            {
                selectedButler = FindSceneButler();
                calibrationStore = FindCalibrationStore(selectedButler);
                lastStatus = selectedButler != null ? $"Found {selectedButler.name}." : "No Butler/player found.";
            }

            if (GUILayout.Button("Ensure Calibration Store"))
            {
                calibrationStore = EnsureCalibrationStore();
                lastStatus = calibrationStore != null ? $"Ensured calibration store on {calibrationStore.name}." : "No Butler/player available for a store.";
            }

            if (GUILayout.Button("Refresh Guest List"))
            {
                RefreshGuestList();
                lastStatus = $"Found {candidates.Count} guest scale target(s).";
            }

            if (GUILayout.Button("Save Scene"))
            {
                EditorSceneManager.SaveOpenScenes();
                lastStatus = "Saved open scene(s).";
            }
        }
    }

    private void DrawGuestSelection()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Guest Selection", EditorStyles.boldLabel);

        if (candidates.Count == 0)
        {
            EditorGUILayout.HelpBox("No guest scale targets found in loaded scenes.", MessageType.Warning);
            return;
        }

        string[] rooms = roomNames.Count > 0 ? roomNames.ToArray() : new[] { "<none>" };
        int nextRoomIndex = EditorGUILayout.Popup("Room", Mathf.Clamp(selectedRoomIndex, 0, rooms.Length - 1), rooms);

        if (nextRoomIndex != selectedRoomIndex)
        {
            selectedRoomIndex = nextRoomIndex;
            selectedGuestIndex = 0;
            LoadSelectionFromCurrentGuest();
        }

        List<GuestCandidate> roomCandidates = GetCandidatesForSelectedRoom();
        string[] labels = BuildCandidateLabels(roomCandidates);
        int nextGuestIndex = EditorGUILayout.Popup("Guest", Mathf.Clamp(selectedGuestIndex, 0, Mathf.Max(0, labels.Length - 1)), labels);

        if (nextGuestIndex != selectedGuestIndex)
        {
            selectedGuestIndex = nextGuestIndex;
            LoadSelectionFromCurrentGuest();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Previous Guest"))
            {
                StepGuest(-1);
            }

            if (GUILayout.Button("Next Guest"))
            {
                StepGuest(1);
            }

            if (GUILayout.Button("Select/Ping Guest"))
            {
                SelectAndPing(GetSelectedCandidate()?.Root);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Select/Ping Scale Root"))
            {
                SelectAndPing(selectedScaleRoot);
            }

            if (GUILayout.Button("Select/Ping Bounds Root"))
            {
                SelectAndPing(selectedBoundsRoot);
            }
        }
    }

    private void DrawGuestDetails()
    {
        GuestCandidate candidate = GetSelectedCandidate();

        if (candidate == null)
        {
            return;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Guest Details", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Controller type", candidate.ControllerType);
        EditorGUILayout.LabelField("Room id", GetSelectedRoomId());
        EditorGUILayout.LabelField("Guest current/native room", string.IsNullOrWhiteSpace(candidate.RoomId) ? "-" : candidate.RoomId);
        EditorGUILayout.LabelField("Room-local foot Y", GetCalibrationFootPoint(candidate).y.ToString("0.###"));
        EditorGUILayout.LabelField("Pose", selectedPose.ToString());

        CalibrationMetrics metrics = BuildMetrics(candidate);
        EditorGUILayout.LabelField("Current visual height px", metrics.CurrentVisualHeight);
        EditorGUILayout.LabelField("Butler baked target height px", metrics.ButlerStandingHeight);
        EditorGUILayout.LabelField("Target guest height px", metrics.TargetGuestHeight);
        EditorGUILayout.LabelField("Current scaleRoot localScale", selectedScaleRoot != null ? selectedScaleRoot.localScale.ToString() : "-");
        EditorGUILayout.LabelField("Current boundsRoot", selectedBoundsRoot != null ? GetObjectPath(selectedBoundsRoot) : "-");
        EditorGUILayout.LabelField("Current scaleRoot", selectedScaleRoot != null ? GetObjectPath(selectedScaleRoot) : "-");
        EditorGUILayout.LabelField("Existing manual calibration?", GetManualEntry(candidate) != null ? "yes" : "no");
        EditorGUILayout.LabelField("Guest key", string.IsNullOrWhiteSpace(candidate.GuestKey) ? "-" : candidate.GuestKey);
    }

    private void DrawManualControls()
    {
        GuestCandidate candidate = GetSelectedCandidate();

        if (candidate == null)
        {
            return;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Manual Controls", EditorStyles.boldLabel);
        GuestPose nextPose = (GuestPose)EditorGUILayout.EnumPopup("Pose", selectedPose);

        if (nextPose != selectedPose)
        {
            selectedPose = nextPose;
            heightRatioToButlerStanding = GetDefaultRatioForCandidate(candidate, selectedPose);
        }

        heightRatioToButlerStanding = EditorGUILayout.Slider("Height Ratio To Butler Standing", heightRatioToButlerStanding, 0.30f, 1.20f);
        manualFineTuneMultiplier = EditorGUILayout.Slider("Manual Fine Tune", manualFineTuneMultiplier, 0.50f, 1.50f);

        if (GUILayout.Button("Preview Target Height Now"))
        {
            TryFitSelectedGuest(false);
        }

        if (GUILayout.Button("Auto Match Guest To Butler Here"))
        {
            TryFitSelectedGuest(false);
        }

        if (GUILayout.Button("Save Calibration For This Guest In This Room"))
        {
            SaveSelectedCalibration();
        }

        if (GUILayout.Button("Remove Calibration For This Guest In This Room"))
        {
            RemoveSelectedCalibration();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Capture Current Scale Root As Base"))
            {
                CaptureBaseScale();
            }

            if (GUILayout.Button("Restore Scale Root Base"))
            {
                RestoreBaseScale();
            }
        }
    }

    private void DrawRootControls()
    {
        GuestCandidate candidate = GetSelectedCandidate();

        if (candidate == null)
        {
            return;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Root Controls", EditorStyles.boldLabel);

        if (GUILayout.Button("Use Auto Detected Visual Root"))
        {
            selectedScaleRoot = candidate.AutoScaleRoot;
            selectedBoundsRoot = candidate.AutoBoundsRoot;
            lastStatus = "Using auto-detected visual roots.";
        }

        if (GUILayout.Button("Use Selected Object As Scale Root"))
        {
            selectedScaleRoot = Selection.activeTransform;
            lastStatus = selectedScaleRoot != null ? $"Scale root set to {selectedScaleRoot.name}." : "No selected object.";
        }

        if (GUILayout.Button("Use Selected Object As Bounds Root"))
        {
            selectedBoundsRoot = Selection.activeTransform;
            lastStatus = selectedBoundsRoot != null ? $"Bounds root set to {selectedBoundsRoot.name}." : "No selected object.";
        }

        using (new EditorGUI.DisabledScope(candidate.Walker == null || candidate.Walker.TargetGraphic == null))
        {
            if (GUILayout.Button("Use TargetGraphic As Scale Root"))
            {
                selectedScaleRoot = candidate.Walker.TargetGraphic.rectTransform;
                selectedBoundsRoot = candidate.Walker.TargetGraphic.transform;
                lastStatus = "Using RoomPersonWalker2D targetGraphic roots.";
            }
        }

        using (new EditorGUI.DisabledScope(candidate.ProjectedEntity == null || candidate.ProjectedEntity.VisualRoot == null))
        {
            if (GUILayout.Button("Use VisualRoot As Scale Root"))
            {
                selectedScaleRoot = candidate.ProjectedEntity.VisualRoot;
                selectedBoundsRoot = candidate.ProjectedEntity.VisualRoot;
                lastStatus = "Using RoomProjectedEntity VisualRoot.";
            }
        }
    }

    private void DrawWarnings()
    {
        GuestCandidate candidate = GetSelectedCandidate();

        if (candidate == null)
        {
            return;
        }

        List<string> warnings = BuildWarnings(candidate);

        for (int i = 0; i < warnings.Count; i++)
        {
            EditorGUILayout.HelpBox(warnings[i], MessageType.Warning);
        }
    }

    private void RefreshGuestList()
    {
        candidates.Clear();
        roomNames.Clear();
        Camera camera = ResolveCamera();
        HashSet<Transform> claimedRoots = new HashSet<Transform>();
        HashSet<string> claimedGuestKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        RoomProjectedEntity[] entities = FindObjectsByType<RoomProjectedEntity>(FindObjectsInactive.Include);
        for (int i = 0; i < entities.Length; i++)
        {
            RoomProjectedEntity entity = entities[i];

            if (!IsLoadedSceneObject(entity) || IsButlerObjectOrChild(entity.transform))
            {
                continue;
            }

            RoomPersonWalker2D walker = entity.GetComponent<RoomPersonWalker2D>();
            ActorRoomState actor = entity.GetComponentInParent<ActorRoomState>(true);

            if (walker == null && !LooksLikeGuestScaleTarget(entity.gameObject, actor))
            {
                continue;
            }

            if (!entity.TryResolveGuestRoomAndFootPoint(out string roomId, out Vector2 footPoint))
            {
                roomId = entity.RoomProfile != null ? entity.RoomProfile.RoomId : string.Empty;
            }

            AddCandidate(
                new GuestCandidate(
                    entity,
                    walker,
                    actor,
                    entity.transform,
                    "RoomProjectedEntity" + (walker != null ? " + RoomPersonWalker2D" : string.Empty),
                    roomId,
                    footPoint,
                    BuildGuestKey(entity, walker, actor, entity.transform),
                    BuildGuestDisplayName(entity.gameObject, actor)),
                camera,
                claimedRoots,
                claimedGuestKeys);
        }

        RoomPersonWalker2D[] walkers = FindObjectsByType<RoomPersonWalker2D>(FindObjectsInactive.Include);
        for (int i = 0; i < walkers.Length; i++)
        {
            RoomPersonWalker2D walker = walkers[i];

            if (!IsLoadedSceneObject(walker) || IsButlerObjectOrChild(walker.transform))
            {
                continue;
            }

            RoomProjectedEntity projection = walker.GetComponent<RoomProjectedEntity>();

            if (projection != null && projection.IsProjectionActive)
            {
                continue;
            }

            walker.TryResolveGuestRoomAndFootPoint(out string roomId, out Vector2 footPoint);
            ActorRoomState actor = walker.GetComponentInParent<ActorRoomState>(true);
            AddCandidate(
                new GuestCandidate(
                    null,
                    walker,
                    actor,
                    walker.transform,
                    projection != null ? "RoomProjectedEntity + RoomPersonWalker2D" : "RoomPersonWalker2D",
                    roomId,
                    footPoint,
                    BuildGuestKey(null, walker, actor, walker.transform),
                    BuildGuestDisplayName(walker.gameObject, actor)),
                camera,
                claimedRoots,
                claimedGuestKeys);
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

            actor.TryResolveGuestRoomAndFootPoint(out string roomId, out Vector2 footPoint);
            AddCandidate(
                new GuestCandidate(
                    null,
                    null,
                    actor,
                    actor.transform,
                    "ActorRoomState",
                    roomId,
                    footPoint,
                    BuildGuestKey(null, null, actor, actor.transform),
                    BuildGuestDisplayName(actor.gameObject, actor)),
                camera,
                claimedRoots,
                claimedGuestKeys);
        }

        AddLooseSceneGuestCandidates(camera, claimedRoots, claimedGuestKeys);
        AddKnownChapterGuestCandidates(camera, claimedRoots, claimedGuestKeys);
        BuildRoomChoices();

        candidates.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase));
        selectedRoomIndex = Mathf.Clamp(selectedRoomIndex, 0, Mathf.Max(0, roomNames.Count - 1));
        selectedGuestIndex = Mathf.Clamp(selectedGuestIndex, 0, Mathf.Max(0, GetCandidatesForSelectedRoom().Count - 1));
        LoadSelectionFromCurrentGuest();
    }

    private void AddCandidate(
        GuestCandidate candidate,
        Camera camera,
        HashSet<Transform> claimedRoots,
        HashSet<string> claimedGuestKeys)
    {
        ResolveAutoRoots(candidate, camera);
        Transform key = candidate.AutoScaleRoot != null ? candidate.AutoScaleRoot : candidate.Root;
        string guestKey = string.IsNullOrWhiteSpace(candidate.GuestKey) ? string.Empty : NormalizeGuestKey(candidate.GuestKey);

        if (!string.IsNullOrWhiteSpace(guestKey) && claimedGuestKeys.Contains(guestKey))
        {
            return;
        }

        if (key != null && claimedRoots.Contains(key))
        {
            return;
        }

        if (key != null)
        {
            claimedRoots.Add(key);
        }

        if (!string.IsNullOrWhiteSpace(guestKey))
        {
            claimedGuestKeys.Add(guestKey);
        }

        candidates.Add(candidate);
    }

    private void ResolveAutoRoots(GuestCandidate candidate, Camera camera)
    {
        Transform root = candidate.Root;

        if (candidate.ProjectedEntity != null && candidate.ProjectedEntity.VisualRoot != null)
        {
            root = candidate.ProjectedEntity.VisualRoot;
        }

        if (candidate.Walker != null && candidate.Walker.TargetGraphic != null)
        {
            candidate.AutoScaleRoot = candidate.Walker.TargetGraphic.rectTransform;
            candidate.AutoBoundsRoot = candidate.Walker.TargetGraphic.transform;
            candidate.PrimaryVisual = candidate.Walker.TargetGraphic.transform;
            return;
        }

        candidate.AutoScaleRoot = root;
        candidate.AutoBoundsRoot = root;

        if (root != null &&
            camera != null &&
            CharacterVisualBoundsUtility.TryResolveCharacterVisualTarget(root, camera, out CharacterVisualBoundsUtility.CharacterVisualTarget target, includeInactive: true))
        {
            candidate.AutoBoundsRoot = target.BoundsRoot;
            candidate.AutoScaleRoot = CharacterVisualBoundsUtility.LooksLikeForbiddenContainer(root)
                ? target.PrimaryVisual
                : root;
            candidate.PrimaryVisual = target.PrimaryVisual;
        }
    }

    private void AddLooseSceneGuestCandidates(
        Camera camera,
        HashSet<Transform> claimedRoots,
        HashSet<string> claimedGuestKeys)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject guestObject = GetLooseSceneGuestRoot(objects[i]);

            if (guestObject == null)
            {
                continue;
            }

            ActorRoomState actor = guestObject.GetComponent<ActorRoomState>();
            RoomProjectedEntity projection = guestObject.GetComponent<RoomProjectedEntity>();
            RoomPersonWalker2D walker = guestObject.GetComponent<RoomPersonWalker2D>();
            string roomId = ResolveRoomId(guestObject.transform, projection, walker, actor);
            Vector2 footPoint = ResolveFootPoint(guestObject.transform, projection, walker, actor);
            string guestKey = BuildGuestKey(projection, walker, actor, guestObject.transform);
            AddCandidate(
                new GuestCandidate(
                    projection,
                    walker,
                    actor,
                    guestObject.transform,
                    "Scene Guest Object",
                    roomId,
                    footPoint,
                    guestKey,
                    BuildGuestDisplayName(guestObject, actor)),
                camera,
                claimedRoots,
                claimedGuestKeys);
        }
    }

    private void AddKnownChapterGuestCandidates(
        Camera camera,
        HashSet<Transform> claimedRoots,
        HashSet<string> claimedGuestKeys)
    {
        for (int i = 0; i < KnownChapterGuests.Length; i++)
        {
            KnownGuestDefinition guest = KnownChapterGuests[i];
            GameObject sceneObject = FindKnownGuestSceneObject(guest);
            ActorRoomState actor = sceneObject != null ? sceneObject.GetComponent<ActorRoomState>() : null;
            RoomProjectedEntity projection = sceneObject != null ? sceneObject.GetComponent<RoomProjectedEntity>() : null;
            RoomPersonWalker2D walker = sceneObject != null ? sceneObject.GetComponent<RoomPersonWalker2D>() : null;
            Transform root = sceneObject != null ? sceneObject.transform : null;
            string roomId = sceneObject != null ? ResolveRoomId(root, projection, walker, actor) : string.Empty;
            Vector2 footPoint = sceneObject != null ? ResolveFootPoint(root, projection, walker, actor) : Vector2.zero;

            AddCandidate(
                new GuestCandidate(
                    projection,
                    walker,
                    actor,
                    root,
                    sceneObject != null ? "Known Chapter Guest" : "Known Chapter Guest (runtime/synthetic)",
                    roomId,
                    footPoint,
                    guest.GuestKey,
                    $"{guest.ShortName} - {guest.DisplayName}"),
                camera,
                claimedRoots,
                claimedGuestKeys);
        }
    }

    private void BuildRoomChoices()
    {
        roomNames.Clear();

        if (selectedButler != null)
        {
            List<string> calibrationRooms = new List<string>();
            selectedButler.GetButlerScaleOverrideRoomIds(calibrationRooms);

            for (int i = 0; i < calibrationRooms.Count; i++)
            {
                AddRoomChoice(calibrationRooms[i]);
            }
        }

        AddNavigationRoomChoices();
        AddRoomVisualCatalogChoices();
        AddDoorCameraSequenceRoomChoices();
        AddRoomPerspectiveProfileChoices();

        RoomContentGroup[] roomContents = FindObjectsByType<RoomContentGroup>(FindObjectsInactive.Include);
        for (int i = 0; i < roomContents.Length; i++)
        {
            RoomContentGroup room = roomContents[i];

            if (room != null && room.gameObject.scene.IsValid())
            {
                AddRoomChoice(room.RoomName);
            }
        }

        if (calibrationStore != null)
        {
            IReadOnlyList<GuestScaleCalibrationEntry> entries = calibrationStore.GetAllEntries();

            for (int i = 0; i < entries.Count; i++)
            {
                AddRoomChoice(entries[i]?.roomId);
            }
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            AddRoomChoice(candidates[i].RoomId);
        }

        if (roomNames.Count == 0)
        {
            roomNames.Add("<no room>");
        }

        roomNames.Sort(StringComparer.OrdinalIgnoreCase);
    }

    private void AddRoomChoice(string roomId)
    {
        string cleanRoomId = string.IsNullOrWhiteSpace(roomId) ? string.Empty : roomId.Trim();

        if (string.IsNullOrWhiteSpace(cleanRoomId))
        {
            return;
        }

        if (!ContainsRoom(roomNames, cleanRoomId))
        {
            roomNames.Add(cleanRoomId);
        }
    }

    private void AddNavigationRoomChoices()
    {
        RoomNavigationManager[] navigationManagers = FindObjectsByType<RoomNavigationManager>(FindObjectsInactive.Include);

        for (int i = 0; i < navigationManagers.Length; i++)
        {
            RoomNavigationManager navigationManager = navigationManagers[i];

            if (navigationManager == null ||
                !navigationManager.gameObject.scene.IsValid() ||
                !navigationManager.gameObject.scene.isLoaded)
            {
                continue;
            }

            List<string> knownRoomNames = navigationManager.GetKnownRoomNames();

            for (int roomIndex = 0; roomIndex < knownRoomNames.Count; roomIndex++)
            {
                AddRoomChoice(knownRoomNames[roomIndex]);
            }
        }
    }

    private void AddRoomVisualCatalogChoices()
    {
        string[] guids = AssetDatabase.FindAssets("t:RoomVisualCatalog");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            RoomVisualCatalog catalog = AssetDatabase.LoadAssetAtPath<RoomVisualCatalog>(path);

            if (catalog == null || catalog.rooms == null)
            {
                continue;
            }

            for (int roomIndex = 0; roomIndex < catalog.rooms.Length; roomIndex++)
            {
                RoomVisualEntry entry = catalog.rooms[roomIndex];
                AddRoomChoice(entry != null ? entry.roomName : null);
            }
        }
    }

    private void AddDoorCameraSequenceRoomChoices()
    {
        string[] guids = AssetDatabase.FindAssets("t:DoorCameraSequence");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            DoorCameraSequence sequence = AssetDatabase.LoadAssetAtPath<DoorCameraSequence>(path);

            if (sequence == null)
            {
                continue;
            }

            AddRoomChoice(sequence.startingRoom);
            AddRoomChoice(sequence.loopStartRoom);

            if (sequence.roomOrder == null)
            {
                continue;
            }

            for (int roomIndex = 0; roomIndex < sequence.roomOrder.Length; roomIndex++)
            {
                AddRoomChoice(sequence.roomOrder[roomIndex]);
            }
        }
    }

    private void AddRoomPerspectiveProfileChoices()
    {
        string[] guids = AssetDatabase.FindAssets("t:RoomPerspectiveProfile");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            RoomPerspectiveProfile profile = AssetDatabase.LoadAssetAtPath<RoomPerspectiveProfile>(path);
            AddRoomChoice(profile != null ? profile.RoomId : null);
        }
    }

    private GameObject GetLooseSceneGuestRoot(GameObject candidate)
    {
        if (!IsLooseSceneGuestCandidate(candidate))
        {
            return null;
        }

        Transform current = candidate.transform.parent;

        while (current != null)
        {
            if (IsLooseSceneGuestCandidate(current.gameObject))
            {
                return null;
            }

            current = current.parent;
        }

        return candidate;
    }

    private bool IsLooseSceneGuestCandidate(GameObject candidate)
    {
        if (candidate == null ||
            !candidate.scene.IsValid() ||
            !candidate.scene.isLoaded ||
            IsButlerObjectOrChild(candidate.transform))
        {
            return false;
        }

        if (!LooksLikeKnownGuestObject(candidate) && !LooksLikeGuestName(candidate.name))
        {
            return false;
        }

        if (ContainsIgnoreCase(candidate.name, "arrival") ||
            ContainsIgnoreCase(candidate.name, "anchor") ||
            ContainsIgnoreCase(candidate.name, "point") ||
            ContainsIgnoreCase(candidate.name, "target") ||
            ContainsIgnoreCase(candidate.name, "coat"))
        {
            return false;
        }

        return candidate.GetComponentInChildren<Renderer>(true) != null ||
            candidate.GetComponentInChildren<UnityEngine.UI.Graphic>(true) != null ||
            candidate.GetComponentInChildren<Animator>(true) != null;
    }

    private static bool LooksLikeKnownGuestObject(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        for (int i = 0; i < KnownChapterGuests.Length; i++)
        {
            if (KnownChapterGuests[i].Matches(candidate.name))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool LooksLikeGuestScaleTarget(GameObject gameObject, ActorRoomState actor)
    {
        return LooksLikeGuestActor(actor) ||
            LooksLikeKnownGuestObject(gameObject) ||
            (gameObject != null && LooksLikeGuestName(gameObject.name));
    }

    private static bool LooksLikeGuestName(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.IndexOf("Guest", StringComparison.OrdinalIgnoreCase) >= 0 &&
            value.IndexOf("GuestArrival", StringComparison.OrdinalIgnoreCase) < 0 &&
            value.IndexOf("GuestPoint", StringComparison.OrdinalIgnoreCase) < 0 &&
            value.IndexOf("GuestAnchor", StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static string BuildGuestKey(
        RoomProjectedEntity projection,
        RoomPersonWalker2D walker,
        ActorRoomState actor,
        Transform root)
    {
        string knownKey = TryGetKnownGuestKey(actor != null ? actor.ActorId : null);

        if (!string.IsNullOrWhiteSpace(knownKey))
        {
            return knownKey;
        }

        knownKey = TryGetKnownGuestKey(root != null ? root.name : null);

        if (!string.IsNullOrWhiteSpace(knownKey))
        {
            return knownKey;
        }

        return GuestScaleCalibrationStore.BuildGuestKey(projection, walker, actor, root);
    }

    private static string BuildGuestDisplayName(GameObject gameObject, ActorRoomState actor)
    {
        string knownName = TryGetKnownGuestDisplayName(actor != null ? actor.ActorId : null);

        if (!string.IsNullOrWhiteSpace(knownName))
        {
            return knownName;
        }

        knownName = TryGetKnownGuestDisplayName(gameObject != null ? gameObject.name : null);

        if (!string.IsNullOrWhiteSpace(knownName))
        {
            return knownName;
        }

        if (actor != null && !string.IsNullOrWhiteSpace(actor.ActorId))
        {
            return actor.ActorId;
        }

        return gameObject != null ? gameObject.name : "Guest";
    }

    private static string TryGetKnownGuestKey(string value)
    {
        for (int i = 0; i < KnownChapterGuests.Length; i++)
        {
            if (KnownChapterGuests[i].Matches(value))
            {
                return KnownChapterGuests[i].GuestKey;
            }
        }

        return string.Empty;
    }

    private static string TryGetKnownGuestDisplayName(string value)
    {
        for (int i = 0; i < KnownChapterGuests.Length; i++)
        {
            if (KnownChapterGuests[i].Matches(value))
            {
                return $"{KnownChapterGuests[i].ShortName} - {KnownChapterGuests[i].DisplayName}";
            }
        }

        return string.Empty;
    }

    private static GameObject FindKnownGuestSceneObject(KnownGuestDefinition guest)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];

            if (candidate != null &&
                candidate.scene.IsValid() &&
                candidate.scene.isLoaded &&
                guest.Matches(candidate.name))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string ResolveRoomId(
        Transform root,
        RoomProjectedEntity projection,
        RoomPersonWalker2D walker,
        ActorRoomState actor)
    {
        if (projection != null && projection.TryResolveGuestRoomAndFootPoint(out string projectionRoom, out _))
        {
            return projectionRoom;
        }

        if (walker != null && walker.TryResolveGuestRoomAndFootPoint(out string walkerRoom, out _))
        {
            return walkerRoom;
        }

        if (actor != null && actor.TryResolveGuestRoomAndFootPoint(out string actorRoom, out _))
        {
            return actorRoom;
        }

        RoomContentGroup room = root != null ? root.GetComponentInParent<RoomContentGroup>(true) : null;
        return room != null ? room.RoomName : string.Empty;
    }

    private static Vector2 ResolveFootPoint(
        Transform root,
        RoomProjectedEntity projection,
        RoomPersonWalker2D walker,
        ActorRoomState actor)
    {
        if (projection != null && projection.TryResolveGuestRoomAndFootPoint(out _, out Vector2 projectionFootPoint))
        {
            return projectionFootPoint;
        }

        if (walker != null && walker.TryResolveGuestRoomAndFootPoint(out _, out Vector2 walkerFootPoint))
        {
            return walkerFootPoint;
        }

        if (actor != null && actor.TryResolveGuestRoomAndFootPoint(out _, out Vector2 actorFootPoint))
        {
            return actorFootPoint;
        }

        if (root is RectTransform rectTransform)
        {
            return rectTransform.anchoredPosition;
        }

        return root != null ? new Vector2(root.localPosition.x, root.localPosition.y) : Vector2.zero;
    }

    private void LoadSelectionFromCurrentGuest()
    {
        GuestCandidate candidate = GetSelectedCandidate();

        if (candidate == null)
        {
            selectedScaleRoot = null;
            selectedBoundsRoot = null;
            selectedPose = GuestPose.Auto;
            heightRatioToButlerStanding = 1f;
            manualFineTuneMultiplier = 1f;
            return;
        }

        GuestScaleCalibrationEntry entry = GetManualEntry(candidate);

        if (entry != null)
        {
            selectedScaleRoot = entry.scaleRoot != null ? entry.scaleRoot : candidate.AutoScaleRoot;
            selectedBoundsRoot = entry.boundsRoot != null ? entry.boundsRoot : candidate.AutoBoundsRoot;
            selectedPose = entry.pose;
            heightRatioToButlerStanding = GuestScaleCalibrationStore.SanitizeRatio(entry.heightRatioToButlerStanding);
            manualFineTuneMultiplier = GuestScaleCalibrationStore.SanitizeFineTune(entry.manualFineTuneMultiplier);
            return;
        }

        selectedScaleRoot = candidate.AutoScaleRoot;
        selectedBoundsRoot = candidate.AutoBoundsRoot;
        selectedPose = GetDefaultPose(candidate);
        heightRatioToButlerStanding = GetDefaultRatioForCandidate(candidate, selectedPose);
        manualFineTuneMultiplier = 1f;
    }

    private void SaveSelectedCalibration()
    {
        GuestCandidate candidate = GetSelectedCandidate();

        if (candidate == null)
        {
            return;
        }

        calibrationStore = EnsureCalibrationStore();

        if (calibrationStore == null)
        {
            lastStatus = "No calibration store available.";
            return;
        }

        Undo.RecordObject(calibrationStore, "Save Guest Scale Calibration");
        string roomId = GetSelectedRoomId();
        GuestScaleCalibrationEntry entry = string.IsNullOrWhiteSpace(candidate.GuestKey)
            ? calibrationStore.SetCalibrationForGuest(
                candidate.ProjectedEntity != null ? (Component)candidate.ProjectedEntity : candidate.Walker != null ? candidate.Walker : candidate.Actor,
                roomId,
                selectedPose,
                selectedScaleRoot,
                selectedBoundsRoot,
                heightRatioToButlerStanding,
                manualFineTuneMultiplier)
            : calibrationStore.SetCalibrationForGuest(
                candidate.GuestKey,
                candidate.Label,
                roomId,
                selectedPose,
                selectedScaleRoot,
                selectedBoundsRoot,
                heightRatioToButlerStanding,
                manualFineTuneMultiplier);
        entry.displayName = candidate.Label;
        MarkDirty(calibrationStore);
        lastStatus = $"Saved manual calibration for {candidate.Label} in {roomId}.";
    }

    private void RemoveSelectedCalibration()
    {
        GuestCandidate candidate = GetSelectedCandidate();

        if (candidate == null || calibrationStore == null)
        {
            return;
        }

        Undo.RecordObject(calibrationStore, "Remove Guest Scale Calibration");
        string roomId = GetSelectedRoomId();
        bool removed = !string.IsNullOrWhiteSpace(candidate.GuestKey) &&
            calibrationStore.RemoveCalibrationForGuest(candidate.GuestKey, candidate.Label, roomId);

        if (!removed)
        {
            removed = calibrationStore.RemoveCalibrationForGuest(
                candidate.ProjectedEntity,
                candidate.Walker,
                candidate.Actor,
                roomId,
                selectedScaleRoot);
        }
        MarkDirty(calibrationStore);
        lastStatus = removed ? $"Removed manual calibration for {candidate.Label}." : "No matching manual calibration found.";
    }

    private void CaptureBaseScale()
    {
        GuestCandidate candidate = GetSelectedCandidate();

        if (candidate == null)
        {
            return;
        }

        calibrationStore = EnsureCalibrationStore();

        if (calibrationStore == null)
        {
            lastStatus = "No calibration store available.";
            return;
        }

        GuestScaleCalibrationEntry entry = !string.IsNullOrWhiteSpace(candidate.GuestKey)
            ? calibrationStore.GetOrCreateEntry(candidate.GuestKey, candidate.Label, GetSelectedRoomId())
            : calibrationStore.GetOrCreateEntry(candidate.ProjectedEntity, candidate.Walker, candidate.Actor, GetSelectedRoomId(), selectedScaleRoot);
        entry.scaleRoot = selectedScaleRoot;
        Undo.RecordObject(calibrationStore, "Capture Guest Scale Base");
        bool captured = calibrationStore.CaptureBaseScale(entry);
        MarkDirty(calibrationStore);
        lastStatus = captured ? "Captured current scale root as base." : "No scale root available to capture.";
    }

    private void RestoreBaseScale()
    {
        GuestCandidate candidate = GetSelectedCandidate();

        if (candidate == null)
        {
            return;
        }

        GuestScaleCalibrationEntry entry = candidate != null ? GetManualEntry(candidate) : null;

        if (entry == null && calibrationStore != null)
        {
            entry = !string.IsNullOrWhiteSpace(candidate.GuestKey)
                ? calibrationStore.GetOrCreateEntry(candidate.GuestKey, candidate.Label, GetSelectedRoomId())
                : calibrationStore.GetOrCreateEntry(candidate.ProjectedEntity, candidate.Walker, candidate.Actor, GetSelectedRoomId(), selectedScaleRoot);
        }

        if (entry == null)
        {
            return;
        }

        Transform undoTarget = entry.scaleRoot != null ? entry.scaleRoot : candidate.Root;

        if (undoTarget != null)
        {
            Undo.RecordObject(undoTarget, "Restore Guest Scale Base");
        }

        bool restored = calibrationStore.RestoreBaseScale(entry);

        if (entry.scaleRoot != null)
        {
            EditorUtility.SetDirty(entry.scaleRoot);
        }

        lastStatus = restored ? "Restored captured scale root base." : "No captured scale root base to restore.";
    }

    private void TryFitSelectedGuest(bool requireSavedEntry)
    {
        GuestCandidate candidate = GetSelectedCandidate();

        if (candidate == null)
        {
            return;
        }

        if (requireSavedEntry && GetManualEntry(candidate) == null)
        {
            lastStatus = "No saved manual calibration for this guest.";
            return;
        }

        if (!TryCalculateTargetHeight(candidate, out float targetHeight, out string diagnostic))
        {
            LogManualApplyFailure(candidate, 0f, 0f, 0f, selectedScaleRoot != null ? selectedScaleRoot.localScale : Vector3.one, selectedScaleRoot != null ? selectedScaleRoot.localScale : Vector3.one, diagnostic);
            lastStatus = diagnostic;
            return;
        }

        Camera camera = ResolveCamera();

        if (camera == null)
        {
            lastStatus = "No camera found.";
            return;
        }

        Transform scaleRoot = selectedScaleRoot;
        Transform boundsRoot = selectedBoundsRoot != null ? selectedBoundsRoot : selectedScaleRoot;

        if (scaleRoot == null || boundsRoot == null)
        {
            lastStatus = "Scale root or bounds root missing.";
            return;
        }

        Undo.RecordObject(scaleRoot, "Manual Guest Scale Fit");
        bool fitted = CharacterVisualBoundsUtility.TryApplyTargetScreenHeight(
            scaleRoot,
            boundsRoot,
            camera,
            targetHeight,
            out CharacterVisualBoundsUtility.CharacterVisualFitResult result,
            includeInactive: true);

        if (!fitted)
        {
            LogManualApplyFailure(candidate, result.BeforeHeight, result.TargetHeight, result.AfterHeight, result.BeforeScale, result.AfterScale, result.Diagnostic);
            lastStatus = result.Diagnostic;
            return;
        }

        EditorUtility.SetDirty(scaleRoot);
        MarkSceneDirty(scaleRoot);
        lastStatus = $"Fit {candidate.Label}: {result.BeforeHeight:0.#} px -> {result.AfterHeight:0.#} px target {result.TargetHeight:0.#} px.";
    }

    private bool TryCalculateTargetHeight(GuestCandidate candidate, out float targetHeight, out string diagnostic)
    {
        targetHeight = 0f;
        diagnostic = string.Empty;
        Camera camera = ResolveCamera();

        if (selectedButler == null)
        {
            diagnostic = "No Butler/player selected.";
            return false;
        }

        if (camera == null)
        {
            diagnostic = "No camera found.";
            return false;
        }

        string roomId = GetSelectedRoomId();
        Vector2 footPoint = GetCalibrationFootPoint(candidate);

        if (!selectedButler.TryEvaluateButlerCharacterScale(roomId, footPoint, out PointClickPlayerMovement.ButlerCharacterScaleSample sample))
        {
            diagnostic = "No Butler calibration for this room";
            return false;
        }

        if (!selectedButler.TryGetButlerTargetScreenHeight(camera, sample, out float butlerTargetHeight, out diagnostic))
        {
            return false;
        }

        targetHeight =
            butlerTargetHeight *
            GuestScaleCalibrationStore.SanitizeRatio(heightRatioToButlerStanding) *
            GuestScaleCalibrationStore.SanitizeFineTune(manualFineTuneMultiplier);
        return targetHeight > 0.01f;
    }

    private CalibrationMetrics BuildMetrics(GuestCandidate candidate)
    {
        Camera camera = ResolveCamera();
        string currentHeight = "-";
        string standingHeight = "-";
        string targetHeight = "-";

        if (camera != null &&
            selectedBoundsRoot != null &&
            CharacterVisualBoundsUtility.TryGetScreenHeight(selectedBoundsRoot, camera, out float height, includeInactive: true))
        {
            currentHeight = height.ToString("0.#");
        }

        if (selectedButler != null &&
            camera != null &&
            selectedButler.TryEvaluateButlerCharacterScale(GetSelectedRoomId(), GetCalibrationFootPoint(candidate), out PointClickPlayerMovement.ButlerCharacterScaleSample sample) &&
            selectedButler.TryGetButlerTargetScreenHeight(camera, sample, out float bakedButlerTargetHeight, out _))
        {
            standingHeight = bakedButlerTargetHeight.ToString("0.#");
        }

        if (TryCalculateTargetHeight(candidate, out float guestTargetHeight, out _))
        {
            targetHeight = guestTargetHeight.ToString("0.#");
        }

        return new CalibrationMetrics(currentHeight, standingHeight, targetHeight);
    }

    private List<string> BuildWarnings(GuestCandidate candidate)
    {
        List<string> warnings = new List<string>();

        if (selectedButler == null ||
            !selectedButler.TryEvaluateButlerCharacterScale(GetSelectedRoomId(), GetCalibrationFootPoint(candidate), out _))
        {
            warnings.Add("No Butler calibration for this room");
        }

        Camera camera = ResolveCamera();
        if (camera == null ||
            selectedBoundsRoot == null ||
            !CharacterVisualBoundsUtility.TryResolveCharacterVisualTarget(selectedBoundsRoot, camera, out _, includeInactive: true))
        {
            warnings.Add("No visible Renderer/Graphic found");
        }

        if (CharacterVisualBoundsUtility.LooksLikeForbiddenContainer(selectedScaleRoot))
        {
            warnings.Add("Scale root is a forbidden container");
        }

        if (CharacterVisualBoundsUtility.LooksLikeForbiddenContainer(selectedBoundsRoot))
        {
            warnings.Add("Bounds root is a forbidden container");
        }

        if (candidate.ProjectedEntity != null && candidate.ProjectedEntity.RoomVisualScaleOverrideCount > 0)
        {
            warnings.Add("Guest has old roomVisualScaleOverrides");
        }

        CharacterVisualProfile profile = candidate.ProjectedEntity != null ? candidate.ProjectedEntity.VisualProfile : null;
        if (profile != null && !Mathf.Approximately(profile.HeightScaleMultiplier, 1f))
        {
            warnings.Add("Guest has non-1 CharacterVisualProfile.HeightScaleMultiplier");
        }

        if (selectedScaleRoot != null &&
            (Mathf.Abs(selectedScaleRoot.localScale.x) > 20f ||
            Mathf.Abs(selectedScaleRoot.localScale.y) > 20f ||
            Mathf.Abs(selectedScaleRoot.lossyScale.x) > 20f ||
            Mathf.Abs(selectedScaleRoot.lossyScale.y) > 20f))
        {
            warnings.Add("Guest has huge authored visual scale");
        }

        warnings.Add("Another script overwrote final scale after harmonizer only appears in Console when the final harmonizer detects a late scale change.");
        return warnings;
    }

    private GuestScaleCalibrationEntry GetManualEntry(GuestCandidate candidate)
    {
        if (candidate == null || calibrationStore == null)
        {
            return null;
        }

        return calibrationStore.TryGetCalibrationForGuest(
            candidate.ProjectedEntity,
            candidate.Walker,
            candidate.Actor,
            GetSelectedRoomId(),
            selectedScaleRoot != null ? selectedScaleRoot : candidate.AutoScaleRoot,
            out GuestScaleCalibrationEntry entry)
            ? entry
            : null;
    }

    private List<GuestCandidate> GetCandidatesForSelectedRoom()
    {
        return new List<GuestCandidate>(candidates);
    }

    private string GetSelectedRoomId()
    {
        if (roomNames.Count == 0)
        {
            return string.Empty;
        }

        string roomId = roomNames[Mathf.Clamp(selectedRoomIndex, 0, roomNames.Count - 1)];
        return roomId == "<no room>" ? string.Empty : roomId;
    }

    private Vector2 GetCalibrationFootPoint(GuestCandidate candidate)
    {
        if (candidate == null)
        {
            return Vector2.zero;
        }

        string selectedRoom = GetSelectedRoomId();

        if (candidate.Root != null)
        {
            RoomContentGroup room = candidate.Root.GetComponentInParent<RoomContentGroup>(true);
            RectTransform roomStage = room != null ? room.transform as RectTransform : null;

            if (roomStage != null && (string.IsNullOrWhiteSpace(selectedRoom) || SameRoom(room.RoomName, selectedRoom)))
            {
                Vector3 localPoint = roomStage.InverseTransformPoint(candidate.Root.position);
                return new Vector2(localPoint.x, localPoint.y);
            }
        }

        return SameRoom(candidate.RoomId, selectedRoom) || string.IsNullOrWhiteSpace(selectedRoom)
            ? candidate.FootPoint
            : Vector2.zero;
    }

    private GuestCandidate GetSelectedCandidate()
    {
        List<GuestCandidate> rows = GetCandidatesForSelectedRoom();

        if (rows.Count == 0)
        {
            return null;
        }

        selectedGuestIndex = Mathf.Clamp(selectedGuestIndex, 0, rows.Count - 1);
        return rows[selectedGuestIndex];
    }

    private void StepGuest(int delta)
    {
        List<GuestCandidate> rows = GetCandidatesForSelectedRoom();

        if (rows.Count == 0)
        {
            return;
        }

        selectedGuestIndex = (selectedGuestIndex + delta + rows.Count) % rows.Count;
        LoadSelectionFromCurrentGuest();
    }

    private GuestPose GetDefaultPose(GuestCandidate candidate)
    {
        return candidate != null && candidate.IsSeated ? GuestPose.Seated : GuestPose.Standing;
    }

    private float GetDefaultRatioForCandidate(GuestCandidate candidate, GuestPose pose)
    {
        if (pose == GuestPose.Auto)
        {
            pose = GetDefaultPose(candidate);
        }

        if (pose == GuestPose.Seated)
        {
            CharacterVisualProfile profile = candidate != null && candidate.ProjectedEntity != null
                ? candidate.ProjectedEntity.VisualProfile
                : null;

            if (profile != null)
            {
                return Mathf.Clamp(profile.SittingVisualHeight / Mathf.Max(1f, profile.StandingVisualHeight), 0.55f, 0.80f);
            }
        }

        return GuestScaleCalibrationStore.GetDefaultHeightRatioForPose(pose);
    }

    private GuestScaleCalibrationStore EnsureCalibrationStore()
    {
        if (calibrationStore != null)
        {
            return calibrationStore;
        }

        if (selectedButler == null)
        {
            selectedButler = FindSceneButler();
        }

        if (selectedButler == null)
        {
            return null;
        }

        calibrationStore = selectedButler.GetComponent<GuestScaleCalibrationStore>();

        if (calibrationStore == null)
        {
            calibrationStore = Undo.AddComponent<GuestScaleCalibrationStore>(selectedButler.gameObject);
        }

        GuestButlerScaleHarmonizer harmonizer = selectedButler.GetComponent<GuestButlerScaleHarmonizer>();

        if (harmonizer != null)
        {
            Undo.RecordObject(harmonizer, "Assign Guest Scale Calibration Store");
            harmonizer.SetCalibrationStore(calibrationStore);
            EditorUtility.SetDirty(harmonizer);
        }

        MarkDirty(calibrationStore);
        return calibrationStore;
    }

    private void LogManualApplyFailure(
        GuestCandidate candidate,
        float beforeHeight,
        float targetHeight,
        float afterHeight,
        Vector3 beforeScale,
        Vector3 afterScale,
        string reason)
    {
        Debug.LogWarning(
            "[GuestScaleCalibration] Apply failed " +
            $"guest={candidate.Label} path={GetObjectPath(candidate.Root)} room={GetSelectedRoomId()} pose={selectedPose} " +
            $"scaleRoot={GetObjectPath(selectedScaleRoot)} boundsRoot={GetObjectPath(selectedBoundsRoot)} " +
            $"primaryVisual={GetObjectPath(candidate.PrimaryVisual)} beforeHeight={beforeHeight:0.###} " +
            $"targetHeight={targetHeight:0.###} afterHeight={afterHeight:0.###} " +
            $"localScaleBefore={beforeScale} localScaleAfter={afterScale} reason={reason}",
            selectedScaleRoot != null ? selectedScaleRoot : candidate.Root);
    }

    private static string[] BuildCandidateLabels(List<GuestCandidate> rows)
    {
        if (rows.Count == 0)
        {
            return new[] { "<none>" };
        }

        string[] labels = new string[rows.Count];

        for (int i = 0; i < rows.Count; i++)
        {
            labels[i] = rows[i].Label;
        }

        return labels;
    }

    private static void SelectAndPing(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        Selection.activeObject = target;
        EditorGUIUtility.PingObject(target);
    }

    private static PointClickPlayerMovement FindSceneButler()
    {
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

            if (NameLooksLikePlayerOrButler(candidate.name) ||
                NameLooksLikePlayerOrButler(candidate.gameObject.name) ||
                string.Equals(candidate.gameObject.tag, "Player", StringComparison.OrdinalIgnoreCase))
            {
                named ??= candidate;
            }
        }

        return named != null ? named : first;
    }

    private static GuestScaleCalibrationStore FindCalibrationStore(PointClickPlayerMovement butler)
    {
        if (butler != null)
        {
            GuestScaleCalibrationStore store = butler.GetComponent<GuestScaleCalibrationStore>();

            if (store != null)
            {
                return store;
            }
        }

        return FindAnyObjectByType<GuestScaleCalibrationStore>(FindObjectsInactive.Include);
    }

    private static Camera ResolveCamera()
    {
        if (Camera.main != null)
        {
            return Camera.main;
        }

        return FindAnyObjectByType<Camera>(FindObjectsInactive.Exclude);
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

    private static bool ContainsIgnoreCase(string value, string fragment)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            !string.IsNullOrWhiteSpace(fragment) &&
            value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool NameLooksLikePlayerOrButler(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            (value.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("Butler", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool ContainsRoom(List<string> rooms, string roomId)
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            if (SameRoom(rooms[i], roomId) ||
                (rooms[i] == "<no room>" && string.IsNullOrWhiteSpace(roomId)))
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
        if (string.IsNullOrWhiteSpace(value) || value == "<no room>")
        {
            return string.Empty;
        }

        return value.Trim()
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static string NormalizeGuestKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.Trim()
            .ToLowerInvariant()
            .Replace("actor:", string.Empty)
            .Replace("root:", string.Empty)
            .Replace("projected:", string.Empty)
            .Replace("walker:", string.Empty)
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("'", string.Empty);

        if (normalized.StartsWith("guest0", StringComparison.OrdinalIgnoreCase))
        {
            int index = "guest".Length;

            while (index < normalized.Length - 1 && normalized[index] == '0')
            {
                normalized = normalized.Remove(index, 1);
            }
        }

        return normalized;
    }

    private static string GetObjectPath(Transform target)
    {
        return target != null ? GuestScaleCalibrationStore.GetStableSceneObjectPath(target) : "-";
    }

    private static void MarkDirty(Component component)
    {
        if (component == null)
        {
            return;
        }

        EditorUtility.SetDirty(component);
        MarkSceneDirty(component.transform);
    }

    private static void MarkSceneDirty(Transform transform)
    {
        if (transform != null && transform.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(transform.gameObject.scene);
        }
    }

    private sealed class GuestCandidate
    {
        public GuestCandidate(
            RoomProjectedEntity projectedEntity,
            RoomPersonWalker2D walker,
            ActorRoomState actor,
            Transform root,
            string controllerType,
            string roomId,
            Vector2 footPoint,
            string guestKey,
            string label)
        {
            ProjectedEntity = projectedEntity;
            Walker = walker;
            Actor = actor;
            Root = root;
            ControllerType = controllerType;
            RoomId = string.IsNullOrWhiteSpace(roomId) ? string.Empty : roomId.Trim();
            FootPoint = footPoint;
            GuestKey = guestKey ?? string.Empty;
            labelOverride = label ?? string.Empty;
        }

        public RoomProjectedEntity ProjectedEntity { get; }
        public RoomPersonWalker2D Walker { get; }
        public ActorRoomState Actor { get; }
        public Transform Root { get; }
        public string ControllerType { get; }
        public string RoomId { get; }
        public Vector2 FootPoint { get; }
        public string GuestKey { get; }
        public Transform AutoScaleRoot { get; set; }
        public Transform AutoBoundsRoot { get; set; }
        public Transform PrimaryVisual { get; set; }
        public bool IsSeated => Actor != null ? Actor.IsSeated : ProjectedEntity != null ? ProjectedEntity.IsGuestSeated() : Walker != null && Walker.IsGuestSeated();
        public string Label => !string.IsNullOrWhiteSpace(labelOverride)
            ? $"{labelOverride} ({ControllerType})"
            : Root != null ? $"{Root.name} ({ControllerType})" : ControllerType;

        private readonly string labelOverride;
    }

    private readonly struct KnownGuestDefinition
    {
        public KnownGuestDefinition(string guestKey, string shortName, string displayName, string[] aliases)
        {
            GuestKey = guestKey;
            ShortName = shortName;
            DisplayName = displayName;
            Aliases = aliases ?? Array.Empty<string>();
        }

        public string GuestKey { get; }
        public string ShortName { get; }
        public string DisplayName { get; }
        public string[] Aliases { get; }

        public bool Matches(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = NormalizeGuestKey(value);

            for (int i = 0; i < Aliases.Length; i++)
            {
                if (string.Equals(normalized, NormalizeGuestKey(Aliases[i]), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return string.Equals(normalized, NormalizeGuestKey(GuestKey), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, NormalizeGuestKey(DisplayName), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, NormalizeGuestKey(ShortName), StringComparison.OrdinalIgnoreCase);
        }
    }

    private readonly struct CalibrationMetrics
    {
        public CalibrationMetrics(string currentVisualHeight, string butlerStandingHeight, string targetGuestHeight)
        {
            CurrentVisualHeight = currentVisualHeight;
            ButlerStandingHeight = butlerStandingHeight;
            TargetGuestHeight = targetGuestHeight;
        }

        public string CurrentVisualHeight { get; }
        public string ButlerStandingHeight { get; }
        public string TargetGuestHeight { get; }
    }
}

public static class GuestScaleOverrideAudit
{
    private const string ReportPath = "Assets/Editor/Reports/GuestScaleOverrideAudit.md";

    [MenuItem("Tools/Characters/Guest Scale Override Audit")]
    public static void RunMenuAudit()
    {
        WriteReport();
    }

    public static GuestScaleOverrideAuditSummary WriteReport()
    {
        GuestScaleOverrideAuditSummary summary = BuildReport(out string report);
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, report);
        AssetDatabase.Refresh();
        Debug.Log(
            $"[GuestScaleOverrideAudit] Wrote {ReportPath}\n" +
            $"RoomProjectedEntity override entries: {summary.RoomProjectedEntityOverrideEntries}\n" +
            $"Drawing Room override entries: {summary.DrawingRoomOverrideEntries}\n" +
            $"Dining Room override entries: {summary.DiningRoomOverrideEntries}\n" +
            $"Entrance / Grand Entrance Hall override entries: {summary.EntranceOverrideEntries}\n" +
            $"CharacterVisualProfile non-1 height multipliers: {summary.NonOneCharacterHeightMultipliers}\n" +
            $"RoomPersonWalker2D custom scale entries: {summary.CustomWalkerScaleEntries}\n" +
            $"ActorRoomState room-stage scale entries: {summary.ActorRoomStageScaleEntries}\n" +
            $"guest visual roots with non-1 scale: {summary.NonOneGuestVisualRootScales}");
        return summary;
    }

    public static GuestScaleOverrideAuditSummary BuildReport(out string report)
    {
        GuestScaleOverrideAuditSummary summary = new GuestScaleOverrideAuditSummary();
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("# Guest Scale Override Audit");
        builder.AppendLine();

        RoomProjectedEntity[] entities = UnityEngine.Object.FindObjectsByType<RoomProjectedEntity>(FindObjectsInactive.Include);
        RoomPersonWalker2D[] walkers = UnityEngine.Object.FindObjectsByType<RoomPersonWalker2D>(FindObjectsInactive.Include);
        ActorRoomState[] actors = UnityEngine.Object.FindObjectsByType<ActorRoomState>(FindObjectsInactive.Include);
        PointClickPlayerMovement[] butlers = UnityEngine.Object.FindObjectsByType<PointClickPlayerMovement>(FindObjectsInactive.Include);

        List<string> drawingOverrides = new List<string>();
        List<string> diningOverrides = new List<string>();
        List<string> entranceOverrides = new List<string>();
        Dictionary<string, int> guestRooms = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        builder.AppendLine("## RoomProjectedEntity");
        builder.AppendLine($"RoomProjectedEntity components: {CountLoaded(entities)}");

        for (int i = 0; i < entities.Length; i++)
        {
            RoomProjectedEntity entity = entities[i];

            if (!IsLoadedSceneObject(entity))
            {
                continue;
            }

            ActorRoomState actor = entity.GetComponentInParent<ActorRoomState>(true);
            RoomPersonWalker2D walker = entity.GetComponent<RoomPersonWalker2D>();
            bool looksLikeGuestEntity = walker != null || GuestScaleCalibrationWindow.LooksLikeGuestScaleTarget(entity.gameObject, actor);
            SerializedProperty overrides = new SerializedObject(entity).FindProperty("roomVisualScaleOverrides");
            int entryCount = overrides != null && overrides.isArray ? overrides.arraySize : 0;
            summary.RoomProjectedEntityOverrideEntries += entryCount;

            if (entryCount > 0)
            {
                summary.NonEmptyRoomProjectedEntityOverrideEntries += entryCount;
            }

            for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
            {
                SerializedProperty item = overrides.GetArrayElementAtIndex(entryIndex);
                string overrideRoomId = item.FindPropertyRelative("roomId")?.stringValue ?? string.Empty;
                Vector3 scale = item.FindPropertyRelative("visualRootScale")?.vector3Value ?? Vector3.one;
                string line = $"- {GetObjectPath(entity.transform)} room={Format(overrideRoomId)} scale={scale}";

                if (!looksLikeGuestEntity)
                {
                    continue;
                }

                if (SameRoom(overrideRoomId, "Drawing Room"))
                {
                    drawingOverrides.Add(line);
                    summary.DrawingRoomOverrideEntries++;
                }
                else if (SameRoom(overrideRoomId, "Dining Room"))
                {
                    diningOverrides.Add(line);
                    summary.DiningRoomOverrideEntries++;
                }
                else if (SameRoom(overrideRoomId, "Entrance") || SameRoom(overrideRoomId, "Grand Entrance Hall"))
                {
                    entranceOverrides.Add(line);
                    summary.EntranceOverrideEntries++;
                }
            }

            if (looksLikeGuestEntity)
            {
                TrackGuestRoom(entity.TryResolveGuestRoomAndFootPoint(out string roomId, out _) ? roomId : string.Empty, guestRooms);
            }
        }

        builder.AppendLine($"RoomProjectedEntity roomVisualScaleOverrides: {summary.RoomProjectedEntityOverrideEntries}");
        builder.AppendLine($"Non-empty roomVisualScaleOverrides: {summary.NonEmptyRoomProjectedEntityOverrideEntries}");
        AppendList(builder, "Drawing Room Guest Scale Overrides", drawingOverrides);
        AppendList(builder, "Dining Room Guest Scale Overrides", diningOverrides);
        AppendList(builder, "Entrance / Grand Entrance Hall Guest Scale Overrides", entranceOverrides);

        builder.AppendLine("## CharacterVisualProfile Assets");
        string[] profileGuids = AssetDatabase.FindAssets("t:CharacterVisualProfile");
        for (int i = 0; i < profileGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(profileGuids[i]);
            CharacterVisualProfile profile = AssetDatabase.LoadAssetAtPath<CharacterVisualProfile>(path);

            if (profile == null)
            {
                continue;
            }

            summary.CharacterVisualProfileAssets++;

            if (!Mathf.Approximately(profile.HeightScaleMultiplier, 1f))
            {
                summary.NonOneCharacterHeightMultipliers++;
            }

            builder.AppendLine($"- {path}: {profile.CharacterId} heightScaleMultiplier={profile.HeightScaleMultiplier:0.###}");
        }

        builder.AppendLine();
        builder.AppendLine("## RoomPersonWalker2D");
        for (int i = 0; i < walkers.Length; i++)
        {
            RoomPersonWalker2D walker = walkers[i];

            if (!IsLoadedSceneObject(walker))
            {
                continue;
            }

            float nearScale = GetSerializedFloat(walker, "nearScale");
            float farScale = GetSerializedFloat(walker, "farScale");

            if (!Mathf.Approximately(nearScale, 1f) || !Mathf.Approximately(farScale, 0.42f))
            {
                summary.CustomWalkerScaleEntries++;
            }

            walker.TryResolveGuestRoomAndFootPoint(out string roomId, out _);
            TrackGuestRoom(roomId, guestRooms);
            builder.AppendLine($"- {GetObjectPath(walker.transform)} nearScale={nearScale:0.###} farScale={farScale:0.###}");
        }

        builder.AppendLine();
        builder.AppendLine("## ActorRoomState Room-Stage Scaling");
        for (int i = 0; i < actors.Length; i++)
        {
            ActorRoomState actor = actors[i];

            if (!IsLoadedSceneObject(actor) || !LooksLikeGuestActor(actor))
            {
                continue;
            }

            actor.TryResolveGuestRoomAndFootPoint(out string roomId, out _);
            TrackGuestRoom(roomId, guestRooms);

            if (actor.ScaleWithRoomStageMotion)
            {
                summary.ActorRoomStageScaleEntries++;
                builder.AppendLine($"- {GetObjectPath(actor.transform)} actorId={actor.ActorId} room={Format(actor.CurrentRoomId)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Guest Visual Roots");
        AppendVisualRoots(builder, entities, walkers, actors, ref summary);

        builder.AppendLine();
        builder.AppendLine("## Butler Calibration Rooms");
        HashSet<string> calibratedRooms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < butlers.Length; i++)
        {
            PointClickPlayerMovement butler = butlers[i];

            if (!IsLoadedSceneObject(butler))
            {
                continue;
            }

            List<string> roomIds = new List<string>();
            butler.GetButlerScaleOverrideRoomIds(roomIds);

            for (int roomIndex = 0; roomIndex < roomIds.Count; roomIndex++)
            {
                string roomId = roomIds[roomIndex];
                butler.TryGetButlerRoomScaleOverride(roomId, out PointClickPlayerMovement.ButlerRoomScaleOverrideData data);
                calibratedRooms.Add(NormalizeRoomName(roomId));
                builder.AppendLine($"- {butler.name}: {roomId} front={data.FrontFinalLocalScaleY:0.###} back={data.BackFinalLocalScaleY:0.###} complete={data.IsComplete}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Rooms With Guests But No Butler Calibration");
        foreach (KeyValuePair<string, int> pair in guestRooms)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || calibratedRooms.Contains(NormalizeRoomName(pair.Key)))
            {
                continue;
            }

            summary.RoomsWithGuestsButNoButlerCalibration++;
            builder.AppendLine($"- {pair.Key}: {pair.Value} guest scale target(s)");
        }

        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine($"RoomProjectedEntity override entries: {summary.RoomProjectedEntityOverrideEntries}");
        builder.AppendLine($"Drawing Room override entries: {summary.DrawingRoomOverrideEntries}");
        builder.AppendLine($"Dining Room override entries: {summary.DiningRoomOverrideEntries}");
        builder.AppendLine($"Entrance / Grand Entrance Hall override entries: {summary.EntranceOverrideEntries}");
        builder.AppendLine($"CharacterVisualProfile non-1 height multipliers: {summary.NonOneCharacterHeightMultipliers}");
        builder.AppendLine($"RoomPersonWalker2D custom scale entries: {summary.CustomWalkerScaleEntries}");
        builder.AppendLine($"ActorRoomState room-stage scale entries: {summary.ActorRoomStageScaleEntries}");
        builder.AppendLine($"guest visual roots with non-1 scale: {summary.NonOneGuestVisualRootScales}");

        report = builder.ToString();
        return summary;
    }

    private static void AppendVisualRoots(
        StringBuilder builder,
        RoomProjectedEntity[] entities,
        RoomPersonWalker2D[] walkers,
        ActorRoomState[] actors,
        ref GuestScaleOverrideAuditSummary summary)
    {
        HashSet<Transform> seen = new HashSet<Transform>();

        for (int i = 0; i < entities.Length; i++)
        {
            RoomProjectedEntity entity = entities[i];

            if (IsLoadedSceneObject(entity))
            {
                ActorRoomState actor = entity.GetComponentInParent<ActorRoomState>(true);
                RoomPersonWalker2D walker = entity.GetComponent<RoomPersonWalker2D>();

                if (walker != null || GuestScaleCalibrationWindow.LooksLikeGuestScaleTarget(entity.gameObject, actor))
                {
                    AppendVisualRoot(builder, entity.GetGuestScaleRoot(), entity.name, seen, ref summary);
                }
            }
        }

        for (int i = 0; i < walkers.Length; i++)
        {
            RoomPersonWalker2D walker = walkers[i];

            if (IsLoadedSceneObject(walker))
            {
                AppendVisualRoot(builder, walker.GetGuestScaleRoot(), walker.name, seen, ref summary);
            }
        }

        for (int i = 0; i < actors.Length; i++)
        {
            ActorRoomState actor = actors[i];

            if (IsLoadedSceneObject(actor) && LooksLikeGuestActor(actor))
            {
                AppendVisualRoot(builder, actor.GetGuestScaleRoot(), actor.ActorId, seen, ref summary);
            }
        }
    }

    private static void AppendVisualRoot(
        StringBuilder builder,
        Transform root,
        string label,
        HashSet<Transform> seen,
        ref GuestScaleOverrideAuditSummary summary)
    {
        if (root == null || seen.Contains(root))
        {
            return;
        }

        seen.Add(root);
        bool nonOne =
            !Mathf.Approximately(Mathf.Abs(root.localScale.x), 1f) ||
            !Mathf.Approximately(Mathf.Abs(root.localScale.y), 1f) ||
            !Mathf.Approximately(Mathf.Abs(root.lossyScale.x), 1f) ||
            !Mathf.Approximately(Mathf.Abs(root.lossyScale.y), 1f);

        if (nonOne)
        {
            summary.NonOneGuestVisualRootScales++;
        }

        builder.AppendLine($"- {label}: {GetObjectPath(root)} localScale={root.localScale} lossyScale={root.lossyScale}");
    }

    private static int CountLoaded<T>(T[] components) where T : Component
    {
        int count = 0;

        for (int i = 0; i < components.Length; i++)
        {
            if (IsLoadedSceneObject(components[i]))
            {
                count++;
            }
        }

        return count;
    }

    private static void AppendList(StringBuilder builder, string heading, List<string> rows)
    {
        builder.AppendLine();
        builder.AppendLine($"## {heading}");

        if (rows.Count == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            builder.AppendLine(rows[i]);
        }
    }

    private static void TrackGuestRoom(string roomId, Dictionary<string, int> guestRooms)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return;
        }

        string clean = roomId.Trim();
        guestRooms.TryGetValue(clean, out int count);
        guestRooms[clean] = count + 1;
    }

    private static float GetSerializedFloat(UnityEngine.Object target, string propertyName)
    {
        SerializedProperty property = new SerializedObject(target).FindProperty(propertyName);
        return property != null && property.propertyType == SerializedPropertyType.Float ? property.floatValue : 0f;
    }

    private static bool IsLoadedSceneObject(Component component)
    {
        return component != null &&
            component.gameObject != null &&
            component.gameObject.scene.IsValid() &&
            component.gameObject.scene.isLoaded;
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
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static string Format(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<empty>" : value.Trim();
    }

    private static string GetObjectPath(Transform target)
    {
        return target != null ? GuestScaleCalibrationStore.GetStableSceneObjectPath(target) : "-";
    }
}

public struct GuestScaleOverrideAuditSummary
{
    public int RoomProjectedEntityOverrideEntries;
    public int NonEmptyRoomProjectedEntityOverrideEntries;
    public int DrawingRoomOverrideEntries;
    public int DiningRoomOverrideEntries;
    public int EntranceOverrideEntries;
    public int CharacterVisualProfileAssets;
    public int NonOneCharacterHeightMultipliers;
    public int CustomWalkerScaleEntries;
    public int ActorRoomStageScaleEntries;
    public int NonOneGuestVisualRootScales;
    public int RoomsWithGuestsButNoButlerCalibration;
}
