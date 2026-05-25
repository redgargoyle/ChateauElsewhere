# Character Animation Tools

## ButlerClassic Idle Variants

Run this from the project root:

```bash
python3 tools/character_animation/build_butler_classic_idle_variants.py
```

The script rebuilds:

- source PNG frames in `Assets/Characters/ButlerClassic/idle_variants`
- Unity idle clips in `Assets/Animation/ButlerClassic/IdleVariants`
- the five selectable ButlerClassic idle variant controllers

It preserves existing Unity GUIDs when `.meta` files already exist, so the
Gameplay character selector stays wired up.

The action poses are intentionally defined in code near the top-level drawing
functions: `watch_pose`, `smoke_pose`, and `scratch_pose`. Tweak those point
coordinates when an arm, hand, pipe, watch, or scratch pose needs adjustment,
then rerun the script.

## Manual Polish Pass

The generated frames are a starting rig, not the final art pass. For a polished
action idle, paint over the generated PNGs directly:

1. Open the eight frames for one direction in Aseprite, Krita, Photoshop, or
   Photopea. Aseprite is the easiest for onion-skin timing; Krita is stronger
   for painting.
2. Keep the canvas at `168x299` and do not move the feet. The bottom-center
   pivot is what keeps the character from sliding in Unity.
3. Make layers in this order: `base`, `old-arm-cover`, `sleeve`, `cuff`,
   `hand`, `prop`, `smoke/effect`.
4. Hide or paint over the original resting arm/hand on the side that is acting
   before painting the new arm. If both the old arm and new arm are visible,
   the pose reads as a third arm.
5. Use onion skin and keep the elbow/wrist moving on a small arc. The hand
   should not pop from the waist to the chest; it should follow the sleeve.
6. Export over the same PNG names, then run Unity's
   `Dreadforge > Characters > Rebuild ButlerClassic Idle Variant Assets`.

Recommended order: polish `right` and `down` first. Those are the angles where
players notice action-idle mistakes most clearly.
