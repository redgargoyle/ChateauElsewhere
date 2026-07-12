# Requirements-first migration plan

## Safety principle

This is not a one-shot rewrite. Each phase must leave a compiling, testable project and a separate commit. Existing scripts retain their Unity GUIDs until scene/prefab references have migrated.

## Phase 0 — Baseline and recovery point — completed in this patch

- Captured Unity version, C# inventory, line counts, direct `MonoBehaviour` count, dependency-repair patterns, and serialized script references.
- Added a static architecture debt ceiling.
- Added source-control guard automation.
- Created a prune log.

Exit condition: the uploaded project can always be reconstructed from the original zip or baseline commit.

## Phase 1 — Architecture foundations — completed in this patch

- Added `GameRoot`, `GameContext`, `ChateauBehaviour`, `GameServiceBase`, validation types, `StateMachine<TState>`, `DefinitionAssetBase`, `GameDatabase`, chapter/room/actor/UI bases, and `StoryBeatBase`.
- Rebased major existing service and chapter classes without changing script GUIDs or public APIs.
- Removed the global fallback search from `ChapterEventScheduler`; it now requires its explicit `ChapterClock` binding.
- Added foundation tests.
- Pruned two statically proven unused scripts.

Exit condition: project compiles in Unity and existing EditMode tests pass.

## Phase 2 — Serialize the composition root — completed

Run the Editor installer in Unity:

```text
Tools > Chateau > Architecture > Install or Refresh Gameplay GameRoot
```

The installer must:

- add exactly one `Chateau_GameRoot`;
- add the currently runtime-created `RoomNavigationManager` and `DoorPromptSequenceController` if absent;
- add `SubtitleService` and `DialogueSpeechService` if absent;
- create and assign `GameDatabase.asset`;
- serialize all current `GameServiceBase` components;
- save `Gameplay.unity` only if validation passes.

Exit condition:

- Gameplay scene has exactly one root and each required service;
- no missing scripts;
- EditMode tests pass;
- Chapter 1 and Chapter 2 smoke tests pass;
- scene changes are committed separately.

## Phase 3 — Remove runtime repair bootstraps — in progress

Only after Phase 2 passes:

- `RoomNavigationBootstrap` replaced by serialized GameRoot validation and removed;
- `ChapterManager` top-level manager-stack self-creation fallback removed; the separately gated Chapter 2 controller factory is also removed;
- exactly one inert `Chapter2Controller`, HUD, monster-stinger owner, panic owner, and guest-search owner serialized and explicitly wired; all five top-level Chapter 2 creation fallbacks are removed;
- the monster stinger validates and directly uses its serialized monster, Drawing Room anchors, navigation, and Image; its structural searches, child-visual repair, and primitive placeholder are removed while audio/sprite/overlay presentation remains separately gated;
- the inactive monster object owns its serialized looping violin source, Game-Sounds binding, and imported clip; host/resource/Editor/component fallbacks are removed;
- the monster object owns its serialized screen-space overlay Canvas at People order `10000`; the Get/Add component fallback is removed;
- the stinger owns the eight approved run sprites as an ordered serialized array from scene load; its resource-path loader and name-sort fallback are removed;
- the Entrance coat-hanger timing failure and late runtime repair are characterized before ownership changes: authored art/transform, generated action/collider/closet, duplicate pantry closet, repeated repair, coat retention, and room round-trip identities are frozen;
- the authored Entrance coat hanger owns the existing serialized closet identity plus one serialized scene action and exact trigger collider from scene load; the pantry placeholder no longer owns a duplicate closet, and all name-search/component/collider/anchor repair paths are removed;
- the now-unowned pantry `CoatCloset / Anchors / ApproachFront` placeholder subtree is pruned from Gameplay, and its dead prop-anchor/hierarchy-name lookup helpers are removed;
- Chapter 1's core-reference repair is characterized before removal: its exact clock, scheduler, camera, navigation, Player movement, and derived Butler root survive repeated resolution, room round trips, repeated Chapter 2 entry, and seven-PM staging;
- Chapter 1 directly serializes its clock, camera, navigation, and Player movement alongside its already-authored manager and scheduler; configuration validation requires the complete graph, all repeated discovery branches are removed, and the Butler root derives only from the Player movement edge;
- Chapter 1's caller-owned manager rebinding is removed; valid/null entry commands retain serialized manager `3301000004`, while a different manager is rejected before sequence or world mutation;
- the front-door interaction timing failure is characterized before ownership changes: authored trigger/action/collider identities and geometry stay intact, but startup creates and binds a second runtime trigger; repeated repair, room travel, and skips retain that duplicate without further growth;
- Chapter 1 directly serializes authored front-door action `1180734300`; startup, repeated repair, room travel, and skips now retain that sole action/trigger/collider graph while the obsolete fallback remains staged for its independent cleanup gate;
- the front-door alias search, runtime trigger/action/renderer/collider/UI factories, computed hitbox, and generic click-target reassignment branch are removed; one narrow method configures only the validated serialized action;
- Chapter 1's remaining immutable data lookups are characterized before migration: the exact footstep catalog, entrance placemark, Drawing Room door target, two RoomContentGroups, and ordered eight guest points retain their identities/transforms through repeated resolution, travel, and skips without runtime seat creation;
- Chapter 1 directly serializes that catalog/placemark/door-target/room-content/eight-point graph and validates ownership, room membership, uniqueness, and ordering while its lookup paths remain staged for cleanup;
- Chapter 1's footstep resource load, RoomAnchor/object-name scans, duplicate door-trigger scan, room-content scan, legacy three-seat fields, and generated-seat path are removed; the validated direct graph is the sole owner, and configuration rejects any Inspector reordering of the eight indexed guest-point anchors;
- Chapter 1's runtime-created doorbell graph is characterized before migration: the controller host, clock, single source, Game-Sounds volume binding, approved imported clip GUID/fileID, first-use load, and repeated-resolution identities are frozen;
- Chapter 1 directly serializes the characterized doorbell/source/Game-Sounds-binding/imported-clip graph on its existing host and validates the exact clock/owner/audio contract while the old fallback paths remain staged for cleanup;
- Chapter 1's doorbell owner/clock/source/binding discovery and factories, resource-path load, and generated-tone fallback are removed; initialization and ringing use only the validated serialized graph;
- the runtime-only Drawing Room exit click target is characterized before pruning: ChapterManager preparation creates one inactive transparent trigger, repeated initialization duplicates it while the room is inactive, every identity persists inert into Chapter 2, and its action only re-invokes completion paths already owned by room/state changes;
- the redundant Drawing Room exit target, inactive-object factory/remover, wrapper, enum role, serialized toggle, and private sprite cache are pruned without replacement; the authored passage event remains the sole room transition and drives the normal completion-ready Chapter 2 handoff;
- the runtime-only `GrandfatherClockInteraction` graph is characterized before retirement: the actual Entrance placeholder host, injected component/source/Atmosphere binding, two-second generated clip, disabled modal, and stable identities are frozen through repeated initialization, room travel, and Chapter 2; the serialized room-aware ambience and Chapter 2 strike/presentation owners remain distinct;
- `GrandfatherClockInteraction`, its script GUID, unordered host discovery, generated tick/modal factories, trailing action role, and unused Chapter 1 HUD/time pass-through are retired; the authored Entrance/Drawing clock placeholders and anchors remain component-free world data, while canonical room ambience and Chapter 2 clock owners are unchanged;
- the runtime-created `ChapterTimeSettingsUI` graph is characterized before changing ownership: its Chapter 1 host, authoritative clock, root Canvas, TMP font/material, text/shadow layout, authored EventSystem non-mutation, repeated initialization, room travel, Chapter 2, and seven-o'clock refresh identities are frozen; the readout is required, while its old editable settings panel is absent and superseded by `RuntimeSettingsMenu`;
- the same script GUID now identifies one global `Chateau.UI.GameTimeHUD` under GameRoot: its exact Canvas/TMP/Shadow graph and authoritative clock are serialized and architecture-validated from scene load, while the old Chapter 1 and HUD repair branches remain staged only for the immediately following cleanup gate;
- Chapter 1's temporary HUD field/YAML edge and every HUD search, Canvas/text/Shadow/EventSystem factory, legacy-panel repair, and compatibility initializer are removed; the four-edge global owner is the only readout path and the debt guard permits zero repair code in its target file;
- `ChapterIntroUI`'s first-use repair is characterized before authoring: the ChapterManager host, dedicated root Canvas, stretched overlay, full-screen black Image, centered Liberation Sans TMP title, exact two fallback warnings, existing EventSystem, repeated `EnsureUI`, Chapter 2 reuse, and zero object/component growth are frozen;
- that exact Chapter Intro Canvas/overlay/Image/TMP graph is now serialized beneath GameRoot and bound directly to existing owner `3301000003`; the compatibility repair code remains staged for one commit and is proven to create, attach, reparent, or warn about nothing;
- the Chapter Intro Canvas is finalized as a canonical scene-root Screen-Space Overlay Canvas with a stretched authored overlay child; all Canvas/view search, creation, component-add, reparent, layout mutation, fallback warnings/names, and safe-canvas coupling are removed while the four direct references and transition behavior remain exact;
- Guest Search owns the serialized GameRoot navigation service from scene load; its five lazy repair calls and global resolver are removed while room-change subscription stays idempotent;
- `Chapter2Controller` validates and directly uses its fourteen serialized stable dependencies; its monolithic `ResolveReferences` repair search is removed;
- Chapter 2 clock-strike playback owns a dedicated serialized child `AudioSource`, Game-Sounds `GameAudioSourceVolume`, and imported clip; its resource load, runtime tone generator, source factory, and binding factory are removed;
- every Chapter2Controller entry command accepts only its serialized ChapterManager (or a null convenience argument that keeps that owner); mismatched callers are rejected before any phase or world state changes;
- Chapter 1 binds the existing serialized guest-scale applier, which owns the serialized calibration and Butler source; both latent runtime owner factories are removed while per-guest participant creation remains deliberate;
- serialized dialogue/subtitle services, line bank, navigation edge, and Chapter 1 consumers are wired; both core-service `FindOrCreate` factories are removed while auxiliary views remain lazy until their own gate;
- one voice-playback owner, dedicated dialogue AudioSource, and speaking-indicator owner are serialized with explicit assets/navigation; both root factories are removed while lazy subtitle/bubble child-view creation remains deliberate;
- DialogueSpeechService owns the exact serialized Butler input edge, and the primary voice AudioSource owns one serialized Dialogue-channel volume component; real cataloged playback/input behavior is lifecycle-gated while transient overlap playback remains dynamic;
- blocking dialogue uses one token-safe service-owned input lease; cancellation releases before transition state is applied, stale routines cannot release a replacement lease, and shutdown/disable force a safe release;
- SubtitleService and GuestVoiceLinePlayback use validated serialized line-bank/catalog/navigation/source dependencies without resource or scene-search repair; lazy subtitle presentation and transient overlap playback remain separately gated;
- SubtitleService is bound to the shared speaking-indicator owner; redundant static cleanup searches are removed from dialogue, subtitle, and Chapter 2 paths;
- ChapterManager owns serialized dialogue/subtitle services and one direct debug-transition cleanup command; settings and teleports delegate to it, and the final static voice-stop/global subtitle searches are removed;
- ChapterManager owns the exact serialized Player input and Chapter 2 controller; its player/controller/debug-canvas repair searches are removed, the public Player root is derived from that input, and legacy movement components are disabled only on the authored Player instance;
- the inert Chapter 1 HUD owner is serialized on the Chapter 1 controller; its lookup/factory/flag are removed while lazy canvas/text presentation remains deliberate;
- RuntimeSettingsMenu and its correctly scaled canvas are serialized under GameRoot with explicit navigation/chapter/clock/music; root/canvas factories are removed while nested controls remain lazy and owner-scoped;
- exploration music has one serialized channel-volume owner at its authored `0.125` base volume; RuntimeSettings no longer creates/reconfigures that component and the prior zero-volume migration regression is lifecycle-gated;
- RuntimeSettings validates and directly uses its serialized navigation/chapter/clock/music graph; external searches plus EventSystem/RectTransform repair are removed, while owner-scoped runtime controls remain intentionally dynamic;
- fireplace and clock ambience ownership is behaviorally frozen, then serialized under GameRoot with distinct sources, explicit catalogs/navigation, and a fireplace-only high-pass filter; their root, resource, and component-repair factories are removed;
- remove `DialogueSpeechService.FindOrCreate` and `SubtitleService.FindOrCreate` call sites after callers receive serialized/service references;
- replace `UrpPostProcessingBootstrap` with a serialized render rig;
- replace runtime clock-hand attachment with an authored `ClockView` reference.
- `GameClockHandsDisplay` is deleted after complete reference/resource proof and a zero-instance lifecycle; the guard and static test require it to remain pruned.

Exit condition: required managers are never created via `new GameObject` or `AddComponent` at runtime.

## Phase 4 — Canonical rooms, navigation, and set-piece props

Build one complete route through the target architecture before touching every door:

```text
InteractionRouter -> PassageInteraction -> NavigationService
  -> RoomViewService -> CameraService -> player arrival
```

Create stable `RoomDefinition`, `PassageDefinition`, `RoomView`, `Passage`, and arrival-anchor data. Migrate one round trip, test it, then migrate the remaining routes.

The first route characterization is complete. Before canonical code or scene binding, the tests now freeze:

- exact Entrance/Drawing trigger IDs, parents, rectangles, route strings, script GUID, and current null repair edges;
- one room-change event per direction, in transition-before-arrival order;
- neutral authored-view logical anchors at `1366x768`: representative far forward approach `(-7.576081, -1.986423)` / Drawing arrival `(5.267176, -2.104616)`, and representative far reverse approach `(5.280546, -2.015396)` / Entrance arrival `(-7.703568, -2.000136)`;
- actual far activation ordering (`ArrivedAtDestination` -> `MovementStopped` -> audio -> room event -> destination warp), synchronous near activation, pending-subscription cleanup, and the `145`-pixel proximity boundary;
- viewport envelopes at `1366x768`, `1440x1080`, `1920x1080`, and `2560x1080`, plus proof that left/center/right click positions converge for this pair while aspect ratio and source position still change the legacy logical result;
- camera active-room/background ownership, prompt/cursor release, Chapter 1 continuity, object/component counts, and the `0.752865` Butler presentation multiplier;
- shared door-audio first-use binding/reuse; and
- the current `RoomContentGroup` force-enable defect, which must be intentionally removed when `RoomView` assumes visibility ownership.

The route then migrates through separately reversible commits: pure contracts; definition assets and GameDatabase registration; passive room/passage scene bindings; direct serialized dependencies; façade-backed canonical traversal; authored arrival anchors; authored approach anchors; and final route certification. `RoomNavigationManager` remains the sole current-room owner until a later explicit owner cutover.

Pure-contract status: complete. The canonical definition/view/passage/interface types are validation-only and have zero serialized instances. No service implementation, room activation command, scene binding, or legacy caller changed in that gate.

Canonical-data status: complete. The two room definitions and two reciprocal directed passage definitions are exact, directly registered in `GameDatabase`, Unity-imported, and behaviorally inert; that data gate passed before any scene binding.

Passive-room-view status: complete. The Entrance and Drawing roots each carry one definition-backed RoomView registered for validation, but no view can change visibility and no canonical Passage exists in the scene. The legacy manager remains the sole activation writer.

Approach/viewport characterization status: complete. Rendered tests own and restore a real Game-view size, neutralize synthetic batch edge-pan, exercise both far walks and both near activations, and quantify the current viewport/source-position dependence. The old `50x29` batch result is no longer treated as an authored anchor. Next, serialize exactly two reciprocal passive Passage bindings with the neutral `1366x768` values while leaving legacy interaction/traversal untouched.

Passive-passage status: complete. The two existing trigger owners now each carry one definition-backed, reciprocal, GameRoot-registered Passage with the neutral reference approach/arrival data. Passage has no lifecycle or command and is not called by any legacy or canonical service. Next, bind the two legacy trigger owners' stable navigation, Player, shared door-audio, and catalog dependencies directly in one separately gated compatibility slice; do not implement `INavigationService` or change traversal yet.

Direct-dependency status: complete. Only the GEH/Drawing reciprocal trigger pair now directly references the existing `RoomNavigationManager`, exact live Player Transform, shared `Audio_DoorOpen` source, and approved `DoorOpenSoundCatalog`; the other 43 scene trigger documents retain all four null compatibility edges. The Player prefab instance required one type-correct stripped Transform proxy, so the proven minimum scene delta is two changed trigger documents plus one added proxy document. Every legacy resolver remains for unmigrated routes. Next, make the existing `RoomNavigationManager` implement `INavigationService` as a no-new-state compatibility façade, prove it delegates to the same inspector-owned route path, and do not cut over either trigger caller in that implementation-only slice.

Navigation-façade implementation status: complete. The existing `RoomNavigationManager` is the sole `INavigationService`; it derives `CurrentRoomDefinition` on demand from the one existing `currentRoom` string and registered `GameDatabase` definitions, validates a scene Passage without mutation, and delegates `TryTraverse` exactly once to `MoveThroughInspectorDoor`. It adds no field, cache, event, object, serialized edge, discovery path, audio behavior, activation writer, or Player-placement algorithm. Both `DoorTriggerNavigation` callers remain byte-identical and still call the legacy method directly. Next, bind only the two first-route triggers to their co-located Passages and cut only those two callers through the façade, retaining the exact legacy fallback for every unmigrated trigger.

Room-local object cutouts are first-class props:

```text
RoomDefinition -> RoomView -> SetPieceView -> RoomDepthResolver
                         \-> RoomNavigationGeometry -> ActorMotor
```

- `RoomDepthResolver` and the static `SetPieceView` foundation are unit-gated before any scene binding;
- `purple_armchair_back` is the completed second vertical slice: `SetPieceView` owns stable order `8289`, the original art/transform/component identities remain, and the accepted Gameplay blocker polygon is collision-only;
- `purple_sofa` is the completed third vertical slice: `SetPieceView` owns stable order `5385`, the original art/transform/component identities remain, and the accepted Gameplay blocker polygon is collision-only;
- `SetPieceView` owns the visual cutout, a room-local occlusion anchor, and a sorting offset.
- `RoomNavigationGeometry` owns the room boundary and authored no-walk footprints.
- couches, desks, beds, toys, chairs, tables, and similar scenery use shared definitions/views rather than object-specific managers;
- static set pieces do not recompute sorting every frame;
- sorting never depends on world-space `Renderer.bounds` or `Collider.bounds`;
- accepted collider shapes are preserved and registered, not regenerated;
- the Drawing Room's duplicate prop-sort writers migrate one prop at a time;
- the first tea-table baseline exposed the duplicate-writer defect, then the table migrated to `Props / Set Pieces`: `SetPieceView` owns stable order `6627`, the accepted blocker polygon remains, and collision sorting is disabled for that prop;
- the second purple-armchair baseline exposed the same defect, then the armchair migrated in place: `SetPieceView` owns stable order `8289`, the accepted lower-seat polygon remains, and collision sorting is disabled for that prop;
- the third purple-sofa baseline exposed bounds-derived order `1225` against intended room-local order `5385`, then the sofa migrated beneath `Props / Set Pieces` with its accepted seating polygon unchanged and collision sorting disabled;
- Dining Room seat occlusion migrates only after `ActorPresenter` exists and uses serialized `SeatOcclusionSlot` data.

Migrate navigation geometry and set-piece views one room at a time. The Grand Entrance Hall/Drawing Room route remains the first passage slice; Dining Room is the final set-piece slice because it depends on actor presentation.

Remove only after migration:

- `doors.txt` runtime path;
- `DoorCameraSequence` runtime path;
- `RoomVisualCatalog` runtime path;
- prompt/door paths that duplicate the canonical passage system.

Exit condition: one route graph, explicit reverse links, explicit parallel-stairway pairing, and PlayMode tests for every passage round trip.

Set-piece exit condition: one visual/sort writer per prop, one navigation-geometry owner per room, unchanged accepted collision footprints, stable occlusion across zoom/aspect ratios, and no room activation code that repairs descendant renderers.

## Phase 5 — Actors, movement, and presentation

Split `PointClickPlayerMovement` behind a temporary façade:

- `PlayerClickCommandSource`;
- shared `RoomStageMotor`;
- `ActorPresenter` as sole transform/scale/sort writer;
- `ActorAnimator`;
- shared actor footstep/voice emitters;
- `PlayerActorController` and `GuestActorController`;
- dedicated player and guest prefab variants.

Consolidate `ActorRoomState`, `RoomProjectedEntity`, guest scale components, `RoomPersonWalker2D`, `NPCWaypointMover`, and duplicate sorting paths only after behavior tests prove parity.

Exit condition: one motor and one presenter writer per actor; guests contain no player-input components.

## Phase 6 — Story flow and beat extraction

Make one `GameFlowService` the only chapter/beat owner. Extract one beat at a time from the large Chapter 1/2 controllers while preserving existing public entry points as façades.

Exit condition: chapter controllers read like the game script, Story Beats are testable, and there is one transition mechanism.

## Phase 7 — UI, dialogue, audio, camera, and lighting

- one serialized UI root and modal stack;
- one dialogue queue and subtitle/voice coordinator;
- one audio settings/ambience owner;
- one serialized render rig;
- editor-only lighting generation/bake;
- narrow runtime light views.

Exit condition: feature controllers create no canvases, cameras, audio managers, or lighting objects.

## Phase 8 — Final pruning and assembly boundaries

For every deletion, complete the prune-proof checklist. Then add namespaces and assembly definitions after dependencies are acyclic.

Exit condition:

- zero unjustified runtime classes;
- zero missing scripts;
- zero unowned state;
- zero permanent parallel systems;
- all critical game paths covered by tests;
- Hamza can redraw and explain the architecture from requirements.
