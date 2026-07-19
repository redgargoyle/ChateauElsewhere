# Main Menu Redesign Design

## Goal

Replace the current top-left kitchen menu with a high-resolution, full-screen mansion menu matching the supplied reference: a right-side Chantilly title plaque, a `developed by Kadabra Games` credit, and three ornate menu buttons labeled Start Game, Settings, and Exit.

## Visual Composition

- Use a high-resolution 16:9 mansion entrance image as a full-bleed background.
- Preserve the supplied reference's open-door, illustrated Victorian mansion character and right-side control composition.
- Place one ornate parchment-and-gold title plaque in the upper-right region.
- Render `Chantilly` and `developed by Kadabra Games` as separate TMP text elements over the title plaque.
- Place three matching ornate button cutouts beneath the plaque in this order: Start Game, Settings, Exit.
- Keep the background visible around the controls; do not add a separate opaque menu panel.
- Use plum text, aged parchment interiors, dark engraved recesses, and antique gold trim consistent with the reference.

## Asset Strategy

- Produce a clean high-resolution background without baked menu controls or lettering.
- Produce one reusable transparent blank button frame based on the approved ornate button shape.
- Produce one reusable transparent blank title plaque in the same visual family.
- Keep all visible wording in TMP rather than baking labels into sprites.
- Import a display serif font close to the reference option text and create a serialized TMP font asset. Prefer Noto Serif Display Medium because its narrow, high-contrast serif forms match the reference more closely than the current Liberation Sans fallback.
- Preserve existing assets as fallbacks; do not overwrite unrelated room artwork.

## Unity Architecture

- Keep `MainMenuController` as the sole menu behavior owner.
- Reuse the existing `Button_NewGame`, `Button_Settings`, and `Button_Exit` objects and their established actions.
- Keep `ContinueGame()` in code for save-flow compatibility, but remove `Button_Continue` from the visible layout.
- Add serialized references for the title plaque, developer credit, and reusable blank button frame where needed.
- Replace the current top-left positioning with a responsive right-side menu rail calculated from the canvas reference size.
- Preserve the fixed gameplay cursor catalog, audio settings, soundscape, UI cursor, and scene-load safeguards.

## Interaction Behavior

- Start Game calls the existing `NewGame()` method and loads Gameplay directly; Gameplay then starts Chapter 1 automatically.
- Settings calls the existing settings toggle and opens the audio settings panel above the menu.
- Exit calls the existing `ExitGame()` implementation.
- Hover and selected states add a restrained warm-gold highlight without obscuring the frame art.
- Pressed state darkens the parchment center briefly.
- Keyboard and gamepad navigation follows Start Game to Settings to Exit, with Start Game selected by default.
- The hidden Continue button cannot receive pointer or navigation focus.

## Responsive Layout

- Author against a 1920x1080 reference while retaining `Scale With Screen Size` canvas scaling.
- Use an aspect-fill background that covers the viewport without stretching.
- Anchor the title and button stack to the right-center safe region.
- Scale the complete menu rail uniformly when width or height becomes constrained.
- Preserve the frame aspect ratios and dynamically size TMP text so labels stay inside their cutouts.
- Verify 1920x1080, 1366x768, 1280x720, and 2560x1080 layouts.

## Settings Presentation

- Preserve all current audio controls and values.
- Restyle the runtime settings panel with the same parchment, plum, and antique-gold palette.
- Keep the panel modal and above the main menu controls.
- Preserve cancel/back behavior and UI cursor behavior.

## Fallbacks And Errors

- If a new sprite reference is unavailable, retain a readable TMP label and use the existing button/background fallback path.
- If the preferred TMP font is unavailable, fall back to the project's existing serif font asset.
- Keep all existing build-settings and scene-load validation unchanged.

## Verification

- Add EditMode regression coverage for the three visible labels, Kadabra Games credit, hidden Continue button, and retained button method wiring.
- Verify Start Game loads Gameplay and starts Chapter 1, Settings still opens audio controls, and Exit retains its existing action.
- Run the focused main-menu/navigation tests and the complete EditMode suite against the existing baseline.
- Capture rendered menu screenshots at the target resolutions and inspect background coverage, frame proportions, text containment, ordering, modal layering, and overlap.

## Out Of Scope

- Save-game/Continue behavior changes.
- Gameplay scene changes.
- Replacing the fixed cursor artwork or reworking audio-setting semantics.
- Replacing the game's existing room art.
