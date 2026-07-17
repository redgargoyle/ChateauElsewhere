# Chapter 1 Guest Animation Choreography Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make all eight Chapter 1 guests use stable, explicit idle/walk directions throughout the entrance sequence, with no walk-left/walk-down jitter from floor-route corrections.

**Architecture:** `Chapter1ArrivalController` chooses the intended animation direction once for each authored story movement. `NPCWaypointMover` owns physical pathing and walk-versus-idle timing, but when Chapter 1 supplies a direction it must preserve that direction for the entire move instead of deriving it from per-frame transform deltas. `CharacterAnimationPresenter` remains the sole component that writes the canonical body Animator parameters.

**Tech Stack:** Unity 6000.4.10f1, C#, NUnit EditMode tests, Unity Animator.

## Global Constraints

- Do not change guest anchors, world positions, floor routing, visible-foot alignment, room-scale calculation, sorting, dialogue order, or coat behavior.
- Preserve each guest's authored Animator Override Controller and animation clips.
- Every Chapter 1 guest must have `CharacterAnimationPresenter`; player and ambient movement components remain disabled on guests.
- Do not modify or save `Assets/Scenes/Gameplay.unity` as part of this fix; its current local changes are outside this implementation.

---

### Task 1: Reproduce the direction-flip architecture in tests

**Files:**
- Modify: `Assets/Editor/CharacterAnimationArchitectureTests.cs`

**Interfaces:**
- Consumes: `NPCWaypointMover`, `CharacterAnimationPresenter`, and `Chapter1ArrivalController` source.
- Produces: regressions proving Chapter 1 supplies explicit directions and a noisy physical delta cannot override a forced left walk.

- [ ] Add a source-level test requiring the entrance move to pass a fixed down/right direction and the drawing-room departure to pass `CharacterWalkDirection.Left`.
- [ ] Add a behavior test that starts a forced-left mover routine toward a diagonally down-left target and asserts only `IsWalkingLeft` is true.
- [ ] Run the focused EditMode tests and confirm the new tests fail because the forced-direction mover overload and Chapter 1 wiring do not exist yet.

### Task 2: Add a per-move direction lock to the shared mover

**Files:**
- Modify: `Assets/Scripts/Story/NPCWaypointMover.cs`

**Interfaces:**
- Produces: `MoveTo(Transform, CharacterWalkDirection)` and `MoveToRoutine(Transform, CharacterWalkDirection)`.
- Preserves: existing `MoveTo(Transform)` and `MoveToRoutine(Transform)` automatic-direction behavior for non-Chapter-1 callers.

- [ ] Store an optional direction override for the active move.
- [ ] When the override is active, send only that direction to `CharacterAnimationPresenter` for both walking and paused/destination idle.
- [ ] Keep floor route and transform calculations unchanged.
- [ ] Run the focused behavior regression and confirm forced left remains left despite a negative Y component.

### Task 3: Make Chapter 1 explicitly choreograph every guest animation phase

**Files:**
- Modify: `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs`

**Interfaces:**
- Consumes: the forced-direction `NPCWaypointMover` overloads.
- Produces: door idle, fixed down-or-right entrance walk, anchor idle, fixed left pair departure, destination idle.

- [ ] Set each guest idle/down when placed at the front door.
- [ ] Choose the door-to-anchor direction once from the authored start/target displacement, restricted to down or right.
- [ ] Pass that fixed direction to the entrance movement.
- [ ] Pass `CharacterWalkDirection.Left` to both members of every drawing-room departure pair.
- [ ] Leave movement pause handling responsible only for switching the already-selected direction between walk and idle.
- [ ] Run the focused source and behavior regressions.

### Task 4: Verify sole ownership and regression safety

**Files:**
- Inspect: `Assets/Scripts/Characters/CharacterAnimationPresenter.cs`
- Inspect: `Assets/Scripts/Story/ActorRoomState.cs`
- Inspect: `Assets/Scripts/Characters/RoomPersonWalker2D.cs`
- Inspect: `Assets/Scripts/PointClickPlayerMovement.cs`
- Inspect: `Assets/Scripts/PlayerMovement.cs`

**Interfaces:**
- Produces: an audit showing Chapter 1 guests cannot receive competing animation commands.

- [ ] Confirm Chapter 1 installs `CharacterAnimationPresenter` on every guest.
- [ ] Confirm Chapter 1 disables `RoomPersonWalker2D`, `PointClickPlayerMovement`, `PlayerMovement`, and `CharacterController2D` on guests.
- [ ] Confirm `ActorRoomState` routes presenter-owned guests through `CharacterAnimationPresenter` rather than writing their Animator directly.
- [ ] Run Unity compilation, the EditMode suite, source architecture checks, and `git diff --check`.
- [ ] Review `git status --short` and keep the pre-existing `Gameplay.unity` modification out of this fix.
