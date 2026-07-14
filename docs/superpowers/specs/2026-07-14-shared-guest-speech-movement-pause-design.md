# Shared Guest Speech Movement Pause Design

## Goal

Any guest whose non-overlapping speech line must wait in `DialogueSpeechService` stops moving in an idle pose, remains stopped while delivering that line, and resumes the same movement afterward. The behavior applies automatically across chapters without adding another dialogue queue or requiring chapter-specific call-site changes.

## Existing Problem

`DialogueSpeechService` serializes speech, but it does not communicate queue-wait state to guest movement. Chapter controllers can therefore enqueue a guest line and independently start `NPCWaypointMover`, allowing the guest to leave while its speech remains pending.

## Chosen Design

The shared speech service owns the entire pause lifecycle. When a normal guest line encounters an already-active normal line, the service resolves the speaking guest through the existing `SpeakingCharacterIndicator` actor-resolution path and acquires a movement-pause lease from that guest's existing `NPCWaypointMover`. The lease remains held while the line waits and while it plays, then releases on completion, skip, cancellation, or service disable.

`NPCWaypointMover` gains a reference-counted speech pause. A paused move keeps its destination and `IsMoving` state, yields without changing position, forces the animator into idle, and resumes toward the same target after the final lease releases. Reference counting prevents two queued lines from the same guest from resuming movement between lines. If an ambient `RoomPersonWalker2D` was active, the first lease temporarily disables it and the final release restores it.

`SpeakingCharacterIndicator` exposes its existing guest speaker resolution as a shared static operation. `DialogueSpeechService` calls that operation instead of duplicating guest-name, line-id, or actor-selection rules.

## Behavior Boundaries

- Only a non-overlapping guest line that actually waits behind active normal speech acquires a pause.
- The guest stays idle through its own speech and resumes only after that line completes.
- Immediately-started lines and `allowOverlap` lines do not add a queue-wait pause.
- Butler and non-guest speakers do not acquire guest movement pauses.
- A guest without `ActorRoomState`/`NPCWaypointMover` remains safe; speech continues and movement pause is a no-op.
- Cancelling the shared queue releases all outstanding guest movement leases synchronously.

## Alternatives Rejected

1. Chapter-specific callbacks were rejected because every caller would need to remember the rule and future chapters could regress.
2. A second guest-dialogue queue was rejected because `DialogueSpeechService` already owns ordering, skipping, and cancellation.
3. Duplicating speaker-name scans in the speech service was rejected because `SpeakingCharacterIndicator` already owns that identity mapping.

## Verification

An Edit Mode integration regression test will drive two real speech routines and a real `NPCWaypointMover`: a Butler line occupies the shared queue, a moving guest queues behind it, the guest must remain positionally frozen while waiting and speaking, and the same movement routine must advance after speech completes. Focused Unity tests and the broader relevant regression suites will run afterward.
