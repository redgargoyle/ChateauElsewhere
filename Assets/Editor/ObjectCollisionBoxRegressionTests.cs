using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ObjectCollisionBoxRegressionTests
{
    private const string PlanPath = "docs/object-collision-box-plan-and-prompt.md";
    private const string MarkerPath = "Assets/Scripts/Navigation/ObjectMovementBlocker2D.cs";
    private const string AuthoringPath = "Assets/Editor/ObjectCollisionBoxAuthoringWindow.cs";
    private const string PointClickPlayerMovementPath = "Assets/Scripts/PointClickPlayerMovement.cs";
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";

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
    public void DiningTableCutoutHasMovementAndSortingBlocker()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(sceneText, Does.Contain("m_Name: correct_dining_table_0"), "The dining room should keep the full table/chairs base sprite from origin/dialog.");
        Assert.That(sceneText, Does.Contain("m_LocalPosition: {x: -10.03, y: -175.58, z: -7166.9155}"), "The full dining table/chairs base sprite should keep the known-good placement from origin/dialog.");
        Assert.That(sceneText, Does.Contain("m_Sprite: {fileID: 5018639196147655082, guid: afdc05b8996bc9af18abbb80afd7a6b8, type: 3}"), "The full dining table/chairs base sprite should use the known-good origin/dialog art.");
        Assert.That(sceneText, Does.Contain("m_Name: DiningTableCutoutOverlay"), "The dining table foreground cutout should remain available for depth sorting.");
        Assert.That(sceneText, Does.Contain("m_LocalPosition: {x: -20.67, y: -129.04, z: -7166.9155}"), "The table foreground cutout should keep the known-good placement from origin/dialog.");
        Assert.That(sceneText, Does.Contain("m_LocalScale: {x: 103.70781, y: 114.65408, z: 79.63239}"), "The table foreground cutout should keep the known-good scale from origin/dialog.");
        Assert.That(sceneText, Does.Contain("m_Name: PlayerBlocker_DiningTableCutoutOverlay"), "The dining table needs an authored no-walk footprint.");
        Assert.That(sceneText, Does.Contain("sourceObject: {fileID: 3800000000}"), "The blocker should sort the dining table cutout overlay, not an unrelated object.");
        Assert.That(sceneText, Does.Contain("sourceObjectName: DiningTableCutoutOverlay"));
        Assert.That(sceneText, Does.Contain("sourceRoomName: Dining Room"));
        Assert.That(sceneText, Does.Contain("category: Table"));
        Assert.That(sceneText, Does.Contain("authoringNote: dining table footprint and foreground cutout depth"));
        Assert.That(sceneText, Does.Contain("m_Father: {fileID: 2300000016}"), "The dining table blocker should live under Room_Dining_Room.");
        Assert.That(sceneText, Does.Contain("m_IsTrigger: 1"), "Movement blockers are walkability holes, not rigid physics bodies.");
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
}
