# Shared Guest Speech Movement Pause Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Freeze any moving guest whose shared non-overlapping speech must queue, keep the guest idle through delivery, and resume the same movement afterward.

**Architecture:** `DialogueSpeechService` remains the only dialogue queue and owns movement-pause leases. It reuses `SpeakingCharacterIndicator` for guest actor resolution and pauses the existing `NPCWaypointMover`, whose movement coroutine retains its target while paused.

**Tech Stack:** Unity 6.4, C#, NUnit/Edit Mode tests, Unity Test Framework

## Global Constraints

- Apply automatically to every guest using `DialogueSpeechService`, across chapters.
- Do not add a second dialogue queue or chapter-specific speech system.
- Preserve the existing speech serialization, skip, cancellation, and overlap behavior.
- Preserve unrelated worktree changes.

---

### Task 1: Add the failing shared-queue movement regression

**Files:**
- Create: `Assets/Editor/DialogueSpeechMovementRegressionTests.cs`
- Create: `Assets/Editor/DialogueSpeechMovementRegressionTests.cs.meta`

**Interfaces:**
- Consumes: `DialogueSpeechService.SpeakLine(...)`, `DialogueSpeechService.SkipCurrentSpeech()`, `NPCWaypointMover.MoveToRoutine(Transform)`
- Produces: A regression proving a real moving guest stays still from queue wait through speech completion and then resumes the same move.

- [x] **Step 1: Write the failing integration test**

Create an actor named `Guest1` with `ActorRoomState` and `NPCWaypointMover`, start its real movement enumerator, start a Butler speech enumerator to occupy normal speech, then start a Guest 1 speech enumerator. Advance the guest movement while its speech waits and assert its position is unchanged; skip the Butler line, advance the guest line, assert it remains unchanged while speaking; skip the guest line and assert the same mover enumerator advances again.

- [x] **Step 2: Run the test to verify it fails for the missing queue pause**

Run:

```bash
/home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -projectPath /home/hamzak/Desktop/ChateauChantilly -runTests -testPlatform EditMode -testFilter DialogueSpeechMovementRegressionTests -testResults Logs/GuestSpeechPause/red.xml -logFile Logs/GuestSpeechPause/red.log
```

Expected: FAIL because the guest position advances while its line waits.

### Task 2: Add resumable idle pause to the existing mover

**Files:**
- Modify: `Assets/Scripts/Story/NPCWaypointMover.cs`

**Interfaces:**
- Produces: `AcquireSpeechPause()`, `ReleaseSpeechPause()`, and a reference-counted pause state consumed by `DialogueSpeechService`.

- [x] **Step 1: Add reference-counted speech pause ownership**

Add a pause count and first-lease ambient-walker state. Acquiring the first lease resolves references, disables an active ambient walker, and applies the idle animator state. Releasing the final lease restores only the ambient walker state owned by the lease.

- [x] **Step 2: Preserve movement destinations while paused**

In both transform and projected movement loops, yield without changing position whenever a speech pause is held and keep `isMoving` true. This retains the original target and allows the same coroutine to continue after release.

### Task 3: Reuse speaker resolution and own leases in the shared speech service

**Files:**
- Modify: `Assets/Scripts/UI/SpeakingCharacterIndicator.cs`
- Modify: `Assets/Scripts/Audio/DialogueSpeechService.cs`

**Interfaces:**
- Produces: `SpeakingCharacterIndicator.TryResolveGuestSpeakerTarget(string lineId, string speaker, out Transform target, out ActorRoomState actor)`
- Consumes: `NPCWaypointMover.AcquireSpeechPause()` and `ReleaseSpeechPause()`

- [x] **Step 1: Expose the existing guest resolution path**

Refactor `SpeakingCharacterIndicator` so its existing guest-number/display-name/named-actor matching is available through one public static guest-target resolver, while `ShowForSpeechLine` continues using the same logic.

- [x] **Step 2: Acquire only when a guest line actually waits**

In `SpeakLineRoutine`, before entering the existing `while (!allowOverlap && normalSpeechActive)` wait, resolve the guest and acquire one tracked mover lease only when normal speech is already active.

- [x] **Step 3: Hold through delivery and release on every exit**

Keep the lease through line resolution and playback. Release it through one idempotent helper on successful completion and all early exits. `CancelQueuedSpeech` and `OnDisable` synchronously release every outstanding tracked lease.

- [x] **Step 4: Run the focused test to verify green**

Run the Task 1 Unity command with `green.xml` and `green.log` output paths.

Expected: PASS, 0 failures.

### Task 4: Verify relevant regressions and inspect scope

**Files:**
- Verify only; no additional production files unless a test exposes a defect.

- [x] **Step 1: Run focused movement and dialogue suites**

Run Edit Mode tests filtered to `DialogueSpeechMovementRegressionTests`, `StoryActorRoomStageLockingTests`, and the dialogue-related `Chapter2RegressionTests` fixture.

- [x] **Step 2: Run the full Edit Mode suite**

Run Unity with `-testPlatform EditMode` and no test filter. Expected: all tests pass with zero failures.

Actual: 180/231 passed. The 51 failures are pre-existing branch/scene/source-contract failures outside this change; both new regression tests passed in the same run and the compiler reported no C# errors.

- [x] **Step 3: Inspect the final diff**

Confirm only the shared speech service, shared actor resolver, existing mover, regression test, and these design/plan files changed. Confirm the pre-existing font, scene, and package changes are untouched.
