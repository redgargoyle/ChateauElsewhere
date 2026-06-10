# Sprite Library Index

Branch: `asset-library/generated-sprite-database`

Current main cutout sprites:
- Guests: 10
- Main sprite PNGs: 736
- Base main sprites per guest: 16
- Focused room-perspective additions: 6 extra sprites each for guests 1-4
- Focused panic-reaction additions: 6 extra sprites each for guests 1-4
- Merged expanded/hurried additions: Baron Hector +4 hurried walking, Lord Ambrose +10,
  Lady Sabine +10, Madame Coralie +10
- Original-style additions from parallel generation: Baron Hector +10, Lady Sabine +10,
  Lord Ambrose +10, Madame Coralie +10
- Transition additions: 4 stand-to-sit frames for each guest
- Shake/sweat additions: 12 shaking frames and 12 sweating frames for each guest
- Nested review additions: ButlerGuest +8 original-style panic candidates in
  `Panic/OriginalStyleGuest2_20260610`
- Guest 1 reference panic additions: 8 `180x290` Lady panic/scream/run frames
  generated from `Assets/Art/Characters/guest1` reference sprites
- Reference-locked panic additions: 8 `166x297` panic/reaction/run frames each
  for ButlerGuest, MisterFlorianKnell, CountessElowenDusk, BaronHectorGlass,
  LadySabineMarrow, LordAmbroseVeil, MadameCoralieThread, MissIsoldeWren, and
  ProfessorLucienVale
- Alpha verification: transparent corners passed for all main sprites

Style-matched mirror:
- `Assets/Art/Library/GeneratedSprites/StyleMatched` contains non-destructive filtered copies
  of 648 style-matched sprite PNGs, including the new Guest 1 reference panic set
  and the new reference-locked panic sets for the other guests.
- The filter uses `Assets/Art/Final Images (DO NOT EDIT)/drawing room 2.png` as the
  primary read-only style target, with original `Assets/Art/Library/AnimationLibrary/*/reference/full_body`
  sprites kept as additional read-only context.
- The current pass uses stronger ochre/olive watercolor glazing, room-derived paper
  texture, sketch-line texture, and sprite-scale pixel roughness.
- The original generated sprites and original animation/reference sprites are not edited.

Guest folders:
- `BaronHectorGlass`
- `ButlerGuest`
- `CountessElowenDusk`
- `Lady`
- `LadySabineMarrow`
- `LordAmbroseVeil`
- `MadameCoralieThread`
- `MissIsoldeWren`
- `MisterFlorianKnell`
- `ProfessorLucienVale`

Category coverage per guest:
- `Idle`: 2 sprites
- `Surprised`: 2 sprites
- `Panic`: 2 sprites
- `Panic/OriginalStyleGuest2_20260610`: 8 guest2 panic review sprites, scaled and
  post-processed toward the original guest2 pixel-watercolor style
- `Sitting`: 2 sprites
- `DiningRoomChair`: 2 sprites, plus room-perspective overlay variants for focused guests
- `DrawingRoomCouch`: 2 sprites, plus room-perspective overlay variants for focused guests
- `Walking`: 4 sprites
- `Transitions`: 4 stand-to-sit sprites
- `Shaking`: 6 standing panic-shake sprites and 6 seated panic-shake sprites
- `Sweating`: 6 standing sweaty-panic sprites and 6 seated sweaty-panic sprites

Focused guest 1-4 room-perspective pass:
- `LordAmbroseVeil`: couch idle/startled/panic and dining idle/startled/panic
- `LadySabineMarrow`: couch idle/startled/panic and dining idle/startled/panic
- `CountessElowenDusk`: couch idle/startled/panic and dining idle/startled/panic
- `MisterFlorianKnell`: couch idle/startled/panic and dining idle/startled/panic

Focused guest 1-4 panic-reaction pass:
- `LordAmbroseVeil`: anxious idle, hand-to-mouth shock, scream recoil, shaken sitting,
  dining-chair braced panic, and couch face-covered panic
- `LadySabineMarrow`: anxious idle, hand-to-mouth shock, scream recoil, shaken sitting,
  dining-chair braced panic, and couch face-covered panic
- `CountessElowenDusk`: anxious idle, hand-to-mouth shock, scream recoil, shaken sitting,
  dining-chair braced panic, and couch face-covered panic
- `MisterFlorianKnell`: anxious idle, hand-to-mouth shock, scream recoil, shaken sitting,
  dining-chair braced panic, and couch face-covered panic

Current main sprite counts:
- `BaronHectorGlass`: 94
- `ButlerGuest`: 52
- `CountessElowenDusk`: 64
- `Lady`: 52
- `LadySabineMarrow`: 106
- `LordAmbroseVeil`: 106
- `MadameCoralieThread`: 94
- `MissIsoldeWren`: 52
- `MisterFlorianKnell`: 64
- `ProfessorLucienVale`: 52

Guest 1 reference panic pass:
- `Lady/Panic/guest1_reference_panic_180x290/` contains 8 panic, scream,
  cower, and run frames based on the original guest1 Lady walking, idle, and
  sitting sprites.
- These frames are cut out with alpha, resized to the original `180x290`
  standing/walking canvas, and palette-matched to the black and mauve-purple
  dress from `Assets/Art/Characters/guest1`.
- The mirrored style-library copy lives at
  `Assets/Art/Library/GeneratedSprites/StyleMatched/Lady/Panic/guest1_reference_panic_180x290/`.

Reference-locked panic pass:
- Each non-Lady guest listed above now has
  `Panic/reference_locked_panic_166x297/` with 8 adult-scale panic frames:
  startled recoil, hands near face, cover-face cower, braced defensive, run left,
  run right, turnaround, and run start.
- These are candidate library cutouts generated from normalized adult-scale project
  references, then alpha-cleaned, palette-matched to each guest's existing sprites,
  given a drawing-room watercolor/pencil texture pass, and validated as `166x297`
  transparent PNG sprites.
- The combined review sheet is
  `_reference_locked_panic_166x297_all_guests_preview.png`.

Shake/sweat pass rules:
- `Shaking/*_shaking_standing_panic_01..06.png` and
  `Shaking/*_shaking_seated_panic_01..06.png` are whole-sprite tremble
  variants generated from existing panic and sitting cutouts.
- `Sweating/*_sweating_standing_panic_01..06.png` and
  `Sweating/*_sweating_seated_panic_01..06.png` add small watercolor-style
  perspiration glints while preserving the original character cutout.
- These frames do not use drawn-on replacement arms, hands, faces, or body
  construction overlays.

Guest2 original-style panic review rules:
- `ButlerGuest/Panic/OriginalStyleGuest2_20260610/*_originalstyle_*.png`
  are smaller review sprites built to match the original `Assets/Art/Characters/guest2`
  scale and darker hand-painted/pixel-watercolor look.
- The raw green-background generation was not placed in the library. The library files
  are alpha-cleaned, palette-reduced, dark-rimmed, and checked for transparent corners
  and white edge halos.
- This set is deliberately nested for critique before promoting any frame into the
  flat `Panic` category.

Transition pass rules:
- `Transitions/*_transition_stand_to_sit_01..04.png` are non-destructive
  whole-sprite in-between cutouts made from existing generated idle and sitting
  sprites.
- These frames do not use drawn-on replacement arms, hands, faces, or motion
  overlays; they are library candidates for review and optional polishing.

Room-perspective pass rules:
- Keep furniture aligned to existing object perspective: the purple drawing-room couch
  should remain low, wide, tufted, and slightly top-down; the dining-table/chair variants
  should match the burgundy/gold chair and lavender tablecloth perspective.
- Keep each row's lower-body/furniture placement stable, with expression and upper-body
  changes doing most of the animation work.

Raw generated sheets:
- Each guest has `_ContactSheets` for original chroma sheets, alpha sheets, and per-sheet manifests.

Review notes:
- Treat this as a candidate asset database. Sprites can be reviewed, critiqued, kept,
  replaced, or polished later without losing previous generations.
- New generations should prioritize the existing Chateau visual style: slight pixelation,
  watercolor paint texture, muted colors, inked sprite edges, and matching game-room
  perspective.
