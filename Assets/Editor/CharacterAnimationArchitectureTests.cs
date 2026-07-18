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
}
