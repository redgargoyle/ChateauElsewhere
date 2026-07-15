# Shared Dialogue Window Design

## Goal

Make every spoken-dialogue window use the existing screenshot-style portrait card in the same screen position and with the same internal layout. Chapter 2 choices must attach to that card without covering the speaker name, dialogue body, Skip control, or any chapter HUD text.

## Existing Problem

The game currently has two runtime dialogue renderers. `SubtitleService` owns the established portrait, nameplate, body, and Skip card used by shared speech and Chapter 1. `Chapter2InteractionHUD` separately creates a bottom-centered dialogue panel with its own speaker text, body text, Skip button, and three choice buttons.

The Chapter 2 panel uses a different canvas reference resolution and physically intersects the Chapter 2 objective band. It also bypasses `SubtitleService` with `showSubtitleOverlay: false`, so layout and behavior can drift between chapters.

## Chosen Design

`SubtitleService` becomes the sole visual owner of spoken dialogue. Its existing card remains the canonical layout, and it gains optional conversation-choice presentation and explicit controls for a persistent Chapter 2 conversation. `DialogueSpeechService` remains the sole speech timing, queue, voice, interruption, and skip owner.

`Chapter2InteractionHUD` keeps only Chapter 2-specific HUD responsibilities: objective, status, found-guest list, primary action, and clock-strike presentation. Its duplicate dialogue panel, dialogue text, dialogue Skip button, and dialogue choice buttons are removed.

No third canvas, dialogue queue, or chapter-specific clone of the portrait card will be introduced.

## Canonical Layout

The shared `Canvas_Subtitles` continues to use a `1920x1080` reference resolution with `Scale With Screen Size`.

- Card: top-left anchor and pivot, position `(32, -150)`, size `780x225`.
- Portrait frame: position `(22, -10)`, size `98x206`.
- Speaker nameplate: position `(146, -24)`, size `598x38`.
- Divider: position `(146, -78)`, size `598x2`.
- Speaker text: position `(164, -29)`, size `562x28`.
- Dialogue body: position `(146, -94)`, size `598x82`.
- Skip button: lower-right inside the card, position `(-14, 12)`, size `92x30`.
- Choice rail: top-left anchor and pivot, position `(32, -387)`, size `780x48`, directly beneath the card with a 12-pixel gap.
- Choice buttons: one to three buttons fill the rail in equal-width columns with 12-pixel gaps; three choices produce three `252x48` buttons.

The card geometry does not change between Chapter 1, Chapter 2, queued speech, interrupted speech, or guest conversations. The choice rail is auxiliary and appears only while choices are available.

## Text Containment And Non-Overlap

Speaker names remain single-line and use ellipsis if a name cannot fit. Dialogue text wraps only inside its fixed body rectangle, uses TMP auto-sizing down to a readable minimum before ellipsis, and never expands into the nameplate or Skip region.

Choice labels wrap or auto-size within their own button rectangles. The choice rail sits outside the card, so choices cannot cover dialogue text or Skip. The top-left card and choice safe zone remain clear of the Chapter 2 bottom-center objective and primary-action bands, the top-left status band, and the top-right found-guest list at `1920x1080`, `1366x768`, `1280x720`, and `2560x1080`.

When a conversation is cleared, the card, choices, callbacks, and Skip callback are cleared together. Existing Chapter 2 HUD elements retain their previous state; they are not re-created or repositioned by the dialogue service.

## Components And Data Flow

1. Chapter 1 and ordinary shared speech continue through `DialogueSpeechService`, which shows and hides the canonical card through `SubtitleService`.
2. Chapter 2 asks `SubtitleService` to show a persistent conversation line and optional choices.
3. Chapter 2 still starts voice playback through `DialogueSpeechService` with its existing queue and interruption behavior. While speech is active, `SubtitleService` exposes the same Skip callback and temporarily disables choices.
4. When speech ends, Skip is removed and choices become interactable without hiding the persistent conversation card.
5. Selecting a choice invokes the existing Chapter 2 callback. Clearing or leaving the conversation removes the shared card state and all UI callbacks.

Portrait lookup continues to use the existing subtitle line ID and speaker-ID bindings. A missing portrait hides only the portrait art; the name and line remain readable in the canonical card.

## Failure And Cleanup Behavior

- Missing or blank choice labels produce no active button and no clickable empty region.
- A choice with no callback is visible only when deliberately supplied and remains non-interactable.
- Room changes, chapter cleanup, service disable, or speech cancellation clear choices and Skip callbacks so stale actions cannot fire later.
- Missing `SubtitleService` references follow the existing resolution path; the change does not add another runtime service or fallback canvas.
- Existing dialogue interruption and queued-guest movement behavior remains unchanged.

## Alternatives Rejected

1. Copying the portrait-card constants into `Chapter2InteractionHUD` was rejected because two visual owners would continue to drift and could overlap again.
2. Keeping the Chapter 2 panel and merely moving it to the top-left was rejected because it would look similar while preserving duplicate speaker, line, Skip, and choice state.
3. Replacing all speech timing with a new conversation system was rejected because `DialogueSpeechService` already owns queueing, voice playback, interruption, skipping, and guest movement pauses.

## Verification

- Add an Edit Mode regression that materializes the shared UI and proves the canonical card geometry and internal text/Skip containment.
- Add regression coverage proving Chapter 2 no longer creates `Panel_Chapter2Dialogue` or its dialogue text and Skip controls.
- Exercise Chapter 2 conversation APIs and verify the shared card shows the correct speaker, portrait, line, choices, and Skip state.
- Assert the card and choice rail do not intersect active Chapter 2 objective, status, found-list, or primary-action rectangles at supported 16:9 resolutions.
- Verify choices are disabled during speech, re-enabled afterward, and callbacks are removed by conversation cleanup.
- Run the focused subtitle, Chapter 2, dialogue input-routing, and dialogue speech regression suites. If the open Unity editor prevents batch mode, run the same focused tests in the editor Test Runner and report that constraint explicitly.

## Out Of Scope

- Rewriting dialogue text, voice clips, subtitle line-bank content, or portrait art.
- Changing speech queue timing, interruption semantics, or guest movement pausing.
- Redesigning Chapter 2 objective, status, found-list, primary-action, or clock-strike UI.
- Broad scene-service ownership migration unrelated to the dialogue-window consolidation.
