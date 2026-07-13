# Architecture-overhaul execution state

## Repository

- Branch: `refactor/final-architecture-overhaul`
- HEAD: `bb64c7c38ec16b6aa35c8bdf8026a723bce20b9e` at slice start
- Unity: `/home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity` (`6000.4.10f1`, revision `feeafc12a938`)
- Working tree clean: yes at slice start
- Last passing commit: `bb64c7c38ec16b6aa35c8bdf8026a723bce20b9e`

## Current slice

- Slice ID: `0.4a` — characterize deterministic Butler/guest scale and sort across independent cold starts before changing scale behavior
- Sole ownership change: architecture evidence gains one canonical startup scale/sort fingerprint and a ten-process cold-start gate; no runtime or editor ownership changes occur in this characterization sub-slice.
- Starting commit: `bb64c7c38ec16b6aa35c8bdf8026a723bce20b9e`
- Allowed files/assets: this execution-state record; the existing `Assets/Tests/PlayMode/ArchitectureBaselinePlayModeTests.cs`; and new evidence files only under `Docs/Architecture/Overhaul/Evidence/Slice_0_4/`. Deterministic generated reports may be refreshed only if a required gate proves them stale. No production script, editor tool, scene, prefab, ScriptableObject, existing `.meta`, package, ProjectSettings, or large asset may change.
- Compatibility surface preserved: every existing public API, script GUID, serialized file ID/reference, all nineteen Butler endpoint records, all nineteen guest calibration records, eight guest captured-base values, Player prefab presentation multiplier `0.7528645`, existing room perspective profiles, and approved visuals/timing/gameplay. Current scale components remain temporary compatibility owners; the profile conversion and sole `ActorPresenter` transfer remain Phase `4` work.
- Characterization test: preserve Slice `0.3` PlayMode `4/4` and full EditMode `264/218/46/0` with failed-test SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`; add one real PlayMode cold-start observation that performs MainMenu -> New Game -> Gameplay at `1366x768`, samples the Butler at the existing front-door approach, stages Chapter 2 without starting random panic motion, and emits a canonical identity-sorted fingerprint for all eight visible guests.
- Focused test filter and expected count: the new cold-start test method must produce exactly `1/1/0/0` in each of ten independent Unity processes; the combined `ArchitecturePlayModeDiscoveryTests;ArchitectureBaselinePlayModeTests` filter must then produce exactly `5/5/0/0`. Architecture-tool unit tests remain `4/4`.
- PlayMode/manual/golden gate: all ten independent processes must produce byte-identical canonical scale/sort fingerprints, exact actor identities/room/count, and no tracked-file mutation. Preserve the accepted entrance values: presentation multiplier `0.7528645 +/- 0.000001`, Butler height `114.417 +/- 0.5 px`, door height `141.481 +/- 0.5 px`, ratio `0.808710 +/- 0.001`, and sorting order `1075`; the broad visual sanity remains `0.65-0.85` rather than silently retuning the approved baseline to mathematical `0.75`.
- Rollback commit: `bb64c7c38ec16b6aa35c8bdf8026a723bce20b9e`

## Evidence

- Static guard: passed for Slice `0.4a`; all architecture-tool tests pass `4/4`, the architecture guard passes, and the deterministic audit remains `112` runtime files / `48,789` lines.
- Runtime ledger: passed, `112` runtime files / `112` exact rows.
- Unity script integrity: passed, `157` current scripts / `1,926` serialized references / `856` external-package references.
- Compile result: passed with no compiler errors in every certified cold-start process; the dedicated compile log remains `Logs/ArchitectureOverhaul/Slice-0.4/compile-final.log`.
- EditMode XML: `Logs/ArchitectureOverhaul/Slice-0.4/editmode-full.xml` is nonempty and verified at exactly `264` total / `218` passed / `46` known failed / `0` skipped, with unchanged 46-case SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`.
- PlayMode XML: the combined real PlayMode filter passes exactly `5/5/0/0` at `Logs/ArchitectureOverhaul/Slice_0_4/playmode-combined-candidate.xml`. With the approved digest assertion enabled, the cold-start method separately passes exactly `1/1/0/0` in each of ten final independent display-backed processes under `Logs/ArchitectureOverhaul/Slice-0.4/cold-start-10-certified/`.
- Manual/golden result: all ten independent runs produce fingerprint SHA-256 `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`, Butler `114.417 px`, entrance door `141.481 px`, ratio `0.808710`, presentation multiplier `0.7528645`, sort `1075`, and the same eight identity-sorted Drawing Room guest scale/sort records. The tracked status and binary diff digest stayed byte-identical during every process.
- Scene/prefab diff reviewed: passed; the selected calibration-sensitive scene, Player prefab, two current `RoomPerspectiveProfile` assets, and relevant script metas remain byte-identical to `Evidence/Slice_0_4/calibration_asset_hashes.csv`. Full Git diff review shows no other scene, prefab, ScriptableObject, or `.meta` change, and no Unity GUID was added or changed.

## Completed slices

| Slice | Commit | Tests | Notes |
|---|---|---|---|
| `0.1` | `872875aa4e3381993c3bb5d9c32a4393a7defe17` | Baseline evidence inherited | Recovery branch and tag established from the reviewed architecture head. |
| `0.2` | `62a389254d4cedfc8128b181d0a17bbe5fdaebf3` | Static controls | Final ledgers, verifier, integrity scanner, and slice-gate controls installed. |
| `0.2 evidence` | `8ec90df46a0e2917a52356692e4938140ed66081` | Integrity scanner `155/1926/856`; guard and ledger pass | Deterministic `script_integrity.csv` tracked as its own commit. |
| `0.2.1` | `daa11ed6d746e1a74c82ddec985ed8286d2b6cf9` | Verifier tests `3/3`; exact baseline XML `264/218/46/0` | NUnit failure evidence now excludes failed suite paths and verifies exact result counts. |
| `0.3` | `bb64c7c38ec16b6aa35c8bdf8026a723bce20b9e` | Real PlayMode `4/4`; lifecycle `10/10`; focused EditMode `31/31`; full EditMode `264/218/46/0`; static controls pass | Real startup, movement/collision, canonical room round trip, current hidden-child defect, Butler visual baseline, and Chapter 2 panic measurements committed without production changes. |

## Remaining adapters and debt

- All migration debt in the final runtime/editor ledgers remains unless explicitly completed above.
- The legacy `264` tests remain discovered as EditMode, including ten editor-hosted `[UnityTest]` methods that depend on `UnityEditor`, `AssetDatabase`, and predefined-assembly production types. Four new black-box tests now run in a genuine PlayMode test assembly; mechanically relocating the ten editor-hosted tests remains a Phase 8 test-structure task rather than a production architecture change.
- `RoomContentGroup` still force-enables an intentionally hidden child when its room is reactivated. Slice `0.3` records that existing defect; the later room-visibility ownership slice must reverse the assertion.
- The `46` exact legacy EditMode failures remain migration debt and are not an accepted final state.

## Exact next safe slice

- Complete Slice `0.4a` from the clean Slice `0.3` commit. If all ten cold starts match, begin a separate `0.4b` editor-only slice that makes calibration-window repaint/open/selection reads non-mutating and protects every intentional write with Undo. Change runtime scale behavior only in a later sub-slice if a focused characterization first proves a deterministic defect.

## Resume command

```text
Give Codex CODEX_RESUME_PROMPT.md.
```
