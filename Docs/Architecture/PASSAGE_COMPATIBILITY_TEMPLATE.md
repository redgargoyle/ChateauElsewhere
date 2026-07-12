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

The manifest supports the reversible intermediate statuses `queued`, `characterized`, `data-authored`, `view-bound`, `passage-bound`, `dependencies-bound`, `caller-bound`, `arrival-owned`, `approach-owned`, and `complete`, plus the two blocked topology statuses. Passage fields, direct dependencies, and the canonical caller are required only from their corresponding stage onward, so each intermediate commit can remain green. Partner IDs are the globally derived reverse-endpoint candidates, not merely every member of a connectivity cluster. The CSV intentionally forbids commas in field values; its 19-column fail-fast parser rejects malformed rows.

## Backlog classification

Gameplay contains 45 trigger owners across 23 connectivity groups:

- 2 triggers in the automatically certified GEH/Drawing template;
- 2 Drawing/Music triggers in 1 characterized reciprocal pair;
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

The first use of this template has completed characterization only for group `01`, Drawing Room <-> Music Room. At `1366x768`, the legacy reciprocal room-side samples are Drawing `(-7.106010, -1.508934)` and Music `(-7.737432, -3.180156)`; far/near traversal, event/audio order, camera/background/active-stage ownership, prompt/cursor cleanup, and the Chapter 2 guest-panic left-exit identity all pass without a production or serialized-content change. The four-aspect evidence remains explicitly legacy and viewport-sensitive: `1440x1080` yields Drawing/Music `(-6.152483, -1.306456)` / `(-6.699176, -2.753424)`, `1920x1080` yields `(-7.104277, -1.508566)` / `(-7.735544, -3.179381)`, and `2560x1080` has produced valid forward candidates `(-8.188315, -1.742414)` and `(-8.212471, -1.426609)`. The reviewed envelope retains both instead of inventing exact determinism; recorded Music arrival/reverse is `(-8.932235, -3.671232)` and reverse-arrival evidence is `(-8.188308, -1.741942)`. No RoomDefinition, PassageDefinition, RoomView, Passage component, direct edge, canonical caller, or authored anchor exists for group `01` yet.

Group `01` characterization gates: manifest fixture `4/4`, combined manifest/static/contract/foundation safety `27/27`, rendered lifecycle `5/5`, and full suite `254` discovered / `208` passed / the same `46` known failures with failure-name SHA-256 `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`. Architecture and serialized-reference inventories remain unchanged; the Y-axis/set-piece audit remains zero hard errors / 38 tracked findings.

Human review still required: visually confirm that walking to and landing on both shared points places the Butler's feet at the intended painted doorway sides. Final target-route certification additionally requires the later interaction, room-view, and camera ownership transfers.
