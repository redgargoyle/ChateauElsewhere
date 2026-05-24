# Dreadforge Development Tracker

This file tracks the current practical pass so we do not lose the thread between Unity sessions.

## Done In This Pass

- Fixed the butler/player render drift caused by room edge panning. The player now keeps a logical walk position inside the polygon boundary and applies the active room-stage visual offset only when rendering.
- Kept the player on the `People` sorting layer and depth-sorted from logical floor position, not the panned visual position.
- Slowed vertical player movement with `verticalMovementSpeedMultiplier` so up/down travel reads less fast than left/right travel.
- Updated NPC walkers to use smoother subpixel UI motion instead of whole-pixel snapping.
- Added lightweight stride bob, sway, endpoint pause, and idle breathing to `RoomPersonWalker2D`.
- Tuned the Grand Entrance Hall gentleman and lady depth scaling so they stay a bit larger at the back and become more present near the front.
- Added player-distance gating to door and stair triggers. A click only transitions rooms when the butler is close enough on screen.
- Improved far door/stair clicks: when clicked from too far away, the butler now walks toward the closest reachable point near the trigger, then auto-uses it only if he ends close enough.
- Added navigation regression coverage for proximity-gated door/stair use and player approach movement.
- Fixed the butler walk-away animation by expanding `Player_Walk_Up.anim` from a two-frame slide into a full back-facing stride cycle.
- Verified `Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj` both build with 0 warnings and 0 errors.

## Immediate Visual Checks In Unity

- Enter Play mode in `Assets/Scenes/Gameplay.unity`.
- Pan the room left/right with the screen edges and confirm the butler stays locked to the same floor point relative to the painting.
- Click around the Grand Entrance Hall and confirm the butler remains inside `PlayerBoundary_Entrance`.
- Watch the gentleman and lady for at least one full path direction change. They should move more smoothly, pause briefly at endpoints, and show subtle idle motion.
- Try clicking a door or stairway while far away. The butler should walk toward it first, then navigate only if he is close enough. Close clicks should navigate immediately.
- Click above the butler and watch him walk away from the camera. The back-facing walk should cycle through a full stride instead of holding one foot-forward pose.

## Next Focus

- Tune the butler animation controller by watching actual Play mode footage: idle should breathe, walking should use up/down/left/right states, and diagonals should pick the dominant averaged direction.
- Replace the current automatic nearest-point door approach with authored approach points for important doors and stairways where the closest collider point is not the best-looking stopping spot.
- Create a small game-state service for current chapter/beat, current room, enabled triggers, and scripted scene flags.
- Add a shared in-game clock service that NPC schedules and visible room clocks can read.
- Replace the sample NPC wandering paths with named schedule actions: idle at fireplace, cross hall, inspect table, leave room, enter room.
- Add room-local foreground occluders and scale curves per room where characters should pass behind furniture or change apparent depth.
