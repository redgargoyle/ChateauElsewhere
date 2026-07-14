# Architecture-overhaul execution state

## Repository

- Branch: `refactor/final-architecture-overhaul`
- HEAD: `0880a998e50ee4e4dd5743142f99d05c1f55adfb` at slice start
- Unity: `/home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity` (`6000.4.10f1`, revision `feeafc12a938`)
- Working tree clean: yes at slice start
- Last passing commit: `0880a998e50ee4e4dd5743142f99d05c1f55adfb`

## Current slice

- Slice ID: `1.3` — make production `GameRoot` strict and lifecycle-safe without adding runtime repair
- Sole ownership change: `GameRoot` becomes the strict owner of composition readiness and teardown sequencing. A valid root initializes every ordered service before binding ordinary scene behaviours; binders unbind while services are still alive; services then shut down in exact reverse order. Production Gameplay and the installer fail startup on any validation error. This slice does not move gameplay state, create services/views, migrate consumers, or change any service's legacy Awake/Start behavior.
- Starting commit: `0880a998e50ee4e4dd5743142f99d05c1f55adfb`
- Allowed files/assets: this execution-state record; migration report and final runtime/editor ledgers; generated architecture evidence; `Assets/_Chateau/Runtime/Core/GameRoot.cs`; `Assets/_Chateau/Runtime/Core/GameServiceBase.cs`; `Assets/_Chateau/Runtime/Core/ChateauBehaviour.cs`; `Assets/_Chateau/Editor/Architecture/GameRootInstaller.cs`; the prior `GameContextContractTests` only to update its now-strict missing-database diagnostic; exactly the existing `failStartupOnValidationErrors` scalar in `Assets/Scenes/Gameplay.unity`; one new focused `GameRootLifecycleContractTests` EditMode source plus `.meta`; and, only if needed to assert the production boundary, the existing architecture PlayMode source. No other scene line, prefab, ScriptableObject, package, ProjectSettings, large asset, existing `.meta`, service implementation, or public compatibility API may change.
- Compatibility surface preserved: all serialized field names and file IDs; all service/component classes and APIs; all existing GUIDs; `GameRoot.Services`; typed `GameContext`; the eight current service registrations and 31 scene-behaviour registrations; every legacy Awake/OnEnable/Start side effect; and visual fingerprint `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`. Only context-ready lifecycle sequencing and fatal production validation change.
- Characterization test: focused synthetic composition must prove exact initialization order, binders observing all services initialized, binders unbinding before reverse shutdown, partial-initialize rollback, binder-failure cleanup, strict rejection of duplicate/null service and binder registrations, duplicate-root scene validation, and the authored Gameplay fatal flag without any runtime factory/search path.
- Focused test filter and expected count: `GameRootLifecycleContractTests` must discover and pass exactly `5/5/0/0`; the combined `ArchitectureFoundationTests;GameContextContractTests;GameRootLifecycleContractTests` gate must pass exactly `29/29/0/0`.
- PlayMode/manual/golden gate: compile under Unity `6000.4.10f1`; inspect the Gameplay diff as exactly one scalar change; run the rendered cold-start fingerprint without `-nographics`; require `1/1`, all eight initialized services, exact typed order, and unchanged digest `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`. Confirm no ignored test scratch scene remains afterward.
- Rollback commit: `0880a998e50ee4e4dd5743142f99d05c1f55adfb`

## Evidence

- Static guard: passed for Slice `1.3`; all architecture-tool tests pass `4/4`, the architecture guard reports no debt above the committed baseline, and the generated audit remains at `48` direct `MonoBehaviour` declarations with unchanged smell totals (`106/17/67/51/4/6`).
- Runtime ledger: passed, `113` runtime files / `113` exact rows and `49,679` runtime lines. The three hardened Core files and focused Editor lifecycle test are classified explicitly.
- Unity script integrity: passed, `161` current scripts / `1,926` serialized references / `856` external-package references. New test GUID `4d6f2e7b90314ac18e56b9f0742d31ca` is unique with zero serialized references; every existing GUID remains unchanged.
- Compile result: Unity `6000.4.10f1` batch compilation passes with no compiler errors in `Logs/ArchitectureOverhaul/Slice-1.3/compile.log`; the final compatibility and cold-start processes also recompiled the final batch successfully.
- EditMode XML: `Logs/ArchitectureOverhaul/Slice-1.3/editmode-focused-final.xml` is verified at exactly `5/5/0/0`; `editmode-compatibility-final.xml` independently verifies Foundation `17`, Context `7`, and Lifecycle `5` at exactly `29/29/0/0`. The lifecycle suite also opens Gameplay read-only and requires zero scene-validation errors.
- PlayMode XML: the display-backed `Logs/ArchitectureOverhaul/Slice-1.3/cold-start.xml` passes exactly `1/1/0/0`, proves all eight services initialize under the strict production root, and asserts fingerprint SHA-256 `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`.
- Manual/golden result: the certified Butler remains `114.417 px` against the `141.481 px` entrance door (`0.808710` ratio), presentation multiplier `0.7528645`, and sort `1075`; all eight guest scale/sort records remain byte-for-byte represented by the same fingerprint.
- Scene/prefab diff reviewed: passed; Gameplay changes by exactly one existing scalar (`failStartupOnValidationErrors: 0 -> 1`). No prefab, ScriptableObject, package, ProjectSettings, large asset, existing `.meta`, service registration, scene-behaviour registration, GUID, or file ID changed. The focused test source and its new `.meta` are the only added files.

## Completed slices

| Slice | Commit | Tests | Notes |
|---|---|---|---|
| `0.1` | `872875aa4e3381993c3bb5d9c32a4393a7defe17` | Baseline evidence inherited | Recovery branch and tag established from the reviewed architecture head. |
| `0.2` | `62a389254d4cedfc8128b181d0a17bbe5fdaebf3` | Static controls | Final ledgers, verifier, integrity scanner, and slice-gate controls installed. |
| `0.2 evidence` | `8ec90df46a0e2917a52356692e4938140ed66081` | Integrity scanner `155/1926/856`; guard and ledger pass | Deterministic `script_integrity.csv` tracked as its own commit. |
| `0.2.1` | `daa11ed6d746e1a74c82ddec985ed8286d2b6cf9` | Verifier tests `3/3`; exact baseline XML `264/218/46/0` | NUnit failure evidence now excludes failed suite paths and verifies exact result counts. |
| `0.3` | `bb64c7c38ec16b6aa35c8bdf8026a723bce20b9e` | Real PlayMode `4/4`; lifecycle `10/10`; focused EditMode `31/31`; full EditMode `264/218/46/0`; static controls pass | Real startup, movement/collision, canonical room round trip, current hidden-child defect, Butler visual baseline, and Chapter 2 panic measurements committed without production changes. |
| `0.4a` | `c86beaea48f4910c2c1937d54574ec7360df8bd5` | Ten cold starts `10 x 1/1`; combined real PlayMode `5/5`; exact fingerprint; static controls pass | Certified deterministic Butler/guest scale and sort without production, editor, scene, prefab, or meta changes. |
| `0.4b` | `e795181c0e27d1cb9d85eed2694013ac45995b46` | Focused EditMode `62/58/4/0` with the exact four known legacy failures; cold fingerprint `1/1`; static controls pass | Calibration-window reads are non-mutating and intentional reference writes record Unity Undo; approved scale data and output remain exact. |
| `1.1` | `4501c51bb5175f18e56a233a8765631d6cb58986` | Identity `12/12`; compatibility `34/34`; cold fingerprint `1/1`; static controls pass | Six stable-ID value contracts added without changing any serialized identity storage or compatibility API. |
| `1.2` | `0880a998e50ee4e4dd5743142f99d05c1f55adfb` | Context `7/7`; compatibility `41/41`; cold fingerprint `1/1`; static controls pass | Seven lifecycle-free typed roles, extensible immutable service order, strict composition validation, and failure cleanup added with no serialized gameplay mutation. |

## Historical failed-gate audit — Slice `1.1`

- `editmode-focused-final.xml` initially produced `12 total / 11 passed / 1 failed`: the expanded current-asset assertion expected eight rooms but received zero.
- Diagnosis: Unity `AssetDatabase.FindAssets("t:RoomDefinition", ...)` does not resolve the namespaced canonical definition type in this project. The fourteen-passage query used the same unsupported assumption. No runtime code, scene, prefab, ScriptableObject, or existing metadata changed.
- Minimal in-scope repair: enumerate only the two canonical data folders and retain assets through typed `LoadAssetAtPath<T>` filtering. The exact eight-room/fourteen-passage identity, uniqueness, and string-preservation assertions remain unchanged. Preserve the failed XML/log and require the complete `12/12` focused rerun before commit.
- Repair result: `editmode-focused-certified.xml` passes exactly `12/12/0/0`; the failed `editmode-focused-final.xml` and its log remain preserved as diagnostic evidence under `Logs/ArchitectureOverhaul/Slice-1.1/`.

## Failed-gate audit within Slice `1.2`

- The first cold-start command incorrectly passed `-nographics`, exited natively with signal `11` / process code `139`, and produced no result XML. The preserved stack in `Logs/ArchitectureOverhaul/Slice-1.2/cold-start.log` ends in Linux `XcursorImageLoadCursor` from `NavigationCursorController.ApplyCursor`; no managed architecture assertion ran.
- Diagnosis: the certified rendered PlayMode gate requires a graphics device and every prior passing cold-start invocation deliberately omitted `-nographics`. The code batch had already compiled and passed focused/compatibility EditMode gates; this failure is a runner-command mismatch, not a behavioral difference.
- Minimal in-scope repair: make no source or asset change. Rerun the exact same single PlayMode test without `-nographics`, preserve both attempts, and accept the gate only if a real XML verifies `1/1` and the exact fingerprint remains unchanged.
- Repair result: `cold-start-certified.xml` verifies exactly `1/1/0/0`; all seven typed roles and eight ordered scene services match their serialized owners, and the visual digest remains exactly `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`.
- Crash cleanup: the aborted runner left one ignored Unity Test Framework `Assets/InitTestScene*.unity` plus `.meta`. Their contents were only the generated `PlaymodeTestsController`; both exact ignored files were removed, and the integrity scan returned from the transient `1927/857` count to the certified `1926/856` baseline. No tracked or authored asset was touched.

## Failed-gate audit within Slice `1.3`

- The first focused runner produced a real `5 total / 0 passed / 5 failed` XML before any test body ran. Every case failed in `SetUp` because Unity refuses `EditorSceneManager.NewScene(..., Additive)` beside the Test Framework's unsaved untitled scene. The second preserved attempt used runtime `SceneManager.CreateScene`, which Unity correctly rejected in EditMode for the same five setup-only failures. Neither attempt executed or disproved a production assertion.
- Minimal in-scope repair: use the batch runner's existing empty test scene for synthetic inactive objects, destroy every owned fixture object in teardown, and use owned additive loads only for the saved MainMenu/Gameplay validation fixtures. The final focused XML passes exactly `5/5/0/0` and the compatibility XML passes exactly `29/29/0/0`.
- A later compatibility launch stopped before test discovery because the test used a nonexistent Unity `6000.4.10f1` API, `EditorSceneManager.ClearSceneDirtiness`; no result XML was produced. The unsupported call was removed, while registration-list snapshots, scene dirty-state equivalence, real Gameplay validation, and byte-for-byte Gameplay readback retain the intended read-only proof. The final compatibility rerun passes `29/29/0/0`.
- All failed logs/XML remain under `Logs/ArchitectureOverhaul/Slice-1.3/`. No failed attempt changed production code, serialized registrations, existing metadata, MainMenu, prefabs, ScriptableObjects, packages, ProjectSettings, or large assets.

## Remaining adapters and debt

- All migration debt in the final runtime/editor ledgers remains unless explicitly completed above.
- The legacy `264` tests remain discovered as EditMode, including ten editor-hosted `[UnityTest]` methods that depend on `UnityEditor`, `AssetDatabase`, and predefined-assembly production types. Four new black-box tests now run in a genuine PlayMode test assembly; mechanically relocating the ten editor-hosted tests remains a Phase 8 test-structure task rather than a production architecture change.
- `RoomContentGroup` still force-enables an intentionally hidden child when its room is reactivated. Slice `0.3` records that existing defect; the later room-visibility ownership slice must reverse the assertion.
- The `46` exact legacy EditMode failures remain migration debt and are not an accepted final state.
- Typed IDs are additive boundaries only: legacy room display strings, `guest_1` actor aliases, and serialized chapter strings are not migrated in Slice `1.1`. Actor aliases require an explicit Phase `4` mapping; no name-derived conversion is permitted.
- `ChapterId` provides lexical/type safety but not catalog membership. Scheduler event IDs remain strings and must not cross the typed chapter boundary; Phase `1.5` database indexes and Phase `5` chapter definitions must reject unregistered values.
- `StoryBeatBase(string)` remains a compatibility constructor and reports no valid typed ID for a legacy label. New beat implementations must use `StoryBeatBase(BeatId)`.
- The seven typed composition interfaces are lifecycle-free markers until each owning slice supplies its real domain contract. Consumers must not cast them back to legacy concrete facades; `INavigationRuntimeService` coexists temporarily with the behavioral `INavigationService` only at composition time.
- `GameContext.Services` is the immutable dependency-ordered snapshot; `GameRoot.Services` intentionally remains serialized Inspector registration order. Neither list is a new lookup API.
- Most real setup remains in legacy `Awake`/`OnEnable`/`Start`; Slice `1.3` certifies strict root-owned composition/context readiness and rollback, not complete game-wide dependency readiness. Independent legacy callbacks must migrate in their owning slices rather than being mistaken for root-owned startup.
- Gameplay's eight services and 31 scene behaviours are now exact serialized sets. Their legacy domain behavior and public facades remain intentionally unchanged until the relevant Clock, Scheduler, World, Story, Dialogue, UI, and Save ownership slices.

## Exact next safe slice

- Commit the fully passing Slice `1.3` from rollback commit `0880a998e50ee4e4dd5743142f99d05c1f55adfb`. Then begin Slice `1.4` from that clean commit: characterize `ChapterClock` and `ChapterEventScheduler`, make Clock the sole time owner and Scheduler the sole timed-callback owner, preserve both existing script GUIDs/public compatibility APIs, and migrate no chapter-specific behavior in the same transaction.

## Resume command

```text
Give Codex CODEX_RESUME_PROMPT.md.
```
