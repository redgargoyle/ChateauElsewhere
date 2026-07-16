# Character Presentation After Phase 1

Phase 1 leaves guest and Butler sizes as static authored transform data. There is no supported runtime or editor-time character-size writer anywhere in the Phase 1 architecture, and the Phase 2 universal size tool does not exist yet. Do not add a replacement scale curve, calibration bridge, preview writer, or per-room sizing workflow before Phase 2 owns that responsibility explicitly.

Animation clips and character-facing code may change sprites, pose, renderer flipping, and Animator parameters, but they must not animate or assign character transform scale. Sitting and standing controller mappings remain authored animation behavior; Phase 1 does not introduce a new size override for them.

Drawing Room and Dining Room anchors, sitting mappings, and occlusion bindings remain presentation data. There is no separate eating runtime state or approved eating clip in Phase 1, and neither sitting nor eating currently owns a scale override.

## Current responsibility boundaries

- `RoomPerspectiveProfile` stores room identity, near/far foot depth, tint, sorting, contact-shadow presentation, and optional floor bounds. It does not store or evaluate character or prop scale.
- `RoomProjectedEntity` may project position, tint, sorting, and contact-shadow presentation. It preserves the authored scale of its visual root.
- `RoomPersonWalker2D` owns room-local path movement, facing, Animator parameters, and depth tint. It preserves its authored card scale.
- `ActorRoomState` owns story identity, current room, visibility, interactability, chapter availability, and seated state. It does not size actors.
- `PointClickPlayerMovement` owns controllable-character movement, routing, animation state, position-stage conversion, and sorting. It does not size the Butler or guests. Phase 2 may read `CurrentRoomId` and `TryGetCurrentRoomLocalFootPoint`; `LogicalPosition` remains the navigation coordinate rather than the room-local sizing input.
- `WorldYSortSpriteRenderer` and `YSortSolidObstacle2D` handle sorting and physical-footprint concerns only.

Room-stage panning and zooming may scale a room hierarchy as a single presentation surface. That coordinate-system behavior is not a character-size rule and must not be converted into a per-character scale writer. Phase 2 must account for inherited room-stage zoom exactly once; existing movement and projection systems must not precompensate body size.

## Authoring rules until Phase 2

Keep each character's existing authored root and visual scales unchanged unless a deliberate asset correction is reviewed separately. Move room anchors to tune placement. Adjust animation clips for sprites and timing, and use renderer sorting or occlusion bindings for layering. Never solve a placement, pose, tint, sorting, or occlusion issue by adding runtime character scaling.

The Phase 2 tool will define the single supported interface for selecting rooms and characters and authoring front, back, preview, and curve-based sizes. Until that implementation lands, there is intentionally no supported character-size calibration workflow.
