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

The Phase 4 navigation seam now contains seven registered definitions, three passive RoomViews, and four directed Passage instances across two fully template-certified reciprocal pairs: Entrance/Drawing and Drawing/Music. `RoomDefinition` and directed reciprocal `PassageDefinition` carry stable data identity; `RoomView` only validates/reports its existing root; and `Passage` validates/reports its definition, source room, reverse link, logical anchors, and one temporary per-pair anchor-migration stage. The existing `RoomNavigationManager` remains the sole room-state owner behind `INavigationService`: canonical current room is derived from its one legacy room string and registered database. All four current Passages serialize `AuthoredAnchors = 2`, and all four corresponding inventory rows are now `complete`. The real Drawing/Music callers therefore treat calibrated Drawing `(-7.16, -1.78)` and Music `(-7.94, -3.27)` as authoritative reciprocal approach/arrival points. Their acceptance contract evaluates each exact non-projected point from two far starts at all four rendered aspects and widest-aspect maximum zoom, drives one real production movement command per direction and aspect, both maximum-zoom directions, and a symmetric stale-destination replay, then protects near synchronous traversal, null-Passage fallback, and the complete lifecycle side-effect sequence. A prerequisite fix synchronizes destination-room collider transforms before exact arrival validation because Unity's automatic 2D transform synchronization is disabled; this prevents a room stage last seen at zoom `1.22` from leaving a stale collider cache during same-call placement. Relative to that prerequisite, the accepted approach-ownership scene slice changed only two stage scalars and altered no caller, direct dependency, coordinate, definition, topology, GameRoot entry, runtime script, prefab, asset, `.meta`, or GUID. The later complete-certification bookkeeping changes no scene, runtime source, prefab, asset, `.meta`, GUID, serialized reference, topology, coordinate, caller, or dependency and passes the complete reciprocal-pair contract plus every repository gate. This temporary 0/1/2 gate keeps caller, arrival, and approach cutovers independently testable and is deleted only after every Passage in the full migration reaches the complete stage. `PointClickPlayerMovement.TryWarpToExact` validates authored destinations without clamping, projection, or an old-room path requirement. Trigger input, proximity, hover/prompt, movement callbacks, and audio ownership remain on the compatibility interaction owner. The other 41 scene triggers retain null direct dependencies and null canonical callers. All resolvers remain, and no `RoomViewService` activation writer exists. The exact next safe phase is tests-only characterization of Group `02`, Music Room <-> Library.

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
