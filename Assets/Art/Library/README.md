# Chateau Elsewhere Art Library

This folder collects production, reference, generated, and archived art that is not part of the active `Assets/Art/Characters` runtime sprite tree.

- `AnimationLibrary/` contains reviewed character reference, intake, approved, request, QA, and clip-building assets.
- `GeneratedSprites/Raw/` contains raw/generated sprite output.
- `GeneratedSprites/StyleMatched/` contains style-matched generated sprite output.
- `LegacyCharacters/` contains older prototype character source folders used by the legacy character animation builder.
- `LegacyPlayer/` contains the old player sprite/clip bundle formerly kept as a loose root asset folder.
- `SourceSheets/` contains original character source sheets.
- `Previews/` contains contact sheets, GIFs, and QC preview images.
- `ExternalArchive/` contains copied external Desktop/generated folders so useful art is not stranded outside the project.
- `Loose/` contains one-off loose art assets that did not belong to a stronger category.

Active Chapter 1/2 character sprites remain in `Assets/Art/Characters`. Runtime coat sprites are under `Assets/Art/Resources/Chapter1/GuestCoats` so Unity keeps the same `Resources.Load("Chapter1/GuestCoats/...")` paths.
