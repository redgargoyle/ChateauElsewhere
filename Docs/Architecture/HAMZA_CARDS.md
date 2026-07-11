# Hamza architecture cards — TL;DR

Use these as teach-back cards. Read a card, close it, then redraw or explain it without looking.

---

## Card 1 — The mission

**We are not memorizing the old repository.**

We are deriving one clean implementation from the approved game script, migrating valid behavior into it, proving parity, and deleting the obsolete path.

```text
Requirements -> owners -> architecture -> tests -> migrate -> prove unused -> delete
```

---

## Card 2 — L0: the whole game

```text
Boot/Menu -> Session -> Chapter -> Beat -> Player action/world event
-> state change -> presentation -> completion -> next beat/chapter -> save
```

Draw this before naming any class.

---

## Card 3 — L1: capability map

```text
Flow | Clock | World/Rooms | Navigation | Actors | Interaction
Narrative | UI | Camera | Audio | Lighting | Save | Data | Validation
```

Every runtime behavior must belong to one capability.

---

## Card 4 — L2: composition root

```text
GameRoot
  -> GameContext
  -> GameDatabase
  -> service instances
  -> scene components
```

`GameRoot` serializes dependencies and initializes them once. It does not search for or invent required systems during play.

---

## Card 5 — L2: story path

```text
GameFlowService -> ChapterController -> StoryBeat -> typed command -> service
```

The controller reads like the chapter script. A beat coordinates; it does not build UI, pathfind, or create managers.

---

## Card 6 — L2: world path

```text
Interaction -> Passage -> NavigationService
-> RoomViewService -> CameraService -> actor arrival
```

There is one current-room owner and one room-visibility owner.

---

## Card 7 — L2: actor path

```text
ActorController
  +-- ActorMotor       movement execution
  +-- ActorPresenter   position/scale/tint/sort
  +-- ActorAnimator
  +-- ActorAudio
  +-- CommandSource    player clicks or NPC decisions
```

Player and guest share behavior components but use separate prefab variants.

---

## Card 8 — One-owner rule

For each important state, ask: **who alone may write it?**

- chapter/beat: Game Flow;
- time: Clock;
- room: Navigation;
- room visibility: Room View;
- actor state: Actor Controller;
- actor transform presentation: Actor Presenter;
- movement: Actor Motor;
- input/modal/cursor: Input Router;
- UI root: UI Service;
- dialogue queue: Narrative/Dialogue Service.

Two writers means a bug waiting to happen.

---

## Card 9 — What inheritance is for

```text
MonoBehaviour -> ChateauBehaviour -> role base -> concrete class
```

A base provides lifecycle or invariants. Feature behavior is composed. Do not create a giant base helper library.

---

## Card 10 — What the first patch changes

- adds the root, context, service lifecycle, base families, validation, data base, and state machine;
- makes major managers and chapter controllers enter those families without changing their Unity GUIDs;
- adds an Editor installer and tests;
- adds static architecture guards;
- removes two proven-unused scripts;
- leaves risky behavior migrations for gated Unity work.

---

## Card 11 — What the first patch deliberately does not claim

It does **not** claim that navigation, actors, chapters, UI, audio, or lighting are fully cleaned yet.

The patch creates a safe migration spine. Unity must compile, install the root, run tests, and verify gameplay before old bootstraps or parallel systems are removed.

---

## Card 12 — The vertical-slice rule

Do not rewrite an entire subsystem at once.

```text
one requirement -> one target path -> tests -> migrate references
-> smoke test -> remove old callers -> prove zero refs -> delete
```

Navigation is the recommended first slice.

---

## Card 13 — Deletion proof

Before deleting a Unity script, prove:

1. no code consumer;
2. no scene/prefab/asset GUID reference;
3. no UnityEvent or animation-event binding;
4. no reflection/resource naming dependency;
5. replacement behavior has tests;
6. target scene works in Play Mode;
7. rollback commit exists.

No proof, no deletion.

---

## Card 14 — How to review any file

Answer eight questions:

1. Which requirement justifies it?
2. Who owns it?
3. What state does it own?
4. What enters?
5. What leaves or changes?
6. What may it depend on?
7. Which test protects it?
8. What disappears if it is deleted?

Unclear answers reveal architecture debt.

---

## Card 15 — Chapter 1 from architecture

```text
GameFlow -> Chapter1Controller
-> title/setup -> arrival schedule -> guest arrival
-> coat loop -> ambient dialogue -> empty bell
-> completion gate -> Chapter 2 request
```

The beats call Clock, Navigation, Actor, Interaction, Narrative, UI and Save owners. They do not implement those capabilities.

---

## Card 16 — Chapter 2 from architecture

```text
GameFlow -> Chapter2Controller
-> setup -> address -> monster -> panic/scatter
-> guest search -> 7 PM -> dining room -> Chapter 3 pending
```

Guest responses are mostly data consumed by `GuestSearchBeat`, not separate global controllers.

---

## Card 17 — Test gate

Every phase ends with:

```text
static guard -> Unity compile -> EditMode tests -> PlayMode tests
-> scene/prefab missing-script scan -> manual gameplay trace -> commit
```

A failing gate means stop and fix the target path—not add another parallel system.

---

## Card 18 — Hamza certification

Hamza is ready to continue development when he can:

- draw L0, L1 and L2 from memory;
- trace five game actions through owners;
- place every major class in one capability;
- explain an unfamiliar file with the eight questions;
- identify the test and deletion proof for a class;
- design a new chapter beat without copying an old god controller.
