# Guest Scale Architecture Overhaul Design

## Purpose

Guest body scale should be controlled by one clear system. The Butler room calibration is trusted and should remain unchanged. Guests should use that room/depth information through a simple per-room master multiplier, without the old overlapping final-scale writers that made entrance guests too small or allowed coat pickup/return to change body size.

This design supersedes `docs/superpowers/plans/2026-06-29-guest-human-scale-from-butler.md` where that plan implies a fragile visible-height fitter as the primary solution. The new primary solution is room calibration plus participant base scale, not visual-height fitting.

## Goals

- Start from latest `main`.
- Keep all Butler movement and existing Butler room calibration values unchanged.
- Add one final guest body-size writer.
- Add a simple room-level guest size slider using the Butler room curve by default.
- Make Chapter 1 entrance guests and RoomPersonWalker2D guests opt into the same final scale system.
- Ensure coat pickup/return does not change guest body scale.
- Provide one simple master editor tool and a separate audit report.
- Preserve placement, sorting, tint, animation, movement, visibility, and story state behavior in existing classes.

## Non-Goals

- Do not change guest art.
- Do not implement another visual-height fitter as the primary scale solution.
- Do not add another large debug tool with many primary buttons.
- Do not delete `RoomProjectedEntity`, `RoomPersonWalker2D`, or `ActorRoomState`.
- Do not make coats, speech bubbles, shadows, prompts, or UI overlays part of guest body scale.

## Architecture

The new architecture has three runtime parts:

1. `GuestRoomScaleCalibration`
   Scene-level room data. Each room has a master multiplier and either uses the Butler front/back room curve or an optional custom guest curve.

2. `GuestScaleParticipant`
   Marker component on each visible human guest. It captures the guest's base body scale once, resolves the room id and room-local Y, and identifies the transform that should be scaled.

3. `GuestRoomScaleApplier`
   Late-running final writer. It finds participants, skips the Butler and excluded objects, evaluates the room guest scale, applies pose ratios, and writes the final scale exactly once.

Final scale formula:

```text
finalScale = capturedBaseScale
  * guestRoomScale
  * poseRatio
  * manualFineTuneMultiplier
```

`guestRoomScale` comes from `GuestRoomScaleCalibration.TryEvaluateGuestScale`. By default it uses the Butler room normalized curve multiplied by `roomGuestScaleMultiplier`. Custom guest front/back curves are an advanced fallback only.

## Legacy Scale Writers

The following systems must stop being final guest body-size authorities:

- `GuestButlerScaleHarmonizer`
- `GuestButlerScaleTool`
- `RoomProjectedEntity.ApplyButlerCharacterScaleNow`
- `RoomProjectedEntity.ForceApplyButlerCharacterScale`
- `RoomPersonWalker2D.ApplyButlerCharacterScaleNow`
- `RoomPersonWalker2D.ApplyButlerScaleSample`
- `ActorRoomState.ApplyButlerCharacterScaleNow`
- `ActorRoomState.BuildButlerActorScale`
- `RoomProjectedEntity.roomVisualScaleOverrides`
- `RoomPersonWalker2D.nearScale` / `farScale` for final body size
- `ActorRoomState.scaleWithRoomStageMotion` for final body size

Where possible, obsolete final-writer methods should be removed. If compile compatibility requires stubs, they must be marked obsolete and must not be used by gameplay or the new applier.

`RoomProjectedEntity`, `RoomPersonWalker2D`, and `ActorRoomState` keep their non-scale responsibilities. Their remaining scale behavior may still support props, placement helpers, or legacy serialized data, but not final guest body size when a `GuestScaleParticipant` is active.

## Guest Participants

`GuestScaleParticipant` marks a single visible guest body.

Fields:

- `characterId`
- `roomIdOverride`
- `pose`: Auto / Standing / Seated / Crouching / Lying
- `scaleRoot`
- `bodyRoot`
- `excludeFromGuestScaling`
- `isButler`
- `manualFineTuneMultiplier`
- `seatedRatioOverride`
- `capturedBaseScale`
- `hasCapturedBaseScale`

Rules:

- Guest 1 through Guest 8 Player prefab instances scale at the guest root transform.
- `RoomPersonWalker2D` guests scale at `targetGraphic.rectTransform` when present; otherwise the walker transform.
- `RoomProjectedEntity` floor characters scale at `visualRoot`.
- The Butler is never scaled as a guest.
- Duplicate participants for the same visible human are not created.
- Objects whose names contain coat, jacket, cloak, shawl, speech, thought, bubble, prompt, highlight, icon, shadow, cursor, or tooltip are ignored as body roots.

## Room Calibration

`GuestRoomScaleCalibration` is scene-level data.

Each `GuestRoomScaleEntry` has:

- `roomId`
- `enabled`
- `roomGuestScaleMultiplier`
- `useButlerRoomCurve`
- optional custom front/back guest curve data

Room matching trims names, ignores case, and ignores spaces, underscores, and hyphens.

Evaluation:

1. If custom guest curve is enabled and complete, evaluate custom front/back guest scale.
2. Else if Butler curve is enabled, evaluate the Butler room normalized scale for that room/depth.
3. Else use `1`.
4. Multiply by `roomGuestScaleMultiplier`.

`InitializeMissingRoomsFromButler(PointClickPlayerMovement butler)` creates room entries for Butler calibrated rooms without changing Butler values.

## Chapter 1 Integration

`Chapter1ArrivalController` should ensure guest participants and the applier exist when guest runtime state is created/prepared. After guests are placed in the entrance, after coat pickup, and after coat return/storage, it should refresh guest scaling.

Coat visuals may move between guest and Butler, but guest base body scale must remain based on the participant body root, not coat children.

## Editor Tools

### Guest Scale Audit

Menu: `Tools > Characters > Guest Scale Audit`

Output: `Assets/Editor/Reports/GuestScaleAudit.md`

The report records Butler room calibration entries, Chapter 1 guests, RoomPersonWalker2D guests, RoomProjectedEntity usage, coat visuals under guests, and active legacy scale writers. The summary includes the required counts from the prompt.

### Guest Size Master

Menu: `Tools > Characters > Guest Size Master`

Primary workflow:

- Room dropdown
- `Guest Size In This Room` slider from `0.25` to `3.0`
- `SET UP GUEST SCALING`
- `PREVIEW ROOM GUEST SIZE`
- `SAVE ROOM GUEST SIZE`
- `APPLY TO ALL GUESTS IN ROOM`
- `SAVE SCENE`

Advanced foldout only:

- Run audit
- Set custom front/back guest curve
- Reset selected room multiplier
- Proof shrink/grow guests
- Emergency restore captured base scales

The main workflow must remain simple and guided.

## Pose Overrides

`GuestPoseScaleOverrideStore` is optional data for seated Drawing Room guests, seated Dining Room guests, and special pose cases.

Default pose ratios:

- Standing: `1`
- Seated: `0.68`
- Crouching: `0.75`
- Lying: `0.45`

`seatedRatioOverride` is clamped between `0.55` and `0.80` when greater than zero.

## Tests

Tests should be added or updated for:

- Butler room calibration initializes guest room entries.
- Butler curve evaluation respects room multiplier.
- Participant base scale capture.
- Applier scales Player prefab guest instances.
- Applier uses walker target graphic.
- Applier uses projected floor-character visual root.
- Coat visuals are ignored.
- Taking/returning coats does not change guest body scale.
- Master tool exposes the simple primary workflow and hides debug controls in advanced.
- Entrance guests use Grand Entrance Hall multiplier.
- Seated guests use seated pose ratio.
- Old harmonizer/tool are removed or obsolete.
- Old `ApplyButlerCharacterScaleNow` paths are not used by the new applier.

## Risks And Mitigations

- Scene serialization risk: prefer editor APIs and focused scene writes; avoid manual YAML edits for scene object setup.
- Legacy compile references: if deleting old files breaks tests or serialized references, replace them with obsolete no-op stubs and then remove usages incrementally.
- Duplicate participants: setup code must deduplicate by resolved scale root or visible human root.
- Coat-driven scale changes: tests must cover coat pickup and return paths, and body-root resolution must ignore coat names.
- Butler regression: tests and implementation must not write Butler calibration values or Butler movement behavior.
