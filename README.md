# ChataeuChatilly Attached Guest Sprite Animations

This package was generated directly from the eight high-quality guest sprite sheets attached by the user.

It deliberately does not use procedurally assembled primitive shapes. Each frame is extracted from the finished sprite-sheet art, cleaned from the checkerboard background, normalized to the existing ButlerYoung aligned canvas, and exported into the project's Unity character layout.

## Project conventions matched

- Character frames: `Assets/Characters/<CharacterName>/walk/aligned`
- Walk row order: row 1 down/toward camera, row 2 left, row 3 right, row 4 up/away
- Shared aligned canvas: 166x297
- Pivot: bottom center `(0.5, 0.0)`
- Frame rate: 12 fps
- Animated properties: both Unity UI `Image.m_Sprite` and `SpriteRenderer.m_Sprite`
- Animator override controllers point at `Assets/Animation/Player/Player.controller`

## Guests and pairs

1. Lord Ambrose Veil + Lady Sabine Marrow
2. Mister Florian Knell + Countess Elowen Dusk
3. Professor Lucien Vale + Madame Coralie Thread
4. Baron Hector Glass + Miss Isolde Wren

## Contents per guest

- 4 walking directions: down, left, right, up
- All walk frames present in the source sheets. Most sheets have 8 per direction; Mister Florian Knell has 7 per direction because his provided source sheet has 7 walk poses per row.
- 4 idle directions: down, left, right, up
- Coat/cloak prop extracted from the lower-right source-sheet prop when present
- `.png.meta` files for Unity import with bottom-center sprite pivot
- `.anim` clips and `.overrideController` for immediate use

The per-character metadata files preserve source-sheet and extraction details.
