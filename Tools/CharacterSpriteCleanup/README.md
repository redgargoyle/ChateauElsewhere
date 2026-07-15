# Character sprite white-matte cleanup

The runtime Butler and guest sprites contain two source-art defects:

- a bright neutral matte baked into the outer one- or two-pixel boundary;
- a few opaque white remnants inside genuine gaps, such as the Butler's arm/torso gap.

Unity's importer and sprite shader cannot remove those pixels without also removing
real white clothing, gloves, eyes, and highlights. This tool therefore makes only
deterministic, reviewable pixel changes. It never crops, resizes, renames, or edits
Unity `.meta` files.

## Safety model

- Boundary cleanup reverses white-matte contamination in translucent RGB while
  preserving alpha and silhouette geometry.
- Internal alpha cuts require an exact path, expected dimensions, and reviewed ROI in
  `reviewed_internal_masks.csv`.
- A reviewed ROI must contain a strict near-white seed. The run fails if the seed is
  absent, dimensions differ, or a flood exceeds 350 pixels.
- `alpha-all` rules are reserved for reviewed negative-space gaps. They start only
  from bright islands or pixels touching existing transparency, then follow neutral
  matte inside the hard ROI.
- `runtime_character_sprites.txt` contains the non-Butler PNGs reached by the
  Gameplay actor controllers or `PanicAnimationLibrary` at the time of this cleanup.
- `butler_sprites.txt` is the complete playable and compatibility Butler set used by
  the second-pass polish workflow.  Its 168x299 cleanup step is reproducible from
  the pre-polish commit; the checked-in 336x598 output must not be upscaled again.

## Build and dry-run

```bash
gcc -std=c11 -D_POSIX_C_SOURCE=200809L -Wall -Wextra -Werror -O2 \
  Tools/CharacterSpriteCleanup/character_sprite_cleanup.c \
  -o /tmp/chateau-character-sprite-cleanup \
  $(pkg-config --cflags --libs MagickWand) -lm

rm -rf /tmp/chateau-character-sprite-cleanup-output
/tmp/chateau-character-sprite-cleanup \
  Tools/CharacterSpriteCleanup/runtime_character_sprites.txt \
  /tmp/chateau-character-sprite-cleanup-output \
  Tools/CharacterSpriteCleanup/reviewed_internal_masks.csv
```

Use `-` as the output root only after reviewing the dry-run contact sheets and
verifying dimensions, alpha changes, and serialized-reference integrity.
