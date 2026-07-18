# Adjust Cursor Accuracy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make hover icons and clicks select the same highest-priority target, with reliable guest-coat and coat-hanger clickboxes.

**Architecture:** `NavigationCursorController` becomes a deterministic arbiter that retains all active hover requests and chooses by explicit priority. A focused Chapter 1 pointer resolver uses existing zoom-safe screen bounds to select coats before hanger/scene actions, while doors and floor movement yield. Every Chapter 1 callback funnels through one guarded action method with duplicate-frame suppression.

**Tech Stack:** Unity 6000.4.10f1, C#, Unity Input System/legacy input compatibility, NUnit EditMode tests.

## Global Constraints

- Priority is UI, guest coats/guest interactions, coat hanger/specific scene actions, doors/stairways, then floor movement.
- Preserve cursor art, movement destinations, story state, camera zoom, and character scaling.
- Use visible screen-space bounds as the authority for scaled Chapter 1 objects.
- Do not edit `Assets/Scenes/Gameplay.unity`.
- Write and run every regression before its production change.

---

### Task 1: Deterministic hover-request arbitration

**Files:**
- Modify: `Assets/Editor/NavigationRegressionTests.cs`
- Modify: `Assets/Map/CameraManager.cs:1970-2215`

**Interfaces:**
- Produces: `SetDoorHover(object owner, HoverIcon icon, int priority, bool active)`
- Produces: `IsPrimaryHoverOwner(object owner) : bool`
- Produces: `NavigationHoverPriority`, `SceneActionHoverPriority`, `GuestActionHoverPriority`, and `UiHoverPriority`

- [ ] **Step 1: Write the failing arbitration test**

Add this behavior test to `NavigationRegressionTests`:

```csharp
[Test]
public void CursorHoverArbitrationRestoresTheNextHighestPriorityOwner()
{
    MethodInfo reset = typeof(NavigationCursorController).GetMethod(
        "ResetForPlayMode",
        BindingFlags.NonPublic | BindingFlags.Static);
    MethodInfo setPrioritizedHover = typeof(NavigationCursorController).GetMethod(
        "SetDoorHover",
        BindingFlags.Public | BindingFlags.Static,
        null,
        new[] { typeof(object), typeof(NavigationCursorController.HoverIcon), typeof(int), typeof(bool) },
        null);
    MethodInfo isPrimaryOwner = typeof(NavigationCursorController).GetMethod(
        "IsPrimaryHoverOwner",
        BindingFlags.Public | BindingFlags.Static);
    FieldInfo navigationPriority = typeof(NavigationCursorController).GetField("NavigationHoverPriority");
    FieldInfo scenePriority = typeof(NavigationCursorController).GetField("SceneActionHoverPriority");
    FieldInfo guestPriority = typeof(NavigationCursorController).GetField("GuestActionHoverPriority");

    Assert.That(setPrioritizedHover, Is.Not.Null);
    Assert.That(isPrimaryOwner, Is.Not.Null);
    Assert.That(navigationPriority, Is.Not.Null);
    Assert.That(scenePriority, Is.Not.Null);
    Assert.That(guestPriority, Is.Not.Null);
    reset.Invoke(null, null);

    object door = new object();
    object hanger = new object();
    object coat = new object();

    setPrioritizedHover.Invoke(null, new[] { hanger, NavigationCursorController.HoverIcon.PlaceHangCoat, scenePriority.GetValue(null), true });
    setPrioritizedHover.Invoke(null, new[] { coat, NavigationCursorController.HoverIcon.PickUpCoat, guestPriority.GetValue(null), true });
    setPrioritizedHover.Invoke(null, new[] { door, NavigationCursorController.HoverIcon.Door, navigationPriority.GetValue(null), true });

    Assert.That((bool)isPrimaryOwner.Invoke(null, new[] { coat }), Is.True);
    NavigationCursorController.ClearDoorHover(coat);
    Assert.That((bool)isPrimaryOwner.Invoke(null, new[] { hanger }), Is.True);
    NavigationCursorController.ClearDoorHover(hanger);
    Assert.That((bool)isPrimaryOwner.Invoke(null, new[] { door }), Is.True);
}
```

- [ ] **Step 2: Run the test and verify RED**

```bash
"/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity" -batchmode \
  -projectPath "/home/hamza/dreadforge_2022_2_fix_coat_zoom_merge" \
  -runTests -testPlatform EditMode \
  -testFilter NavigationRegressionTests.CursorHoverArbitrationRestoresTheNextHighestPriorityOwner \
  -testResults /tmp/adjust-cursor-arbitration-red.xml \
  -logFile /tmp/adjust-cursor-arbitration-red.log
```

Expected: FAIL at the first missing reflected API assertion, proving the current one-owner implementation cannot satisfy the behavior.

- [ ] **Step 3: Implement retained hover requests**

Add a request record/list to `NavigationCursorController` and preserve the existing overloads:

```csharp
public const int NavigationHoverPriority = 100;
public const int SceneActionHoverPriority = 200;
public const int GuestActionHoverPriority = 300;
public const int UiHoverPriority = 400;

private sealed class HoverRequest
{
    public object Owner;
    public HoverIcon Icon;
    public int Priority;
    public long RegistrationOrder;
}

private static readonly List<HoverRequest> hoverRequests = new List<HoverRequest>();
private static long nextHoverRegistrationOrder;

public static void SetDoorHover(object owner, HoverIcon icon, int priority, bool active)
{
    if (!active)
    {
        ClearDoorHover(owner);
        return;
    }

    if (owner == null || (gameplayHoverBlocked && icon != HoverIcon.Ui))
    {
        return;
    }

    HoverRequest request = FindHoverRequest(owner);
    if (request == null)
    {
        request = new HoverRequest
        {
            Owner = owner,
            RegistrationOrder = nextHoverRegistrationOrder++
        };
        hoverRequests.Add(request);
    }

    request.Icon = icon;
    request.Priority = priority;
    ResolvePrimaryHoverRequest();
    ApplyCursor();
}

public static bool IsPrimaryHoverOwner(object owner)
{
    return owner != null && ReferenceEquals(doorHoverOwner, owner);
}
```

`ClearDoorHover` removes only that owner's request, resolves the next winner, and reapplies the cursor. `ResetForPlayMode` clears all requests. `SetGameplayHoverBlocked(true)` removes non-UI requests. Resolution ignores destroyed `UnityEngine.Object` owners and selects highest priority, then stable registration order.

- [ ] **Step 4: Run Step 2 again and verify GREEN**

Expected: one test passes with zero failures.

- [ ] **Step 5: Commit the arbiter**

```bash
git add Assets/Editor/NavigationRegressionTests.cs Assets/Map/CameraManager.cs
git commit -m "Fix cursor hover arbitration"
```

---

### Task 2: One Chapter 1 pointer target and click decision

**Files:**
- Create: `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1PointerPriority.cs`
- Create: `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1PointerPriority.cs.meta`
- Modify: `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1CoatPickup.cs`
- Modify: `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1SceneAction.cs`
- Modify: `Assets/Editor/Chapter1GuestRoomVisibilityRegressionTests.cs`

**Interfaces:**
- Produces: `TryGetTarget(Vector2 screenPosition, out MonoBehaviour target) : bool`
- Produces: `IsPointerOverAction(Vector2 screenPosition) : bool`
- Consumes Task 1's primary owner query and explicit priorities.

- [ ] **Step 1: Write failing Chapter 1 routing guards**

```csharp
[Test]
public void Chapter1PointerPriorityUsesOneCoatFirstTargetForHoverAndClick()
{
    const string priorityPath =
        "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1PointerPriority.cs";
    Assert.That(File.Exists(priorityPath), Is.True, "Chapter 1 needs one shared pointer-priority resolver.");

    string coatText = File.ReadAllText(Chapter1CoatPickupPath);
    string actionText = File.ReadAllText(Chapter1SceneActionPath);
    string priorityText = File.ReadAllText(priorityPath);

    Assert.That(priorityText, Does.Match(
        @"TryGetCoatAtScreenPosition[\s\S]*TryGetSceneActionAtScreenPosition"));
    Assert.That(coatText, Does.Contain("TryHandlePointerAction"));
    Assert.That(actionText, Does.Contain("TryHandlePointerAction"));
    Assert.That(coatText, Does.Contain("lastPointerActionFrame"));
    Assert.That(actionText, Does.Contain("lastPerformedFrame"));
    Assert.That(coatText, Does.Contain("GuestActionHoverPriority"));
    Assert.That(actionText, Does.Contain("SceneActionHoverPriority"));
    Assert.That(coatText, Does.Contain("IsPrimaryHoverOwner(this)"));
    Assert.That(actionText, Does.Contain("IsPrimaryHoverOwner(this)"));
}
```

- [ ] **Step 2: Run the new test and verify RED**

Expected: FAIL because `Chapter1PointerPriority.cs` and guarded action methods do not exist.

- [ ] **Step 3: Implement the shared coat-first target query**

```csharp
public static class Chapter1PointerPriority
{
    public static bool IsPointerOverAction(Vector2 screenPosition)
    {
        return TryGetTarget(screenPosition, out _);
    }

    public static bool TryGetTarget(Vector2 screenPosition, out MonoBehaviour target)
    {
        if (Chapter1CoatPickup.TryGetCoatAtScreenPosition(screenPosition, out Chapter1CoatPickup coat))
        {
            target = coat;
            return true;
        }

        if (Chapter1SceneAction.TryGetSceneActionAtScreenPosition(screenPosition, out Chapter1SceneAction action))
        {
            target = action;
            return true;
        }

        target = null;
        return false;
    }
}
```

Each static target query uses `FindObjectsByType` with inactive objects excluded, rejects disabled targets, and calls its existing screen-space bounds function.

- [ ] **Step 4: Funnel all target callbacks through one guard**

EventSystem callbacks, `OnMouseDown`, and manual polling call `TryHandlePointerAction(screenPosition, activate)`. It verifies blocking UI, asks the shared resolver for the selected target, registers the explicit cursor priority, and activates only if `IsPrimaryHoverOwner(this)` is true. Coat activation records `lastPointerActionFrame`; scene actions retain `lastPerformedFrame`.

```csharp
private void TryHandlePointerAction(Vector2 screenPosition, bool activate)
{
    bool selected = Chapter1PointerPriority.TryGetTarget(screenPosition, out MonoBehaviour target) &&
        target == this;
    SetCoatCursorHover(selected);

    if (!activate || !selected ||
        !NavigationCursorController.IsPrimaryHoverOwner(this) ||
        lastPointerActionFrame == Time.frameCount)
    {
        return;
    }

    lastPointerActionFrame = Time.frameCount;
    TryPickUp();
}
```

- [ ] **Step 5: Run the Chapter 1 regression and verify GREEN**

Expected: the new test and all existing `Chapter1GuestRoomVisibilityRegressionTests` pass.

- [ ] **Step 6: Commit Chapter 1 pointer ownership**

```bash
git add Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1PointerPriority.cs \
  Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1PointerPriority.cs.meta \
  Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1CoatPickup.cs \
  Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1SceneAction.cs \
  Assets/Editor/Chapter1GuestRoomVisibilityRegressionTests.cs
git commit -m "Route Chapter 1 pointer actions deterministically"
```

---

### Task 3: Make doors and floor movement yield to Chapter 1

**Files:**
- Modify: `Assets/Scripts/PointClickPlayerMovement.cs:519-548,1360-1390`
- Modify: `Assets/Scripts/Navigation/DoorTriggerNavigation.cs:170-230,847-902`
- Modify: `Assets/Editor/NavigationRegressionTests.cs`

**Interfaces:**
- Consumes: `Chapter1PointerPriority.IsPointerOverAction(Vector2 screenPosition)`

- [ ] **Step 1: Write failing priority-chain assertions**

```csharp
[Test]
public void Chapter1ActionsBeatDoorsAndFloorMovement()
{
    string movementText = File.ReadAllText(PointClickPlayerMovementPath);
    string doorText = File.ReadAllText(DoorTriggerNavigationPath);

    Assert.That(movementText, Does.Match(
        @"TryGetFloorClick[\s\S]*Chapter1PointerPriority\.IsPointerOverAction\(screenPosition\)[\s\S]*return false"));
    Assert.That(movementText, Does.Match(
        @"UpdateWalkCursor[\s\S]*Chapter1PointerPriority\.IsPointerOverAction\(screenPosition\)[\s\S]*ClearWalkHover"));
    Assert.That(doorText, Does.Match(
        @"UpdateFallbackPointerHoverAndClick[\s\S]*Chapter1PointerPriority\.IsPointerOverAction\(screenPosition\)[\s\S]*ClearActiveDoorHover"));
    Assert.That(doorText, Does.Match(
        @"OnPointerClick[\s\S]*Chapter1PointerPriority\.IsPointerOverAction\(eventData\.position\)[\s\S]*return"));
}
```

- [ ] **Step 2: Run the test and verify RED**

Expected: FAIL because movement and doors do not query Chapter 1.

- [ ] **Step 3: Add Chapter 1 deferral before navigation processing**

In floor-click and walk-cursor paths, return before movement evaluation when `Chapter1PointerPriority.IsPointerOverAction(screenPosition)` is true. In door pointer/fallback paths, clear door hover and return before selecting or activating a door.

```csharp
if (Chapter1PointerPriority.IsPointerOverAction(screenPosition))
{
    NavigationCursorController.ClearWalkHover(this);
    return;
}
```

- [ ] **Step 4: Run the test and verify GREEN**

Expected: PASS, and the existing Chapter 2 guest priority tests remain green.

- [ ] **Step 5: Commit cross-system guards**

```bash
git add Assets/Scripts/PointClickPlayerMovement.cs \
  Assets/Scripts/Navigation/DoorTriggerNavigation.cs \
  Assets/Editor/NavigationRegressionTests.cs
git commit -m "Prioritize Chapter 1 actions over navigation"
```

---

### Task 4: Guarantee sprite-sized coat and hanger clickboxes

**Files:**
- Modify: `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs:1658-1667,5227-5244`
- Modify: `Assets/Editor/Chapter1GuestRoomVisibilityRegressionTests.cs`

**Interfaces:**
- Consumes existing `GetCoatClickColliderSize` and `GetCoatHangerColliderSize`.

- [ ] **Step 1: Write failing collider guarantees**

Extract the coat creation and hanger-collider method bodies and assert both always assign size, offset, trigger state, and enabled state:

```csharp
Assert.That(coatSetupBody, Does.Match(
    @"collider\.size\s*=\s*GetCoatClickColliderSize[\s\S]*collider\.offset[\s\S]*collider\.isTrigger\s*=\s*true[\s\S]*collider\.enabled\s*=\s*true"));
Assert.That(hangerColliderBody, Does.Match(
    @"collider\.size\s*=\s*GetCoatHangerColliderSize[\s\S]*collider\.offset[\s\S]*collider\.isTrigger\s*=\s*true[\s\S]*collider\.enabled\s*=\s*true"));
```

- [ ] **Step 2: Run the test and verify RED**

Expected: FAIL because coat setup omits `enabled = true` and hanger sizing is conditional on a newly created collider.

- [ ] **Step 3: Configure both clickboxes unconditionally**

After ensuring each `BoxCollider2D` exists, always calculate and assign visible-sprite size and offset, mark it as a trigger, and enable it. Later story-state code remains responsible for disabling unavailable coat interaction.

- [ ] **Step 4: Run the test and verify GREEN**

Expected: PASS with no `Gameplay.unity` change.

- [ ] **Step 5: Commit clickbox guarantees**

```bash
git add Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs \
  Assets/Editor/Chapter1GuestRoomVisibilityRegressionTests.cs
git commit -m "Guarantee coat interaction clickboxes"
```

---

### Task 5: Integrated verification

**Files:**
- Verify only; modify production files only for a directly related failing regression.

- [ ] **Step 1: Run focused EditMode suites**

```bash
"/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity" -batchmode \
  -projectPath "/home/hamza/dreadforge_2022_2_fix_coat_zoom_merge" \
  -runTests -testPlatform EditMode \
  -testFilter 'NavigationRegressionTests;Chapter1GuestRoomVisibilityRegressionTests;Chapter2RegressionTests;CharacterAnimationArchitectureTests;CharacterPresentationOwnershipTests' \
  -testResults /tmp/adjust-cursor-accuracy-focused.xml \
  -logFile /tmp/adjust-cursor-accuracy-focused.log
```

Expected: all selected tests pass and the log contains no C# compiler errors.

- [ ] **Step 2: Verify file scope and scene preservation**

```bash
git diff --check main...HEAD
git diff --name-only main...HEAD
git status --short --branch
```

Expected: only the spec/plan, cursor arbitration, Chapter 1 pointer/collider, movement/door routing, and regression-test files appear. `Assets/Scenes/Gameplay.unity` is absent.

- [ ] **Step 3: Perform focused PlayMode smoke checks**

- Hover each offered guest coat at normal and zoomed room-stage scale: pick-up icon stays stable and one click starts the coat action.
- Carry a coat and hover the entrance hanger: place/hang icon stays stable and one click starts storage or the Butler approach.
- Without a carried coat, the hanger shows locked and does not become a floor click.
- Where coat/hanger areas overlap a door or walkable floor, the approved priority wins and the displayed icon matches the performed action.
- Hover ordinary doors, stairs, floor, Chapter 2 guests, and blocking UI to confirm existing behavior remains.

- [ ] **Step 4: Report results**

Summarize root cause, implementation, test counts/result paths, smoke coverage, branch/status, and how to diagnose the same issue next time.
