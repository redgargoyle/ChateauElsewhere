# Remove Old Tick Sound Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the procedural Grand Entrance Hall clock loop so the imported Victorian/antique catalog clip is the only continuous tick in that room.

**Architecture:** `ClockTickingAmbienceController`, created by `RoomNavigationManager`, remains the only continuous clock ambience owner. Retire the legacy `GrandfatherClockInteraction` lifecycle and the Chapter 1-only wiring that creates it; leave authored prop objects and the separate Chapter 2 clock-strike one-shot untouched.

**Tech Stack:** Unity 6000.4.10f1, C#, NUnit EditMode regression tests, YAML scene serialization, Git.

## Global Constraints

- Preserve `Assets/Audio/clock-ticking/12_distant_hall_clock_ticks_tangoflux_seed1221164_48khz.wav` (`45876613868b614ca83e9d719a3a2f63`) as the Grand Entrance Hall catalog assignment at base volume `0.18`.
- Retain `ClockTickingAmbienceController`, `ClockTickingAmbienceCatalog`, `RoomNavigationManager`, the authored `GrandfatherClock`/`GrandfatherClock_Optional` props, and the Chapter 2 non-looping clock strike.
- Remove, rather than mute or replace, every runtime reference to `GrandfatherClockInteraction` and `RuntimeGrandfatherClockTicking`.
- Do not add a second clock/audio system or alter unrelated room ambience, dialogue, gameplay, or scene layout.
- Do not close the user's open Unity editor; report a batch-test project-lock limitation if it prevents the focused run.

---

## File Structure

- `Assets/Editor/NavigationRegressionTests.cs`: source/scene ownership regression coverage.
- `Assets/Scripts/Story/GrandfatherClockInteraction.cs` and `.meta`: obsolete procedural tick and disabled close-up owner; both are deleted.
- `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs`: remove the legacy serialized field, fallback attachment, initialization, and clock-specific helper.
- `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1InteractionHUD.cs`: remove unused legacy clock parameters/state and stale debug button removal.
- `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1SceneAction.cs`: remove the retired action enum case, serialized reference, pointer action, and inspect hover path.
- `Assets/Scenes/Gameplay.unity`: delete only serialized null fields for the retired Chapter 1 clock reference/action field.

### Task 1: Retire the duplicate procedural clock owner

**Files:**
- Modify: `Assets/Editor/NavigationRegressionTests.cs:42-43, 175-212, 317-354, 611-622`
- Delete: `Assets/Scripts/Story/GrandfatherClockInteraction.cs`
- Delete: `Assets/Scripts/Story/GrandfatherClockInteraction.cs.meta`
- Modify: `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs:58-70, 3823-3842, 6388-6420`
- Modify: `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1InteractionHUD.cs:9-41, 126-140`
- Modify: `Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1SceneAction.cs:8-56, 157-177, 272-285, 462-477`
- Modify: `Assets/Scenes/Gameplay.unity:78844-78860, 103763-103780, 147734-147755`

**Interfaces:**
- Consumes: `ClockTickingAmbienceController.FindOrCreate(RoomNavigationManager)` and the existing `ClockTickingAmbienceCatalog` mapping for `Grand Entrance Hall`.
- Produces: no `GrandfatherClockInteraction` type, `RuntimeGrandfatherClockTicking` clip, or Chapter 1 runtime fallback; `Chapter1InteractionHUD.Initialize(Chapter1ArrivalController)` and `Chapter1SceneAction.Initialize(Chapter1SceneActionType, Chapter1ArrivalController)` are the resulting signatures.

- [ ] **Step 1: Write the failing regression test**

In `NavigationRegressionTests.cs`, remove the `GrandfatherClockInteractionPath` constant and change `AudioPlaybackGuardsAgainstDisabledSources` so it no longer reads or lists the retired script. Extend `ClockTickingAmbienceLoopsRandomTicksInClockRooms` immediately after the `catalogText` read with:

```csharp
const string grandEntranceHallTickGuid = "45876613868b614ca83e9d719a3a2f63";

Assert.That(catalogText, Does.Contain("- roomName: Grand Entrance Hall"));
Assert.That(
    catalogText,
    Does.Contain($"roomName: Grand Entrance Hall\\n    clip: {{fileID: 8300000, guid: {grandEntranceHallTickGuid}, type: 3}}"),
    "Grand Entrance Hall must keep the imported antique clock tick.");
```

Replace `GrandfatherClockCloseUpDoesNotOpenOverGameplay` with the following source-ownership test:

```csharp
[Test]
public void LegacyGrandfatherClockInteractionIsRetiredWithoutChangingCanonicalClockOwners()
{
    const string retiredClockInteractionPath = "Assets/Scripts/Story/GrandfatherClockInteraction.cs";
    const string retiredClockInteractionMetaPath = "Assets/Scripts/Story/GrandfatherClockInteraction.cs.meta";
    const string legacyClockGuid = "c6da9f56f65d9988ff5f7da0f8e59fb0";
    string chapter1ArrivalText = File.ReadAllText(Chapter1ArrivalControllerPath);
    string chapter1ActionText = File.ReadAllText(Chapter1SceneActionPath);
    string chapter1HudText = File.ReadAllText(Chapter1InteractionHUDPath);
    string navigationText = File.ReadAllText(NavigationManagerPath);
    string chapter2ControllerText = File.ReadAllText(Chapter2ControllerPath);
    string clockAmbienceText = File.ReadAllText(ClockTickingAmbienceControllerPath);
    string gameplayText = File.ReadAllText(GameplayScenePath);
    string drawingRoomPrefabText = File.ReadAllText("Assets/Prefabs/Room_Drawing_Room.prefab");
    string drawingRoomPerspectivePrefabText = File.ReadAllText("Assets/Prefabs/Room_Drawing_Room_Perspective.prefab");

    Assert.That(File.Exists(retiredClockInteractionPath), Is.False);
    Assert.That(File.Exists(retiredClockInteractionMetaPath), Is.False);

    foreach (string runtimeScriptPath in Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories))
    {
        string normalizedPath = runtimeScriptPath.Replace('\\', '/');
        if (normalizedPath.Contains("/Editor/"))
        {
            continue;
        }

        string runtimeText = File.ReadAllText(runtimeScriptPath);
        Assert.That(runtimeText, Does.Not.Contain("GrandfatherClockInteraction"), normalizedPath);
        Assert.That(runtimeText, Does.Not.Contain("RuntimeGrandfatherClockTicking"), normalizedPath);
        Assert.That(runtimeText, Does.Not.Contain("Canvas_GrandfatherClockCloseUp"), normalizedPath);
        Assert.That(runtimeText, Does.Not.Contain("Button_InspectClock"), normalizedPath);
    }

    Assert.That(chapter1ArrivalText, Does.Not.Contain("grandfatherClock"));
    Assert.That(chapter1ArrivalText, Does.Not.Contain("AddComponent<GrandfatherClockInteraction>"));
    Assert.That(chapter1ArrivalText, Does.Contain("interactionHUD.Initialize(this);"));
    Assert.That(chapter1ActionText, Does.Not.Contain("GrandfatherClock"));
    Assert.That(chapter1ActionText, Does.Contain("DrawingRoomExit = 3"));
    Assert.That(chapter1HudText, Does.Not.Contain("clockInteraction"));
    Assert.That(gameplayText, Does.Not.Contain("grandfatherClock:"));
    Assert.That(gameplayText, Does.Not.Contain("clockInteraction:"));
    Assert.That(gameplayText, Does.Not.Contain(legacyClockGuid));
    Assert.That(Regex.Matches(gameplayText, @"(?m)^  m_Name: GrandfatherClock$").Count, Is.EqualTo(1));
    Assert.That(Regex.Matches(gameplayText, @"(?m)^  m_Name: GrandfatherClock_Optional$").Count, Is.EqualTo(1));
    Assert.That(drawingRoomPrefabText, Does.Contain("m_Name: GrandfatherClock_Optional"));
    Assert.That(drawingRoomPerspectivePrefabText, Does.Contain("m_Name: GrandfatherClock_Optional"));
    Assert.That(navigationText, Does.Contain("clockTickingAmbienceController = ClockTickingAmbienceController.FindOrCreate(this);"));
    Assert.That(clockAmbienceText, Does.Contain("ControllerObjectName = \"Audio_ClockTickingAmbience\""));
    Assert.That(chapter2ControllerText, Does.Contain("[SerializeField] private AudioSource clockStrikeAudioSource;"));
    Assert.That(chapter2ControllerText, Does.Contain("clockStrikeAudioSource.loop = false"));
    Assert.That(chapter2ControllerText, Does.Contain("GameAudioSettings.TryPlay(clockStrikeAudioSource)"));
    Assert.That(gameplayText, Does.Contain("clockStrikeClockFaceSprite:"));
    Assert.That(clockAmbienceText, Does.Not.Contain("AudioClip.Create"));
}
```

- [ ] **Step 2: Run the focused test to verify it fails for the expected legacy owner**

Run:

```bash
/home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics -projectPath /home/hamzak/Desktop/ChateauChantilly -runTests -testPlatform EditMode -testFilter NavigationRegressionTests.LegacyGrandfatherClockInteractionIsRetiredWithoutChangingCanonicalClockOwners -testResults /tmp/remove-old-tick-red.xml -logFile /tmp/remove-old-tick-red.log
```

Expected: the test fails at `File.Exists(retiredClockInteractionPath)` because the procedural source still exists. If Unity returns a project-lock error because the user's editor is open, record that specific limitation and use the source assertions as the red-state evidence; do not close the editor.

- [ ] **Step 3: Delete the legacy owner and remove its Chapter 1 wiring**

Delete `GrandfatherClockInteraction.cs` and its `.meta`. Apply these exact interface reductions:

```csharp
// Chapter1ArrivalController: delete this serialized field.
[SerializeField] private GrandfatherClockInteraction grandfatherClock;

// Chapter1ArrivalController: retain only the surviving initializers.
doorbellSystem?.Initialize(chapterClock);
timeSettingsUI?.Initialize(chapterClock);
interactionHUD?.Initialize(this);
frontDoorSceneAction.Initialize(Chapter1SceneActionType.FrontDoor, this);
```

Delete the `grandfatherClock` lookup/fallback block and its now-unused `FindGameObjectByNormalizedName` helper from `ResolveStoryHelpers`. In `Chapter1InteractionHUD`, reduce the initializer to:

```csharp
public void Initialize(Chapter1ArrivalController controller)
{
    arrivalController = controller;
    EnsureUI();
}
```

Remove `clockInteraction`, `chapterClock`, and `Button_InspectClock` handling. In `Chapter1SceneAction`, reduce the enum and initializer to:

```csharp
public enum Chapter1SceneActionType
{
    FrontDoor = 0,
    CoatCloset = 1,
    DrawingRoomExit = 3
}

public void Initialize(Chapter1SceneActionType nextActionType, Chapter1ArrivalController controller)
{
    actionType = nextActionType;
    arrivalController = controller;
}
```

Remove only the `GrandfatherClock` switch case, `clockInteraction` field/resolution, and `HoverIcon.Inspect` branch. Keep `DrawingRoomExit = 3`, its switch case, initialization, and hover behavior intact so any serialized/external value remains stable. In `Gameplay.unity`, remove only these two serialized null fields:

```yaml
clockInteraction: {fileID: 0}
grandfatherClock: {fileID: 0}
```

There is one `clockInteraction` entry and one `grandfatherClock` entry; do not remove either authored clock GameObject.

- [ ] **Step 4: Run the focused regression and inspect the ownership boundary**

Run:

```bash
/home/hamzak/Unity/Hub/Editor/6000.4.10f1/Editor/Unity -batchmode -nographics -projectPath /home/hamzak/Desktop/ChateauChantilly -runTests -testPlatform EditMode -testFilter NavigationRegressionTests -testResults /tmp/remove-old-tick-green.xml -logFile /tmp/remove-old-tick-green.log
rg -n "GrandfatherClockInteraction|RuntimeGrandfatherClockTicking|Canvas_GrandfatherClockCloseUp|Button_InspectClock" Assets --glob '!Assets/Editor/**'
rg -n -A 2 "roomName: Grand Entrance Hall$" Assets/Resources/Audio/ClockTickingAmbienceCatalog.asset
git diff --check
```

Expected: Unity reports the fixture passing, the first `rg` returns no runtime hits, the catalog output retains GUID `45876613868b614ca83e9d719a3a2f63`, and `git diff --check` has no output. A project-lock error remains a recorded test-run limitation, not permission to close the editor.

- [ ] **Step 5: Commit the focused implementation**

Run:

```bash
git add Assets/Editor/NavigationRegressionTests.cs Assets/Scripts/Story/GrandfatherClockInteraction.cs Assets/Scripts/Story/GrandfatherClockInteraction.cs.meta Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1InteractionHUD.cs Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1SceneAction.cs Assets/Scenes/Gameplay.unity docs/superpowers/plans/2026-07-14-remove-old-tick-sound.md
git commit -m "fix(audio): retire legacy grandfather clock tick"
```

Expected: one focused implementation commit that contains the regression guard, legacy removal, Chapter 1 cleanup, scene-field cleanup, and this plan.
