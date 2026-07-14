using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ObjectCollisionBoxRegressionTests
{
    private const string PlanPath = "docs/object-collision-box-plan-and-prompt.md";
    private const string MarkerPath = "Assets/Scripts/Navigation/ObjectMovementBlocker2D.cs";
    private const string AuthoringPath = "Assets/Editor/ObjectCollisionBoxAuthoringWindow.cs";
    private const string PointClickPlayerMovementPath = "Assets/Scripts/PointClickPlayerMovement.cs";
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string DrawingRoomChairName = "drawing_room_red_chair_guest6";
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
    public void DrawingRoomPurpleArmchairUsesLowerFootprintWithoutTakingOverProjectedSorting()
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
            Assert.That(chairRenderer.sortingOrder, Is.EqualTo(800), "The seated guest's authored chair layering should remain unchanged.");

            SerializedObject serializedProjection = new SerializedObject(projectedEntity);
            Assert.That(serializedProjection.FindProperty("applySorting").boolValue, Is.True);
            Assert.That(serializedProjection.FindProperty("sortingOffset").intValue, Is.EqualTo(-5776));

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
            Assert.That(marker.SortSourceRenderers, Is.False,
                "RoomProjectedEntity must remain the chair's only sorting owner so the seated guest layering is not disturbed.");

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
                Assert.That(chairRenderer.sortingOrder, Is.EqualTo(800),
                    "Activating the movement blocker must not take over the chair's seated-guest sorting.");
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
}
