# Object Collision Box Plan and Prompt

## Summary

The movement blocker for an object should be the part that physically sits on the floor, not the full painted sprite or UI image. A chair is the clearest example: legs and the lower seat stop the Butler from walking through it, while the backrest is mostly an occlusion/sorting problem because the character can visually pass behind it.

Rule: physical blocker equals floor-contact footprint. Occlusion depth remains a separate y-sort/depth-line problem.

## Architecture

- Keep room walkable floors as `PlayerBoundary*` colliders.
- Keep no-walk holes as `PlayerBlocker_*` colliders under each `RoomContentGroup`.
- Add `ObjectMovementBlocker2D` as a marker for generated or hand-authored object movement blockers.
- Keep `YSortSolidObstacle2D` and occlusion footprints visual-only. They should not become movement blockers.
- Use an editor authoring tool to generate conservative lower-footprint blockers and log what it skipped.
- Do not mass-generate full sprite/image bounds.

## Collision Rules

- Chairs, stools: lower legs/seat base only.
- Sofas, benches, settees: lower base only.
- Tables, desks, carts: leg/base area only.
- Cabinets, wardrobes, bookcases, clocks: floor base strip.
- Beds and pianos: lower footprint, then review manually.
- Plants, urns, vases: small floor base.
- Wall art, paintings, portraits, windows, curtains, lights, flames, overlays, shadows: no movement blocker.

Large diagonal objects may still need an occlusion footprint or split art after movement blockers are added. The collision blocker stops walking through the physical base; it does not solve partial drawing order by itself.

## Designer Workflow

1. Open the gameplay scene in Edit Mode.
2. Open `Dreadforge > Object Collision > Collision Box Authoring`.
3. Click `Dry Run` and inspect the proposed blockers.
4. Click `Generate Missing PlayerBlockers` only after the dry run looks reasonable.
5. Inspect generated `PlayerBlocker_*` colliders room by room.
6. Move/reshape generated colliders manually when needed.
7. Test walking in Play Mode.
8. Keep unusual occlusion cases for the y-depth/occlusion branch, not this movement-blocker pass.

## Codex implementation prompt

You are working in `redgargoyle/ChateauElsewhere`.

Goal: add safe object movement collision boxes for room props without using full image bounds. Movement blockers must represent the floor-contact footprint of props. Do not change guest behavior or occlusion sorting as part of this pass.

Implementation requirements:

1. Preserve the existing `PointClickPlayerMovement` walkability architecture.
   - `PlayerBoundary*` colliders define walkable floor.
   - `PlayerBlocker_*` colliders define no-walk holes inside that floor.
   - `IsWalkableWorldPoint` remains the one path used by hover, click, and route building.

2. Add `ObjectMovementBlocker2D`.
   - It marks a generated or hand-authored object movement blocker.
   - It requires `Collider2D`.
   - It forces its collider to `isTrigger = true`.
   - It stores source object name, room name, category, footprint fraction, generated flag, and authoring notes.

3. Update `PointClickPlayerMovement`.
   - Keep collecting existing `PlayerBlocker_*` colliders.
   - Also collect enabled colliders with `ObjectMovementBlocker2D`.
   - Do not make `YSortSolidObstacle2D` a movement blocker.

4. Add editor tooling.
   - Create `Dreadforge > Object Collision > Collision Box Authoring`.
   - Include `Dry Run`, `Generate Missing PlayerBlockers`, and `Delete Generated PlayerBlockers`.
   - Generate child objects under the matching `RoomContentGroup`.
   - Name them `PlayerBlocker_<sourceObjectName>`.
   - Add `PolygonCollider2D` and `ObjectMovementBlocker2D`.
   - Mark the scene dirty after generation/deletion.

5. Use conservative lower-footprint heuristics.
   - Chair/stool: lower 30% of visual bounds with side inset.
   - Sofa/bench: lower 32%.
   - Table/desk/cart: lower 30%.
   - Cabinet/wardrobe/bookcase: lower 26%.
   - Bed: lower 42%, then review.
   - Piano: lower 35%, then review.
   - Plant/vase/urn: lower 26% with stronger side inset.
   - Skip wall decor, paintings, portraits, windows, curtains, lights, flames, overlays, and shadows.

6. Add regression tests.
   - Chair blockers use lower footprint, not full visual bounds.
   - Wall decor and lighting are skipped.
   - Generator creates `PlayerBlocker_*` under the room with `PolygonCollider2D` and `ObjectMovementBlocker2D`.
   - `PointClickPlayerMovement` collects explicit object blockers.
   - The authoring tool and this prompt exist.

7. Verification.
   - Run focused EditMode tests for object collision boxes.
   - Run related navigation regression tests if practical.
   - Inspect `git diff` and avoid scene mass edits unless explicitly requested.

Acceptance criteria:

- Chairs and similar props block only their floor-contact region, not their backrests.
- Existing `PlayerBlocker_*` colliders still work.
- Generated blockers are reviewable, named, and editable in the scene.
- Occlusion components remain visual-depth-only.
- The scene is not filled with arbitrary per-object full-image colliders.
