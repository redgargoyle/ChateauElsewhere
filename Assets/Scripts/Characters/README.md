# Room People

People are authored as real scene objects under each room's `People` child. They move with the room stage, so panning and zooming keep them locked to the painted background.

`RoomPersonWalker2D` owns legacy room-local path movement, tiny step/idle offsets, Animator parameter updates, and—only for objects without `CharacterRoomScaleTarget`—its legacy per-object depth scale. If the same object has an active `RoomProjectedEntity`, the walker feeds the projected foot point and lets projection own position and tint. It does not choose animation frames. Character frames live in regular Unity Animator clips under `Assets/Animation/<CharacterName>`, using the same `Speed`, `IsWalkingUp`, `IsWalkingDown`, `IsWalkingLeft`, and `IsWalkingRight` parameter protocol as the controllable player.

Room people use a UI `Image` plus an `Animator` override controller so they still live inside the painted room stage. The generated clips animate both the Image `m_Sprite` field and SpriteRenderer `m_Sprite` field. This keeps the animation editable in Unity's Animation window while preserving the room-stage panning/zooming workflow.

The current Chapter 1 guest and butler animation sprites live in `Assets/Art/Characters/guest1` through `Assets/Art/Characters/guest8` and `Assets/Art/Characters/butler`. Keep frames normalized to a shared foot baseline, and move each `.png.meta` with its `.png` so Unity Animator clips keep the same sprite GUID references. Older prototype character sources may still live in `Assets/Art/Library/LegacyCharacters/<CharacterName>`.

Exact duplicate idle holds are consolidated: animation clips may reuse an earlier idle frame or a matching neutral walk frame instead of keeping multiple identical PNG files. This keeps the folders smaller without changing the visible timing.

If a character's original sheet is side-only, add an explicit `directional/aligned` folder before rebuilding. Rows are interpreted like the butler controller: row 1 walks down/toward camera, row 2 walks left, row 3 walks right, and row 4 walks up/away. `GentlemanBlack` uses this folder because its original sheets did not contain authored front/back rows.

`ButlerClassic` currently has the most complete player-facing setup: `Assets/Animation/ButlerClassic/ButlerClassic.controller` uses persistent `IsFacingUp`, `IsFacingDown`, `IsFacingLeft`, and `IsFacingRight` Animator parameters to pick one of four looping directional idle clips. Its source idle and walk frames live in `Assets/Art/Characters/butler`.

World-space SpriteRenderer props use `WorldYSortSpriteRenderer` when they need to depth-sort against the controllable butler and are not using room projection. It mirrors `PointClickPlayerMovement`'s player sorting rule: Sorting Layer `People`, order `1000 - y * 100`, and Sprite Sort Point `Pivot`. If an active `RoomProjectedEntity` is present, projection owns sorting and `WorldYSortSpriteRenderer` does not write orders. The player sorts from the bottom of the visible SpriteRenderer bounds, and props can sort from the bottom of an editable physical footprint. A copied tutorial prop will not y-sort just because its pivot is bottom-center; it also needs this dynamic sorting order.

Props that need a custom base footprint can also use `YSortSolidObstacle2D` plus a 2D trigger collider around the base area. That footprint drives sorting and optional occlusion safety only; player movement is intentionally controlled by the active `PlayerBoundary` floor collider. Keep the collider tight around the physical base, not the full painted sprite. Keep `Force Behind Player Inside Physical Bounds` off for grouped props like mushrooms so nearby props do not reorder while the player moves between them.

## Room Projection

`RoomPerspectiveProfile` remains the shared perspective source for projected position, tint, sorting, contact shadows, and legacy/unmanaged entity size. A Butler or Guest with `CharacterRoomScaleTarget` receives its displayed body size from `CharacterRoomScaleCatalog` instead; projection still owns the other responsibilities and does not write that target's scale root.

`CharacterVisualProfile` owns source-art normalization for projected entities. Put deliberate differences such as source height, foot pivot, and local body/coat/shadow sorting offsets there. For a managed Butler or Guest, use the character target's `Display Size Multiplier` only for a deliberate per-character fine adjustment; use the catalog for room-dependent size.

`RoomProjectedEntity` owns room projection. Chapter controllers and waypoint movers should move its room-local foot point; the component applies position, depth tint, SpriteRenderer sorting, and optional contact shadow. It applies its legacy visual-root scale only when no active `CharacterRoomScaleTarget` owns that transform. Keep animation clips focused on sprite changes and pose. Keep coat replacement focused on sprites. Neither should fight the managed character scale root.

`ActorRoomState` still owns story identity, current room, visibility, interactability, chapter availability, seated state, and room placement. When a managed target is present, it does not write the target's displayed size. No scale rule is allowed to alter room state, visibility, movement, collision, interaction, animation selection, tint, or sorting.

Chapter controllers should place guests by authored room anchors such as `DrawingRoomGuestPoint_01`, then let `RoomProjectedEntity` convert that anchor into a room-local foot point. Existing transform and `ActorRoomState.PlaceAt` paths remain as compatibility fallbacks for guests that have not migrated yet.

To calibrate general room projection, open `Tools > Room Projection > Calibration Window`. These controls still apply to props and unmanaged projected entities. Butler/Guest displayed body size is calibrated separately through the character room-scale catalog below.

## Character Room Scale

`CharacterRoomScaleCatalog`, `CharacterRoomScaleController`, and `CharacterRoomScaleTarget` form one isolated size module for the controllable Butler and the eight scene Guests.

The contract is strict:

- The catalog is the only source of room-dependent Butler/Guest size.
- The controller is the only authority that applies the catalog's final Y-size magnitude to a managed target.
- The target identifies the character's body scale root, profile, current room context, and room-local foot point.
- The module changes only the target scale root's `localScale`. It preserves the current facing sign, authored X:Y aspect ratio, and Z scale.
- Existing movement, position, rotation, animation, sorting, tint, visibility, collision, dialogue, interaction, and chapter state remain owned by their existing systems.

Each catalog room stores:

- front room-local foot Y
- back room-local foot Y
- Butler front and back final displayed `localScale.y`
- Guest front and back final displayed `localScale.y`
- one normalized scale function used between those endpoints
- the room-stage scale at which the endpoints were calibrated

At runtime, the target resolves the character's actual room and visible foot point from existing placement systems. Actual parent room, active projection/walker context, actor room state, and active navigation context take priority over serialized fallback fields. `currentRoomId`, `roomIdOverride`, and the authored Guest-number fallback exist only when no stronger placement source is available.

The controller evaluates the selected profile at that room-local foot Y, applies the target's optional display multiplier, compensates for the current room-stage zoom, and removes inherited stage zoom when the character is already parented under that stage. This keeps the character visually locked to the room without double-scaling.

A target's `Scale Root` must be the animated human body root. Do not select coats, jackets, speech/thought bubbles, prompts, highlights, icons, shadows, cursors, or tooltips. Those objects retain their existing independent behavior.

There are no room-size exceptions for hiding spaces, seated state, chapter state, or Guest identity. Seated Drawing Room/Dining Room exceptions remain sorting and occlusion rules only; they are not size rules.

### Calibration workflow

1. Open `Tools > Characters > Character Room Scale Catalog`.
2. Choose the room and `Butler` or `Guest` profile.
3. Select the corresponding scene character with `CharacterRoomScaleTarget`.
4. Move or place the character at the front calibration point and make its visible size correct.
5. Click `Capture As Front`.
6. Move or place it at the back calibration point and make its visible size correct.
7. Click `Capture As Back`.
8. Adjust `Scale Function` only when the interpolation between the two endpoints needs shaping.
9. Use `Apply Catalog Preview To Selected Character` to inspect the saved result at the current position.
10. Save the scene and test movement through the room in Play Mode.

The Gameplay scene is already migrated to one catalog, one controller, the Player target with the Butler profile, and eight Guest targets with the Guest profile. The previous separate Butler calibration window, Guest size master, Guest scale audit, Guest calibration component, Guest applier, Guest participant, and Guest stage-scale helper are intentionally removed. Do not reintroduce them or add another room-dependent scale writer.

Legacy scale APIs remain in movement/projection classes only so unrelated objects without `CharacterRoomScaleTarget` keep their existing behavior. Their inspectors no longer present those fields as Butler/Guest size authorities.

The prototype walking NPCs are currently disabled in the gameplay scene. Keep `RoomPersonWalker2D` available for future authored NPC movement, but do not rely on random walkers for the Chapter 1 slice.

Useful tweaks:

- Move path points on `RoomPersonWalker2D` to change where a person walks.
- For a managed Butler/Guest, adjust room endpoints in `Character Room Scale Catalog`; do not add another scale script.
- Fix frame timing, bad frames, direction bugs, or inconsistent foot baselines in `Assets/Animation/<CharacterName>/*.anim` and the source sprites, not in the size controller.
- Use `Dreadforge > Characters > Rebuild Character Animation Assets` after changing legacy source frame folders under `Assets/Art/Library/LegacyCharacters`; use character-specific rebuild menu items when they exist.
- `RoomPerspectiveProfile` near/far scale controls remain valid for props and unmanaged projected entities.
- Keep `Preview Path In Edit Mode` off while placing people. The animation frames still preview, but the scene object will not quietly walk away while you edit.
- Keep `Snap To Whole Pixels` off for scaled room walkers unless a specific character needs crunchy pixel locking. Subpixel motion reads smoother while the room stage pans and zooms.
- Use the motion polish fields for tiny stride bob, sway, endpoint pauses, and idle breathing. These are only positional offsets; the path points remain the stable foot positions.
- Add `WorldYSortSpriteRenderer` to any world SpriteRenderer prop that should sort in front of or behind the Butler by base/pivot Y.
- Add `YSortSolidObstacle2D` to props that need an editable base footprint, then resize the trigger collider to the base area used for sorting and occlusion safety.
