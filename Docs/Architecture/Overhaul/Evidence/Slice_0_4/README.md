# Slice 0.4 visual-baseline evidence

## Provenance

- Branch: `refactor/final-architecture-overhaul`
- Slice `0.4a` starting and rollback commit:
  `bb64c7c38ec16b6aa35c8bdf8026a723bce20b9e`
- Unity executable:
  `/home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity`
- Unity version: `6000.4.10f1`, revision `feeafc12a938`
- Evidence date: `2026-07-13` (`America/Los_Angeles`)
- Rendering resolution: `1366x768`

## Characterization boundary

Slice `0.4a` adds one black-box PlayMode characterization and changes no
production or editor behavior. The test:

1. enters Gameplay through the real MainMenu and cursor chooser;
2. moves the Butler to the existing front-door approach;
3. waits for scale and sorting to settle and proves two later frames are
   identical;
4. stages Chapter 2 in the Drawing Room without starting random panic motion;
5. resolves all eight visible guests by stable actor identity; and
6. fingerprints each actor's authored/captured scale, effective local scale,
   room, and sorting order.

The fixture explicitly disables live mouse-edge pan, vertical pan, and wheel
zoom, then applies the fixed evidence look `horizontal=-0.55`, `vertical=-1`,
`fov=0.8`, `zoom=1.06` before every room sample. This matches the accepted
Entrance capture from all original cold starts and prevents the batch runner's
host mouse position or frame timing from sampling a room mid-pan. It does not
change any serialized or production setting.

The canonical fingerprint is committed in `cold_start_fingerprint.txt`. Its
SHA-256 is:

```text
34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96
```

This digest is asserted by the PlayMode test so later editor/runtime changes
cannot silently replace the accepted baseline with a merely consistent but
different scale.

## Ten independent cold starts

The focused method was run in ten separate Unity processes. Every process ran
display-backed, in batch mode, without `-quit` and without `-nographics`:

```text
Unity -batchmode -projectPath /home/hamzak/Desktop/ChataeuChantilly \
  -runTests -testPlatform PlayMode \
  -testFilter ArchitectureBaselinePlayModeTests.ColdStartScaleAndSortFingerprintMatchesApprovedBaseline \
  -testResults Logs/ArchitectureOverhaul/Slice-0.4/cold-start-10-certified/run-NN.xml \
  -logFile Logs/ArchitectureOverhaul/Slice-0.4/cold-start-10-certified/run-NN.log
```

`-nographics` is invalid for this rendered gate: the game intentionally applies
a native cursor during CameraManager update, and Unity cannot create that cursor
without a graphics display. The one rejected diagnostic launch failed inside
Unity/Xcursor before producing test XML; it was not counted.

Results:

- ten independent XML files, each exactly `1 total / 1 passed / 0 failed /
  0 skipped`;
- ten identical scale/sort fingerprints;
- ten identical measurements: Butler `114.417 px`, door `141.481 px`, ratio
  `0.808710`, Butler sort `1075`;
- presentation multiplier `0.7528645` in every run;
- eight unique Drawing Room guests in every run;
- the tracked status and binary diff digest remained exactly unchanged after
  every process (`eb7a9d08272905616c4588cb0a681f030ae4dadc6713be1b25475ef20d99c61f`
  during the gate).

The combined real PlayMode filter passes exactly `5/5/0/0`. The full EditMode
suite remains exactly `264 total / 218 passed / 46 known failed / 0 skipped`,
with unchanged failure-name SHA-256
`544759729ac446b3814a3f206021a23c64fd46cc9edc1e997b179affaa0f69f9`.
Architecture-tool tests pass `4/4`; the guard, audit, runtime ledger,
serialized-reference scan, and script-integrity scan all pass at the unchanged
production baselines.

The logical door-approach coordinate is asserted separately within `0.001` and
is not part of the scale/sort digest. This excludes irrelevant `1e-7` projection
noise observed when this test follows another PlayMode test in the same process.
Scale values are normalized to six decimal places (maximum quantization below
`0.000001`) to exclude irrelevant single-bit floating-point differences;
identity, room, state flags, and sorting orders remain exact in the digest.

The accepted approximately three-quarter visual is the already-reviewed
renderer comparison at the real door approach. It is intentionally preserved
at `0.808710`; this slice does not silently retune it to mathematical `0.75`.

The combined PlayMode gate retains the already-committed Slice `0.3`
panic-frame golden (`477.884,301.136`) with its `1 px` tolerance. Four final
display-backed observations fell within `x=477.127-477.599` and
`y=300.658-300.956`; all retained the exact `255.668 px` height and sorting
order `1620`. The fixture fixes the capture delta to `1/60` only while starting
panic so host frame timing cannot select a materially different movement step.
No production value or serialized setting changed.

The old Drawing Room blocker coordinate (`614.435,114.201`) was recorded one
frame after a room transition while edge-pan input was still advancing. Under
the fixed evidence look, its deterministic screen coordinate is
`654.474,135.569`; the collision projection and completed logical destination
remain exactly `-1.045052,-3.514679`. Slice `0.3` keeps the historical sample;
Slice `0.4` makes future golden runs repeatable.

## Serialized calibration preservation

`calibration_asset_hashes.csv` freezes the scene, Player prefab, the two current
room perspective profiles, and relevant script/meta files before the editor
write-safety sub-slice. Slice `0.4a` changes none of them.

The nineteen Butler endpoint rows and nineteen guest calibration rows still
live in their existing serialized compatibility components. Converting those
rows into the final actor presentation profiles is explicitly scheduled for
Phase `4` by the runtime ledger. Pulling that conversion into this evidence
slice would create a parallel owner, so `0.4` preserves the authored values and
prevents accidental writes first; the atomic `ActorPresenter`/profile transfer
remains a later characterized migration.
