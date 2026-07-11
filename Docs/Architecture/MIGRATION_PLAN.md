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

## Phase 2 — Serialize the composition root — next required phase

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

## Phase 3 — Remove runtime repair bootstraps

Only after Phase 2 passes:

- replace `RoomNavigationBootstrap` with startup validation, then remove it;
- remove `ChapterManager` self-creation fallback;
- remove `DialogueSpeechService.FindOrCreate` and `SubtitleService.FindOrCreate` call sites after callers receive serialized/service references;
- replace `UrpPostProcessingBootstrap` with a serialized render rig;
- replace runtime clock-hand attachment with an authored `ClockView` reference.

Exit condition: required managers are never created via `new GameObject` or `AddComponent` at runtime.

## Phase 4 — Canonical rooms and navigation vertical slice

Build one complete route through the target architecture before touching every door:

```text
InteractionRouter -> PassageInteraction -> NavigationService
  -> RoomViewService -> CameraService -> player arrival
```

Create stable `RoomDefinition`, `PassageDefinition`, `RoomView`, `Passage`, and arrival-anchor data. Migrate one round trip, test it, then migrate the remaining routes.

Remove only after migration:

- `doors.txt` runtime path;
- `DoorCameraSequence` runtime path;
- `RoomVisualCatalog` runtime path;
- prompt/door paths that duplicate the canonical passage system.

Exit condition: one route graph, explicit reverse links, explicit parallel-stairway pairing, and PlayMode tests for every passage round trip.

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
