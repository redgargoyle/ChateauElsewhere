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
| `Chapter2Controller.ResolveReferences` HUD creation branch | Chapter 2 startup could add `Chapter2InteractionHUD` at runtime | `Gameplay.unity` serializes and binds exactly one Chapter 2 HUD | The lifecycle test captures the HUD before Chapter 2 and proves repeated skips reuse it; the static guard bans `AddComponent<Chapter2InteractionHUD>`; serialized-reference scan and full Unity gate. |
| `Chapter2Controller.ResolveReferences` feature-owner creation branches | Chapter 2 startup added monster-stinger, guest-panic, and guest-search controllers at runtime | `Gameplay.unity` serializes and binds exactly one of each feature owner and GameRoot supplies their context | The lifecycle test captures all three inert instances before Chapter 2 and proves repeated skips reuse them; the static guard bans all three `AddComponent` calls; 22/22 scene-structure audit, serialized-reference scan, and full Unity gate. |
| Remaining `Chapter2Controller.ResolveReferences` dependency repairs | Chapter entry/debug paths repeatedly searched the scene or host object for eleven already serialized owners | `Gameplay.unity` binds the manager, navigation, intro UI, clock, Butler motor, HUD, three feature owners, subtitle service, and speech service; configuration validation rejects an incomplete graph | Static test verifies all serialized edges and bans the repair API/searches; focused regression/lifecycle gates pass; full suite discovers 237 tests with the exact baseline 51-failure name set. |
| Chapter 1 guest-scale owner factories | Chapter 1 or `GuestRoomScaleApplier.EnsureInScene` could create a replacement applier/calibration at runtime | Chapter 1 binds the existing applier, which owns the authored calibration and approved Butler source; only per-guest `GuestScaleParticipant` creation remains dynamic | Lifecycle test proves exact owner identity and Butler-source equality through room travel and repeated Chapter 2 skips; static guard bans both owner factories while affirming participant creation; full Unity gate. |
| `DialogueSpeechService.FindOrCreate` / `SubtitleService.FindOrCreate` | Callers could create unregistered, context-free replacements for core GameRoot services | Gameplay serializes both services and explicitly binds their service/line-bank/navigation/chapter edges | Lifecycle test proves both exact services are initialized/context-bound while auxiliary views stay lazy; static guard bans both factories/call sites; 6/6 scene audit and full Unity gate. |
| `GuestVoiceLinePlayback.FindOrCreate` / `SpeakingCharacterIndicator.FindOrCreate` | First dialogue could create replacement root owners at runtime | Gameplay serializes one voice owner with a dedicated AudioSource and one indicator owner, then binds both to the dialogue services | Lifecycle test proves both owners are inert at boot and reused across repeated speech; static guard bans root factories while affirming the lazy bubble child; 11/11 scene audit and full Unity gate. |
| Dialogue player lookup and primary voice component repair | Blocking speech globally selected a Butler movement component; first real voice line attached its channel-volume owner at runtime | DialogueSpeechService serializes Player component `81962842`; the primary source serializes binding `1878887003` and GuestVoice configures it directly | Real cataloged line gate first exposed nondeterministic player selection, then proves exact player/source/binding identity and both input restoration states; byte-preserving scene audit and full-suite failure set pass. Temporary overlap audio remains intentionally dynamic. |
| Redundant global dialogue cleanup lookups | Dialogue/Chapter 2/subtitle cleanup searched globally after already addressing their bound owners | DialogueSpeechService, SubtitleService, and Chapter2Controller call their serialized voice/indicator/service references directly | Repeated-speech lifecycle and room round trip pass; regression tests require direct owner cleanup; static guard bans `HideAnyCurrent` and `StopAnyCurrentSpeech`; full Unity gate. |
| `GuestVoiceLinePlayback.StopAnyCurrentLine` and debug-transition dialogue searches | Chapter skips/settings/teleports globally searched voice/subtitle owners | ChapterManager exposes one direct debug-transition command that cancels queued speech then clears SubtitleService; settings delegates before navigation | Lifecycle begins active speech and proves a repeated Chapter 2 skip stops it without replacing any owner; static guards ban the global API/searches; settings ordering regressions and full Unity gate. |
| Chapter 1 HUD owner repair | Chapter 1 searched globally and could add `Chapter1InteractionHUD` at runtime | Gameplay serializes one HUD on the Chapter 1 controller and binds it directly | Lifecycle proves exact HUD/canvas/status identity and sorting through room travel/repeated skips; static guard bans lookup/factory and the obsolete `createRuntimeHud` flag; full Unity gate. |
| `RuntimeSettingsMenu.FindOrCreate` / settings-canvas root factory | Navigation startup searched/created a settings owner and overlay canvas | Gameplay serializes one correctly scaled settings canvas/owner under GameRoot and binds all four external dependencies | Lifecycle opens/closes the menu and proves input/time-scale/identity across travel/skips; 7/7 scene audit; static guard bans root/canvas factories while affirming nested control construction; full Unity gate. |
| RuntimeSettings exploration-music volume-binding factory | Settings initialization/update added or reconfigured `GameAudioSourceVolume`; after source serialization its uncaptured base volume was clamped to zero | `Audio_ExplorationMusic` serializes one Music-channel binding with base volume `0.125`, and RuntimeSettings references/applies that exact owner | Desired-volume test first failed at `0.0`, then passed at `0.125`; lifecycle proves source/binding identity through reinitialization, travel, and skips; source-document hash and full-suite failure set remain exact. |
| RuntimeSettings external dependency/EventSystem/RectTransform repair | Settings startup and commands searched for serialized navigation, chapter, clock, music, EventSystem, and its own UI transform, with root/component creation fallbacks | RoomNavigationManager delegates validation of the menu's complete serialized graph; Gameplay owns one EventSystem and RectTransform; the menu directly uses those owners and constructs only its nested controls | Static guard proves zero RuntimeSettings scene searches and bans the repair APIs; scene-reset and MainMenu/round-trip lifecycle gates pass; full suite retains the exact baseline failure-name set. |
| Fireplace/clock ambience root and self-repair factories | Navigation and each ambience owner could globally search, load catalogs, create roots, and add missing audio/filter components | Gameplay serializes two dedicated owners with explicit navigation/catalog/source/filter references | Characterized audio contract and identity through travel; 9-document graft audit; 5/5 cleanup audit; static bans; lifecycle and full Unity gate. |
| Tea-table actor projection and blocker-owned sorting | `RoomProjectedEntity` and `ObjectMovementBlocker2D` both rewrote one static cutout every frame from incompatible depth spaces | One static `SetPieceView` owns room-local order `6627`; the same blocker/polygon owns navigation only | Before/after writer characterization; exact asset audit across scene/two prefabs; bound lifecycle identity; blocker no-op proof; full Unity gate. |

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
