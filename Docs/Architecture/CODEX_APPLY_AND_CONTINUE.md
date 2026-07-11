# Codex execution prompt — apply, validate, install, and continue the Chateau cleanup

You are operating on the Chateau Chantilly Unity repository. The supplied foundation patch was generated from the source snapshot whose Unity version is `6000.4.10f1`. This is a staged, behavior-preserving migration. Do not perform a one-shot rewrite.

## Inputs

Place these beside or inside the repository as appropriate:

- `Chateau_Chantilly_Architecture_Foundation.patch`;
- this prompt;
- the original latest project/repository checkout.

Read before editing:

- `Docs/Architecture/ARCHITECTURE.md`;
- `Docs/Architecture/MIGRATION_PLAN.md`;
- `Docs/Architecture/TESTING_AND_RECOVERY.md`;
- `Docs/Architecture/PRUNE_LOG.md`.

## Hard rules

1. Work on a new branch.
2. Preserve Unity `.meta` files and script GUIDs.
3. Do not replace serialized components by renaming/deleting scripts without an Editor migration.
4. Do not add a service locator, singleton, generic scene-search helper, or runtime dependency repair.
5. Do not silence the architecture guard by increasing its baseline. The baseline may only decrease after a completed migration with an ADR/prune proof.
6. Do not delete a file without the evidence required by `PRUNE_LOG.md`.
7. Stop at the last passing commit when a gate fails.
8. Record pre-existing failures separately from patch-caused failures.

## Stage A — apply the foundation patch

```bash
git status --short
git switch -c refactor/requirements-first-architecture
git tag architecture-before-foundation
git apply --check --whitespace=nowarn Chateau_Chantilly_Architecture_Foundation.patch
git apply --whitespace=nowarn Chateau_Chantilly_Architecture_Foundation.patch
python Tools/architecture/guard.py --project-root .
python Tools/architecture/audit.py --project-root . --output Docs/Architecture/Generated
python Tools/architecture/serialized_refs.py --project-root . --output Docs/Architecture/Generated/serialized_script_refs.csv
git diff --check
```

If `git apply --check` fails, do not force it. Compare the repository to the uploaded snapshot, apply changes semantically while preserving `.meta` GUIDs, and document every conflict.

Commit the applied foundation before opening Unity.

## Stage B — compile in the exact Unity version

Open the project with Unity `6000.4.10f1`. Wait for all imports and compilation.

Required result:

- no new compiler errors;
- no missing scripts in MainMenu or Gameplay;
- `ArchitectureFoundationTests` are discovered;
- existing test failures are documented as pre-existing or patch-caused.

Likely compile-error policy:

- fix only the smallest type/API issue;
- preserve existing public APIs;
- do not revert the architecture by changing migrated classes back to direct `MonoBehaviour` merely to satisfy a brittle source-text test;
- replace architecture-string assertions with behavior/ownership assertions when appropriate.

Commit compile-only fixes separately.

## Stage C — run EditMode tests before scene migration

Run the full EditMode suite. Export results to `Logs/Tests/editmode-before-root.xml` if possible.

Do not install the root until all patch-caused failures pass.

## Stage D — install the composition root in Unity

Run either:

```text
Tools > Chateau > Architecture > Install or Refresh Gameplay GameRoot
```

or:

```bash
"$UNITY" \
  -batchmode -quit \
  -projectPath "$PWD" \
  -executeMethod Chateau.Editor.Architecture.GameRootInstaller.InstallGameplaySceneBatch \
  -logFile Logs/game-root-install.log
```

Inspect the diff. Only expected scene/data changes are allowed:

- one `Chateau_GameRoot`;
- `GameRoot`;
- one `RoomNavigationManager`;
- one `DoorPromptSequenceController`;
- one `SubtitleService`;
- one `DialogueSpeechService`;
- serialized lists of migrated services/behaviours;
- `Assets/_Chateau/Data/GameDatabase.asset` and its `.meta`.

If Unity changes unrelated room, actor, art, lighting, or animation assets, stop and investigate before committing.

Run `Tools > Chateau > Architecture > Validate Active Scene`. It must report zero errors. Commit scene/data migration separately.

## Stage E — regression gate

Run:

1. architecture guard;
2. full EditMode suite;
3. available PlayMode suite;
4. the manual checklist in `TESTING_AND_RECOVERY.md`.

Record results in `Docs/Architecture/MIGRATION_REPORT.md` with:

- commit hash;
- Unity version;
- tests run/passed/failed;
- pre-existing failures;
- hierarchy screenshots;
- gameplay observations;
- console errors/warnings;
- rollback point.

Do not remove bootstraps until this gate passes.

## Stage F — retire the first runtime repair paths

After Stage E passes, make a new commit that removes **only** repair paths whose required objects are now serialized and validated.

Candidate order:

1. `RoomNavigationBootstrap`;
2. ChapterManager's runtime self-creation path;
3. normal-game call sites of `SubtitleService.FindOrCreate` and `DialogueSpeechService.FindOrCreate`.

For each candidate:

- identify all callers;
- replace callers with explicit serialized/service references;
- add startup validation for the required reference;
- run all gates;
- add a prune-log entry;
- delete only after code and serialized-reference counts are zero.

Do not remove `UrpPostProcessingBootstrap` until a serialized render rig has been authored and visual parity is demonstrated.

## Stage G — first complete vertical slice: one passage round trip

Implement the target path for exactly one low-risk door pair:

```text
InteractionRouter -> PassageInteraction -> NavigationService
-> RoomViewService -> CameraService -> arrival anchor
```

Requirements:

- stable source/destination IDs;
- explicit arrival anchor in both directions;
- no destination display-name inference;
- no first-match reverse-door inference;
- unknown IDs fail validation;
- room activation does not force-enable descendants;
- PlayMode test covers both directions and renderer-state persistence.

Keep the existing path as a temporary adapter only for unmigrated doors. Mark every adapter with an owner, removal issue, and consumer list. Do not build a second permanent navigation system.

After the one pair passes, migrate remaining passages in small batches. Only then retire `doors.txt`, `DoorCameraSequence`, `RoomVisualCatalog`, and redundant prompt paths.

## Stage H — continue in the approved order

Follow `MIGRATION_PLAN.md`:

1. navigation and rooms;
2. actor identity/movement/presentation and dedicated guest prefab;
3. chapter beat extraction and one flow state owner;
4. UI/dialogue/audio/camera/lighting consolidation;
5. final prune proof;
6. namespaces and assembly definitions.

At the end of every stage, update:

- `Docs/Architecture/MIGRATION_REPORT.md`;
- `Docs/Architecture/PRUNE_LOG.md`;
- generated audit metrics;
- Hamza cards if implemented ownership differs from the design.

## Final acceptance criteria

Do not call the recovery complete until:

- the project compiles cleanly;
- all patch-caused EditMode and PlayMode failures are resolved;
- MainMenu and Gameplay have no missing scripts;
- required systems are serialized and validated, not runtime-repaired;
- one canonical route graph exists;
- one owner exists for chapter/beat, time, current room, room visibility, actor state, actor presentation, input/modal state, UI root, dialogue queue, audio settings, and save state;
- guests use dedicated guest prefabs with no player input components;
- every deleted file has prune proof;
- the architecture guard is lower than or equal to its original debt ceiling;
- Hamza can redraw the L0/L1/L2 maps and trace the two implemented chapters through the actual code.
