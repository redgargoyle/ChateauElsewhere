# Architecture migration report

## Current phase

**Phase 2 composition root serialized and validated; Phase 3 bootstrap retirement is next.**

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

## Current static result

| Metric | Baseline | Candidate | Delta |
|---|---:|---:|---:|
| Runtime C# files | 90 | 106 | +16 |
| Runtime C# lines | 49,902 | 50,686 | +784 |
| Direct `MonoBehaviour` declarations | 63 | 51 | -12 |
| `FindObject*`/`GameObject.Find` | 199 | 198 | -1 |
| `Resources.Load` | 27 | 27 | 0 |
| runtime `new GameObject` | 98 | 98 | 0 |
| runtime `AddComponent<T>` | 100 | 100 | 0 |
| runtime initialization hooks | 9 | 9 | 0 |

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
- the full EditMode discovery count is 223: 172 pass and the same 51 pre-existing baseline failures remain, with no new failed test names;
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

1. Delete only the now-redundant, zero-serialized-reference `RoomNavigationBootstrap`, with prune proof and the existing lifecycle/navigation gates.
2. Remove only `ChapterManager.BootstrapChapterManagerForGameplay`; do not yet remove the independent Chapter 2 creation path.
3. Serialize Chapter 2 and its required service references before removing any corresponding repair path.
4. Migrate one door pair as the first complete navigation vertical slice.

Do not begin bulk deletion until those gates pass.
