# Room People

People are authored as real scene objects under each room's `People` child. They move with the room stage, so panning and zooming keep them locked to the painted background.

`RoomPersonWalker2D` now only owns room-local movement, depth scale/tint, tiny step/idle offsets, and Animator parameter updates. It does not choose animation frames. Character frames live in regular Unity Animator clips under `Assets/Animation/<CharacterName>`, using the same `Speed`, `IsWalkingUp`, `IsWalkingDown`, `IsWalkingLeft`, and `IsWalkingRight` parameter protocol as the controllable player.

Room people use a UI `Image` plus an `Animator` override controller so they still live inside the painted room stage. The generated clips animate both the Image `m_Sprite` field and SpriteRenderer `m_Sprite` field, which lets the same character Animation folders drive NPCs and the controllable player test selector. This keeps the animation editable in Unity's Animation window while preserving the room-stage panning/zooming workflow.

The source character sheets in `Assets/Characters/<CharacterName>` are normalized to a shared foot baseline per frame. Use the aligned frame folders when rebuilding clips so animation frames do not make the character bounce around.

If a character's original sheet is side-only, add an explicit `directional/aligned` folder before rebuilding. Rows are interpreted like the butler controller: row 1 walks down/toward camera, row 2 walks left, row 3 walks right, and row 4 walks up/away. `GentlemanBlack` uses this folder because its original sheets did not contain authored front/back rows.

`ButlerClassic` currently has the most complete player-facing setup: `Assets/Animation/ButlerClassic/ButlerClassic.controller` uses persistent `IsFacingUp`, `IsFacingDown`, `IsFacingLeft`, and `IsFacingRight` Animator parameters to pick one of four looping directional idle clips. The source idle frames live in `Assets/Characters/ButlerClassic/idle/aligned`.

Foreground occlusion uses a simple 90s prerendered trick: `RoomForegroundOccluder` objects are editable `RawImage` crops of the room painting placed above the `People` layer. Put them over railings, table edges, and other foreground furniture so walkers can pass behind those objects without a 3D setup.

World-space SpriteRenderer props use `WorldYSortSpriteRenderer` when they need to depth-sort against the controllable butler. It mirrors `PointClickPlayerMovement`'s player sorting rule: Sorting Layer `People`, order `1000 - y * 100`, and Sprite Sort Point `Pivot`. The player sorts from the bottom of the visible SpriteRenderer bounds, and props can sort from the bottom of an editable physical footprint. A copied tutorial prop will not y-sort just because its pivot is bottom-center; it also needs this dynamic sorting order.

Props that need a custom base footprint can also use `YSortSolidObstacle2D` plus a 2D trigger collider around the base area. That footprint drives both sorting and simple point-click avoidance: when `Block Player Movement` is on, `PointClickPlayerMovement` routes around the footprint corners so the player can snake through gaps without walking through the prop. Keep the collider tight around the physical base, not the full painted sprite.

Current examples:

- `Gameplay.unity > Canvas_Background > Rooms > Room_Grand_Entrance_Hall > People > Walker_GEH_GreenGentleman`
- `Gameplay.unity > Canvas_Background > Rooms > Room_Grand_Entrance_Hall > People > Walker_GEH_GreenLady`
- `Gameplay.unity > Canvas_Background > Rooms > Room_Grand_Entrance_Hall > ForegroundOccluders`
- `Gameplay.unity > UI_CharacterSelectionMenu` for swapping the controllable player between test character override controllers in Play mode.

Useful tweaks:

- Move path points on `RoomPersonWalker2D` to change where a person walks.
- Resize the `Image` RectTransform to change character height.
- Fix frame timing, bad frames, or direction bugs in `Assets/Animation/<CharacterName>/*.anim`, not in `RoomPersonWalker2D`.
- Use `Dreadforge > Characters > Rebuild Character Animation Assets` after changing source frame folders.
- Adjust `Near Y`, `Far Y`, `Near Scale`, and `Far Scale` for perspective.
- Keep `Preview Path In Edit Mode` off while placing people. The animation frames still preview, but the scene object will not quietly walk away while you edit.
- Keep `Snap To Whole Pixels` off for scaled room walkers unless a specific character needs crunchy pixel locking. Subpixel motion reads smoother while the room stage pans and zooms.
- Use the motion polish fields for tiny stride bob, sway, endpoint pauses, and idle breathing. These are only offsets on the card; the path points remain the stable foot positions.
- Add foreground occluder cards whenever a walker should disappear behind painted furniture.
- Add `WorldYSortSpriteRenderer` to any world SpriteRenderer prop that should sort in front of or behind the butler by base/pivot Y.
- Add `YSortSolidObstacle2D` to props that need an editable base footprint, then resize the trigger collider to the base area used for sorting and path avoidance.
