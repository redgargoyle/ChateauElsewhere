# Architecture migration report

## Current phase

**Phase 4 vertical migration in progress; the first three set pieces are complete while Phase 3 compatibility retirement continues.**

This report records what is implemented in the repository at this commit. It must be updated after every Unity-validated migration phase.

## Source baseline

- Unity editor version: `6000.4.10f1`
- Runtime C# files: 90
- Runtime C# lines: 49,902
- Direct `MonoBehaviour` declarations: 63
- Architecture-smell counts are recorded in `Baseline/architecture_guard_baseline.json`.

## Implemented and Unity-validated

- Added the explicit `GameRoot`/`GameContext` composition spine.
- Added service, chapter, room, interaction, actor, motor, presenter, UI, definition, story-beat and state-machine bases.
- Rebased selected major managers/controllers while retaining their existing script filenames and `.meta` GUIDs.
- Added configuration validation and deterministic service initialization/shutdown.
- Removed the scheduler's global clock search fallback.
- Added an Editor-only GameRoot installer and active-scene validator.
- Added static architecture inventory, serialized-reference scan and debt-ceiling guard.
- Added CI guard workflow.
- Deleted three proven-unused scripts: `NewBehaviourScript`, `PickupObject`, and the behaviorally-zero `GameClockHandsDisplay` runtime hook.
- Serialized exactly one `Chateau_GameRoot`, eight unique services, one scene behaviour, and `GameDatabase.asset` in `Gameplay.unity`.
- Preserved all 5,937 unrelated pre-existing Gameplay YAML documents byte-for-byte during the root graft.
- Added a real MainMenu-to-Gameplay lifecycle test with an Entrance/Drawing Room round trip and exact-one service assertions.
- Separated room-stage coordinate layout from Butler presentation scaling. The approved `0.7528645` presentation baseline is explicit on `Player.prefab`; raw room calibration and guest scale data remain unchanged.
- Removed `RoomNavigationBootstrap` after proving that the serialized root supplies exactly one navigation manager and prompt controller from MainMenu startup through a room round trip.
- Removed `ChapterManager.BootstrapChapterManagerForGameplay`; the serialized Chapter 1 stack now owns startup. Chapter 2 controller ownership was migrated in a separate gate.
- Serialized one inert `Chapter2Controller`, wired its existing dependencies, and bound it through GameRoot. Repeated Chapter 2 debug skips reuse the same controller and HUD while preserving the characterized feature behavior.
- Removed the `ChapterManager` factory for `Chapter2Controller`; every chapter transition and debug-skip entry path now resolves the single serialized controller.
- Removed the Chapter 2 HUD factory; Chapter 2 now reuses the single serialized HUD on every entry path.
- Serialized the three inert Chapter 2 feature owners (monster stinger, guest panic, and guest search), bound their stable scene references, registered them with GameRoot, and removed their independently gated creation fallbacks.
- Removed `Chapter2Controller.ResolveReferences` and its dormant scene-wide repair searches. Chapter 2 now validates and uses fourteen serialized manager, navigation, clock, player, UI, feature, subtitle, speech, and clock-strike audio dependencies directly.
- Bound Chapter 1 to the existing serialized guest-scale applier, preserving the single ownership chain from applier to calibration to approved Butler source; every identity is lifecycle-tested before factory retirement.
- Removed runtime creation of guest-scale applier/calibration owners; the Guest Size Master retains an Editor-only, Undo-aware authoring action, and runtime creation is limited to per-guest participants.
- Explicitly wired the serialized dialogue and subtitle services, subtitle line bank, navigation edge, and Chapter 1 consumers while preserving lazy voice/indicator/subtitle-view creation.
- Removed the core dialogue/subtitle `FindOrCreate` factories and every caller; GameRoot is now the only owner of these services, while voice/indicator child ownership remains separately gated.
- Serialized one voice-playback owner with a dedicated AudioSource and one speaking-indicator owner, explicitly wiring their catalog, navigation, sprite, and service consumers while preserving lazy subtitle/bubble child views.
- Removed the voice-playback and speaking-indicator root factories; GameRoot validation now rejects missing dialogue ownership, while the actual subtitle canvas and bubble renderer remain first-use presentation children.
- Bound SubtitleService to the same serialized speaking-indicator owner used by DialogueSpeechService, preparing direct cleanup without global lookup.
- Replaced redundant global dialogue cleanup with direct serialized-owner calls and removed `SpeakingCharacterIndicator.HideAnyCurrent` plus `DialogueSpeechService.StopAnyCurrentSpeech`.
- Bound ChapterManager directly to the serialized dialogue/subtitle services and added strict composition validation before removing its remaining skip-time global cleanup.
- Routed chapter skips and settings teleports through ChapterManager's direct dialogue cleanup command, then removed `GuestVoiceLinePlayback.StopAnyCurrentLine` and all associated global subtitle searches.
- Added a real cataloged-voice lifecycle using `SUB_CH01_BUTLER_WELCOME_001`: it verifies authored subtitle/clip GUID resolution, Dialogue-channel volume math, primary source/binding reuse, and restoration of both previously enabled and previously disabled player input.
- Serialized DialogueSpeechService's exact Butler movement owner and the primary voice source's `GameAudioSourceVolume`. Stable speech now configures that authored Dialogue binding directly and validates the playback graph; the intentional temporary overlap source/binding remains dynamic.
- Replaced coroutine-local blocking-input restoration with a token-safe lease owned by DialogueSpeechService. Cancellation releases synchronously before chapter transitions apply their state, older routines cannot release newer speech, and service disable/shutdown cannot strand the Butler input-disabled.
- Removed SubtitleService's line-bank resource/navigation repair and GuestVoiceLinePlayback's catalog/navigation repair. Both owners now use validated serialized dependencies directly; subtitle canvas/EventSystem composition and temporary overlap audio remain explicitly deferred presentation behavior.
- Deleted the legacy `GameClockHandsDisplay` hook and its `.meta` after exhaustive code/serialized/binary/resource/reflection/animation checks and a MainMenu-to-Gameplay lifecycle proved it attached zero components and created zero analog-clock canvases. Current ticking, disabled generic close-up, and Chapter 2 seven-o'clock presentation remain owned elsewhere.
- Serialized the inert Chapter 1 HUD owner on the Chapter 1 controller while preserving the characterized first-use canvas/text construction and sorting order.
- Removed Chapter 1 HUD global lookup/runtime attachment and the obsolete `createRuntimeHud` flag; HUD child presentation remains lazy and owner-scoped.
- Serialized the RuntimeSettingsMenu owner and correctly scaled overlay canvas under GameRoot, explicitly wiring navigation, chapter, clock, and exploration-music dependencies while keeping controls lazy.
- Removed RuntimeSettingsMenu's global/root/canvas factory and made navigation initialize the serialized owner directly; nested controls remain lazy inside that owner.
- Serialized the exploration-music `GameAudioSourceVolume` owner and bound it directly to RuntimeSettingsMenu. This removes its active component factory and fixes the migration regression that clamped the authored `0.125` base volume to zero whenever the already-serialized AudioSource bypassed legacy discovery.
- Removed RuntimeSettingsMenu's eight scene-wide dependency searches, EventSystem creation, RectTransform repair, manager/clock/music resolvers, and per-frame source reconfiguration. Its complete serialized graph is now delegated into architecture validation; only the twelve owner-scoped control constructors and their eight UI-component additions remain dynamic.
- Characterized fireplace and clock ambience before changing ownership: each has a distinct 2D looping source and assigned entrance clip, only fireplace has an enabled high-pass filter, and both owner/source identities survive room travel.
- Serialized dedicated fireplace and clock ambience owners under GameRoot, explicitly binding navigation, catalogs, separate AudioSources, and the fireplace high-pass filter while retaining the old factories for a separate removal gate.
- Removed both ambience root factories plus their global navigation lookup, Resources catalog repair, and AudioSource/filter component repair; room navigation now initializes only its serialized owners.
- Characterized the first Drawing Room set piece without changing it: the tea table's exact sprite/material/transform/profile and four-point collision footprint are frozen, and tests prove `RoomProjectedEntity` plus `ObjectMovementBlocker2D` both overwrite its renderer. The intended order remains `6627`; the collider-bounds writer varied between `1358` and `1452` across valid runs.
- Added the target `World / Rooms / Props / Set Pieces` foundation: a pure `RoomDepthResolver` plus a static `SetPieceView` that owns only sorting layer/order/pivot and has no frame loop, bounds lookup, search, factory, transform, tint, or collision mutation.
- Migrated `tea_service_table` in Gameplay and both Drawing Room prefabs beneath literal `Props / Set Pieces` owners. The original GameObject/Transform/SpriteRenderer/component IDs, art, material, authored transform, depth anchor/order, blocker identity, and polygon are preserved; the blocker now owns collision only and cannot rewrite presentation.
- Characterized `purple_armchair_back` as the second set-piece candidate across Gameplay and both Drawing Room prefabs: shared chair art/material/profile/anchor resolves to order `8289`, exact transforms and component IDs are frozen, and the Gameplay four-point lower-seat collision footprint is preserved before migration.
- Migrated `purple_armchair_back` in place beneath the existing `Props / Set Pieces` owners. Its original GameObject/Transform/SpriteRenderer/component IDs, chair art/material, authored transforms, depth anchor/order, blocker identity, and four-point lower-seat polygon are preserved; its blocker now owns collision only and cannot rewrite presentation.
- Characterized `purple_sofa` as the third set-piece candidate across Gameplay and both Drawing Room prefabs: its sofa art/material/transform/component identities are exact across all three assets, the room-local anchor resolves to order `5385`, and the Gameplay four-point seating footprint is frozen before migration.
- Migrated `purple_sofa` beneath the existing `Props / Set Pieces` owners with one explicit `SetPieceView` per asset. Its original GameObject/Transform/SpriteRenderer IDs, art/material, authored transform, blocker identity, and four-point seating polygon are preserved; the blocker now owns collision only and cannot rewrite presentation.
- Characterized ChapterManager's remaining player/Chapter 2 repair before removal: runtime resolves the exact Player `PointClickPlayerMovement` and already-serialized Chapter 2 controller, keeps point-click enabled, derives the public Player root correctly, and leaves both legacy Player controllers disabled.
- Serialized ChapterManager's exact Player input (`81962842`) and removed all player, Chapter 2, and obsolete debug-canvas repair searches. Its full eight-owner graph is architecture-validated, the public Player root derives from the serialized input, and only the actual Player prefab instance authors legacy movement disabled.
- Characterized Chapter2Controller's manager identity before tightening its entry boundary: the controller starts with ChapterManager `3301000004` and retains that exact serialized owner through first and repeated Chapter 2 debug entry.
- Made Chapter2Controller's four external entry commands enforce their serialized ChapterManager instead of rebinding it from a caller. Valid and null convenience calls keep existing behavior; a missing or different manager is rejected before phase, navigation, clock, guest, or UI mutation.
- Grafted a dedicated `Audio_Chapter2ClockStrike` child beneath Chapter 2 with its own serialized non-looping 2D `AudioSource`, Game-Sounds `GameAudioSourceVolume` at base volume `0.4`, and imported clip GUID `d7084eafa9124afcbcbf12529e08bc70`. Seven-PM playback reuses those exact identities, and the obsolete resource/component/runtime-tone fallback is removed.
- Characterized and retired the monster stinger's structural repair: Begin/Stop reuses the exact serialized monster, Drawing Room start/target anchors, navigation service, and Image, restores the authored sprite, and creates no placeholder; validation now rejects a missing/mismatched graph instead of searching or repairing it.
- Characterized and serialized Guest Search's navigation owner: the pre-migration field was null until seven-PM staging, while the migrated owner is the exact GameRoot service from boot through repeated Chapter 2 and seven-PM paths; all five lazy repair calls and the resolver are removed.
- Characterized the monster presentation fallback before authoring it: first visible use creates one host violin source and Game-Sounds binding, loads clip GUID `69f06d321e4549cdcad1133332661f6d`, adds one sorted Canvas to the monster, loads eight ordered run sprites, and reuses every identity on repetition.
- Grafted the violin source and Game-Sounds binding directly onto the existing inactive monster object, serialized the imported clip, and removed every host/resource/Editor/component fallback. Playback configures only that owned graph without a hierarchy expansion or ChapterManager-host collision.
- Grafted the characterized overlay Canvas directly onto the same monster object with screen-space overlay and People order `10000`, then removed its dormant Get/Add component fallback. The stinger validates and configures that exact Canvas without adding a hierarchy object or component.
- Serialized the eight approved monster run sprites in exact raw frame `01` through `08` order, then removed the resource-path loader and name-sort fallback. Configuration validation rejects an incomplete array, and lifecycle proof shows the same array exists at boot and survives repeated stingers.
- Characterized the Entrance coat-hanger ownership failure before changing it. Fresh boot attempts repair before the authored hanger is discoverable and leaves only the pantry closet; a later explicit repair creates one stable Entrance closet, action, and trigger collider, preserves stored coats across repeated resolution, and retains identities through a room round trip.
- Moved existing closet component `3303000001` onto authored Entrance hanger `1592234992`, added serialized action `1592234995` and trigger `1592234996`, and bound the controller approach directly to transform `1592234993`. The pantry placeholder remains for separate review but no longer owns a duplicate closet.
- Removed the Entrance hanger name search, closet/action/collider factories, computed-collider fallback, pantry-anchor fallback, and global closet scan. Chapter 1 now validates its two serialized hanger edges; repeated resolution retains them without mutation.
- Characterized Chapter 1's remaining core-reference repair: the resolved ChapterManager, clock, scheduler, camera, navigation, Player movement, and Butler root retain exact identities through repeated resolution, room travel, repeated Chapter 2 entry, and seven-PM staging.
- Serialized Chapter 1's exact clock `3301000001`, camera `2050006783`, navigation `1878886997`, and Player movement `81962842` edges in its existing component document, with validation for the complete core graph and a derived Butler root.
- Removed all Chapter 1 discovery for its manager, clock, scheduler, camera, navigation, and Player across reference resolution, projection, room checks, subscriptions, and guest-scale setup. The Butler root now derives only from serialized Player movement.
- Characterized the final Chapter 1 core-graph ownership hole: `BeginChapter1` still replaces its serialized manager from a non-null caller, while valid startup and repeated transition lifecycles retain manager `3301000004`.
- Hardened the Chapter 1 entry boundary: valid and null convenience calls retain serialized manager `3301000004`; a mismatched manager logs one explicit error and returns before reset, scheduling, or world/UI mutation.
- Characterized the front-door ownership failure: authored trigger `1180734296` with action `1180734300` and collider `1180734299` remains unchanged, but startup creates a second `Door_answer_trigger` and binds Chapter 1 to that duplicate. Repeated repair, room travel, and chapter skips retain the duplicate without further component growth.
- Serialized Chapter 1's direct edge to authored action `1180734300` and validated its existing trigger collider. The authored action is now preferred from boot, eliminating the runtime duplicate while the old search/factory path remains isolated for the next cleanup gate.
- Removed the front-door alias lookup, runtime trigger/action/renderer/collider/UI factories, collider-size derivation, and generic front-door click-target branch. Chapter 1 now only activates and configures its validated serialized action; the approach fallback addresses that same action directly.
- Pruned the unowned pantry `CoatCloset / Anchors / ApproachFront` placeholder subtree (documents `3503000002`–`3503000008`) and removed its dead `FindPropAnchor`/`IsUnderNamedTransform` lookup path. The Entrance hanger remains the sole serialized closet owner.
- Characterized Chapter 1's remaining immutable data lookups: Resources resolves footstep catalog GUID `0e780686c6653db1a1c74916a591d484`; dynamic searches resolve the exact Entrance placemark, Drawing Room door target, Entrance/Drawing room-content owners, and ordered eight authored guest points without generating fallback seats.
- Serialized the characterized catalog, Entrance placemark, Drawing Room door target, Entrance/Drawing `RoomContentGroup` owners, and ordered eight guest points directly on Chapter 1. Configuration validation now requires that exact complete graph while the lookup code remains staged for its cleanup gate.
- Removed Chapter 1's footstep Resources fallback, RoomAnchor/object-name discovery, duplicate door-trigger scan, room-content scan, legacy three-seat fields, and generated-seat fallback. Guest placement now reads only the validated ordered eight-point graph; configuration rejects point reordering by exact `RoomAnchor` identity, and startup diagnostics consume the same authoritative report while unrelated dynamic guest creation remains intact.
- Characterized Chapter 1's runtime doorbell graph before migration: first startup creates exactly one `DoorbellSystem`, `AudioSource`, and Game-Sounds binding on the Chapter 1 host; it binds the Chapter clock, loads approved clip GUID `67dc6970d473422a86e0c071ef23abd1` / fileID `8300000` on first ring, and reuses every identity thereafter.
- Serialized that exact doorbell graph on the existing Chapter 1 host as components `3302000003`–`3302000005`, with direct clock/source/binding/imported-clip edges and configuration validation. The AudioSource resource remains empty because `DoorbellSystem` owns the one-shot clip, matching characterized behavior; repair paths remain staged for the cleanup gate.
- Removed Chapter 1's doorbell discovery/component factory and `DoorbellSystem` clock/source/binding/resource/generated-tone fallbacks. Initialization rejects a different clock, configures only its direct source/binding, and rings only the validated imported clip.
- Characterized the runtime-only Drawing Room exit click target before pruning. ChapterManager preparation creates one transparent `160x160` trigger under inactive Drawing Room anchors; another initialization duplicates it because `GameObject.Find` cannot see the inactive owner, and those identities persist into Chapter 2. Its action performs no navigation or state mutation beyond calling the same completion gate already owned by empty-door answer, coat storage, guest seating, and Drawing Room entry.

## Current static result

| Metric | Baseline | Candidate | Delta |
|---|---:|---:|---:|
| Runtime C# files | 90 | 106 | +16 |
| Runtime C# lines | 49,902 | 48,982 | -920 |
| Direct `MonoBehaviour` declarations | 63 | 50 | -13 |
| `FindObject*`/`GameObject.Find` | 199 | 120 | -79 |
| `Resources.Load` | 27 | 17 | -10 |
| runtime `new GameObject` | 98 | 81 | -17 |
| runtime `AddComponent<T>` | 100 | 61 | -39 |
| runtime initialization hooks | 9 | 4 | -5 |

The temporary source increase is the migration spine and verification tooling. It is not evidence that the cleanup is finished.

## Validation completed

- architecture debt-ceiling guard passed;
- `git diff --check` passed;
- all C# scripts have `.meta` files;
- no duplicate script GUIDs were found;
- static source inventory was regenerated;
- serialized text-asset reference inventory was regenerated;
- new architecture files passed lightweight delimiter/preprocessor checks.
- Unity `6000.4.10f1` compiled the project and produced result XML for every automated run;
- monster overlay cleanup passed its static ownership guard and rendered lifecycle gate; the full suite remained exactly 240 total / 190 passing / 50 known failures with an unchanged failure-name set;
- the Canvas cleanup changed no scene, prefab, asset, `.meta`, component ID, or script GUID;
- monster sprite authoring passed its exact ordered-reference guard and rendered lifecycle gate; the full suite remained exactly 240 total / 190 passing / 50 known failures with an unchanged failure-name set;
- the sprite graft kept all 5,982 scene documents in identical order, changed only controller document `3301000007`, and preserved `SceneRoots`, every sprite/importer/`.meta`, and every component ID;
- monster sprite fallback cleanup passed source guards, the rendered repeated-use lifecycle, and the exact full-suite comparison; only the resource-path property was removed from controller document `3301000007`;
- Entrance coat-hanger characterization passed exact serialized art/transform ownership checks and rendered before/after-repair lifecycle checks; the full suite remained 240 total / 190 passing / 50 known failures with an unchanged failure-name set;
- Entrance coat-hanger graft passed exact serialized-owner checks and rendered boot/repeated-resolution/room-round-trip lifecycle checks; the scene stayed at the same roots, preserved every prior document in order, added only action/collider documents, changed only four intended owner/reference documents, and kept the authored Transform and SpriteRenderer byte-identical;
- Entrance coat-hanger cleanup passed ownership/source guards and the rendered lifecycle; the full suite improved to 191 passing / 49 known failures because its stale source-extraction regression was corrected, with no new failure names;
- Chapter 1 core-reference characterization passed its static null-edge/source-path guard and rendered multi-transition lifecycle; the full suite remained 191 passing / 49 known failures with an unchanged failure-name set;
- Chapter 1 core-reference graft passed exact serialized-edge/configuration guards and the rendered multi-transition lifecycle; all 5,984 scene documents/roots remained ordered and only component `3302000001` changed; the full suite remained 191/49;
- Chapter 1 core-reference cleanup passed zero-discovery source guards and the rendered multi-transition lifecycle; the full suite remained 191/49 with an unchanged failure-name set;
- Chapter 1 manager-boundary characterization passed its source guard and rendered valid-manager lifecycle; the full suite remained 191/49;
- Chapter 1 manager-boundary guards passed static, explicit mismatch, valid lifecycle, and full-suite tests; the suite remained 191/49 with an unchanged failure-name set;
- front-door characterization passed exact authored-document/source guards and a rendered two-owner lifecycle; the full suite remained 191/49 with an unchanged failure-name set;
- front-door serialization passed its exact one-document scene audit: all 5,984 document IDs/order, uniqueness, and 13 roots remain unchanged; the rendered lifecycle retains one authored trigger/action/collider through repeated initialization and a room round trip; the full suite remains 191/49 with an unchanged failure-name set;
- front-door fallback cleanup changes no serialized asset; source guards ban every alias/search/factory/repair method, the rendered lifecycle keeps the exact action/collider and one trigger through repeated configuration/travel/skips, and the full suite remains 191/49 with an unchanged failure-name set;
- pantry placeholder pruning removes exactly seven Gameplay documents, preserves all other document order and all 13 roots, leaves no reference to the removed IDs, and passes the Entrance ownership guard plus stored-coat/room-round-trip lifecycle; the full suite remains 191/49 with an unchanged failure-name set;
- Chapter 1 immutable-data characterization passes exact null-edge/source-path guards and a rendered lifecycle that freezes catalog GUID/fileID, room/anchor identities, all eight guest-point transforms, repeated resolution, room travel, skips, and zero runtime-seat growth; the full suite remains 191/49 with an unchanged failure-name set;
- Chapter 1 immutable-data serialization changes only controller document `3302000001`, preserves all 5,977 document IDs/order and 13 roots, leaves every referenced object/asset document byte-identical, and passes strict configuration plus the same rendered identity lifecycle; the full suite remains 191/49 with an unchanged failure-name set;
- Chapter 1 immutable-data cleanup removes only five obsolete properties from controller `3302000001`; document IDs/order/roots and every catalog/anchor/room document remain exact, source guards ban every retired resolver/factory path, and the rendered lifecycle retains all identities/transforms, rejects and recovers from an intentional point-order swap, and creates zero seats. The full suite is 194/46 with no new failures; corrected method/document extractors resolve the former false-red `WorldSpaceGuestsUseWorldSpaceDrawingRoomTargets`, `DrawingRoomGuestMovementUsesEditableScenePoints`, and `LiveDoorAnswerUsesStableEntranceWorldPositions` gates;
- Chapter 1 doorbell characterization passes its pre-graft source/scene guard and rendered first-ring/repeated-resolution lifecycle, pinning one same-host owner/source/binding plus the exact imported clip while the existing factories remain intentionally staged; the full suite remains 194/46 with an unchanged failure-name set;
- Chapter 1 doorbell serialization adds only documents `3302000003`–`3302000005`, changes only host `1696549391` and controller `3302000001`, preserves every prior document's order plus SceneRoots, and passes exact YAML, imported-clip, configuration, first-ring, repeated-resolution, and full-suite gates at 194/46 with an unchanged failure-name set;
- Chapter 1 doorbell cleanup removes only the obsolete resource-path property from document `3302000003`, preserves all 5,980 document IDs/order/roots, bans every retired discovery/factory/resource/tone symbol, and retains the exact owner/source/binding/clip through first ring and room travel; the full suite remains 194/46 with an unchanged failure-name set;
- Drawing Room exit-target characterization passes exact source/scene guards and a rendered lifecycle that freezes its inactive-parent component contract, proves repeat initialization grows the target count from one to two, and retains both identities through room travel and Chapter 2 while independent completion-gate callers remain pinned; the full suite remains 194/46 with an unchanged failure-name set;
- the strict GameRoot graft audit passed 53/53 checks;
- the Chapter 2 feature graft audit passed 22/22 checks: three documents added, only three intended existing documents changed, and all other scene documents/order/roots preserved;
- the guest-scale ownership-chain audit passed 6/6 checks: no documents added/deleted, only the Chapter 1 component changed, and document order stayed exact;
- the dialogue-core binding audit passed 6/6 checks: no documents added/deleted, exactly three intended components changed, and SceneRoots/document order stayed exact;
- the dialogue-auxiliary graft audit passed 11/11 checks: one AudioSource and two owner documents added, only their three intended consumers changed, and all old scene documents/order/roots stayed exact;
- the SubtitleService indicator-binding audit passed 5/5 checks: no document churn, one intended service document changed, and document order stayed exact;
- the ChapterManager dialogue-binding audit passed 6/6 checks: no document churn, one intended manager document changed, and exact service-reference counts were preserved;
- the Chapter 1 HUD graft audit passed 5/5 checks: one owner document added, only its GameObject/controller changed, and all old document order stayed exact;
- the RuntimeSettings owner graft audit passed 7/7 checks: eight documents added under GameRoot, only navigation/root-transform changed, and SceneRoots/old document order stayed exact;
- the exploration-music ownership graft added exactly one component document, changed only its GameObject component list and RuntimeSettings binding, preserved the original AudioSource document hash (`3ed1ddba...2487`), old document order, root Transform, and SceneRoots; the desired `.125` lifecycle contract was red before the fix and green after it;
- the RuntimeSettings dependency-cleanup static, scene-reset, and full lifecycle gates pass; the only scene edit authors the already-enforced `playOnAwake = false` value on the existing music AudioSource, with document order, component identity, roots, clip, pitch, loop, and base volume unchanged;
- the dialogue stable-owner graft added one volume-owner document and changed only the GameRoot component list plus the speech/playback binding documents; old document order is exact, the voice AudioSource hash remains `eb0ec0c...6486`, the Player stripped component remains `d2182512...db56`, SubtitleService remains `9938c314...79a08`, and GameRoot lists/Transform/SceneRoots are unchanged;
- the real voice/input lifecycle initially exposed nondeterministic global player selection, then passed with the serialized `81962842` owner and pre-existing `1878887003` voice binding through repeated cancellation/reuse;
- the blocking-input lease lifecycle proves enabled and disabled restoration, synchronous cancel-before-transition ordering, cancelled-coroutine non-interference, and same-frame replacement ownership; focused/static and full-suite gates pass;
- the dialogue dependency cleanup changes only the serialized SubtitleService and GuestVoiceLinePlayback documents by removing their obsolete resource-path strings; document order, all references, GameRoot lists/Transform, and SceneRoots remain exact, while real voice/input lifecycle and full suite pass;
- the clock-hands absence, navigation close-up, Chapter 2 clock-strike, lifecycle, architecture guard, serialized scan, and full-suite gates pass after deletion; all 51 baseline failure names remain exact and no missing-script warning appears;
- the focused ambience characterization passed and the full-suite gate retained the exact baseline failure-name set;
- the ambience-owner graft audit passed 6/6 checks: nine documents added, only navigation/root-transform changed, every old document retained its exact order, and SceneRoots stayed byte-identical;
- the ambience-factory cleanup audit passed 5/5 checks: no document churn, only the two ambience owner documents changed, document order stayed exact, and SceneRoots stayed byte-identical;
- the tea-table static characterization and full MainMenu/room-loop lifecycle passed, including direct proof that both legacy owners can write the same renderer while the polygon remains unchanged;
- both SetPiece foundation unit/static tests passed, and the full-suite failure-name set remained unchanged;
- the tea-table asset audit passed 16/16 structural checks across Gameplay and both prefabs: six documents added total, none deleted, only the exact hierarchy/owner documents changed, all prior document order stayed exact, and SceneRoots stayed byte-identical;
- the migrated lifecycle keeps one bound set-piece identity through activation/travel, resolves order `6627`, preserves all four collider points, and proves blocker sorting is a no-op;
- the purple-armchair static/lifecycle characterization passes across all three assets; at runtime legacy projection writes `8289` and the blocker then overwrites it with `1498`, proving the same competing-writer defect without freezing the bounds-derived blocker value;
- the purple-armchair migration changes no YAML document IDs or order: the three existing projection documents become `SetPieceView` in place, only the intended hierarchy/renderer/blocker/GameRoot documents change, and all three `SceneRoots` sections remain byte-identical;
- the migrated purple-armchair lifecycle keeps the same bound view/blocker identities through activation and travel, resolves order `8289`, preserves its art, authored transform and all four collider points, and proves blocker sorting is a no-op;
- the purple-sofa static/lifecycle characterization passes across all three assets; at runtime its only legacy sort writer changes the renderer to bounds-derived order `1225` while the intended room-local order is `5385`, and the trigger polygon remains unchanged;
- the purple-sofa asset audit passes across all three assets: exactly one `SetPieceView` document was added per asset, every prior document keeps its relative order, only the intended seven Gameplay/five-per-prefab documents change, the collider hash is exact, and `SceneRoots` stays byte-identical;
- the migrated purple-sofa lifecycle keeps the same view/blocker identities through activation and travel, resolves order `5385`, preserves its art, authored transform and all four collider points, and proves blocker sorting is a no-op;
- the ChapterManager dependency-cleanup audit changes only scene documents `81962841` and `3301000004`: the exact Player instance gains two disabled legacy-controller overrides, the manager binds Player input `81962842`, every existing transform/scale/calibration value remains byte-identical, and document order/`SceneRoots` are unchanged;
- ChapterManager now contains zero global searches or resolver methods; focused architecture/debug-skip/handoff tests and the full MainMenu/room-loop/repeated-Chapter-2 lifecycle pass with the same Player and controller identities;
- the Chapter 2 manager-boundary test proves a mismatched manager cannot replace the serialized owner or reset phase state, while all four entry methods share the same guard; the valid repeated-entry lifecycle retains manager `3301000004`;
- the Chapter 2 dependency-cleanup static, regression, and lifecycle gates pass; all fourteen serialized references resolve exactly once, `ResolveReferences` and its seven scene-wide searches are absent, and repeated entry/debug paths retain the same owners;
- the Chapter 2 clock-strike graft audit passes: Gameplay documents increase exactly from 5,975 to 5,979; the only added documents are IDs `3301000010` through `3301000013`; the only changed existing documents are parent Transform `2099709258` and `Chapter2Controller` `3301000006`; no document is removed; `SceneRoots`, host GameObject `2099709257`, the imported WAV, and its `.meta` remain byte-identical;
- the focused clock-strike lifecycle and static architecture gates pass, proving exact serialized source/binding/clip reuse, no AudioSource or volume-binding collision on the Chapter 2 host, and no component growth across repeated seven-PM playback;
- the clock-strike cleanup changes only the existing Chapter 2 controller document, removes its obsolete resource-path property, and leaves all 5,979 document headers, `SceneRoots`, dedicated audio documents, imported WAV, and `.meta` exact;
- the cleanup-specific source guards ban `Resources.Load`, `AudioClip.Create`, source/binding factories, and the obsolete resolver/fields; two consecutive seven-PM lifecycles reuse the exact serialized graph without component growth;
- the monster structural characterization pins controller `3301000007`, monster `3700000000`, Image `3700000003`, anchors `98514617`/`382498960`, their RoomAnchor owners/room IDs, exact authored positions, and navigation `1878886997`; focused lifecycle/static gates and the full suite pass before any repair removal;
- the monster structural cleanup keeps all 5,979 document headers and `SceneRoots` exact; only controller document `3301000007` loses its two obsolete repair fields, while every monster/anchor/navigation document and every deferred audio, sprite, timing, shake, sorting, visibility, and overlay field stays byte-identical;
- source guards prove the stinger has zero structural object/anchor searches, primitive placeholder creation, or Image/SpriteRenderer child repair; configuration validation, the unchanged Begin/Stop lifecycle, and the full 240-test failure-name baseline all pass;
- the Guest Search navigation characterization pins the pre-migration null-to-service transition, all five resolver call sites, and exact owner identity through repeated seven-PM staging; focused/static and full-suite failure-name gates pass before serialization;
- the Guest Search migration keeps all 5,979 document headers and `SceneRoots` exact; only component `3301000009` gains `navigationManager: {fileID: 1878886997}`, and the navigation/GameRoot documents remain byte-identical;
- Guest Search validation and source guards require the serialized manager and ban its resolver/global lookup; boot, repeated Chapter 2, repeated seven-PM, architecture, and full-suite gates retain the exact owner and 50-failure baseline;
- the monster presentation characterization freezes source/clip/binding properties, Canvas layer/order, all eight sprite GUIDs in approved order, original-sprite restoration, visible Drawing Room behavior, and repeated-use no-growth; focused/static and full-suite gates pass before scene authoring;
- the monster violin graft adds exactly AudioSource `3700000004` and binding `3700000005`, changing only monster GO `3700000000` and controller `3301000007`; all prior 5,979 document order, `SceneRoots`, monster transform/Image, ChapterManager host, clock audio, MP3, and `.meta` remain exact;
- boot/visible/repeated-use lifecycle gates prove the monster owns one exact source/binding/clip, the shared controller host remains component-free, activation precedes playback, stop restores inactivity, and the full suite retains the same 50 failure names;
- the violin cleanup keeps all 5,981 document headers and `SceneRoots` exact; only controller `3301000007` loses its obsolete fallback-name property, while source/binding/monster/clip/MP3/meta remain byte-identical;
- source guards ban host source discovery/attachment, Resources and Editor clip searches, binding creation, and runtime base-volume discovery; direct serialized configuration plus focused/lifecycle/full gates retain exact behavior and failure names;
- the overlay graft adds exactly Canvas `3700000006`, changing only monster GO `3700000000` and controller `3301000007`; the predicted 5,982-document header hash, `SceneRoots`, transform/Image/audio/host documents, People layer ID, and Canvas schema all verify exactly;
- boot/inactive, visible, stop, and restart lifecycle checks prove the same serialized Canvas remains the sole overlay with unchanged render mode/layer/order; the full suite retains the exact 50 failure names;
- the rendered full EditMode suite discovers 240 tests: 190 pass and the exact same 50 pre-existing failure names remain, with no clock-strike graft or cleanup regression;
- the separate `-nographics` invalid-viewport issue was independently hardened in commit `4d8a6d9a` and is not attributed to the clock-strike graft;
- the MainMenu boot/navigation lifecycle passed three independent cold Unity processes;
- each cold lifecycle run produced the same entrance multiplier (`0.752865`) at startup, after settling, and after the room round trip;
- the ChapterManager dialogue-binding gate produced two consecutive clean full-suite reruns after one transient full-run GameView zoom assertion; no files changed between those three runs, and both reruns restored the exact `0.752865` entrance multiplier;
- Gameplay scene hashing confirmed that batch validation did not rewrite the reviewed scene;
- all 147 C# scripts have `.meta` files and unique GUIDs.

## Validation still requiring human/golden review

- visual confirmation of the Butler-to-painted-entrance-door proportion in the interactive Game view;
- complete manual Chapter 1 and Chapter 2 golden-path smoke tests;
- dialogue/subtitle/voice, modal input, ambience, camera-shake and lighting visual/audio checks;
- visual confirmation that the Drawing Room tea table occludes the Butler/guests correctly and retains the accepted no-walk footprint;
- visual confirmation that the Drawing Room purple armchair occludes the Butler/guests correctly and retains the accepted no-walk footprint;
- visual confirmation that the Drawing Room purple sofa occludes the Butler/guests correctly and retains the accepted no-walk footprint;
- a player build and save/load trace after `SaveService` exists.

## Compatibility adapters still present

The following remain intentionally because their replacements have not yet passed behavioral migration and deletion proof:

- runtime manager/bootstrap creation paths;
- current room/navigation implementation and parallel navigation data;
- legacy and point-click movement paths;
- duplicate actor presentation/scale/sort writers;
- chapter god controllers;
- feature-created UI;
- static/global audio and session state;
- runtime/editor lighting mutation paths.

## Next approved phase

1. Prune the redundant runtime Drawing Room exit target, factory/remover, wrapper, enum role, and private sprite cache without serializing a replacement; retain and render-test the canonical room-change completion handoff.

Do not begin bulk deletion until those gates pass.
