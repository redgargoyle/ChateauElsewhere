# Audio Mix Safety Implementation Plan

1. Add failing EditMode coverage for the shared filter utility, player/guest walking filters, door mix profile, fireplace filters, and panic-scream cap.
2. Extend `GameAudioSettings` with the shared band-limiting helper.
3. Reuse that helper from the existing footstep and fireplace components.
4. Put door gain/filter settings in `DoorOpenSoundCatalog` and apply them from `DoorTriggerNavigation`.
5. Put scream safety settings and the hard gain cap in `Chapter2PanicScreamCatalog`, then apply them from `Chapter2GuestPanicController`.
6. Update the existing resource assets with the intended mix values.
7. Run focused EditMode tests, the relevant navigation/Chapter 2 suite, and a broader EditMode verification pass.
8. Commit and publish the verified audio pass on `architecture_laptop_overhaul`.
