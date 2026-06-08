using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class RoomProjectionRegressionTests
{
    private const string Chapter1ArrivalControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs";
    private const string ActorRoomStatePath = "Assets/Scripts/Story/ActorRoomState.cs";
    private const string RoomPersonWalkerPath = "Assets/Scripts/Characters/RoomPersonWalker2D.cs";
    private const string WorldYSortPath = "Assets/Scripts/Characters/WorldYSortSpriteRenderer.cs";
    private const string NPCWaypointMoverPath = "Assets/Scripts/Story/NPCWaypointMover.cs";

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
    public void ProjectedEntityDoesNotDependOnRandomRootPrefabScale()
    {
        RoomPerspectiveProfile profile = CreatePerspectiveProfile();
        GameObject root = new GameObject("ScaledPrefabRoot");
        root.transform.localScale = new Vector3(3f, 0.25f, 7f);
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.AddComponent<SpriteRenderer>();
        RoomProjectedEntity entity = root.AddComponent<RoomProjectedEntity>();
        entity.SetVisualRoot(visual.transform);
        entity.SetRoomProfile(profile);
        entity.SetRoomLocalFootPoint(new Vector2(0f, -40f));

        try
        {
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(visual.transform.localScale.x, Is.EqualTo(entity.CurrentScale).Within(0.0001f));
            Assert.That(visual.transform.localScale.y, Is.EqualTo(entity.CurrentScale).Within(0.0001f));
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
        string shouldFollowBody = ExtractMethodBody(actorRoomStateText, "ShouldFollowRoomStageMotion");

        Assert.That(actorRoomStateText, Does.Contain("RoomProjectedEntity"), "ActorRoomState should know how to detect projection without owning perspective math.");
        Assert.That(shouldFollowBody, Does.Contain("!HasActiveProjection()"), "Projection should own visual scale and room-stage positioning when present.");
        Assert.That(actorRoomStateText, Does.Contain("projection.IsProjectionActive"), "ActorRoomState should only defer to projection in the matching projected room.");
        Assert.That(actorRoomStateText, Does.Contain("ApplyState()"), "ActorRoomState should continue to own visibility and interaction state.");
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
