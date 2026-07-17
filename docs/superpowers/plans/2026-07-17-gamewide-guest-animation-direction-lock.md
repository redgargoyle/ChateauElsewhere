# Game-Wide Guest Animation Direction Lock Plan

**Goal:** Apply the stable Chapter 1 guest-animation behavior to every playable chapter without changing movement, positions, scaling, sorting, dialogue, or staging.

**Architecture:** Story controllers choose one intended animation direction at the start of each authored movement segment. Physical pathing remains owned by the existing movement systems. All guest animation commands pass through `CharacterAnimationPresenter`; no movement system may switch direction because of incidental per-frame Y corrections.

## Implementation

1. Preserve Chapter 1's explicit entrance and drawing-room departure directions.
2. Make Chapter 2 hiding-place exits choose one direction from visible feet to the route door and pass it to the direction-locked `NPCWaypointMover` overload.
3. Ensure every Chapter 2 guest receives `CharacterAnimationPresenter`, including debug-skip and dining staging paths.
4. Preserve Chapter 2 panic's existing one-direction-per-route-segment behavior.
5. Confirm the Chapter 3 handoff only stages seated guests and has no separate movement controller requiring another animation writer.
6. Run focused architecture tests, relevant Chapter 2 regressions, Unity compilation, and a final diff audit.
