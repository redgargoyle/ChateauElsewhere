using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class RoomProjectionRegressionTests
{
    private const string Chapter1ArrivalControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs";
    private const string ActorRoomStatePath = "Assets/Scripts/Story/ActorRoomState.cs";
    private const string PointClickPlayerMovementPath = "Assets/Scripts/PointClickPlayerMovement.cs";
    private const string RoomPerspectiveProfilePath = "Assets/Scripts/Characters/RoomPerspectiveProfile.cs";
    private const string RoomProjectedEntityPath = "Assets/Scripts/Characters/RoomProjectedEntity.cs";
    private const string RoomPersonWalkerPath = "Assets/Scripts/Characters/RoomPersonWalker2D.cs";
    private const string WorldYSortPath = "Assets/Scripts/Characters/WorldYSortSpriteRenderer.cs";
    private const string NPCWaypointMoverPath = "Assets/Scripts/Story/NPCWaypointMover.cs";
    private const string RoomPerspectiveProfileEditorPath = "Assets/Editor/RoomPerspectiveProfileEditor.cs";
    private const string RoomProjectionCalibrationWindowPath = "Assets/Editor/RoomProjectionCalibrationWindow.cs";
    private const string PlayModeLayoutCaptureWindowPath = "Assets/Editor/PlayModeLayoutCaptureWindow.cs";
    private const string RoomProjectedEntityEditorPath = "Assets/Editor/RoomProjectedEntityEditor.cs";
    private const string PointClickPlayerMovementEditorPath = "Assets/Editor/PointClickPlayerMovementEditor.cs";
    private const string ButlerRoomScaleCalibrationWindowPath = "Assets/Editor/ButlerRoomScaleCalibrationWindow.cs";

    [Test]
    public void SameRoomLocalFootYProducesSameRoomScaleForProjectedEntities()
    {
        RoomPerspectiveProfile profile = CreatePerspectiveProfile();
        RoomProjectedEntity left = CreateProjectedEntity("LeftGuest", profile, null, new Vector2(-140f, -40f));
        RoomProjectedEntity right = CreateProjectedEntity("RightGuest", profile, null, new Vector2(140f, -40f));

        try
        {
            Assert.That(left.CurrentScale, Is.EqualTo(right.CurrentScale).Within(0.0001f));
        }
        finally
        {
            DestroyEntity(left);
            DestroyEntity(right);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void NearerFootYProducesLargerScaleThanFartherFootY()
    {
        RoomPerspectiveProfile profile = CreatePerspectiveProfile();

        try
        {
            float nearScale = profile.GetScale(new Vector2(0f, -120f));
            float farScale = profile.GetScale(new Vector2(0f, 120f));

            Assert.That(nearScale, Is.GreaterThan(farScale));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void RoomPerspectiveProfileExposesRoomWideYScaleControls()
    {
        RoomPerspectiveProfile profile = CreatePerspectiveProfile();

        try
        {
            profile.SetDepthYRange(-220f, 180f);
            profile.SetScaleEndpoints(1.18f, 0.64f);

            Assert.That(profile.NearFootY, Is.EqualTo(-220f));
            Assert.That(profile.FarFootY, Is.EqualTo(180f));
            Assert.That(profile.NearScale, Is.EqualTo(1.18f).Within(0.0001f));
            Assert.That(profile.FarScale, Is.EqualTo(0.64f).Within(0.0001f));
            Assert.That(profile.GetScale(new Vector2(0f, -220f)), Is.EqualTo(1.18f).Within(0.0001f));
            Assert.That(profile.GetScale(new Vector2(0f, 180f)), Is.EqualTo(0.64f).Within(0.0001f));

            profile.ApplyScaleMultiplier(1.1f);

            Assert.That(profile.NearScale, Is.EqualTo(1.18f * 1.1f).Within(0.0001f));
            Assert.That(profile.FarScale, Is.EqualTo(0.64f * 1.1f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void RoomPerspectiveProfileEditorExposesArtistFriendlyYScaleControls()
    {
        string profileText = File.ReadAllText(RoomPerspectiveProfilePath);
        string editorText = File.ReadAllText(RoomPerspectiveProfileEditorPath);
        string calibrationWindowText = File.ReadAllText(RoomProjectionCalibrationWindowPath);

        Assert.That(profileText, Does.Contain("SetDepthYRange"), "Rooms should expose the y range that drives perspective scaling.");
        Assert.That(profileText, Does.Contain("SetScaleEndpoints"), "Rooms should expose near/front and far/back scale endpoints.");
        Assert.That(profileText, Does.Contain("ApplyScaleMultiplier"), "Rooms should support making everyone larger or smaller without changing the y-depth relationship.");
        Assert.That(editorText, Does.Contain("[CustomEditor(typeof(RoomPerspectiveProfile))]"), "Room perspective profiles should have a focused inspector.");
        Assert.That(editorText, Does.Contain("EditorGUILayout.FloatField(\"Front/Near Scale\""), "Artists should be able to tune the front-room scale with one number.");
        Assert.That(editorText, Does.Contain("EditorGUILayout.FloatField(\"Back/Far Scale\""), "Artists should be able to tune the back-room scale with one number.");
        Assert.That(editorText, Does.Contain("EditorGUILayout.FloatField(\"Uniform Multiplier\""), "Artists should be able to make the whole room larger or smaller while preserving y scaling.");
        Assert.That(editorText, Does.Contain("RefreshProjectedEntitiesUsing"), "Changing the room profile should refresh the projected guests visible in the open scene.");
        Assert.That(editorText, Does.Contain("RefreshPointClickMovementsUsing"), "Changing the room profile should also refresh old point-click characters visible in the open scene.");
        Assert.That(editorText, Does.Contain("RefreshRoomPersonWalkersUsing"), "Changing the room profile should also refresh standalone RoomPersonWalker2D characters.");
        Assert.That(calibrationWindowText, Does.Contain("Create Perspective Profiles For Scene Rooms"), "Room profile setup should be available from the Tools menu.");
        Assert.That(calibrationWindowText, Does.Contain("Create/Assign Profiles For Scene Rooms"), "The calibration window should make one-click per-room profile assignment available.");
        Assert.That(calibrationWindowText, Does.Contain("room.SetPerspectiveProfile(profile)"), "Creating room profiles should wire them into RoomContentGroup.");
    }

    [Test]
    public void PlayModeLayoutCaptureCanPersistRuntimeAnchorTuning()
    {
        string captureWindowText = File.ReadAllText(PlayModeLayoutCaptureWindowPath);

        Assert.That(captureWindowText, Does.Contain("PlayModeStateChange.EnteredEditMode"), "Captured play-mode edits should be reapplied after Unity returns to edit mode.");
        Assert.That(captureWindowText, Does.Contain("SessionState.SetString"), "Captured transform data should survive the play/edit transition within the editor session.");
        Assert.That(captureWindowText, Does.Contain("Capture Dining Seat Anchors"), "Dining room seats should have a focused capture action.");
        Assert.That(captureWindowText, Does.Contain("Ch2_DiningSeat_"), "The dining seat capture should target the Chapter 2 dining anchors.");
        Assert.That(captureWindowText, Does.Contain("RectTransform"), "UI anchors should persist anchored position and rect transform data, not only world transform data.");
        Assert.That(captureWindowText, Does.Contain("Apply + Save Scenes"), "Artists should have a one-click path to apply captured data and save the edited scene.");
        Assert.That(captureWindowText, Does.Contain("Application.isPlaying"), "The apply action should not try to write edit-time scene data while Unity is still in Play Mode.");
        Assert.That(captureWindowText, Does.Contain("Stop Play Mode And Apply"), "The tool should make the play-to-edit apply handoff explicit.");
    }

    [Test]
    public void PointClickMovementCanUseRoomPerspectiveProfileScaling()
    {
        string movementText = File.ReadAllText(PointClickPlayerMovementPath);
        string applyScaleBody = ExtractMethodBody(movementText, "private void ApplyPerspectiveScale");

        Assert.That(movementText, Does.Contain("useRoomPerspectiveProfileScale"), "Old point-click characters should be able to opt into room profile scaling.");
        Assert.That(movementText, Does.Contain("TryGetCurrentRoomPerspectiveProfile"), "PointClickPlayerMovement should resolve the profile for the active room.");
        Assert.That(movementText, Does.Contain("TryGetActiveRoomStageLocalPoint"), "PointClickPlayerMovement should convert world/logical points back into room-local profile coordinates.");
        Assert.That(movementText, Does.Contain("TryFindRoomContentForRoom"), "Profile lookup should use the same active room name path as walkable boundaries.");
        Assert.That(movementText, Does.Contain("RefreshPerspectiveScaleNow"), "Editor profile changes should be able to refresh point-click character scale immediately.");
        Assert.That(movementText, Does.Contain("UsesPerspectiveProfile"), "Editor refreshes should only target point-click characters using the edited profile.");
        Assert.That(applyScaleBody, Does.Contain("usesRoomProfileScale ? depthScale : fallbackRelativeScale"), "Room profiles should apply absolute room scale while the old fields keep relative authored-scale fallback behavior.");
        Assert.That(applyScaleBody, Does.Contain("depthScale / Mathf.Max(0.0001f, authoredPerspectiveScaleReference)"), "The original point-click fallback scaling math should remain available.");
    }

    [Test]
    public void SortingOrderFollowsRoomFootYDepth()
    {
        RoomPerspectiveProfile profile = CreatePerspectiveProfile();

        try
        {
            int nearOrder = profile.GetSortingOrder(new Vector2(0f, -120f));
            int farOrder = profile.GetSortingOrder(new Vector2(0f, 120f));

            Assert.That(nearOrder, Is.GreaterThan(farOrder));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void CharacterVisualProfileHeightMultiplierAffectsProjectedScalePredictably()
    {
        RoomPerspectiveProfile roomProfile = CreatePerspectiveProfile();
        CharacterVisualProfile visualProfile = ScriptableObject.CreateInstance<CharacterVisualProfile>();
        visualProfile.Configure("TallGuest", 1.25f, 320f, 240f, new Vector2(0.5f, 0f), 0, 1, -2);
        Vector2 footPoint = new Vector2(0f, 0f);
        RoomProjectedEntity entity = CreateProjectedEntity("TallGuest", roomProfile, visualProfile, footPoint);

        try
        {
            Assert.That(entity.CurrentScale, Is.EqualTo(roomProfile.GetScale(footPoint) * 1.25f).Within(0.0001f));
        }
        finally
        {
            DestroyEntity(entity);
            UnityEngine.Object.DestroyImmediate(visualProfile);
            UnityEngine.Object.DestroyImmediate(roomProfile);
        }
    }

    [Test]
    public void ProjectedGuestsExposeRoomSpecificVisualScaleOverrides()
    {
        string projectionText = File.ReadAllText(RoomProjectedEntityPath);
        string editorText = File.ReadAllText(RoomProjectedEntityEditorPath);

        Assert.That(projectionText, Does.Contain("roomVisualScaleOverrides"), "Projected guests should store per-room visual scale overrides.");
        Assert.That(projectionText, Does.Contain("SetVisualRootScaleForRoom"), "The editor needs a safe API for room-specific scale edits.");
        Assert.That(projectionText, Does.Contain("GetAuthoredVisualRootScaleForCurrentRoom"), "Projection scale should use a room-specific base scale before falling back to the global authored scale.");
        Assert.That(projectionText, Does.Contain("GetCurrentVisualScaleRoomKey"), "Edit Mode room dropdown selection should be able to preview a selected room scale.");
        Assert.That(editorText, Does.Contain("[CustomEditor(typeof(RoomProjectedEntity))]"), "RoomProjectedEntity should expose the room scale dropdown in the Inspector.");
        Assert.That(editorText, Does.Contain("EditorGUILayout.Popup(\"Room\""), "The per-room scale workflow should use a room dropdown.");
        Assert.That(editorText, Does.Contain("EditorGUILayout.Vector3Field(\"Visual Root Scale\""), "The selected room's transform scale values should be directly editable.");
        Assert.That(editorText, Does.Contain("CaptureCurrentVisualRootScaleForRoom"), "Artists should be able to capture manual Scene/Inspector resizing into the selected room override.");
    }

    [Test]
    public void PointClickMovementExposesButlerRoomFrontBackScaleOverrides()
    {
        string movementText = File.ReadAllText(PointClickPlayerMovementPath);

        Assert.That(movementText, Does.Contain("butlerRoomScaleOverrides"), "The controllable Butler should store per-room scale calibration on PointClickPlayerMovement.");
        Assert.That(movementText, Does.Contain("ButlerRoomScaleOverride"), "The Butler scale override should be a dedicated serialized data type.");
        Assert.That(movementText, Does.Contain("frontFootY"), "The Butler override should store the front room-local foot Y.");
        Assert.That(movementText, Does.Contain("frontScale"), "The Butler override should store the front scale endpoint.");
        Assert.That(movementText, Does.Contain("backFootY"), "The Butler override should store the back room-local foot Y.");
        Assert.That(movementText, Does.Contain("backScale"), "The Butler override should store the back scale endpoint.");
        Assert.That(movementText, Does.Contain("SetButlerFrontScaleForRoom"), "The editor needs a safe API for setting the front endpoint.");
        Assert.That(movementText, Does.Contain("SetButlerBackScaleForRoom"), "The editor needs a safe API for setting the back endpoint.");
        Assert.That(movementText, Does.Contain("RemoveButlerScaleOverrideForRoom"), "Artists should be able to remove a room calibration.");
        Assert.That(movementText, Does.Contain("GetButlerScaleForRoomAtY"), "The runtime should expose the per-room interpolation helper for tests and editor previews.");
        Assert.That(movementText, Does.Contain("TryGetCurrentButlerRoomLocalFootPoint"), "The editor should be able to capture the Butler's current room-local foot point.");
        Assert.That(movementText, Does.Contain("CaptureCurrentButlerScaleMultiplier"), "The editor should be able to capture current scale without room-stage multiplier.");
    }

    [Test]
    public void PointClickMovementKeepsFallbackScaleWhenNoButlerOverride()
    {
        string movementText = File.ReadAllText(PointClickPlayerMovementPath);
        string applyScaleBody = ExtractMethodBody(movementText, "private void ApplyPerspectiveScale");

        Assert.That(movementText, Does.Contain("CalculateExistingPerspectiveScale"), "The old profile/fallback calculation should remain available when a room lacks Butler calibration.");
        Assert.That(movementText, Does.Contain("TryEvaluateButlerRoomScaleOverride"), "Butler calibration should be an opt-in branch before falling back to existing scaling.");
        Assert.That(applyScaleBody, Does.Contain("TryEvaluateButlerRoomScaleOverride(out float calibratedScale)"), "Complete Butler overrides should replace the old depth scale for that room only.");
        Assert.That(applyScaleBody, Does.Contain("CalculateExistingPerspectiveScale()"), "Rooms without complete Butler overrides should preserve old profile/fallback behavior.");
        Assert.That(movementText, Does.Contain("usesRoomProfileScale ? depthScale : fallbackRelativeScale"), "The original profile-vs-fallback math should remain present.");
    }

    [Test]
    public void PointClickMovementUsesButlerOverrideWithoutChangingGuestProjection()
    {
        string movementText = File.ReadAllText(PointClickPlayerMovementPath);
        string projectionText = File.ReadAllText(RoomProjectedEntityPath);

        Assert.That(movementText, Does.Contain("TryEvaluateButlerRoomScaleOverride"), "PointClickPlayerMovement should own Butler-only room scale evaluation.");
        Assert.That(movementText, Does.Contain("currentRoomStageScaleRatio"), "Butler calibration should preserve the existing room-stage scale multiplier.");
        Assert.That(projectionText, Does.Not.Contain("butlerRoomScaleOverrides"), "Guest projection should not receive Butler-specific serialized data.");
        Assert.That(projectionText, Does.Not.Contain("ButlerRoomScaleOverride"), "RoomProjectedEntity should stay guest/projected-actor focused.");
    }

    [Test]
    public void PointClickPlayerMovementEditorExposesButlerCalibrationWorkflow()
    {
        Assert.That(File.Exists(PointClickPlayerMovementEditorPath), Is.True, "PointClickPlayerMovement should have a focused inspector for Butler room scale calibration.");

        string editorText = File.ReadAllText(PointClickPlayerMovementEditorPath);

        Assert.That(editorText, Does.Contain("[CustomEditor(typeof(PointClickPlayerMovement))]"), "The Butler calibration controls should live on the player movement inspector.");
        Assert.That(editorText, Does.Contain("Butler Room Scale Calibration"), "The inspector should clearly label Butler-only scale calibration.");
        Assert.That(editorText, Does.Contain("Preview FRONT Size"), "Artists should be able to preview the front endpoint scale without saving.");
        Assert.That(editorText, Does.Contain("Save FRONT: Current Position + Scale"), "Artists should be able to save the front endpoint from the current player position.");
        Assert.That(editorText, Does.Contain("Preview BACK Size"), "Artists should be able to preview the back endpoint scale without saving.");
        Assert.That(editorText, Does.Contain("Save BACK: Current Position + Scale"), "Artists should be able to save the back endpoint from the current player position.");
        Assert.That(editorText, Does.Contain("Preview Saved Room Scaling Here"), "Artists should be able to preview interpolation at the player's current depth.");
        Assert.That(editorText, Does.Contain("Initialize Room From Existing Perspective"), "Artists should be able to seed room calibration from the old perspective behavior.");
        Assert.That(editorText, Does.Contain("Clear Saved Scale For This Room"), "Artists should be able to remove a stored calibration.");
        Assert.That(editorText, Does.Contain("PLAY MODE: preview only"), "The inspector should make play-mode calibration preview-only.");
    }

    [Test]
    public void ButlerRoomScaleCalibrationWindowExists()
    {
        Assert.That(File.Exists(ButlerRoomScaleCalibrationWindowPath), Is.True, "Butler scale calibration should also be available as a focused tool window.");

        string windowText = File.ReadAllText(ButlerRoomScaleCalibrationWindowPath);

        Assert.That(windowText, Does.Contain("[MenuItem(\"Tools/Butler/Room Scale Calibration\")]"), "The calibration window should have the requested Tools menu path.");
        Assert.That(windowText, Does.Contain("Find Scene Player"), "The calibration window should use a clear, player-specific finder label.");
        Assert.That(windowText, Does.Contain("Butler / Player Object"), "The object field should make clear that the scene player object is expected.");
        Assert.That(windowText, Does.Contain("Previous Room"), "The calibration window should support stepping to the previous room.");
        Assert.That(windowText, Does.Contain("Next Room"), "The calibration window should support stepping to the next room.");
        Assert.That(windowText, Does.Contain("Preview FRONT Size"), "The calibration window should preview the front endpoint scale without saving.");
        Assert.That(windowText, Does.Contain("Save FRONT: Current Position + Scale"), "The calibration window should capture the front endpoint.");
        Assert.That(windowText, Does.Contain("Preview BACK Size"), "The calibration window should preview the back endpoint scale without saving.");
        Assert.That(windowText, Does.Contain("Save BACK: Current Position + Scale"), "The calibration window should capture the back endpoint.");
        Assert.That(windowText, Does.Contain("Preview Saved Room Scaling Here"), "The calibration window should preview saved room interpolation at the current position.");
        Assert.That(windowText, Does.Contain("PLAY MODE: preview only"), "The calibration window should warn that play mode is preview-only.");
        Assert.That(windowText, Does.Contain("Detected PointClickPlayerMovement Objects"), "The calibration window should expose candidate debugging.");
        Assert.That(windowText, Does.Contain("No safe player object found"), "The calibration window should refuse to silently select guest-looking candidates.");
        Assert.That(windowText, Does.Contain("DrawScaleSliderNumericNudge"), "The scale UI should include slider, numeric, and nudge controls.");
        Assert.That(windowText, Does.Contain("HorizontalSlider"), "The scale UI should include a slider.");
        Assert.That(windowText, Does.Contain("\"-0.05\""), "The scale UI should include a negative nudge button.");
        Assert.That(windowText, Does.Contain("\"+0.05\""), "The scale UI should include a positive nudge button.");
    }

    [Test]
    public void ButlerRoomScaleFinderPrefersPlayerNamedObjectOverGuests()
    {
        GameObject previousSelection = Selection.activeGameObject;
        GameObject guest = new GameObject("Guest 3");
        GameObject player = new GameObject("player");

        try
        {
            guest.AddComponent<Rigidbody2D>();
            guest.AddComponent<Animator>();
            PointClickPlayerMovement guestMovement = guest.AddComponent<PointClickPlayerMovement>();
            guest.AddComponent<RoomProjectedEntity>();

            player.AddComponent<Rigidbody2D>();
            player.AddComponent<Animator>();
            PointClickPlayerMovement playerMovement = player.AddComponent<PointClickPlayerMovement>();

            Selection.activeGameObject = guest;

            PointClickPlayerMovement found = ButlerRoomScaleCalibrationWindow.FindScenePlayer();

            Assert.That(found, Is.Not.Null, "A scene object named player should be considered a safe player candidate.");
            Assert.That(string.Equals(found.gameObject.name, "player", StringComparison.OrdinalIgnoreCase), Is.True, "The finder should prefer a scene object named player over guest-looking PointClickPlayerMovement objects.");
            Assert.That(found, Is.Not.EqualTo(guestMovement), "The finder should not silently select Guest objects when a safe player object exists.");
            Assert.That(ButlerRoomScaleCalibrationWindow.IsSafePlayerObjectForSaving(guestMovement), Is.False, "Guest-looking objects should be blocked from saving Butler calibration.");
            Assert.That(ButlerRoomScaleCalibrationWindow.IsSafePlayerObjectForSaving(playerMovement), Is.True, "The scene object named player should be safe for saving Butler calibration.");
        }
        finally
        {
            Selection.activeGameObject = previousSelection;
            UnityEngine.Object.DestroyImmediate(guest);
            UnityEngine.Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void ProjectedEntityPreservesAuthoredVisualScaleWhileNormalizingRoot()
    {
        RoomPerspectiveProfile profile = CreatePerspectiveProfile();
        GameObject root = new GameObject("ScaledPrefabRoot");
        root.transform.localScale = new Vector3(3f, 0.25f, 7f);
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = new Vector3(1.4f, 0.75f, 2f);
        visual.AddComponent<SpriteRenderer>();
        RoomProjectedEntity entity = root.AddComponent<RoomProjectedEntity>();
        entity.SetVisualRoot(visual.transform);
        entity.SetRoomProfile(profile);
        entity.SetRoomLocalFootPoint(new Vector2(0f, -40f));

        try
        {
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(visual.transform.localScale.x, Is.EqualTo(1.4f * entity.CurrentScale).Within(0.0001f));
            Assert.That(visual.transform.localScale.y, Is.EqualTo(0.75f * entity.CurrentScale).Within(0.0001f));
            Assert.That(visual.transform.localScale.z, Is.EqualTo(2f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void ProjectedEntityUsesEditedVisualScaleAsProjectionBase()
    {
        RoomPerspectiveProfile profile = CreatePerspectiveProfile();
        GameObject root = new GameObject("EditedVisualRoot");
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.AddComponent<SpriteRenderer>();
        RoomProjectedEntity entity = root.AddComponent<RoomProjectedEntity>();
        entity.SetVisualRoot(visual.transform);
        entity.SetRoomProfile(profile);
        entity.SetRoomLocalFootPoint(new Vector2(0f, -40f));

        try
        {
            Vector3 editedScale = new Vector3(2f, 0.5f, 1.25f);
            visual.transform.localScale = editedScale;

            entity.ApplyProjection();

            Assert.That(visual.transform.localScale.x, Is.EqualTo(editedScale.x * entity.CurrentScale).Within(0.0001f));
            Assert.That(visual.transform.localScale.y, Is.EqualTo(editedScale.y * entity.CurrentScale).Within(0.0001f));
            Assert.That(visual.transform.localScale.z, Is.EqualTo(editedScale.z).Within(0.0001f));

            entity.SetRoomLocalFootPoint(new Vector2(0f, -120f));

            Assert.That(visual.transform.localScale.x, Is.EqualTo(editedScale.x * entity.CurrentScale).Within(0.0001f));
            Assert.That(visual.transform.localScale.y, Is.EqualTo(editedScale.y * entity.CurrentScale).Within(0.0001f));
            Assert.That(visual.transform.localScale.z, Is.EqualTo(editedScale.z).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void ProjectedEntityUsesEditedRootScaleWhenVisualRootIsActorRoot()
    {
        RoomPerspectiveProfile profile = CreatePerspectiveProfile();
        GameObject root = new GameObject("SameRootProjectedActor");
        root.transform.localScale = new Vector3(1.5f, 0.8f, 1.1f);
        root.AddComponent<SpriteRenderer>();
        RoomProjectedEntity entity = root.AddComponent<RoomProjectedEntity>();
        entity.SetRoomProfile(profile);
        entity.SetRoomLocalFootPoint(new Vector2(0f, -40f));

        try
        {
            Assert.That(root.transform.localScale.x, Is.EqualTo(1.5f * entity.CurrentScale).Within(0.0001f));
            Assert.That(root.transform.localScale.y, Is.EqualTo(0.8f * entity.CurrentScale).Within(0.0001f));
            Assert.That(root.transform.localScale.z, Is.EqualTo(1.1f).Within(0.0001f));

            Vector3 editedScale = new Vector3(1.75f, 1.2f, 0.9f);
            root.transform.localScale = editedScale;

            entity.ApplyProjection();

            Assert.That(root.transform.localScale.x, Is.EqualTo(editedScale.x * entity.CurrentScale).Within(0.0001f));
            Assert.That(root.transform.localScale.y, Is.EqualTo(editedScale.y * entity.CurrentScale).Within(0.0001f));
            Assert.That(root.transform.localScale.z, Is.EqualTo(editedScale.z).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void ActorRoomStateVisibilityRemainsSeparateFromProjection()
    {
        RoomPerspectiveProfile profile = CreatePerspectiveProfile();
        GameObject actor = new GameObject("ProjectedStoryActor");
        SpriteRenderer renderer = actor.AddComponent<SpriteRenderer>();
        RoomProjectedEntity projection = actor.AddComponent<RoomProjectedEntity>();
        ActorRoomState actorState = actor.AddComponent<ActorRoomState>();
        projection.SetRoomProfile(profile);
        projection.SetRoomLocalFootPoint(new Vector2(0f, -40f));

        try
        {
            actorState.SetVisibleByChapterState(false);

            Assert.That(renderer.enabled, Is.False);
            Assert.That(actorState.Projection, Is.EqualTo(projection));
            Assert.That(projection.RoomLocalFootPoint, Is.EqualTo(new Vector2(0f, -40f)));
            Assert.That(projection.HasUsableProfile, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actor);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void ProjectionIsInactiveOutsideActorProfileRoom()
    {
        RoomPerspectiveProfile profile = CreatePerspectiveProfile();
        GameObject actor = new GameObject("ProjectedStoryActor");
        RoomProjectedEntity projection = actor.AddComponent<RoomProjectedEntity>();
        ActorRoomState actorState = actor.AddComponent<ActorRoomState>();
        projection.SetRoomProfile(profile);

        try
        {
            actorState.SetCurrentRoom("Grand Entrance Hall");
            Assert.That(projection.HasUsableProfile, Is.True);
            Assert.That(projection.IsProjectionActive, Is.False);

            actorState.SetCurrentRoom("Drawing Room");
            Assert.That(projection.IsProjectionActive, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actor);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void Chapter1ProjectedGuestPlacementKeepsSafeFallbacks()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string placeBody = ExtractMethodBody(controllerText, "PlaceGuestAt");
        string bindBody = ExtractMethodBody(controllerText, "BindGuestToRoomStagePoint");
        string coatSortingBody = ExtractMethodBody(controllerText, "ConfigureAssignedCoatSorting");
        string projectedPlacementBody = ExtractMethodBody(controllerText, "TryPlaceProjectedGuestAtTarget");

        Assert.That(placeBody, Does.Match(@"TryPlaceProjectedGuestAtTarget\(guestState, target\)[\s\S]*TryGetAnchoredPositionForGuestTarget"), "Projected guests should use room-local foot placement before UI fallback.");
        Assert.That(placeBody, Does.Contain("TryGetWorldPositionForGuestTarget"), "Non-projected world-space guests should keep the visible-anchor world conversion fallback.");
        Assert.That(placeBody, Does.Contain("ActorState.PlaceAt(target)"), "ActorRoomState placement fallback should remain for non-projected guests.");
        Assert.That(bindBody, Does.Contain("HasActiveProjection(guestState)"), "Projected guests should not also receive ActorRoomState room-stage scale binding.");
        Assert.That(coatSortingBody, Does.Contain("projection.GetSortingOrder"), "Coats should sort relative to projected foot-point order when projection is active.");
        Assert.That(projectedPlacementBody, Does.Contain("projection.TrySetRoomLocalFootPointFromTarget(target)"), "Chapter 1 should set logical projected foot points when a room target is projectable.");
    }

    [Test]
    public void ActorRoomStateSkipsRoomStageScaleBindingWhenProjectionIsActive()
    {
        string actorRoomStateText = File.ReadAllText(ActorRoomStatePath);
        string projectionText = File.ReadAllText("Assets/Scripts/Characters/RoomProjectedEntity.cs");
        string placeBody = ExtractMethodBody(actorRoomStateText, "PlaceAt");
        string shouldFollowBody = ExtractMethodBody(actorRoomStateText, "ShouldFollowRoomStageMotion");
        string projectedScaleBody = ExtractMethodBody(projectionText, "ApplyProjectedScale");

        Assert.That(actorRoomStateText, Does.Contain("RoomProjectedEntity"), "ActorRoomState should know how to detect projection without owning perspective math.");
        Assert.That(placeBody, Does.Match(@"projection\.CanProjectTarget\(target\)[\s\S]*projection\.TrySetRoomLocalFootPointFromTarget\(target\)[\s\S]*projection\.IsProjectionActive"), "ActorRoomState should seed projected foot points before checking whether projection is already active.");
        Assert.That(shouldFollowBody, Does.Contain("!HasActiveProjection()"), "Projection should own visual scale and room-stage positioning when present.");
        Assert.That(actorRoomStateText, Does.Contain("projection.IsProjectionActive"), "ActorRoomState should only defer to projection in the matching projected room.");
        Assert.That(actorRoomStateText, Does.Contain("ApplyState()"), "ActorRoomState should continue to own visibility and interaction state.");
        Assert.That(projectionText, Does.Contain("GetRoomStageScaleMultiplier"), "Projected world-space guests should still scale with room-stage zoom.");
        Assert.That(projectedScaleBody, Does.Contain("currentScale * currentRoomStageScaleMultiplier"), "Projection should multiply room-depth scale by room-stage zoom without replacing the authored base scale.");
    }

    [Test]
    public void LegacyWalkerAndWorldYSortDeferToActiveProjection()
    {
        string walkerText = File.ReadAllText(RoomPersonWalkerPath);
        string walkerApplyBody = ExtractMethodBody(walkerText, "ApplyVisuals");
        string ySortText = File.ReadAllText(WorldYSortPath);
        string ySortApplyBody = ExtractMethodBody(ySortText, "ApplySorting");
        string waypointText = File.ReadAllText(NPCWaypointMoverPath);
        string projectedTargetBody = ExtractMethodBody(waypointText, "TryGetProjectedTarget");

        Assert.That(walkerApplyBody, Does.Contain("roomProjection.IsProjectionActive"), "RoomPersonWalker2D should detect active projection before writing visual transforms.");
        Assert.That(walkerApplyBody, Does.Contain("roomProjection.SetRoomLocalFootPoint"), "RoomPersonWalker2D should feed the shared foot point instead of duplicating projected scale/tint.");
        Assert.That(walkerText, Does.Contain("RoomPerspectiveProfile"), "Standalone RoomPersonWalker2D instances should be able to share room-wide depth scaling.");
        Assert.That(walkerText, Does.Contain("TryGetRoomPerspectiveProfile"), "Standalone RoomPersonWalker2D instances should resolve their room profile before falling back to local near/far values.");
        Assert.That(walkerText, Does.Contain("RefreshDepthVisualsNow"), "Editor profile changes should be able to refresh standalone RoomPersonWalker2D scale immediately.");
        Assert.That(walkerText, Does.Contain("UsesPerspectiveProfile"), "Editor refreshes should only target standalone walkers using the edited profile.");
        Assert.That(ySortApplyBody, Does.Contain("roomProjection.IsProjectionActive"), "WorldYSortSpriteRenderer should not fight projected sorting orders.");
        Assert.That(projectedTargetBody, Does.Contain("roomProjection.CanProjectTarget(target)"), "NPC waypoint movement should not project anchors from the wrong room profile.");
        Assert.That(projectedTargetBody, Does.Contain("roomProjection.IsProjectionActive"), "NPC waypoint movement should only use projected motion when projection owns the actor's current room.");
    }

    private static RoomPerspectiveProfile CreatePerspectiveProfile()
    {
        RoomPerspectiveProfile profile = ScriptableObject.CreateInstance<RoomPerspectiveProfile>();
        profile.Configure(
            "Drawing Room",
            new Vector2(1366f, 768f),
            -160f,
            160f,
            AnimationCurve.Linear(0f, 1f, 1f, 0.5f),
            null,
            1000,
            8000,
            AnimationCurve.Linear(0f, 1f, 1f, 0f));
        return profile;
    }

    private static RoomProjectedEntity CreateProjectedEntity(
        string name,
        RoomPerspectiveProfile roomProfile,
        CharacterVisualProfile visualProfile,
        Vector2 footPoint)
    {
        GameObject root = new GameObject(name);
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.AddComponent<SpriteRenderer>();
        RoomProjectedEntity entity = root.AddComponent<RoomProjectedEntity>();
        entity.SetVisualRoot(visual.transform);
        entity.SetRoomProfile(roomProfile);
        entity.SetVisualProfile(visualProfile);
        entity.SetRoomLocalFootPoint(footPoint);
        return entity;
    }

    private static void DestroyEntity(RoomProjectedEntity entity)
    {
        if (entity != null)
        {
            UnityEngine.Object.DestroyImmediate(entity.gameObject);
        }
    }

    private static string ExtractMethodBody(string sourceText, string methodName)
    {
        int methodIndex = sourceText.IndexOf(methodName, StringComparison.Ordinal);
        Assert.That(methodIndex, Is.GreaterThanOrEqualTo(0), $"Could not find method '{methodName}'.");

        int bodyStart = sourceText.IndexOf('{', methodIndex);
        Assert.That(bodyStart, Is.GreaterThanOrEqualTo(0), $"Could not find method body for '{methodName}'.");

        int depth = 0;

        for (int i = bodyStart; i < sourceText.Length; i++)
        {
            if (sourceText[i] == '{')
            {
                depth++;
            }
            else if (sourceText[i] == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return sourceText.Substring(bodyStart, i - bodyStart + 1);
                }
            }
        }

        Assert.Fail($"Could not find end of method body for '{methodName}'.");
        return string.Empty;
    }
}
