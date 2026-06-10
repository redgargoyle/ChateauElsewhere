# SpriteLibrary Browse Layer

This folder is the central place to browse current character sprite work without hunting through scattered source, generated, and runtime folders.

## Structure
- `Characters/<GuestID>_<Name>/` is the current primary browse lane for each game character.
- `Characters/<GuestID>_<Name>/PanicFrames/` contains the current primary panic review frames for that character.
- `ReviewSheets/` contains contact sheets for fast visual review.
- `Indexes/` contains CSV indexes for finding every PNG across known art locations.
- `Archive/MisfiledOrRetired/` preserves generated or browse-layer work that should not be in the current primary path.

## Current Guest 3 Rule
The real game `Guest03` is the colonel in `Assets/Art/Characters/guest3`. The previous `Guest03_MisterFlorianKnell` folder is preserved under `Archive/MisfiledOrRetired/` and is not the current Guest03 browse lane.

## What Is Not Moved
Runtime/source art remains in its original locations, including `Assets/Art/Characters`, `Assets/AnimationLibrary`, `Assets/GeneratedSpriteLibrary`, and `Assets/GeneratedSpriteLibraryStyleMatched`. This browse layer is non-destructive.

## Current Panic Rule
Use `Characters/<GuestID>_<Name>/PanicFrames/` for the current primary panic frame sets. Treat `_ContactSheets`, chroma sheets, and archived misfiled folders as process evidence or retired candidates, not final gameplay sprites.
