# Architecture migration report

## Current phase

**Foundation candidate prepared; Unity validation pending.**

This report records what is implemented in the repository at this commit. It must be updated after every Unity-validated migration phase.

## Source baseline

- Unity editor version: `6000.4.10f1`
- Runtime C# files: 90
- Runtime C# lines: 49,902
- Direct `MonoBehaviour` declarations: 63
- Architecture-smell counts are recorded in `Baseline/architecture_guard_baseline.json`.

## Implemented in the foundation candidate

- Added the explicit `GameRoot`/`GameContext` composition spine.
- Added service, chapter, room, interaction, actor, motor, presenter, UI, definition, story-beat and state-machine bases.
- Rebased selected major managers/controllers while retaining their existing script filenames and `.meta` GUIDs.
- Added configuration validation and deterministic service initialization/shutdown.
- Removed the scheduler's global clock search fallback.
- Added an Editor-only GameRoot installer and active-scene validator.
- Added static architecture inventory, serialized-reference scan and debt-ceiling guard.
- Added CI guard workflow.
- Deleted two statically proven-unused scripts: `NewBehaviourScript` and `PickupObject`.

## Current static result

| Metric | Baseline | Candidate | Delta |
|---|---:|---:|---:|
| Runtime C# files | 90 | 106 | +16 |
| Runtime C# lines | 49,902 | 50,616 | +714 |
| Direct `MonoBehaviour` declarations | 63 | 51 | -12 |
| `FindObject*`/`GameObject.Find` | 199 | 198 | -1 |
| `Resources.Load` | 27 | 27 | 0 |
| runtime `new GameObject` | 98 | 98 | 0 |
| runtime `AddComponent<T>` | 100 | 100 | 0 |
| runtime initialization hooks | 9 | 9 | 0 |

The temporary source increase is the migration spine and verification tooling. It is not evidence that the cleanup is finished.

## Validation completed outside Unity

- architecture debt-ceiling guard passed;
- `git diff --check` passed;
- all C# scripts have `.meta` files;
- no duplicate script GUIDs were found;
- static source inventory was regenerated;
- serialized text-asset reference inventory was regenerated;
- new architecture files passed lightweight delimiter/preprocessor checks.

## Validation not yet completed

No Unity editor or standalone C# compiler is available in the build environment used to prepare this candidate. Therefore these remain open gates:

- Unity compilation;
- complete EditMode test suite;
- GameRoot installer execution and review of generated scene/data changes;
- complete EditMode rerun;
- available PlayMode tests;
- MainMenu, navigation, Chapter 1 and Chapter 2 smoke tests;
- missing-script and duplicate-service inspection.

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

1. Compile and test the foundation in Unity `6000.4.10f1`.
2. Run the Editor installer and commit only its scene/data output.
3. Verify complete Chapter 1/2 and navigation behavior.
4. Retire only the now-redundant runtime bootstrap paths.
5. Migrate one door pair as the first complete navigation vertical slice.

Do not begin bulk deletion until those gates pass.
