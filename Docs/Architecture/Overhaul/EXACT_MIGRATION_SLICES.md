# Exact migration slices from `latest_architecture` commit `872875aa`

This is a single continuous assignment executed as many clean commits. It is **not** one giant patch.

## Transaction used by every slice

```text
clean passing commit
  -> state one ownership change and allowed files
  -> add/identify characterization test
  -> introduce smallest replacement or adapter
  -> migrate one owner or consumer
  -> static/meta/GUID checks
  -> Unity compile
  -> focused EditMode tests
  -> relevant PlayMode smoke
  -> inspect complete diff
  -> commit
```

Legacy deletion is always a later transaction:

```text
replacement proven
  -> all callers migrated
  -> zero code references
  -> zero serialized GUID references
  -> no UnityEvent/animation/reflection/resource dependency
  -> behavioral parity
  -> remove components through Unity migration tooling
  -> delete script and .meta together
  -> full gate
  -> prune commit
```

## Phase 0 — make the evidence trustworthy

### 0.1 Protect the branch

- Verify current branch is `latest_architecture` at `872875aa4e3381993c3bb5d9c32a4393a7defe17`, or record and review any newer head before proceeding.
- Create `refactor/final-architecture-overhaul`.
- Tag the starting commit `pre-final-architecture-overhaul-872875aa`.
- Require a clean working tree.
- Record Unity `6000.4.10f1`.

### 0.2 Install the continuation controls

- Add the final runtime and editor ledgers.
- Add `verify_nunit_xml.py`, `validate_runtime_ledger.py`, `scan_unity_script_integrity.py` and the slice-gate scripts.
- Run all static scripts on the untouched branch and commit only tooling/docs.

### 0.3 Establish real test truth

- Run the 264-test EditMode suite without `-quit` and require a nonempty XML.
- Record the exact 46 current failure names and resolve them during the overhaul; they are not accepted in the final state.
- Split actual `[UnityTest]` tests into a PlayMode assembly and add:
  - MainMenu -> New Game -> Gameplay;
  - initial GameRoot/service state;
  - initial chapter/beat/time/room state;
  - one completed passage round trip;
  - room-root visibility and hidden-child persistence;
  - player movement/collision;
  - Butler scale at fixed resolution;
  - dialogue/subtitle startup.
- Capture fixed-resolution golden images/measurements for the entrance, Drawing Room, one passage transition and one Chapter 2 panic frame.

### 0.4 Stabilize the visual baseline

- Make Butler and guest scale deterministic across repeated cold starts.
- Stop calibration windows from writing values on ordinary `OnGUI` repaint.
- Preserve approved calibration in data profiles.
- Gate: ten cold starts produce identical scale/sort and the approved three-quarter-door visual.

## Phase 1 — harden Core, Clock and Data

### 1.1 Typed identity contracts

Add serializable stable value types or validated definition references for:

- `RoomId`;
- `PassageId`;
- `ActorId`;
- `ChapterId`;
- `BeatId`;
- `ObjectiveId`.

Display names never act as runtime identity.

### 1.2 Typed `GameContext`

- Keep `GameContext` non-global.
- Add explicit typed service properties in initialization order.
- Do not add `Get<T>()`, string lookup or singleton access.
- Test missing, duplicate and incorrectly ordered services.

### 1.3 Strict `GameRoot`

- In production scenes, validation errors stop startup.
- `GameRoot` owns composition only.
- Required services/views are serialized; none are created as repair.
- Add duplicate-root, duplicate-service and scene-binder tests.

### 1.4 Clock ownership

- Refactor `ChapterClock` into the sole `ClockService` while preserving GUID/API until callers migrate.
- Refactor `ChapterEventScheduler` into `GameScheduler`.
- Move chapter-specific schedule definitions out of the scheduler.
- `ClockView` reads the clock; it does not own time.
- Test pause, resume, crossing scheduled times, cancellation and save/restore values.

### 1.5 Complete definitions

- Author canonical definitions for all 19 rooms.
- Expand `GameDatabase` typed indexes.
- Eliminate duplicate IDs and unresolved definitions.
- Do not remove legacy route sources yet.

## Phase 2 — finish World and Navigation

The current six completed reciprocal groups remain regression anchors. Continue from Group 06.

### 2.1 Group 06 — Butlers Pantry / Billiard Room

- Advance both passages from dependency-bound stage 0 to fully authored/caller-bound.
- Bind explicit approach and arrival anchors.
- Migrate callers.
- Test both directions, camera, prompt and player placement.

### 2.2 Groups 07–09

Migrate one reciprocal pair per commit:

1. Butlers Pantry / Service Corridor;
2. Service Corridor / Kitchen;
3. Service Corridor / Chapel.

Each commit reuses the Slice 1.5 canonical RoomDefinitions, creates only the missing RoomView plus passage definitions/scene passages, migrates callers, and adds tests. Never duplicate or replace a room definition that Slice 1.5 already authored.

### 2.3 Groups 10–12

1. Grand Entrance Hall / Rear View — normalize the case/alias into one stable room ID;
2. Rear View / Billiard Room — resolve legacy endpoint conflict by scene truth and approved route behavior;
3. Rear View / Conservatory — preserve mixed interaction type explicitly.

### 2.4 Groups 13–19

One pair per commit:

- Service Corridor / Side Stair & Mudroom;
- Side Stair / Upper Sitting Hall;
- Upper Sitting Hall / Upper Gallery;
- Upper Gallery / Master Bedroom Suite;
- Upper Sitting Hall / Nursery;
- Upper Sitting Hall / Blue Bedroom;
- Nursery / Blue Bedroom.

### 2.5 Exceptional directed passages

- Group 20 Rear View -> Library: model as one-way if that is the approved design; otherwise add and author a real reverse scene interaction. Never invent a reverse by inference.
- Group 21 Service Corridor -> Billiard Room: same rule.
- Group 22 GEH/Upper Gallery parallel stairs: model the two outbound directed passages and the single return passage independently, each with its own explicit arrival. Add an optional logical group ID only for UI/analytics; do not require false reciprocal pairing.
- Replace mandatory reverse validation with explicit `PassageTopologyKind` (`Reciprocal`, `OneWay`, `SharedReturn`) and precise validators.

### 2.6 Introduce target owners behind facades

- `NavigationService`: sole current-room and transition transaction owner.
- `RoomViewService`: sole active-room-root owner.
- `InteractionRouter`: physical interaction routing.
- Existing `RoomNavigationManager` becomes a thin compatibility facade with no state of its own.
- Existing `DoorTriggerNavigation` becomes a thin caller facade during rollout.

### 2.7 Replace physical door interaction

- Add `PassageInteraction : InteractionTargetBase`.
- Migrate one completed route pair at a time.
- Preserve cursor, range, approach, prompt and audio behavior.
- After each pair, assert one current-room writer and one transition executor.

### 2.8 Transfer room activation

- `RoomViewService` toggles only room roots.
- `RoomView` never enables descendant renderers, flattens transforms or rewrites story/actor state.
- Add leave/re-enter tests for hidden guests, coats, oddities, particles and set pieces.

### 2.9 Prune the legacy route stack

Only after every directed passage and caller passes:

- remove runtime `doors.txt` loading;
- delete `DoorDataParser` after its temporary Editor importer is no longer needed;
- convert/delete `DoorCameraSequence`;
- convert/delete `RoomVisualCatalog`;
- delete `DoorButton`;
- delete `DoorPromptSequenceController`;
- delete `DoorTriggerNavigation`;
- delete `RoomNavigationManager` facade;
- delete `RoomContentGroup`.

Gate: all route tests pass, zero serialized refs, and no current-room string writer remains.

## Phase 3 — rooms, set pieces, collision and occlusion

### 3.1 Inventory every cutout

Create a generated ledger for every room-local visual/collider pair:

- room ID;
- object name and stable authoring ID;
- renderer;
- occlusion anchor;
- sorting profile/offset;
- collision/no-walk footprint;
- current component owners;
- migration status.

### 3.2 `RoomNavigationGeometry`

- Create one room-owned component/data set for walkable bounds and no-walk polygons.
- Collision/geometry never changes renderer sorting.
- Migrate `ObjectMovementBlocker2D` uses.

### 3.3 Migrate set pieces room by room

For each of the 19 rooms, in a separate commit or small validated room batch:

- use `SetPieceView` for cutout sprite and room-local depth;
- use `RoomNavigationGeometry` for collision;
- remove `WorldYSortSpriteRenderer`, `YSortSolidObstacle2D` and prop uses of `RoomProjectedEntity`;
- validate front/behind walking, no visual intersection and correct room visibility.

### 3.4 Finish Dining Room occlusion

- Model seating as authored `SeatOcclusionSlot` data consumed by `ActorPresenter`.
- Remove global seated-guest occlusion controllers after all eight seats pass.

### 3.5 Set-piece prune

Delete obsolete sorting/blocking scripts only after the generated inventory reports zero remaining instances and all room visual/collision tests pass.

## Phase 4 — actors, player and guests

### 4.1 Actor data and registry

- Add `ActorDefinition`, `ActorId`, `ActorState` and `ActorRegistry`.
- `ActorRegistry` owns lookup and room-membership indexing.
- `ActorControllerBase` owns logical actor state only.

### 4.2 Shared movement

- Implement `RoomStageMotor : ActorMotorBase`.
- Move path execution, collision, stop/completion and locomotion state from `PointClickPlayerMovement` and `RoomPersonWalker2D`.
- Player/guest decisions issue commands; they do not execute movement.

### 4.3 One presentation writer

- Implement `ActorPresenter : ActorPresenterBase`.
- Atomically transfer position projection, scale, tint, visibility and sorting.
- `ActorPresenter` is the only writer of those outputs.
- Use deterministic `RoomPerspectiveProfile`/actor profile data.

### 4.4 Animation and audio

- Implement `ActorAnimator` and `ActorAudioEmitter`.
- Merge player/guest footsteps.
- Voice playback is commanded by `DialogueService`; actor emitter plays it.

### 4.5 Command sources

- `PlayerCommandSource`: point/click intent through `InputRouter`.
- `GuestDecisionSource`: waypoint, panic, search, seating decisions.
- No command source writes transforms directly.

### 4.6 Prefab split

- Create shared actor base prefab or shared component layout.
- Create dedicated Player prefab.
- Create dedicated Guest prefab/variants.
- Replace one representative guest first while preserving references.
- Test that guest through Chapter 1 arrival/coat/seating and Chapter 2 panic/search/dining.
- Migrate the remaining seven guests one at a time.
- Assert every guest has no player input, `PlayerMovement` or `CharacterController2D`.

### 4.7 Actor prune

After zero references and full actor traces, delete/retire:

- `CharacterController2D`;
- `PlayerMovement`;
- `PointClickPlayerMovement`;
- `ActorRoomState`;
- `RoomProjectedEntity`;
- `GuestRoomScaleApplier`;
- runtime `GuestRoomScaleCalibration`;
- `GuestScaleParticipant`;
- duplicate scale utility behavior;
- `RoomPersonWalker2D`;
- `NPCWaypointMover` as a second motor;
- duplicate Y-sort and footstep components;
- Dining Room global occlusion controllers.

Gate: one motor and one presenter per actor, no guest input components, deterministic visual parity.

## Phase 5 — Story, chapters, objectives and physical interactions

### 5.1 `GameFlowService` and `StoryState`

- Add the sole chapter/beat/objective state owner.
- Keep `ChapterManager` as a facade until all callers migrate.
- Remove the separate Chapter 2 phase authority.

### 5.2 Objective model

- Add typed objective IDs, state and completion predicates.
- Add explicit Story interaction requirements.
- Story listens to physical Game events; it does not locate or mutate world components.

### 5.3 Chapter 1 extraction

Extract one tested beat per commit:

1. `TitleAndSetupBeat`;
2. `ArrivalScheduleBeat`;
3. `GuestArrivalBeat`;
4. `CoatServiceBeat`;
5. `DrawingRoomAmbientBeat`;
6. `EmptyDoorbellBeat`;
7. `Chapter1CompletionBeat`.

Move doorbell and coat-closet physical behavior under Game props. Convert `GuestArrivalConfig` to definitions. Reduce `Chapter1ArrivalController` to a facade, migrate callers, then delete it.

### 5.4 Chapter 2 extraction

One beat per commit:

1. `DrawingRoomSetupBeat`;
2. `PreSpeechBarksBeat`;
3. `AddressGuestsBeat`;
4. `MonsterStingerBeat`;
5. `PanicAndScatterBeat`;
6. `GuestSearchBeat`;
7. `ClockStrikeBeat`;
8. `DiningRoomTransitionBeat`;
9. `DiningRoomRevealBeat`.

Convert panic animation/audio rosters to stable-ID data. Remove the feature controllers after each responsibility has moved.

### 5.5 Story prune

Delete `ChapterManager` facade, `ChapterFeatureBase`, legacy action components and duplicate chapter phase enums only after full Chapter 1 and 2 PlayMode traces pass and save state can identify exact chapter/beat/objective state.

## Phase 6 — input, UI, dialogue, audio, camera, lighting and remaining props

### 6.1 Input and cursor

- `InputRouter` owns modal blocking and player input enablement.
- `CursorPresenter` owns cursor visuals.
- `InteractionTargetBase` owns shared hover/click/range checks.
- Remove `NavigationCursorHoverTarget` and cursor logic from `CameraManager`.

### 6.2 UI

- One authored runtime `UIRoot` and modal stack.
- Convert Main Menu, settings, chapter HUD, intro and subtitle UI into serialized screens deriving `UIScreenBase`.
- No feature controller creates canvases, EventSystems, buttons or required child graphs.
- Move debug teleport/time controls into development-only `DebugScreen`.

### 6.3 Dialogue

- `DialogueService` owns queue, voice lease, interruption and subtitle state.
- `SubtitleScreen` and `DialogueSpeakerView` render only.
- Migrate line/portrait/voice data to stable-ID `DialogueCatalog` definitions.

### 6.4 Audio

- `AudioService` owns channels, settings, one-shots and room ambience.
- Merge fireplace and clock ambience catalogs/controllers.
- Passages reference door audio profiles.
- Remove static `GameAudioSettings`.

### 6.5 Camera

- Split `CameraManager` into `CameraService`, serialized `RoomCameraRig` and `CursorPresenter`.
- Preserve all room framing/pan/zoom/shake behavior.
- Delete runtime camera repair and `UrpPostProcessingBootstrap` after golden parity.

### 6.6 Lighting

- `LightingService` controls serialized light views only.
- Move generation/repair/baking to explicit Editor tools.
- Convert or narrow `RoomLightOverlay` to `LightView` while preserving its 680 serialized references through an automated Unity migration.
- Remove `ExecuteAlways` scene-generation behavior.

### 6.7 Frame/oddity consolidation

Audit `StaticNoisePlayer`, `StaticSetImagePlayer`, `StaticSet`, `StaticFrameGroup` and `OdditySpriteAnimator`. Choose one `FrameSequenceView` and one `FrameSequenceDefinition`, migrate all approved uses, then delete the duplicate branch.

## Phase 7 — Save and release flow

- Add versioned `SaveGameData` and `SaveService`.
- Save/restore StoryState, clock, current room, actor logical state, durable prop state and player choices through owner APIs.
- Implement a real Continue path; if no valid save exists, disable it visibly.
- Add New Game reset and save/load round-trip tests.
- Main Menu must no longer create gameplay state directly.

## Phase 8 — editor tools, assets, folders and assemblies

### 8.1 Editor tools

Use `FINAL_EDITOR_TOOL_LEDGER.csv`.

- Move tests into proper EditMode/PlayMode folders/assemblies.
- Consolidate scale/projection calibration into one tool.
- Keep only documented authoring and diagnostics tools.
- Delete one-off repair and migration tools after their outputs are certified.
- Delete custom editors when their legacy runtime classes disappear.

### 8.2 Content cleanup

Generate exact reference/hash reports before moving or deleting.

- Move source sheets, previews, external archives, recovery copies and reproducible intermediates outside `Assets` into `ProjectArchive`.
- Delete exact duplicates only when canonical GUID references are migrated.
- Keep runtime art/audio/animation inside `_Chateau/Content`.
- Fix typo/temporary folders such as `Assets/Prefabs/ights` through Unity-safe moves.
- Do not delete anything merely because it is named `Legacy`.

### 8.3 Final source moves

Move scripts with their `.meta` files only after ownership is stable. Remove empty first-party roots:

- `Assets/Scripts`;
- `Assets/Map`;
- `Assets/_Chateau/Scripts`;
- root `Assets/Editor`.

### 8.4 Namespaces and asmdefs

Add bottom-up, acyclic assemblies only after source moves:

1. `Chateau.Core`;
2. `Chateau.Data`;
3. `Chateau.Game.Contracts`;
4. `Chateau.Game`;
5. `Chateau.Story`;
6. `Chateau.Game.Presentation`;
7. `Chateau.Story.Presentation`;
8. `Chateau.Editor`;
9. `Chateau.Tests.EditMode`;
10. `Chateau.Tests.PlayMode`.

Use `[MovedFrom]` and `[FormerlySerializedAs]` where applicable and verify the project’s Unity version supports the chosen migration attributes.

## Phase 9 — final certification

Required final gates:

- zero compiler errors;
- zero untriaged EditMode or PlayMode failures;
- no missing scripts or duplicate GUIDs;
- no required runtime repair/search/create path;
- one owner/writer assertions pass;
- every passage direction tested;
- all 19 room roots and set-piece inventories validated;
- all nine actors use one motor and one presenter;
- no guest has player input;
- Chapter 1 and Chapter 2 full traces pass;
- New Game and Continue pass;
- save/load round trip passes;
- release build succeeds;
- final prune manifest and class catalog match the repository;
- Hamza can draw and explain the final high-level chart from memory.
