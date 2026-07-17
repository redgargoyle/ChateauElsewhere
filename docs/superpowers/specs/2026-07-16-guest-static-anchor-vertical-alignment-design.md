# Guest Static Anchor Vertical Alignment Design

## Goal

Eliminate the small upward visual offset affecting Guests at Drawing Room seating/standing points, Chapter 2 hiding spots, and Dining Room seats while preserving all authored anchor coordinates, character scale behavior, animation behavior, X positions, sorting, visibility, interaction, and story flow.

## Root Cause

All three affected systems eventually bind a detached world-space Guest to a `RoomAnchor` through `ActorRoomState`. The binding stores the correct room-local anchor point, but every refresh then samples the current animation's live renderer bounds and moves the actor root so `bounds.min.y` reaches the anchor.

That renderer-derived correction is not a stable position source. Sprite bounds can differ between idle, sitting, and other animation frames because of frame dimensions, transparent margins, and tight sprite geometry. The result is a pose-dependent vertical offset even though the authored anchor and X coordinate are correct. Ordinary walking follows a separate logical movement path, which explains why the issue is limited to static staging.

## Considered Approaches

### 1. Use the authored anchor as the exact actor root/foot point (selected)

For a world-space Guest bound to a room-stage anchor, map the stored room-local point to world space and assign that result directly to the persistent actor root. Do not read renderer bounds when maintaining a static binding. Guest sprites are authored with bottom/foot pivots, so the actor root is the stable animation-independent foot reference.

Benefits:

- Fixes Drawing Room points, hiding spots, and Dining Room seats through their one shared path.
- Keeps every manually authored anchor X/Y value intact.
- Cannot drift when an Animator changes frame or pose.
- Does not add per-room, per-seat, or per-character correction data.

### 2. Lower all affected scene anchors

This could visually compensate for the current frames, but would require editing 24 anchors across several rooms. It would preserve the frame-dependent correction and could regress as soon as a different animation or scale is displayed.

### 3. Add pose-specific vertical offsets

Offsets could be stored for idle and sitting poses, but this would duplicate information already represented by sprite pivots, require ongoing calibration, and remain vulnerable to individual frame differences.

## Runtime Design

`ActorRoomState` remains the sole owner of persistent room-stage position binding. Its static binding refresh will:

1. Resolve the authored room-local anchor through `CameraManager` exactly as it does now.
2. Preserve the actor's existing world Z.
3. Write the mapped point directly to the actor root X/Y.
4. Apply the room/Y character display scale through `CharacterAnimationDisplay` without allowing scale or renderer bounds to modify actor position.

The Chapter 1 placement helper will follow the same invariant: bind the Guest to the supplied room-stage anchor and place its actor root at the resolved anchor point without subtracting a live renderer-bounds offset.

No changes will be made to:

- Scene anchor positions or `Gameplay.unity` staging data.
- Character room-scale catalog values.
- `CharacterAnimationDisplay` scale ownership.
- Animator controllers, clips, sprite imports, sorting, occlusion, visibility, collisions, dialogue, or chapter state.
- Moving waypoint behavior, including its existing optional visible-feet endpoint alignment.

## Test Design

Regression tests will prove the invariant with a real actor root and a visual child whose sprite bounds deliberately change:

- Static binding places the actor root exactly on the authored anchor.
- Swapping to a sprite/frame with different bounds cannot move the actor root on a later binding refresh.
- Changing between standing and seated Animator state cannot introduce a Y offset.
- Pan, zoom, and viewport refreshes keep the root mapped to the same room-local anchor.
- Root X remains exactly aligned as well as Y.
- Character display scale still evaluates from the bound room-local Y and does not alter root position.

Existing Chapter 1 structural regression tests will be updated to require anchor-root placement and reject renderer-bounds correction in static placement. Focused Unity EditMode tests will run first, followed by the complete EditMode suite and a PlayMode smoke test covering Drawing Room staging, hiding placement, and Dining Room seating.

## Success Criteria

- Guests no longer float above Drawing Room seats/standing points, hiding spots, or Dining Room seats.
- All affected Guest roots remain exactly at their authored room anchors through pose and frame changes.
- Existing X coordinates and all scene anchors remain untouched.
- Butler and Guest room scaling remains unchanged.
- Walking and story behavior remain unchanged.
- Focused regression tests pass and the change introduces no new failures in the full suite.
