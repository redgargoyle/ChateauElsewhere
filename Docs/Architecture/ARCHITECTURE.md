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

At the completed Group `03` checkpoint, the Phase 4 seam contained thirteen registered definitions, five passive RoomViews, and eight directed Passage instances. All eight belonged to four fully template-certified reciprocal pairs—Entrance/Drawing, Drawing/Music, Music/Library, and Library/Ballroom—and serialized `AuthoredAnchors = 2`. `RoomNavigationManager` remained the sole room-state owner behind `INavigationService`; `RoomView` and `Passage` remained validation/reporting seams rather than additional activation owners. The accepted room-side anchors were Entrance `(-7.75, -2.22)`, Drawing `(5.267176, -2.104616)`, Drawing `(-7.16, -1.78)`, Music `(-7.94, -3.27)`, Music `(7.714471, -3.121709)`, Library `(-7.744175, -3.059095)`, Library `(7.95, -3)`, and Ballroom `(-8.607888, -2.439877)`. That checkpoint retained 37 fully null direct-dependency sets and 37 null canonical callers. Its exact next step was the tests-only Group `04` characterization now recorded in the current checkpoint below.

At the Group `02` characterization checkpoint, its two four-component trigger owners retained exact hierarchy, sibling order, rectangles, standard Door profile, `145`-pixel proximity, null serialized dependencies/callers, and zero Passage components; runtime resolution reused the existing navigation manager, Player, shared door AudioSource, and catalog. Music retained RoomView `4100000003`, while Library had no canonical definition/view/passage yet. The Library chair/lamp blocker, both room boundaries, and Chapter 2 hide anchors at Library `(-255, -181)` and Music `(306, -162)` remained part of the contract. At `1366x768`, far Music -> Library approached `(7.439471, -2.846709)` and landed at the source-sensitive Library result `(-7.287828, -2.936489)`, while the immediate near path landed at `(-7.244175, -2.799095)`; reverse approach/arrival were `(-7.244175, -2.799095)` / `(7.439471, -2.846709)`. Null/left/center/right candidates converged exactly, four aspect ratios retained their locked viewport-dependent values, and `2560x1080` maximum zoom `1.22` preserved forward approach/arrival `(8.625211, -3.301599)` / `(-8.3582, -3.229418)` and reverse `(-8.356381, -3.228665)` / `(8.676323, -3.178201)`. Both locked runs shared seven-line observation-message SHA-256 `f2a4b15ccee94282032102f6b6c93a2673444d5d8d099a8bd1f24be6190fc2ab` using `\n` separators with no trailing newline. That characterization changed no runtime, scene, data asset, prefab, `.meta`, GUID, serialized reference, hierarchy, geometry, topology, dependency, caller, or anchor. Focused gates passed `10/10`, safety passed `28/28`, rendered lifecycle passed `6/6`, and the full suite remained `256` discovered / `210` passed / the same `46` known failures with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`; architecture/serialization/Y-axis audits remained 112 / 48,789 / 48 / 155 / zero hard errors / 38 tracked.

Group `02` is now `data-authored`. `Room_Library.asset` owns stable ID `room.library`, primary/display `Library`, scene background GUID `0a85e4fdd73e4714fabde63002a457e7`, null profile, and asset GUID `8da3a3e936712e7b9f534786110323e4`. Directed Door / `Open Door` assets use stable/legacy IDs `passage.music-room.library` / `MusicRoom_Library` at GUID `aefe77f20874eb81b83fccb6ff5b8046` and `passage.library.music-room` / `Library_MusicRoom` at GUID `3a641d5febbfd7aec481ada678ba9fe4`; they swap exact Music/Library endpoints and directly reverse-link. `GameDatabase` appends Library, Music -> Library, and Library -> Music once after the prior seven definitions, advancing `7 -> 10`. This slice adds only those three data assets and their new metas and changes the database plus matching tests/manifest/docs; it changes no production/runtime source, scene, prefab, existing `.meta`/GUID, RoomView, Passage component, trigger dependency, caller, or anchor. Focused gates pass `10/10`, safety passes `28/28`, rendered lifecycle passes `6/6`, and the full suite remains `256/210/46` with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture remains 112 runtime files / 48,789 lines / 48 direct `MonoBehaviour` declarations / 155 serialized-script rows; Y-axis remains zero hard errors / 38 tracked findings. Next, add exactly one passive RoomView to existing Library root `1367921344` and register it once with GameRoot; do not add either Passage component or change trigger dependencies/callers/anchors.

Group `02` is now `view-bound`. Existing Library root `1367921344` owns passive RoomView `4100000004`, bound to Library definition GUID `8da3a3e936712e7b9f534786110323e4` and existing content `2102000003`; GameRoot registers it once after Music RoomView `4100000003` and before Passage `4100000011`. The scene delta is exactly 16 added YAML lines with no removals, advancing Gameplay from `6,014` to `6,015` documents and from three to four RoomViews while retaining four Passages. No hierarchy, Transform, SceneRoots, trigger dependency/caller, Passage component, runtime source, prefab, asset, `.meta`, collider, prop, occlusion, activation, or camera ownership changes. Focused gates pass `10/10`, safety passes `28/28`, rendered lifecycle passes `6/6`, and the full suite remains `256/210/46` with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture remains 112 runtime files / 48,789 lines / 48 direct `MonoBehaviour` declarations / 155 serialized-script rows; RoomView scene references advance from three to four, and Y-axis remains zero hard errors / 38 tracked findings. Next, add only passive reciprocal Passage components `4100000015` and `4100000016` to the existing trigger owners and GameRoot. Serialize the characterized reciprocal reference coordinates at `LegacySampling = 0`, but do not bind dependencies/callers or transfer approach/arrival ownership in that slice.

Group `02` is now `passage-bound`. Music -> Library Passage `4100000015` and Library -> Music Passage `4100000016` are co-located with their existing trigger owners, registered after Passage `4100000014`, and directly reverse-link the existing Music/Library RoomViews and definitions. Music point `(7.439471, -2.846709)` and Library point `(-7.244175, -2.799095)` serialize reciprocally as finite validation data at `LegacySampling = 0`; production still owns neither approach nor arrival through these components. The distinct characterized far Library result `(-7.287828, -2.936489)` remains locked as proof that legacy source-sensitive placement is still authoritative. The current-schema scene delta is exactly 44 added YAML lines and no removals: two owner references, two GameRoot references, and two 20-line Passage documents. Gameplay advances `6,015 -> 6,017` documents and `4 -> 6` Passages while retaining four RoomViews. Both trigger documents, their null dependencies/callers, hierarchy, geometry, colliders, props, occlusion, activation, camera ownership, runtime source, prefabs, assets, and `.meta` files remain unchanged. Focused gates pass `10/10`, safety passes `28/28`, rendered lifecycle passes `6/6`, and the full suite remains `256/210/46` with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture remains 112 runtime files / 48,789 lines / 48 direct `MonoBehaviour` declarations / 155 serialized-script rows; Passage scene references advance from four to six, and Y-axis remains zero hard errors / 38 tracked findings. The next safe slice binds only the existing direct dependencies.

Group `02` is now `dependencies-bound`. Trigger documents `552135204` and `2300000079` each replace only four null references with navigation manager `1878886997`, Player Transform `81962843`, shared `Audio_DoorOpen` source `2201000013`, and door catalog GUID `9a77542e25184fbc945d6a79f77007e7`; stair catalogs remain null and canonical callers remain absent. The scene diff is exactly eight replacement lines added and eight removed, with 6,017 documents / four RoomViews / six Passages unchanged. Passages `4100000015/16`, their stage-0 coordinates/topology, GameRoot, owners, hierarchy, colliders, props, occlusion, activation/camera ownership, runtime source, prefabs, assets, and `.meta` files remain unchanged. Focused gates pass `10/10`, safety passes `28/28`, rendered lifecycle passes `6/6`, and the full suite remains `256/210/46` with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. The seven-line observation hash advances only because its profile records `serializedDependencies=bound runtimeDependencies=stable`, to `cb6e7c14702f7e9adcedfda1d5a0fba5f14462581c607d203c53152b5a3b40a7`; all six coordinate lines remain exact. Architecture remains 112 runtime files / 48,789 lines / 48 direct `MonoBehaviour` declarations / 155 serialized-script rows, and Y-axis remains zero hard errors / 38 tracked findings. Next, bind only the co-located canonical callers while retaining `LegacySampling = 0`.

Group `02` is now `caller-bound`. Trigger `552135204` gains only `canonicalPassage: 4100000015`; trigger `2300000079` gains only `canonicalPassage: 4100000016`. The exact scene delta is two added lines and no removals. Gameplay remains 6,017 documents / four RoomViews / six Passages; both Passages remain `LegacySampling = 0`, so canonical identity delegates to the byte-preserved legacy approach/arrival samplers. All dependencies, coordinates, Passage documents, GameRoot, component topology, hierarchy, colliders, props, occlusion, activation/camera ownership, runtime source, prefabs, assets, and `.meta` files remain unchanged. Rendered tests poison every passive coordinate by more than 100 units and independently null/restore both callers. The untouched primary still proves first-entry far Library `(-7.287828, -2.936489)` versus near `(-7.244175, -2.799095)`; both post-primary proofs retain the correct history-sensitive `(-7.244175, -2.799095)` Library result and exact eight-event/audio lifecycle. Focused gates pass `10/10`, safety passes `28/28`, rendered lifecycle passes `6/6`, and the full suite remains `256/210/46` with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. The seven-line observation hash changes only `callers=null` to `callers=bound`, to `46d857f57e7e41d6a7facaa3a39d0f97bf6dd4996d7f7d10e3da809d8eced2b7`. Architecture remains 112 runtime files / 48,789 lines / 48 direct `MonoBehaviour` declarations / 155 serialized-script rows, and Y-axis remains zero hard errors / 38 tracked findings. Next, run a test-only arrival calibration/preflight before changing either stage scalar.

The bounded Group `02` ownership batch is now authored through `complete`: arrival preflight, arrival ownership, approach preflight, approach ownership, and complete certification are represented together. Passages `4100000015` and `4100000016` both serialize `AuthoredAnchors = 2`; Music `(7.714471, -3.121709)` and Library `(-7.744175, -3.059095)` are authoritative in both reciprocal roles. The Library -> Music trigger alone changes proximity `145 -> 149` because the accepted Library point's worst rendered distance is `148.573` pixels; this is the minimal four-pixel pair-local compatibility calibration, while Music remains `145`. Apart from the two reciprocal anchor vectors, two stage scalars, and that one proximity scalar, the batch changes no GUID, `.meta`, topology, canonical caller, direct dependency, GameRoot registration, serialized object identity, or runtime source. The inventory now contains 6 complete, 34 queued, 2 blocked-one-way, and 3 blocked-parallel rows, and all six scene Passage components are stage 2. Focused contracts pass `10/10`, combined safety passes `28/28`, rendered lifecycle passes `6/6`, and the full suite remains `256` discovered / `210` passed / the same `46` known failures with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture/serialization/Y-axis audits remain 112 runtime files / 48,789 lines / 48 direct `MonoBehaviour` declarations / 155 serialized rows / zero hard errors / 38 tracked findings.

Group `03` Library <-> Ballroom is accepted through the first bounded five-change batch: characterization, Ballroom data, Ballroom RoomView, passive reciprocal Passages, and direct dependencies. Characterization locks Library `L0 = (7.465074, -2.665671)` and Ballroom `B0 = (-8.107888, -2.079877)` as the neutral reciprocal references while preserving the distinct first far Ballroom arrival `(-8.144631, -2.043134)`. Four rendered aspects plus widest-aspect maximum zoom were frozen before production edits by no-trailing-newline observation SHA-256 `39f311ffa5df816fe7a9b9d510bda7451109c35cb119b2540b26f5128a0e35db`; after direct dependency binding, the same seven coordinate lines change only the serialized-dependency profile token and hash to `ab7f0108d462d26d45571923d33ff88469a98fb3b8c5a192bc9142a54fae3017`. `GameDatabase` now contains 13 ordered definitions; Gameplay contains 6,020 documents, five RoomViews, and eight Passages, with stage counts `2 / 0 / 6` for stages `0 / 1 / 2`. Both new triggers retain `145`-pixel proximity and null canonical callers, so production remains entirely on the legacy sampling path even though their direct dependencies are serialized. The inventory is 6 complete, 2 dependencies-bound, 32 queued, 2 blocked-one-way, and 3 blocked-parallel. This batch changes no runtime source, prefab, or existing `.meta` file. Targeted rendered behavior passes `1/1`, focused contracts pass `11/11`, combined safety passes `29/29`, and rendered lifecycle passes `7/7`. The full suite is `258` discovered / `212` passed / the same `46` known failures with unchanged failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture/serialization/Y-axis audits remain 112 runtime files / 48,789 lines / 48 direct `MonoBehaviour` declarations / 155 serialized rows / zero hard errors / 38 tracked findings. The exact next slice binds only the two co-located canonical callers while keeping both Passages at stage 0.

Group `03` is now `caller-bound`. Trigger `2300000084` gains only Library -> Ballroom caller `4100000017`, and trigger `2101000025` gains only Ballroom -> Library caller `4100000018`, for an exact `+2/-0` scene delta. Gameplay remains 6,020 documents / five RoomViews / eight Passages, with stage distribution `2 / 0 / 6`; all eight characterized direct-dependency sets remain bound, 37 triggers retain fully null dependencies, eight canonical callers are bound, and 37 callers remain null. Both new Passages stay at `LegacySampling = 0`, both thresholds remain `145`, and canonical identity continues to delegate approach and arrival to the byte-preserved legacy samplers. Rendered proofs poison and restore all four passive coordinates, then null and restore only this pair's callers, without changing the frozen movement, room, audio, prompt, camera, visibility, or cleanup behavior. The seven observation lines change only `callers=null` to `callers=bound`, producing no-trailing-newline SHA-256 `22e969882f5c3481bd0957a46db3008ea556726bf395d30c1997f2f775e17118`. The inventory is 6 complete, 2 caller-bound, 32 queued, 2 blocked-one-way, and 3 blocked-parallel. No dependency, Passage document, GameRoot entry, topology, hierarchy, collider, set piece, occlusion, runtime source, prefab, asset, `.meta`, or GUID changes. Targeted/focused/safety/lifecycle gates pass `1/1`, `11/11`, `29/29`, and `7/7`; the full suite remains `258/212/46` with unchanged failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`; and architecture/serialization/Y-axis audits remain 112 / 48,789 / 48 / 155 / zero hard errors / 38 tracked findings. Next, run a tests-only reciprocal arrival calibration/preflight before changing either stage scalar.

The bounded Group `03` ownership batch is now authored through `complete`: arrival preflight, arrival ownership, approach preflight, approach ownership, and complete certification are represented together. Passages `4100000017` and `4100000018` both serialize `AuthoredAnchors = 2`; Library `(7.95, -3)` and Ballroom `(-8.607888, -2.439877)` are authoritative in both reciprocal roles. Both triggers retain `145`-pixel proximity. The exact points are walkable, path-reachable, non-projected, and inside their trigger envelopes from two far starts across all four rendered aspects and widest-aspect maximum zoom; worst measured distances are `123.877` and `106.234` pixels. Apart from the two reciprocal anchor vectors and two stage scalars, the batch changes no GUID, `.meta`, topology, canonical caller, direct dependency, threshold, GameRoot registration, serialized object identity, runtime source, prefab, or data asset. The authored seven-line observation hash is no-trailing-newline SHA-256 `ba483637abce4dc40b5c2910ff90a8ac9c364f686e1a7f4b45fd0c766b82848f`. The inventory now contains 8 complete, 32 queued, 2 blocked-one-way, and 3 blocked-parallel rows, and all eight scene Passage components are stage 2. Targeted/focused/safety/lifecycle gates pass `1/1`, `11/11`, `29/29`, and `7/7`; the full suite remains `258` discovered / `212` passed / the same `46` known failures with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture/serialization/Y-axis audits remain 112 runtime files / 48,789 lines / 48 direct `MonoBehaviour` declarations / 155 serialized rows / zero hard errors / 38 tracked findings. The exact next safe step is tests-only Group `04` Grand Entrance Hall <-> Dining Room characterization.

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

## Current Group 04 navigation checkpoint

Grand Entrance Hall <-> Dining Room is accepted at `dependencies-bound` in the bounded foundation batch. Dining definition GUID `0eb3282aded74fc4889f4321df8c5258` owns stable ID `room.dining-room`, background `004ab4cca930d0387892725fe69b4f72`, and its required non-null perspective profile `a63248cfbd6b4a72af45c62cff7e94d0`. Reciprocal definitions `30b5c4cfef2b45e2970b4cdac4b7a3ef` / `94e16c6eca714188bced397612d48fff`, RoomView `4100000006`, and Passages `4100000019` / `4100000020` preserve reference points Entrance `(8.205841, -1.986406)` and Dining `(-6.692237, -1.380209)` at stage `0`. Both triggers have direct dependencies, null canonical callers, and threshold `145`; behavior remains legacy sampling. Gameplay now has 6,023 documents, six RoomViews, ten Passages, eight stage-2 and two stage-0 Passages; `GameDatabase` has 16 definitions. The manifest is 8 complete, 2 dependencies-bound, 30 queued, 2 blocked-one-way, and 3 blocked-parallel. Pre-edit observation hash is `5479746e65e27c0d22985f3e02f9a9c6d77562d2497f5c743168f162c99200b3`; dependency binding changes only the profile token and yields `b30fa6610435667265b8b3c32965713f1af317ccddb9a5cce11618112dfcc578`. Targeted/focused/safety/lifecycle gates pass `1/1`, `12/12`, `30/30`, and `8/8`; the full suite remains `260/214/46` with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture/serialization/Y-axis audits remain `112 / 48,789 / 48 / 155 / zero hard errors / 38 findings`. Exact next step: bind only the two Group 04 canonical callers while both Passages remain stage `0`.

## Current Group 04 caller checkpoint

Grand Entrance Hall <-> Dining Room is now `caller-bound`. Trigger `340611600` binds co-located Passage `4100000019`, and trigger `2300000109` binds co-located Passage `4100000020`; no dependency, coordinate, Passage document, threshold, topology, hierarchy, collider, prop, occlusion, GameRoot registration, runtime source, prefab, data asset, `.meta`, or GUID changes. All ten characterized direct-dependency sets remain bound and 35 remain fully null; canonical callers likewise total `10 bound / 35 null`. The two Group 04 Passages remain at `LegacySampling = 0`, so the global stage distribution is `2 stage 0 / 0 stage 1 / 8 stage 2` and the canonical identities continue to delegate both approach and arrival to the unchanged legacy samplers. Gameplay remains 6,023 documents / six RoomViews / ten Passages, and `GameDatabase` remains 16 definitions. The seven-line observation profile changes only `callers=null` to `callers=bound`, producing no-trailing-newline SHA-256 `9519761fb43d7e8eb6f76edb036462d22595a607a8b5844bf9fa720874b76b66`. The manifest is 8 complete, 2 caller-bound, 30 queued, 2 blocked-one-way, and 3 blocked-parallel. The exact next safe step is tests-only reciprocal arrival calibration/preflight; do not change either stage scalar until those proofs identify a collision-safe pair across every rendered profile.

## Current Group 04 complete checkpoint

Grand Entrance Hall <-> Dining Room is now `complete` after the bounded five-change ownership batch: arrival preflight, arrival ownership, approach preflight, approach ownership, and template certification. Passages `4100000019` and `4100000020` both serialize `AuthoredAnchors = 2`; Entrance `(8.705841, -2.346406)` and Dining `(-7.192237, -1.740209)` are the authoritative reciprocal room-side points. Stage-1 rendered distance evidence was Entrance `23.943, 67.613, 33.707, 5.751, 6.619` and Dining `23.943, 58.835, 33.694, 59.736, 68.753`; all accepted probes remained exact, path-reachable, walkable, non-projected, and inside their unchanged trigger envelopes. The global threshold profile remains 44 triggers at `145` pixels and one at `149`; Group 04 changes no threshold.

Gameplay remains 6,023 documents / six RoomViews / ten Passages, `GameDatabase` remains 16 definitions, and all ten Passages are now stage 2 (`0 / 0 / 10` for stages `0 / 1 / 2`). Direct dependencies and canonical callers remain `10 bound / 35 null`. The manifest is 10 complete, 30 queued, 2 blocked-one-way, and 3 blocked-parallel, with no `dependencies-bound` or `caller-bound` rows. The no-trailing-newline authored seven-line observation SHA-256 is `93a264ab16817e8864461da376c9631ae36ae3808f770bee5d5094f89a60e509`. Targeted/focused/safety/lifecycle gates remain `1/1`, `12/12`, `30/30`, and `8/8`; the full suite remains `260/214/46` with unchanged failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. The exact next safe step is tests-only Group `05` Dining Room <-> Butlers Pantry characterization; production data, scene bindings, and ownership remain unchanged until that baseline passes.

## Accepted Group 05 foundation checkpoint (historical)

Dining Room <-> Butlers Pantry is accepted at `dependencies-bound` after the bounded five-change foundation batch. Butlers Pantry definition GUID `f2e9016bf08c45ebba8600eabc9e0b4d` owns stable ID `room.butlers-pantry`, exact legacy-name order `Butlers Pantry` then `Butler's Pantry`, background GUID `e73e44419d3782452bb6abd0e8edd452`, and a null perspective profile. Reciprocal definitions `1dedaedb6c544e9e8ca4fd2a5be912cf` / `d42e018868914021a713f19df8fe60e8`, passive RoomView `4100000007`, and Passages `4100000021` / `4100000022` preserve Dining `(2.891918, 0)` and Butlers Pantry `(-4.663103, -3.103186)` as reciprocal stage-0 values. Both triggers have direct dependencies, null canonical callers, and threshold `145`, so behavior remains on legacy sampling.

Gameplay now has 6,026 documents, seven RoomViews, twelve Passages, ten stage-2 and two stage-0 Passages; `GameDatabase` has 19 definitions. Direct dependencies total `12 bound / 33 null`, while canonical callers remain `10 bound / 35 null`. The manifest is 10 complete, 2 dependencies-bound, 28 queued, 2 blocked-one-way, and 3 blocked-parallel. Pre-edit observation hash is `32c4e3227cf5135f264c1b6f720a771ff3a5333a4cec1c032954bed69f5f5dbb`; dependency binding changes only the profile token and yields `319285f885882c452f985bcb6eafb3a69d79065efc1476a990ade17094610123`. Targeted/focused/safety/lifecycle gates pass `1/1`, `13/13`, `31/31`, and `9/9`; the full suite remains `262/216/46` with unchanged failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`; architecture/serialization/Y-axis audits remain `112 / 48,789 / 48 / 155 / zero hard errors / 38 findings`. This batch changes no runtime source, prefab, existing `.meta`/GUID, hierarchy, collider, set piece, prop, occlusion, activation/camera owner, canonical caller, threshold, or authored-anchor ownership. Exact next step: bind only the two Group `05` canonical callers while both Passages remain stage `0`.

## Current Group 05 complete checkpoint

Dining Room <-> Butlers Pantry is now `complete` after one bounded caller-plus-five-ownership batch: bind callers `2300000119 -> 4100000021` and `2300000139 -> 4100000022`, run arrival preflight, transfer arrival ownership, run approach preflight, transfer approach ownership, and certify the pair. Passages `4100000021` and `4100000022` both serialize `AuthoredAnchors = 2`; Dining `(3.391918, -0.36)` and Butlers Pantry `(-5.163103, -3.463186)` are authoritative in both reciprocal roles. The five rendered distances for Dining are `23.943, 38.849, 33.662, 38.87, 44.737`; the five for Butlers Pantry are `23.943, 83.785, 33.733, 27.022, 31.101`. Every accepted probe is exact, path-reachable, walkable, non-projected, and within its unchanged `145`-pixel trigger envelope; the worst result is `83.785` pixels.

Relative to the foundation scene, this bounded batch adds the two caller lines and replaces the four reciprocal anchor-coordinate lines plus two stage lines, for scene numstat `8 insertions / 6 deletions`. It changes no GUID, `.meta`, topology, dependency, threshold, GameRoot registration, serialized object identity, runtime source, prefab, data asset, hierarchy, collider, set piece, prop, occlusion, activation, or camera ownership. Gameplay remains 6,026 documents / seven RoomViews / twelve Passages, `GameDatabase` remains 19 definitions, all twelve Passages are stage 2 (`0 / 0 / 12`), and dependencies/callers are each `12 bound / 33 null`. The manifest is 12 complete, 28 queued, 2 blocked-one-way, and 3 blocked-parallel. The no-trailing-newline authored seven-line observation SHA-256 is `78267865829752279aaa796771f8d51c92b0fac44dc2fe93a4dd12885ccf2d7e`.

Final Group `05` gates pass targeted `1/1`, focused `13/13`, safety `31/31`, rendered lifecycle `9/9`, and full `262/216/46` with unchanged failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture/serialization/Y-axis audits pass at `112 runtime files / 48,789 lines / 48 direct MonoBehaviour declarations / 155 serialized rows / zero hard errors / 38 findings`. Human review must confirm that Dining and Butlers Pantry landings place the Butler's feet naturally at both painted doorway sides at default and maximum zoom. The exact next safe step is tests-only Group `06` Butlers Pantry <-> Billiard Room characterization; do not author Billiard Room data or scene ownership before that baseline passes.

## Accepted Group 06 characterization checkpoint (historical)

Group `06` Butlers Pantry <-> Billiard Room is now `characterized` without a production, scene, asset, prefab, `.meta`, GUID, serialized-reference, hierarchy, geometry, blocker, cutout, topology, dependency, caller, or anchor change. The six completed reciprocal pairs remain exactly as summarized above: `GameDatabase 19`, Gameplay `6,026` documents / seven RoomViews / twelve Passages, stages `0 / 0 / 12`, and direct dependencies/callers each `12 bound / 33 null`. The route manifest is now `12 complete / 2 characterized / 26 queued / 2 blocked-one-way / 3 blocked-parallel`.

The forward owner is GameObject `1505671644`, RectTransform `1505671645`, trigger `1505671646`, under Butlers Pantry root `2300000020` / content `2300000022` / `Doors` `2300000024`; its exact rectangle is position `(304.7408, 0.153)`, size `(187.9324, 422.4507)`. The reverse owner is GameObject `2300000130`, RectTransform `2300000131`, trigger `2300000134`, under Billiard root `2300000010` / content `2300000012` / `Doors` `2300000014`; its exact rectangle is position `(565, 52.91918)`, size `(120, 333.8383)`. Both are unchanged standard Door triggers with threshold `145`, null serialized manager/player/audio/catalog dependencies, no `CanonicalPassage`, and runtime-resolved legacy dependencies.

Butlers Pantry retains background GUID `e73e44419d3782452bb6abd0e8edd452`, its existing passive RoomView, and a null perspective profile. Billiard Room retains background GUID `5987c5a8b3a09fc1ca848ac0ece03658`, a null perspective profile, and no RoomView or canonical definition. Its three navigation blockers and three corresponding foreground cutouts remain owned in place: `PlayerBlocker_billiard_table` / `billiard_table`, `PlayerBlocker_billiard_left_armchair` / `billiard_left_armchair`, and `PlayerBlocker_billiard_left_lamp_table` / `billiard_left_lamp_table`. `doors.txt` contains neither `Butlers_Pantry_BilliardRoom` nor `BilliardRoom_ButlersPantry`; therefore the two exact Inspector routes are authoritative and must not be regenerated from that incomplete text graph.

The seven rendered observation records lock primary `1366x768`, aspects `1366x768`, `1440x1080`, `1920x1080`, `2560x1080`, widest-aspect maximum zoom `1.22`, and the structural profile. Their no-trailing-newline SHA-256 is `528af110846bebb59cc866eb6f94f14e3db1e4fdaad971434eec2e057e9b872e`. At primary, forward approach/arrival are `(2.744461, -2.748338)` / `(6.575521, -1.484375)` and reverse approach/arrival are `(6.575521, -1.484375)` / `(5.191498, -2.748338)`; near results match those arrivals. The four aspect neutral forward/reverse approach/arrival pairs are `1366: (2.744461, -2.748338)/(6.575521, -1.484375)/(6.575521, -1.484375)/(5.191498, -2.748338)`, `1440: (2.376193, -2.37955)/(5.693179, -1.285192)/(5.693179, -1.285192)/(4.494872, -2.37955)`, `1920: (2.743791, -2.747668)/(6.573917, -1.484013)/(6.573917, -1.484013)/(5.19023, -2.747668)`, and `2560: (3.168257, -3.172733)/(7.590901, -1.71359)/(7.590901, -1.71359)/(5.993162, -3.172733)`. Maximum zoom retains the `2560` room-side values (forward null differs only at the final float digit: `3.168258`; reverse null `7.590905`). The exact literal seven lines are preserved in `MIGRATION_REPORT.md` and summarized in `PASSAGE_COMPATIBILITY_TEMPLATE.md`.

Final Group `06` characterization gates pass targeted `1/1`, focused `14/14`, safety `32/32`, rendered lifecycle `10/10`, and full `264/218/46` with unchanged failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture/serialization/Y-axis audits pass at `112 runtime files / 48,789 lines / 48 direct MonoBehaviour declarations / 155 serialized rows / zero hard errors / 38 findings`. Human review must still confirm the legacy Butlers Pantry/Billiard approach and arrival framing, doorway foot placement, active-room visibility, camera, and the three Billiard cutout/blocker occlusion relationships. The exact next safe step is the bounded Group `06` foundation batch only: author Billiard Room data, bind one passive RoomView, add two passive reciprocal stage-0 Passages, and bind the two direct dependency sets. Do not bind canonical callers or transfer arrival/approach ownership in that batch.

## Accepted Group 06 foundation checkpoint (historical)

Group `06` Butlers Pantry <-> Billiard Room is now authored at `dependencies-bound` through one bounded five-change foundation batch: accept the characterized baseline, author Billiard Room data, bind one passive Billiard RoomView, add two passive reciprocal stage-0 Passages, and bind both direct-dependency sets. `Room_BilliardRoom.asset` GUID `bed158a9affd015fcc961340d9be5dd8` owns stable ID `room.billiard-room`, primary/display name `Billiard Room`, background GUID `5987c5a8b3a09fc1ca848ac0ece03658`, and a null perspective profile. Standard Door / `Open Door` PassageDefinitions use GUIDs `71ea8ce4d4eb8fa7f107abe24d7c903e` for Butlers Pantry -> Billiard Room and `be2f1b94b724dcfa061876e33bce02ca` for the reverse route.

Existing Billiard root `2300000010` now owns passive RoomView `4100000008`, bound to legacy content `2300000012`. Passages `4100000023` / `4100000024` are co-located with triggers `1505671646` / `2300000134`, directly reverse-link the Butlers Pantry and Billiard views/definitions, and serialize the reciprocal passive points Butlers Pantry `(2.744461, -2.748338)` and Billiard Room `(6.575521, -1.484375)` at `LegacySampling = 0`. Both triggers bind only the existing navigation manager, Player Transform, shared door AudioSource, and door catalog. Their canonical callers remain null, both thresholds remain `145`, and production approach/arrival sampling remains entirely legacy-owned.

`GameDatabase` now has 22 definitions. Gameplay has 6,029 documents / eight RoomViews / fourteen Passages with stages `2 / 0 / 12`, direct dependencies `14 bound / 31 null`, and callers `12 bound / 33 null`. The route manifest has `12 complete / 2 dependencies-bound / 26 queued / 2 blocked-one-way / 3 blocked-parallel`. Relative to the characterized scene the bounded foundation delta is exactly `68 insertions / 8 deletions`; it adds only the passive view/passages/registrations and replaces the eight null direct-dependency lines. It changes no production source, prefab, existing `.meta`/GUID, canonical caller, authored-anchor stage, threshold, hierarchy, rectangle, collider, blocker, foreground cutout, prop, occlusion, activation, camera ownership, or exceptional topology.

The seven rendered records retain every characterized coordinate and event line. Only the profile token changes from `serializedDependencies=null` to `serializedDependencies=bound`, advancing the no-trailing-newline observation SHA-256 from `528af110846bebb59cc866eb6f94f14e3db1e4fdaad971434eec2e057e9b872e` to `b6fbb280fed2f2310b32de4cefa85ce8dbb7ed5ebfebae72b55951ba0026093f`. Final Group `06` foundation gates pass targeted `1/1`, focused `14/14`, safety `32/32`, rendered lifecycle `10/10`, and full `264/218/46` with unchanged failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture/serialization/Y-axis audits pass at `112 runtime files / 48,789 lines / 48 direct MonoBehaviour declarations / 155 serialized script records / zero hard errors / 38 findings`. Human review still must confirm doorway foot placement, camera/visibility, and all three Billiard cutout/blocker relationships. The exact next safe step is caller-only binding `1505671646 -> 4100000023` and `2300000134 -> 4100000024` while both Passages remain at stage `0`.

## Current Phase 1.5 canonical-data checkpoint

All 19 approved Gameplay room identities now have one canonical RoomDefinition. The 11 definitions added in Slice `1.5` reuse their exact scene background textures and compatibility spellings, but remain passive data until their dedicated Phase 2 RoomView/Passage slices. `GameDatabase` preserves its original 22 ordered references and appends those 11 once, for exactly 33 definitions (`19` rooms / `14` passages).

GameDatabase exposes read-only typed room/passage collections and `RoomId`/`PassageId` lookup only. Duplicate valid IDs fail closed, cross-room aliases fail validation, and every Passage endpoint/reverse must be the exact registered object. No generic string locator or parallel owner was introduced. Gameplay remains byte-identical at 6,029 documents / 19 legacy room owners / eight RoomViews / fourteen Passages / 45 legacy triggers; no scene, prefab, existing meta/GUID, route source, caller, anchor, or presentation behavior changed.

Slice `1.5` passes static controls at `113` runtime files / `50,212` lines / `48` direct MonoBehaviours, focused EditMode `38/38`, and rendered cold start `1/1` with exact visual digest `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`. The exact next safe step is Phase `2.1`: reuse the already-authored Group `06` definitions/views/passages and finish caller plus reciprocal anchor ownership/certification without changing its geometry, threshold, blockers, or cutouts.

## Accepted Slice 2.1 Group 06 complete checkpoint (historical)

Group `06` Butlers Pantry <-> Billiard Room is now `complete`. Triggers `1505671646` and `2300000134` call their co-located Passages `4100000023` and `4100000024`; both Passages serialize `AuthoredAnchors = 2`. Butlers Pantry `(3.244461, -3.108338)` and Billiard Room `(6.9, -1.6)` are the authoritative reciprocal approach/arrival points. Both standard-Door thresholds remain `145`.

Gameplay remains `6,029` documents / eight RoomViews / fourteen Passages, with all fourteen Passages at stage 2. `GameDatabase` remains 33 definitions. Direct dependencies and canonical callers are each `14 bound / 31 null`; the route inventory is `14 complete / 26 queued / 2 blocked-one-way / 3 blocked-parallel`. The no-trailing-newline authored profile SHA-256 is `cd248f01301448b5cd807cc9331e58d99bd59a139bb772dab352869527b9a6eb`; Passage `4100000023` hashes to `61ae1a519da8a47275e2713b3182b7297c049b0eb9b2d590f5cb5037607abcc2`, and Passage `4100000024` hashes to `b46ec8f0b48730348e80287e50393c153ce05621c35db00741dfd7af50e6b1ce`.

Slice `2.1` gates pass targeted `1/1`, focused `14/14`, safety `32/32`, rendered lifecycle `10/10`, focused PlayMode `1/1`, and cold-start PlayMode `1/1` with unchanged digest `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`. This slice changes no runtime source, prefab, definition, `.meta`, or GUID. Human review must still confirm Butler foot placement at both painted doorway sides, active-room visibility and camera framing, and all three Billiard foreground-cutout/blocker occlusion pairs. The exact next safe slice is tests-only Group `07` Butlers Pantry <-> Service Corridor characterization; do not author or bind Group `07` production ownership before that baseline passes.

## Accepted Slice 2.2-07 Group 07 complete checkpoint (historical)

Group `07` Butlers Pantry <-> Service Corridor is now `complete`. The legacy reciprocal traversal was locked first with no-trailing-newline six-line SHA-256 `1a2d83cdf5e9cce175cf16333875f183629c6e67fe8d82241479bae79b4e8150`, then the replacement was authored in the same ownership shape as the accepted passage template. Existing Service Corridor definition GUID `85d51b6fcb4840458d45f66bbf6c233b` is now represented by passive RoomView `4100000009`; new reciprocal PassageDefinitions use GUIDs `1b2d5f64523942a08e10402e24e88738` / `b485e8a6f574414a84f77437e02147f1` and stable IDs `passage.butlers-pantry.service-corridor` / `passage.service-corridor.butlers-pantry`. Existing triggers bind co-located Passages `4100000025` / `4100000026`, their direct dependency sets, and canonical callers. Both Passages serialize `AuthoredAnchors = 2`.

Butlers Pantry `(7, -2.8)` and Service Corridor `(4.2, -3.3)` are the authoritative reciprocal approach/arrival points. Across `1366x768`, `1440x1080`, `1920x1080`, `2560x1080`, and widest-aspect maximum zoom, Pantry distances are `18.338 / 66.306 / 25.839 / 52.36 / 60.263` and Service distances are `16.712 / 71.262 / 23.565 / 29.137 / 33.535`. Every accepted point is exact, walkable, path-reachable, non-projected, and inside the unchanged `145`-pixel threshold; the worst observations are Pantry `66.306` and Service `71.262`. The authored six-line SHA-256 is `7cc7c8706a02443b30c91c7a3cdcce40b7c04fec2f54e3f2eb7d5acad7058ea9`.

Gameplay now has `6,032` YAML documents / nine RoomViews / sixteen Passages / 45 triggers. `GameDatabase` has `35` definitions (`19` rooms / `16` passages); Passage stages are `0 / 0 / 16`; direct dependencies and canonical callers are each `16 bound / 29 null`. The route inventory is `16 complete / 24 queued / 2 blocked-one-way / 3 blocked-parallel`. Exact SHA-256 evidence is Gameplay `c48e6f4a68cd311e958677136b572e06c05266ca556153dafd8e2656ffb1d30c`, inventory `47440df151a302a10493d14f2258c470a4a07d8683b858caf73f1c82e07c8c15`, and GameDatabase `155e065c03ec20c6b7d5f8e0a8e1746d1da166d74dc8d8c4214db8f7446989f1`. Legacy `doors.txt` remains byte-identical at `8dc956b84e8436054a8835a7fa7f12f0aa2ce14d1d9a90701e8d98c3f001798e`; its two Group07 entries remain required compatibility data.

Static architecture, GUID/meta, serialized-reference, ledger, and Unity `6000.4.10f1` compilation gates pass. Relevant EditMode execution accounts for 71 passing cases: 67 accepted first-pass cases plus four isolated test-only correction reruns. The real Group07 PlayMode round trip passes `1/1`, and independent cold start passes `1/1` with unchanged digest `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`. This slice prunes nothing: the legacy catalog entries, Service Corridor left-table/right-desk cutouts, both matching movement blockers, and `PlayerBoundary` remain protected content. Human review must confirm Butler foot placement at both doorway sides, camera/active-room visibility, prompt/cursor clearing, and the two cutout/blocker occlusion relationships. The exact next safe slice is tests-first Group `08` Service Corridor <-> Kitchen characterization; do not author its replacement ownership before that legacy behavior is locked.

## Accepted Slice 2.2-08 Group 08 complete checkpoint (historical)

Group `08` Service Corridor <-> Kitchen is now `complete`. Its legacy reciprocal behavior remains locked by six-line SHA-256 `9af5d16f40f2f186d02a777fb53ac80fd28fe898e0e8953db9455d047a5432de`. Existing Kitchen definition GUID `70531cbf9a67476f81f54b528029132e` is now represented by passive RoomView `4100000010`; new reciprocal PassageDefinitions use GUIDs `2985cbdd527b4faaec13ff03091dbcd1` / `453ad73cf2df1107f56be7a00daa3145` and stable IDs `passage.service-corridor.kitchen` / `passage.kitchen.service-corridor`. Existing triggers `2300000164` / `802263367` bind co-located Passages `4100000027` / `4100000028`, their direct dependency sets, and canonical callers. Both Passages serialize `AuthoredAnchors = 2`.

This is the first production pair to use the Slice `2.2-08a` RoomView-local coordinate mode. Service Corridor `(589.9897, -419.25894)` and Kitchen `(-478.36285, -156.76599)` are invariant native room-side approach/arrival points rather than aspect-dependent player-logical values. Across `1366x768`, `1440x1080`, `1920x1080`, `2560x1080`, and widest-aspect maximum zoom, both points remain finite, exactly walkable, path-reachable, non-projected, within the unchanged `145`-pixel trigger envelope, and round-trip to the same RoomView-local coordinates. The five rendered records hash, with no trailing newline, to `6d5f83aa399b12d8945a660e0372f65206c2c04a52e2657422ea92966fe0393b`.

Gameplay now has `6,035` YAML documents / ten RoomViews / eighteen stage-2 Passages / 45 triggers. `GameDatabase` has `37` definitions (`19` rooms / `18` passages); direct dependencies and canonical callers are each `18 bound / 27 null`. The route inventory is `18 complete / 22 queued / 2 blocked-one-way / 3 blocked-parallel`. Exact SHA-256 evidence is Gameplay `5203d054870b01b2e6db55286bc4a2d6b25ee6795cb84765306728090b4e1d98`, inventory `b692f1333f0271903fd9ff60e20323ce628311f37592a481a23fad142b468ae3`, and GameDatabase `9b9b4ae428340a9fd6597e18873adbdce9b3a675786dbee568adb9a595d710b1`. Legacy `doors.txt` remains byte-identical at `8dc956b84e8436054a8835a7fa7f12f0aa2ce14d1d9a90701e8d98c3f001798e`; both Group08 entries remain required compatibility data.

Architecture-tool tests pass `4/4`; architecture guard and the exact `113/113` runtime ledger pass; script integrity passes with `164` current scripts / `1,947` serialized references / `856` external-package references; and all `7,360` asset `.meta` GUID records are paired and globally unique. Unity `6000.4.10f1` compiles without errors. All 76 relevant EditMode cases are accounted: 74 passed in the consolidated launch and two focused test-only corrections passed separately. The genuine Group08 round trip plus independent cold start pass `2/2`, with unchanged cold digest `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`. This slice prunes nothing: both legacy catalog rows, the Service Corridor cutouts/blockers and `PlayerBoundary`, and Kitchen's work-table cutout/blocker and room boundary remain protected content. Human review must confirm Butler foot placement at both painted door sides, prompt/cursor cleanup, camera/active-room visibility, and both rooms' collision/occlusion relationships. At that checkpoint, the exact next safe slice was tests-first Group `09` Service Corridor <-> Chapel characterization.

## Accepted Slice 2.2-09 Group 09 complete checkpoint (historical)

Group `09` Service Corridor <-> Chapel is now `complete`, starting from clean rollback commit `07bb61fff50f63dfdc36acf244b391b26fa48d61`. Its pre-authoring reciprocal behavior remains locked by no-trailing-newline six-line SHA-256 `9574b82e5099f96bdaef11dd20bdbab386f5b54baf9ad06278b40af7a58449b2`. Existing Chapel RoomDefinition GUID `e3102dbfecc44551b6443ca88625a924` now owns passive RoomView `4100000029`; reciprocal PassageDefinitions use GUIDs `fc2a0af2de3f4ade831c53f64fe0271b` / `47e06869bf2b47a2980b0d02a53ee1df` and stable IDs `passage.service-corridor.chapel` / `passage.chapel.service-corridor`. Existing triggers `2300000169` / `2300000179` bind co-located Passages `4100000030` / `4100000031`, their direct dependency sets, and canonical callers. Both Passages serialize `AuthoredAnchors = 2`, remain standard Doors at the unchanged `145`-pixel threshold, and directly reverse-link in scene and data.

The authoritative reciprocal RoomView-local points are Service Corridor `(-133.2642, -171.8258)` and Chapel `(461.4019, -190.7613)`. Across `1366x768`, `1440x1080`, `1920x1080`, `2560x1080`, and widest-aspect maximum zoom, both anchors remain finite, exact, walkable, path-reachable, non-projected, within threshold, and round-trip invariant. The accepted authored rendered fingerprint is `bff3067e14fd0710f8de6dd4f7e4434d9025a2b7593adf3cd54571020a622475`. Chapel's room hierarchy, background, null perspective profile, three bench cutout/blocker pairs, three additional foreground cutouts, 18-point `PlayerBoundary`, and `Ch2_Hide_Guest06` anchor remain unchanged.

Gameplay now has `6,038` YAML documents / eleven RoomViews / twenty stage-2 Passages / 45 triggers. `GameDatabase` has `39` definitions (`19` rooms / `20` passages). GameRoot registers exactly `40` scene behaviours and retains `48` total serialized references including its eight services. Direct dependencies and canonical callers are each `20 bound / 25 null`; the route inventory is `20 complete / 20 queued / 2 blocked-one-way / 3 blocked-parallel`. Sixteen Passage documents retain legacy-logical coordinate serialization and the four Group08/09 Passage documents use certified RoomView-local anchors. Exact SHA-256 evidence is Gameplay `220009ae5f8d2bbe3a3e62ccab6d9c88da56a4c66d9dde092da11f0cae8b8a93`, inventory `43a8134256b39291e7954141ef15d11c3a179b735f65f6437088779dd8abbc2c`, and GameDatabase `90734ca890e57a4b64062e10a7e2c2e04f281dc783beb96f012c2fa673674ebc`. Legacy `doors.txt` remains byte-identical at `8dc956b84e8436054a8835a7fa7f12f0aa2ce14d1d9a90701e8d98c3f001798e`; both Group09 rows remain required compatibility data.

Architecture and serialization controls pass at `113` runtime files / `50,339` lines / `48` direct MonoBehaviours / `113/113` ledger coverage, `164` scripts, and `1,952` serialized script references (`1,096` project / `856` external-package). All `7,362` asset `.meta` GUID records are paired and globally unique, and Unity `6000.4.10f1` compiles without errors. The focused Group09 EditMode gate passes `4/4`, the final affected architecture suite passes `62/62`, rendered lifecycle passes `1/1`, and the real Group09 route plus independent cold start pass `2/2`. This slice prunes nothing. Human review must confirm Butler foot placement at both painted doorway sides, camera/active-room visibility, prompt/cursor cleanup, all Service Corridor cutout/blocker/boundary behavior, and Chapel's cutout/blocker/boundary/hide-anchor presentation. The exact next safe slice is tests-first Group `10` Grand Entrance Hall <-> Grand Entrance Hall Rear, including explicit characterization and canonical handling of the existing room-name case alias.

## Accepted Slice 2.2-10 Group 10 checkpoint (historical)

Group `10` Grand Entrance Hall <-> Grand Entrance Hall Rear is accepted from clean rollback commit `f91ee2338261ee061a8502e06542fd49b5d81831`. Its reviewed pre-authoring behavior remains frozen by the no-trailing-newline six-line SHA-256 `9bc19b299d38e9403dcf93da79336a0e45307ec7820f4c36214931b77c46b638`; that legacy fingerprint remains before-state evidence and is not relabeled as the canonical structure. Existing rear RoomDefinition GUID `64bc36c6e2d546d6bb878373c4e6d0b6` is represented by RoomView `4100000032`. Reciprocal PassageDefinitions use GUIDs `aa8a2282356d4ad0aa3c9499a6f6f064` / `d57bc53c2dfb4a10bd63739d37028899`; scene Passages `4100000033` / `4100000034` are co-located with existing triggers `1858342503` / `70736571`, bind their direct dependencies and canonical callers, directly reverse-link, and serialize stage `2`.

This bottom-edge pair deliberately owns destination **regions**, not invented point arrivals. Entrance -> Rear approaches at RoomView-local `(0.00030518, -456.4991)` and resolves inside the Rear region `BL (-764.707458, -451.0935) / TL (-764.707458, -423.094452) / TR (785.200256, -423.094452) / BR (785.200256, -451.0935)`. Rear -> Entrance approaches at `(10.2463989, -437.093964)` and resolves inside the Entrance region `BL (-835.9997, -470.4991) / TL (-835.9997, -442.4991) / TR (836.0003, -442.4991) / BR (836.0003, -470.4991)`. The forward definition explicitly preserves compatibility destination spelling `Grand Entrance Hall Rear View`, while the existing rear primary alias `Grand Entrance Hall Rear view` remains valid. Neither Group10 legacy door ID occurs in `doors.txt`; the file remains byte-identical at `8dc956b84e8436054a8835a7fa7f12f0aa2ce14d1d9a90701e8d98c3f001798e`.

The accepted state is Gameplay `6,041` YAML documents / twelve RoomViews / twenty-two stage-2 Passages / 45 triggers; `GameDatabase` has `41` definitions (`19` rooms / `22` passages); GameRoot registers `43` scene behaviours and has `51` serialized references including eight services. Direct dependencies and callers are each `22 bound / 23 null`; inventory is `22 complete / 18 queued / 2 blocked-one-way / 3 blocked-parallel`. Exact hashes are Gameplay `867e9395e38b4a2e675d5e0fc6abbf9449cb03f4e75ab2c3b4397172efde44ae`, GameDatabase `90417c118dfc1cda7bcb3cf17c1d92fda30af7433be7ae446162146925e2c39a`, inventory `3291b6e856a252d78411de186e76f818dda12bd9b63cae1972bfd7001187aa5d`, and unchanged `doors.txt` `8dc956b84e8436054a8835a7fa7f12f0aa2ce14d1d9a90701e8d98c3f001798e`.

Static controls pass at `114` runtime files / `51,062` lines / `48` direct MonoBehaviours / `114/114` runtime-ledger rows, `165` scripts, `1,957` serialized script references (`1,101` project / `856` external-package), and `7,365` paired, globally unique `.meta` GUID records. Unity `6000.4.10f1` compiles; focused EditMode passes `4/4`, affected architecture passes `67/67`, rendered Group10 passes `1/1` with the exact locked SHA, and genuine Group10 plus independent cold-start PlayMode passes `2/2`. The final resolver preserves legacy ordering, validates the serialized RoomView-local region, and projects its bit-exact corners from the canonical reverse Passage RectTransform without consulting `DoorTriggerNavigation`. Human visual review remains. No deletion is authorized: retain both triggers, all compatibility paths, the 49-point Entrance and 5-point Rear boundaries, and the four railing cutouts. The exact next safe slice is tests-first Group `11` Grand Entrance Hall Rear <-> Billiard Room characterization, including the `doors.txt` endpoint conflict; do not author Group11 production ownership before that conflict is locked.

## Accepted Slice 2.2-11 Group 11 checkpoint (historical)

Group `11` Grand Entrance Hall Rear View <-> Billiard Room is accepted from clean rollback commit `9ed538fa209c239f50675dbc0b9a04c0bbaa01ea` after its separate characterization and source-region prerequisite commits. The six-line legacy Inspector-route fingerprint remains byte-for-byte frozen at SHA-256 `56e7cc4b59801f8dfb9da547ff584f7b184bea0d15398e7a0b31f85b83b1b39b`. PassageDefinition GUIDs `cd0978fc337c41b982afb4b46c7a2b3c` / `ef375ba8c3744447add18ebec1fd1a83` own stable IDs `passage.grand-entrance-hall-rear-view.billiard-room` / `passage.billiard-room.grand-entrance-hall-rear-view`; Passages `4100000035` / `4100000036` are co-located with triggers `357269799` / `2300000124`, bind only their existing dependencies and callers, directly reverse-link RoomViews `4100000032` / `4100000008`, and serialize stage `2`.

Both directions use `BestReachableInSourceRegion = 1` and `BestReachableInDestinationRegion = 1`, with no point approach or arrival anchor. Rear -> Billiard owns the Billiard destination region `BL (-745.00006, -114.72981) / TL (-745.00006, 238.13548) / TR (-501.32404, 238.13548) / BR (-501.32404, -114.72981)`. Billiard -> Rear owns the Rear destination region `BL (579.6167, -250.84499) / TL (579.6167, 31.911606) / TR (702.0674, 31.911606) / BR (702.0674, -250.84499)`. Each source approach reuses the reciprocal region through the validated Passage-owned policy, so there is one serialized geometry owner and no fabricated fixed point. The reverse definition explicitly preserves compatibility destination spelling `Grand Entrance Hall Rear View`; `doors.txt` remains the unchanged conflicting front-Entrance catalog rather than being silently rewritten.

The accepted state is Gameplay `6,043` YAML documents / twelve RoomViews / twenty-four stage-2 Passages / 45 triggers; `GameDatabase` has `43` definitions (`19` rooms / `24` passages); GameRoot registers `45` scene behaviours and has `53` serialized references including eight services. Direct dependencies and callers are each `24 bound / 21 null`; inventory is `24 complete / 16 queued / 2 blocked-one-way / 3 blocked-parallel`. Exact hashes are Gameplay `ae7a5c400b4edc9a874331bb54a1684d759239f9ccbb03975a3975b3e6649585`, GameDatabase `2c8f1dfbb90fc81f8032e3f8c3010dc3e394e0e2a397f4a46cc34d0b5a05c9a0`, inventory `c5cec68785b0699aedcd8a5dae46ecbb14a7a94ae969d04e1ac75a70cc05d4fe`, and unchanged `doors.txt` `8dc956b84e8436054a8835a7fa7f12f0aa2ce14d1d9a90701e8d98c3f001798e`.

Static controls pass at `114` runtime files / `51,224` lines / `48` direct MonoBehaviours / `114/114` runtime-ledger rows, `165` scripts, `1,961` serialized script references (`1,105` project / `856` external-package), and `7,367` paired, globally unique `.meta` GUID records. Unity `6000.4.10f1` compiles. The accepted focused EditMode gate passes `70/70`; the broader 113-case navigation/architecture run exposed one corrected Group11 fixture count and ten unchanged historical navigation failures, none in the new ownership seam. Rendered legacy/canonical parity passes `2/2`, and genuine Group11 plus representative Group10/09 routes and independent cold start pass `4/4`. Cold start retains digest `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`, Butler height `114.417 px`, entrance-door height `141.481 px`, ratio `0.808710`, and sort `1075`. No deletion is authorized. Human review remains for both landings, prompt/cursor and door audio, camera/visibility, Billiard cutout/blocker occlusion, and collision. The exact next safe slice is tests-first Group `12` Grand Entrance Hall Rear View <-> Conservatory characterization, including its mixed standard-door/bottom-edge behavior and exact scaled RectTransform geometry.

## Accepted Slice 2.2-12 Group 12 checkpoint

Group `12` transfers the live Grand Entrance Hall Rear View <-> Conservatory Inspector route onto the canonical scene/data graph without changing its mixed interaction semantics. Existing trigger `1119941194` remains the standard-door side (`GEH_Conservatory`), and trigger `2300000074` remains the immediate bottom-edge side (`Conservatory_GEH_Rear_View`). Conservatory RoomDefinition GUID `78d9317381ab411e8adb1aa6c7386263` is represented by RoomView `4100000037`. PassageDefinitions `2388aec2b64647e2a7b6c50c3ee3c8b6` / `d54f1f34f2fb45428117d7b831c0ef40` own stable IDs `passage.grand-entrance-hall-rear-view.conservatory` / `passage.conservatory.grand-entrance-hall-rear-view`; Passages `4100000038` / `4100000039` are co-located with the existing triggers and directly reverse-link RoomViews `4100000032` / `4100000037`.

Both Passages serialize the shared Passage-owned source-region policy and destination-region placement, with no point anchors. The standard-door direction consumes its source region for the characterized proximity walk. The bottom-edge trigger remains immediate and therefore does not consume its authored source region: it gains no proximity walk or movement callback. Exact region corners come from the reviewed Unity-transformed trigger rectangles, including the scaled Conservatory bottom edge. The legacy catalog remains byte-identical: it still places `GEH_Conservatory` under the front Grand Entrance Hall and contains `Conservatory_GEH -> Grand Entrance Hall`, while the live Inspector route is Rear View and uses a different reverse ID.

The accepted shape is Gameplay `6,046` YAML documents / thirteen RoomViews / twenty-six stage-2 Passages / 45 triggers; `GameDatabase` `45` definitions (`19` rooms / `26` passages); GameRoot `48` scene behaviours / `56` total references including eight services; direct dependencies and callers `26 bound / 19 null`; and inventory `26 complete / 14 queued / 2 blocked-one-way / 3 blocked-parallel`. Exact hashes are Gameplay `3138c43665353b0a3a10336ce36ab3c8aa72386ddb5c46f7d46ed683c33bac00`, GameDatabase `7d7520fb992cf2c39ba1ef6b696d4a2a49148c9c208205129b46593a1e14957d`, inventory `8e626c25cbca2e26d7390149540daf6745187707048ce40fc3eaf30e4f31f795`, and unchanged `doors.txt` `8dc956b84e8436054a8835a7fa7f12f0aa2ce14d1d9a90701e8d98c3f001798e`.

Static controls pass at `114` runtime files / `51,224` lines / `48` direct MonoBehaviours / `114/114` runtime-ledger rows, `165` scripts, `1,966` serialized references (`1,110` project / `856` external-package), unchanged smell totals `106/17/67/51/4/6`, and `7,369` paired, globally unique `.meta` GUID records. Unity `6000.4.10f1` compiles in `compile.log`. The first focused EditMode run is preserved as a real `72 total / 71 passed / 1 failed` test-literal failure; the isolated corrected case passes `1/1`, accounting for all `72`. Rendered Group12 plus locked Group11 parity passes `2/2` in `rendered-group12-and-group11.xml`, retaining SHAs `b371eb16186010923ac6d6c63851a5fe0e0de0cf642ac3099b4fab2b93db1fa8` and `56e7cc4b59801f8dfb9da547ff584f7b184bea0d15398e7a0b31f85b83b1b39b`. Genuine Group12, representative Group11/10, and independent cold start pass `4/4` in `playmode-group12-representatives-cold.xml`. Group12 lands in Conservatory local `(33.322, -451.0936)` and Rear local `(0.000015, -138.5048)`, with forward movement stop `(0.43388, -1.839959)`. Cold start retains SHA `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`, Butler `114.417 px`, entrance door `141.481 px`, ratio `0.808710`, presentation `0.7528645`, and sort `1075`.

No pruning is authorized. Both compatibility triggers, the conflicting catalog rows, prompt/cursor/audio behavior, the Rear five-point boundary and rail cutouts, the Conservatory seven-point boundary, and all four Conservatory cutout/blocker pairs remain required. Human review remains for the standard-door approach and landing, immediate bottom-edge return, Butler feet, prompt/cursor/audio cleanup, camera and active-room visibility, and all protected collision/occlusion relationships. The exact next safe slice is tests-first Group `13` Service Corridor <-> Side Stair & Mudroom characterization from the forthcoming Group12 commit; no resulting commit hash is predeclared.

## Accepted Slice 2.2-13 Group 13 checkpoint

Group `13` transfers the reciprocal Service Corridor <-> Side Stair & Mudroom route to the canonical scene/data graph. Passive Side Stair & Mudroom RoomView `4100000040` joins the existing Service Corridor RoomView. PassageDefinition GUIDs `0491e7071cda47e7b779cf87f71d026e` / `5c0c635bc6c04da19e0909a6f81d0caf` own stable IDs `passage.service-corridor.side-stair-mudroom` / `passage.side-stair-mudroom.service-corridor`; Passages `4100000041` / `4100000042` are co-located with existing triggers `2300000174` / `2300000184`, directly reverse-link, and serialize stage `2`. Compatibility identity remains exact across `Service Corridor`, `ServiceCorridor_SideStairMudroom`, `Side Stair & Mudroom`, `Side Stair Mudroom`, and `SideStairMudroom_ServiceCorridor`.

Both directions use `BestReachableInSourceRegion = 1` and `BestReachableInDestinationRegion = 1`; neither invents a point approach or arrival anchor. Service Corridor -> Side Stair & Mudroom owns destination region `BL (-569.47998, -470.50003) / TL (-569.47998, -338.82755) / TR (836.02002, -338.82755) / BR (836.02002, -470.50003)`. The reverse owns the Service Corridor destination region `BL (52.839996, -166.62186) / TL (52.839996, 188.62186) / TR (172.84, 188.62186) / BR (172.84, -166.62186)`. The source approach in each direction reuses the reciprocal Passage-owned region, preserving standard-door behavior without duplicating geometry.

The accepted shape is Gameplay `6,049` YAML documents / fourteen RoomViews / twenty-eight stage-2 Passages / 45 triggers; `GameDatabase` `47` definitions (`19` rooms / `28` passages); GameRoot `51` scene behaviours / `59` total references including eight services; dependencies and callers `28 bound / 17 null`; and inventory `28 complete / 12 queued / 2 blocked-one-way / 3 blocked-parallel`. Exact hashes are Gameplay `82e6b51a51cc3a5cf34afe6cfb26dfe35a292cafbdd6a3dd58e6221cbac3d119`, GameDatabase `13f8161b0a5f107e984c3c856bdc9f75bb922f2c3082370de571f412a42ab5f3`, inventory `2aaba6eed5c2bd28026dcca5dedb863b2ecbf646574da168799c1249b9c2fc29`, and unchanged `doors.txt` `8dc956b84e8436054a8835a7fa7f12f0aa2ce14d1d9a90701e8d98c3f001798e`.

Architecture tools pass `4/4`; static controls pass at `114` runtime files / `51,224` lines / `48` direct MonoBehaviours, exact `114/114` ledger coverage, smells `106/17/67/51/4/6`, `165` scripts / `1,971` serialized references (`1,115` project / `856` external), and `7,371` paired, globally unique `.meta` GUID records. Unity `6000.4.10f1` compiles in `Logs/ArchitectureOverhaul/Slice-2.2-13/compile.log`. Preserve the honest focused sequence: `73/60/13`, then `17/14/3`, then `3/3`; together the correction runs account for all 73 focused cases. The synchronized full lifecycle characterization fixture also passes exact `19/19`. The initial rendered `3/1/2` run passed Group13 and failed only the two prior locked fixtures on stale structural-count literals; their isolated corrections pass `2/2`. Locked SHAs are Group13 `14315ebb94e0991564058de63ed42aad056f85b3578c8af626acc4e75c1dab3c`, Group12 `b371eb16186010923ac6d6c63851a5fe0e0de0cf642ac3099b4fab2b93db1fa8`, and Group11 `56e7cc4b59801f8dfb9da547ff584f7b184bea0d15398e7a0b31f85b83b1b39b`. Genuine Group13, representative routes, and independent cold start pass `5/5`; cold SHA `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96` and Butler scale/presentation metrics remain unchanged.

No pruning is authorized. Retain both triggers, all compatibility routes, prompt/cursor/audio behavior, and Service Corridor / Side Stair & Mudroom collision and occlusion content. Human review remains for feet and landings, prompt/cursor/audio cleanup, camera and active-room visibility, and both rooms' collision/occlusion relationships. The exact next safe slice is tests-first Group `14` Side Stair & Mudroom <-> Upper Sitting Hall characterization, including its mixed standard-door and inferred-stairway behavior; no resulting commit hash is predeclared.

## Accepted Slice 2.2-14 Group 14 checkpoint

Group `14` transfers Side Stair & Mudroom <-> Upper Sitting Hall onto the canonical scene/data graph while preserving the asymmetric interaction contract. Upper Sitting Hall RoomView `4100000043`, PassageDefinitions `d5c370a383ecaf7257b8f796d0164e72` / `77a3efbd4a250afa71e5998bb7118532`, and Passages `4100000044` / `4100000045` bind existing triggers `1915714844` / `2300000189`. Upper -> Side remains a standard Door / `Open Door`; Side -> Upper resolves as an upward Stairway / `Use Stairway` even though its legacy serialized fields remain `Door` / `Auto`. Compatibility spellings `Side Stair & Mudroom` and `Side Stair Mudroom` remain distinct, deliberate aliases.

Both directions are stage `2`, use Passage-owned reciprocal source regions and destination regions, and serialize no point anchors. Upper -> Side owns `BL (253.170868, -202.263962) / TL (253.170868, 236.559952) / TR (471.909576, 236.559952) / BR (471.909576, -202.263962)`. Side -> Upper owns `BL (70.8670044, -32.7176476) / TL (70.8670044, 130.0000458) / TR (129, 130.0000458) / BR (129, -32.7176476)`. The route keeps its exact door/stair prompts, cursors, movement, event order, and audio split; Passage owns identity/topology/placement but does not become a second room-state or interaction owner.

The accepted shape is Gameplay `6,052` documents / fifteen RoomViews / thirty stage-2 Passages / 45 triggers; `GameDatabase 49` (`19` rooms / `30` passages); GameRoot `54` scene behaviours / `62` references; dependencies/callers `30 bound / 15 null`; and inventory `30 complete / 10 queued / 2 blocked-one-way / 3 blocked-parallel`. Exact hashes are Gameplay `7b802932693726a6a1c94736c715b9ffc03b53c0d31cb247db0d9cacb1082407`, GameDatabase `7bec731d33b2bd1631adfa842533f91af072523d5e9b81b0ef7822a3f8facfef`, inventory `d1c630d178b6c7ce8db765b590273b93eff3ea6372fcec503071f7ec786dd64a`, and unchanged `doors.txt` `8dc956b84e8436054a8835a7fa7f12f0aa2ce14d1d9a90701e8d98c3f001798e`.

Static controls pass at `114` runtime files / `51,224` lines / `48` direct MonoBehaviours / `114/114` ledger rows, `165` scripts / `1,976` serialized references (`1,120` project / `856` external), and `7,373` paired unique meta GUIDs. Unity compiles; focused EditMode passes `74/74`; the complete rendered lifecycle fixture passes `20/20` with Group14 SHA `8feac87c6e631543491757c0bab3f8f16383733f1bad102419bd1af4f57f357c` and unchanged Group13 SHA `14315ebb94e0991564058de63ed42aad056f85b3578c8af626acc4e75c1dab3c`; Group14/representatives/cold PlayMode passes `5/5`. The expanded PlayMode fixture is accounted as `13/12/1` plus isolated unchanged Chapter 2 golden `1/1`. Cold SHA and Butler scale metrics remain exact.

No pruning is authorized. Human review remains for mixed stair/door interaction, both approach stops and landings, Butler feet, prompt/cursor/audio cleanup, camera/active-room visibility, and protected Side Stair / Upper Sitting collision and occlusion content. The exact next safe slice is tests-first Group `15` Upper Sitting Hall <-> Upper Gallery characterization.
