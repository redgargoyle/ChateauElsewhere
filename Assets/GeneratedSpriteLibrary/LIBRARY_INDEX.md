# Sprite Library Index

Branch: `asset-library/generated-sprite-database`

Current main cutout sprites:
- Guests: 10
- Main sprite PNGs: 214
- Base main sprites per guest: 16
- Focused room-perspective additions: 6 extra sprites each for guests 1-4
- Focused panic-reaction additions: 6 extra sprites each for guests 1-4
- Baron Hector expanded additions: 6 extra sprite candidates
- Alpha verification: transparent corners passed for all main sprites

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
- `Sitting`: 2 sprites
- `DiningRoomChair`: 2 sprites, plus room-perspective overlay variants for focused guests
- `DrawingRoomCouch`: 2 sprites, plus room-perspective overlay variants for focused guests
- `Walking`: 4 sprites

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
- `BaronHectorGlass`: 22
- `ButlerGuest`: 16
- `CountessElowenDusk`: 28
- `Lady`: 16
- `LadySabineMarrow`: 28
- `LordAmbroseVeil`: 28
- `MadameCoralieThread`: 16
- `MissIsoldeWren`: 16
- `MisterFlorianKnell`: 28
- `ProfessorLucienVale`: 16

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
