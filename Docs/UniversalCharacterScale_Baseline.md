# Universal Character Scale Refactor Baseline

Captured on 2026-07-20 before the compliance changes in this branch.

## Repository state

- Source branch: `new-archoitecture-overhaul` (the pre-existing name contains a typo).
- Requested working branch: `new-architecture-overhaul`.
- Starting SHA: `c5a6aa3177ee4f028907551611950b3cbd1a9860`.
- The pasted prompt also names `universal_character_scale_refactor`; the user's direct branch instruction supersedes that name.
- Unity version: `6000.4.10f1` (`feeafc12a938`).
- Unity executable: `/home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity`.

The starting worktree was not clean:

```text
## new-archoitecture-overhaul
 M Assets/Art/UI/Fonts/NotoSerifDisplay-Medium SDF.asset
 M Assets/Scenes/Gameplay.unity
?? TestResults/
```

Those font, Gameplay scene, and existing test-result changes predate this branch. They are user-owned and were preserved. The baseline/build runs briefly caused Unity-generated changes in three rendering/project settings assets; those generated changes were reverted immediately and are not part of the refactor.

## Architecture already present at the starting SHA

The starting SHA already contained an earlier universal-scale implementation: `CharacterScaleCatalog`, `CharacterScaleFunction`, `CharacterScaleRoom`, `CharacterAnimationDisplay`, `CharacterScaleTool`, the migrated catalog asset, and the legacy snapshot. The named legacy guest/projection scripts and editor windows were already deleted.

This branch therefore starts as a compliance refactor of that implementation, not a second scale stack. The baseline audit found residual cross-layer ownership that still violated the pasted boundary:

- `ActorRoomState` called `CharacterAnimationDisplay` during room-stage binding.
- `CharacterAnimationDisplay` called `CharacterFloorReference.AlignActorToWorldPoint`, translating the gameplay root after a display-scale write.
- Butler scale evaluation called a navigation conversion API that can refresh/rebase movement state.
- The display factory also provisioned `CharacterAnimationPresenter`, coupling scale setup to animation-state ownership.
- One disabled, non-build scene contained an unrelated unresolved Input System UI component.

## Baseline editor, build, and test status

All commands used Unity `6000.4.10f1`, `-batchmode`, `-nographics`, and project path `/home/hamzak/Desktop/ChataeuChantilly`.

| Check | Baseline result | Evidence |
|---|---:|---|
| Editor import/compile | Passed, exit 0 | `TestResults/UniversalCharacterScale/Baseline/Compile.log` |
| Linux standalone player build | Passed, exit 0 | `TestResults/UniversalCharacterScale/Baseline/LinuxBuild.log` (`Build Finished, Result: Success.`) |
| Architecture + ownership EditMode filter | 29/29 passed | `TestResults/UniversalCharacterScale/Baseline/ArchitectureOwnership.xml` and `.log` |
| PlayMode discovery | 0 discovered; runner passed | `TestResults/UniversalCharacterScale/Baseline/PlayMode.xml` and `.log` |
| Full EditMode suite | Incomplete: Unity native crash; no result XML | `TestResults/UniversalCharacterScale/Baseline/EditMode.log` |

The full EditMode run did not produce a pass/fail test result. `NavigationRegressionTests.Chapter1ResolverSynchronizesHoverOwnershipBeforeClickDispatch` called `UnityEngine.Cursor.SetCursor`; under headless X11 this aborted Unity with signal 11 in `XcursorImageLoadCursor`. This is a baseline runner/environment limitation rather than a compiler or character-scale assertion failure.

Windows, Linux, and macOS standalone support modules are installed for this Unity version. Only the Linux player was built for this baseline.
