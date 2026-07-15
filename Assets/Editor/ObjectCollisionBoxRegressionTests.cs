using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class ObjectCollisionBoxRegressionTests
{
    private const string PlanPath = "docs/object-collision-box-plan-and-prompt.md";
    private const string MarkerPath = "Assets/Scripts/Navigation/ObjectMovementBlocker2D.cs";
    private const string AuthoringPath = "Assets/Editor/ObjectCollisionBoxAuthoringWindow.cs";
    private const string PointClickPlayerMovementPath = "Assets/Scripts/PointClickPlayerMovement.cs";
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string DrawingRoomChairName = "drawing_room_red_chair_guest6";
    private const string DrawingRoomTeaTableName = "tea_service_table";
    private const string DrawingRoomChairSpritePath = "Assets/Art/Objects/purple_armchair_front.png";

    [Test]
    public void ChairBlockerUsesLowerFootprintInsteadOfWholeImage()
    {
        Bounds visualBounds = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(2f, 4f, 0f));

        Assert.That(
            ObjectCollisionBoxAuthoring.TryCreateFootprintFromBounds("Dining Chair", visualBounds, out ObjectCollisionFootprint footprint),
            Is.True);

        Assert.That(footprint.Category, Is.EqualTo(ObjectCollisionBoxCategory.Chair));
        Assert.That(footprint.WorldRect.yMin, Is.EqualTo(-1f).Within(0.001f), "A chair blocks at the floor-contact bottom, not the backrest.");
        Assert.That(footprint.WorldRect.yMax, Is.LessThan(-1f + visualBounds.size.y * 0.36f), "Chair backrests should remain walk-behind/occlusion territory.");
        Assert.That(footprint.WorldRect.height, Is.GreaterThan(0.05f));
        Assert.That(footprint.WorldRect.width, Is.LessThan(visualBounds.size.x), "The blocker should not cover the entire painted chair width.");
    }

    [Test]
    public void WallDecorAndLightingAreSkipped()
    {
        Bounds visualBounds = new Bounds(Vector3.zero, new Vector3(3f, 2f, 0f));

        Assert.That(ObjectCollisionBoxAuthoring.TryCreateFootprintFromBounds("Portrait Wall Frame", visualBounds, out _), Is.False);
        Assert.That(ObjectCollisionBoxAuthoring.TryCreateFootprintFromBounds("Window Curtain Overlay", visualBounds, out _), Is.False);
        Assert.That(ObjectCollisionBoxAuthoring.TryCreateFootprintFromBounds("RoomLight_ChandelierGlow", visualBounds, out _), Is.False);
    }

    [Test]
    public void GeneratorCreatesNamedPlayerBlockerUnderRoom()
    {
        GameObject roomObject = null;
        GameObject sourceObject = null;

        try
        {
            roomObject = new GameObject("Room_Test Dining");
            RoomContentGroup room = roomObject.AddComponent<RoomContentGroup>();
            room.SetRoomName("Test Dining");

            sourceObject = new GameObject("Dining Chair");
            sourceObject.transform.SetParent(roomObject.transform, false);
            sourceObject.transform.position = new Vector3(3f, 2f, 0f);
            sourceObject.AddComponent<SpriteRenderer>();

            ObjectCollisionFootprint footprint = new ObjectCollisionFootprint(
                ObjectCollisionBoxCategory.Chair,
                new Rect(2.5f, 1.2f, 1f, 0.4f),
                0.3f,
                "chair lower legs/seat footprint");

            PolygonCollider2D collider = ObjectCollisionBoxAuthoring.CreateOrUpdatePlayerBlocker(room, sourceObject, footprint);

            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.name, Does.StartWith("PlayerBlocker_Dining_Chair"));
            Assert.That(collider.isTrigger, Is.True, "Movement blockers are point-click no-walk holes, not physics collision bodies.");
            Assert.That(collider.transform.parent, Is.EqualTo(roomObject.transform));
            Assert.That(collider.GetComponent<ObjectMovementBlocker2D>(), Is.Not.Null);

            ObjectMovementBlocker2D marker = collider.GetComponent<ObjectMovementBlocker2D>();
            Assert.That(marker.SortSourceRenderers, Is.True, "Generated blockers should sort their source prop against the same physical footprint.");
        }
        finally
        {
            if (sourceObject != null)
            {
                Object.DestroyImmediate(sourceObject);
            }

            if (roomObject != null)
            {
                Object.DestroyImmediate(roomObject);
            }
        }
    }

    [Test]
    public void ObjectMovementBlockerSortsSourceRendererFromFrontEdge()
    {
        GameObject sourceObject = null;
        GameObject blockerObject = null;

        try
        {
            sourceObject = new GameObject("Dining Chair");
            sourceObject.transform.position = new Vector3(0f, 10f, 0f);
            SpriteRenderer renderer = sourceObject.AddComponent<SpriteRenderer>();

            blockerObject = new GameObject("PlayerBlocker_Dining_Chair");
            blockerObject.transform.position = Vector3.zero;
            BoxCollider2D collider = blockerObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1f, 0.4f);
            collider.offset = new Vector2(0f, 1.4f);

            ObjectMovementBlocker2D marker = blockerObject.AddComponent<ObjectMovementBlocker2D>();
            marker.Configure(sourceObject, "Test Room", "Chair", 0.3f, "test blocker", true);

            Physics2D.SyncTransforms();
            marker.ApplySourceSortingNow();

            int expectedOrderFromBlockerFrontEdge = 1000 - Mathf.RoundToInt(collider.bounds.min.y * 100f);
            int wrongOrderFromSourcePivot = 1000 - Mathf.RoundToInt(sourceObject.transform.position.y * 100f);

            Assert.That(renderer.sortingOrder, Is.EqualTo(expectedOrderFromBlockerFrontEdge));
            Assert.That(renderer.sortingOrder, Is.Not.EqualTo(wrongOrderFromSourcePivot), "Prop sorting should follow its physical blocker, not its visual pivot.");
        }
        finally
        {
            if (blockerObject != null)
            {
                Object.DestroyImmediate(blockerObject);
            }

            if (sourceObject != null)
            {
                Object.DestroyImmediate(sourceObject);
            }
        }
    }

    [Test]
    public void ActorYSorterTracksVisibleFeetAndPreservesRendererOffsets()
    {
        GameObject sortingSourceObject = null;
        GameObject actorObject = null;
        Texture2D texture = null;
        Sprite sprite = null;
        RoomPerspectiveProfile projectionProfile = null;

        try
        {
            sortingSourceObject = new GameObject("ButlerSortingSource");
            PointClickPlayerMovement sortingSource = sortingSourceObject.AddComponent<PointClickPlayerMovement>();

            actorObject = new GameObject("Guest");
            SpriteRenderer bodyRenderer = actorObject.AddComponent<SpriteRenderer>();
            GameObject coatObject = new GameObject("GuestCoat");
            coatObject.transform.SetParent(actorObject.transform, false);
            SpriteRenderer coatRenderer = coatObject.AddComponent<SpriteRenderer>();
            texture = new Texture2D(8, 8);
            sprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 8f);
            bodyRenderer.sprite = sprite;
            coatRenderer.sprite = sprite;

            RoomProjectedEntity projection = actorObject.AddComponent<RoomProjectedEntity>();
            projectionProfile = ScriptableObject.CreateInstance<RoomPerspectiveProfile>();
            projectionProfile.ConfigureDrawingRoomDefaults();
            SerializedObject serializedProjection = new SerializedObject(projection);
            serializedProjection.FindProperty("roomProfile").objectReferenceValue = projectionProfile;
            serializedProjection.FindProperty("applyPosition").boolValue = false;
            serializedProjection.FindProperty("applyScale").boolValue = false;
            serializedProjection.FindProperty("applyTint").boolValue = false;
            serializedProjection.FindProperty("applySorting").boolValue = true;
            serializedProjection.FindProperty("requireActorRoomMatch").boolValue = false;
            serializedProjection.ApplyModifiedPropertiesWithoutUndo();
            projection.RefreshVisualTargets();

            WorldYSortSpriteRenderer sorter = actorObject.AddComponent<WorldYSortSpriteRenderer>();
            bodyRenderer.sortingOrder = 120;
            coatRenderer.sortingOrder = 123;
            sorter.ConfigureForActor(sortingSource, bodyRenderer);

            Assert.That(projection.OwnsProjectedSorting, Is.True, "The characterization must exercise a genuinely active legacy projection writer.");
            int firstExpectedOrder = sortingSource.GetSortingOrderForFootY(bodyRenderer.bounds.min.y);
            Assert.That(bodyRenderer.sortingOrder, Is.EqualTo(firstExpectedOrder));
            Assert.That(coatRenderer.sortingOrder, Is.EqualTo(firstExpectedOrder + 3), "Coat/body layering should survive continuous depth updates.");

            actorObject.transform.position = new Vector3(0f, 2f, 0f);
            projection.ApplyProjection();
            sorter.ApplySorting();

            int movedExpectedOrder = sortingSource.GetSortingOrderForFootY(bodyRenderer.bounds.min.y);
            Assert.That(bodyRenderer.sortingOrder, Is.EqualTo(movedExpectedOrder));
            Assert.That(bodyRenderer.sortingOrder, Is.Not.EqualTo(firstExpectedOrder));
            Assert.That(coatRenderer.sortingOrder, Is.EqualTo(movedExpectedOrder + 3));
        }
        finally
        {
            if (actorObject != null)
            {
                Object.DestroyImmediate(actorObject);
            }

            if (sortingSourceObject != null)
            {
                Object.DestroyImmediate(sortingSourceObject);
            }

            if (sprite != null)
            {
                Object.DestroyImmediate(sprite);
            }

            if (texture != null)
            {
                Object.DestroyImmediate(texture);
            }

            if (projectionProfile != null)
            {
                Object.DestroyImmediate(projectionProfile);
            }
        }
    }

    [Test]
    public void SeatedGuestOverrideIsAboveChairAndBelowTableThenRestoresYSorting()
    {
        GameObject roomObject = null;
        GameObject actorObject = null;
        GameObject chairObject = null;
        GameObject tableObject = null;

        try
        {
            roomObject = new GameObject("Room_Drawing_Room");
            RoomContentGroup room = roomObject.AddComponent<RoomContentGroup>();
            room.SetRoomName("Drawing Room");
            GameObject seatObject = new GameObject("DrawingRoomGuestPoint_01");
            seatObject.transform.SetParent(roomObject.transform, false);
            RoomAnchor seat = seatObject.AddComponent<RoomAnchor>();
            seat.RefreshFromHierarchy();

            actorObject = new GameObject("Guest1");
            actorObject.AddComponent<SpriteRenderer>();
            ActorRoomState actorState = actorObject.AddComponent<ActorRoomState>();
            SerializedObject serializedActor = new SerializedObject(actorState);
            serializedActor.FindProperty("restrictVisibilityToCurrentRoom").boolValue = false;
            serializedActor.ApplyModifiedPropertiesWithoutUndo();
            actorState.SetCurrentRoom("Drawing Room");
            actorState.SetAvailableInCurrentChapter(true);
            actorState.SetVisibleByChapterState(true);
            actorState.SetSeated(true);

            chairObject = new GameObject("purple_sofa");
            SpriteRenderer chairRenderer = chairObject.AddComponent<SpriteRenderer>();
            chairRenderer.sortingLayerName = "People";
            chairRenderer.sortingOrder = 1200;
            tableObject = new GameObject("tea_service_table");
            SpriteRenderer tableRenderer = tableObject.AddComponent<SpriteRenderer>();
            tableRenderer.sortingLayerName = "People";
            tableRenderer.sortingOrder = 1800;

            DiningRoomSeatedGuestOcclusionException seatedException =
                actorObject.AddComponent<DiningRoomSeatedGuestOcclusionException>();
            seatedException.ActivateForSeat(
                actorState,
                seat,
                chairObject,
                chairRenderer,
                tableRenderer,
                "Drawing Room",
                "Butler");

            SortingGroup group = actorObject.GetComponent<SortingGroup>();
            Assert.That(seatedException.IsExceptionActive, Is.True);
            Assert.That(group, Is.Not.Null);
            Assert.That(group.enabled, Is.True);
            Assert.That(group.sortingOrder, Is.GreaterThan(chairRenderer.sortingOrder));
            Assert.That(group.sortingOrder, Is.LessThan(tableRenderer.sortingOrder));

            actorState.SetSeated(false);
            seatedException.ActivateForSeat(
                actorState,
                seat,
                chairObject,
                chairRenderer,
                tableRenderer,
                "Drawing Room",
                "Butler");

            Assert.That(seatedException.IsExceptionActive, Is.False);
            Assert.That(group.enabled, Is.False, "Standing again should return external depth ownership to ordinary Y sorting.");
        }
        finally
        {
            if (tableObject != null)
            {
                Object.DestroyImmediate(tableObject);
            }

            if (chairObject != null)
            {
                Object.DestroyImmediate(chairObject);
            }

            if (actorObject != null)
            {
                Object.DestroyImmediate(actorObject);
            }

            if (roomObject != null)
            {
                Object.DestroyImmediate(roomObject);
            }
        }
    }

    [Test]
    public void DrawingRoomPurpleArmchairUsesLowerFootprintForSharedButlerYSort()
    {
        SceneSetup[] previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            Transform room = FindTransformInScene(scene, "Room_Drawing_Room");
            Transform chair = FindDescendant(room, DrawingRoomChairName);

            Assert.That(room, Is.Not.Null, "The authored Drawing Room should exist in Gameplay.unity.");
            Assert.That(chair, Is.Not.Null, "The purple front armchair should remain authored under the Drawing Room.");

            SpriteRenderer chairRenderer = chair.GetComponent<SpriteRenderer>();
            RoomProjectedEntity projectedEntity = chair.GetComponent<RoomProjectedEntity>();

            Assert.That(chairRenderer, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(chairRenderer.sprite), Is.EqualTo(DrawingRoomChairSpritePath));
            Assert.That(projectedEntity, Is.Not.Null, "The chair must keep the shared Drawing Room y-axis occlusion component.");
            Assert.That(projectedEntity.Mode, Is.EqualTo(RoomProjectedEntity.ProjectionMode.ForegroundOccluder));
            Assert.That(projectedEntity.RoomLocalFootPoint, Is.EqualTo(new Vector2(59f, -208.5f)));

            SerializedObject serializedProjection = new SerializedObject(projectedEntity);
            Assert.That(serializedProjection.FindProperty("applySorting").boolValue, Is.False,
                "The chair must not fight the shared Butler/furniture y-axis sorter with a second sorting writer.");
            Assert.That(serializedProjection.FindProperty("sortingOffset").intValue, Is.Zero,
                "The legacy seated-guest offset must not bypass the shared y-axis sorter.");

            Transform blockerTransform = FindDescendant(room, $"PlayerBlocker_{DrawingRoomChairName}");
            Assert.That(blockerTransform, Is.Not.Null, "The purple front armchair needs a separate movement footprint.");

            ObjectMovementBlocker2D marker = blockerTransform.GetComponent<ObjectMovementBlocker2D>();
            PolygonCollider2D blocker = blockerTransform.GetComponent<PolygonCollider2D>();

            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.SourceObject, Is.SameAs(chair.gameObject));
            Assert.That(marker.SourceRoomName, Is.EqualTo("Drawing Room"));
            Assert.That(marker.Category, Is.EqualTo(ObjectCollisionBoxCategory.Chair.ToString()));
            Assert.That(marker.FootprintHeightFraction, Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(marker.GeneratedByCollisionBoxTool, Is.False,
                "This explicitly authored exception must survive generated-blocker cleanup because the broad scanner skips projected guest-named props.");
            Assert.That(marker.SortSourceRenderers, Is.True,
                "The chair must use the same lower-footprint y-axis sorter as the Butler and other furniture.");

            Assert.That(blocker, Is.Not.Null);
            Assert.That(blocker.enabled, Is.True);
            Assert.That(blocker.isTrigger, Is.True);
            Assert.That(blocker.offset, Is.EqualTo(Vector2.zero));
            Assert.That(blocker.pathCount, Is.EqualTo(1));
            Assert.That(blocker.GetPath(0), Has.Length.EqualTo(4));

            Assert.That(
                ObjectCollisionBoxAuthoring.TryCreateFootprintFromBounds("Purple Armchair", chairRenderer.bounds, out ObjectCollisionFootprint expectedFootprint),
                Is.True);

            Vector2[] points = blocker.GetPath(0);
            Vector2 actualMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 actualMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
            {
                Vector3 worldPoint = blocker.transform.TransformPoint(points[pointIndex]);
                actualMin = Vector2.Min(actualMin, worldPoint);
                actualMax = Vector2.Max(actualMax, worldPoint);
            }

            Rect expected = expectedFootprint.WorldRect;
            const float BoundsTolerance = 0.05f;

            Assert.That(actualMin.x, Is.EqualTo(expected.xMin).Within(BoundsTolerance));
            Assert.That(actualMax.x, Is.EqualTo(expected.xMax).Within(BoundsTolerance));
            Assert.That(actualMin.y, Is.EqualTo(expected.yMin).Within(BoundsTolerance));
            Assert.That(actualMax.y, Is.EqualTo(expected.yMax).Within(BoundsTolerance));
            Assert.That(actualMax.y - actualMin.y, Is.LessThan(chairRenderer.bounds.size.y * 0.31f),
                "The collider should cover only the chair's lower legs/seat footprint, not the seated woman or chair back.");

            bool roomWasActive = room.gameObject.activeSelf;

            try
            {
                room.gameObject.SetActive(true);
                Physics2D.SyncTransforms();

                Assert.That(blocker.gameObject.activeInHierarchy, Is.True,
                    "The chair blocker should become active with the current Drawing Room.");
                Assert.That(blocker.OverlapPoint(expected.center), Is.True,
                    "The active Physics2D trigger should cover the authored chair footprint.");

                marker.ApplySourceSortingNow();
                int expectedChairOrder = 1000 - Mathf.RoundToInt(blocker.bounds.min.y * 100f);
                int butlerOrderJustBehindChair = 1000 - Mathf.RoundToInt((blocker.bounds.min.y + 0.1f) * 100f);
                int butlerOrderJustInFrontOfChair = 1000 - Mathf.RoundToInt((blocker.bounds.min.y - 0.1f) * 100f);

                Assert.That(chairRenderer.sortingOrder, Is.EqualTo(expectedChairOrder),
                    "The chair must use the exact visible-feet y-axis order that the Butler uses when walking behind or in front of it.");
                Assert.That(chairRenderer.sortingOrder, Is.EqualTo(marker.CurrentSortingOrder));
                Assert.That(butlerOrderJustBehindChair, Is.LessThan(chairRenderer.sortingOrder),
                    "A Butler whose visible feet are above the chair's lower edge must render behind the chair.");
                Assert.That(butlerOrderJustInFrontOfChair, Is.GreaterThan(chairRenderer.sortingOrder),
                    "A Butler whose visible feet are below the chair's lower edge must render in front of the chair.");
            }
            finally
            {
                room.gameObject.SetActive(roomWasActive);
            }
        }
        finally
        {
            if (previousSceneSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSceneSetup);
            }
        }
    }

    [Test]
    public void DrawingRoomTeaTableKeepsButlerOcclusionAcrossVerticalCameraPan()
    {
        SceneSetup[] previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            Transform room = FindTransformInScene(scene, "Room_Drawing_Room");
            Transform table = FindDescendant(room, DrawingRoomTeaTableName);
            Transform blockerTransform = FindDescendant(room, $"PlayerBlocker_{DrawingRoomTeaTableName}");

            Assert.That(room, Is.Not.Null, "The authored Drawing Room should exist in Gameplay.unity.");
            Assert.That(table, Is.Not.Null, "The Drawing Room tea table should remain authored in Gameplay.unity.");
            Assert.That(blockerTransform, Is.Not.Null, "The Drawing Room tea table needs its physical movement footprint.");

            SpriteRenderer tableRenderer = table.GetComponent<SpriteRenderer>();
            RoomProjectedEntity projectedEntity = table.GetComponent<RoomProjectedEntity>();
            ObjectMovementBlocker2D marker = blockerTransform.GetComponent<ObjectMovementBlocker2D>();
            PolygonCollider2D blocker = blockerTransform.GetComponent<PolygonCollider2D>();
            RoomContentGroup roomContent = room.GetComponent<RoomContentGroup>();
            CameraManager cameraManager = FindComponentInScene<CameraManager>(scene);
            PointClickPlayerMovement playerMovement = FindComponentInScene<PointClickPlayerMovement>(scene);

            Assert.That(tableRenderer, Is.Not.Null);
            Assert.That(projectedEntity, Is.Not.Null);
            Assert.That(projectedEntity.Mode, Is.EqualTo(RoomProjectedEntity.ProjectionMode.ForegroundOccluder));
            Assert.That(projectedEntity.RoomLocalFootPoint, Is.EqualTo(new Vector2(-80.26f, -211.67f)));
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.SourceObject, Is.SameAs(table.gameObject));
            Assert.That(marker.SortSourceRenderers, Is.True,
                "The table's lower physical footprint must be its sole Butler-compatible sorting owner.");
            Assert.That(blocker, Is.Not.Null);
            Assert.That(roomContent, Is.Not.Null);
            Assert.That(cameraManager, Is.Not.Null);
            Assert.That(playerMovement, Is.Not.Null);

            SerializedObject serializedProjection = new SerializedObject(projectedEntity);
            Assert.That(serializedProjection.FindProperty("applySorting").boolValue, Is.False,
                "RoomProjectedEntity must not race the table's physical-footprint sorter every LateUpdate.");

            bool roomWasActive = room.gameObject.activeSelf;
            RectTransform roomStage = room as RectTransform;
            Vector2 originalRoomPosition = roomStage.anchoredPosition;
            Vector2 originalRoomSize = roomStage.sizeDelta;
            Vector3 originalRoomScale = roomStage.localScale;
            Vector2 originalRoomAnchorMin = roomStage.anchorMin;
            Vector2 originalRoomAnchorMax = roomStage.anchorMax;
            Vector2 originalRoomPivot = roomStage.pivot;

            try
            {
                room.gameObject.SetActive(true);
                cameraManager.SetActiveRoomContent(roomContent, false);

                float[] verticalPans = { -1f, 0f, 1f };
                int[] tableOrders = new int[verticalPans.Length];

                for (int panIndex = 0; panIndex < verticalPans.Length; panIndex++)
                {
                    cameraManager.SetRoomLookForPreview(0f, verticalPans[panIndex], cameraManager.defaultRoomFov);
                    Canvas.ForceUpdateCanvases();
                    Physics2D.SyncTransforms();
                    marker.ApplySourceSortingNow();

                    float physicalFrontEdgeY = blocker.bounds.min.y;
                    int expectedTableOrder = playerMovement.GetSortingOrderForFootY(physicalFrontEdgeY);
                    int butlerOrderBehindTable = playerMovement.GetSortingOrderForFootY(physicalFrontEdgeY + 0.1f);
                    int butlerOrderInFrontOfTable = playerMovement.GetSortingOrderForFootY(physicalFrontEdgeY - 0.1f);

                    tableOrders[panIndex] = tableRenderer.sortingOrder;
                    Assert.That(tableRenderer.sortingOrder, Is.EqualTo(marker.CurrentSortingOrder));
                    Assert.That(tableRenderer.sortingOrder, Is.EqualTo(expectedTableOrder),
                        $"The table and Butler should share one world-feet sorting formula at vertical pan {verticalPans[panIndex]}.");
                    Assert.That(butlerOrderBehindTable, Is.LessThan(tableRenderer.sortingOrder),
                        $"The Butler should render behind the table above its front edge at vertical pan {verticalPans[panIndex]}.");
                    Assert.That(butlerOrderInFrontOfTable, Is.GreaterThan(tableRenderer.sortingOrder),
                        $"The Butler should render in front of the table below its front edge at vertical pan {verticalPans[panIndex]}.");

                    projectedEntity.ApplyProjection();
                    Assert.That(tableRenderer.sortingOrder, Is.EqualTo(expectedTableOrder),
                        "Applying the retained projection metadata must not overwrite the physical occlusion result.");
                }

                Assert.That(tableOrders[0], Is.Not.EqualTo(tableOrders[2]),
                    "The regression must exercise two genuinely different room-stage positions.");
            }
            finally
            {
                cameraManager.SetActiveRoomContent(null, false);
                roomStage.anchoredPosition = originalRoomPosition;
                roomStage.sizeDelta = originalRoomSize;
                roomStage.localScale = originalRoomScale;
                roomStage.anchorMin = originalRoomAnchorMin;
                roomStage.anchorMax = originalRoomAnchorMax;
                roomStage.pivot = originalRoomPivot;
                room.gameObject.SetActive(roomWasActive);
            }
        }
        finally
        {
            if (previousSceneSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSceneSetup);
            }
        }
    }

    [UnityTest]
    public IEnumerator Chapter2SkipStagesEveryGuestWithCanonicalOcclusion()
    {
        SceneSetup[] previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        Selection.activeObject = null;

        yield return new EnterPlayMode();

        MainMenuController mainMenu = Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
        Assert.That(mainMenu, Is.Not.Null, "The runtime regression must enter Gameplay through the real Main Menu bootstrap.");
        mainMenu.ContinueGame();

        for (int frame = 0; frame < 30 && SceneManager.GetActiveScene().name != "Gameplay"; frame++)
        {
            yield return null;
        }

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Gameplay"));
        yield return null;

        ChapterManager chapterManager = Object.FindAnyObjectByType<ChapterManager>(FindObjectsInactive.Include);
        Assert.That(chapterManager, Is.Not.Null);
        chapterManager.SkipToChapter2ForTesting();

        for (int frame = 0; frame < 5; frame++)
        {
            yield return null;
        }

        RoomNavigationManager navigation = Object.FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
        PointClickPlayerMovement playerMovement = Object.FindAnyObjectByType<PointClickPlayerMovement>(FindObjectsInactive.Include);
        CameraManager cameraManager = Object.FindAnyObjectByType<CameraManager>(FindObjectsInactive.Include);
        Transform room = FindTransformInScene(SceneManager.GetActiveScene(), "Room_Drawing_Room");
        Transform table = FindDescendant(room, DrawingRoomTeaTableName);
        Transform blockerTransform = FindDescendant(room, $"PlayerBlocker_{DrawingRoomTeaTableName}");
        Transform greenChair = FindDescendant(room, "drawingroomgreenchair_0");
        SpriteRenderer tableRenderer = table != null ? table.GetComponent<SpriteRenderer>() : null;
        SpriteRenderer greenChairRenderer = greenChair != null ? greenChair.GetComponent<SpriteRenderer>() : null;
        WorldYSortSpriteRenderer greenChairSorter = greenChair != null
            ? greenChair.GetComponent<WorldYSortSpriteRenderer>()
            : null;
        ObjectMovementBlocker2D tableMarker = blockerTransform != null
            ? blockerTransform.GetComponent<ObjectMovementBlocker2D>()
            : null;
        PolygonCollider2D tableBlocker = blockerTransform != null
            ? blockerTransform.GetComponent<PolygonCollider2D>()
            : null;

        Assert.That(navigation, Is.Not.Null);
        Assert.That(navigation.CurrentRoom, Is.EqualTo("Drawing Room").IgnoreCase);
        Assert.That(playerMovement, Is.Not.Null);
        Assert.That(cameraManager, Is.Not.Null);
        Assert.That(room, Is.Not.Null);
        Assert.That(tableRenderer, Is.Not.Null);
        Assert.That(tableMarker, Is.Not.Null);
        Assert.That(tableBlocker, Is.Not.Null);
        Assert.That(greenChairRenderer, Is.Not.Null);
        Assert.That(greenChairSorter, Is.Not.Null);

        ActorRoomState[] actorStates = Object.FindObjectsByType<ActorRoomState>(FindObjectsInactive.Include);
        List<ActorRoomState> drawingRoomGuests = new List<ActorRoomState>();

        for (int i = 0; i < actorStates.Length; i++)
        {
            ActorRoomState actor = actorStates[i];

            if (actor != null &&
                string.Equals(actor.CurrentRoomId, "Drawing Room", System.StringComparison.OrdinalIgnoreCase) &&
                actor.ActorId.IndexOf("Guest", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                drawingRoomGuests.Add(actor);
            }
        }

        Assert.That(drawingRoomGuests.Count, Is.EqualTo(8), "Skip to Chapter 2 should stage the complete guest roster.");

        float[] verticalPans = { -1f, 0f, 1f };

        for (int panIndex = 0; panIndex < verticalPans.Length; panIndex++)
        {
            cameraManager.SetRoomLookForPreview(0f, verticalPans[panIndex], cameraManager.defaultRoomFov);
            yield return null;
            yield return null;
            Physics2D.SyncTransforms();
            tableMarker.ApplySourceSortingNow();
            greenChairSorter.ApplySorting();
            yield return null;
            yield return null;

            Assert.That(
                greenChairRenderer.sortingOrder,
                Is.EqualTo(playerMovement.GetSortingOrderForFootY(greenChair.position.y)),
                "The full green chair must use the same world-Y order space as every standing actor.");

            int seatedCount = 0;
            int standingCount = 0;
            ActorRoomState standingProbe = null;
            SpriteRenderer standingProbeRenderer = null;
            WorldYSortSpriteRenderer standingProbeSorter = null;

            for (int guestIndex = 0; guestIndex < drawingRoomGuests.Count; guestIndex++)
            {
                ActorRoomState guest = drawingRoomGuests[guestIndex];
                WorldYSortSpriteRenderer sorter = guest.GetComponent<WorldYSortSpriteRenderer>();
                DiningRoomSeatedGuestOcclusionException seatedException =
                    guest.GetComponent<DiningRoomSeatedGuestOcclusionException>();

                Assert.That(sorter, Is.Not.Null, $"{guest.ActorId} should retain the shared actor sorter after the skip.");
                Assert.That(sorter.IsConfiguredForActor, Is.True);
                sorter.ApplySorting();

                if (guest.IsSeated)
                {
                    seatedCount++;
                    Assert.That(seatedException, Is.Not.Null);
                    seatedException.ApplyOcclusionNow();
                    Assert.That(seatedException.IsExceptionActive, Is.True);
                    SortingGroup group = guest.GetComponentInChildren<SortingGroup>(true);
                    SpriteRenderer chairRenderer = seatedException.AssignedChair != null
                        ? seatedException.AssignedChair.GetComponent<SpriteRenderer>()
                        : null;
                    Assert.That(group, Is.Not.Null);
                    Assert.That(chairRenderer, Is.Not.Null);
                    Assert.That(group.sortingOrder, Is.GreaterThan(chairRenderer.sortingOrder));
                    Assert.That(group.sortingOrder, Is.LessThan(tableRenderer.sortingOrder));
                }
                else
                {
                    standingCount++;
                    Assert.That(seatedException == null || !seatedException.IsExceptionActive, Is.True);
                    SpriteRenderer characterRenderer = sorter.ActorFootRenderer != null
                        ? sorter.ActorFootRenderer
                        : FindVisibleCharacterRenderer(guest.gameObject);
                    Assert.That(characterRenderer, Is.Not.Null);
                    standingProbe ??= guest;
                    standingProbeRenderer ??= characterRenderer;
                    standingProbeSorter ??= sorter;
                    Assert.That(
                        characterRenderer.sortingOrder,
                        Is.EqualTo(playerMovement.GetSortingOrderForFootY(characterRenderer.bounds.min.y)),
                        $"{guest.ActorId} should continuously sort from visible feet at pan {verticalPans[panIndex]}.");

                    if (characterRenderer.bounds.min.y > tableBlocker.bounds.min.y)
                    {
                        Assert.That(characterRenderer.sortingOrder, Is.LessThan(tableRenderer.sortingOrder),
                            $"{guest.ActorId} should render behind the tea table when its feet are above the table edge.");
                    }
                }
            }

            Assert.That(seatedCount, Is.EqualTo(5));
            Assert.That(standingCount, Is.EqualTo(3));
            Assert.That(standingProbe, Is.Not.Null);
            Assert.That(standingProbeRenderer, Is.Not.Null);
            Assert.That(standingProbeSorter, Is.Not.Null);

            Vector3 originalProbePosition = standingProbe.transform.position;
            float behindTableFootY = tableBlocker.bounds.min.y + 0.5f;
            standingProbe.transform.position += Vector3.up * (behindTableFootY - standingProbeRenderer.bounds.min.y);
            yield return null;
            yield return null;
            standingProbe.transform.position += Vector3.up * (behindTableFootY - standingProbeRenderer.bounds.min.y);
            standingProbeSorter.ApplySorting();

            Assert.That(
                standingProbeRenderer.sortingOrder,
                Is.EqualTo(playerMovement.GetSortingOrderForFootY(standingProbeRenderer.bounds.min.y)));
            Assert.That(standingProbeRenderer.sortingOrder, Is.LessThan(tableRenderer.sortingOrder),
                $"A real standing guest must render behind the table above its front edge at pan {verticalPans[panIndex]}.");

            float inFrontOfTableFootY = tableBlocker.bounds.min.y - 0.5f;
            standingProbe.transform.position += Vector3.up * (inFrontOfTableFootY - standingProbeRenderer.bounds.min.y);
            yield return null;
            yield return null;
            standingProbe.transform.position += Vector3.up * (inFrontOfTableFootY - standingProbeRenderer.bounds.min.y);
            standingProbeSorter.ApplySorting();

            Assert.That(
                standingProbeRenderer.sortingOrder,
                Is.EqualTo(playerMovement.GetSortingOrderForFootY(standingProbeRenderer.bounds.min.y)));
            Assert.That(standingProbeRenderer.sortingOrder, Is.GreaterThan(tableRenderer.sortingOrder),
                $"A real standing guest must render in front of the table below its front edge at pan {verticalPans[panIndex]}.");

            standingProbe.transform.position = originalProbePosition;
            yield return null;
            yield return null;

            float behindGreenChairFootY = greenChair.position.y + 0.5f;
            standingProbe.transform.position += Vector3.up * (behindGreenChairFootY - standingProbeRenderer.bounds.min.y);
            yield return null;
            yield return null;
            standingProbe.transform.position += Vector3.up * (behindGreenChairFootY - standingProbeRenderer.bounds.min.y);
            standingProbeSorter.ApplySorting();
            Assert.That(standingProbeRenderer.sortingOrder, Is.LessThan(greenChairRenderer.sortingOrder),
                $"A real standing guest must render behind the green chair above its Y edge at pan {verticalPans[panIndex]}.");

            float inFrontOfGreenChairFootY = greenChair.position.y - 0.5f;
            standingProbe.transform.position += Vector3.up * (inFrontOfGreenChairFootY - standingProbeRenderer.bounds.min.y);
            yield return null;
            yield return null;
            standingProbe.transform.position += Vector3.up * (inFrontOfGreenChairFootY - standingProbeRenderer.bounds.min.y);
            standingProbeSorter.ApplySorting();
            Assert.That(standingProbeRenderer.sortingOrder, Is.GreaterThan(greenChairRenderer.sortingOrder),
                $"A real standing guest must render in front of the green chair below its Y edge at pan {verticalPans[panIndex]}.");

            standingProbe.transform.position = originalProbePosition;
            yield return null;
            yield return null;
        }

        yield return new ExitPlayMode();

        if (previousSceneSetup != null && previousSceneSetup.Length > 0)
        {
            EditorSceneManager.RestoreSceneManagerSetup(previousSceneSetup);
        }
    }

    [Test]
    public void PointClickMovementCollectsExplicitObjectMovementBlockers()
    {
        string playerText = File.ReadAllText(PointClickPlayerMovementPath);

        Assert.That(playerText, Does.Contain("ObjectMovementBlocker2D"), "Generated movement blockers should be collected explicitly, not only by fragile name matching.");
        Assert.That(playerText, Does.Contain("IsWalkableWorldPoint"), "Object blockers must feed the same walkability path as existing PlayerBlocker holes.");
    }

    [Test]
    public void ObjectCollisionAuthoringToolAndPromptExist()
    {
        Assert.That(File.Exists(MarkerPath), Is.True);
        Assert.That(File.Exists(AuthoringPath), Is.True);
        Assert.That(File.Exists(PlanPath), Is.True);

        string authoringText = File.ReadAllText(AuthoringPath);
        string planText = File.ReadAllText(PlanPath);

        Assert.That(authoringText, Does.Contain("Dreadforge/Object Collision/Collision Box Authoring"));
        Assert.That(authoringText, Does.Contain("Dreadforge/Object Collision/Sync Gameplay PlayerBlocker Sorting"));
        Assert.That(authoringText, Does.Contain("Generate / Sync PlayerBlockers"));
        Assert.That(authoringText, Does.Contain("Generate Missing PlayerBlockers"));
        Assert.That(authoringText, Does.Contain("Dry Run"));
        Assert.That(planText, Does.Contain("physical blocker equals floor-contact footprint"));
        Assert.That(planText, Does.Contain("Codex implementation prompt"));
    }

    private static Transform FindTransformInScene(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Transform match = FindDescendant(roots[rootIndex].transform, objectName);

            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            T match = roots[rootIndex].GetComponentInChildren<T>(true);

            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < descendants.Length; index++)
        {
            if (descendants[index].name == objectName)
            {
                return descendants[index];
            }
        }

        return null;
    }

    private static SpriteRenderer FindVisibleCharacterRenderer(GameObject actorObject)
    {
        if (actorObject == null)
        {
            return null;
        }

        SpriteRenderer[] renderers = actorObject.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];

            if (renderer != null &&
                renderer.enabled &&
                renderer.sprite != null &&
                renderer.name.IndexOf("coat", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                return renderer;
            }
        }

        return null;
    }
}
