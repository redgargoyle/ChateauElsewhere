# Guest Exit Movement Design

## Goal

After a Chapter 2 guest finishes giving their order, keep the guest visible while they walk to the authored door on the route toward the Dining Room, then hide and stage them for the Dining Room reveal.

## Existing Flow

`Chapter2GuestSearchController` already owns the complete departure sequence:

1. `MarkGuestFound` starts `RunGuestExitToDiningRoomRoutine`.
2. The routine resolves an authored `DoorTriggerNavigation` toward the Dining Room.
3. It reuses `NPCWaypointMover.MoveTo` and waits while the mover is active.
4. It calls `StageGuestForDiningRoomReveal` only after arrival or timeout.

This flow must remain the only Chapter 2 guest-order departure pathway.

## Root Cause

Chapter 2 moves non-UI guest roots out of their `RoomContentGroup` and under `ChapterActors_Runtime` so room activation does not destroy or hide persistent actor state. Their `RoomProjectedEntity` remains active and continues to own the visible position through `roomLocalFootPoint`; `ActorRoomState.PlaceAt` already relies on this behavior for detached guests.

`NPCWaypointMover.CanUseProjectionAsMotionOwner` instead requires the projection to remain parented under a `RoomContentGroup`. For a detached but active projection, the mover therefore advances the actor root transform while `RoomProjectedEntity.LateUpdate` reapplies the unchanged projected foot point to the visible guest. The animator walks, the visible guest stays in place, and the exit timeout eventually stages the guest out of view.

## Approaches Considered

1. **Use any active projection as the shared mover's motion owner.** This matches `ActorRoomState.PlaceAt`, preserves projected scale/sorting, and fixes all callers of `NPCWaypointMover` without adding a departure implementation. This is the selected approach.
2. **Reparent the guest into the source room for departure.** This adds Chapter 2-specific hierarchy changes and risks room-visibility and persistent-actor regressions.
3. **Disable projection during departure and move the actor transform.** This requires restoring projection state and can disrupt projected position, scale, tint, and sorting.

## Design

Change `NPCWaypointMover.CanUseProjectionAsMotionOwner` to return true when the projection exists and `IsProjectionActive` is true. Keep the existing `TryGetProjectedTarget`, `MoveProjectedToRoutine`, controller routing, timeout, pending-exit gate, and Dining Room staging behavior unchanged.

The visible movement remains:

`order complete -> authored route door -> NPCWaypointMover -> RoomProjectedEntity.roomLocalFootPoint -> arrival -> Dining Room staging`

If no valid door exists or movement exceeds the existing timeout, the current warning and fallback staging behavior remains intact.

## Testing

Add an EditMode behavior regression proving that a detached `RoomProjectedEntity` with an active room profile is accepted as the `NPCWaypointMover` motion owner. Verify the test fails before the production change and passes after it. Then run the complete `RoomProjectionRegressionTests`, `Chapter2RegressionTests`, and full EditMode suite.
