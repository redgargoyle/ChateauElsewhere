# Codex master assignment — finish the Chateau Chantilly architecture overhaul

You are working inside the **full** `redgargoyle/ChateauElsewhere` Unity repository, including all large assets omitted from the review snapshot.

## Starting state

- Expected source branch: `latest_architecture`.
- Reviewed commit: `872875aa4e3381993c3bb5d9c32a4393a7defe17` (`feat(navigation): bind billiard route foundation`).
- Unity: `6000.4.10f1`.
- The branch already contains a real `GameRoot`, `GameContext`, `GameDatabase`, base families, 8 canonical rooms, 14 canonical passages, a partial navigation migration, and a three-object `SetPieceView` pilot.
- It is **not** finished. Giant chapter, player, navigation, UI, camera and lighting controllers remain authoritative.
- Do not reapply an old foundation patch.
- Do not restart from the pre-foundation branch.

If the current head is newer than `872875aa`, inspect every intervening commit and update baseline evidence before editing. Do not discard newer valid work.

## Read before editing

Read completely:

1. `Docs/Architecture/Overhaul/FINAL_TARGET_ARCHITECTURE.md`
2. `Docs/Architecture/Overhaul/EXACT_MIGRATION_SLICES.md`
3. `Docs/Architecture/Overhaul/FINAL_RUNTIME_MIGRATION_LEDGER.csv`
4. `Docs/Architecture/Overhaul/FINAL_EDITOR_TOOL_LEDGER.csv`
5. existing `Docs/Architecture/ARCHITECTURE.md`
6. existing `Docs/Architecture/MIGRATION_PLAN.md`
7. existing `Docs/Architecture/MIGRATION_REPORT.md`
8. existing `Docs/Architecture/PRUNE_LOG.md`
9. existing `Docs/Architecture/TESTING_AND_RECOVERY.md`
10. existing `Docs/Architecture/RemainingRouteInventory.csv`
11. the approved Chapter 1/2 source-of-truth script in `Docs`.

The files in `Docs/Architecture/Overhaul` supersede earlier broad sequencing where they conflict, but approved behavior and serialized evidence remain authoritative.

## Mission

Complete the architecture migration and produce a clean release-demo codebase while preserving approved gameplay, visuals, audio, timing, content bindings and Unity serialization.

“Complete” means all target owners are real, all legacy facades and duplicate implementations are removed after proof, all current runtime and editor files are explicitly classified, the project hierarchy is clean, and the demo can run Main Menu, Chapter 1 and Chapter 2 with a real save/Continue path.

It does **not** authorize one giant rewrite. Execute this one assignment as many small, independently tested, reversible commits.

## Architecture constitution

```text
                                  GameRoot
                                         |
                       +-----------------+-----------------+
                       |                                   |
                     STORY                                GAME
                       |                                   |
        +--------------+-------------+       +-------------+-------------+
        |                            |       |             |             |
   GameFlowService              StoryState  World       Scheduler       Save
        |                                      |             |
   ChapterController                         House          Clock
        |                                      |
     StoryBeat                              Room instances
        |                                      |
    Objective                         +---------+----------+
        |                             |                    |
Story interaction requirement      Actors                Props
                                      |          +---------+---------+
                                  Player/Guests Passages SetPieces Lights
```

Central rule:

> Story decides what should happen. Game determines and records what physically happens. Physical interactions translate player intent into typed results. Game events report results back to Story.

`GameRoot` composes and validates. It owns no gameplay state.

## Sole-owner rules

There must ultimately be exactly one owner for each:

- current chapter/beat/objectives: `GameFlowService` + `StoryState`;
- time: `ClockService`;
- scheduled callbacks: `GameScheduler`;
- current room and transition transaction: `NavigationService`;
- active room root: `RoomViewService`;
- actor logical state: one `ActorControllerBase` per actor;
- actor registry/room index: `ActorRegistry`;
- movement execution: one `RoomStageMotor` per actor;
- actor position/scale/tint/sorting: one `ActorPresenter` per actor;
- pointer/click/modal/cursor routing: `InputRouter`/`InteractionRouter`;
- camera framing: `CameraService`;
- runtime UI root/modal stack: `UIService`;
- dialogue/subtitle/voice queue: `DialogueService`;
- audio settings/ambience/one-shots: `AudioService`;
- runtime light state: `LightingService`;
- durable session state: `SaveService`;
- set-piece cutout/depth: `SetPieceView`;
- walk boundaries/no-walk footprints: `RoomNavigationGeometry`.

Do not create a second owner and promise to clean it later. A temporary facade may delegate, but may not retain duplicate state.

## Set-piece rule

Couches, desks, beds, toys, tables, chairs, cabinets, fireplaces and other cutout scenery are `Props/SetPieces`.

```text
SetPiece
├── SetPieceView
│   ├── SpriteRenderer
│   ├── room-local occlusion anchor
│   └── sorting profile/offset
└── RoomNavigationGeometry
    └── authored no-walk footprint
```

Do not create `CouchManager`, `BedController` or other furniture-specific managers. Rendering and collision remain separate owners.

## Non-negotiable safety rules

1. Create `refactor/final-architecture-overhaul` from the reviewed/current `latest_architecture` head.
2. Tag the start. Never modify or force-push the stable branch.
3. Require a clean tree before each migration slice.
4. One ownership transfer per slice; one passing commit per slice.
5. Characterize behavior before changing it.
6. Introduce replacement, migrate consumers and delete legacy in separate slices.
7. Preserve every `.meta`, GUID and serialized file ID until an explicit Unity migration changes it.
8. Use `git mv`; never recreate moved Unity assets without their metadata.
9. Use `[FormerlySerializedAs]` for serialized field renames.
10. Use Unity Editor migration commands for component/prefab/scene replacements.
11. No `git reset --hard`, `git clean`, force checkout, force push or destructive asset regeneration.
12. No generic service locator, new global singleton or global event bus.
13. No new required dependency repaired with `FindObject*`, `new GameObject`, `AddComponent` or arbitrary `Resources.Load`.
14. Do not increase architecture-debt baselines to hide a regression.
15. Do not move large source trees, add namespaces or add asmdefs before ownership is stable.
16. Do not delete an asset because its name says `Legacy`; prove it is obsolete.
17. Do not claim a test passed without a nonempty result XML and expected test count.
18. Never pass `-quit` together with Unity `-runTests`.
19. Do not run a batch Unity instance while the same project is open interactively.
20. Stop only for a real failed gate, dirty starting state, unexpected Unity serialization mutation, missing required validation environment or a product ambiguity that changes approved behavior. Make routine professional decisions independently.

## Install the continuation controls first

Copy the supplied overhaul files into:

```text
Docs/Architecture/Overhaul/
Tools/architecture/
```

Run:

```bash
python3 Tools/architecture/validate_runtime_ledger.py --project-root .
python3 Tools/architecture/scan_unity_script_integrity.py --project-root .
python3 Tools/architecture/guard.py --project-root .
```

Commit only the tooling/docs as:

```text
chore(architecture): install final overhaul controls
```

## Mandatory slice protocol

Before each slice, write to `Docs/Architecture/Overhaul/EXECUTION_STATE.md`:

- slice ID and one ownership change;
- starting commit;
- allowed files/assets;
- compatibility surface to preserve;
- characterization test;
- focused gate;
- PlayMode/manual/golden gate;
- rollback commit.

Then:

1. verify clean tree;
2. implement the smallest change;
3. run `git diff --check`;
4. run architecture audit/guard;
5. run runtime-ledger and Unity-script-integrity checks;
6. compile in Unity `6000.4.10f1`;
7. run focused tests without `-quit`;
8. verify XML with `verify_nunit_xml.py`;
9. run relevant PlayMode/manual/golden test;
10. inspect all scene/prefab/asset/meta changes;
11. update ledger/report/prune evidence;
12. commit only when every gate passes.

Use the supplied `run_architecture_slice_gate` script for the static/compile/focused portion. It is not a substitute for the relevant PlayMode and manual/golden gate.

## Execute the phases in this order

Follow every slice in `EXACT_MIGRATION_SLICES.md`. The required order is summarized below.

### Phase 0 — trustworthy baseline

- branch/tag/baseline evidence;
- install controls;
- correct EditMode result evidence;
- real PlayMode startup/room/navigation/scale/dialogue tests;
- fixed-resolution golden captures;
- deterministic Butler/guest scale and non-mutating calibration windows.

Architecture changes do not continue until the visual baseline is repeatable.

### Phase 1 — Core, Clock and Data

- stable ID contracts;
- typed `GameContext`;
- strict `GameRoot`;
- `ClockService` as sole time owner;
- `GameScheduler` as sole timed-callback owner;
- all 19 `RoomDefinition`s and typed `GameDatabase` indexes.

### Phase 2 — World and Navigation

Continue from current Group 06; do not redo completed Groups 00–05.

- finish Groups 06–19 one pair per commit;
- model Groups 20–21 as explicit directed one-way or real authored reverse passages according to approved behavior;
- model Group 22 as independent directed parallel/shared-return passages—never first-match inference;
- add `PassageTopologyKind` and precise validators;
- create target `NavigationService`, `RoomViewService`, `InteractionRouter` behind stateless facades;
- migrate every door caller to `PassageInteraction`;
- transfer room-root activation;
- remove legacy route data, managers, triggers, prompts and room-content activation after proof.

Completion requires every directed passage test, zero legacy route refs and one current-room writer.

### Phase 3 — Rooms, set pieces, collision and occlusion

- generate complete cutout/collision inventory;
- add one `RoomNavigationGeometry` owner per room;
- migrate all set pieces room by room;
- finish Dining Room seated occlusion through actor presentation data;
- remove duplicate sorting and blocker components after zero-reference proof.

### Phase 4 — Actors

- stable actor definitions and `ActorRegistry`;
- `RoomStageMotor`;
- sole `ActorPresenter`;
- `ActorAnimator` and shared `ActorAudioEmitter`;
- player and guest command sources;
- dedicated Player and Guest prefab variants;
- representative guest full trace, then seven remaining guests;
- remove all legacy movement/scale/sort/state components after proof.

Completion requires one motor and one presenter per actor and no player input on guests.

### Phase 5 — Story

- `GameFlowService` + explicit `StoryState`;
- objective model and typed Story/Game boundary;
- Chapter 1 beats, one per commit;
- Chapter 2 beats, one per commit;
- physical coat closet, doorbell and actor interactions remain under Game;
- delete giant chapter facades/feature controllers after full chapter traces.

### Phase 6 — Presentation and remaining props

- `InputRouter`, cursor and interaction path;
- one authored UI root/screens;
- `DialogueService` + subtitle/speaker views;
- `AudioService` + merged ambience + actor emitters;
- `CameraService` + serialized rig;
- `LightingService` + Editor bake tools;
- one frame-sequence implementation;
- remove runtime UI/camera/light factories and duplicate catalogs/controllers.

### Phase 7 — Save and menu

- versioned `SaveService`;
- durable owner-state capture/restore;
- real Continue path or visibly disabled when no valid save;
- save/load round-trip and MainMenu PlayMode tests.

### Phase 8 — final pruning and project organization

- apply every row of `FINAL_RUNTIME_MIGRATION_LEDGER.csv`;
- apply every row of `FINAL_EDITOR_TOOL_LEDGER.csv`;
- maintain `Docs/Architecture/Overhaul/PRUNE_MANIFEST.csv` with proof;
- move non-runtime archives/source/previews outside `Assets` after reference/hash audit;
- move first-party runtime files with `.meta` into final Story/Game ownership tree;
- remove empty `Assets/Scripts`, `Assets/Map`, `Assets/_Chateau/Scripts` and root `Assets/Editor`;
- add namespaces and bottom-up acyclic asmdefs last.

### Phase 9 — release-demo certification

- zero untriaged test failures;
- no missing scripts/duplicate GUIDs;
- no required runtime repair paths;
- all routes, rooms, actors, set pieces, chapters and save flow pass;
- target build succeeds;
- final class catalog, prune manifest, flowcharts and Hamza guide generated from the actual repository.

## Class/file decisions

`FINAL_RUNTIME_MIGRATION_LEDGER.csv` covers all 112 runtime files in the reviewed snapshot. Treat every row as a required disposition, not a suggestion. If newer files exist, add them before changing behavior. No runtime file may finish as `MIGRATE/REVIEW`, `UNKNOWN`, `TEMPORARY` or unclassified.

`FINAL_EDITOR_TOOL_LEDGER.csv` covers all 43 reviewed Editor files. Tests must move into real test folders/assemblies. One-off repair/migration tools must be deleted after use. Permanent tools must have documented workflows.

## Prune proof

Before deleting any file, append a row to `PRUNE_MANIFEST.csv` containing:

- file/GUID;
- former responsibility;
- replacement owner;
- code-reference result;
- serialized-reference result;
- UnityEvent/animation/reflection/resource audit;
- migration tool used, if any;
- replacement tests;
- manual/golden evidence;
- migration commit;
- deletion commit.

No proof, no deletion.

## Required final deliverables

Create/update:

```text
Docs/Architecture/Final/FINAL_ARCHITECTURE.md
Docs/Architecture/Final/HIGH_LEVEL_FLOWCHART.md
Docs/Architecture/Final/HIGH_LEVEL_FLOWCHART.mmd
Docs/Architecture/Final/CLASS_ORGANIZATION.mmd
Docs/Architecture/Final/CHAPTER_1_RUNTIME_TRACE.md
Docs/Architecture/Final/CHAPTER_2_RUNTIME_TRACE.md
Docs/Architecture/Final/ROOM_TRANSITION_TRACE.md
Docs/Architecture/Final/ACTOR_COMPOSITION_TRACE.md
Docs/Architecture/Final/SET_PIECE_TRACE.md
Docs/Architecture/Final/SAVE_TRACE.md
Docs/Architecture/Final/FINAL_PROJECT_TREE.txt
Docs/Architecture/Final/CLASS_CATALOG.md
Docs/Architecture/Final/CLASS_CATALOG.csv
Docs/Architecture/Final/PRUNE_MANIFEST.md
Docs/Architecture/Final/PRUNE_MANIFEST.csv
Docs/Architecture/Final/EDITOR_TOOL_LEDGER.md
Docs/Architecture/Final/ASSET_CLEANUP_REPORT.md
Docs/Architecture/Final/TEST_REPORT.md
Docs/Architecture/Final/MIGRATION_LOG.md
Docs/Architecture/Final/DECISIONS.md
Docs/Architecture/Final/CONTINUE_DEVELOPMENT.md
Docs/Architecture/Final/HAMZA_TLDR.md
Docs/Architecture/Overhaul/EXECUTION_STATE.md
```

Generate diagrams from the implemented code—not from the proposal.

## Required final metrics

Report baseline versus final:

- runtime file and LOC counts;
- all object-search/factory/resource counts;
- current-room writers;
- story-state writers;
- actor transform/scale/sort writers;
- movement executors on player and guest prefabs;
- runtime UI roots;
- required runtime repair hooks;
- unresolved script GUIDs;
- test counts and failures;
- files deleted/merged/moved;
- Editor tools deleted/consolidated;
- assets archived/deleted;
- LOC of former god controllers and their final replacements.

Do not optimize for a deletion percentage. Optimize for zero unjustified files, zero duplicate owners and a clean dependency graph.

## Session continuation

This assignment may exceed one Codex session. Before ending any session:

1. finish or revert the current slice;
2. leave a clean passing commit;
3. update `EXECUTION_STATE.md` with completed slices, current metrics, latest test evidence and exact next slice;
4. include the command needed to resume.

On the next session, read this master prompt and `EXECUTION_STATE.md`, verify the clean commit and continue from the exact next slice. Do not restart analysis or repeat completed migrations.

## Completion response

When—and only when—the definition of done passes, report:

1. branch and final commit;
2. release-demo build path;
3. test evidence;
4. concise final architecture diagram;
5. final major-class list by Story and Game ownership;
6. complete deletion/prune summary;
7. remaining intentional debt, ideally none for the demo scope;
8. exact steps for Hamza to open, test and continue development.

Do not describe the overhaul as complete while facades, duplicate owners, untriaged failures or unclassified files remain.
