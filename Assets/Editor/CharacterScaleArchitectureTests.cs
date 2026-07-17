using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CharacterScaleArchitectureTests
{
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string CatalogSourcePath = "Assets/Scripts/Characters/CharacterScaleCatalog.cs";
    private const string TestRoomName = "Test Room";

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
    public void CatalogIsAValueOnlyScriptableObject()
    {
        Assert.That(typeof(CharacterScaleCatalog).IsSubclassOf(typeof(ScriptableObject)), Is.True);
        Assert.That(typeof(Component).IsAssignableFrom(typeof(CharacterScaleCatalog)), Is.False);

        FieldInfo roomsField = typeof(CharacterScaleCatalog).GetField(
            "rooms",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(roomsField, Is.Not.Null);
        Assert.That(roomsField.GetCustomAttribute<SerializeField>(), Is.Not.Null);
        Assert.That(
            roomsField.GetCustomAttribute<HideInInspector>(),
            Is.Not.Null,
            "Saved room rows should be edited through the explicit handle workflow, not the ordinary Inspector.");

        FieldInfo[] definitionFields = typeof(CharacterScaleRoomDefinition).GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(definitionFields, Is.Not.Empty);

        foreach (FieldInfo field in definitionFields)
        {
            Assert.That(
                typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType),
                Is.False,
                $"Runtime catalog field '{field.Name}' must contain saved values, not a scene object reference.");
        }

        string source = File.ReadAllText(CatalogSourcePath);
        Assert.That(
            Regex.IsMatch(
                source,
                @"#if\s+UNITY_EDITOR[\s\S]*?public\s+void\s+SetRooms\s*\([\s\S]*?public\s+void\s+SetRoom\s*\([\s\S]*?#endif"),
            Is.True,
            "Catalog mutation APIs must be absent from player/runtime compilation.");
    }

    [Test]
    public void FrontAndBackHandleXNeverAffectsSavedOrRuntimeScale()
    {
        using (ScaleRig rig = ScaleRig.Create())
        {
            rig.Front.localPosition = new Vector3(-500f, -400f, 0f);
            rig.Back.localPosition = new Vector3(900f, -100f, 0f);

            Assert.That(
                CharacterScaleTool.SaveRoomHandlesToCatalog(
                    rig.Catalog,
                    rig.ScaleRoom,
                    false,
                    out string report),
                Is.True,
                report);

            GameObject left = CreateActor("Left", rig.Catalog, out CharacterAnimationDisplay leftDisplay);
            GameObject right = CreateActor("Right", rig.Catalog, out CharacterAnimationDisplay rightDisplay);

            try
            {
                left.transform.position = rig.GetWorldPoint(-250f, -1000f);
                right.transform.position = rig.GetWorldPoint(-250f, 1000f);

                Assert.That(leftDisplay.TryApplyScaleForRoom(TestRoomName), Is.True);
                Assert.That(rightDisplay.TryApplyScaleForRoom(TestRoomName), Is.True);
                Assert.That(leftDisplay.AnimationDisplay.localScale.x, Is.EqualTo(1.5f).Within(0.0001f));
                Assert.That(rightDisplay.AnimationDisplay.localScale, Is.EqualTo(leftDisplay.AnimationDisplay.localScale));

                Assert.That(rig.Catalog.TryGetRoom(TestRoomName, out CharacterScaleRoomDefinition saved), Is.True);
                Assert.That(saved.FrontY, Is.EqualTo(-400f).Within(0.0001f));
                Assert.That(saved.BackY, Is.EqualTo(-100f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(left);
                UnityEngine.Object.DestroyImmediate(right);
            }
        }
    }

    [Test]
    public void MovingSceneHandlesCannotChangeRuntimeScaleUntilExplicitSave()
    {
        using (ScaleRig rig = ScaleRig.Create())
        {
            GameObject actor = CreateActor("UnsavedHandleProbe", rig.Catalog, out CharacterAnimationDisplay display);

            try
            {
                actor.transform.position = rig.GetWorldPoint(-400f);
                Assert.That(display.TryApplyScaleForRoom(TestRoomName), Is.True);
                Assert.That(display.AnimationDisplay.localScale.x, Is.EqualTo(2f).Within(0.0001f));

                // These are deliberately extreme but valid editor-handle values.
                // Runtime must continue to use the ScriptableObject snapshot.
                rig.SetHandleCalibration(-600f, 4f, -200f, 2f);

                Assert.That(display.TryApplyScaleForRoom(TestRoomName), Is.True);
                Assert.That(
                    display.AnimationDisplay.localScale.x,
                    Is.EqualTo(2f).Within(0.0001f),
                    "Moving or scaling editor handles must not become an implicit runtime save.");
                Assert.That(rig.Catalog.TryGetRoom(TestRoomName, out CharacterScaleRoomDefinition stillSaved), Is.True);
                Assert.That(stillSaved.FrontY, Is.EqualTo(-400f).Within(0.0001f));
                Assert.That(stillSaved.FrontScale, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(stillSaved.BackY, Is.EqualTo(-100f).Within(0.0001f));
                Assert.That(stillSaved.BackScale, Is.EqualTo(1f).Within(0.0001f));

                Assert.That(
                    CharacterScaleTool.SaveRoomHandlesToCatalog(
                        rig.Catalog,
                        rig.ScaleRoom,
                        false,
                        out string report),
                    Is.True,
                    report);

                Assert.That(display.TryApplyScaleForRoom(TestRoomName), Is.True);
                Assert.That(
                    display.AnimationDisplay.localScale.x,
                    Is.EqualTo(3f).Within(0.0001f),
                    "The same handles should affect runtime only after the explicit Save action copies them into the catalog.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }
        }
    }

    [Test]
    public void ExplicitLoadCopiesSavedValuesToHandlesWithoutChangingRuntimeData()
    {
        using (ScaleRig rig = ScaleRig.Create())
        {
            rig.SetHandleCalibration(-700f, 6f, -20f, 0.25f);
            Assert.That(rig.TryGetScaleAtRoomY(-250f, out float beforeLoad), Is.True);
            Assert.That(beforeLoad, Is.EqualTo(1.5f).Within(0.0001f));

            Assert.That(CharacterScaleTool.LoadAssetRoomIntoHandles(rig.Catalog, rig.ScaleRoom), Is.True);
            Assert.That(rig.Front.localPosition.y, Is.EqualTo(-400f).Within(0.0001f));
            Assert.That(rig.Front.localScale.x, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(rig.Back.localPosition.y, Is.EqualTo(-100f).Within(0.0001f));
            Assert.That(rig.Back.localScale.x, Is.EqualTo(1f).Within(0.0001f));

            Assert.That(rig.TryGetScaleAtRoomY(-250f, out float afterLoad), Is.True);
            Assert.That(afterLoad, Is.EqualTo(beforeLoad).Within(0.0001f));
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

            actor.transform.position = rig.GetWorldPoint(-250f, 25f);
            Vector3 rootPosition = actor.transform.position;
            Vector3 rootScale = actor.transform.localScale;
            BoxCollider2D collider = actor.GetComponent<BoxCollider2D>();
            Vector2 colliderSize = collider.size;
            Physics2D.SyncTransforms();
            Bounds colliderBounds = collider.bounds;

            Assert.That(display.TryApplyScaleForRoom(TestRoomName), Is.True);
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
            guestState.SetCurrentRoom(TestRoomName);

            GameObject guestFootAnchor = new GameObject("Guest Foot Anchor");
            guestFootAnchor.transform.SetParent(rig.Room.transform, false);
            guestFootAnchor.transform.localPosition = new Vector3(0f, -220f, 0f);
            guestState.BindToRoomStagePoint(guestFootAnchor.transform);

            butler.transform.position = rig.GetWorldPoint(-220f);
            guest.transform.position = rig.GetWorldPoint(-100f);

            Assert.That(butlerDisplay.TryApplyScaleForRoom(TestRoomName), Is.True);
            Assert.That(guestDisplay.TryApplyCurrentRoomScale(), Is.True);
            Vector3 standingScale = guestDisplay.AnimationDisplay.localScale;
            Assert.That(standingScale, Is.EqualTo(butlerDisplay.AnimationDisplay.localScale));

            guestState.SetSeated(true);
            Assert.That(guestDisplay.TryApplyCurrentRoomScale(), Is.True);
            Assert.That(
                guestDisplay.AnimationDisplay.localScale,
                Is.EqualTo(standingScale),
                "Forced sitting changes the Animator pose, never the room scale function.");

            UnityEngine.Object.DestroyImmediate(butler);
            UnityEngine.Object.DestroyImmediate(guest);
        }
    }

    [Test]
    public void CatalogDefinitionsRejectInvalidValuesAndDuplicateRooms()
    {
        CharacterScaleRoomDefinition valid = new CharacterScaleRoomDefinition(TestRoomName, -400f, 2f, -100f, 1f);
        Assert.That(valid.IsConfigured(out _), Is.True);

        AssertInvalidDefinition(
            new CharacterScaleRoomDefinition(TestRoomName, -100f, 2f, -100f, 1f),
            "different Y");
        AssertInvalidDefinition(
            new CharacterScaleRoomDefinition(TestRoomName, float.NaN, 2f, -100f, 1f),
            "finite");
        AssertInvalidDefinition(
            new CharacterScaleRoomDefinition(TestRoomName, -400f, -2f, -100f, 1f),
            "positive");
        AssertInvalidDefinition(
            new CharacterScaleRoomDefinition(TestRoomName, -400f, 2f, -100f, float.PositiveInfinity),
            "finite");

        CharacterScaleCatalog catalog = ScriptableObject.CreateInstance<CharacterScaleCatalog>();

        try
        {
            catalog.SetRooms(new[]
            {
                valid,
                new CharacterScaleRoomDefinition("Test-Room", -500f, 3f, -50f, 0.5f)
            });

            Assert.That(catalog.ValidateCatalog(out string report), Is.False);
            Assert.That(report, Does.Contain("appears more than once"));
            Assert.That(catalog.Rooms[0], Is.Not.SameAs(valid), "The asset must own a defensive value copy.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void RoomStageCoordinatesAndZoomAreCombinedWithoutReadingHandles()
    {
        using (ScaleRig rig = ScaleRig.Create())
        {
            Vector3 authoredPoint = rig.GetWorldPoint(-250f);
            Assert.That(rig.ScaleRoom.TryGetCharacterRoomY(authoredPoint, out float authoredY), Is.True);
            Assert.That(authoredY, Is.EqualTo(-250f).Within(0.0001f));
            Assert.That(rig.TryGetScaleAtRoomY(authoredY, out float authoredScale), Is.True);
            Assert.That(authoredScale, Is.EqualTo(1.5f).Within(0.0001f));

            rig.SetHandleCalibration(-900f, 9f, 400f, 8f);
            Assert.That(rig.ScaleRoom.TryGetCharacterRoomY(authoredPoint, out float afterHandleMoveY), Is.True);
            Assert.That(afterHandleMoveY, Is.EqualTo(authoredY).Within(0.0001f));
            Assert.That(rig.TryGetScaleAtRoomY(afterHandleMoveY, out float afterHandleMoveScale), Is.True);
            Assert.That(afterHandleMoveScale, Is.EqualTo(authoredScale).Within(0.0001f));

            rig.Room.transform.localScale = Vector3.one * 2f;
            Vector3 zoomedPoint = rig.GetWorldPoint(-250f);
            Assert.That(rig.ScaleRoom.TryGetCharacterRoomY(zoomedPoint, out float zoomedY), Is.True);
            Assert.That(zoomedY, Is.EqualTo(-250f).Within(0.0001f));
            Assert.That(rig.TryGetScaleAtRoomY(zoomedY, out float zoomedScale), Is.True);
            Assert.That(zoomedScale, Is.EqualTo(3f).Within(0.0001f));
        }
    }

    [Test]
    public void GameplayHasOneValidCatalogRecordAndRoomMappingForEveryAuthoritativeRoom()
    {
        CharacterScaleCatalog catalog = LoadRealCatalog();
        Scene previousScene = SceneManager.GetActiveScene();
        Scene scene = OpenGameplayScene(out bool openedHere);

        try
        {
            RoomContentGroup[] roomGroups = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomContentGroup>(true))
                .GroupBy(room => CharacterScaleCatalog.NormalizeRoomName(room.RoomName), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(room => room.RoomName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            CharacterScaleRoom[] roomMappings = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CharacterScaleRoom>(true))
                .ToArray();

            Assert.That(AssetDatabase.GetAssetPath(catalog), Is.EqualTo(CharacterScaleTool.DefaultCatalogAssetPath));
            Assert.That(roomGroups, Has.Length.EqualTo(19));
            Assert.That(roomMappings, Has.Length.EqualTo(19));
            Assert.That(catalog.Rooms.Count, Is.EqualTo(19));
            Assert.That(catalog.ValidateCatalog(out string report), Is.True, report);
            Assert.That(
                catalog.Rooms.Select(room => CharacterScaleCatalog.NormalizeRoomName(room.RoomName)),
                Is.EquivalentTo(roomGroups.Select(room => CharacterScaleCatalog.NormalizeRoomName(room.RoomName))));

            foreach (RoomContentGroup room in roomGroups)
            {
                CharacterScaleRoom roomMapping = room.GetComponent<CharacterScaleRoom>();
                Assert.That(roomMapping, Is.Not.Null, $"{room.RoomName} runtime coordinate mapping");
                Assert.That(roomMapping.Room, Is.EqualTo(room), room.RoomName);
                Assert.That(roomMapping.AreHandlesConfigured(out string handleReason), Is.True, handleReason);
                Assert.That(roomMapping.FrontHandle.name, Is.EqualTo("Front"), room.RoomName);
                Assert.That(roomMapping.BackHandle.name, Is.EqualTo("Back"), room.RoomName);
                Assert.That(roomMapping.FrontHandle.parent, Is.EqualTo(roomMapping.BackHandle.parent), room.RoomName);
                Assert.That(roomMapping.FrontHandle.parent.name, Is.EqualTo("Character Scale"), room.RoomName);
                Assert.That(
                    roomMapping.FrontHandle.parent.CompareTag("EditorOnly"),
                    Is.True,
                    $"{room.RoomName} calibration handles must be stripped from player builds.");

                Assert.That(
                    catalog.TryGetRoom(room.RoomName, out CharacterScaleRoomDefinition definition),
                    Is.True,
                    room.RoomName);
                Assert.That(definition.IsConfigured(out string definitionReason), Is.True, definitionReason);

                Vector2 frontHandlePosition = roomMapping.GetHandleRoomLocalPosition(roomMapping.FrontHandle);
                Vector2 backHandlePosition = roomMapping.GetHandleRoomLocalPosition(roomMapping.BackHandle);
                Assert.That(
                    definition.FrontY,
                    Is.EqualTo(frontHandlePosition.y).Within(0.0001f),
                    $"{room.RoomName} seeded Front Y must match the migration source handle.");
                Assert.That(
                    definition.FrontScale,
                    Is.EqualTo(roomMapping.GetHandleUniformScale(roomMapping.FrontHandle)).Within(0.0001f),
                    $"{room.RoomName} seeded Front scale must match the migration source handle.");
                Assert.That(
                    definition.BackY,
                    Is.EqualTo(backHandlePosition.y).Within(0.0001f),
                    $"{room.RoomName} seeded Back Y must match the migration source handle.");
                Assert.That(
                    definition.BackScale,
                    Is.EqualTo(roomMapping.GetHandleUniformScale(roomMapping.BackHandle)).Within(0.0001f),
                    $"{room.RoomName} seeded Back scale must match the migration source handle.");

                Assert.That(
                    catalog.TryEvaluateScaleAtRoomY(
                        room.RoomName,
                        definition.FrontY,
                        roomMapping.CurrentStageScale,
                        out float frontScale),
                    Is.True,
                    room.RoomName);
                Assert.That(
                    frontScale,
                    Is.EqualTo(definition.FrontScale * roomMapping.CurrentStageScale).Within(0.0001f),
                    room.RoomName);
            }
        }
        finally
        {
            CloseGameplayScene(scene, openedHere, previousScene);
        }
    }

    [Test]
    public void GameplayActorsKeepUnitRootsDedicatedDisplaysAndTheCanonicalCatalog()
    {
        CharacterScaleCatalog catalog = LoadRealCatalog();
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
                Is.EquivalentTo(new[]
                {
                    "Player", "Guest 1", "Guest 2", "Guest 3", "Guest 4",
                    "Guest 5", "Guest 6", "Guest 7", "Guest 8"
                }));

            foreach (CharacterAnimationDisplay display in displays)
            {
                Assert.That(display.Catalog, Is.SameAs(catalog), $"{display.name} catalog");
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
    public void ScreenSpaceRoomMappingMatchesDirectCatalogYEvaluation()
    {
        CharacterScaleCatalog catalog = LoadRealCatalog();
        Scene previousScene = SceneManager.GetActiveScene();
        Scene scene = OpenGameplayScene(out bool openedHere);
        RenderTexture renderTexture = null;
        Camera worldCamera = null;
        RenderTexture previousTarget = null;

        try
        {
            CharacterScaleRoom roomMapping = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CharacterScaleRoom>(true))
                .First(room =>
                    room.Room != null &&
                    catalog.TryGetRoom(room.RoomName, out CharacterScaleRoomDefinition definition) &&
                    definition.IsConfigured(out _));
            Assert.That(catalog.TryGetRoom(roomMapping.RoomName, out CharacterScaleRoomDefinition saved), Is.True);

            worldCamera = Camera.main;
            Assert.That(worldCamera, Is.Not.Null, "Character scale world-to-room mapping requires Main Camera.");
            previousTarget = worldCamera.targetTexture;
            renderTexture = new RenderTexture(1366, 768, 24);
            renderTexture.Create();
            worldCamera.targetTexture = renderTexture;
            Canvas.ForceUpdateCanvases();

            float roomY = Mathf.Lerp(saved.FrontY, saved.BackY, 0.37f);
            Vector3 roomSurfaceWorldPoint = roomMapping.Room.transform.TransformPoint(new Vector3(0f, roomY, 0f));
            Canvas canvas = roomMapping.Room.GetComponentInParent<Canvas>();
            Camera canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, roomSurfaceWorldPoint);
            float depth = Mathf.Abs(worldCamera.transform.position.z);
            Vector3 detachedActorWorldPoint = worldCamera.ScreenToWorldPoint(
                new Vector3(screenPoint.x, screenPoint.y, depth));

            Assert.That(
                roomMapping.TryGetCharacterRoomY(detachedActorWorldPoint, out float mappedRoomY),
                Is.True);
            Assert.That(mappedRoomY, Is.EqualTo(roomY).Within(0.001f));
            Assert.That(
                catalog.TryEvaluateScaleAtRoomY(
                    roomMapping.RoomName,
                    roomY,
                    roomMapping.CurrentStageScale,
                    out float directScale),
                Is.True);
            Assert.That(
                catalog.TryEvaluateScaleAtRoomY(
                    roomMapping.RoomName,
                    mappedRoomY,
                    roomMapping.CurrentStageScale,
                    out float mappedScale),
                Is.True);
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
    public void PlayerPrefabSeparatesAnimationDisplayAndReferencesCanonicalCatalogAsset()
    {
        CharacterScaleCatalog catalog = LoadRealCatalog();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(prefab.GetComponent<SpriteRenderer>(), Is.Null);
        Assert.That(prefab.GetComponent<Animator>(), Is.Null);

        CharacterAnimationDisplay display = prefab.GetComponent<CharacterAnimationDisplay>();
        Assert.That(display, Is.Not.Null);
        Assert.That(display.Catalog, Is.SameAs(catalog));
        Assert.That(AssetDatabase.GetAssetPath(display.Catalog), Is.EqualTo(CharacterScaleTool.DefaultCatalogAssetPath));
        Assert.That(display.HasValidDisplayRoot(), Is.True);
        Assert.That(display.AnimationDisplay.name, Is.EqualTo("AnimationDisplay"));
        Assert.That(display.AnimationDisplay.GetComponent<SpriteRenderer>(), Is.Not.Null);
        Assert.That(display.AnimationDisplay.GetComponent<Animator>(), Is.Not.Null);
    }

    private static void AssertInvalidDefinition(CharacterScaleRoomDefinition definition, string expectedReason)
    {
        Assert.That(definition.IsConfigured(out string reason), Is.False);
        Assert.That(reason, Does.Contain(expectedReason).IgnoreCase);
    }

    private static CharacterScaleCatalog LoadRealCatalog()
    {
        CharacterScaleCatalog catalog = AssetDatabase.LoadAssetAtPath<CharacterScaleCatalog>(
            CharacterScaleTool.DefaultCatalogAssetPath);
        Assert.That(
            catalog,
            Is.Not.Null,
            $"Missing canonical catalog asset at {CharacterScaleTool.DefaultCatalogAssetPath}.");
        Assert.That(CharacterScaleCatalog.LoadDefault(), Is.SameAs(catalog));
        return catalog;
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
            rig.Catalog = ScriptableObject.CreateInstance<CharacterScaleCatalog>();
            rig.Catalog.name = "CharacterScaleArchitectureTestCatalog";
            rig.Catalog.SetRooms(new[]
            {
                new CharacterScaleRoomDefinition(TestRoomName, -400f, 2f, -100f, 1f)
            });

            rig.Root = new GameObject("CharacterScaleTestRoot");
            GameObject roomObject = new GameObject(
                "Room_Test_Room",
                typeof(RectTransform),
                typeof(RoomContentGroup),
                typeof(CharacterScaleRoom));
            roomObject.transform.SetParent(rig.Root.transform, false);
            rig.Room = roomObject.GetComponent<RoomContentGroup>();
            rig.ScaleRoom = roomObject.GetComponent<CharacterScaleRoom>();

            GameObject markerRoot = new GameObject("Character Scale");
            markerRoot.transform.SetParent(roomObject.transform, false);
            markerRoot.tag = "EditorOnly";
            GameObject frontObject = new GameObject("Front");
            frontObject.transform.SetParent(markerRoot.transform, false);
            GameObject backObject = new GameObject("Back");
            backObject.transform.SetParent(markerRoot.transform, false);
            rig.Front = frontObject.transform;
            rig.Back = backObject.transform;
            rig.SetHandleCalibration(-400f, 2f, -100f, 1f);
            rig.ScaleRoom.ConfigureHandles(rig.Room, rig.Front, rig.Back);
            return rig;
        }

        public Vector3 GetWorldPoint(float roomY, float roomX = 0f)
        {
            return Room.transform.TransformPoint(new Vector3(roomX, roomY, 0f));
        }

        public bool TryGetScaleAtRoomY(float roomY, out float scale)
        {
            return Catalog.TryEvaluateScaleAtRoomY(
                TestRoomName,
                roomY,
                ScaleRoom.CurrentStageScale,
                out scale);
        }

        public void SetHandleCalibration(float frontY, float frontScale, float backY, float backScale)
        {
            Front.localPosition = new Vector3(Front.localPosition.x, frontY, 0f);
            Front.localScale = new Vector3(frontScale, frontScale, 1f);
            Back.localPosition = new Vector3(Back.localPosition.x, backY, 0f);
            Back.localScale = new Vector3(backScale, backScale, 1f);
        }

        public void Dispose()
        {
            if (Root != null)
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }

            if (Catalog != null)
            {
                UnityEngine.Object.DestroyImmediate(Catalog);
            }
        }
    }
}
