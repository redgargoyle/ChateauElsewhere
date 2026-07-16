# Character Scale Phase 1 Ownership Audit

## Scope and ownership rule

Phase 1 removes every evaluator, writer, factory, editor, and serialized store that can change the Butler or Guest 1-8 body-root scale at runtime. It preserves authored static root scales, room-stage position conversion, room-stage zoom, movement, animation, facing, sorting, tint, shadows, visibility, interactions, anchors, seats, occlusion, coats, held items, click targets, and speech bubbles.

The body-scale ownership boundary is the Butler/guest actor root or an explicitly selected character visual root. Scale writes to room-stage parents, props, UI, effects, shadows, click targets, speech bubbles, and presentation-only children are not body-size ownership and remain in scope for their existing systems.

Baseline status terms used during discovery:

- **Active**: runs or can run in the current authored Gameplay scene.
- **Guarded**: the former `GuestScaleParticipant` checks suppressed a competing writer for the eight guests; deleting the guard first would have reactivated it.
- **Dormant**: serialized configuration or callable tooling exists, but the current authored scene does not exercise the writer.
- **Dead**: no live asset/reference was found.

## Frozen baseline evidence

| Evidence | Baseline result |
|---|---|
| Git source baseline | `2a92396176c2baa6310e42f9ee906ee846d94e03` |
| Unity | `6000.4.10f1` |
| Full EditMode baseline | 279 total, 226 passed, 53 failed |
| Baseline XML | `/tmp/character-size-phase1-baseline-editmode.xml` |
| Baseline log | `/tmp/character-size-phase1-baseline-editmode.log` |
| Canonical migration evidence | `docs/migrations/character-scale/legacy-character-scale-snapshot.json`, schema version 1 |

The baseline XML was read directly and reports `testcasecount="279" total="279" passed="226" failed="53"`. Phase 1 does not claim to repair those 53 pre-existing failures.

Primary source hashes frozen in the snapshot:

| Path | GUID | SHA-256 |
|---|---|---|
| `Assets/Scenes/Gameplay.unity` | `8c9cfa26abfee488c85f1582747f6a02` | `1099b1469437d46f5c45b7b8041e50977817112a7ec65027d10899222d2bd17d` |
| `Assets/Prefabs/Player.prefab` | `3c2a23f8d68b2d05cace0338fba9a1d1` | `fcc64c863c1101340cf4cb96d91389af679e7a7fea8f6bdcb2d1c0e6101b3f71` |
| `Assets/ScriptableObjects/Rooms/DrawingRoomPerspectiveProfile.asset` | `426f8e326a60b3a0eaeb540d7d670267` | `96a746b728e0048deec1f4df782ca3e79a67ab11137a887130d91f9fe53c2032` |
| `Assets/ScriptableObjects/Rooms/DiningRoomPerspectiveProfile.asset` | `a63248cfbd6b4a72af45c62cff7e94d0` | `aca70313aa7fc8a5568a54e9c0955517cfc84b477d00b765e2c48b148804db7a` |

Snapshot integrity counts are 4 sources, 19 Butler room records, 19 guest calibration rows, 8 guest participant records, 8 sitting mappings, 2 room perspective profiles, 8 Drawing Room assignments, 8 Dining Room assignments, and 8 Dining Room occlusion bindings. All captured serialized datum objects carry `propertyPath`, invariant-culture `rawValue`, and asset/object provenance.

## Final Phase 1 implementation status

Phase 1 is implemented at the intended static-authored-size boundary. The legacy Butler and guest evaluators, scale writers, runtime factories, calibration windows, custom scale inspectors, compatibility fields, and active serialized records are gone. `PointClickPlayerMovement`, `ActorRoomState`, `RoomPersonWalker2D`, `RoomProjectedEntity`, chapter placement/panic code, and facing code retain their non-size responsibilities without writing character body-scale magnitude. The Phase 2 universal room/character size tool, catalog, controller, preview, and curve authoring UI are intentionally not present.

The two `RoomPerspectiveProfile` assets now contain room identity, depth, tint, sorting, contact-shadow, and floor data only. `RoomProjectedEntity` retains position, tint, sorting, and contact-shadow presentation only; the contact shadow may scale independently, but its character or prop visual root is never scaled. Room-stage zoom remains a parent-coordinate-system behavior. Authored Butler/guest root scales, nine Player-prefab instances, Drawing/Dining anchors, the eight sitting mappings, and Dining occlusion bindings remain intact. There is still no distinct eating runtime state or approved eating clip.

Final deterministic serialized hashes after both migration passes and canonical Player sorting-layer serialization:

| Path | SHA-256 |
|---|---|
| `Assets/Scenes/Gameplay.unity` | `4639ab0a2b293eddff249d20466079aa6c172733b362704bc6c4e1fbce6bf00a` |
| `Assets/Prefabs/Player.prefab` | `f8bcc892b98971f380da17fdc26df31f966462c06d0551ddfdb5f1d08ca4a8c2` |
| `Assets/Prefabs/Room_Drawing_Room.prefab` | `317868e23e001fa912d89e570606d66d4da72ac359b0aeeaca59deb83e86dc30` |
| `Assets/Prefabs/Room_Drawing_Room_Perspective.prefab` | `dc64d4a22c92517d0f7128ee6be3b325c5f75ab14ae045eaf477fdd5d1816a3e` |
| `Assets/ScriptableObjects/Rooms/DrawingRoomPerspectiveProfile.asset` | `ece88e58335242b96a82e4a123b69126af78dffed5a9fbfa6c701431b8bcdadc` |
| `Assets/ScriptableObjects/Rooms/DiningRoomPerspectiveProfile.asset` | `2da3dfd1cbc94330122affc519c955c88577bab62af81ef34170a822c275e5f5` |

The frozen snapshot remains evidence for Phase 2, not a runtime compatibility source. The matrices below preserve the discovered legacy responsibility in their historical columns while their status, decision, and proof cells record the completed Phase 1 disposition.

## Runtime ownership matrix

| Path / component | Actor scope | Trigger / order | Property written | Responsibility | Serialized data | Status | Decision | Regression proof |
|---|---|---|---|---|---|---|---|---|
| `Assets/Scripts/PointClickPlayerMovement.cs` / `PointClickPlayerMovement` | Butler; inherited by all eight Player-prefab guests | initialization, room change, movement update | no character-scale write | movement, floor constraints, room-stage coordinate conversion, animation, sorting, and Phase 2 read-only input | scale fallback, 19-room override, preview, selected-room, and captured-base fields removed | **Active, scale-neutral** | **Completed**: movement/position/animation/sorting retained; all Butler body-size evaluation, APIs, DTOs, stores, and writes removed; `CurrentRoomId` and `TryGetCurrentRoomLocalFootPoint` expose room/local-foot data without mutation | prohibited-symbol/source/API guard, prefab/scene serialized seam guard, compile and movement regressions |
| `Assets/Scripts/Characters/GuestRoomScaleCalibration.cs` / `GuestRoomScaleCalibration` | Guest 1-8 | formerly read by applier/editor | none; type removed | legacy room multiplier and Butler-curve store | snapshot retains the former 19 rows and reference-stage values | **Deleted** | **Completed**: source, meta, callers, GUID references, and serialized object removed | snapshot count/provenance checks plus deleted-type/GUID/scene guards |
| `Assets/Scripts/Characters/GuestRoomScaleApplier.cs` / `GuestRoomScaleApplier` | Guest 1-8 | formerly execution order 10000 / `LateUpdate` | none; type removed | legacy guest room/depth/zoom evaluator and runtime factory | snapshot retains former calibration wiring | **Deleted** | **Completed**: source, meta, factories, callbacks, GUID references, and serialized object removed | deleted-type/factory/GUID/scene guards |
| `Assets/Scripts/Characters/GuestScaleParticipant.cs` / `GuestScaleParticipant` | one per Guest 1-8 | formerly lifecycle, room/pose, and applier calls | none; type removed | legacy scale-root selection, captured base, and competing-writer guard | snapshot retains all eight former participant records and mismatches | **Deleted** | **Completed last among guest owners**: source, meta, arbitration guards, component records, and GUID references removed | eight-record snapshot plus deleted-type/GUID/scene guards |
| `Assets/Scripts/Characters/GuestRoomStageScaleUtility.cs` | Guest 1-8 | formerly called during guest scale evaluation | none; type removed | legacy room-stage compensation evaluator | snapshot retains former 19 reference-stage values | **Deleted** | **Completed**: source, meta, callers, and symbols removed; stage position conversion remains elsewhere | deleted-file/symbol guard and room-stage position regressions |
| `Assets/Scripts/Story/ActorRoomState.cs` / `ActorRoomState` | world-space story actors including Guest 1-8 | room binding, state application, room-stage motion/zoom | position and state only; no body-scale write | identity, room, visibility, interaction, seating, placement, and room-stage position binding | actor/room/state references; legacy authored/bound scale caches removed | **Active, scale-neutral** | **Completed**: state/visibility/position preserved; scale capture, compensation, profile sizing, and participant sync removed | room/stage-motion position and authored-scale invariance tests |
| `Assets/Scripts/Characters/RoomProjectedEntity.cs` / `RoomProjectedEntity` | generic projected entities, including possible character visual roots | projection refresh and room/profile changes | projected position, tint, sorting, and contact-shadow child scale only | non-size projection presentation | profile, mode, foot point, visual root, presentation flags, sorting/shadow refs | **Active, scale-neutral for visual roots** | **Completed**: all character/prop visual-scale fields, caches, overrides, APIs, and writes removed; shadow-only scaling retained with dedicated-subtree eligibility cached at hierarchy/reference refresh boundaries | 13-record serialized seam guard plus character/prop visual-scale, shadow behavior, mixed-subtree, and per-frame-allocation-path tests |
| `Assets/Scripts/Characters/RoomPersonWalker2D.cs` / `RoomPersonWalker2D` | UI/room walkers and possible guests | movement and visual refresh | no walker/card scale write | room-local movement, bob/sway, facing, animation, and depth tint | movement/profile presentation data only; scale endpoints/caches removed | **Active, scale-neutral** | **Completed**: movement/animation/tint retained; perspective/Butler/body-size ownership removed | walker source guards and authored-scale invariance tests |
| `Assets/Scripts/Characters/RoomPerspectiveProfile.cs` / `RoomPerspectiveProfile` | room-wide consumers | profile evaluation by projection/walker/state | no character or prop scale output | depth normalization, tint, sorting, contact shadows, and floor polygon | room id, near/far Y, tint, sorting, shadow curves, floor polygon; no `scaleByDepth` or native reference size | **Active, non-size profile** | **Completed**: legacy size curve, endpoints, multiplier APIs, and native-size field removed while live presentation data remains | profile API/source guard, two-asset serialized seam guard, retained behavior tests, frozen legacy curves in snapshot |
| `Assets/Scripts/Characters/CharacterVisualProfile.cs` / `CharacterVisualProfile` | projected characters | formerly consumed by projected scale path | none; type removed | dead legacy height multiplier and renderer offsets | no assets or live references were found | **Deleted** | **Completed**: source, meta, creator path, consumers, and GUID references removed | file/type/GUID absence guard |
| `Assets/Scripts/CharacterController2D.cs` / `CharacterController2D.Flip` | Butler/Player-prefab actors using controller | facing change and late child attachment | per-renderer `SpriteRenderer.flipX` presentation state, not root scale | movement facing and held-item/coat orientation | runtime renderer baselines only | **Active, scale-neutral** | **Completed**: facing preserves each renderer's authored relative flip, late renderers adopt the current presentation direction, and `FacingChanged` moves the carried coat attachment to the correct local-X side without changing root-scale magnitude | facing/root-magnitude test covers opposite authored renderer baselines and a late renderer; carried-coat test flips attachment X repeatedly while root scale stays exact |
| `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs` | Guest 1-8 | chapter setup, room transitions, seating | placement, pose, visibility, occlusion, and coat presentation; no guest body-scale write | Chapter 1 arrival, coats, movement, placement, pose, and occlusion | authored guest refs/config, anchors, seats, standing set, occlusion bindings | **Active, scale-neutral for guest bodies** | **Completed**: guest scale factories, inference, capture/restore, participant mutation, and refresh removed; non-body coat visuals retained | Drawing/Dining assignment, coat, seated, room-motion, and no-factory regressions |
| `Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestPanicController.cs` | Guest 1-8 | panic begin/update/stop | position, animation, sprite, and orientation only; no actor-root scale | panic movement and presentation | panic motion/runtime state; body-scale snapshots removed | **Active during panic, scale-neutral** | **Completed**: actor-root capture/restore and sprite-bounds size compensation removed | repeated panic and authored-scale invariance tests |
| `Assets/Map/CameraManager.cs` / room-stage transform | room-stage parent, not actor body | pan/zoom and room activation | `activeRoomStage.localScale` | camera/room-stage zoom and coordinate system | room-stage layout state | **Active, permitted boundary** | **Keep**; actor bodies may inherit stage scale naturally, but no body-root compensation writer remains | rewritten room-stage tests distinguish parent zoom from body scale |
| `Assets/Scripts/Characters/DiningRoomSeatedGuestOcclusionController.cs` | seated Dining guests | seating activation/deactivation | sorting/occlusion only; no body scale | table/chair occlusion | controller fileID `3920000002`, eight seat bindings | **Active, not a size owner** | **Keep** | snapshot locks all eight bindings and Dining assignments |
| `Assets/Scripts/Story/NPCWaypointMover.cs` / `NPCWaypointMover` | Guest/story actors | scripted waypoint coroutine and stop | actor position, Animator direction/speed, and projected foot point; no scale | authored guest movement and projection handoff | waypoint target and motion settings | **Active, scale-neutral** | **Keep**: movement releases passive room-stage binding before its first step and never sizes the actor | waypoint/stage-locking behavior regression plus no-scale static scan |
| `Assets/Scripts/Navigation/RoomContentGroup.cs` / `RoomContentGroup` | room stage and all room-owned content | scene load, room activation, profile/floor lookup | room metadata/references only; no actor transform | room identity, stage ownership, perspective-profile and walkable-floor lookup | authored room id, stage, profile, floor and background references | **Active, not a size owner** | **Keep**: defines the coordinate-system boundary consumed by movement/projection; CameraManager alone applies parent-stage zoom | scene/profile reference validation and room-stage position regressions |
| `Assets/Scripts/Characters/WorldYSortSpriteRenderer.cs` / `WorldYSortSpriteRenderer` | characters and world props | continuous/explicit depth refresh | SpriteRenderer sorting layer/order only | visible-foot Y sorting | renderer, footprint, offsets and sorting settings | **Active, scale-neutral** | **Keep**: dynamic order remains independent of body size | ObjectCollisionBox and room-projection sorting regressions |
| `Assets/Scripts/Characters/YSortSolidObstacle2D.cs` / `YSortSolidObstacle2D` | solid props/occluders | footprint and overlap refresh | sorting/occlusion state only; no character scale | physical-footprint ordering and occlusion safety | collider/footprint/sorting references | **Active, prop-only** | **Keep**: affects collision/layering, not actor appearance magnitude | ObjectCollisionBox behavior regressions and scoped scale-writer scan |
| `Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestSearchController.cs` / `Ch2_ClickTarget` | Guest 1-8 click interaction | hidden-guest search setup | dedicated click-target child `localScale = Vector3.one`; never actor/body root | collider and click-action presentation | runtime-only named child beneath the actor | **Active, permitted child boundary** | **Keep**: the child scale normalizes only its collider coordinate frame | source guard requires the named child, actor parenting, and no actor-root scale write; Chapter 2 click-target regressions retain behavior |
| `Assets/Scripts/UI/SpeakingCharacterIndicator.cs` / bubble renderer | Butler and Guest 1-8 speech indicator | active speech target bounds refresh | bubble-renderer child `localScale`; never target/actor root | speech-bubble placement and readable screen presentation | runtime-created UI/presentation object | **Active, permitted child boundary** | **Keep**: bubble scale follows visible bounds without becoming character size | source guard restricts the write to `bubbleRenderer.transform`; dialogue indicator behavior regressions retain target resolution and cleanup |

### Final character-adjacent scale-write allowlist

The final scoped static scan permits only the room-stage parent zoom in `CameraManager`, the validated dedicated contact-shadow child in `RoomProjectedEntity`, coat visual children in `Chapter1ArrivalController`, the named `Ch2_ClickTarget` child in `Chapter2GuestSearchController`, and the standalone bubble renderer in `SpeakingCharacterIndicator`. UI layout, lighting, menu, environment, monster, oddity, and prop-only scale writes are outside the managed Butler/Guest body hierarchy. No surviving allowlisted path writes the Butler/guest actor root or authored visual root. `RoomProjectedEntity` additionally rejects a shadow target that is its component root, visual root, outside its hierarchy, an ancestor of the visual root, not the owner of an explicitly assigned shadow renderer/graphic, or a mixed subtree containing any unassigned `SpriteRenderer` or `Graphic`.

## Editor and authoring ownership matrix

| Path / component | Actor scope | Trigger / order | Property written | Responsibility | Serialized data | Status | Decision | Regression proof |
|---|---|---|---|---|---|---|---|---|
| `Assets/Editor/GuestRoomScaleMasterWindow.cs` | Guest 1-8 and calibration scene objects | formerly manual preview/save | none; tool removed | legacy all-guest calibration workflow | frozen only in snapshot | **Deleted** | **Completed**: source/menu/workflow removed | file/type/menu/factory guards |
| `Assets/Editor/ButlerRoomScaleCalibrationWindow.cs` | Butler | formerly manual preview/reset/save | none; tool removed | legacy Butler room calibration | frozen 19-room records and stale aliases in snapshot | **Deleted** | **Completed**: source, meta, menu, and workflow removed | file/type/menu guards and snapshot checks |
| `Assets/Editor/PointClickPlayerMovementEditor.cs` | Butler inspector | formerly Inspector draw | none; custom scale inspector removed | legacy calibration entry point | legacy movement scale fields removed | **Deleted** | **Completed**: source, meta, and calibration entry point removed | file/type/menu guard |
| `Assets/Editor/RoomProjectedEntityEditor.cs` | projected entities/possible characters | formerly Inspector buttons | none; custom scale inspector removed | legacy projection scale authoring | visual scale caches/overrides removed | **Deleted** | **Completed**: source, meta, controls, and GUID references removed | file/GUID/source guard plus projection behavior tests |
| `Assets/Editor/RoomPerspectiveProfileEditor.cs` | all profile consumers | profile Inspector changes and explicit refresh | profile data plus position/tint/sorting/shadow presentation refresh; no scale | non-size room profile authoring | room depth/tint/sort/shadow/floor fields only | **Active, scale-neutral** | **Completed**: default Inspector and depth-range editing retained; scale endpoints, multipliers, labels, and character refresh removed | editor UI/source guard and projection/walker presentation tests |
| `Assets/Editor/RoomProjectionCalibrationWindow.cs` | scene rooms and projected entities | manual profile creation/assignment and presentation preview | profile assignment and projection refresh; no visual-root scale | depth/tint/sorting room projection authoring | non-size room profiles only; no character visual profile/adult-height path | **Active, scale-neutral** | **Completed**: preview/creation retained without size controls; Drawing defaults apply only on new asset creation | strengthened editor UI/source guard and existing-asset preservation assertion |
| `Assets/Editor/PlayModeLayoutCaptureWindow.cs` | arbitrary layout transforms except managed character hierarchies | capture in Play Mode, apply in Edit Mode | position/rotation/scale for permitted layout targets only | anchor/layout persistence | pending capture items | **Active, constrained** | **Completed**: capture and application both reject Butler/guest transforms while retaining RoomAnchor/layout use | managed-transform capture/apply rejection tests |
| `Assets/Editor/GuestScaleAudit.cs` | guest scale stack | formerly manual report | none; coupled audit removed | legacy diagnostics | none active | **Deleted** | **Completed**: mutating/coupled legacy audit removed; regression suite is the read-only ownership guard | file/type/menu plus prohibited writer/GUID/factory/curve tests |
| `Assets/Editor/CharacterAnimationAssetBuilder.cs` | all override-controller characters | confirmed manual rebuild | override-controller clip mapping only | animation asset generation | eight override controllers and authored sitting clips | **Active, confirmed and hardened** | **Completed**: destructive rebuild confirmation added and `Player_Croutch` preserves authored/existing sitting mapping | eight explicit mapping tests and builder source guard |
| `Assets/Editor/Guest2ButlerAnimationAssetBuilder.cs` | Guest 2 | confirmed manual asset rebuild | generated clips/override controller only; no Gameplay mutation | Guest 2 animation asset generation | Guest 2 clips/controller | **Active, asset-only and confirmed** | **Completed**: automatic Gameplay open, Animator creation, scene assignment, and save removed | Guest 2 mapping and no-scene-mutation guards |

## Serialized ownership matrix

| Path / component | Actor scope | Trigger / order | Property written | Responsibility | Serialized data | Status | Decision | Regression proof |
|---|---|---|---|---|---|---|---|---|
| `Assets/Prefabs/Player.prefab` / `PointClickPlayerMovement` | Butler and inherited guest prefab instances | prefab load | no runtime root-scale input | movement/routing/animation/sorting | legacy `nearY/farY/nearScale/farScale` fields removed | **Clean, active movement component** | **Completed**: movement serialization retained; all scale fields removed | snapshot preserves former fallback; current hash and serialized seam/source guards prove removal |
| `Assets/Scenes/Gameplay.unity` / Butler prefab instance `81962841` | Butler | scene load | authored root scale only; no evaluator data | static authored appearance | root scale retained; 19-room records, selected room, captured-base flag, and obsolete paths removed | **Clean authored state** | **Completed**: authored transform preserved while scale evaluator modifications were removed | frozen snapshot plus deterministic scene hash and stale-path guards |
| `Assets/Scenes/Gameplay.unity` / stale Butler aliases | Butler | formerly inert prefab modifications | none; stale paths removed | no current code owner | conflicting values retained only in snapshot evidence | **Deleted stale data** | **Completed**: all obsolete alias property modifications removed | snapshot warnings plus stale-property-path guard |
| `Assets/Scenes/Gameplay.unity` / calibration `1844861547` and applier `86244178` | Guest 1-8 | formerly scene load | none; objects/components removed | former global guest-size infrastructure | former rows and wiring retained only in snapshot | **Deleted serialized owners** | **Completed**: exact standalone objects, components, GUID references, and configuration removed | snapshot IDs/counts plus scene GUID/object guard |
| `Assets/Scenes/Gameplay.unity` / eight added `GuestScaleParticipant`s | Guest 1-8 | formerly scene load | none; components removed | former guest identity/pose/base scale and writer guard | former eight records retained only in snapshot | **Deleted serialized owners** | **Completed**: all eight component records and GUID references removed after secondary writers were neutralized | eight-record snapshot and scene GUID/component guard |
| `Assets/Scenes/Gameplay.unity` / eight Player prefab root overrides | Guest 1-8 | scene load | authored static root scale | current authored appearance baseline | all X/Y `1.42`; Z `1`, `1.12`, or `1.3` by guest | **Active authored state** | **Keep in Phase 1**; do not replace with captured-base values | snapshot records every property modification and mismatch warning |
| Drawing/Dining `RoomPerspectiveProfile` assets | room consumers | asset load | feeds non-size projection only | room depth/tint/sort/contact-shadow/floor profile | near/far Y, tint, sorting, shadow, floor data; no `scaleByDepth` or `nativeRoomReferenceSize` | **Active, non-size assets** | **Completed**: both legacy size seams removed while retained presentation data stays byte-verifiable | frozen legacy curves in snapshot, final hashes, two-asset absence/retention assertions |
| Gameplay and Drawing prefabs / `RoomProjectedEntity` serialized state | projected entities | scene/prefab load | position/tint/sorting/shadow only; never visual-root size | generic non-size projection | 13 instances, all stable numeric mode `4`; no `applyScale` or prop base/cache fields | **Active, scale-neutral serialized state** | **Completed**: scale-only records and stale Butler/captured fields removed while component modes/references remain | exact 13-record/mode/field-absence guard, final hashes, prop/character/shadow behavior tests |
| Eight guest `.overrideController` assets | Guest 1-8 | Animator load or builder rebuild | animation clip replacement, not transform scale | authored per-guest animation/pose | common base controller; crouch slot maps to eight distinct sitting clips | **Active preserved state** | **Keep** | eight parameterized `Player_Croutch` mapping tests |
| managed Butler/Guest `AnimationClip` assets under `Player`, `ButlerClassic`, the eight guest animation roots, and `Chapter2Panic` | Butler and Guest 1-8 | Animator playback | animation bindings | managed-character motion/sprites | no binding whose property contains `localScale`, case-insensitive | **Clean** | keep the managed character corpus clean; prohibit every transform-scale curve spelling, including `m_LocalScale`, without banning legitimate scale animation for unrelated UI/effects/props | scoped `AssetDatabase.FindAssets("t:AnimationClip", managedCharacterAnimationRoots)`, `LoadAllAssetsAtPath`, and `AnimationUtility.GetCurveBindings` test |
| Gameplay Drawing/Dining anchor and occlusion records | Guest 1-8 | Chapter 1 placement and Chapter 2 numeric/name sorting | position, pose assignment and sorting/occlusion; no body scale | authored placement and visibility layering | 8 Drawing anchors, standing Guests 3/5/7, 8 Dining anchors, 8 seat/chair bindings | **Active preserved state** | **Keep** | snapshot locks both anchor sets, assignment rules and bindings |

## Known inconsistencies and risks

- The stale Drawing Room Butler aliases conflicted with the former final fields: front `2.0664465` versus `2.057692`, and back `1.1154557` versus `1.3752748`. Both conflicting values survive only as migration evidence; the active serialized paths are removed.
- Every former guest participant's captured base differed from at least one live scene-root override component. Those captured bases remain historical evidence and must not become Phase 2 authored baselines.
- Migration order was a material risk: removing `GuestScaleParticipant` before neutralizing secondary writers would have reactivated body-scale writes. That order was enforced; secondary writers were neutralized first and the participant stack was deleted last.
- `PlayModeLayoutCaptureWindow` still records scale for legitimate layout targets, but now rejects managed character hierarchies both when capturing and when applying pending data.
- Animation builders are still destructive asset-generation tools, so they require explicit confirmation. All eight sitting mappings are guarded, the general builder preserves authored/existing crouch mappings, and the Guest 2 builder no longer opens or saves Gameplay.
- The profiles remain shared non-size presentation assets. Their legacy character scale curves are frozen in the snapshot, removed from both active assets, and guarded against reintroduction.
- Phase 2 must use the current authored roots as reviewed inputs, not captured legacy bases, and must account for inherited room-stage zoom exactly once. Phase 1 deliberately provides no active size compatibility bridge.
- Three pre-existing unresolved serialized GUIDs remain in Gameplay, but none is a script or character-sizing reference: `picture_frame_background`'s Image sprite (`9c6ee14c201faa34abd09ec9f9949f9d`), disabled CameraManager `anchoredAnimationFrames` (`c0e133a71a4f4d3bb8b4a65e5a9f1c02`), and the Drawing Room `Lady` picture's `StaticSetImagePlayer.set` (`ed7c27fb5f0b4d3cab8db1f6b4a6d901`). Both migration passes proved these references were unchanged. All deleted legacy scale GUIDs and component records are absent.

## Final Phase 1 report and verification

### Deleted legacy architecture

Runtime sources/types removed with their metadata:

- `GuestRoomScaleCalibration`
- `GuestRoomScaleApplier`
- `GuestRoomStageScaleUtility`
- `GuestScaleParticipant`
- `CharacterVisualProfile`

Editor and obsolete-test sources removed:

- `GuestRoomScaleMasterWindow`
- `ButlerRoomScaleCalibrationWindow`
- `PointClickPlayerMovementEditor`
- `RoomProjectedEntityEditor`
- `GuestScaleAudit`
- `GuestButlerScaleRegressionTests`

Their runtime factories, discovery/repair paths, compatibility APIs, serialized components, stale prefab modifications, custom controls, legacy scale curves, and GUID references were removed as part of the same migration. The machine-readable pre-removal evidence is `docs/migrations/character-scale/legacy-character-scale-snapshot.json`.

### Retained responsibilities

- `PointClickPlayerMovement`: movement, routing, room transitions, visible-position conversion, animation, and sorting; Phase 2 gets only read-only `CurrentRoomId` plus the current visible foot point transformed by `CameraManager.TryGetActiveRoomStageLocalPoint`.
- `ActorRoomState`: actor ID, room/pose/visibility/interaction state, placement, and position-only room-stage binding.
- `RoomProjectedEntity` and `RoomPerspectiveProfile`: position, tint, sorting, floor, and dedicated contact-shadow presentation; no character or prop visual-root size output.
- `RoomPersonWalker2D`: authored movement, bob/sway, Animator state, presentation rotation, and tint.
- Chapter code: authored anchors, seats, standing/seated pose, visibility, panic motion/sprite swaps, occlusion, coats, held items, and interaction children without actor-root size mutation.
- Camera room-stage zoom remains a parent-coordinate-system operation. Dining/Drawing sitting mappings and placements are preserved. No distinct eating runtime state or approved eating clip exists yet.

### Exact verification commands

```bash
timeout 240s /home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics -quit -projectPath /home/hamzak/Desktop/ChateauChantilly -logFile /tmp/character-scale-phase1-final-compile3.log
timeout 300s /home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics -projectPath /home/hamzak/Desktop/ChateauChantilly -runTests -testPlatform EditMode -testFilter CharacterScaleOwnershipRegressionTests -testResults /tmp/character-scale-phase1-final-ownership3.xml -logFile /tmp/character-scale-phase1-final-ownership3.log
timeout 240s /home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics -projectPath /home/hamzak/Desktop/ChateauChantilly -runTests -testPlatform EditMode -testFilter 'CharacterScaleOwnershipRegressionTests.SeatedAndChapterOneCoatTransitionsDoNotResizeActorRoots;Chapter2RegressionTests.RepeatedChapter2GuestPanicBeginStopRestoresActorStateWithoutScaleDrift;CharacterScaleOwnershipRegressionTests.CharacterControllerFacingFlipsRenderersWithoutChangingRootScale;CharacterScaleOwnershipRegressionTests.CharacterAdjacentScaleWritesStayOnDedicatedPresentationChildren;CharacterScaleOwnershipRegressionTests.PointClickSourceOwnsPositionAndSortingButNeverCharacterScale;RoomProjectionRegressionTests.ContactShadowDepthScaleDoesNotChangeAuthoredVisualScale;RoomProjectionRegressionTests.ContactShadowEligibilityIsCachedOutsideThePerFrameProjectionPath' -testResults /tmp/character-scale-phase1-final-transition3.xml -logFile /tmp/character-scale-phase1-final-transition3.log
timeout 240s /home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics -projectPath /home/hamzak/Desktop/ChateauChantilly -runTests -testPlatform EditMode -testFilter StoryActorRoomStageLockingTests -testResults /tmp/character-scale-phase1-final-story3.xml -logFile /tmp/character-scale-phase1-final-story3.log
timeout 600s /home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics -projectPath /home/hamzak/Desktop/ChateauChantilly -runTests -testPlatform EditMode -testResults /tmp/character-scale-phase1-final-editmode3.xml -logFile /tmp/character-scale-phase1-final-editmode3.log
timeout 300s /home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics -projectPath /home/hamzak/Desktop/ChateauChantilly -runTests -testPlatform PlayMode -testFilter CharacterScalePhaseOnePlayModeTests -testResults /tmp/character-scale-phase1-final-playmode3.xml -logFile /tmp/character-scale-phase1-final-playmode3.log
rg -n "GuestRoomScale|GuestScaleParticipant|ButlerCharacterScale|ApplyPerspectiveScale|butlerRoomScaleOverrides|editorSelectedButlerScaleRoomId|CharacterVisualProfile|\bscaleByDepth\b|nativeRoomReferenceSize|scriptedGuestPanicSpriteScaleMultiplier|CaptureOriginalSpriteLocalSize|\boriginalSpriteLocalSize\b|\bcapturedBaseScale\b|\bauthoredActorLocalScale\b|scaleWithRoomStageMotion|roomVisualScaleOverrides" Assets/Scripts Assets/_Chateau/Scripts Assets/Map Assets/Prefabs Assets/Scenes Assets/ScriptableObjects Assets/Animation Assets/Editor --glob '!**/*Tests.cs' --glob '!**/*RegressionTests.cs'
rg -n -i "localScale|m_LocalScale|m_SizeDelta" Assets/Animation/Player Assets/Animation/ButlerClassic Assets/Animation/Lady Assets/Animation/ButlerGuest Assets/Animation/MisterFlorianKnell Assets/Animation/CountessElowenDusk Assets/Animation/BaronHectorGlass Assets/Animation/LadySabineMarrow Assets/Animation/LordAmbroseVeil Assets/Animation/MadameCoralieThread Assets/Animation/Chapter2Panic
sha256sum Assets/Scenes/Gameplay.unity Assets/Prefabs/Player.prefab Assets/Prefabs/Room_Drawing_Room.prefab Assets/Prefabs/Room_Drawing_Room_Perspective.prefab Assets/ScriptableObjects/Rooms/DrawingRoomPerspectiveProfile.asset Assets/ScriptableObjects/Rooms/DiningRoomPerspectiveProfile.asset
git diff --check
```

Unity test commands intentionally omit `-quit`; Unity compilation uses it.

### Verification results

| Gate | Result | Evidence |
|---|---|---|
| Unity compile/import | exit 0; no C# errors/warnings and no missing-script, failed-conversion, or serialization-layout match | `/tmp/character-scale-phase1-final-compile3.log` |
| Ownership fixture | 38/38 passed; includes Player prefab and Gameplay preview reload with no missing/obsolete scale components, exact sitting mappings, managed-animation binding scan, read-only foot seam, and serialized cleanup | `/tmp/character-scale-phase1-final-ownership3.xml` |
| Repeated transition/facing/panic/shadow gate | 7/7 passed, including cached contact-shadow eligibility outside the per-frame path | `/tmp/character-scale-phase1-final-transition3.xml` |
| Room-stage fixture in isolation | 10/10 passed | `/tmp/character-scale-phase1-final-story3.xml` |
| Live Gameplay PlayMode gate | 1/1 passed; exact authored Butler scale `(2.150601, 2.150601, 1)`, all eight guests X/Y `1.42`, guest Z multiset `1, 1, 1.12, 1.12, 1.3, 1.3, 1.3, 1.3`, no drift for five frames, no missing component slots, and no targeted load/serialization failure log | `/tmp/character-scale-phase1-final-playmode3.xml` |
| Full EditMode comparison | 250 total, 209 passed, 41 failed versus baseline 279/226/53; twelve fewer failures and no new failure lineage | `/tmp/character-scale-phase1-final-editmode3.xml` and `/tmp/character-size-phase1-baseline-editmode.xml` |
| Legacy production/serialized symbol scan | zero matches | command above |
| Managed animation raw scale-binding scan | zero `localScale`, `m_LocalScale`, or `m_SizeDelta` matches in the explicit Butler/guest roots; the ownership fixture also checks imported curve bindings | final shell audit plus ownership XML |
| Deterministic serialized hashes | all six Gameplay/Player/Drawing asset/profile hashes match the final table above | final `sha256sum` audit |
| Patch whitespace | clean | `git diff --check` |

The full suite is reported as failed, not passed. Every final failure either existed under the same name at baseline or is one of six renamed scale-neutral versions of a baseline scale-era test:

| Final test | Baseline lineage |
|---|---|
| `CharacterRegressionTests.RoomPeopleAreEditableAnimatedSceneObjects` | `CharacterRegressionTests.RoomPeopleAreEditableDepthScaledSceneObjects` |
| `StoryActorRoomStageLockingTests.ActorRoomStateExposesTheBoundRoomLocalFootPoint` | `StoryActorRoomStageLockingTests.GuestScaleDepthUsesTheActorsBoundRoomStageFootPoint` |
| `StoryActorRoomStageLockingTests.RoomStageBindingKeepsVisibleFeetOnAnchorWithoutResizingActor` | `StoryActorRoomStageLockingTests.RoomStageBindingAndGuestScalingKeepVisibleFeetOnTheAnchor` |
| `StoryActorRoomStageLockingTests.WorldActorBindingPreservesAuthoredScaleWithRoomPerspectiveProfile` | `StoryActorRoomStageLockingTests.WorldActorBindingUsesRoomPerspectiveProfileScale` |
| `StoryActorRoomStageLockingTests.WorldActorKeepsAuthoredScaleAcrossRoomBindingPanZoomAndRoomRefresh` | `StoryActorRoomStageLockingTests.WorldActorCanKeepAuthoredScaleWhileLockedToRoomStage` |
| `StoryActorRoomStageLockingTests.WorldActorRoomZoomChangesBoundPositionWithoutResizingActor` | `StoryActorRoomStageLockingTests.WorldActorUsesAuthoredScaleAsRoomZoomBaseline` |

The five Story cases fail only in the contaminated full headless order, where a global camera rectangle becomes invalid; the same complete fixture passes 10/10 in isolation. This pre-existing test-isolation problem is not hidden by the Phase 1 completion claim.

## Task 1 regression proof

`CharacterScaleOwnershipRegressionTests` began with three guardrails before runtime cleanup:

1. the schema/version/source/hash/count snapshot contract;
2. eight explicit `Player_Croutch` to authored sitting-clip mappings;
3. a scan of every imported `AnimationClip` sub-asset in the explicit Butler, Guest 1-8, shared Player/Butler, and Chapter 2 panic roots using `AnimationUtility.GetCurveBindings`, rejecting any property whose name contains `localScale` with case-insensitive comparison (therefore including `m_LocalScale`).

Task 1 focused result was 10 tests, 10 passed, 0 failed. The completed ownership fixture is now 38/38 and also guards deleted types/files/GUIDs, runtime factories, source and serialized scale seams, all 13 projected-entity records, both profile assets, authored per-renderer facing, carried-coat attachment position, room/stage motion, panic, sitting mappings, editor scale controls, authored visual-scale invariance, Gameplay/Player reload, and dedicated-only contact-shadow behavior.
