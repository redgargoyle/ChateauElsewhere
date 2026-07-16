# Character Size Phase 1 Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove every legacy Butler/Guest body-size owner and serialized store while preserving static authored character scales, room placement, animation, orientation, sorting, and occlusion, stopping before the Phase 2 universal sizing system.

**Architecture:** First freeze the existing calibration and pose data into a tested, non-runtime JSON snapshot. Then neutralize secondary writers before removing the guard components that currently suppress them, remove Butler scale ownership while retaining room-stage position conversion, migrate scene/prefab YAML through Unity APIs, delete the guest-scale stack, and finish by constraining projection/profile/editor tooling to non-character responsibilities.

**Tech Stack:** Unity 6000.4.10f1, C#, NUnit/EditMode Unity Test Framework 1.6.0, Unity YAML serialization, Git.

## Global Constraints

- Do not implement a universal scale catalog, controller, scale curve, participant, or editor window in Phase 1.
- Preserve point-and-click movement, guest waypoint movement, room transitions, room-stage position conversion, authored anchors, Animator state, sprite replacement, visibility, interaction, Y sorting, Dining/Drawing Room occlusion, coats, held items, click targets, shadows, and speech bubbles.
- Butler/Guest body-size magnitude must end Phase 1 with no runtime evaluator or writer; characters retain authored static scales.
- Neutralize dormant writers before deleting `GuestScaleParticipant`; deleting the guard first would reactivate fallback scaling.
- Keep `PointClickPlayerMovement.currentRoomStageScaleRatio` and room-stage coordinate conversion because they also control position.
- Keep `RoomPerspectiveProfile` near/far depth normalization while it is required by tint, sorting, shadow, and floor geometry; remove character-size consumers and only remove scale fields after consumers are gone.
- Preserve numeric values of `RoomProjectedEntity.ProjectionMode`; live serialized instances use value `4`.
- Do not blanket-delete `localScale` writes belonging to unrelated props, UI, effects, shadows, click targets, speech bubbles, or presentation-only children.
- Do not use fuzzy name inference, runtime `Find*`, `Ensure*`, `AddComponent`, or compatibility shells for scale ownership.
- Use Unity scene/prefab APIs for component/property migration; do not leave missing-script YAML.
- The legacy snapshot is documentation only. Runtime assemblies must not read it.
- The untouched baseline at `2a92396176c2` is 279 EditMode tests: 226 passed and 53 failed. Focused Phase 1 suites must pass, and the final full run must introduce no failures beyond the recorded baseline.
- Test invocations that use `-runTests` must omit `-quit`; Unity 6000.4.10f1 exits before running tests when both are supplied in this project.
- Do not push or merge this branch.

---

## File Structure

- `docs/architecture/character-scale-phase1-ownership-audit.md`: complete ownership matrix, baseline evidence, retained responsibilities, and final risk ledger.
- `docs/migrations/character-scale/legacy-character-scale-snapshot.json`: canonical Phase 2 migration evidence; no runtime reader.
- `Assets/Editor/CharacterScaleOwnershipRegressionTests.cs`: snapshot, sole-writer, scene GUID, animation, pose, and invariant behavior guardrails.
- `Assets/Editor/CharacterScaleOwnershipAudit.cs`: optional read-only menu validation; it may report but never repair or create.
- `Assets/Scripts/PointClickPlayerMovement.cs`: retain movement/position/sorting and remove Butler sizing.
- `Assets/Scripts/Story/ActorRoomState.cs`: retain identity/room/visibility/seating/position binding and remove all scale ownership.
- `Assets/Scripts/Characters/RoomProjectedEntity.cs`: retain generic projection position/tint/sorting/shadow responsibilities and remove managed-character scale ownership.
- `Assets/Scripts/Characters/RoomPersonWalker2D.cs`: retain walker movement/animation/tint and remove depth/body scale ownership.
- `Assets/Scripts/Characters/RoomPerspectiveProfile.cs`: retain room/depth/tint/sort/shadow/floor responsibilities; remove character scale data after consumers are gone.
- `Assets/Scripts/CharacterController2D.cs`: replace root-scale facing with renderer-only orientation.
- `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs`: remove scale factories, refresh, inference, and preservation behavior.
- `Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestPanicController.cs`: retain panic presentation/motion while removing actor-root scale capture, compensation, and restore.
- `Assets/Editor/PlayModeLayoutCaptureWindow.cs`: retain anchor/layout capture but reject roster body transforms.
- `Assets/Editor/CharacterAnimationAssetBuilder.cs`: preserve all sitting override mappings during rebuild.
- `Assets/Editor/Guest2ButlerAnimationAssetBuilder.cs`: remove automatic Gameplay open/save mutation or retire the one-off builder.
- `Assets/Scenes/Gameplay.unity` and `Assets/Prefabs/Player.prefab`: remove old components/objects/property modifications while preserving roots, positions, Animator overrides, anchors, seats, and occlusion.
- Delete after callers and serialization are clean: `GuestRoomScaleCalibration`, `GuestRoomScaleApplier`, `GuestRoomStageScaleUtility`, `GuestScaleParticipant`, `CharacterVisualProfile`, old calibration windows, old projection scale inspector, and their `.meta` files.

### Task 1: Freeze migration evidence and establish Phase 1 guardrails

**Files:**
- Create: `docs/architecture/character-scale-phase1-ownership-audit.md`
- Create: `docs/migrations/character-scale/legacy-character-scale-snapshot.json`
- Create: `Assets/Editor/CharacterScaleOwnershipRegressionTests.cs`
- Create: `Assets/Editor/CharacterScaleOwnershipRegressionTests.cs.meta`

**Interfaces:**
- Consumes: current serialized data at baseline `2a92396176c2` from Gameplay, Player, Drawing/Dining profiles, and the eight guest override controllers.
- Produces: canonical JSON schema version `1`, a reusable `CharacterScaleOwnershipRegressionTests` fixture, and the evidence contract every later task extends.

- [ ] **Step 1: Add the failing snapshot contract test**

Create `CharacterScaleOwnershipRegressionTests.cs` with constants for the snapshot, Gameplay, Player, and profile paths. Define serializable DTOs sufficient for `JsonUtility.FromJson<LegacySnapshot>()` to expose `schemaVersion`, `source`, `butler.roomOverrides`, `guestRoomCalibration.rooms`, `guests`, `roomPerspectiveProfiles`, Drawing/Dining assignments, and `integrity.expectedCounts`.

Add this test before the JSON exists:

```csharp
[Test]
public void LegacySnapshotPreservesCompletePhaseOneMigrationEvidence()
{
    Assert.That(File.Exists(SnapshotPath), Is.True, "Phase 1 must freeze legacy values before deleting their owners.");
    LegacySnapshot snapshot = JsonUtility.FromJson<LegacySnapshot>(File.ReadAllText(SnapshotPath));

    Assert.That(snapshot.schemaVersion, Is.EqualTo(1));
    Assert.That(snapshot.source.gitCommit, Is.EqualTo("2a92396176c2baa6310e42f9ee906ee846d94e03"));
    Assert.That(snapshot.source.unityVersion, Is.EqualTo("6000.4.10f1"));
    Assert.That(snapshot.butler.roomOverrides, Has.Length.EqualTo(19));
    Assert.That(snapshot.guestRoomCalibration.rooms, Has.Length.EqualTo(19));
    Assert.That(snapshot.guests, Has.Length.EqualTo(8));
    Assert.That(snapshot.posePlacement.drawingRoom.assignments, Has.Length.EqualTo(8));
    Assert.That(snapshot.posePlacement.diningRoom.assignments, Has.Length.EqualTo(8));
    Assert.That(snapshot.posePlacement.diningRoom.occlusionBindings, Has.Length.EqualTo(8));
    Assert.That(snapshot.guests.All(guest => !string.IsNullOrWhiteSpace(guest.sittingMapping.replacementClipGuid)), Is.True);
    Assert.That(snapshot.roomPerspectiveProfiles, Has.Length.EqualTo(2));
}
```

- [ ] **Step 2: Run the snapshot test and verify RED**

Run without `-quit`:

```bash
/home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics \
  -projectPath /home/hamzak/Desktop/ChateauChantilly \
  -runTests -testPlatform EditMode \
  -testFilter CharacterScaleOwnershipRegressionTests.LegacySnapshotPreservesCompletePhaseOneMigrationEvidence \
  -testResults /tmp/character-size-phase1-snapshot-red.xml \
  -logFile /tmp/character-size-phase1-snapshot-red.log
```

Expected: one failed test because the snapshot file does not exist. A compilation error is not an acceptable red state.

- [ ] **Step 3: Create the canonical snapshot**

Write JSON using invariant-culture decimal strings and stable array ordering. Use this top-level shape:

```json
{
  "schemaVersion": 1,
  "source": { "gitCommit": "2a92396176c2baa6310e42f9ee906ee846d94e03", "unityVersion": "6000.4.10f1", "files": [] },
  "butler": { "prefabInstanceFileId": "81962841", "sourcePrefab": {}, "scenePointClickFileId": "81962842", "fallback": {}, "calibrationBase": {}, "rootScale": {}, "roomOverrides": [], "stalePropertyModifications": [] },
  "guestRoomCalibration": { "gameObjectFileId": "1844861546", "componentFileId": "1844861547", "transformFileId": "1844861549", "applier": {}, "rooms": [] },
  "guests": [],
  "roomPerspectiveProfiles": [],
  "posePlacement": { "drawingRoom": { "standingCharacterIds": [], "assignments": [] }, "diningRoom": { "assignmentRule": "", "assignments": [], "occlusionBindings": [] } },
  "integrity": { "expectedCounts": {}, "warnings": [] }
}
```

Each serialized datum must preserve `propertyPath`, `rawValue`, and provenance. Curves preserve every key field plus infinity modes and rotation order. Include the four source hashes, 19 Butler records, 19 guest rows, eight participant records, two profiles, eight sitting mappings, both eight-anchor sets, eight occlusion bindings, stale Butler aliases, and captured-base inconsistencies recorded in the approved audit.

- [ ] **Step 4: Add roster animation and no-scale-curve tests**

Add a parameterized roster table for Guest 1-8 controller and sitting-clip paths. For each controller, load the `AnimatorOverrideController`, locate original clip `Player_Croutch`, and assert the expected sitting clip. For every `.anim` under `Assets`, use `AnimationUtility.GetCurveBindings` and assert no binding property begins with `m_LocalScale`.

```csharp
Assert.That(
    AnimationUtility.GetCurveBindings(clip).Any(binding => binding.propertyName.StartsWith("m_LocalScale", StringComparison.Ordinal)),
    Is.False,
    clipPath);
```

- [ ] **Step 5: Write the ownership matrix**

Document every runtime/editor/serialized writer with columns: path/component, actor scope, trigger/order, property written, responsibility, serialized data, active/guarded/dead status, keep/decouple/delete decision, and regression proof. Record the full-suite baseline: 279 total, 226 passed, 53 failed, with XML `/tmp/character-size-phase1-baseline-editmode.xml` and log `/tmp/character-size-phase1-baseline-editmode.log`.

- [ ] **Step 6: Run Task 1 GREEN verification**

Run the full new fixture and `git diff --check`. Expected: snapshot/animation tests pass, exactly eight sitting mappings are protected, and no transform-scale animation curve is found.

- [ ] **Step 7: Commit Task 1**

```bash
git add docs/architecture/character-scale-phase1-ownership-audit.md \
  docs/migrations/character-scale/legacy-character-scale-snapshot.json \
  Assets/Editor/CharacterScaleOwnershipRegressionTests.cs \
  Assets/Editor/CharacterScaleOwnershipRegressionTests.cs.meta \
  docs/superpowers/plans/2026-07-16-character-size-phase1-cleanup.md
git commit -m "test: freeze legacy character scale evidence"
```

### Task 2: Neutralize chapter, panic, facing, layout-capture, and animation-builder scale writes

**Files:**
- Modify: `Assets/Editor/CharacterScaleOwnershipRegressionTests.cs`
- Modify: `Assets/Scripts/CharacterController2D.cs`
- Modify: `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs`
- Modify: `Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestPanicController.cs`
- Modify: `Assets/Editor/PlayModeLayoutCaptureWindow.cs`
- Modify: `Assets/Editor/CharacterAnimationAssetBuilder.cs`
- Modify or delete: `Assets/Editor/Guest2ButlerAnimationAssetBuilder.cs`
- Modify: `Assets/Editor/Chapter2RegressionTests.cs`
- Modify: `Assets/Editor/RoomProjectionRegressionTests.cs`

**Interfaces:**
- Consumes: current `ActorRoomState` room/seated behavior and the roster mapping protected by Task 1.
- Produces: presentation behavior that cannot change roster body-root magnitude; chapter code no longer creates or refreshes guest scale infrastructure.

- [ ] **Step 1: Add failing invariance tests**

Add tests proving:

```csharp
[Test]
public void CharacterControllerFacingFlipsRenderersWithoutChangingRootScale()
{
    GameObject actor = new GameObject("FacingActor");
    GameObject visual = new GameObject("Visual");
    visual.transform.SetParent(actor.transform, false);
    SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
    CharacterController2D controller = actor.AddComponent<CharacterController2D>();
    Vector3 before = new Vector3(1.4f, 2.1f, 1f);
    actor.transform.localScale = before;

    typeof(CharacterController2D).GetMethod("Flip", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);

    Assert.That(actor.transform.localScale, Is.EqualTo(before));
    Assert.That(renderer.flipX, Is.True);
    UnityEngine.Object.DestroyImmediate(actor);
}
```

Also assert that panic begin/stop leaves actor-root `localScale` equal to its original value, seated state does not resize, coat take/return does not resize, and layout capture refuses the Player/Guest root and descendants while still accepting `RoomAnchor` layout objects.

Extend the source ownership test to require Chapter 1 to contain no `EnsureGuestScale*`, `GuestRoomScale*`, `GuestScaleParticipant`, `PreserveGuestAuthoredScale`, `SetPerspectiveScaleEnabled`, or participant room/pose synchronization.

- [ ] **Step 2: Run focused tests and verify RED**

Run `CharacterScaleOwnershipRegressionTests`, the two panic tests in `Chapter2RegressionTests`, and `RoomProjectionRegressionTests.PlayModeLayoutCaptureCanPersistRuntimeAnchorTuning`. Confirm failures point to the existing root-scale/factory behavior.

- [ ] **Step 3: Replace root-scale facing**

Cache child `SpriteRenderer` instances and implement `Flip()` by toggling `flipX` on each renderer. Do not write `transform.localScale`. Preserve the public movement contract and facing state.

- [ ] **Step 4: Remove Chapter 1 scale ownership**

Delete the guest applier/calibration/participant creation and discovery paths, refresh calls, room/pose synchronization, authored-scale capture/restore used as scaling, and perspective-scale toggles. Preserve actor creation, component disabling, room/pose state, placement, animation controllers, sorting, coats, and waypoints.

- [ ] **Step 5: Remove panic root scaling**

Delete root-scale snapshot/restore, original sprite-size capture, `GetSpriteScaleMultiplier`, `ApplySpriteScale`, and any managed-scaler guard whose only purpose was choosing whether panic could scale. Preserve sprite assignment, position routes, bob/jitter, Animator/mover enable state, room, pose, and restoration.

Invert the existing Chapter 2 mid-panic test from `Is.Not.EqualTo(originalLocalScale)` to `Is.EqualTo(originalLocalScale)`.

- [ ] **Step 6: Constrain layout capture and builders**

Reject transforms belonging to a `PointClickPlayerMovement` or an `ActorRoomState` actor in `PlayModeLayoutCaptureWindow.TryCreateCaptureItem`; this covers the Butler and all eight inherited Player-prefab guests without fuzzy names. Retain RoomAnchor and ordinary layout capture. Make the global animation builder preserve the existing `Player_Croutch` override rather than mapping it to idle. Remove automatic Gameplay open/save and component mutation from the Guest 2 builder, or delete it if it is a completed one-off whose generated assets are already protected by Task 1.

- [ ] **Step 7: Run Task 2 GREEN verification and commit**

Run the new fixture, `Chapter2RegressionTests`, and the layout-capture projection test. Compile smoke with Unity `-batchmode -nographics -quit` and no `-runTests`. Then commit:

```bash
git add Assets/Editor/CharacterScaleOwnershipRegressionTests.cs \
  Assets/Scripts/CharacterController2D.cs \
  Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs \
  Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestPanicController.cs \
  Assets/Editor/PlayModeLayoutCaptureWindow.cs \
  Assets/Editor/CharacterAnimationAssetBuilder.cs \
  Assets/Editor/Guest2ButlerAnimationAssetBuilder.cs \
  Assets/Editor/Chapter2RegressionTests.cs Assets/Editor/RoomProjectionRegressionTests.cs
git commit -m "refactor: neutralize secondary character scale writers"
```

### Task 3: Remove stage, projection, and walker character-scale ownership

**Files:**
- Modify: `Assets/Editor/CharacterScaleOwnershipRegressionTests.cs`
- Modify: `Assets/Scripts/Story/ActorRoomState.cs`
- Modify: `Assets/Scripts/Characters/RoomProjectedEntity.cs`
- Modify: `Assets/Scripts/Characters/RoomPersonWalker2D.cs`
- Delete: `Assets/Scripts/Characters/CharacterVisualProfile.cs`
- Delete: `Assets/Scripts/Characters/CharacterVisualProfile.cs.meta`
- Modify: `Assets/Editor/StoryActorRoomStageLockingTests.cs`
- Modify: `Assets/Editor/RoomProjectionRegressionTests.cs`
- Modify: `Assets/Editor/CharacterRegressionTests.cs`
- Modify: `Assets/Editor/NavigationRegressionTests.cs`

**Interfaces:**
- Consumes: room-stage screen/world conversion, projection position, `NPCWaypointMover`, WorldY sorting, and seated occlusion.
- Produces: position-only `ActorRoomState`; prop/presentation-only projection; movement/tint-only walker; no `CharacterVisualProfile` size source.

- [ ] **Step 1: Add failing scale-invariance ownership tests**

Require `ActorRoomState` to preserve root scale across `BindToRoomStagePoint`, room-stage pan/zoom, and `SetCurrentRoom`; require the walker update to leave its graphic scale unchanged; require managed actor placement/projection tests to preserve root/visual scale. Add source guards rejecting `ApplyButlerCharacterScaleNow`, `BuildButlerActorScale`, `ScaleXY`, `scaleWithRoomStageMotion`, `roomVisualScaleOverrides`, and Butler scale sampling in these three runtime files.

- [ ] **Step 2: Verify RED in focused fixtures**

Run `CharacterScaleOwnershipRegressionTests`, `StoryActorRoomStageLockingTests`, relevant `RoomProjectionRegressionTests`, `CharacterRegressionTests.RoomPeopleAreEditableDepthScaledSceneObjects`, and `NavigationRegressionTests.ActorRoomStateBindsWorldActorsToRoomStageLocalPoints`.

- [ ] **Step 3: Make ActorRoomState position-only**

Remove scale flags, captured scales, Butler debug/evaluation APIs, scale application in both room-stage delta and local-binding paths, and guest participant synchronization. Keep actor ID, current room, seated state, visibility, interaction, position binding, visible-foot correction, and room-change subscriptions. Expose a clean read-only room-local foot-point query needed by Phase 2 without calculating size.

- [ ] **Step 4: Restrict RoomProjectedEntity**

Remove per-room visual-scale overrides, character visual profile, Butler/guest compatibility APIs, managed-character normalization, and character scale diagnostics. Preserve serialized enum numeric values and keep proven generic position, tint, sorting, shadow, floor, and occlusion behavior. If `applyScale` remains temporarily for props, explicitly scope and name it as prop projection and prove no roster component uses it.

- [ ] **Step 5: Remove walker scale ownership and dead profile**

Remove walker near/far/depth/Butler scale fields, evaluation, and `RectTransform.localScale` writes. Separate tint from the removed profile-scale flag. Preserve path movement, anchored position, bob/sway, Animator state, raycast state, and presentation-only facing. Delete `CharacterVisualProfile` after confirming its asset/reference count remains zero.

- [ ] **Step 6: Rewrite related tests around position and presentation**

Retain stage-lock, live waypoint, projection position, sorting, tint, visibility, and occlusion assertions. Remove or invert requirements for room zoom, depth, or projection to resize actors. Rename the room-people test to express editable movement/animation rather than depth scaling.

- [ ] **Step 7: Run Task 3 GREEN verification and commit**

Run the four affected fixtures, compile smoke, and static searches. Expected: no direct roster scale writer in these files and no new anchor/position failures relative to baseline.

```bash
git add Assets/Editor/CharacterScaleOwnershipRegressionTests.cs \
  Assets/Scripts/Story/ActorRoomState.cs \
  Assets/Scripts/Characters/RoomProjectedEntity.cs \
  Assets/Scripts/Characters/RoomPersonWalker2D.cs \
  Assets/Scripts/Characters/CharacterVisualProfile.cs \
  Assets/Scripts/Characters/CharacterVisualProfile.cs.meta \
  Assets/Editor/StoryActorRoomStageLockingTests.cs \
  Assets/Editor/RoomProjectionRegressionTests.cs \
  Assets/Editor/CharacterRegressionTests.cs Assets/Editor/NavigationRegressionTests.cs
git commit -m "refactor: make character stage and projection scale neutral"
```

### Task 4A: Neutralize point-and-click character scale writers

**Compile-safe execution amendment (2026-07-16):** Task 4 is split across 4A and Task 5. Task 4A removes every PointClick body-root scale writer, preview/mutation/debug API, profile/fallback evaluator, and authored-scale capture/restore path. It deliberately retains the hidden serialized scale fields/rows as migration evidence plus only the read-only `ButlerCharacterScaleSample`, `TryEvaluateButlerCharacterScale`, `GetButlerScaleOverrideRoomIds`, and `ButlerScaleRevision` bridge required by the still-compiling guest stack. Task 5 performs the Unity serialization migration first, then deletes that guest stack and the remaining PointClick bridge/evidence in the same compile-atomic slice.

The completed legacy Guest Size Master, mutating Guest audit, and old Guest/Butler scale regression suite move forward from Task 5 into Task 4A so they cannot keep calling removed mutation APIs.

**Files:**
- Modify: `Assets/Editor/CharacterScaleOwnershipRegressionTests.cs`
- Modify: `Assets/Scripts/PointClickPlayerMovement.cs`
- Delete: `Assets/Editor/ButlerRoomScaleCalibrationWindow.cs`
- Delete: `Assets/Editor/ButlerRoomScaleCalibrationWindow.cs.meta`
- Delete: `Assets/Editor/PointClickPlayerMovementEditor.cs`
- Delete: `Assets/Editor/PointClickPlayerMovementEditor.cs.meta`
- Delete: `Assets/Editor/GuestRoomScaleMasterWindow.cs`
- Delete: `Assets/Editor/GuestRoomScaleMasterWindow.cs.meta`
- Delete: `Assets/Editor/GuestScaleAudit.cs`
- Delete: `Assets/Editor/GuestScaleAudit.cs.meta`
- Delete: `Assets/Editor/GuestButlerScaleRegressionTests.cs`
- Delete: `Assets/Editor/GuestButlerScaleRegressionTests.cs.meta`
- Modify: `Assets/Editor/CharacterRegressionTests.cs`
- Modify: `Assets/Editor/RoomPerspectiveProfileEditor.cs`
- Modify: `Assets/Editor/RoomProjectionRegressionTests.cs`
- Modify: `Assets/Editor/NavigationRegressionTests.cs`
- Modify: `docs/superpowers/plans/2026-07-16-character-size-phase1-cleanup.md`

**Interfaces:**
- Consumes: the existing logical/world position conversion and current room-stage ratio.
- Produces: `PointClickPlayerMovement` with movement, room/foot-position, animation, and sorting ownership plus the temporary read-only migration bridge described above; it has no character transform writer.

- [ ] **Step 1: Add a failing Butler ownership test**

Read `PointClickPlayerMovement.cs` and require absence of `ApplyPerspectiveScale`, every root-scale assignment, calibration preview/setter/debug APIs, authored scale capture/restore, guest-participant scale guards, and perspective profile/fallback evaluators. Require only the four temporary bridge symbols above to remain publicly visible. At the same time require `currentRoomStageScaleRatio`, `TryGetRoomStageLocalPointForRoom`, logical/world conversion, `ApplyVisualPosition`, and sorting to remain.

- [ ] **Step 2: Verify RED**

Run the focused ownership test. Expected: failure on the active Butler scale fields/APIs.

- [ ] **Step 3: Neutralize Butler scale writers while retaining migration evidence**

Hide and retain the scale-only serialized fields and nested row until Task 5 can migrate their YAML safely. Delete calibration/evaluation/preview/debug mutation methods, scale calculations, all `ApplyPerspectiveScale` calls, and every Butler body `localScale` write. Keep only the temporary read-only bridge. Do not remove `currentRoomStageScaleRatio`; keep it in room-stage position conversion. Keep room-local foot queries clean and read-only for Phase 2.

- [ ] **Step 4: Delete obsolete Butler editors and migrate tests**

Delete the Butler calibration window, PointClick inspector, Guest Size Master, mutating Guest audit, and old Guest/Butler scale regression suite. Remove Butler-scale tests from `RoomProjectionRegressionTests`; rewrite arrival, dialogue, and navigation tests around feet/room/position rather than calibrated size. Preserve movement, room transition, sorting, and anchor assertions.

- [ ] **Step 5: Run Task 4 GREEN verification and commit**

Run `CharacterScaleOwnershipRegressionTests`, `RoomProjectionRegressionTests`, `Chapter1GuestRoomVisibilityRegressionTests`, `DialogueSpeechMovementRegressionTests`, and `NavigationRegressionTests`; compare failures with the saved baseline. Compile smoke and commit.

```bash
git add Assets/Editor/CharacterScaleOwnershipRegressionTests.cs \
  Assets/Scripts/PointClickPlayerMovement.cs \
  Assets/Editor/ButlerRoomScaleCalibrationWindow.cs \
  Assets/Editor/ButlerRoomScaleCalibrationWindow.cs.meta \
  Assets/Editor/PointClickPlayerMovementEditor.cs \
  Assets/Editor/PointClickPlayerMovementEditor.cs.meta \
  Assets/Editor/RoomProjectionRegressionTests.cs \
  Assets/Editor/Chapter1GuestRoomVisibilityRegressionTests.cs \
  Assets/Editor/DialogueSpeechMovementRegressionTests.cs Assets/Editor/NavigationRegressionTests.cs
git commit -m "refactor: neutralize point click character scaling"
```

### Task 5: Migrate serialized data and delete the guest-scale stack and temporary bridge

**Files:**
- Create then delete after execution: `Assets/Editor/CharacterScalePhaseOneSceneMigration.cs` and `.meta`
- Modify: `Assets/Editor/CharacterScaleOwnershipRegressionTests.cs`
- Delete: `Assets/Scripts/Characters/GuestRoomScaleCalibration.cs` and `.meta`
- Delete: `Assets/Scripts/Characters/GuestRoomScaleApplier.cs` and `.meta`
- Delete: `Assets/Scripts/Characters/GuestRoomStageScaleUtility.cs` and `.meta`
- Delete: `Assets/Scripts/Characters/GuestScaleParticipant.cs` and `.meta`
- Modify: `Assets/Scenes/Gameplay.unity`
- Modify: `Assets/Prefabs/Player.prefab`
- Modify: `Assets/Scripts/PointClickPlayerMovement.cs`

**Interfaces:**
- Consumes: the tested snapshot and scale-neutral runtime produced by Tasks 1-4.
- Produces: no guest scale types/components/objects/GUIDs and no Butler scale property modifications, while retaining all nine root scales and all placement/animation/occlusion data.

- [ ] **Step 1: Add failing serialized-ownership assertions**

Require the legacy files not to exist. Search Gameplay, Player, and all character/room prefabs for these GUIDs and require zero hits:

```text
31d79ef7452a4c5288644569bd958a60  GuestRoomScaleCalibration
2d396ad445bc46b9a6acb3ac62291ef0  GuestRoomScaleApplier
b099f2b1c3494d8fa900d71915c16f31  GuestScaleParticipant
c209e3f5ef8c464db5163927439bd6a4  GuestRoomStageScaleUtility
```

Also reject `butlerRoomScaleOverrides`, stale `frontScale`/`backScale`, guest calibration object names, and unknown/missing MonoBehaviour script records. Require eight guest prefab instances, eight sitting mappings, eight Drawing anchors, eight Dining anchors, eight Dining occlusion bindings, and the authored static root scales documented in the snapshot.

- [ ] **Step 2: Verify RED**

Run the serialized ownership tests. Expected: failures on the current components, objects, GUIDs, and property paths.

- [ ] **Step 3: Implement and run the one-time Unity migration**

While legacy guest types still compile, create an Editor execute method that:

1. Opens Gameplay explicitly.
2. Removes exactly eight `GuestScaleParticipant` components with Undo-safe immediate destruction.
3. Destroys only the standalone applier GameObject `86244176` and calibration GameObject `1844861546`.
4. Finds the Player prefab instance, obtains `PrefabUtility.GetPropertyModifications`, filters exact PointClick target fileID `6481024636379014001` property prefixes for removed Butler fields/records/aliases, and writes the remaining modifications back.
5. Saves Gameplay.
6. Loads and resaves `Assets/Prefabs/Player.prefab` through `PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset` so removed fields are not retained as unknown YAML.
7. Validates preserved roots, controllers, anchor counts, and occlusion binding counts before exiting nonzero on mismatch.

Run it with Unity `-batchmode -nographics -quit -executeMethod CharacterScalePhaseOneSceneMigration.Run`. Inspect the diff before deleting any script.

- [ ] **Step 4: Delete legacy sources and old test/tool suite**

Delete the four runtime types, the old 57-test Guest suite, Guest Size Master, and mutating Guest audit. Delete the one-time migration script after successful execution. Do not leave `[Obsolete]` shells.

- [ ] **Step 5: Verify no serialization damage**

Run compile smoke, the ownership fixture, all roster animation tests, Chapter 1 visibility tests, Chapter 2 tests, and scene GUID searches. Open/resave in batchmode once more and prove deleted property paths/GUIDs do not return.

- [ ] **Step 6: Commit Task 5**

```bash
git add -A Assets/Editor Assets/Scripts/Characters Assets/Scenes/Gameplay.unity Assets/Prefabs/Player.prefab
git commit -m "refactor: purge legacy guest scale architecture"
```

### Task 6: Finish profile/projection editor cleanup and documentation

**Files:**
- Modify: `Assets/Scripts/Characters/RoomPerspectiveProfile.cs`
- Modify: `Assets/Editor/RoomPerspectiveProfileEditor.cs`
- Modify: `Assets/Editor/RoomProjectionCalibrationWindow.cs`
- Delete: `Assets/Editor/RoomProjectedEntityEditor.cs` and `.meta`
- Modify: `Assets/Scripts/Characters/README.md`
- Modify: `Assets/Editor/RoomProjectionRegressionTests.cs`
- Modify: `Assets/Editor/ObjectCollisionBoxRegressionTests.cs` only if its explicit no-scale fixture requires API adaptation
- Modify: `Assets/Editor/CharacterScaleOwnershipRegressionTests.cs`
- Modify: `docs/architecture/character-scale-phase1-ownership-audit.md`

**Interfaces:**
- Consumes: scale-neutral runtime and clean serialization.
- Produces: prop/presentation-only room depth/profile tools, accurate documentation, and a final source/serialized sole-writer guard.

- [ ] **Step 1: Add failing final ownership assertions**

Require room profiles and editors to contain no character scale endpoints, `scaleByDepth`, `GetScale`, `SetScaleEndpoints`, multiplier UI, standard-adult profile creation, or character scale refresh. Require projection calibration to retain only proven room identity, floor geometry, tint, sorting, shadow, and prop-position responsibilities.

- [ ] **Step 2: Verify RED**

Run the ownership and projection fixtures. Expected: failures on current profile scale data/editor controls.

- [ ] **Step 3: Remove final profile/editor character-scale paths**

Remove `scaleByDepth`, Near/Far Scale, multiplier APIs, and managed-character refresh from `RoomPerspectiveProfile` and its editor after confirming no retained prop uses `applyScale`. Remove the now-unused `applyScale` field/path from `RoomProjectedEntity` in the same change so no dead profile scale API remains. Preserve near/far foot Y and `GetDepth01` for tint/sort/shadow. Remove standard-adult/character actions from the calibration window. Delete the old room-specific character scale inspector. Update README to state that Phase 1 intentionally has no runtime character-size owner and that Phase 2 will provide it.

- [ ] **Step 4: Complete the read-only audit guard**

Either retain the test fixture as the durable audit or add `CharacterScaleOwnershipAudit` as a read-only menu that performs the same checks. It may log/report only; it may not open/save scenes, add components, mutate assets, or infer identities by name.

- [ ] **Step 5: Run Task 6 GREEN verification and commit**

Run ownership, projection, object-collision, character-animation, and scene validation fixtures; compile smoke and `git diff --check`.

```bash
git add -A Assets/Scripts/Characters Assets/Editor docs/architecture/character-scale-phase1-ownership-audit.md
git commit -m "refactor: finish character scale ownership cleanup"
```

### Task 7: Full verification, baseline comparison, and Phase 1 handoff

**Files:**
- Modify: `docs/architecture/character-scale-phase1-ownership-audit.md`
- Modify: `Assets/Editor/CharacterScaleOwnershipRegressionTests.cs` only for defects discovered through red-green verification

**Interfaces:**
- Consumes: all Phase 1 cleanup commits.
- Produces: evidence-backed completion report and a clean Phase 2 boundary.

- [ ] **Step 1: Run all affected fixtures individually**

Run without `-quit`:

```text
CharacterScaleOwnershipRegressionTests
RoomProjectionRegressionTests
StoryActorRoomStageLockingTests
Chapter1GuestRoomVisibilityRegressionTests
Chapter2RegressionTests
NavigationRegressionTests
DialogueSpeechMovementRegressionTests
CharacterRegressionTests
ObjectCollisionBoxRegressionTests
ButlerSpriteQualityRegressionTests
```

Record total/pass/fail for every filter. Any new failure is investigated systematically; do not mask it by weakening unrelated coverage.

- [ ] **Step 2: Run full EditMode comparison**

```bash
/home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics \
  -projectPath /home/hamzak/Desktop/ChateauChantilly \
  -runTests -testPlatform EditMode \
  -testResults /tmp/character-size-phase1-final-editmode.xml \
  -logFile /tmp/character-size-phase1-final-editmode.log
```

Compare test names/results against `/tmp/character-size-phase1-baseline-editmode.xml`. Expected: focused Phase 1 suites are green and there are no new unrelated failures beyond the baseline's 53.

- [ ] **Step 3: Run static and serialized verification**

Run scoped `rg` searches for deleted type names, GUIDs, Butler fields, root-scale writers, runtime factories, and scale animation curves. Run `git diff --check`, inspect `git status -sb`, and verify no one-time migration source remains.

- [ ] **Step 4: Perform manual Gameplay verification**

In Unity Play Mode verify Butler and Guests 1-8 through walking, room changes, Drawing Room standing/seating, Dining seating, coat transitions, and Chapter 2 panic/restore. Confirm position, facing, animation, sorting, and occlusion still work and root scales do not drift. Do not implement Phase 2 while testing.

- [ ] **Step 5: Complete audit report and final commit**

Update the ownership matrix with deleted files/types, retained responsibilities, migrated coverage, exact commands/results, baseline comparison, snapshot location, and remaining risks.

```bash
git add docs/architecture/character-scale-phase1-ownership-audit.md Assets/Editor/CharacterScaleOwnershipRegressionTests.cs
git commit -m "test: verify character scale phase one boundary"
```

- [ ] **Step 6: Request whole-branch review**

Generate a review package from base `2a92396176c2` to HEAD and request final spec-compliance and code-quality review. Resolve every Critical/Important finding with focused tests and re-review before claiming completion.
