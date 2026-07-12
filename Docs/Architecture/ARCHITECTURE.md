# Chateau Chantilly architecture constitution

## Purpose

This document defines the architecture the project is migrating **toward**. The current repository remains a source of behavior, content bindings, and migration evidence; it is not the authority for future ownership.

The architecture starts from the game script:

```text
Approved game script
  -> chapter
  -> story beat
  -> capability command
  -> one authoritative state owner
  -> presentation views
  -> tested completion condition
  -> next beat / next chapter
```

## Whiteboard model, transcribed

The supplied whiteboard contains two useful mental models.

### Game loop

```text
Game Loop
  +-- Chapter List
  |     +-- Chapter 1
  |     |     +-- Event List
  |     |     |     +-- Event
  |     |     |           +-- Time
  |     |     |           +-- Effect
  |     |     +-- chapter-specific setup and triggers
  |     +-- Chapter 2
  |     +-- Chapter 3
  +-- Current State
```

### Initialization and world structure

```text
Bootstrap / setup
  +-- Player
  +-- Game
  |     +-- Scheduler
  |     |     +-- Chapters
  |     |     +-- Current Time
  |     +-- Current Room
  |     +-- House
  |           +-- Rooms
  |           +-- Visuals
  |           |     +-- Lights
  |           |     +-- Object cutouts
  |           +-- Doors
  |                 +-- Source room
  |                 +-- Target room
  |                 +-- Door/room boundary
  |                 +-- Door transition
  +-- Guests
        +-- Dialogue
```

The whiteboard is directionally correct: it starts with the game loop and initialization rather than with accidental classes. The target below strengthens it by separating immutable definitions, authoritative state, commands, and presentation.

## Refined high-level mental model

```text
GameRoot
  -> GameContext
  -> GameDatabase
  -> GameFlowService
       -> ChapterController
            -> StoryBeat
  -> ClockService / Scheduler
  -> WorldService / NavigationService
       -> House
            -> RoomView
                 -> Actors
                 -> Props
                      -> SetPieceView
                      -> InteractiveProp
                      -> Passage
                      -> LightView
                 -> RoomNavigationGeometry
                 -> RoomAnchor
  -> ActorRegistry
       -> PlayerActor
       -> GuestActors
  -> InteractionRouter
  -> NarrativeService
  -> UIService
  -> CameraService
  -> AudioService
  -> LightingService
  -> SaveService
```

### Why this differs slightly from the whiteboard

- **Bootstrap becomes `GameRoot`**, a composition root, not a system that repairs missing objects.
- **`Current Room` has one owner** (`NavigationService`/`WorldService`) rather than living loosely under `Game`.
- **The scheduler consumes time; it does not own chapter logic.** `ClockService` owns time. `GameFlowService` owns chapter and beat state.
- **Guests do not own dialogue infrastructure.** Guest definitions contain line IDs and personality data; `NarrativeService` owns queueing, interruption, subtitles, and voice coordination.
- **Rooms own no global state.** A `RoomView` presents a room; services own active-room state and transitions.
- **Doors become passages.** A passage has stable source/destination IDs plus explicit authored approach and arrival anchors.
- **Furniture and object cutouts are room props.** Couches, desks, beds, toys, chairs, tables, and similar cutout scenery use shared `SetPieceView` data instead of bespoke controllers.
- **Set-piece presentation and navigation geometry are separate owners.** `SetPieceView` owns the visual cutout and room-local occlusion anchor. `RoomNavigationGeometry` owns walkable boundaries and no-walk footprints. A collision marker must never rewrite renderer sorting.
- **Definitions and runtime state are separate.** Assets describe rooms, actors, passages, chapters, dialogue, and presentation; runtime objects hold mutable state.

## Base-class hierarchy

Inheritance is shallow. Composition carries feature behavior.

```text
MonoBehaviour
  +-- ChateauBehaviour
       +-- GameServiceBase
       +-- ChapterControllerBase
       +-- ChapterFeatureBase              (temporary extraction seam)
       +-- RoomElementBase
       |    +-- InteractionTargetBase
       +-- ActorControllerBase
       +-- ActorMotorBase
       +-- ActorPresenterBase
       +-- UIScreenBase

ScriptableObject
  +-- DefinitionAssetBase

Plain C#
  +-- StoryBeatBase
  +-- StateMachine<TState>
```

### Base-class rule

A base class may provide only a shared lifecycle, invariant, or contract. It must not become a generic bag of helpers. Maximum planned depth is three project-defined levels beneath a Unity base type.

## One-owner table

| State or side effect | Sole target owner |
|---|---|
| Current game/chapter/beat | `GameFlowService` |
| In-game time | `ClockService` |
| Scheduled callbacks | scheduler owned by `ClockService` |
| Current room and passage transition | `NavigationService` |
| Active room root | `RoomViewService` |
| Set-piece sprite, room-local occlusion anchor and sorting offset | one `SetPieceView` per prop |
| Walkable boundary and set-piece no-walk footprints | `RoomNavigationGeometry` per room |
| Room-local Y to sorting-order calculation | pure `RoomDepthResolver` |
| Room projection/pan/zoom | `CameraService` |
| Actor identity and logical state | `ActorControllerBase` per actor |
| Actor registry and room membership | `ActorRegistry` |
| Actor movement execution | one `ActorMotorBase` per actor |
| Actor position/scale/tint/sorting presentation | one `ActorPresenter` per actor |
| Pointer/click/modal/cursor routing | `InputRouter` / `InteractionRouter` |
| Runtime UI root and modal stack | `UIService` |
| Dialogue queue, subtitle state, voice interruption | `NarrativeService` / `DialogueService` |
| Mixer settings, one-shots, room ambience | `AudioService` |
| Runtime light state | `LightingService` |
| Durable session state | `SaveService` |

No second owner is allowed without an Architecture Decision Record (ADR) and a dated removal plan.

## How chapters come alive

```text
ChapterDefinition
  -> GameFlowService selects ChapterController
  -> ChapterController enters a StoryBeat
  -> StoryBeat sends typed commands to capability owners
  -> owner changes authoritative state
  -> owner publishes events / query results
  -> room, actor, UI, audio, camera and lighting views update
  -> beat completion predicate passes
  -> GameFlowService advances the beat
  -> SaveService records durable state
```

A chapter controller should read like the approved script. It must not implement pathfinding, construct UI, discover actors globally, or create services.

### Chapter 1 target beat shape

```text
Chapter1Controller
  +-- TitleAndSetupBeat
  +-- ArrivalScheduleBeat
  +-- GuestArrivalBeat
  +-- CoatServiceBeat
  +-- DrawingRoomAmbientBeat
  +-- EmptyDoorbellBeat
  +-- Chapter1CompletionBeat
```

### Chapter 2 target beat shape

```text
Chapter2Controller
  +-- DrawingRoomSetupBeat
  +-- PreSpeechBarksBeat
  +-- AddressGuestsBeat
  +-- MonsterStingerBeat
  +-- PanicAndScatterBeat
  +-- GuestSearchBeat
  +-- ClockStrikeBeat
  +-- DiningRoomTransitionBeat
  +-- DiningRoomRevealBeat
```

Dialogue variants and guest responses should be data consumed by a beat, not separate control-flow controllers unless they own a real lifecycle.

## Runtime dependency direction

```text
Data definitions
      v
GameRoot -> services -> chapter/room/actor controllers -> views
                          ^
                          |
                    typed commands/events
```

Forbidden dependency directions:

- views changing global story state directly;
- actors finding chapter controllers;
- chapter code constructing cameras, canvases, audio services, or navigation managers;
- room activation forcing descendant renderers or actor state;
- collision or navigation components changing renderer sorting;
- set-piece depth derived from world-space renderer/collider bounds;
- multiple `LateUpdate` writers changing the same prop or actor sorting order;
- runtime code creating required managers as a repair;
- display names acting as gameplay identity;
- essential data loaded by an arbitrary string path.

## Current migration seam

The first safe patch introduces the composition root and base families without replacing serialized script identities. Major existing managers now derive `GameServiceBase`; Chapter 1/2 orchestration classes derive chapter bases. Their public APIs and Unity script GUIDs are preserved.

`GameRootInstaller` has installed the composition root and formerly runtime-created navigation/dialogue components into `Gameplay.unity` **inside Unity**, with migrated services serialized under that root. The old root/navigation bootstrap paths were removed only after their lifecycle and full-suite gates passed; remaining compatibility resolvers stay until their own replacement slices pass.

The Phase 4 navigation seam now contains ten registered definitions, four passive RoomViews, and six directed Passage instances across two fully template-certified reciprocal pairs: Entrance/Drawing and Drawing/Music. `RoomDefinition` and directed reciprocal `PassageDefinition` carry stable data identity; `RoomView` only validates/reports its existing root; and `Passage` validates/reports its definition, source room, reverse link, logical anchors, and one temporary per-pair anchor-migration stage. The existing `RoomNavigationManager` remains the sole room-state owner behind `INavigationService`: canonical current room is derived from its one legacy room string and registered database. Four scene Passages serialize `AuthoredAnchors = 2` for the four `complete` rows; the two Music/Library Passages remain `LegacySampling = 0`, while their rows are `caller-bound` with exact direct dependencies and co-located canonical identities. Their finite reciprocal points remain passive validation data. The real Drawing/Music callers therefore treat calibrated Drawing `(-7.16, -1.78)` and Music `(-7.94, -3.27)` as authoritative reciprocal approach/arrival points. Their acceptance contract evaluates each exact non-projected point from two far starts at all four rendered aspects and widest-aspect maximum zoom, drives one real production movement command per direction and aspect, both maximum-zoom directions, and a symmetric stale-destination replay, then protects near synchronous traversal, null-Passage fallback, and the complete lifecycle side-effect sequence. A prerequisite fix synchronizes destination-room collider transforms before exact arrival validation because Unity's automatic 2D transform synchronization is disabled; this prevents a room stage last seen at zoom `1.22` from leaving a stale collider cache during same-call placement. Relative to that prerequisite, the accepted approach-ownership scene slice changed only two stage scalars and altered no caller, direct dependency, coordinate, definition, topology, GameRoot entry, runtime script, prefab, asset, `.meta`, or GUID. The later complete-certification bookkeeping changes no scene, runtime source, prefab, asset, `.meta`, GUID, serialized reference, topology, coordinate, caller, or dependency and passes the complete reciprocal-pair contract plus every repository gate. This temporary 0/1/2 gate keeps caller, arrival, and approach cutovers independently testable and is deleted only after every Passage in the full migration reaches the complete stage. `PointClickPlayerMovement.TryWarpToExact` validates authored destinations without clamping, projection, or an old-room path requirement. Trigger input, proximity, hover/prompt, movement callbacks, and audio ownership remain on the compatibility interaction owner. Thirty-nine scene triggers retain fully null direct dependencies, and 39 retain null canonical callers. All resolvers remain, and no `RoomViewService` activation writer exists. The exact next safe slice is a test-only arrival calibration/preflight that resolves the source-sensitive Library far/near placement before any stage-1 ownership change.

At the Group `02` characterization checkpoint, its two four-component trigger owners retained exact hierarchy, sibling order, rectangles, standard Door profile, `145`-pixel proximity, null serialized dependencies/callers, and zero Passage components; runtime resolution reused the existing navigation manager, Player, shared door AudioSource, and catalog. Music retained RoomView `4100000003`, while Library had no canonical definition/view/passage yet. The Library chair/lamp blocker, both room boundaries, and Chapter 2 hide anchors at Library `(-255, -181)` and Music `(306, -162)` remained part of the contract. At `1366x768`, far Music -> Library approached `(7.439471, -2.846709)` and landed at the source-sensitive Library result `(-7.287828, -2.936489)`, while the immediate near path landed at `(-7.244175, -2.799095)`; reverse approach/arrival were `(-7.244175, -2.799095)` / `(7.439471, -2.846709)`. Null/left/center/right candidates converged exactly, four aspect ratios retained their locked viewport-dependent values, and `2560x1080` maximum zoom `1.22` preserved forward approach/arrival `(8.625211, -3.301599)` / `(-8.3582, -3.229418)` and reverse `(-8.356381, -3.228665)` / `(8.676323, -3.178201)`. Both locked runs shared seven-line observation-message SHA-256 `f2a4b15ccee94282032102f6b6c93a2673444d5d8d099a8bd1f24be6190fc2ab` using `\n` separators with no trailing newline. That characterization changed no runtime, scene, data asset, prefab, `.meta`, GUID, serialized reference, hierarchy, geometry, topology, dependency, caller, or anchor. Focused gates passed `10/10`, safety passed `28/28`, rendered lifecycle passed `6/6`, and the full suite remained `256` discovered / `210` passed / the same `46` known failures with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`; architecture/serialization/Y-axis audits remained 112 / 48,789 / 48 / 155 / zero hard errors / 38 tracked.

Group `02` is now `data-authored`. `Room_Library.asset` owns stable ID `room.library`, primary/display `Library`, scene background GUID `0a85e4fdd73e4714fabde63002a457e7`, null profile, and asset GUID `8da3a3e936712e7b9f534786110323e4`. Directed Door / `Open Door` assets use stable/legacy IDs `passage.music-room.library` / `MusicRoom_Library` at GUID `aefe77f20874eb81b83fccb6ff5b8046` and `passage.library.music-room` / `Library_MusicRoom` at GUID `3a641d5febbfd7aec481ada678ba9fe4`; they swap exact Music/Library endpoints and directly reverse-link. `GameDatabase` appends Library, Music -> Library, and Library -> Music once after the prior seven definitions, advancing `7 -> 10`. This slice adds only those three data assets and their new metas and changes the database plus matching tests/manifest/docs; it changes no production/runtime source, scene, prefab, existing `.meta`/GUID, RoomView, Passage component, trigger dependency, caller, or anchor. Focused gates pass `10/10`, safety passes `28/28`, rendered lifecycle passes `6/6`, and the full suite remains `256/210/46` with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture remains 112 runtime files / 48,789 lines / 48 direct `MonoBehaviour` declarations / 155 serialized-script rows; Y-axis remains zero hard errors / 38 tracked findings. Next, add exactly one passive RoomView to existing Library root `1367921344` and register it once with GameRoot; do not add either Passage component or change trigger dependencies/callers/anchors.

Group `02` is now `view-bound`. Existing Library root `1367921344` owns passive RoomView `4100000004`, bound to Library definition GUID `8da3a3e936712e7b9f534786110323e4` and existing content `2102000003`; GameRoot registers it once after Music RoomView `4100000003` and before Passage `4100000011`. The scene delta is exactly 16 added YAML lines with no removals, advancing Gameplay from `6,014` to `6,015` documents and from three to four RoomViews while retaining four Passages. No hierarchy, Transform, SceneRoots, trigger dependency/caller, Passage component, runtime source, prefab, asset, `.meta`, collider, prop, occlusion, activation, or camera ownership changes. Focused gates pass `10/10`, safety passes `28/28`, rendered lifecycle passes `6/6`, and the full suite remains `256/210/46` with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture remains 112 runtime files / 48,789 lines / 48 direct `MonoBehaviour` declarations / 155 serialized-script rows; RoomView scene references advance from three to four, and Y-axis remains zero hard errors / 38 tracked findings. Next, add only passive reciprocal Passage components `4100000015` and `4100000016` to the existing trigger owners and GameRoot. Serialize the characterized reciprocal reference coordinates at `LegacySampling = 0`, but do not bind dependencies/callers or transfer approach/arrival ownership in that slice.

Group `02` is now `passage-bound`. Music -> Library Passage `4100000015` and Library -> Music Passage `4100000016` are co-located with their existing trigger owners, registered after Passage `4100000014`, and directly reverse-link the existing Music/Library RoomViews and definitions. Music point `(7.439471, -2.846709)` and Library point `(-7.244175, -2.799095)` serialize reciprocally as finite validation data at `LegacySampling = 0`; production still owns neither approach nor arrival through these components. The distinct characterized far Library result `(-7.287828, -2.936489)` remains locked as proof that legacy source-sensitive placement is still authoritative. The current-schema scene delta is exactly 44 added YAML lines and no removals: two owner references, two GameRoot references, and two 20-line Passage documents. Gameplay advances `6,015 -> 6,017` documents and `4 -> 6` Passages while retaining four RoomViews. Both trigger documents, their null dependencies/callers, hierarchy, geometry, colliders, props, occlusion, activation, camera ownership, runtime source, prefabs, assets, and `.meta` files remain unchanged. Focused gates pass `10/10`, safety passes `28/28`, rendered lifecycle passes `6/6`, and the full suite remains `256/210/46` with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture remains 112 runtime files / 48,789 lines / 48 direct `MonoBehaviour` declarations / 155 serialized-script rows; Passage scene references advance from four to six, and Y-axis remains zero hard errors / 38 tracked findings. The next safe slice binds only the existing direct dependencies.

Group `02` is now `dependencies-bound`. Trigger documents `552135204` and `2300000079` each replace only four null references with navigation manager `1878886997`, Player Transform `81962843`, shared `Audio_DoorOpen` source `2201000013`, and door catalog GUID `9a77542e25184fbc945d6a79f77007e7`; stair catalogs remain null and canonical callers remain absent. The scene diff is exactly eight replacement lines added and eight removed, with 6,017 documents / four RoomViews / six Passages unchanged. Passages `4100000015/16`, their stage-0 coordinates/topology, GameRoot, owners, hierarchy, colliders, props, occlusion, activation/camera ownership, runtime source, prefabs, assets, and `.meta` files remain unchanged. Focused gates pass `10/10`, safety passes `28/28`, rendered lifecycle passes `6/6`, and the full suite remains `256/210/46` with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. The seven-line observation hash advances only because its profile records `serializedDependencies=bound runtimeDependencies=stable`, to `cb6e7c14702f7e9adcedfda1d5a0fba5f14462581c607d203c53152b5a3b40a7`; all six coordinate lines remain exact. Architecture remains 112 runtime files / 48,789 lines / 48 direct `MonoBehaviour` declarations / 155 serialized-script rows, and Y-axis remains zero hard errors / 38 tracked findings. Next, bind only the co-located canonical callers while retaining `LegacySampling = 0`.

Group `02` is now `caller-bound`. Trigger `552135204` gains only `canonicalPassage: 4100000015`; trigger `2300000079` gains only `canonicalPassage: 4100000016`. The exact scene delta is two added lines and no removals. Gameplay remains 6,017 documents / four RoomViews / six Passages; both Passages remain `LegacySampling = 0`, so canonical identity delegates to the byte-preserved legacy approach/arrival samplers. All dependencies, coordinates, Passage documents, GameRoot, component topology, hierarchy, colliders, props, occlusion, activation/camera ownership, runtime source, prefabs, assets, and `.meta` files remain unchanged. Rendered tests poison every passive coordinate by more than 100 units and independently null/restore both callers. The untouched primary still proves first-entry far Library `(-7.287828, -2.936489)` versus near `(-7.244175, -2.799095)`; both post-primary proofs retain the correct history-sensitive `(-7.244175, -2.799095)` Library result and exact eight-event/audio lifecycle. Focused gates pass `10/10`, safety passes `28/28`, rendered lifecycle passes `6/6`, and the full suite remains `256/210/46` with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. The seven-line observation hash changes only `callers=null` to `callers=bound`, to `46d857f57e7e41d6a7facaa3a39d0f97bf6dd4996d7f7d10e3da809d8eced2b7`. Architecture remains 112 runtime files / 48,789 lines / 48 direct `MonoBehaviour` declarations / 155 serialized-script rows, and Y-axis remains zero hard errors / 38 tracked findings. Next, run a test-only arrival calibration/preflight before changing either stage scalar.

The characterization deliberately separates canonical identity from legacy viewport sampling. Each room side now owns one shared doorway point: Entrance `(-7.75, -2.22)` is the forward approach and reverse arrival; Drawing Room `(5.267176, -2.104616)` is the reverse approach and forward arrival. Both are exact, path-reachable, inside the `145`-pixel activation envelope, source-independent, and valid at `1366x768`, `1440x1080`, `1920x1080`, and `2560x1080`. The old neutral-view samples—Entrance `(-7.703568, -2.000136)` and approach `(-7.576081, -1.986423)`, Drawing approach `(5.280546, -2.015396)`—remain only legacy-fallback evidence. Canonical far order remains `ArrivedAtDestination` -> `MovementStopped` -> audio -> room event -> exact reciprocal arrival.

That round trip is automatically certified as the reusable **compatibility-seam** template in [PASSAGE_COMPATIBILITY_TEMPLATE.md](PASSAGE_COMPATIBILITY_TEMPLATE.md), backed by the exact 45-trigger [RemainingRouteInventory.csv](RemainingRouteInventory.csv). Human doorway/camera/visibility review remains pending. This does not claim final target-route ownership: `DoorTriggerNavigation` interaction, `RoomNavigationManager` room activation/camera compatibility, and the future `InteractionRouter`, `PassageInteraction`, `RoomViewService`, and `CameraService` transfers remain separate later gates.

## Definition of a justified runtime class

Every retained runtime class must answer:

1. Which approved requirement or architecture invariant justifies it?
2. Who owns it?
3. What state does it own?
4. What inputs cross its boundary?
5. What outputs or side effects can it produce?
6. Which direction may it depend on?
7. Which test protects its critical behavior?
8. What exact behavior disappears if it is removed?

If the answers are unclear, the class is not ready to survive the pruning phase.
