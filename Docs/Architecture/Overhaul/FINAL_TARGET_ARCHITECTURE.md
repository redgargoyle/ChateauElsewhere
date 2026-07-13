# Chateau Chantilly — final target architecture

## Central rule

> **Story decides what should happen. Game determines and records what physically happens. Physical interactions translate player intent into typed results. Game events report results back to Story.**

`GameRoot` is only the composition root. It owns no chapter, room, actor, objective, dialogue, or save state.

```text
                                  GameRoot
                     composition, validation, initialization
                                         |
                       +-----------------+-----------------+
                       |                                   |
                     STORY                                GAME
                       |                                   |
        +--------------+-------------+       +-------------+-------------+
        |                            |       |             |             |
   GameFlowService              StoryState  World       Scheduler       Save
        |                                      |             |
   ChapterController                         House          Clock
        |                                      |
     StoryBeat                              Room instances
        |                                      |
    Objective                         +---------+----------+
        |                             |                    |
Story interaction requirement      Actors                Props
        |                        +----+----+       +--------+---------+
        |                      Player   Guests   Passages  Set pieces Lights
        |                         |        |         |         |         |
        +--- typed request ------>|        |      anchors   depth +   LightView
                                  |        |               collision
          STORY <------ explicit events/results from GAME ------+
```

## Final ownership

| State or side effect | Sole owner |
|---|---|
| Current chapter, beat and objective state | `GameFlowService` + `StoryState` |
| In-game time | `ClockService` |
| Timed callbacks | `GameScheduler` |
| Current room and transition transaction | `NavigationService` |
| Active room root | `RoomViewService` |
| Room lookup/content registry | `WorldService` |
| Actor identity, room and logical pose | one `ActorControllerBase` per actor |
| Actor lookup and room membership index | `ActorRegistry` |
| Actor movement execution | one `RoomStageMotor` per actor |
| Actor position, scale, tint and sorting | one `ActorPresenter` per actor |
| Physical pointer/click/range/modal routing | `InputRouter` / `InteractionRouter` |
| Room camera framing and transition | `CameraService` |
| Game audio mixer, one-shots and ambience | `AudioService` |
| Dialogue queue, subtitle state and voice interruption | `DialogueService` |
| Runtime light state | `LightingService` |
| Runtime UI root and modal stack | `UIService` |
| Durable session state | `SaveService` |
| Cutout sprite and room-local depth | one `SetPieceView` per prop |
| Walkable boundaries and no-walk footprints | one `RoomNavigationGeometry` per room |

No second owner is acceptable without a written ADR, an expiration date and a tested removal slice.

## Final source tree

```text
Assets/_Chateau
├── Runtime
│   ├── Core
│   │   ├── Bootstrap
│   │   │   ├── GameRoot.cs
│   │   │   └── GameContext.cs
│   │   ├── Contracts
│   │   │   ├── IDs
│   │   │   ├── Commands
│   │   │   └── Events
│   │   ├── Data
│   │   ├── State
│   │   └── Validation
│   │
│   ├── Story
│   │   ├── GameFlow
│   │   │   ├── GameFlowService.cs
│   │   │   └── StoryState.cs
│   │   ├── Chapters
│   │   │   ├── ChapterControllerBase.cs
│   │   │   ├── StoryBeatBase.cs
│   │   │   ├── Chapter01
│   │   │   │   ├── Chapter1Controller.cs
│   │   │   │   └── Beats
│   │   │   └── Chapter02
│   │   │       ├── Chapter2Controller.cs
│   │   │       └── Beats
│   │   ├── Objectives
│   │   ├── Interactions
│   │   └── Presentation
│   │       ├── Dialogue
│   │       ├── Subtitles
│   │       └── UI
│   │
│   └── Game
│       ├── World
│       │   ├── WorldService.cs
│       │   ├── Navigation
│       │   │   ├── NavigationService.cs
│       │   │   ├── RoomViewService.cs
│       │   │   └── InteractionRouter.cs
│       │   ├── House
│       │   │   ├── Rooms
│       │   │   │   ├── RoomDefinition.cs
│       │   │   │   ├── RoomView.cs
│       │   │   │   ├── RoomAnchor.cs
│       │   │   │   └── RoomNavigationGeometry.cs
│       │   │   ├── Actors
│       │   │   │   ├── Core
│       │   │   │   ├── Player
│       │   │   │   ├── Guests
│       │   │   │   ├── Movement
│       │   │   │   └── Presentation
│       │   │   └── Props
│       │   │       ├── Doors
│       │   │       ├── SetPieces
│       │   │       ├── Lights
│       │   │       └── Environment
│       │   └── Presentation
│       │       ├── Camera
│       │       ├── Audio
│       │       └── UI
│       ├── Scheduler
│       │   ├── GameScheduler.cs
│       │   └── Clock
│       │       ├── ClockService.cs
│       │       ├── GameTime.cs
│       │       └── ClockView.cs
│       └── Save
│           ├── SaveService.cs
│           ├── SaveGameData.cs
│           └── SaveVersion.cs
│
├── Data
│   ├── GameDatabase.asset
│   ├── Story
│   └── Game
├── Prefabs
├── Scenes
├── Content
├── Editor
│   ├── Authoring
│   ├── Diagnostics
│   ├── Validation
│   └── Migration
└── Tests
    ├── EditMode
    └── PlayMode
```

The final project has no first-party dumping grounds named `Assets/Scripts`, `Assets/Map`, `Assets/_Chateau/Scripts` or root `Assets/Editor`. Those folders are removed only after all contained files have completed their ownership migration with `.meta` files preserved.

## Story execution

```text
ChapterDefinition
  -> GameFlowService selects ChapterController
  -> ChapterController enters one StoryBeat
  -> beat starts or updates one Objective
  -> objective listens for a typed Game result
  -> beat asks Game capability owners to act through explicit interfaces
  -> Game changes physical state
  -> Game publishes a typed result
  -> objective completes
  -> GameFlowService advances the beat
  -> SaveService records durable state
```

### Chapter 1

```text
Chapter1Controller
├── TitleAndSetupBeat
├── ArrivalScheduleBeat
├── GuestArrivalBeat
├── CoatServiceBeat
├── DrawingRoomAmbientBeat
├── EmptyDoorbellBeat
└── Chapter1CompletionBeat
```

### Chapter 2

```text
Chapter2Controller
├── DrawingRoomSetupBeat
├── PreSpeechBarksBeat
├── AddressGuestsBeat
├── MonsterStingerBeat
├── PanicAndScatterBeat
├── GuestSearchBeat
├── ClockStrikeBeat
├── DiningRoomTransitionBeat
└── DiningRoomRevealBeat
```

Dialogue variants, actor animation mappings and timing values are definitions, not additional state-machine owners.

## World transition

```text
Pointer/click
  -> InteractionRouter
  -> PassageInteraction
  -> NavigationService validates directed PassageDefinition
  -> current-room transition transaction begins
  -> RoomViewService switches room roots
  -> CameraService applies target room framing
  -> ActorPresenter places player at explicit arrival anchor
  -> NavigationService commits current room
  -> RoomChanged event is published
```

A passage is a directed edge. Reverse links are optional. One-way passages and parallel/shared-return stairs are authored explicitly; no implementation may infer “the first door that points back.”

## Actor composition

```text
ActorControllerBase
├── ActorState
├── RoomStageMotor
├── ActorPresenter
├── ActorAnimator
├── ActorAudioEmitter
└── command source
    ├── PlayerCommandSource
    └── GuestDecisionSource
```

The player and guests use distinct prefabs built from shared actor components. A guest prefab contains no player input components.

## Set pieces

A couch, desk, bed, toy, chair, table or foreground cutout is one room prop:

```text
SetPiece
├── SetPieceView
│   ├── cutout SpriteRenderer
│   ├── room-local occlusion anchor
│   └── sorting offset/profile
└── RoomNavigationGeometry
    └── authored no-walk footprint
```

`SetPieceView` never owns collision. Collision never changes rendering. `RoomDepthResolver` is pure room-local math.

## Data rules

- Stable IDs are separate from display names.
- `GameDatabase` contains all essential definitions through serialized references.
- Runtime code does not use arbitrary `Resources.Load` paths for essential dependencies.
- Definitions are immutable during play; mutable state lives in services/controllers.
- There is no generic service locator or global event bus. Cross-domain communication uses small explicit interfaces and typed events.

## Base-class rule

Base classes provide lifecycle, validation and invariants only. Feature behavior is composed. Maximum project-defined inheritance depth is three below a Unity base.

## Final acceptance conditions

- Unity compiles without errors or warnings introduced by the overhaul.
- EditMode and PlayMode suites have zero untriaged failures.
- MainMenu, Chapter 1 and Chapter 2 complete in an end-to-end demo run.
- Every directed passage works and uses explicit anchors.
- No missing scripts or unresolved script GUIDs exist.
- One owner exists for every state/side effect in the table above.
- No required manager, UI root, camera, actor component or data source is repaired at runtime.
- No guest contains player input components.
- Every actor has one motor and one presenter.
- `Chapter1ArrivalController`, the giant Chapter 2 feature controllers, `PointClickPlayerMovement`, `RoomNavigationManager`, `DoorTriggerNavigation`, `RoomContentGroup` and other facades are gone after proof.
- Every deleted file has a prune record with code, serialized and behavioral evidence.
- The final folder tree and assembly graph are acyclic and documented.
