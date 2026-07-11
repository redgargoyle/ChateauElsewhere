# Foundation patch report

## Scope

This patch performs the maximum low-risk cleanup that can be justified from static source and Unity text-serialization evidence without running the editor. It deliberately stops before scene migration or large behavioral rewrites.

## Baseline snapshot

| Metric | Uploaded project |
|---|---:|
| Runtime C# files | 90 |
| Runtime C# lines | 49,902 |
| Top-level runtime classes | 100 |
| Direct `MonoBehaviour` classes | 63 |
| `FindObject*`/`GameObject.Find` occurrences | 199 |
| `Resources.Load` occurrences | 27 |
| runtime `new GameObject` occurrences | 98 |
| runtime `AddComponent<T>` occurrences | 100 |
| runtime initialization hooks | 9 |

## Changes made

### Architecture spine

Added:

- `GameRoot`;
- `GameContext`;
- `ChateauBehaviour`;
- `IGameService` and `GameServiceBase`;
- validation report/messages;
- `StateMachine<TState>`;
- `DefinitionAssetBase` and `GameDatabase`;
- chapter, feature, room, interaction, actor, motor, presenter and UI base families;
- plain-C# `StoryBeatBase`.

### Existing classes brought under the spine

Without renaming their files or changing their `.meta` GUIDs:

- `ChapterManager`;
- `ChapterClock`;
- `ChapterEventScheduler`;
- `RoomNavigationManager`;
- `CameraManager`;
- `DialogueSpeechService`;
- `SubtitleService`;
- `RoomLightingController`;
- Chapter 1 controller;
- Chapter 2 controller and three Chapter 2 feature controllers.

The new bases contain no Unity message methods, so they do not compete with existing `Awake`, `Start`, `Update`, `OnEnable`, or `OnDisable` methods.

### Dependency cleanup

`ChapterEventScheduler` no longer performs a global `FindAnyObjectByType<ChapterClock>` fallback or repeats reference resolution every frame. The Gameplay scene already contains an explicit serialized clock reference. Missing configuration now fails clearly instead of silently binding to an arbitrary clock.

### Safe pruning

Removed:

- empty `NewBehaviourScript` and its `.meta`;
- unused `PickupObject` and its `.meta`.

Both had zero serialized GUID references. The only non-declaration reference to `PickupObject` was a source-text regression test, which was updated.

### Migration automation

Added an Editor installer that can safely create/serialize the composition root and currently runtime-created navigation/dialogue components in `Gameplay.unity`, then validate and save only when the scene passes.

### Guardrails

Added:

- static inventory and serialized-reference scripts;
- a debt-ceiling guard that rejects new dependency-repair patterns and new direct `MonoBehaviour` classes;
- a GitHub Actions static guard;
- foundation tests;
- target architecture, migration plan, test runbook, prune log, migration ledger and Hamza cards.

## Static result after patch

| Metric | After patch | Change |
|---|---:|---:|
| Runtime C# files | 106 | +16 (18 focused architecture files added, 2 dead files deleted) |
| Runtime C# lines | 50,616 | +714 foundation lines |
| Direct `MonoBehaviour` declarations | 51 | -12 |
| `FindObject*`/`GameObject.Find` occurrences | 198 | -1 |
| `Resources.Load` occurrences | 27 | 0 |
| runtime `new GameObject` occurrences | 98 | 0 |
| runtime `AddComponent<T>` occurrences | 100 | 0 |

The temporary line-count increase is intentional: this patch installs the migration spine and safety tooling. The large reduction comes only after navigation, actor, story and presentation behavior has moved behind the new owners and the old paths have passed deletion proof.

## Validation performed here

- checked every new C# file for balanced delimiters and preprocessor pairs;
- preserved all modified existing script `.meta` files;
- generated unique `.meta` GUIDs for new scripts;
- verified no duplicate script GUIDs;
- verified the architecture guard passes;
- verified `git diff --check` passes;
- regenerated static runtime and serialized-reference reports.

## Validation still required in Unity

This environment does not contain Unity or a C# compiler. Therefore the following are mandatory before merging:

- compile with Unity 6000.4.10f1;
- run all EditMode tests;
- run the GameRoot installer;
- inspect and commit the scene/data diff separately;
- run EditMode and PlayMode tests again;
- perform the full Chapter 1/2 and navigation smoke checklist;
- verify no missing scripts or duplicate services.

Until those gates pass, this is a **reviewable migration candidate**, not a production release.
