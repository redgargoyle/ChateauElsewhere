# Generated Sprite Library

This folder is a growing asset database for generated guest sprites and animation references.
Assets here are intentionally not wired into gameplay yet.

Branch purpose:
- Preserve generated character animation/image options in the repo.
- Keep experimental and unused sprites available for later selection.
- Build organized folders by guest and action/expression category.

Generation rules:
- Generate complete illustrated sprites intentionally; do not draw primitive lines,
  circles, stick arms, or simple shape overlays onto existing character art.
- Preserve the current game art style: slight pixelation, watercolor-painted texture,
  muted Victorian palette, inked sprite edges, and the same top-down-ish/orthographic
  game perspective used by the existing character and room art.
- Avoid style drift into smooth cartoon, vector, anime, glossy mobile-game, or overly
  clean digital illustration finishes.
- Keep transparent PNG outputs with clean alpha edges and no white/checkered/chroma
  background in the final sprite files.
- Keep room/furniture perspective close to the current game art, especially dining
  chairs, drawing room couches, and character scale/angle.
- Store raw generated contact sheets in each guest's `_ContactSheets` folder.
- Store usable cutout sprites in category folders such as `Idle`, `Surprised`,
  `Panic`, `Sitting`, `DiningRoomChair`, `DrawingRoomCouch`, and `Walking`.

Importer notes:
- PNG `.meta` files use Unity Sprite import settings.
- Sprite pivot is bottom-center to make character swapping easier later.
