# Requirements-first migration plan

## Safety principle

This is not a one-shot rewrite. Each phase must leave a compiling, testable project and a separate commit. Existing scripts retain their Unity GUIDs until scene/prefab references have migrated.

## Phase 0 — Baseline and recovery point — completed in this patch

- Captured Unity version, C# inventory, line counts, direct `MonoBehaviour` count, dependency-repair patterns, and serialized script references.
- Added a static architecture debt ceiling.
- Added source-control guard automation.
- Created a prune log.

Exit condition: the uploaded project can always be reconstructed from the original zip or baseline commit.

## Phase 1 — Architecture foundations — completed in this patch

- Added `GameRoot`, `GameContext`, `ChateauBehaviour`, `GameServiceBase`, validation types, `StateMachine<TState>`, `DefinitionAssetBase`, `GameDatabase`, chapter/room/actor/UI bases, and `StoryBeatBase`.
- Rebased major existing service and chapter classes without changing script GUIDs or public APIs.
- Removed the global fallback search from `ChapterEventScheduler`; it now requires its explicit `ChapterClock` binding.
- Added foundation tests.
- Pruned two statically proven unused scripts.

Exit condition: project compiles in Unity and existing EditMode tests pass.

## Phase 2 — Serialize the composition root — completed

Run the Editor installer in Unity:

```text
Tools > Chateau > Architecture > Install or Refresh Gameplay GameRoot
```

The installer must:

- add exactly one `Chateau_GameRoot`;
- add the currently runtime-created `RoomNavigationManager` and `DoorPromptSequenceController` if absent;
- add `SubtitleService` and `DialogueSpeechService` if absent;
- create and assign `GameDatabase.asset`;
- serialize all current `GameServiceBase` components;
- save `Gameplay.unity` only if validation passes.

Exit condition:

- Gameplay scene has exactly one root and each required service;
- no missing scripts;
- EditMode tests pass;
- Chapter 1 and Chapter 2 smoke tests pass;
- scene changes are committed separately.

## Phase 3 — Remove runtime repair bootstraps — in progress

Only after Phase 2 passes:

- `RoomNavigationBootstrap` replaced by serialized GameRoot validation and removed;
- `ChapterManager` top-level manager-stack self-creation fallback removed; the separately gated Chapter 2 controller factory is also removed;
- exactly one inert `Chapter2Controller`, HUD, monster-stinger owner, panic owner, and guest-search owner serialized and explicitly wired; all five top-level Chapter 2 creation fallbacks are removed;
- `Chapter2Controller` validates and directly uses its eleven serialized stable dependencies; its monolithic `ResolveReferences` repair search is removed while clock-strike audio composition remains separately gated;
- Chapter 1 binds the existing serialized guest-scale applier, which owns the serialized calibration and Butler source; both latent runtime owner factories are removed while per-guest participant creation remains deliberate;
- serialized dialogue/subtitle services, line bank, navigation edge, and Chapter 1 consumers are wired; both core-service `FindOrCreate` factories are removed while auxiliary views remain lazy until their own gate;
- one voice-playback owner, dedicated dialogue AudioSource, and speaking-indicator owner are serialized with explicit assets/navigation; both root factories are removed while lazy subtitle/bubble child-view creation remains deliberate;
- DialogueSpeechService owns the exact serialized Butler input edge, and the primary voice AudioSource owns one serialized Dialogue-channel volume component; real cataloged playback/input behavior is lifecycle-gated while transient overlap playback remains dynamic;
- blocking dialogue uses one token-safe service-owned input lease; cancellation releases before transition state is applied, stale routines cannot release a replacement lease, and shutdown/disable force a safe release;
- SubtitleService and GuestVoiceLinePlayback use validated serialized line-bank/catalog/navigation/source dependencies without resource or scene-search repair; lazy subtitle presentation and transient overlap playback remain separately gated;
- SubtitleService is bound to the shared speaking-indicator owner; redundant static cleanup searches are removed from dialogue, subtitle, and Chapter 2 paths;
- ChapterManager owns serialized dialogue/subtitle services and one direct debug-transition cleanup command; settings and teleports delegate to it, and the final static voice-stop/global subtitle searches are removed;
- the inert Chapter 1 HUD owner is serialized on the Chapter 1 controller; its lookup/factory/flag are removed while lazy canvas/text presentation remains deliberate;
- RuntimeSettingsMenu and its correctly scaled canvas are serialized under GameRoot with explicit navigation/chapter/clock/music; root/canvas factories are removed while nested controls remain lazy and owner-scoped;
- exploration music has one serialized channel-volume owner at its authored `0.125` base volume; RuntimeSettings no longer creates/reconfigures that component and the prior zero-volume migration regression is lifecycle-gated;
- RuntimeSettings validates and directly uses its serialized navigation/chapter/clock/music graph; external searches plus EventSystem/RectTransform repair are removed, while owner-scoped runtime controls remain intentionally dynamic;
- fireplace and clock ambience ownership is behaviorally frozen, then serialized under GameRoot with distinct sources, explicit catalogs/navigation, and a fireplace-only high-pass filter; their root, resource, and component-repair factories are removed;
- remove `DialogueSpeechService.FindOrCreate` and `SubtitleService.FindOrCreate` call sites after callers receive serialized/service references;
- replace `UrpPostProcessingBootstrap` with a serialized render rig;
- replace runtime clock-hand attachment with an authored `ClockView` reference.
- `GameClockHandsDisplay` is deleted after complete reference/resource proof and a zero-instance lifecycle; the guard and static test require it to remain pruned.

Exit condition: required managers are never created via `new GameObject` or `AddComponent` at runtime.

## Phase 4 — Canonical rooms, navigation, and set-piece props

Build one complete route through the target architecture before touching every door:

```text
InteractionRouter -> PassageInteraction -> NavigationService
  -> RoomViewService -> CameraService -> player arrival
```

Create stable `RoomDefinition`, `PassageDefinition`, `RoomView`, `Passage`, and arrival-anchor data. Migrate one round trip, test it, then migrate the remaining routes.

Room-local object cutouts are first-class props:

```text
RoomDefinition -> RoomView -> SetPieceView -> RoomDepthResolver
                         \-> RoomNavigationGeometry -> ActorMotor
```

- `RoomDepthResolver` and the static `SetPieceView` foundation are unit-gated before any scene binding;
- `purple_armchair_back` is the completed second vertical slice: `SetPieceView` owns stable order `8289`, the original art/transform/component identities remain, and the accepted Gameplay blocker polygon is collision-only;
- `purple_sofa` is the characterized third vertical slice: exact sofa art/transform/component identities and its Gameplay blocker polygon are frozen before the bounds-based writer is replaced;
- `SetPieceView` owns the visual cutout, a room-local occlusion anchor, and a sorting offset.
- `RoomNavigationGeometry` owns the room boundary and authored no-walk footprints.
- couches, desks, beds, toys, chairs, tables, and similar scenery use shared definitions/views rather than object-specific managers;
- static set pieces do not recompute sorting every frame;
- sorting never depends on world-space `Renderer.bounds` or `Collider.bounds`;
- accepted collider shapes are preserved and registered, not regenerated;
- the Drawing Room's duplicate prop-sort writers migrate one prop at a time;
- the first tea-table baseline exposed the duplicate-writer defect, then the table migrated to `Props / Set Pieces`: `SetPieceView` owns stable order `6627`, the accepted blocker polygon remains, and collision sorting is disabled for that prop;
- the second purple-armchair baseline exposed the same defect, then the armchair migrated in place: `SetPieceView` owns stable order `8289`, the accepted lower-seat polygon remains, and collision sorting is disabled for that prop;
- Dining Room seat occlusion migrates only after `ActorPresenter` exists and uses serialized `SeatOcclusionSlot` data.

Migrate navigation geometry and set-piece views one room at a time. The Grand Entrance Hall/Drawing Room route remains the first passage slice; Dining Room is the final set-piece slice because it depends on actor presentation.

Remove only after migration:

- `doors.txt` runtime path;
- `DoorCameraSequence` runtime path;
- `RoomVisualCatalog` runtime path;
- prompt/door paths that duplicate the canonical passage system.

Exit condition: one route graph, explicit reverse links, explicit parallel-stairway pairing, and PlayMode tests for every passage round trip.

Set-piece exit condition: one visual/sort writer per prop, one navigation-geometry owner per room, unchanged accepted collision footprints, stable occlusion across zoom/aspect ratios, and no room activation code that repairs descendant renderers.

## Phase 5 — Actors, movement, and presentation

Split `PointClickPlayerMovement` behind a temporary façade:

- `PlayerClickCommandSource`;
- shared `RoomStageMotor`;
- `ActorPresenter` as sole transform/scale/sort writer;
- `ActorAnimator`;
- shared actor footstep/voice emitters;
- `PlayerActorController` and `GuestActorController`;
- dedicated player and guest prefab variants.

Consolidate `ActorRoomState`, `RoomProjectedEntity`, guest scale components, `RoomPersonWalker2D`, `NPCWaypointMover`, and duplicate sorting paths only after behavior tests prove parity.

Exit condition: one motor and one presenter writer per actor; guests contain no player-input components.

## Phase 6 — Story flow and beat extraction

Make one `GameFlowService` the only chapter/beat owner. Extract one beat at a time from the large Chapter 1/2 controllers while preserving existing public entry points as façades.

Exit condition: chapter controllers read like the game script, Story Beats are testable, and there is one transition mechanism.

## Phase 7 — UI, dialogue, audio, camera, and lighting

- one serialized UI root and modal stack;
- one dialogue queue and subtitle/voice coordinator;
- one audio settings/ambience owner;
- one serialized render rig;
- editor-only lighting generation/bake;
- narrow runtime light views.

Exit condition: feature controllers create no canvases, cameras, audio managers, or lighting objects.

## Phase 8 — Final pruning and assembly boundaries

For every deletion, complete the prune-proof checklist. Then add namespaces and assembly definitions after dependencies are acyclic.

Exit condition:

- zero unjustified runtime classes;
- zero missing scripts;
- zero unowned state;
- zero permanent parallel systems;
- all critical game paths covered by tests;
- Hamza can redraw and explain the architecture from requirements.
