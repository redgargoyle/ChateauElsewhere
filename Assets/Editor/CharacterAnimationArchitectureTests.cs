using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class CharacterAnimationArchitectureTests
{
    private const string ActorRoomStatePath = "Assets/Scripts/Story/ActorRoomState.cs";
    private const string NPCWaypointMoverPath = "Assets/Scripts/Story/NPCWaypointMover.cs";
    private const string Chapter1ArrivalControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs";
    private const string Chapter2GuestPanicControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestPanicController.cs";
    private const string Chapter2GuestSearchControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestSearchController.cs";

    [Test]
    public void GuestAnimationRuntimeRoutesThroughSinglePresenter()
    {
        string actorRoomStateText = File.ReadAllText(ActorRoomStatePath);
        string waypointMoverText = File.ReadAllText(NPCWaypointMoverPath);
        string chapter1Text = File.ReadAllText(Chapter1ArrivalControllerPath);
        string panicText = File.ReadAllText(Chapter2GuestPanicControllerPath);

        Assert.That(
            actorRoomStateText,
            Does.Contain("CharacterAnimationPresenter"),
            "ActorRoomState should ask the character animation presenter for seated/reset pose changes instead of writing Animator booleans directly.");
        Assert.That(
            waypointMoverText,
            Does.Contain("animationPresenter.ApplyMovement"),
            "NPCWaypointMover should request walking/idle animation through the presenter.");
        Assert.That(
            waypointMoverText,
            Does.Match(@"if \(animationPresenter != null\)[\s\S]*return;[\s\S]*animatorParameters\.ApplyMovement"),
            "A presenter-owned guest must return before the mover's legacy direct-Animator fallback.");
        Assert.That(
            chapter1Text,
            Does.Contain("CharacterAnimationPresenter.EnsureForActor(guestObject)"),
            "Chapter 1 guest setup should install the presenter on every scene guest.");
        Assert.That(
            chapter1Text,
            Does.Not.Contain(".SetBool("),
            "Chapter 1 choreography must not bypass the presenter and write Animator parameters directly.");
        Assert.That(
            chapter1Text,
            Does.Contain("DisableAmbientWalkers(guestObject);")
                .And.Contain("DisablePlayerOnlyComponents(guestObject);"),
            "Chapter 1 setup must disable the two other movement systems that could animate a guest.");
        Assert.That(
            chapter1Text,
            Does.Contain("presenter.BodyRenderer"),
            "Chapter 1 renderer lookup should prefer the presenter's canonical body renderer.");
        Assert.That(
            panicText,
            Does.Contain("CharacterAnimationPresenter"),
            "Chapter 2 panic should route presenter-owned guests through the same animation authority.");
        Assert.That(
            panicText,
            Does.Contain("if (animationPresenter != null)"),
            "Chapter 2 panic direct sprite/animator paths should fence off presenter-owned guest bodies.");
    }

    [Test]
    public void PresenterUsesAnimationDisplayBodyRendererInsteadOfCoatChildren()
    {
        GameObject actor = new GameObject("Guest Presenter Test");
        GameObject coat = new GameObject("coatcutout_0", typeof(SpriteRenderer));
        GameObject visual = new GameObject("AnimationDisplay", typeof(SpriteRenderer), typeof(Animator));

        try
        {
            coat.transform.SetParent(actor.transform, false);
            visual.transform.SetParent(actor.transform, false);

            CharacterAnimationDisplay display = actor.AddComponent<CharacterAnimationDisplay>();
            display.Configure(visual.transform);

            CharacterAnimationPresenter presenter = CharacterAnimationPresenter.EnsureForActor(actor);

            Assert.That(presenter, Is.Not.Null);
            Assert.That(presenter.BodyRenderer, Is.SameAs(visual.GetComponent<SpriteRenderer>()));
            Assert.That(presenter.Animator, Is.SameAs(visual.GetComponent<Animator>()));
        }
        finally
        {
            Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void Chapter1CoatsShareTheirOwnersAnimationDisplayScale()
    {
        GameObject actor = new GameObject("Coat Zoom Owner");
        GameObject visual = new GameObject("AnimationDisplay");
        GameObject coat = new GameObject("coatcutout_zoom_test");

        try
        {
            visual.transform.SetParent(actor.transform, false);
            coat.transform.SetParent(actor.transform, false);

            CharacterAnimationDisplay display = actor.AddComponent<CharacterAnimationDisplay>();
            display.Configure(visual.transform);

            Vector3 authoredPosition = new Vector3(0.43f, 1.08f, 0f);
            Quaternion authoredRotation = Quaternion.Euler(0f, 0f, 7f);
            Vector3 authoredScale = new Vector3(0.07f, 0.0988f, 1f);
            coat.transform.localPosition = authoredPosition;
            coat.transform.localRotation = authoredRotation;
            coat.transform.localScale = authoredScale;

            MethodInfo attachMethod = typeof(Chapter1ArrivalController).GetMethod(
                "AttachCoatToCharacterDisplay",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(attachMethod, Is.Not.Null, "Chapter 1 needs one coat attachment path that targets the owner's AnimationDisplay.");
            attachMethod.Invoke(null, new object[] { coat, actor.transform });

            Assert.That(coat.transform.parent, Is.SameAs(visual.transform), "A worn or carried coat must inherit the same visual scale as its owner.");
            Assert.That(coat.transform.localPosition, Is.EqualTo(authoredPosition), "Reparenting must preserve the coat's authored local position.");
            Assert.That(Quaternion.Angle(coat.transform.localRotation, authoredRotation), Is.LessThan(0.001f), "Reparenting must preserve the coat's authored local rotation.");
            Assert.That(coat.transform.localScale, Is.EqualTo(authoredScale), "Reparenting must preserve the coat's authored local scale.");

            visual.transform.localScale = new Vector3(2f, 2f, 1f);

            Assert.That(coat.transform.lossyScale.x, Is.EqualTo(authoredScale.x * 2f).Within(0.0001f));
            Assert.That(coat.transform.lossyScale.y, Is.EqualTo(authoredScale.y * 2f).Within(0.0001f));

            string chapter1Text = File.ReadAllText(Chapter1ArrivalControllerPath);
            Assert.That(chapter1Text, Does.Not.Contain("coatObject.transform.SetParent(butlerTransform, false)"), "Butler-carried coats must not bypass AnimationDisplay.");
            Assert.That(chapter1Text, Does.Not.Contain("coatObject.transform.SetParent(guest.GuestObject.transform, false)"), "Guest-worn coats must not bypass AnimationDisplay.");
        }
        finally
        {
            Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void AllAssignedGuestCoatsUseHalfSizeGuestSevenWaistPresentation()
    {
        const float AuthoredScaleX = 0.07031f;
        const float AuthoredScaleY = 0.09882f;
        const float PreviousFallbackScale = 0.4f;
        const float HalfScale = 0.5f;
        const float GuestSevenCenterAboveFeet = 1.08f;
        GameObject controllerObject = new GameObject("Chapter1 Coat Presentation Test");
        Texture2D bodyTexture = new Texture2D(100, 300, TextureFormat.RGBA32, false);
        Sprite bodySprite = Sprite.Create(
            bodyTexture,
            new Rect(0f, 0f, bodyTexture.width, bodyTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        try
        {
            Chapter1ArrivalController controller = controllerObject.AddComponent<Chapter1ArrivalController>();
            Type controllerType = typeof(Chapter1ArrivalController);
            Type guestType = controllerType.GetNestedType("GuestRuntimeState", BindingFlags.NonPublic);
            MethodInfo applyAssignedCoatSprite = controllerType.GetMethod(
                "ApplyAssignedCoatSprite",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo configField = guestType?.GetField("Config", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo guestIndexField = guestType?.GetField("GuestIndex", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo guestObjectField = guestType?.GetField("GuestObject", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo ySorterField = guestType?.GetField("YSorter", BindingFlags.Instance | BindingFlags.Public);
            Sprite authoredPlaceholder = LoadSpriteAtPath("Assets/Art/coatcutout.png", "coatcutout_0");

            Assert.That(guestType, Is.Not.Null);
            Assert.That(applyAssignedCoatSprite, Is.Not.Null);
            Assert.That(configField, Is.Not.Null);
            Assert.That(guestIndexField, Is.Not.Null);
            Assert.That(guestObjectField, Is.Not.Null);
            Assert.That(ySorterField, Is.Not.Null);
            Assert.That(authoredPlaceholder, Is.Not.Null);

            for (int guestNumber = 1; guestNumber <= 8; guestNumber++)
            {
                GameObject actor = new GameObject($"Guest {guestNumber} Coat Presentation Probe");

                try
                {
                    actor.transform.position = new Vector3(3.25f, -2.4f, 0.5f);
                    actor.transform.localScale = new Vector3(1.25f, 0.75f, 1f);
                    SpriteRenderer bodyRenderer = actor.AddComponent<SpriteRenderer>();
                    bodyRenderer.sprite = bodySprite;
                    WorldYSortSpriteRenderer sorter = actor.AddComponent<WorldYSortSpriteRenderer>();

                    GuestArrivalConfig config = new GuestArrivalConfig();
                    config.ConfigureRuntime(
                        $"guest_{guestNumber}",
                        $"Guest {guestNumber}",
                        actor,
                        null,
                        null,
                        string.Empty,
                        null,
                        $"guest_{guestNumber}_coat");
                    object guestState = Activator.CreateInstance(guestType, true);
                    configField.SetValue(guestState, config);
                    guestIndexField.SetValue(guestState, guestNumber - 1);
                    guestObjectField.SetValue(guestState, actor);
                    ySorterField.SetValue(guestState, sorter);

                    Sprite assignedSprite = Resources.Load<Sprite>($"Chapter1/GuestCoats/guest{guestNumber}_coat");
                    Assert.That(assignedSprite, Is.Not.Null, $"Guest {guestNumber} assigned coat sprite");

                    GameObject authoredCoat = new GameObject($"Guest {guestNumber} Authored Coat");
                    authoredCoat.transform.SetParent(actor.transform, false);
                    authoredCoat.transform.localPosition = new Vector3(0.43f, 1.08f, 0.2f);
                    authoredCoat.transform.localScale = new Vector3(AuthoredScaleX, AuthoredScaleY, 1f);
                    SpriteRenderer authoredRenderer = authoredCoat.AddComponent<SpriteRenderer>();
                    authoredRenderer.sprite = authoredPlaceholder;
                    Vector2 authoredReferenceWorldSize = authoredRenderer.bounds.size;

                    applyAssignedCoatSprite.Invoke(controller, new[] { guestState, authoredCoat, (object)true });

                    Assert.That(authoredRenderer.sprite, Is.SameAs(assignedSprite), $"Guest {guestNumber} authored sprite");
                    Assert.That(
                        authoredRenderer.bounds.size.x,
                        Is.EqualTo(authoredReferenceWorldSize.x * HalfScale).Within(0.0001f),
                        $"Guest {guestNumber} authored coat width");
                    Assert.That(
                        authoredRenderer.bounds.size.y,
                        Is.EqualTo(authoredReferenceWorldSize.y * HalfScale).Within(0.0001f),
                        $"Guest {guestNumber} authored coat height");
                    Assert.That(
                        authoredCoat.transform.localScale.x,
                        Is.EqualTo(AuthoredScaleX * authoredPlaceholder.bounds.size.x / assignedSprite.bounds.size.x * HalfScale).Within(0.0001f),
                        $"Guest {guestNumber} authored local scale X");
                    Assert.That(
                        authoredCoat.transform.localScale.y,
                        Is.EqualTo(AuthoredScaleY * authoredPlaceholder.bounds.size.y / assignedSprite.bounds.size.y * HalfScale).Within(0.0001f),
                        $"Guest {guestNumber} authored local scale Y");
                    AssertCoatCenterAboveBodyFeet(
                        authoredRenderer,
                        bodyRenderer,
                        actor.transform,
                        GuestSevenCenterAboveFeet,
                        $"Guest {guestNumber} authored coat");
                    AssertCoatUsesAssignedAnchorHand(
                        authoredRenderer,
                        bodyRenderer,
                        actor.transform,
                        guestNumber,
                        $"Guest {guestNumber} authored coat");

                    Vector3 authoredPositionAfterFirstApply = authoredCoat.transform.localPosition;
                    Vector3 authoredScaleAfterFirstApply = authoredCoat.transform.localScale;
                    applyAssignedCoatSprite.Invoke(controller, new[] { guestState, authoredCoat, (object)true });
                    AssertVector3Approximately(
                        authoredCoat.transform.localPosition,
                        authoredPositionAfterFirstApply,
                        $"Guest {guestNumber} authored repeat position");
                    AssertVector3Approximately(
                        authoredCoat.transform.localScale,
                        authoredScaleAfterFirstApply,
                        $"Guest {guestNumber} authored repeat scale");

                    GameObject fallbackCoat = new GameObject($"Guest {guestNumber} Fallback Coat");
                    fallbackCoat.transform.SetParent(actor.transform, false);
                    fallbackCoat.transform.localPosition = new Vector3(-0.35f, 0.45f, -0.2f);
                    applyAssignedCoatSprite.Invoke(controller, new[] { guestState, fallbackCoat, (object)false });
                    SpriteRenderer fallbackRenderer = fallbackCoat.GetComponent<SpriteRenderer>();

                    Assert.That(fallbackRenderer, Is.Not.Null, $"Guest {guestNumber} fallback renderer");
                    Assert.That(fallbackRenderer.sprite, Is.SameAs(assignedSprite), $"Guest {guestNumber} fallback sprite");
                    Assert.That(fallbackCoat.transform.localScale, Is.EqualTo(new Vector3(0.2f, 0.2f, 1f)), $"Guest {guestNumber} fallback scale");
                    Assert.That(
                        fallbackRenderer.bounds.size.x,
                        Is.EqualTo(assignedSprite.bounds.size.x * PreviousFallbackScale * actor.transform.lossyScale.x * HalfScale).Within(0.0001f),
                        $"Guest {guestNumber} fallback coat width");
                    Assert.That(
                        fallbackRenderer.bounds.size.y,
                        Is.EqualTo(assignedSprite.bounds.size.y * PreviousFallbackScale * actor.transform.lossyScale.y * HalfScale).Within(0.0001f),
                        $"Guest {guestNumber} fallback coat height");
                    AssertCoatCenterAboveBodyFeet(
                        fallbackRenderer,
                        bodyRenderer,
                        actor.transform,
                        GuestSevenCenterAboveFeet,
                        $"Guest {guestNumber} fallback coat");
                    AssertCoatUsesAssignedAnchorHand(
                        fallbackRenderer,
                        bodyRenderer,
                        actor.transform,
                        guestNumber,
                        $"Guest {guestNumber} fallback coat");

                    Vector3 fallbackPositionAfterFirstApply = fallbackCoat.transform.localPosition;
                    Vector3 fallbackScaleAfterFirstApply = fallbackCoat.transform.localScale;
                    applyAssignedCoatSprite.Invoke(controller, new[] { guestState, fallbackCoat, (object)false });
                    AssertVector3Approximately(
                        fallbackCoat.transform.localPosition,
                        fallbackPositionAfterFirstApply,
                        $"Guest {guestNumber} fallback repeat position");
                    AssertVector3Approximately(
                        fallbackCoat.transform.localScale,
                        fallbackScaleAfterFirstApply,
                        $"Guest {guestNumber} fallback repeat scale");
                }
                finally
                {
                    Object.DestroyImmediate(actor);
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(bodySprite);
            Object.DestroyImmediate(bodyTexture);
        }
    }

    [TestCase(0f)]
    [TestCase(0.5f)]
    public void WornCoatWaistAlignmentHandlesBodyPivotsAndPreservesAuthoredAxes(float bodyPivotY)
    {
        const float GuestSevenCenterAboveFeet = 1.08f;
        GameObject actor = new GameObject("Coat Alignment Pivot Probe");
        GameObject bodyObject = new GameObject("AnimationDisplay");
        GameObject coatObject = new GameObject("coatcutout_alignment_probe");
        Texture2D bodyTexture = new Texture2D(80, 240, TextureFormat.RGBA32, false);
        Texture2D coatTexture = new Texture2D(50, 100, TextureFormat.RGBA32, false);
        Sprite bodySprite = Sprite.Create(
            bodyTexture,
            new Rect(0f, 0f, bodyTexture.width, bodyTexture.height),
            new Vector2(0.5f, bodyPivotY),
            100f);
        Sprite coatSprite = Sprite.Create(
            coatTexture,
            new Rect(0f, 0f, coatTexture.width, coatTexture.height),
            new Vector2(0.2f, 0.7f),
            100f);

        try
        {
            actor.transform.position = new Vector3(5f, -3f, 0.25f);
            bodyObject.transform.SetParent(actor.transform, false);
            bodyObject.transform.localPosition = new Vector3(-0.2f, 0.4f, 0f);
            bodyObject.transform.localScale = new Vector3(1.4f, 0.85f, 1f);
            SpriteRenderer bodyRenderer = bodyObject.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = bodySprite;

            coatObject.transform.SetParent(bodyObject.transform, false);
            Vector3 authoredPosition = new Vector3(0.43f, 7f, 0.37f);
            coatObject.transform.localPosition = authoredPosition;
            coatObject.transform.localScale = new Vector3(0.2f, 0.2f, 1f);
            SpriteRenderer coatRenderer = coatObject.AddComponent<SpriteRenderer>();
            coatRenderer.sprite = coatSprite;

            MethodInfo alignMethod = typeof(Chapter1ArrivalController).GetMethod(
                "AlignWornCoatToGuestSevenWaist",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(alignMethod, Is.Not.Null);
            alignMethod.Invoke(null, new object[] { coatObject, coatRenderer, bodyRenderer, null });

            Vector3 expectedBodyPoint = bodySprite.bounds.center;
            expectedBodyPoint.y = bodySprite.bounds.min.y + GuestSevenCenterAboveFeet;
            float expectedCenterY = bodyObject.transform.InverseTransformPoint(
                bodyRenderer.transform.TransformPoint(expectedBodyPoint)).y;
            float actualCenterY = bodyObject.transform.InverseTransformPoint(coatRenderer.bounds.center).y;

            Assert.That(actualCenterY, Is.EqualTo(expectedCenterY).Within(0.0001f));
            Assert.That(coatObject.transform.localPosition.x, Is.EqualTo(authoredPosition.x).Within(0.0001f));
            Assert.That(coatObject.transform.localPosition.z, Is.EqualTo(authoredPosition.z).Within(0.0001f));

            Vector3 alignedPosition = coatObject.transform.localPosition;
            alignMethod.Invoke(null, new object[] { coatObject, coatRenderer, bodyRenderer, null });
            AssertVector3Approximately(coatObject.transform.localPosition, alignedPosition, "Repeated alignment must not drift.");
        }
        finally
        {
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(bodySprite);
            Object.DestroyImmediate(coatSprite);
            Object.DestroyImmediate(bodyTexture);
            Object.DestroyImmediate(coatTexture);
        }
    }

    [Test]
    public void WornCoatWaistAlignmentUsesStableFloorWhenBodyFramePivotChanges()
    {
        const float GuestSevenCenterAboveFeet = 1.08f;
        GameObject actor = new GameObject("Stable Floor Coat Alignment Probe");
        GameObject visual = new GameObject("AnimationDisplay");
        GameObject coatObject = new GameObject("coatcutout_stable_floor_probe");
        Texture2D texture = new Texture2D(64, 128, TextureFormat.RGBA32, false);
        Sprite centerPivotBody = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        Sprite bottomPivotBody = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0f),
            100f);
        Sprite coatSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 32f, 64f),
            new Vector2(0.5f, 0.5f),
            100f);

        try
        {
            visual.transform.SetParent(actor.transform, false);
            visual.transform.localScale = new Vector3(1.3f, 1.6f, 1f);
            SpriteRenderer bodyRenderer = visual.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = centerPivotBody;
            coatObject.transform.SetParent(visual.transform, false);
            Vector3 authoredPosition = new Vector3(-0.31f, 4f, 0.27f);
            coatObject.transform.localPosition = authoredPosition;
            SpriteRenderer coatRenderer = coatObject.AddComponent<SpriteRenderer>();
            coatRenderer.sprite = coatSprite;

            CharacterFloorReference floorReference = actor.AddComponent<CharacterFloorReference>();
            Vector3 stableFloorLocalPoint = new Vector3(0.12f, -0.73f, 0f);
            floorReference.CaptureWorldPoint(visual.transform.TransformPoint(stableFloorLocalPoint), visual.transform);
            MethodInfo alignMethod = typeof(Chapter1ArrivalController).GetMethod(
                "AlignWornCoatToGuestSevenWaist",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(alignMethod, Is.Not.Null);

            alignMethod.Invoke(null, new object[] { coatObject, coatRenderer, bodyRenderer, floorReference });
            float expectedCenterY = stableFloorLocalPoint.y + GuestSevenCenterAboveFeet;
            float centerPivotAlignedY = visual.transform.InverseTransformPoint(coatRenderer.bounds.center).y;
            Assert.That(centerPivotAlignedY, Is.EqualTo(expectedCenterY).Within(0.0001f));

            bodyRenderer.sprite = bottomPivotBody;
            alignMethod.Invoke(null, new object[] { coatObject, coatRenderer, bodyRenderer, floorReference });
            float bottomPivotAlignedY = visual.transform.InverseTransformPoint(coatRenderer.bounds.center).y;
            Assert.That(bottomPivotAlignedY, Is.EqualTo(expectedCenterY).Within(0.0001f));
            Assert.That(coatObject.transform.localPosition.x, Is.EqualTo(authoredPosition.x).Within(0.0001f));
            Assert.That(coatObject.transform.localPosition.z, Is.EqualTo(authoredPosition.z).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(centerPivotBody);
            Object.DestroyImmediate(bottomPivotBody);
            Object.DestroyImmediate(coatSprite);
            Object.DestroyImmediate(texture);
        }
    }

    [TestCase(0, -1f)]
    [TestCase(1, 1f)]
    [TestCase(2, -1f)]
    [TestCase(3, 1f)]
    [TestCase(4, -1f)]
    [TestCase(5, 1f)]
    [TestCase(6, -1f)]
    [TestCase(7, 1f)]
    public void WornCoatHandAlignmentMirrorsRenderedCenterAndPreservesPresentation(
        int zeroBasedAnchorIndex,
        float expectedSide)
    {
        GameObject actor = new GameObject($"Anchor {zeroBasedAnchorIndex + 1} Coat Hand Probe");
        GameObject bodyObject = new GameObject("AnimationDisplay");
        GameObject coatObject = new GameObject("coatcutout_hand_probe");
        Texture2D bodyTexture = new Texture2D(80, 220, TextureFormat.RGBA32, false);
        Texture2D coatTexture = new Texture2D(60, 110, TextureFormat.RGBA32, false);
        Sprite bodySprite = Sprite.Create(
            bodyTexture,
            new Rect(0f, 0f, bodyTexture.width, bodyTexture.height),
            new Vector2(0.2f, 0.35f),
            100f);
        Sprite coatSprite = Sprite.Create(
            coatTexture,
            new Rect(0f, 0f, coatTexture.width, coatTexture.height),
            new Vector2(0.8f, 0.15f),
            100f);

        try
        {
            actor.transform.position = new Vector3(4.5f, -2.75f, 0.4f);
            actor.transform.localScale = new Vector3(1.35f, 0.8f, 1f);
            bodyObject.transform.SetParent(actor.transform, false);
            bodyObject.transform.localPosition = new Vector3(-0.18f, 0.27f, 0.05f);
            bodyObject.transform.localRotation = Quaternion.Euler(0f, 0f, 9f);
            bodyObject.transform.localScale = new Vector3(1.4f, 0.85f, 1f);
            SpriteRenderer bodyRenderer = bodyObject.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = bodySprite;
            bodyRenderer.sortingLayerName = "People";
            bodyRenderer.sortingOrder = 320;

            coatObject.transform.SetParent(bodyObject.transform, false);
            coatObject.transform.localPosition = new Vector3(1.15f, 0.92f, 0.31f);
            coatObject.transform.localRotation = Quaternion.Euler(0f, 0f, -7f);
            coatObject.transform.localScale = new Vector3(0.23f, 0.17f, 1f);
            SpriteRenderer coatRenderer = coatObject.AddComponent<SpriteRenderer>();
            coatRenderer.sprite = coatSprite;
            coatRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            coatRenderer.sortingOrder = bodyRenderer.sortingOrder + 1;

            MethodInfo alignMethod = typeof(Chapter1ArrivalController).GetMethod(
                "AlignWornCoatToAssignedAnchorHand",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(alignMethod, Is.Not.Null);

            Transform placementSpace = coatObject.transform.parent;
            Vector3 bodyCenterBefore = placementSpace.InverseTransformPoint(bodyRenderer.bounds.center);
            Vector3 coatCenterBefore = placementSpace.InverseTransformPoint(coatRenderer.bounds.center);
            float initialDistance = Mathf.Abs(coatCenterBefore.x - bodyCenterBefore.x);
            Assert.That(initialDistance, Is.GreaterThan(0.01f), "The probe must begin visibly on one side of the body.");
            Assert.That(coatCenterBefore.x, Is.GreaterThan(bodyCenterBefore.x), "The probe must begin on the authored left-hand/screen-right side.");

            Vector3 preservedLocalPosition = coatObject.transform.localPosition;
            Quaternion preservedLocalRotation = coatObject.transform.localRotation;
            Vector3 preservedLocalScale = coatObject.transform.localScale;
            int preservedSortingLayerId = coatRenderer.sortingLayerID;
            int preservedSortingOrder = coatRenderer.sortingOrder;

            alignMethod.Invoke(
                null,
                new object[] { zeroBasedAnchorIndex, coatObject, coatRenderer, bodyRenderer });

            Vector3 bodyCenterAfter = placementSpace.InverseTransformPoint(bodyRenderer.bounds.center);
            Vector3 coatCenterAfter = placementSpace.InverseTransformPoint(coatRenderer.bounds.center);
            float expectedCoatCenterX = bodyCenterAfter.x + expectedSide * initialDistance;
            Assert.That(
                coatCenterAfter.x,
                Is.EqualTo(expectedCoatCenterX).Within(0.0001f),
                $"Anchor {zeroBasedAnchorIndex + 1} rendered coat center X");
            Assert.That(
                coatCenterAfter.y,
                Is.EqualTo(coatCenterBefore.y).Within(0.0001f),
                "Hand selection must preserve waist height.");
            Assert.That(
                coatObject.transform.localPosition.y,
                Is.EqualTo(preservedLocalPosition.y).Within(0.0001f));
            Assert.That(
                coatObject.transform.localPosition.z,
                Is.EqualTo(preservedLocalPosition.z).Within(0.0001f));
            Assert.That(
                Quaternion.Angle(coatObject.transform.localRotation, preservedLocalRotation),
                Is.LessThan(0.0001f));
            AssertVector3Approximately(
                coatObject.transform.localScale,
                preservedLocalScale,
                "Hand selection must preserve coat scale");
            Assert.That(coatRenderer.sortingLayerID, Is.EqualTo(preservedSortingLayerId));
            Assert.That(coatRenderer.sortingOrder, Is.EqualTo(preservedSortingOrder));

            Vector3 positionAfterFirstAlignment = coatObject.transform.localPosition;
            Vector3 renderedCenterAfterFirstAlignment = coatCenterAfter;
            alignMethod.Invoke(
                null,
                new object[] { zeroBasedAnchorIndex, coatObject, coatRenderer, bodyRenderer });
            AssertVector3Approximately(
                coatObject.transform.localPosition,
                positionAfterFirstAlignment,
                "Repeated hand alignment must not drift");
            Vector3 renderedCenterAfterRepeat = placementSpace.InverseTransformPoint(coatRenderer.bounds.center);
            AssertVector3Approximately(
                renderedCenterAfterRepeat,
                renderedCenterAfterFirstAlignment,
                "Repeated hand alignment must preserve the rendered center");
        }
        finally
        {
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(bodySprite);
            Object.DestroyImmediate(coatSprite);
            Object.DestroyImmediate(bodyTexture);
            Object.DestroyImmediate(coatTexture);
        }
    }

    [Test]
    public void Chapter1SuppliesStableDirectionsForBothGuestMovementPhases()
    {
        string chapter1Text = File.ReadAllText(Chapter1ArrivalControllerPath);
        string waypointMoverText = File.ReadAllText(NPCWaypointMoverPath);

        Assert.That(
            chapter1Text,
            Does.Contain("GetEntranceApproachAnimationDirection(guest, frontAnchor)"),
            "The door-to-speaking-anchor animation direction must be selected once from the authored start and target.");
        Assert.That(
            chapter1Text,
            Does.Contain("GetEntranceApproachAnimationDirection(guest, waitSpot)"),
            "The speaking-anchor-to-coat-spot direction must be calculated independently for the second leg.");
        Assert.That(
            chapter1Text,
            Does.Contain("CharacterWalkDirection.Left);"),
            "Every drawing-room departure must explicitly use walk-left regardless of floor-route Y corrections.");
        Assert.That(
            chapter1Text,
            Does.Contain("mover.MoveTo(target, animationDirection);"),
            "Chapter 1 must pass its selected direction into the mover instead of letting the mover infer it every frame.");
        Assert.That(
            waypointMoverText,
            Does.Contain("hasAnimationDirectionOverride"),
            "The mover must retain the Chapter 1 direction for the complete movement and dialogue-pause lifecycle.");
    }

    [Test]
    public void EntranceApproachDirectionUsesSignedHorizontalDeltaAndVerticalFallback()
    {
        GameObject guestObject = new GameObject("Signed Direction Guest");
        GameObject targetObject = new GameObject("Signed Direction Target");

        try
        {
            Type controllerType = typeof(Chapter1ArrivalController);
            Type guestType = controllerType.GetNestedType("GuestRuntimeState", BindingFlags.NonPublic);
            MethodInfo directionMethod = controllerType.GetMethod(
                "GetEntranceApproachAnimationDirection",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(guestType, Is.Not.Null);
            Assert.That(directionMethod, Is.Not.Null);

            object guestState = Activator.CreateInstance(guestType, true);
            FieldInfo guestObjectField = guestType.GetField("GuestObject", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(guestObjectField, Is.Not.Null);
            guestObjectField.SetValue(guestState, guestObject);
            guestObject.transform.position = Vector3.zero;

            targetObject.transform.position = new Vector3(-5f, -1f, 0f);
            Assert.That(
                directionMethod.Invoke(null, new object[] { guestState, targetObject.transform }),
                Is.EqualTo(CharacterWalkDirection.Left),
                "A horizontal-dominant route to the left must not display walk-right.");

            targetObject.transform.position = new Vector3(5f, -1f, 0f);
            Assert.That(
                directionMethod.Invoke(null, new object[] { guestState, targetObject.transform }),
                Is.EqualTo(CharacterWalkDirection.Right),
                "A horizontal-dominant route to the right should display walk-right.");

            targetObject.transform.position = new Vector3(1f, -5f, 0f);
            Assert.That(
                directionMethod.Invoke(null, new object[] { guestState, targetObject.transform }),
                Is.EqualTo(CharacterWalkDirection.Down),
                "A vertical-dominant entrance route should retain the authored walk-down presentation.");
        }
        finally
        {
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(guestObject);
        }
    }

    [Test]
    public void ForcedLeftMoveDoesNotSwitchToDownForDiagonalPhysicalMovement()
    {
        GameObject actor = new GameObject("Forced Direction Guest");
        GameObject visual = new GameObject("AnimationDisplay", typeof(SpriteRenderer), typeof(Animator));
        GameObject target = new GameObject("Diagonal Down Left Target");
        IEnumerator routine = null;

        try
        {
            visual.transform.SetParent(actor.transform, false);
            Animator animator = visual.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Animation/Player/Player.controller");
            animator.Rebind();
            animator.Update(0f);

            CharacterAnimationDisplay display = actor.AddComponent<CharacterAnimationDisplay>();
            display.Configure(visual.transform);
            CharacterAnimationPresenter.EnsureForActor(actor);

            NPCWaypointMover mover = actor.AddComponent<NPCWaypointMover>();
            mover.ConstrainToPlayerFloorBoundary = false;
            mover.MoveSpeed = 2.2f;
            target.transform.position = new Vector3(-5f, -5f, 0f);

            MethodInfo forcedMoveRoutine = typeof(NPCWaypointMover).GetMethod(
                "MoveToRoutine",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(Transform), typeof(CharacterWalkDirection) },
                null);

            Assert.That(
                forcedMoveRoutine,
                Is.Not.Null,
                "NPCWaypointMover needs a forced-direction overload for scripted Chapter 1 choreography.");

            routine = (IEnumerator)forcedMoveRoutine.Invoke(
                mover,
                new object[] { target.transform, CharacterWalkDirection.Left });

            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(animator.GetBool("IsWalkingLeft"), Is.True);
            Assert.That(animator.GetBool("IsWalkingDown"), Is.False);
            Assert.That(animator.GetBool("IsWalkingUp"), Is.False);
            Assert.That(animator.GetBool("IsWalkingRight"), Is.False);
        }
        finally
        {
            (routine as IDisposable)?.Dispose();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void Chapter2GuestExitsChooseOneDirectionFromVisibleFeetForTheWholeMove()
    {
        string guestSearchText = File.ReadAllText(Chapter2GuestSearchControllerPath);

        Assert.That(
            guestSearchText,
            Does.Contain("CharacterAnimationPresenter.EnsureForActor(actorObject);"),
            "Every Chapter 2 guest path, including direct debug skips, must install the single animation presenter.");
        Assert.That(
            guestSearchText,
            Does.Contain("GetGuestExitAnimationDirection(actorState, exitTarget)"),
            "A hiding-place exit must choose its intended direction once before movement begins.");
        Assert.That(
            guestSearchText,
            Does.Contain("CharacterFootPositionUtility.TryGetWorldPoint"),
            "The selected exit direction must start from the guest's visible feet rather than its transform pivot.");
        Assert.That(
            guestSearchText,
            Does.Contain("mover.MoveTo(exitTarget, animationDirection);"),
            "The selected direction must be locked for the complete waypoint move.");
        Assert.That(
            guestSearchText,
            Does.Not.Contain("mover.MoveTo(exitTarget);"),
            "Chapter 2 must not return to per-frame direction inference for dining-room exits.");
    }

    [Test]
    public void Chapter2PanicKeepsOneAnimationDirectionForEachMovementSegment()
    {
        string panicText = File.ReadAllText(Chapter2GuestPanicControllerPath);

        Assert.That(
            panicText,
            Does.Contain("PanicAction lockedRunAction"),
            "Scripted panic movement should receive one locked action for the complete pass.");
        Assert.That(
            panicText,
            Does.Contain("participant.UpdateAnimatorWalk(lockedRunAction, scriptedGuestWalkAnimationSpeed)"),
            "Scripted panic animation must not be reclassified from incidental per-frame transform changes.");
        Assert.That(
            panicText,
            Does.Contain("participant.UpdateAnimatorWalk(participant.CurrentRunAction, scriptedGuestWalkAnimationSpeed)"),
            "Shared panic routes should retain their authored current segment direction until the route advances.");
        Assert.That(
            panicText,
            Does.Contain("animationPresenter.BeginWalk(direction, 1f)"),
            "Panic movement should issue its locked direction through the same presenter as every other guest move.");
    }

    private static Sprite LoadSpriteAtPath(string assetPath, string spriteName)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite && string.Equals(sprite.name, spriteName, StringComparison.Ordinal))
            {
                return sprite;
            }
        }

        return null;
    }

    private static void AssertCoatCenterAboveBodyFeet(
        SpriteRenderer coatRenderer,
        SpriteRenderer bodyRenderer,
        Transform placementSpace,
        float centerAboveFeet,
        string context)
    {
        Vector3 bodyPoint = bodyRenderer.sprite.bounds.center;
        bodyPoint.y = bodyRenderer.sprite.bounds.min.y + centerAboveFeet;
        float expectedCenterY = placementSpace.InverseTransformPoint(
            bodyRenderer.transform.TransformPoint(bodyPoint)).y;
        float actualCenterY = placementSpace.InverseTransformPoint(coatRenderer.bounds.center).y;
        Assert.That(actualCenterY, Is.EqualTo(expectedCenterY).Within(0.0001f), context);
    }

    private static void AssertCoatUsesAssignedAnchorHand(
        SpriteRenderer coatRenderer,
        SpriteRenderer bodyRenderer,
        Transform placementSpace,
        int oneBasedAnchorIndex,
        string context)
    {
        float bodyCenterX = placementSpace.InverseTransformPoint(bodyRenderer.bounds.center).x;
        float coatCenterX = placementSpace.InverseTransformPoint(coatRenderer.bounds.center).x;
        float horizontalOffset = coatCenterX - bodyCenterX;

        Assert.That(
            Mathf.Abs(horizontalOffset),
            Is.GreaterThan(0.0001f),
            $"{context} must remain visibly offset from the body center.");

        if (oneBasedAnchorIndex % 2 == 1)
        {
            Assert.That(
                horizontalOffset,
                Is.LessThan(0f),
                $"{context} at odd anchor {oneBasedAnchorIndex} must use the guest's right hand (screen-left).");
        }
        else
        {
            Assert.That(
                horizontalOffset,
                Is.GreaterThan(0f),
                $"{context} at even anchor {oneBasedAnchorIndex} must retain the original left-hand (screen-right) side.");
        }
    }

    private static void AssertVector3Approximately(Vector3 actual, Vector3 expected, string context)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f), $"{context} X");
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f), $"{context} Y");
        Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f), $"{context} Z");
    }
}
