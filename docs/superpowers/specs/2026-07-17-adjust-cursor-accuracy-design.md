# Adjust Cursor Accuracy Design

## Goal

Make the cursor icon and the action performed by a click agree everywhere, with particular protection for Chapter 1 guest coats and the entrance coat hanger.

The interaction priority is:

1. Blocking UI
2. Guest coats and guest interactions
3. Coat hanger and other specific scene actions
4. Doors and stairways
5. Floor movement

## Confirmed Problems

- `PointClickPlayerMovement` and `DoorTriggerNavigation` defer to Chapter 2 guest actions, but they do not check Chapter 1 coats or scene actions. A floor or door handler can therefore process the same pointer position as a coat or hanger.
- `NavigationCursorController` retains only one hover owner. An overlapping owner can replace it, and clearing that owner loses the previous valid hover request instead of restoring it.
- Chapter 1 coats and scene actions independently process EventSystem callbacks, `OnMouse` callbacks, and manual pointer polling. These paths use different timing and can attempt different actions for one click.
- Coats and the hanger receive runtime `BoxCollider2D` clickboxes, but coat setup does not explicitly re-enable an existing disabled collider. Hanger setup only recalculates bounds when it creates a new collider.

## Chosen Design

### Deterministic cursor arbitration

`NavigationCursorController` will retain all active hover requests instead of one last-writer owner. Each request has an explicit priority. It will display the highest-priority active request and restore the next request automatically when the winner clears.

UI requests outrank gameplay. Guest/coat requests outrank scene actions. Scene actions outrank navigation. Floor movement remains the fallback. Equal-priority requests retain stable registration order so script update order cannot make the icon flicker.

The controller will expose whether a particular owner currently wins. Click handlers can therefore require the same ownership that selected the displayed icon.

### Shared Chapter 1 pointer priority

Chapter 1 coats and scene actions will expose their existing screen-space hit tests through one shared query. The query selects one target at the approved priority: a visible guest coat first, then the coat hanger or another Chapter 1 scene action.

`PointClickPlayerMovement` and `DoorTriggerNavigation` will call this query before showing their cursor or processing a click. They will yield when a Chapter 1 target owns that pointer position, matching the Chapter 2 priority pattern already in the project.

### One authoritative click decision

Manual screen-space polling remains authoritative for scaled room-stage objects because it already follows their visible bounds. EventSystem and `OnMouse` callbacks may forward into the same guarded activation method, but they will not independently decide what target wins. Frame-level duplicate suppression ensures one physical click produces at most one action.

A coat or hanger activates only when it is both under the pointer and the selected priority owner. The icon shown and action executed therefore come from the same target.

### Clickboxes

Every active guest coat and the authored entrance coat hanger will have an enabled trigger `BoxCollider2D` sized from its visible sprite bounds, with the existing fallback size used only when no usable sprite bounds exist. The collider supports Unity pointer callbacks, while the screen-space bounds remain the zoom-safe authority.

No scene YAML edit is required; runtime setup continues to attach these properties to authored objects without disturbing their visual transforms.

## Scope

The change may touch the cursor arbiter, Chapter 1 coat and scene-action handlers, floor-click filtering, door filtering, coat/hanger runtime collider setup, and regression tests.

It will not change character scale, camera zoom, movement destinations, coat story state, room navigation rules, cursor art, or `Gameplay.unity`.

## Verification

- A failing regression first demonstrates that clearing an overlapping hover restores the remaining higher-priority request and that update order does not choose the icon.
- Pointer-priority tests verify coats beat the hanger, the hanger beats doors, and all three beat floor movement.
- Interaction tests verify one click dispatch, enabled and sprite-sized coat/hanger colliders, and matching cursor/click ownership.
- Existing navigation, Chapter 1, cursor-style, character-presentation, and coat-zoom regression suites run after the fix.
- A focused PlayMode smoke test checks guest-coat pickup and coat-hanger storage at normal and zoomed room-stage scales.
