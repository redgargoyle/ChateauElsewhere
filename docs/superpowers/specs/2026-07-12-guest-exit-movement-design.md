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

`NPCWaypointMover` must only select a projection that owns visible position. An active projection with `applyPosition` disabled still accepts room-local foot-point updates, but never applies them to its visible transform; selecting it therefore leaves the visible actor behind. Detached projections with active positional ownership, by contrast, must remain valid motion owners even without a `RoomContentGroup` parent.

## Approaches Considered

1. **Use any active position-owning projection as the shared mover's motion owner.** This matches `ActorRoomState.PlaceAt`, preserves projected scale/sorting, and fixes all callers of `NPCWaypointMover` without adding a departure implementation. This is the selected approach.
2. **Reparent the guest into the source room for departure.** This adds Chapter 2-specific hierarchy changes and risks room-visibility and persistent-actor regressions.
3. **Disable projection during departure and move the actor transform.** This requires restoring projection state and can disrupt projected position, scale, tint, and sorting.

## Design

Expose `RoomProjectedEntity.OwnsProjectedPosition`, defined as `applyPosition && IsProjectionActive`. `NPCWaypointMover.CanUseProjectionAsMotionOwner` consumes that property, making the helper the sole owner of the projection-active positional criterion. `TryGetProjectedTarget` and `TryPlaceProjectedAtTarget` retain their `CanProjectTarget` checks but do not repeat active-projection checks.

Keep `MoveProjectedToRoutine`, controller routing, timeout, pending-exit gate, and Dining Room staging behavior unchanged.

The visible movement remains:

`order complete -> authored route door -> NPCWaypointMover -> RoomProjectedEntity.roomLocalFootPoint -> arrival -> Dining Room staging`

If no valid door exists or movement exceeds the existing timeout, the current warning and fallback staging behavior remains intact.

## Testing

Add EditMode regressions for:

1. An active projection with `applyPosition=false` cannot own waypoint motion.
2. A detached actor root with a child position-owning projection moves its projected foot point and visible projected transform to a compatible same-room target, does not move the actor root as fallback, and finishes movement.
3. Matching-room targets remain projectable and wrong-room targets remain rejected by `CanProjectTarget`.

Verify both ownership and coroutine behavior regressions fail before the production correction and pass afterward. Then run the focused Chapter 2 exit regression plus the complete `RoomProjectionRegressionTests` and `Chapter2RegressionTests` suites, recording unrelated existing failures separately.
