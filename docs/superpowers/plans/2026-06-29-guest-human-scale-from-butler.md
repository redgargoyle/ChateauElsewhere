# Guest Human Scale From Butler Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make standing/walking/running guests visually match the Butler's manually calibrated human scale in every room, with small explicit pose exceptions for seated/special guests.

**Architecture:** Butler room front/back calibration remains the room human-scale source of truth. Guests do not copy Butler `Transform.localScale` directly; instead, one final late-running guest scale writer fits each guest's visible body height to the Butler-derived target height for that room/depth. Legacy guest scale paths become data providers only, not final visible-size writers.

**Tech Stack:** Unity C#, EditMode regression tests, existing `PointClickPlayerMovement`, `RoomProjectedEntity`, `RoomPersonWalker2D`, `ActorRoomState`, and `CharacterVisualProfile`.

## Global Constraints

- Work from latest `main`.
- Do not change Butler calibration values.
- Do not change guest art.
- Do not use broad per-room hardcoded guest scale hacks.
- Existing Butler scaling remains unchanged.
- Existing guest position, tint, sorting, animation, and room movement behavior must remain unchanged.
- Normal standing/walking/running guests use Butler-derived room human scale automatically.
- Seated Drawing Room, seated Dining Room, crouching, lying, hiding, and special furniture poses may use explicit pose multipliers.
- The tool must be simple: one enable/apply path, one proof path, one optional manual override path.

---

## Current Root Cause

`main` already contains the Butler final-local-scale calibration data and an attempted guest harmonizer, but guest visual size is still unstable because multiple code paths write guest scale:

- `RoomProjectedEntity.ApplyProjectedScale()` multiplies guest `authoredVisualRootScale` by Butler normalized scale and `CharacterVisualProfile.HeightScaleMultiplier`.
- `RoomProjectedEntity.ForceApplyButlerCharacterScale()` repeats that same guest-base multiplication.
- `RoomPersonWalker2D.ApplyButlerScaleSample()` preserves walker authored local scale and multiplies by Butler normalized scale.
- `ActorRoomState.ApplyButlerCharacterScaleNow()` multiplies Butler final local scale by actor authored scale.
- `ActorRoomState.ApplyRoomStageMotionDeltaIfNeeded()` and room-stage binding can rewrite actor scale again.
- `Chapter1ArrivalController.PreserveGuestAuthoredScale()` enables `ActorRoomState.scaleWithRoomStageMotion`, which explains why taking/returning coats can make guests suddenly switch size modes.
- `GuestButlerScaleHarmonizer` runs late, but it calls those legacy scale methods instead of owning final visible size itself.

The fix is not "make guests bigger." The fix is "one final visible-size writer, using Butler calibration as room human target."

---

## File Structure

- Modify: `Assets/Scripts/PointClickPlayerMovement.cs`
  - Expose stable Butler human-scale reference helpers without changing runtime Butler scale behavior.
- Create: `Assets/Scripts/Characters/CharacterVisibleBodyScaleUtility.cs`
  - Resolve visible body art, measure visible body height, and apply a target visible height to a scale root.
- Create: `Assets/Scripts/Characters/RoomHumanScaleService.cs`
  - Convert Butler room calibration into a room/depth standing human target.
- Create: `Assets/Scripts/Characters/GuestHumanScaleSubject.cs`
  - Lightweight component/data bridge for guest room id, foot point, pose, scale root, bounds root, and optional fine tune.
- Modify: `Assets/Scripts/Characters/GuestButlerScaleHarmonizer.cs`
  - Replace current legacy-method calls with the single final visible-size write path.
- Modify: `Assets/Scripts/Characters/RoomProjectedEntity.cs`
  - Keep projection/tint/sorting/position. Stop applying final Butler guest size internally when final human scale is active.
- Modify: `Assets/Scripts/Characters/RoomPersonWalker2D.cs`
  - Keep walking/animation/position. Stop applying final Butler guest size internally when final human scale is active.
- Modify: `Assets/Scripts/Story/ActorRoomState.cs`
  - Keep room-stage motion and actor state. Stop acting as final human scale writer when a projected/walker/subject guest exists.
- Modify: `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs`
  - Stop coat/preservation flow from activating a conflicting final scale mode.
- Modify: `Assets/Editor/GuestButlerScaleTool.cs`
  - Simplify to one enable/apply workflow plus proof and optional manual override controls.
- Create: `Assets/Scripts/Characters/GuestHumanScaleOverrideStore.cs`
  - Store rare pose/manual fine-tune overrides only.
- Modify: `Assets/Editor/GuestButlerScaleRegressionTests.cs`
  - Replace tests that expect legacy scale calls with tests proving one final writer wins.
- Modify: `Assets/Editor/RoomProjectionRegressionTests.cs`
  - Preserve Butler calibration and projection behavior tests.
- Modify: `Assets/Scripts/Characters/README.md`
  - Document the final rule and editor workflow.

---

## Task 1: Lock The Current Failure With Tests

**Files:**
- Modify: `Assets/Editor/GuestButlerScaleRegressionTests.cs`

**Interfaces:**
- Consumes: existing `GuestButlerScaleHarmonizer`, `RoomProjectedEntity`, `RoomPersonWalker2D`, `ActorRoomState`, `PointClickPlayerMovement.ButlerCharacterScaleSample`.
- Produces: failing tests that prove the old architecture is wrong.

- [ ] **Step 1: Add tests for duplicate scale writers**

Add tests that assert the harmonizer text no longer calls:

```csharp
ApplyButlerCharacterScaleNow(source, debugGuestScaleMultiplier)
ApplyButlerScaleSample
BuildButlerActorScale
```

Expected initial result: FAIL, because `GuestButlerScaleHarmonizer` currently calls legacy methods.

- [ ] **Step 2: Add a coat-mode regression test**

Add a text regression around `Chapter1ArrivalController` requiring coat/guest preparation not to use `SetScaleWithRoomStageMotion(true)` as a final visible-size system when final human scale is active.

Expected initial result: FAIL.

- [ ] **Step 3: Add a direct-size regression**

Create a test scene object with:

```csharp
Butler final local scale Y = 1.90 at room-local Y -100
Guest authored local scale Y = 100
Guest visible body height = 200
Butler reference body height = 300
```

Assert the guest target visible height is based on Butler visible human target, not `100 * normalizedScale`.

Expected initial result: FAIL.

- [ ] **Step 4: Run the focused tests**

Run:

```bash
dotnet build Assembly-CSharp.csproj
dotnet build Assembly-CSharp-Editor.csproj
```

Expected: builds still pass; text/logic tests should fail until later tasks are implemented.

---

## Task 2: Add A Visible Body Measurement Utility

**Files:**
- Create: `Assets/Scripts/Characters/CharacterVisibleBodyScaleUtility.cs`
- Test: `Assets/Editor/GuestButlerScaleRegressionTests.cs`

**Interfaces:**
- Produces:
  - `CharacterVisibleBodyScaleUtility.VisualTarget`
  - `TryResolveVisibleBodyTarget(Transform candidateRoot, Camera camera, out VisualTarget target, bool includeInactive = false)`
  - `TryMeasureVisibleHeight(VisualTarget target, Camera camera, out float height)`
  - `TryApplyTargetVisibleHeight(Transform preferredScaleRoot, Transform boundsRoot, Camera camera, float targetHeight, out FitResult result)`

- [ ] **Step 1: Write utility tests**

Tests must verify:

- Enabled `SpriteRenderer`, `Renderer`, and `UnityEngine.UI.Graphic` count as visible body art.
- Containers named `Canvas`, `Rooms`, `People`, and names starting with `Room_` are not selected as scale roots.
- Visual names containing `Shadow`, `Bubble`, `Speech`, `Thought`, `Cursor`, `Marker`, `Icon`, `Tooltip`, `Prompt`, `Interact`, or `Highlight` are ignored.
- Screen Space Overlay graphics are measured with a null camera.
- If scaling the preferred root does not change measured height, the utility restores it and scales the primary visual as fallback.

- [ ] **Step 2: Implement target resolution**

Implementation rule:

```csharp
// Choose the largest visible body renderer/graphic under candidateRoot.
// BoundsRoot may be candidateRoot when it contains only this character's visible art.
// ScaleRoot is preferredScaleRoot when valid, otherwise PrimaryVisual.
```

- [ ] **Step 3: Implement target-height fitting**

Implementation rule:

```csharp
ratio = targetHeight / beforeHeight;
scaleRoot.localScale = ScaleXY(scaleRoot.localScale, ratio);
```

Then re-measure. If height does not move in the expected direction, restore and retry the primary visual.

- [ ] **Step 4: Verify**

Run:

```bash
dotnet build Assembly-CSharp.csproj
dotnet build Assembly-CSharp-Editor.csproj
```

Expected: builds pass; new utility tests pass.

---

## Task 3: Convert Butler Calibration Into Human-Scale Samples

**Files:**
- Create: `Assets/Scripts/Characters/RoomHumanScaleService.cs`
- Modify: `Assets/Scripts/PointClickPlayerMovement.cs`
- Test: `Assets/Editor/GuestButlerScaleRegressionTests.cs`

**Interfaces:**
- Produces:

```csharp
public readonly struct HumanScaleSample
{
    public readonly string RoomId;
    public readonly Vector2 RoomLocalFootPoint;
    public readonly float Depth01;
    public readonly float NormalizedStandingScale;
    public readonly float ButlerFinalLocalScaleY;
    public readonly float ButlerBaseLocalScaleY;
    public readonly string Source;
}

public static bool TryEvaluateStandingScale(
    PointClickPlayerMovement butlerSource,
    string roomId,
    Vector2 roomLocalFootPoint,
    out HumanScaleSample sample)
```

- [ ] **Step 1: Add tests around Butler calibrated rooms**

Given Butler front/back final local scale values, assert:

- At front Y, `ButlerFinalLocalScaleY` equals front final local scale.
- At back Y, `ButlerFinalLocalScaleY` equals back final local scale.
- Midpoint interpolates exactly once.
- `NormalizedStandingScale` equals `finalLocalScaleY / ButlerBaseLocalScaleY`.

- [ ] **Step 2: Implement service by delegating to `PointClickPlayerMovement.TryEvaluateButlerCharacterScale`**

Do not re-parse scene YAML and do not duplicate room matching rules.

- [ ] **Step 3: Add Butler visible reference helper**

Add:

```csharp
public bool TryGetButlerStandingReferenceVisibleHeight(Camera camera, out float referenceHeight, out string diagnostic)
```

It should compute:

```csharp
referenceHeight = currentButlerVisibleBodyHeight / currentHumanScaleSample.NormalizedStandingScale;
```

If current room has no Butler calibration, return false with a diagnostic.

- [ ] **Step 4: Verify**

Run both C# builds and focused tests.

---

## Task 4: Add GuestHumanScaleSubject As The Data Bridge

**Files:**
- Create: `Assets/Scripts/Characters/GuestHumanScaleSubject.cs`
- Modify: `Assets/Scripts/Characters/RoomProjectedEntity.cs`
- Modify: `Assets/Scripts/Characters/RoomPersonWalker2D.cs`
- Modify: `Assets/Scripts/Story/ActorRoomState.cs`
- Test: `Assets/Editor/GuestButlerScaleRegressionTests.cs`

**Interfaces:**
- Produces:

```csharp
public enum CharacterPoseKind
{
    Auto,
    Standing,
    Seated,
    Crouching,
    Lying
}

public sealed class GuestHumanScaleSubject : MonoBehaviour
{
    public bool TryResolveRoomAndFootPoint(out string roomId, out Vector2 roomLocalFootPoint);
    public bool TryResolveVisualTarget(Camera camera, out CharacterVisibleBodyScaleUtility.VisualTarget target);
    public CharacterPoseKind ResolvePose();
    public float FineTuneMultiplier { get; }
}
```

- [ ] **Step 1: Add tests for room/foot source priority**

Priority:

1. `RoomProjectedEntity` room id and `roomLocalFootPoint`.
2. `RoomPersonWalker2D` room id and rendered/current position.
3. `ActorRoomState` bound room-stage point.
4. Parent `RoomContentGroup` inverse transform fallback.

- [ ] **Step 2: Implement `GuestHumanScaleSubject`**

It should be tiny and inspect nearby components. It should not write scale.

- [ ] **Step 3: Expose safe read-only helpers from guest components**

Add read-only methods/properties only where needed. Do not change their movement behavior.

- [ ] **Step 4: Verify**

Run both C# builds and focused tests.

---

## Task 5: Make GuestButlerScaleHarmonizer The Only Final Guest Size Writer

**Files:**
- Modify: `Assets/Scripts/Characters/GuestButlerScaleHarmonizer.cs`
- Modify: `Assets/Scripts/Characters/RoomProjectedEntity.cs`
- Modify: `Assets/Scripts/Characters/RoomPersonWalker2D.cs`
- Modify: `Assets/Scripts/Story/ActorRoomState.cs`
- Test: `Assets/Editor/GuestButlerScaleRegressionTests.cs`

**Interfaces:**
- Consumes:
  - `RoomHumanScaleService.TryEvaluateStandingScale`
  - `CharacterVisibleBodyScaleUtility.TryApplyTargetVisibleHeight`
  - `GuestHumanScaleSubject`

- [ ] **Step 1: Remove legacy final-size calls from the harmonizer**

`GuestButlerScaleHarmonizer` must not call:

```csharp
RoomProjectedEntity.ApplyButlerCharacterScaleNow
RoomPersonWalker2D.ApplyButlerCharacterScaleNow
ActorRoomState.ApplyButlerCharacterScaleNow
```

- [ ] **Step 2: Apply final target height directly**

For every guest subject:

```csharp
standingTargetHeight =
    butlerStandingReferenceHeight *
    humanScaleSample.NormalizedStandingScale;

finalTargetHeight =
    standingTargetHeight *
    poseRatio *
    subject.FineTuneMultiplier *
    debugGuestScaleMultiplier;
```

Then call:

```csharp
CharacterVisibleBodyScaleUtility.TryApplyTargetVisibleHeight(...)
```

- [ ] **Step 3: Ensure the harmonizer runs late**

Keep:

```csharp
[DefaultExecutionOrder(10000)]
```

This means RoomProjectedEntity, ActorRoomState, and walker movement can still position/animate first. Harmonizer wins only visible size at the end.

- [ ] **Step 4: Disable internal final human scale when harmonizer is active**

Add fields:

```csharp
[SerializeField] private bool deferFinalHumanScaleToHarmonizer = true;
```

In `RoomProjectedEntity` and `RoomPersonWalker2D`, this prevents Butler guest scaling from writing final scale while preserving position/tint/sorting/animation.

- [ ] **Step 5: Prevent ActorRoomState duplicate targets**

If an actor has a child `RoomProjectedEntity`, child `RoomPersonWalker2D`, or `GuestHumanScaleSubject`, `ActorRoomState` must not be treated as an independent visual scale target.

- [ ] **Step 6: Verify**

Run focused tests proving:

- Proof 50% changes every detected guest.
- Proof 150% changes every detected guest.
- Running the proof repeatedly is idempotent.
- Coat pickup/return does not switch guests into a different size mode.
- Guests at the same room-local Y as Butler match visible body height within tolerance.

---

## Task 6: Add Pose And Manual Fine-Tune Overrides Only For Exceptions

**Files:**
- Create: `Assets/Scripts/Characters/GuestHumanScaleOverrideStore.cs`
- Modify: `Assets/Scripts/Characters/GuestButlerScaleHarmonizer.cs`
- Test: `Assets/Editor/GuestButlerScaleRegressionTests.cs`

**Interfaces:**
- Produces:

```csharp
public sealed class GuestHumanScaleOverrideStore : ScriptableObject
{
    public bool TryGetOverride(string roomId, string guestKey, out GuestHumanScaleOverride entry);
}
```

Override fields:

```csharp
enabled
roomId
guestKey
pose
manualPoseHeightRatio
manualFineTuneMultiplier
scaleRoot
boundsRoot
```

- [ ] **Step 1: Add tests proving normal standing guests do not require overrides**

Standing/walking/running guests should work with no override entry.

- [ ] **Step 2: Add seated ratio tests**

Use `CharacterVisualProfile.SittingVisualHeight / StandingVisualHeight` when available. Clamp seated ratio to `0.55f` through `0.80f`. Default seated ratio is `0.68f`.

- [ ] **Step 3: Implement override store**

Only apply manual fine-tune for configured special entries.

- [ ] **Step 4: Verify**

Run focused tests and C# builds.

---

## Task 7: Simplify The Editor Tool

**Files:**
- Modify: `Assets/Editor/GuestButlerScaleTool.cs`
- Test: `Assets/Editor/GuestButlerScaleRegressionTests.cs`

**Required UI:**

1. `ENABLE AUTOMATIC GUEST HUMAN SCALE`
2. `REFRESH GUEST HUMAN SCALE NOW`
3. `PROOF: SHRINK GUESTS TO 50%`
4. `PROOF: GROW GUESTS TO 150%`
5. `RESTORE REAL GUEST SCALE`
6. `SELECT PROBLEM GUEST`
7. `OPTIONAL MANUAL FINE TUNE FOR SELECTED GUEST`
8. `SAVE SCENE`

- [ ] **Step 1: Remove confusing multi-step buttons**

Remove or hide old workflow labels that imply several setup phases.

- [ ] **Step 2: Make enable automatic**

The enable button must:

- Ensure one `GuestButlerScaleHarmonizer` on the Butler.
- Add `GuestHumanScaleSubject` to each guest visual root that lacks one.
- Set guest components to defer final visible human scale to the harmonizer.
- Not change Butler calibration.
- Not change guest art.

- [ ] **Step 3: Keep proof mode simple**

Proof buttons should temporarily multiply final target height by `0.5` or `1.5`, then `RESTORE REAL GUEST SCALE` resets multiplier to `1`.

- [ ] **Step 4: Verify**

Run tests proving proof mode visibly changes every detected guest.

---

## Task 8: Fix Chapter 1 Coat/Arrival Scale Mode Switching

**Files:**
- Modify: `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs`
- Modify: `Assets/Scripts/Story/ActorRoomState.cs`
- Test: `Assets/Editor/GuestButlerScaleRegressionTests.cs`

- [ ] **Step 1: Add test for coat state stability**

Test that `PreserveGuestAuthoredScale`, coat transfer, and coat return do not enable a competing final guest scale path when `GuestHumanScaleSubject` or harmonizer is active.

- [ ] **Step 2: Update `PreserveGuestAuthoredScale`**

Keep disabling player-only `PointClickPlayerMovement` on guests, but do not use `SetScaleWithRoomStageMotion(true)` as final human scale when harmonizer is active.

- [ ] **Step 3: Update `ActorRoomState` room-stage scale logic**

Room-stage motion may preserve position and stage compensation, but final visible human height must be overwritten by harmonizer at the end of the frame.

- [ ] **Step 4: Verify**

Manual test:

1. Start Chapter 1.
2. Let first guests enter Grand Entrance Hall.
3. Compare guests next to Butler.
4. Take a coat.
5. Return or store a coat.
6. Confirm guests do not snap into a new size mode.

---

## Task 9: Documentation And Main Integration

**Files:**
- Modify: `Assets/Scripts/Characters/README.md`

- [ ] **Step 1: Document final rule**

Add:

```markdown
Guest human scale rule:
- Butler room calibration is the room human-scale source of truth.
- Guests use the same room/depth human target as Butler.
- Guests are fitted by visible body height, not raw Transform.localScale.
- Room visual overrides and visual profile height multipliers do not decide final standing guest size when automatic human scale is active.
- Manual overrides are only for seated/special poses.
```

- [ ] **Step 2: Document testing checklist**

Checklist:

1. Grand Entrance Hall: guests standing next to Butler match height.
2. Drawing Room: guests standing next to Butler match height.
3. Guests still grow toward camera and shrink away from camera.
4. Coat pickup/return does not change guest scale mode.
5. Seated guests remain intentionally seated-height.
6. Proof 50/150 visibly changes every detected guest.

- [ ] **Step 3: Build**

Run:

```bash
dotnet build Assembly-CSharp.csproj
dotnet build Assembly-CSharp-Editor.csproj
```

Expected: both builds pass.

- [ ] **Step 4: Commit to main**

Commit in small pieces while implementing. Final merge target is `main`.

Suggested final commit:

```bash
git add Assets/Scripts/Characters Assets/Scripts/Story Assets/_Chateau/Scripts/Chapter/Chapter01 Assets/Editor Assets/Scripts/Characters/README.md
git commit -m "Unify guest human scale with Butler calibration"
git push origin main
```

---

## Acceptance Criteria

- `main` contains the fix.
- Butler calibration values are unchanged.
- Guests do not use raw Butler `localScale` directly.
- Standing/walking/running guests match Butler visible body height at the same room-local depth.
- Guests still scale larger toward the camera and smaller away from the camera.
- Grand Entrance Hall guests match Butler when standing beside him.
- Drawing Room standing guests match Butler when standing beside him.
- Coat pickup/return does not trigger a guest size jump.
- Seated/special pose guests can use explicit pose ratio or manual fine-tune overrides.
- Proof 50/150 affects every detected guest.
- There is one final guest visible-size writer: `GuestButlerScaleHarmonizer`.
- Legacy guest scale methods may remain for compatibility but are not called by the final harmonizer path.
- Runtime and editor C# builds pass.
