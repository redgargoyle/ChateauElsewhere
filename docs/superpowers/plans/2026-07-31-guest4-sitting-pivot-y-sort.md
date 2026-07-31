# Guest 4 Sitting Pivot and Y-Sort Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permanently return Guest 4 to ordinary actor world-Y sorting while keeping only the green-chair foreground cutout over her, and import every sprite used by her sitting animation with a Bottom Center pivot.

**Architecture:** GuestIndex `3` will use the existing `ActivateFrontOccluderOnly` path so `WorldYSortSpriteRenderer` retains ownership of Guest 4 while the detached chair foreground remains the sole seated exception. The two unique sprite assets referenced by `CountessElowenDusk_Sitting.anim` will use Unity's Bottom Center importer preset, guarded by an animation-driven regression that discovers the frames from the clip rather than duplicating a filename list.

**Tech Stack:** Unity 6000.4.10f1, C#, Unity Test Framework EditMode/UnityTest, TextureImporter sprite metadata, SpriteRenderer world-Y sorting, Git.

## Global Constraints

- Do not create Windows, macOS, or Linux executables.
- Do not modify Guest 4's standing, walking, or panic sprites.
- Do not modify any other guest's pivot, scale, position, or sorting route.
- Do not add a fixed `SortingGroup` or fixed SpriteRenderer order to Guest 4.
- Do not modify the Drawing Room tea table, chair positions, room anchors, or other rooms.
- Preserve the user's existing `Assets/Scenes/Gameplay.unity` changes exactly.
- Preserve unrelated working-tree changes and stage only explicitly listed files.

---

### Task 1: Add Regressions for the Sitting Frames and Sorting Ownership

**Files:**
- Modify: `Assets/Editor/ObjectCollisionBoxRegressionTests.cs`
- Modify: `Assets/Editor/Chapter1GuestRoomVisibilityRegressionTests.cs:684-719`

**Interfaces:**
- Consumes: `CountessElowenDusk_Sitting.anim`, `TextureImporter.spriteAlignment`, `TextureImporter.spritePivot`, `WorldYSortSpriteRenderer`, and `DiningRoomSeatedGuestOcclusionException.ApplyOcclusionNow()`.
- Produces: regression contracts proving that the clip's complete sprite set is Bottom Center and that the seated exception leaves Guest 4's actor order unchanged.

- [ ] **Step 1: Add a clip-driven failing pivot regression**

Add this constant near the other asset paths in `ObjectCollisionBoxRegressionTests`:

```csharp
private const string Guest4SittingClipPath =
    "Assets/Animation/CountessElowenDusk/CountessElowenDusk_Sitting.anim";
```

Add this test near the other Drawing Room tests:

```csharp
[Test]
public void EveryGuest4SittingFrameUsesBottomCenterPivot()
{
    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Guest4SittingClipPath);
    Assert.That(clip, Is.Not.Null, $"Missing Guest 4 sitting clip at {Guest4SittingClipPath}.");

    EditorCurveBinding[] spriteBindings =
        AnimationUtility.GetObjectReferenceCurveBindings(clip);
    List<string> spritePaths = new List<string>();

    for (int bindingIndex = 0; bindingIndex < spriteBindings.Length; bindingIndex++)
    {
        EditorCurveBinding binding = spriteBindings[bindingIndex];

        if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite")
        {
            continue;
        }

        ObjectReferenceKeyframe[] keyframes =
            AnimationUtility.GetObjectReferenceCurve(clip, binding);

        for (int keyframeIndex = 0; keyframeIndex < keyframes.Length; keyframeIndex++)
        {
            Sprite sprite = keyframes[keyframeIndex].value as Sprite;
            string spritePath = sprite != null ? AssetDatabase.GetAssetPath(sprite) : string.Empty;

            if (!string.IsNullOrWhiteSpace(spritePath) && !spritePaths.Contains(spritePath))
            {
                spritePaths.Add(spritePath);
            }
        }
    }

    Assert.That(spritePaths, Has.Count.EqualTo(2),
        "Guest 4's sitting clip should expose both unique sitting frames to the pivot audit.");

    for (int i = 0; i < spritePaths.Count; i++)
    {
        TextureImporter importer = AssetImporter.GetAtPath(spritePaths[i]) as TextureImporter;
        Assert.That(importer, Is.Not.Null, $"Missing TextureImporter for {spritePaths[i]}.");
        Assert.That(importer.spriteAlignment,
            Is.EqualTo((int)SpriteAlignment.BottomCenter),
            $"{spritePaths[i]} must use Unity's Bottom Center alignment.");
        Assert.That(importer.spritePivot.x, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(importer.spritePivot.y, Is.EqualTo(0f).Within(0.0001f),
            $"{spritePaths[i]} must keep its pivot on the bottom edge.");
    }
}
```

- [ ] **Step 2: Change the controller source contract to require foreground-only ownership**

In `DrawingRoomUsesContinuousYSortingWithOnlySeatedOverrides`, retain the GuestIndex `7` armrest assertions and replace the old global prohibition on `ActivateFrontOccluderOnly` plus the Guest 4 full-chair assertion with:

```csharp
Assert.That(seatedMethodBody, Does.Contain("guestState.GuestIndex == 3"),
    "The selected green-chair exception must target Guest 4.");
Assert.That(
    seatedMethodBody,
    Does.Match(
        @"guestState\.GuestIndex\s*==\s*3[\s\S]*" +
        @"ActivateFrontOccluderOnly\([\s\S]*" +
        @"drawingRoomGreenChairForegroundRenderer"),
    "Guest 4 must keep normal actor Y sorting while only the detached foreground follows her.");
Assert.That(
    seatedMethodBody,
    Does.Not.Contain("TryFindGuestByNumber(2, out GuestRuntimeState preservedBehindGuest)"),
    "Guest 4's furniture exception must not force Guest 2 behind her.");
Assert.That(seatedMethodBody, Does.Not.Contain("guestState.GuestIndex == 0"),
    "The green-chair exception must not target Guest 1.");
```

Do not remove the existing GuestIndex `7` `ActivateBehindOccluder` contract.

- [ ] **Step 3: Update the live all-guest contract to prove the exception does not rewrite actors**

In the Guest 4 branch of `Chapter2SkipStagesEveryGuestWithCanonicalOcclusion`, replace the assertions that disable every Guest 4 `SortingGroup` and place the full chair above her with:

```csharp
Assert.That(seatedException.FrontOccluderRenderer,
    Is.SameAs(greenChairForegroundRenderer));
SpriteRenderer guestFootRenderer = sorter.ActorFootRenderer;
Assert.That(guestFootRenderer, Is.Not.Null);
sorter.ApplySorting();
int worldYOrderBeforeException = guestFootRenderer.sortingOrder;
seatedException.ApplyOcclusionNow();
Assert.That(guestFootRenderer.sortingOrder, Is.EqualTo(worldYOrderBeforeException),
    "Guest 4's seated furniture exception must not replace actor world-Y sorting.");

SpriteRenderer frontmostGuestRenderer = FindFrontmostActiveRenderer(guest.gameObject);
Assert.That(frontmostGuestRenderer, Is.Not.Null);
Assert.That(greenChairForegroundRenderer.sortingLayerID,
    Is.EqualTo(frontmostGuestRenderer.sortingLayerID));
Assert.That(greenChairForegroundRenderer.sortingOrder,
    Is.EqualTo(frontmostGuestRenderer.sortingOrder + 1),
    "Only the detached green-chair foreground should be forced immediately over Guest 4.");
```

Later in the same pan loop, replace the `Guest 2 < Guest 4 < chair` packed-chain assertions with this ownership check:

```csharp
WorldYSortSpriteRenderer guest2Sorter = guest2.GetComponent<WorldYSortSpriteRenderer>();
WorldYSortSpriteRenderer guest4Sorter = guest4.GetComponent<WorldYSortSpriteRenderer>();
Assert.That(guest2Sorter, Is.Not.Null);
Assert.That(guest4Sorter, Is.Not.Null);
guest2Sorter.ApplySorting();
guest4Sorter.ApplySorting();
int guest2WorldYOrder = guest2Sorter.ActorFootRenderer.sortingOrder;
int guest4WorldYOrder = guest4Sorter.ActorFootRenderer.sortingOrder;

guest4Exception.ApplyOcclusionNow();

Assert.That(guest2Sorter.ActorFootRenderer.sortingOrder, Is.EqualTo(guest2WorldYOrder));
Assert.That(guest4Sorter.ActorFootRenderer.sortingOrder, Is.EqualTo(guest4WorldYOrder));
SpriteRenderer guest4Front = FindFrontmostActiveRenderer(guest4.gameObject);
Assert.That(greenChairForegroundRenderer.sortingOrder,
    Is.EqualTo(guest4Front.sortingOrder + 1));
```

Keep the existing tea-table and Guest 8 armrest ownership assertions unchanged.

- [ ] **Step 4: Update the normal-arrival test to assert world-Y ownership**

Rename `NormalChapter1ArrivalKeepsYellowDressGuestBehindSelectedGreenChair` to
`NormalChapter1ArrivalKeepsYellowDressGuestOnWorldYUnderForegroundCutout`.
Remove the loop requiring all Guest 4 `SortingGroup`s to be disabled and remove
the assertion requiring the full green-chair renderer to be above Guest 4.
After resolving `guestSorter` and `seatedException`, add:

```csharp
SpriteRenderer guestFootRenderer = guestSorter.ActorFootRenderer;
Assert.That(guestFootRenderer, Is.Not.Null);
guestSorter.ApplySorting();
int worldYOrderBeforeException = guestFootRenderer.sortingOrder;
seatedException.ApplyOcclusionNow();
Assert.That(guestFootRenderer.sortingOrder, Is.EqualTo(worldYOrderBeforeException));
Assert.That(greenChairForegroundRenderer.sortingLayerID,
    Is.EqualTo(frontmostGuestRenderer.sortingLayerID));
Assert.That(greenChairForegroundRenderer.sortingOrder,
    Is.EqualTo(frontmostGuestRenderer.sortingOrder + 1));
```

Keep the assertion that the full chair itself still equals
`playerMovement.GetSortingOrderForFootY(greenChair.position.y)`.

- [ ] **Step 5: Run the focused tests and verify RED**

Run:

```bash
/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity \
  -batchmode -nographics -quit \
  -projectPath /home/hamza/dreadforge_2022_2 \
  -runTests -testPlatform EditMode \
  -testFilter 'ObjectCollisionBoxRegressionTests.EveryGuest4SittingFrameUsesBottomCenterPivot;Chapter1GuestRoomVisibilityRegressionTests.DrawingRoomUsesContinuousYSortingWithOnlySeatedOverrides;ObjectCollisionBoxRegressionTests.Chapter2SkipStagesEveryGuestWithCanonicalOcclusion;ObjectCollisionBoxRegressionTests.NormalChapter1ArrivalKeepsYellowDressGuestOnWorldYUnderForegroundCutout' \
  -testResults /tmp/chantilly-guest4-pivot-red.xml \
  -logFile /tmp/chantilly-guest4-pivot-red.log
```

Expected: the pivot test fails with importer alignment `0` instead of Bottom
Center `7`, and the source/live sorting contracts fail because Guest 4 still
uses `ActivateBehindOccluder` and preserves Guest 2. Compilation and scene
setup must succeed.

- [ ] **Step 6: Commit the proven failing regressions**

```bash
git add Assets/Editor/ObjectCollisionBoxRegressionTests.cs \
  Assets/Editor/Chapter1GuestRoomVisibilityRegressionTests.cs
git commit -m "test: prove Guest 4 sitting pivot and sorting ownership"
```

---

### Task 2: Apply the Permanent Guest 4 Fix

**Files:**
- Modify: `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs:5262-5287`
- Modify: `Assets/Art/Characters/guest4_no_white_artifacts/guest4sitidle/guest4sit2.png.meta:49-50`
- Modify: `Assets/Art/Characters/guest4_no_white_artifacts/guest4sitidle/guest4sit3.png.meta:49-50`
- Test: `Assets/Editor/ObjectCollisionBoxRegressionTests.cs`
- Test: `Assets/Editor/Chapter1GuestRoomVisibilityRegressionTests.cs`

**Interfaces:**
- Consumes: `DiningRoomSeatedGuestOcclusionException.ActivateFrontOccluderOnly(ActorRoomState,RoomAnchor,SpriteRenderer,string,string)`.
- Produces: foreground-only seated occlusion for GuestIndex `3` and Bottom Center importer metadata for all two sitting frames.

- [ ] **Step 1: Replace Guest 4's actor-wide override with foreground-only mode**

In the GuestIndex `3` branch, preserve the existing null checks and component
creation, then replace the preserved-Guest-2 lookup and `ActivateBehindOccluder`
call with:

```csharp
seatedException.ActivateFrontOccluderOnly(
    guestState.ActorState,
    seatAnchor,
    drawingRoomGreenChairForegroundRenderer,
    drawingRoomId,
    "Butler");
return;
```

Delete only:

```csharp
TryFindGuestByNumber(2, out GuestRuntimeState preservedBehindGuest);
```

and the old GuestIndex `3` `ActivateBehindOccluder` call. Do not change the
GuestIndex `7` armrest route.

- [ ] **Step 2: Set both sitting sprite importers to Bottom Center**

In both `guest4sit2.png.meta` and `guest4sit3.png.meta`, set exactly:

```yaml
  alignment: 7
  spritePivot: {x: 0.5, y: 0}
```

Do not change GUIDs, pixel density, compression, sprite IDs, internal IDs, or
any PNG contents. In particular, preserve the currently serialized
`guest4sit2.png.meta` internal ID rather than replacing the asset.

- [ ] **Step 3: Run the focused tests and verify GREEN**

Repeat Task 1 Step 5 with results and log paths:

```text
/tmp/chantilly-guest4-pivot-green.xml
/tmp/chantilly-guest4-pivot-green.log
```

Expected: four tests pass, zero fail. Read the XML totals and search the log for
compilation errors, unhandled exceptions, and assertion failures.

- [ ] **Step 4: Inspect both source frames and the live Drawing Room output**

Open these assets at original detail:

```text
Assets/Art/Characters/guest4_no_white_artifacts/guest4sitidle/guest4sit2.png
Assets/Art/Characters/guest4_no_white_artifacts/guest4sitidle/guest4sit3.png
/tmp/chantilly-drawing-room-all-guests-after.png
```

Confirm that both intended sitting frames were audited, Guest 4 remains
visually seated at a stable floor point, the foreground cutout covers only the
intended chair pixels, and other guests are not packed behind her.

- [ ] **Step 5: Commit the permanent fix without the user's scene file**

```bash
git add Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs \
  Assets/Art/Characters/guest4_no_white_artifacts/guest4sitidle/guest4sit2.png.meta \
  Assets/Art/Characters/guest4_no_white_artifacts/guest4sitidle/guest4sit3.png.meta
git diff --cached --name-only
git commit -m "fix: restore Guest 4 world sorting and sitting pivots"
```

Expected staged files: exactly the controller and two sitting-frame metadata
files. `Assets/Scenes/Gameplay.unity` remains dirty and uncommitted.

---

### Task 3: Verify Scope and Hand Back for Editor Testing

**Files:**
- Verify: `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs`
- Verify: `Assets/Art/Characters/guest4_no_white_artifacts/guest4sitidle/guest4sit2.png.meta`
- Verify: `Assets/Art/Characters/guest4_no_white_artifacts/guest4sitidle/guest4sit3.png.meta`
- Verify: `Assets/Editor/ObjectCollisionBoxRegressionTests.cs`
- Verify: `Assets/Editor/Chapter1GuestRoomVisibilityRegressionTests.cs`

**Interfaces:**
- Consumes: the focused test evidence and two implementation commits.
- Produces: verified source ready for the user's final Unity Editor play test.

- [ ] **Step 1: Run the surrounding regression suites**

```bash
/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity \
  -batchmode -nographics -quit \
  -projectPath /home/hamza/dreadforge_2022_2 \
  -runTests -testPlatform EditMode \
  -testFilter 'Chapter1GuestRoomVisibilityRegressionTests;ObjectCollisionBoxRegressionTests;SortingOwnershipAuditTests;CharacterPresentationOwnershipTests' \
  -testResults /tmp/chantilly-guest4-pivot-focused.xml \
  -logFile /tmp/chantilly-guest4-pivot-focused.log
```

Expected: every selected test passes with zero failures. Confirm the XML result
and inspect the log rather than relying only on the process exit code.

- [ ] **Step 2: Verify the animation and importer inventory**

Confirm the sitting clip still contains exactly the two expected GUIDs and that
both metadata files report alignment `7` and pivot `(0.5, 0)`:

```bash
rg -o 'guid: [0-9a-f]+' \
  Assets/Animation/CountessElowenDusk/CountessElowenDusk_Sitting.anim | sort -u
rg -n '^(guid:|  alignment:|  spritePivot:)' \
  Assets/Art/Characters/guest4_no_white_artifacts/guest4sitidle/*.png.meta
```

Expected GUIDs:

```text
3c77696129c44fc49b56737e0ffdc4e9
68e57e21c4bd49ebbc1faec9dc2d6830
```

- [ ] **Step 3: Verify repository scope and whitespace**

```bash
git diff --check HEAD~2..HEAD
git diff --stat HEAD~2..HEAD
git status --short
```

Expected: no whitespace errors; the two task commits contain only the five
planned files. `Assets/Scenes/Gameplay.unity` remains the user's separate dirty
scene change. No build output exists and no executable build command ran.

- [ ] **Step 4: Report the exact test evidence**

Report the focused and surrounding XML totals, the two sitting frame paths,
the inspected live PNG path, the implementation commit hashes, and the
preserved dirty scene. Explicitly state that no executables were built and that
the user should perform the final visual check in Play Mode.
