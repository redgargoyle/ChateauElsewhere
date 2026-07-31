# Drawing Room Guest Order Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep Guest 2 behind Guest 4 while the selected green chair remains in front of Guest 4, without changing the chair or tea table's ordinary world-Y sorting.

**Architecture:** Extend Guest 4's existing late seated-occlusion component to own one optional companion actor. The component will preserve each actor's internal draw order, pack Guest 2 immediately behind Guest 4, and restore both actors exactly when the exception is inactive. The controller will pass Guest 2 only for Guest 4's selected green-chair seat.

**Tech Stack:** Unity 6.0.4, C#, Unity 2D `SpriteRenderer`, `SortingGroup`, NUnit EditMode/PlayMode tests.

## Global Constraints

- Preserve the strict back-to-front chain `Guest 2 < Guest 4 < full green chair < detached foreground rail`.
- Do not modify the green chair's ordinary world-Y sorting order.
- Do not modify the tea table, its collider, its blocker, or its `ObjectMovementBlocker2D` sorting ownership.
- Do not change other Drawing Room seats or other rooms.
- Restore renderer layer, order, pivot sort point, and local `SortingGroup` state exactly.
- Do not modify the user's unrelated dirty files.
- Do not build executables.

---

### Task 1: Add the Guest-to-Guest Ordering Regressions

**Files:**
- Modify: `Assets/Editor/ObjectCollisionBoxRegressionTests.cs`

**Interfaces:**
- Consumes: Existing `DiningRoomSeatedGuestOcclusionException.ActivateBehindOccluder(...)`, live `guest_2`/`guest_4` actor IDs, `ObjectMovementBlocker2D.CurrentSortingOrder`.
- Produces: A focused unit test and live scene assertions defining the required ordering and table-ownership behavior.

- [ ] **Step 1: Add a focused unit test for ordering and restoration**

Add `SeatedGuestBehindOccluderPreservesEarlierGuestOrderAndRestoresBothActors`.
Construct seated Guest 2 and Guest 4 actors with enabled root and nested
`SortingGroup` overrides, a chair at order 1050, and a foreground rail. Call
the new overload that takes both actor states, then assert:

```csharp
Assert.That(guest2Front.sortingOrder, Is.LessThan(guest4Back.sortingOrder));
Assert.That(guest4Front.sortingOrder, Is.LessThan(chairRenderer.sortingOrder));
Assert.That(frontRenderer.sortingOrder, Is.EqualTo(chairRenderer.sortingOrder + 1));
Assert.That(guest2Group.enabled, Is.False);
Assert.That(guest4Group.enabled, Is.False);
```

After `DeactivateForSeat()`, assert the original layer IDs, orders,
`SpriteSortPoint` values, `SortingGroup.enabled`, `sortingOrder`, and
`sortAtRoot` values for both actors are restored.

- [ ] **Step 2: Extend the live Chapter 2 regression**

In `Chapter2SkipStagesEveryGuestWithCanonicalOcclusion`, resolve `guest_2` and
`guest_4` once from `drawingRoomGuests`. During every vertical pan, apply the
Guest 4 exception and assert:

```csharp
SpriteRenderer guest2Front = FindFrontmostActiveRenderer(guest2.gameObject);
SpriteRenderer guest4Back = FindBackmostActiveRenderer(guest4.gameObject);
SpriteRenderer guest4Front = FindFrontmostActiveRenderer(guest4.gameObject);

Assert.That(guest2Front.sortingLayerID, Is.EqualTo(guest4Back.sortingLayerID));
Assert.That(guest2Front.sortingOrder, Is.LessThan(guest4Back.sortingOrder));
Assert.That(guest4Front.sortingOrder, Is.LessThan(greenChairRenderer.sortingOrder));
Assert.That(greenChairForegroundRenderer.sortingOrder,
    Is.EqualTo(greenChairRenderer.sortingOrder + 1));
```

Capture the tea-table order before applying the Guest 4 exception and prove the
exception cannot change it:

```csharp
int tableOrderBeforeGuestException = tableRenderer.sortingOrder;
guest4Exception.ApplyOcclusionNow();
Assert.That(tableRenderer.sortingOrder, Is.EqualTo(tableOrderBeforeGuestException));
Assert.That(tableRenderer.sortingOrder, Is.EqualTo(tableMarker.CurrentSortingOrder));
```

Add `FindBackmostActiveRenderer(GameObject)` beside
`FindFrontmostActiveRenderer(GameObject)` using the inverse layer/order
comparison and the same active renderer filtering.

- [ ] **Step 3: Run the focused tests and verify the new contract fails**

Run:

```bash
/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity \
  -batchmode -nographics \
  -projectPath /home/hamza/dreadforge_2022_2 \
  -runTests -testPlatform EditMode \
  -testFilter ObjectCollisionBoxRegressionTests \
  -testResults /tmp/chantilly-guest-order-red.xml \
  -logFile /tmp/chantilly-guest-order-red.log
```

Expected: FAIL because the companion-aware `ActivateBehindOccluder` overload
does not exist or because live Guest 2 sorts ahead of Guest 4. Existing
unrelated tests should continue to compile once the test calls the planned
overload.

- [ ] **Step 4: Commit the red tests**

```bash
git add Assets/Editor/ObjectCollisionBoxRegressionTests.cs
git commit -m "test: cover drawing room guest order chain"
```

### Task 2: Preserve Guest 2 Behind Guest 4 in the Existing Exception

**Files:**
- Modify: `Assets/Scripts/Characters/DiningRoomSeatedGuestOcclusionException.cs`
- Modify: `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs`
- Test: `Assets/Editor/ObjectCollisionBoxRegressionTests.cs`

**Interfaces:**
- Consumes: `TryFindGuestByNumber(int, out GuestRuntimeState)` and the existing Guest 4 chair override.
- Produces: `ActivateBehindOccluder(ActorRoomState targetActorState, ActorRoomState targetPreservedBehindActorState, RoomAnchor seatAnchor, SpriteRenderer targetOccluderRenderer, SpriteRenderer targetFrontOccluderRenderer, string targetRoomName, string targetButlerExclusionObjectName)`.

- [ ] **Step 1: Add an optional companion actor to the seated exception**

Keep the existing public overload for all current callers and delegate it to a
new overload:

```csharp
public void ActivateBehindOccluder(
    ActorRoomState targetActorState,
    RoomAnchor seatAnchor,
    SpriteRenderer targetOccluderRenderer,
    SpriteRenderer targetFrontOccluderRenderer,
    string targetRoomName,
    string targetButlerExclusionObjectName)
{
    ActivateBehindOccluder(
        targetActorState,
        null,
        seatAnchor,
        targetOccluderRenderer,
        targetFrontOccluderRenderer,
        targetRoomName,
        targetButlerExclusionObjectName);
}

public void ActivateBehindOccluder(
    ActorRoomState targetActorState,
    ActorRoomState targetPreservedBehindActorState,
    RoomAnchor seatAnchor,
    SpriteRenderer targetOccluderRenderer,
    SpriteRenderer targetFrontOccluderRenderer,
    string targetRoomName,
    string targetButlerExclusionObjectName)
```

Store the optional actor in `preservedBehindActorState`. Give it separate
renderer-state, sorting-group-state, and active-renderer collections so its
state can be restored independently if it leaves the seat while Guest 4 stays
seated.

- [ ] **Step 2: Generalize the renderer-packing helper**

Extract the current Guest 4 normalization into:

```csharp
private bool ApplyActorImmediatelyBehindOrder(
    ActorRoomState targetActorState,
    List<SpriteRenderer> targetActiveRenderers,
    Dictionary<SpriteRenderer, RendererSortingState> targetRendererStates,
    Dictionary<SortingGroup, SortingGroupState> targetSortingGroupStates,
    int targetFrontmostOrder,
    out int targetBackmostOrder)
```

The helper must gather only enabled, active renderers; sort them by their
effective pre-override group path; capture and disable only groups under the
specified actor root; preserve internal back-to-front order; and assign dense
orders ending at `targetFrontmostOrder`.

Update `CompareRendererBackToFront`, `BuildEffectiveSortPath`, and
`CaptureAndDisableActorLocalSortingGroups` to accept the explicit actor root
and state collections instead of assuming the primary `actorState`.

- [ ] **Step 3: Apply and restore the strict actor chain**

In `ApplyActorBehindOccluder`, first pack Guest 4 ending at
`chair.sortingOrder - 1`. If `preservedBehindActorState` is seated, visible,
non-Butler, and in the Drawing Room, pack Guest 2 ending at one less than Guest
4's backmost renderer:

```csharp
int guest4FrontOrder = assignedChairRenderer.sortingOrder - 1;

if (!ApplyActorImmediatelyBehindOrder(
    actorState,
    activeActorRenderers,
    actorRendererStates,
    actorSortingGroupStates,
    guest4FrontOrder,
    out int guest4BackOrder))
{
    RestoreNormalSorting();
    return;
}

if (CanApplyToPreservedBehindActor())
{
    ApplyActorImmediatelyBehindOrder(
        preservedBehindActorState,
        preservedBehindActiveRenderers,
        preservedBehindRendererStates,
        preservedBehindSortingGroupStates,
        guest4BackOrder - 1,
        out _);
}
else
{
    RestorePreservedBehindActorSorting();
}
```

The full chair remains unmodified. Keep the existing rail assignment at
`chair.sortingOrder + 1`. `RestoreNormalSorting`, `DeactivateForSeat`,
`OnDisable`, and `OnDestroy` must restore both actors.

- [ ] **Step 4: Wire only Guest 4 to Guest 2**

Inside the `guestState.GuestIndex == 3` branch of
`ApplyDrawingRoomSeatedOcclusion`, resolve Guest 2 with the existing
one-based helper:

```csharp
TryFindGuestByNumber(2, out GuestRuntimeState preservedBehindGuest);

seatedException.ActivateBehindOccluder(
    guestState.ActorState,
    preservedBehindGuest?.ActorState,
    seatAnchor,
    drawingRoomGreenChairRenderer,
    drawingRoomGreenChairForegroundRenderer,
    drawingRoomId,
    "Butler");
```

Do not change `GetDrawingRoomFrontOccluderRenderer`, the chair sorter, the
table marker, scene YAML, or other guest branches.

- [ ] **Step 5: Run the focused regression suite**

Run the Task 1 Unity command with results at
`/tmp/chantilly-guest-order-green.xml` and log at
`/tmp/chantilly-guest-order-green.log`.

Expected: all `ObjectCollisionBoxRegressionTests` pass, including the live
three-pan guest chain and table ownership assertions.

- [ ] **Step 6: Run the ownership and controller contract regressions**

Run:

```bash
/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity \
  -batchmode -nographics \
  -projectPath /home/hamza/dreadforge_2022_2 \
  -runTests -testPlatform EditMode \
  -testFilter "Chapter1GuestRoomVisibilityRegressionTests|SortingOwnershipAuditTests" \
  -testResults /tmp/chantilly-guest-order-contracts.xml \
  -logFile /tmp/chantilly-guest-order-contracts.log
```

Expected: PASS with no new sorting-owner conflicts. If Unity's filter does not
accept `|`, run the two classes separately with the same arguments.

- [ ] **Step 7: Inspect scope and commit**

Run:

```bash
git diff --check
git status --short
git diff -- Assets/Scripts/Characters/DiningRoomSeatedGuestOcclusionException.cs
git diff -- Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs
git diff -- Assets/Editor/ObjectCollisionBoxRegressionTests.cs
```

Confirm no scene, table, chair-authoring, shader, build, or executable files
were changed by this task. Preserve the pre-existing user modifications in
`Assets/Art/UI/Fonts/NotoSerifDisplay-Medium SDF.asset`,
`Assets/Scenes/Gameplay.unity`, and `ProjectSettings/ProjectSettings.asset`.

Commit:

```bash
git add \
  Assets/Scripts/Characters/DiningRoomSeatedGuestOcclusionException.cs \
  Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs
git commit -m "fix: preserve drawing room seated guest order"
```
