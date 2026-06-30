using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class GuestButlerScaleRegressionTests
{
    private const string PointClickPlayerMovementPath = "Assets/Scripts/PointClickPlayerMovement.cs";
    private const string RoomProjectedEntityPath = "Assets/Scripts/Characters/RoomProjectedEntity.cs";
    private const string RoomPersonWalkerPath = "Assets/Scripts/Characters/RoomPersonWalker2D.cs";
    private const string ActorRoomStatePath = "Assets/Scripts/Story/ActorRoomState.cs";
    private const string HarmonizerPath = "Assets/Scripts/Characters/GuestButlerScaleHarmonizer.cs";
    private const string ToolPath = "Assets/Editor/GuestButlerScaleTool.cs";

    [Test]
    public void PointClickPlayerMovementExposesSharedButlerScaleEvaluator()
    {
        string movementText = File.ReadAllText(PointClickPlayerMovementPath);
        GameObject butler = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));

        try
        {
            PointClickPlayerMovement movement = butler.GetComponent<PointClickPlayerMovement>();
            movement.CaptureCurrentTransformAsButlerCalibrationBaseScale();
            movement.SetButlerFrontFinalLocalScaleForRoom("Drawing Room", -100f, 1f, false);
            movement.SetButlerBackFinalLocalScaleForRoom("Drawing Room", 100f, 1f, false);

            Assert.That(movementText, Does.Contain("ButlerCharacterScaleSample"));
            Assert.That(movementText, Does.Contain("TryEvaluateButlerCharacterScale"));
            Assert.That(
                movement.TryEvaluateButlerCharacterScale(
                    "Drawing Room",
                    Vector2.zero,
                    out PointClickPlayerMovement.ButlerCharacterScaleSample sample),
                Is.True);
            Assert.That(sample.NormalizedScale, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(sample.ButlerFinalLocalScaleY, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(sample.ButlerBaseLocalScaleY, Is.EqualTo(2f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(butler);
        }
    }

    [Test]
    public void RoomProjectedEntityUsesButlerRulesBeforeRoomProfile()
    {
        RoomPerspectiveProfile profile = CreateProfile(1f, 1f);
        GameObject butler = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
        RoomProjectedEntity entity = CreateProjectedEntity("ProjectedGuest", profile, null, Vector2.zero);

        try
        {
            ConfigureHalfScaleButler(butler.GetComponent<PointClickPlayerMovement>());
            entity.SetButlerScaleSource(butler.GetComponent<PointClickPlayerMovement>(), false);
            entity.SetButlerCharacterScaleRulesEnabled(true);

            Assert.That(entity.IsUsingButlerCharacterScaleRules, Is.True);
            Assert.That(entity.CurrentScale, Is.EqualTo(0.5f).Within(0.0001f));
        }
        finally
        {
            DestroyEntity(entity);
            UnityEngine.Object.DestroyImmediate(butler);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void RoomProjectedEntityBypassesOldVisualOverridesWhenUsingButlerRules()
    {
        RoomPerspectiveProfile profile = CreateProfile(1f, 1f);
        GameObject butler = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
        GameObject root = new GameObject("ProjectedGuest");
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = new Vector3(2f, 3f, 4f);
        visual.AddComponent<SpriteRenderer>();
        RoomProjectedEntity entity = root.AddComponent<RoomProjectedEntity>();
        entity.SetVisualRoot(visual.transform);
        entity.SetRoomProfile(profile);
        entity.SetRoomLocalFootPoint(Vector2.zero);

        try
        {
            ConfigureHalfScaleButler(butler.GetComponent<PointClickPlayerMovement>());
            entity.SetVisualRootScaleForRoom("Drawing Room", new Vector3(0.1f, 0.1f, 8f), false);
            entity.SetButlerScaleSource(butler.GetComponent<PointClickPlayerMovement>(), false);
            entity.SetButlerCharacterScaleRulesEnabled(true, false);
            entity.SetIgnoreRoomVisualScaleOverridesWhenUsingButlerRules(true, false);
            entity.ApplyButlerCharacterScaleNow();

            Assert.That(visual.transform.localScale.x, Is.EqualTo(2f / 3f).Within(0.0001f));
            Assert.That(visual.transform.localScale.y, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(visual.transform.localScale.z, Is.EqualTo(4f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(butler);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void RoomPersonWalkerUsesButlerRulesBeforeNearFarFallback()
    {
        RoomPerspectiveProfile profile = CreateProfile(1f, 1f);
        GameObject room = new GameObject("Drawing Room");
        room.AddComponent<RoomContentGroup>().SetPerspectiveProfile(profile);
        GameObject butler = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
        GameObject walkerObject = new GameObject("Walker", typeof(RectTransform));
        walkerObject.transform.SetParent(room.transform, false);
        RoomPersonWalker2D walker = walkerObject.AddComponent<RoomPersonWalker2D>();

        try
        {
            ConfigureHalfScaleButler(butler.GetComponent<PointClickPlayerMovement>());
            walker.SetButlerScaleSource(butler.GetComponent<PointClickPlayerMovement>(), false);
            walker.SetButlerCharacterScaleRulesEnabled(true, false);
            walker.ApplyButlerCharacterScaleNow();

            Assert.That(walker.IsUsingButlerCharacterScaleRules, Is.True);
            Assert.That(walker.CurrentDepthScale, Is.EqualTo(0.5f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(walkerObject);
            UnityEngine.Object.DestroyImmediate(room);
            UnityEngine.Object.DestroyImmediate(butler);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void GuestButlerScaleHarmonizerRunsAsFinalWriter()
    {
        string harmonizerText = File.ReadAllText(HarmonizerPath);

        Assert.That(harmonizerText, Does.Contain("[DefaultExecutionOrder(10000)]"));
        Assert.That(harmonizerText, Does.Contain("GetRoomGuestScaleMultiplier"));
        Assert.That(harmonizerText, Does.Contain("SetRoomGuestScaleMultiplier"));
        Assert.That(harmonizerText, Does.Contain("ApplyToActorRoomStates"));
        Assert.That(harmonizerText, Does.Contain("IsButlerObjectOrChild"));
    }

    [Test]
    public void ActorRoomStateWorldGuestsUseButlerScaleRules()
    {
        RoomPerspectiveProfile profile = CreateProfile(1f, 1f);
        GameObject room = new GameObject("Room_Grand_Entrance_Hall");
        room.AddComponent<RoomContentGroup>().SetRoomName("Grand Entrance Hall");
        GameObject butler = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
        GameObject guest = new GameObject("Guest 1");
        guest.transform.SetParent(room.transform, false);
        guest.transform.localPosition = Vector3.zero;
        guest.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
        ActorRoomState actor = guest.AddComponent<ActorRoomState>();

        try
        {
            PointClickPlayerMovement movement = butler.GetComponent<PointClickPlayerMovement>();
            movement.CaptureCurrentTransformAsButlerCalibrationBaseScale();
            movement.SetButlerFrontFinalLocalScaleForRoom("Grand Entrance Hall", -100f, 1f, false);
            movement.SetButlerBackFinalLocalScaleForRoom("Grand Entrance Hall", 100f, 1f, false);
            actor.SetActorId("Guest 1");
            actor.SetCurrentRoom("Grand Entrance Hall");
            actor.ResetAuthoredActorScaleForEditor();

            Assert.That(actor.ApplyButlerCharacterScaleNow(movement), Is.True);
            Assert.That(actor.IsUsingButlerCharacterScaleRules, Is.True);
            Assert.That(actor.CurrentButlerCharacterScale, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(guest.transform.localScale.y, Is.EqualTo(1f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
            UnityEngine.Object.DestroyImmediate(room);
            UnityEngine.Object.DestroyImmediate(butler);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void ProofMultiplierChangesGuests()
    {
        RoomPerspectiveProfile profile = CreateProfile(1f, 1f);
        GameObject butler = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
        PointClickPlayerMovement movement = butler.GetComponent<PointClickPlayerMovement>();
        GuestButlerScaleHarmonizer harmonizer = butler.AddComponent<GuestButlerScaleHarmonizer>();
        RoomProjectedEntity entity = CreateProjectedEntity("ProjectedGuest", profile, null, Vector2.zero);
        Transform visualRoot = entity.VisualRoot;

        try
        {
            ConfigureHalfScaleButler(movement);
            harmonizer.SetButlerScaleSource(movement);
            entity.SetButlerScaleSource(movement, false);

            harmonizer.SetDebugGuestScaleMultiplier(0.5f);
            harmonizer.RefreshNow();
            Vector3 proofScale = visualRoot.localScale;

            harmonizer.SetDebugGuestScaleMultiplier(1f);
            harmonizer.RefreshNow();
            Vector3 restoredScale = visualRoot.localScale;

            Assert.That(proofScale.x, Is.Not.EqualTo(restoredScale.x).Within(0.0001f));
            Assert.That(proofScale.x, Is.EqualTo(restoredScale.x * 0.5f).Within(0.0001f));
        }
        finally
        {
            DestroyEntity(entity);
            UnityEngine.Object.DestroyImmediate(butler);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void EditorToolContainsRequiredButtons()
    {
        string toolText = File.ReadAllText(ToolPath);

        Assert.That(toolText, Does.Contain("Auto Setup + Apply Now"));
        Assert.That(toolText, Does.Contain("Guest Size In This Room"));
        Assert.That(toolText, Does.Contain("Save Guest Room Scale"));
        Assert.That(toolText, Does.Contain("Reset Room To Butler Size"));
        Assert.That(toolText, Does.Contain("Test 50%"));
        Assert.That(toolText, Does.Contain("Test 150%"));
        Assert.That(toolText, Does.Contain("Restore Real Butler Scaling"));
        Assert.That(File.ReadAllText(ActorRoomStatePath), Does.Contain("ApplyButlerCharacterScaleNow"));
    }

    [Test]
    public void ButlerFinalGuestScaleIgnoresRoomStageZoomMultiplier()
    {
        string movementText = File.ReadAllText(PointClickPlayerMovementPath);
        string projectedText = File.ReadAllText(RoomProjectedEntityPath);
        string actorText = File.ReadAllText(ActorRoomStatePath);
        string playerScaleBody = ExtractMethodBody(movementText, "private void ApplyPerspectiveScale");
        string projectedScaleBody = ExtractMethodBody(projectedText, "private void ApplyProjectedScale");
        string forceScaleBody = ExtractMethodBody(projectedText, "private void ForceApplyButlerCharacterScale");
        string roomStageMotionBody = ExtractMethodBody(actorText, "private void ApplyRoomStageMotionDeltaIfNeeded");
        string roomStageBindingBody = ExtractMethodBody(actorText, "private bool TryApplyRoomStageLocalBindingIfNeeded");

        Assert.That(playerScaleBody, Does.Contain("transform.localScale = calibratedLocalScale"), "The calibrated Butler should use the saved final local scale directly.");
        Assert.That(playerScaleBody, Does.Not.Contain("calibratedLocalScale.x * roomStageScale"), "The calibrated Butler must not grow/shrink when scroll zoom changes the room stage.");
        Assert.That(projectedScaleBody, Does.Contain("currentButlerCharacterFinalLocalScaleY"), "Projected guests should still use the Butler final local-scale value.");
        Assert.That(projectedScaleBody, Does.Not.Contain("currentButlerCharacterFinalLocalScaleY * currentRoomStageScaleMultiplier"), "Final Butler guest scale must not grow/shrink again when scroll zoom changes the room stage.");
        Assert.That(forceScaleBody, Does.Not.Contain("currentRoomStageScaleMultiplier > 0f"), "Tool/runtime force-apply should be idempotent and independent from current room-stage zoom.");
        Assert.That(roomStageMotionBody, Does.Contain("!isUsingButlerCharacterScaleRules"), "ActorRoomState room-stage motion should not scale actors after final Butler guest scaling is active.");
        Assert.That(roomStageBindingBody, Does.Contain("!isUsingButlerCharacterScaleRules"), "Bound world actors using final Butler guest scale should keep position binding but not room-stage scale multiplication.");
    }

    private static RoomPerspectiveProfile CreateProfile(float nearScale, float farScale)
    {
        RoomPerspectiveProfile profile = ScriptableObject.CreateInstance<RoomPerspectiveProfile>();
        profile.Configure(
            "Drawing Room",
            new Vector2(1366f, 768f),
            -100f,
            100f,
            AnimationCurve.Linear(0f, nearScale, 1f, farScale),
            null,
            1000,
            8000,
            AnimationCurve.Linear(0f, 1f, 1f, 0f));
        return profile;
    }

    private static void ConfigureHalfScaleButler(PointClickPlayerMovement movement)
    {
        movement.CaptureCurrentTransformAsButlerCalibrationBaseScale();
        movement.SetButlerFrontFinalLocalScaleForRoom("Drawing Room", -100f, 1f, false);
        movement.SetButlerBackFinalLocalScaleForRoom("Drawing Room", 100f, 1f, false);
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
}
