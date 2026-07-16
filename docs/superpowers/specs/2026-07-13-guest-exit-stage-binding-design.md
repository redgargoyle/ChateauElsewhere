# Guest Exit Stage-Binding Design

> [!IMPORTANT]
> **Superseded implementation note retained for history.** Its direct-movement and binding-release findings informed the current code, but Phase 1 removed the parallel projection/scale architecture. Follow the current contract in [Character Presentation Legacy Removal Audit](../../../Docs/CharacterPresentationLegacyRemovalAudit.md), not old projection guidance.

## Goal

After a Chapter 2 guest finishes giving their order, keep the guest visible while they physically walk to the authored door or stairway on the route toward the Dining Room. Hide and stage the guest for Dining only after the walk finishes or the existing safety timeout expires.

## Runtime Evidence

The July 13 Unity Editor log records the real failure for two guests:

- `guest_7`: Upper Gallery to `StairwayTrigger_UpperGallery_GEH`, `owner=transform`, 4.48 world units, then timeout.
- `guest_2`: Music Room to `DoorTrigger_MusicRoom_DrawingRoom`, `owner=transform`, 11.26 world units, then timeout.

Both distances are reachable within the eight-second timeout at the existing 2.2 world-unit movement speed. The mover remains active for the full timeout, proving that its position is being overwritten rather than that the route or animation failed to start.

## Root Cause

The authored Chapter guests are world-space instances of `Player.prefab`. Chapter 1 adds `ActorRoomState` and `NPCWaypointMover` at runtime. Chapter 2 places each guest at a hide anchor through `ActorRoomState.PlaceAt`, which creates a room-stage point binding so detached world actors remain aligned with a panning or zooming room.

When the order conversation ends, `Chapter2GuestSearchController` starts the existing `NPCWaypointMover` without releasing that binding. The mover advances the actor transform during `Update`, then `ActorRoomState` restores the bound hide-anchor position during `LateUpdate`. The animation therefore walks in place until the controller timeout stages and hides the guest.

## Approaches Considered

1. **Release passive stage binding in `NPCWaypointMover` when transform movement begins.** This is selected. The shared mover becomes the sole position owner for the duration of scripted movement. It matches Chapter 1's existing lifecycle, which binds a guest to the room stage after a scripted walk completes.
2. **Clear the binding in `Chapter2GuestSearchController`.** This would fix only this caller and duplicate a prerequisite that belongs to transform movement ownership.
3. **Teach `ActorRoomState` to suspend itself while an `NPCWaypointMover` is active.** This couples passive room-stage alignment to one movement component and risks snapping the actor back to a stale anchor after movement.

## Design

Keep the existing departure data flow unchanged:

`order complete -> route door/stair -> NPCWaypointMover -> arrival -> Dining Room staging`

Before `NPCWaypointMover` writes its own transform, it resolves a colocated `ActorRoomState` and calls the existing `ClearRoomStagePointBinding()` API. Direct transform movement is the sole scripted movement path; there is no parallel presentation/projection owner. The disabled-mover instant-placement fallback also releases the binding before assigning the transform, preventing a later stage update from restoring a stale point.

No Chapter 2-specific movement coroutine, route planner, fake offset, or scene edit is added. `FindExitDoorTowardDiningRoom`, authored `DoorTriggerNavigation` targets, animation handling, pending-exit gating, timeout behavior, and `StageGuestForDiningRoomReveal` remain intact.

## Testing

Add an EditMode regression using the existing room-stage locking rig:

1. Create a detached world-space actor with `ActorRoomState`, matching the real guest hierarchy.
2. Place it at a room-stage anchor and prove the binding is active.
3. Start `NPCWaypointMover.MoveToRoutine` toward another point in the room.
4. Assert the binding can no longer reapply after the mover's first step.

Also strengthen the Chapter 2 source-contract regression to require the shared mover to perform the release and to reject a duplicate binding clear in the Chapter 2 exit-preparation method. Run the focused tests first, then the current stage-locking, Chapter 2, and navigation regression fixtures.
