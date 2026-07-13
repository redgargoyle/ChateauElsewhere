# Hamza TL;DR cards — final overhaul

## Card 1 — The entire game

```text
GameRoot
├── Story: decides what should happen
└── Game: records what physically happens
```

## Card 2 — Story

```text
GameFlowService
→ Chapter
→ Beat
→ Objective
→ Story interaction requirement
```

Story never pathfinds, moves transforms, creates UI, or searches the scene.

## Card 3 — Game

```text
Game
├── World
│   └── House
│       └── Rooms
│           ├── Actors
│           └── Props
├── Scheduler / Clock
└── Save
```

## Card 4 — Room transition

```text
click
→ InteractionRouter
→ PassageInteraction
→ NavigationService
→ RoomViewService
→ CameraService
→ player arrival anchor
→ RoomChanged event
```

One current-room writer. One active-room writer.

## Card 5 — Actor

```text
ActorController
├── RoomStageMotor
├── ActorPresenter
├── ActorAnimator
├── ActorAudioEmitter
└── command source
```

One motor. One presenter. Guests have no player input.

## Card 6 — Set piece

```text
SetPieceView = sprite + depth
RoomNavigationGeometry = collision
```

A couch is data/content, not a `CouchManager`.

## Card 7 — Physical object versus story requirement

```text
DoorbellView belongs to Game.
“Answer the door” belongs to Story.
```

The object emits an event; the objective decides whether it matters.

## Card 8 — Replacement rule

```text
characterize
→ add replacement
→ migrate callers
→ prove parity
→ prove zero refs
→ delete legacy
```

Never add a permanent parallel system.

## Card 9 — Unity safety

```text
.meta + GUID + serialized fields are identity
```

Move with `git mv`. Use Editor migrations. No proof, no deletion.

## Card 10 — Test truth

```text
No result XML = no test run.
Zero discovered tests = failure.
```

Do not use `-quit` with `-runTests`.

## Card 11 — Failure response

```text
stop
→ reproduce
→ add a failing test
→ repair the authoritative owner
→ rerun
→ or revert the slice
```

Never bypass a gate.

## Card 12 — File explanation

For every remaining runtime file, answer:

1. Which requirement justifies it?
2. Who owns it?
3. What state does it own?
4. What enters?
5. What leaves or changes?
6. What may it depend on?
7. Which test protects it?
8. What disappears if it is deleted?

## Card 13 — Folder moves happen last

Architecture is ownership, not folder cosmetics. Move files and add asmdefs only after dependencies are stable.

## Card 14 — Definition of done

```text
no duplicate owners
no runtime repairs
no missing scripts
all passages work
one motor/presenter per actor
Chapters 1 and 2 pass
save/Continue pass
zero untriaged tests
complete prune proof
```
