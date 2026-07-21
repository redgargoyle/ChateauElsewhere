using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
    private const string LibraryFlowerSideTableName = "library_flower_side_table_0";
    private const string LibraryFlowerSideTableSpritePath = "Assets/Art/Objects/library_flower_side_table.png";
    private const string LibraryBackgroundPath = "Assets/Art/Final Images (DO NOT EDIT)/library.png";

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
    public void ActorYSorterTracksStableFloorReferenceAcrossSpriteFrameChanges()
    {
        GameObject sortingSourceObject = null;
        GameObject actorObject = null;
        Texture2D texture = null;
        Sprite firstFrame = null;
        Sprite differentlyCroppedFrame = null;

        try
        {
            sortingSourceObject = new GameObject("ButlerSortingSource");
            PointClickPlayerMovement sortingSource = sortingSourceObject.AddComponent<PointClickPlayerMovement>();

            actorObject = new GameObject("Guest");
            GameObject visualObject = new GameObject("AnimationDisplay");
            visualObject.transform.SetParent(actorObject.transform, false);
            SpriteRenderer bodyRenderer = visualObject.AddComponent<SpriteRenderer>();
            GameObject coatObject = new GameObject("GuestCoat");
            coatObject.transform.SetParent(actorObject.transform, false);
            SpriteRenderer coatRenderer = coatObject.AddComponent<SpriteRenderer>();
            texture = new Texture2D(16, 16);
            firstFrame = Sprite.Create(
                texture,
                new Rect(0f, 0f, 8f, 8f),
                new Vector2(0.5f, 0.5f),
                8f);
            differentlyCroppedFrame = Sprite.Create(
                texture,
                new Rect(0f, 0f, 16f, 16f),
                new Vector2(0.5f, 1f),
                4f);
            bodyRenderer.sprite = firstFrame;
            coatRenderer.sprite = firstFrame;

            WorldYSortSpriteRenderer sorter = actorObject.AddComponent<WorldYSortSpriteRenderer>();
            bodyRenderer.sortingOrder = 120;
            coatRenderer.sortingOrder = 123;
            sorter.ConfigureForActor(sortingSource, bodyRenderer);

            Assert.That(sorter.ActorFloorReference, Is.Not.Null);
            Assert.That(sorter.ActorFloorReference.IsInitialized, Is.True);
            Assert.That(sorter.ActorFloorReference.ReferenceTransform, Is.SameAs(actorObject.transform));
            float stableFloorY = sorter.CurrentActorSortingY;
            int firstExpectedOrder = sortingSource.GetSortingOrderForFootY(stableFloorY);
            Assert.That(bodyRenderer.sortingOrder, Is.EqualTo(firstExpectedOrder));
            Assert.That(coatRenderer.sortingOrder, Is.EqualTo(firstExpectedOrder + 3), "Coat/body layering should survive continuous depth updates.");

            visualObject.transform.localScale = new Vector3(2f, 2f, 1f);
            sorter.ApplySorting();
            float scaledStableFloorY = sorter.CurrentActorSortingY;
            Assert.That(
                scaledStableFloorY,
                Is.EqualTo(stableFloorY).Within(0.0001f),
                "Display-only scale must not change the actor's canonical gameplay floor point.");
            Assert.That(
                Mathf.Abs(scaledStableFloorY - bodyRenderer.bounds.min.y),
                Is.GreaterThan(0.001f),
                "This centered-pivot fixture should prove sorting no longer follows scale-dependent renderer bounds.");
            int scaledExpectedOrder = sortingSource.GetSortingOrderForFootY(scaledStableFloorY);
            Assert.That(bodyRenderer.sortingOrder, Is.EqualTo(scaledExpectedOrder));
            float scaledVisibleBoundsY = bodyRenderer.bounds.min.y;

            bodyRenderer.sprite = differentlyCroppedFrame;
            Assert.That(
                Mathf.Abs(bodyRenderer.bounds.min.y - scaledVisibleBoundsY),
                Is.GreaterThan(0.001f),
                "The alternate frame should exercise a genuinely different visible lower bound.");
            sorter.ApplySorting();

            Assert.That(sorter.CurrentActorSortingY, Is.EqualTo(scaledStableFloorY).Within(0.0001f));
            Assert.That(bodyRenderer.sortingOrder, Is.EqualTo(scaledExpectedOrder), "Animation frame bounds must not move an actor between depth bands.");
            Assert.That(coatRenderer.sortingOrder, Is.EqualTo(scaledExpectedOrder + 3));

            actorObject.transform.position = new Vector3(0f, 2f, 0f);
            sorter.ApplySorting();

            int movedExpectedOrder = sortingSource.GetSortingOrderForFootY(scaledStableFloorY + 2f);
            Assert.That(bodyRenderer.sortingOrder, Is.EqualTo(movedExpectedOrder));
            Assert.That(bodyRenderer.sortingOrder, Is.Not.EqualTo(scaledExpectedOrder));
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

            if (firstFrame != null)
            {
                Object.DestroyImmediate(firstFrame);
            }

            if (differentlyCroppedFrame != null)
            {
                Object.DestroyImmediate(differentlyCroppedFrame);
            }

            if (texture != null)
            {
                Object.DestroyImmediate(texture);
            }

        }
    }

    [Test]
    public void ActorYSorterStopsWritingCoatAfterItLeavesActorHierarchy()
    {
        GameObject sortingSourceObject = null;
        GameObject actorObject = null;
        GameObject carrierObject = null;
        Texture2D texture = null;
        Sprite sprite = null;

        try
        {
            sortingSourceObject = new GameObject("ButlerSortingSource");
            PointClickPlayerMovement sortingSource = sortingSourceObject.AddComponent<PointClickPlayerMovement>();

            actorObject = new GameObject("Guest07");
            GameObject visualObject = new GameObject("AnimationDisplay");
            visualObject.transform.SetParent(actorObject.transform, false);
            SpriteRenderer bodyRenderer = visualObject.AddComponent<SpriteRenderer>();

            // The arrival controller nests a worn coat beneath AnimationDisplay.
            // Removing that grandchild does not change the guest root's direct
            // child list, so stale renderer caches must be rejected explicitly.
            GameObject coatObject = new GameObject("Guest07Coat");
            coatObject.transform.SetParent(visualObject.transform, false);
            SpriteRenderer coatRenderer = coatObject.AddComponent<SpriteRenderer>();

            texture = new Texture2D(8, 8);
            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 8f, 8f),
                new Vector2(0.5f, 0f),
                8f);
            bodyRenderer.sprite = sprite;
            coatRenderer.sprite = sprite;

            WorldYSortSpriteRenderer guestSorter = actorObject.AddComponent<WorldYSortSpriteRenderer>();
            bodyRenderer.sortingOrder = 120;
            coatRenderer.sortingOrder = 121;
            guestSorter.ConfigureForActor(sortingSource, bodyRenderer);
            Assert.That(
                coatRenderer.sortingOrder,
                Is.EqualTo(bodyRenderer.sortingOrder + 1),
                "The coat should initially participate in the guest's shared Y-sort group.");
            int guestOrderBeforeMove = bodyRenderer.sortingOrder;

            carrierObject = new GameObject("ButlerCoatCarryAnchor");
            coatObject.transform.SetParent(carrierObject.transform, true);
            Assert.That(coatObject.transform.IsChildOf(actorObject.transform), Is.False);

            int carrierOwnedOrder = guestSorter.CurrentBaseSortingOrder + 257;
            coatRenderer.sortingOrder = carrierOwnedOrder;

            actorObject.transform.position = new Vector3(0f, 3f, 0f);
            guestSorter.ApplySorting();

            Assert.That(
                coatRenderer.sortingOrder,
                Is.EqualTo(carrierOwnedOrder),
                "Once the coat leaves the guest hierarchy, only its new sorting owner may write it.");
            Assert.That(
                bodyRenderer.sortingOrder,
                Is.Not.EqualTo(guestOrderBeforeMove),
                "The guest sorter should still update its remaining body renderer.");
        }
        finally
        {
            if (actorObject != null)
            {
                Object.DestroyImmediate(actorObject);
            }

            if (carrierObject != null)
            {
                Object.DestroyImmediate(carrierObject);
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
        }
    }

    [Test]
    public void ButlerSortingAccessoryFollowsBodyAndTransfersCleanlyBetweenCoats()
    {
        GameObject butlerObject = null;
        Texture2D texture = null;
        Sprite sprite = null;

        try
        {
            butlerObject = new GameObject("Butler");
            SpriteRenderer bodyRenderer = butlerObject.AddComponent<SpriteRenderer>();
            texture = new Texture2D(8, 8);
            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 8f, 8f),
                new Vector2(0.5f, 0f),
                8f);
            bodyRenderer.sprite = sprite;
            bodyRenderer.sortingOrder = 42;

            PointClickPlayerMovement movement = butlerObject.AddComponent<PointClickPlayerMovement>();
            movement.SetPlayerSortingEnabled(true);

            // Carried coats are attached after the Butler has cached its body
            // renderers. Place this coat well below the visible feet so the test
            // detects any accidental use of accessory bounds for body depth.
            GameObject firstCoatObject = new GameObject("Guest07Coat");
            firstCoatObject.transform.SetParent(butlerObject.transform, false);
            firstCoatObject.transform.localPosition = new Vector3(0f, -5f, 0f);
            SpriteRenderer firstCoatRenderer = firstCoatObject.AddComponent<SpriteRenderer>();
            firstCoatRenderer.sprite = sprite;
            firstCoatRenderer.sortingOrder = 500;

            GameObject firstCoatTrimObject = new GameObject("Guest07CoatTrim");
            firstCoatTrimObject.transform.SetParent(firstCoatObject.transform, false);
            SpriteRenderer firstCoatTrimRenderer = firstCoatTrimObject.AddComponent<SpriteRenderer>();
            firstCoatTrimRenderer.sprite = sprite;
            firstCoatTrimRenderer.sortingOrder = 503;

            MethodInfo applyPlayerSorting = typeof(PointClickPlayerMovement).GetMethod(
                "ApplyPlayerSorting",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(applyPlayerSorting, Is.Not.Null);

            movement.RegisterSortingAccessory(firstCoatObject, 1);

            int expectedInitialBodyOrder = movement.GetSortingOrderForFootY(bodyRenderer.bounds.min.y);
            Assert.That(movement.CurrentSortingOrder, Is.EqualTo(expectedInitialBodyOrder));
            Assert.That(bodyRenderer.sortingOrder, Is.EqualTo(expectedInitialBodyOrder));
            Assert.That(firstCoatRenderer.sortingOrder, Is.EqualTo(expectedInitialBodyOrder + 1));
            Assert.That(
                firstCoatTrimRenderer.sortingOrder,
                Is.EqualTo(expectedInitialBodyOrder + 4),
                "Accessory registration should preserve the coat hierarchy's internal +3 renderer spacing.");

            butlerObject.transform.position = new Vector3(0f, 2f, 0f);
            applyPlayerSorting.Invoke(movement, null);

            int movedBodyOrder = movement.GetSortingOrderForFootY(bodyRenderer.bounds.min.y);
            Assert.That(movement.CurrentSortingOrder, Is.EqualTo(movedBodyOrder));
            Assert.That(bodyRenderer.sortingOrder, Is.EqualTo(movedBodyOrder));
            Assert.That(firstCoatRenderer.sortingOrder, Is.EqualTo(movedBodyOrder + 1));
            Assert.That(firstCoatTrimRenderer.sortingOrder, Is.EqualTo(movedBodyOrder + 4));
            Assert.That(
                movedBodyOrder,
                Is.Not.EqualTo(movement.GetSortingOrderForFootY(firstCoatRenderer.bounds.min.y)),
                "The coat's lower bounds must not pull the Butler into the coat's Y-depth band.");

            int firstCoatOrderBeforeAuthoredRestore = firstCoatRenderer.sortingOrder;
            int firstTrimOrderBeforeAuthoredRestore = firstCoatTrimRenderer.sortingOrder;
            movement.SetPlayerSortingEnabled(false, true);

            Assert.That(bodyRenderer.sortingOrder, Is.EqualTo(42), "Disabling dynamic sorting should restore the authored Butler order.");
            Assert.That(
                firstCoatRenderer.sortingOrder,
                Is.EqualTo(firstCoatOrderBeforeAuthoredRestore),
                "Restoring authored body sorting must not rewrite a registered accessory.");
            Assert.That(firstCoatTrimRenderer.sortingOrder, Is.EqualTo(firstTrimOrderBeforeAuthoredRestore));

            movement.SetPlayerSortingEnabled(true);
            applyPlayerSorting.Invoke(movement, null);
            movement.UnregisterSortingAccessory(firstCoatObject);
            const int releasedCoatOrder = 22000;
            const int releasedTrimOrder = 22003;
            firstCoatRenderer.sortingOrder = releasedCoatOrder;
            firstCoatTrimRenderer.sortingOrder = releasedTrimOrder;

            GameObject secondCoatObject = new GameObject("Guest08Coat");
            secondCoatObject.transform.SetParent(butlerObject.transform, false);
            SpriteRenderer secondCoatRenderer = secondCoatObject.AddComponent<SpriteRenderer>();
            secondCoatRenderer.sprite = sprite;
            secondCoatRenderer.sortingOrder = -300;
            movement.RegisterSortingAccessory(secondCoatObject, 1);

            butlerObject.transform.position = new Vector3(0f, -2f, 0f);
            applyPlayerSorting.Invoke(movement, null);

            int secondMoveBodyOrder = movement.GetSortingOrderForFootY(bodyRenderer.bounds.min.y);
            Assert.That(movement.CurrentSortingOrder, Is.EqualTo(secondMoveBodyOrder));
            Assert.That(bodyRenderer.sortingOrder, Is.EqualTo(secondMoveBodyOrder));
            Assert.That(secondCoatRenderer.sortingOrder, Is.EqualTo(secondMoveBodyOrder + 1));
            Assert.That(
                firstCoatRenderer.sortingOrder,
                Is.EqualTo(releasedCoatOrder),
                "After release, the previous coat must stop receiving Butler sorting writes.");
            Assert.That(firstCoatTrimRenderer.sortingOrder, Is.EqualTo(releasedTrimOrder));
        }
        finally
        {
            if (butlerObject != null)
            {
                Object.DestroyImmediate(butlerObject);
            }

            if (sprite != null)
            {
                Object.DestroyImmediate(sprite);
            }

            if (texture != null)
            {
                Object.DestroyImmediate(texture);
            }
        }
    }

    [Test]
    public void EqualYActorsKeepDeterministicOrderWhenTheirAnimationFramesSwap()
    {
        GameObject sortingSourceObject = null;
        GameObject firstActorObject = null;
        GameObject secondActorObject = null;
        Texture2D texture = null;
        Sprite bottomPivotFrame = null;
        Sprite topPivotFrame = null;

        try
        {
            sortingSourceObject = new GameObject("SharedButlerSortingSource");
            PointClickPlayerMovement sortingSource = sortingSourceObject.AddComponent<PointClickPlayerMovement>();

            firstActorObject = new GameObject("Guest01");
            ActorRoomState firstActorState = firstActorObject.AddComponent<ActorRoomState>();
            firstActorState.SetActorId("Guest01");
            SpriteRenderer firstRenderer = firstActorObject.AddComponent<SpriteRenderer>();

            secondActorObject = new GameObject("Guest02");
            ActorRoomState secondActorState = secondActorObject.AddComponent<ActorRoomState>();
            secondActorState.SetActorId("Guest02");
            SpriteRenderer secondRenderer = secondActorObject.AddComponent<SpriteRenderer>();

            texture = new Texture2D(16, 16);
            bottomPivotFrame = Sprite.Create(
                texture,
                new Rect(0f, 0f, 8f, 8f),
                new Vector2(0.5f, 0f),
                8f);
            topPivotFrame = Sprite.Create(
                texture,
                new Rect(0f, 0f, 16f, 16f),
                new Vector2(0.5f, 1f),
                4f);
            firstRenderer.sprite = bottomPivotFrame;
            secondRenderer.sprite = topPivotFrame;

            WorldYSortSpriteRenderer firstSorter = firstActorObject.AddComponent<WorldYSortSpriteRenderer>();
            WorldYSortSpriteRenderer secondSorter = secondActorObject.AddComponent<WorldYSortSpriteRenderer>();
            firstSorter.ConfigureForActor(sortingSource, firstRenderer);
            secondSorter.ConfigureForActor(sortingSource, secondRenderer);
            firstSorter.ActorFloorReference.CaptureWorldPoint(Vector3.zero);
            secondSorter.ActorFloorReference.CaptureWorldPoint(Vector3.zero);
            firstSorter.ApplySorting();
            secondSorter.ApplySorting();

            Assert.That(firstSorter.CurrentBaseSortingOrder, Is.EqualTo(secondSorter.CurrentBaseSortingOrder));
            Assert.That(firstSorter.CurrentActorSortingY, Is.EqualTo(secondSorter.CurrentActorSortingY).Within(0.0001f));
            Assert.That(firstSorter.CurrentTieBreakOffset, Is.Not.EqualTo(secondSorter.CurrentTieBreakOffset));
            int firstStableOrder = firstRenderer.sortingOrder;
            int secondStableOrder = secondRenderer.sortingOrder;
            Assert.That(firstStableOrder, Is.Not.EqualTo(secondStableOrder), "Equal-Y guests need a deterministic render order instead of an engine tie.");

            for (int frame = 0; frame < 4; frame++)
            {
                firstRenderer.sprite = frame % 2 == 0 ? topPivotFrame : bottomPivotFrame;
                secondRenderer.sprite = frame % 2 == 0 ? bottomPivotFrame : topPivotFrame;

                // Intentionally reverse the update order to prove the result is not
                // decided by whichever guest happened to sort last this frame.
                secondSorter.ApplySorting();
                firstSorter.ApplySorting();

                Assert.That(firstSorter.CurrentActorSortingY, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(secondSorter.CurrentActorSortingY, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(firstRenderer.sortingOrder, Is.EqualTo(firstStableOrder));
                Assert.That(secondRenderer.sortingOrder, Is.EqualTo(secondStableOrder));
            }
        }
        finally
        {
            if (firstActorObject != null)
            {
                Object.DestroyImmediate(firstActorObject);
            }

            if (secondActorObject != null)
            {
                Object.DestroyImmediate(secondActorObject);
            }

            if (sortingSourceObject != null)
            {
                Object.DestroyImmediate(sortingSourceObject);
            }

            if (bottomPivotFrame != null)
            {
                Object.DestroyImmediate(bottomPivotFrame);
            }

            if (topPivotFrame != null)
            {
                Object.DestroyImmediate(topPivotFrame);
            }

            if (texture != null)
            {
                Object.DestroyImmediate(texture);
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
            Assert.That(chairRenderer, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(chairRenderer.sprite), Is.EqualTo(DrawingRoomChairSpritePath));

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
                "This explicitly authored exception must survive generated-blocker cleanup.");
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
    public void LibraryFlowerSideTableUsesLowerBoxFootprintForSharedButlerYSort()
    {
        SceneSetup[] previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            Transform room = FindTransformInScene(scene, "Room_Library");
            Transform table = FindDescendant(room, LibraryFlowerSideTableName);
            Transform blockerTransform = FindDescendant(room, $"PlayerBlocker_{LibraryFlowerSideTableName}");

            Assert.That(room, Is.Not.Null, "The authored Library should exist in Gameplay.unity.");
            Assert.That(table, Is.Not.Null, "The flower side table should remain authored under the Library.");
            Assert.That(blockerTransform, Is.Not.Null, "The Library flower side table needs a physical lower-footprint blocker.");

            SpriteRenderer tableRenderer = table.GetComponent<SpriteRenderer>();
            ObjectMovementBlocker2D marker = blockerTransform.GetComponent<ObjectMovementBlocker2D>();
            BoxCollider2D blocker = blockerTransform.GetComponent<BoxCollider2D>();

            Assert.That(tableRenderer, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(tableRenderer.sprite), Is.EqualTo(LibraryFlowerSideTableSpritePath));
            Assert.That(table.localPosition.x, Is.EqualTo(-306f).Within(0.1f),
                "The cutout must remain registered over the painted Library side table.");
            Assert.That(table.localPosition.y, Is.EqualTo(-261.5f).Within(0.1f),
                "The cutout's floor contact must remain aligned to the painted table feet.");
            Assert.That(table.localScale.x, Is.EqualTo(100f).Within(0.001f));
            Assert.That(table.localScale.y, Is.EqualTo(100f).Within(0.001f));
            Assert.That(tableRenderer.sprite.rect.size, Is.EqualTo(new Vector2(180f, 264f)),
                "The prop must be a 1:1 pixel extraction from the original Library background, not a regenerated illustration.");
            Assert.That(table.GetComponent<WorldYSortSpriteRenderer>(), Is.Null,
                "The lower-footprint blocker is the table's sole y-axis sorting writer.");

            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.SourceObject, Is.SameAs(table.gameObject));
            Assert.That(marker.SourceRoomName, Is.EqualTo("Library"));
            Assert.That(marker.Category, Is.EqualTo(ObjectCollisionBoxCategory.Table.ToString()));
            Assert.That(marker.FootprintHeightFraction, Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(marker.GeneratedByCollisionBoxTool, Is.False);
            Assert.That(marker.SortSourceRenderers, Is.True,
                "The physical lower edge must drive the same y-axis sorting formula as the Butler.");

            Assert.That(blocker, Is.Not.Null);
            Assert.That(blocker.enabled, Is.True);
            Assert.That(blocker.isTrigger, Is.True);
            Assert.That(blocker.offset, Is.EqualTo(new Vector2(-310f, -222f)));
            Assert.That(blocker.size, Is.EqualTo(new Vector2(125f, 35f)),
                "The collision footprint must stay in the bottom leg area instead of reaching up into the table opening.");

            bool roomWasActive = room.gameObject.activeSelf;

            try
            {
                room.gameObject.SetActive(true);
                Physics2D.SyncTransforms();

                Assert.That(blocker.gameObject.activeInHierarchy, Is.True);
                Assert.That(blocker.OverlapPoint(blocker.bounds.center), Is.True,
                    "The active trigger must cover the lower legs/shelf footprint used for pathing.");
                Assert.That(blocker.bounds.size.y, Is.LessThan(tableRenderer.bounds.size.y * 0.31f),
                    "Collision must remain in the lower portion of the table, leaving the vase and flowers pass-through.");

                marker.ApplySourceSortingNow();
                int expectedTableOrder = 1000 - Mathf.RoundToInt(blocker.bounds.min.y * 100f);
                int butlerOrderJustBehindTable = 1000 - Mathf.RoundToInt((blocker.bounds.min.y + 0.1f) * 100f);
                int butlerOrderJustInFrontOfTable = 1000 - Mathf.RoundToInt((blocker.bounds.min.y - 0.1f) * 100f);

                Assert.That(tableRenderer.sortingOrder, Is.EqualTo(expectedTableOrder));
                Assert.That(tableRenderer.sortingOrder, Is.EqualTo(marker.CurrentSortingOrder));
                Assert.That(butlerOrderJustBehindTable, Is.LessThan(tableRenderer.sortingOrder),
                    "Butler feet above the table's lower edge must render behind it.");
                Assert.That(butlerOrderJustInFrontOfTable, Is.GreaterThan(tableRenderer.sortingOrder),
                    "Butler feet below the table's lower edge must render in front of it.");
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
    public void LibraryFlowerSideTableUsesExactSourcePixelsAndKeepsEveryFloorGapTransparent()
    {
        const int sourceLeft = 440;
        const int sourceTop = 468;
        const int expectedWidth = 180;
        const int expectedHeight = 264;

        Texture2D libraryTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Texture2D tableTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        try
        {
            Assert.That(ImageConversion.LoadImage(libraryTexture, File.ReadAllBytes(LibraryBackgroundPath)), Is.True);
            Assert.That(ImageConversion.LoadImage(tableTexture, File.ReadAllBytes(LibraryFlowerSideTableSpritePath)), Is.True);
            Assert.That(tableTexture.width, Is.EqualTo(expectedWidth));
            Assert.That(tableTexture.height, Is.EqualTo(expectedHeight));

            Color32[] libraryPixels = libraryTexture.GetPixels32();
            Color32[] tablePixels = tableTexture.GetPixels32();

            for (int topY = 0; topY < expectedHeight; topY++)
            {
                int tableBottomY = expectedHeight - 1 - topY;
                int libraryBottomY = libraryTexture.height - 1 - (sourceTop + topY);

                for (int x = 0; x < expectedWidth; x++)
                {
                    Color32 source = libraryPixels[(libraryBottomY * libraryTexture.width) + sourceLeft + x];
                    Color32 cutout = tablePixels[(tableBottomY * expectedWidth) + x];

                    if (source.r == cutout.r && source.g == cutout.g && source.b == cutout.b)
                    {
                        continue;
                    }

                    Assert.Fail($"The Library table must retain the exact source RGB at top-left pixel ({x}, {topY}); regenerated color or geometry cannot be registered pixel-perfectly over the room.");
                }
            }

            Vector2Int[] transparentFloorSamples =
            {
                new Vector2Int(25, 190), // Outside the rear-left leg.
                new Vector2Int(43, 175), // Between the two left legs.
                new Vector2Int(45, 195), // Lower gap between the two left legs.
                new Vector2Int(70, 190), // Open floor between the front-left leg and books.
                new Vector2Int(80, 190), // Same opening, nearer its center.
                new Vector2Int(85, 205), // Open floor immediately left of the lower books.
                new Vector2Int(110, 170), // Open space below the apron and above the books.
                new Vector2Int(90, 235), // Floor below the shelf center.
                new Vector2Int(110, 235), // Floor below the right side of the shelf.
                new Vector2Int(125, 235), // Floor between the shelf and foreground desk.
            };

            for (int i = 0; i < transparentFloorSamples.Length; i++)
            {
                Vector2Int point = transparentFloorSamples[i];
                byte alpha = tablePixels[((expectedHeight - 1 - point.y) * expectedWidth) + point.x].a;
                Assert.That(alpha, Is.EqualTo(0),
                    $"Floor/open-space pixel ({point.x}, {point.y}) must be fully transparent so a character behind the table remains visible.");
            }

            Vector2Int[] opaqueFurnitureSamples =
            {
                new Vector2Int(90, 155), // Tabletop/apron.
                new Vector2Int(33, 190), // Rear-left leg.
                new Vector2Int(55, 190), // Front-left leg.
                new Vector2Int(120, 200), // Books.
                new Vector2Int(80, 215), // Lower shelf.
                new Vector2Int(55, 240), // Front-left foot.
            };

            for (int i = 0; i < opaqueFurnitureSamples.Length; i++)
            {
                Vector2Int point = opaqueFurnitureSamples[i];
                byte alpha = tablePixels[((expectedHeight - 1 - point.y) * expectedWidth) + point.x].a;
                Assert.That(alpha, Is.GreaterThanOrEqualTo(240),
                    $"Furniture pixel ({point.x}, {point.y}) must remain opaque while the surrounding floor is removed.");
            }
        }
        finally
        {
            Object.DestroyImmediate(libraryTexture);
            Object.DestroyImmediate(tableTexture);
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
            ObjectMovementBlocker2D marker = blockerTransform.GetComponent<ObjectMovementBlocker2D>();
            PolygonCollider2D blocker = blockerTransform.GetComponent<PolygonCollider2D>();
            RoomContentGroup roomContent = room.GetComponent<RoomContentGroup>();
            CameraManager cameraManager = FindComponentInScene<CameraManager>(scene);
            PointClickPlayerMovement playerMovement = FindComponentInScene<PointClickPlayerMovement>(scene);

            Assert.That(tableRenderer, Is.Not.Null);
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.SourceObject, Is.SameAs(table.gameObject));
            Assert.That(marker.SortSourceRenderers, Is.True,
                "The table's lower physical footprint must be its sole Butler-compatible sorting owner.");
            Assert.That(blocker, Is.Not.Null);
            Assert.That(roomContent, Is.Not.Null);
            Assert.That(cameraManager, Is.Not.Null);
            Assert.That(playerMovement, Is.Not.Null);

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

                    marker.ApplySourceSortingNow();
                    Assert.That(tableRenderer.sortingOrder, Is.EqualTo(expectedTableOrder),
                        "The physical-footprint sorter must remain the table's sole occlusion owner.");
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

        GameplayRuntimeState.ResetForNewGame();
        SceneManager.sceneLoaded += ConfigureHeadlessTestCameras;

        try
        {
            AsyncOperation gameplayLoad = SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Single);
            Assert.That(gameplayLoad, Is.Not.Null, "The live occlusion regression must be able to load the Gameplay build scene.");

            while (!gameplayLoad.isDone)
            {
                yield return null;
            }
        }
        finally
        {
            SceneManager.sceneLoaded -= ConfigureHeadlessTestCameras;
        }

        ConfigureHeadlessTestCameras(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        yield return null;

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
                        Is.EqualTo(sorter.CurrentBaseSortingOrder + sorter.CurrentTieBreakOffset),
                        $"{guest.ActorId} should continuously sort from its stable floor reference at pan {verticalPans[panIndex]}.");

                    if (sorter.CurrentActorSortingY > tableBlocker.bounds.min.y)
                    {
                        Assert.That(characterRenderer.sortingOrder, Is.LessThan(tableRenderer.sortingOrder),
                            $"{guest.ActorId} should render behind the tea table when its stable floor point is above the table edge.");
                    }
                }
            }

            Assert.That(seatedCount, Is.EqualTo(5));
            Assert.That(standingCount, Is.EqualTo(3));
            Assert.That(standingProbe, Is.Not.Null);
            Assert.That(standingProbeRenderer, Is.Not.Null);
            Assert.That(standingProbeSorter, Is.Not.Null);
            Assert.That(standingProbeSorter.ActorFloorReference, Is.Not.Null);

            Vector3 originalProbePosition = standingProbe.transform.position;
            float behindTableFootY = tableBlocker.bounds.min.y + 0.5f;
            standingProbeSorter.ActorFloorReference.AlignActorToWorldPoint(
                new Vector2(standingProbeSorter.ActorFloorReference.WorldPoint.x, behindTableFootY));
            yield return null;
            yield return null;
            standingProbeSorter.ActorFloorReference.AlignActorToWorldPoint(
                new Vector2(standingProbeSorter.ActorFloorReference.WorldPoint.x, behindTableFootY));
            standingProbeSorter.ApplySorting();

            Assert.That(
                standingProbeRenderer.sortingOrder,
                Is.EqualTo(standingProbeSorter.CurrentBaseSortingOrder + standingProbeSorter.CurrentTieBreakOffset));
            Assert.That(standingProbeRenderer.sortingOrder, Is.LessThan(tableRenderer.sortingOrder),
                $"A real standing guest must render behind the table above its front edge at pan {verticalPans[panIndex]}.");

            float inFrontOfTableFootY = tableBlocker.bounds.min.y - 0.5f;
            standingProbeSorter.ActorFloorReference.AlignActorToWorldPoint(
                new Vector2(standingProbeSorter.ActorFloorReference.WorldPoint.x, inFrontOfTableFootY));
            yield return null;
            yield return null;
            standingProbeSorter.ActorFloorReference.AlignActorToWorldPoint(
                new Vector2(standingProbeSorter.ActorFloorReference.WorldPoint.x, inFrontOfTableFootY));
            standingProbeSorter.ApplySorting();

            Assert.That(
                standingProbeRenderer.sortingOrder,
                Is.EqualTo(standingProbeSorter.CurrentBaseSortingOrder + standingProbeSorter.CurrentTieBreakOffset));
            Assert.That(standingProbeRenderer.sortingOrder, Is.GreaterThan(tableRenderer.sortingOrder),
                $"A real standing guest must render in front of the table below its front edge at pan {verticalPans[panIndex]}.");

            standingProbe.transform.position = originalProbePosition;
            yield return null;
            yield return null;

            float behindGreenChairFootY = greenChair.position.y + 0.5f;
            standingProbeSorter.ActorFloorReference.AlignActorToWorldPoint(
                new Vector2(standingProbeSorter.ActorFloorReference.WorldPoint.x, behindGreenChairFootY));
            yield return null;
            yield return null;
            standingProbeSorter.ActorFloorReference.AlignActorToWorldPoint(
                new Vector2(standingProbeSorter.ActorFloorReference.WorldPoint.x, behindGreenChairFootY));
            standingProbeSorter.ApplySorting();
            Assert.That(standingProbeRenderer.sortingOrder, Is.LessThan(greenChairRenderer.sortingOrder),
                $"A real standing guest must render behind the green chair above its Y edge at pan {verticalPans[panIndex]}.");

            float inFrontOfGreenChairFootY = greenChair.position.y - 0.5f;
            standingProbeSorter.ActorFloorReference.AlignActorToWorldPoint(
                new Vector2(standingProbeSorter.ActorFloorReference.WorldPoint.x, inFrontOfGreenChairFootY));
            yield return null;
            yield return null;
            standingProbeSorter.ActorFloorReference.AlignActorToWorldPoint(
                new Vector2(standingProbeSorter.ActorFloorReference.WorldPoint.x, inFrontOfGreenChairFootY));
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

    private static void ConfigureHeadlessTestCameras(Scene scene, LoadSceneMode mode)
    {
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && (cameras[i].pixelWidth <= 1 || cameras[i].pixelHeight <= 1))
            {
                cameras[i].pixelRect = new Rect(0f, 0f, 800f, 450f);
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
