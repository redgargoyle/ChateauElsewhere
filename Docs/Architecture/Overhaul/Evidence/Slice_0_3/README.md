# Slice 0.3 test-truth evidence

## Provenance

- Branch: `refactor/final-architecture-overhaul`
- Slice starting and rollback commit: `daa11ed6d746e1a74c82ddec985ed8286d2b6cf9`
- Baseline commit under EditMode test: `8ec90df46a0e2917a52356692e4938140ed66081`
- Unity executable: `/home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity`
- Unity version: `6000.4.10f1`, revision `feeafc12a938`
- Evidence date: `2026-07-13` (`America/Los_Angeles`)
- Tester: Codex automated architecture gate
- Rendering resolution: `1366x768`

## EditMode baseline

The untouched suite was run display-backed, without `-quit` and without
`-nographics`:

```text
Unity -batchmode -projectPath /home/hamzak/Desktop/ChataeuChantilly \
  -runTests -testPlatform EditMode \
  -testResults Logs/ArchitectureOverhaul/Slice-0.3/baseline-editmode-8ec90df4.xml \
  -logFile Logs/ArchitectureOverhaul/Slice-0.3/baseline-editmode-8ec90df4.log
```

- Result: `264 total / 218 passed / 46 failed / 0 skipped`
- XML UTC window: `2026-07-13 22:10:28Z` to `22:11:09Z`
- XML duration: `41.1952487s`
- XML bytes: `38,707,394`
- XML SHA-256: `f7704e44e083ed8aa6fcd8f7f8be561aea6475258e797e1d79e58433c666ca63`
- Sorted, LF-terminated failed-test-name SHA-256:
  `544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`
- Exact failure inventory: `editmode_baseline_failures.csv`

These 46 failures are recorded migration debt. They are not an accepted final
state and may not be hidden by raising a guard baseline.

After the PlayMode boundary was added, the full EditMode suite was rerun at
`Logs/ArchitectureOverhaul/Slice-0.3/editmode-final-daa11ed6.xml`. It
remained exactly `264/218/46/0` with the same failed-test-name digest. The
preserved `GameplayLifecycleCharacterizationTests` filter separately passed
`10/10`, and the focused foundation/room/passage filter passed `31/31`.

## Real PlayMode boundary

Unity Test Framework cannot compile frame-yielding `[UnityTest]` tests in the
predefined `Assembly-CSharp`: that assembly has NUnit during test discovery but
does not reference `UnityEngine.TestRunner`. Slice 0.3 explicitly requires a
real PlayMode assembly, so `Assets/Tests/PlayMode/Chateau.Tests.PlayMode.asmdef`
is the narrow test-only exception to the general "asmdefs later" rule. It does
not move production code, does not add a production dependency, and is omitted
from normal player builds by `optionalUnityReferences: ["TestAssemblies"]`.

Because Unity user assemblies cannot reference predefined `Assembly-CSharp`,
the fixture treats the current game as a black box: it loads real scenes, uses
Unity objects and UI controls, and inspects existing public compatibility
surfaces through reflection. Production code and serialization are unchanged.

Final focused command:

```text
Unity -batchmode -projectPath /home/hamzak/Desktop/ChataeuChantilly \
  -runTests -testPlatform PlayMode \
  -testFilter 'ArchitecturePlayModeDiscoveryTests;ArchitectureBaselinePlayModeTests' \
  -testResults Logs/ArchitectureOverhaul/Slice-0.3/playmode-after-movement-daa11ed6.xml \
  -logFile Logs/ArchitectureOverhaul/Slice-0.3/playmode-after-movement-daa11ed6.log
```

- Result: `4 total / 4 passed / 0 failed / 0 skipped`
- The discovery test proves a real PlayMode player-loop frame advances.
- The startup test uses the real MainMenu, cursor chooser and Gameplay scene.
- Exactly one initialized `GameRoot`, eight unique initialized services,
  Chapter 1's intro beat surrogate, `5:59 PM`, the Entrance room, dialogue and
  subtitle presentation, and the approved Butler presentation scale are proven.
- Entrance -> Drawing Room -> Entrance completes through canonical passages.
- Room-root visibility follows the current room.
- A click in the tea-table blocker is rejected at the exact point and projected
  to a reachable destination.
- The actual movement owner accepts that projected destination, moves the Butler
  over real frames, stops at the characterized logical point, and leaves the
  Butler's converted world point outside the real collider.
- Chapter 2 debug staging produces eight visible panic participants and an
  approved panic sprite frame.

## Known baseline defect

The room round-trip test deliberately disables the Drawing Room tea-table
renderer before leaving the room. Current `RoomContentGroup` behavior
force-enables it on re-entry. The passing assertion records that defect; it is
not approval of the behavior. The later room-visibility ownership slice must
change the production owner and reverse this assertion so hidden children stay
hidden.

## Fixed-resolution golden measurements

`golden_measurements.csv` records Entrance, Drawing Room, passage-transition and
Chapter 2 panic evidence from the final passing PlayMode log. The fixture now
asserts the stable structural values, logical arrival/projection positions,
scale, visible count, room, sprite, sorting order, and fixed-resolution screen
measurements. Coordinate values permit `0.5 px`; the panic participant center
permits `1.0 px` because its runtime motion begins in the capture frame.

The ten legacy editor-hosted `[UnityTest]` methods cannot be mechanically moved
into this assembly: they directly depend on `UnityEditor`, `AssetDatabase`, and
types in the predefined `Assembly-CSharp` assembly, which user assemblies cannot
reference. Slice 0.3 therefore establishes the required real PlayMode boundary
with black-box scene tests. A later Phase 8 test-structure slice may extract the
legacy fixtures after production assembly boundaries exist.

Regenerating `serialized_script_refs.csv` adds the two new test scripts and also
repairs stale pre-slice counts already present in the repository: the Billiard
Room raises `RoomDefinition`/`RoomView` references from `7` to `8`, and its two
canonical pantry passages raise `Passage`/`PassageDefinition` references from
`12` to `14`. No serialized asset was changed by this evidence refresh.

The visible entrance-door comparison is taken only after warping the Butler to
the existing front-door approach. Measuring the Butler at the foreground spawn
against the small door interaction renderer produces a meaningless ratio. At
the real approach, the observed pixel ratio is `0.808710`, while the serialized
presentation multiplier remains the approved `0.7528645`.
