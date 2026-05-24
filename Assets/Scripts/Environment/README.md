# Room Environment Authoring

Use `Dreadforge > Rooms > Environment Authoring` in `Gameplay.unity`.

The tool creates missing editable scene objects only. It does not reset existing objects, so it is safe to run again after Hamza has moved or resized things.

## Workflow

1. Open `Gameplay.unity`.
2. Run `Dreadforge > Rooms > Environment Authoring`.
3. Click `Create Missing Suggested Items In Open Scene`.
4. Expand a room under `Canvas_Background/Rooms`.
5. Polish these children directly in Edit mode:
   - `Lighting`: soft overlay lights that preview immediately.
   - `TrueParticleFire`: real `ParticleSystem` fire placeholders.
   - `AnimatedPatches`: inactive placeholders for prerendered frame swaps or image-particle tricks.
   - `ForegroundOccluders`: cropped room-image cards that let people pass behind furniture.

## Occluders

`RoomForegroundOccluder` now auto-crops from its own `RectTransform`.

Move or resize the occluder rectangle and it will update the `RawImage.uvRect` from the room background. This is much faster than manually editing UV values.

For railings, banisters, thin chair backs, wall edges, and anything with holes, make a transparent foreground plate instead of leaving the whole rectangle opaque:

1. Select the occluder object in the Hierarchy.
2. Run `Dreadforge > Rooms > Occluders > Export Selected Dark Foreground Plate`.
3. The tool writes a PNG under `Assets/Art/ForegroundOccluders/<RoomName>/`, assigns it to the selected occluder, and turns off auto-crop for that object.
4. Polish the saved PNG alpha by hand if the first-pass mask kept too much floor/wall paint.

Use `Export Selected Rectangular Plate` when you want an exact opaque crop first, such as a bed footboard or table front that does not need holes.

Grand Entrance Hall already has a starter full-room plate at `Assets/Art/ForegroundOccluders/Grand_Entrance_Hall/GEH_ForegroundRailingSystem_fullroom_dark-plate.png`. It includes both lower foreground railings, the center returns, and leaves the walkway/gaps transparent.

Good occluder candidates are:

- Near table fronts.
- Bed footboards.
- Stair rails and balcony rails.
- Piano bodies and benches.
- Chairs, plants, counters, cabinets, and sideboards that are visibly in front of the walkable floor.

Keep occluder objects under `ForegroundOccluders`, above `People` in sibling order.

## Lighting Methods

Use all three approaches where they fit:

- Global bloom / soft overlay: tune `RoomLightOverlay` objects under `Lighting`.
- Prerendered image patches: assign a `StaticSet` to placeholders under `AnimatedPatches`, then activate the object.
- True particle fire: tune `ParticleSystem` objects under `TrueParticleFire`.

Fireplaces usually want both a small `FireplaceSource` overlay and a wider `HearthBreath` room spill, plus a true particle fire when the flame should have visible motion.

## Character Depth

People should keep their visual pivot at the foot/base line. For UI characters this means the RectTransform pivot should sit near the feet, not at the center of the sprite. That keeps y-based scale/sorting and foreground occluders feeling grounded.
