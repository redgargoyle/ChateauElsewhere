using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CharacterScaleArchitectureTests
{
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

    [TestCase(-400f, -400f, 2f, -100f, 1f, 2f)]
    [TestCase(-250f, -400f, 2f, -100f, 1f, 1.5f)]
    [TestCase(-100f, -400f, 2f, -100f, 1f, 1f)]
    [TestCase(-600f, -400f, 2f, -100f, 1f, 2f)]
    [TestCase(100f, -400f, 2f, -100f, 1f, 1f)]
    [TestCase(-250f, -100f, 1f, -400f, 2f, 1.5f)]
    public void SharedYScaleFunctionIsLinearClampedAndOrderIndependent(
        float characterY,
        float frontY,
        float frontScale,
        float backY,
        float backScale,
        float expected)
    {
        Assert.That(
            CharacterScaleFunction.Evaluate(characterY, frontY, frontScale, backY, backScale),
            Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public void FrontAndBackXNeverAffectScale()
    {
        using (ScaleRig rig = ScaleRig.Create())
        {
            rig.Front.localPosition = new Vector3(-500f, -400f, 0f);
            rig.Back.localPosition = new Vector3(900f, -100f, 0f);

            Vector3 leftPoint = rig.Room.transform.TransformPoint(new Vector3(-1000f, -250f, 0f));
            Vector3 rightPoint = rig.Room.transform.TransformPoint(new Vector3(1000f, -250f, 0f));

            Assert.That(rig.ScaleRoom.TryEvaluateScale(leftPoint, out float leftScale), Is.True);
            Assert.That(rig.ScaleRoom.TryEvaluateScale(rightPoint, out float rightScale), Is.True);
            Assert.That(leftScale, Is.EqualTo(rightScale).Within(0.0001f));
            Assert.That(leftScale, Is.EqualTo(1.5f).Within(0.0001f));
        }
    }

    [Test]
    public void AnimationDisplayChangesOnlyVisualChildSize()
    {
        using (ScaleRig rig = ScaleRig.Create())
        {
            GameObject actor = new GameObject("Butler", typeof(BoxCollider2D));
            GameObject visual = new GameObject("AnimationDisplay", typeof(SpriteRenderer), typeof(Animator));
            visual.transform.SetParent(actor.transform, false);
            CharacterAnimationDisplay display = actor.AddComponent<CharacterAnimationDisplay>();
            display.Configure(visual.transform, rig.Catalog);

            actor.transform.position = rig.Room.transform.TransformPoint(new Vector3(25f, -250f, 0f));
            Vector3 rootPosition = actor.transform.position;
            Vector3 rootScale = actor.transform.localScale;
            BoxCollider2D collider = actor.GetComponent<BoxCollider2D>();
            Vector2 colliderSize = collider.size;
            Physics2D.SyncTransforms();
            Bounds colliderBounds = collider.bounds;

            Assert.That(display.TryApplyScaleForRoom("Test Room"), Is.True);
            Assert.That(visual.transform.localScale, Is.EqualTo(new Vector3(1.5f, 1.5f, 1f)));
            Assert.That(actor.transform.position, Is.EqualTo(rootPosition));
            Assert.That(actor.transform.localScale, Is.EqualTo(rootScale));
            Assert.That(collider.size, Is.EqualTo(colliderSize));
            Physics2D.SyncTransforms();
            Assert.That(collider.bounds.size, Is.EqualTo(colliderBounds.size));

            UnityEngine.Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void ButlerAndGuestUseTheSameRoomAndYResultIncludingSitting()
    {
        using (ScaleRig rig = ScaleRig.Create())
        {
            GameObject butler = CreateActor("Butler", rig.Catalog, out CharacterAnimationDisplay butlerDisplay);
            GameObject guest = CreateActor("Guest", rig.Catalog, out CharacterAnimationDisplay guestDisplay);
            ActorRoomState guestState = guest.AddComponent<ActorRoomState>();
            guestState.SetCurrentRoom("Test Room");

            GameObject guestFootAnchor = new GameObject("Guest Foot Anchor");
            guestFootAnchor.transform.SetParent(rig.Room.transform, false);
            guestFootAnchor.transform.localPosition = new Vector3(0f, -220f, 0f);
            guestState.BindToRoomStagePoint(guestFootAnchor.transform);

            Vector3 position = rig.Room.transform.TransformPoint(new Vector3(0f, -220f, 0f));
            butler.transform.position = position;
            guest.transform.position = rig.Room.transform.TransformPoint(new Vector3(0f, -100f, 0f));

            Assert.That(butlerDisplay.TryApplyScaleForRoom("Test Room"), Is.True);
            Assert.That(guestDisplay.TryApplyCurrentRoomScale(), Is.True);
            Vector3 standingScale = guestDisplay.AnimationDisplay.localScale;
            Assert.That(standingScale, Is.EqualTo(butlerDisplay.AnimationDisplay.localScale));

            guestState.SetSeated(true);
            Assert.That(guestDisplay.TryApplyCurrentRoomScale(), Is.True);
            Assert.That(guestDisplay.AnimationDisplay.localScale, Is.EqualTo(standingScale),
                "Forced sitting changes the Animator pose, never the room scale function.");

            UnityEngine.Object.DestroyImmediate(butler);
            UnityEngine.Object.DestroyImmediate(guest);
        }
    }

    [Test]
    public void MarkerScalesMustBePositiveAndUniform()
    {
        using (ScaleRig rig = ScaleRig.Create())
        {
            rig.Front.localScale = new Vector3(-2f, -2f, 1f);
            Assert.That(rig.ScaleRoom.IsConfigured(out string negativeReason), Is.False);
            Assert.That(negativeReason, Does.Contain("positive"));

            rig.Front.localScale = new Vector3(2f, 1.5f, 1f);
            Assert.That(rig.ScaleRoom.IsConfigured(out string nonUniformReason), Is.False);
            Assert.That(nonUniformReason, Does.Contain("uniform"));
        }
    }

    [Test]
    public void RoomStageZoomIsConvertedInsideTheCatalogOwner()
    {
        using (ScaleRig rig = ScaleRig.Create())
        {
            Vector3 authoredPoint = rig.Room.transform.TransformPoint(new Vector3(0f, -250f, 0f));
            Assert.That(rig.ScaleRoom.TryEvaluateScale(authoredPoint, out float authoredScale), Is.True);
            Assert.That(authoredScale, Is.EqualTo(1.5f).Within(0.0001f));

            rig.Room.transform.localScale = Vector3.one * 2f;
            Vector3 zoomedPoint = rig.Room.transform.TransformPoint(new Vector3(0f, -250f, 0f));
            Assert.That(rig.ScaleRoom.TryEvaluateScale(zoomedPoint, out float zoomedScale), Is.True);
            Assert.That(zoomedScale, Is.EqualTo(3f).Within(0.0001f));
        }
    }

    [Test]
    public void GameplayHasOneCompleteDefinitionForEveryAuthoritativeRoom()
    {
        Scene previousScene = SceneManager.GetActiveScene();
        Scene scene = OpenGameplayScene(out bool openedHere);

        try
        {
            CharacterScaleCatalog[] catalogs = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CharacterScaleCatalog>(true))
                .ToArray();
            RoomContentGroup[] roomGroups = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomContentGroup>(true))
                .GroupBy(room => CharacterScaleCatalog.NormalizeRoomName(room.RoomName), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();

            Assert.That(catalogs, Has.Length.EqualTo(1));
            Assert.That(roomGroups, Has.Length.EqualTo(19));
            Assert.That(catalogs[0].Rooms.Count, Is.EqualTo(19));
            Assert.That(catalogs[0].ValidateCatalog(out string report), Is.True, report);

            foreach (RoomContentGroup room in roomGroups)
            {
                Assert.That(catalogs[0].TryGetRoom(room.RoomName, out CharacterScaleRoom definition), Is.True, room.RoomName);
                Assert.That(definition.Room, Is.EqualTo(room));
                Assert.That(definition.Front.name, Is.EqualTo("Front"));
                Assert.That(definition.Back.name, Is.EqualTo("Back"));
                Assert.That(definition.Front.parent, Is.EqualTo(definition.Back.parent));
                Assert.That(definition.Front.parent.name, Is.EqualTo("Character Scale"));
                Assert.That(definition.Front.parent.IsChildOf(room.transform), Is.True);
                Assert.That(definition.ReferenceStageScale, Is.EqualTo(Mathf.Abs(room.transform.localScale.x)).Within(0.0001f));
            }
        }
        finally
        {
            CloseGameplayScene(scene, openedHere, previousScene);
        }
    }

    [Test]
    public void GameplayActorsKeepUnitRootsAndDedicatedAnimationDisplays()
    {
        Scene previousScene = SceneManager.GetActiveScene();
        Scene scene = OpenGameplayScene(out bool openedHere);

        try
        {
            CharacterAnimationDisplay[] displays = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CharacterAnimationDisplay>(true))
                .OrderBy(display => display.name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(displays, Has.Length.EqualTo(9));
            Assert.That(
                displays.Select(display => display.name),
                Is.EquivalentTo(new[] { "Player", "Guest 1", "Guest 2", "Guest 3", "Guest 4", "Guest 5", "Guest 6", "Guest 7", "Guest 8" }));

            foreach (CharacterAnimationDisplay display in displays)
            {
                Assert.That(display.transform.localScale, Is.EqualTo(Vector3.one), $"{display.name} actor root");
                Assert.That(display.GetComponent<SpriteRenderer>(), Is.Null, $"{display.name} root renderer");
                Assert.That(display.GetComponent<Animator>(), Is.Null, $"{display.name} root Animator");
                Assert.That(display.HasValidDisplayRoot(), Is.True, display.name);
                Assert.That(display.AnimationDisplay.parent, Is.EqualTo(display.transform), display.name);
                Assert.That(display.AnimationDisplay.name, Is.EqualTo("AnimationDisplay"), display.name);
                Assert.That(display.AnimationDisplay.localPosition, Is.EqualTo(Vector3.zero), display.name);
                Assert.That(display.AnimationDisplay.localScale, Is.EqualTo(Vector3.one), display.name);

                SpriteRenderer renderer = display.AnimationDisplay.GetComponent<SpriteRenderer>();
                Assert.That(renderer, Is.Not.Null, display.name);
                Assert.That(renderer.drawMode, Is.EqualTo(SpriteDrawMode.Simple), display.name);
                Assert.That(display.AnimationDisplay.GetComponent<Animator>(), Is.Not.Null, display.name);

                PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(display.gameObject) ??
                    Array.Empty<PropertyModification>();
                Assert.That(
                    modifications.Any(modification =>
                        modification.target is SpriteRenderer &&
                        modification.propertyPath.StartsWith("m_Size", StringComparison.Ordinal)),
                    Is.False,
                    $"{display.name} retains a SpriteRenderer size override outside CharacterAnimationDisplay.");
            }
        }
        finally
        {
            CloseGameplayScene(scene, openedHere, previousScene);
        }
    }

    [Test]
    public void ScreenSpaceRoomMappingMatchesDirectRoomYEvaluation()
    {
        Scene previousScene = SceneManager.GetActiveScene();
        Scene scene = OpenGameplayScene(out bool openedHere);
        RenderTexture renderTexture = null;
        Camera worldCamera = null;
        RenderTexture previousTarget = null;

        try
        {
            CharacterScaleRoom definition = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CharacterScaleRoom>(true))
                .First(room => room.IsConfigured(out _));
            worldCamera = Camera.main;
            Assert.That(worldCamera, Is.Not.Null, "Character scale world-to-room mapping requires Main Camera.");
            previousTarget = worldCamera.targetTexture;
            renderTexture = new RenderTexture(1366, 768, 24);
            renderTexture.Create();
            worldCamera.targetTexture = renderTexture;
            Canvas.ForceUpdateCanvases();

            float roomY = Mathf.Lerp(
                definition.GetRoomLocalPosition(definition.Front).y,
                definition.GetRoomLocalPosition(definition.Back).y,
                0.37f);
            Vector3 roomSurfaceWorldPoint = definition.Room.transform.TransformPoint(new Vector3(0f, roomY, 0f));
            Canvas canvas = definition.Room.GetComponentInParent<Canvas>();
            Camera canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, roomSurfaceWorldPoint);
            float depth = Mathf.Abs(worldCamera.transform.position.z);
            Vector3 detachedActorWorldPoint = worldCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, depth));

            Assert.That(definition.TryEvaluateScaleAtRoomY(roomY, out float directScale), Is.True);
            Assert.That(definition.TryEvaluateScale(detachedActorWorldPoint, out float mappedScale), Is.True);
            Assert.That(mappedScale, Is.EqualTo(directScale).Within(0.0001f));
        }
        finally
        {
            if (worldCamera != null)
            {
                worldCamera.targetTexture = previousTarget;
            }

            if (renderTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }

            CloseGameplayScene(scene, openedHere, previousScene);
        }
    }

    [Test]
    public void PlayerPrefabSeparatesAnimationDisplayFromMovementRoot()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(prefab.GetComponent<SpriteRenderer>(), Is.Null);
        Assert.That(prefab.GetComponent<Animator>(), Is.Null);

        CharacterAnimationDisplay display = prefab.GetComponent<CharacterAnimationDisplay>();
        Assert.That(display, Is.Not.Null);
        Assert.That(display.HasValidDisplayRoot(), Is.True);
        Assert.That(display.AnimationDisplay.name, Is.EqualTo("AnimationDisplay"));
        Assert.That(display.AnimationDisplay.GetComponent<SpriteRenderer>(), Is.Not.Null);
        Assert.That(display.AnimationDisplay.GetComponent<Animator>(), Is.Not.Null);
    }

    private static GameObject CreateActor(
        string name,
        CharacterScaleCatalog catalog,
        out CharacterAnimationDisplay display)
    {
        GameObject actor = new GameObject(name);
        GameObject visual = new GameObject("AnimationDisplay", typeof(SpriteRenderer), typeof(Animator));
        visual.transform.SetParent(actor.transform, false);
        display = actor.AddComponent<CharacterAnimationDisplay>();
        display.Configure(visual.transform, catalog);
        return actor;
    }

    private static Scene OpenGameplayScene(out bool openedHere)
    {
        Scene scene = SceneManager.GetSceneByPath(GameplayScenePath);
        openedHere = !scene.IsValid() || !scene.isLoaded;
        return openedHere
            ? EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Additive)
            : scene;
    }

    private static void CloseGameplayScene(Scene scene, bool openedHere, Scene previousScene)
    {
        if (openedHere && scene.IsValid() && scene.isLoaded)
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        if (previousScene.IsValid() && previousScene.isLoaded)
        {
            SceneManager.SetActiveScene(previousScene);
        }
    }

    private sealed class ScaleRig : IDisposable
    {
        public GameObject Root { get; private set; }
        public RoomContentGroup Room { get; private set; }
        public Transform Front { get; private set; }
        public Transform Back { get; private set; }
        public CharacterScaleRoom ScaleRoom { get; private set; }
        public CharacterScaleCatalog Catalog { get; private set; }

        public static ScaleRig Create()
        {
            ScaleRig rig = new ScaleRig();
            rig.Root = new GameObject("CharacterScaleTestRoot");
            GameObject catalogObject = new GameObject("Rooms", typeof(CharacterScaleCatalog));
            catalogObject.transform.SetParent(rig.Root.transform, false);
            rig.Catalog = catalogObject.GetComponent<CharacterScaleCatalog>();

            GameObject roomObject = new GameObject("Room_Test_Room", typeof(RectTransform), typeof(RoomContentGroup));
            roomObject.transform.SetParent(catalogObject.transform, false);
            rig.Room = roomObject.GetComponent<RoomContentGroup>();

            GameObject markerRoot = new GameObject("Character Scale");
            markerRoot.transform.SetParent(roomObject.transform, false);
            GameObject frontObject = new GameObject("Front");
            frontObject.transform.SetParent(markerRoot.transform, false);
            GameObject backObject = new GameObject("Back");
            backObject.transform.SetParent(markerRoot.transform, false);
            rig.Front = frontObject.transform;
            rig.Back = backObject.transform;
            rig.Front.localPosition = new Vector3(0f, -400f, 0f);
            rig.Back.localPosition = new Vector3(0f, -100f, 0f);
            rig.Front.localScale = new Vector3(2f, 2f, 1f);
            rig.Back.localScale = Vector3.one;

            rig.ScaleRoom = roomObject.AddComponent<CharacterScaleRoom>();
            rig.ScaleRoom.Configure(rig.Room, rig.Front, rig.Back, 1f);
            rig.Catalog.SetRooms(new[] { rig.ScaleRoom });
            return rig;
        }

        public void Dispose()
        {
            if (Root != null)
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }
        }
    }
}
