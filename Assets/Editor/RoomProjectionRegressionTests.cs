using System;
using System.IO;
using NUnit.Framework;
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
    private const string GuestScaleCalibrationStorePath = "Assets/Scripts/Characters/GuestScaleCalibrationStore.cs";
    private const string GuestButlerScaleHarmonizerPath = "Assets/Scripts/Characters/GuestButlerScaleHarmonizer.cs";
    private const string GuestScaleCalibrationWindowPath = "Assets/Editor/GuestScaleCalibrationWindow.cs";
    private const string GuestButlerScaleToolPath = "Assets/Editor/GuestButlerScaleTool.cs";

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
        Assert.That(movementText, Does.Contain("usesRoomProfileScale ? depthScale : fallbackRelativeScale"), "Room profiles should apply absolute room scale while the old fields keep relative authored-scale fallback behavior.");
        Assert.That(movementText, Does.Contain("depthScale / Mathf.Max(0.0001f, authoredPerspectiveScaleReference)"), "The original point-click fallback scaling math should remain available.");
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
    public void ButlerCalibrationStoresFinalLocalScaleValues()
    {
        string movementText = File.ReadAllText(PointClickPlayerMovementPath);
        string windowText = File.ReadAllText(ButlerRoomScaleCalibrationWindowPath);

        Assert.That(movementText, Does.Contain("hasButlerCalibrationBaseLocalScale"), "Butler calibration should serialize whether a stable base local scale has been captured.");
        Assert.That(movementText, Does.Contain("butlerCalibrationBaseLocalScale"), "Butler calibration should serialize the stable base local scale used by editor previews and runtime.");
        Assert.That(movementText, Does.Contain("EnsureButlerCalibrationBaseScale"), "The editor should be able to capture the Butler base scale once without preview drift.");
        Assert.That(movementText, Does.Contain("CaptureCurrentTransformAsButlerCalibrationBaseScale"), "The advanced editor path should be able to intentionally recapture the setup base scale.");
        Assert.That(movementText, Does.Contain("RestoreButlerCalibrationBaseScalePreview"), "The editor should be able to restore the visible preview to the stored base scale.");
        Assert.That(movementText, Does.Contain("ButlerRoomScaleOverride"), "The controllable Butler should store dedicated per-room front/back calibration.");
        Assert.That(movementText, Does.Contain("frontRoomLocalFootY"), "The Butler front endpoint should store room-local foot Y.");
        Assert.That(movementText, Does.Contain("backRoomLocalFootY"), "The Butler back endpoint should store room-local foot Y.");
        Assert.That(movementText, Does.Contain("frontFinalLocalScaleY"), "The Butler front endpoint should store final visible localScale.y, not a hidden multiplier.");
        Assert.That(movementText, Does.Contain("backFinalLocalScaleY"), "The Butler back endpoint should store final visible localScale.y, not a hidden multiplier.");
        Assert.That(movementText, Does.Contain("BuildButlerFinalLocalScale"), "Final localScale construction should be centralized.");
        Assert.That(movementText, Does.Contain("TryGetButlerCalibrationContext"), "Saving, preview, and runtime should share one room-local foot point path.");
        Assert.That(movementText, Does.Contain("TryGetRoomLocalFootPointForButlerCalibration"), "Butler calibration should convert through the selected room's coordinate frame instead of whatever room stage is currently active.");
        Assert.That(movementText, Does.Contain("TryGetRoomStageLocalPointForRoom(roomId"), "Butler save/test/runtime scale evaluation should use the chosen room id for room-local foot Y.");
        Assert.That(movementText, Does.Contain("preferCurrentTransformInEditMode"), "Edit Mode calibration should be able to use the current visible Transform instead of stale runtime logicalPosition.");
        Assert.That(movementText, Does.Contain("TryEvaluateButlerCalibratedFinalLocalScale"), "Runtime should evaluate saved final local scale directly.");
        Assert.That(windowText, Does.Contain("Preview Final Butler Local Scale"), "The editor should expose final visible local scale instead of multiplier wording.");
        Assert.That(windowText, Does.Not.Contain("Preview Butler Size Here"), "The old ambiguous multiplier wording should be gone.");
    }

    [Test]
    public void ButlerSavedScalePreviewIsIdempotent()
    {
        GameObject player = CreatePointClickPlayer("player", new Vector3(1f, 1f, 1f));

        try
        {
            PointClickPlayerMovement movement = player.GetComponent<PointClickPlayerMovement>();
            movement.CaptureCurrentTransformAsButlerCalibrationBaseScale();
            movement.SetButlerFrontFinalLocalScaleForRoom("Drawing Room", -6f, 1.93f, false);
            movement.SetButlerBackFinalLocalScaleForRoom("Drawing Room", -2f, 1.12f, false);

            Vector3 firstScale = Vector3.zero;

            for (int i = 0; i < 5; i++)
            {
                Assert.That(
                    movement.TryEvaluateButlerFinalLocalScaleForRoomAtY("Drawing Room", -4f, out _, out _, out float finalLocalScaleY),
                    Is.True);

                movement.ApplyButlerFinalLocalScalePreview(finalLocalScaleY);

                if (i == 0)
                {
                    firstScale = player.transform.localScale;
                }
                else
                {
                    Assert.That(player.transform.localScale.x, Is.EqualTo(firstScale.x).Within(0.0001f));
                    Assert.That(player.transform.localScale.y, Is.EqualTo(firstScale.y).Within(0.0001f));
                    Assert.That(player.transform.localScale.z, Is.EqualTo(firstScale.z).Within(0.0001f));
                }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void ButlerRuntimeUsesFinalLocalScaleDirectly()
    {
        GameObject player = CreatePointClickPlayer("player", new Vector3(2f, 2f, 5f));

        try
        {
            PointClickPlayerMovement movement = player.GetComponent<PointClickPlayerMovement>();
            movement.CaptureCurrentTransformAsButlerCalibrationBaseScale();
            movement.SetButlerFrontFinalLocalScaleForRoom("Drawing Room", -6f, 1.93f, false);
            movement.SetButlerBackFinalLocalScaleForRoom("Drawing Room", -2f, 1.12f, false);

            Assert.That(
                movement.TryEvaluateButlerFinalLocalScaleForRoomAtY("Drawing Room", -6f, out Vector3 frontScale, out _, out float frontFinalLocalScaleY),
                Is.True);
            Assert.That(
                movement.TryEvaluateButlerFinalLocalScaleForRoomAtY("Drawing Room", -2f, out Vector3 backScale, out _, out float backFinalLocalScaleY),
                Is.True);

            Assert.That(frontFinalLocalScaleY, Is.EqualTo(1.93f).Within(0.0001f));
            Assert.That(backFinalLocalScaleY, Is.EqualTo(1.12f).Within(0.0001f));
            Assert.That(frontScale.y, Is.EqualTo(1.93f).Within(0.0001f));
            Assert.That(backScale.y, Is.EqualTo(1.12f).Within(0.0001f));
            Assert.That(Mathf.Abs(frontScale.y - (2f * 1.93f)), Is.GreaterThan(0.0001f), "Saved final localScale.y must not be multiplied by the authored/base scale again.");
            Assert.That(Mathf.Abs(backScale.y - (2f * 1.12f)), Is.GreaterThan(0.0001f), "Saved final localScale.y must not be multiplied by the authored/base scale again.");
            Assert.That(frontScale.z, Is.EqualTo(5f).Within(0.0001f), "Calibrated Butler scale should preserve the reference Z scale.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void ButlerUncalibratedRoomsKeepOldScaleBehavior()
    {
        string movementText = File.ReadAllText(PointClickPlayerMovementPath);
        string applyScaleBody = ExtractMethodBody(movementText, "private void ApplyPerspectiveScale");

        Assert.That(applyScaleBody, Does.Contain("TryEvaluateButlerCalibratedFinalLocalScale"), "Complete Butler room calibration should be able to replace the old depth scale.");
        Assert.That(applyScaleBody, Does.Contain("CalculateExistingPerspectiveScale() * currentRoomStageScaleRatio"), "Rooms without Butler calibration should keep the old profile/fallback behavior.");
        Assert.That(applyScaleBody, Does.Contain("authoredLocalScale.x * scale"), "Uncalibrated rooms should still scale from the original authored local scale.");
        Assert.That(movementText, Does.Contain("usesRoomProfileScale ? depthScale : fallbackRelativeScale"), "The original profile-vs-fallback scale path should remain available for uncalibrated rooms.");
    }

    [Test]
    public void ButlerScaleWindowUsesClearLabels()
    {
        Assert.That(File.Exists(ButlerRoomScaleCalibrationWindowPath), Is.True, "Butler room scale calibration should be available as a focused window.");

        string windowText = File.ReadAllText(ButlerRoomScaleCalibrationWindowPath);

        Assert.That(windowText, Does.Contain("[MenuItem(\"Tools/Butler/Room Scale Calibration\")]"), "The calibration window should have the requested Tools menu path.");
        Assert.That(windowText, Does.Contain("Butler Room Scale"), "The calibration window should use the requested title.");
        Assert.That(windowText, Does.Contain("Butler / Player Object"), "The object field should make clear that the scene player object is expected.");
        Assert.That(windowText, Does.Contain("Find Scene Player"), "The calibration window should expose a safe player finder.");
        Assert.That(windowText, Does.Contain("Preview Final Butler Local Scale"), "The primary workflow should have one obvious final local scale preview control.");
        Assert.That(windowText, Does.Contain("SAVE FRONT: Current Position + Current Visible Size"), "The front save button should store current position plus current visible size.");
        Assert.That(windowText, Does.Contain("SAVE BACK: Current Position + Current Visible Size"), "The back save button should store current position plus current visible size.");
        Assert.That(windowText, Does.Contain("PREVIEW SAVED SIZE AT CURRENT POSITION (does not save)"), "Saved interpolation preview should be explicit and non-destructive.");
        Assert.That(windowText, Does.Contain("RESTORE BUTLER START TRANSFORM"), "Designers should be able to restore the calibration session start Transform before saving the scene.");
        Assert.That(windowText, Does.Contain("Advanced / Reset Tools"), "Destructive reset/delete controls should be hidden in an advanced foldout.");
        Assert.That(windowText, Does.Contain("RESET THIS ROOM TO OLD DEFAULT SCALE VALUES"), "The old perspective initializer should be renamed as a destructive reset.");
        Assert.That(windowText, Does.Contain("DELETE THIS ROOM"), "Deleting room calibration should be explicit and destructive.");
        Assert.That(windowText, Does.Contain("This overwrites the saved FRONT and BACK calibration for"), "Resetting old defaults should require confirmation.");
        Assert.That(windowText, Does.Contain("Stop Play Mode to save calibration."), "The window should make Play Mode read-only for saving.");
        Assert.That(windowText, Does.Not.Contain("Preview FRONT Size"), "The old separate front preview button should be removed from the primary workflow.");
        Assert.That(windowText, Does.Not.Contain("Preview BACK Size"), "The old separate back preview button should be removed from the primary workflow.");
        Assert.That(windowText, Does.Not.Contain("Initialize Room From Existing Perspective"), "The old initializer label should not appear in the primary workflow.");
        Assert.That(windowText, Does.Not.Contain("Clear Saved Scale For This Room"), "The old clear label should not appear in the primary workflow.");
    }

    [Test]
    public void PointClickPlayerMovementInspectorOpensButlerScaleWindow()
    {
        Assert.That(File.Exists(PointClickPlayerMovementEditorPath), Is.True, "PointClickPlayerMovement should have a focused inspector extension for Butler calibration.");

        string editorText = File.ReadAllText(PointClickPlayerMovementEditorPath);

        Assert.That(editorText, Does.Contain("[CustomEditor(typeof(PointClickPlayerMovement))]"), "The inspector extension should target PointClickPlayerMovement.");
        Assert.That(editorText, Does.Contain("Open Butler Room Scale Calibration Window"), "The inspector should send designers to the safer step-based calibration window.");
        Assert.That(editorText, Does.Not.Contain("Preview FRONT Size"), "The inspector should not keep the old confusing endpoint preview workflow.");
        Assert.That(editorText, Does.Not.Contain("Preview BACK Size"), "The inspector should not keep the old confusing endpoint preview workflow.");
    }

    [Test]
    public void GuestProjectionUsesButlerScaleApiWithoutOwningCalibrationData()
    {
        string projectionText = File.ReadAllText(RoomProjectedEntityPath);
        string projectionEditorText = File.ReadAllText(RoomProjectedEntityEditorPath);

        Assert.That(projectionText, Does.Contain("TryEvaluateButlerCharacterScale"), "Guest projection should consume the shared Butler scale evaluator.");
        Assert.That(projectionText, Does.Not.Contain("ButlerRoomScaleOverride"), "Guest projection should not serialize its own Butler room calibration data.");
        Assert.That(projectionText, Does.Not.Contain("butlerCalibrationBaseLocalScale"), "Guest projection should not copy the Butler base scale field.");
        Assert.That(projectionEditorText, Does.Not.Contain("Preview Final Butler Local Scale"), "Guest projection editor should not expose Butler-only calibration workflow.");
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

    [Test]
    public void GuestScaleCalibrationStoreSavesManualEntry()
    {
        GameObject storeObject = new GameObject("ButlerScaleStore");
        GameObject guestObject = new GameObject("GuestManualRoot");
        RoomProjectedEntity projection = guestObject.AddComponent<RoomProjectedEntity>();
        GuestScaleCalibrationStore store = storeObject.AddComponent<GuestScaleCalibrationStore>();

        try
        {
            GuestScaleCalibrationEntry saved = store.SetCalibrationForGuest(
                projection,
                "Drawing Room",
                GuestPose.Seated,
                guestObject.transform,
                guestObject.transform,
                0.68f,
                1.12f);

            Assert.That(saved.pose, Is.EqualTo(GuestPose.Seated));
            Assert.That(saved.heightRatioToButlerStanding, Is.EqualTo(0.68f).Within(0.0001f));
            Assert.That(saved.manualFineTuneMultiplier, Is.EqualTo(1.12f).Within(0.0001f));
            Assert.That(store.TryGetCalibrationForGuest(projection, "Drawing Room", null, out GuestScaleCalibrationEntry byComponent), Is.True);
            Assert.That(byComponent, Is.EqualTo(saved));
            Assert.That(store.TryGetCalibrationForGuest(null, "Drawing Room", guestObject.transform, out GuestScaleCalibrationEntry byRoot), Is.True);
            Assert.That(byRoot, Is.EqualTo(saved));
            Assert.That(store.GetAllEntries().Count, Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guestObject);
            UnityEngine.Object.DestroyImmediate(storeObject);
        }
    }

    [Test]
    public void ManualGuestCalibrationOverridesOldRoomVisualScale()
    {
        string harmonizerText = File.ReadAllText(GuestButlerScaleHarmonizerPath);
        string prepareBody = ExtractMethodBody(harmonizerText, "private void PrepareControllerForFinalHumanScale");
        string targetHeightBody = ExtractMethodBody(harmonizerText, "private bool TryGetTargetScreenHeight");

        Assert.That(harmonizerText, Does.Contain("ResolveManualCalibration"), "The final harmonizer should look up saved manual guest calibration entries.");
        Assert.That(harmonizerText, Does.Contain("TryResolveManualVisualRoots"), "Saved manual roots should be applied before automatic visual-root selection.");
        Assert.That(prepareBody, Does.Contain("SetIgnoreRoomVisualScaleOverridesWhenUsingButlerRules(true"), "Old roomVisualScaleOverrides should be bypassed when final human scaling is active.");
        Assert.That(targetHeightBody.IndexOf("target.ManualCalibration != null", StringComparison.Ordinal), Is.LessThan(targetHeightBody.IndexOf("target.RelativeHeightMultiplier", StringComparison.Ordinal)), "Manual target height should be resolved before the old relative-height path.");
    }

    [Test]
    public void ManualGuestCalibrationBypassesCharacterHeightMultiplier()
    {
        string projectionText = File.ReadAllText(RoomProjectedEntityPath);
        string harmonizerText = File.ReadAllText(GuestButlerScaleHarmonizerPath);
        string prepareBody = ExtractMethodBody(harmonizerText, "private void PrepareControllerForFinalHumanScale");
        string manualBranch = ExtractBetween(harmonizerText, "if (target.ManualCalibration != null)", "float relativeHeight =");

        Assert.That(projectionText, Does.Contain("SetIgnoreVisualProfileHeightMultiplierWhenUsingButlerRules"), "Projection should expose the profile-height bypass.");
        Assert.That(prepareBody, Does.Contain("SetIgnoreVisualProfileHeightMultiplierWhenUsingButlerRules(true"), "Final human scaling should turn the profile-height bypass on.");
        Assert.That(manualBranch, Does.Not.Contain("HeightScaleMultiplier"), "Manual target height must not multiply by CharacterVisualProfile.HeightScaleMultiplier.");
        Assert.That(manualBranch, Does.Not.Contain("RelativeHeightMultiplier"), "Manual target height must not use the legacy relative-height multiplier path.");
    }

    [Test]
    public void RoomPersonWalkerManualCalibrationUsesTargetGraphic()
    {
        string windowText = File.ReadAllText(GuestScaleCalibrationWindowPath);
        string harmonizerText = File.ReadAllText(GuestButlerScaleHarmonizerPath);

        Assert.That(windowText, Does.Contain("Use TargetGraphic As Scale Root"), "The manual tool should expose the requested targetGraphic root shortcut.");
        Assert.That(windowText, Does.Contain("candidate.Walker.TargetGraphic.rectTransform"), "The targetGraphic shortcut should use the Graphic RectTransform as the scale root.");
        Assert.That(harmonizerText, Does.Contain("walker.TargetGraphic.rectTransform"), "Automatic walker root detection should continue to prefer targetGraphic when present.");
    }

    [Test]
    public void SeatedGuestManualCalibrationUsesSeatedRatio()
    {
        GameObject storeObject = new GameObject("Store");
        GameObject actorObject = new GameObject("DiningGuestActor");
        GuestScaleCalibrationStore store = storeObject.AddComponent<GuestScaleCalibrationStore>();
        ActorRoomState actor = actorObject.AddComponent<ActorRoomState>();

        try
        {
            actor.SetActorId("Guest_Dining_01");
            actor.SetSeated(true);
            GuestScaleCalibrationEntry entry = store.GetOrCreateEntry(null, null, actor, "Dining Room", actorObject.transform);

            Assert.That(entry.pose, Is.EqualTo(GuestPose.Seated));
            Assert.That(entry.heightRatioToButlerStanding, Is.EqualTo(0.68f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actorObject);
            UnityEngine.Object.DestroyImmediate(storeObject);
        }
    }

    [Test]
    public void ManualCalibrationDoesNotMultiplyByGuestBaseScale()
    {
        string harmonizerText = File.ReadAllText(GuestButlerScaleHarmonizerPath);
        string manualBranch = ExtractBetween(harmonizerText, "if (target.ManualCalibration != null)", "float relativeHeight =");

        Assert.That(manualBranch, Does.Contain("standingHumanReferenceScreenHeight"), "Manual calibration should target a visible height derived from the Butler standing reference.");
        Assert.That(manualBranch, Does.Contain("sample.NormalizedScale"), "Manual calibration should still use the Butler room/depth scale.");
        Assert.That(manualBranch, Does.Contain("manualFineTune"), "Manual calibration should apply the saved fine-tune multiplier.");
        Assert.That(manualBranch, Does.Not.Contain("localScale"), "Manual target height must not multiply by the guest's base localScale.");
    }

    [Test]
    public void ManualCalibrationProofChangesGuestWithoutButlerRoomCalibration()
    {
        string harmonizerText = File.ReadAllText(GuestButlerScaleHarmonizerPath);
        string targetHeightBody = ExtractMethodBody(harmonizerText, "private bool TryGetTargetScreenHeight");

        Assert.That(targetHeightBody.IndexOf("if (proofMode)", StringComparison.Ordinal), Is.LessThan(targetHeightBody.IndexOf("if (!hasSample)", StringComparison.Ordinal)), "Proof mode should run before missing Butler calibration fails the target.");
        Assert.That(targetHeightBody, Does.Contain("baseline.ScreenHeight * Mathf.Max(0.001f, debugGuestScaleMultiplier)"), "Proof mode should use captured baseline visible height.");
        Assert.That(targetHeightBody, Does.Contain("currentScreenHeight * Mathf.Max(0.001f, debugGuestScaleMultiplier)"), "Proof mode should still change guests without a saved proof baseline.");
    }

    [Test]
    public void FinalHarmonizerUsesManualCalibrationWhenPresent()
    {
        string harmonizerText = File.ReadAllText(GuestButlerScaleHarmonizerPath);

        Assert.That(harmonizerText, Does.Contain("GuestScaleCalibrationStore store"), "The final harmonizer should resolve the manual calibration store.");
        Assert.That(harmonizerText, Does.Contain("ResolveManualCalibration"), "The final harmonizer should query manual calibration entries.");
        Assert.That(harmonizerText, Does.Contain("TryResolveManualVisualRoots"), "The final harmonizer should honor saved manual roots.");
        Assert.That(harmonizerText, Does.Contain("ManualCalibration = manualCalibration"), "Each scale target should carry the resolved manual entry.");
        Assert.That(harmonizerText, Does.Contain("target.ManualCalibration.manualFineTuneMultiplier"), "The final target height should use the saved fine-tune multiplier.");
    }

    [Test]
    public void ManualToolContainsRequiredButtons()
    {
        string windowText = File.ReadAllText(GuestScaleCalibrationWindowPath);
        string toolText = File.ReadAllText(GuestButlerScaleToolPath);

        Assert.That(windowText, Does.Contain("Auto Match Guest To Butler Here"));
        Assert.That(windowText, Does.Contain("Save Calibration For This Guest In This Room"));
        Assert.That(windowText, Does.Contain("Use Selected Object As Scale Root"));
        Assert.That(windowText, Does.Contain("Use Selected Object As Bounds Root"));
        Assert.That(windowText, Does.Contain("Capture Current Scale Root As Base"));
        Assert.That(windowText, Does.Contain("Restore Scale Root Base"));
        Assert.That(windowText, Does.Contain("KnownChapterGuests"), "The manual tool should offer known chapter guests even before runtime guest objects exist.");
        Assert.That(windowText, Does.Contain("GetButlerScaleOverrideRoomIds"), "The room dropdown should come from Butler calibration rooms, not only rooms where old guest scale overrides/components exist.");
        Assert.That(windowText, Does.Contain("AddNavigationRoomChoices"), "The room dropdown should include navigation/catalog rooms even when no guest is currently authored there.");
        Assert.That(windowText, Does.Contain("return new List<GuestCandidate>(candidates)"), "Selecting a room should not hide guests that are not currently placed in that room.");
        Assert.That(windowText, Does.Contain("LooksLikeGuestScaleTarget"), "Projected furniture/props should not be treated as guest calibration candidates just because they have RoomProjectedEntity.");
        Assert.That(toolText, Does.Contain("Open Manual Guest Scale Calibration"));
        Assert.That(toolText, Does.Contain("Proof 50% Using Manual Roots"));
        Assert.That(toolText, Does.Contain("Proof 150% Using Manual Roots"));
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

    private static GameObject CreatePointClickPlayer(string name, Vector3 localScale)
    {
        GameObject player = new GameObject(name);
        player.transform.localScale = localScale;
        player.AddComponent<Rigidbody2D>();
        player.AddComponent<Animator>();
        player.AddComponent<PointClickPlayerMovement>();
        return player;
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

    private static string ExtractBetween(string sourceText, string startText, string endText)
    {
        int startIndex = sourceText.IndexOf(startText, StringComparison.Ordinal);
        Assert.That(startIndex, Is.GreaterThanOrEqualTo(0), $"Could not find start text '{startText}'.");

        int endIndex = sourceText.IndexOf(endText, startIndex, StringComparison.Ordinal);
        Assert.That(endIndex, Is.GreaterThan(startIndex), $"Could not find end text '{endText}'.");

        return sourceText.Substring(startIndex, endIndex - startIndex);
    }
}
