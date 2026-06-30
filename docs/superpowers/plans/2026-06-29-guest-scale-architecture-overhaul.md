# Guest Scale Architecture Overhaul Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace overlapping guest size writers with one room-calibration-driven guest body scale system and simple editor tooling.

**Architecture:** `GuestRoomScaleCalibration` owns per-room guest multipliers and optional guest curves. `GuestScaleParticipant` marks the exact guest body root and captures authored base scale once. `GuestRoomScaleApplier` runs late and is the single final guest body-size writer; old Butler-rule guest scale paths become obsolete/no-op or non-authoritative.

**Tech Stack:** Unity C#, NUnit editor tests, existing `PointClickPlayerMovement` Butler room scale evaluator, existing Chapter 1 guest lifecycle.

## Global Constraints

- Start from latest `main` on branch `guest_scale_architecture_overhaul`.
- Do not change Butler movement.
- Do not change existing Butler room calibration values.
- Do not change guest art.
- Do not build another visual-height fitter as the primary solution.
- Do not add another giant debug tool with many primary buttons.
- Do not delete `RoomProjectedEntity`, `RoomPersonWalker2D`, or `ActorRoomState`.
- Do not let coats, speech bubbles, shadows, prompts, highlights, icons, cursors, or tooltips define guest body scale.
- Main `Guest Size Master` workflow exposes only five primary buttons.

---

### Task 1: Replace Old Regression Tests With New Contract Tests

**Files:**
- Modify: `Assets/Editor/GuestButlerScaleRegressionTests.cs`

**Interfaces:**
- Consumes: current test helpers in `GuestButlerScaleRegressionTests`.
- Produces: failing tests that define `GuestRoomScaleCalibration`, `GuestScaleParticipant`, `GuestRoomScaleApplier`, and legacy-obsolete contracts.

- [ ] **Step 1: Rename the test fixture purpose**

Replace `public sealed class GuestButlerScaleRegressionTests` with:

```csharp
public sealed class GuestRoomScaleRegressionTests
```

- [ ] **Step 2: Add path constants for new files**

Insert these constants near the existing path constants:

```csharp
private const string GuestRoomScaleCalibrationPath = "Assets/Scripts/Characters/GuestRoomScaleCalibration.cs";
private const string GuestScaleParticipantPath = "Assets/Scripts/Characters/GuestScaleParticipant.cs";
private const string GuestRoomScaleApplierPath = "Assets/Scripts/Characters/GuestRoomScaleApplier.cs";
private const string GuestPoseScaleOverrideStorePath = "Assets/Scripts/Characters/GuestPoseScaleOverrideStore.cs";
private const string GuestRoomScaleMasterWindowPath = "Assets/Editor/GuestRoomScaleMasterWindow.cs";
private const string GuestScaleAuditPath = "Assets/Editor/GuestScaleAudit.cs";
```

- [ ] **Step 3: Replace old harmonizer/tool expectations**

Delete tests that require `GuestButlerScaleHarmonizer` or `GuestButlerScaleTool` behavior, then add:

```csharp
[Test]
public void OldGuestButlerScaleHarmonizerIsRemovedOrObsolete()
{
    if (File.Exists(HarmonizerPath))
    {
        string text = File.ReadAllText(HarmonizerPath);
        Assert.That(text, Does.Contain("[Obsolete"));
        Assert.That(text, Does.Not.Contain("ApplyButlerCharacterScaleNow(source, debugGuestScaleMultiplier)"));
    }

    if (File.Exists(ToolPath))
    {
        string text = File.ReadAllText(ToolPath);
        Assert.That(text, Does.Contain("[Obsolete"));
        Assert.That(text, Does.Not.Contain("Refresh Guest Scaling Now"));
    }
}
```

- [ ] **Step 4: Add calibration tests**

Add tests:

```csharp
[Test]
public void GuestRoomScaleCalibrationInitializesFromButler()
{
    GameObject butlerObject = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
    GameObject calibrationObject = new GameObject("GuestScaleCalibration");

    try
    {
        PointClickPlayerMovement butler = butlerObject.GetComponent<PointClickPlayerMovement>();
        butler.CaptureCurrentTransformAsButlerCalibrationBaseScale();
        butler.SetButlerFrontFinalLocalScaleForRoom("Grand Entrance Hall", -100f, 2f, false);
        butler.SetButlerBackFinalLocalScaleForRoom("Drawing_Room", 100f, 1f, false);

        GuestRoomScaleCalibration calibration = calibrationObject.AddComponent<GuestRoomScaleCalibration>();
        calibration.InitializeMissingRoomsFromButler(butler);

        Assert.That(calibration.TryGetRoom("Grand Entrance Hall", out _), Is.True);
        Assert.That(calibration.TryGetRoom("Drawing Room", out _), Is.True);
        Assert.That(calibration.Rooms.Count, Is.EqualTo(2));
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(calibrationObject);
        UnityEngine.Object.DestroyImmediate(butlerObject);
    }
}

[Test]
public void GuestRoomScaleCalibrationEvaluatesButlerCurveWithRoomMultiplier()
{
    GameObject butlerObject = CreatePointClickPlayer("player", new Vector3(2f, 2f, 1f));
    GameObject calibrationObject = new GameObject("GuestScaleCalibration");

    try
    {
        PointClickPlayerMovement butler = butlerObject.GetComponent<PointClickPlayerMovement>();
        butler.CaptureCurrentTransformAsButlerCalibrationBaseScale();
        butler.SetButlerFrontFinalLocalScaleForRoom("Grand Entrance Hall", -100f, 2f, false);
        butler.SetButlerBackFinalLocalScaleForRoom("Grand Entrance Hall", 100f, 1f, false);

        GuestRoomScaleCalibration calibration = calibrationObject.AddComponent<GuestRoomScaleCalibration>();
        calibration.InitializeMissingRoomsFromButler(butler);
        calibration.SetButlerScaleSource(butler);
        calibration.SetRoomMultiplier("grand_entrance-hall", 1.25f);

        Assert.That(calibration.TryEvaluateGuestScale("Grand Entrance Hall", 0f, out float scale, out float depth01, out string diagnostic), Is.True, diagnostic);
        Assert.That(depth01, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(scale, Is.EqualTo(0.75f * 1.25f).Within(0.0001f));
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(calibrationObject);
        UnityEngine.Object.DestroyImmediate(butlerObject);
    }
}
```

- [ ] **Step 5: Add participant/applier tests**

Add tests covering base capture, prefab root scaling, walker graphic scaling, projected visual root scaling, coat ignore, entrance room multiplier, and seated pose ratio using real `GameObject` instances. Each test should:

```csharp
GuestScaleParticipant participant = guest.AddComponent<GuestScaleParticipant>();
participant.SetRoomIdOverride("Grand Entrance Hall");
participant.SetScaleRoot(guest.transform);
participant.CaptureBaseScale(true);
GuestRoomScaleApplier applier = applierObject.AddComponent<GuestRoomScaleApplier>();
applier.RefreshAllNow();
```

Expected assertions:

```csharp
Assert.That(guest.transform.localScale.y, Is.EqualTo(expectedY).Within(0.0001f));
Assert.That(coat.transform.localScale, Is.EqualTo(authoredCoatScale));
```

- [ ] **Step 6: Add editor-tool text contract tests**

Add tests:

```csharp
[Test]
public void GuestSizeMasterHasSimplePrimaryWorkflow()
{
    string text = File.ReadAllText(GuestRoomScaleMasterWindowPath);
    Assert.That(text, Does.Contain("Guest Size Master"));
    Assert.That(text, Does.Contain("Guest Size In This Room"));
    Assert.That(text, Does.Contain("SET UP GUEST SCALING"));
    Assert.That(text, Does.Contain("PREVIEW ROOM GUEST SIZE"));
    Assert.That(text, Does.Contain("SAVE ROOM GUEST SIZE"));
    Assert.That(text, Does.Contain("APPLY TO ALL GUESTS IN ROOM"));
    Assert.That(text, Does.Contain("SAVE SCENE"));
}

[Test]
public void DebugButtonsAreAdvancedOnly()
{
    string text = File.ReadAllText(GuestRoomScaleMasterWindowPath);
    Assert.That(text, Does.Contain("advancedFoldout"));
    Assert.That(text.IndexOf("Proof shrink", StringComparison.Ordinal), Is.GreaterThan(text.IndexOf("advancedFoldout", StringComparison.Ordinal)));
}
```

- [ ] **Step 7: Run tests to verify RED**

Run:

```bash
dotnet build Assembly-CSharp-Editor.csproj
```

Expected: FAIL because `GuestRoomScaleCalibration`, `GuestScaleParticipant`, `GuestRoomScaleApplier`, editor tool files, and new APIs do not exist yet.

### Task 2: Add Runtime Guest Scale Core

**Files:**
- Create: `Assets/Scripts/Characters/GuestRoomScaleCalibration.cs`
- Create: `Assets/Scripts/Characters/GuestScaleParticipant.cs`
- Create: `Assets/Scripts/Characters/GuestRoomScaleApplier.cs`
- Create: `Assets/Scripts/Characters/GuestPoseScaleOverrideStore.cs`

**Interfaces:**
- Produces: `GuestRoomScaleCalibration.GetOrCreateRoom`, `TryGetRoom`, `TryEvaluateGuestScale`, `SetRoomMultiplier`, `SetFront`, `SetBack`, `RemoveRoom`, `InitializeMissingRoomsFromButler`.
- Produces: `GuestScaleParticipant.ResolveScaleRoot`, `ResolveRoomId`, `ResolveRoomLocalY`, `CaptureBaseScale`, `ApplyFinalScale`.
- Produces: `GuestRoomScaleApplier.EnsureInScene`, `RefreshAllNow`, `RefreshParticipantNow`, `EnsureParticipantsForSceneGuests`.

- [ ] **Step 1: Implement `GuestRoomScaleCalibration`**

Create a scene component with:

```csharp
[DisallowMultipleComponent]
public sealed class GuestRoomScaleCalibration : MonoBehaviour
{
    [SerializeField] private bool enableGuestRoomScaling = true;
    [SerializeField] private PointClickPlayerMovement butlerScaleSource;
    [SerializeField] private List<GuestRoomScaleEntry> rooms = new List<GuestRoomScaleEntry>();

    public bool EnableGuestRoomScaling => enableGuestRoomScaling;
    public IReadOnlyList<GuestRoomScaleEntry> Rooms => rooms;
}
```

Room normalization removes spaces, underscores, hyphens, and trims before case-insensitive comparison.

- [ ] **Step 2: Implement Butler curve evaluation**

In `TryEvaluateGuestScale`, use:

```csharp
if (entry.useCustomGuestCurve && entry.HasCompleteCustomCurve)
{
    depth01 = Mathf.InverseLerp(entry.frontRoomLocalY, entry.backRoomLocalY, roomLocalY);
    scale = Mathf.Lerp(entry.frontGuestScale, entry.backGuestScale, Mathf.Clamp01(depth01));
}
else if (entry.useButlerRoomCurve && ResolveButlerScaleSource() != null &&
    ResolveButlerScaleSource().TryEvaluateButlerCharacterScale(entry.roomId, new Vector2(0f, roomLocalY), out PointClickPlayerMovement.ButlerCharacterScaleSample sample))
{
    depth01 = sample.Depth01;
    scale = sample.NormalizedScale;
}
else
{
    depth01 = 0f;
    scale = 1f;
}

scale *= Mathf.Max(0.001f, entry.roomGuestScaleMultiplier);
```

- [ ] **Step 3: Implement `GuestScaleParticipant`**

Use enum:

```csharp
public enum CharacterPose
{
    Auto,
    Standing,
    Seated,
    Crouching,
    Lying
}
```

Base capture must sanitize zero components and store exactly once unless forced.

- [ ] **Step 4: Implement body-root exclusion**

Use exact helper:

```csharp
public static bool NameLooksExcludedFromBodyScale(string value)
{
    if (string.IsNullOrWhiteSpace(value)) return false;
    string[] tokens = { "coat", "coatcutout", "jacket", "cloak", "shawl", "speech", "thought", "bubble", "prompt", "highlight", "icon", "shadow", "cursor", "tooltip" };
    for (int i = 0; i < tokens.Length; i++)
    {
        if (value.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
    }
    return false;
}
```

- [ ] **Step 5: Implement `GuestRoomScaleApplier`**

Use:

```csharp
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class GuestRoomScaleApplier : MonoBehaviour
```

`LateUpdate` runs only in play mode. `RefreshAllNow()` works in edit mode and play mode.

- [ ] **Step 6: Implement pose ratios**

Use:

```csharp
Standing = 1f
Seated = seatedRatioOverride > 0f ? Mathf.Clamp(seatedRatioOverride, 0.55f, 0.80f) : 0.68f
Crouching = 0.75f
Lying = 0.45f
Auto = ActorRoomState.IsSeated ? Seated : Standing
```

- [ ] **Step 7: Implement optional pose override store**

`GuestPoseScaleOverrideStore` stores room id, character id, pose, pose ratio, and fine tune multiplier. The applier consults it when present, but normal standing guests do not require entries.

- [ ] **Step 8: Run tests to verify GREEN for runtime core**

Run:

```bash
dotnet build Assembly-CSharp-Editor.csproj
```

Expected: runtime-type errors are gone; editor tool and legacy deprecation tests may still fail.

### Task 3: Deactivate Legacy Final Scale Writers

**Files:**
- Modify or delete: `Assets/Scripts/Characters/GuestButlerScaleHarmonizer.cs`
- Modify or delete: `Assets/Editor/GuestButlerScaleTool.cs`
- Modify: `Assets/Scripts/Characters/RoomProjectedEntity.cs`
- Modify: `Assets/Scripts/Characters/RoomPersonWalker2D.cs`
- Modify: `Assets/Scripts/Story/ActorRoomState.cs`
- Modify: `Assets/Scripts/Characters/README.md`

**Interfaces:**
- Consumes: new `GuestScaleParticipant`.
- Produces: old Butler-rule guest scale paths are obsolete/no-op and the new applier is the only participant body-scale writer.

- [ ] **Step 1: Replace harmonizer with obsolete stub or remove file**

Preferred: remove `GuestButlerScaleHarmonizer.cs` and its `.meta` if no compile references remain. If compile references remain, replace with:

```csharp
[Obsolete("GuestButlerScaleHarmonizer has been replaced by GuestRoomScaleApplier.")]
public sealed class GuestButlerScaleHarmonizer : MonoBehaviour
{
    public void SetButlerScaleSource(PointClickPlayerMovement source) { }
    public void SetDebugGuestScaleMultiplier(float multiplier) { }
    public GuestScaleApplySummary RefreshNow() => GuestScaleApplySummary.Empty;
}
```

- [ ] **Step 2: Replace old editor tool with obsolete menu stub or remove file**

Preferred: remove `GuestButlerScaleTool.cs` and its `.meta`. If compile references remain, keep an obsolete window that directs users to `Tools > Characters > Guest Size Master`.

- [ ] **Step 3: Gate `RoomProjectedEntity.ApplyProjectedScale`**

If `GetComponentInParent<GuestScaleParticipant>(true)` exists and uses this entity's `VisualRoot`, `ApplyProjectedScale` must return before writing `VisualRoot.localScale`. Keep position, tint, sorting, and shadow.

- [ ] **Step 4: Mark Butler-rule APIs obsolete/no-op**

For `RoomProjectedEntity`, `RoomPersonWalker2D`, and `ActorRoomState`, add `[Obsolete("Guest body scale is now applied by GuestRoomScaleApplier.")]` to old Butler-scale APIs and make final-scale methods no-op when a participant is active.

- [ ] **Step 5: Remove walker near/far as participant final size**

When a `RoomPersonWalker2D` has a `GuestScaleParticipant`, `ApplyVisuals` keeps anchored position, tint, raycast target, and animator parameters, but does not write the participant scale root's final body scale.

- [ ] **Step 6: Prevent ActorRoomState room-stage scale from participant body size**

When `actorObject` has a `GuestScaleParticipant`, skip `targetTransform.localScale = ScaleXY(...)` in room-stage motion code. Position correction stays.

- [ ] **Step 7: Run tests**

Run:

```bash
dotnet build Assembly-CSharp-Editor.csproj
```

Expected: legacy contract tests pass; editor tool tests may still fail until Task 4.

### Task 4: Add Audit And Master Editor Tools

**Files:**
- Create: `Assets/Editor/GuestScaleAudit.cs`
- Create: `Assets/Editor/GuestRoomScaleMasterWindow.cs`

**Interfaces:**
- Produces menu `Tools > Characters > Guest Scale Audit`.
- Produces report `Assets/Editor/Reports/GuestScaleAudit.md`.
- Produces menu `Tools > Characters > Guest Size Master`.

- [ ] **Step 1: Implement audit menu**

`GuestScaleAudit.RunAndWriteReport()` writes markdown with the exact summary keys:

```text
Butler room calibrations found: X
Guest prefab instances found: X
RoomPersonWalker2D guests found: X
RoomProjectedEntity FloorCharacters found: X
RoomProjectedEntity props/furniture found: X
Non-empty roomVisualScaleOverrides found: X
Coat visuals found under guests: X
Active guest scale writers found: X
```

- [ ] **Step 2: Implement master window primary UI**

Top-level UI shows status lines, room dropdown, `Guest Size In This Room` slider, and exactly these primary buttons:

```text
SET UP GUEST SCALING
PREVIEW ROOM GUEST SIZE
SAVE ROOM GUEST SIZE
APPLY TO ALL GUESTS IN ROOM
SAVE SCENE
```

- [ ] **Step 3: Implement setup action**

`SET UP GUEST SCALING` finds the Butler, ensures `GuestRoomScaleCalibration`, ensures `GuestRoomScaleApplier`, initializes rooms, finds guests, adds participants, captures base scales, previews, and logs a summary.

- [ ] **Step 4: Implement advanced foldout**

Only inside `advancedFoldout`, expose audit, custom front/back guest curve save actions, reset selected room multiplier, proof shrink/grow, and emergency restore captured base scales.

- [ ] **Step 5: Run tests**

Run:

```bash
dotnet build Assembly-CSharp-Editor.csproj
```

Expected: editor-tool text contract tests pass.

### Task 5: Integrate Chapter 1 Guest Lifecycle

**Files:**
- Modify: `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs`

**Interfaces:**
- Consumes: `GuestRoomScaleApplier.EnsureInScene`, `GuestRoomScaleApplier.EnsureParticipantForGuestObject`, `GuestRoomScaleApplier.RefreshAllNow`.
- Produces: entrance and drawing-room guests get participants and scaling refresh after placement, coat pickup, coat storage, and seated pose changes.

- [ ] **Step 1: Add participant field**

Add to `GuestRuntimeState`:

```csharp
public GuestScaleParticipant ScaleParticipant;
```

- [ ] **Step 2: Ensure runtime scaling when states are created**

Inside `ResetGuestStates`, after `GuestObject`, `ActorState`, and `Projection` are known:

```csharp
runtimeState.ScaleParticipant = EnsureGuestScaleParticipant(runtimeState, entryRoomId, CharacterPose.Standing);
RefreshGuestScalingNow();
```

- [ ] **Step 3: Refresh after entrance placement**

After each `PlaceGuestAtDoorArrival`, `PlaceGuestAt`, or `MoveGuestTo` completion in entrance admission, call:

```csharp
EnsureGuestScaleParticipant(guest, entryRoomId, CharacterPose.Standing);
RefreshGuestScalingNow();
```

- [ ] **Step 4: Refresh after coat transfer and storage**

After `TransferCoatVisualToButler(guestState)` and after `StoreCarriedCoatInCloset()` clears `carriedCoatVisual`, call:

```csharp
RefreshGuestScalingNow();
```

- [ ] **Step 5: Refresh after drawing room seating**

After `ApplyDrawingRoomWaitingPose(guest)` and after `StageGuestInDrawingRoomForChapter2`, call:

```csharp
EnsureGuestScaleParticipant(guest, drawingRoomId, CharacterPose.Seated);
RefreshGuestScalingNow();
```

- [ ] **Step 6: Add helper methods**

Add private helpers:

```csharp
private GuestScaleParticipant EnsureGuestScaleParticipant(GuestRuntimeState guest, string roomId, CharacterPose pose)
private GuestRoomScaleApplier EnsureGuestScaleApplier()
private void RefreshGuestScalingNow()
```

These helpers must not touch the Butler and must capture base scale before coat visual reparenting can influence the body root.

- [ ] **Step 7: Run tests**

Run:

```bash
dotnet build Assembly-CSharp-Editor.csproj
```

Expected: Chapter 1 lifecycle references compile.

### Task 6: Final Verification, Audit Report, Commit, Push

**Files:**
- Modify: `Assets/Scripts/Characters/README.md`
- Generate: `Assets/Editor/Reports/GuestScaleAudit.md`
- Include: `docs/superpowers/specs/2026-06-29-guest-scale-architecture-overhaul-design.md`
- Include: `docs/superpowers/plans/2026-06-29-guest-scale-architecture-overhaul.md`

**Interfaces:**
- Produces: documentation for using the new master tool and a generated audit report.

- [ ] **Step 1: Update README**

Replace references to `GuestButlerScaleHarmonizer` and old tool steps with five steps:

```text
Tools > Characters > Guest Size Master
SET UP GUEST SCALING
Choose a room
Adjust Guest Size In This Room
PREVIEW ROOM GUEST SIZE / SAVE ROOM GUEST SIZE / SAVE SCENE
```

- [ ] **Step 2: Generate audit report**

Run Unity editor menu if available. If not available in shell, leave the code capable of generating `Assets/Editor/Reports/GuestScaleAudit.md` and report that the report must be generated from Unity.

- [ ] **Step 3: Run build verification**

Run:

```bash
dotnet build dreadforge_2022.sln
dotnet build Assembly-CSharp-Editor.csproj
git diff --check
```

Expected: all commands pass.

- [ ] **Step 4: Commit coherent branch**

Run:

```bash
git status --short
git add Assets/Scripts/Characters/GuestRoomScaleCalibration.cs \
        Assets/Scripts/Characters/GuestScaleParticipant.cs \
        Assets/Scripts/Characters/GuestRoomScaleApplier.cs \
        Assets/Scripts/Characters/GuestPoseScaleOverrideStore.cs \
        Assets/Editor/GuestScaleAudit.cs \
        Assets/Editor/GuestRoomScaleMasterWindow.cs \
        Assets/Editor/GuestButlerScaleRegressionTests.cs \
        Assets/Scripts/Characters/RoomProjectedEntity.cs \
        Assets/Scripts/Characters/RoomPersonWalker2D.cs \
        Assets/Scripts/Story/ActorRoomState.cs \
        Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs \
        Assets/Scripts/Characters/README.md \
        docs/superpowers/specs/2026-06-29-guest-scale-architecture-overhaul-design.md \
        docs/superpowers/plans/2026-06-29-guest-scale-architecture-overhaul.md
git add -u Assets/Scripts/Characters/GuestButlerScaleHarmonizer.cs Assets/Editor/GuestButlerScaleTool.cs
git commit -m "Overhaul guest room scale calibration"
```

- [ ] **Step 5: Push branch**

Run:

```bash
git push -u origin guest_scale_architecture_overhaul
```

- [ ] **Step 6: Final response**

Include latest main commit used, audit counts if generated, whether old harmonizer/tool were removed or deprecated, how to use Guest Size Master in five steps, commit hash, push status, and build status.
