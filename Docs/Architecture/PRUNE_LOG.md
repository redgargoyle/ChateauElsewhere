# Prune log

No file may be deleted merely because a text search looks empty. Each deletion needs code-reference, serialized-reference, UnityEvent/animation/reflection/resource, and behavioral-test evidence.

## Completed in the foundation patch

| File | Reason | Code references | Serialized references | Replacement | Proof / test |
|---|---|---:|---:|---|---|
| `Assets/Scripts/NewBehaviourScript.cs` | Empty Unity starter class; no behavior | 0 outside declaration | 0 | None | `ArchitectureFoundationTests.ProvenDeadStarterAndPickupScriptsStayPruned` and static GUID scan |
| `Assets/Scripts/PickupObject.cs` | Uninstantiated generic cursor-hover component; only consumer was a source-text regression assertion | 0 runtime | 0 | Existing interaction-specific hover behavior; future pickups must use `InteractionTargetBase` | Same test plus architecture guard |

Their `.meta` files were also removed because no serialized asset referenced their script GUIDs.

## Quarantined for review, not deleted

| Candidate | Why it looks redundant | Why deletion is not yet safe |
|---|---|---|
| `OdditySpriteAnimator` | No current serialized instance; current requirements do not mention an oddity UI animator | Future chapter intent is unresolved and a regression test explicitly tracks the file |
| `RoomNavigationBootstrap` | Creates required managers at runtime | `RoomNavigationManager` and prompt controller are not serialized in the uploaded Gameplay scene until the installer runs |
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
