# Character Animation Display and Scale

The Butler and all eight guests use one presentation architecture. Their actor root remains at unit scale and owns movement, physics, room state, authored placement, and interaction. A dedicated child named `AnimationDisplay` owns the body `SpriteRenderer`, `Animator`, and the only runtime character-size change.

The legacy migration reference and cleanup audit live in `Docs/Migration/LegacyCharacterScaleSnapshot.json` and `Docs/CharacterPresentationLegacyRemovalAudit.md`. Runtime code must never load the migration snapshot.

## Recognizable hierarchy

```text
Rooms                                CharacterScaleCatalog
└── Room_<Name>                      RoomContentGroup + CharacterScaleRoom
    └── Character Scale
        ├── Front                    manual room-local X/Y + uniform scale
        └── Back                     manual room-local X/Y + uniform scale

Butler or Guest                      movement / physics / placement root (scale 1)
└── AnimationDisplay                 Animator + SpriteRenderer + visual scale
```

Open `Dreadforge > Characters > Character Scale Tool` to choose a room and edit its Front and Back objects. Position is authored in that room's X/Y space; uniform object scale is the desired character display size at that endpoint. X makes the guides easy to place in the painted room, while the shared runtime function intentionally reads Y only. Values between Front and Back are linearly interpolated and values beyond them are clamped.

`CharacterScaleCatalog` is the lookup for every authoritative room. `CharacterScaleRoom` converts a world-space actor foot/root position into the selected room's local Y and evaluates `CharacterScaleFunction`. It also converts the room stage's current local zoom relative to its authored reference. Canvas resolution is not a scale input. `CharacterAnimationDisplay` applies that result only to `AnimationDisplay`; it never moves or scales the actor root.

There are no guest multipliers, per-character fine tunes, perspective profiles, tint/opacity changes, shadow scaling, sprite-bounds compensation, or sitting scale overrides. The Butler and every guest receive the same result for the same room and Y. Drawing Room and Dining Room forced sitting remain Animator/state overrides only.

## Ownership boundaries

- `PointClickPlayerMovement` owns Butler input, click routing, logical/world coordinate conversion, locomotion, facing, animation parameters, footsteps, and player sorting. It does not own body size.
- `RoomPersonWalker2D` owns its ambient room person's authored local path, bob/sway, facing, and Animator parameters. Its size is a fixed authored value; it has no perspective, tint, or shadow behavior.
- `ActorRoomState` owns actor identity, current room, chapter visibility, interactability, seated state, authored `PlaceAt` behavior, and position-only room-anchor following.
- `NPCWaypointMover` owns direct scripted movement. It releases passive anchor following before taking movement ownership.
- `CameraManager` owns room-stage pan and zoom. Only `CharacterScaleRoom` converts that room zoom into display size.
- `WorldYSortSpriteRenderer` and `DiningRoomSeatedGuestOcclusionException` own sorting only. The Dining Room exception places a seated guest between assigned chair and table layers without changing position, scale, or appearance.
- Chapter controllers own story placement, room transitions, hiding/finding, panic routes, animation state, and seat assignment. They do not resize a body.

Coats, speech bubbles, click targets, prompts, UI, room backgrounds, props, and VFX are not character bodies. Any scale write for those objects must remain narrowly targeted and documented by the ownership regression guard.

## Character art and animation

Current Chapter 1 sources live under `Assets/Art/Characters/guest1` through `guest8` and `Assets/Art/Characters/butler`. Keep each `.png.meta` beside its texture so animation sprite GUIDs remain stable. Normalize frames with consistent pixels-per-unit, canvas dimensions, and bottom-center foot pivots. Intentional differences in character height belong in art/import data; animation clips must animate sprites and state only and must keep transform-scale curves empty.

Directional animation uses the shared `Speed`, `IsWalkingUp`, `IsWalkingDown`, `IsWalkingLeft`, and `IsWalkingRight` Animator parameters. `ButlerClassic` also uses persistent `IsFacingUp`, `IsFacingDown`, `IsFacingLeft`, and `IsFacingRight` parameters for directional idle clips.

Use `Dreadforge > Characters > Rebuild Character Animation Assets` after changing legacy source folders, or the character-specific rebuild command where one exists. Fix timing, direction, bad frames, pivots, and import normalization in source/import/animation assets rather than compensating in movement or story code.

## Placement and sorting authoring

- Place story guests at authored room, waiting, hide, door, and seat anchors. Preserve the Drawing Room standing/seated mapping and Dining Room `SetSeated(true)` flow.
- Edit `RoomPersonWalker2D` path points to change an ambient person's route. Keep `Preview Path In Edit Mode` off during ordinary placement.
- Add `WorldYSortSpriteRenderer` to world-space props that must sort against the Butler by base/pivot Y.
- Add `YSortSolidObstacle2D` only when a prop needs an editable physical-base footprint for sorting or occlusion safety. Player navigation remains controlled by the active `PlayerBoundary` collider.
- Do not add a scale exception for a character, animation, seated/standing state, panic state, or room. Change the room's Front/Back definition or the shared function instead.
