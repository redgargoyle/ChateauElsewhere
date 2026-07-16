# Guest Exit Movement Design

> [!IMPORTANT]
> **Superseded historical design.** Phase 1 removed the projection/scale architecture described below. Do not recreate its components, APIs, or tests. The current static-scale ownership contract and migration outcome are recorded in [Character Presentation Legacy Removal Audit](../../../Docs/CharacterPresentationLegacyRemovalAudit.md).

## Goal

After a Chapter 2 guest finishes giving their order, keep the guest visible while they walk to the authored door on the route toward the Dining Room, then hide and stage them for the Dining Room reveal.

## Existing Flow

`Chapter2GuestSearchController` already owns the complete departure sequence:

1. `MarkGuestFound` starts `RunGuestExitToDiningRoomRoutine`.
2. The routine resolves an authored `DoorTriggerNavigation` toward the Dining Room.
3. It reuses `NPCWaypointMover.MoveTo` and waits while the mover is active.
4. It calls `StageGuestForDiningRoomReveal` only after arrival or timeout.

This flow must remain the only Chapter 2 guest-order departure pathway.

## Historical Root Cause

Chapter 2 moved non-UI guest roots out of their `RoomContentGroup` and under `ChapterActors_Runtime` so room activation would not destroy or hide persistent actor state. At the time, a now-deleted projection component could remain active on those detached actors and own their visible position through a separate room-local foot point.

The historical bug came from `NPCWaypointMover` selecting a presentation path that accepted logical point updates without applying them to the visible transform. This explained why the route completed logically while the detached actor appeared stationary.

## Approaches Considered

1. **Use an active position-owning projection as the shared mover's motion owner.** This was selected for the pre-cleanup architecture but was later superseded and removed.
2. **Reparent the guest into the source room for departure.** This adds Chapter 2-specific hierarchy changes and risks room-visibility and persistent-actor regressions.
3. **Move the actor transform directly.** This became the current solution after passive room-anchor binding was made explicit and the competing presentation stack was removed.

## Historical Design and Current Disposition

The superseded implementation introduced a projection ownership predicate and a projected movement routine. Those APIs and their regression fixture were deleted during the Phase 1 cleanup.

The useful gameplay flow remains, but movement now has one direct transform owner:

`order complete -> authored route door -> NPCWaypointMover -> actor transform -> arrival -> Dining Room staging`

If no valid door exists or movement exceeds the existing timeout, the current warning and fallback staging behavior remains intact.

## Testing

Current regressions should verify that the shared mover releases passive room-stage binding before direct transform movement, reaches the authored exit, and leaves character scale untouched. Do not restore the deleted projection-specific tests.
