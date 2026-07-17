# Guest Static Anchor Vertical Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep every statically staged Guest exactly on its authored room anchor, independent of animation-frame renderer bounds.

**Architecture:** `ActorRoomState` remains the shared persistent room-stage binding for Drawing Room points, hiding spots, and Dining Room seats. Static binding maps an authored room-local point to the world-space actor root directly; `CharacterAnimationDisplay` continues to own only visual scale, and waypoint movement retains its separate optional visible-feet endpoint behavior.

**Tech Stack:** Unity 6000.4.10f1, C#, NUnit EditMode tests, Unity Test Framework

## Global Constraints

- Preserve every authored scene anchor X/Y value and do not modify `Assets/Scenes/Gameplay.unity`.
- Do not alter room-scale catalog values or character display-scale ownership.
- Do not change animation, sorting, occlusion, visibility, interaction, collision, dialogue, chapter state, or moving-waypoint behavior.
- Drawing Room points, Chapter 2 hiding spots, and Dining Room seats must use the same stable static-binding invariant.
- Static placement must never derive actor-root X/Y from live renderer bounds.

---

### Task 1: Add the static anchor regression

**Files:**
- Modify: `Assets/Editor/StoryActorRoomStageLockingTests.cs`
- Modify: `Assets/Editor/Chapter1GuestRoomVisibilityRegressionTests.cs`

**Interfaces:**
- Consumes: `ActorRoomState.PlaceAt(Transform)`, private `TryApplyRoomStageLocalBindingIfNeeded()` invoked by the existing reflection helper, and Chapter 1 `PlaceGuestAt(...)`.
- Produces: Regression coverage requiring exact actor-root-to-anchor alignment regardless of sprite bounds.

- [x] **Step 1: Replace the old bounds-based static-binding expectations**

Rename `RoomStageBindingKeepsVisibleFeetOnTheAnchorWithoutChangingScale` to `RoomStageBindingKeepsActorRootOnTheAnchorWithoutChangingScale`. Keep its deliberately center-pivoted sprite, but assert with `AssertActorLockedToAnchor(...)` before and after pan/zoom instead of `AssertVisibleFeetLockedToAnchor(...)`.

Rename `StaticBoundGuestUsesFinalAnimationDisplayScaleBeforeFeetAreAligned` to `StaticBoundGuestUsesFinalAnimationDisplayScaleWithoutMovingActorRoot`. Preserve its scale assertions and replace its visible-bounds assertion with `AssertActorLockedToAnchor(...)`.

Add `StaticRoomAnchorBindingIgnoresAnimationFrameBounds` using two center-pivoted sprites of different dimensions. Place the actor at the anchor, swap the renderer sprite, invoke the binding refresh, and assert the actor root stays locked to the anchor after both frames.

In `Chapter1GuestsKeepAuthoredStaticScaleWhileUsingRoomAnchors`, require the Chapter 1 placement helper to bind the Guest and assign the mapped anchor position directly. Reject `TryGetGuestFeetWorldPoint` inside that static helper.

- [x] **Step 2: Run the focused tests and verify RED**

Run:

```bash
/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity \
  -batchmode \
  -projectPath /home/hamza/dreadforge_2022_2 \
  -runTests \
  -testPlatform EditMode \
  -testFilter StoryActorRoomStageLockingTests \
  -testResults /tmp/GuestStaticAnchor-red.xml \
  -logFile /tmp/GuestStaticAnchor-red.log
```

Expected: the new/changed anchor-root tests fail because current static placement still adds renderer-bounds correction. Confirm compilation succeeds and failures are behavioral assertions, not test setup errors.

---

### Task 2: Make static room anchors authoritative

**Files:**
- Modify: `Assets/Scripts/Story/ActorRoomState.cs`
- Modify: `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs`

**Interfaces:**
- Consumes: the existing room-local binding, `CameraManager.TryGetActiveRoomStageWorldPoint(...)`, `CharacterAnimationDisplay.TryApplyScaleForRoom(...)`, and authored room-anchor positions.
- Produces: animation-independent static actor-root placement for all three affected staging systems.

- [x] **Step 1: Remove renderer-bounds correction from persistent static binding**

In `ActorRoomState.TryApplyRoomStageLocalBindingIfNeeded()`, retain room-stage mapping, world-Z preservation, and display-scale refresh. Remove the `CharacterFootPositionUtility.TryGetWorldPoint(...)` correction block so `targetTransform.position = worldPoint` is the final X/Y position write.

- [x] **Step 2: Remove renderer-bounds correction from immediate Chapter 1 placement**

Keep the existing `PlaceGuestFeetAtPosition(...)` integration point to minimize churn. Bind first so scale still resolves from the target room-local Y, then assign the supplied mapped anchor position directly to the Guest root while preserving its existing world Z. Remove the `TryGetGuestFeetWorldPoint(...)` offset subtraction from this helper.

- [x] **Step 3: Run focused tests and verify GREEN**

Run the same focused Unity command with result paths `/tmp/GuestStaticAnchor-green.xml` and `/tmp/GuestStaticAnchor-green.log`, then run `Chapter1GuestRoomVisibilityRegressionTests` with its own result and log paths.

Expected: all focused tests pass with zero compiler errors.

- [x] **Step 4: Inspect the diff for scope**

Run:

```bash
git diff --check
git status --short
git diff --stat
git diff -- Assets/Scripts/Story/ActorRoomState.cs Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs Assets/Editor/StoryActorRoomStageLockingTests.cs Assets/Editor/Chapter1GuestRoomVisibilityRegressionTests.cs
```

Expected: only the two runtime files, two test files, and this plan are changed; `Gameplay.unity`, scale catalog assets, animations, and prefabs are untouched.

---

### Task 3: Verify the integrated behavior

**Files:**
- Test: `Assets/Editor/StoryActorRoomStageLockingTests.cs`
- Test: `Assets/Editor/Chapter1GuestRoomVisibilityRegressionTests.cs`
- Test: `Assets/Editor/NavigationRegressionTests.cs`
- Test: existing EditMode test assembly

**Interfaces:**
- Consumes: completed Tasks 1-2.
- Produces: compiler, regression, and integration evidence for handoff.

- [x] **Step 1: Run all EditMode tests**

```bash
/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity \
  -batchmode \
  -projectPath /home/hamza/dreadforge_2022_2 \
  -runTests \
  -testPlatform EditMode \
  -testResults /tmp/GuestStaticAnchor-all-editmode.xml \
  -logFile /tmp/GuestStaticAnchor-all-editmode.log
```

Compare failures against the branch baseline. Expected: zero new failures and no C# compiler errors.

- [x] **Step 2: Run a focused PlayMode smoke check**

Use the existing gameplay/debug staging routes to exercise all eight Guests at Drawing Room points, verify the authored Chapter 2 hiding and Dining Room call paths, and run the real scene scale-architecture tests. Across multiple frames, pan states, and seated/standing state changes, verify the shared static binding remains stable and room/Y display scale continues to update.

- [x] **Step 3: Final scope and status check**

Run `git diff --check`, inspect `git status --short`, and confirm no Unity import/re-serialization changed unrelated assets.

- [x] **Step 4: Commit the verified implementation**

```bash
git add Assets/Editor/StoryActorRoomStageLockingTests.cs \
  Assets/Editor/Chapter1GuestRoomVisibilityRegressionTests.cs \
  Assets/Editor/NavigationRegressionTests.cs \
  Assets/Scripts/Story/ActorRoomState.cs \
  Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs \
  docs/superpowers/plans/2026-07-16-guest-static-anchor-vertical-alignment.md
git commit -m "Fix guest static anchor vertical alignment"
```
