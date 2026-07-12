# Automated passage compatibility-template certification

## Scope

The Grand Entrance Hall <-> Drawing Room round trip is the automatically certified template for moving a legacy `DoorTriggerNavigation` pair onto canonical room/passage data without changing approved interaction behavior. Human doorway, camera, and room-visibility review remains pending.

This is an **automated canonical compatibility-seam certification**, not the final navigation architecture. The existing trigger still owns input, proximity, prompt/cursor, movement subscription, and navigation audio. `RoomNavigationManager` still owns legacy room activation, camera integration, ambience refresh, and compatibility discovery. `InteractionRouter`, `PassageInteraction`, `RoomViewService`, and final camera ownership remain later transfers.

The exact Gameplay backlog and migration order live in [RemainingRouteInventory.csv](RemainingRouteInventory.csv). The scene is authoritative; `doors.txt` and `DoorCameraSequence` are incompatible legacy/reference graphs and must not be used to regenerate hand-authored triggers.

## Frozen first-route manifest

| Item | Grand Entrance Hall -> Drawing Room | Drawing Room -> Grand Entrance Hall |
|---|---|---|
| Trigger component fileID | `109889178` | `2300000104` |
| Trigger owner | `DoorTrigger_GEH_DrawingRoom` | `DoorTrigger_DrawingRoom_GEH` |
| Legacy door ID | `GEH_Drawing_Room` | `DrawingRoom_GEH` |
| Passage component fileID | `4100000011` | `4100000012` |
| Passage definition GUID | `0344228bb90d4997818e13c84f0bcf63` | `50ae5112eed74cfda8588ff835b92516` |
| RoomView fileID | `4100000001` | `4100000002` |
| Approach | `(-7.75, -2.22)` | `(5.267176, -2.104616)` |
| Arrival | `(5.267176, -2.104616)` | `(-7.75, -2.22)` |

Both triggers bind navigation manager `1878886997`, Player Transform `81962843`, shared door AudioSource `2201000013`, and door catalog GUID `9a77542e25184fbc945d6a79f77007e7`. The two Passages are co-located with those triggers, point at their source RoomViews, link reciprocally in scene and data, and register once under GameRoot.

Frozen compatibility-seam hashes at commit `ba54f0e9cbcbf55f2204d6dd1ee23fa8a3d01239`:

- Gameplay: `4212944dbf7f00fd5ed39528ba953bab3ff997b1cbd3200ecc441633ba99fd87`;
- `DoorTriggerNavigation.cs`: `e34afa809b1a8351d9b586a6ca52b6c094d763f7558323c4635aaf79d98aa63b`;
- `RoomNavigationManager.cs`: `ab27464f4b37fea45809876ead1af3a7cd5909b4bb7387086a78cefa8f2375ea`;
- `Passage.cs`: `21b86299bc689332bd854c11b048582bc274988927cfbc6834c7f75bf5f7c4cf`;
- `INavigationService.cs`: `d7171f63fa9bdbf79644c4c7c20f9f5cc7347370f34e183c4d4d2bf00d3954ca`.

## Reusable pair gate

Every ordinary reciprocal pair must pass these gates in order. A slice stops at the first failure.

1. **Clean checkpoint** — record branch and starting commit; require an empty worktree; declare the one pair, allowed files, and whether either RoomDefinition/RoomView is missing.
2. **Characterize first** — run both directions from far and near positions through the actual trigger path. Freeze trigger IDs/rectangles, approach/arrival samples, callback/audio/room-event order, cursor/prompt profile, camera behavior, room activation, and any story consumer.
3. **Canonical room data** — add at most the newly reached room's stable RoomDefinition and passive RoomView; include every exact legacy alias; register the RoomDefinition once in `GameDatabase` and the RoomView once in GameRoot. Do not add an activation writer.
4. **Reciprocal passage data** — add exactly two directed PassageDefinitions with stable IDs, swapped room endpoints, reciprocal data links, correct kind/prompt, and the exact existing legacy door IDs.
5. **Scene bindings** — add exactly two Passage components on the existing trigger owners; bind definitions, source RoomViews, reciprocal scene links, and GameRoot registration. Do not move or resize the owners.
6. **Shared doorway points** — author one finite logical point per room side so outbound approach equals reverse arrival and reverse approach equals outbound arrival. Prove exact collision/walkability, actual path reachability, source independence, and the existing activation envelope at all four rendered aspects before production consumption.
7. **Direct dependencies** — bind only the pair's exact navigation manager, Player Transform, shared audio source, correct catalog, and Passage. Do not introduce discovery, a service locator, a second state owner, or runtime repair.
8. **Caller and service ownership** — require fail-closed graph/anchor validation, exact approach through the existing movement subscription, exactly one room assignment/event, and exact arrival. Preserve near synchronous behavior and every prompt/cursor/audio/camera/story side effect.
9. **Compatibility proof** — temporarily null both Passage edges and complete far and near round trips in both directions. The unchanged sampler, Inspector traversal, audio/event sequence, sampled arrivals, and cleanup must still pass. Freeze relevant legacy method-body hashes.
10. **Serialization gate** — preserve all prior YAML documents/order/SceneRoots except the explicitly reviewed new component documents and reference-list edits. Require no missing script, no unrelated scene/prefab/data mutation, no existing `.meta` or GUID change, and idempotent Unity reimport.
11. **Repository gate** — run diff-check, architecture guard, generated audit, GUID/meta scan, serialized-reference scan, Y-axis/set-piece audit, the manifest fixture, focused pair tests, architecture foundation, rendered lifecycle, and the full failure-name comparison.
12. **Commit and advance** — update the inventory row(s), report, ledger, and evidence; commit the single passing pair. Start the next characterization only from that clean commit.

### TL;DR pair card

```text
CHARACTERIZE BOTH DIRECTIONS
  -> ADD/REGISTER AT MOST ONE NEW ROOM + VIEW
  -> ADD 2 RECIPROCAL DEFINITIONS + 2 CO-LOCATED PASSAGES
  -> CERTIFY 2 SHARED ROOM-SIDE ANCHORS
  -> BIND ONLY THIS PAIR'S DIRECT EDGES
  -> PROVE CANONICAL FAR/NEAR + NULL-PASSAGE FAR/NEAR
  -> CHECK YAML/META/GUID/REFS + FULL FAILURE SET
  -> COMMIT ONE PAIR
```

The manifest supports the reversible intermediate statuses `queued`, `characterized`, `data-authored`, `view-bound`, `passage-bound`, `dependencies-bound`, `caller-bound`, `arrival-owned`, `approach-owned`, and `complete`, plus the two blocked topology statuses. Passage fields, direct dependencies, and the canonical caller are required only from their corresponding stage onward, so each intermediate commit can remain green. The staged certification contract requires `passage-bound` to include finite approach/arrival values; those values remain passive validation data until the separately gated `arrival-owned` and `approach-owned` stages consume and certify them, when `CanTraverse` also enforces finiteness. Partner IDs are the globally derived reverse-endpoint candidates, not merely every member of a connectivity cluster. The CSV intentionally forbids commas in field values; its 19-column fail-fast parser rejects malformed rows.

## Backlog classification

Gameplay contains 45 trigger owners across 23 connectivity groups:

- 2 triggers in the automatically certified GEH/Drawing template;
- 2 Drawing/Music triggers in 1 arrival-owned reciprocal pair;
- 36 triggers in 18 uniquely reciprocal queued pairs;
- 2 one-way triggers blocked by missing reverse owners;
- 3 GEH/Upper Gallery stair triggers blocked by a two-outbound/one-return shape that the strict one-to-one reverse contract cannot represent.

There are 19 room roots. GEH and Drawing Room are canonical; the migration order introduces the other 17 exactly once before closing the Nursery/Blue Bedroom cycle. All 45 trigger owners already descend from the matching source room root.

Special profiles are part of behavior, not cleanup noise:

- bottom-edge/no-proximity: components `1858342503`, `70736571`, and `2300000074`;
- runtime-inferred stairway with serialized Door/Auto values: `106972347`, `2300000069`, `2300000189`, and `2300000194`;
- the reverse Upper Sitting Hall -> Side Stair Mudroom trigger currently remains a Door, so its asymmetry must be characterized before any intentional correction.

The blocked one-way triggers are `1615236111` (GEH Rear -> Library) and `2300000159` (Service Corridor -> Billiard Room). The parallel stair cluster is `106972347`, `2300000069`, and `2300000194`. These five stay on the compatibility path until a separate modeled decision passes characterization; they must never be forced into fabricated reciprocal links.

## Current automated evidence

- `PassageMigrationCertificationTests`: manifest/template/topology gate;
- canonical/static/foundation safety: `23/23` before this certification fixture;
- rendered lifecycle: `4/4`;
- full pre-certification suite: `249` discovered / `203` passed / the same `46` known failures, failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`;
- certification fixture `3/3`; combined manifest/static/contract/foundation safety `26/26`; rendered lifecycle `4/4`; full certification suite `252` discovered / `206` passed / the same `46` known failures with the same failure-name hash;
- architecture inventory: 112 runtime files / 48,741 lines, 48 direct `MonoBehaviour` declarations, unchanged smell totals;
- Y-axis/set-piece audit: zero hard errors and 38 tracked design-required findings;
- Gameplay contains 6,011 unchanged YAML documents. Production serialized references are unchanged; the generated script-reference inventory adds only `PassageMigrationCertificationTests.cs` with zero serialized instances, while all prior 154 rows remain unchanged.

The first use of this template completed its characterization gate for group `01`, Drawing Room <-> Music Room, before data authoring. At `1366x768`, the legacy reciprocal room-side samples are Drawing `(-7.106010, -1.508934)` and Music `(-7.737432, -3.180156)`; far/near traversal, event/audio order, camera/background/active-stage ownership, prompt/cursor cleanup, and the Chapter 2 guest-panic left-exit identity all pass without a production or serialized-content change. The four-aspect evidence remains explicitly legacy and viewport-sensitive: `1440x1080` yields Drawing/Music `(-6.152483, -1.306456)` / `(-6.699176, -2.753424)`, `1920x1080` yields `(-7.104277, -1.508566)` / `(-7.735544, -3.179381)`, and `2560x1080` yields forward approach `(-8.188315, -1.742414)`, Music arrival/reverse approach `(-8.932235, -3.671232)`, and reverse arrival `(-8.188308, -1.741942)`. Both rendered aspect probes explicitly synchronize 2D physics after GameView layout changes so their PolygonCollider queries remain deterministic. At that characterization checkpoint, no RoomDefinition, PassageDefinition, RoomView, Passage component, direct edge, canonical caller, or authored anchor existed for group `01`.

Group `01` characterization gates: manifest fixture `4/4`, combined manifest/static/contract/foundation safety `27/27`, rendered lifecycle `5/5`, and full suite `254` discovered / `208` passed / the same `46` known failures with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture and serialized-reference inventories remain unchanged; the Y-axis/set-piece audit remains zero hard errors / 38 tracked findings.

At the completed Group `01` `data-authored` checkpoint, Music Room definition GUID `c0f34d74a30db58bb2b87b6ec316120b` gained stable ID `room.music-room`, alias/display `Music Room`, background GUID `028084782cdcf3d4ab3b596624c8b7c5`, and a null profile. Passage-definition GUIDs `3167361ca4c671298c0e84f43320619b` and `01544de8f55723585d60e5c0915345fd` gained the reciprocal Drawing/Music stable IDs and legacy door IDs as Door / `Open Door` data. `GameDatabase` contained the prior four followed once by these three, for seven definitions; focused canonical/manifest gates passed `9/9`. The scene and every prefab/runtime script/existing meta/GUID remained unchanged, with no Music RoomView, Passage component, direct dependency, caller cutover, or authored anchor yet.

Group `01` data gates: canonical-data plus manifest `9/9`, combined safety `27/27`, rendered lifecycle `5/5`, and full suite `254` discovered / `208` passed / the same `46` known failures with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture/Y-axis totals remain unchanged; serialized references advance only to three RoomDefinition data assets and four PassageDefinition data assets.

At the completed Group `01` `view-bound` checkpoint, Music root `354156755` gained RoomView `4100000003`, bound to definition `c0f34d74a30db58bb2b87b6ec316120b` and legacy content `2102000001`; GameRoot registered it once after the existing two views. Drawing view `4100000002` remained the forward source. That checkpoint's scene diff was exactly 16 added YAML lines: one component-list reference in the existing Music GameObject document, one `sceneBehaviours` reference in the existing GameRoot document, and one new 14-line RoomView document. No document was removed, all pre-existing document order was preserved, and there was no prefab/data/runtime/meta/trigger mutation; Gameplay reached `6,012` documents / 3 RoomViews / 2 Passages. Focused canonical/manifest gates passed `9/9`, combined safety passed `27/27`, rendered lifecycle passed `5/5`, and the full suite remained `254` discovered / `208` passed / the same `46` known failures with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture remained 112 runtime files / 48,741 lines / 48 direct `MonoBehaviour` declarations; Y-axis validation remained zero hard errors / 38 tracked findings; the 155-row serialized-reference report changed only RoomView scene instances from two to three.

At the completed Group `01` `passage-bound` checkpoint, Drawing owner GameObject `2300000095` gained Passage `4100000013` with definition GUID `3167361ca4c671298c0e84f43320619b`, source RoomView `4100000002`, reverse `4100000014`, approach `(-7.106010, -1.508934)`, and arrival `(-7.737432, -3.180156)`. Music owner GameObject `2300000085` gained Passage `4100000014` with definition GUID `01544de8f55723585d60e5c0915345fd`, source RoomView `4100000003`, reverse `4100000013`, approach `(-7.737432, -3.180156)`, and arrival `(-7.106010, -1.508934)`. GameRoot's Passage order was exactly `4100000011`, `4100000012`, `4100000013`, `4100000014`; the reciprocal values were passive certification data.

The passage-only scene delta is exactly 42 added YAML lines: two GameRoot list references, one component-list reference on each existing trigger-owner GameObject, and two new 19-line Passage documents. Gameplay advances `6,012` -> `6,014` documents, remains at 3 RoomViews, and advances 2 -> 4 Passages. No document is removed, all prior relative order is preserved, both trigger component documents retain null direct dependencies and an absent `canonicalPassage`, and runtime scripts, prefabs, data assets, existing `.meta` files/GUIDs, and unrelated scene content remain unchanged. Focused canonical/manifest gates pass `9/9`, combined safety passes `27/27`, rendered lifecycle passes `5/5`, and the full suite remains `254` discovered / `208` passed / the same `46` known failures with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture remains 112 runtime files / 48,741 lines / 48 direct `MonoBehaviour` declarations; Y-axis validation remains zero hard errors / 38 tracked findings; and the 155-row serialized-reference report retains RoomView at 3 while Passage advances 2 -> 4.

At the completed Group `01` `dependencies-bound` checkpoint, trigger documents `2300000099` and `2300000089` each replaced the same four prior null fields with navigation manager `1878886997`, Player Transform `81962843`, shared door AudioSource `2201000013`, and door catalog `{fileID: 11400000, guid: 9a77542e25184fbc945d6a79f77007e7, type: 2}`. Their stairway catalogs remained `{fileID: 0}` and `canonicalPassage` remained absent. This was exactly eight removed null-field lines plus eight replacement reference lines, with no added/removed/reordered YAML document; Gameplay remained `6,014` documents / 3 RoomViews / 4 Passages. All owner/component topology, both new Passages and every definition/source/reverse/anchor field, GameRoot contents/order, and all prior document order remained unchanged. A separate reversible byte audit nulled only those four fields per trigger and exactly reproduced the passage-bound trigger hashes `d1ff8a7ca08b1b687975c34a6f3ec09511e756751452104d85c88d05f5e93934` and `bad9c5f7df3c5034e4d46bb9cfcf7f88a465f6248502c38ad6c452075b973280`. The inventory rows were `dependencies-bound`; focused canonical/manifest gates passed `9/9`, combined safety passed `27/27`, rendered lifecycle passed `5/5`, and the full suite remained `254` discovered / `208` passed / the same `46` known failures with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture remained 112 runtime files / 48,741 lines / 48 direct `MonoBehaviour` declarations; Y-axis validation remained zero hard errors / 38 tracked findings; serialized script references remained unchanged at 155 rows.

Before caller binding, the reusable template now requires one temporary per-Passage `anchorMigrationStage`: `0` keeps legacy approach and arrival sampling, `1` owns authored arrival only, and `2` owns both authored coordinates. Complete Passages `4100000011`/`4100000012` serialize `2`; Drawing/Music Passages `4100000013`/`4100000014` serialize `0`. This single enum makes an invalid approach-before-arrival combination unrepresentable, while configuration and traversal reject any reciprocal-stage mismatch. Mode 0 still traverses through canonical identity but reuses the existing legacy sampler and placement method; no parallel algorithm or state owner is permitted. The preparation delta is four added scalar lines in four existing Passage documents, with `6,014` documents / 3 RoomViews / 4 Passages and every prior topology/reference/value unchanged. A rendered mismatch plus caller/poison proof freezes legacy behavior in both directions and restores all in-memory mutations and subscriptions in `finally`. Gates pass focused `9/9`, safety `27/27`, lifecycle `5/5`, and full suite `254` discovered / `208` passed / the same `46` known failures with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture is 112 runtime files / 48,787 lines / 48 direct `MonoBehaviour` declarations, serialized references remain 155 rows, and Y-axis remains zero hard errors / 38 tracked findings.

After every inventory row with a Passage reaches `complete` and every scene Passage is stage `2`, certify that invariant, make authored arrival/approach unconditional, rerun all gates, then delete `PassageAnchorMigrationStage`, its serialized field/properties, the two conditionals, and all YAML scalars. The rollout seam is evidence-bearing temporary code, not target architecture.

At the completed Group `01` `caller-bound` checkpoint, trigger `2300000099` gained co-located Passage `4100000013`; trigger `2300000089` gained `4100000014`. This was exactly two added lines in two existing trigger documents, with Gameplay unchanged at `6,014` documents / 3 RoomViews / 4 Passages. All Passage stages remained `0`, all coordinates and direct dependencies remained exact, and no topology, GameRoot, runtime, asset, prefab, or meta changed. Four of 45 triggers then had canonical callers and 41 remained null. The real serialized callers passed the rendered poison-anchor far/near round trip in both directions, proving stage 0 preserved every frozen legacy outcome. Focused gates passed `9/9`, safety passed `27/27`, lifecycle passed `5/5`, and the full suite remained `254` discovered / `208` passed / the same `46` known failures with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture remained 112 runtime files / 48,787 lines / 48 direct `MonoBehaviour` declarations, serialized references remained 155 rows, and Y-axis remained zero hard errors / 38 tracked findings.

Group `01` has now advanced only to `arrival-owned`. The first stage-1 rendered gate correctly rejected the passive Drawing reference `(-7.106010, -1.508934)`: the six-decimal value copied from the legacy projection lay exactly on the Drawing `PlayerBoundary`, and both an explicit `Physics2D.SyncTransforms()` retry and a settled-layout query still rejected it. The legacy placement path had hidden that edge case through its clamped fallback. Before accepting authored ownership, both reciprocal room-side values were minimally moved to collision-safe, trigger-safe points: Drawing `(-7.16, -1.78)` and Music `(-7.94, -3.27)`. Passage `4100000013` therefore serializes approach/arrival references `(-7.16, -1.78)` / `(-7.94, -3.27)` and owns only its Music arrival; Passage `4100000014` serializes the reciprocal references and owns only its Drawing arrival. Both serialize stage `1` together. No caller, dependency, definition, topology, component, GameRoot entry, document ID/order, runtime script, prefab, asset, `.meta`, or GUID changed.

The byte-preserved legacy sampler remains authoritative for approach and still returns the original four-aspect values, while far and near traversal in both directions land exactly on the new authored arrivals. Both arrivals are collision-safe and inside their reciprocal `145`-pixel trigger envelopes at `1366x768`, `1440x1080`, `1920x1080`, and `2560x1080`; the widest aspect also passes at maximum room zoom `1.22`. Focused gates pass `9/9`, safety passes `27/27`, lifecycle passes `5/5`, and the full suite remains `254` discovered / `208` passed / the exact same `46` known failures with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture remains 112 runtime files / 48,787 lines / 48 direct `MonoBehaviour` declarations, serialized references remain 155 rows, and Y-axis validation remains zero hard errors / 38 tracked findings.

Before any stage-2 scene change, the separate stage-1 approach preflight certifies both still-non-authoritative approach references without changing production dispatch. At each of the four rendered aspects, Drawing `(-7.16, -1.78)` and Music `(-7.94, -3.27)` each pass the exact production reachable-path evaluator from two distinct resolved far starts; the widest aspect repeats both rooms and starts at maximum zoom `1.22`, for 20 total probes. Every query is exactly walkable, reachable, moving, and non-projected, returns the authored coordinate unchanged, and remains inside the real `145`-pixel trigger envelope. Direct canonical-candidate validation returns the future point, while live stage-1 traversal dispatch still returns a different legacy sample. Player position and idle state restore in `finally`, both Passage stages remain `AuthoredArrival`, and scene/runtime/prefab/asset/meta/inventory state is unchanged. Rendered lifecycle remains `5/5`; the full suite remains `254/208/46` with the same failure-name hash.

Next, the `approach-owned` slice may replace only `anchorMigrationStage: 1` with `anchorMigrationStage: 2` in Passage documents `4100000013` and `4100000014`, plus matching inventory/test/documentation updates. Keep both calibrated reciprocal coordinates exact and prove the production movement command reaches them from multiple far starts across every rendered aspect and maximum zoom. End at `approach-owned`, then use a separate no-scene-change certification/status slice to advance the pair to `complete`.

Human review still required: visually confirm that walking to and landing on both shared points places the Butler's feet at the intended painted doorway sides. Final target-route certification additionally requires the later interaction, room-view, and camera ownership transfers.
