using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class CharacterRoomScaleRegressionTests
{
    private const string CatalogPath = "Assets/Scripts/Characters/CharacterRoomScaleCatalog.cs";
    private const string ControllerPath = "Assets/Scripts/Characters/CharacterRoomScaleController.cs";
    private const string TargetPath = "Assets/Scripts/Characters/CharacterRoomScaleTarget.cs";
    private const string StageUtilityPath = "Assets/Scripts/Characters/CharacterRoomStageScaleUtility.cs";
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string TargetGuid = "b099f2b1c3494d8fa900d71915c16f31";

    [Test]
    public void CatalogInterpolatesButlerAndGuestFromTheSameRoomDepth()
    {
        GameObject catalogObject = new GameObject("CharacterRoomScaleCatalog_Test");

        try
        {
            CharacterRoomScaleCatalog catalog = catalogObject.AddComponent<CharacterRoomScaleCatalog>();
            CharacterRoomScaleEntry entry = catalog.GetOrCreateRoom("Drawing Room");
            entry.frontRoomLocalFootY = -400f;
            entry.backRoomLocalFootY = -100f;
            entry.butlerFrontLocalScaleY = 2f;
            entry.butlerBackLocalScaleY = 1f;
            entry.guestFrontLocalScaleY = 3f;
            entry.guestBackLocalScaleY = 1.5f;
            entry.scaleFunction = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            Assert.That(
                catalog.TryEvaluate("Drawing_Room", CharacterScaleProfile.Butler, -250f, out CharacterRoomScaleSample butler),
                Is.True);
            Assert.That(
                catalog.TryEvaluate("Drawing-Room", CharacterScaleProfile.Guest, -250f, out CharacterRoomScaleSample guest),
                Is.True);
            Assert.That(butler.FrontToBack01, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(guest.FrontToBack01, Is.EqualTo(butler.FrontToBack01).Within(0.0001f));
            Assert.That(butler.FinalLocalScaleY, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(guest.FinalLocalScaleY, Is.EqualTo(2.25f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(catalogObject);
        }
    }

    [Test]
    public void CatalogUsesTheSavedScaleFunctionBetweenSharedEndpoints()
    {
        GameObject catalogObject = new GameObject("CharacterRoomScaleCurve_Test");

        try
        {
            CharacterRoomScaleCatalog catalog = catalogObject.AddComponent<CharacterRoomScaleCatalog>();
            CharacterRoomScaleEntry entry = catalog.GetOrCreateRoom("Library");
            entry.frontRoomLocalFootY = -10f;
            entry.backRoomLocalFootY = 10f;
            entry.butlerFrontLocalScaleY = 2f;
            entry.butlerBackLocalScaleY = 1f;
            entry.scaleFunction = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            Assert.That(
                catalog.TryEvaluate("Library", CharacterScaleProfile.Butler, -5f, out CharacterRoomScaleSample sample),
                Is.True);
            Assert.That(sample.FrontToBack01, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(sample.FinalLocalScaleY, Is.GreaterThan(1.75f),
                "Ease-in should keep the character closer to the front endpoint than a linear quarter-depth sample.");
            Assert.That(sample.FinalLocalScaleY, Is.LessThan(2f));
        }
        finally
        {
            Object.DestroyImmediate(catalogObject);
        }
    }

    [Test]
    public void ControllerChangesOnlyTargetLocalScale()
    {
        GameObject catalogObject = new GameObject("CharacterRoomScaleCatalog_Test");
        GameObject controllerObject = new GameObject("CharacterRoomScaleController_Test");
        GameObject characterObject = new GameObject("Guest 1");

        try
        {
            CharacterRoomScaleCatalog catalog = catalogObject.AddComponent<CharacterRoomScaleCatalog>();
            CharacterRoomScaleEntry entry = catalog.GetOrCreateRoom("Grand Entrance Hall");
            entry.frontRoomLocalFootY = -10f;
            entry.backRoomLocalFootY = 10f;
            entry.butlerFrontLocalScaleY = 2f;
            entry.butlerBackLocalScaleY = 1f;
            entry.guestFrontLocalScaleY = 2f;
            entry.guestBackLocalScaleY = 1f;
            entry.scaleFunction = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            CharacterRoomScaleController controller = controllerObject.AddComponent<CharacterRoomScaleController>();
            controller.SetCatalog(catalog);
            CharacterRoomScaleTarget target = characterObject.AddComponent<CharacterRoomScaleTarget>();
            target.SetCurrentRoomId("Grand Entrance Hall");
            target.SetScaleProfile(CharacterScaleProfile.Guest);
            target.SetScaleRoot(characterObject.transform);
            characterObject.transform.localPosition = new Vector3(7f, 0f, 3f);
            characterObject.transform.localRotation = Quaternion.Euler(0f, 0f, 17f);
            characterObject.transform.localScale = Vector3.one;
            target.CaptureBaseScale(true);

            Vector3 positionBefore = characterObject.transform.localPosition;
            Quaternion rotationBefore = characterObject.transform.localRotation;
            bool activeBefore = characterObject.activeSelf;

            Assert.That(controller.RefreshTargetNow(target), Is.True);
            Assert.That(characterObject.transform.localScale.y, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(characterObject.transform.localPosition, Is.EqualTo(positionBefore));
            Assert.That(characterObject.transform.localRotation, Is.EqualTo(rotationBefore));
            Assert.That(characterObject.activeSelf, Is.EqualTo(activeBefore));
        }
        finally
        {
            Object.DestroyImmediate(characterObject);
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(catalogObject);
        }
    }

    [Test]
    public void TargetPreservesAuthoredAspectRatioCurrentFacingSignAndZScale()
    {
        GameObject characterObject = new GameObject("Guest Aspect Test");

        try
        {
            CharacterRoomScaleTarget target = characterObject.AddComponent<CharacterRoomScaleTarget>();
            characterObject.transform.localScale = new Vector3(2f, 1f, 7f);
            target.SetScaleRoot(characterObject.transform);
            target.CaptureBaseScale(true);
            characterObject.transform.localScale = new Vector3(-2f, 1f, 7f);

            Assert.That(target.ApplyFinalScale(3f), Is.True);
            Assert.That(characterObject.transform.localScale.x, Is.EqualTo(-6f).Within(0.0001f));
            Assert.That(characterObject.transform.localScale.y, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(characterObject.transform.localScale.z, Is.EqualTo(7f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(characterObject);
        }
    }

    [Test]
    public void EnsuringAnExistingTargetDoesNotReplaceItsConfiguredScaleRoot()
    {
        GameObject characterObject = new GameObject("Player");
        GameObject bodyObject = new GameObject("AnimatedBody");
        bodyObject.transform.SetParent(characterObject.transform, false);

        try
        {
            CharacterRoomScaleTarget target = characterObject.AddComponent<CharacterRoomScaleTarget>();
            target.SetScaleRoot(bodyObject.transform);

            CharacterRoomScaleTarget ensured = CharacterRoomScaleController.EnsureTargetForCharacterObject(
                characterObject,
                "Player",
                null,
                CharacterPose.Standing,
                CharacterScaleProfile.Butler,
                false);

            Assert.That(ensured, Is.SameAs(target));
            Assert.That(ensured.ScaleRoot, Is.SameAs(bodyObject.transform));
        }
        finally
        {
            Object.DestroyImmediate(characterObject);
        }
    }

    [Test]
    public void TargetOwnsItsBodyHierarchyButNotTheRoomStageAncestor()
    {
        GameObject roomObject = new GameObject("RoomStage");
        GameObject characterObject = new GameObject("Guest 1");
        GameObject visualObject = new GameObject("AnimatedBody");
        GameObject unrelatedObject = new GameObject("UnrelatedSibling");
        characterObject.transform.SetParent(roomObject.transform, false);
        visualObject.transform.SetParent(characterObject.transform, false);
        unrelatedObject.transform.SetParent(roomObject.transform, false);

        try
        {
            CharacterRoomScaleTarget target = characterObject.AddComponent<CharacterRoomScaleTarget>();
            target.SetScaleRoot(visualObject.transform);

            Assert.That(target.OwnsScaleTransform(visualObject.transform), Is.True);
            Assert.That(target.OwnsScaleTransform(characterObject.transform), Is.True);
            Assert.That(CharacterRoomScaleTarget.FindForTransform(visualObject.transform), Is.SameAs(target));
            Assert.That(CharacterRoomScaleTarget.FindForTransform(characterObject.transform), Is.SameAs(target));
            Assert.That(target.OwnsScaleTransform(roomObject.transform), Is.False);
            Assert.That(CharacterRoomScaleTarget.FindForTransform(roomObject.transform), Is.Null,
                "A room-stage scale writer must not be blocked merely because a managed character is below it.");
            Assert.That(target.OwnsScaleTransform(unrelatedObject.transform), Is.False);
            Assert.That(CharacterRoomScaleTarget.FindForTransform(unrelatedObject.transform), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(roomObject);
        }
    }

    [Test]
    public void StageCompensationDoesNotDoubleScaleCharactersUnderRoomStage()
    {
        Assert.That(
            CharacterRoomStageScaleUtility.CalculateTargetLocalScale(
                calibratedLocalScale: 2f,
                currentZoomRatio: 1.5f,
                inheritedZoomRatio: 1.5f),
            Is.EqualTo(2f).Within(0.0001f));
        Assert.That(
            CharacterRoomStageScaleUtility.CalculateTargetLocalScale(
                calibratedLocalScale: 2f,
                currentZoomRatio: 1.5f,
                inheritedZoomRatio: 1f),
            Is.EqualTo(3f).Within(0.0001f));
    }

    [Test]
    public void GameplaySceneContainsOneCatalogOneControllerNineteenRoomsAndNineTargets()
    {
        string text = File.ReadAllText(GameplayScenePath);

        Assert.That(Count(text, "Assembly-CSharp::CharacterRoomScaleCatalog"), Is.EqualTo(1));
        Assert.That(Count(text, "Assembly-CSharp::CharacterRoomScaleController"), Is.EqualTo(1));
        Assert.That(Count(text, "Assembly-CSharp::CharacterRoomScaleTarget"), Is.EqualTo(9));
        Assert.That(Count(text, "  - roomId:"), Is.EqualTo(19));
        Assert.That(Count(text, "    scaleFunction:"), Is.EqualTo(19));
        Assert.That(Count(text, "scaleProfile: 1"), Is.EqualTo(1));
        Assert.That(Count(text, "scaleProfile: 2"), Is.EqualTo(8));
        Assert.That(Count(text, "characterId: Player"), Is.EqualTo(1));
        Assert.That(Count(text, "characterId: Guest"), Is.EqualTo(8));
        Assert.That(text, Does.Not.Contain("Assembly-CSharp::GuestRoomScaleCalibration"));
        Assert.That(text, Does.Not.Contain("Assembly-CSharp::GuestRoomScaleApplier"));
        Assert.That(text, Does.Not.Contain("Assembly-CSharp::GuestScaleParticipant"));
    }

    [Test]
    public void SharedPlayerPrefabDoesNotContainACharacterScaleTarget()
    {
        string prefabText = File.ReadAllText(PlayerPrefabPath);
        string sceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(prefabText, Does.Not.Contain(TargetGuid),
            "The Player prefab is shared by Guests, so the Butler target must stay on the Player scene instance.");
        Assert.That(sceneText, Does.Contain(TargetGuid));
    }

    [Test]
    public void RemovedScaleArchitecturesAreAbsentFromTheProject()
    {
        string[] removedPaths =
        {
            "Assets/Editor/ButlerRoomScaleCalibrationWindow.cs",
            "Assets/Editor/GuestButlerScaleRegressionTests.cs",
            "Assets/Editor/GuestRoomScaleMasterWindow.cs",
            "Assets/Editor/GuestScaleAudit.cs",
            "Assets/Scripts/Characters/GuestRoomScaleApplier.cs",
            "Assets/Scripts/Characters/GuestRoomScaleCalibration.cs",
            "Assets/Scripts/Characters/GuestRoomStageScaleUtility.cs",
            "Assets/Scripts/Characters/GuestScaleParticipant.cs"
        };

        for (int i = 0; i < removedPaths.Length; i++)
        {
            Assert.That(File.Exists(removedPaths[i]), Is.False, removedPaths[i]);
            Assert.That(File.Exists(removedPaths[i] + ".meta"), Is.False, removedPaths[i] + ".meta");
        }
    }

    [Test]
    public void DedicatedModuleContainsNoGameplayOrPlacementWriter()
    {
        string controller = File.ReadAllText(ControllerPath);
        string target = File.ReadAllText(TargetPath);
        string stageUtility = File.ReadAllText(StageUtilityPath);

        Assert.That(controller, Does.Contain("target.ApplyFinalScale"));
        Assert.That(controller, Does.Not.Contain("ActiveInstance"), "The scale module must not globally disable unrelated legacy objects.");
        Assert.That(controller, Does.Not.Contain("SetActive("));
        Assert.That(controller, Does.Not.Contain(".position ="));
        Assert.That(controller, Does.Not.Contain(".localPosition ="));
        Assert.That(controller, Does.Not.Contain(".rotation ="));
        Assert.That(controller, Does.Not.Contain(".localRotation ="));
        Assert.That(controller, Does.Not.Contain("sortingOrder ="));
        Assert.That(controller, Does.Not.Contain("Animator"));

        Assert.That(target, Does.Contain("root.localScale = targetScale"));
        Assert.That(target, Does.Not.Contain("SetActive("));
        Assert.That(target, Does.Not.Contain(".localPosition ="));
        Assert.That(target, Does.Not.Contain(".localRotation ="));
        Assert.That(stageUtility, Does.Not.Contain(".localScale ="), "Stage compensation must be calculation-only.");
    }

    [Test]
    public void EveryKnownLegacyCharacterScaleWriterDefersToManagedTargets()
    {
        string movement = File.ReadAllText("Assets/Scripts/PointClickPlayerMovement.cs");
        string projected = File.ReadAllText("Assets/Scripts/Characters/RoomProjectedEntity.cs");
        string walker = File.ReadAllText("Assets/Scripts/Characters/RoomPersonWalker2D.cs");
        string actorState = File.ReadAllText("Assets/Scripts/Story/ActorRoomState.cs");
        string chapter1 = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs");
        string chapter2Panic = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestPanicController.cs");

        Assert.That(movement, Does.Contain("if (HasActiveCharacterRoomScaleTarget())"));
        Assert.That(movement, Does.Contain("return CharacterRoomScaleTarget.OwnsScaleFor(transform);"));
        Assert.That(projected, Does.Contain("HasActiveCharacterRoomScaleTarget(targetRoot)"));
        Assert.That(projected, Does.Contain("!HasActiveCharacterRoomScaleTarget(transform)"));
        Assert.That(walker, Does.Contain("!HasActiveCharacterRoomScaleTarget()"));
        Assert.That(actorState, Does.Contain("HasActiveCharacterRoomScaleTarget(targetTransform)"));
        Assert.That(chapter1, Does.Contain("if (!CharacterRoomScaleTarget.OwnsScaleFor(guestObject.transform))"));
        Assert.That(chapter2Panic, Does.Contain("CharacterRoomScaleTarget.OwnsScaleFor(rootTransform)"));
        Assert.That(chapter2Panic, Does.Contain("characterRoomScaleControllerOwnsScale"));
    }

    [Test]
    public void CatalogAndControllerAreTheOnlyNewRoomDependentSizeDataAndApplicationPath()
    {
        string catalog = File.ReadAllText(CatalogPath);
        string controller = File.ReadAllText(ControllerPath);
        string target = File.ReadAllText(TargetPath);

        Assert.That(catalog, Does.Contain("frontRoomLocalFootY"));
        Assert.That(catalog, Does.Contain("backRoomLocalFootY"));
        Assert.That(catalog, Does.Contain("butlerFrontLocalScaleY"));
        Assert.That(catalog, Does.Contain("guestFrontLocalScaleY"));
        Assert.That(catalog, Does.Contain("scaleFunction"));
        Assert.That(controller, Does.Contain("catalog.TryEvaluate"));
        Assert.That(controller, Does.Contain("CharacterRoomStageScaleUtility.CalculateTargetLocalScale"));
        Assert.That(controller, Does.Contain("target.ApplyFinalScale"));
        Assert.That(target, Does.Not.Contain("frontRoomLocalFootY"));
        Assert.That(target, Does.Not.Contain("backRoomLocalFootY"));
        Assert.That(target, Does.Not.Contain("AnimationCurve"));
    }

    private static int Count(string value, string needle)
    {
        int count = 0;
        int index = 0;

        while ((index = value.IndexOf(needle, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
