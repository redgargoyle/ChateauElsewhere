# Drawing Room Guest Ordering Design

## Problem

The selected green chair now renders in front of seated Guest 4, but the local
override lowers Guest 4 beneath Guest 2. This reverses their established visual
relationship. The Drawing Room tea table and all ordinary world-Y sorting must
remain unchanged.

## Approved Design

Keep the existing Guest 4 chair exception as the single late sorting owner for
this narrow seated cluster. While both guests are seated and visible in the
Drawing Room, it will preserve this strict back-to-front order:

1. Guest 2
2. Guest 4
3. Full green chair
4. Detached chair foreground rail

The exception will normalize Guest 4 immediately behind the chair and Guest 2
immediately behind Guest 4 while preserving each actor's internal renderer
order. It will capture and restore renderer layers, orders, pivot sort points,
and local `SortingGroup` states for both actors.

If Guest 2 is not seated and visible in the Drawing Room, the existing Guest 4
chair behavior remains active without overriding Guest 2.

## Scope Boundaries

- Do not modify the green chair's world-Y sorting order.
- Do not modify the tea table, its collider, its blocker, or its
  `ObjectMovementBlocker2D` sorting ownership.
- Do not add global sorting-layer or scene-wide order overrides.
- Do not change other Drawing Room seats or other rooms.
- Do not build executables.

## Verification

- Add a focused unit regression proving both actors' renderer and sorting-group
  state is restored exactly.
- Extend the live Chapter 2 Drawing Room regression to prove
  `Guest 2 < Guest 4 < chair < rail` across vertical camera pans.
- In the same live regression, prove the table's sorting order remains under
  `ObjectMovementBlocker2D` control and ordinary standing guests still cross in
  front of and behind it correctly.
