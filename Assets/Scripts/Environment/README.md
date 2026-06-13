# Room Environment Authoring

Use `ChataeuChatilly > Rooms > Environment Authoring` in `Gameplay.unity`.

The tool creates missing editable scene objects only. It does not reset existing objects, so it is safe to run again after Hamza has moved or resized things.

## Workflow

1. Open `Gameplay.unity`.
2. Run `ChataeuChatilly > Rooms > Environment Authoring`.
3. Click `Create Missing Suggested Items In Open Scene`.
4. Expand a room under `Canvas_Background/Rooms`.
5. Polish these children directly in Edit mode:
   - `Lighting`: soft overlay lights that preview immediately.
   - `TrueParticleFire`: real `ParticleSystem` fire placeholders.
   - `AnimatedPatches`: inactive placeholders for prerendered frame swaps or image-particle tricks.

## Lighting Methods

Use all three approaches where they fit:

- Global bloom / soft overlay: tune `RoomLightOverlay` objects under `Lighting`.
- Prerendered image patches: assign a `StaticSet` to placeholders under `AnimatedPatches`, then activate the object.
- True particle fire: tune `ParticleSystem` objects under `TrueParticleFire`.

Fireplaces usually want both a small `FireplaceSource` overlay and a wider `HearthBreath` room spill, plus a true particle fire when the flame should have visible motion.

## Character Depth

People should keep their visual pivot at the foot/base line. For UI characters this means the RectTransform pivot should sit near the feet, not at the center of the sprite. That keeps y-based scale and sorting grounded.
