using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

public sealed class CharacterDepthLifecycleTests
{
    private const string RoomName = "Depth Lifecycle Test";
    private SceneSetup[] previousSceneSetup;
    private float previousTimeScale;

    [UnitySetUp]
    public IEnumerator EnterIsolatedPlayMode()
    {
        previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        previousTimeScale = Time.timeScale;
        Time.timeScale = 1f;
    }

    [UnityTearDown]
    public IEnumerator RestoreEditorScene()
    {
        if (EditorApplication.isPlaying)
        {
            Time.timeScale = previousTimeScale;
            yield return new ExitPlayMode();
        }

        if (previousSceneSetup != null && previousSceneSetup.Length > 0)
            EditorSceneManager.RestoreSceneManagerSetup(previousSceneSetup);
        previousSceneSetup = null;
    }

    [UnityTest]
    public IEnumerator FirstEnableInitializesDepthBeforeSeatingAndPreservesTheAuthoredPose()
    {
        using (LifecycleFixture fixture = new LifecycleFixture())
        {
            ActorRig actor = fixture.CreateActor("Guest4", true);
            GameObject hiddenCoat = new GameObject("Stored coat sibling", typeof(SpriteRenderer));
            hiddenCoat.transform.SetParent(actor.Root.transform, false);
            hiddenCoat.GetComponent<SpriteRenderer>().sprite = fixture.ProbeSprite;
            hiddenCoat.SetActive(false);

            // Configure the inactive actor as a serialized scene actor. The test
            // never calls CharacterDepthGroup.EnsureForActor or RefreshDepth.
            actor.Root.SetActive(true);
            actor.State.SetSeated(true);

            CharacterDepthGroup depth = actor.Root.GetComponent<CharacterDepthGroup>();
            Assert.That(depth, Is.Not.Null, "The actor must acquire depth during activation, before its first LateUpdate.");
            Assert.That(depth.Group, Is.Not.Null, "The sorting wrapper must exist before the first seated command.");
            Assert.That(actor.Presenter.BodyRenderer, Is.SameAs(actor.Body),
                "A stored coat must not become the character's selected body after wrapping.");
            Assert.That(actor.Animator.GetBool("IsCrouching"), Is.True);

            Transform wrapper = depth.Group.transform;
            bool sawAuthoredSittingSprite = false;
            float deadline = Time.realtimeSinceStartup + 1.5f;
            int completedFrames = 0;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                completedFrames++;
                Assert.That(actor.State.IsSeated, Is.True);
                Assert.That(actor.Presenter.IsSeated, Is.True);
                Assert.That(actor.Animator.GetBool("IsCrouching"), Is.True,
                    "Depth initialization must not reset the seated Animator parameter in a later frame.");
                Assert.That(actor.Display.AnimationDisplay.parent, Is.SameAs(wrapper));
                Assert.That(actor.Root.GetComponentsInChildren<SortingGroup>(true).Length, Is.EqualTo(1));
                AssertFloorAndGroupAgree(actor, depth);

                string spriteGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(actor.Body.sprite));
                sawAuthoredSittingSprite |= spriteGuid == "3c77696129c44fc49b56737e0ffdc4e9" ||
                    spriteGuid == "68e57e21c4bd49ebbc1faec9dc2d6830";
                if (sawAuthoredSittingSprite && completedFrames >= 4) break;
            }

            Assert.That(sawAuthoredSittingSprite, Is.True,
                "The real Countess controller must reach an authored yellow-dress sitting sprite after activation.");
        }
    }

    [UnityTest]
    public IEnumerator ScalingPreservesFloorAndCoatThenSeatReleaseRefreshesBothActorsImmediately()
    {
        using (LifecycleFixture fixture = new LifecycleFixture())
        {
            ActorRig actor = fixture.CreateActor("Guest4", false);
            ActorRig preserved = fixture.CreateActor("Guest2", false);
            actor.Root.SetActive(true);
            preserved.Root.SetActive(true);
            yield return null;
            yield return null;

            CharacterDepthGroup depth = actor.Root.GetComponent<CharacterDepthGroup>();
            CharacterDepthGroup preservedDepth = preserved.Root.GetComponent<CharacterDepthGroup>();
            Assert.That(depth, Is.Not.Null);
            Assert.That(preservedDepth, Is.Not.Null);
            Vector3 originalFloor = actor.Floor.WorldPoint;
            Vector3 rootScale = actor.Root.transform.localScale;
            Vector3 bodySize = actor.Body.bounds.size;
            Vector3 coatSize = actor.Coat.bounds.size;
            Transform wrapper = depth.Group.transform;

            fixture.SetScale(2f);
            fixture.AssertScaleConfiguration(actor, 2f);
            fixture.AssertScaleConfiguration(preserved, 2f);
            // EditMode test-runner updates can outpace player frames. Wait for
            // the observable scale/depth result instead of counting two yields.
            float scaleDeadline = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < scaleDeadline &&
                (actor.Display.AnimationDisplay.localScale.x != 2f ||
                 Vector3.Distance(depth.Group.transform.position, actor.Floor.WorldPoint) > 0.0001f))
                yield return null;

            Assert.That(Vector3.Distance(actor.Floor.WorldPoint, originalFloor), Is.LessThan(0.0001f));
            Assert.That(actor.Root.transform.localScale, Is.EqualTo(rootScale));
            Assert.That(actor.Display.AnimationDisplay.localScale, Is.EqualTo(new Vector3(2f, 2f, 1f)));
            Assert.That(actor.Body.bounds.size.x, Is.EqualTo(bodySize.x * 2f).Within(0.0001f));
            Assert.That(actor.Body.bounds.size.y, Is.EqualTo(bodySize.y * 2f).Within(0.0001f));
            Assert.That(actor.Coat.bounds.size.x, Is.EqualTo(coatSize.x * 2f).Within(0.0001f));
            Assert.That(actor.Coat.transform.parent, Is.SameAs(actor.Display.AnimationDisplay));
            Assert.That(actor.Coat.sortingOrder, Is.EqualTo(actor.Body.sortingOrder + 1));
            Assert.That(actor.Display.AnimationDisplay.parent, Is.SameAs(wrapper));
            AssertFloorAndGroupAgree(actor, depth);

            RoomAnchor seat = new GameObject("Depth lifecycle seat", typeof(RoomAnchor)).GetComponent<RoomAnchor>();
            seat.transform.SetParent(fixture.Room.transform, false);
            seat.RefreshFromHierarchy();
            SpriteRenderer chair = fixture.CreateProp("Depth lifecycle chair", 1600);
            SpriteRenderer foreground = fixture.CreateProp("Depth lifecycle chair front", 1601);
            actor.State.SetSeated(true);
            preserved.State.SetSeated(true);
            DiningRoomSeatedGuestOcclusionException exception =
                actor.Root.AddComponent<DiningRoomSeatedGuestOcclusionException>();
            exception.ActivateBehindOccluder(actor.State, preserved.State, seat,
                chair, foreground, RoomName, "Butler");
            Assert.That(exception.IsExceptionActive, Is.True);
            Assert.That(depth.Group.enabled, Is.False);
            Assert.That(preservedDepth.Group.enabled, Is.False);

            int capturedActorOrder = depth.Group.sortingOrder;
            int capturedPreservedOrder = preservedDepth.Group.sortingOrder;
            actor.Floor.AlignActorToWorldPoint(new Vector2(0f, -2f));
            preserved.Floor.AlignActorToWorldPoint(new Vector2(0f, -3f));

            // Reproduce the player-loop ordering explicitly: normal world sorting
            // at 20000, group at 20500, then the exception releases at 21000.
            actor.Sorter.ApplySorting();
            preserved.Sorter.ApplySorting();
            depth.RefreshDepth();
            preservedDepth.RefreshDepth();
            int expectedActorOrder = actor.Body.sortingOrder;
            int expectedPreservedOrder = preserved.Body.sortingOrder;
            Assert.That(expectedActorOrder, Is.Not.EqualTo(capturedActorOrder));
            Assert.That(expectedPreservedOrder, Is.Not.EqualTo(capturedPreservedOrder));

            actor.State.SetSeated(false);
            preserved.State.SetSeated(false);
            exception.ApplyOcclusionNow();

            Assert.That(exception.IsExceptionActive, Is.False);
            Assert.That(depth.Group.enabled, Is.True);
            Assert.That(preservedDepth.Group.enabled, Is.True);
            Assert.That(depth.Group.sortingOrder, Is.EqualTo(expectedActorOrder),
                "Seat release must refresh external group depth immediately, without another frame.");
            Assert.That(preservedDepth.Group.sortingOrder, Is.EqualTo(expectedPreservedOrder),
                "The preserved-behind actor must also leave the seated override in this frame.");
            Assert.That(depth.Group.sortingLayerID, Is.EqualTo(actor.Body.sortingLayerID));
            Assert.That(preservedDepth.Group.sortingLayerID, Is.EqualTo(preserved.Body.sortingLayerID));
            AssertFloorAndGroupAgree(actor, depth);
            AssertFloorAndGroupAgree(preserved, preservedDepth);
        }
    }

    private static void AssertFloorAndGroupAgree(ActorRig actor, CharacterDepthGroup depth)
    {
        Assert.That(Vector3.Distance(depth.Group.transform.position, actor.Floor.WorldPoint), Is.LessThan(0.0001f));
        Assert.That(depth.Group.sortAtRoot, Is.False);
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(target, value);
    }

    private sealed class ActorRig
    {
        public GameObject Root;
        public SpriteRenderer Body;
        public SpriteRenderer Coat;
        public Animator Animator;
        public ActorRoomState State;
        public CharacterAnimationDisplay Display;
        public CharacterAnimationPresenter Presenter;
        public CharacterFloorReference Floor;
        public WorldYSortSpriteRenderer Sorter;
    }

    private sealed class LifecycleFixture : System.IDisposable
    {
        private readonly GameObject root = new GameObject("Depth lifecycle fixture");
        private readonly CharacterScaleCatalog catalog = ScriptableObject.CreateInstance<CharacterScaleCatalog>();
        private readonly Texture2D texture = new Texture2D(16, 16);
        private readonly PointClickPlayerMovement sortingSource;
        public readonly Sprite ProbeSprite;
        public readonly RoomContentGroup Room;

        public LifecycleFixture()
        {
            ProbeSprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f);
            GameObject roomObject = new GameObject("Room_Depth_Lifecycle_Test", typeof(RoomContentGroup), typeof(CharacterScaleRoom));
            roomObject.transform.SetParent(root.transform, false);
            Room = roomObject.GetComponent<RoomContentGroup>();
            roomObject.GetComponent<CharacterScaleRoom>().ConfigureHandles(Room, null, null);
            GameObject source = new GameObject("Depth lifecycle sorting source");
            source.transform.SetParent(root.transform, false);
            source.SetActive(false);
            sortingSource = source.AddComponent<PointClickPlayerMovement>();
            SetScale(1f);
        }

        public void SetScale(float scale)
        {
            catalog.SetRooms(new[] { new CharacterScaleRoomDefinition(RoomName, -4f, scale, 4f, scale) });
        }

        public void AssertScaleConfiguration(ActorRig actor, float expectedScale)
        {
            Assert.That(Room.RoomName, Is.EqualTo(RoomName));
            Assert.That(actor.State.CurrentRoomId, Is.EqualTo(RoomName));
            Assert.That(actor.Display.isActiveAndEnabled, Is.True);
            Assert.That(actor.Display.Catalog, Is.SameAs(catalog));
            CharacterScaleRoom roomScale = Room.GetComponent<CharacterScaleRoom>();
            Assert.That(roomScale.RoomName, Is.EqualTo(RoomName));
            Assert.That(roomScale.TryGetCharacterRoomY(actor.Floor.WorldPoint, out float roomY), Is.True);
            Assert.That(catalog.TryEvaluateScaleAtRoomY(RoomName, roomY, roomScale.CurrentStageScale, out float scale), Is.True);
            Assert.That(scale, Is.EqualTo(expectedScale).Within(0.0001f));
        }

        public ActorRig CreateActor(string id, bool realAnimator)
        {
            ActorRig actor = new ActorRig { Root = new GameObject(id) };
            actor.Root.SetActive(false);
            actor.Root.transform.SetParent(Room.transform, false);
            GameObject visual = new GameObject("AnimationDisplay", typeof(SpriteRenderer));
            visual.transform.SetParent(actor.Root.transform, false);
            actor.Body = visual.GetComponent<SpriteRenderer>();
            actor.Body.sprite = realAnimator
                ? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/guest4_no_white_artifacts/countess_elowen_dusk_idle_down_03.png")
                : ProbeSprite;
            Assert.That(actor.Body.sprite, Is.Not.Null);
            actor.Body.sortingLayerName = "People";
            actor.Body.sortingOrder = 1000;
            GameObject coat = new GameObject("Carried coat", typeof(SpriteRenderer));
            coat.transform.SetParent(visual.transform, false);
            actor.Coat = coat.GetComponent<SpriteRenderer>();
            actor.Coat.sprite = ProbeSprite;
            actor.Coat.sortingLayerName = "People";
            actor.Coat.sortingOrder = 1001;

            if (realAnimator)
            {
                actor.Animator = visual.AddComponent<Animator>();
                actor.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                actor.Animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/Animation/CountessElowenDusk/CountessElowenDusk.overrideController");
                Assert.That(actor.Animator.runtimeAnimatorController, Is.Not.Null);
            }

            actor.State = actor.Root.AddComponent<ActorRoomState>();
            SetField(actor.State, "actorId", id);
            SetField(actor.State, "currentRoomId", RoomName);
            SetField(actor.State, "restrictVisibilityToCurrentRoom", false);
            actor.Display = actor.Root.AddComponent<CharacterAnimationDisplay>();
            SetField(actor.Display, "animationDisplay", visual.transform);
            SetField(actor.Display, "catalog", catalog);
            actor.Presenter = actor.Root.AddComponent<CharacterAnimationPresenter>();
            actor.Sorter = actor.Root.AddComponent<WorldYSortSpriteRenderer>();
            actor.Sorter.ConfigureForActor(sortingSource, actor.Body);
            // Production coat attachment explicitly registers its body-relative
            // layer; an unconfigured sorter's Reset can flatten authored orders.
            Assert.That(actor.Sorter.RegisterActorRenderer(actor.Coat, 1), Is.True);
            actor.Floor = actor.Sorter.ActorFloorReference;
            return actor;
        }

        public SpriteRenderer CreateProp(string name, int order)
        {
            GameObject prop = new GameObject(name, typeof(SpriteRenderer));
            prop.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = prop.GetComponent<SpriteRenderer>();
            renderer.sprite = ProbeSprite;
            renderer.sortingLayerName = "People";
            renderer.sortingOrder = order;
            return renderer;
        }

        public void Dispose()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(ProbeSprite);
            Object.DestroyImmediate(texture);
        }
    }
}
