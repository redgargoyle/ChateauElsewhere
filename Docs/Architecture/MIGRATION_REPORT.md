# Architecture migration report

## Current phase

**Phase 4 vertical migration in progress; the first two set pieces are complete while Phase 3 compatibility retirement continues.**

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
- Removed `Chapter2Controller.ResolveReferences` and its dormant scene-wide repair searches. Chapter 2 now validates and uses its eleven serialized manager, navigation, clock, player, UI, feature, subtitle, and speech dependencies directly; only the separately gated clock-strike audio fallback remains dynamic.
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

## Current static result

| Metric | Baseline | Candidate | Delta |
|---|---:|---:|---:|
| Runtime C# files | 90 | 106 | +16 |
| Runtime C# lines | 49,902 | 49,716 | -186 |
| Direct `MonoBehaviour` declarations | 63 | 50 | -13 |
| `FindObject*`/`GameObject.Find` | 199 | 148 | -51 |
| `Resources.Load` | 27 | 23 | -4 |
| runtime `new GameObject` | 98 | 82 | -16 |
| runtime `AddComponent<T>` | 100 | 75 | -25 |
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
- the Chapter 2 dependency-cleanup static, regression, and lifecycle gates pass; all eleven serialized references resolve exactly once, `ResolveReferences` and its seven scene-wide searches are absent, and repeated entry/debug paths retain the same owners;
- the full EditMode discovery count is 238: 187 pass and the same 51 pre-existing baseline failures remain, with no new failed test names;
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

1. Characterize `purple_sofa` as the next candidate before migrating it; preserve its exact art/transform and Gameplay no-walk polygon.
2. Retire the already-serialized ChapterManager-to-Chapter2Controller lookup after a dedicated transition gate.
3. Continue one prop/owner at a time with exact art, transform, occlusion, and collision preservation.

Do not begin bulk deletion until those gates pass.
