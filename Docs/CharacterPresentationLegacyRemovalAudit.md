# Character presentation legacy-removal and scale-architecture audit

This report covers the destructive legacy cleanup and the subsequently requested replacement architecture. The original Phase 1 brief said not to build the replacement tool; the later explicit request superseded that restriction and authorized the saved `CharacterScaleCatalog` asset plus editor-only room calibration handles documented below.

## 1. Starting and ending commit/branch state

- Repository: `redgargoyle/ChateauElsewhere`
- Unity: `6000.4.10f1` (`feeafc12a938`)
- Starting branch after the requested branch creation: `chatgpt-character-size-overhaul`
- Starting commit: `2a92396176c2baa6310e42f9ee906ee846d94e03`
- Ending branch: `chatgpt-character-size-overhaul`
- Ending commit: `2a92396176c2baa6310e42f9ee906ee846d94e03`
- No commit, push, reset, or history rewrite was performed. The implementation remains as working-tree changes for review.
- The worktree already contained unrelated local work. It was not reset or overwritten; the diff totals in section 7 describe the whole current worktree, not an assertion that every changed file belongs to this migration.

Baseline Unity Test Framework results, captured before the cleanup:

| Platform | Result | Evidence |
|---|---:|---|
| EditMode | 279 total, 226 passed, 53 failed | `TestResults/CharacterPresentationCleanup/Baseline/EditMode.xml` and `.log` |
| PlayMode | 0 discovered, runner passed | `TestResults/CharacterPresentationCleanup/Baseline/PlayMode.xml` and `.log` |

The repository therefore did not begin with a green full suite. Baseline failures were retained as comparison evidence rather than hidden.

## 2. What was removed

### Before/after writer inventory

| Before owner | Former target | Former timing/trigger | Disposition after migration |
|---|---|---|---|
| `PointClickPlayerMovement` | Butler root/body transform | update, room change, editor preview/calibration | All near/far values, room overrides, profiles, capture/preview APIs, stage compensation, revisions, and scale writes removed. It now owns input, logical movement, facing, animation, footsteps, and sorting only. |
| `GuestRoomScaleCalibration` | guest body | calibration/refresh | Type, component, fields, and serialized data deleted. |
| `GuestRoomScaleApplier` | guest body | `LateUpdate`, room/anchor refresh | Type, component, formula inheritance, fine-tunes, and direct writes deleted. |
| `GuestScaleParticipant` | guest body or selected scale root | registration, enable/refresh | Type and all eight serialized components deleted. |
| `GuestRoomStageScaleUtility` | guest body | stage pan/zoom compensation | Type and callers deleted. |
| `ActorRoomState` legacy scale binding | actor body | placement and room-stage follow | Authored-scale capture, bound scale ratios, Butler samples, and scale writes removed. The surviving binding is position-only. |
| `RoomProjectedEntity` | visual position, scale, tint, shadow/sorting state | projection refresh/editor preview | Runtime type, 13 serialized uses, editor, profile links, and assets deleted. Required prop sorting remains under sorting-specific owners. |
| `RoomPerspectiveProfile` / `CharacterVisualProfile` | projection/scale data | profile evaluation | Types, two profile assets, fields, and inspectors deleted. |
| `RoomPersonWalker2D` projection branch | ambient visual scale, tint, shadow | per-frame path/projection update | Perspective, tint, opacity, shadow-scale, and collision-avoidance branches removed. Authored path motion/facing remain. |
| `Chapter1ArrivalController` | guest scale stack | spawn, arrival, repeated refresh | Legacy `Ensure`/refresh creation removed. It only ensures the new dedicated display when constructing a runtime fallback guest. |
| `Chapter2GuestPanicController` | character transform based on `Sprite.bounds` | panic frame changes and restore | Sprite-size scale capture/apply/restore deleted. Panic frames were normalized in import data instead. |
| Legacy animation scale curves | animation transforms | animation sampling | Verified absent across all 197 authored `.anim` files. |
| **After: `CharacterAnimationDisplay`** | **dedicated child `AnimationDisplay` only** | **`LateUpdate` and explicit apply** | **The sole runtime Butler/guest body-size writer. It never changes the actor root, logical position, colliders, tint, opacity, or sorting.** |

Deleted runtime types:

- `GuestRoomScaleCalibration`
- `GuestRoomScaleApplier`
- `GuestScaleParticipant`
- `GuestRoomStageScaleUtility`
- `RoomProjectedEntity`
- `RoomPerspectiveProfile`
- `CharacterVisualProfile`

Deleted obsolete tools/inspectors:

- `ButlerRoomScaleCalibrationWindow`
- `GuestRoomScaleMasterWindow`
- `GuestScaleAudit`
- `RoomProjectionCalibrationWindow`
- `RoomPerspectiveProfileEditor`
- `RoomProjectedEntityEditor`

Also deleted were the two legacy perspective profile assets, their now-empty `Assets/ScriptableObjects/Rooms` hierarchy, and obsolete `GuestButlerScaleRegressionTests` / `RoomProjectionRegressionTests`. Coverage was replaced by ownership and architecture tests rather than simply dropped.

## 3. What was preserved and what now owns scale

The recognizable replacement hierarchy is:

```text
Assets/Resources/CharacterScaleCatalog.asset
└── CharacterScaleCatalog            sole runtime calibration authority
    └── room definition               room name + Front/Back Y and scale

Rooms
└── Room_<Name>                      RoomContentGroup + room-coordinate adapter
    └── Character Scale               editor-only calibration drafts
        ├── Front                    Scene-view draft handle
        └── Back                     Scene-view draft handle

Butler or Guest                      movement / physics / placement root (scale 1)
└── AnimationDisplay                 Animator + SpriteRenderer + visual scale
```

- `CharacterScaleFunction` is the one clamped linear Y-to-scale function. The saved asset contains only Front/Back Y and scale; handle X exists solely to make editor calibration convenient. Every Butler and guest gets the same result for the same room-local Y.
- `Assets/Resources/CharacterScaleCatalog.asset` is the sole runtime calibration authority and the one normalized room lookup. `Player.prefab` references it directly, and dynamically ensured displays use the same Resources default.
- `CharacterScaleRoom` no longer owns runtime Front/Back values. It remains on each room as a coordinate adapter that converts a world-space foot position to room-local Y and reports current room-stage scale. Its Front/Back Transform fields and handle APIs are editor-only.
- `CharacterAnimationDisplay` resolves the current room and stable visible-foot point, combines the room coordinate and stage scale with the saved catalog definition, and writes only `AnimationDisplay.localScale`.
- `Dreadforge > Characters > Character Scale Tool` creates or repairs editor handles, loads saved asset values into those handles, previews saved versus unsaved values, validates the asset, and exposes Scene-view handles. Scene changes remain drafts until an explicit Save action publishes them.
- `Save Handles To Asset` publishes only the selected room; `Save All Loaded Handles To Asset` publishes every loaded room. These are the only actions that persist handle calibration to runtime data. Moving handles, saving the scene, loading, validating, repairing, or entering Play mode does not publish draft edits.
- Room pan/zoom stays with `CameraManager`. The current room-stage scale is applied to the saved catalog result so a detached world-space character remains visually attached to the room surface. Canvas resolution is deliberately excluded.
- Butler click-to-move, pathfinding, room transitions, logical foot position, facing, walking/idle animation, footsteps, and sorting remain.
- Guest arrivals/departures, waiting spots, room transitions, room-anchor placement, hiding/finding, panic routes, interaction, and seat placement remain.
- `ActorRoomState` retains actor/room identity, availability, visibility, interactability, `IsSeated`, pose state, `PlaceAt`, and position-only room-anchor following.
- Drawing Room guests 3, 5, and 7 remain standing; guests 1, 2, 4, 6, and 8 remain forced seated. Dining Room seating/eating remains `PlaceAt(seat)` plus `SetSeated(true)`. These are Animator/state overrides only and never scale overrides.
- `WorldYSortSpriteRenderer` and `DiningRoomSeatedGuestOcclusionException` remain sorting-only. The Dining exception keeps a seated guest above the chair and below the table.

## 4. Serialized migration performed

Before deleting data, `Docs/Migration/LegacyCharacterScaleSnapshot.json` captured 19 room IDs, Butler endpoints/fallbacks, 19 guest-room calibration records, two perspective profiles, eight character scale records, two ambient walkers, Drawing/Dining pose and seat mappings, and 11 panic compensation records. Runtime code does not load this snapshot.

The deterministic Unity migration and approved authority follow-up made these serialized changes:

- Created `Assets/Resources/CharacterScaleCatalog.asset` with exactly 19 saved room definitions, one for every authoritative Gameplay `RoomContentGroup`. The scene no longer owns a catalog component.
- Retained one `CharacterScaleRoom` per room only for runtime room-coordinate/stage-scale conversion and editor handle references; it does not contain runtime calibration values.
- Added one editor-only `Character Scale/Front/Back` draft hierarchy per room. All handle Z values are zero, Front/Back Y values differ, and X/Y handle scales are positive and uniform.
- Seeded the 19 asset records from the snapshot's final Butler Front/Back endpoints. Legacy guest multipliers and per-character fine-tunes were intentionally not carried forward.
- Removed per-room `referenceStageScale` calibration. Saved values are authored in the room's baseline coordinate space, and runtime applies the current room-stage local scale while continuing to exclude `CanvasScaler`/screen resolution.
- Pointed `Player.prefab` at the Resources catalog asset. All Gameplay Butler/guest prefab instances inherit that same authority, and runtime-created displays load the same default asset rather than finding scene calibration data.
- Converted the Player prefab to a unit movement/physics root plus an `AnimationDisplay` child containing the `Animator` and `SpriteRenderer`.
- Migrated Gameplay Player and Guest 1-8 to nine dedicated displays with unit actor roots and no root/display scale overrides.
- Removed four stale `SpriteRenderer.m_Size` prefab overrides from Guests 2, 3, 4, and 7.
- Removed all serialized legacy component GUID references and obsolete profile assets.
- Normalized 211 relevant panic/directional texture import metadata files to stable character world height and bottom-center pivots, replacing panic-time transform resizing.
- Fixed the two ambient Drawing Room walkers at authored static scales and white tint; their scale/tint no longer changes with Y.
- Restored 674 unrelated Unity-generated room-light preview color changes after scene serialization, keeping the architecture diff focused.

Validation found no duplicate YAML object IDs, no unresolved project MonoBehaviour references in enabled scenes/prefabs, and no deleted legacy GUID in any `.unity`, `.prefab`, or `.asset` file.

## 5. Exact tests run and results

All commands used `/home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics -projectPath /home/hamzak/Desktop/ChataeuChantilly` followed by the arguments shown below.

| Run | Arguments / scope | Result | Evidence |
|---|---|---:|---|
| Baseline EditMode | `-runTests -testPlatform EditMode` | 226/279 passed; 53 failed | `TestResults/CharacterPresentationCleanup/Baseline/EditMode.xml`, `.log` |
| Baseline PlayMode | `-runTests -testPlatform PlayMode` | 0/0, runner passed | `TestResults/CharacterPresentationCleanup/Baseline/PlayMode.xml`, `.log` |
| Phase 1 ownership | `-testFilter CharacterPresentationOwnershipTests` | 7/7 passed | `TestResults/CharacterPresentationCleanup/Final/OwnershipEditMode.xml`, `.log` |
| Phase 1 required behavior | cleanup ownership, movement/stage, Chapter 1, selected Chapter 2, panic, seating/occlusion | 61/61 passed | `TestResults/CharacterPresentationCleanup/Final/RequiredBehaviorEditMode.xml`, `.log` |
| Architecture + ownership | `-testFilter CharacterScaleArchitectureTests;CharacterPresentationOwnershipTests` | 22/22 passed | `TestResults/CharacterScale/ArchitectureOwnershipEditModeFinal.xml`, `.log` |
| Final required behavior | architecture/ownership + stage binding + Chapter 1 + selected Chapter 2 panic/hide/find/seat + movement + sorting/occlusion | 76/76 passed | `TestResults/CharacterScale/RequiredBehaviorEditMode02.xml`, `.log` |
| Final full EditMode | all discovered EditMode tests | 183/213 passed; 30 failed | `TestResults/CharacterScale/FullEditModeFinal.xml`, `.log` |
| Final PlayMode | all discovered PlayMode tests | 0/0, runner passed | `TestResults/CharacterScale/FinalPlayMode.xml`, `.log` |

The passing architecture/ownership coverage proves:

- one clamped, order-independent Y function;
- Front/Back X does not affect scale;
- Butler and guest results are identical at identical room/Y, including when seated;
- only the visual child changes size; actor root, root position, and collider bounds do not;
- all 19 saved catalog-asset room definitions validate;
- all nine Gameplay actors have dedicated unit displays;
- stage zoom conversion is non-cumulative;
- legacy names/GUIDs/runtime recreation cannot return;
- all 197 animation clips under `Assets` have empty scale curves;
- all 224 panic sprite slots have normalized height and bottom-center pivots;
- Drawing and Dining pose/placement signals remain;
- enabled scenes and prefabs have resolvable MonoBehaviour scripts.

## 6. Remaining risks or blockers

- The full EditMode suite is not green. It began at 53 failures and currently has 30. Current failures are pre-existing/out-of-scope scene, navigation, dialogue/audio, lighting, oddity, asset-source, or broad collision expectations. The suite membership also changed because obsolete scale/projection tests were deleted, so the count reduction is context rather than a claim that every baseline failure was fixed.
- There are no authored PlayMode tests in the project, so both baseline and final PlayMode runs discover zero tests. The batch runner passed, but this does not replace an in-Editor visual playthrough.
- Saved Front/Back values were seeded from final Butler endpoints. Because the requested model deliberately deletes guest multipliers/fine-tunes, artists should visually review all 19 rooms. Any handle adjustment remains an editor draft until `Save Handles To Asset` or `Save All Loaded Handles To Asset` is deliberately pressed.
- Unity regenerated equivalent embedded room-light preview sprite/texture object IDs while saving `Gameplay.unity`. Their unrelated preview colors were restored, but those generated ID changes remain scene-diff noise.
- `Player.prefab` references `Assets/Resources/CharacterScaleCatalog.asset`, and missing display references resolve through `CharacterScaleCatalog.LoadDefault()`. Additive or new gameplay scenes must provide the appropriate `CharacterScaleRoom` coordinate adapter for each room, not another runtime catalog.
- Unbound moving guests use the rendered visible foot to evaluate room Y. Current art is normalized to bottom-center pivots; future character art must keep that invariant to avoid visible-foot feedback while scale changes.
- The repository has one pre-existing serialized issue outside enabled gameplay content: `Assets/Scenes/New Scene.unity` references an Input System UI module whose package GUID is absent. It is present at the starting commit and was not introduced or broadened here.
- No manual GUI playthrough was performed in this headless environment. Before merging, use the Character Scale Tool to load the asset into the Front/Back handles and inspect it, then walk the Butler plus representative guests through each room, especially Drawing/Dining forced poses. Do not press either Save action unless a reviewed draft is intended to replace runtime calibration.

These are review risks, not competing body-scale owners. The ownership guard and required behavior suite are green.

## 7. File list and diff summary

Tracked diff at report time: 283 files changed, 6,086 insertions, and 18,788 deletions. This excludes new untracked architecture/report/test-result files and includes pre-existing user-owned worktree changes.

Primary additions:

- `Assets/Resources/CharacterScaleCatalog.asset`
- `Assets/Scripts/Characters/CharacterScaleFunction.cs`
- `Assets/Scripts/Characters/CharacterScaleRoom.cs`
- `Assets/Scripts/Characters/CharacterScaleCatalog.cs`
- `Assets/Scripts/Characters/CharacterAnimationDisplay.cs`
- `Assets/Editor/CharacterScaleTool.cs`
- `Assets/Editor/CharacterScaleArchitectureTests.cs`
- `Assets/Editor/CharacterPresentationOwnershipTests.cs`
- `Docs/Migration/LegacyCharacterScaleSnapshot.json`
- `Docs/Migration/LegacyCharacterScaleSnapshot.README.md`
- this audit

Primary serialized/code modifications:

- `Assets/Scenes/Gameplay.unity`
- `Assets/Prefabs/Player.prefab`
- `Assets/Scripts/PointClickPlayerMovement.cs`
- `Assets/Scripts/PlayerMovement.cs`
- `Assets/Scripts/Story/ActorRoomState.cs`
- `Assets/Scripts/Story/NPCWaypointMover.cs`
- `Assets/Scripts/Characters/RoomPersonWalker2D.cs`
- `Assets/Scripts/Characters/WorldYSortSpriteRenderer.cs`
- `Assets/Map/CameraManager.cs`
- `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs`
- `Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestPanicController.cs`
- `Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestSearchController.cs`
- 211 relevant character texture `.meta` files

The permanent guard now scans every production C# file under `Assets`, not a fixed character-file list. It found exactly 44 runtime `localScale` assignments: one body-display writer and 43 exact non-body allowlist entries. The non-body categories are:

- Body display: exactly one `animationDisplay.localScale = requestedScale;` in `CharacterAnimationDisplay`.
- Chapter 1 coat prop setup: three exact assignments (`coatObject`, assigned coat renderer scale, fallback coat renderer scale).
- Chapter 2 interaction child: one `targetTransform.localScale = Vector3.one;` for the search click target.
- `CameraManager`: two camera-shake assignments, four canvas/background reset assignments, and one `activeRoomStage` zoom assignment.
- UI/canvas/HUD/navigation/speech-bubble layout: 25 assignments.
- Lighting/environment/post-process helpers: five assignments.
- Oddity VFX: one assignment.
- Chapter 2 fallback monster placeholder: one assignment.

Any new production scale assignment now fails until it is explicitly reviewed and added as a precise file/statement/count exception. There are no other Butler/guest body scale assignments in movement, story, camera, sorting, animation, or presentation sources.
