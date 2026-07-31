# Guest 4 Sitting Pivot and Y-Sort Design

## Goal

Make Countess Elowen Dusk (Guest 4) use ordinary actor world-Y sorting against
all other guests and the full green chair, while retaining the narrow seated
exception that keeps the chair's detached foreground cutout over her. Make
every sprite used by her sitting animation use a Bottom Center pivot so frame
changes cannot move her effective floor point.

No executable builds will be produced.

## Proven Scope

`CountessElowenDusk_Sitting.anim` references exactly two sprite assets:

- `guest4sit2.png` (`3c77696129c44fc49b56737e0ffdc4e9`)
- `guest4sit3.png` (`68e57e21c4bd49ebbc1faec9dc2d6830`)

Both texture importers currently use Center alignment. The working-tree change
on `guest4sit2.png.meta` also stores pivot `(0.5, 0.5)`, so neither frame is
reliably imported with the requested Bottom Center preset.

Guest 4's current Drawing Room path calls `ActivateBehindOccluder` and passes
Guest 2 as a preserved-behind actor. That seated exception repacks both actors'
renderer orders every LateUpdate, bypassing their normal world-Y relationship.

## Selected Design

1. Configure both sitting-frame importers with Unity's Bottom Center alignment
   and normalized pivot `(0.5, 0)`.
2. Change only GuestIndex `3` in `ApplyDrawingRoomSeatedOcclusion` to call
   `ActivateFrontOccluderOnly` with `drawingRoomGreenChairForegroundRenderer`.
3. Do not pass Guest 2 into Guest 4's exception. Guest 4 and every other actor
   will therefore keep their normal foot-based world-Y order.
4. Continue updating the detached chair foreground cutout immediately above
   Guest 4 while she is seated. The full green chair remains under its ordinary
   physical/pivot Y-sort owner.

The resulting ownership is:

```text
Guest 4 versus actors/full chair: WorldYSortSpriteRenderer
Detached green-chair foreground: DiningRoomSeatedGuestOcclusionException
```

## Scope Boundaries

- Do not change Guest 4's standing, walking, or panic sprites.
- Do not change any other guest's sprite pivots or scale.
- Do not add a fixed `SortingGroup` or fixed SpriteRenderer order to Guest 4.
- Do not change the tea table, chair positions, room anchors, or other rooms.
- Preserve the user's existing `Gameplay.unity` changes.
- Do not build executables.

## Test-First Verification

1. Add a regression that resolves every unique sprite GUID referenced by
   `CountessElowenDusk_Sitting.anim` and requires Bottom Center importer
   alignment and pivot `(0.5, 0)`. Confirm it fails on the current metadata.
2. Add or update a focused controller regression requiring Guest 4 to use the
   foreground-only path and forbidding the preserved-Guest-2 actor argument.
   Confirm it fails against the current controller.
3. Apply the two importer metadata changes and the narrow controller change.
4. Re-run the focused regressions, then the surrounding character-presentation,
   collision/occlusion, and sorting-ownership suites.
5. Inspect the final diff to verify that only the two sitting-frame metadata
   files, Guest 4's controller branch, and focused tests changed.

## Acceptance Criteria

- Both and only both sprites used by Guest 4's sitting clip import as Bottom
  Center with normalized pivot `(0.5, 0)`.
- Guest 4 is no longer assigned an actor-wide seated order or allowed to force
  Guest 2 behind her.
- Guest 4 follows normal world-Y sorting against other guests and the full
  chair.
- The detached green-chair foreground still renders immediately over Guest 4
  while she sits.
- Targeted regressions pass with no new Unity errors, and no executable is
  created.
