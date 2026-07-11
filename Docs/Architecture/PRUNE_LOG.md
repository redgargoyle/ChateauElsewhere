# Prune log

No file may be deleted merely because a text search looks empty. Each deletion needs code-reference, serialized-reference, UnityEvent/animation/reflection/resource, and behavioral-test evidence.

## Completed in the foundation patch

| File | Reason | Code references | Serialized references | Replacement | Proof / test |
|---|---|---:|---:|---|---|
| `Assets/Scripts/NewBehaviourScript.cs` | Empty Unity starter class; no behavior | 0 outside declaration | 0 | None | `ArchitectureFoundationTests.ProvenDeadStarterAndPickupScriptsStayPruned` and static GUID scan |
| `Assets/Scripts/PickupObject.cs` | Uninstantiated generic cursor-hover component; only consumer was a source-text regression assertion | 0 runtime | 0 | Existing interaction-specific hover behavior; future pickups must use `InteractionTargetBase` | Same test plus architecture guard |
| `Assets/Scripts/Navigation/RoomNavigationBootstrap.cs` | Runtime repair duplicated the serialized composition root | 0 runtime outside the obsolete source-text test | 0; deleted GUID `ee39d7886e1b437f59e4e82b50f560c8` never appeared in serialized content | `Gameplay.unity` serializes one `GameRoot`, `RoomNavigationManager`, and `DoorPromptSequenceController` | `GameplayLifecycleCharacterizationTests`, `NavigationRegressionTests.GameplaySerializedRootOwnsNavigationStartup`, `ArchitectureFoundationTests.RuntimeNavigationBootstrapStaysPruned`, serialized-reference scan, and full Unity gate |

Their `.meta` files were also removed because no serialized asset referenced their script GUIDs.

## Retired runtime repair paths

| Path | Former behavior | Replacement | Proof / test |
|---|---|---|---|
| `ChapterManager.BootstrapChapterManagerForGameplay` | A post-scene-load hook created an entire chapter manager stack with six `AddComponent` calls | `Gameplay.unity` serializes `ChapterManager`, `ChapterClock`, `ChapterEventScheduler`, `ChapterIntroUI`, and `Chapter1ArrivalController`; GameRoot validates and initializes the services | `GameplayLifecycleCharacterizationTests`, `ArchitectureFoundationTests.ChapterStackIsSerializedInsteadOfRepairedAtRuntime`, exact serialized GUID scan, and full Unity gate. Chapter 2 was migrated in the subsequent independently gated slice. |
| `ChapterManager.ResolveChapter2Controller(createIfMissing)` creation branch | Chapter transitions and debug skips could add `Chapter2Controller` at runtime | `Gameplay.unity` serializes one controller, binds `ChapterManager.chapter2Controller`, and registers it with GameRoot | The lifecycle test captures the inert serialized instance before Chapter 2 and proves both skips reuse it; the static guard bans `AddComponent<Chapter2Controller>`; serialized-reference scan and full Unity gate. |

## Quarantined for review, not deleted

| Candidate | Why it looks redundant | Why deletion is not yet safe |
|---|---|---|
| `OdditySpriteAnimator` | No current serialized instance; current requirements do not mention an oddity UI animator | Future chapter intent is unresolved and a regression test explicitly tracks the file |
| `UrpPostProcessingBootstrap` | Searches cameras and adds URP data at runtime | Render-rig parity has not yet been tested after authoring components in-scene |
| `PlayerMovement` / `CharacterController2D` | Legacy movement path beside point-click movement | Player and guest prefab bindings must be migrated and tested first |
| `DoorButton`, `DoorPromptSequenceController`, `DoorDataParser`, `DoorCameraSequence`, `RoomVisualCatalog` | Parallel navigation representations | Route content must first be converted into one canonical passage graph |
| `StaticNoisePlayer`, `StaticSetImagePlayer`, `StaticSet`, `StaticFrameGroup` | Overlapping frame/static presentation | Serialized content and behavior comparison is still required |
| guest/player footstep and room ambience pairs | Similar implementations | Data and behavioral differences must be captured before merging |

## Required deletion proof template

```text
Candidate:
Requirement/invariant it used to satisfy:
Replacement owner:
Code-reference result:
Serialized GUID-reference result:
UnityEvent/animation/reflection/resource result:
Scene/prefab migration command or record:
Tests protecting replacement:
Unity smoke test result:
Commit removing file:
Rollback commit:
```
