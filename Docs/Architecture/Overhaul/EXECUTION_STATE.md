# Architecture-overhaul execution state

## Repository

- Branch: `refactor/final-architecture-overhaul`
- HEAD: `e795181c0e27d1cb9d85eed2694013ac45995b46` at slice start
- Unity: `/home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity` (`6000.4.10f1`, revision `feeafc12a938`)
- Working tree clean: yes at slice start
- Last passing commit: `e795181c0e27d1cb9d85eed2694013ac45995b46`

## Current slice

- Slice ID: `1.1` — establish typed stable-identity contracts without migrating serialized identity storage
- Sole ownership change: Core becomes the sole owner of the canonical stable-ID syntax and the six domain value contracts (`RoomId`, `PassageId`, `ActorId`, `ChapterId`, `BeatId`, and `ObjectiveId`). Room and passage definitions expose typed read seams, and `StoryBeatBase` exposes a typed identity while retaining its existing string API. This slice does not transfer any gameplay state or replace any serialized string field.
- Starting commit: `e795181c0e27d1cb9d85eed2694013ac45995b46`
- Allowed files/assets: this execution-state record; `Docs/Architecture/Overhaul/FINAL_RUNTIME_MIGRATION_LEDGER.csv`; `Docs/Architecture/Overhaul/FINAL_EDITOR_TOOL_LEDGER.csv`; the Phase `1.1` migration-report entry; generated architecture evidence; new folder metadata and `Assets/_Chateau/Runtime/Core/Contracts/IDs/StableIds.cs`; `Assets/_Chateau/Runtime/World/Rooms/RoomDefinition.cs`; `Assets/_Chateau/Runtime/World/Rooms/Passages/PassageDefinition.cs`; `Assets/_Chateau/Runtime/Gameplay/StoryBeatBase.cs`; and one focused `StableIdentityContractTests` EditMode source plus its new `.meta`. No scene, prefab, ScriptableObject, package, ProjectSettings, large asset, existing `.meta`, or serialized identity field may change.
- Compatibility surface preserved: `DefinitionAssetBase.stableId`, every legacy room/actor/chapter string, `RoomDefinition.StableId`, `PassageDefinition.StableId`, `StoryBeatBase(string)`, `StoryBeatBase.BeatId`, all public runtime APIs, all existing GUIDs and serialized file IDs/references, and the certified visual fingerprint `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`. The new contracts reject display labels but do not silently rewrite legacy serialized values.
- Characterization test: focused EditMode coverage must prove all six types accept their approved canonical token forms, reject display labels and malformed tokens, remain type-separated, handle defaults and invalid parsing explicitly, round-trip through Unity serialization, expose typed canonical room/passage IDs, and preserve the existing StoryBeat string surface.
- Focused test filter and expected count: `StableIdentityContractTests` must discover and pass exactly `12/12` tests with zero failures or skips. Run it once after the cohesive batch, not after each test-only adjustment.
- PlayMode/manual/golden gate: compile under Unity `6000.4.10f1`, inspect the complete diff for zero serialized asset mutation, then run the certified cold-start fingerprint exactly once and require `1/1` with digest `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`. Manually confirm only new `.meta` GUIDs exist and every existing GUID remains unchanged.
- Rollback commit: `e795181c0e27d1cb9d85eed2694013ac45995b46`

## Evidence

- Static guard: passed for Slice `1.1`; all architecture-tool tests pass `4/4`, the architecture guard reports no debt above the committed baseline, and the generated audit remains at `48` direct `MonoBehaviour` declarations with unchanged smell totals.
- Runtime ledger: passed, `113` runtime files / `113` exact rows; the one new runtime contract file is explicitly classified `KEEP` under Core ownership.
- Unity script integrity: passed, `159` current scripts / `1,926` serialized references / `856` external-package references. Both new script GUIDs are unique and correctly report zero serialized references.
- Compile result: Unity `6000.4.10f1` final batch compilation passes with no compiler errors; evidence is `Logs/ArchitectureOverhaul/Slice-1.1/compile-final.log`.
- EditMode XML: the post-review `Logs/ArchitectureOverhaul/Slice-1.1/editmode-focused-certified.xml` is verified at exactly `12/12/0/0` and enumerates all eight current room plus fourteen current passage assets; the compatibility filter across identity, Core foundation, and canonical room/passage contracts is independently verified at `34/34/0/0` in `editmode-compatibility.xml`.
- PlayMode XML: `Logs/ArchitectureOverhaul/Slice-1.1/cold-start.xml` passes exactly `1/1/0/0` and asserts fingerprint SHA-256 `34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96`.
- Manual/golden result: the certified Butler remains `114.417 px` against the `141.481 px` entrance door (`0.808710` ratio), presentation multiplier `0.7528645`, and sort `1075`; all eight guest scale/sort records remain byte-for-byte represented by the same fingerprint.
- Scene/prefab diff reviewed: passed; no `.unity`, `.prefab`, `.asset`, package, ProjectSettings, existing `.meta`, or large-asset file changed. The only new metadata belongs to the two new folders and two new scripts; no existing Unity GUID or serialized identity field changed.

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

## Failed-gate audit within current slice

- `editmode-focused-final.xml` initially produced `12 total / 11 passed / 1 failed`: the expanded current-asset assertion expected eight rooms but received zero.
- Diagnosis: Unity `AssetDatabase.FindAssets("t:RoomDefinition", ...)` does not resolve the namespaced canonical definition type in this project. The fourteen-passage query used the same unsupported assumption. No runtime code, scene, prefab, ScriptableObject, or existing metadata changed.
- Minimal in-scope repair: enumerate only the two canonical data folders and retain assets through typed `LoadAssetAtPath<T>` filtering. The exact eight-room/fourteen-passage identity, uniqueness, and string-preservation assertions remain unchanged. Preserve the failed XML/log and require the complete `12/12` focused rerun before commit.
- Repair result: `editmode-focused-certified.xml` passes exactly `12/12/0/0`; the failed `editmode-focused-final.xml` and its log remain preserved as diagnostic evidence under `Logs/ArchitectureOverhaul/Slice-1.1/`.

## Remaining adapters and debt

- All migration debt in the final runtime/editor ledgers remains unless explicitly completed above.
- The legacy `264` tests remain discovered as EditMode, including ten editor-hosted `[UnityTest]` methods that depend on `UnityEditor`, `AssetDatabase`, and predefined-assembly production types. Four new black-box tests now run in a genuine PlayMode test assembly; mechanically relocating the ten editor-hosted tests remains a Phase 8 test-structure task rather than a production architecture change.
- `RoomContentGroup` still force-enables an intentionally hidden child when its room is reactivated. Slice `0.3` records that existing defect; the later room-visibility ownership slice must reverse the assertion.
- The `46` exact legacy EditMode failures remain migration debt and are not an accepted final state.
- Typed IDs are additive boundaries only: legacy room display strings, `guest_1` actor aliases, and serialized chapter strings are not migrated in Slice `1.1`. Actor aliases require an explicit Phase `4` mapping; no name-derived conversion is permitted.
- `ChapterId` provides lexical/type safety but not catalog membership. Scheduler event IDs remain strings and must not cross the typed chapter boundary; Phase `1.5` database indexes and Phase `5` chapter definitions must reject unregistered values.
- `StoryBeatBase(string)` remains a compatibility constructor and reports no valid typed ID for a legacy label. New beat implementations must use `StoryBeatBase(BeatId)`.

## Exact next safe slice

- Commit the fully passing Slice `1.1` from rollback commit `e795181c0e27d1cb9d85eed2694013ac45995b46`. Then begin Slice `1.2` from that clean commit: characterize `GameContext` initialization order and add explicit typed service properties without a generic lookup, singleton, or service-state duplication.

## Resume command

```text
Give Codex CODEX_RESUME_PROMPT.md.
```
