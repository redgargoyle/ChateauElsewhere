# Guest Exit Movement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make detached projected Chapter 2 guests visibly walk to the existing authored exit door before they are hidden and staged for the Dining Room.

**Architecture:** Keep `Chapter2GuestSearchController` on its current `NPCWaypointMover` departure path. Correct the shared mover's ownership predicate so an active `RoomProjectedEntity` remains the visible motion owner after its actor root is detached from `RoomContentGroup`.

**Tech Stack:** Unity 6000.4.10f1, C#, NUnit EditMode tests, Unity Test Framework 1.6.0

## Global Constraints

- Reuse `NPCWaypointMover`; do not add a second movement coroutine or route planner.
- Continue selecting authored `DoorTriggerNavigation` targets through `FindExitDoorTowardDiningRoom`.
- Keep guests visible until arrival or the existing timeout, then use `StageGuestForDiningRoomReveal`.
- Do not modify `Assets/Scenes/Gameplay.unity` or the user's existing workspace changes.

---

### Task 1: Let Active Detached Projections Own Waypoint Movement

**Files:**
- Modify: `Assets/Editor/RoomProjectionRegressionTests.cs`
- Modify: `Assets/Scripts/Story/NPCWaypointMover.cs`

**Interfaces:**
- Consumes: `RoomProjectedEntity.IsProjectionActive`
- Produces: `NPCWaypointMover.CanUseProjectionAsMotionOwner(RoomProjectedEntity projection) -> bool`

- [ ] **Step 1: Write the failing detached-projection regression test**

Add this test to `RoomProjectionRegressionTests`:

```csharp
[Test]
public void DetachedActiveProjectionCanOwnWaypointMovement()
{
    RoomPerspectiveProfile profile = CreatePerspectiveProfile();
    RoomProjectedEntity projection = CreateProjectedEntity(
        "DetachedProjectedGuest",
        profile,
        null,
        Vector2.zero);

    try
    {
        Assert.That(projection.GetComponentInParent<RoomContentGroup>(true), Is.Null);
        Assert.That(projection.IsProjectionActive, Is.True);
        Assert.That(
            NPCWaypointMover.CanUseProjectionAsMotionOwner(projection),
            Is.True,
            "A detached active projection still owns the visible foot point and must be moved instead of its pinned actor root.");
    }
    finally
    {
        DestroyEntity(projection);
        UnityEngine.Object.DestroyImmediate(profile);
    }
}
```

- [ ] **Step 2: Run the test and verify the expected failure**

Run:

```bash
/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics -projectPath /home/hamza/dreadforge_2022_2 -runTests -testPlatform EditMode -testFilter RoomProjectionRegressionTests.DetachedActiveProjectionCanOwnWaypointMovement -testResults /tmp/dreadforge-guest-exit-red.xml -logFile /tmp/dreadforge-guest-exit-red.log
```

Expected: one failed test because `CanUseProjectionAsMotionOwner` returns false for the detached active projection.

- [ ] **Step 3: Correct the shared projection ownership predicate**

Replace `CanUseProjectionAsMotionOwner` with:

```csharp
public static bool CanUseProjectionAsMotionOwner(RoomProjectedEntity projection)
{
    return projection != null && projection.IsProjectionActive;
}
```

- [ ] **Step 4: Run the focused test and verify it passes**

Run:

```bash
/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics -projectPath /home/hamza/dreadforge_2022_2 -runTests -testPlatform EditMode -testFilter RoomProjectionRegressionTests.DetachedActiveProjectionCanOwnWaypointMovement -testResults /tmp/dreadforge-guest-exit-green.xml -logFile /tmp/dreadforge-guest-exit-green.log
```

Expected: one passed test and zero failures.

- [ ] **Step 5: Run related regression suites**

Run:

```bash
/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics -projectPath /home/hamza/dreadforge_2022_2 -runTests -testPlatform EditMode -testFilter RoomProjectionRegressionTests -testResults /tmp/dreadforge-room-projection.xml -logFile /tmp/dreadforge-room-projection.log
```

```bash
/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics -projectPath /home/hamza/dreadforge_2022_2 -runTests -testPlatform EditMode -testFilter Chapter2RegressionTests -testResults /tmp/dreadforge-chapter2.xml -logFile /tmp/dreadforge-chapter2.log
```

Expected: both suites pass with zero failures.

- [ ] **Step 6: Run the full EditMode suite and inspect scope**

Run:

```bash
/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics -projectPath /home/hamza/dreadforge_2022_2 -runTests -testPlatform EditMode -testResults /tmp/dreadforge-editmode.xml -logFile /tmp/dreadforge-editmode.log
```

Then run:

```bash
git diff --check
git diff -- Assets/Scripts/Story/NPCWaypointMover.cs Assets/Editor/RoomProjectionRegressionTests.cs
git status --short
```

Expected: all EditMode tests pass, `git diff --check` reports no whitespace errors, and the production diff contains only the shared ownership correction.
