# Architecture migration report

## Current phase

**Phase 3 bootstrap retirement in progress; serialized GameRoot now owns navigation startup.**

This report records what is implemented in the repository at this commit. It must be updated after every Unity-validated migration phase.

## Source baseline

- Unity editor version: `6000.4.10f1`
- Runtime C# files: 90
- Runtime C# lines: 49,902
- Direct `MonoBehaviour` declarations: 63
- Architecture-smell counts are recorded in `Baseline/architecture_guard_baseline.json`.

## Implemented and Unity-validated

- Added the explicit `GameRoot`/`GameContext` composition spine.
- Added service, chapter, room, interaction, actor, motor, presenter, UI, definition, story-beat and state-machine bases.
- Rebased selected major managers/controllers while retaining their existing script filenames and `.meta` GUIDs.
- Added configuration validation and deterministic service initialization/shutdown.
- Removed the scheduler's global clock search fallback.
- Added an Editor-only GameRoot installer and active-scene validator.
- Added static architecture inventory, serialized-reference scan and debt-ceiling guard.
- Added CI guard workflow.
- Deleted two statically proven-unused scripts: `NewBehaviourScript` and `PickupObject`.
- Serialized exactly one `Chateau_GameRoot`, eight unique services, one scene behaviour, and `GameDatabase.asset` in `Gameplay.unity`.
- Preserved all 5,937 unrelated pre-existing Gameplay YAML documents byte-for-byte during the root graft.
- Added a real MainMenu-to-Gameplay lifecycle test with an Entrance/Drawing Room round trip and exact-one service assertions.
- Separated room-stage coordinate layout from Butler presentation scaling. The approved `0.7528645` presentation baseline is explicit on `Player.prefab`; raw room calibration and guest scale data remain unchanged.
- Removed `RoomNavigationBootstrap` after proving that the serialized root supplies exactly one navigation manager and prompt controller from MainMenu startup through a room round trip.
- Removed `ChapterManager.BootstrapChapterManagerForGameplay`; the serialized Chapter 1 stack now owns startup. Chapter 2 controller ownership was migrated in a separate gate.
- Serialized one inert `Chapter2Controller`, wired its existing dependencies, and bound it through GameRoot. Repeated Chapter 2 debug skips reuse the same controller and HUD while preserving the characterized feature behavior.
- Removed the `ChapterManager` factory for `Chapter2Controller`; every chapter transition and debug-skip entry path now resolves the single serialized controller.
- Removed the Chapter 2 HUD factory; Chapter 2 now reuses the single serialized HUD on every entry path.
- Serialized the three inert Chapter 2 feature owners (monster stinger, guest panic, and guest search), bound their stable scene references, registered them with GameRoot, and removed their independently gated creation fallbacks.
- Bound Chapter 1 to the existing serialized guest-scale applier, preserving the single ownership chain from applier to calibration to approved Butler source; every identity is lifecycle-tested before factory retirement.
- Removed runtime creation of guest-scale applier/calibration owners; the Guest Size Master retains an Editor-only, Undo-aware authoring action, and runtime creation is limited to per-guest participants.

## Current static result

| Metric | Baseline | Candidate | Delta |
|---|---:|---:|---:|
| Runtime C# files | 90 | 105 | +15 |
| Runtime C# lines | 49,902 | 50,567 | +665 |
| Direct `MonoBehaviour` declarations | 63 | 51 | -12 |
| `FindObject*`/`GameObject.Find` | 199 | 189 | -10 |
| `Resources.Load` | 27 | 27 | 0 |
| runtime `new GameObject` | 98 | 94 | -4 |
| runtime `AddComponent<T>` | 100 | 85 | -15 |
| runtime initialization hooks | 9 | 5 | -4 |

The temporary source increase is the migration spine and verification tooling. It is not evidence that the cleanup is finished.

## Validation completed

- architecture debt-ceiling guard passed;
- `git diff --check` passed;
- all C# scripts have `.meta` files;
- no duplicate script GUIDs were found;
- static source inventory was regenerated;
- serialized text-asset reference inventory was regenerated;
- new architecture files passed lightweight delimiter/preprocessor checks.
- Unity `6000.4.10f1` compiled the project and produced result XML for every automated run;
- the strict GameRoot graft audit passed 53/53 checks;
- the Chapter 2 feature graft audit passed 22/22 checks: three documents added, only three intended existing documents changed, and all other scene documents/order/roots preserved;
- the guest-scale ownership-chain audit passed 6/6 checks: no documents added/deleted, only the Chapter 1 component changed, and document order stayed exact;
- the full EditMode discovery count is 227: 176 pass and the same 51 pre-existing baseline failures remain, with no new failed test names;
- the MainMenu boot/navigation lifecycle passed three independent cold Unity processes;
- each cold lifecycle run produced the same entrance multiplier (`0.752865`) at startup, after settling, and after the room round trip;
- Gameplay scene hashing confirmed that batch validation did not rewrite the reviewed scene;
- all 146 C# scripts have `.meta` files and unique GUIDs.

## Validation still requiring human/golden review

- visual confirmation of the Butler-to-painted-entrance-door proportion in the interactive Game view;
- complete manual Chapter 1 and Chapter 2 golden-path smoke tests;
- dialogue/subtitle/voice, modal input, ambience, camera-shake and lighting visual/audio checks;
- a player build and save/load trace after `SaveService` exists.

## Compatibility adapters still present

The following remain intentionally because their replacements have not yet passed behavioral migration and deletion proof:

- runtime manager/bootstrap creation paths;
- current room/navigation implementation and parallel navigation data;
- legacy and point-click movement paths;
- duplicate actor presentation/scale/sort writers;
- chapter god controllers;
- feature-created UI;
- static/global audio and session state;
- runtime/editor lighting mutation paths.

## Next approved phase

1. Serialize Chapter 2 and its required service references before removing its independent creation path.
2. Serialize dialogue/subtitle/navigation edges before removing service factories.
3. Migrate one door pair as the first complete navigation vertical slice.

Do not begin bulk deletion until those gates pass.
