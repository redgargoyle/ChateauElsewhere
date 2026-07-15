# Remove Old Grand Entrance Hall Tick Sound Design

## Goal

Remove the older basic clock tick from the Grand Entrance Hall while retaining the supplied Victorian/antique tick as the sole continuous clock ambience in that room.

## Confirmed Ownership

- `ClockTickingAmbienceController` remains the canonical, room-aware owner of continuous clock ambience.
- `ClockTickingAmbienceCatalog` remains the source of the Grand Entrance Hall assignment to `12_distant_hall_clock_ticks_tangoflux_seed1221164_48khz.wav`.
- `RoomNavigationManager` remains responsible for creating and updating that shared ambience owner as rooms change.
- Chapter 2's non-looping clock-strike one-shot remains independent and unchanged.

## Root Cause

`Chapter1ArrivalController` currently creates `GrandfatherClockInteraction` at runtime on the Grand Entrance Hall's authored `GrandfatherClock` prop. That legacy component creates the procedural `RuntimeGrandfatherClockTicking` clip and loops it through a second Atmosphere audio source. It therefore overlaps the catalog-owned antique tick whenever the hall is active.

The authored clock prop itself does not contain a serialized legacy audio component and must remain in the scene.

## Chosen Design

Retire the entire `GrandfatherClockInteraction` path rather than muting or swapping its clip:

- Delete the procedural interaction and its `.meta` file.
- Remove Chapter 1's serialized reference, runtime fallback creation, initialization call, and stale clock-close-up plumbing.
- Remove the obsolete Chapter 1 scene-action and HUD references that exist solely to open the retired interaction.
- Remove only the stale serialized fields from `Gameplay.unity`; keep the authored `GrandfatherClock` and `GrandfatherClock_Optional` scene objects intact.
- Preserve the room navigation, ambience catalog, imported antique clip, and Chapter 2 clock strike without modification.

## Alternatives Rejected

1. Muting the legacy source would stop the symptom but retain a hidden runtime audio owner that could be re-enabled or reconfigured later.
2. Replacing the synthetic clip with the antique clip would still create two concurrent loops and would duplicate catalog ownership.
3. Removing the authored clock prop would break the room's visual set dressing without addressing the runtime ownership problem cleanly.

## Regression Coverage

Replace the legacy-positive regression with a guard that proves:

- no runtime script can create or reference `GrandfatherClockInteraction` or `RuntimeGrandfatherClockTicking`;
- the legacy source file and meta file are absent;
- Grand Entrance Hall's imported antique clip remains assigned through the shared catalog/controller;
- the authored Grand Entrance Hall and Drawing Room clock props remain in scene/prefab data; and
- the Chapter 2 clock strike bindings remain present.

Run the focused `NavigationRegressionTests` EditMode suite using Unity after the open editor has released the project, then inspect the working tree and source references before committing the implementation.
