using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SortingOwnershipAuditTests
{
    [Test]
    public void SelectedActorAuditFlagsDuplicateNormalSortingOwners()
    {
        GameObject actorObject = null;

        try
        {
            actorObject = new GameObject("Audit Actor");
            actorObject.AddComponent<SpriteRenderer>();
            actorObject.AddComponent<WorldYSortSpriteRenderer>();
            PointClickPlayerMovement pointClickMovement =
                actorObject.AddComponent<PointClickPlayerMovement>();

            string conflictReport =
                GreenChairRenderStateDiagnostic.BuildSelectedSortingOwnershipAuditForTests(actorObject);

            Assert.That(conflictReport, Does.Contain("CONFLICT renderer="));
            Assert.That(conflictReport, Does.Contain(nameof(WorldYSortSpriteRenderer)));
            Assert.That(conflictReport, Does.Contain(nameof(PointClickPlayerMovement)));
            Assert.That(conflictReport, Does.Contain("Inspector sorting values are overwritten"));

            pointClickMovement.SetPlayerSortingEnabled(false, false);

            string cleanReport =
                GreenChairRenderStateDiagnostic.BuildSelectedSortingOwnershipAuditForTests(actorObject);

            Assert.That(cleanReport, Does.Not.Contain("CONFLICT renderer="));
        }
        finally
        {
            if (actorObject != null)
            {
                Object.DestroyImmediate(actorObject);
            }
        }
    }

    [Test]
    public void SelectedActorAuditAllowsWorldYOwnerPlusActiveSeatedException()
    {
        GameObject roomObject = null;
        GameObject actorObject = null;
        GameObject chairObject = null;

        try
        {
            roomObject = new GameObject("Room_Drawing_Room");
            RoomContentGroup room = roomObject.AddComponent<RoomContentGroup>();
            room.SetRoomName("Drawing Room");
            GameObject seatObject = new GameObject("DrawingRoomGuestPoint_04");
            seatObject.transform.SetParent(roomObject.transform, false);
            RoomAnchor seat = seatObject.AddComponent<RoomAnchor>();
            seat.RefreshFromHierarchy();

            actorObject = new GameObject("Guest 4");
            SpriteRenderer actorRenderer = actorObject.AddComponent<SpriteRenderer>();
            actorRenderer.sortingLayerName = "People";
            actorRenderer.sortingOrder = 1500;
            ActorRoomState actorState = actorObject.AddComponent<ActorRoomState>();
            SerializedObject serializedActor = new SerializedObject(actorState);
            serializedActor.FindProperty("restrictVisibilityToCurrentRoom").boolValue = false;
            serializedActor.ApplyModifiedPropertiesWithoutUndo();
            actorState.SetCurrentRoom("Drawing Room");
            actorState.SetAvailableInCurrentChapter(true);
            actorState.SetVisibleByChapterState(true);
            actorState.SetSeated(true);
            actorObject.AddComponent<WorldYSortSpriteRenderer>();

            chairObject = new GameObject("drawingroomgreenchair_0");
            SpriteRenderer chairRenderer = chairObject.AddComponent<SpriteRenderer>();
            chairRenderer.sortingLayerName = "People";
            chairRenderer.sortingOrder = 1050;

            DiningRoomSeatedGuestOcclusionException seatedException =
                actorObject.AddComponent<DiningRoomSeatedGuestOcclusionException>();
            seatedException.ActivateBehindOccluder(
                actorState,
                seat,
                chairRenderer,
                null,
                "Drawing Room",
                "Butler");

            string report =
                GreenChairRenderStateDiagnostic.BuildSelectedSortingOwnershipAuditForTests(actorObject);

            Assert.That(report, Does.Contain("ALLOWED_NARROW_EXCEPTION renderer="));
            Assert.That(report, Does.Contain(nameof(WorldYSortSpriteRenderer)));
            Assert.That(report, Does.Contain(nameof(DiningRoomSeatedGuestOcclusionException)));
            Assert.That(report, Does.Not.Contain("CONFLICT renderer="));
        }
        finally
        {
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
    public void SelectedEnvironmentAuditFlagsWorldYAndBlockerWritingTheSameRenderer()
    {
        GameObject propObject = null;
        GameObject blockerObject = null;

        try
        {
            propObject = new GameObject("Audit Prop");
            propObject.AddComponent<SpriteRenderer>();
            propObject.AddComponent<WorldYSortSpriteRenderer>();

            blockerObject = new GameObject("PlayerBlocker_Audit_Prop");
            blockerObject.AddComponent<BoxCollider2D>();
            ObjectMovementBlocker2D blocker =
                blockerObject.AddComponent<ObjectMovementBlocker2D>();
            blocker.Configure(
                propObject,
                "Drawing Room",
                "Chair",
                0.3f,
                "Ownership audit regression",
                true);

            string report =
                GreenChairRenderStateDiagnostic.BuildSelectedSortingOwnershipAuditForTests(propObject);

            Assert.That(report, Does.Contain("CONFLICT renderer="));
            Assert.That(report, Does.Contain(nameof(WorldYSortSpriteRenderer)));
            Assert.That(report, Does.Contain(nameof(ObjectMovementBlocker2D)));
        }
        finally
        {
            if (blockerObject != null)
            {
                Object.DestroyImmediate(blockerObject);
            }

            if (propObject != null)
            {
                Object.DestroyImmediate(propObject);
            }
        }
    }

    [Test]
    public void SelectedExternalRailAuditReportsTheActiveSeatedExceptionOwner()
    {
        GameObject roomObject = null;
        GameObject actorObject = null;
        GameObject chairObject = null;
        GameObject railObject = null;

        try
        {
            roomObject = new GameObject("Room_Drawing_Room");
            RoomContentGroup room = roomObject.AddComponent<RoomContentGroup>();
            room.SetRoomName("Drawing Room");
            GameObject seatObject = new GameObject("DrawingRoomGuestPoint_04");
            seatObject.transform.SetParent(roomObject.transform, false);
            RoomAnchor seat = seatObject.AddComponent<RoomAnchor>();
            seat.RefreshFromHierarchy();

            actorObject = new GameObject("Guest 4");
            actorObject.AddComponent<SpriteRenderer>();
            ActorRoomState actorState = actorObject.AddComponent<ActorRoomState>();
            SerializedObject serializedActor = new SerializedObject(actorState);
            serializedActor.FindProperty("restrictVisibilityToCurrentRoom").boolValue = false;
            serializedActor.ApplyModifiedPropertiesWithoutUndo();
            actorState.SetCurrentRoom("Drawing Room");
            actorState.SetAvailableInCurrentChapter(true);
            actorState.SetVisibleByChapterState(true);
            actorState.SetSeated(true);

            chairObject = new GameObject("drawingroomgreenchair_0");
            SpriteRenderer chairRenderer = chairObject.AddComponent<SpriteRenderer>();
            chairRenderer.sortingLayerName = "People";
            chairRenderer.sortingOrder = 1050;
            railObject = new GameObject("drawingroomgreenchair[_0");
            SpriteRenderer railRenderer = railObject.AddComponent<SpriteRenderer>();
            railRenderer.sortingLayerName = "People";
            railRenderer.sortingOrder = 1100;

            DiningRoomSeatedGuestOcclusionException seatedException =
                actorObject.AddComponent<DiningRoomSeatedGuestOcclusionException>();
            seatedException.ActivateBehindOccluder(
                actorState,
                seat,
                chairRenderer,
                railRenderer,
                "Drawing Room",
                "Butler");

            string report =
                GreenChairRenderStateDiagnostic.BuildSelectedSortingOwnershipAuditForTests(railObject);

            Assert.That(report, Does.Contain("narrowExceptionOwners=1"));
            Assert.That(report, Does.Contain(nameof(DiningRoomSeatedGuestOcclusionException)));
            Assert.That(report, Does.Contain("path=Guest 4"));
        }
        finally
        {
            if (railObject != null)
            {
                Object.DestroyImmediate(railObject);
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
    public void SelectedAuditDoesNotAssignChildRendererToNonRecursiveWorldYSorter()
    {
        GameObject rootObject = null;

        try
        {
            rootObject = new GameObject("Non Recursive Sort Root");
            WorldYSortSpriteRenderer sorter =
                rootObject.AddComponent<WorldYSortSpriteRenderer>();
            SerializedObject serializedSorter = new SerializedObject(sorter);
            serializedSorter.FindProperty("includeChildren").boolValue = false;
            serializedSorter.ApplyModifiedPropertiesWithoutUndo();
            GameObject childObject = new GameObject("Unowned Child Renderer");
            childObject.transform.SetParent(rootObject.transform, false);
            childObject.AddComponent<SpriteRenderer>();

            string report =
                GreenChairRenderStateDiagnostic.BuildSelectedSortingOwnershipAuditForTests(rootObject);

            Assert.That(report, Does.Contain("normalSortingOwners=0"));
            Assert.That(report, Does.Not.Contain(nameof(WorldYSortSpriteRenderer) + " path="));
        }
        finally
        {
            if (rootObject != null)
            {
                Object.DestroyImmediate(rootObject);
            }
        }
    }
}
