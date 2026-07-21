using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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
            PlaceActorAt(rig, rig.Anchor);
            AssertActorLockedToAnchor(rig, "initial placement");

            rig.CameraManager.SetRoomLookForPreview(1f, 0f, 0.8f);
            Assert.That(ApplyBinding(rig), Is.True);
            AssertActorLockedToAnchor(rig, "room pan");

            rig.CameraManager.defaultRoomZoom = rig.CameraManager.maxRoomZoom;
            rig.CameraManager.SetRoomLookForPreview(1f, 0f, 0.8f);
            Assert.That(ApplyBinding(rig), Is.True);
            AssertActorLockedToAnchor(rig, "room zoom");

            SetViewportSize(rig.Viewport, 960f, 540f);
            Canvas.ForceUpdateCanvases();
            rig.CameraManager.SetRoomLookForPreview(-0.4f, 0f, 0.8f);
            Assert.That(ApplyBinding(rig), Is.True);
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

            Assert.That(ApplyBinding(rig, stageActorState), Is.False);
            Assert.That(stageActor.transform.localPosition, Is.EqualTo(originalLocalPosition));
        }
        finally
        {
            rig.Destroy();
        }
    }

    [UnityTest]
    public IEnumerator TransformWaypointMovementReleasesRoomStageBindingBeforeFirstStep()
    {
        TestRig rig = CreateRig();
        RectTransform exit = new GameObject("Exit", typeof(RectTransform)).GetComponent<RectTransform>();
        exit.SetParent(rig.Stage, false);
        exit.anchoredPosition = rig.Anchor.anchoredPosition + new Vector2(120f, 0f);

        try
        {
            rig.ActorState.SetCurrentRoom(rig.RoomContent.RoomName);
            PlaceActorAt(rig, rig.Anchor);
            Assert.That(
                ApplyBinding(rig),
                Is.True,
                "The real Chapter 2 guest starts bound to its hide anchor.");
            Vector3 startPosition = rig.ActorState.transform.position;

            NPCWaypointMover mover = rig.ActorState.gameObject.AddComponent<NPCWaypointMover>();
            mover.ConstrainToPlayerFloorBoundary = false;
            mover.MoveSpeed = 1f;
            IEnumerator move = mover.MoveToRoutine(exit);

            Assert.That(move.MoveNext(), Is.True, "The exit waypoint should require at least one movement step.");
            Assert.That(
                ApplyBinding(rig),
                Is.False,
                "Scripted transform movement must release the passive stage binding before LateUpdate can pin the guest.");

            // EditMode can report a zero delta on the exact tick that starts a manually
            // advanced coroutine. Give the real movement loop one editor frame before
            // asserting distance so this test measures movement instead of test-runner timing.
            yield return null;
            Assert.That(move.MoveNext(), Is.True, "The exit walk should continue after its first editor frame.");
            Vector3 positionAfterFirstStep = rig.ActorState.transform.position;
            Assert.That(
                Vector2.Distance(startPosition, positionAfterFirstStep),
                Is.GreaterThan(0.0001f),
                "The visible world-space guest should physically advance on the first exit step.");
            Assert.That(
                ApplyBinding(rig),
                Is.False,
                "The room-stage binding must remain released after the guest physically advances.");

            yield return null;
            Assert.That(
                rig.ActorState.transform.position,
                Is.EqualTo(positionAfterFirstStep),
                "LateUpdate must not restore the moving guest to the released hide anchor.");

            mover.StopMoving();
        }
        finally
        {
            rig.Destroy();
        }
    }

    [UnityTest]
    public IEnumerator SpeechPausedWaypointKeepsRoomStageBindingUntilMovementResumes()
    {
        TestRig rig = CreateRig();
        RectTransform exit = new GameObject("ExitAfterSpeech", typeof(RectTransform)).GetComponent<RectTransform>();
        exit.SetParent(rig.Stage, false);
        exit.anchoredPosition = rig.Anchor.anchoredPosition + new Vector2(120f, 0f);

        try
        {
            rig.ActorState.SetCurrentRoom(rig.RoomContent.RoomName);

            PlaceActorAt(rig, rig.Anchor);
            AssertActorLockedToAnchor(rig, "door spawn before speech pause");

            NPCWaypointMover mover = rig.ActorState.gameObject.AddComponent<NPCWaypointMover>();
            mover.ConstrainToPlayerFloorBoundary = false;
            mover.MoveSpeed = 1f;
            mover.AcquireSpeechPause();
            IEnumerator move = mover.MoveToRoutine(exit);

            Assert.That(move.MoveNext(), Is.True, "The queued walk should wait while the guest speaks.");
            Assert.That(
                ApplyBinding(rig),
                Is.True,
                "Queuing a paused walk must not detach a stationary guest from the door anchor.");
            rig.CameraManager.defaultRoomZoom = rig.CameraManager.maxRoomZoom;
            rig.CameraManager.SetRoomLookForPreview(0.65f, -0.35f, 0.8f);
            Assert.That(ApplyBinding(rig), Is.True);
            AssertActorLockedToAnchor(rig, "door spawn while panning and zooming during speech");

            mover.ReleaseSpeechPause();
            Assert.That(move.MoveNext(), Is.True, "Movement should resume after the speech pause ends.");
            Assert.That(
                ApplyBinding(rig),
                Is.False,
                "The shared mover should release the anchor on the first real movement frame.");

            mover.StopMoving();
            yield return null;
        }
        finally
        {
            Object.DestroyImmediate(exit.gameObject);
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
            Vector3 authoredScale = rig.ActorState.transform.localScale;

            PlaceActorAt(rig, rig.Anchor);
            AssertActorLockedToAnchor(rig, "initial placement");

            rig.CameraManager.defaultRoomZoom = rig.CameraManager.maxRoomZoom;
            rig.CameraManager.SetRoomLookForPreview(1f, 0f, 0.8f);
            Assert.That(ApplyBinding(rig), Is.True);

            AssertActorLockedToAnchor(rig, "room zoom");
            Assert.That(rig.ActorState.transform.localScale, Is.EqualTo(authoredScale));
        }
        finally
        {
            rig.Destroy();
        }
    }

    [Test]
    public void PlannerResolvedWorldFeetBecomeTheStableRoomStageBinding()
    {
        TestRig rig = CreateRig();
        Texture2D bodyTexture = new Texture2D(20, 40);
        Sprite bodySprite = Sprite.Create(
            bodyTexture,
            new Rect(0f, 0f, bodyTexture.width, bodyTexture.height),
            new Vector2(0.5f, 0f),
            20f);
        SpriteRenderer bodyRenderer = rig.ActorState.GetComponent<SpriteRenderer>();
        bodyRenderer.sprite = bodySprite;
        RectTransform resolvedFloorPoint = new GameObject("ResolvedFloorPoint", typeof(RectTransform)).GetComponent<RectTransform>();
        resolvedFloorPoint.SetParent(rig.Stage, false);
        resolvedFloorPoint.anchoredPosition = rig.Anchor.anchoredPosition + new Vector2(-170f, 65f);

        try
        {
            rig.ActorState.SetCurrentRoom(rig.RoomContent.RoomName);
            EnsureRigCameraIsMain(rig);
            AttachCameraTarget(rig);

            try
            {
                Vector3 localFloorPoint = rig.Stage.InverseTransformPoint(resolvedFloorPoint.position);
                Assert.That(
                    rig.CameraManager.TryGetActiveRoomStageWorldPoint(localFloorPoint, 10f, out Vector3 resolvedWorldPoint),
                    Is.True);
                rig.ActorState.transform.position = resolvedWorldPoint;
                Assert.That(
                    rig.ActorState.BindCurrentWorldFootPointToRoomStage(rig.Anchor),
                    Is.True,
                    "The resolved floor endpoint should become the actor's stable room-stage binding.");
            }
            finally
            {
                rig.Camera.targetTexture = null;
            }

            AssertVisibleFeetLockedToAnchor(rig, bodyRenderer, resolvedFloorPoint, "resolved endpoint binding");

            rig.CameraManager.defaultRoomZoom = rig.CameraManager.maxRoomZoom;
            rig.CameraManager.SetRoomLookForPreview(0.7f, -0.35f, 0.8f);
            Assert.That(ApplyBinding(rig), Is.True);
            AssertVisibleFeetLockedToAnchor(rig, bodyRenderer, resolvedFloorPoint, "resolved endpoint pan and zoom");
        }
        finally
        {
            Object.DestroyImmediate(bodySprite);
            Object.DestroyImmediate(bodyTexture);
            rig.Destroy();
        }
    }

    [Test]
    public void RoomStageBindingKeepsActorRootOnTheAnchorWithoutChangingScale()
    {
        TestRig rig = CreateRig();
        Texture2D bodyTexture = new Texture2D(20, 40);
        Sprite bodySprite = Sprite.Create(
            bodyTexture,
            new Rect(0f, 0f, bodyTexture.width, bodyTexture.height),
            new Vector2(0.5f, 0.5f),
            20f);
        SpriteRenderer bodyRenderer = rig.ActorState.GetComponent<SpriteRenderer>();
        bodyRenderer.sprite = bodySprite;

        try
        {
            Vector3 authoredScale = rig.ActorState.transform.localScale;
            rig.ActorState.SetCurrentRoom(rig.RoomContent.RoomName);
            PlaceActorAt(rig, rig.Anchor);
            AssertActorLockedToAnchor(rig, "initial root binding");
            Assert.That(rig.ActorState.transform.localScale, Is.EqualTo(authoredScale));

            rig.CameraManager.defaultRoomZoom = rig.CameraManager.maxRoomZoom;
            rig.CameraManager.SetRoomLookForPreview(0.6f, -0.25f, 0.8f);
            Assert.That(ApplyBinding(rig), Is.True);
            AssertActorLockedToAnchor(rig, "pan and zoom root refresh");
            Assert.That(rig.ActorState.transform.localScale, Is.EqualTo(authoredScale));
        }
        finally
        {
            Object.DestroyImmediate(bodySprite);
            Object.DestroyImmediate(bodyTexture);
            rig.Destroy();
        }
    }

    [Test]
    public void StaticBoundGuestUsesFinalAnimationDisplayScaleWithoutMovingActorRoot()
    {
        TestRig rig = CreateRig();
        Texture2D bodyTexture = new Texture2D(20, 40);
        Sprite bodySprite = Sprite.Create(
            bodyTexture,
            new Rect(0f, 0f, bodyTexture.width, bodyTexture.height),
            new Vector2(0.5f, 0.5f),
            20f);
        SpriteRenderer rootRenderer = rig.ActorState.GetComponent<SpriteRenderer>();

        GameObject visual = new GameObject("AnimationDisplay", typeof(SpriteRenderer));
        visual.transform.SetParent(rig.ActorState.transform, false);
        SpriteRenderer bodyRenderer = visual.GetComponent<SpriteRenderer>();
        bodyRenderer.sprite = bodySprite;

        CharacterDisplayScaleCatalog catalog = ScriptableObject.CreateInstance<CharacterDisplayScaleCatalog>();
        catalog.SetRooms(new[]
        {
            new RoomDisplayScaleEntry(
                rig.RoomContent.RoomName,
                new Vector2(0f, -100f),
                2f,
                new Vector2(0f, 100f),
                2f,
                AnimationCurve.Linear(0f, 0f, 1f, 1f))
        });
        GameObject controllerObject = new GameObject("Character Display Scale Controller Test");
        controllerObject.SetActive(false);
        CharacterDisplayScaleController controller =
            controllerObject.AddComponent<CharacterDisplayScaleController>();
        controller.ConfigureForEditor(catalog);
        CharacterDisplayScaleTestContext context =
            rig.ActorState.gameObject.AddComponent<CharacterDisplayScaleTestContext>();
        context.RoomId = rig.RoomContent.RoomName;
        context.RoomLocalFootY = 0f;
        CharacterDisplayScaleSubject subject =
            rig.ActorState.gameObject.AddComponent<CharacterDisplayScaleSubject>();
        subject.ConfigureForEditor(CharacterDisplayId.Guest1, visual.transform, context);
        controllerObject.SetActive(true);

        try
        {
            Vector3 authoredRootScale = rig.ActorState.transform.localScale;
            rig.ActorState.SetCurrentRoom(rig.RoomContent.RoomName);
            rootRenderer.enabled = false;

            PlaceActorAt(rig, rig.Anchor);
            Assert.That(
                controller.TryApplySubject(subject),
                Is.True,
                "CharacterDisplayScaleController, not ActorRoomState, should apply the target room scale.");

            Assert.That(
                subject.VisualScaleRoot.localScale,
                Is.EqualTo(new Vector3(2f, 2f, 1f)),
                "Static placement must apply the target room's display scale without moving the actor root.");
            AssertActorLockedToAnchor(rig, "scaled static root binding");
            Assert.That(rig.ActorState.transform.localScale, Is.EqualTo(authoredRootScale),
                "Static room-anchor binding must not take ownership of the actor root scale.");
        }
        finally
        {
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(bodySprite);
            Object.DestroyImmediate(bodyTexture);
            rig.Destroy();
        }
    }

    [Test]
    public void StaticRoomAnchorBindingIgnoresAnimationFrameBounds()
    {
        TestRig rig = CreateRig();
        Texture2D shortTexture = new Texture2D(20, 40);
        Texture2D tallTexture = new Texture2D(20, 80);
        Sprite shortFrame = Sprite.Create(
            shortTexture,
            new Rect(0f, 0f, shortTexture.width, shortTexture.height),
            new Vector2(0.5f, 0.5f),
            20f);
        Sprite tallFrame = Sprite.Create(
            tallTexture,
            new Rect(0f, 0f, tallTexture.width, tallTexture.height),
            new Vector2(0.5f, 0.5f),
            20f);
        SpriteRenderer bodyRenderer = rig.ActorState.GetComponent<SpriteRenderer>();

        try
        {
            rig.ActorState.SetCurrentRoom(rig.RoomContent.RoomName);
            bodyRenderer.sprite = shortFrame;
            PlaceActorAt(rig, rig.Anchor);
            AssertActorLockedToAnchor(rig, "short animation frame");
            Vector3 shortFrameRootPosition = rig.ActorState.transform.position;

            bodyRenderer.sprite = tallFrame;
            Assert.That(ApplyBinding(rig), Is.True);
            AssertActorLockedToAnchor(rig, "tall animation frame");
            Assert.That(
                rig.ActorState.transform.position,
                Is.EqualTo(shortFrameRootPosition),
                "Changing animation-frame bounds must never move a statically anchored actor root.");
        }
        finally
        {
            Object.DestroyImmediate(shortFrame);
            Object.DestroyImmediate(tallFrame);
            Object.DestroyImmediate(shortTexture);
            Object.DestroyImmediate(tallTexture);
            rig.Destroy();
        }
    }

    [UnityTest]
    public IEnumerator TransformWaypointMovementFollowsAnchorMovedDuringWalk()
    {
        TestRig rig = CreateRig();
        RectTransform destination = new GameObject("MovingDestination", typeof(RectTransform)).GetComponent<RectTransform>();
        destination.SetParent(rig.Stage, false);
        destination.anchoredPosition = rig.Anchor.anchoredPosition + new Vector2(180f, 0f);

        try
        {
            NPCWaypointMover mover = rig.ActorState.gameObject.AddComponent<NPCWaypointMover>();
            mover.ConstrainToPlayerFloorBoundary = false;
            mover.MoveSpeed = 1000000f;
            IEnumerator move = mover.MoveToRoutine(destination);

            Assert.That(move.MoveNext(), Is.True, "The transform waypoint should begin with a movement frame.");
            destination.anchoredPosition += new Vector2(140f, -70f);

            int guard = 0;

            while (move.MoveNext() && guard++ < 120)
            {
                yield return null;
            }

            Assert.That(guard, Is.LessThan(120), "The moving-anchor regression should finish promptly.");
            Assert.That(
                Vector2.Distance(rig.ActorState.transform.position, destination.position),
                Is.LessThan(0.001f),
                "A transform-based guest must finish at the physical anchor's current position after camera-stage movement.");
        }
        finally
        {
            Object.DestroyImmediate(destination.gameObject);
            rig.Destroy();
        }
    }

    [UnityTest]
    public IEnumerator VisibleFeetWaypointMovementEndsOnTheLivePhysicalAnchor()
    {
        TestRig rig = CreateRig();
        Texture2D bodyTexture = new Texture2D(20, 40);
        Sprite bodySprite = Sprite.Create(
            bodyTexture,
            new Rect(0f, 0f, bodyTexture.width, bodyTexture.height),
            new Vector2(0.5f, 0.5f),
            20f);
        SpriteRenderer bodyRenderer = rig.ActorState.GetComponent<SpriteRenderer>();
        bodyRenderer.sprite = bodySprite;
        RectTransform destination = new GameObject("MovingFootDestination", typeof(RectTransform)).GetComponent<RectTransform>();
        destination.SetParent(rig.Stage, false);
        destination.anchoredPosition = rig.Anchor.anchoredPosition + new Vector2(180f, 0f);

        try
        {
            NPCWaypointMover mover = rig.ActorState.gameObject.AddComponent<NPCWaypointMover>();
            mover.ConstrainToPlayerFloorBoundary = false;
            mover.MoveSpeed = 1000000f;
            mover.AlignVisibleFeetToWaypoints = true;
            IEnumerator move = mover.MoveToRoutine(destination);

            Assert.That(move.MoveNext(), Is.True, "The foot-aligned waypoint should begin with a movement frame.");
            destination.anchoredPosition += new Vector2(140f, -70f);

            int guard = 0;

            while (move.MoveNext() && guard++ < 120)
            {
                yield return null;
            }

            Vector2 visibleFeet = new Vector2(bodyRenderer.bounds.center.x, bodyRenderer.bounds.min.y);
            Assert.That(guard, Is.LessThan(120), "The foot-aligned movement should finish promptly.");
            Assert.That(
                Vector2.Distance(visibleFeet, destination.position),
                Is.LessThan(0.001f),
                "The guest's visible feet, rather than its transform center, should finish on the anchor's current position.");
        }
        finally
        {
            Object.DestroyImmediate(destination.gameObject);
            Object.DestroyImmediate(bodySprite);
            Object.DestroyImmediate(bodyTexture);
            rig.Destroy();
        }
    }

    private static TestRig CreateRig()
    {
        GameObject root = new GameObject("StoryActorRoomStageLockingTestRoot");
        Texture2D texture = new Texture2D(400, 200) { name = "RuntimeRoomTexture" };
        Camera[] existingCameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<GameObject> previousMainCameras = new List<GameObject>();

        for (int i = 0; i < existingCameras.Length; i++)
        {
            Camera existingCamera = existingCameras[i];

            if (existingCamera != null && existingCamera.CompareTag("MainCamera"))
            {
                previousMainCameras.Add(existingCamera.gameObject);
                existingCamera.tag = "Untagged";
            }
        }

        Camera camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.orthographic = true;
        camera.transform.SetParent(root.transform, false);
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.rect = new Rect(0f, 0f, 1f, 1f);
        RenderTexture cameraTarget = new RenderTexture(800, 450, 0) { name = "StoryActorRoomStageTestViewport" };
        cameraTarget.Create();

        Canvas canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.transform.SetParent(root.transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;

        RectTransform viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
        viewport.SetParent(canvas.transform, false);
        SetViewportSize(viewport, 800f, 450f);
        Canvas.ForceUpdateCanvases();

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

        TestRig rig = new TestRig
        {
            Root = root,
            Texture = texture,
            Camera = camera,
            CameraTarget = cameraTarget,
            Canvas = canvas,
            CameraManager = cameraManager,
            Viewport = viewport,
            Stage = stage,
            RoomContent = roomContent,
            Anchor = anchor,
            ActorState = actor.GetComponent<ActorRoomState>(),
            PreviousMainCameras = previousMainCameras
        };

        SetPrivateField(rig.ActorState, "cameraManager", cameraManager);
        SetPrivateField(rig.ActorState, "restrictVisibilityToCurrentRoom", false);
        EnsureRigCameraIsMain(rig);
        return rig;
    }

    private static void SetPrivateField<T>(ActorRoomState actorState, string fieldName, T value)
    {
        FieldInfo field = typeof(ActorRoomState).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"ActorRoomState test fixture field '{fieldName}' must remain available.");
        field.SetValue(actorState, value);
    }

    private static void SetViewportSize(RectTransform viewport, float width, float height)
    {
        viewport.anchorMin = new Vector2(0.5f, 0.5f);
        viewport.anchorMax = new Vector2(0.5f, 0.5f);
        viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.anchoredPosition = Vector2.zero;
        viewport.sizeDelta = new Vector2(width, height);
    }

    private static bool ApplyBinding(TestRig rig, ActorRoomState actorState = null)
    {
        Assert.That(ApplyBindingMethod, Is.Not.Null, "ActorRoomState binding method should remain available to this regression test.");
        EnsureRigCameraIsMain(rig);
        AttachCameraTarget(rig);

        try
        {
            return (bool)ApplyBindingMethod.Invoke(actorState != null ? actorState : rig.ActorState, null);
        }
        finally
        {
            rig.Camera.targetTexture = null;
        }
    }

    private static void PlaceActorAt(TestRig rig, Transform target)
    {
        EnsureRigCameraIsMain(rig);
        AttachCameraTarget(rig);

        try
        {
            rig.ActorState.PlaceAt(target);
        }
        finally
        {
            rig.Camera.targetTexture = null;
        }
    }

    private static void EnsureRigCameraIsMain(TestRig rig)
    {
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];

            if (camera != null && camera != rig.Camera && camera.CompareTag("MainCamera"))
            {
                if (!rig.PreviousMainCameras.Contains(camera.gameObject))
                {
                    rig.PreviousMainCameras.Add(camera.gameObject);
                }

                camera.tag = "Untagged";
            }
        }

        rig.Camera.tag = "MainCamera";
        rig.Camera.rect = new Rect(0f, 0f, 1f, 1f);
        Assert.That(Camera.main, Is.SameAs(rig.Camera), "The room-stage test must own Camera.main regardless of earlier fixtures.");
    }

    private static void AttachCameraTarget(TestRig rig)
    {
        if (!rig.CameraTarget.IsCreated())
        {
            rig.CameraTarget.Create();
        }

        rig.Camera.targetTexture = rig.CameraTarget;
        rig.Camera.rect = new Rect(0f, 0f, 1f, 1f);
        Assert.That(rig.CameraTarget.IsCreated(), Is.True, "The room-stage test render target must be created.");
        Assert.That(rig.Camera.pixelWidth, Is.GreaterThan(1), "The room-stage test camera needs a deterministic render width.");
        Assert.That(rig.Camera.pixelHeight, Is.GreaterThan(1), "The room-stage test camera needs a deterministic render height.");
    }

    private static void AssertActorLockedToAnchor(TestRig rig, string context)
    {
        AttachCameraTarget(rig);

        try
        {
            Vector2 actorScreen = rig.Camera.WorldToScreenPoint(rig.ActorState.transform.position);
            Vector2 anchorScreen = RectTransformUtility.WorldToScreenPoint(rig.Canvas.worldCamera, rig.Anchor.position);
            Assert.That(Vector2.Distance(actorScreen, anchorScreen), Is.LessThanOrEqualTo(ScreenLockTolerance), context);
        }
        finally
        {
            rig.Camera.targetTexture = null;
        }
    }

    private static void AssertVisibleFeetLockedToAnchor(
        TestRig rig,
        SpriteRenderer renderer,
        RectTransform anchor,
        string context)
    {
        AttachCameraTarget(rig);

        try
        {
            Vector3 feetWorld = new Vector3(renderer.bounds.center.x, renderer.bounds.min.y, renderer.bounds.center.z);
            Vector2 feetScreen = rig.Camera.WorldToScreenPoint(feetWorld);
            Vector2 anchorScreen = RectTransformUtility.WorldToScreenPoint(rig.Canvas.worldCamera, anchor.position);
            Assert.That(Vector2.Distance(feetScreen, anchorScreen), Is.LessThanOrEqualTo(ScreenLockTolerance), context);
        }
        finally
        {
            rig.Camera.targetTexture = null;
        }
    }

    private sealed class TestRig
    {
        public GameObject Root;
        public Texture2D Texture;
        public Camera Camera;
        public RenderTexture CameraTarget;
        public Canvas Canvas;
        public CameraManager CameraManager;
        public RectTransform Viewport, Stage, Anchor;
        public RoomContentGroup RoomContent;
        public ActorRoomState ActorState;
        public List<GameObject> PreviousMainCameras;

        public void Destroy()
        {
            Camera.targetTexture = null;
            Object.DestroyImmediate(Root);
            Object.DestroyImmediate(Texture);
            Object.DestroyImmediate(CameraTarget);
            for (int i = 0; i < PreviousMainCameras.Count; i++)
            {
                if (PreviousMainCameras[i] != null)
                {
                    PreviousMainCameras[i].tag = "MainCamera";
                }
            }
        }
    }
}
