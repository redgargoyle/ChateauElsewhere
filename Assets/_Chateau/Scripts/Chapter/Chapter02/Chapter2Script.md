# Chapter 2 Implementation Script

## Story Beats

### Beat 00 — Start black after Chapter 1

Chapter 2 begins from black immediately after Chapter 1 completes. The chapter transition request should come from `chapter_02` logic, not from Chapter 1 code.

### Beat 01 — Fade in on Drawing Room

Fade in with the current room set to the Drawing Room. The assembled guests should already be present in the Drawing Room state before the player regains input.

### Beat 02 — Butler stands before assembled guests

Place the Butler at `Ch2_ButlerSpeechSpot`. Guests are gathered in the Drawing Room, facing or implied to be attending the Butler.

### Beat 03 — Player prompt: Address the guests

Show a simple prompt or interaction affordance for the player:

`Address the guests`

### Beat 04 — Butler speech

When the player addresses the guests, the Butler says:

“Welcome friends and gentlemen, guests of the evening, Count and Countess of Chantilly—”

### Beat 05 — High-pitched screaming violin begins

A high-pitched screaming violin begins abruptly, cutting through the speech before the Butler can finish.

### Beat 06 — Monster charge

A long-haired arm-swinging beast on nine spider legs charges rightward from `Ch2_MonsterRunStart` for 1-2 random seconds while the screaming violin plays.

### Beat 07 — Monster freezes

The monster freezes for 1-2 random seconds. The violin stops at the same moment, leaving sudden silence.

### Beat 08 — Continue the run/freeze/silence cycle

Repeat until the monster has completed exactly three rightward runs and three freezes. Each run continues from the previous frozen position; the monster should not reset or move backward.

### Beat 09 — Guests scatter

Guests scream, panic, and scatter to hiding spots around the house. Assign each guest to one authored hiding anchor, `Ch2_Hide_Guest01` through `Ch2_Hide_Guest08`.

### Beat 10 — Player regains control

After the panic sequence ends, the player regains control of the Butler.

### Beat 11 — Main objective

Objective: find each guest, announce dinner at 7:00 PM, ask meal preference, ask cigar/pipe preference, and record spirits bottle info.

### Beat 12 — Find hidden guests

The player clicks/taps hidden guests to mark them found. Found guests should become visible/interactable only when their current room and chapter state allow it.

### Beat 13 — Favor order

The order in which guests are found creates a simple favor order for dinner seating and service priority.

### Beat 14 — Clock strikes 7:00 PM

When all guests are found, the clock strikes 7:00 PM.

### Beat 15 — Butler goes to Dining Room

The Butler must go to the Dining Room after the clock strikes 7:00 PM.

### Beat 16 — Guests seated

Guests are seated in found/favor order at `Ch2_DiningSeat_01` through `Ch2_DiningSeat_08`.

### Beat 17 — Fade out and request Chapter 3

Fade out to black and request `chapter_03_dinner_pending`.

## Implementation Rules

- No NavMesh.
- No behavior trees.
- No quest system.
- No dialogue editor.
- No inventory system.
- No full relationship system yet.
- No Chapter 3 implementation yet.
- Do not modify `Chapter1ArrivalController`.
- Use `ActorRoomState` for actor visibility.
- Use `RoomAnchor` for authored positions.
- Use `RoomNavigationManager` for current room.
- Use `PointClickPlayerMovement` for player input.
- Keep Chapter 2 logic in Chapter 2 files.

## Anchor Naming

- `Ch2_ButlerSpeechSpot`
- `Ch2_MonsterRunStart`
- `Ch2_MonsterFreezeTarget`
- `Ch2_Hide_Guest01` through `Ch2_Hide_Guest08`
- `Ch2_DiningSeat_01` through `Ch2_DiningSeat_08`
