# Main Menu Redesign Implementation Plan

> **For Codex:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task by task.

**Goal:** Replace the current kitchen/top-left main menu with a high-resolution illustrated mansion menu whose layered TMP title, Kadabra Games credit, and ornate Start Game, Settings, and Exit controls match the approved reference while preserving all existing menu behavior.

**Architecture:** Keep `MainMenuController` as the only runtime behavior owner and reuse the existing menu button GameObjects and callbacks. Add clean raster presentation assets plus a serialized Noto Serif Display TMP font, then make the controller deterministically apply the new right-side layout and visual states. Use an editor builder only to import/configure assets and persist the scene hierarchy; runtime repair remains in the controller so the menu is resilient to scene serialization drift.

**Tech Stack:** Unity 6.0.4, C#, Unity UI, TextMesh Pro, NUnit EditMode tests, Codex image generation.

---

## Constraints

- Do not edit or stage `Assets/Scenes/Gameplay.unity` or `.vscode/settings.json`; both contain pre-existing user work.
- Keep the existing `NewGame()`, `ToggleAudioSettingsPanel()`, `ExitGame()`, soundscape, and scene-loading ownership in `MainMenuController`.
- Keep `ContinueGame()` available in code but make `Button_Continue` inactive and non-navigable.
- Keep all visible menu copy in TMP, never baked into the generated sprites.
- Use one reusable blank button sprite for all three actions; do not create duplicate button pathways.

### Task 1: Add Failing Main Menu Presentation Tests

**Files:**
- Modify: `Assets/Editor/NavigationRegressionTests.cs`
- Test: `Assets/Editor/NavigationRegressionTests.cs`

**Step 1: Add a scene presentation regression test**

Add `MainMenuUsesLayeredRightRailPresentation()` that opens `Assets/Scenes/MainMenu.unity`, locates inactive objects, and asserts:

- the main title TMP value is exactly `Chantilly`;
- a TMP credit reads exactly `developed by Kadabra Games`;
- active button labels are exactly `Start Game`, `Settings`, and `Exit`;
- `Button_Continue` exists but is inactive;
- the background, title plaque, and three active button images have sprites assigned;
- all three active buttons share the same blank frame sprite;
- the title plaque and button stack are right-anchored.

**Step 2: Replace the obsolete top-left source-contract assertions**

Update `MainMenuLayoutScalesToShortGameViews()` so it checks the new implementation contract: right-side rail anchors, aspect-fill background behavior, three-button spacing, and hidden Continue behavior. Remove assertions tied to `buttonSpacing * 3f`, the old top-left title coordinates, and four visible buttons.

**Step 3: Preserve behavior coverage**

Retain the direct Start Game, settings/audio, and Exit behavior tests. Add or retain a scene wiring assertion that the Start Game, Settings, and Exit objects resolve to the existing controller actions rather than new handlers.

**Step 4: Run the focused tests and confirm RED**

Run:

```bash
xvfb-run -a /home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity \
  -batchmode \
  -projectPath /home/hamza/dreadforge_2022_2 \
  -runTests \
  -testPlatform EditMode \
  -testFilter NavigationRegressionTests \
  -testResults /tmp/main-menu-red.xml \
  -logFile /tmp/main-menu-red.log
```

Expected: the new presentation assertions fail because the scene still contains the kitchen background, baked-label sprites, old title copy, and active Continue button.

**Step 5: Commit the failing test contract**

```bash
git add Assets/Editor/NavigationRegressionTests.cs
git commit -m "test: define redesigned main menu presentation"
```

### Task 2: Create And Import High-Resolution Layered Assets

**Files:**
- Create: `Assets/Art/MainMenuRedesign/MainMenu_Background.png`
- Create: `Assets/Art/MainMenuRedesign/MainMenu_ButtonBlank.png`
- Create: `Assets/Art/MainMenuRedesign/MainMenu_TitlePlaque.png`
- Create: `Assets/Art/UI/Fonts/NotoSerifDisplay-Medium.ttf`
- Create: Unity-generated `.meta` files for those assets

**Step 1: Generate the clean mansion background**

Use the approved user reference as the image-generation reference. Produce a high-resolution 16:9 illustrated Victorian mansion entrance with open foreground doors, the same warm hand-painted detail and central sightline, and a visually quieter right-side wall/foliage area for controls. Remove all title plaques, buttons, words, logos, and other UI from the image. Target at least 2048 pixels wide.

**Step 2: Generate the reusable blank button cutout**

Use `Assets/Art/MainMenuButtons/MainMenu_NewGame.png` as the local reference. Remove the baked words while preserving the exact wide parchment center, engraved dark recess, curled antique-gold filigree silhouette, and transparent exterior. Keep the center clean enough for TMP text and retain a high-resolution RGBA output.

**Step 3: Generate the blank title plaque**

Use the ornate button reference to create a taller matching parchment plaque with a transparent exterior, antique-gold floral frame, and blank center. It must accommodate both the `Chantilly` title and one-line developer credit without baked lettering.

**Step 4: Add the display font source**

Copy `/usr/share/fonts/noto/NotoSerifDisplay-Medium.ttf` to `Assets/Art/UI/Fonts/NotoSerifDisplay-Medium.ttf`. Keep the existing Liberation fonts as fallback assets.

**Step 5: Inspect every raster before integration**

Use `view_image` on all three PNG outputs. Confirm the background has no baked UI, the button and plaque exteriors are transparent, no text remnants remain, and the frame edges are not cropped.

### Task 3: Implement The Layered Runtime Menu

**Files:**
- Modify: `Assets/Scripts/MainMenuController.cs`

**Step 1: Replace visual asset constants and references**

Point the primary menu paths at the new clean background, blank button frame, title plaque, and Noto Serif Display TMP asset. Retain the current sprites/font as explicit fallbacks. Add serialized references for the title plaque image and developer credit TMP text without introducing a second controller.

**Step 2: Build deterministic TMP labels**

Replace `HideLegacyButtonText` usage with a helper that finds or creates one TMP child label per visible button and sets:

- `Start Game`, `Settings`, or `Exit` from the owning button;
- Noto Serif Display font with Liberation Serif fallback;
- centered alignment, plum text, restrained outline/shadow, and auto-size bounds;
- `raycastTarget = false` and stretch anchors with safe horizontal padding.

Ensure repeated calls reuse existing label objects instead of duplicating them.

**Step 3: Apply the right-side responsive layout**

Replace the top-left layout with a 1920x1080-authored right rail:

- aspect-fill the background with `AspectRatioFitter.AspectMode.EnvelopeParent`;
- make the menu panel transparent and full-screen;
- right-anchor the title plaque near the upper-right safe region;
- layer `Chantilly` and `developed by Kadabra Games` over the plaque;
- stack only Start Game, Settings, and Exit beneath it with stable aspect ratios;
- uniformly reduce the rail scale for constrained heights/widths;
- keep the rail clear of screen edges at 4:3, 16:9, and ultrawide dimensions.

**Step 4: Preserve and tighten interaction states**

Keep the current callbacks, then configure each visible button's `ColorBlock`, sprite, and navigation for subtle gold hover/selection and darker pressed feedback. Set explicit navigation Start Game -> Settings -> Exit, select Start Game by default, and deactivate Continue before navigation is built.

**Step 5: Restyle the existing settings modal**

Leave its control hierarchy and callbacks unchanged. Apply parchment/plum/gold colors, the display serif font, and modal sorting/layout so it remains legible above the new menu at all target resolutions.

**Step 6: Run a compile check**

Run:

```bash
xvfb-run -a /home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity \
  -batchmode \
  -quit \
  -projectPath /home/hamza/dreadforge_2022_2 \
  -logFile /tmp/main-menu-compile.log
```

Expected: Unity exits successfully with no C# compiler errors.

### Task 4: Persist The Scene And TMP Font Asset

**Files:**
- Create: `Assets/Editor/MainMenuRedesignBuilder.cs`
- Create: `Assets/Editor/MainMenuRedesignBuilder.cs.meta`
- Create: `Assets/Art/UI/Fonts/NotoSerifDisplay-Medium SDF.asset`
- Create: `Assets/Art/UI/Fonts/NotoSerifDisplay-Medium SDF.asset.meta`
- Modify: `Assets/Scenes/MainMenu.unity`
- Modify: generated `.meta` files under `Assets/Art/MainMenuRedesign/`

**Step 1: Add an idempotent editor builder**

Create `MainMenuRedesignBuilder.Rebuild()` that:

- imports the background as a high-quality 4096-max sprite and the two frame assets as alpha sprites;
- creates or refreshes the Noto Serif Display TMP font asset;
- opens only `Assets/Scenes/MainMenu.unity`;
- finds the existing controller/buttons by name and never creates replacement action buttons;
- creates or reuses `Image_TitlePlaque`, `Text_Title`, and `Text_DeveloperCredit`;
- assigns serialized references, sprites, fonts, labels, anchors, and navigation;
- deactivates `Button_Continue`;
- saves the scene only when the expected hierarchy has been applied.

**Step 2: Add deterministic visual capture support**

Add `MainMenuRedesignBuilder.CaptureScreenshots()` that opens the menu scene and renders the canvas at 1920x1080, 1366x768, 1280x720, and 2560x1080 into `/tmp/main-menu-captures/`. The method may temporarily use a camera/render texture but must restore scene state and not save capture-only changes.

**Step 3: Execute the builder**

Run:

```bash
xvfb-run -a /home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity \
  -batchmode \
  -quit \
  -projectPath /home/hamza/dreadforge_2022_2 \
  -executeMethod MainMenuRedesignBuilder.Rebuild \
  -logFile /tmp/main-menu-builder.log
```

Expected: the asset import completes, the TMP asset is serialized, `MainMenu.unity` is updated, and no unrelated scene is saved.

**Step 4: Run the focused tests and confirm GREEN**

Run the Task 1 test command again, writing to `/tmp/main-menu-green.xml` and `/tmp/main-menu-green.log`.

Expected: all `NavigationRegressionTests` pass.

**Step 5: Commit implementation and assets**

Stage only the controller, menu scene, new assets/meta files, builder, and TMP font files. Confirm `Gameplay.unity` and `.vscode/settings.json` remain unstaged.

```bash
git add Assets/Scripts/MainMenuController.cs Assets/Scenes/MainMenu.unity Assets/Editor/MainMenuRedesignBuilder.cs Assets/Editor/MainMenuRedesignBuilder.cs.meta Assets/Art/MainMenuRedesign Assets/Art/UI/Fonts/NotoSerifDisplay-Medium.ttf Assets/Art/UI/Fonts/NotoSerifDisplay-Medium.ttf.meta "Assets/Art/UI/Fonts/NotoSerifDisplay-Medium SDF.asset" "Assets/Art/UI/Fonts/NotoSerifDisplay-Medium SDF.asset.meta"
git commit -m "feat: redesign main menu presentation"
```

### Task 5: Verify Behavior, Rendering, And Repository Scope

**Files:**
- Test: `Assets/Editor/NavigationRegressionTests.cs`
- Test: `Assets/Editor/Chapter2RegressionTests.cs`
- Inspect: `Assets/Scenes/MainMenu.unity`
- Inspect: `/tmp/main-menu-captures/*.png`

**Step 1: Run the complete EditMode suite**

```bash
xvfb-run -a /home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity \
  -batchmode \
  -projectPath /home/hamza/dreadforge_2022_2 \
  -runTests \
  -testPlatform EditMode \
  -testResults /tmp/main-menu-all.xml \
  -logFile /tmp/main-menu-all.log
```

Expected: all tests pass, or only a clearly documented pre-existing baseline failure remains unrelated to this menu work.

**Step 2: Capture all target resolutions**

```bash
xvfb-run -a /home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity \
  -batchmode \
  -quit \
  -projectPath /home/hamza/dreadforge_2022_2 \
  -executeMethod MainMenuRedesignBuilder.CaptureScreenshots \
  -logFile /tmp/main-menu-capture.log
```

**Step 3: Inspect captures visually and by pixels**

Use `view_image` for every capture and an image-inspection command to verify nonblank dimensions. Check:

- full-bleed background with no stretch or empty bars;
- title plaque and all three buttons visible in the first viewport;
- `Chantilly` and `developed by Kadabra Games` contained inside the plaque;
- no baked duplicate labels;
- stable frame proportions and no overlap at every resolution;
- settings modal layers above the rail.

**Step 4: Audit the diff and worktree**

Run:

```bash
git diff --check
git status --short
git diff --stat origin/architecture_overhaul...HEAD
```

Confirm the two user-owned dirty files remain untouched and unstaged, no generated logs/captures entered the repository, and all menu changes match the approved scope.

**Step 5: Commit any verification-driven corrections**

If visual inspection reveals containment or spacing defects, first add or tighten a regression assertion, reproduce RED, correct the controller/builder, rerun focused and full suites, then commit only those corrections.
