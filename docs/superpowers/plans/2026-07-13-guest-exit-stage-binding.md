# Guest Exit Stage-Binding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make world-space Chapter 2 guests physically reach the existing authored door or stairway before Dining Room staging.

**Architecture:** Keep `Chapter2GuestSearchController` on its existing route-selection and `NPCWaypointMover` pathway. Transfer position ownership inside the shared mover by clearing a colocated `ActorRoomState` room-stage binding immediately before transform-owned movement.

**Tech Stack:** Unity 6000.4.10f1, C#, NUnit EditMode tests, Unity Test Framework 1.6.0

## Global Constraints

- Reuse `NPCWaypointMover`; do not add a Chapter 2 movement coroutine or route planner.
- Continue selecting authored `DoorTriggerNavigation` door and stair targets through `FindExitDoorTowardDiningRoom`.
- Keep guests visible until arrival or the existing timeout, then use `StageGuestForDiningRoomReveal`.
- Preserve projection-owned movement and the branch's existing projection ownership guard.
- Do not modify `Assets/Scenes/Gameplay.unity` or `.vscode/settings.json`.

---

### Task 1: Transfer World-Transform Ownership to the Shared Waypoint Mover

**Files:**
- Modify: `Assets/Editor/StoryActorRoomStageLockingTests.cs`
- Modify: `Assets/Editor/Chapter2RegressionTests.cs`
- Modify: `Assets/Scripts/Story/NPCWaypointMover.cs`

**Interfaces:**
- Consumes: `ActorRoomState.ClearRoomStagePointBinding() -> void`
- Produces: `NPCWaypointMover` releases a colocated `ActorRoomState` binding before any direct transform placement or movement.

- [ ] **Step 1: Write the failing stage-binding behavior regression**

Add this test to `StoryActorRoomStageLockingTests`:

```csharp
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
        Assert.That(ApplyBinding(rig.ActorState), Is.True, "The real Chapter 2 guest starts bound to its hide anchor.");

        NPCWaypointMover mover = rig.ActorState.gameObject.AddComponent<NPCWaypointMover>();
        IEnumerator move = mover.MoveToRoutine(exit);

        Assert.That(move.MoveNext(), Is.True, "The exit waypoint should require at least one movement step.");
        Assert.That(ApplyBinding(rig.ActorState), Is.False,
            "Scripted transform movement must release the passive stage binding before LateUpdate can pin the guest.");

        mover.StopMoving();
    }
    finally
    {
        rig.Destroy();
    }
}
```

Add `using System.Collections;` to the test file.

- [ ] **Step 2: Run the focused test to verify RED**

Run:

```bash
/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics -projectPath /home/hamza/dreadforge_2022_2 -runTests -testPlatform EditMode -testFilter StoryActorRoomStageLockingTests.TransformWaypointMovementReleasesRoomStageBindingBeforeFirstStep -testResults /tmp/dreadforge-guest-exit-binding-red.xml -logFile /tmp/dreadforge-guest-exit-binding-red.log
```

Expected: one failed test because `ApplyBinding(rig.ActorState)` still returns `true` after the mover's first step.

- [ ] **Step 3: Add the no-duplicate-path source contract**

In `Chapter2GuestPreferenceExitWalksToDoorBeforeDiningTransfer`, extract `PrepareGuestForExitWalk` and assert:

```csharp
Assert.That(waypointText, Does.Contain("actorRoomState.ClearRoomStagePointBinding()"),
    "The shared mover should release passive room-stage binding whenever it owns the actor transform.");
Assert.That(prepareExitBody, Does.Not.Contain("ClearRoomStagePointBinding"),
    "Chapter 2 should not duplicate a transform-movement prerequisite owned by NPCWaypointMover.");
```

- [ ] **Step 4: Implement the ownership handoff in `NPCWaypointMover`**

Add a serialized reference and resolve only the colocated actor state:

```csharp
[SerializeField] private ActorRoomState actorRoomState;

if (actorRoomState == null)
{
    actorRoomState = GetComponent<ActorRoomState>();
}
```

Add and call this helper immediately before both direct transform paths:

```csharp
private void ReleaseRoomStageBindingForTransformMotion()
{
    ResolveReferences();
    actorRoomState?.ClearRoomStagePointBinding();
}
```

Use it before `transform.position = GetTargetPosition(target)` in the disabled-mover fallback and immediately after projected-target selection fails in `MoveToRoutine`. Do not call it inside `MoveProjectedToRoutine`.

- [ ] **Step 5: Run focused GREEN verification**

Run the stage-binding behavior test from Step 2 again with results at `/tmp/dreadforge-guest-exit-binding-green.xml`. Expected: one passed test, zero failures.

Run:

```bash
/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics -projectPath /home/hamza/dreadforge_2022_2 -runTests -testPlatform EditMode -testFilter Chapter2RegressionTests.Chapter2GuestPreferenceExitWalksToDoorBeforeDiningTransfer -testResults /tmp/dreadforge-guest-exit-contract-green.xml -logFile /tmp/dreadforge-guest-exit-contract-green.log
```

Expected: one passed test, zero failures.

- [ ] **Step 6: Run related regression fixtures**

Run `StoryActorRoomStageLockingTests`, `Chapter2RegressionTests`, `RoomProjectionRegressionTests`, and `NavigationRegressionTests` as separate EditMode test filters. Write their XML and logs under `/tmp/dreadforge-<fixture>.xml` and `/tmp/dreadforge-<fixture>.log`. Record exact pass/fail counts and distinguish any pre-existing failures from the focused behavior.

- [ ] **Step 7: Inspect scope and commit owned files**

Run:

```bash
git diff --check -- Assets/Scripts/Story/NPCWaypointMover.cs Assets/Editor/StoryActorRoomStageLockingTests.cs Assets/Editor/Chapter2RegressionTests.cs docs/superpowers/specs/2026-07-13-guest-exit-stage-binding-design.md docs/superpowers/plans/2026-07-13-guest-exit-stage-binding.md
git status --short
```

Commit only the shared mover, tests, and these two documents. Leave the existing scene and editor-settings changes untouched.
