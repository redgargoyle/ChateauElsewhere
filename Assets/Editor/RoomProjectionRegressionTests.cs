using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class RoomProjectionRegressionTests
{
    private const string Chapter1ArrivalControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs";
    private const string ActorRoomStatePath = "Assets/Scripts/Story/ActorRoomState.cs";
    private const string RoomProjectedEntityPath = "Assets/Scripts/Characters/RoomProjectedEntity.cs";
    private const string RoomPersonWalkerPath = "Assets/Scripts/Characters/RoomPersonWalker2D.cs";
    private const string WorldYSortPath = "Assets/Scripts/Characters/WorldYSortSpriteRenderer.cs";
    private const string NPCWaypointMoverPath = "Assets/Scripts/Story/NPCWaypointMover.cs";
    private const string RoomPerspectiveProfileEditorPath = "Assets/Editor/RoomPerspectiveProfileEditor.cs";
    private const string RoomProjectionCalibrationWindowPath = "Assets/Editor/RoomProjectionCalibrationWindow.cs";
    private const string PlayModeLayoutCaptureWindowPath = "Assets/Editor/PlayModeLayoutCaptureWindow.cs";
    private const string RoomProjectedEntityEditorPath = "Assets/Editor/RoomProjectedEntityEditor.cs";
    private const string CharacterVisualProfilePath = "Assets/Scripts/Characters/CharacterVisualProfile.cs";

    [Test]
    public void RoomPerspectiveProfileRetainsDepthTintSortingAndPropScaleData()
    {
        RoomPerspectiveProfile profile = CreatePerspectiveProfile();

        try
        {
            Vector2 nearPoint = new Vector2(0f, -120f);
            Vector2 farPoint = new Vector2(0f, 120f);

            Assert.That(profile.GetDepth01(nearPoint), Is.LessThan(profile.GetDepth01(farPoint)));
            Assert.That(profile.GetScale(nearPoint), Is.GreaterThan(profile.GetScale(farPoint)));
            Assert.That(profile.GetSortingOrder(nearPoint), Is.GreaterThan(profile.GetSortingOrder(farPoint)));
            Assert.That(profile.GetTint(nearPoint), Is.Not.EqualTo(profile.GetTint(farPoint)));
            Assert.That(profile.GetShadowScale(nearPoint), Is.GreaterThan(0f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void RoomPerspectiveProfileEditorRefreshesProjectionAndWalkerPresentationOnly()
    {
        string editorText = File.ReadAllText(RoomPerspectiveProfileEditorPath);
        string calibrationText = File.ReadAllText(RoomProjectionCalibrationWindowPath);

        Assert.That(editorText, Does.Contain("RefreshProjectedEntitiesUsing"));
        Assert.That(editorText, Does.Contain("RefreshRoomPersonWalkersUsing"));
        Assert.That(editorText, Does.Not.Contain("PointClickPlayerMovement"));
        Assert.That(editorText, Does.Not.Contain("RefreshPointClickMovementsUsing"));
        Assert.That(calibrationText, Does.Contain("Create Perspective Profiles For Scene Rooms"));
        Assert.That(calibrationText, Does.Contain("Create/Assign Profiles For Scene Rooms"));
        Assert.That(calibrationText, Does.Contain("room.SetPerspectiveProfile(profile)"));
        Assert.That(calibrationText, Does.Contain("Prop Projection Scale"));
        Assert.That(calibrationText, Does.Not.Contain("Standard Adult"));
        Assert.That(calibrationText, Does.Not.Contain("Projected Adult Height"));
    }

    [Test]
    public void PlayModeLayoutCaptureCanPersistRuntimeAnchorTuning()
    {
        string captureWindowText = File.ReadAllText(PlayModeLayoutCaptureWindowPath);
        string captureItemBody = ExtractMethodBody(captureWindowText, "TryCreateCaptureItem");
        string applyCaptureBody = ExtractMethodBody(captureWindowText, "ApplyPendingCapture");
        string applyCaptureItemBody = ExtractMethodBody(captureWindowText, "ApplyCaptureItem");

        Assert.That(captureWindowText, Does.Contain("PlayModeStateChange.EnteredEditMode"));
        Assert.That(captureWindowText, Does.Contain("SessionState.SetString"));
        Assert.That(captureWindowText, Does.Contain("Capture Dining Seat Anchors"));
        Assert.That(captureWindowText, Does.Contain("Apply + Save Scenes"));
        Assert.That(captureItemBody, Does.Contain("IsProtectedEntranceGuestSpot(target)"));
        Assert.That(captureItemBody, Does.Contain("IsManagedCharacterTransform(target)"));
        Assert.That(applyCaptureBody, Does.Contain("IsManagedCharacterTransform(target)"));
        Assert.That(applyCaptureItemBody, Does.Match(@"IsManagedCharacterTransform\(target\)[\s\S]*return;[\s\S]*target\.localPosition ="));
        Assert.That(captureWindowText, Does.Match(@"IsManagedCharacterTransform\s*\([^)]*\)[\s\S]*GetComponentInParent<PointClickPlayerMovement>\(true\)[\s\S]*GetComponentInParent<ActorRoomState>\(true\)"));
    }

    [Test]
    public void FloorCharacterProjectionPreservesVisualScaleAndProjectsPosition()
    {
        RoomPerspectiveProfile profile = CreatePerspectiveProfile();
        GameObject root = new GameObject("ProjectedCharacter");
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        visual.transform.localScale = new Vector3(1.4f, 0.75f, 2f);
        RoomProjectedEntity entity = root.AddComponent<RoomProjectedEntity>();
        entity.SetVisualRoot(visual.transform);
        Vector3 authoredScale = visual.transform.localScale;

        try
        {
            entity.SetRoomLocalFootPoint(new Vector2(25f, -40f), false);
            entity.SetRoomProfile(profile);

            Assert.That(entity.Mode, Is.EqualTo(RoomProjectedEntity.ProjectionMode.FloorCharacter));
            Assert.That(visual.transform.localScale, Is.EqualTo(authoredScale));
            Assert.That(root.transform.localPosition.x, Is.EqualTo(25f).Within(0.001f));
            Assert.That(root.transform.localPosition.y, Is.EqualTo(-40f).Within(0.001f));
            Assert.That(renderer.sortingOrder, Is.EqualTo(entity.GetSortingOrder()));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void PropProjectionMayScaleItsVisualWithoutOpeningCharacterScalePath()
    {
        RoomPerspectiveProfile profile = CreatePerspectiveProfile();
        GameObject root = new GameObject("ProjectedProp");
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.AddComponent<SpriteRenderer>();
        visual.transform.localScale = new Vector3(1.4f, 0.75f, 2f);
        RoomProjectedEntity entity = root.AddComponent<RoomProjectedEntity>();
        SetProjectionMode(entity, RoomProjectedEntity.ProjectionMode.FloorProp);
        entity.SetVisualRoot(visual.transform);
        Vector3 baseScale = visual.transform.localScale;
        Vector2 footPoint = new Vector2(0f, -40f);

        try
        {
            entity.SetRoomLocalFootPoint(footPoint, false);
            entity.SetRoomProfile(profile);
            float expectedScale = profile.GetScale(footPoint);

            Assert.That(visual.transform.localScale.x, Is.EqualTo(baseScale.x * expectedScale).Within(0.0001f));
            Assert.That(visual.transform.localScale.y, Is.EqualTo(baseScale.y * expectedScale).Within(0.0001f));
            Assert.That(visual.transform.localScale.z, Is.EqualTo(baseScale.z).Within(0.0001f));

            string source = File.ReadAllText(RoomProjectedEntityPath);
            string scaleBody = ExtractMethodBody(source, "ShouldApplyPropProjectionScale");
            Assert.That(scaleBody, Does.Contain("applyScale"));
            Assert.That(source, Does.Contain("projectionMode == ProjectionMode.FloorProp"));
            Assert.That(source, Does.Not.Contain("GuestScaleParticipant"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void CharacterScaleProfileAndProjectionOverrideEditorAreRemoved()
    {
        string projectionText = File.ReadAllText(RoomProjectedEntityPath);
        string calibrationText = File.ReadAllText(RoomProjectionCalibrationWindowPath);

        Assert.That(File.Exists(CharacterVisualProfilePath), Is.False);
        Assert.That(File.Exists(RoomProjectedEntityEditorPath), Is.False);
        Assert.That(projectionText, Does.Not.Contain("CharacterVisualProfile"));
        Assert.That(projectionText, Does.Not.Contain("roomVisualScaleOverrides"));
        Assert.That(projectionText, Does.Not.Contain("ButlerCharacterScale"));
        Assert.That(calibrationText, Does.Not.Contain("CharacterVisualProfile"));
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
    public void ChapterOneProjectedPlacementPreservesPositionSortingAndCoatOffset()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string placeBody = ExtractMethodBody(controllerText, "PlaceGuestAt");
        string coatSortingBody = ExtractMethodBody(controllerText, "ConfigureAssignedCoatSorting");
        string projectedPlacementBody = ExtractMethodBody(controllerText, "TryPlaceProjectedGuestAtTarget");

        Assert.That(placeBody, Does.Contain("TryPlaceProjectedGuestAtTarget"));
        Assert.That(placeBody, Does.Contain("TryGetWorldPositionForGuestTarget"));
        Assert.That(placeBody, Does.Contain("ActorState.PlaceAt(target)"));
        Assert.That(coatSortingBody, Does.Contain("projection.GetSortingOrder"));
        Assert.That(coatSortingBody, Does.Contain("const int coatSortingOffset = 1"));
        Assert.That(coatSortingBody, Does.Not.Contain("VisualProfile"));
        Assert.That(projectedPlacementBody, Does.Contain("projection.TrySetRoomLocalFootPointFromTarget(target)"));
    }

    [Test]
    public void ActorRoomStateDefersProjectedPositionWithoutOwningScale()
    {
        string actorRoomStateText = File.ReadAllText(ActorRoomStatePath);
        string projectionText = File.ReadAllText(RoomProjectedEntityPath);
        string placeBody = ExtractMethodBody(actorRoomStateText, "PlaceAt");
        string shouldFollowBody = ExtractMethodBody(actorRoomStateText, "ShouldFollowRoomStageMotion");

        Assert.That(placeBody, Does.Match(@"projection\.CanProjectTarget\(target\)[\s\S]*projection\.TrySetRoomLocalFootPointFromTarget\(target\)[\s\S]*projection\.IsProjectionActive"));
        Assert.That(shouldFollowBody, Does.Contain("!HasActiveProjection()"));
        Assert.That(actorRoomStateText, Does.Contain("TryGetRoomLocalFootPoint"));
        Assert.That(actorRoomStateText, Does.Not.Contain("localScale"));
        Assert.That(projectionText, Does.Contain("GetRoomStageScaleMultiplier"));
    }

    [Test]
    public void WalkerAndWorldYSortDeferToActiveProjectionWithoutResizing()
    {
        string walkerText = File.ReadAllText(RoomPersonWalkerPath);
        string walkerApplyBody = ExtractMethodBody(walkerText, "ApplyVisuals");
        string ySortText = File.ReadAllText(WorldYSortPath);
        string ySortApplyBody = ExtractMethodBody(ySortText, "ApplySorting");
        string waypointText = File.ReadAllText(NPCWaypointMoverPath);
        string projectedTargetBody = ExtractMethodBody(waypointText, "TryGetProjectedTarget");

        Assert.That(walkerApplyBody, Does.Contain("roomProjection.IsProjectionActive"));
        Assert.That(walkerApplyBody, Does.Contain("roomProjection.SetRoomLocalFootPoint"));
        Assert.That(walkerText, Does.Contain("TryGetRoomPerspectiveProfile"));
        Assert.That(walkerText, Does.Contain("RefreshDepthVisualsNow"));
        Assert.That(walkerText, Does.Contain("UsesPerspectiveProfile"));
        Assert.That(walkerText, Does.Not.Contain("rectTransform.localScale"));
        Assert.That(ySortApplyBody, Does.Contain("roomProjection.OwnsProjectedSorting"));
        Assert.That(projectedTargetBody, Does.Contain("roomProjection.CanProjectTarget(target)"));
        Assert.That(projectedTargetBody, Does.Contain("CanUseProjectionAsMotionOwner(roomProjection)"));
    }

    [Test]
    public void DetachedActiveProjectionCanOwnWaypointMovement()
    {
        RoomPerspectiveProfile profile = CreatePerspectiveProfile();
        RoomProjectedEntity projection = CreateProjectedEntity("DetachedProjectedGuest", profile, Vector2.zero);

        try
        {
            Assert.That(projection.GetComponentInParent<RoomContentGroup>(true), Is.Null);
            Assert.That(projection.IsProjectionActive, Is.True);
            Assert.That(NPCWaypointMover.CanUseProjectionAsMotionOwner(projection), Is.True);
        }
        finally
        {
            DestroyEntity(projection);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void ActiveProjectionWithoutPositionOwnershipCannotOwnWaypointMovement()
    {
        RoomPerspectiveProfile profile = CreatePerspectiveProfile();
        RoomProjectedEntity projection = CreateProjectedEntity("NonPositionOwningProjectedGuest", profile, Vector2.zero);

        try
        {
            SerializedObject serializedProjection = new SerializedObject(projection);
            serializedProjection.FindProperty("applyPosition").boolValue = false;
            serializedProjection.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(projection.IsProjectionActive, Is.True);
            Assert.That(NPCWaypointMover.CanUseProjectionAsMotionOwner(projection), Is.False);
        }
        finally
        {
            DestroyEntity(projection);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [UnityTest]
    public IEnumerator DetachedPositionOwningProjectionMovesVisibleFootPointWithoutMovingActorRoot()
    {
        RoomPerspectiveProfile profile = CreatePerspectiveProfile();
        GameObject actor = new GameObject("DetachedProjectedActor");
        GameObject projectedVisual = new GameObject("ProjectedVisual");
        GameObject room = new GameObject("Room_Drawing_Room");
        GameObject target = new GameObject("ExitTarget");
        projectedVisual.transform.SetParent(actor.transform, false);
        target.transform.SetParent(room.transform, false);
        actor.transform.position = new Vector3(0f, 0f, 5f);
        target.transform.localPosition = new Vector3(6f, -2f, 0f);
        projectedVisual.AddComponent<SpriteRenderer>();
        RoomProjectedEntity projection = projectedVisual.AddComponent<RoomProjectedEntity>();
        NPCWaypointMover mover = actor.AddComponent<NPCWaypointMover>();
        room.AddComponent<RoomContentGroup>();
        projection.SetRoomProfile(profile);
        projection.SetRoomLocalFootPoint(new Vector2(-6f, -2f));
        mover.MoveSpeed = 100f;
        Vector3 actorStartPosition = actor.transform.position;
        Vector2 targetFootPoint = new Vector2(target.transform.position.x, target.transform.position.y);

        try
        {
            Assert.That(projection.IsProjectionActive, Is.True);
            Assert.That(projection.CanProjectTarget(target.transform), Is.True);

            yield return RunToCompletion(mover.MoveToRoutine(target.transform));

            Assert.That(projection.RoomLocalFootPoint, Is.EqualTo(targetFootPoint).Within(0.001f));
            Assert.That((Vector2)projection.transform.position, Is.EqualTo(targetFootPoint).Within(0.001f));
            Assert.That(actor.transform.position, Is.EqualTo(actorStartPosition));
            Assert.That(mover.IsMoving, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actor);
            UnityEngine.Object.DestroyImmediate(room);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void ProjectionRejectsTargetsInDifferentRooms()
    {
        RoomPerspectiveProfile profile = CreatePerspectiveProfile();
        RoomProjectedEntity projection = CreateProjectedEntity("DetachedProjectedGuest", profile, Vector2.zero);
        GameObject matchingRoom = new GameObject("Room_Drawing_Room");
        GameObject wrongRoom = new GameObject("Room_Dining_Room");
        GameObject matchingTarget = new GameObject("MatchingTarget");
        GameObject wrongTarget = new GameObject("WrongTarget");
        matchingRoom.AddComponent<RoomContentGroup>();
        wrongRoom.AddComponent<RoomContentGroup>();
        matchingTarget.transform.SetParent(matchingRoom.transform, false);
        wrongTarget.transform.SetParent(wrongRoom.transform, false);

        try
        {
            Assert.That(projection.CanProjectTarget(matchingTarget.transform), Is.True);
            Assert.That(projection.CanProjectTarget(wrongTarget.transform), Is.False);
        }
        finally
        {
            DestroyEntity(projection);
            UnityEngine.Object.DestroyImmediate(matchingRoom);
            UnityEngine.Object.DestroyImmediate(wrongRoom);
        }
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
        Vector2 footPoint)
    {
        GameObject root = new GameObject(name);
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.AddComponent<SpriteRenderer>();
        RoomProjectedEntity entity = root.AddComponent<RoomProjectedEntity>();
        entity.SetVisualRoot(visual.transform);
        entity.SetRoomProfile(roomProfile);
        entity.SetRoomLocalFootPoint(footPoint);
        return entity;
    }

    private static void SetProjectionMode(RoomProjectedEntity entity, RoomProjectedEntity.ProjectionMode mode)
    {
        SerializedObject serializedEntity = new SerializedObject(entity);
        serializedEntity.FindProperty("projectionMode").intValue = (int)mode;
        serializedEntity.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void DestroyEntity(RoomProjectedEntity entity)
    {
        if (entity != null)
        {
            UnityEngine.Object.DestroyImmediate(entity.gameObject);
        }
    }

    private static IEnumerator RunToCompletion(IEnumerator routine)
    {
        Stack<IEnumerator> routines = new Stack<IEnumerator>();
        routines.Push(routine);

        while (routines.Count > 0)
        {
            IEnumerator current = routines.Peek();

            if (!current.MoveNext())
            {
                routines.Pop();
                continue;
            }

            if (current.Current is IEnumerator nestedRoutine)
            {
                routines.Push(nestedRoutine);
                continue;
            }

            yield return current.Current;
        }
    }

    private static string ExtractMethodBody(string sourceText, string methodName)
    {
        Match declaration = Regex.Match(
            sourceText,
            $@"(?m)^[ \t]*(?:(?:public|private|protected|internal|static|virtual|override|sealed|async|new)[ \t]+)*[A-Za-z_][A-Za-z0-9_<>,\[\]?]*[ \t]+{Regex.Escape(methodName)}[ \t]*\(");
        Assert.That(declaration.Success, Is.True, $"Could not find method '{methodName}'.");

        int bodyStart = sourceText.IndexOf('{', declaration.Index);
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
