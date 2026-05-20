# Room Lighting Animation

The current prototype uses soft UI overlays generated at runtime from `Resources/Lighting/RoomLightingPreset.asset`.

`Gameplay.unity` has a visible `RoomLightingController` object. If the lights ever disappear, check that object first: it owns the preset reference, HUD toggle, and `L` key binding.

## Options We Considered

1. **Soft overlay lights**: transparent radial glows placed over the prerendered room images. This is implemented first because it is small, fast to tune, and keeps every light in one editable preset.
2. **Hand-painted frame swaps**: alternate lit/unlit room frames or small animated patches. This can look the most like a 90s prerendered game, but it needs more art export discipline.
3. **Mask/shader lighting**: room-specific mask textures driven by a shader. This is powerful, but heavier to debug and less friendly while the room art is still changing.

## How To Tune

Enter Play mode, open `RoomLightingPreset.asset`, and adjust a light's room name, position, size, color, alpha, speed, and animation style. The runtime keeps reading the preset so small changes can be auditioned quickly.

Press `L` or use the `Lights` button to turn room lights on and off.

Once a room feels right, Codex can copy the pattern to more rooms or turn rough values into cleaner final presets.
