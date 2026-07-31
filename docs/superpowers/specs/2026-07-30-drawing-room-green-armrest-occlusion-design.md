# Drawing Room Green Armrest Occlusion Design

## Goal

Make the selected `greenchairarmrest_0` render in front of the seated guest
whose painted pixels overlap it, without changing ordinary room Y occlusion,
the Drawing Room tea table, or any unrelated guest.

No executables will be produced for this change.

## Proven Context

The selected cutout is the right-side green armrest:

- Scene renderer: `greenchairarmrest_0`, file ID `362573330`
- Physical Y-sort owner: `PlayerBlocker_greenchairarmrest_0`
- Overlapping seat: `DrawingRoomGuestPoint_08`
- Runtime actor: `guest_8` / Madame Coralie Thread, zero-based guest index `7`

The scene object named `Guest 1` is staged at
`DrawingRoomGuestPoint_01`, roughly 530 room units away, and cannot overlap
this selected cutout. The visual target is therefore identified by the
selected renderer and painted overlap rather than by the informal guest
number.

The current mapping reaches the correct actor, but uses
`ActivateFrontOccluderOnly`. That path reads one raw guest renderer and writes
the armrest to that renderer's order plus one. It has two weaknesses:

1. It does not normalize all active guest renderers or disable a nested
   `SortingGroup`, so a guest-local effective override can still submit in
   front.
2. It takes the armrest order away from its physical-footprint Y sorter,
   creating an unnecessary risk to normal prop and tea-table relationships.

The existing all-guest regression passes because it compares raw renderer
orders after manually applying the exception. The previous camera PNG staged
only the left yellow-dress guest, so neither check proved the selected
armrest's final rendered pixels.

## Selected Design

Guest index `7` will use the existing actor-behind-occluder path with
`greenchairarmrest_0` as its occlusion ceiling:

```text
physical Y sorter -> greenchairarmrest_0 order
seated exception  -> every active guest_8 renderer immediately below armrest
```

The seated exception will:

- capture Guest 8's active renderer and local `SortingGroup` state;
- disable local sorting groups while the exception is active;
- preserve the guest renderers' internal back-to-front order;
- place the guest renderers immediately below the blocker-owned armrest;
- leave the armrest renderer itself unchanged;
- restore the captured state and immediately reapply ordinary actor Y sorting
  when the guest is no longer eligible for the seated exception.

The controller change will be limited to GuestIndex `7`. Guest 4's existing
full-chair exception, Guest 2's preserved order, standing guests, other rooms,
and the tea-table sorter will not be modified.

## Rendered-Pixel Verification

The live Chapter 2 skip regression will stage all eight real guests in the
Gameplay scene and resolve the selected armrest, Guest 8, the armrest blocker,
the tea table, and the table blocker explicitly.

A test-only camera helper will render three isolated variants synchronously in
the same Unity frame:

1. the actual final sorting state;
2. the armrest forced behind the actor;
3. the armrest forced in front of the actor.

The helper will:

- isolate only the armrest and active actor SpriteRenderers on a temporary
  camera layer;
- preserve and restore every changed layer, camera, renderer, and sorting
  value in `finally`;
- derive the region of interest from viewport-projected renderer bounds rather
  than hard-coded screen coordinates;
- create an overlap mask from pixels where the forced-front and forced-behind
  images differ, excluding transparent sprite padding;
- require the actual overlap pixels to match the forced-front variant;
- save actual, forced-front, forced-behind, and full Drawing Room PNGs under
  `/tmp/chantilly-*` for direct visual inspection.

The regression will also prove that:

- the selected armrest order still equals its blocker-owned order after the
  seated exception runs;
- every effective Guest 8 renderer is below the armrest;
- the tea-table renderer order is unchanged by the exception and still equals
  its own blocker-owned order;
- the existing Guest 2 → Guest 4 → full green chair order remains intact.

## Test-First Sequence

1. Add the explicit Guest 8/armrest ownership and rendered-pixel regression.
2. Run it against the current implementation and confirm it fails because the
   front-only exception rewrites the blocker-owned armrest or loses the
   forced-front pixel comparison under a guest-local override.
3. Change only GuestIndex `7` to the actor-behind-occluder path.
4. Re-run the focused regression and inspect the generated PNG.
5. Run the surrounding controller, sorting-ownership, and collision-box suites.

## Rejected Alternatives

- **Give the armrest a permanently huge sort order.** This would bypass normal
  Y occlusion and could put it over the tea table or moving actors.
- **Keep making the armrest follow a raw guest renderer.** This retains the
  effective `SortingGroup` blind spot and competing-writer problem.
- **Edit the armrest sprite or position first.** Scene coordinates, sprite
  alpha, and seat mapping already overlap correctly; rendered A/B evidence
  should exonerate sorting before any art change is considered.
- **Sweep every Drawing Room prop or guest.** The rest of the room already
  works, and a broad rewrite would add avoidable regression risk.

## Acceptance Criteria

- In the real all-guest Drawing Room frame, the selected green armrest's
  painted overlap renders in front of its seated occupant.
- The automated camera-pixel comparison identifies real overlapping pixels and
  reports the actual image as the forced-front result.
- The armrest and tea table remain owned by their respective physical Y
  sorters.
- Guest 4, Guest 2, all standing guests, and normal table occlusion retain
  their existing tested order.
- All touched tests pass, the generated PNG is visually inspected, and no
  executable build is created.
