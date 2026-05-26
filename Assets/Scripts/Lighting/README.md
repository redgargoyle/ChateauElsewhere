# Room Lighting Animation

The current prototype uses soft UI overlays that live as editable scene objects under each room's `Lighting` child.

`Gameplay.unity` has a visible `RoomLightingController` object. If the lights ever disappear, check that object first: it owns the preset reference, HUD toggle, and `L` key binding.

The preset at `Resources/Lighting/RoomLightingPreset.asset` is a starter/template. It creates missing `RoomLight_*` objects, but once a light exists, the scene object is the thing to move, resize, recolor, and tune.

## Fireplace Source Pattern

Hamza's fireplace idea works best as two separate overlays:

1. **FireplaceSource**: a small, hotter core placed directly on the visible flames/embers. It flickers faster and uses a tighter generated sprite, so the room has an obvious light source.
2. **HearthBreath**: a wider, slower spill over nearby walls, floors, and furniture. This is the piece that makes the room feel lit rather than just decorated.

The first comparison set is in `Kitchen`, `Drawing Room`, `Dining Room`, `Master Bedroom Suite`, and `Billiard Room`. Each has a small source light paired with a larger spill so the source and room-lighting behavior can be judged independently.

## Flame Local Lights

For real particle flames, keep the particle readable by rendering it after post-processing, then let the particle carry its own local light/glow. This keeps fireplace light tight to the source instead of asking the global volume to bloom the whole room.

Select the flame root or any child particle object and run `Dreadforge > Lighting > Setup Selected Flame Local Light`. The command adds `NoPostProcessRenderLayer`, keeps the flame particles and local glow on `NoPostProcessFlame`, adds `FlameLocalLight`, creates a `LocalFlameLight2D` child on the normal camera layer, and ensures `Camera_NoPostProcessFlame` renders the particle layer after the main camera with post-processing disabled.

At runtime the bootstrap scans flame/fire/hearth particles in every room and applies the same setup automatically. `FlameLocalLight` configures a small URP `Light2D` for the room background only and a tight local sprite glow fallback for image-heavy rooms. Tune the flame component first; use `RoomLight_*` overlays only for deliberate painted spill that should affect the room image.

## Prototype Tour

Use the map/doors to check these moods:

- `Kitchen`: practical hearth plus counter warmth.
- `Drawing Room`: central fireplace source and broad parlor spill.
- `Dining Room`: right-side fireplace sidewash plus a low table candle line.
- `Master Bedroom Suite`: fireplace warmth against soft lamp and moon-window layers.
- `Billiard Room`: fireplace warmth competing with a cooler green table pool.
- `Blue Bedroom`: cool moonwash plus calm bedside warmth.
- `Upper Gallery`: cold oculus glow with distant fireplace warmth in the side room.
- `Chapel` and `Conservatory`: existing candle/glass examples for sacred versus airy light.

## Options We Considered

1. **Soft overlay lights**: transparent radial glows placed over the prerendered room images. This is implemented first because it is small, fast to tune, and keeps every light in one editable preset.
2. **Hand-painted frame swaps**: alternate lit/unlit room frames or small animated patches. This can look the most like a 90s prerendered game, but it needs more art export discipline.
3. **Mask/shader lighting**: room-specific mask textures driven by a shader. This is powerful, but heavier to debug and less friendly while the room art is still changing.

## How To Tune

Open `Gameplay.unity`, expand a room, then expand its `Lighting` child. Select a `RoomLight_*` object and adjust its RectTransform plus light fields directly in Edit mode. The glow previews immediately, and the animation styles also repaint in Edit mode. For flame particles, tune the `FlameLocalLight` on the particle object instead of increasing global bloom.

Edit mode does not animate the light object's transform scale, so resizing with the Rect/Scale tools should stick. In Play mode the flicker/breath styles animate around the scale saved on the scene object.

Use the `RoomLightingController` context menu item `Create Missing Scene Lights From Preset` only when you add new preset entries and want matching scene objects created for them.

Press `L` or use the `Lights` button to turn room lights on and off.

Once a room feels right, Codex can copy the pattern to more rooms or turn rough values into cleaner final presets.
