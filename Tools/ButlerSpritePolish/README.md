# Butler sprite polish pipeline

The playable Butler artwork is only 168x299 pixels and is displayed at more than
twice that size near the Dining Room camera.  This pipeline fixes the source matte
and produces deterministic 2x detail-recovery images without changing animation
timing, sprite GUIDs, pivots, or world-space size.

## 1. Clean the source alpha and matte

Run the reviewed cleanup into a temporary directory first:

```bash
gcc -std=c11 -D_POSIX_C_SOURCE=200809L -Wall -Wextra -Werror -O2 \
  Tools/CharacterSpriteCleanup/character_sprite_cleanup.c \
  -o /tmp/chateau-character-sprite-cleanup \
  $(pkg-config --cflags --libs MagickWand) -lm

rm -rf /tmp/chateau-butler-clean
/tmp/chateau-character-sprite-cleanup \
  Tools/CharacterSpriteCleanup/butler_sprites.txt \
  /tmp/chateau-butler-clean \
  Tools/CharacterSpriteCleanup/reviewed_internal_masks.csv
```

The cleanup reverses white-matte contamination on antialiased pixels and only cuts
opaque internal remnants inside reviewed arm/torso and leg-gap regions.

## 2. Recover 2x detail

The workstation's Forge environment supplies Torch, Spandrel, SciPy, Pillow, and
the local RealESRGAN x2 model:

```bash
/home/hamzak/ai/forge/venv/bin/python \
  Tools/ButlerSpritePolish/butler_sprite_upscale.py \
  Tools/CharacterSpriteCleanup/butler_sprites.txt \
  /tmp/chateau-butler-clean \
  /tmp/chateau-butler-x2 \
  /home/hamzak/ai/models/MIDI-3D-texture-checkpoints/RealESRGAN_x2plus.pth
```

The script upscales RGB with RealESRGAN, alpha with Lanczos, and copies the exact
canonical lower 128 rows into every breathing-idle output so the planted feet stay
bit-identical.  Review all frames on both dark and real room backgrounds before
copying the output into the project.

When the 336x598 output replaces the existing PNGs, their importers must use 200
pixels per unit and Uncompressed texture format.  Doubling both pixel dimensions
and PPU preserves the exact world-space bounds and bottom-center registration.
