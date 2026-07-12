# Testing, migration, and recovery runbook

## Important limitation

The foundation patch was produced from the source-only project snapshot. It has been statically validated, but it cannot be declared production-ready until Unity 6000.4.10f1 compiles it, runs the tests, installs the root into the scene, and executes the gameplay smoke tests below.

## 1. Create a protected branch

```bash
git switch -c refactor/requirements-first-architecture
git status --short
git tag architecture-baseline-before-migration
```

Do not work directly on the only copy of `main`.

## Automated foundation gate

After applying the patch and making a clean branch, the full foundation gate can be run with either wrapper:

```powershell
Tools/architecture/run_foundation_gate.ps1 `
  -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe" `
  -ProjectPath "$PWD"
```

```bash
Tools/architecture/run_foundation_gate.sh \
  /path/to/Unity \
  "$PWD"
```

The wrapper runs static checks, EditMode tests, the GameRoot installer, a second EditMode pass, and PlayMode tests. It intentionally leaves the Unity-generated scene/data changes uncommitted for human review. Use `-SkipPlayMode` or `--skip-playmode` only when the project currently has no runnable PlayMode suite, and record that gap.

## 2. Run static checks before Unity

```bash
python Tools/architecture/guard.py --project-root .
python Tools/architecture/audit.py --project-root . --output Docs/Architecture/Generated
python Tools/architecture/serialized_refs.py --project-root . --output Docs/Architecture/Generated/serialized_script_refs.csv
```

The guard must pass. The audit is informational; old debt is expected to remain during migration.

## 3. Open with the exact editor version

Use Unity **6000.4.10f1**. Let script compilation and asset import finish completely.

Stop immediately if:

- any new compiler error appears;
- Gameplay or MainMenu reports missing scripts;
- Unity rewrites a large unrelated set of assets;
- the console shows duplicate service/root errors.

Record pre-existing errors separately.

## 4. Run EditMode tests before installing the root

In Test Runner, run all EditMode tests. Save the XML result if available. The new `ArchitectureFoundationTests` must pass.

If a brittle source-text test fails solely because an old architecture assumption changed, replace it with a behavioral or ownership assertion—do not reintroduce the old architecture to satisfy the string test.

## 5. Install the composition root

From Unity:

```text
Tools > Chateau > Architecture > Install or Refresh Gameplay GameRoot
```

Or in batch mode:

```bash
"<UNITY>" \
  -batchmode -quit \
  -projectPath "<PROJECT>" \
  -executeMethod Chateau.Editor.Architecture.GameRootInstaller.InstallGameplaySceneBatch \
  -logFile "Logs/game-root-install.log"
```

Review the scene diff. Expected changes are limited to:

- one `Chateau_GameRoot` object;
- `GameRoot`;
- `RoomNavigationManager` and `DoorPromptSequenceController` if previously absent;
- `SubtitleService` and `DialogueSpeechService` if previously absent;
- serialized root service/component lists;
- `Assets/_Chateau/Data/GameDatabase.asset`.

Unexpected broad scene changes are a rollback signal.

The active-scene validator also scans the full loaded hierarchy for missing MonoBehaviour script references.

## 6. Run all EditMode tests again

Do not continue until compilation is clean and all non-pre-existing tests pass.

## 7. Run PlayMode smoke tests manually

### Per-passage compatibility-template gate

Before binding any queued route, update and run the manifest-backed gate in `PassageMigrationCertificationTests` and follow [PASSAGE_COMPATIBILITY_TEMPLATE.md](PASSAGE_COMPATIBILITY_TEMPLATE.md). The scene—not `doors.txt`—is authoritative.

For one ordinary reciprocal pair, require:

- both actual trigger directions characterized from far and near starts before production changes;
- stable trigger/component IDs, route strings, interaction profile, room roots, and direct-edge baseline;
- exactly two reciprocal definition assets and two co-located Passage components;
- one shared logical point per room side, proven finite, collision-safe, path-reachable, source-independent, and inside the existing rendered activation envelope;
- canonical and temporarily nulled compatibility paths completing both directions with exact callback/audio/event order and cleanup;
- exact Unity YAML document/order review, existing `.meta`/GUID preservation, serialized-reference comparison, and idempotent import;
- architecture/foundation/manifest/focused rendered/full-suite gates; and
- a manual foot-placement/camera/room-visibility pass recorded below.

One-way and many-to-one route shapes stay explicitly blocked in the manifest until a separately characterized model can represent them. Do not invent reverse links to satisfy the current strict reciprocal contract.

### Boot and menu

- MainMenu opens without console errors.
- New Game reaches Gameplay.
- cursor selection/settings still work.
- Continue is not allowed to masquerade as a real restore; either verify restore or keep it visibly disabled.

### Room/navigation loop

- Every visible door/stair trigger responds once.
- Butler approaches the trigger correctly.
- room root changes exactly once.
- camera changes to the destination view.
- Butler arrives at the correct destination anchor.
- returning uses the corresponding reverse anchor.
- left and right parallel stairs return to their matching stair.
- leaving/re-entering preserves intentionally hidden renderers and story props.

### Chapter 1

- starts at 5:59 PM in the Grand Entrance Hall;
- arrivals fire at 6:00–6:03;
- delayed groups escalate bell behavior;
- all pending groups are admitted in order;
- one-coat constraint works;
- empty-handed wardrobe line works;
- guests become handled/seated only after coat flow;
- 6:04 empty bell works;
- completion waits for all conditions and Butler in Drawing Room;
- transition reaches Chapter 2 once.

### Chapter 2

- Drawing Room setup and objective appear;
- opening speech starts and is interrupted;
- monster performs three run/freeze cycles;
- panic audio/animation and scatter complete;
- each hidden guest can be found exactly once;
- conversation resumes correctly after leaving/returning;
- all guests found advances to 7:00 PM and Dining Room;
- Chapter 3 pending handoff occurs once.

### Cross-cutting

- dialogue, subtitles and voice remain synchronized;
- modal UI blocks movement and interactions;
- room ambience changes correctly;
- camera post-processing/flame bypass remains correct;
- lighting does not unexpectedly create/delete scene objects during play;
- no duplicate root, navigation, dialogue, subtitle or UI systems appear in the hierarchy.

## 8. Capture evidence

For each test pass, record:

- Unity version;
- git commit;
- scene;
- steps;
- expected/observed result;
- relevant console output;
- screenshot or short video for visual behavior;
- tester name/date.

## 9. Commit gates

Recommended commits:

1. foundation patch;
2. Unity-generated GameRoot scene/data migration;
3. test fixes that replace obsolete architecture assertions;
4. each later vertical slice;
5. each deletion set with prune proof.

Never combine a large feature migration, scene rewrite, and bulk deletion in one commit.

## 10. Recovery

If a gate fails:

1. save logs and test evidence;
2. do not pile another system beside the failed one;
3. revert to the last passing commit;
4. isolate the smallest behavior difference;
5. add a failing characterization test;
6. repair the target implementation;
7. rerun the complete phase gate.
