# Architecture-overhaul execution state

## Repository

- Branch: `refactor/final-architecture-overhaul`
- HEAD: `4346f8f53a740f6b0557ff6e719c2a0eacc30170` at slice start
- Unity: `/home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity` (`6000.4.10f1`, revision `feeafc12a938`)
- Working tree clean: yes at slice start
- Last passing commit: `4346f8f53a740f6b0557ff6e719c2a0eacc30170`

## Current slice

- Slice ID: `2.1` — complete Group `06`, Butlers Pantry / Billiard Room
- Sole ownership change: the two already-authored Group `06` `DoorTriggerNavigation` components become explicit callers of their co-located canonical Passages, and the reciprocal Passage pair becomes the sole owner of one invariant approach/arrival point per room. This slice does not introduce a second route system or migrate any other pair.
- Starting commit: `4346f8f53a740f6b0557ff6e719c2a0eacc30170`
- Allowed files/assets: this execution-state record; `Assets/Scenes/Gameplay.unity`; `Docs/Architecture/RemainingRouteInventory.csv`; the existing Group `06` assertions in `Assets/Editor/GameplayLifecycleCharacterizationTests.cs`, `Assets/Editor/NavigationRegressionTests.cs`, `Assets/_Chateau/Editor/Architecture/CanonicalRoomPassageContractTests.cs`, and `Assets/_Chateau/Editor/Architecture/PassageMigrationCertificationTests.cs`; one genuine Group `06` case in the existing `Assets/Tests/PlayMode/ArchitectureBaselinePlayModeTests.cs`; and the architecture report/template/ledgers/generated evidence needed to record the passing result. Runtime source, prefabs, definitions, `GameDatabase.asset`, every `.meta`, packages, ProjectSettings, large assets, and unrelated scene documents are forbidden.
- Compatibility surface preserved: all GUIDs, script identities, public APIs, stable IDs, definitions, RoomViews, Passage components/file IDs, direct dependencies, GameRoot registration order, trigger owners, hierarchy, rectangles, `145`-pixel thresholds, blockers, cutouts, backgrounds, camera behavior, event/audio/prompt order, and all 43 non-Group-`06` triggers. The stage-0 fallback remains available only for later unmigrated pairs.
- Characterization test: retain the accepted seven-record rendered legacy Group `06` fingerprint and certify one rendered authored round trip in both directions across `1366x768`, `1440x1080`, `1920x1080`, `2560x1080`, and maximum zoom. Prove caller-bound stage 0 ignores poisoned canonical anchors, stage 1 owns only arrivals, stage 2 owns both reciprocal coordinates, null-caller fallback remains pair-local, and all Billiard blocker/cutout relationships stay unchanged.
- Focused test filter and expected count: targeted Group `06` lifecycle `1/1`; canonical-room/passage plus passage-migration certification `14/14`; the relevant navigation safety and rendered lifecycle gates must retain exact nonzero expected counts from their selected filters. Run architecture tools, guard, runtime-ledger, GUID/meta, and serialized-reference checks once after the complete batch, then compile once and run the focused Unity gates.
- PlayMode/manual/golden gate: run the rendered Group `06` lifecycle without `-nographics`, a genuine real-menu Group `06` PlayMode round trip, then the cold-start PlayMode fingerprint with unchanged digest `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`. Human review remains required for Butler foot placement on both painted doorway sides, prompt/cursor clearing, camera/visibility, and all three Billiard cutout/blocker occlusion pairs.
- Rollback commit: `4346f8f53a740f6b0557ff6e719c2a0eacc30170`

## Current slice evidence

- Starting/rollback commit: `4346f8f53a740f6b0557ff6e719c2a0eacc30170`; the repository was clean before the Slice `2.1` declaration.
- Production serialization: Gameplay remains `6,029` documents / eight RoomViews / fourteen Passages / 45 triggers. The exact scene delta is `8 insertions / 6 deletions`: callers `1505671646 -> 4100000023` and `2300000134 -> 4100000024`, four reciprocal anchor replacements, and two stage `0 -> 2` replacements. Final room-side points are Pantry `(3.244461, -3.108338)` and Billiard `(6.9, -1.6)`; both thresholds remain `145`. Gameplay SHA-256 is `c207301bc56a5096de0c6377606a281f99e2bc5084ca9beecafdbf8ff2d47d53`; inventory SHA-256 is `36534307882904596b3e294220b7ba1b77499ea7391561b8afd920166ed50cdb`.
- Static/GUID/reference gates: architecture-tool unit tests pass `4/4`; guard reports no debt above baseline; audit remains `113` runtime files / `50,212` lines / `48` direct MonoBehaviours with smell totals `106/17/67/51/4/6`; runtime ledger passes `113/113`; script integrity passes `164` scripts / `1,937` serialized references / `856` external-package references. No `.meta`, GUID, prefab, definition, package, ProjectSettings, large asset, or unrelated scene document changed.
- Compile: Unity `6000.4.10f1` exits `0` with no C# compiler error in `Logs/ArchitectureOverhaul/Slice-2.1/compile-final.log`; every subsequent current-source Unity test runner also compiled successfully without unexpected tracked mutation.
- EditMode: targeted authored Group `06` lifecycle passes exact `1/1`; canonical/manifest certification passes `14/14`; combined architecture/navigation safety passes `32/32`; the full rendered lifecycle fixture passes `10/10`. Verified XMLs are `group06-lifecycle-passing.xml`, `editmode-focused-final.xml`, `editmode-safety.xml`, and `editmode-lifecycle.xml` under `Logs/ArchitectureOverhaul/Slice-2.1/`.
- PlayMode/golden: the genuine real-menu Group `06` authored caller round trip passes `1/1` in `playmode-group06-final.xml`; cold start passes `1/1` in `cold-start.xml` with Butler `114.417 px`, entrance door `141.481 px`, ratio `0.808710`, presentation `0.7528645`, sort `1075`, and exact digest `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`.
- Authored evidence: worst stage-one screen distances are Pantry `78.646 px` and Billiard `87.01 px`, both below the unchanged `145 px` threshold. The seven-line profile hashes to `cd248f01301448b5cd807cc9331e58d99bd59a139bb772dab352869527b9a6eb`; Passage documents `4100000023` / `4100000024` hash to `61ae1a519da8a47275e2713b3182b7297c049b0eb9b2d590f5cb5037607abcc2` / `b46ec8f0b48730348e80287e50393c153ce05621c35db00741dfd7af50e6b1ce`.
- Automated gates are complete. Human review remains required for Butler feet at both painted door sides, Group `06` prompt/cursor behavior, camera/active-room visibility, and the three Billiard foreground-cutout/blocker occlusion pairs. No pruning is authorized by this slice.

## Failed-gate audit within Slice `2.1`

- The first rendered lifecycle produced exact `0/1`: candidate Billiard point `(7.075521, -1.844375)` worked at primary and `1366x768` but the `1440x1080` reverse approach was rejected by the movement boundary. The threshold and boundary were not weakened. A tests-only five-profile sweep selected invariant `(6.9, -1.6)` at worst `87.01 px`; the temporary calibration was removed, and the final lifecycle passes `1/1` plus the full `10/10` fixture.
- The calibrated lifecycle then exposed two previously unvalidated test-only fallback fingerprints: caller-bound stage `0` far arrival `(6.545288, -1.559969)` differs from pair-local null-caller fallback `(6.575521, -1.484375)`. The shared helper now accepts the precise expected path; no production path changed and tolerances were not widened.
- The first canonical/manifest run passed `13/14`; one source-text assertion demanded the obsolete widest-aspect legacy arrival literal after authored arrival ownership. Removing only that stale assertion yielded exact `14/14`; rendered lifecycle coverage remained intact.
- The first genuine Group `06` PlayMode run passed route setup but stopped at `0/1` because Chapter 1's intro correctly kept input disabled. The harness now explicitly enables input and resets the click-frame guard before simulating each trigger, matching the accepted characterization; the final real-trigger round trip passes exact `1/1`. All failed and repaired XML/log pairs remain preserved under `Logs/ArchitectureOverhaul/Slice-2.1/`.

## Completed Slice `1.5` evidence

- Static guard: passed for Slice `1.5`; architecture-tool tests pass `4/4`, the guard reports no debt above baseline, and generated audit reports `113` runtime files / `50,212` lines / `48` direct MonoBehaviours with unchanged smell totals `106/17/67/51/4/6`.
- Runtime ledger: passed at `113` current runtime files / `113` exact final-ledger rows. `GameDatabase` is recorded as a typed fail-closed index owner, all 19 RoomDefinitions are recorded, and the new focused fixture is classified in the final Editor-tool ledger.
- Unity script integrity: passed at `164` current scripts / `1,937` serialized script references / `856` external-package references. New test GUID `63be092c1c1640789df8a05643140f1d` and all eleven new room-asset GUIDs are unique; RoomDefinition data references advance from eight to nineteen while every existing script/asset GUID remains unchanged.
- Compile result: Unity `6000.4.10f1` batch compilation passes with no compiler errors in `Logs/ArchitectureOverhaul/Slice-1.5/compile.log`. Unity import leaves GameDatabase and all nineteen room assets byte-identical to their pre-import candidate bytes.
- EditMode XML: `Logs/ArchitectureOverhaul/Slice-1.5/editmode-focused-final.xml` verifies exactly `38/38/0/0`: GameDatabase definitions `9`, stable identities `12`, canonical room/passage `5`, passage migration `9`, room/startup roots `2`, and rendered Group `06` characterization `1`.
- PlayMode XML: `Logs/ArchitectureOverhaul/Slice-1.5/cold-start.xml` verifies exactly `1/1/0/0`, including strict GameDatabase validation and the existing eight initialized service roles/order.
- Manual/golden result: passed; Butler remains `114.417 px` against the `141.481 px` entrance door (`0.808710` ratio), presentation `0.7528645`, sort `1075`, and all guest records retain exact digest `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`.
- Scene/prefab diff reviewed: passed; no scene, prefab, existing asset, existing `.meta`, route inventory, legacy route source, package, ProjectSettings, or large asset changed. Gameplay remains `bb851d116bb616d5d8e25dc863bdb71f48c9314bfcd6b63d6e23acc1b3aced0d`, route inventory remains `1e821937d2561ac2239f5d5d5c1765c04f83dcb6d66e8b15f0b144b36b85b2cb`, and `doors.txt` remains `8dc956b84e8436054a8835a7fa7f12f0aa2ce14d1d9a90701e8d98c3f001798e`. No ignored `InitTestScene` scratch remains.

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
| `1.3` | `1f5af1c85a5fd337c7ba94ae20f772f488e6cf15` | Lifecycle `5/5`; Core compatibility `29/29`; cold fingerprint `1/1`; static controls pass | Production GameRoot is validation-fatal and lifecycle-safe; services initialize before binders and tear down after binders in exact reverse order without runtime repair. |
| `1.4` | `53a28cdf2bbd238b2c0bc71e011a0a187269b307` | Clock/Scheduler `8/8`; compatibility `37/37`; real PlayMode `1/1`; cold fingerprint `1/1`; static controls pass | Clock is the lifecycle-gated sole time writer; Scheduler consumes deterministic clock advances, owns timed callbacks/cancellation, and Chapter 1 has no fallback scheduling path. |
| `1.5` | `4346f8f53a740f6b0557ff6e719c2a0eacc30170` | Definitions `9/9`; focused `38/38`; cold fingerprint `1/1`; static controls pass | All 19 canonical room definitions and strict typed GameDatabase indexes are complete without changing Gameplay, existing GUIDs, or legacy navigation behavior. |

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

## Failed-gate audit within Slice `1.4`

- The first focused PlayMode run produced a real `1 total / 0 passed / 1 failed` XML before any Clock/Scheduler assertion. The test called synchronous `SceneManager.LoadScene("Gameplay")` and immediately asserted the active scene, but Unity Test Framework retained its generated `InitTestScene...` as active until the test yielded; the log then shows Gameplay loading successfully. Production code, scheduling behavior, and serialized content were not implicated.
- Minimal test-only repair: wait up to 120 frames for Gameplay to become the active scene before inspecting real services. No production, scene, prefab, data, Core contract, existing `.meta`, or GUID changed; the already-passing static, compile, and EditMode gates were not redundantly rerun after this harness-only adjustment.
- Repair result: `playmode-focused-certified.xml` passes exactly `1/1/0/0`, and the subsequent independent rendered cold-start passes `1/1/0/0` with the exact approved visual digest. Both the failed `playmode-focused.xml`/log and the certified rerun remain preserved under `Logs/ArchitectureOverhaul/Slice-1.4/`.

## Failed-gate audit within Slice `1.5`

- The first 38-test focused launch incorrectly used `-nographics`. The rendered Group `06` characterization loaded Gameplay and reached `NavigationCursorController.ApplyCursor`, where Linux Unity crashed in `XcursorImageLoadCursor` with signal `11` / exit `139`; no result XML was produced. This is the same graphics-runner incompatibility already established by the rendered cold-start gates. The crash log is preserved as `editmode-focused.log`; no source, asset, or serialized content changed.
- The graphics-enabled rerun discovered all `38` tests and passed `37`. Its only failure, `NavigationRegressionTests.GameplayRoomsOwnBackgroundsAndDoorGroups`, expects obsolete prefab name `Room_NewRoom` and is one of the exact pre-existing baseline failures in earlier full-suite XMLs; it is unrelated to room-definition/database ownership and did not reveal a Slice `1.5` behavior change. The `37/38` XML/log remain preserved as `editmode-focused-certified.*`.
- Minimal runner-filter correction: replace only that mistakenly selected known-failing legacy assertion with `NavigationRegressionTests.GameplayHasManualRoomStageRoot`, which checks the current authored room-stage root. No source, test, production asset, or serialized file changed between attempts. The accepted `editmode-focused-final.xml` then passes exactly `38/38/0/0`.

## Remaining adapters and debt

- All migration debt in the final runtime/editor ledgers remains unless explicitly completed above.
- The historical full EditMode baseline is `264/218/46`; the current focused runner reports `309` discoverable tests in `Assembly-CSharp-Editor`, but Slice `2.1` does not rerun the complete legacy suite and therefore makes no new full-suite pass/failure-count claim. Ten editor-hosted `[UnityTest]` methods still depend on `UnityEditor`, `AssetDatabase`, and predefined-assembly production types, while seven black-box cases are discoverable in the genuine PlayMode test assembly; mechanically splitting and relocating the editor-hosted tests remains a Phase 8 task.
- `RoomContentGroup` still force-enables an intentionally hidden child when its room is reactivated. Slice `0.3` records that existing defect; the later room-visibility ownership slice must reverse the assertion.
- The `46` exact legacy EditMode failures remain migration debt and are not an accepted final state.
- Typed IDs are additive boundaries only: legacy room display strings, `guest_1` actor aliases, and serialized chapter strings are not migrated in Slice `1.1`. Actor aliases require an explicit Phase `4` mapping; no name-derived conversion is permitted.
- `ChapterId` provides lexical/type safety but not catalog membership. Scheduler event IDs remain strings and must not cross the typed chapter boundary; Phase `1.5` database indexes and Phase `5` chapter definitions must reject unregistered values.
- `StoryBeatBase(string)` remains a compatibility constructor and reports no valid typed ID for a legacy label. New beat implementations must use `StoryBeatBase(BeatId)`.
- The seven typed composition interfaces are lifecycle-free markers until each owning slice supplies its real domain contract. Consumers must not cast them back to legacy concrete facades; `INavigationRuntimeService` coexists temporarily with the behavioral `INavigationService` only at composition time.
- `GameContext.Services` is the immutable dependency-ordered snapshot; `GameRoot.Services` intentionally remains serialized Inspector registration order. Neither list is a new lookup API.
- Most real setup remains in legacy `Awake`/`OnEnable`/`Start`; Slice `1.3` certifies strict root-owned composition/context readiness and rollback, not complete game-wide dependency readiness. Independent legacy callbacks must migrate in their owning slices rather than being mistaken for root-owned startup.
- Gameplay's eight services and 31 scene behaviours are now exact serialized sets. Their legacy domain behavior and public facades remain intentionally unchanged until the relevant Clock, Scheduler, World, Story, Dialogue, UI, and Save ownership slices.
- Scheduler callback queues are deliberately transient derived runtime state: callbacks contain Story-owned delegates and clear on Scheduler shutdown. A future restore transaction must explicitly clear Scheduler, restore immutable Story/clock values, and let the owning Story beat re-arm schedules. Durable schedule reconstruction belongs with StoryState and SaveService rather than serializing dead callback metadata.
- Every approved room now has canonical passive definition data, but eleven do not yet have RoomViews or Passage bindings. Phase 2 must reuse those exact assets/GUIDs and must not create replacement definitions.
- `RoomVisualCatalog`, `doors.txt`, legacy trigger strings, RoomContentGroup activation, and exceptional one-way/parallel topology remain intentional compatibility debt. Complete replacement data alone is not deletion proof; consumers migrate and reach zero before pruning.

## Exact next safe slice

- Commit the fully passing Slice `2.1` from rollback commit `4346f8f53a740f6b0557ff6e719c2a0eacc30170`. Then begin Group `07` Butlers Pantry / Service Corridor with tests-only legacy characterization from the clean Slice `2.1` commit. Reuse the Slice `1.5` room definitions, preserve Group `06` as a regression anchor, and do not author the missing Service Corridor RoomView/Passage graph or bind Group `07` callers until its behavior is locked.

## Resume command

```text
Give Codex CODEX_RESUME_PROMPT.md.
```
