# Architecture-overhaul execution state

## Repository

- Branch: `refactor/final-architecture-overhaul`
- HEAD: `4501c51bb5175f18e56a233a8765631d6cb58986` at slice start
- Unity: `/home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity` (`6000.4.10f1`, revision `feeafc12a938`)
- Working tree clean: yes at slice start
- Last passing commit: `4501c51bb5175f18e56a233a8765631d6cb58986`

## Current slice

- Slice ID: `1.2` — make `GameContext` an immutable, typed, non-global composition contract
- Sole ownership change: Core becomes the sole owner of seven canonical runtime composition roles and their deterministic context-binding order. `GameContext` binds one explicit typed instance per canonical role from an ordered immutable service snapshot while allowing additional transition/future services. Legacy `SubtitleService` remains in the ordered snapshot but is deliberately not promoted to a permanent Core role because final subtitle state belongs to Dialogue. The context remains non-global and exposes no generic, string-keyed, singleton, or consumer-callable lifecycle surface. This slice does not migrate service behavior, consumers, serialized references, or legacy service APIs.
- Starting commit: `4501c51bb5175f18e56a233a8765631d6cb58986`
- Allowed files/assets: this execution-state record; `Docs/Architecture/Overhaul/FINAL_RUNTIME_MIGRATION_LEDGER.csv`; `Docs/Architecture/Overhaul/FINAL_EDITOR_TOOL_LEDGER.csv`; the Phase `1.2` migration-report entry; generated architecture evidence; `Assets/_Chateau/Runtime/Core/IGameService.cs`; `Assets/_Chateau/Runtime/Core/GameContext.cs`; `Assets/_Chateau/Runtime/Core/GameRoot.cs`; the eight registered service scripts (`CameraManager`, `ChapterClock`, `ChapterEventScheduler`, `RoomNavigationManager`, `RoomLightingController`, `SubtitleService`, `DialogueSpeechService`, and `ChapterManager`); `Assets/_Chateau/Editor/Architecture/GameRootInstaller.cs` solely to make its exact-count validation include the newly required lighting role; one new focused `GameContextContractTests` EditMode source plus its new `.meta`; and the existing `ArchitectureBaselinePlayModeTests` source. No scene, prefab, ScriptableObject, package, ProjectSettings, large asset, or existing `.meta` may change.
- Compatibility surface preserved: all eight legacy component class names and public APIs; every serialized field and object/file reference; `GameRoot.Services`; `GameContext.Root`, `Database`, and `Services`; all existing GUIDs; the serialized Inspector registration order; current Awake/Start behavior; and the certified visual fingerprint `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`.
- Characterization test: focused EditMode coverage must prove exact typed role binding, immutable ordered snapshots, no initialization side effect, rejection of every missing canonical role, null slots, duplicate instances, duplicate role implementations, multi-role implementations, wrong order, and any generic/static/string-key/lifecycle-control surface. It must also prove that additional role-less transition/future services are retained safely when their order is unique. Concrete service order values must be unique and match the declared composition order.
- Focused test filter and expected count: `GameContextContractTests` must discover and pass exactly `7/7` tests with zero failures or skips. A Core/identity/canonical compatibility filter must retain its expected passing count after discovery is measured. Run each completed batch gate once; do not rerun the full gate for each test-only adjustment.
- PlayMode/manual/golden gate: compile under Unity `6000.4.10f1`, inspect the complete diff for zero serialized asset mutation, then run the certified cold-start fingerprint exactly once and require `1/1`, the exact seven typed canonical identities plus all eight current scene services in declared order, and digest `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`. Manually confirm the new test `.meta` is the only new GUID and every existing GUID remains unchanged.
- Rollback commit: `4501c51bb5175f18e56a233a8765631d6cb58986`

## Evidence

- Static guard: passed for Slice `1.2`; all architecture-tool tests pass `4/4`, the architecture guard reports no debt above the committed baseline, and the generated audit remains at `48` direct `MonoBehaviour` declarations with unchanged smell totals (`106/17/67/51/4/6`).
- Runtime ledger: passed, `113` runtime files / `113` exact rows and `49,494` runtime lines. All nine declarations in `IGameService.cs`, every modified facade, and the focused Editor test are classified exactly.
- Unity script integrity: passed, `160` current scripts / `1,926` serialized references / `856` external-package references. The new test GUID `8d59f2a463a44a67a0305ce6f9b712d4` is unique with zero serialized references; every existing GUID remains unchanged.
- Compile result: Unity `6000.4.10f1` final batch compilation passes with no compiler errors; evidence is `Logs/ArchitectureOverhaul/Slice-1.2/compile-final.log`.
- EditMode XML: `Logs/ArchitectureOverhaul/Slice-1.2/editmode-focused-certified.xml` is verified at exactly `7/7/0/0`; the combined identity/context/Core/canonical compatibility filter is independently verified at `41/41/0/0` in `editmode-compatibility-certified.xml`.
- PlayMode XML: the correctly rendered `Logs/ArchitectureOverhaul/Slice-1.2/cold-start-certified.xml` passes exactly `1/1/0/0`, proves the exact seven typed identities plus all eight ordered service instances, and asserts fingerprint SHA-256 `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`.
- Manual/golden result: the certified Butler remains `114.417 px` against the `141.481 px` entrance door (`0.808710` ratio), presentation multiplier `0.7528645`, and sort `1075`; all eight guest scale/sort records remain byte-for-byte represented by the same fingerprint.
- Scene/prefab diff reviewed: passed; no `.unity`, `.prefab`, `.asset`, package, ProjectSettings, existing `.meta`, serialized field, or large-asset file changed. The focused test `.meta` is the only new metadata; no existing Unity GUID or file ID changed.

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
- Most real setup remains in legacy `Awake`/`OnEnable`/`Start`; Slice `1.2` certifies deterministic composition/context binding, not complete runtime dependency readiness.
- Until Slice `1.3`, `failStartupOnValidationErrors` remains false and `BuildOrderedServiceList` can normalize an extra null/duplicate registration after reporting it. Strict production-root rejection, duplicate roots, scene registration membership, reverse shutdown, and partial-initialize rollback remain the exact next bounded work.

## Exact next safe slice

- Commit the fully passing Slice `1.2` from rollback commit `4501c51bb5175f18e56a233a8765631d6cb58986`. Then begin Slice `1.3` from that clean commit: make production `GameRoot` validation fatal, characterize exact initialization/reverse-shutdown/partial-failure behavior, reject duplicate roots/services and registration mismatches, and prove scene binders without introducing repair or changing serialized gameplay data.

## Resume command

```text
Give Codex CODEX_RESUME_PROMPT.md.
```
