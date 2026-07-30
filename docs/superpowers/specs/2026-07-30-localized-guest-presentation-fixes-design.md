# Localized Guest Presentation Fixes Design

## Goal

Correct the reported coat scale, Drawing Room seated armrest occlusion, Baron Hector Glass/Kitchen table depth, one Dining Room chair overlap, and Chapter HUD TMP overlap without changing the game-wide sorting architecture or producing builds.

## Constraints

- Make guest-carried coats exactly twice their current rendered width and height.
- Keep the coat visually attached to the same hand after scaling.
- Limit sorting changes to the reported Drawing Room seats, `kitchen_work_table`, and the anomalous Dining Room chair area.
- Preserve the working y-axis occlusion setup in all other rooms.
- Use bottom/pivot-based sort references for the affected Kitchen and Dining Room props.
- Never show Chapter 1 and Chapter 2 top-left status TMP blocks simultaneously.
- Do not create Windows, macOS, or Linux executables in this change.

## Root Causes

### Guest coats

`Chapter1ArrivalController.ApplyAssignedCoatSprite` deliberately multiplies authored replacement-coat scale by `WornCoatVisualScaleMultiplier`, currently `0.5f`. The subsequent hand-selection routine mirrors the coat by its rendered center, so simply changing the multiplier can preserve the side but does not explicitly preserve the hand-facing grab point.

### Drawing Room armrests

The seated-guest exception places a guest between a chair/sofa renderer and the tea table. The scene already contains separate front armrest cutouts (`redcoucharmrest_0` and `greenchairarmrest_0`), but the exception has no front-occluder concept. Those cutouts therefore continue using independent world-y orders and can fall behind a seated character.

### Kitchen table

`kitchen_work_table` has bottom-left sprite pivot metadata and uses `SpriteSortPoint.Pivot`, but its renderer order is owned by the movement blocker's lowest collider edge. That threshold is substantially below the authored sprite pivot, keeping the table in front after an actor's feet have visually crossed to the table's foreground side.

### Dining Room chair

`DiningChair_Rightback04_Overlay` is the local outlier: it is not represented in the seated binding list and its blocker has `sortSourceRenderers: 0`. The ordinary seated exception consequently raises a nearby guest to the dining-table band without accounting for this foreground chair cutout.

### TMP overlap

Chapter 1 and Chapter 2 each create a status TMP block at the same top-left anchored position. Chapter 1 continues updating “Outside / Hall / Drawing Room / Hands free” after Chapter 2 creates its own status, so two separate TMP objects render over one another. This is a chapter-HUD lifecycle problem, not text wrapping within one TMP object.

## Design

### Coat scaling and grab-point preservation

Restore the authored replacement-coat multiplier from `0.5f` to `1f`. Before changing scale, capture the coat's hand-facing upper-inner grab point in the coat placement space; after scaling, translate the coat so that point returns to the same location. Run the existing waist and hand-side alignment afterward. Fallback coats will also use a doubled fallback scale so every guest-carried coat receives the same visual doubling.

The coat remains an actor child and retains its current sorting offset. Butler transfer logic is not broadened; it continues to carry the same coat object and presentation.

### Optional seated front occluder

Extend `DiningRoomSeatedGuestOcclusionException` with an optional front-occluder renderer. When present, choose a guest order strictly above the assigned seat back and strictly below both the front occluder and table. Capture and restore any runtime order adjustment made to that front cutout.

Pass the existing Drawing Room armrest cutouts only for the seats that overlap them. Do not alter standing guests or unrelated Drawing Room furniture.

Extend the Dining Room seat binding with the same optional front-occluder reference and use it only for the reported chair area. Keep all existing seat mappings unchanged except the affected binding.

### Kitchen table bottom-pivot ownership

Give `kitchen_work_table` a single `WorldYSortSpriteRenderer` whose `yReference` is the table's authored bottom pivot. Disable sorting ownership on `PlayerBlocker_kitchen_work_table` while retaining its collision footprint. This removes the collider-min threshold without modifying `ObjectMovementBlocker2D` or any other room.

### Chapter HUD lifecycle

Expose a Chapter 1 HUD visibility method and call it when Chapter 2 starts. The Chapter 1 update loop must not reactivate its status while hidden. Chapter 1 initialization restores visibility for a new Chapter 1 run. Chapter 2's own status layout remains unchanged.

## Verification

- Regression tests prove authored and fallback coats render at twice their former dimensions while their hand-facing grab point remains stable.
- Scene regression tests prove only the named Kitchen table and Dining/Drawing front cutouts receive the new references.
- Occlusion unit tests prove `chair/back < guest < front occluder/table` and restoration on deactivation.
- HUD regression tests prove Chapter 2 hides Chapter 1 status and a hidden Chapter 1 HUD cannot reactivate itself.
- Run the focused EditMode suites plus relevant existing character, collision, navigation, and Chapter 2 regression suites.
- Do not invoke any build script or standalone build target.

