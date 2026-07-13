using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class StoryActorRoomStageLockingTests
{
    private const float ScreenLockTolerance = 0.75f;
    private static readonly MethodInfo ApplyBindingMethod = typeof(ActorRoomState).GetMethod(
        "TryApplyRoomStageLocalBindingIfNeeded",
        BindingFlags.Instance | BindingFlags.NonPublic);

    [Test]
    public void WorldActorBindingStaysLockedAcrossPanZoomAndResize()
    {
        TestRig rig = CreateRig();

        try
        {
            rig.ActorState.SetCurrentRoom(rig.RoomContent.RoomName);
            rig.ActorState.PlaceAt(rig.Anchor);
            AssertActorLockedToAnchor(rig, "initial placement");

            rig.CameraManager.SetRoomLookForPreview(1f, 0f, 0.8f);
            Assert.That(ApplyBinding(rig.ActorState), Is.True);
            AssertActorLockedToAnchor(rig, "room pan");

            rig.CameraManager.defaultRoomZoom = rig.CameraManager.maxRoomZoom;
            rig.CameraManager.SetRoomLookForPreview(1f, 0f, 0.8f);
            Assert.That(ApplyBinding(rig.ActorState), Is.True);
            AssertActorLockedToAnchor(rig, "room zoom");

            SetViewportSize(rig.Viewport, 960f, 540f);
            Canvas.ForceUpdateCanvases();
            rig.CameraManager.SetRoomLookForPreview(-0.4f, 0f, 0.8f);
            Assert.That(ApplyBinding(rig.ActorState), Is.True);
            AssertActorLockedToAnchor(rig, "viewport resize");

            GameObject stageActor = new GameObject("StageOwnedActor");
            stageActor.transform.SetParent(rig.Stage, false);
            stageActor.transform.localPosition = new Vector3(24f, -12f, 0f);
            ActorRoomState stageActorState = stageActor.AddComponent<ActorRoomState>();
            stageActorState.SetCurrentRoom(rig.RoomContent.RoomName);
            stageActorState.BindToRoomStagePoint(rig.Anchor);
            Vector3 originalLocalPosition = stageActor.transform.localPosition;

            rig.CameraManager.defaultRoomZoom = rig.CameraManager.maxRoomZoom;
            rig.CameraManager.SetRoomLookForPreview(1f, 0f, 0.8f);

            Assert.That(ApplyBinding(stageActorState), Is.False);
            Assert.That(stageActor.transform.localPosition, Is.EqualTo(originalLocalPosition));
        }
        finally
        {
            rig.Destroy();
        }
    }

    [Test]
    public void TransformWaypointMovementReleasesRoomStageBindingBeforeFirstStep()
    {
        TestRig rig = CreateRig();
        RectTransform exit = new GameObject("Exit", typeof(RectTransform)).GetComponent<RectTransform>();
        exit.SetParent(rig.Stage, false);
        exit.anchoredPosition = rig.Anchor.anchoredPosition + new Vector2(120f, 0f);

        try
        {
            rig.ActorState.SetCurrentRoom(rig.RoomContent.RoomName);
            rig.ActorState.PlaceAt(rig.Anchor);
            Assert.That(
                ApplyBinding(rig.ActorState),
                Is.True,
                "The real Chapter 2 guest starts bound to its hide anchor.");
            Vector3 startPosition = rig.ActorState.transform.position;

            NPCWaypointMover mover = rig.ActorState.gameObject.AddComponent<NPCWaypointMover>();
            mover.MoveSpeed = 100f;
            IEnumerator move = mover.MoveToRoutine(exit);

            Assert.That(move.MoveNext(), Is.True, "The exit waypoint should require at least one movement step.");
            Vector3 positionAfterFirstStep = rig.ActorState.transform.position;
            Assert.That(
                Vector2.Distance(startPosition, positionAfterFirstStep),
                Is.GreaterThan(0.0001f),
                "The visible world-space guest should physically advance on the first exit step.");
            Assert.That(
                ApplyBinding(rig.ActorState),
                Is.False,
                "Scripted transform movement must release the passive stage binding before LateUpdate can pin the guest.");
            Assert.That(
                rig.ActorState.transform.position,
                Is.EqualTo(positionAfterFirstStep),
                "The released binding must not restore the guest to the hide anchor.");

            mover.StopMoving();
        }
        finally
        {
            rig.Destroy();
        }
    }

    [Test]
    public void TransformWaypointMovementTracksTargetMovedDuringWalk()
    {
        GameObject actor = new GameObject("MovingTargetActor", typeof(NPCWaypointMover));
        GameObject target = new GameObject("MovingTarget");
        actor.transform.position = Vector3.zero;
        target.transform.position = new Vector3(1000f, 0f, 0f);

        try
        {
            NPCWaypointMover mover = actor.GetComponent<NPCWaypointMover>();
            mover.MoveSpeed = 100f;
            IEnumerator move = mover.MoveToRoutine(target.transform);

            Assert.That(move.MoveNext(), Is.True);
            float positionAfterFirstStep = actor.transform.position.x;
            target.transform.position = new Vector3(-1000f, 0f, 0f);

            Assert.That(move.MoveNext(), Is.True);
            Assert.That(
                actor.transform.position.x,
                Is.LessThan(positionAfterFirstStep),
                "Transform movement should pursue the room-stage target's current world position each step.");

            mover.StopMoving();
        }
        finally
        {
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void DisabledTransformWaypointPlacementReleasesRoomStageBinding()
    {
        TestRig rig = CreateRig();
        RectTransform exit = new GameObject("InstantExit", typeof(RectTransform)).GetComponent<RectTransform>();
        exit.SetParent(rig.Stage, false);
        exit.anchoredPosition = rig.Anchor.anchoredPosition + new Vector2(120f, 0f);

        try
        {
            rig.ActorState.SetCurrentRoom(rig.RoomContent.RoomName);
            rig.ActorState.PlaceAt(rig.Anchor);
            Assert.That(ApplyBinding(rig.ActorState), Is.True);

            NPCWaypointMover mover = rig.ActorState.gameObject.AddComponent<NPCWaypointMover>();
            mover.enabled = false;
            Vector3 expectedPosition = exit.position;
            expectedPosition.z = rig.ActorState.transform.position.z;

            Assert.That(mover.MoveTo(exit), Is.Null);
            Assert.That(rig.ActorState.transform.position, Is.EqualTo(expectedPosition));
            Assert.That(
                ApplyBinding(rig.ActorState),
                Is.False,
                "Instant transform placement must release the old room-stage anchor binding.");
            Assert.That(rig.ActorState.transform.position, Is.EqualTo(expectedPosition));
        }
        finally
        {
            rig.Destroy();
        }
    }

    [Test]
    public void WorldActorCanKeepAuthoredScaleWhileLockedToRoomStage()
    {
        TestRig rig = CreateRig();

        try
        {
            rig.ActorState.SetCurrentRoom(rig.RoomContent.RoomName);
            rig.ActorState.SetScaleWithRoomStageMotion(false);
            Vector3 authoredScale = rig.ActorState.transform.localScale;

            rig.ActorState.PlaceAt(rig.Anchor);
            AssertActorLockedToAnchor(rig, "initial placement");

            rig.CameraManager.defaultRoomZoom = rig.CameraManager.maxRoomZoom;
            rig.CameraManager.SetRoomLookForPreview(1f, 0f, 0.8f);
            Assert.That(ApplyBinding(rig.ActorState), Is.True);

            AssertActorLockedToAnchor(rig, "room zoom");
            Assert.That(rig.ActorState.transform.localScale, Is.EqualTo(authoredScale));
        }
        finally
        {
            rig.Destroy();
        }
    }

    [Test]
    public void WorldActorUsesAuthoredScaleAsRoomZoomBaseline()
    {
        TestRig rig = CreateRig();

        try
        {
            rig.ActorState.SetCurrentRoom(rig.RoomContent.RoomName);
            Vector3 authoredScale = rig.ActorState.transform.localScale;

            rig.ActorState.PlaceAt(rig.Anchor);
            AssertActorLockedToAnchor(rig, "initial placement");

            float boundStageScale = rig.Stage.lossyScale.x;
            rig.CameraManager.defaultRoomZoom = rig.CameraManager.maxRoomZoom;
            rig.CameraManager.SetRoomLookForPreview(1f, 0f, 0.8f);
            Assert.That(ApplyBinding(rig.ActorState), Is.True);

            float zoomRatio = rig.Stage.lossyScale.x / Mathf.Max(0.0001f, boundStageScale);
            Vector3 expectedScale = new Vector3(
                authoredScale.x * zoomRatio,
                authoredScale.y * zoomRatio,
                authoredScale.z);

            AssertActorLockedToAnchor(rig, "room zoom");
            Assert.That(rig.ActorState.transform.localScale.x, Is.EqualTo(expectedScale.x).Within(0.0001f));
            Assert.That(rig.ActorState.transform.localScale.y, Is.EqualTo(expectedScale.y).Within(0.0001f));
            Assert.That(rig.ActorState.transform.localScale.z, Is.EqualTo(expectedScale.z).Within(0.0001f));
        }
        finally
        {
            rig.Destroy();
        }
    }

    [Test]
    public void WorldActorBindingUsesRoomPerspectiveProfileScale()
    {
        TestRig rig = CreateRig();
        RoomPerspectiveProfile profile = ScriptableObject.CreateInstance<RoomPerspectiveProfile>();
        profile.Configure(
            rig.RoomContent.RoomName,
            new Vector2(400f, 200f),
            -100f,
            100f,
            AnimationCurve.Linear(0f, 1.25f, 1f, 0.75f),
            null,
            1000,
            8000,
            AnimationCurve.Linear(0f, 1f, 1f, 0f));
        rig.RoomContent.SetPerspectiveProfile(profile);

        try
        {
            rig.ActorState.SetCurrentRoom(rig.RoomContent.RoomName);
            Vector3 authoredScale = rig.ActorState.transform.localScale;

            rig.ActorState.PlaceAt(rig.Anchor);
            Assert.That(ApplyBinding(rig.ActorState), Is.True);

            float expectedPerspectiveScale = profile.GetScale(rig.Anchor.anchoredPosition);
            AssertActorLockedToAnchor(rig, "profile-scaled placement");
            Assert.That(rig.ActorState.transform.localScale.x, Is.EqualTo(authoredScale.x * expectedPerspectiveScale).Within(0.0001f));
            Assert.That(rig.ActorState.transform.localScale.y, Is.EqualTo(authoredScale.y * expectedPerspectiveScale).Within(0.0001f));
            Assert.That(rig.ActorState.transform.localScale.z, Is.EqualTo(authoredScale.z).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(profile);
            rig.Destroy();
        }
    }

    private static TestRig CreateRig()
    {
        GameObject root = new GameObject("StoryActorRoomStageLockingTestRoot");
        Texture2D texture = new Texture2D(400, 200) { name = "RuntimeRoomTexture" };
        GameObject[] previousMainCameras = GameObject.FindGameObjectsWithTag("MainCamera");

        for (int i = 0; i < previousMainCameras.Length; i++)
        {
            previousMainCameras[i].tag = "Untagged";
        }

        Camera camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.orthographic = true;
        camera.transform.SetParent(root.transform, false);
        camera.transform.position = new Vector3(0f, 0f, -10f);

        Canvas canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.transform.SetParent(root.transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        RectTransform viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
        viewport.SetParent(canvas.transform, false);
        SetViewportSize(viewport, 800f, 450f);

        RectTransform stage = new GameObject("Room_Test_Room", typeof(RectTransform), typeof(RoomContentGroup)).GetComponent<RectTransform>();
        stage.SetParent(viewport, false);
        RoomContentGroup roomContent = stage.GetComponent<RoomContentGroup>();
        roomContent.SetRoomBackgroundTexture(texture);

        RawImage background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
        background.transform.SetParent(canvas.transform, false);
        background.texture = texture;

        RectTransform anchor = new GameObject("Anchor", typeof(RectTransform)).GetComponent<RectTransform>();
        anchor.SetParent(stage, false);
        anchor.localPosition = new Vector3(80f, -30f, 0f);

        CameraManager cameraManager = new GameObject("CameraManager").AddComponent<CameraManager>();
        cameraManager.transform.SetParent(root.transform, false);
        cameraManager.cameraBackground = background;
        cameraManager.panRoomWithMouseEdges = false;
        cameraManager.zoomRoomWithMouseWheel = false;
        cameraManager.SetActiveRoomContent(roomContent, false);
        cameraManager.SetRoomLookForPreview(0f, 0f, 0.8f);

        GameObject actor = new GameObject("WorldStoryActor", typeof(SpriteRenderer), typeof(ActorRoomState));
        actor.transform.SetParent(root.transform, false);
        actor.transform.position = Vector3.zero;
        actor.transform.localScale = new Vector3(1.2f, 0.8f, 1f);

        return new TestRig
        {
            Root = root,
            Texture = texture,
            Camera = camera,
            CameraManager = cameraManager,
            Viewport = viewport,
            Stage = stage,
            RoomContent = roomContent,
            Anchor = anchor,
            ActorState = actor.GetComponent<ActorRoomState>(),
            PreviousMainCameras = previousMainCameras
        };
    }

    private static void SetViewportSize(RectTransform viewport, float width, float height)
    {
        viewport.anchorMin = new Vector2(0.5f, 0.5f);
        viewport.anchorMax = new Vector2(0.5f, 0.5f);
        viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.anchoredPosition = Vector2.zero;
        viewport.sizeDelta = new Vector2(width, height);
    }

    private static bool ApplyBinding(ActorRoomState actorState)
    {
        Assert.That(ApplyBindingMethod, Is.Not.Null, "ActorRoomState binding method should remain available to this regression test.");
        return (bool)ApplyBindingMethod.Invoke(actorState, null);
    }

    private static void AssertActorLockedToAnchor(TestRig rig, string context)
    {
        Vector2 actorScreen = rig.Camera.WorldToScreenPoint(rig.ActorState.transform.position);
        Vector2 anchorScreen = RectTransformUtility.WorldToScreenPoint(null, rig.Anchor.position);
        Assert.That(Vector2.Distance(actorScreen, anchorScreen), Is.LessThanOrEqualTo(ScreenLockTolerance), context);
    }

    private sealed class TestRig
    {
        public GameObject Root;
        public Texture2D Texture;
        public Camera Camera;
        public CameraManager CameraManager;
        public RectTransform Viewport, Stage, Anchor;
        public RoomContentGroup RoomContent;
        public ActorRoomState ActorState;
        public GameObject[] PreviousMainCameras;

        public void Destroy()
        {
            Object.DestroyImmediate(Root);
            Object.DestroyImmediate(Texture);

            for (int i = 0; i < PreviousMainCameras.Length; i++)
            {
                if (PreviousMainCameras[i] != null)
                {
                    PreviousMainCameras[i].tag = "MainCamera";
                }
            }
        }
    }
}
