using System;
using System.IO;
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
            Assert.That(scale, Is.EqualTo(0.75f * 1.25f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(calibrationObject);
            UnityEngine.Object.DestroyImmediate(butler);
        }
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

            Assert.That(guest.transform.localScale.y, Is.EqualTo(0.75f * 1.25f).Within(0.0001f));
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
        GameObject walkerObject = new GameObject("Walker_GEH_GreenGentleman", typeof(RectTransform));
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

            Assert.That(targetGraphic.rectTransform.localScale.y, Is.EqualTo(0.75f * 1.1f).Within(0.0001f));
            Assert.That(walkerObject.transform.localScale.y, Is.EqualTo(4f).Within(0.0001f));
        }
        finally
        {
            DestroyScaleTestScene(scene);
            UnityEngine.Object.DestroyImmediate(walkerObject);
        }
    }

    [Test]
    public void GuestRoomScaleApplierUsesProjectedVisualRootForFloorCharacters()
    {
        ScaleTestScene scene = CreateScaleTestScene("Grand Entrance Hall", 0.9f);
        RoomPerspectiveProfile profile = CreateProfile(1f, 1f, "Grand Entrance Hall");
        RoomProjectedEntity entity = CreateProjectedEntity("ProjectedGuest", profile, Vector2.zero);
        Transform visualRoot = entity.VisualRoot;

        try
        {
            GuestScaleParticipant participant = entity.gameObject.AddComponent<GuestScaleParticipant>();
            participant.SetRoomIdOverride("Grand Entrance Hall");
            participant.CaptureBaseScale(true);

            scene.Applier.RefreshAllNow();

            Assert.That(visualRoot.localScale.y, Is.EqualTo(0.75f * 0.9f).Within(0.0001f));
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

            Assert.That(guest.transform.localScale.y, Is.EqualTo(0.75f * 1.4f).Within(0.0001f));
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

            Assert.That(guest.transform.localScale.y, Is.EqualTo(1.5f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
            DestroyScaleTestScene(scene);
        }
    }

    [Test]
    public void SeatedGuestsUseSeatedPoseRatio()
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

            Assert.That(guest.transform.localScale.y, Is.EqualTo(0.75f * 0.68f).Within(0.0001f));

            participant.SetSeatedRatioOverride(0.9f);
            scene.Applier.RefreshAllNow();
            Assert.That(guest.transform.localScale.y, Is.EqualTo(0.75f * 0.8f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guest);
            DestroyScaleTestScene(scene);
        }
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
