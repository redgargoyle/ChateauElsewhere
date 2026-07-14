# Audio Mix Safety Design

## Goal

Keep walking, door-opening, and fireplace audio out of the muddy low-frequency range, then make the Chapter 2 panic screams dramatically quieter and band-limited so simultaneous voices cannot produce an extreme output spike.

## Existing ownership

- `GameAudioSettings` remains the shared runtime audio service.
- `PlayerFootstepAudio` and `GuestFootstepAudio` remain the walking-audio owners.
- `DoorOpenSoundCatalog` remains the source of door and stairway one-shots.
- `FireplaceAmbienceController` remains the room-aware fireplace loop owner.
- `Chapter2PanicScreamCatalog` and `Chapter2GuestPanicController` remain the panic-scream owners.

No second audio pipeline or replacement dialogue/audio service will be added.

## Mix contract

- Add one shared `GameAudioSettings` helper that creates and configures a high-pass and low-pass filter on an existing `AudioSource`.
- Route player footsteps, guest footsteps, fireplace ambience, door/stairway one-shots, and panic screams through that helper.
- Keep walking high-passed around 180 Hz and low-passed around 9 kHz.
- Give door-opening clips a catalog-owned base gain and band limits instead of inheriting the scene source's loud `0.8` gain.
- Keep fireplace ambience high-passed and add a gentle high-frequency ceiling.
- Cap every panic scream at `0.08` gain or lower, use deliberately quieter catalog assignments, and remove sub-bass/harsh upper frequencies with high- and low-pass filters.

## Verification

- EditMode tests instantiate and inspect the actual runtime components and filters.
- Catalog tests verify all eight scream assignments remain under the safety cap.
- Existing navigation and Chapter 2 regression tests remain green for the changed paths.
