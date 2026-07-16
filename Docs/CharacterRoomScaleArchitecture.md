# Character Room Scale Architecture

## Scope

This module changes one thing: the displayed body size of the controllable Butler and scene Guests as a function of room and room-local foot Y.

It must not change movement, position, rotation, room placement, pathfinding, collision, animation selection or timing, sorting, tint, visibility, interaction, dialogue, chapter state, camera behavior, or room-stage behavior.

## Components

### `CharacterRoomScaleCatalog`

Scene-owned room data. Each room entry contains shared front/back foot-Y endpoints, Butler front/back final local size, Guest front/back final local size, one normalized interpolation curve, and an optional reference room-stage scale.

The catalog stores final displayed `localScale.y` magnitudes. Values are not hidden multipliers over some second room-scale model.

### `CharacterRoomScaleTarget`

An opt-in marker on one Butler/Guest display. It resolves:

- the body `ScaleRoot`
- Butler or Guest profile
- actual room context
- room-local visible foot point
- optional per-character `DisplaySizeMultiplier`
- authored X:Y aspect ratio and current facing sign

It contains no room curve or endpoint data.

### `CharacterRoomScaleController`

Late-frame size authority. It reads the catalog and targets, computes final size, then applies only the target scale root's local scale. It does not move or otherwise control a character.

### `CharacterRoomStageScaleUtility`

Pure calculation/helper code for existing room-stage zoom. It never writes a transform.

### `CharacterRoomScaleCatalogWindow`

Editor workflow at `Tools > Characters > Character Room Scale Catalog`. The preview calls the same runtime controller path; it does not duplicate the scale formula.

## Data flow

```text
Existing movement / projection / actor state
                 |
                 | room id + room-local foot point
                 v
      CharacterRoomScaleTarget
                 |
                 v
      CharacterRoomScaleCatalog
       profile endpoints + curve
                 |
                 v
     CharacterRoomScaleController
       stage zoom compensation
                 |
                 v
 ScaleRoot.localScale (size only)
```

## Room-resolution priority

Runtime room resolution prefers actual placement over authored fallback data:

1. parent `RoomContentGroup`
2. active `RoomProjectedEntity` current visual-scale room
3. `RoomProjectedEntity` room profile
4. `RoomPersonWalker2D` room profile
5. `ActorRoomState.CurrentRoomId`
6. active `RoomNavigationManager` room for an active visible character
7. target `currentRoomId`
8. target `roomIdOverride`
9. authored Guest-number fallback used by the existing Chapter 1 setup

The editor can explicitly request a selected room for calibration preview without changing runtime room ownership.

## Scale formula

For room-local foot Y `y`:

```text
depth01 = clamp01(inverseLerp(frontFootY, backFootY, y))
curvedDepth = clamp01(scaleFunction(depth01))
catalogSize = lerp(profileFrontSize, profileBackSize, curvedDepth)
calibratedSize = catalogSize * targetDisplaySizeMultiplier
finalLocalSizeY = calibratedSize * currentStageZoomRatio / inheritedStageZoomRatio
```

The target applies `finalLocalSizeY` while preserving:

- the captured authored absolute X:Y ratio
- the current X facing sign
- the current Y sign
- the current Z scale

## Room-stage compensation

Catalog endpoints are saved against a reference room-stage scale. The current/reference ratio keeps characters following the existing stage zoom. A character already parented under the matching active room stage inherits that zoom, so the same ratio is divided back out to prevent double-scaling. A stale, inactive, or different room stage is not treated as inherited active zoom.

## Fenced legacy writers

The following existing systems retain all of their non-size responsibilities, but skip their old scale write when `CharacterRoomScaleTarget` owns the affected transform:

- `PointClickPlayerMovement`
- `RoomProjectedEntity`
- `RoomPersonWalker2D`
- `ActorRoomState`
- Chapter 1 Guest setup/restoration
- Chapter 2 Guest panic sprite-size normalization/restoration

This is deliberately transform-specific. A room-stage ancestor is not considered character-owned, and unmanaged props/NPCs keep their previous scale behavior.

`CharacterController2D` may still mirror the character by changing the X sign. The character scale target preserves that live sign while controlling the room-dependent size magnitude.

## Scale-root rules

Use the animated human body root, normally the root that already receives the animation display scale. The target rejects common non-body names such as coat, jacket, cloak, shawl, speech/thought bubble, prompt, highlight, icon, shadow, cursor, and tooltip.

Animation assets should share a stable foot baseline/pivot. Fix frame alignment in the asset pipeline rather than adding room- or animation-specific position/size exceptions.

## Migration performed

Removed:

- `ButlerRoomScaleCalibrationWindow`
- `GuestRoomScaleMasterWindow`
- `GuestScaleAudit`
- `GuestRoomScaleCalibration`
- `GuestRoomScaleApplier`
- `GuestScaleParticipant`
- `GuestRoomStageScaleUtility`
- their dedicated obsolete regression suite

Added:

- `CharacterRoomScaleCatalog`
- `CharacterRoomScaleController`
- `CharacterRoomScaleTarget`
- `CharacterRoomStageScaleUtility`
- `CharacterRoomScaleCatalogWindow`
- focused character room-scale regression coverage

The replacement runtime scripts and focused editor assets preserve the corresponding old `.meta` GUIDs where serialized references or test-asset continuity mattered.

## Gameplay scene setup

`Assets/Scenes/Gameplay.unity` contains:

- one `CharacterRoomScaleCatalog`
- one `CharacterRoomScaleController`
- nineteen migrated room entries
- one Player target using the Butler profile
- eight Guest targets using the Guest profile

The Player target is added only to the Player scene instance, not `Assets/Prefabs/Player.prefab`, because the same prefab is also used by the eight Guest instances.

## Invariants for future changes

1. Room-dependent Butler/Guest size data belongs only in the catalog.
2. A managed target must have only one final room-size authority.
3. No size rule may branch on chapter, hiding state, seated state, animation, or Guest identity.
4. Preview and runtime must use the same computation path.
5. New scale writes must check `CharacterRoomScaleTarget.OwnsScaleFor(...)` before touching a managed character transform.
6. Do not put the target on the shared Player prefab while Guests use that prefab.
7. Preserve unrelated behavior for objects that do not opt into this module.
