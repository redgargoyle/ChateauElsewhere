# Localized Guest Presentation Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the five reported presentation regressions with room-specific sorting and HUD lifecycle changes, without producing executables.

**Architecture:** Keep the shared world-y system intact. Add a small optional front-occluder seam to the existing seated exception, use scene data only for the affected props, preserve a coat grab point during the requested scale increase, and make chapter HUD ownership mutually exclusive.

**Tech Stack:** Unity 2022.2, C#, TextMesh Pro, Unity 2D SpriteRenderer/SortingGroup, NUnit EditMode tests, Unity scene YAML.

## Global Constraints

- Make guest-carried coats exactly twice their current rendered width and height while preserving the hand contact point.
- Modify only the reported Drawing Room seats, `kitchen_work_table`, and anomalous Dining Room chair area.
- Do not change game-wide y-axis sorting behavior.
- Use bottom/pivot sort references for the affected Kitchen and Dining props.
- Do not create executables.

---

### Task 1: Double guest-carried coats around the grab point

**Files:**
- Modify: `Assets/Editor/CharacterAnimationArchitectureTests.cs`
- Modify: `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs`

**Interfaces:**
- Consumes: `ApplyAssignedCoatSprite`, `AlignWornCoatToAssignedAnchorHand`
- Produces: `GetWornCoatGrabPointInPlacementSpace(SpriteRenderer, SpriteRenderer, bool)` and full-size authored/fallback coats

- [ ] **Step 1: Write failing coat scale and grab-point tests**

Update the authored-coat expectations from `HalfScale` to `1f`, update fallback scale from `(0.2, 0.2, 1)` to `(0.4, 0.4, 1)`, and add a reflection test that records the hand-facing upper-inner coat point before a 2x scale change and verifies the helper restores it within `0.0001f`.

- [ ] **Step 2: Run the focused coat tests and verify RED**

Run:

```bash
Unity -batchmode -nographics -projectPath "$PROJECT_COPY" -runTests -testPlatform EditMode -testFilter CharacterAnimationArchitectureTests -testResults "$RESULTS_DIR/coat-red.xml" -quit
```

Expected: failures report the existing half-size authored bounds, `0.2f` fallback scale, or missing grab-point helper.

- [ ] **Step 3: Implement minimal coat scaling**

Set `WornCoatVisualScaleMultiplier` to `1f`, set `AssignedCoatFallbackScale` to `(0.4f, 0.4f, 1f)`, and preserve the hand-facing upper-inner grab point while changing the coat renderer scale. Keep waist alignment and anchor-side selection intact.

- [ ] **Step 4: Run the focused coat tests and verify GREEN**

Repeat the Task 1 test command with `coat-green.xml`; expect zero failures.

### Task 2: Add optional seated front-occluder ordering

**Files:**
- Modify: `Assets/Editor/ObjectCollisionBoxRegressionTests.cs`
- Modify: `Assets/Scripts/Characters/DiningRoomSeatedGuestOcclusionException.cs`
- Modify: `Assets/Scripts/Characters/DiningRoomSeatedGuestOcclusionController.cs`

**Interfaces:**
- Consumes: existing `ActivateForSeat(...)`
- Produces: an overload/parameter accepting `SpriteRenderer frontOccluderRenderer`, with order invariant `chairOrder < guestOrder < min(frontOrder, tableOrder)`

- [ ] **Step 1: Write failing order and restoration tests**

Create renderers with chair order `100`, guest original order `150`, front occluder order `200`, and table order `300`. Activate the exception and assert the guest becomes `199`, then deactivate and assert the guest's original sorting group state and front renderer state are restored.

- [ ] **Step 2: Run the focused exception tests and verify RED**

Run the `ObjectCollisionBoxRegressionTests` EditMode suite and expect the new front-occluder assertions to fail because the API/order path does not yet exist.

- [ ] **Step 3: Implement the optional front-occluder path**

Add the optional renderer to activation and serialized state. Compute the guest order immediately below the nearest front boundary, validate it remains above the assigned chair, and restore all captured state on deactivation. Preserve the existing behavior when the optional renderer is null.

- [ ] **Step 4: Run the focused exception tests and verify GREEN**

Repeat the Task 2 suite and expect zero failures.

### Task 3: Wire only the affected Drawing and Dining seats

**Files:**
- Modify: `Assets/Editor/Chapter1GuestRoomVisibilityRegressionTests.cs`
- Modify: `Assets/Editor/ObjectCollisionBoxRegressionTests.cs`
- Modify: `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs`
- Modify: `Assets/Scenes/Gameplay.unity`

**Interfaces:**
- Consumes: Task 2 optional front-occluder API
- Produces: Drawing Room serialized armrest references and one Dining Room front-chair binding

- [ ] **Step 1: Write failing scene-wiring tests**

Assert the Chapter 1 controller serializes `redcoucharmrest_0` and `greenchairarmrest_0` renderers and passes them only for overlapping seated guest indices. Assert the Dining Room controller binding for the reported right-back chair serializes its front renderer and all other bindings retain null front references.

- [ ] **Step 2: Run scene-wiring tests and verify RED**

Run the focused Chapter 1 visibility and object collision EditMode suites; expect missing serialized fields/references.

- [ ] **Step 3: Add the localized scene references**

Add two Drawing Room front-occluder fields, map only the affected seated indices, extend the Dining Room binding data, and serialize the existing scene renderer file IDs. Do not change unrelated seat anchors or renderer orders.

- [ ] **Step 4: Run scene-wiring tests and verify GREEN**

Repeat the focused suites and expect zero failures.

### Task 4: Give the Kitchen table bottom-pivot sorting ownership

**Files:**
- Modify: `Assets/Editor/Chapter2RegressionTests.cs`
- Modify: `Assets/Scenes/Gameplay.unity`

**Interfaces:**
- Consumes: existing `WorldYSortSpriteRenderer`
- Produces: exactly one sorting writer for `kitchen_work_table`, referenced at its authored bottom pivot

- [ ] **Step 1: Write a failing Kitchen table scene test**

Assert `kitchen_work_table` has `WorldYSortSpriteRenderer`, `forcePivotSortPoint: 1`, and `yReference` set to its own transform. Assert `PlayerBlocker_kitchen_work_table` retains collision but has `sortSourceRenderers: 0`.

- [ ] **Step 2: Run the Chapter 2 regression suite and verify RED**

Expected: the table lacks the sorter and the blocker still owns source sorting.

- [ ] **Step 3: Update only the Kitchen table scene components**

Add the sorter component to file ID `618835546`, reference transform `618835547`, and change only that blocker's `sortSourceRenderers` to `0`.

- [ ] **Step 4: Run the Chapter 2 regression suite and verify GREEN**

Repeat the suite and expect zero failures.

### Task 5: Make Chapter HUD status ownership exclusive

**Files:**
- Modify: `Assets/Editor/NavigationRegressionTests.cs`
- Modify: `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1InteractionHUD.cs`
- Modify: `Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Controller.cs`

**Interfaces:**
- Produces: `Chapter1InteractionHUD.SetStatusVisible(bool visible)`
- Consumes: Chapter 2 `BeginChapter2`

- [ ] **Step 1: Write failing HUD lifecycle tests**

Assert hidden Chapter 1 status stays inactive across `Update`, Chapter 1 initialization enables status ownership, and `BeginChapter2` finds the Chapter 1 HUD and calls `SetStatusVisible(false)`.

- [ ] **Step 2: Run Navigation regression tests and verify RED**

Expected: missing `SetStatusVisible` API and missing Chapter 2 handoff call.

- [ ] **Step 3: Implement mutually exclusive HUD ownership**

Store a `statusVisible` flag in Chapter 1 HUD, make `Update` respect it, enable it in `Initialize`, and disable it from Chapter 2 startup. Do not reposition or resize either TMP block.

- [ ] **Step 4: Run Navigation regression tests and verify GREEN**

Repeat the suite and expect zero failures.

### Task 6: Integrated editor-only verification

**Files:**
- Verify only; no production edits unless a test exposes a scoped regression.

- [ ] **Step 1: Run all focused suites**

Run EditMode tests for:

```text
CharacterAnimationArchitectureTests
ObjectCollisionBoxRegressionTests
Chapter1GuestRoomVisibilityRegressionTests
Chapter2RegressionTests
NavigationRegressionTests
```

Expected: zero failures and no compile errors.

- [ ] **Step 2: Review the final diff against constraints**

Confirm no build scripts, player settings, unrelated rooms, shaders, or executable artifacts changed. Confirm `git status --short` lists only the approved code, tests, scene, and planning documents.

- [ ] **Step 3: Do not build**

Do not invoke Unity `BuildPipeline`, `BuildPlayer`, platform build scripts, or the Chantilly desktop output path.

