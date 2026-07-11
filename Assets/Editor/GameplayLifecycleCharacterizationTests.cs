using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class GameplayLifecycleCharacterizationTests
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string GameplaySceneName = "Gameplay";
    private const string EntranceRoom = "Grand Entrance Hall";
    private const string DrawingRoom = "Drawing Room";

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Selection.activeObject = null;
        EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (EditorApplication.isPlaying)
        {
            yield return new ExitPlayMode();
        }

        Selection.activeObject = null;
    }

    [UnityTest]
    public IEnumerator MainMenuNewGameBootsGameplayAndNavigatesEntranceRoundTrip()
    {
        MainMenuController menu = RequireExactlyOneInActiveScene<MainMenuController>();
        menu.NewGame();
        yield return null;

        GameObject cursorChoice = GameObject.Find("Button_CursorStyle_01");
        Assert.That(cursorChoice, Is.Not.Null, "New Game must expose the cursor-style chooser.");
        Button cursorButton = cursorChoice.GetComponent<Button>();
        Assert.That(cursorButton, Is.Not.Null);
        cursorButton.onClick.Invoke();

        yield return WaitForSettledLayout();

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameplaySceneName));

        Chateau.Architecture.GameRoot gameRoot = RequireExactlyOneInActiveScene<Chateau.Architecture.GameRoot>();
        CameraManager cameraManager = RequireExactlyOneInActiveScene<CameraManager>();
        RoomNavigationManager navigation = RequireExactlyOneInActiveScene<RoomNavigationManager>();
        DoorPromptSequenceController prompts = RequireExactlyOneInActiveScene<DoorPromptSequenceController>();
        ChapterManager chapter = RequireExactlyOneInActiveScene<ChapterManager>();
        ChapterClock clock = RequireExactlyOneInActiveScene<ChapterClock>();
        ChapterEventScheduler scheduler = RequireExactlyOneInActiveScene<ChapterEventScheduler>();
        ChapterIntroUI intro = RequireExactlyOneInActiveScene<ChapterIntroUI>();
        Chapter1ArrivalController arrival = RequireExactlyOneInActiveScene<Chapter1ArrivalController>();
        Chapter1InteractionHUD chapter1Hud = RequireExactlyOneInActiveScene<Chapter1InteractionHUD>();
        Chapter2Controller serializedChapter2 = RequireExactlyOneInActiveScene<Chapter2Controller>();
        Chapter2InteractionHUD serializedChapter2Hud = RequireExactlyOneInActiveScene<Chapter2InteractionHUD>();
        Chapter2MonsterStingerController serializedMonsterStinger = RequireExactlyOneInActiveScene<Chapter2MonsterStingerController>();
        Chapter2GuestPanicController serializedGuestPanic = RequireExactlyOneInActiveScene<Chapter2GuestPanicController>();
        Chapter2GuestSearchController serializedGuestSearch = RequireExactlyOneInActiveScene<Chapter2GuestSearchController>();
        GuestRoomScaleApplier serializedGuestScaleApplier = RequireExactlyOneInActiveScene<GuestRoomScaleApplier>();
        GuestRoomScaleCalibration serializedGuestScaleCalibration = RequireExactlyOneInActiveScene<GuestRoomScaleCalibration>();
        GuestVoiceLinePlayback serializedVoicePlayback = RequireExactlyOneInActiveScene<GuestVoiceLinePlayback>();
        SpeakingCharacterIndicator serializedSpeakingIndicator = RequireExactlyOneInActiveScene<SpeakingCharacterIndicator>();
        RuntimeSettingsMenu runtimeSettings = RequireExactlyOneInActiveScene<RuntimeSettingsMenu>();
        FireplaceAmbienceController fireplaceAmbience = RequireExactlyOneInActiveScene<FireplaceAmbienceController>();
        ClockTickingAmbienceController clockAmbience = RequireExactlyOneInActiveScene<ClockTickingAmbienceController>();
        RoomLightingController lighting = RequireExactlyOneInActiveScene<RoomLightingController>();
        RoomProjectedEntity teaTableProjection = FindInActiveScene<RoomProjectedEntity>()
            .Single(item => item.gameObject.name == "tea_service_table");
        SpriteRenderer teaTableRenderer = teaTableProjection.GetComponent<SpriteRenderer>();
        ObjectMovementBlocker2D teaTableBlocker = FindInActiveScene<ObjectMovementBlocker2D>()
            .Single(item => item.SourceObjectName == "tea_service_table");
        PolygonCollider2D teaTableCollider = teaTableBlocker.BlockingCollider as PolygonCollider2D;

        Assert.That(cameraManager, Is.Not.Null);
        Assert.That(prompts, Is.Not.Null);
        Assert.That(lighting, Is.Not.Null);
        Assert.That(chapter.Clock, Is.SameAs(clock));
        Assert.That(chapter.EventScheduler, Is.SameAs(scheduler));
        SubtitleService subtitle = RequireExactlyOneInActiveScene<SubtitleService>();
        DialogueSpeechService speech = RequireExactlyOneInActiveScene<DialogueSpeechService>();
        Assert.That(subtitle.IsInitialized, Is.True);
        Assert.That(subtitle.HasGameContext, Is.True);
        Assert.That(speech.IsInitialized, Is.True);
        Assert.That(speech.HasGameContext, Is.True);
        Assert.That(chapter1Hud.gameObject, Is.SameAs(arrival.gameObject));
        Canvas chapter1Canvas = FindInActiveScene<Canvas>().Single(item => item.name == "Canvas_Chapter1HUD");
        Assert.That(chapter1Canvas.sortingOrder, Is.EqualTo(9100));
        Assert.That(FindInActiveScene<Transform>().Count(item => item.name == "Text_Chapter1Status"), Is.EqualTo(1));
        Canvas settingsCanvas = FindInActiveScene<Canvas>().Single(item => item.name == "Canvas_RuntimeSettingsMenu");
        Assert.That(settingsCanvas.sortingOrder, Is.EqualTo(10050));
        Assert.That(settingsCanvas.transform.localScale, Is.Not.EqualTo(Vector3.zero));
        Button settingsButton = FindInActiveScene<Button>().Single(item => item.name == "Button_Settings");
        Assert.That(RuntimeSettingsMenu.BlocksGameInput, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        settingsButton.onClick.Invoke();
        yield return null;
        Assert.That(RuntimeSettingsMenu.BlocksGameInput, Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        settingsButton.onClick.Invoke();
        yield return null;
        Assert.That(RuntimeSettingsMenu.BlocksGameInput, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        AudioSource fireplaceSource = fireplaceAmbience.GetComponent<AudioSource>();
        AudioSource clockSource = clockAmbience.GetComponent<AudioSource>();
        Assert.That(fireplaceSource, Is.Not.Null);
        Assert.That(clockSource, Is.Not.Null);
        Assert.That(clockSource, Is.Not.SameAs(fireplaceSource));
        Assert.That(clockAmbience.gameObject, Is.Not.SameAs(fireplaceAmbience.gameObject));
        Assert.That(fireplaceSource.playOnAwake, Is.False);
        Assert.That(clockSource.playOnAwake, Is.False);
        Assert.That(fireplaceSource.loop, Is.True);
        Assert.That(clockSource.loop, Is.True);
        Assert.That(fireplaceSource.spatialBlend, Is.Zero);
        Assert.That(clockSource.spatialBlend, Is.Zero);
        Assert.That(fireplaceSource.ignoreListenerVolume, Is.True);
        Assert.That(clockSource.ignoreListenerVolume, Is.True);
        Assert.That(fireplaceSource.clip, Is.Not.Null, "The entrance should resolve its authored fireplace ambience clip.");
        Assert.That(clockSource.clip, Is.Not.Null, "The entrance should resolve its authored clock ambience clip.");
        AudioHighPassFilter fireplaceFilter = fireplaceAmbience.GetComponent<AudioHighPassFilter>();
        Assert.That(fireplaceFilter, Is.Not.Null);
        Assert.That(fireplaceFilter.enabled, Is.True);
        Assert.That(fireplaceFilter.cutoffFrequency, Is.GreaterThanOrEqualTo(200f));
        Assert.That(fireplaceFilter.highpassResonanceQ, Is.InRange(0.1f, 10f));
        Assert.That(
            clockAmbience.GetComponents<AudioHighPassFilter>().Any(filter => filter.enabled),
            Is.False,
            "Clock ambience must not inherit an enabled high-pass filter.");
        Assert.That(
            clockAmbience.GetComponents<AudioLowPassFilter>().Any(filter => filter.enabled),
            Is.False,
            "Clock ambience must not inherit an enabled low-pass filter.");
        Assert.That(teaTableProjection.gameObject.activeInHierarchy, Is.False, "The Drawing Room set piece starts inactive with its room.");
        Assert.That(teaTableRenderer, Is.Not.Null);
        Assert.That(teaTableBlocker.SourceObject, Is.SameAs(teaTableProjection.gameObject));
        Assert.That(teaTableBlocker.SortSourceRenderers, Is.True, "Baseline intentionally records the blocker's competing sort writer.");
        Assert.That(teaTableCollider, Is.Not.Null);
        Assert.That(teaTableCollider.isTrigger, Is.True);
        Assert.That(teaTableProjection.Mode, Is.EqualTo(RoomProjectedEntity.ProjectionMode.ForegroundOccluder));
        Assert.That(teaTableProjection.RoomLocalFootPoint, Is.EqualTo(new Vector2(-80.26f, -211.67f)));
        Assert.That(teaTableProjection.RoomProfile, Is.Not.Null);
        Assert.That(teaTableProjection.RoomProfile.GetSortingOrder(teaTableProjection.RoomLocalFootPoint), Is.EqualTo(6627));
        Assert.That(teaTableRenderer.sortingOrder, Is.EqualTo(6627));
        Assert.That(teaTableRenderer.sortingLayerName, Is.EqualTo("People"));
        Assert.That(teaTableRenderer.spriteSortPoint, Is.EqualTo(SpriteSortPoint.Pivot));
        Assert.That(teaTableProjection.transform.localPosition.x, Is.EqualTo(-80.26f).Within(0.001f));
        Assert.That(teaTableProjection.transform.localPosition.y, Is.EqualTo(-211.67f).Within(0.001f));
        Assert.That(teaTableProjection.transform.localPosition.z, Is.Zero, "RoomContentGroup currently flattens room art at runtime; the authored Z remains covered statically.");
        Assert.That(teaTableProjection.transform.localScale.x, Is.EqualTo(99.52793f).Within(0.0001f));
        Assert.That(teaTableProjection.transform.localScale.y, Is.EqualTo(99.40213f).Within(0.0001f));
        Assert.That(teaTableProjection.transform.localScale.z, Is.EqualTo(73.00117f).Within(0.0001f));
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(teaTableRenderer.sprite)), Is.EqualTo("c9c9711a41d82097fbae9cb69d6b7e6d"));
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(teaTableRenderer.sharedMaterial)), Is.EqualTo("a97c105638bdf8b4a8650670310a4cd3"));
        Vector2[] teaTableFootprint = teaTableCollider.points;
        Assert.That(teaTableFootprint, Has.Length.EqualTo(4));
        Assert.That(teaTableFootprint[0], Is.EqualTo(new Vector2(-214.44357f, -357.79114f)));
        Assert.That(teaTableFootprint[1], Is.EqualTo(new Vector2(53.923557f, -357.79114f)));
        Assert.That(teaTableFootprint[2], Is.EqualTo(new Vector2(53.923557f, -270.11847f)));
        Assert.That(teaTableFootprint[3], Is.EqualTo(new Vector2(-214.44357f, -270.11847f)));
        Assert.That(gameRoot.IsInitialized, Is.True);
        Assert.That(gameRoot.Database, Is.Not.Null);
        Assert.That(gameRoot.Context, Is.Not.Null);
        Assert.That(gameRoot.Services, Has.Count.EqualTo(8));
        Assert.That(gameRoot.Services.Select(service => service.GetType()).Distinct().Count(), Is.EqualTo(8));
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name == "ChapterManager_Runtime"), Is.False);
        Assert.That(serializedChapter2.CurrentPhase, Is.EqualTo(Chapter2Phase.NotStarted));
        Assert.That(serializedChapter2.HasGameContext, Is.True);
        Assert.That(serializedMonsterStinger.HasGameContext, Is.True);
        Assert.That(serializedGuestPanic.HasGameContext, Is.True);
        Assert.That(serializedGuestSearch.HasGameContext, Is.True);
        Assert.That(serializedMonsterStinger.IsRunning, Is.False);
        Assert.That(serializedGuestPanic.IsRunning, Is.False);
        Assert.That(serializedGuestSearch.GuestCount, Is.Zero);
        Assert.That(serializedGuestSearch.FoundGuestCount, Is.Zero);
        Chateau.Architecture.ValidationReport rootValidation = new Chateau.Architecture.ValidationReport();
        gameRoot.ValidateConfiguration(rootValidation);
        Assert.That(rootValidation.HasErrors, Is.False);

        GameObject playerObject = GameObject.Find("Player");
        Assert.That(playerObject, Is.Not.Null);
        PointClickPlayerMovement player = playerObject.GetComponent<PointClickPlayerMovement>();
        Assert.That(player, Is.Not.Null);
        Assert.That(serializedGuestScaleApplier.Calibration, Is.SameAs(serializedGuestScaleCalibration));
        Assert.That(serializedGuestScaleCalibration.ButlerScaleSource, Is.SameAs(player));
        Assert.That(serializedVoicePlayback.IsPlaying, Is.False);
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name == "Canvas_Subtitles"), Is.False);
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name == "Sprite_ChatBubble"), Is.False);

        speech.BeginSpeakLine(
            "ARCH_LIFECYCLE_DIALOGUE",
            "Butler",
            "Architecture lifecycle dialogue.",
            showSubtitleOverlay: false);
        yield return null;

        Assert.That(RequireExactlyOneInActiveScene<GuestVoiceLinePlayback>(), Is.SameAs(serializedVoicePlayback));
        Assert.That(RequireExactlyOneInActiveScene<SpeakingCharacterIndicator>(), Is.SameAs(serializedSpeakingIndicator));
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name == "Canvas_Subtitles"), Is.True);
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name == "Sprite_ChatBubble"), Is.True);
        speech.StopCurrentSpeech();
        yield return null;

        speech.BeginSpeakLine(
            "ARCH_LIFECYCLE_DIALOGUE_REPEAT",
            "Butler",
            "Architecture lifecycle dialogue repeat.",
            showSubtitleOverlay: false);
        yield return null;

        Assert.That(RequireExactlyOneInActiveScene<GuestVoiceLinePlayback>(), Is.SameAs(serializedVoicePlayback));
        Assert.That(RequireExactlyOneInActiveScene<SpeakingCharacterIndicator>(), Is.SameAs(serializedSpeakingIndicator));
        speech.StopCurrentSpeech();
        yield return null;

        Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom));
        RequireOnlyActiveRoom(navigation.CurrentRoom);

        ScaleSnapshot firstScale = CaptureScale(player, RequireOnlyActiveRoom(EntranceRoom));
        yield return WaitForSettledLayout();
        ScaleSnapshot settledScale = CaptureScale(player, RequireOnlyActiveRoom(EntranceRoom));

        Assert.That(
            settledScale.AppliedMultiplier,
            Is.EqualTo(firstScale.AppliedMultiplier).Within(0.001f),
            "The initial Butler scale multiplier must settle deterministically instead of depending on Awake order.");
        Assert.That(
            settledScale.PlayerLocalScaleY,
            Is.EqualTo(firstScale.PlayerLocalScaleY).Within(0.001f));
        Assert.That(
            settledScale.AppliedMultiplier,
            Is.EqualTo(player.RoomPresentationScale).Within(0.001f),
            "The default entrance presentation must preserve the approved Butler-to-door proportion.");

        DoorTriggerNavigation outbound = RequireSceneObject<DoorTriggerNavigation>(
            "DoorTrigger_GEH_DrawingRoom");
        Assert.That(outbound.SourceRoom, Is.EqualTo(EntranceRoom));
        Assert.That(outbound.DestinationRoom, Is.EqualTo(DrawingRoom));
        Assert.That(
            navigation.MoveThroughInspectorDoor(
                outbound.SourceRoom,
                outbound.DoorName,
                outbound.DestinationRoom,
                true),
            Is.True);

        yield return WaitForSettledLayout();
        Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom));
        RequireOnlyActiveRoom(DrawingRoom);
        Assert.That(RequireExactlyOneInActiveScene<FireplaceAmbienceController>(), Is.SameAs(fireplaceAmbience));
        Assert.That(RequireExactlyOneInActiveScene<ClockTickingAmbienceController>(), Is.SameAs(clockAmbience));
        Assert.That(fireplaceAmbience.GetComponent<AudioSource>(), Is.SameAs(fireplaceSource));
        Assert.That(clockAmbience.GetComponent<AudioSource>(), Is.SameAs(clockSource));
        Assert.That(fireplaceSource.clip, Is.Not.Null);
        Assert.That(clockSource.clip, Is.Not.Null);
        Assert.That(teaTableProjection.gameObject.activeInHierarchy, Is.True);
        Assert.That(teaTableBlocker.gameObject.activeInHierarchy, Is.True);
        teaTableRenderer.sortingOrder = -12345;
        teaTableProjection.ApplyProjection();
        int projectionSortingOrder = teaTableRenderer.sortingOrder;
        Assert.That(projectionSortingOrder, Is.EqualTo(6627));
        Assert.That(projectionSortingOrder, Is.EqualTo(teaTableProjection.CurrentSortingOrder));
        Assert.That(projectionSortingOrder, Is.EqualTo(teaTableProjection.GetSortingOrder()));

        teaTableRenderer.sortingOrder = -12346;
        Physics2D.SyncTransforms();
        teaTableBlocker.ApplySourceSortingNow();
        int blockerSortingOrder = teaTableRenderer.sortingOrder;
        Assert.That(blockerSortingOrder, Is.EqualTo(teaTableBlocker.CurrentSortingOrder));
        Assert.That(
            blockerSortingOrder,
            Is.EqualTo(1000 - Mathf.RoundToInt(teaTableCollider.bounds.min.y * 100f)));

        teaTableRenderer.sortingOrder = -12347;
        teaTableProjection.ApplyProjection();
        Assert.That(teaTableRenderer.sortingOrder, Is.EqualTo(projectionSortingOrder));
        Debug.Log(
            $"[SetPieceBaseline] tea_service_table projection={projectionSortingOrder} " +
            $"blocker={blockerSortingOrder} " +
            $"colliderMinY={teaTableCollider.bounds.min.y:0.####}");

        DoorTriggerNavigation reverse = RequireSceneObject<DoorTriggerNavigation>(
            "DoorTrigger_DrawingRoom_GEH");
        Assert.That(reverse.SourceRoom, Is.EqualTo(DrawingRoom));
        Assert.That(reverse.DestinationRoom, Is.EqualTo(EntranceRoom));
        Assert.That(
            navigation.MoveThroughInspectorDoor(
                reverse.SourceRoom,
                reverse.DoorName,
                reverse.DestinationRoom,
                true),
            Is.True);

        yield return WaitForSettledLayout();
        Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom));
        ScaleSnapshot returnedScale = CaptureScale(player, RequireOnlyActiveRoom(EntranceRoom));
        Assert.That(
            returnedScale.AppliedMultiplier,
            Is.EqualTo(settledScale.AppliedMultiplier).Within(0.01f),
            "Returning to the entrance must reproduce the same room-stage scale policy.");

        Assert.That(RequireExactlyOneInActiveScene<CameraManager>(), Is.SameAs(cameraManager));
        Assert.That(RequireExactlyOneInActiveScene<RoomNavigationManager>(), Is.SameAs(navigation));
        Assert.That(RequireExactlyOneInActiveScene<DoorPromptSequenceController>(), Is.SameAs(prompts));
        Assert.That(RequireExactlyOneInActiveScene<ChapterManager>(), Is.SameAs(chapter));
        Assert.That(RequireExactlyOneInActiveScene<ChapterClock>(), Is.SameAs(clock));
        Assert.That(RequireExactlyOneInActiveScene<ChapterEventScheduler>(), Is.SameAs(scheduler));
        Assert.That(RequireExactlyOneInActiveScene<ChapterIntroUI>(), Is.SameAs(intro));
        Assert.That(RequireExactlyOneInActiveScene<Chapter1ArrivalController>(), Is.SameAs(arrival));
        Assert.That(RequireExactlyOneInActiveScene<Chapter1InteractionHUD>(), Is.SameAs(chapter1Hud));
        Assert.That(RequireExactlyOneInActiveScene<RoomLightingController>(), Is.SameAs(lighting));
        Assert.That(RequireExactlyOneInActiveScene<Chateau.Architecture.GameRoot>(), Is.SameAs(gameRoot));
        Assert.That(RequireExactlyOneInActiveScene<RuntimeSettingsMenu>(), Is.SameAs(runtimeSettings));
        Assert.That(RequireExactlyOneInActiveScene<GuestRoomScaleApplier>(), Is.SameAs(serializedGuestScaleApplier));
        Assert.That(RequireExactlyOneInActiveScene<GuestRoomScaleCalibration>(), Is.SameAs(serializedGuestScaleCalibration));
        Assert.That(RequireExactlyOneInActiveScene<RuntimeSettingsMenu>(), Is.SameAs(runtimeSettings));
        Assert.That(RequireExactlyOneInActiveScene<FireplaceAmbienceController>(), Is.SameAs(fireplaceAmbience));
        Assert.That(RequireExactlyOneInActiveScene<ClockTickingAmbienceController>(), Is.SameAs(clockAmbience));
        Assert.That(fireplaceAmbience.GetComponent<AudioSource>(), Is.SameAs(fireplaceSource));
        Assert.That(clockAmbience.GetComponent<AudioSource>(), Is.SameAs(clockSource));
        Assert.That(fireplaceSource.clip, Is.Not.Null);
        Assert.That(clockSource.clip, Is.Not.Null);
        Assert.That(teaTableProjection.gameObject.activeInHierarchy, Is.False);
        Assert.That(
            FindInActiveScene<RoomProjectedEntity>().Single(item => item.gameObject.name == "tea_service_table"),
            Is.SameAs(teaTableProjection));
        Assert.That(
            FindInActiveScene<ObjectMovementBlocker2D>().Single(item => item.SourceObjectName == "tea_service_table"),
            Is.SameAs(teaTableBlocker));

        speech.BeginSpeakLine(
            "ARCH_LIFECYCLE_SKIP_DIALOGUE",
            "Butler",
            "This line must be cancelled by the chapter transition.",
            showSubtitleOverlay: false);
        yield return null;
        Assert.That(speech.IsNormalSpeechActive, Is.True);

        chapter.SkipToChapter2ForTesting();
        yield return WaitForSettledLayout();

        Chapter2Controller chapter2 = RequireExactlyOneInActiveScene<Chapter2Controller>();
        Chapter2InteractionHUD chapter2Hud = RequireExactlyOneInActiveScene<Chapter2InteractionHUD>();
        Chapter2MonsterStingerController monsterStinger = RequireExactlyOneInActiveScene<Chapter2MonsterStingerController>();
        Chapter2GuestPanicController guestPanic = RequireExactlyOneInActiveScene<Chapter2GuestPanicController>();
        Chapter2GuestSearchController guestSearch = RequireExactlyOneInActiveScene<Chapter2GuestSearchController>();
        Assert.That(chapter2.CurrentPhase, Is.Not.EqualTo(Chapter2Phase.NotStarted));
        Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom));
        Assert.That(chapter2, Is.SameAs(serializedChapter2));
        Assert.That(chapter2Hud, Is.SameAs(serializedChapter2Hud));
        Assert.That(speech.IsNormalSpeechActive, Is.False);
        Assert.That(serializedVoicePlayback.IsPlaying, Is.False);
        Transform chatBubble = FindInActiveScene<Transform>().Single(item => item.name == "Sprite_ChatBubble");
        Assert.That(chatBubble.gameObject.activeSelf, Is.False);
        Assert.That(monsterStinger, Is.SameAs(serializedMonsterStinger));
        Assert.That(guestPanic, Is.SameAs(serializedGuestPanic));
        Assert.That(guestSearch, Is.SameAs(serializedGuestSearch));

        chapter.SkipToChapter2ForTesting();
        yield return WaitForSettledLayout();

        Assert.That(RequireExactlyOneInActiveScene<Chapter2Controller>(), Is.SameAs(chapter2));
        Assert.That(RequireExactlyOneInActiveScene<Chapter2InteractionHUD>(), Is.SameAs(chapter2Hud));
        Assert.That(RequireExactlyOneInActiveScene<Chapter2MonsterStingerController>(), Is.SameAs(monsterStinger));
        Assert.That(RequireExactlyOneInActiveScene<Chapter2GuestPanicController>(), Is.SameAs(guestPanic));
        Assert.That(RequireExactlyOneInActiveScene<Chapter2GuestSearchController>(), Is.SameAs(guestSearch));
        Assert.That(RequireExactlyOneInActiveScene<Chapter1InteractionHUD>(), Is.SameAs(chapter1Hud));
        Assert.That(RequireExactlyOneInActiveScene<GuestRoomScaleApplier>(), Is.SameAs(serializedGuestScaleApplier));
        Assert.That(RequireExactlyOneInActiveScene<GuestRoomScaleCalibration>(), Is.SameAs(serializedGuestScaleCalibration));
        Assert.That(RequireExactlyOneInActiveScene<FireplaceAmbienceController>(), Is.SameAs(fireplaceAmbience));
        Assert.That(RequireExactlyOneInActiveScene<ClockTickingAmbienceController>(), Is.SameAs(clockAmbience));
    }

    private static IEnumerator WaitForSettledLayout()
    {
        for (int frame = 0; frame < 4; frame++)
        {
            Canvas.ForceUpdateCanvases();
            yield return null;
        }
    }

    private static T RequireExactlyOneInActiveScene<T>() where T : Component
    {
        T[] components = FindInActiveScene<T>();
        Assert.That(
            components,
            Has.Length.EqualTo(1),
            $"Expected exactly one active-scene {typeof(T).Name}, found {components.Length}.");
        return components[0];
    }

    private static T[] FindInActiveScene<T>() where T : Component
    {
        Scene activeScene = SceneManager.GetActiveScene();
        return Resources.FindObjectsOfTypeAll<T>()
            .Where(component =>
                component != null &&
                component.gameObject != null &&
                component.gameObject.scene == activeScene)
            .ToArray();
    }

    private static RoomContentGroup RequireOnlyActiveRoom(string expectedRoom)
    {
        RoomContentGroup[] activeRooms = FindInActiveScene<RoomContentGroup>()
            .Where(room => room.gameObject.activeInHierarchy)
            .ToArray();
        Assert.That(activeRooms, Has.Length.EqualTo(1));
        Assert.That(activeRooms[0].RoomName, Is.EqualTo(expectedRoom));
        return activeRooms[0];
    }

    private static T RequireSceneObject<T>(string objectName) where T : Component
    {
        T component = FindInActiveScene<T>()
            .FirstOrDefault(candidate => candidate.gameObject.name == objectName);
        Assert.That(component, Is.Not.Null, $"Missing active-scene object '{objectName}'.");
        return component;
    }

    private static ScaleSnapshot CaptureScale(
        PointClickPlayerMovement player,
        RoomContentGroup activeRoom)
    {
        Assert.That(player.TryEvaluateCurrentButlerCharacterScale(out PointClickPlayerMovement.ButlerCharacterScaleSample sample), Is.True);
        float playerScaleY = Mathf.Abs(player.transform.localScale.y);
        float finalScaleY = Mathf.Max(0.001f, sample.ButlerFinalLocalScaleY);
        float appliedMultiplier = playerScaleY / finalScaleY;
        float roomStageScale = Mathf.Abs(activeRoom.transform.lossyScale.x);

        Debug.Log(
            $"[ArchitectureBaseline] room={sample.RoomId} footY={sample.RoomLocalFootPoint.y:0.####} " +
            $"finalY={sample.ButlerFinalLocalScaleY:0.######} playerY={playerScaleY:0.######} " +
            $"stageScale={roomStageScale:0.######} appliedMultiplier={appliedMultiplier:0.######}");

        Assert.That(float.IsNaN(appliedMultiplier), Is.False);
        Assert.That(float.IsInfinity(appliedMultiplier), Is.False);
        Assert.That(appliedMultiplier, Is.GreaterThan(0f));
        CameraManager cameraManager = RequireExactlyOneInActiveScene<CameraManager>();
        Assert.That(cameraManager.TryGetActiveRoomStageActorZoomRatio(out float actorZoomRatio), Is.True);
        float expectedPresentationScale = player.RoomPresentationScale * actorZoomRatio;
        Assert.That(
            player.CurrentWorldActorScaleMultiplier,
            Is.EqualTo(expectedPresentationScale).Within(0.001f));
        Assert.That(
            appliedMultiplier,
            Is.EqualTo(expectedPresentationScale).Within(0.01f),
            "The Butler must follow CameraManager's relative actor zoom, not viewport layout or a lazily captured reference.");
        return new ScaleSnapshot(playerScaleY, roomStageScale, appliedMultiplier);
    }

    private readonly struct ScaleSnapshot
    {
        public ScaleSnapshot(float playerLocalScaleY, float roomStageScale, float appliedMultiplier)
        {
            PlayerLocalScaleY = playerLocalScaleY;
            RoomStageScale = roomStageScale;
            AppliedMultiplier = appliedMultiplier;
        }

        public float PlayerLocalScaleY { get; }
        public float RoomStageScale { get; }
        public float AppliedMultiplier { get; }
    }
}
