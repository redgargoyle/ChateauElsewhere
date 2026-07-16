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

public sealed class CharacterScaleOwnershipRegressionTests
{
    private const string SnapshotPath = "docs/migrations/character-scale/legacy-character-scale-snapshot.json";
    private const string GameplayPath = "Assets/Scenes/Gameplay.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string DrawingRoomProfilePath = "Assets/ScriptableObjects/Rooms/DrawingRoomPerspectiveProfile.asset";
    private const string DiningRoomProfilePath = "Assets/ScriptableObjects/Rooms/DiningRoomPerspectiveProfile.asset";
    private const string Chapter1ArrivalControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs";
    private const string PanicControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestPanicController.cs";
    private const string LayoutCaptureWindowPath = "Assets/Editor/PlayModeLayoutCaptureWindow.cs";

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
    public void AnimationClipsDoNotWriteTransformScale()
    {
        string[] clipPaths = Directory.GetFiles("Assets", "*.anim", SearchOption.AllDirectories)
            .Select(path => path.Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.That(clipPaths, Is.Not.Empty);

        foreach (string clipPath in clipPaths)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            Assert.That(clip, Is.Not.Null, clipPath);
            Assert.That(
                AnimationUtility.GetCurveBindings(clip).Any(
                    binding => binding.propertyName.StartsWith("m_LocalScale", StringComparison.Ordinal)),
                Is.False,
                clipPath);
        }
    }

    [Test]
    public void CharacterControllerFacingFlipsRenderersWithoutChangingRootScale()
    {
        GameObject actor = new GameObject("FacingActor");
        GameObject visual = new GameObject("Visual");

        try
        {
            visual.transform.SetParent(actor.transform, false);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.flipX = true;
            CharacterController2D controller = actor.AddComponent<CharacterController2D>();
            typeof(CharacterController2D).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(controller, null);
            Vector3 before = new Vector3(1.4f, 2.1f, 1f);
            actor.transform.localScale = before;

            MethodInfo flip = typeof(CharacterController2D).GetMethod(
                "Flip",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(flip, Is.Not.Null);
            FieldInfo facingRight = typeof(CharacterController2D).GetField(
                "m_FacingRight",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(facingRight, Is.Not.Null);
            Assert.That(facingRight.GetValue(controller), Is.False, "An authored flipX actor starts facing left.");

            flip.Invoke(controller, null);

            Assert.That(actor.transform.localScale, Is.EqualTo(before));
            Assert.That(renderer.flipX, Is.False);
            Assert.That(facingRight.GetValue(controller), Is.True);

            flip.Invoke(controller, null);

            Assert.That(actor.transform.localScale, Is.EqualTo(before));
            Assert.That(renderer.flipX, Is.True);
            Assert.That(facingRight.GetValue(controller), Is.False);
        }
        finally
        {
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

            actorState.SetSeated(true);
            Assert.That(actor.transform.localScale, Is.EqualTo(authoredScale));
            actorState.SetSeated(false);
            Assert.That(actor.transform.localScale, Is.EqualTo(authoredScale));

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
        const string temporaryScenePath = "Assets/__CharacterScaleLayoutCaptureRegression.unity";
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        try
        {
            EditorSceneManager.SetActiveScene(scene);

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
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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

    private static bool InvokeTryCreateCaptureItem(MethodInfo method, Transform target, out object captureItem)
    {
        object[] arguments = { target, null };
        bool accepted = (bool)method.Invoke(null, arguments);
        captureItem = arguments[1];
        return accepted;
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
