# Architecture-overhaul execution state

## Repository

- Branch: `refactor/final-architecture-overhaul`
- HEAD: `daa11ed6d746e1a74c82ddec985ed8286d2b6cf9` at slice start
- Unity: `/home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity` (`6000.4.10f1`, revision `feeafc12a938`)
- Working tree clean: yes at slice start
- Last passing commit: `daa11ed6d746e1a74c82ddec985ed8286d2b6cf9`

## Current slice

- Slice ID: `0.3` — establish real EditMode, PlayMode, and fixed-resolution golden test truth without changing production behavior
- Sole ownership change: architecture evidence gains an explicit real-PlayMode test boundary and committed fixed-resolution measurements; no runtime ownership transfers in this slice.
- Starting commit: `daa11ed6d746e1a74c82ddec985ed8286d2b6cf9`
- Allowed files/assets: this execution-state record; the new `Assets/Tests.meta`, `Assets/Tests/PlayMode.meta`, and test-only files with matching new `.meta` files under `Assets/Tests/PlayMode/`; the existing `Assets/Editor/GameplayLifecycleCharacterizationTests.cs` and `.meta` only if a minimal test split proves necessary; `Docs/Architecture/Overhaul/Evidence/Slice_0_3/`; deterministic reports under `Docs/Architecture/Generated/`; and the architecture audit's shared runtime-file classifier plus its focused unit test if needed to keep test-only assemblies out of production metrics.
- Compatibility surface preserved: all production scripts and script GUIDs, public APIs, serialized file IDs/references, scenes, prefabs, ScriptableObjects, art/audio assets, approved gameplay, visuals, timing, scale, dialogue, input, room navigation, and current known defects. No production `.asmdef`, namespace, service, owner, or behavior may be introduced or changed.
- Characterization test: the fresh full EditMode result at `Logs/ArchitectureOverhaul/Slice-0.3/baseline-editmode-8ec90df4.xml` must remain exactly `264/218/46/0` with failed-test SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9` until any tests are deliberately reclassified; every reclassified test must be accounted for by exact EditMode plus PlayMode counts.
- Focused test filter and expected count: `ArchitecturePlayModeDiscoveryTests;ArchitectureBaselinePlayModeTests` must produce exactly `4/4` real PlayMode tests with zero failures; all architecture-tool unit tests must pass. The existing `GameplayLifecycleCharacterizationTests` filter remains expected `10/10` until explicitly split.
- PlayMode/manual/golden gate: at `1366x768`, prove MainMenu -> New Game -> Gameplay, initial GameRoot/service/chapter/beat/time/room state, one passage round trip, room-root visibility and the current hidden-child behavior, player movement/collision, Butler scale, and dialogue/subtitle startup; commit entrance, Drawing Room, one passage-transition, and one Chapter 2 panic-frame measurements.
- Rollback commit: `daa11ed6d746e1a74c82ddec985ed8286d2b6cf9`

## Evidence

- Static guard: passed for Slice `0.3`; all architecture-tool tests pass `4/4`, the architecture guard passes, and the deterministic audit remains `112` runtime files / `48,789` lines.
- Runtime ledger: passed, `112` runtime files / `112` exact rows.
- Unity script integrity: passed, `157` current scripts / `1,926` serialized references / `856` external-package references.
- Compile log: passed with no compiler errors at `Logs/ArchitectureOverhaul/Slice-0.3/compile-after-movement-daa11ed6.log`.
- EditMode XML: `Logs/ArchitectureOverhaul/Slice-0.3/editmode-final-daa11ed6.xml` is nonempty and verified at exactly `264` total / `218` passed / `46` known failed / `0` skipped, with the same 46-case SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. The focused architecture filter passes `31/31`, and the preserved `GameplayLifecycleCharacterizationTests` filter separately passes `10/10`.
- PlayMode XML: passed `4/4` at `Logs/ArchitectureOverhaul/Slice-0.3/playmode-after-movement-daa11ed6.xml`; the test-only `Chateau.Tests.PlayMode` assembly advances the real player loop, exercises actual player movement over frames, and has no production dependency edge.
- Manual/golden result: automated fixed-resolution measurements passed at `1366x768`; committed evidence covers Entrance, Drawing Room, a canonical passage round trip and a Chapter 2 panic frame. Entrance Butler/door ratio at the real door approach is `0.808710`, with approved presentation scale `0.7528645`.
- Scene/prefab diff reviewed: passed; no existing scene, prefab, ScriptableObject, production asset, production script, or existing `.meta` file changed. The only new Unity GUIDs belong to the test folder, PlayMode test assembly, and its two test scripts.

## Completed slices

| Slice | Commit | Tests | Notes |
|---|---|---|---|
| `0.1` | `872875aa4e3381993c3bb5d9c32a4393a7defe17` | Baseline evidence inherited | Recovery branch and tag established from the reviewed architecture head. |
| `0.2` | `62a389254d4cedfc8128b181d0a17bbe5fdaebf3` | Static controls | Final ledgers, verifier, integrity scanner, and slice-gate controls installed. |
| `0.2 evidence` | `8ec90df46a0e2917a52356692e4938140ed66081` | Integrity scanner `155/1926/856`; guard and ledger pass | Deterministic `script_integrity.csv` tracked as its own commit. |
| `0.2.1` | `daa11ed6d746e1a74c82ddec985ed8286d2b6cf9` | Verifier tests `3/3`; exact baseline XML `264/218/46/0` | NUnit failure evidence now excludes failed suite paths and verifies exact result counts. |

## Remaining adapters and debt

- All migration debt in the final runtime/editor ledgers remains unless explicitly completed above.
- The legacy `264` tests remain discovered as EditMode, including ten editor-hosted `[UnityTest]` methods that depend on `UnityEditor`, `AssetDatabase`, and predefined-assembly production types. Four new black-box tests now run in a genuine PlayMode test assembly; mechanically relocating the ten editor-hosted tests remains a Phase 8 test-structure task rather than a production architecture change.
- `RoomContentGroup` still force-enables an intentionally hidden child when its room is reactivated. Slice `0.3` records that existing defect; the later room-visibility ownership slice must reverse the assertion.
- The `46` exact legacy EditMode failures remain migration debt and are not an accepted final state.

## Exact next safe slice

- Begin Slice `0.4`: declare the deterministic Butler/guest scale ownership change from the clean Slice `0.3` commit, characterize the editor calibration write path, and gate ten cold starts at the approved entrance-door visual.

## Resume command

```text
Give Codex CODEX_RESUME_PROMPT.md.
```
