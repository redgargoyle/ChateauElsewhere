# Guest Exit Movement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make detached projected Chapter 2 guests visibly walk to the existing authored exit door before they are hidden and staged for the Dining Room.

**Architecture:** Keep `Chapter2GuestSearchController` on its current `NPCWaypointMover` departure path. Correct the shared mover's ownership predicate so an active position-owning `RoomProjectedEntity` remains the visible motion owner after its actor root is detached from `RoomContentGroup`.

**Tech Stack:** Unity 6000.4.10f1, C#, NUnit EditMode tests, Unity Test Framework 1.6.0

## Global Constraints

- Reuse `NPCWaypointMover`; do not add a second movement coroutine or route planner.
- Continue selecting authored `DoorTriggerNavigation` targets through `FindExitDoorTowardDiningRoom`.
- Keep guests visible until arrival or the existing timeout, then use `StageGuestForDiningRoomReveal`.
- Do not modify `Assets/Scenes/Gameplay.unity` or the user's existing workspace changes.

---

### Task 1: Let Active Position-Owning Detached Projections Own Waypoint Movement

**Files:**
- Modify: `Assets/Editor/RoomProjectionRegressionTests.cs`
- Modify: `Assets/Scripts/Characters/RoomProjectedEntity.cs` - exposes the active position-ownership contract consumed by waypoint movement.
- Modify: `Assets/Scripts/Story/NPCWaypointMover.cs`

**Interfaces:**
- Consumes: `RoomProjectedEntity.OwnsProjectedPosition`
- Produces: `NPCWaypointMover.CanUseProjectionAsMotionOwner(RoomProjectedEntity projection) -> bool`

- [ ] **Step 1: Write and run the failing non-position-owning regression**

Use a serialized test setup to set `applyPosition` to `false` on an otherwise active projection. Assert that `CanUseProjectionAsMotionOwner` returns `false`.

Run:

```bash
/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics -projectPath /home/hamza/dreadforge_2022_2 -runTests -testPlatform EditMode -testFilter RoomProjectionRegressionTests.ActiveProjectionWithoutPositionOwnershipCannotOwnWaypointMovement -testResults /tmp/dreadforge-guest-exit-apply-position-red.xml -logFile /tmp/dreadforge-guest-exit-apply-position-red.log
```

Expected: one failed test because the active-only predicate incorrectly returns true.

- [ ] **Step 2: Add the detached-projection coroutine behavior regression**

Create a detached actor root containing `NPCWaypointMover` and a child `RoomProjectedEntity` that applies position. Move it to a compatible target under a matching `RoomContentGroup`. Drive `MoveToRoutine` to completion in the Unity coroutine test and assert the projected foot point and visible projected transform reach the target, the actor root remains unchanged, and `IsMoving` becomes false. Also cover matching and wrong-room `CanProjectTarget` results.

For RED evidence, temporarily restore only the old `RoomContentGroup` ownership predicate, run the coroutine test, and restore the intended predicate. The old behavior must leave the projected foot point at its start value.

- [ ] **Step 3: Correct the shared projection ownership predicate**

```csharp
public bool OwnsProjectedPosition => applyPosition && IsProjectionActive;

public static bool CanUseProjectionAsMotionOwner(RoomProjectedEntity projection)
{
    return projection != null && projection.OwnsProjectedPosition;
}
```

Remove direct `IsProjectionActive` checks immediately after `CanUseProjectionAsMotionOwner` in the mover's projected-target and projected-placement paths; retain `CanProjectTarget` protection.

- [ ] **Step 4: Run focused GREEN verification**

Run:

```bash
/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics -projectPath /home/hamza/dreadforge_2022_2 -runTests -testPlatform EditMode -testFilter RoomProjectionRegressionTests.DetachedPositionOwningProjectionMovesVisibleFootPointWithoutMovingActorRoot -testResults /tmp/dreadforge-guest-exit-behavior-final-green.xml -logFile /tmp/dreadforge-guest-exit-behavior-final-green.log
```

Expected: one passed test and zero failures. Also run the non-position-owning ownership regression to verify the new property is consumed.

- [ ] **Step 5: Run related regression suites**

Run:

```bash
/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics -projectPath /home/hamza/dreadforge_2022_2 -runTests -testPlatform EditMode -testFilter RoomProjectionRegressionTests -testResults /tmp/dreadforge-room-projection.xml -logFile /tmp/dreadforge-room-projection.log
```

```bash
/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics -projectPath /home/hamza/dreadforge_2022_2 -runTests -testPlatform EditMode -testFilter Chapter2RegressionTests -testResults /tmp/dreadforge-chapter2.xml -logFile /tmp/dreadforge-chapter2.log
```

Run the focused `Chapter2RegressionTests.Chapter2GuestPreferenceExitWalksToDoorBeforeDiningTransfer` regression before both suites. Record the exact suite counts and do not fix unrelated existing failures.

- [ ] **Step 6: Inspect scope and commit owned tracked files**

Run:

```bash
git diff --check -- Assets/Scripts/Characters/RoomProjectedEntity.cs Assets/Scripts/Story/NPCWaypointMover.cs Assets/Editor/RoomProjectionRegressionTests.cs docs/superpowers/specs/2026-07-12-guest-exit-movement-design.md docs/superpowers/plans/2026-07-12-guest-exit-movement.md
git status --short
```

Commit only the owned tracked source, test, and documentation files. Do not stage the protected scene or editor-settings changes, and leave the ignored task report untracked.
