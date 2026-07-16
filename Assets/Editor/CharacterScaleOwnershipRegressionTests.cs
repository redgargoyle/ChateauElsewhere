using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class CharacterScaleOwnershipRegressionTests
{
    private const string SnapshotPath = "docs/migrations/character-scale/legacy-character-scale-snapshot.json";
    private const string GameplayPath = "Assets/Scenes/Gameplay.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string DrawingRoomProfilePath = "Assets/ScriptableObjects/Rooms/DrawingRoomPerspectiveProfile.asset";
    private const string DiningRoomProfilePath = "Assets/ScriptableObjects/Rooms/DiningRoomPerspectiveProfile.asset";
    private const string Chapter1ArrivalControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs";
    private const string Chapter2GuestSearchControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestSearchController.cs";
    private const string PanicControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestPanicController.cs";
    private const string SpeakingCharacterIndicatorPath = "Assets/Scripts/UI/SpeakingCharacterIndicator.cs";
    private const string LayoutCaptureWindowPath = "Assets/Editor/PlayModeLayoutCaptureWindow.cs";
    private const string ActorRoomStatePath = "Assets/Scripts/Story/ActorRoomState.cs";
    private const string RoomPerspectiveProfilePath = "Assets/Scripts/Characters/RoomPerspectiveProfile.cs";
    private const string RoomProjectedEntityPath = "Assets/Scripts/Characters/RoomProjectedEntity.cs";
    private const string RoomPersonWalkerPath = "Assets/Scripts/Characters/RoomPersonWalker2D.cs";
    private const string PointClickPlayerMovementPath = "Assets/Scripts/PointClickPlayerMovement.cs";
    private const string CharacterVisualProfileGuid = "9d7c5206bdd145f4bdd4426f7ccc37bd";
    private const string RoomProjectedEntityEditorGuid = "9ce1fe34319045699aa184a301f7f45f";
    private const string RoomProjectedEntityGuid = "361e3658088b41ab98d330ae6457640b";
    private const string PlayerPrefabGuid = "3c2a23f8d68b2d05cace0338fba9a1d1";
    private static readonly string[] LegacyGuestScaleGuids =
    {
        "31d79ef7452a4c5288644569bd958a60",
        "2d396ad445bc46b9a6acb3ac62291ef0",
        "c209e3f5ef8c464db5163927439bd6a4",
        "b099f2b1c3494d8fa900d71915c16f31"
    };
    private static readonly string[] LegacyGuestScaleRuntimePaths =
    {
        "Assets/Scripts/Characters/GuestRoomScaleCalibration.cs",
        "Assets/Scripts/Characters/GuestRoomScaleApplier.cs",
        "Assets/Scripts/Characters/GuestRoomStageScaleUtility.cs",
        "Assets/Scripts/Characters/GuestScaleParticipant.cs"
    };

    private static readonly object[] GuestSittingRoster =
    {
        new object[] { "Guest 1", "Assets/Animation/Lady/Lady.overrideController", "Assets/Animation/Lady/Lady_Sitting.anim" },
        new object[] { "Guest 2", "Assets/Animation/ButlerGuest/ButlerGuest.overrideController", "Assets/Animation/ButlerGuest/ButlerGuest_Sitting.anim" },
        new object[] { "Guest 3", "Assets/Animation/MisterFlorianKnell/MisterFlorianKnell.overrideController", "Assets/Animation/MisterFlorianKnell/MisterFlorianKnell_Sitting.anim" },
        new object[] { "Guest 4", "Assets/Animation/CountessElowenDusk/CountessElowenDusk.overrideController", "Assets/Animation/CountessElowenDusk/CountessElowenDusk_Sitting.anim" },
        new object[] { "Guest 5", "Assets/Animation/BaronHectorGlass/BaronHectorGlass.overrideController", "Assets/Animation/BaronHectorGlass/BaronHectorGlass_Sitting.anim" },
        new object[] { "Guest 6", "Assets/Animation/LadySabineMarrow/LadySabineMarrow.overrideController", "Assets/Animation/LadySabineMarrow/LadySabineMarrow_Sitting.anim" },
        new object[] { "Guest 7", "Assets/Animation/LordAmbroseVeil/LordAmbroseVeil.overrideController", "Assets/Animation/LordAmbroseVeil/LordAmbroseVeil_Sitting.anim" },
        new object[] { "Guest 8", "Assets/Animation/MadameCoralieThread/MadameCoralieThread.overrideController", "Assets/Animation/MadameCoralieThread/MadameCoralieThread_Sitting.anim" }
    };

    [Test]
    public void LegacySnapshotPreservesCompletePhaseOneMigrationEvidence()
    {
        Assert.That(File.Exists(SnapshotPath), Is.True, "Phase 1 must freeze legacy values before deleting their owners.");
        LegacySnapshot snapshot = JsonUtility.FromJson<LegacySnapshot>(File.ReadAllText(SnapshotPath));

        Assert.That(snapshot.schemaVersion, Is.EqualTo(1));
        Assert.That(snapshot.source.gitCommit, Is.EqualTo("2a92396176c2baa6310e42f9ee906ee846d94e03"));
        Assert.That(snapshot.source.unityVersion, Is.EqualTo("6000.4.10f1"));
        Assert.That(snapshot.source.files, Has.Length.EqualTo(4));
        Assert.That(
            snapshot.source.files.Select(file => file.sha256),
            Is.EqualTo(new[]
            {
                "1099b1469437d46f5c45b7b8041e50977817112a7ec65027d10899222d2bd17d",
                "fcc64c863c1101340cf4cb96d91389af679e7a7fea8f6bdcb2d1c0e6101b3f71",
                "96a746b728e0048deec1f4df782ca3e79a67ab11137a887130d91f9fe53c2032",
                "aca70313aa7fc8a5568a54e9c0955517cfc84b477d00b765e2c48b148804db7a"
            }));
        Assert.That(snapshot.butler.roomOverrides, Has.Length.EqualTo(19));
        Assert.That(snapshot.guestRoomCalibration.rooms, Has.Length.EqualTo(19));
        Assert.That(snapshot.guests, Has.Length.EqualTo(8));
        Assert.That(snapshot.posePlacement.drawingRoom.assignments, Has.Length.EqualTo(8));
        Assert.That(
            snapshot.posePlacement.drawingRoom.standingCharacterIds,
            Is.EqualTo(new[] { "Guest 3", "Guest 5", "Guest 7" }));
        Assert.That(snapshot.posePlacement.diningRoom.assignments, Has.Length.EqualTo(8));
        Assert.That(snapshot.posePlacement.diningRoom.occlusionBindings, Has.Length.EqualTo(8));
        Assert.That(snapshot.guests.All(guest => !string.IsNullOrWhiteSpace(guest.sittingMapping.replacementClipGuid)), Is.True);
        Assert.That(snapshot.roomPerspectiveProfiles, Has.Length.EqualTo(2));
        Assert.That(snapshot.integrity.expectedCounts.sourceFiles, Is.EqualTo(4));
        Assert.That(snapshot.integrity.expectedCounts.butlerRoomOverrides, Is.EqualTo(19));
        Assert.That(snapshot.integrity.expectedCounts.guestRoomCalibrationRows, Is.EqualTo(19));
        Assert.That(snapshot.integrity.expectedCounts.participantRecords, Is.EqualTo(8));
        Assert.That(snapshot.integrity.expectedCounts.sittingMappings, Is.EqualTo(8));
        Assert.That(snapshot.integrity.expectedCounts.drawingRoomAssignments, Is.EqualTo(8));
        Assert.That(snapshot.integrity.expectedCounts.diningRoomAssignments, Is.EqualTo(8));
        Assert.That(snapshot.integrity.expectedCounts.diningRoomOcclusionBindings, Is.EqualTo(8));
        Assert.That(snapshot.integrity.expectedCounts.roomPerspectiveProfiles, Is.EqualTo(2));
    }

    [TestCaseSource(nameof(GuestSittingRoster))]
    public void GuestOverrideControllerPreservesSittingMapping(
        string characterId,
        string controllerPath,
        string expectedSittingClipPath)
    {
        AnimatorOverrideController controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(controllerPath);
        AnimationClip expectedSittingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(expectedSittingClipPath);

        Assert.That(controller, Is.Not.Null, $"{characterId} override controller must remain at {controllerPath}.");
        Assert.That(expectedSittingClip, Is.Not.Null, $"{characterId} sitting clip must remain at {expectedSittingClipPath}.");

        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(controller.overridesCount);
        controller.GetOverrides(overrides);
        KeyValuePair<AnimationClip, AnimationClip> sittingMapping = overrides.SingleOrDefault(
            mapping => mapping.Key != null && string.Equals(mapping.Key.name, "Player_Croutch", StringComparison.Ordinal));

        Assert.That(sittingMapping.Key, Is.Not.Null, $"{characterId} must retain the Player_Croutch override slot.");
        Assert.That(sittingMapping.Value, Is.SameAs(expectedSittingClip), $"{characterId} must retain its authored sitting clip.");
    }

    [Test]
    public void ManagedCharacterAnimationClipsDoNotWriteTransformScale()
    {
        string[] managedCharacterAnimationRoots =
        {
            "Assets/Animation/Player",
            "Assets/Animation/ButlerClassic",
            "Assets/Animation/Lady",
            "Assets/Animation/ButlerGuest",
            "Assets/Animation/MisterFlorianKnell",
            "Assets/Animation/CountessElowenDusk",
            "Assets/Animation/BaronHectorGlass",
            "Assets/Animation/LadySabineMarrow",
            "Assets/Animation/LordAmbroseVeil",
            "Assets/Animation/MadameCoralieThread",
            "Assets/Animation/Chapter2Panic"
        };
        string[] clipPaths = AssetDatabase.FindAssets("t:AnimationClip", managedCharacterAnimationRoots)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.That(clipPaths, Is.Not.Empty);

        foreach (string clipPath in clipPaths)
        {
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(clipPath)
                .OfType<AnimationClip>()
                .ToArray();
            Assert.That(clips, Is.Not.Empty, clipPath);

            foreach (AnimationClip clip in clips)
            {
                Assert.That(
                    AnimationUtility.GetCurveBindings(clip).Any(
                        binding => (binding.propertyName ?? string.Empty)
                            .IndexOf("localScale", StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.False,
                    $"{clipPath}::{clip.name}");
            }
        }
    }

    [Test]
    public void CharacterControllerFacingFlipsRenderersWithoutChangingRootScale()
    {
        GameObject actor = new GameObject("FacingActor");
        GameObject visual = new GameObject("Visual");
        GameObject authoredOppositeVisual = new GameObject("AuthoredOppositeVisual");

        try
        {
            visual.transform.SetParent(actor.transform, false);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.flipX = true;
            authoredOppositeVisual.transform.SetParent(actor.transform, false);
            SpriteRenderer authoredOppositeRenderer = authoredOppositeVisual.AddComponent<SpriteRenderer>();
            authoredOppositeRenderer.flipX = false;
            CharacterController2D controller = actor.AddComponent<CharacterController2D>();
            typeof(CharacterController2D).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(controller, null);
            Vector3 before = new Vector3(1.4f, 2.1f, 1f);
            actor.transform.localScale = before;

            MethodInfo flip = typeof(CharacterController2D).GetMethod(
                "Flip",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo refreshFacingPresentation = typeof(CharacterController2D).GetMethod(
                "RefreshFacingPresentationNow",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(flip, Is.Not.Null);
            Assert.That(refreshFacingPresentation, Is.Not.Null,
                "Late-added held-item and coat renderers need an explicit presentation refresh without touching root scale.");
            FieldInfo facingRight = typeof(CharacterController2D).GetField(
                "m_FacingRight",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(facingRight, Is.Not.Null);
            Assert.That(facingRight.GetValue(controller), Is.False, "An authored flipX actor starts facing left.");
            Assert.That(authoredOppositeRenderer.flipX, Is.False,
                "Initialization must preserve each renderer's authored relative orientation.");

            flip.Invoke(controller, null);

            Assert.That(actor.transform.localScale, Is.EqualTo(before));
            Assert.That(renderer.flipX, Is.False);
            Assert.That(authoredOppositeRenderer.flipX, Is.True);
            Assert.That(facingRight.GetValue(controller), Is.True);

            flip.Invoke(controller, null);

            Assert.That(actor.transform.localScale, Is.EqualTo(before));
            Assert.That(renderer.flipX, Is.True);
            Assert.That(authoredOppositeRenderer.flipX, Is.False);
            Assert.That(facingRight.GetValue(controller), Is.False);

            GameObject heldCoat = new GameObject("LateAddedHeldCoat");
            SpriteRenderer heldCoatRenderer = heldCoat.AddComponent<SpriteRenderer>();
            heldCoat.transform.SetParent(actor.transform, false);
            refreshFacingPresentation.Invoke(controller, null);

            Assert.That(actor.transform.localScale, Is.EqualTo(before));
            Assert.That(heldCoatRenderer.flipX, Is.True,
                "A renderer added while facing left must immediately match the presentation orientation.");

            flip.Invoke(controller, null);

            Assert.That(actor.transform.localScale, Is.EqualTo(before));
            Assert.That(renderer.flipX, Is.False);
            Assert.That(heldCoatRenderer.flipX, Is.False);
            Assert.That(facingRight.GetValue(controller), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void CarriedCoatAttachmentTracksButlerFacingWithoutChangingActorScale()
    {
        GameObject actor = new GameObject("CoatFacingActor");
        GameObject coat = new GameObject("CarriedCoat");
        GameObject chapterObject = new GameObject("ChapterOneCoatFacingFixture");

        try
        {
            CharacterController2D controller = actor.AddComponent<CharacterController2D>();
            typeof(CharacterController2D).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(controller, null);
            Vector3 authoredScale = new Vector3(1.42f, 1.42f, 1.3f);
            actor.transform.localScale = authoredScale;
            coat.transform.SetParent(actor.transform, false);

            Chapter1ArrivalController chapter = chapterObject.AddComponent<Chapter1ArrivalController>();
            typeof(Chapter1ArrivalController).GetField(
                "carriedCoatVisual",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(chapter, coat);
            MethodInfo bindFacing = typeof(Chapter1ArrivalController).GetMethod(
                "BindCarriedCoatFacing",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo unbindFacing = typeof(Chapter1ArrivalController).GetMethod(
                "UnbindCarriedCoatFacing",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo flip = typeof(CharacterController2D).GetMethod(
                "Flip",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(bindFacing, Is.Not.Null);
            Assert.That(unbindFacing, Is.Not.Null);
            Assert.That(flip, Is.Not.Null);
            Assert.That(controller.IsFacingRight, Is.True);

            bindFacing.Invoke(chapter, new object[] { controller });
            Assert.That(coat.transform.localPosition.x, Is.GreaterThan(0f));

            flip.Invoke(controller, null);
            Assert.That(controller.IsFacingRight, Is.False);
            Assert.That(coat.transform.localPosition.x, Is.LessThan(0f));
            Assert.That(actor.transform.localScale, Is.EqualTo(authoredScale));

            flip.Invoke(controller, null);
            Assert.That(controller.IsFacingRight, Is.True);
            Assert.That(coat.transform.localPosition.x, Is.GreaterThan(0f));
            Assert.That(actor.transform.localScale, Is.EqualTo(authoredScale));

            unbindFacing.Invoke(chapter, null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(chapterObject);
            UnityEngine.Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void ChapterOneOwnsPlacementAndPoseButNoCharacterScaleOrRuntimeGuestSynthesis()
    {
        string source = File.ReadAllText(Chapter1ArrivalControllerPath);
        string[] prohibitedSymbols =
        {
            "EnsureGuestScale",
            "GuestRoomScale",
            "GuestScaleParticipant",
            "PreserveGuestAuthoredScale",
            "SetPerspectiveScaleEnabled",
            "SyncGuestScaleParticipant",
            "ResolveGuestScaleRoomId",
            "ResolveGuestScalePose",
            "runtimeGeneratedGuestObjects",
            "runtimeGuestSprite",
            "FindRuntimeGuestTemplate",
            "CreateRuntimeGuestObject",
            "CreateRuntimeVisual"
        };

        for (int i = 0; i < prohibitedSymbols.Length; i++)
        {
            Assert.That(
                source,
                Does.Not.Contain(prohibitedSymbols[i]),
                $"Chapter 1 must not retain the legacy scale/fallback symbol '{prohibitedSymbols[i]}'.");
        }

        Assert.That(source, Does.Contain("Missing authored guest reference"));
        Assert.That(source, Does.Not.Contain("Runtime placeholder guests will be created"));
        Assert.That(source, Does.Not.Contain("Missing guests will be created at runtime"));
        Assert.That(source, Does.Not.Contain("fallback guest creation"));
        Assert.That(
            ExtractMethodBody(source, "EnsureGuestConfigs"),
            Does.Contain("throw new InvalidOperationException"),
            "Chapter 1 must stop immediately when a required authored actor is missing.");
        Assert.That(source, Does.Contain("ApplyDrawingRoomWaitingPose"));
        Assert.That(source, Does.Contain("ApplyDrawingRoomSeatedOcclusion"));
        Assert.That(source, Does.Contain("CreateCoatPickup"));
    }

    [Test]
    public void SeatedAndChapterOneCoatTransitionsDoNotResizeActorRoots()
    {
        GameObject actor = new GameObject("SeatedScaleInvariantActor");

        try
        {
            Vector3 authoredScale = new Vector3(1.25f, 1.6f, 1f);
            actor.transform.localScale = authoredScale;
            ActorRoomState actorState = actor.AddComponent<ActorRoomState>();

            for (int cycle = 0; cycle < 8; cycle++)
            {
                actorState.SetCurrentRoom(cycle % 2 == 0 ? "Drawing Room" : "Dining Room");
                actorState.SetSeated(true);
                Assert.That(actor.transform.localScale, Is.EqualTo(authoredScale),
                    $"Room/seat cycle {cycle} must not drift the authored scale while seated.");
                actorState.SetSeated(false);
                Assert.That(actor.transform.localScale, Is.EqualTo(authoredScale),
                    $"Room/seat cycle {cycle} must not drift the authored scale after standing.");
            }

            string chapterOneSource = File.ReadAllText(Chapter1ArrivalControllerPath);
            string takeCoatBody = ExtractMethodBody(chapterOneSource, "TakeGuestCoat");
            string storeCoatBody = ExtractMethodBody(chapterOneSource, "StoreCarriedCoatInCloset");

            Assert.That(takeCoatBody, Does.Not.Contain("localScale"));
            Assert.That(storeCoatBody, Does.Not.Contain("localScale"));
            Assert.That(takeCoatBody, Does.Not.Contain("RefreshGuestScaling"));
            Assert.That(storeCoatBody, Does.Not.Contain("RefreshGuestScaling"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void CharacterAdjacentScaleWritesStayOnDedicatedPresentationChildren()
    {
        string chapterOneSource = File.ReadAllText(Chapter1ArrivalControllerPath);
        string coatBody = ExtractMethodBody(chapterOneSource, "ApplyAssignedCoatSprite");
        string transferCoatBody = ExtractMethodBody(chapterOneSource, "TransferCoatVisualToButler");
        string bindCoatFacingBody = ExtractMethodBody(chapterOneSource, "BindCarriedCoatFacing");
        Assert.That(coatBody, Does.Contain("spriteRenderer.transform.localScale"));
        Assert.That(coatBody, Does.Not.Contain("guest.GuestObject.transform.localScale"));
        Assert.That(transferCoatBody, Does.Contain("BindCarriedCoatFacing"));
        Assert.That(bindCoatFacingBody, Does.Contain("RefreshFacingPresentationNow"));

        string guestSearchSource = File.ReadAllText(Chapter2GuestSearchControllerPath);
        string clickTargetBody = ExtractMethodBody(guestSearchSource, "EnsureRuntimeClickTarget");
        Assert.That(clickTargetBody, Does.Contain("new GameObject(ClickTargetName)"));
        Assert.That(clickTargetBody, Does.Contain("targetTransform.SetParent(actorObject.transform, false)"));
        Assert.That(clickTargetBody, Does.Contain("targetTransform.localScale = Vector3.one"));
        Assert.That(clickTargetBody, Does.Not.Contain("actorObject.transform.localScale"));

        string speakingIndicatorSource = File.ReadAllText(SpeakingCharacterIndicatorPath);
        string bubbleBody = ExtractMethodBody(speakingIndicatorSource, "UpdateBubbleTransform");
        Assert.That(bubbleBody, Does.Contain("bubbleRenderer.transform.localScale"));
        Assert.That(bubbleBody, Does.Not.Contain("currentTarget.localScale"));
        Assert.That(bubbleBody, Does.Not.Contain("currentActor.transform.localScale"));
    }

    [Test]
    public void PanicPresentationContainsNoActorRootScaleOwnership()
    {
        string source = File.ReadAllText(PanicControllerPath);

        Assert.That(source, Does.Not.Contain("originalLocalScale"));
        Assert.That(source, Does.Not.Contain("originalSpriteLocalSize"));
        Assert.That(source, Does.Not.Contain("CaptureOriginalSpriteLocalSize"));
        Assert.That(source, Does.Not.Contain("GetSpriteScaleMultiplier"));
        Assert.That(source, Does.Not.Contain("ApplySpriteScale"));
        Assert.That(source, Does.Not.Contain("GuestRoomScaleApplier"));
        Assert.That(source, Does.Not.Contain("GuestScaleParticipant"));
        Assert.That(source, Does.Not.Contain("targetTransform.localScale"));
        Assert.That(source, Does.Contain("spriteRenderer.sprite = sprite"));
        Assert.That(source, Does.Contain("image.sprite = sprite"));
    }

    [Test]
    public void LayoutCaptureRejectsManagedActorRootsAndDescendantsAtEveryWriteBoundary()
    {
        const string temporarySceneTemplatePath = "Assets/Settings/Scenes/URP2DSceneTemplate.unity";
        Scene previousActiveScene = SceneManager.GetActiveScene();
        bool hadLoadedActiveScene = previousActiveScene.IsValid() && previousActiveScene.isLoaded;
        int initialSceneCount = SceneManager.sceneCount;
        string temporarySceneName = $"__CharacterScaleLayoutCaptureRegression_{Guid.NewGuid():N}";
        string temporaryScenePath = AssetDatabase.GenerateUniqueAssetPath(
            $"Assets/{temporarySceneName}.unity");
        Scene scene = default;

        try
        {
            Assert.That(AssetDatabase.CopyAsset(temporarySceneTemplatePath, temporaryScenePath), Is.True);
            scene = EditorSceneManager.OpenScene(temporaryScenePath, OpenSceneMode.Additive);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
            Assert.That(SceneManager.sceneCount, Is.GreaterThanOrEqualTo(initialSceneCount + 1));

            if (hadLoadedActiveScene)
            {
                Assert.That(
                    previousActiveScene.IsValid() && previousActiveScene.isLoaded,
                    Is.True,
                    "Opening the additive layout-capture fixture must preserve the editor's active scene.");
            }

            EditorSceneManager.SetActiveScene(scene);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            GameObject butler = new GameObject("ButlerActor");
            butler.AddComponent<PointClickPlayerMovement>();
            GameObject butlerVisual = new GameObject("ButlerVisual");
            butlerVisual.transform.SetParent(butler.transform, false);

            GameObject guest = new GameObject("GuestActor");
            guest.AddComponent<ActorRoomState>();
            GameObject guestVisual = new GameObject("GuestVisual");
            guestVisual.transform.SetParent(guest.transform, false);
            guestVisual.transform.localScale = new Vector3(1.3f, 1.4f, 1f);

            GameObject anchorObject = new GameObject("OrdinaryRoomAnchor");
            anchorObject.AddComponent<RoomAnchor>();

            Assert.That(EditorSceneManager.SaveScene(scene, temporaryScenePath), Is.True);

            MethodInfo tryCreate = typeof(PlayModeLayoutCaptureWindow).GetMethod(
                "TryCreateCaptureItem",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo applyItem = typeof(PlayModeLayoutCaptureWindow).GetMethod(
                "ApplyCaptureItem",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(tryCreate, Is.Not.Null);
            Assert.That(applyItem, Is.Not.Null);

            bool butlerRootAccepted = InvokeTryCreateCaptureItem(tryCreate, butler.transform, out _);
            bool butlerChildAccepted = InvokeTryCreateCaptureItem(tryCreate, butlerVisual.transform, out _);
            bool guestRootAccepted = InvokeTryCreateCaptureItem(tryCreate, guest.transform, out _);
            bool guestChildAccepted = InvokeTryCreateCaptureItem(tryCreate, guestVisual.transform, out _);
            bool anchorAccepted = InvokeTryCreateCaptureItem(tryCreate, anchorObject.transform, out object anchorItem);

            Assert.That(anchorItem, Is.Not.Null);
            FieldInfo localScaleField = anchorItem.GetType().GetField(
                "LocalScale",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(localScaleField, Is.Not.Null);
            localScaleField.SetValue(anchorItem, new Vector3(9f, 8f, 7f));

            Vector3 guestVisualScaleBeforeApply = guestVisual.transform.localScale;
            applyItem.Invoke(null, new[] { guestVisual.transform, anchorItem });

            string source = File.ReadAllText(LayoutCaptureWindowPath);
            string applyPendingBody = ExtractMethodBody(source, "ApplyPendingCapture");

            bool allBoundariesProtected =
                !butlerRootAccepted &&
                !butlerChildAccepted &&
                !guestRootAccepted &&
                !guestChildAccepted &&
                anchorAccepted &&
                guestVisual.transform.localScale == guestVisualScaleBeforeApply &&
                applyPendingBody.Contains("IsManagedCharacterTransform(target)");

            Assert.That(
                allBoundariesProtected,
                Is.True,
                $"capture results were Butler root/child={butlerRootAccepted}/{butlerChildAccepted}, " +
                $"Guest root/child={guestRootAccepted}/{guestChildAccepted}, anchor={anchorAccepted}, " +
                $"direct apply scale={guestVisual.transform.localScale}.");
        }
        finally
        {
            if (hadLoadedActiveScene && previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                EditorSceneManager.SetActiveScene(previousActiveScene);
            }

            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            AssetDatabase.DeleteAsset(temporaryScenePath);
        }
    }

    [TestCase("Assets/Editor/CharacterAnimationAssetBuilder.cs", "RebuildAllCharacterAnimationAssets", "BuildCharacter(characterFolder")]
    [TestCase("Assets/Editor/Guest2ButlerAnimationAssetBuilder.cs", "RebuildGuest2ButlerAnimation", "EnsureFolder(OutputFolder)")]
    [TestCase("Assets/Editor/AnimationLibraryClipBuilder.cs", "RebuildApprovedFullBodyClips", "CreateSpriteClip(")]
    [TestCase("Assets/Editor/Chapter2PanicAnimationLibraryBuilder.cs", "RebuildPanicAnimationLibrary", "EnsureFolder(OutputFolder)")]
    public void DestructiveAnimationBuildersRequireConfirmationBeforeFirstWrite(
        string sourcePath,
        string methodName,
        string firstWriteMarker)
    {
        string methodBody = ExtractMethodBody(File.ReadAllText(sourcePath), methodName);
        int confirmationIndex = methodBody.IndexOf("EditorUtility.DisplayDialog", StringComparison.Ordinal);
        int firstWriteIndex = methodBody.IndexOf(firstWriteMarker, StringComparison.Ordinal);

        Assert.That(confirmationIndex, Is.GreaterThanOrEqualTo(0), sourcePath);
        Assert.That(firstWriteIndex, Is.GreaterThan(confirmationIndex), sourcePath);
    }

    [Test]
    public void PanicBuilderValidatesEveryInputBeforeCreatingOrRewritingAssets()
    {
        string source = File.ReadAllText("Assets/Editor/Chapter2PanicAnimationLibraryBuilder.cs");
        string rebuildBody = ExtractMethodBody(source, "RebuildPanicAnimationLibrary");
        int validationIndex = rebuildBody.IndexOf("CollectValidatedBuildInputs", StringComparison.Ordinal);
        int errorExitIndex = rebuildBody.IndexOf("errors.Count", StringComparison.Ordinal);
        int firstWriteIndex = rebuildBody.IndexOf("EnsureFolder(OutputFolder)", StringComparison.Ordinal);

        Assert.That(validationIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(errorExitIndex, Is.GreaterThan(validationIndex));
        Assert.That(firstWriteIndex, Is.GreaterThan(errorExitIndex));
    }

    [Test]
    public void GuestTwoBuilderDoesNotOpenOrSaveGameplayOrMutateSceneComponents()
    {
        string source = File.ReadAllText("Assets/Editor/Guest2ButlerAnimationAssetBuilder.cs");

        Assert.That(source, Does.Not.Contain("EditorSceneManager"));
        Assert.That(source, Does.Not.Contain("GameplayScenePath"));
        Assert.That(source, Does.Not.Contain("ApplyToGuest2"));
        Assert.That(source, Does.Not.Contain("AddComponent<Animator>"));
        Assert.That(source, Does.Contain("\"Player_Croutch\" => sittingClip"));
    }

    [Test]
    public void GlobalCharacterBuilderPreservesAuthoredSittingOverrideWithoutFallbackSubstitution()
    {
        string source = File.ReadAllText("Assets/Editor/CharacterAnimationAssetBuilder.cs");
        string buildBody = ExtractMethodBody(source, "BuildCharacter");
        string methodBody = ExtractMethodBody(source, "CreateOverrideController");

        Assert.That(buildBody, Does.Contain("{characterName}_Sitting.anim"));
        Assert.That(buildBody, Does.Contain("LoadAssetAtPath<AnimationClip>"));
        Assert.That(methodBody, Does.Contain("GetOverrides"));
        Assert.That(methodBody, Does.Contain("existingCrouchClip"));
        Assert.That(methodBody, Does.Contain("sittingClip"));
        Assert.That(methodBody, Does.Contain("Debug.LogWarning"));
        Assert.That(methodBody, Does.Not.Contain("existingCrouchClip : baseClip"));
    }

    [Test]
    public void StageProjectionAndWalkerSourcesContainNoManagedCharacterScaleOwnership()
    {
        var prohibitedByPath = new Dictionary<string, string[]>
        {
            [ActorRoomStatePath] = new[]
            {
                "ApplyButlerCharacterScaleNow",
                "BuildButlerActorScale",
                "ScaleXY",
                "scaleWithRoomStageMotion",
                "boundLocalScale",
                "authoredActorLocalScale",
                "GuestScaleParticipant"
            },
            [RoomProjectedEntityPath] = new[]
            {
                "ApplyButlerCharacterScaleNow",
                "ButlerCharacterScale",
                "CharacterVisualProfile",
                "roomVisualScaleOverrides",
                "CurrentVisualScaleRoomId",
                "CurrentScale =>",
                "GetProjectedScale",
                "GuestScaleParticipant"
            },
            [RoomPersonWalkerPath] = new[]
            {
                "ApplyButlerCharacterScaleNow",
                "ButlerCharacterScale",
                "nearScale",
                "farScale",
                "authoredWalkerLocalScale",
                "rectTransform.localScale",
                "GuestScaleParticipant"
            }
        };

        foreach (KeyValuePair<string, string[]> entry in prohibitedByPath)
        {
            string source = File.ReadAllText(entry.Key);

            foreach (string symbol in entry.Value)
            {
                Assert.That(
                    source,
                    Does.Not.Contain(symbol),
                    $"{entry.Key} must not retain managed-character size ownership through '{symbol}'.");
            }
        }
    }

    [Test]
    public void PointClickLifecyclePreservesAuthoredNonuniformRootScale()
    {
        GameObject actor = new GameObject("ScaleNeutralPointClickActor");
        Vector3 authoredScale = new Vector3(1.35f, 2.1f, 0.8f);
        actor.transform.localScale = authoredScale;

        try
        {
            PointClickPlayerMovement movement = actor.AddComponent<PointClickPlayerMovement>();
            MethodInfo initializeVisualState = typeof(PointClickPlayerMovement).GetMethod(
                "InitializeVisualStateFromTransform",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo lateUpdate = typeof(PointClickPlayerMovement).GetMethod(
                "LateUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo logicalPosition = typeof(PointClickPlayerMovement).GetField(
                "logicalPosition",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo isReady = typeof(PointClickPlayerMovement).GetField(
                "isReady",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(initializeVisualState, Is.Not.Null);
            Assert.That(lateUpdate, Is.Not.Null);
            Assert.That(logicalPosition, Is.Not.Null);
            Assert.That(isReady, Is.Not.Null);

            initializeVisualState.Invoke(movement, null);
            Assert.That(actor.transform.localScale, Is.EqualTo(authoredScale),
                "PointClick initialization must preserve the authored actor-root scale.");

            logicalPosition.SetValue(movement, new Vector2(0f, -4.25f));
            isReady.SetValue(movement, true);
            lateUpdate.Invoke(movement, null);

            Assert.That(actor.transform.localScale, Is.EqualTo(authoredScale),
                "PointClick updates may move and sort the actor, but must not resize its root.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void PointClickSourceOwnsPositionAndSortingButNeverCharacterScale()
    {
        string source = File.ReadAllText(PointClickPlayerMovementPath);
        PropertyInfo currentRoomId = typeof(PointClickPlayerMovement).GetProperty(
            "CurrentRoomId",
            BindingFlags.Instance | BindingFlags.Public);
        MethodInfo currentRoomFootPoint = typeof(PointClickPlayerMovement).GetMethod(
            "TryGetCurrentRoomLocalFootPoint",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.That(source, Does.Not.Contain("transform.localScale ="));
        Assert.That(source, Does.Not.Contain("ApplyPerspectiveScale"));
        Assert.That(source, Does.Not.Contain("CaptureAuthoredLocalScale"));
        Assert.That(source, Does.Not.Contain("RestoreAuthoredLocalScale"));
        Assert.That(source, Does.Not.Contain("authoredPerspectiveScaleReference"));
        Assert.That(source, Does.Not.Contain("HasActiveGuestScaleParticipant"));
        Assert.That(currentRoomId, Is.Not.Null);
        Assert.That(currentRoomId.CanWrite, Is.False);
        Assert.That(currentRoomFootPoint, Is.Not.Null,
            "Phase 2 needs one scale-neutral read-only room/local-foot input seam.");
        string currentRoomFootPointBody = ExtractMethodBody(source, "TryGetCurrentRoomLocalFootPoint");
        Assert.That(currentRoomFootPointBody, Does.Contain("GetCurrentVisibleMovementWorldPoint()"),
            "Phase 2 room-local depth must be sampled from the character's current visible foot point.");
        Assert.That(currentRoomFootPointBody, Does.Contain("TryGetActiveRoomStageLocalPoint"));
        Assert.That(currentRoomFootPointBody, Does.Not.Contain("logicalPosition"),
            "The read seam must not pair a new room id with cached logical/stage conversion state.");
        Assert.That(currentRoomFootPointBody, Does.Not.Contain("TryGetRoomStageLocalPoint("));

        string[] requiredPositionAndSortingSymbols =
        {
            "currentRoomStageScaleRatio",
            "ApplyVisualPosition",
            "UpdateVisualOffset",
            "ResetRoomStageVisualReference",
            "LogicalToWalkableWorldPoint",
            "WalkableWorldToLogicalPoint",
            "ApplyPlayerSorting",
            "TryGetCurrentRoomLocalFootPoint"
        };

        foreach (string symbol in requiredPositionAndSortingSymbols)
        {
            Assert.That(source, Does.Contain(symbol),
                $"PointClick must retain position/sorting responsibility through '{symbol}'.");
        }
    }

    [Test]
    public void PointClickContainsNoDormantButlerScaleBridgeAfterSerializedMigration()
    {
        string source = File.ReadAllText(PointClickPlayerMovementPath);
        string[] removedScaleSymbols =
        {
            "ButlerCharacterScaleSample",
            "ButlerScaleRevision",
            "GetButlerScaleOverrideRoomIds",
            "TryEvaluateButlerCharacterScale",
            "ButlerRoomScaleOverride",
            "butlerRoomScaleOverrides",
            "editorSelectedButlerScaleRoomId",
            "LegacyButlerRoomScaleEndpointEpsilon",
            "SanitizeButlerFinalLocalScaleY"
        };

        foreach (string symbol in removedScaleSymbols)
        {
            Assert.That(source, Does.Not.Contain(symbol),
                $"PointClick must not retain the dormant Butler scale bridge symbol '{symbol}'.");
        }

        string[] removedLegacySources =
        {
            "Assets/Editor/ButlerRoomScaleCalibrationWindow.cs",
            "Assets/Editor/PointClickPlayerMovementEditor.cs",
            "Assets/Editor/GuestRoomScaleMasterWindow.cs",
            "Assets/Editor/GuestScaleAudit.cs",
            "Assets/Editor/GuestButlerScaleRegressionTests.cs"
        };

        foreach (string path in removedLegacySources)
        {
            Assert.That(File.Exists(path), Is.False, $"Legacy sizing source must be removed: {path}");
            Assert.That(File.Exists(path + ".meta"), Is.False, $"Legacy sizing metadata must be removed: {path}.meta");
        }
    }

    [Test]
    public void LegacyGuestScaleSourcesGuidsAndSceneRecordsAreAbsent()
    {
        foreach (string path in LegacyGuestScaleRuntimePaths)
        {
            Assert.That(File.Exists(path), Is.False, $"Legacy runtime sizing source must be removed: {path}");
            Assert.That(File.Exists(path + ".meta"), Is.False, $"Legacy runtime sizing metadata must be removed: {path}.meta");
        }

        string[] liveAssetPaths = Directory.GetFiles("Assets", "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(
                path.Replace('\\', '/'),
                "Assets/Editor/CharacterScaleOwnershipRegressionTests.cs",
                StringComparison.OrdinalIgnoreCase))
            .Where(path =>
                path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (string path in liveAssetPaths)
        {
            string text = File.ReadAllText(path);
            foreach (string guid in LegacyGuestScaleGuids)
            {
                Assert.That(text, Does.Not.Contain(guid), $"Deleted legacy GUID {guid} remains in {path}.");
            }
        }

        string gameplay = File.ReadAllText(GameplayPath);
        string[] removedSceneObjectIds =
        {
            "86244176", "86244178", "86244179",
            "1844861546", "1844861547", "1844861549",
            "436671157", "13922402", "1485188971", "1206924327",
            "2142464084", "1356668536", "1439972665", "2100000121"
        };
        foreach (string fileId in removedSceneObjectIds)
        {
            Assert.That(gameplay, Does.Not.Match($@"(?m)^--- !u!\d+ &{fileId}(?: stripped)?$"),
                $"Deleted legacy scene object {fileId} remains serialized.");
        }

        string[] removedButlerPaths =
        {
            "editorSelectedButlerScaleRoomId",
            "hasButlerCalibrationBaseLocalScale",
            "butlerRoomScaleOverrides.Array.size",
            "butlerRoomScaleOverrides.Array.data[",
            "frontScale",
            "backScale"
        };
        foreach (string propertyPath in removedButlerPaths)
        {
            Assert.That(gameplay, Does.Not.Contain(propertyPath),
                $"Deleted Butler sizing property path '{propertyPath}' remains serialized.");
        }
    }

    [Test]
    public void GameplayPreservesCharacterRosterScalesAndAuthoredPoseInfrastructureAfterLegacyPurge()
    {
        string gameplay = File.ReadAllText(GameplayPath);
        Assert.That(
            Regex.Matches(gameplay, $@"m_SourcePrefab: \{{fileID: 100100000, guid: {PlayerPrefabGuid}, type: 3\}}").Count,
            Is.EqualTo(9),
            "Gameplay must retain Butler plus all eight guest Player prefab instances.");
        Assert.That(
            Regex.Matches(gameplay, @"propertyPath: walkableFloor\s+value:\s*\r?\n\s*objectReference: \{fileID: 551531667\}").Count,
            Is.EqualTo(9),
            "Every retained character must keep the Dining/room walkable-floor reference.");

        string[] instanceIds =
        {
            "81962841", "436671155", "13922399", "1485188968", "1206924324",
            "2100000100", "2100000105", "2100000110", "2100000115"
        };
        float[] expectedXy = { 2.150601f, 1.42f, 1.42f, 1.42f, 1.42f, 1.42f, 1.42f, 1.42f, 1.42f };
        float[] expectedZ = { 1f, 1f, 1f, 1.12f, 1.12f, 1.3f, 1.3f, 1.3f, 1.3f };
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Assert.That(playerPrefab, Is.Not.Null);
        for (int i = 0; i < instanceIds.Length; i++)
        {
            string block = ExtractYamlObjectBlock(gameplay, instanceIds[i]);
            AssertPrefabInstanceScale(block, "x", expectedXy[i], playerPrefab.transform.localScale.x);
            AssertPrefabInstanceScale(block, "y", expectedXy[i], playerPrefab.transform.localScale.y);
            AssertPrefabInstanceScale(block, "z", expectedZ[i], playerPrefab.transform.localScale.z);

            if (i > 0)
            {
                object[] rosterRow = (object[])GuestSittingRoster[i - 1];
                string expectedControllerGuid = AssetDatabase.AssetPathToGUID((string)rosterRow[1]);
                Assert.That(
                    block,
                    Does.Match($@"(?ms)propertyPath: m_Controller\s+value:\s*objectReference: \{{fileID: 22100000, guid: {expectedControllerGuid}, type: 2\}}"),
                    $"Guest {i} must retain its authored override controller assignment.");
            }
        }

        for (int i = 1; i <= 8; i++)
        {
            string suffix = i.ToString("00");
            Assert.That(gameplay, Does.Contain($"m_Name: DrawingRoomGuestPoint_{suffix}"));
            Assert.That(gameplay, Does.Contain($"anchorId: DrawingRoomGuestPoint_{suffix}"));
            Assert.That(gameplay, Does.Contain($"m_Name: Ch2_DiningSeat_{suffix}"));
            Assert.That(gameplay, Does.Contain($"anchorId: Ch2_DiningSeat_{suffix}"));
        }

        string diningOcclusionBlock = ExtractYamlObjectBlock(gameplay, "3920000002");
        Assert.That(Regex.Matches(diningOcclusionBlock, @"(?m)^  - seatAnchor: \{fileID: \d+\}$").Count, Is.EqualTo(8));
    }

    [Test]
    public void PlayerPrefabAndGameplayReloadWithoutMissingOrObsoleteScaleComponents()
    {
        string[] obsoleteComponentTypeNames =
        {
            "GuestRoomScaleApplier",
            "GuestRoomScaleCalibration",
            "GuestScaleParticipant"
        };
        GameObject playerPrefabRoot = null;
        Scene gameplayPreview = default;

        try
        {
            playerPrefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            Assert.That(playerPrefabRoot, Is.Not.Null);
            AssertHierarchyHasNoMissingOrObsoleteComponents(
                playerPrefabRoot,
                obsoleteComponentTypeNames,
                PlayerPrefabPath);

            gameplayPreview = EditorSceneManager.OpenPreviewScene(GameplayPath);
            Assert.That(gameplayPreview.IsValid() && gameplayPreview.isLoaded, Is.True);

            foreach (GameObject root in gameplayPreview.GetRootGameObjects())
            {
                AssertHierarchyHasNoMissingOrObsoleteComponents(
                    root,
                    obsoleteComponentTypeNames,
                    GameplayPath);
            }
        }
        finally
        {
            if (gameplayPreview.IsValid() && gameplayPreview.isLoaded)
            {
                EditorSceneManager.ClosePreviewScene(gameplayPreview);
            }

            if (playerPrefabRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(playerPrefabRoot);
            }
        }
    }

    [Test]
    public void FloorCharacterProjectionPreservesVisualScaleWhileKeepingPositionProjection()
    {
        RoomPerspectiveProfile profile = ScriptableObject.CreateInstance<RoomPerspectiveProfile>();
        GameObject actor = new GameObject("ScaleNeutralProjectedActor");

        try
        {
            Vector3 authoredScale = new Vector3(1.35f, 1.65f, 0.9f);
            actor.transform.localScale = authoredScale;
            RoomProjectedEntity projection = actor.AddComponent<RoomProjectedEntity>();
            profile.SetDepthYRange(-10f, 10f);

            projection.SetRoomLocalFootPoint(new Vector2(42f, -7f), false);
            projection.SetRoomProfile(profile);

            Assert.That(actor.transform.localScale, Is.EqualTo(authoredScale));
            Assert.That(actor.transform.localPosition.x, Is.EqualTo(42f).Within(0.001f));
            Assert.That(actor.transform.localPosition.y, Is.EqualTo(-7f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actor);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void RoomPersonWalkerVisualRefreshPreservesGraphicScale()
    {
        GameObject walkerObject = new GameObject(
            "ScaleNeutralWalker",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        try
        {
            RoomPersonWalker2D walker = walkerObject.AddComponent<RoomPersonWalker2D>();
            Vector3 authoredScale = new Vector3(1.25f, 1.4f, 0.8f);
            walkerObject.transform.localScale = authoredScale;

            walker.RefreshDepthVisualsNow();

            Assert.That(walkerObject.transform.localScale, Is.EqualTo(authoredScale));
            Assert.That(walker.TargetGraphic.raycastTarget, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(walkerObject);
        }
    }

    [Test]
    public void RoomPersonWalkerFacingUsesPresentationRotationWithoutChangingScale()
    {
        GameObject walkerObject = new GameObject(
            "ScaleNeutralFacingWalker",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        try
        {
            RoomPersonWalker2D walker = walkerObject.AddComponent<RoomPersonWalker2D>();
            Vector3 authoredScale = new Vector3(1.25f, 1.4f, 0.8f);
            walkerObject.transform.localScale = authoredScale;
            typeof(RoomPersonWalker2D).GetField(
                "pathPoints",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(
                walker,
                new[] { Vector2.zero, new Vector2(-100f, 0f) });
            typeof(RoomPersonWalker2D).GetField(
                "pixelsPerSecond",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(walker, 100f);

            MethodInfo tick = typeof(RoomPersonWalker2D).GetMethod(
                "Tick",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(tick, Is.Not.Null);
            tick.Invoke(walker, new object[] { 0.25f, true });

            Assert.That(walkerObject.transform.localScale, Is.EqualTo(authoredScale));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(walkerObject.transform.localEulerAngles.y, 180f)), Is.LessThan(0.01f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(walkerObject);
        }
    }

    [Test]
    public void ProjectionModeNumericValuesRemainSerializationStable()
    {
        Assert.That((int)RoomProjectedEntity.ProjectionMode.FloorCharacter, Is.EqualTo(0));
        Assert.That((int)RoomProjectedEntity.ProjectionMode.FloorProp, Is.EqualTo(1));
        Assert.That((int)RoomProjectedEntity.ProjectionMode.WallProp, Is.EqualTo(2));
        Assert.That((int)RoomProjectedEntity.ProjectionMode.FurnitureSurfaceProp, Is.EqualTo(3));
        Assert.That((int)RoomProjectedEntity.ProjectionMode.ForegroundOccluder, Is.EqualTo(4));

        string source = File.ReadAllText(RoomProjectedEntityPath);
        Assert.That(source, Does.Match(@"FloorCharacter\s*=\s*0"));
        Assert.That(source, Does.Match(@"FloorProp\s*=\s*1"));
        Assert.That(source, Does.Match(@"WallProp\s*=\s*2"));
        Assert.That(source, Does.Match(@"FurnitureSurfaceProp\s*=\s*3"));
        Assert.That(source, Does.Match(@"ForegroundOccluder\s*=\s*4"));
    }

    [Test]
    public void RoomPerspectiveProfileAndProjectedEntityExposeNoCharacterOrPropScaleSeam()
    {
        string profileSource = File.ReadAllText(RoomPerspectiveProfilePath);
        string projectionSource = File.ReadAllText(RoomProjectedEntityPath);
        string[] profileScaleSymbols =
        {
            "nativeRoomReferenceSize",
            "NativeRoomReferenceSize",
            "scaleByDepth",
            "NearScale",
            "FarScale",
            "GetScale(",
            "GetScaleAtDepth",
            "SetScaleEndpoints",
            "ApplyScaleMultiplier",
            "CreateDepthScaleCurve"
        };
        string[] projectionScaleSymbols =
        {
            "applyScale",
            "propProjectionBaseScale",
            "hasPropProjectionBaseScale",
            "CurrentPropProjectionScale",
            "CurrentRoomStageScaleMultiplier",
            "GetPropProjectionScale",
            "ShouldApplyPropProjectionScale",
            "IsPropProjectionMode",
            "ApplyProjectedPropScale",
            "SanitizeScale"
        };

        foreach (string symbol in profileScaleSymbols)
        {
            Assert.That(profileSource, Does.Not.Contain(symbol),
                $"RoomPerspectiveProfile must not retain scale seam '{symbol}'.");
        }

        foreach (string symbol in projectionScaleSymbols)
        {
            Assert.That(projectionSource, Does.Not.Contain(symbol),
                $"RoomProjectedEntity must not retain scale seam '{symbol}'.");
        }

        Assert.That(projectionSource, Does.Not.Contain("targetRoot.localScale ="));
    }

    [Test]
    public void SerializedProjectionAndProfileAssetsContainNoScaleSeam()
    {
        string[] serializedPaths =
        {
            GameplayPath,
            "Assets/Prefabs/Room_Drawing_Room.prefab",
            "Assets/Prefabs/Room_Drawing_Room_Perspective.prefab"
        };
        var projectionBlocks = new List<string>();

        foreach (string path in serializedPaths)
        {
            string text = File.ReadAllText(path);
            projectionBlocks.AddRange(
                Regex.Matches(text, @"(?ms)^--- !u!114 &.*?(?=^--- !u!|\z)")
                    .Cast<Match>()
                    .Select(match => match.Value)
                    .Where(block => block.Contains($"guid: {RoomProjectedEntityGuid}")));
        }

        Assert.That(projectionBlocks, Has.Count.EqualTo(13));
        Assert.That(projectionBlocks.All(block => Regex.IsMatch(block, @"(?m)^  projectionMode: 4$")), Is.True);
        Assert.That(projectionBlocks.All(block => !Regex.IsMatch(block, @"(?m)^  applyScale:")), Is.True);
        Assert.That(projectionBlocks.All(block => !Regex.IsMatch(block, @"(?m)^  propProjectionBaseScale:")), Is.True);
        Assert.That(projectionBlocks.All(block => !Regex.IsMatch(block, @"(?m)^  hasPropProjectionBaseScale:")), Is.True);

        foreach (string profilePath in new[] { DrawingRoomProfilePath, DiningRoomProfilePath })
        {
            string profile = File.ReadAllText(profilePath);
            Assert.That(profile, Does.Not.Contain("\n  scaleByDepth:"), profilePath);
            Assert.That(profile, Does.Not.Contain("\n  nativeRoomReferenceSize:"), profilePath);
            Assert.That(profile, Does.Contain("\n  nearFootY:"), profilePath);
            Assert.That(profile, Does.Contain("\n  farFootY:"), profilePath);
            Assert.That(profile, Does.Contain("\n  tintByDepth:"), profilePath);
            Assert.That(profile, Does.Contain("\n  sortingOrderByDepth:"), profilePath);
            Assert.That(profile, Does.Contain("\n  shadowScaleByDepth:"), profilePath);
            Assert.That(profile, Does.Contain("\n  shadowOpacityByDepth:"), profilePath);
        }
    }

    [Test]
    public void DeletedProjectionScaleTypesLeaveNoGuidReferences()
    {
        string[] serializedPaths = Directory.GetFiles("Assets", "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (string path in serializedPaths)
        {
            string text = File.ReadAllText(path);
            Assert.That(text, Does.Not.Contain(CharacterVisualProfileGuid), path);
            Assert.That(text, Does.Not.Contain(RoomProjectedEntityEditorGuid), path);
        }
    }

    private static bool InvokeTryCreateCaptureItem(MethodInfo method, Transform target, out object captureItem)
    {
        object[] arguments = { target, null };
        bool accepted = (bool)method.Invoke(null, arguments);
        captureItem = arguments[1];
        return accepted;
    }

    private static string ExtractYamlObjectBlock(string yaml, string fileId)
    {
        Match match = Regex.Match(
            yaml,
            $@"(?ms)^--- !u!\d+ &{Regex.Escape(fileId)}(?: stripped)?\r?\n.*?(?=^--- !u!|\z)");
        Assert.That(match.Success, Is.True, $"Could not find serialized object {fileId}.");
        return match.Value.Replace("\r\n", "\n");
    }

    private static void AssertPrefabInstanceScale(
        string prefabInstanceBlock,
        string axis,
        float expected,
        float prefabDefault)
    {
        Match valueMatch = Regex.Match(
            prefabInstanceBlock,
            $@"propertyPath: m_LocalScale\.{axis}\n      value: ([^\n]+)");
        float effective = valueMatch.Success
            ? float.Parse(valueMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)
            : prefabDefault;
        Assert.That(effective, Is.EqualTo(expected).Within(0.000001f), $"Unexpected effective {axis}-scale.");
    }

    private static void AssertHierarchyHasNoMissingOrObsoleteComponents(
        GameObject root,
        IReadOnlyCollection<string> obsoleteComponentTypeNames,
        string assetPath)
    {
        foreach (Transform current in root.GetComponentsInChildren<Transform>(true))
        {
            Assert.That(
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(current.gameObject),
                Is.Zero,
                $"{assetPath}::{current.name} contains a missing MonoBehaviour script slot.");

            foreach (Component component in current.GetComponents<Component>())
            {
                Assert.That(component, Is.Not.Null,
                    $"{assetPath}::{current.name} contains a missing component slot.");
                Assert.That(
                    obsoleteComponentTypeNames.Contains(component.GetType().Name),
                    Is.False,
                    $"{assetPath}::{current.name} retains obsolete component {component.GetType().Name}.");
            }
        }
    }

    private static string ExtractMethodBody(string source, string methodName)
    {
        Match declaration = Regex.Match(
            source,
            $@"(?m)^[ \t]*(?:(?:public|private|protected|internal|static|virtual|override|sealed|async|new)[ \t]+)*[A-Za-z_][A-Za-z0-9_<>,\[\]?]*[ \t]+{Regex.Escape(methodName)}[ \t]*\(");
        Assert.That(declaration.Success, Is.True, $"Could not find method '{methodName}'.");
        int bodyStart = source.IndexOf('{', declaration.Index);
        Assert.That(bodyStart, Is.GreaterThanOrEqualTo(0), $"Could not find body for '{methodName}'.");
        int depth = 0;

        for (int i = bodyStart; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return source.Substring(bodyStart, i - bodyStart + 1);
                }
            }
        }

        Assert.Fail($"Could not find end of method '{methodName}'.");
        return string.Empty;
    }

    [Serializable]
    private sealed class LegacySnapshot
    {
        public int schemaVersion;
        public SnapshotSource source;
        public ButlerSnapshot butler;
        public GuestRoomCalibrationSnapshot guestRoomCalibration;
        public GuestSnapshot[] guests;
        public RoomPerspectiveProfileSnapshot[] roomPerspectiveProfiles;
        public PosePlacementSnapshot posePlacement;
        public IntegritySnapshot integrity;
    }

    [Serializable]
    private sealed class SnapshotSource
    {
        public string gitCommit;
        public string unityVersion;
        public SnapshotSourceFile[] files;
    }

    [Serializable]
    private sealed class SnapshotSourceFile
    {
        public string path;
        public string guid;
        public string sha256;
    }

    [Serializable]
    private sealed class ButlerSnapshot
    {
        public SerializedRecord[] roomOverrides;
    }

    [Serializable]
    private sealed class GuestRoomCalibrationSnapshot
    {
        public SerializedRecord[] rooms;
    }

    [Serializable]
    private sealed class GuestSnapshot
    {
        public SittingMappingSnapshot sittingMapping;
    }

    [Serializable]
    private sealed class SittingMappingSnapshot
    {
        public string replacementClipGuid;
    }

    [Serializable]
    private sealed class RoomPerspectiveProfileSnapshot
    {
        public string roomId;
    }

    [Serializable]
    private sealed class PosePlacementSnapshot
    {
        public DrawingRoomPlacementSnapshot drawingRoom;
        public DiningRoomPlacementSnapshot diningRoom;
    }

    [Serializable]
    private sealed class DrawingRoomPlacementSnapshot
    {
        public SerializedRecord[] assignments;
        public string[] standingCharacterIds;
    }

    [Serializable]
    private sealed class DiningRoomPlacementSnapshot
    {
        public SerializedRecord[] assignments;
        public SerializedRecord[] occlusionBindings;
    }

    [Serializable]
    private sealed class IntegritySnapshot
    {
        public ExpectedCounts expectedCounts;
    }

    [Serializable]
    private sealed class ExpectedCounts
    {
        public int sourceFiles;
        public int butlerRoomOverrides;
        public int guestRoomCalibrationRows;
        public int guests;
        public int participantRecords;
        public int sittingMappings;
        public int drawingRoomAssignments;
        public int diningRoomAssignments;
        public int diningRoomOcclusionBindings;
        public int roomPerspectiveProfiles;
    }

    [Serializable]
    private sealed class SerializedRecord
    {
        public string propertyPath;
        public string rawValue;
        public string provenance;
    }
}
