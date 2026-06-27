# Room People

People are authored as real scene objects under each room's `People` child. They move with the room stage, so panning and zooming keep them locked to the painted background.

`RoomPersonWalker2D` now only owns legacy room-local path movement, per-object depth scale/tint when no projection is present, tiny step/idle offsets, and Animator parameter updates. If the same object has an active `RoomProjectedEntity`, the walker feeds the projected foot point and lets projection own position, scale, and tint. It does not choose animation frames. Character frames live in regular Unity Animator clips under `Assets/Animation/<CharacterName>`, using the same `Speed`, `IsWalkingUp`, `IsWalkingDown`, `IsWalkingLeft`, and `IsWalkingRight` parameter protocol as the controllable player.

Room people use a UI `Image` plus an `Animator` override controller so they still live inside the painted room stage. The generated clips animate both the Image `m_Sprite` field and SpriteRenderer `m_Sprite` field. This keeps the animation editable in Unity's Animation window while preserving the room-stage panning/zooming workflow.

The current Chapter 1 guest and butler animation sprites live in `Assets/Art/Characters/guest1` through `Assets/Art/Characters/guest8` and `Assets/Art/Characters/butler`. Keep frames normalized to a shared foot baseline, and move each `.png.meta` with its `.png` so Unity Animator clips keep the same sprite GUID references. Older prototype character sources may still live in `Assets/Art/Library/LegacyCharacters/<CharacterName>`.

Exact duplicate idle holds are consolidated: animation clips may reuse an earlier idle frame or a matching neutral walk frame instead of keeping multiple identical PNG files. This keeps the folders smaller without changing the visible timing.

If a character's original sheet is side-only, add an explicit `directional/aligned` folder before rebuilding. Rows are interpreted like the butler controller: row 1 walks down/toward camera, row 2 walks left, row 3 walks right, and row 4 walks up/away. `GentlemanBlack` uses this folder because its original sheets did not contain authored front/back rows.

`ButlerClassic` currently has the most complete player-facing setup: `Assets/Animation/ButlerClassic/ButlerClassic.controller` uses persistent `IsFacingUp`, `IsFacingDown`, `IsFacingLeft`, and `IsFacingRight` Animator parameters to pick one of four looping directional idle clips. Its source idle and walk frames live in `Assets/Art/Characters/butler`.

Dynamic room occlusion is y-depth based by default. Inside a profiled room, `RoomPerspectiveProfile` is the shared sorting domain for the Butler/player, projected guests, UI Image room people, and y-sorted room props. Objects at the same room-local foot/base Y should receive effectively the same base sorting order. Outside a profiled room, world objects fall back to the old `1000 - y * 100` style sorting.

Use `RoomProjectedEntity` for projected guests/people. UI Image guests sort through a local override-sorting `Canvas` owned by `RoomProjectedEntity`; they should not rely on transform sibling order. Use `WorldYSortSpriteRenderer` for world SpriteRenderer props and occluders that the Butler can walk in front of or behind. If an active `RoomProjectedEntity` is present, projection owns sorting and `WorldYSortSpriteRenderer` does not write orders.

Use `YSortSolidObstacle2D` when a prop needs an editable physical base for sorting/occlusion safety. Use `YSortOcclusionFootprint2D` for large or diagonal objects whose pivot cannot describe their footprint, such as pool tables, beds, carts, desks, and coffee tables. Author a depth line across the object so the prop can make a tiny local correction relative to the actor when the actor is near the footprint. Do not fix normal occlusion with arbitrary per-room or per-object absolute sorting orders.

Some single sprites cannot represent partial occlusion correctly. Split that art into a back/top/base sprite that y-sorts normally and a foreground rail, edge, leg, or table-front sprite with a small deliberate local offset. Use this for beds, pool-table rails, desk fronts, and coffee-table fronts when the object must hide only part of a character.

Manual sorting offsets are reserved for true exceptions: sitting, lying, coat/body layering, shadows, held items, and deliberate foreground art. Use `DepthPoseSortingOverride` or small visual-profile offsets for those cases; normal walking/object occlusion should still come from foot/base y-depth.

## Room Projection

`RoomPerspectiveProfile` is the source of truth for a painted room's shared perspective. It stores the room id, reference texture size, near/far foot Y values, scale curve, depth tint, sorting curve, optional contact-shadow curves, and future floor bounds. If two people stand at the same room-local foot Y in the same room profile, they should receive the same room scale and sorting depth.

`CharacterVisualProfile` owns source-art normalization. Put deliberate differences like a taller guest, sitting height, foot pivot, and local body/coat/shadow sorting offsets there. Do not tune guest height by randomly scaling each guest root in the scene.

`RoomProjectedEntity` owns the projection. Chapter controllers and waypoint movers should move its room-local foot point; the component applies position, visual-root scale, depth tint, SpriteRenderer sorting, and optional contact shadow. Keep animation clips focused on sprite changes and pose. Keep coat replacement focused on sprites. Neither should fight the logical root scale.

`ActorRoomState` still owns story identity, current room, visibility, interactability, chapter availability, and seated state. When a projected entity is active for the actor's current room, `ActorRoomState` leaves room-stage projection scale/motion to `RoomProjectedEntity`.

Chapter controllers should place guests by authored room anchors such as `DrawingRoomGuestPoint_01`, then let `RoomProjectedEntity` convert that anchor into a room-local foot point. Existing transform and `ActorRoomState.PlaceAt` paths remain as compatibility fallbacks for guests that have not migrated yet.

To calibrate the Drawing Room, open `Tools > Room Projection > Calibration Window`, create the Drawing Room perspective profile, then assign it to the Drawing Room `RoomContentGroup` and to any `RoomProjectedEntity` that is not under the room stage. Adjust near/far Y until a standard adult preview matches the painting at the front and back of the floor, then tune the scale/tint/sorting curves. Add `RoomProjectedEntity` to guests, set their `Visual Root` to the animated sprite child, assign a suitable `CharacterVisualProfile`, and move only the room-local foot point or the existing `DrawingRoomGuestPoint_##` anchors.

## Butler Room Scale Calibration

The controllable Butler uses `PointClickPlayerMovement` Butler room scale overrides. Guests are unchanged and still use `RoomProjectedEntity` projection and room visual scale tools.

Workflow:

1. Open Tools > Butler > Room Scale Calibration.
2. Select/find the Butler.
3. Pick a room.
4. Move the Butler to the front/closest walkable area.
5. Adjust Preview Final Butler Local Scale until the Butler looks correct.
6. Click SAVE FRONT.
7. Move the Butler to the back/farthest walkable area.
8. Adjust Preview Final Butler Local Scale again.
9. Click SAVE BACK.
10. Optionally click PREVIEW SAVED SIZE AT CURRENT POSITION.
11. Use RESTORE BUTLER START TRANSFORM before saving the scene.
12. Save Scene.
13. Test in Play Mode by walking the Butler around the room.

Visual target:
- roughly 3/4 of a matching door height
- or roughly 1.5x a matching chair

Do not edit Transform scale manually for calibration. Do not use Advanced reset buttons unless intentionally resetting. Guests are unchanged.

The prototype walking NPCs are currently disabled in the gameplay scene. Keep `RoomPersonWalker2D` available for future authored NPC movement, but do not rely on random walkers for the Chapter 1 slice.

Useful tweaks:

- Move path points on `RoomPersonWalker2D` to change where a person walks.
- Resize the `Image` RectTransform to change character height.
- Fix frame timing, bad frames, or direction bugs in `Assets/Animation/<CharacterName>/*.anim`, not in `RoomPersonWalker2D`.
- Use `Dreadforge > Characters > Rebuild Character Animation Assets` after changing legacy source frame folders under `Assets/Art/Library/LegacyCharacters`; use character-specific rebuild menu items when they exist.
- Adjust `Near Y`, `Far Y`, `Near Scale`, and `Far Scale` for perspective.
- Keep `Preview Path In Edit Mode` off while placing people. The animation frames still preview, but the scene object will not quietly walk away while you edit.
- Keep `Snap To Whole Pixels` off for scaled room walkers unless a specific character needs crunchy pixel locking. Subpixel motion reads smoother while the room stage pans and zooms.
- Use the motion polish fields for tiny stride bob, sway, endpoint pauses, and idle breathing. These are only offsets on the card; the path points remain the stable foot positions.
- Add `WorldYSortSpriteRenderer` to any world SpriteRenderer prop that should sort in front of or behind the Butler by base/pivot Y.
- Add `YSortSolidObstacle2D` to props that need an editable base footprint, then resize the trigger collider to the base area used for sorting and occlusion safety.
- Add `YSortOcclusionFootprint2D` and an authored depth line to wide or diagonal props such as pool tables, beds, desks, carts, and coffee tables.
