# Guest Exit Movement Implementation Plan — Archived

> [!IMPORTANT]
> **Superseded historical plan. Do not execute it.** It targeted the projection/scale architecture that Phase 1 later removed. Do not recreate the deleted components, APIs, editor tests, or compatibility paths. See [Character Presentation Legacy Removal Audit](../../../Docs/CharacterPresentationLegacyRemovalAudit.md) for the current static-scale ownership contract.

## Historical goal

Keep a detached Chapter 2 guest visible while they walk to an authored exit, then hide and stage them for the Dining Room reveal.

## What this plan originally attempted

The pre-cleanup implementation routed visible movement through a child presentation component while leaving the actor root detached from `RoomContentGroup`. The proposed fix added an explicit ownership predicate so `NPCWaypointMover` could decide whether that alternate presentation path owned the visible position.

The plan called for:

- a regression proving that a non-position-owning presentation component could not own waypoint movement;
- a coroutine regression for a detached actor whose child presentation component moved the visible foot point;
- a shared ownership predicate consumed by `NPCWaypointMover`;
- focused Chapter 2 and presentation regression runs.

That approach was appropriate evidence for the bug at the time, but it preserved overlapping presentation ownership and was removed during the Phase 1 cleanup.

## Current disposition

The gameplay requirements remain valid:

- reuse `NPCWaypointMover` rather than introducing another Chapter 2 movement coroutine;
- select authored `DoorTriggerNavigation` exits;
- keep guests visible until arrival or timeout;
- stage guests for the Dining Room only after that handoff.

The architecture is now simpler. `NPCWaypointMover` releases `ActorRoomState`'s passive room-stage point binding before moving the actor transform directly. Story code owns the route and authored placement; camera code owns room-stage pan/zoom; sorting code owns sorting only; character body scale stays at its authored static value.

Current tests should cover direct transform arrival, binding release, timeout fallback, visibility handoff, and unchanged character scale. The deleted projection-specific fixture and APIs must not be restored.
