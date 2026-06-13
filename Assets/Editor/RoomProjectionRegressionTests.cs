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
    private const string RoomProjectedEntityEditorPath = "Assets/Editor/RoomProjectedEntityEditor.cs";

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
        Assert.That(projectionText, Does.Contain("SetApplySorting"), "Scene-specific compositions need to be able to keep projection position/scale while owning renderer sorting.");
        Assert.That(projectionText, Does.Contain("public bool AppliesSorting"), "Tests and debugging should be able to see when projection sorting is intentionally disabled.");
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
