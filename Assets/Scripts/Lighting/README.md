# Room Lighting Animation

The current prototype uses soft UI overlays that live as editable scene objects under each room's `Lighting` child.

`Gameplay.unity` has a visible `RoomLightingController` object. If the lights ever disappear, check that object first: it owns the preset reference, HUD toggle, and `L` key binding.

The preset at `Resources/Lighting/RoomLightingPreset.asset` is a starter/template. It creates missing `RoomLight_*` objects, but once a light exists, the scene object is the thing to move, resize, recolor, and tune.

## Options We Considered

1. **Soft overlay lights**: transparent radial glows placed over the prerendered room images. This is implemented first because it is small, fast to tune, and keeps every light in one editable preset.
2. **Hand-painted frame swaps**: alternate lit/unlit room frames or small animated patches. This can look the most like a 90s prerendered game, but it needs more art export discipline.
3. **Mask/shader lighting**: room-specific mask textures driven by a shader. This is powerful, but heavier to debug and less friendly while the room art is still changing.

## How To Tune

Open `Gameplay.unity`, expand a room, then expand its `Lighting` child. Select a `RoomLight_*` object and adjust its RectTransform plus light fields directly in Edit mode. The glow previews immediately, and the animation styles also repaint in Edit mode.

Use the `RoomLightingController` context menu item `Create Missing Scene Lights From Preset` only when you add new preset entries and want matching scene objects created for them.

Press `L` or use the `Lights` button to turn room lights on and off.

Once a room feels right, Codex can copy the pattern to more rooms or turn rough values into cleaner final presets.
