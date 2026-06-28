# Room People

People are authored as real scene objects under each room's `People` child. They move with the room stage, so panning and zooming keep them locked to the painted background.

`RoomPersonWalker2D` now only owns legacy room-local path movement, per-object depth scale/tint when no projection is present, tiny step/idle offsets, and Animator parameter updates. If the same object has an active `RoomProjectedEntity`, the walker feeds the projected foot point and lets projection own position, scale, and tint. It does not choose animation frames. Character frames live in regular Unity Animator clips under `Assets/Animation/<CharacterName>`, using the same `Speed`, `IsWalkingUp`, `IsWalkingDown`, `IsWalkingLeft`, and `IsWalkingRight` parameter protocol as the controllable player.

Room people use a UI `Image` plus an `Animator` override controller so they still live inside the painted room stage. The generated clips animate both the Image `m_Sprite` field and SpriteRenderer `m_Sprite` field. This keeps the animation editable in Unity's Animation window while preserving the room-stage panning/zooming workflow.

The current Chapter 1 guest and butler animation sprites live in `Assets/Art/Characters/guest1` through `Assets/Art/Characters/guest8` and `Assets/Art/Characters/butler`. Keep frames normalized to a shared foot baseline, and move each `.png.meta` with its `.png` so Unity Animator clips keep the same sprite GUID references. Older prototype character sources may still live in `Assets/Art/Library/LegacyCharacters/<CharacterName>`.

Exact duplicate idle holds are consolidated: animation clips may reuse an earlier idle frame or a matching neutral walk frame instead of keeping multiple identical PNG files. This keeps the folders smaller without changing the visible timing.

If a character's original sheet is side-only, add an explicit `directional/aligned` folder before rebuilding. Rows are interpreted like the butler controller: row 1 walks down/toward camera, row 2 walks left, row 3 walks right, and row 4 walks up/away. `GentlemanBlack` uses this folder because its original sheets did not contain authored front/back rows.

`ButlerClassic` currently has the most complete player-facing setup: `Assets/Animation/ButlerClassic/ButlerClassic.controller` uses persistent `IsFacingUp`, `IsFacingDown`, `IsFacingLeft`, and `IsFacingRight` Animator parameters to pick one of four looping directional idle clips. Its source idle and walk frames live in `Assets/Art/Characters/butler`.

World-space SpriteRenderer props use `WorldYSortSpriteRenderer` when they need to depth-sort against the controllable butler and are not using room projection. It mirrors `PointClickPlayerMovement`'s player sorting rule: Sorting Layer `People`, order `1000 - y * 100`, and Sprite Sort Point `Pivot`. If an active `RoomProjectedEntity` is present, projection owns sorting and `WorldYSortSpriteRenderer` does not write orders. The player sorts from the bottom of the visible SpriteRenderer bounds, and props can sort from the bottom of an editable physical footprint. A copied tutorial prop will not y-sort just because its pivot is bottom-center; it also needs this dynamic sorting order.

Props that need a custom base footprint can also use `YSortSolidObstacle2D` plus a 2D trigger collider around the base area. That footprint drives sorting and optional occlusion safety only; player movement is intentionally controlled by the active `PlayerBoundary` floor collider. Keep the collider tight around the physical base, not the full painted sprite. Keep `Force Behind Player Inside Physical Bounds` off for grouped props like mushrooms so nearby props do not reorder while the player moves between them.

## Room Projection

`RoomPerspectiveProfile` is the source of truth for a painted room's shared perspective. It stores the room id, reference texture size, near/far foot Y values, scale curve, depth tint, sorting curve, optional contact-shadow curves, and future floor bounds. If two people stand at the same room-local foot Y in the same room profile, they should receive the same room scale and sorting depth.

`CharacterVisualProfile` owns source-art normalization. Put deliberate differences like a taller guest, sitting height, foot pivot, and local body/coat/shadow sorting offsets there. Do not tune guest height by randomly scaling each guest root in the scene.

`RoomProjectedEntity` owns the projection. Chapter controllers and waypoint movers should move its room-local foot point; the component applies position, visual-root scale, depth tint, SpriteRenderer sorting, and optional contact shadow. Keep animation clips focused on sprite changes and pose. Keep coat replacement focused on sprites. Neither should fight the logical root scale.

`ActorRoomState` still owns story identity, current room, visibility, interactability, chapter availability, and seated state. When a projected entity is active for the actor's current room, `ActorRoomState` leaves room-stage projection scale/motion to `RoomProjectedEntity`.

Chapter controllers should place guests by authored room anchors such as `DrawingRoomGuestPoint_01`, then let `RoomProjectedEntity` convert that anchor into a room-local foot point. Existing transform and `ActorRoomState.PlaceAt` paths remain as compatibility fallbacks for guests that have not migrated yet.

To calibrate the Drawing Room, open `Tools > Room Projection > Calibration Window`, create the Drawing Room perspective profile, then assign it to the Drawing Room `RoomContentGroup` and to any `RoomProjectedEntity` that is not under the room stage. Adjust near/far Y until a standard adult preview matches the painting at the front and back of the floor, then tune the scale/tint/sorting curves. Add `RoomProjectedEntity` to guests, set their `Visual Root` to the animated sprite child, assign a suitable `CharacterVisualProfile`, and move only the room-local foot point or the existing `DrawingRoomGuestPoint_##` anchors.

## Butler Room Scale Calibration

The controllable Butler uses `PointClickPlayerMovement` Butler room scale overrides.

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

Do not edit Transform scale manually for calibration. Do not use Advanced reset buttons unless intentionally resetting.

## Guest Scaling From Butler Rules

The manually calibrated Butler room scale is now the shared depth scale source for guests. Guests do not copy the Butler's raw size. Guests use the Butler's normalized room/depth scale multiplier, then keep their own authored scale, sitting/standing art proportions, and `CharacterVisualProfile.HeightScaleMultiplier`.

Guest scaling also uses a final visual-height fitter. The Butler's manually calibrated room scale gives the depth rule, but guests are finally resized by measured rendered height, not by raw `localScale`. This is necessary because the Butler and guests may live in different coordinate systems. The fitter resolves visible art first, then keeps separate `BoundsRoot`, `ScaleRoot`, and primary visual targets so containers and room stages do not accidentally become the measured character.

`RoomProjectedEntity`, `RoomPersonWalker2D`, and world-space `ActorRoomState` guests consume the same Butler scale evaluator. `GuestButlerScaleHarmonizer` runs late to prevent old scale writers or room visual overrides from hiding the result, then uses `CharacterVisualBoundsUtility` to fit each guest's visible screen height to the Butler-derived target height.

Manual guest scale calibration is available from `Tools > Characters > Manual Guest Scale Calibration`. This stores per-guest/per-room entries in `GuestScaleCalibrationStore` on the Butler or harmonizer object. A saved entry can choose the exact `scaleRoot` to resize and the `boundsRoot` to measure, which is the escape hatch when automatic root detection picks a room/container instead of the visible guest art.

Manual entries do not create per-guest front/back room curves. The Butler room calibration remains the room/depth source. A manual guest target height is:

`Butler standing reference height * Butler normalized room/depth scale * pose ratio * manual fine tune`

The manual path bypasses old guest visible-height inputs while the final fitter is active: `RoomProjectedEntity.roomVisualScaleOverrides`, `CharacterVisualProfile.HeightScaleMultiplier`, `RoomPersonWalker2D` near/far/profile scale, and `ActorRoomState` room-stage scale writes may still exist for compatibility, but the final harmonizer writes the visible result last.

Pose ratios:

- Standing: `1.0`
- Seated: `0.68` by default, or `SittingVisualHeight / StandingVisualHeight` from `CharacterVisualProfile` clamped to `0.55-0.80`
- Crouching: `0.75`
- Lying: `0.45`

To enable:

1. Open `Tools > Characters > Apply Butler Scaling To Guests`.
2. Click `Find Scene Butler`.
3. Click `Add/Ensure GuestButlerScaleHarmonizer`.
4. Click `ENABLE FINAL HUMAN SCALE FOR ALL GUESTS`.
5. Click `PRINT SCALE WRITER AUDIT`.
6. Click `REFRESH FINAL HUMAN SCALE NOW`.
7. Use `PROOF 50%` and `PROOF 150%`.
   Every detected guest should visibly change, even if that room has no Butler calibration.
8. Check `Room Calibration Coverage`.
   Any room with guests and no Butler calibration must be calibrated or aliased before real Butler scaling can affect those guests.
9. Click `SAVE SCENE`.
10. Test in Play Mode by standing the Butler next to guests and confirming their visual height/scale belongs to the same room system.

Manual workflow:

1. Open `Tools > Characters > Manual Guest Scale Calibration`.
2. Click `Find Scene Butler`.
3. Click `Ensure Calibration Store`.
4. Pick a room and guest.
5. Verify the selected scale root is the visible character art, not the room/container.
6. Set pose: `Standing`, `Seated`, `Crouching`, or `Lying`.
7. Click `Auto Match Guest To Butler Here`.
8. Adjust `Manual Fine Tune` if needed.
9. Click `Save Calibration For This Guest In This Room`.
10. Use `Next Guest` and repeat, then `Save Scene`.

Use `Tools > Characters > Human Scale Audit` or `PRINT ALL GUEST SCALE WRITERS` when a guest still looks wrong. The report lists each guest-like object's controller type, visible art, scale root, room-local foot Y, seated state, current/target visual height, and competing writers such as room visual overrides, profile height multipliers, walker near/far scale, room-stage scale motion, and Animator localScale curves.

Use `Tools > Characters > Guest Scale Override Audit` to write `Assets/Editor/Reports/GuestScaleOverrideAudit.md`. The audit summarizes old room visual overrides, non-1 character profile height multipliers, custom walker scale values, room-stage scaling actors, non-1 guest visual roots, Butler room calibrations, and rooms that contain guests without complete Butler calibration.

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
- Add `WorldYSortSpriteRenderer` to any world SpriteRenderer prop that should sort in front of or behind the butler by base/pivot Y.
- Add `YSortSolidObstacle2D` to props that need an editable base footprint, then resize the trigger collider to the base area used for sorting and occlusion safety.
