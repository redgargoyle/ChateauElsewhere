# ChataeuChatilly Development Tracker

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
- Hardened door approach after multi-room testing: UI door clicks no longer get overwritten by floor clicks, clamped approach points inset inside the polygon, and trigger static state resets between Play Mode sessions.
- Added a door trigger RectTransform pointer fallback so door hover/click still works when Unity UI raycasts miss a visible trigger.
- Fixed the butler walk-away animation by expanding `Player_Walk_Up.anim` from a two-frame slide into a full back-facing stride cycle.
- Added a shared movement reachability query so player clicks, door auto-approach, and cursor feedback all agree on whether a point is usable.
- Changed out-of-bounds floor clicks to walk to the nearest reachable point when that would actually move the butler.
- Added generated walk and blocked-walk cursors. The blocked cursor appears when a floor click would do nothing.
- Improved far door/stair clicks by sampling the trigger rect and choosing the closest reachable floor destination instead of using a single guessed point.
- Reworked room people so `RoomPersonWalker2D` no longer animates atlas UV frames. It now only handles room-local movement, depth scale/tint, motion offsets, and shared Animator parameters.
- Generated character Animation folders under `Assets/Animation/<CharacterName>` with editable idle/walk clips and Animator override controllers for the new character sprite sheets.
- Converted the two Grand Entrance Hall example walkers from `RawImage` atlas playback to UI `Image` plus `Animator` override controllers.
- Added a Gameplay IMGUI animation test selector for `ButlerClassic`, `ButlerYoung`, and `GentlemanBlack`; it swaps the player Animator override controller and initial sprite without adding another Canvas.
- Fixed the `GentlemanBlack` test character so it no longer maps every movement state to the same side-only walk cycle. It now has an explicit directional source folder, mirrored left-facing frames, right-facing 8-frame cycle, and front/back fallback rows matching the butler controller layout.
- Added a dedicated `ButlerClassic` Animator controller with four directional idle states. The shared driver now writes persistent facing booleans so ButlerClassic idles up/down/left/right with a subtle breathing cycle after movement stops.
- Verified `Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj` both build with 0 warnings and 0 errors; `dotnet test Assembly-CSharp-Editor.csproj --no-build` exits successfully.

## Immediate Visual Checks In Unity

- Enter Play mode in `Assets/Scenes/Gameplay.unity`.
- Pan the room left/right with the screen edges and confirm the butler stays locked to the same floor point relative to the painting.
- Click around the Grand Entrance Hall and confirm the butler remains inside `PlayerBoundary_Entrance`.
- Watch the gentleman and lady for at least one full path direction change. They should move more smoothly, pause briefly at endpoints, and show subtle idle motion.
- Inspect `Assets/Animation/GentlemanGreen` and `Assets/Animation/LadyGreen` in Unity. The room walkers should now be driven by editable clips through Animator override controllers.
- Start Gameplay and use the opening character selector to test `ButlerClassic`, `ButlerYoung`, and `GentlemanBlack` against the same player movement/animation parameters.
- With `GentlemanBlack` selected, walk left and right to confirm the character faces the travel direction, then walk toward/away from camera to confirm those states no longer show a sideways walk cycle.
- With `ButlerClassic` selected, stop after walking in all four directions and confirm he holds the last facing direction while playing the small idle motion.
- Try clicking a door or stairway while far away. The butler should walk toward it first, then navigate only if he is close enough. Close clicks should navigate immediately.
- Hover walkable floor, unreachable floor, and already-current floor. The cursor should show feet for movement and feet with an X when clicking would not move.
- Click above the butler and watch him walk away from the camera. The back-facing walk should cycle through a full stride instead of holding one foot-forward pose.

## Next Focus

- Tune character clips directly in `Assets/Animation/<CharacterName>` by watching actual Play mode footage: idle should breathe, walking should use up/down/left/right states, and diagonals should pick the dominant averaged direction.
- Add authored approach points for important doors and stairways only where the automatic sampled approach still stops in an awkward-looking place.
- Create a small game-state service for current chapter/beat, current room, enabled triggers, and scripted scene flags.
- Add a shared in-game clock service that NPC schedules and visible room clocks can read.
- Replace the sample NPC wandering paths with named schedule actions: idle at fireplace, cross hall, inspect table, leave room, enter room.
- Add room-local foreground occluders and scale curves per room where characters should pass behind furniture or change apparent depth.
