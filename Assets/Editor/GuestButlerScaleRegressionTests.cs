using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class GuestRoomScaleRegressionTests
{
    private const string PointClickPlayerMovementPath = "Assets/Scripts/PointClickPlayerMovement.cs";
    private const string RoomProjectedEntityPath = "Assets/Scripts/Characters/RoomProjectedEntity.cs";
    private const string RoomPersonWalkerPath = "Assets/Scripts/Characters/RoomPersonWalker2D.cs";
    private const string ActorRoomStatePath = "Assets/Scripts/Story/ActorRoomState.cs";
    private const string HarmonizerPath = "Assets/Scripts/Characters/GuestButlerScaleHarmonizer.cs";
    private const string ToolPath = "Assets/Editor/GuestButlerScaleTool.cs";
    private const string GuestRoomScaleCalibrationPath = "Assets/Scripts/Characters/GuestRoomScaleCalibration.cs";
    private const string GuestScaleParticipantPath = "Assets/Scripts/Characters/GuestScaleParticipant.cs";
    private const string GuestRoomScaleApplierPath = "Assets/Scripts/Characters/GuestRoomScaleApplier.cs";
    private const string GuestRoomStageScaleUtilityPath = "Assets/Scripts/Characters/GuestRoomStageScaleUtility.cs";
    private const string GuestPoseScaleOverrideStorePath = "Assets/Scripts/Characters/GuestPoseScaleOverrideStore.cs";
    private const string GuestRoomScaleMasterWindowPath = "Assets/Editor/GuestRoomScaleMasterWindow.cs";
    private const string GuestScaleAuditPath = "Assets/Editor/GuestScaleAudit.cs";

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
    public void GuestRoomScaleCalibrationInitializesFromButler()
    {
        GameObject butler = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
        GameObject calibrationObject = new GameObject("GuestScaleCalibration");

        try
        {
            PointClickPlayerMovement movement = butler.GetComponent<PointClickPlayerMovement>();
            movement.CaptureCurrentTransformAsButlerCalibrationBaseScale();
            movement.SetButlerFrontFinalLocalScaleForRoom("Grand Entrance Hall", -100f, 2f, false);
            movement.SetButlerBackFinalLocalScaleForRoom("Grand Entrance Hall", 100f, 1f, false);
            movement.SetButlerFrontFinalLocalScaleForRoom("Drawing_Room", -100f, 2f, false);
            movement.SetButlerBackFinalLocalScaleForRoom("Drawing_Room", 100f, 1f, false);

            GuestRoomScaleCalibration calibration = calibrationObject.AddComponent<GuestRoomScaleCalibration>();
            calibration.InitializeMissingRoomsFromButler(movement);

            Assert.That(calibration.TryGetRoom("Grand Entrance Hall", out _), Is.True);
            Assert.That(calibration.TryGetRoom("Drawing Room", out _), Is.True);
            Assert.That(calibration.Rooms.Count, Is.EqualTo(2));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(calibrationObject);
            UnityEngine.Object.DestroyImmediate(butler);
        }
    }

    [Test]
    public void GuestRoomScaleCalibrationEvaluatesButlerCurveWithRoomMultiplier()
    {
        GameObject butler = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
        GameObject calibrationObject = new GameObject("GuestScaleCalibration");

        try
        {
            PointClickPlayerMovement movement = butler.GetComponent<PointClickPlayerMovement>();
            ConfigureButlerRoom(movement, "Grand Entrance Hall");

            GuestRoomScaleCalibration calibration = calibrationObject.AddComponent<GuestRoomScaleCalibration>();
            calibration.SetButlerScaleSource(movement);
            calibration.InitializeMissingRoomsFromButler(movement);
            calibration.SetRoomMultiplier("grand_entrance-hall", 1.25f);

            Assert.That(
                calibration.TryEvaluateGuestScale(
                    "Grand Entrance Hall",
                    0f,
                    out float scale,
                    out float depth01,
                    out string diagnostic),
                Is.True,
                diagnostic);
            Assert.That(depth01, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(scale, Is.EqualTo(1.5f * 1.25f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(calibrationObject);
            UnityEngine.Object.DestroyImmediate(butler);
        }
    }

    [Test]
    public void GuestRoomScaleCalibrationManualFrontBackCurveOverridesButlerMatching()
    {
        GameObject butler = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
        GameObject calibrationObject = new GameObject("GuestScaleCalibration");

        try
        {
            PointClickPlayerMovement movement = butler.GetComponent<PointClickPlayerMovement>();
            ConfigureButlerRoom(movement, "Grand Entrance Hall");

            GuestRoomScaleCalibration calibration = calibrationObject.AddComponent<GuestRoomScaleCalibration>();
            calibration.SetButlerScaleSource(movement);
            calibration.InitializeMissingRoomsFromButler(movement);
            calibration.SetRoomMultiplier("Grand Entrance Hall", 5f);
            calibration.SetFront("Grand Entrance Hall", -100f, 2.4f);
            calibration.SetBack("Grand Entrance Hall", 100f, 1.2f);

            Assert.That(
                calibration.TryEvaluateGuestScale(
                    "Grand Entrance Hall",
                    0f,
                    out float manualScale,
                    out float manualDepth,
                    out string manualDiagnostic),
                Is.True,
                manualDiagnostic);
            Assert.That(manualDepth, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(manualScale, Is.EqualTo(1.8f).Within(0.0001f));
            Assert.That(manualDiagnostic, Does.Contain("Custom guest curve"));

            calibration.ClearCustomCurve("Grand Entrance Hall");

            Assert.That(
                calibration.TryEvaluateGuestScale(
                    "Grand Entrance Hall",
                    0f,
                    out float butlerScale,
                    out _,
                    out string butlerDiagnostic),
                Is.True,
                butlerDiagnostic);
            Assert.That(butlerScale, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(butlerDiagnostic, Does.Contain("Butler final local scale"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(calibrationObject);
            UnityEngine.Object.DestroyImmediate(butler);
        }
    }

    [Test]
    public void GuestRoomScaleCalibrationFixedManualScalePersistsExactRoomSize()
    {
        GameObject butler = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
        GameObject calibrationObject = new GameObject("GuestScaleCalibration");

        try
        {
            PointClickPlayerMovement movement = butler.GetComponent<PointClickPlayerMovement>();
            ConfigureButlerRoom(movement, "Drawing Room");

            GuestRoomScaleCalibration calibration = calibrationObject.AddComponent<GuestRoomScaleCalibration>();
            calibration.SetButlerScaleSource(movement);
            calibration.InitializeMissingRoomsFromButler(movement);
            calibration.SetRoomMultiplier("Drawing Room", 0.25f);
            calibration.SetFixedGuestScale("Drawing Room", 1.45f);

            Assert.That(
                calibration.TryEvaluateGuestScale(
                    "Drawing Room",
                    0f,
                    out float fixedScale,
                    out _,
                    out string diagnostic),
                Is.True,
                diagnostic);
            Assert.That(fixedScale, Is.EqualTo(1.45f).Within(0.0001f));
            Assert.That(diagnostic, Does.Contain("Fixed manual guest scale"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(calibrationObject);
            UnityEngine.Object.DestroyImmediate(butler);
        }
    }

    [Test]
    public void GuestRoomScaleCalibrationFrontBackCurveReplacesFixedManualScale()
    {
        GameObject calibrationObject = new GameObject("GuestScaleCalibration");

        try
        {
            GuestRoomScaleCalibration calibration = calibrationObject.AddComponent<GuestRoomScaleCalibration>();
            calibration.SetFixedGuestScale("Drawing Room", 1.45f);
            calibration.SetFront("Drawing Room", -100f, 2.4f);
            calibration.SetBack("Drawing Room", 100f, 1.2f);

            Assert.That(
                calibration.TryEvaluateGuestScale(
                    "Drawing Room",
                    0f,
                    out float curveScale,
                    out _,
                    out string diagnostic),
                Is.True,
                diagnostic);
            Assert.That(curveScale, Is.EqualTo(1.8f).Within(0.0001f));
            Assert.That(diagnostic, Does.Contain("Custom guest curve"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(calibrationObject);
        }
    }

    [Test]
    public void GuestRoomScaleCalibrationCanLoadDepthCurveFromButlerScale()
    {
        GameObject butler = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
        GameObject calibrationObject = new GameObject("GuestScaleCalibration");

        try
        {
            PointClickPlayerMovement movement = butler.GetComponent<PointClickPlayerMovement>();
            movement.CaptureCurrentTransformAsButlerCalibrationBaseScale();
            movement.SetButlerFrontFinalLocalScaleForRoom("Drawing Room", -120f, 2.4f, false);
            movement.SetButlerBackFinalLocalScaleForRoom("Drawing Room", 80f, 1.2f, false);

            GuestRoomScaleCalibration calibration = calibrationObject.AddComponent<GuestRoomScaleCalibration>();
            calibration.SetFixedGuestScale("Drawing Room", 1.45f);

            Assert.That(calibration.LoadCustomCurveFromButlerScale(movement, "Drawing Room"), Is.True);
            Assert.That(calibration.TryGetRoom("Drawing Room", out GuestRoomScaleEntry entry), Is.True);
            Assert.That(entry.useCustomGuestCurve, Is.True);
            Assert.That(entry.useFixedGuestScale, Is.False);
            Assert.That(entry.frontRoomLocalY, Is.EqualTo(-120f).Within(0.0001f));
            Assert.That(entry.frontGuestScale, Is.EqualTo(2.4f).Within(0.0001f));
            Assert.That(entry.backRoomLocalY, Is.EqualTo(80f).Within(0.0001f));
            Assert.That(entry.backGuestScale, Is.EqualTo(1.2f).Within(0.0001f));

            Assert.That(
                calibration.TryEvaluateGuestScale(
                    "Drawing Room",
                    -20f,
                    out float scale,
                    out float depth,
                    out string diagnostic),
                Is.True,
                diagnostic);
            Assert.That(depth, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(scale, Is.EqualTo(1.8f).Within(0.0001f));
            Assert.That(diagnostic, Does.Contain("Custom guest curve"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(calibrationObject);
            UnityEngine.Object.DestroyImmediate(butler);
        }
    }

    [Test]
    public void GuestRoomScaleCalibrationDoesNotLoadIncompleteButlerCurve()
    {
        GameObject butler = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
        GameObject calibrationObject = new GameObject("GuestScaleCalibration");

        try
        {
            PointClickPlayerMovement movement = butler.GetComponent<PointClickPlayerMovement>();
            movement.SetButlerFrontFinalLocalScaleForRoom("Drawing Room", -120f, 2.4f, false);

            GuestRoomScaleCalibration calibration = calibrationObject.AddComponent<GuestRoomScaleCalibration>();

            Assert.That(calibration.LoadCustomCurveFromButlerScale(movement, "Drawing Room"), Is.False);
            Assert.That(calibration.TryGetRoom("Drawing Room", out GuestRoomScaleEntry entry), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(calibrationObject);
            UnityEngine.Object.DestroyImmediate(butler);
        }
    }

    [Test]
    public void SavingRoomGuestSizeStoresReferenceRoomStageScale()
    {
        GameObject calibrationObject = new GameObject("GuestScaleCalibration");

        try
        {
            GuestRoomScaleCalibration calibration = calibrationObject.AddComponent<GuestRoomScaleCalibration>();

            calibration.SetReferenceRoomStageScale("Grand Entrance Hall", 1.75f);

            Assert.That(
                calibration.TryGetReferenceRoomStageScale("Grand_Entrance-Hall", out float stageScale),
                Is.True);
            Assert.That(stageScale, Is.EqualTo(1.75f).Within(0.0001f));

            calibration.SetReferenceRoomStageScale("Grand Entrance Hall", 0f);
            Assert.That(calibration.TryGetReferenceRoomStageScale("Grand Entrance Hall", out stageScale), Is.True);
            Assert.That(stageScale, Is.EqualTo(0.0001f).Within(0.00001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(calibrationObject);
        }
    }

    [Test]
    public void GuestRoomScaleApplierAppliesRoomZoomToGuestsOutsideStage()
    {
        float targetLocalScale = GuestRoomScaleApplier.CalculateTargetLocalScale(
            baseGuestScale: 2f,
            roomStageZoomRatio: 1.5f,
            inheritedRoomStageZoomRatio: 1f);

        Assert.That(targetLocalScale, Is.EqualTo(3f).Within(0.0001f));
    }

    [Test]
    public void GuestRoomScaleApplierDoesNotDoubleZoomGuestsInsideStage()
    {
        float targetLocalScale = GuestRoomScaleApplier.CalculateTargetLocalScale(
            baseGuestScale: 2f,
            roomStageZoomRatio: 1.5f,
            inheritedRoomStageZoomRatio: 1.5f);

        Assert.That(targetLocalScale, Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void GuestEffectiveScaleMatchesRoomZoom()
    {
        float targetLocalScale = GuestRoomScaleApplier.CalculateTargetLocalScale(
            baseGuestScale: 2f,
            roomStageZoomRatio: 1.5f,
            inheritedRoomStageZoomRatio: 1.2f);
        float effectiveScale = targetLocalScale * 1.2f;

        Assert.That(effectiveScale, Is.EqualTo(2f * 1.5f).Within(0.0001f));
    }

    [Test]
    public void GuestRoomStageScaleUtilityCalculatesRoomZoomFromReferenceScale()
    {
        Assert.That(
            GuestRoomStageScaleUtility.CalculateRoomStageZoomRatio(
                currentRoomStageScale: 3f,
                referenceRoomStageScale: 2f),
            Is.EqualTo(1.5f).Within(0.0001f));
    }

    [Test]
    public void GuestScaleParticipantCapturesGuestBaseScale()
    {
        GameObject guest = new GameObject("Guest 1");
        guest.transform.localScale = new Vector3(0.8f, 1.2f, 1f);

        try
        {
            GuestScaleParticipant participant = guest.AddComponent<GuestScaleParticipant>();
            participant.SetScaleRoot(guest.transform);
            participant.CaptureBaseScale(true);
            guest.transform.localScale = Vector3.one * 3f;
            participant.CaptureBaseScale(false);

            Assert.That(participant.HasCapturedBaseScale, Is.True);
            Assert.That(participant.CapturedBaseScale, Is.EqualTo(new Vector3(0.8f, 1.2f, 1f)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
        }
    }

    [Test]
    public void GuestRoomScaleApplierAppliesScaleToGuestPrefabInstances()
    {
        ScaleTestScene scene = CreateScaleTestScene("Grand Entrance Hall", 1.25f);
        GameObject guest = new GameObject("Guest 1");
        guest.transform.localScale = Vector3.one;

        try
        {
            GuestScaleParticipant participant = guest.AddComponent<GuestScaleParticipant>();
            participant.SetRoomIdOverride("Grand Entrance Hall");
            participant.SetScaleRoot(guest.transform);
            participant.CaptureBaseScale(true);

            scene.Applier.RefreshAllNow();

            Assert.That(guest.transform.localScale.y, Is.EqualTo(1.5f * 1.25f).Within(0.0001f));
        }
        finally
        {
            DestroyScaleTestScene(scene);
            UnityEngine.Object.DestroyImmediate(guest);
        }
    }

    [Test]
    public void GuestRoomScaleApplierUsesWalkerTargetGraphic()
    {
        ScaleTestScene scene = CreateScaleTestScene("Grand Entrance Hall", 1.1f);
        GameObject walkerObject = new GameObject("Guest 9", typeof(RectTransform));
        walkerObject.AddComponent<Image>();
        RoomPersonWalker2D walker = walkerObject.AddComponent<RoomPersonWalker2D>();
        walkerObject.transform.localScale = Vector3.one * 4f;
        Graphic targetGraphic = walkerObject.GetComponent<Graphic>();
        targetGraphic.rectTransform.localScale = Vector3.one;

        try
        {
            GuestScaleParticipant participant = walkerObject.AddComponent<GuestScaleParticipant>();
            participant.SetRoomIdOverride("Grand Entrance Hall");
            participant.CaptureBaseScale(true);

            scene.Applier.RefreshAllNow();

            Assert.That(targetGraphic.rectTransform.localScale.y, Is.EqualTo(1.5f * 1.1f).Within(0.0001f));
            Assert.That(walkerObject.transform.localScale.y, Is.EqualTo(4f).Within(0.0001f));
        }
        finally
        {
            DestroyScaleTestScene(scene);
            UnityEngine.Object.DestroyImmediate(walkerObject);
        }
    }

    [Test]
    public void GuestRoomScaleApplierDoesNotCreateParticipantsForLegacyWalkers()
    {
        GameObject applierObject = new GameObject("GuestScaleApplier");
        GameObject legacyWalker = new GameObject("Walker_GEH_GreenGentleman", typeof(RectTransform));

        try
        {
            legacyWalker.AddComponent<Image>();
            legacyWalker.AddComponent<RoomPersonWalker2D>();
            GuestRoomScaleApplier applier = applierObject.AddComponent<GuestRoomScaleApplier>();

            int ensured = applier.EnsureParticipantsForSceneGuests();

            Assert.That(ensured, Is.EqualTo(0));
            Assert.That(legacyWalker.GetComponent<GuestScaleParticipant>(), Is.Null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(legacyWalker);
            UnityEngine.Object.DestroyImmediate(applierObject);
        }
    }

    [Test]
    public void GuestRoomScaleApplierClassifiesManagedChapterGuestsOnly()
    {
        GameObject chapterGuest = new GameObject("Guest 5");
        GameObject legacyWalker = new GameObject("Walker_GEH_GreenLady");

        try
        {
            GuestScaleParticipant chapterParticipant = chapterGuest.AddComponent<GuestScaleParticipant>();
            GuestScaleParticipant walkerParticipant = legacyWalker.AddComponent<GuestScaleParticipant>();

            Assert.That(GuestRoomScaleApplier.IsManagedGuestParticipant(chapterParticipant), Is.True);
            Assert.That(GuestRoomScaleApplier.IsManagedGuestParticipant(walkerParticipant), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(legacyWalker);
            UnityEngine.Object.DestroyImmediate(chapterGuest);
        }
    }

    [Test]
    public void GuestRoomScaleApplierIgnoresExistingLegacyWalkerParticipants()
    {
        ScaleTestScene scene = CreateScaleTestScene("Grand Entrance Hall", 1.1f);
        GameObject legacyWalker = new GameObject("Walker_GEH_GreenLady", typeof(RectTransform));

        try
        {
            legacyWalker.AddComponent<Image>();
            legacyWalker.AddComponent<RoomPersonWalker2D>();
            GuestScaleParticipant participant = legacyWalker.AddComponent<GuestScaleParticipant>();
            participant.SetRoomIdOverride("Grand Entrance Hall");
            participant.SetScaleRoot(legacyWalker.transform);
            participant.CaptureBaseScale(true);

            GuestScaleApplyResult result = scene.Applier.RefreshAllWithResultNow();

            Assert.That(result.Applied, Is.EqualTo(0));
            Assert.That(legacyWalker.transform.localScale.y, Is.EqualTo(1f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(legacyWalker);
            DestroyScaleTestScene(scene);
        }
    }

    [Test]
    public void GuestRoomScaleApplierUsesProjectedVisualRootForFloorCharacters()
    {
        ScaleTestScene scene = CreateScaleTestScene("Grand Entrance Hall", 0.9f);
        RoomPerspectiveProfile profile = CreateProfile(1f, 1f, "Grand Entrance Hall");
        RoomProjectedEntity entity = CreateProjectedEntity("Guest 10", profile, Vector2.zero);
        Transform visualRoot = entity.VisualRoot;

        try
        {
            GuestScaleParticipant participant = entity.gameObject.AddComponent<GuestScaleParticipant>();
            participant.SetRoomIdOverride("Grand Entrance Hall");
            participant.CaptureBaseScale(true);

            scene.Applier.RefreshAllNow();

            Assert.That(visualRoot.localScale.y, Is.EqualTo(1.5f * 0.9f).Within(0.0001f));
        }
        finally
        {
            DestroyEntity(entity);
            UnityEngine.Object.DestroyImmediate(profile);
            DestroyScaleTestScene(scene);
        }
    }

    [Test]
    public void GuestRoomScaleApplierIgnoresCoatVisuals()
    {
        ScaleTestScene scene = CreateScaleTestScene("Grand Entrance Hall", 1.4f);
        GameObject guest = new GameObject("Guest 2");
        GameObject coat = new GameObject("Coat_Guest2");
        coat.transform.SetParent(guest.transform, false);
        coat.transform.localScale = new Vector3(8f, 9f, 1f);

        try
        {
            GuestScaleParticipant participant = guest.AddComponent<GuestScaleParticipant>();
            participant.SetRoomIdOverride("Grand Entrance Hall");
            participant.CaptureBaseScale(true);

            scene.Applier.RefreshAllNow();

            Assert.That(guest.transform.localScale.y, Is.EqualTo(1.5f * 1.4f).Within(0.0001f));
            Assert.That(coat.transform.localScale, Is.EqualTo(new Vector3(8f, 9f, 1f)));
            Assert.That(GuestScaleParticipant.NameLooksExcludedFromBodyScale(coat.name), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
            DestroyScaleTestScene(scene);
        }
    }

    [Test]
    public void TakingAndReturningCoatDoesNotChangeGuestBodyScale()
    {
        ScaleTestScene scene = CreateScaleTestScene("Grand Entrance Hall", 1.3f);
        GameObject butler = new GameObject("Butler");
        GameObject guest = new GameObject("Guest 3");
        GameObject coat = new GameObject("Guest3_Coat");
        coat.transform.SetParent(guest.transform, false);

        try
        {
            GuestScaleParticipant participant = guest.AddComponent<GuestScaleParticipant>();
            participant.SetRoomIdOverride("Grand Entrance Hall");
            participant.CaptureBaseScale(true);

            scene.Applier.RefreshAllNow();
            Vector3 firstScale = guest.transform.localScale;

            coat.transform.SetParent(butler.transform, false);
            scene.Applier.RefreshAllNow();
            Assert.That(guest.transform.localScale, Is.EqualTo(firstScale));

            coat.transform.SetParent(guest.transform, false);
            scene.Applier.RefreshAllNow();
            Assert.That(guest.transform.localScale, Is.EqualTo(firstScale));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
            UnityEngine.Object.DestroyImmediate(butler);
            DestroyScaleTestScene(scene);
        }
    }

    [Test]
    public void EntranceGuestsUseGrandEntranceHallRoomMultiplier()
    {
        ScaleTestScene scene = CreateScaleTestScene("Grand Entrance Hall", 2f);
        GameObject guest = new GameObject("Guest 4");

        try
        {
            GuestScaleParticipant participant = guest.AddComponent<GuestScaleParticipant>();
            participant.SetCharacterId("Guest 4");
            participant.SetRoomIdOverride("Grand_Entrance-Hall");
            participant.CaptureBaseScale(true);

            scene.Applier.RefreshAllNow();

            Assert.That(guest.transform.localScale.y, Is.EqualTo(3f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
            DestroyScaleTestScene(scene);
        }
    }

    [Test]
    public void GuestRoomScaleApplierRefreshRoomNowTargetsSelectedRoomOnly()
    {
        ScaleTestScene scene = CreateScaleTestScene("Grand Entrance Hall", 2f);
        GameObject entranceGuest = new GameObject("Guest Entrance");
        GameObject drawingGuest = new GameObject("Guest Drawing");

        try
        {
            GuestScaleParticipant entranceParticipant = entranceGuest.AddComponent<GuestScaleParticipant>();
            entranceParticipant.SetRoomIdOverride("Grand_Entrance-Hall");
            entranceParticipant.SetScaleRoot(entranceGuest.transform);
            entranceParticipant.CaptureBaseScale(true);

            GuestScaleParticipant drawingParticipant = drawingGuest.AddComponent<GuestScaleParticipant>();
            drawingParticipant.SetRoomIdOverride("Drawing Room");
            drawingParticipant.SetScaleRoot(drawingGuest.transform);
            drawingParticipant.CaptureBaseScale(true);
            scene.Calibration.SetRoomMultiplier("Drawing Room", 3f);

            GuestScaleApplyResult result = scene.Applier.RefreshRoomNow("Grand Entrance Hall");

            Assert.That(result.Applied, Is.EqualTo(1));
            Assert.That(result.Changed, Is.EqualTo(1));
            Assert.That(entranceGuest.transform.localScale.y, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(drawingGuest.transform.localScale.y, Is.EqualTo(1f).Within(0.0001f));

            GuestScaleApplyResult repeatedResult = scene.Applier.RefreshRoomNow("Grand Entrance Hall");
            Assert.That(repeatedResult.Applied, Is.EqualTo(1));
            Assert.That(repeatedResult.Changed, Is.EqualTo(0));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(entranceGuest);
            UnityEngine.Object.DestroyImmediate(drawingGuest);
            DestroyScaleTestScene(scene);
        }
    }

    [Test]
    public void SeatedGuestsDoNotAutoShrinkWithoutExplicitOverride()
    {
        ScaleTestScene scene = CreateScaleTestScene("Drawing Room", 1f);
        GameObject guest = new GameObject("Guest 5");

        try
        {
            GuestScaleParticipant participant = guest.AddComponent<GuestScaleParticipant>();
            participant.SetRoomIdOverride("Drawing Room");
            participant.SetPose(CharacterPose.Seated);
            participant.CaptureBaseScale(true);

            scene.Applier.RefreshAllNow();

            Assert.That(guest.transform.localScale.y, Is.EqualTo(1.5f).Within(0.0001f));

            participant.SetSeatedRatioOverride(0.9f);
            scene.Applier.RefreshAllNow();
            Assert.That(guest.transform.localScale.y, Is.EqualTo(1.5f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
            DestroyScaleTestScene(scene);
        }
    }

    [Test]
    public void GuestScaleParticipantAppliesTargetButlerScaleFromCapturedBaseAspect()
    {
        GameObject guest = new GameObject("Guest Aspect");
        guest.transform.localScale = new Vector3(2f, 4f, 9f);

        try
        {
            GuestScaleParticipant participant = guest.AddComponent<GuestScaleParticipant>();
            participant.SetScaleRoot(guest.transform);
            participant.CaptureBaseScale(true);

            bool changed = participant.ApplyFinalScale(1f);

            Assert.That(changed, Is.True);
            Assert.That(guest.transform.localScale, Is.EqualTo(new Vector3(0.5f, 1f, 9f)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
        }
    }

    [Test]
    public void GuestRoomScaleApplierDoesNotCreateParticipantsForGuestScaleInfrastructure()
    {
        GameObject applierObject = new GameObject("GuestRoomScaleApplier");
        GameObject calibrationObject = new GameObject("GuestRoomScaleCalibration");
        GameObject realGuest = new GameObject("Guest 6");

        try
        {
            GuestRoomScaleApplier applier = applierObject.AddComponent<GuestRoomScaleApplier>();
            calibrationObject.AddComponent<GuestRoomScaleCalibration>();

            int ensured = applier.EnsureParticipantsForSceneGuests();

            Assert.That(ensured, Is.EqualTo(1));
            Assert.That(realGuest.GetComponent<GuestScaleParticipant>(), Is.Not.Null);
            Assert.That(applierObject.GetComponent<GuestScaleParticipant>(), Is.Null);
            Assert.That(calibrationObject.GetComponent<GuestScaleParticipant>(), Is.Null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(realGuest);
            UnityEngine.Object.DestroyImmediate(calibrationObject);
            UnityEngine.Object.DestroyImmediate(applierObject);
        }
    }

    [Test]
    public void AuthoredChapterGuestsInferRoomBeforeRuntimeStateExists()
    {
        GameObject drawingRoomGuest = new GameObject("Guest 6");
        GameObject entranceGuest = new GameObject("Guest 2");

        try
        {
            Assert.That(
                GuestRoomScaleApplier.TryInferAuthoredSceneGuestRoomId(drawingRoomGuest, out string drawingRoomId),
                Is.True);
            Assert.That(drawingRoomId, Is.EqualTo("Drawing Room"));

            Assert.That(
                GuestRoomScaleApplier.TryInferAuthoredSceneGuestRoomId(entranceGuest, out string entranceRoomId),
                Is.True);
            Assert.That(entranceRoomId, Is.EqualTo("Grand Entrance Hall"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(drawingRoomGuest);
            UnityEngine.Object.DestroyImmediate(entranceGuest);
        }
    }

    [Test]
    public void GuestScaleParticipantUsesResolvedRoomContentForTopLevelWorldGuestY()
    {
        GameObject roomObject = new GameObject("Room_Drawing_Room");
        GameObject guest = new GameObject("Guest 6");

        try
        {
            roomObject.transform.position = new Vector3(10f, 100f, 0f);
            RoomContentGroup room = roomObject.AddComponent<RoomContentGroup>();
            room.SetRoomName("Drawing Room");

            guest.transform.position = new Vector3(15f, 125f, 0f);
            GuestScaleParticipant participant = guest.AddComponent<GuestScaleParticipant>();
            participant.SetRoomIdOverride("Drawing Room");
            participant.SetScaleRoot(guest.transform);

            Assert.That(participant.ResolveRoomLocalY(), Is.EqualTo(25f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
            UnityEngine.Object.DestroyImmediate(roomObject);
        }
    }

    [Test]
    public void GuestScaleParticipantPrefersLiveActorRoomOverStaleOverride()
    {
        GameObject guest = new GameObject("Guest 1");

        try
        {
            ActorRoomState actorState = guest.AddComponent<ActorRoomState>();
            actorState.SetCurrentRoom("Drawing Room");

            GuestScaleParticipant participant = guest.AddComponent<GuestScaleParticipant>();
            participant.SetRoomIdOverride("Grand Entrance Hall");

            Assert.That(participant.ResolveCurrentRoomId(), Is.EqualTo("Drawing Room"));
            Assert.That(participant.ResolveRoomId(), Is.EqualTo("Drawing Room"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
        }
    }

    [Test]
    public void GuestRoomScaleApplierInfersLiveActorRoomBeforeStaleParticipantOverride()
    {
        GameObject guest = new GameObject("Guest 1");

        try
        {
            ActorRoomState actorState = guest.AddComponent<ActorRoomState>();
            actorState.SetCurrentRoom("Drawing Room");

            GuestScaleParticipant participant = guest.AddComponent<GuestScaleParticipant>();
            participant.SetRoomIdOverride("Grand Entrance Hall");

            Assert.That(
                GuestRoomScaleApplier.TryInferAuthoredSceneGuestRoomId(guest, out string roomId),
                Is.True);
            Assert.That(roomId, Is.EqualTo("Drawing Room"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
        }
    }

    [Test]
    public void GuestScaleParticipantLiveCurrentRoomBeatsStaleOverride()
    {
        GameObject guest = new GameObject("Guest 1");

        try
        {
            GuestScaleParticipant participant = guest.AddComponent<GuestScaleParticipant>();
            participant.SetRoomIdOverride("Grand Entrance Hall");
            participant.SetCurrentRoomId("Drawing Room");

            Assert.That(participant.CurrentRoomId, Is.EqualTo("Drawing Room"));
            Assert.That(participant.ResolveRoomId(), Is.EqualTo("Drawing Room"));
            Assert.That(participant.LastRoomResolutionSource, Is.EqualTo("CurrentRoomId"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
        }
    }

    [Test]
    public void GuestScaleParticipantLiveCurrentRoomBeatsStaleProjectedProfile()
    {
        RoomPerspectiveProfile entranceProfile = CreateProfile(1f, 1f, "Grand Entrance Hall");
        RoomProjectedEntity entity = CreateProjectedEntity("Guest 1", entranceProfile, Vector2.zero);

        try
        {
            GuestScaleParticipant participant = entity.gameObject.AddComponent<GuestScaleParticipant>();
            participant.SetCurrentRoomId("Drawing Room");

            Assert.That(participant.ResolveRoomId(), Is.EqualTo("Drawing Room"));
            Assert.That(participant.LastRoomResolutionSource, Is.EqualTo("CurrentRoomId"));
        }
        finally
        {
            DestroyEntity(entity);
            UnityEngine.Object.DestroyImmediate(entranceProfile);
        }
    }

    [Test]
    public void ActorRoomStateSetCurrentRoomUpdatesGuestScaleParticipant()
    {
        GameObject guest = new GameObject("Guest 1");

        try
        {
            GuestScaleParticipant participant = guest.AddComponent<GuestScaleParticipant>();
            ActorRoomState actorState = guest.AddComponent<ActorRoomState>();

            actorState.SetCurrentRoom("Drawing Room");

            Assert.That(participant.CurrentRoomId, Is.EqualTo("Drawing Room"));
            Assert.That(participant.ResolveRoomId(), Is.EqualTo("Drawing Room"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
        }
    }

    [Test]
    public void GuestRoomScaleApplierEnsureParticipantCanSetCurrentRoom()
    {
        GameObject guest = new GameObject("Guest 1");

        try
        {
            GuestScaleParticipant participant = GuestRoomScaleApplier.EnsureParticipantForGuestObject(
                guest,
                "Guest 1",
                "Drawing Room",
                CharacterPose.Standing,
                true);

            Assert.That(participant.CurrentRoomId, Is.EqualTo("Drawing Room"));
            Assert.That(participant.ResolveRoomId(), Is.EqualTo("Drawing Room"));
            Assert.That(participant.LastRoomResolutionSource, Is.EqualTo("CurrentRoomId"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
        }
    }

    [Test]
    public void GuestRoomScaleMasterIncludesLiveDrawingRoomGuestsDespiteStaleOverrides()
    {
        GuestScaleParticipant[] guests = new GuestScaleParticipant[8];
        GameObject[] guestObjects = new GameObject[8];

        try
        {
            for (int i = 0; i < guests.Length; i++)
            {
                guestObjects[i] = new GameObject($"Guest {i + 1}");
                guests[i] = guestObjects[i].AddComponent<GuestScaleParticipant>();
                guests[i].SetCharacterId($"Guest {i + 1}");
                guests[i].SetRoomIdOverride(i < 4 ? "Grand Entrance Hall" : "Drawing Room");
                guests[i].SetCurrentRoomId("Drawing Room");
            }

            MethodInfo method = typeof(GuestRoomScaleMasterWindow).GetMethod(
                "FindGuestsInRoom",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            GuestScaleParticipant[] roomGuests = (GuestScaleParticipant[])method.Invoke(
                null,
                new object[] { guests, "Drawing Room" });

            Assert.That(roomGuests, Has.Length.EqualTo(8));
        }
        finally
        {
            for (int i = 0; i < guestObjects.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(guestObjects[i]);
            }
        }
    }

    [Test]
    public void GuestRoomScaleMasterTreatsVisibleGuestsAsSelectedRoomInEditMode()
    {
        GuestScaleParticipant[] guests = new GuestScaleParticipant[8];
        GameObject[] guestObjects = new GameObject[8];

        try
        {
            for (int i = 0; i < guests.Length; i++)
            {
                guestObjects[i] = new GameObject($"Guest {i + 1}");
                guestObjects[i].AddComponent<SpriteRenderer>();
                guests[i] = guestObjects[i].AddComponent<GuestScaleParticipant>();
                guests[i].SetCharacterId($"Guest {i + 1}");
                guests[i].SetRoomIdOverride(i < 4 ? "Grand Entrance Hall" : "Drawing Room");
                guests[i].SetCurrentRoomId("Drawing Room");
            }

            MethodInfo method = typeof(GuestRoomScaleMasterWindow).GetMethod(
                "FindGuestsInRoom",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            GuestScaleParticipant[] roomGuests = (GuestScaleParticipant[])method.Invoke(
                null,
                new object[] { guests, "Grand Entrance Hall" });

            Assert.That(roomGuests, Has.Length.EqualTo(8));
        }
        finally
        {
            for (int i = 0; i < guestObjects.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(guestObjects[i]);
            }
        }
    }

    [Test]
    public void GuestRoomScaleApplierAppliesExplicitRoomContextDespiteStaleCurrentRoom()
    {
        ScaleTestScene scene = CreateScaleTestScene("Grand Entrance Hall", 1f);
        GameObject guest = new GameObject("Guest 1");

        try
        {
            guest.AddComponent<SpriteRenderer>();
            GuestScaleParticipant participant = guest.AddComponent<GuestScaleParticipant>();
            participant.SetCharacterId("Guest 1");
            participant.SetRoomIdOverride("Grand Entrance Hall");
            participant.SetCurrentRoomId("Drawing Room");
            participant.SetScaleRoot(guest.transform);
            participant.CaptureBaseScale(true);

            scene.Calibration.SetFixedGuestScale("Grand Entrance Hall", 1.5f);
            scene.Calibration.SetFixedGuestScale("Drawing Room", 0.5f);
            GuestScaleApplyResult result = scene.Applier.RefreshRoomNow("Grand Entrance Hall");

            Assert.That(result.Applied, Is.EqualTo(1));
            Assert.That(guest.transform.localScale.y, Is.EqualTo(1.5f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
            DestroyScaleTestScene(scene);
        }
    }

    [Test]
    public void GuestRoomScaleMasterCanAdoptVisibleGuestsIntoSelectedRoomForManualEditing()
    {
        GuestScaleParticipant[] guests = new GuestScaleParticipant[8];
        GameObject[] guestObjects = new GameObject[8];

        try
        {
            for (int i = 0; i < guests.Length; i++)
            {
                guestObjects[i] = new GameObject($"Guest {i + 1}");
                guestObjects[i].AddComponent<SpriteRenderer>();
                guests[i] = guestObjects[i].AddComponent<GuestScaleParticipant>();
                guests[i].SetCharacterId($"Guest {i + 1}");
                guests[i].SetRoomIdOverride(i < 4 ? "Grand Entrance Hall" : "Drawing Room");
            }

            int synced = GuestRoomScaleMasterWindow.SyncVisibleGuestsToSelectedRoomForManualEditing(
                guests,
                "Drawing Room");

            MethodInfo findMethod = typeof(GuestRoomScaleMasterWindow).GetMethod(
                "FindGuestsInRoom",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(findMethod, Is.Not.Null);

            GuestScaleParticipant[] roomGuests = (GuestScaleParticipant[])findMethod.Invoke(
                null,
                new object[] { guests, "Drawing Room" });

            Assert.That(synced, Is.EqualTo(8));
            Assert.That(roomGuests, Has.Length.EqualTo(8));

            for (int i = 0; i < guests.Length; i++)
            {
                Assert.That(guests[i].CurrentRoomId, Is.EqualTo("Drawing Room"));
            }
        }
        finally
        {
            for (int i = 0; i < guestObjects.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(guestObjects[i]);
            }
        }
    }

    [Test]
    public void GuestRoomScaleMasterUsesButlerRoomSelectionSources()
    {
        GameObject butler = CreatePointClickPlayer("player", Vector3.one);

        try
        {
            PointClickPlayerMovement movement = butler.GetComponent<PointClickPlayerMovement>();
            movement.SetEditorSelectedButlerScaleRoomId("Drawing Room");

            string[] rooms = GuestRoomScaleMasterWindow.BuildRoomOptions(null, movement);
            int selectedIndex = 0;
            string selectedRoom = GuestRoomScaleMasterWindow.ResolveSelectedRoom(
                movement,
                rooms,
                ref selectedIndex);

            Assert.That(rooms, Does.Contain("Drawing Room"));
            Assert.That(selectedRoom, Is.EqualTo("Drawing Room"));

            GuestRoomScaleMasterWindow.SelectGuestScaleRoom(movement, "Grand Entrance Hall");

            Assert.That(movement.EditorSelectedButlerScaleRoomId, Is.EqualTo("Grand Entrance Hall"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(butler);
        }
    }

    [Test]
    public void EntranceGuestsRemainEntranceBeforeMove()
    {
        GameObject guest = new GameObject("Guest 1");

        try
        {
            GuestScaleParticipant participant = guest.AddComponent<GuestScaleParticipant>();
            participant.SetRoomIdOverride("Grand Entrance Hall");
            participant.SetCurrentRoomId("Grand Entrance Hall");

            Assert.That(participant.ResolveRoomId(), Is.EqualTo("Grand Entrance Hall"));
            Assert.That(participant.LastRoomResolutionSource, Is.EqualTo("CurrentRoomId"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
        }
    }

    [Test]
    public void ActiveNavigationDoesNotMisclassifyInactiveGuests()
    {
        GameObject guest = new GameObject("Guest 1");

        try
        {
            guest.SetActive(false);
            GuestScaleParticipant participant = guest.AddComponent<GuestScaleParticipant>();
            participant.SetRoomIdOverride("Grand Entrance Hall");

            Assert.That(participant.ResolveRoomId(), Is.EqualTo("Grand Entrance Hall"));
            Assert.That(participant.LastRoomResolutionSource, Is.EqualTo("RoomIdOverride"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
        }
    }

    [Test]
    public void ResolveRoomTraceReportsChosenSource()
    {
        GameObject guest = new GameObject("Guest 1");

        try
        {
            GuestScaleParticipant participant = guest.AddComponent<GuestScaleParticipant>();
            participant.SetRoomIdOverride("Grand Entrance Hall");
            participant.SetCurrentRoomId("Drawing Room");

            GuestRoomResolutionTrace trace = participant.BuildRoomResolutionTrace("Drawing Room");

            Assert.That(trace.CharacterId, Is.EqualTo("Guest 1"));
            Assert.That(trace.FinalRoomId, Is.EqualTo("Drawing Room"));
            Assert.That(trace.FinalSource, Is.EqualTo("CurrentRoomId"));
            Assert.That(trace.IncludedInSelectedRoom, Is.True);
            Assert.That(trace.ExclusionReason, Is.Empty);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
        }
    }

    [Test]
    public void GuestScaleOwnershipGuardsAllScaleWriters()
    {
        string movementText = File.ReadAllText(PointClickPlayerMovementPath);
        string actorRoomStateText = File.ReadAllText(ActorRoomStatePath);
        string projectedText = File.ReadAllText(RoomProjectedEntityPath);
        string walkerText = File.ReadAllText(RoomPersonWalkerPath);

        Assert.That(movementText, Does.Contain("HasActiveGuestScaleParticipant"), "PointClickPlayerMovement should not apply perspective or room-stage zoom scale to guests.");
        Assert.That(actorRoomStateText, Does.Contain("GetComponentInChildren<GuestScaleParticipant>"), "ActorRoomState should find guest ownership even when the checked transform is not the participant root.");
        Assert.That(projectedText, Does.Contain("GetComponentInChildren<GuestScaleParticipant>"), "RoomProjectedEntity should find guest ownership even when the visual root and participant live on different transforms.");
        Assert.That(walkerText, Does.Contain("GetComponentInParent<GuestScaleParticipant>"), "RoomPersonWalker2D should find guest ownership when the walker is not the participant root.");
        Assert.That(walkerText, Does.Contain("GetComponentInChildren<GuestScaleParticipant>"), "RoomPersonWalker2D should find guest ownership when the target graphic contains the participant.");
    }

    [Test]
    public void GuestSizeMasterHasSimplePrimaryWorkflow()
    {
        string text = File.ReadAllText(GuestRoomScaleMasterWindowPath);

        Assert.That(text, Does.Contain("Guest Size Master"));
        Assert.That(text, Does.Contain("Guest Size In This Room"));
        Assert.That(text, Does.Contain("SET UP GUEST SCALING"));
        Assert.That(text, Does.Contain("PREVIEW ROOM GUEST SIZE"));
        Assert.That(text, Does.Contain("SAVE ROOM GUEST SIZE"));
        Assert.That(text, Does.Contain("APPLY TO ALL GUESTS IN ROOM"));
        Assert.That(text, Does.Contain("SAVE SCENE"));
    }

    [Test]
    public void GuestSizeMasterHasManualFrontBackGuestCurveWorkflow()
    {
        string text = File.ReadAllText(GuestRoomScaleMasterWindowPath);

        Assert.That(text, Does.Contain("Guest Depth Scale Curve"));
        Assert.That(text, Does.Contain("Selected Guest"));
        Assert.That(text, Does.Contain("Manual Guest Scale"));
        Assert.That(text, Does.Contain("Closest Guest Scale"));
        Assert.That(text, Does.Contain("Furthest Guest Scale"));
        Assert.That(text, Does.Contain("DrawDepthPointScaleControls"));
        Assert.That(text, Does.Contain("PREVIEW SELECTED GUEST SIZE"));
        Assert.That(text, Does.Contain("LOAD FROM BUTLER SCALE"));
        Assert.That(text, Does.Contain("SAVE CLOSEST POINT FROM SELECTED GUEST"));
        Assert.That(text, Does.Contain("SAVE FURTHEST POINT FROM SELECTED GUEST"));
        Assert.That(text, Does.Contain("PREVIEW GUEST DEPTH CURVE IN ROOM"));
        Assert.That(text, Does.Contain("CLEAR MANUAL CURVE"));
        Assert.That(text, Does.Contain("ResolveSelectedGuest"));
    }

    [Test]
    public void GuestSizeMasterHasExplicitManualGuestSelectionAndAllGuestWorkflow()
    {
        string text = File.ReadAllText(GuestRoomScaleMasterWindowPath);

        Assert.That(GuestRoomScaleMasterWindow.ActiveSelectionGuestOptionLabel, Is.EqualTo("Active Hierarchy Selection"));
        Assert.That(GuestRoomScaleMasterWindow.AllGuestsSelectionLabel, Is.EqualTo("All Guests In Selected Room"));
        Assert.That(GuestRoomScaleMasterWindow.AllGuestsInAllRoomsSelectionLabel, Is.EqualTo("All Guests In All Rooms"));
        Assert.That(GuestRoomScaleMasterWindow.ApplyManualSizeToAllGuestsButtonLabel, Is.EqualTo("APPLY MANUAL SIZE TO SELECTED ROOM"));
        Assert.That(GuestRoomScaleMasterWindow.ApplyManualSizeToAllRoomsButtonLabel, Is.EqualTo("APPLY MANUAL SIZE TO ALL ROOMS"));
        Assert.That(text, Does.Contain("OnSelectionChange"));
        Assert.That(text, Does.Contain("DrawManualGuestSelection"));
        Assert.That(text, Does.Contain("PreviewAllGuestsManualScale"));
        Assert.That(text, Does.Contain("PreviewAllRoomsManualScale"));
        Assert.That(text, Does.Contain("Manual Guest"));
    }

    [Test]
    public void GuestSizeMasterPreviewUsesSelectedRoomRefreshWithVisibleFeedback()
    {
        string text = File.ReadAllText(GuestRoomScaleMasterWindowPath);

        Assert.That(text, Does.Contain("PreviewSelectedRoom(selectedRoom)"));
        Assert.That(text, Does.Contain("RefreshRoomNow(selectedRoom)"));
        Assert.That(text, Does.Contain("SceneView.RepaintAll()"));
        Assert.That(text, Does.Contain("changed"));
    }

    [Test]
    public void GuestSizeMasterSavesManualRoomSizeAsFixedCalibration()
    {
        GameObject calibrationObject = new GameObject("GuestScaleCalibration");

        try
        {
            GuestRoomScaleCalibration calibration = calibrationObject.AddComponent<GuestRoomScaleCalibration>();

            GuestRoomScaleMasterWindow.SaveRoomGuestSizeForCalibration(
                calibration,
                "Drawing Room",
                0.25f,
                1.45f,
                true);

            Assert.That(
                calibration.TryEvaluateGuestScale(
                    "Drawing Room",
                    0f,
                    out float savedScale,
                    out _,
                    out string diagnostic),
                Is.True,
                diagnostic);
            Assert.That(savedScale, Is.EqualTo(1.45f).Within(0.0001f));
            Assert.That(diagnostic, Does.Contain("Fixed manual guest scale"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(calibrationObject);
        }
    }

    [Test]
    public void GuestSizeMasterSavesExplicitClosestAndFurthestCurveScales()
    {
        GameObject calibrationObject = new GameObject("GuestScaleCalibration");

        try
        {
            GuestRoomScaleCalibration calibration = calibrationObject.AddComponent<GuestRoomScaleCalibration>();

            GuestRoomScaleMasterWindow.SaveGuestDepthCurvePointForCalibration(
                calibration,
                "Dining Room",
                -180f,
                2.25f,
                true);
            GuestRoomScaleMasterWindow.SaveGuestDepthCurvePointForCalibration(
                calibration,
                "Dining Room",
                -20f,
                0.8f,
                false);

            Assert.That(
                calibration.TryEvaluateGuestScale(
                    "Dining Room",
                    -100f,
                    out float savedScale,
                    out float depth,
                    out string diagnostic),
                Is.True,
                diagnostic);
            Assert.That(depth, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(savedScale, Is.EqualTo(1.525f).Within(0.0001f));
            Assert.That(diagnostic, Does.Contain("Custom guest curve"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(calibrationObject);
        }
    }

    [Test]
    public void GuestRoomScaleMasterShowsStageScaleStatus()
    {
        string text = File.ReadAllText(GuestRoomScaleMasterWindowPath);

        Assert.That(text, Does.Contain("Current room-stage scale"));
        Assert.That(text, Does.Contain("Saved reference room-stage scale"));
        Assert.That(text, Does.Contain("Computed room-stage zoom ratio"));
        Assert.That(text, Does.Contain("Guests in selected room"));
        Assert.That(text, Does.Contain("SetReferenceRoomStageScale"));
    }

    [Test]
    public void GuestScaleApplierReadsRoomStageZoomThroughUtility()
    {
        string applierText = File.ReadAllText(GuestRoomScaleApplierPath);

        Assert.That(applierText, Does.Contain("GuestRoomStageScaleUtility.TryGetCurrentRoomStageZoomRatio"));
        Assert.That(applierText, Does.Contain("GuestRoomStageScaleUtility.TryGetInheritedRoomStageZoomRatio"));
        Assert.That(applierText, Does.Contain("CalculateTargetLocalScale"));
        Assert.That(applierText, Does.Not.Contain("Camera.main"));
    }

    [Test]
    public void DebugButtonsAreAdvancedOnly()
    {
        string text = File.ReadAllText(GuestRoomScaleMasterWindowPath);

        Assert.That(text, Does.Contain("advancedFoldout"));
        Assert.That(text.IndexOf("Proof shrink", StringComparison.Ordinal), Is.GreaterThan(text.IndexOf("advancedFoldout", StringComparison.Ordinal)));
        Assert.That(text.IndexOf("Emergency restore captured base scales", StringComparison.Ordinal), Is.GreaterThan(text.IndexOf("advancedFoldout", StringComparison.Ordinal)));
    }

    [Test]
    public void OldGuestButlerScaleHarmonizerIsRemovedOrObsolete()
    {
        if (File.Exists(HarmonizerPath))
        {
            string text = File.ReadAllText(HarmonizerPath);
            Assert.That(text, Does.Contain("[Obsolete"));
            Assert.That(text, Does.Not.Contain("ApplyButlerCharacterScaleNow(source, debugGuestScaleMultiplier)"));
        }

        if (File.Exists(ToolPath))
        {
            string text = File.ReadAllText(ToolPath);
            Assert.That(text, Does.Contain("[Obsolete"));
            Assert.That(text, Does.Not.Contain("Refresh Guest Scaling Now"));
        }
    }

    [Test]
    public void OldApplyButlerCharacterScalePathsAreNotCalledByNewApplier()
    {
        string applierText = File.ReadAllText(GuestRoomScaleApplierPath);

        Assert.That(applierText, Does.Not.Contain("ApplyButlerCharacterScaleNow"));
        Assert.That(applierText, Does.Not.Contain("ForceApplyButlerCharacterScale"));
        Assert.That(applierText, Does.Not.Contain("ApplyButlerScaleSample"));
        Assert.That(applierText, Does.Not.Contain("BuildButlerActorScale"));
    }

    [Test]
    public void NewGuestScaleFilesExist()
    {
        Assert.That(File.Exists(GuestRoomScaleCalibrationPath), Is.True);
        Assert.That(File.Exists(GuestScaleParticipantPath), Is.True);
        Assert.That(File.Exists(GuestRoomScaleApplierPath), Is.True);
        Assert.That(File.Exists(GuestRoomStageScaleUtilityPath), Is.True);
        Assert.That(File.Exists(GuestPoseScaleOverrideStorePath), Is.True);
        Assert.That(File.Exists(GuestScaleAuditPath), Is.True);
    }

    private static ScaleTestScene CreateScaleTestScene(string roomId, float roomMultiplier)
    {
        GameObject butler = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
        PointClickPlayerMovement movement = butler.GetComponent<PointClickPlayerMovement>();
        ConfigureButlerRoom(movement, roomId);

        GameObject calibrationObject = new GameObject("GuestScaleCalibration");
        GuestRoomScaleCalibration calibration = calibrationObject.AddComponent<GuestRoomScaleCalibration>();
        calibration.SetButlerScaleSource(movement);
        calibration.InitializeMissingRoomsFromButler(movement);
        calibration.SetRoomMultiplier(roomId, roomMultiplier);

        GameObject applierObject = new GameObject("GuestScaleApplier");
        GuestRoomScaleApplier applier = applierObject.AddComponent<GuestRoomScaleApplier>();
        applier.SetCalibration(calibration);

        return new ScaleTestScene(butler, calibrationObject, applierObject, calibration, applier);
    }

    private static void ConfigureButlerRoom(PointClickPlayerMovement movement, string roomId)
    {
        movement.CaptureCurrentTransformAsButlerCalibrationBaseScale();
        movement.SetButlerFrontFinalLocalScaleForRoom(roomId, -100f, 2f, false);
        movement.SetButlerBackFinalLocalScaleForRoom(roomId, 100f, 1f, false);
    }

    private static RoomPerspectiveProfile CreateProfile(float nearScale, float farScale, string roomId)
    {
        RoomPerspectiveProfile profile = ScriptableObject.CreateInstance<RoomPerspectiveProfile>();
        profile.Configure(
            roomId,
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

    private static void DestroyScaleTestScene(ScaleTestScene scene)
    {
        UnityEngine.Object.DestroyImmediate(scene.Butler);
        UnityEngine.Object.DestroyImmediate(scene.CalibrationObject);
        UnityEngine.Object.DestroyImmediate(scene.ApplierObject);
    }

    private readonly struct ScaleTestScene
    {
        public ScaleTestScene(
            GameObject butler,
            GameObject calibrationObject,
            GameObject applierObject,
            GuestRoomScaleCalibration calibration,
            GuestRoomScaleApplier applier)
        {
            Butler = butler;
            CalibrationObject = calibrationObject;
            ApplierObject = applierObject;
            Calibration = calibration;
            Applier = applier;
        }

        public GameObject Butler { get; }
        public GameObject CalibrationObject { get; }
        public GameObject ApplierObject { get; }
        public GuestRoomScaleCalibration Calibration { get; }
        public GuestRoomScaleApplier Applier { get; }
    }
}
