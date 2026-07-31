# Drawing Room Green Armrest Occlusion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Keep the selected Drawing Room green armrest visibly in front of its seated occupant while preserving the armrest, tea table, and all unrelated actors under their existing Y-sorting ownership.

**Architecture:** GuestIndex 7 will stop moving the armrest to a raw actor order and will instead use the existing actor-behind-occluder path, which leaves the blocker-owned armrest untouched and normalizes only Guest 8 immediately below it. The all-guest Gameplay regression will gain an isolated same-frame A/B camera probe so final pixels—not just integer orders—prove the armrest is in front.

**Tech Stack:** Unity 6000.4.10f1, C#, Unity Test Framework EditMode/UnityTest, URP 2D SpriteRenderer/SortingGroup, Git.

## Global Constraints

- Do not create Windows, macOS, or Linux executables for this task.
- Do not modify Assets/Scenes/Gameplay.unity, ProjectSettings/ProjectSettings.asset, or Assets/Art/UI/Fonts/NotoSerifDisplay-Medium SDF.asset; their current changes belong to the user.
- Limit runtime behavior changes to GuestIndex 7 and greenchairarmrest_0 in the Drawing Room.
- Keep PlayerBlocker_greenchairarmrest_0 as the armrest's sole physical Y-order owner.
- Keep PlayerBlocker_tea_service_table as the tea table's sole physical Y-order owner.
- Preserve the existing Guest 2 → Guest 4 → full green chair chain and ordinary standing-actor Y sorting.
- Visual evidence must come from the real Gameplay camera after all eight guests are staged.

---

### Task 1: Add a Failing Armrest Ownership and Rendered-Pixel Regression

**Files:**
- Modify: Assets/Editor/Chapter1GuestRoomVisibilityRegressionTests.cs:685-714
- Modify: Assets/Editor/ObjectCollisionBoxRegressionTests.cs:1744-2078
- Modify: Assets/Editor/Diagnostics/GreenChairRenderStateDiagnostic.cs:9-218

**Interfaces:**
- Consumes: DiningRoomSeatedGuestOcclusionException.ApplyOcclusionNow(), ObjectMovementBlocker2D.ApplySourceSortingNow(), and GreenChairRenderStateDiagnostic.CaptureGameplayCameraPngForTests(Camera,string).
- Produces: GreenChairRenderStateDiagnostic.PixelComparisonResult and CaptureOcclusionPixelComparisonForTests(Camera,SpriteRenderer,GameObject,string).

- [ ] **Step 1: Change the controller source-contract test to require fixed-occluder ownership**

Replace the armrest-only expectations in DrawingRoomUsesContinuousYSortingWithOnlySeatedOverrides with:

~~~csharp
Assert.That(
    seatedMethodBody,
    Does.Match(
        @"guestState\.GuestIndex\s*==\s*7[\s\S]*" +
        @"ActivateBehindOccluder\([\s\S]*" +
        @"drawingRoomGreenChairArmrestRenderer[\s\S]*null"),
    "Guest 8 must be pinned behind the blocker-owned green armrest.");
Assert.That(
    seatedMethodBody,
    Does.Not.Contain("ActivateFrontOccluderOnly"),
    "The Drawing Room must not move a physical armrest to a raw guest order.");
Assert.That(
    seatedMethodBody,
    Does.Not.Contain("GetDrawingRoomFrontOccluderRenderer"),
    "The selected armrest now has one explicit fixed-occluder branch.");
~~~

Keep the Guest 4 full-chair assertions and three serialized renderer-field assertions. Remove frontOccluderMapMethodBody and its obsolete case-7 assertions.

- [ ] **Step 2: Add the same-frame camera pixel comparison contract**

Add this nested public result inside GreenChairRenderStateDiagnostic:

~~~csharp
public readonly struct PixelComparisonResult
{
    public PixelComparisonResult(
        int overlapPixelCount,
        long actualToFrontError,
        long actualToBehindError,
        string actualPath,
        string knownFrontPath,
        string knownBehindPath)
    {
        OverlapPixelCount = overlapPixelCount;
        ActualToFrontError = actualToFrontError;
        ActualToBehindError = actualToBehindError;
        ActualPath = actualPath;
        KnownFrontPath = knownFrontPath;
        KnownBehindPath = knownBehindPath;
    }

    public int OverlapPixelCount { get; }
    public long ActualToFrontError { get; }
    public long ActualToBehindError { get; }
    public string ActualPath { get; }
    public string KnownFrontPath { get; }
    public string KnownBehindPath { get; }
    public bool ActualMatchesKnownFront =>
        OverlapPixelCount >= 25 &&
        ActualToFrontError * 50L <= ActualToBehindError;
}
~~~

Add this exact entry point:

~~~csharp
public static PixelComparisonResult CaptureOcclusionPixelComparisonForTests(
    Camera camera,
    SpriteRenderer occluder,
    GameObject actorRoot,
    string outputPrefix)
~~~

Implementation invariants:

~~~csharp
const int width = 1672;
const int height = 941;
const int isolatedLayer = 31;
const int forcedBehindOrder = short.MinValue;
const int forcedFrontOrder = short.MaxValue;

SpriteRenderer[] actorRenderers =
    actorRoot.GetComponentsInChildren<SpriteRenderer>(true);
List<SpriteRenderer> activeActorRenderers = new List<SpriteRenderer>();

for (int i = 0; i < actorRenderers.Length; i++)
{
    SpriteRenderer renderer = actorRenderers[i];

    if (renderer != null &&
        renderer.enabled &&
        renderer.gameObject.activeInHierarchy &&
        renderer.sprite != null)
    {
        activeActorRenderers.Add(renderer);
    }
}
~~~

Save the camera target, culling mask, clear flags, background color, each selected GameObject layer, and the occluder's layer/order/sort point. In try/finally, move the occluder and active actor renderer GameObjects to layer 31, use cullingMask 1 << 31 and a transparent solid clear, then render actual, known-behind, and known-front synchronously without yielding.

Project renderer bounds through camera.WorldToViewportPoint and multiply by 1672x941. Intersect the occluder pixel rect with each actor rect, union non-empty intersections, and clamp to the target. Do not use WorldToScreenPoint or hard-coded screen coordinates.

Use:

~~~csharp
private static int ColorDistance(Color32 left, Color32 right)
{
    return Mathf.Abs(left.r - right.r) +
        Mathf.Abs(left.g - right.g) +
        Mathf.Abs(left.b - right.b) +
        Mathf.Abs(left.a - right.a);
}
~~~

A pixel enters the painted-overlap mask when ColorDistance(knownFront[p], knownBehind[p]) > 24. Sum actual-to-front and actual-to-behind errors only on that mask. Write:

~~~text
<outputPrefix>-actual.png
<outputPrefix>-known-front.png
<outputPrefix>-known-behind.png
~~~

Restore every camera, layer, renderer, sorting, texture, and render-target value in finally and destroy temporary Unity objects with DestroyImmediate.

- [ ] **Step 3: Extend the real all-guest test with exact Guest 8 and armrest ownership**

Resolve:

~~~csharp
Transform greenChairArmrest = FindDescendant(room, "greenchairarmrest_0");
Transform greenChairArmrestBlocker =
    FindDescendant(room, "PlayerBlocker_greenchairarmrest_0");
SpriteRenderer greenChairArmrestRenderer = greenChairArmrest != null
    ? greenChairArmrest.GetComponent<SpriteRenderer>()
    : null;
ObjectMovementBlocker2D greenChairArmrestMarker =
    greenChairArmrestBlocker != null
        ? greenChairArmrestBlocker.GetComponent<ObjectMovementBlocker2D>()
        : null;
~~~

Resolve actorId guest_8 beside Guest 2 and Guest 4. Assert the marker owns greenChairArmrest.gameObject. At each pan apply the marker before the exception, then require:

~~~csharp
int blockerOwnedArmrestOrder = greenChairArmrestMarker.CurrentSortingOrder;
int tableOrderBeforeGuest8Exception = tableRenderer.sortingOrder;
DiningRoomSeatedGuestOcclusionException guest8Exception =
    guest8.GetComponent<DiningRoomSeatedGuestOcclusionException>();

Assert.That(guest8Exception, Is.Not.Null);
guest8Exception.ApplyOcclusionNow();

SpriteRenderer guest8Front = FindFrontmostActiveRenderer(guest8.gameObject);
Assert.That(guest8Exception.AssignedChair,
    Is.SameAs(greenChairArmrestRenderer.gameObject));
Assert.That(guest8Exception.FrontOccluderRenderer, Is.Null);
AssertAllLocalSortingGroupsDisabled(
    guest8.GetComponentsInChildren<SortingGroup>(true),
    "Guest 8 must not retain an effective override over the green armrest.");
Assert.That(guest8Front.sortingLayerID,
    Is.EqualTo(greenChairArmrestRenderer.sortingLayerID));
Assert.That(guest8Front.sortingOrder,
    Is.LessThan(greenChairArmrestRenderer.sortingOrder));
Assert.That(greenChairArmrestRenderer.sortingOrder,
    Is.EqualTo(blockerOwnedArmrestOrder));
Assert.That(tableRenderer.sortingOrder,
    Is.EqualTo(tableOrderBeforeGuest8Exception));
Assert.That(tableRenderer.sortingOrder,
    Is.EqualTo(tableMarker.CurrentSortingOrder));
~~~

Special-case Guest 8 before the generic FrontOccluderRenderer assertion because fixed-occluder mode exposes the armrest through AssignedChair and intentionally leaves FrontOccluderRenderer null.

At pan zero, add an enabled SortingGroup to Guest 8 with the armrest's layer and armrest order + 100. In try/finally, apply the exception, require the group to be disabled, capture /tmp/chantilly-drawing-room-all-guests-after.png, and call:

~~~csharp
GreenChairRenderStateDiagnostic.PixelComparisonResult pixels =
    GreenChairRenderStateDiagnostic.CaptureOcclusionPixelComparisonForTests(
        Camera.main,
        greenChairArmrestRenderer,
        guest8.gameObject,
        "/tmp/chantilly-green-armrest-guest8");

Assert.That(pixels.OverlapPixelCount, Is.GreaterThanOrEqualTo(25));
Assert.That(
    pixels.ActualMatchesKnownFront,
    Is.True,
    $"Actual did not match forced-front evidence. " +
    $"frontError={pixels.ActualToFrontError} " +
    $"behindError={pixels.ActualToBehindError} " +
    $"actual={pixels.ActualPath}");
~~~

Always DestroyImmediate the injected group in finally.

- [ ] **Step 4: Run the focused regressions and verify RED**

Run:

~~~bash
/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity \
  -batchmode -nographics \
  -projectPath /home/hamza/dreadforge_2022_2 \
  -runTests -testPlatform EditMode \
  -testFilter 'Chapter1GuestRoomVisibilityRegressionTests.DrawingRoomUsesContinuousYSortingWithOnlySeatedOverrides;ObjectCollisionBoxRegressionTests.Chapter2SkipStagesEveryGuestWithCanonicalOcclusion' \
  -testResults /tmp/chantilly-green-armrest-red.xml \
  -logFile /tmp/chantilly-green-armrest-red.log
~~~

Expected: FAIL because current code contains ActivateFrontOccluderOnly, exposes the armrest as FrontOccluderRenderer instead of AssignedChair, rewrites the blocker order, or leaves the injected SortingGroup enabled. Confirm this is an assertion failure about missing fixed-occluder behavior, not compilation or scene setup.

- [ ] **Step 5: Commit the proven failing regression**

~~~bash
git add Assets/Editor/Chapter1GuestRoomVisibilityRegressionTests.cs \
  Assets/Editor/ObjectCollisionBoxRegressionTests.cs \
  Assets/Editor/Diagnostics/GreenChairRenderStateDiagnostic.cs
git commit -m "test: prove selected green armrest occlusion"
~~~

---

### Task 2: Route Only Guest 8 Behind the Blocker-Owned Armrest

**Files:**
- Modify: Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs:5237-5321
- Test: Assets/Editor/Chapter1GuestRoomVisibilityRegressionTests.cs
- Test: Assets/Editor/ObjectCollisionBoxRegressionTests.cs

**Interfaces:**
- Consumes: the existing ActivateBehindOccluder(ActorRoomState,RoomAnchor,SpriteRenderer,SpriteRenderer,string,string) overload.
- Produces: an explicit GuestIndex 7 fixed-occluder branch; no new runtime API.

- [ ] **Step 1: Replace the raw front-only path**

After GuestIndex 3's full-chair branch, replace the generic front-occluder lookup/call with:

~~~csharp
if (guestState.GuestIndex != 7)
{
    seatedException?.DeactivateForSeat();
    return;
}

if (drawingRoomGreenChairArmrestRenderer == null)
{
    Debug.LogError(
        "Drawing Room green armrest occlusion is not wired for seated guest 8.",
        this);
    seatedException?.DeactivateForSeat();
    return;
}

if (seatedException == null)
{
    seatedException = guestState.ActorState.gameObject
        .AddComponent<DiningRoomSeatedGuestOcclusionException>();
}

seatedException.ActivateBehindOccluder(
    guestState.ActorState,
    seatAnchor,
    drawingRoomGreenChairArmrestRenderer,
    null,
    drawingRoomId,
    "Butler");
~~~

Delete GetDrawingRoomFrontOccluderRenderer. Do not modify DiningRoomSeatedGuestOcclusionException: the existing fixed-occluder path already captures all active actor renderers, disables local groups, preserves internal order, restores state, and leaves a null front cutout untouched.

- [ ] **Step 2: Run the two RED tests and verify GREEN**

Repeat Task 1's Unity command, writing to /tmp/chantilly-green-armrest-green.xml and /tmp/chantilly-green-armrest-green.log.

Expected: two tests passed, zero failed. Read the XML and log rather than relying on process exit alone.

- [ ] **Step 3: Inspect all generated images**

Open at high/original detail:

~~~text
/tmp/chantilly-drawing-room-all-guests-after.png
/tmp/chantilly-green-armrest-guest8-actual.png
/tmp/chantilly-green-armrest-guest8-known-front.png
/tmp/chantilly-green-armrest-guest8-known-behind.png
~~~

The actual overlap must visibly match known-front, visibly differ from known-behind, and the full scene must retain normal table occlusion.

- [ ] **Step 4: Commit the runtime fix**

~~~bash
git add Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs
git commit -m "fix: keep green armrest over its seated guest"
~~~

---

### Task 3: Verify, Review, and Hand Back for Editor Testing

**Files:**
- Verify: Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs
- Verify: Assets/Scripts/Characters/DiningRoomSeatedGuestOcclusionException.cs
- Verify: Assets/Editor/Chapter1GuestRoomVisibilityRegressionTests.cs
- Verify: Assets/Editor/ObjectCollisionBoxRegressionTests.cs
- Verify: Assets/Editor/Diagnostics/GreenChairRenderStateDiagnostic.cs

**Interfaces:**
- Consumes: the fixed route and PNG evidence.
- Produces: fresh focused-suite evidence and reviewed commits ready for the user's editor test.

- [ ] **Step 1: Run surrounding suites**

~~~bash
/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity \
  -batchmode -nographics \
  -projectPath /home/hamza/dreadforge_2022_2 \
  -runTests -testPlatform EditMode \
  -testFilter 'Chapter1GuestRoomVisibilityRegressionTests;ObjectCollisionBoxRegressionTests;SortingOwnershipAuditTests' \
  -testResults /tmp/chantilly-green-armrest-focused.xml \
  -logFile /tmp/chantilly-green-armrest-focused.log
~~~

Expected: every selected test passes with zero failures. Read XML totals and search the log for assertion failures, unhandled exceptions, and compilation errors.

- [ ] **Step 2: Verify repository scope and whitespace**

~~~bash
git diff --check HEAD~2..HEAD
git status --short
git diff --stat HEAD~2..HEAD
git diff HEAD~2..HEAD -- \
  Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs \
  Assets/Editor/Chapter1GuestRoomVisibilityRegressionTests.cs \
  Assets/Editor/ObjectCollisionBoxRegressionTests.cs \
  Assets/Editor/Diagnostics/GreenChairRenderStateDiagnostic.cs
~~~

Expected: no whitespace errors; only planned files occur in the two implementation commits. The user's three pre-existing dirty files remain dirty and otherwise untouched.

- [ ] **Step 3: Request and address code review**

Use superpowers:requesting-code-review on the two implementation commits. The reviewer must check:

~~~text
1. GuestIndex 7 is the only new runtime route.
2. Armrest and table remain blocker-owned.
3. Guest 2/Guest 4 order is unchanged.
4. Pixel-probe mutations are restored in finally.
5. A/B mask proves painted overlap, not transparent bounds.
6. No scene, project-setting, font, or build artifact is included.
~~~

Address actionable findings, rerun focused tests, and create a review-fix commit only when needed.

- [ ] **Step 4: Report evidence without building**

Report XML totals, PNG paths inspected, implementation hashes, and preserved dirty files. Explicitly state that no executable build command ran.
