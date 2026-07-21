using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class CharacterDisplayScaleArchitectureTests
{
    private const string TestRoomId = "Scale Test Room";
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";

    [Test]
    public void SameRoomSameYUsesSameScaleForButlerAndGuest()
    {
        CharacterDisplayScaleCatalog catalog = CreateCatalog(TestRoomId, 2.5f, 0.75f);

        try
        {
            Assert.That(
                catalog.TryEvaluateScale(
                    TestRoomId,
                    CharacterDisplayId.Butler,
                    CharacterDisplayState.Normal,
                    -50f,
                    out float butlerScale),
                Is.True);
            Assert.That(
                catalog.TryEvaluateScale(
                    TestRoomId,
                    CharacterDisplayId.Guest8,
                    CharacterDisplayState.Normal,
                    -50f,
                    out float guestScale),
                Is.True);
            Assert.That(guestScale, Is.EqualTo(butlerScale).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void RoomDefaultAppliesToEveryCharacterWithoutOverrides()
    {
        CharacterDisplayScaleCatalog catalog = CreateCatalog(TestRoomId, 3f, 1f);

        try
        {
            foreach (CharacterDisplayId characterId in Enum.GetValues(typeof(CharacterDisplayId)))
            {
                Assert.That(
                    catalog.TryEvaluateScale(
                        TestRoomId,
                        characterId,
                        CharacterDisplayState.Normal,
                        -25f,
                        out float scale),
                    Is.True,
                    characterId.ToString());
                Assert.That(scale, Is.EqualTo(1.5f).Within(0.0001f), characterId.ToString());
            }
        }
        finally
        {
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void IndividualOverrideReplacesOnlyTheSelectedCharacterDefault()
    {
        RoomDisplayScaleEntry room = CreateRoom(TestRoomId, 2f, 1f);
        room.SetCharacterOverride(new CharacterDisplayScaleOverride(
            CharacterDisplayId.Guest4,
            new Vector2(0f, -100f),
            4f,
            new Vector2(0f, 0f),
            2f,
            AnimationCurve.Linear(0f, 0f, 1f, 1f)));
        CharacterDisplayScaleCatalog catalog = CreateCatalog(room);

        try
        {
            Assert.That(catalog.TryEvaluateScale(
                TestRoomId,
                CharacterDisplayId.Guest4,
                CharacterDisplayState.Normal,
                -50f,
                out float overriddenScale), Is.True);
            Assert.That(catalog.TryEvaluateScale(
                TestRoomId,
                CharacterDisplayId.Guest3,
                CharacterDisplayState.Normal,
                -50f,
                out float defaultScale), Is.True);
            Assert.That(overriddenScale, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(defaultScale, Is.EqualTo(1.5f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void DrawingRoomSeatedUsesOnlyTheAllowedDrawingOverride()
    {
        RoomDisplayScaleEntry room = CreateRoom(CharacterDisplayScaleCatalog.DrawingRoomId, 2f, 1f);
        room.SetStateOverrides(new RoomStateScaleOverrides(
            new CharacterDisplayStateScaleOverride(true, 1.37f),
            new CharacterDisplayStateScaleOverride(false, 9f)));
        CharacterDisplayScaleCatalog catalog = CreateCatalog(room);

        try
        {
            Assert.That(catalog.TryEvaluateScale(
                CharacterDisplayScaleCatalog.DrawingRoomId,
                CharacterDisplayId.Guest1,
                CharacterDisplayState.DrawingRoomSeated,
                -100f,
                out float seatedScale), Is.True);
            Assert.That(catalog.TryEvaluateScale(
                CharacterDisplayScaleCatalog.DrawingRoomId,
                CharacterDisplayId.Guest1,
                CharacterDisplayState.Normal,
                -100f,
                out float standingScale), Is.True);
            Assert.That(seatedScale, Is.EqualTo(1.37f).Within(0.0001f));
            Assert.That(standingScale, Is.EqualTo(2f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void DiningRoomSeatedUsesOnlyTheAllowedDiningOverride()
    {
        RoomDisplayScaleEntry room = CreateRoom(CharacterDisplayScaleCatalog.DiningRoomId, 2f, 1f);
        room.SetStateOverrides(new RoomStateScaleOverrides(
            new CharacterDisplayStateScaleOverride(false, 9f),
            new CharacterDisplayStateScaleOverride(true, 1.21f)));
        CharacterDisplayScaleCatalog catalog = CreateCatalog(room);

        try
        {
            Assert.That(catalog.TryEvaluateScale(
                CharacterDisplayScaleCatalog.DiningRoomId,
                CharacterDisplayId.Guest2,
                CharacterDisplayState.DiningRoomSeated,
                0f,
                out float seatedScale), Is.True);
            Assert.That(catalog.TryEvaluateScale(
                CharacterDisplayScaleCatalog.DiningRoomId,
                CharacterDisplayId.Guest2,
                CharacterDisplayState.Normal,
                0f,
                out float standingScale), Is.True);
            Assert.That(seatedScale, Is.EqualTo(1.21f).Within(0.0001f));
            Assert.That(standingScale, Is.EqualTo(1f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void ControllerUsesAbsoluteScaleWithoutAccumulationOrGameplayRootWrites()
    {
        CharacterDisplayScaleCatalog catalog = CreateCatalog(TestRoomId, 2f, 1f);
        GameObject controllerObject = new GameObject("Display Scale Controller Test");
        GameObject actor = new GameObject("Guest1");
        GameObject visual = new GameObject("AnimationDisplay");

        controllerObject.SetActive(false);
        actor.SetActive(false);
        visual.transform.SetParent(actor.transform, false);
        visual.transform.localScale = new Vector3(-0.45f, 0.45f, 1.3f);
        actor.transform.position = new Vector3(12f, 34f, 56f);
        actor.transform.localScale = new Vector3(1.1f, 1.2f, 1.3f);

        try
        {
            CharacterDisplayScaleTestContext context = actor.AddComponent<CharacterDisplayScaleTestContext>();
            context.RoomId = TestRoomId;
            context.RoomLocalFootY = -50f;
            CharacterDisplayScaleSubject subject = actor.AddComponent<CharacterDisplayScaleSubject>();
            subject.ConfigureForEditor(CharacterDisplayId.Guest1, visual.transform, context);

            CharacterDisplayScaleController controller =
                controllerObject.AddComponent<CharacterDisplayScaleController>();
            controller.ConfigureForEditor(catalog);
            controllerObject.SetActive(true);
            actor.SetActive(true);

            Vector3 authoredActorPosition = actor.transform.position;
            Vector3 authoredActorScale = actor.transform.localScale;

            Assert.That(controller.TryApplySubject(subject), Is.True);
            Vector3 firstResult = visual.transform.localScale;
            Assert.That(controller.TryApplySubject(subject), Is.True);
            Vector3 secondResult = visual.transform.localScale;

            Assert.That(firstResult, Is.EqualTo(new Vector3(-1.5f, 1.5f, 1.3f)));
            Assert.That(secondResult, Is.EqualTo(firstResult), "Repeated updates must not compound scale.");
            Assert.That(actor.transform.position, Is.EqualTo(authoredActorPosition));
            Assert.That(actor.transform.localScale, Is.EqualTo(authoredActorScale));
        }
        finally
        {
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void CatalogValidationRejectsMissingRoomsIdenticalYInvalidCurvesAndDuplicates()
    {
        CharacterDisplayScaleCatalog emptyCatalog = ScriptableObject.CreateInstance<CharacterDisplayScaleCatalog>();
        CharacterDisplayScaleCatalog invalidCatalog = ScriptableObject.CreateInstance<CharacterDisplayScaleCatalog>();
        RoomDisplayScaleEntry duplicateOverrideRoom = CreateRoom(TestRoomId, 2f, 1f);
        CharacterDisplayScaleOverride guestOverride = new CharacterDisplayScaleOverride(
            CharacterDisplayId.Guest1,
            new Vector2(0f, -100f),
            2f,
            new Vector2(0f, 0f),
            1f,
            AnimationCurve.Linear(0f, 0f, 1f, 1f));
        duplicateOverrideRoom.SetCharacterOverride(guestOverride);

        try
        {
            Assert.That(emptyCatalog.ValidateCatalog(out string emptyReport), Is.False);
            Assert.That(emptyReport, Does.Contain("no room"));

            invalidCatalog.SetRooms(new[]
            {
                new RoomDisplayScaleEntry(
                    TestRoomId,
                    new Vector2(0f, -50f),
                    2f,
                    new Vector2(0f, -50f),
                    1f,
                    new AnimationCurve())
            });
            Assert.That(invalidCatalog.ValidateCatalog(out string invalidReport), Is.False);
            Assert.That(invalidReport, Does.Contain("different"));
        }
        finally
        {
            Object.DestroyImmediate(emptyCatalog);
            Object.DestroyImmediate(invalidCatalog);
        }
    }

    [Test]
    public void CanonicalCatalogAndGameplayActorsAreFullyIntegrated()
    {
        CharacterDisplayScaleCatalog catalog = AssetDatabase.LoadAssetAtPath<CharacterDisplayScaleCatalog>(
            CharacterDisplayScaleCatalog.DefaultAssetPath);
        Assert.That(catalog, Is.Not.Null, "The canonical runtime catalog asset is missing.");
        Assert.That(catalog.ValidateCatalog(out string report), Is.True, report);
        Assert.That(catalog.Rooms, Has.Count.EqualTo(19));
        Assert.That(CharacterDisplayScaleCatalog.LoadDefault(), Is.SameAs(catalog));

        Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Additive);

        try
        {
            CharacterDisplayScaleSubject[] subjects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CharacterDisplayScaleSubject>(true))
                .ToArray();

            Assert.That(subjects, Has.Length.EqualTo(9), "Gameplay must contain the Butler plus eight managed Guests.");
            Assert.That(subjects.Select(subject => subject.CharacterId), Is.EquivalentTo(
                Enum.GetValues(typeof(CharacterDisplayId)).Cast<CharacterDisplayId>()));

            foreach (CharacterDisplayScaleSubject subject in subjects)
            {
                Assert.That(subject.HasValidVisualScaleRoot(), Is.True, subject.name);
                Assert.That(subject.VisualScaleRoot.name, Is.EqualTo("AnimationDisplay"), subject.name);
                Assert.That(subject.VisualScaleRoot, Is.Not.SameAs(subject.transform), subject.name);
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void RuntimeBootstrapAndSingleControllerRemainTheOnlyApplicationPath()
    {
        string controllerSource = File.ReadAllText(
            "Assets/Scripts/Characters/DisplayScale/CharacterDisplayScaleController.cs");
        string bootstrapSource = File.ReadAllText(
            "Assets/Scripts/Characters/DisplayScale/CharacterDisplayScaleBootstrap.cs");
        string animationDisplaySource = File.ReadAllText(
            "Assets/Scripts/Characters/CharacterAnimationDisplay.cs");

        Assert.That(controllerSource, Does.Contain("visualScaleRoot.localScale = requestedScale;").IgnoreCase);
        Assert.That(bootstrapSource, Does.Contain("RuntimeInitializeOnLoadMethod")
            .And.Contain("DontDestroyOnLoad"));
        Assert.That(animationDisplaySource, Does.Not.Contain("localScale ="));
        Assert.That(AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/Scripts/Characters/CharacterScaleCatalog.cs"), Is.Null);
        Assert.That(AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/Scripts/Characters/CharacterScaleFunction.cs"), Is.Null);
        Assert.That(AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/Scripts/Characters/CharacterScaleRoom.cs"), Is.Null);
    }

    private static CharacterDisplayScaleCatalog CreateCatalog(
        string roomId,
        float frontScale,
        float backScale)
    {
        return CreateCatalog(CreateRoom(roomId, frontScale, backScale));
    }

    private static CharacterDisplayScaleCatalog CreateCatalog(RoomDisplayScaleEntry room)
    {
        CharacterDisplayScaleCatalog catalog =
            ScriptableObject.CreateInstance<CharacterDisplayScaleCatalog>();
        catalog.SetRooms(new[] { room });
        return catalog;
    }

    private static RoomDisplayScaleEntry CreateRoom(
        string roomId,
        float frontScale,
        float backScale)
    {
        return new RoomDisplayScaleEntry(
            roomId,
            new Vector2(0f, -100f),
            frontScale,
            new Vector2(0f, 0f),
            backScale,
            AnimationCurve.Linear(0f, 0f, 1f, 1f));
    }
}

public sealed class CharacterDisplayScaleTestContext : MonoBehaviour, ICharacterDisplayScaleContext
{
    public string RoomId { get; set; }
    public float RoomLocalFootY { get; set; }
    public CharacterDisplayState DisplayState { get; set; }

    public string CurrentRoomId => RoomId;
    public float CurrentRoomLocalFootY => RoomLocalFootY;
    public CharacterDisplayState CurrentDisplayState => DisplayState;
}
