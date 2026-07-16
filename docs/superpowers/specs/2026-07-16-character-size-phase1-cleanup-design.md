# Character Size Phase 1 Cleanup Design

**Status:** Approved by the user on 2026-07-16 through the Phase 1 execution prompt.

## Objective

Prepare ChateauChantilly for a later universal Butler-and-guest sizing tool by removing every legacy runtime, editor, and serialized path that can determine or overwrite the body-size magnitude of the Butler or Guests 1-8. Phase 1 deliberately stops before creating the new catalog, participant, controller, curve evaluator, or editor window.

The temporary end state is intentionally simple: each character retains a stable authored scale, and no movement, room, pose, panic, animation, projection, or calibration system recalculates that scale.

## Ownership Boundary

Phase 1 separates five responsibilities:

1. **Position and room state** remain owned by point-and-click navigation, `ActorRoomState`, room-stage conversion, authored anchors, and `NPCWaypointMover`.
2. **Animation and appearance** remain owned by Animator controllers, story presentation, sprite replacement, coats, and held-item logic.
3. **Orientation** remains supported, but may not be expressed by changing body-size magnitude.
4. **Sorting and occlusion** remain owned by the Y-sort and room-specific occlusion systems.
5. **Body size** has no runtime evaluator after Phase 1. The future Phase 2 controller will become its sole owner.

Code may continue to scale unrelated props, shadows, UI, click targets, speech bubbles, and effects. A retained generic projection path must be proven not to target the nine managed characters.

## Migration Evidence

Before deleting serialized owners, Phase 1 produces a machine-readable snapshot containing:

- the 19 Butler room endpoint records and legacy global endpoints;
- the 19 guest room multiplier/reference-stage records;
- all eight guest participant identities, roots, authored and captured scales, room state, and pose state;
- room-stage reference values;
- Drawing and Dining perspective curves;
- Drawing Room standing/seated assignments;
- Dining seat assignments;
- all eight sitting animation mappings;
- stale property paths and inconsistent captured-scale observations.

This snapshot is documentation and Phase 2 input only. No runtime code may read it.

## Cleanup Slices

### 1. Evidence and regression boundary

Create the ownership matrix and snapshot first. Add a focused Phase 1 regression fixture that can prove snapshot completeness, absence of deleted owners, preservation of scene/animation assignments, and the final scale-writer allowlist.

### 2. Guest-derived scale chain

Remove `GuestRoomScaleCalibration`, `GuestRoomScaleApplier`, `GuestScaleParticipant`, `GuestRoomStageScaleUtility`, their editor tools, their scene components, and all runtime creation/discovery/synchronization paths. Keep guest ID, room, seated state, visibility, placement, and animation in their non-scaling owners.

### 3. Butler scale chain

Remove Butler endpoints, global perspective-scale fields, evaluators, previews, debug state, and transform writes from `PointClickPlayerMovement`. Preserve room-local coordinate conversion, foot-position queries, movement, warping, animation, and sorting.

### 4. Competing and latent writers

Remove managed-character scale behavior from `ActorRoomState`, `RoomProjectedEntity`, `RoomPersonWalker2D`, `CharacterController2D`, and the Chapter 2 panic presentation. Retain only proven prop projection, position tracking, animation, tint, sorting, shadow, and presentation-child behavior.

Delete dead `CharacterVisualProfile` size data if live-use and serialization checks remain empty. Room perspective profiles may retain prop projection and non-size presentation data, but no Butler/guest consumer.

### 5. Editor, test, and serialized cleanup

Remove obsolete calibration windows, stale inspector controls, misleading documentation, false-positive source-string tests, old components, stale prefab overrides, and orphaned GUID references. Preserve all anchors, seats, sitting mappings, sorting, and occlusion wiring.

## Testing Strategy

Every cleanup slice follows red-green-refactor:

1. Add a focused behavioral or ownership regression that fails for the active legacy path.
2. Run it and record the expected failure.
3. Remove the smallest complete ownership path.
4. Run the focused fixture and directly related existing fixtures.
5. Compile before moving to the next dependency slice.

The full EditMode baseline on `main` at `2a92396176c2` was 279 total, 226 passed, and 53 failed. Phase 1 therefore uses both focused green suites and a final full-suite comparison; it must not add failures beyond the recorded baseline.

## Completion Conditions

Phase 1 is complete only when:

- the audit covers every discovered managed-character scale writer/store;
- the migration snapshot is complete and not read at runtime;
- legacy scale classes, editors, factories, fields, scene components, and compatibility shells are absent;
- no managed-character magnitude write remains in movement, room state, projection, walking, panic, facing, or animation;
- room placement, animation, visibility, sorting, and seated occlusion remain intact;
- Gameplay and Player contain no missing scripts, stale scale overrides, or orphan GUIDs;
- focused regressions pass, the full-suite result introduces no new unrelated failures, and `git diff --check` passes;
- no Phase 2 catalog, controller, scale curve, participant, or authoring window has been implemented.
