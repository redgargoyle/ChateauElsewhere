using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class GuestButlerScaleRegressionTests
{
    private const string PointClickPlayerMovementPath = "Assets/Scripts/PointClickPlayerMovement.cs";
    private const string RoomProjectedEntityPath = "Assets/Scripts/Characters/RoomProjectedEntity.cs";
    private const string RoomPersonWalkerPath = "Assets/Scripts/Characters/RoomPersonWalker2D.cs";
    private const string ActorRoomStatePath = "Assets/Scripts/Story/ActorRoomState.cs";
    private const string HarmonizerPath = "Assets/Scripts/Characters/GuestButlerScaleHarmonizer.cs";
    private const string VisualBoundsUtilityPath = "Assets/Scripts/Characters/CharacterVisualBoundsUtility.cs";
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

            Assert.That(visual.transform.localScale.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(visual.transform.localScale.y, Is.EqualTo(1.5f).Within(0.0001f));
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
        Assert.That(harmonizerText, Does.Contain("CharacterVisualBoundsUtility.TryApplyTargetScreenHeight"));
        Assert.That(harmonizerText, Does.Contain("BoundsRoot"));
        Assert.That(harmonizerText, Does.Contain("ScaleRoot"));
        Assert.That(harmonizerText, Does.Contain("BuildGuestScaleTargets"));
        Assert.That(harmonizerText, Does.Contain("applyToActorRoomStates"));
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
            Assert.That(guest.transform.localScale.y, Is.EqualTo(1.1f).Within(0.0001f));
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
    public void CharacterVisualBoundsUtilityCanMeasureAndFitScreenHeight()
    {
        Camera camera = CreateTestCamera();
        GameObject root = CreateUiVisual("BoundsTarget", new Vector2(100f, 200f), Vector3.one);

        try
        {
            Assert.That(CharacterVisualBoundsUtility.TryGetScreenHeight(root.transform, camera, out float originalHeight), Is.True);
            Assert.That(CharacterVisualBoundsUtility.TryFitScreenHeight(root.transform, camera, originalHeight * 0.5f, out _, out _), Is.True);
            Assert.That(CharacterVisualBoundsUtility.TryGetScreenHeight(root.transform, camera, out float halfHeight), Is.True);
            Assert.That(halfHeight, Is.EqualTo(originalHeight * 0.5f).Within(1f));

            Vector3 scaleAfterFirstFit = root.transform.localScale;
            Assert.That(CharacterVisualBoundsUtility.TryFitScreenHeight(root.transform, camera, halfHeight, out _, out _), Is.True);
            Assert.That(root.transform.localScale.x, Is.EqualTo(scaleAfterFirstFit.x).Within(0.0001f));
            Assert.That(root.transform.localScale.y, Is.EqualTo(scaleAfterFirstFit.y).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }

    [Test]
    public void CharacterVisualBoundsIgnoresContainerRectTransforms()
    {
        Camera camera = CreateTestCamera();
        GameObject root = new GameObject("HugeContainer", typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(2000f, 2000f);
        GameObject child = CreateUiVisual("GuestBody", new Vector2(100f, 200f), Vector3.one);
        child.transform.SetParent(root.transform, false);

        try
        {
            Assert.That(CharacterVisualBoundsUtility.TryGetScreenHeight(child.transform, camera, out float childHeight), Is.True);
            Assert.That(CharacterVisualBoundsUtility.TryGetScreenHeight(root.transform, camera, out float rootHeight), Is.True);
            Assert.That(rootHeight, Is.EqualTo(childHeight).Within(1f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(child);
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }

    [Test]
    public void OverlayCanvasGraphicMeasuresWithNullCamera()
    {
        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        GameObject child = CreateUiVisual("OverlayGuestBody", new Vector2(100f, 200f), Vector3.one);
        child.transform.SetParent(canvasObject.transform, false);

        try
        {
            Assert.That(
                CharacterVisualBoundsUtility.TryResolveCharacterVisualTarget(
                    canvasObject.transform,
                    null,
                    out CharacterVisualBoundsUtility.CharacterVisualTarget target),
                Is.True);
            Assert.That(target.ScreenHeight, Is.GreaterThan(0f));
            Assert.That(target.PrimaryVisual, Is.EqualTo(child.transform));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(child);
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    [Test]
    public void RoomPersonWalkerUsesTargetGraphicForFinalScale()
    {
        Camera camera = CreateTestCamera();
        GameObject butler = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
        PointClickPlayerMovement movement = butler.GetComponent<PointClickPlayerMovement>();
        GuestButlerScaleHarmonizer harmonizer = butler.AddComponent<GuestButlerScaleHarmonizer>();
        GameObject room = new GameObject("Room_Uncalibrated");
        room.AddComponent<RoomContentGroup>().SetRoomName("Uncalibrated Room");
        GameObject walkerRoot = new GameObject("Walker_With_TargetGraphic", typeof(RectTransform));
        walkerRoot.transform.SetParent(room.transform, false);
        GameObject graphicObject = CreateUiVisual("WalkerBodyGraphic", new Vector2(100f, 200f), Vector3.one);
        graphicObject.transform.SetParent(walkerRoot.transform, false);
        Image image = graphicObject.GetComponent<Image>();
        RoomPersonWalker2D walker = walkerRoot.AddComponent<RoomPersonWalker2D>();
        SerializedObject serializedWalker = new SerializedObject(walker);
        serializedWalker.FindProperty("targetGraphic").objectReferenceValue = image;
        serializedWalker.ApplyModifiedPropertiesWithoutUndo();

        try
        {
            harmonizer.SetButlerScaleSource(movement);
            walker.SetButlerScaleSource(movement, false);
            Vector3 rootScale = walkerRoot.transform.localScale;
            Vector3 graphicScale = graphicObject.transform.localScale;

            harmonizer.SetDebugGuestScaleMultiplier(0.5f);
            GuestButlerScaleHarmonizer.GuestScaleApplySummary proofSummary = harmonizer.RefreshNow();

            Assert.That(proofSummary.Scaled, Is.GreaterThanOrEqualTo(1));
            Assert.That(walkerRoot.transform.localScale, Is.EqualTo(rootScale));
            Assert.That(graphicObject.transform.localScale.y, Is.EqualTo(graphicScale.y * 0.5f).Within(0.02f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(graphicObject);
            UnityEngine.Object.DestroyImmediate(walkerRoot);
            UnityEngine.Object.DestroyImmediate(room);
            UnityEngine.Object.DestroyImmediate(butler);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }

    [Test]
    public void ProofModeChangesGuestsWithoutCalibration()
    {
        Camera camera = CreateTestCamera();
        GameObject butler = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
        PointClickPlayerMovement movement = butler.GetComponent<PointClickPlayerMovement>();
        GuestButlerScaleHarmonizer harmonizer = butler.AddComponent<GuestButlerScaleHarmonizer>();
        GameObject room = new GameObject("Room_Uncalibrated");
        room.AddComponent<RoomContentGroup>().SetRoomName("Uncalibrated Room");
        GameObject walkerObject = CreateUiVisual("Walker_UncalibratedGuest", new Vector2(100f, 200f), Vector3.one);
        walkerObject.transform.SetParent(room.transform, false);
        RoomPersonWalker2D walker = walkerObject.AddComponent<RoomPersonWalker2D>();

        try
        {
            harmonizer.SetButlerScaleSource(movement);
            walker.SetButlerScaleSource(movement, false);
            Assert.That(CharacterVisualBoundsUtility.TryGetScreenHeight(walkerObject.transform, camera, out float originalHeight), Is.True);

            harmonizer.SetDebugGuestScaleMultiplier(0.5f);
            GuestButlerScaleHarmonizer.GuestScaleApplySummary proofSummary = harmonizer.RefreshNow();
            Assert.That(proofSummary.Scaled, Is.GreaterThanOrEqualTo(1));
            Assert.That(CharacterVisualBoundsUtility.TryGetScreenHeight(walkerObject.transform, camera, out float proofHeight), Is.True);
            Assert.That(proofHeight, Is.EqualTo(originalHeight * 0.5f).Within(1f));

            harmonizer.RestoreRealButlerScaling();
            Assert.That(CharacterVisualBoundsUtility.TryGetScreenHeight(walkerObject.transform, camera, out float restoredHeight), Is.True);
            Assert.That(restoredHeight, Is.EqualTo(originalHeight).Within(1f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(walkerObject);
            UnityEngine.Object.DestroyImmediate(room);
            UnityEngine.Object.DestroyImmediate(butler);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }

    [Test]
    public void ProofModeChangesGuestWithoutButlerCalibration()
    {
        ProofModeChangesGuestsWithoutCalibration();
    }

    [Test]
    public void HarmonizerUsesVisualHeightNotRawLocalScale()
    {
        Camera camera = CreateTestCamera();
        GameObject butler = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
        butler.AddComponent<SpriteRenderer>().sprite = CreateTestSprite();
        PointClickPlayerMovement movement = butler.GetComponent<PointClickPlayerMovement>();
        GuestButlerScaleHarmonizer harmonizer = butler.AddComponent<GuestButlerScaleHarmonizer>();
        GameObject room = new GameObject("Room_Drawing");
        room.AddComponent<RoomContentGroup>().SetRoomName("Drawing Room");
        GameObject guest = CreateUiVisual("Walker_VisualHeightGuest", new Vector2(60f, 500f), new Vector3(0.2f, 0.2f, 1f));
        guest.transform.SetParent(room.transform, false);
        RoomPersonWalker2D walker = guest.AddComponent<RoomPersonWalker2D>();

        try
        {
            ConfigureHalfScaleButler(movement);
            harmonizer.SetButlerScaleSource(movement);
            walker.SetButlerScaleSource(movement, false);
            Assert.That(movement.TryGetButlerHumanScaleReference(camera, out float referenceHeight, out _), Is.True);

            GuestButlerScaleHarmonizer.GuestScaleApplySummary summary = harmonizer.RefreshNow();
            Assert.That(summary.Scaled, Is.GreaterThanOrEqualTo(1));
            Assert.That(CharacterVisualBoundsUtility.TryGetScreenHeight(guest.transform, camera, out float guestHeight), Is.True);
            Assert.That(guestHeight, Is.EqualTo(referenceHeight * 0.5f).Within(2f));
            Assert.That(guest.transform.localScale.y, Is.Not.EqualTo(butler.transform.localScale.y).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
            UnityEngine.Object.DestroyImmediate(room);
            UnityEngine.Object.DestroyImmediate(butler);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }

    [Test]
    public void RoomCalibrationCoverageReportsMissingRooms()
    {
        string toolText = File.ReadAllText(ToolPath);

        Assert.That(toolText, Does.Contain("Room Calibration Coverage"));
        Assert.That(toolText, Does.Contain("Print Missing Guest Room Calibrations"));
        Assert.That(toolText, Does.Contain("Missing Butler calibration for"));
    }

    [Test]
    public void ProofUnchangedErrorOnlyReportsActualFitterFailures()
    {
        Camera camera = CreateTestCamera();
        GameObject butler = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
        PointClickPlayerMovement movement = butler.GetComponent<PointClickPlayerMovement>();
        GuestButlerScaleHarmonizer harmonizer = butler.AddComponent<GuestButlerScaleHarmonizer>();
        GameObject room = new GameObject("Room_Uncalibrated");
        room.AddComponent<RoomContentGroup>().SetRoomName("Uncalibrated Room");
        GameObject valid = CreateUiVisual("Walker_ValidProofGuest", new Vector2(100f, 200f), Vector3.one);
        valid.transform.SetParent(room.transform, false);
        valid.AddComponent<RoomPersonWalker2D>().SetButlerScaleSource(movement, false);
        GameObject noBounds = new GameObject("Guest_NoVisualBounds");
        noBounds.transform.SetParent(room.transform, false);
        ActorRoomState actor = noBounds.AddComponent<ActorRoomState>();
        actor.SetActorId("Guest No Bounds");
        actor.SetCurrentRoom("Uncalibrated Room");

        try
        {
            harmonizer.SetButlerScaleSource(movement);
            harmonizer.SetDebugGuestScaleMultiplier(0.5f);
            GuestButlerScaleHarmonizer.GuestScaleApplySummary summary = harmonizer.RefreshNow();

            string failures = summary.ProofFailures != null ? string.Join("\n", summary.ProofFailures) : string.Empty;
            Assert.That(failures, Does.Not.Contain("Walker_ValidProofGuest"));
            Assert.That(failures, Does.Contain("No visual bounds found"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(noBounds);
            UnityEngine.Object.DestroyImmediate(valid);
            UnityEngine.Object.DestroyImmediate(room);
            UnityEngine.Object.DestroyImmediate(butler);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }

    [Test]
    public void RealScalingSkipsUncalibratedRoomsButReportsThem()
    {
        Camera camera = CreateTestCamera();
        GameObject butler = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
        butler.AddComponent<SpriteRenderer>().sprite = CreateTestSprite();
        PointClickPlayerMovement movement = butler.GetComponent<PointClickPlayerMovement>();
        GuestButlerScaleHarmonizer harmonizer = butler.AddComponent<GuestButlerScaleHarmonizer>();
        GameObject room = new GameObject("Room_Drawing");
        room.AddComponent<RoomContentGroup>().SetRoomName("Drawing Room");
        GameObject guest = CreateUiVisual("Walker_RealScalingGuest", new Vector2(100f, 200f), Vector3.one);
        guest.transform.SetParent(room.transform, false);
        guest.AddComponent<RoomPersonWalker2D>().SetButlerScaleSource(movement, false);

        try
        {
            harmonizer.SetButlerScaleSource(movement);
            GuestButlerScaleHarmonizer.GuestScaleApplySummary missingSummary = harmonizer.RefreshNow();
            Assert.That(missingSummary.MissingCalibration, Is.GreaterThanOrEqualTo(1));
            Assert.That(missingSummary.Scaled, Is.EqualTo(0));

            ConfigureHalfScaleButler(movement);
            GuestButlerScaleHarmonizer.GuestScaleApplySummary scaledSummary = harmonizer.RefreshNow();
            Assert.That(scaledSummary.Scaled, Is.GreaterThanOrEqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
            UnityEngine.Object.DestroyImmediate(room);
            UnityEngine.Object.DestroyImmediate(butler);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }

    [Test]
    public void SeatedGuestUsesSeatedHeightRatio()
    {
        Camera camera = CreateTestCamera();
        GameObject butler = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
        butler.AddComponent<SpriteRenderer>().sprite = CreateTestSprite();
        PointClickPlayerMovement movement = butler.GetComponent<PointClickPlayerMovement>();
        ConfigureHalfScaleButler(movement);
        GuestButlerScaleHarmonizer harmonizer = butler.AddComponent<GuestButlerScaleHarmonizer>();
        GameObject room = new GameObject("Room_Drawing");
        room.AddComponent<RoomContentGroup>().SetRoomName("Drawing Room");
        GameObject guest = CreateUiVisual("Guest_Seated", new Vector2(100f, 300f), Vector3.one);
        guest.transform.SetParent(room.transform, false);
        ActorRoomState actor = guest.AddComponent<ActorRoomState>();
        actor.SetActorId("Guest Seated");
        actor.SetCurrentRoom("Drawing Room");
        actor.SetSeated(true);

        try
        {
            harmonizer.SetButlerScaleSource(movement);
            Assert.That(movement.TryGetButlerHumanScaleReference(camera, out float referenceHeight, out _), Is.True);
            GuestButlerScaleHarmonizer.GuestScaleApplySummary summary = harmonizer.RefreshNow();

            Assert.That(summary.Scaled, Is.GreaterThanOrEqualTo(1));
            Assert.That(CharacterVisualBoundsUtility.TryGetScreenHeight(guest.transform, camera, out float guestHeight), Is.True);
            Assert.That(guestHeight, Is.EqualTo(referenceHeight * 0.5f * 0.68f).Within(2f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
            UnityEngine.Object.DestroyImmediate(room);
            UnityEngine.Object.DestroyImmediate(butler);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }

    [Test]
    public void RoomProjectedEntityBypassesOldScaleOverridesInFinalHumanScale()
    {
        Camera camera = CreateTestCamera();
        RoomPerspectiveProfile profile = CreateProfile(1f, 1f);
        GameObject butler = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
        butler.AddComponent<SpriteRenderer>().sprite = CreateTestSprite();
        PointClickPlayerMovement movement = butler.GetComponent<PointClickPlayerMovement>();
        ConfigureHalfScaleButler(movement);
        GuestButlerScaleHarmonizer harmonizer = butler.AddComponent<GuestButlerScaleHarmonizer>();
        RoomProjectedEntity entity = CreateProjectedEntity("ProjectedGuest_FinalHumanScale", profile, null, Vector2.zero);
        GameObject visual = entity.VisualRoot.gameObject;
        visual.GetComponent<SpriteRenderer>().sprite = CreateTestSprite();

        try
        {
            entity.SetVisualRootScaleForRoom("Drawing Room", new Vector3(0.05f, 0.05f, 1f), false);
            entity.SetButlerScaleSource(movement, false);
            entity.SetIgnoreRoomVisualScaleOverridesWhenUsingButlerRules(true, false);
            entity.SetIgnoreVisualProfileHeightMultiplierWhenUsingButlerRules(true, false);
            harmonizer.SetButlerScaleSource(movement);
            Assert.That(movement.TryGetButlerHumanScaleReference(camera, out float referenceHeight, out _), Is.True);

            GuestButlerScaleHarmonizer.GuestScaleApplySummary summary = harmonizer.RefreshNow();

            Assert.That(summary.Scaled, Is.GreaterThanOrEqualTo(1));
            Assert.That(CharacterVisualBoundsUtility.TryGetScreenHeight(visual.transform, camera, out float guestHeight), Is.True);
            Assert.That(guestHeight, Is.EqualTo(referenceHeight * 0.5f).Within(2f));
        }
        finally
        {
            DestroyEntity(entity);
            UnityEngine.Object.DestroyImmediate(butler);
            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }

    [Test]
    public void FinalHumanScaleWinsAfterRoomProjectedEntityApplyProjection()
    {
        Camera camera = CreateTestCamera();
        RoomPerspectiveProfile profile = CreateProfile(1f, 1f);
        GameObject butler = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
        butler.AddComponent<SpriteRenderer>().sprite = CreateTestSprite();
        PointClickPlayerMovement movement = butler.GetComponent<PointClickPlayerMovement>();
        ConfigureHalfScaleButler(movement);
        GuestButlerScaleHarmonizer harmonizer = butler.AddComponent<GuestButlerScaleHarmonizer>();
        RoomProjectedEntity entity = CreateProjectedEntity("ProjectedGuest_AfterProjection", profile, null, Vector2.zero);
        GameObject visual = entity.VisualRoot.gameObject;
        visual.GetComponent<SpriteRenderer>().sprite = CreateTestSprite();

        try
        {
            entity.SetButlerScaleSource(movement, false);
            entity.SetButlerCharacterScaleRulesEnabled(true, false);
            entity.ApplyProjection();
            harmonizer.SetButlerScaleSource(movement);
            Assert.That(movement.TryGetButlerHumanScaleReference(camera, out float referenceHeight, out _), Is.True);

            GuestButlerScaleHarmonizer.GuestScaleApplySummary summary = harmonizer.RefreshNow();

            Assert.That(summary.Scaled, Is.GreaterThanOrEqualTo(1));
            Assert.That(CharacterVisualBoundsUtility.TryGetScreenHeight(visual.transform, camera, out float guestHeight), Is.True);
            Assert.That(guestHeight, Is.EqualTo(referenceHeight * 0.5f).Within(2f));
        }
        finally
        {
            DestroyEntity(entity);
            UnityEngine.Object.DestroyImmediate(butler);
            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }

    [Test]
    public void EditorToolContainsRequiredButtons()
    {
        string toolText = File.ReadAllText(ToolPath);

        Assert.That(toolText, Does.Contain("ENABLE FINAL HUMAN SCALE FOR ALL GUESTS"));
        Assert.That(toolText, Does.Contain("REFRESH FINAL HUMAN SCALE NOW"));
        Assert.That(toolText, Does.Contain("PROOF 50%"));
        Assert.That(toolText, Does.Contain("PROOF 150%"));
        Assert.That(toolText, Does.Contain("Restore Real Butler Scaling"));
        Assert.That(toolText, Does.Contain("PRINT SCALE WRITER AUDIT"));
        Assert.That(toolText, Does.Contain("PRINT ALL GUEST SCALE WRITERS"));
        Assert.That(toolText, Does.Contain("EMERGENCY: Restore Proof Baselines / Clamp Bad Guest Scales"));
        Assert.That(toolText, Does.Contain("Bypass Old Room Visual Scale Overrides For All Guests"));
        Assert.That(toolText, Does.Contain("Current Visual Height px"));
        Assert.That(toolText, Does.Contain("Target Visual Height px"));
        Assert.That(toolText, Does.Contain("Room Calibration Coverage"));
        Assert.That(File.ReadAllText(VisualBoundsUtilityPath), Does.Contain("TryResolveCharacterVisualTarget"));
        Assert.That(File.ReadAllText(VisualBoundsUtilityPath), Does.Contain("TryApplyTargetScreenHeight"));
        Assert.That(File.ReadAllText(ActorRoomStatePath), Does.Contain("ApplyButlerCharacterScaleNow"));
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

    private static Camera CreateTestCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
        camera.transform.position = new Vector3(0f, 0f, -10f);
        return camera;
    }

    private static GameObject CreateUiVisual(string name, Vector2 size, Vector3 localScale)
    {
        GameObject visual = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rectTransform = visual.GetComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.localScale = localScale;
        visual.GetComponent<Image>().color = Color.white;
        return visual;
    }

    private static Sprite CreateTestSprite()
    {
        Texture2D texture = new Texture2D(8, 16);
        Color[] pixels = new Color[8 * 16];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, 8f, 16f), new Vector2(0.5f, 0f), 16f);
    }

    private static void DestroyEntity(RoomProjectedEntity entity)
    {
        if (entity != null)
        {
            UnityEngine.Object.DestroyImmediate(entity.gameObject);
        }
    }
}
