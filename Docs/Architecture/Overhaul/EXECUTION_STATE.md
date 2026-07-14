# Architecture-overhaul execution state

## Repository

- Branch: `refactor/final-architecture-overhaul`
- HEAD: `c86beaea48f4910c2c1937d54574ec7360df8bd5` at slice start
- Unity: `/home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity` (`6000.4.10f1`, revision `feeafc12a938`)
- Working tree clean: yes at slice start
- Last passing commit: `c86beaea48f4910c2c1937d54574ec7360df8bd5`

## Current slice

- Slice ID: `0.4b` — make calibration-window reads non-mutating and intentional editor writes Undo-safe
- Sole ownership change: the two temporary actor-scale calibration windows become read-only during open, selection, layout, and repaint. Pure query extraction leaves existing mutating compatibility APIs intact, and calibration-reference writes touched by this batch record Unity Undo first. No runtime scale owner or serialized calibration data moves in this slice.
- Starting commit: `c86beaea48f4910c2c1937d54574ec7360df8bd5`
- Allowed files/assets: this execution-state record; `Assets/Editor/ButlerRoomScaleCalibrationWindow.cs`; `Assets/Editor/GuestRoomScaleMasterWindow.cs`; `Assets/Editor/GuestButlerScaleRegressionTests.cs`; and pure, compatibility-preserving query extraction only in `Assets/Scripts/Characters/GuestRoomScaleCalibration.cs` and `Assets/Scripts/Characters/GuestScaleParticipant.cs`. No runtime scale output or write path, scene, prefab, ScriptableObject, `.meta`, package, ProjectSettings, generated baseline, large asset, or existing calibration record may change.
- Compatibility surface preserved: every public/runtime API, script GUID, serialized file ID/reference, all nineteen Butler endpoint records, all nineteen guest calibration records, eight guest captured-base values, the certified fingerprint `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`, and all approved gameplay/visual/timing behavior. The temporary editor windows keep their menus and workflows; actor-profile conversion remains Phase `4` work.
- Characterization test: add behavior-level EditMode coverage proving Butler selection/open reads do not initialize serialized base scale; guest room/repaint reads do not sanitize calibration entries or cache guest presentation fields; and calibration-reference assignment records Undo before mutation.
- Focused test filter and expected count: `GuestRoomScaleRegressionTests` must discover exactly `62` tests with the four new regressions passing and the same four certified legacy failures only (`58 passed / 4 known failed / 0 skipped`, failed-name SHA-256 `b3e17ee67f553f0ec455ca79052a7aff16d984462c04601ceb1b2cb5b7434ab5`). Use one Unity process for the completed batch, not one process per small edit.
- PlayMode/manual/golden gate: inspect the complete diff for pure-query-only runtime changes and zero scene/prefab/meta change, then run the certified cold-start fingerprint once and require exact digest `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`. Behavior-level regressions must prove window open/selection/read paths leave serialized state unchanged until an explicit control is used.
- Rollback commit: `c86beaea48f4910c2c1937d54574ec7360df8bd5`

## Evidence

- Static guard: passed for Slice `0.4b`; all architecture-tool tests pass `4/4` and the architecture guard reports no debt above the committed baseline.
- Runtime ledger: passed, `112` runtime files / `112` exact rows.
- Unity script integrity: passed, `157` current scripts / `1,926` serialized references / `856` external-package references.
- Compile result: passed with no compiler errors during the focused EditMode and certified PlayMode processes under `Logs/ArchitectureOverhaul/Slice-0.4b/`.
- EditMode XML: `Logs/ArchitectureOverhaul/Slice-0.4b/editmode-focused.xml` is verified at exactly `62 total / 58 passed / 4 known failed / 0 skipped`, with exact four-case SHA-256 `b3e17ee67f553f0ec455ca79052a7aff16d984462c04601ceb1b2cb5b7434ab5`; all four new write-safety regressions pass.
- PlayMode XML: `Logs/ArchitectureOverhaul/Slice-0.4b/cold-start.xml` passes exactly `1/1/0/0` and asserts fingerprint SHA-256 `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`.
- Manual/golden result: behavior-level Editor serialization snapshots remain byte-identical across Butler selection and guest calibration/participant read-only queries; applier calibration assignment restores through Undo. The certified Butler `114.417 px`, entrance door `141.481 px`, ratio `0.808710`, presentation multiplier `0.7528645`, sort `1075`, and eight guest records remain unchanged.
- Scene/prefab diff reviewed: passed; full Git diff contains no scene, prefab, ScriptableObject, `.meta`, package, ProjectSettings, or large-asset change. The calibration-sensitive scene/prefab hashes remain those committed in `Evidence/Slice_0_4/calibration_asset_hashes.csv`, and no Unity GUID was added or changed.

## Completed slices

| Slice | Commit | Tests | Notes |
|---|---|---|---|
| `0.1` | `872875aa4e3381993c3bb5d9c32a4393a7defe17` | Baseline evidence inherited | Recovery branch and tag established from the reviewed architecture head. |
| `0.2` | `62a389254d4cedfc8128b181d0a17bbe5fdaebf3` | Static controls | Final ledgers, verifier, integrity scanner, and slice-gate controls installed. |
| `0.2 evidence` | `8ec90df46a0e2917a52356692e4938140ed66081` | Integrity scanner `155/1926/856`; guard and ledger pass | Deterministic `script_integrity.csv` tracked as its own commit. |
| `0.2.1` | `daa11ed6d746e1a74c82ddec985ed8286d2b6cf9` | Verifier tests `3/3`; exact baseline XML `264/218/46/0` | NUnit failure evidence now excludes failed suite paths and verifies exact result counts. |
| `0.3` | `bb64c7c38ec16b6aa35c8bdf8026a723bce20b9e` | Real PlayMode `4/4`; lifecycle `10/10`; focused EditMode `31/31`; full EditMode `264/218/46/0`; static controls pass | Real startup, movement/collision, canonical room round trip, current hidden-child defect, Butler visual baseline, and Chapter 2 panic measurements committed without production changes. |
| `0.4a` | `c86beaea48f4910c2c1937d54574ec7360df8bd5` | Ten cold starts `10 x 1/1`; combined real PlayMode `5/5`; exact fingerprint; static controls pass | Certified deterministic Butler/guest scale and sort without production, editor, scene, prefab, or meta changes. |

## Remaining adapters and debt

- All migration debt in the final runtime/editor ledgers remains unless explicitly completed above.
- The legacy `264` tests remain discovered as EditMode, including ten editor-hosted `[UnityTest]` methods that depend on `UnityEditor`, `AssetDatabase`, and predefined-assembly production types. Four new black-box tests now run in a genuine PlayMode test assembly; mechanically relocating the ten editor-hosted tests remains a Phase 8 test-structure task rather than a production architecture change.
- `RoomContentGroup` still force-enables an intentionally hidden child when its room is reactivated. Slice `0.3` records that existing defect; the later room-visibility ownership slice must reverse the assertion.
- The `46` exact legacy EditMode failures remain migration debt and are not an accepted final state.

## Exact next safe slice

- Complete the five closely related editor read/write-safety changes in Slice `0.4b`, run one focused EditMode suite and one certified cold-start fingerprint, then commit from the clean `0.4a` baseline. Do not change runtime scale output or migrate calibration profiles in this slice.

## Resume command

```text
Give Codex CODEX_RESUME_PROMPT.md.
```
