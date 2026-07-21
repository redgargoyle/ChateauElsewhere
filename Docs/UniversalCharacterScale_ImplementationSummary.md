# Universal character display-scale implementation

## What changed

A single runtime pipeline now sizes the Butler and Guests:

1. `CharacterDisplayScaleSubject` identifies `Butler` or `Guest1`–`Guest8`, points to the dedicated animation child, and reads room/floor/seated context.
2. `CharacterDisplayScaleCatalog` selects a room default or an explicitly enabled character override and evaluates the room's Front/Back Y curve.
3. Drawing Room seated and Dining Room seated are the only state-specific alternatives.
4. `CharacterDisplayScaleController` applies the resulting absolute scale. `CharacterDisplayScaleBootstrap` creates one persistent controller when managed subjects load.

No position, pathfinding, room assignment, animation selection, story state, sorting order, or actor root transform is controlled by this pipeline.

## Created

- `Assets/Scripts/Characters/DisplayScale/CharacterDisplayScaleTypes.cs`
- `Assets/Scripts/Characters/DisplayScale/ICharacterDisplayScaleContext.cs`
- `Assets/Scripts/Characters/DisplayScale/CharacterDisplayScaleCatalog.cs`
- `Assets/Scripts/Characters/DisplayScale/CharacterDisplayScaleSubject.cs`
- `Assets/Scripts/Characters/DisplayScale/CharacterDisplayScaleController.cs`
- `Assets/Scripts/Characters/DisplayScale/CharacterDisplayScaleBootstrap.cs`
- `Assets/Editor/Characters/DisplayScale/CharacterDisplayScaleAuthoringWindow.cs`
- `Assets/Editor/Characters/DisplayScale/CharacterDisplayScaleArchitectureTests.cs`
- `Assets/_Chateau/Data/Resources/CharacterDisplayScaleCatalog.asset`

The legacy interim stack and asset listed in `UniversalCharacterScale_LegacyRemovalAudit.md` were deleted.

## Authoring workflow

Open `Tools/Chateau/Universal Character Display Scale`.

- Select, locate, or create the catalog, then choose a room.
- Edit the room default, or switch to an individual character override.
- Capture Front/Back positions and scales from the selected managed character, or copy the room default before tuning one character.
- Preview Front, Middle, Back, current room-local foot Y, or slider depth. Preview changes are temporary and restored when the target/mode/selection changes, the window closes, scripts reload, play mode changes, Undo occurs, or an error interrupts preview.
- Drawing Room exposes only its seated value; Dining Room exposes only its seated/eating value.
- Resolve validation warnings, then save the asset explicitly.

## Migration and tuning

The canonical asset was seeded with all 19 room endpoint values from the previous authored catalog. Curves are linear, individual character overrides are empty, and seated overrides begin disabled. This intentionally prevents hidden per-character multipliers.

Manual visual review is still recommended for all 19 rooms, especially the Drawing Room seated poses and Dining Room seated/eating poses. Enable those two state values only after their final visual magnitudes are approved. No runtime code or scene handles need to change for later tuning.
