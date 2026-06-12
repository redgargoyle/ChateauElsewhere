using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class Chapter3DinnerRegressionTests
{
    private const string ChapterManagerPath = "Assets/Scripts/Story/ChapterManager.cs";

    [Test]
    public void ManifestValidationFailsWhenRequiredTableLayersAreMissing()
    {
        Sprite sprite = CreateSprite(Color.white);
        Chapter3LayeredDinnerAssetManifest manifest = CreateValidManifest(sprite);
        manifest.tableBack = null;

        try
        {
            Assert.That(manifest.Validate(out string message), Is.False);
            Assert.That(message, Does.Contain("table_back.png"));
        }
        finally
        {
            DestroyManifestAndSprite(manifest, sprite);
        }
    }

    [Test]
    public void ManifestValidationFailsWhenSeatHasNoIdleOrEatFrames()
    {
        Sprite sprite = CreateSprite(Color.white);
        Chapter3LayeredDinnerAssetManifest manifest = CreateValidManifest(sprite);
        manifest.seats[3].idleFrames = System.Array.Empty<Sprite>();
        manifest.seats[3].eatFrames = new[] { sprite };

        try
        {
            Assert.That(manifest.Validate(out string message), Is.False);
            Assert.That(message, Does.Contain("Seat04"));
            Assert.That(message, Does.Contain("idle"));
            Assert.That(message, Does.Contain("eat"));
        }
        finally
        {
            DestroyManifestAndSprite(manifest, sprite);
        }
    }

    [Test]
    public void BuilderCreatesExactlyEightSeatAnimators()
    {
        Sprite sprite = CreateSprite(Color.white);
        Chapter3LayeredDinnerAssetManifest manifest = CreateValidManifest(sprite);
        GameObject room = new GameObject("Room_Dining Room");
        room.AddComponent<RoomContentGroup>();
        GameObject builderObject = new GameObject("Builder");
        Chapter3LayeredDinnerBuilder builder = builderObject.AddComponent<Chapter3LayeredDinnerBuilder>();
        SetPrivateField(builder, "manifest", manifest);

        try
        {
            Assert.That(builder.BuildOrRefresh(), Is.True);
            Assert.That(builder.SeatAnimators.Count, Is.EqualTo(8));
            Assert.That(builder.DinnerRoot, Is.Not.Null);
        }
        finally
        {
            Object.DestroyImmediate(builderObject);
            Object.DestroyImmediate(room);
            DestroyManifestAndSprite(manifest, sprite);
        }
    }

    [UnityTest]
    public IEnumerator SeatAnimatorChangesSpritesDuringEat()
    {
        Sprite idle = CreateSprite(Color.white);
        Sprite eatA = CreateSprite(Color.red);
        Sprite eatB = CreateSprite(Color.green);
        GameObject seatObject = new GameObject("Seat");
        SpriteRenderer baseRenderer = seatObject.AddComponent<SpriteRenderer>();
        GameObject overlayObject = new GameObject("Overlay");
        overlayObject.transform.SetParent(seatObject.transform, false);
        SpriteRenderer overlayRenderer = overlayObject.AddComponent<SpriteRenderer>();
        Chapter3LayeredSeatAnimator animator = seatObject.AddComponent<Chapter3LayeredSeatAnimator>();
        Chapter3LayeredDinnerAssetManifest.Chapter3SeatLayerSet seatSet =
            new Chapter3LayeredDinnerAssetManifest.Chapter3SeatLayerSet
            {
                seatId = "Seat01",
                idleFrames = new[] { idle },
                eatFrames = new[] { eatA, eatB }
            };

        try
        {
            animator.Configure(0, seatSet, null, baseRenderer, null, overlayRenderer);
            IEnumerator eat = animator.PlayEatOnce();
            Assert.That(eat.MoveNext(), Is.True);
            yield return eat.Current;
            Assert.That(eat.MoveNext(), Is.True);
            Assert.That(baseRenderer.sprite, Is.EqualTo(eatA));
        }
        finally
        {
            Object.DestroyImmediate(seatObject);
            DestroySprite(idle);
            DestroySprite(eatA);
            DestroySprite(eatB);
        }
    }

    [Test]
    public void GuestVisualSuppressorHidesOldGuestComponents()
    {
        GameObject actorObject = new GameObject("GuestActor");
        ActorRoomState actor = actorObject.AddComponent<ActorRoomState>();
        GameObject visualObject = new GameObject("StandingVisual");
        visualObject.transform.SetParent(actorObject.transform, false);
        SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
        Animator animator = visualObject.AddComponent<Animator>();
        BoxCollider2D collider = visualObject.AddComponent<BoxCollider2D>();
        Chapter3GuestVisualSuppressor suppressor = actorObject.AddComponent<Chapter3GuestVisualSuppressor>();

        try
        {
            suppressor.Initialize(actor);
            suppressor.Suppress();
            Assert.That(renderer.enabled, Is.False);
            Assert.That(animator.enabled, Is.False);
            Assert.That(collider.enabled, Is.False);
            Assert.That(actor.enabled, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
        }
    }

    [Test]
    public void FoodStateTogglesLayersWithoutCreatingObjects()
    {
        GameObject root = new GameObject("FoodRoot");
        Chapter3LayeredFoodState foodState = root.AddComponent<Chapter3LayeredFoodState>();
        SpriteRenderer covered = CreateFoodLayer(root, "Covered");
        SpriteRenderer full = CreateFoodLayer(root, "Full");
        SpriteRenderer half = CreateFoodLayer(root, "Half");
        SpriteRenderer empty = CreateFoodLayer(root, "Empty");
        int childCount = root.transform.childCount;

        try
        {
            foodState.Configure(null, null, null, null, covered, full, half, empty);
            foodState.ShowCovered();
            Assert.That(covered.enabled, Is.True);
            Assert.That(full.enabled, Is.False);
            foodState.ShowFull();
            Assert.That(full.enabled, Is.True);
            foodState.ShowHalf();
            Assert.That(half.enabled, Is.True);
            foodState.ShowEmpty();
            Assert.That(empty.enabled, Is.True);
            Assert.That(root.transform.childCount, Is.EqualTo(childCount));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void TableForegroundOccluderTracksExistingDiningTableAboveGuests()
    {
        Sprite sprite = CreateSprite(Color.white);
        GameObject room = new GameObject("Dining Room");
        RoomContentGroup roomGroup = room.AddComponent<RoomContentGroup>();
        roomGroup.SetRoomName("Dining Room");
        GameObject sourceTable = new GameObject("correct_dining_table_0");
        sourceTable.transform.SetParent(room.transform, false);
        SpriteRenderer sourceRenderer = sourceTable.AddComponent<SpriteRenderer>();
        sourceRenderer.sprite = sprite;
        sourceRenderer.sortingOrder = 1628;
        GameObject controllerObject = new GameObject("Controller");
        Chapter3DiningTableForegroundOccluder occluder = controllerObject.AddComponent<Chapter3DiningTableForegroundOccluder>();

        try
        {
            Assert.That(occluder.EnsureOccluder(), Is.True);
            Assert.That(occluder.OccluderRenderer, Is.Not.Null);
            Assert.That(occluder.OccluderRenderer.sortingOrder, Is.GreaterThan(sourceRenderer.sortingOrder));
            Assert.That(occluder.OccluderRenderer.transform.parent, Is.EqualTo(sourceTable.transform.parent));
        }
        finally
        {
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(room);
            DestroySprite(sprite);
        }
    }

    [Test]
    public void ChapterManagerRoutesChapter3DinnerPendingToDinnerController()
    {
        string text = File.ReadAllText(ChapterManagerPath);
        Assert.That(text, Does.Contain("Chapter3PendingId = \"chapter_03_dinner_pending\""));
        Assert.That(text, Does.Contain("Chapter3DinnerController"));
        Assert.That(text, Does.Contain("chapter3DinnerController.BeginChapter3Dinner(this)"));
        Assert.That(text, Does.Contain("chapter_03_dinner"));
    }

    private static Chapter3LayeredDinnerAssetManifest CreateValidManifest(Sprite sprite)
    {
        Chapter3LayeredDinnerAssetManifest manifest = ScriptableObject.CreateInstance<Chapter3LayeredDinnerAssetManifest>();
        manifest.canvasSize = new Vector2Int(1448, 1086);
        manifest.tableBack = sprite;
        manifest.tableFrontOverlay = sprite;
        manifest.seats = new Chapter3LayeredDinnerAssetManifest.Chapter3SeatLayerSet[8];

        for (int i = 0; i < manifest.seats.Length; i++)
        {
            manifest.seats[i] = new Chapter3LayeredDinnerAssetManifest.Chapter3SeatLayerSet
            {
                seatId = $"Seat{i + 1:00}",
                idleFrames = new[] { sprite },
                eatFrames = new[] { sprite, sprite }
            };
        }

        return manifest;
    }

    private static SpriteRenderer CreateFoodLayer(GameObject root, string name)
    {
        GameObject layer = new GameObject(name);
        layer.transform.SetParent(root.transform, false);
        SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSprite(Color.white);
        return renderer;
    }

    private static Sprite CreateSprite(Color color)
    {
        Texture2D texture = new Texture2D(1448, 1086, TextureFormat.RGBA32, false);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing private field {fieldName}.");
        field.SetValue(target, value);
    }

    private static void DestroyManifestAndSprite(Chapter3LayeredDinnerAssetManifest manifest, Sprite sprite)
    {
        Object.DestroyImmediate(manifest);
        DestroySprite(sprite);
    }

    private static void DestroySprite(Sprite sprite)
    {
        if (sprite == null)
        {
            return;
        }

        Texture texture = sprite.texture;
        Object.DestroyImmediate(sprite);

        if (texture != null)
        {
            Object.DestroyImmediate(texture);
        }
    }
}
