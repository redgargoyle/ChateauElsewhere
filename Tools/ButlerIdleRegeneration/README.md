# Butler idle regeneration

This tool rebuilds the playable Butler's twelve-frame breathing loop from the
canonical planted standing pose:

`Assets/Art/Characters/butler/butler_classic_walk_01_r01_c01.png`

The old idle frames scaled and translated the entire sprite. That made both
shoes slide horizontally and changed their silhouettes even though the Unity
sprite pivot was already correct.

The regenerated loop keeps the existing five-pose cadence:

`neutral, inhale-1, inhale-2, inhale-2, inhale-2, inhale-1, neutral,
exhale-1, exhale-2, exhale-2, exhale-2, exhale-1`

Breathing is rendered as a small vertical deformation above source row 235.
Rows 235 through 298 are copied byte-for-byte from the canonical pose in every
frame, so the lower legs and shoes cannot slide, stretch, or repaint. The
deformation uses premultiplied-alpha interpolation to avoid introducing bright
fringes around the sprite.

Build:

```bash
gcc -std=c11 -D_POSIX_C_SOURCE=200809L -Wall -Wextra -Werror -O2 \
  Tools/ButlerIdleRegeneration/butler_idle_regenerate.c \
  -o /tmp/chateau-butler-idle-regenerate \
  $(pkg-config --cflags --libs MagickWand) -lm
```

Generate a review copy first:

```bash
mkdir -p /tmp/chateau-butler-idle-review
/tmp/chateau-butler-idle-regenerate \
  Assets/Art/Characters/butler/butler_classic_walk_01_r01_c01.png \
  /tmp/chateau-butler-idle-review
```

After visual and pixel validation, regenerate the live frames in place:

```bash
/tmp/chateau-butler-idle-regenerate \
  Assets/Art/Characters/butler/butler_classic_walk_01_r01_c01.png \
  Assets/Art/Characters/butler/butler_idle
```

The tool changes PNG pixels only. Existing filenames, `.meta` files, GUIDs,
sprite pivots, animation clips, controllers, and timing remain unchanged.
