using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Chateau.UI;
using Chateau.World.Navigation;
using Chateau.World.Rooms.Props;
using CanonicalPassage = Chateau.World.Rooms.Passages.Passage;
using CanonicalRoomDefinition = Chateau.World.Rooms.RoomDefinition;
using CanonicalRoomView = Chateau.World.Rooms.RoomView;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class GameplayLifecycleCharacterizationTests
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string GameplaySceneName = "Gameplay";
    private const string EntranceRoom = "Grand Entrance Hall";
    private const string DrawingRoom = "Drawing Room";
    private const string MusicRoom = "Music Room";
    private const string CharacterizationGameViewSizeName = "Chantilly Architecture Characterization";

    private bool hasOriginalPlayModeView;
    private PlayModeWindow.PlayModeViewTypes originalPlayModeViewType;
    private uint originalRenderingWidth;
    private uint originalRenderingHeight;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        originalPlayModeViewType = PlayModeWindow.GetViewType();
        PlayModeWindow.GetRenderingResolution(out originalRenderingWidth, out originalRenderingHeight);
        hasOriginalPlayModeView = true;
        Selection.activeObject = null;
        EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (hasOriginalPlayModeView)
        {
            PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
            PlayModeWindow.SetCustomRenderingResolution(
                originalRenderingWidth,
                originalRenderingHeight,
                CharacterizationGameViewSizeName);

            for (int frame = 0; frame < 12; frame++)
            {
                PlayModeWindow.GetRenderingResolution(out uint width, out uint height);
                bool editorSizeRestored = width == originalRenderingWidth && height == originalRenderingHeight;
                bool runtimeSizeRestored = !EditorApplication.isPlaying ||
                    (Screen.width == (int)originalRenderingWidth && Screen.height == (int)originalRenderingHeight);
                if (editorSizeRestored && runtimeSizeRestored)
                {
                    break;
                }

                Canvas.ForceUpdateCanvases();
                yield return null;
            }
        }

        if (EditorApplication.isPlaying)
        {
            yield return new ExitPlayMode();
        }

        if (hasOriginalPlayModeView)
        {
            PlayModeWindow.SetViewType(originalPlayModeViewType);
            hasOriginalPlayModeView = false;
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
        yield return SetAndWaitForRenderedGameViewResolution(1366, 768);

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameplaySceneName));

        Chateau.Architecture.GameRoot gameRoot = RequireExactlyOneInActiveScene<Chateau.Architecture.GameRoot>();
        List<Chateau.Architecture.ChateauBehaviour> serializedSceneBehaviours =
            GetPrivateField<List<Chateau.Architecture.ChateauBehaviour>>(gameRoot, "sceneBehaviours");
        CameraManager cameraManager = RequireExactlyOneInActiveScene<CameraManager>();
        cameraManager.panRoomWithMouseEdges = false;
        cameraManager.zoomRoomWithMouseWheel = false;
        cameraManager.ResetRoomLookForPreview();
        yield return WaitForSettledLayout();
        RoomNavigationManager navigation = RequireExactlyOneInActiveScene<RoomNavigationManager>();
        DoorPromptSequenceController prompts = RequireExactlyOneInActiveScene<DoorPromptSequenceController>();
        ChapterManager chapter = RequireExactlyOneInActiveScene<ChapterManager>();
        ChapterClock clock = RequireExactlyOneInActiveScene<ChapterClock>();
        ChapterEventScheduler scheduler = RequireExactlyOneInActiveScene<ChapterEventScheduler>();
        ChapterIntroUI intro = RequireExactlyOneInActiveScene<ChapterIntroUI>();
        GameObject characterizedIntroHost = intro.gameObject;
        Canvas characterizedIntroCanvas = GetPrivateField<Canvas>(intro, "canvas");
        RectTransform characterizedIntroOverlay = GetPrivateField<RectTransform>(intro, "overlayRoot");
        Image characterizedIntroFade = GetPrivateField<Image>(intro, "fadeImage");
        TMP_Text characterizedIntroTitle = GetPrivateField<TMP_Text>(intro, "titleText");
        RectTransform characterizedIntroCanvasRect = characterizedIntroCanvas != null
            ? characterizedIntroCanvas.GetComponent<RectTransform>()
            : null;
        CanvasScaler characterizedIntroScaler = characterizedIntroCanvas != null
            ? characterizedIntroCanvas.GetComponent<CanvasScaler>()
            : null;
        GraphicRaycaster characterizedIntroRaycaster = characterizedIntroCanvas != null
            ? characterizedIntroCanvas.GetComponent<GraphicRaycaster>()
            : null;
        RectTransform characterizedIntroFadeRect = characterizedIntroFade != null
            ? characterizedIntroFade.GetComponent<RectTransform>()
            : null;
        RectTransform characterizedIntroTitleRect = characterizedIntroTitle != null
            ? characterizedIntroTitle.GetComponent<RectTransform>()
            : null;
        TMP_FontAsset characterizedIntroFont = characterizedIntroTitle != null
            ? characterizedIntroTitle.font
            : null;
        Material characterizedIntroFontMaterial = characterizedIntroTitle != null
            ? characterizedIntroTitle.fontSharedMaterial
            : null;
        Chapter1ArrivalController arrival = RequireExactlyOneInActiveScene<Chapter1ArrivalController>();
        ChapterManager serializedArrivalChapterManager = GetPrivateField<ChapterManager>(arrival, "chapterManager");
        ChapterClock serializedArrivalClock = GetPrivateField<ChapterClock>(arrival, "chapterClock");
        ChapterEventScheduler serializedArrivalScheduler = GetPrivateField<ChapterEventScheduler>(arrival, "eventScheduler");
        CameraManager serializedArrivalCamera = GetPrivateField<CameraManager>(arrival, "cameraManager");
        RoomNavigationManager serializedArrivalNavigation = GetPrivateField<RoomNavigationManager>(arrival, "navigationManager");
        PointClickPlayerMovement serializedArrivalPlayerMovement = GetPrivateField<PointClickPlayerMovement>(arrival, "playerMovement");
        GameObject serializedArrivalButlerRoot = GetPrivateField<GameObject>(arrival, "playerButlerReference");
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name == "Chapter1_ClickTarget_DrawingRoomExit"), Is.False);
        Transform entranceClockPlaceholder = FindInActiveScene<Transform>()
            .Single(item => item.name == "GrandfatherClock");
        Transform drawingRoomClockPlaceholder = FindInActiveScene<Transform>()
            .Single(item => item.name == "GrandfatherClock_Optional");
        RoomContentGroup entranceClockRoom = entranceClockPlaceholder.GetComponentInParent<RoomContentGroup>(true);
        RoomContentGroup drawingRoomClockRoom = drawingRoomClockPlaceholder.GetComponentInParent<RoomContentGroup>(true);
        GameTimeHUD characterizedGameTimeHud = RequireExactlyOneInActiveScene<GameTimeHUD>();
        ChapterClock characterizedGameTimeHudClock = GetPrivateField<ChapterClock>(characterizedGameTimeHud, "chapterClock");
        Canvas characterizedTimeCanvas = GetPrivateField<Canvas>(characterizedGameTimeHud, "canvas");
        TMP_Text characterizedTimeText = GetPrivateField<TMP_Text>(characterizedGameTimeHud, "clockText");
        RectTransform characterizedTimeCanvasRect = characterizedTimeCanvas != null
            ? characterizedTimeCanvas.GetComponent<RectTransform>()
            : null;
        CanvasScaler characterizedTimeScaler = characterizedTimeCanvas != null
            ? characterizedTimeCanvas.GetComponent<CanvasScaler>()
            : null;
        GraphicRaycaster characterizedTimeRaycaster = characterizedTimeCanvas != null
            ? characterizedTimeCanvas.GetComponent<GraphicRaycaster>()
            : null;
        RectTransform characterizedTimeTextRect = characterizedTimeText != null
            ? characterizedTimeText.GetComponent<RectTransform>()
            : null;
        Shadow characterizedTimeShadow = characterizedTimeText != null
            ? characterizedTimeText.GetComponent<Shadow>()
            : null;
        TMP_FontAsset characterizedTimeFont = characterizedTimeText != null
            ? characterizedTimeText.font
            : null;
        Material characterizedTimeFontMaterial = characterizedTimeText != null
            ? characterizedTimeText.fontSharedMaterial
            : null;
        EventSystem characterizedTimeEventSystem = RequireExactlyOneInActiveScene<EventSystem>();
        DoorbellSystem characterizedDoorbell = GetPrivateField<DoorbellSystem>(arrival, "doorbellSystem");
        Assert.That(characterizedDoorbell, Is.Not.Null);
        AudioSource characterizedDoorbellSource = GetPrivateField<AudioSource>(characterizedDoorbell, "audioSource");
        GameAudioSourceVolume characterizedDoorbellBinding = characterizedDoorbell.GetComponent<GameAudioSourceVolume>();
        AudioClip serializedDoorbellClip = GetPrivateField<AudioClip>(characterizedDoorbell, "doorbellClip");
        Chapter1InteractionHUD chapter1Hud = RequireExactlyOneInActiveScene<Chapter1InteractionHUD>();
        Transform[] frontDoorTriggers = FindInActiveScene<Transform>()
            .Where(item => item.name == "Door_answer_trigger")
            .ToArray();
        Assert.That(frontDoorTriggers, Has.Length.EqualTo(1));
        Transform authoredFrontDoorTrigger = frontDoorTriggers.Single();
        SpriteRenderer authoredFrontDoorRenderer = authoredFrontDoorTrigger.GetComponent<SpriteRenderer>();
        BoxCollider2D authoredFrontDoorCollider = authoredFrontDoorTrigger.GetComponent<BoxCollider2D>();
        Chapter1SceneAction authoredFrontDoorAction = authoredFrontDoorTrigger.GetComponent<Chapter1SceneAction>();
        Chapter1SceneAction resolvedFrontDoorAction = GetPrivateField<Chapter1SceneAction>(arrival, "frontDoorSceneAction");
        GuestFootstepCatalog resolvedGuestFootstepCatalog = GetPrivateField<GuestFootstepCatalog>(arrival, "guestFootstepCatalog");
        Transform resolvedGuestEntranceSpawnPlacemark = GetPrivateField<Transform>(arrival, "guestEntranceSpawnPlacemark");
        Transform authoredGuestEntranceSpawnPlacemark = FindInActiveScene<Transform>()
            .Single(item => item.name == "Placemark_guests_entrance");
        Transform characterizedDrawingRoomDoorTarget = FindInActiveScene<Transform>()
            .Single(item => item.name == "GuestDrawingRoomDoorTarget");
        RoomContentGroup characterizedEntranceRoomContent = FindInActiveScene<RoomContentGroup>()
            .Single(item => item.RoomName == EntranceRoom);
        RoomContentGroup characterizedDrawingRoomContent = FindInActiveScene<RoomContentGroup>()
            .Single(item => item.RoomName == DrawingRoom);
        CanonicalRoomView characterizedEntranceRoomView = FindInActiveScene<CanonicalRoomView>()
            .Single(item => item.Definition != null && item.Definition.StableId == "room.grand-entrance-hall");
        CanonicalRoomView characterizedDrawingRoomView = FindInActiveScene<CanonicalRoomView>()
            .Single(item => item.Definition != null && item.Definition.StableId == "room.drawing-room");
        CanonicalRoomDefinition characterizedEntranceRoomDefinition = characterizedEntranceRoomView.Definition;
        CanonicalRoomDefinition characterizedDrawingRoomDefinition = characterizedDrawingRoomView.Definition;
        AssertCanonicalRoomViewsRemainStable(
            characterizedEntranceRoomView,
            characterizedDrawingRoomView,
            characterizedEntranceRoomDefinition,
            characterizedDrawingRoomDefinition,
            characterizedEntranceRoomContent,
            characterizedDrawingRoomContent,
            entranceVisible: true,
            drawingVisible: false);
        Transform[] characterizedDrawingRoomGuestPoints = Enumerable.Range(1, 8)
            .Select(index => FindInActiveScene<Transform>()
                .Single(item => item.name == $"DrawingRoomGuestPoint_{index:00}"))
            .ToArray();
        Vector3[] expectedDrawingRoomGuestPointPositions =
        {
            new Vector3(-290.4f, -126.1f, -7691.114f),
            new Vector3(-335.8f, -144.1f, -7691.114f),
            new Vector3(-173.3f, -85.8f, -7691.114f),
            new Vector3(-393f, -154.8f, -7691.114f),
            new Vector3(-83f, -69f, -7691.114f),
            new Vector3(53.3f, -97.8f, -7691.114f),
            new Vector3(-257f, -72f, -7691.114f),
            new Vector3(217.9f, -132f, -7691.114f)
        };
        int characterizedRoomAnchorCount = FindInActiveScene<RoomAnchor>().Length;
        int characterizedRoomContentCount = FindInActiveScene<RoomContentGroup>().Length;
        int characterizedFootstepCatalogCount = Resources.FindObjectsOfTypeAll<GuestFootstepCatalog>().Length;
        Transform entranceCoatHanger = FindInActiveScene<Transform>()
            .Single(item => item.name == "entrance_coat_hanger_0");
        SpriteRenderer entranceCoatHangerRenderer = entranceCoatHanger.GetComponent<SpriteRenderer>();
        CoatCloset entranceCoatCloset = RequireExactlyOneInActiveScene<CoatCloset>();
        Chapter1SceneAction entranceCoatAction = entranceCoatHanger.GetComponent<Chapter1SceneAction>();
        BoxCollider2D entranceCoatCollider = entranceCoatHanger.GetComponent<BoxCollider2D>();
        Chapter2Controller serializedChapter2 = RequireExactlyOneInActiveScene<Chapter2Controller>();
        Chapter2InteractionHUD serializedChapter2Hud = RequireExactlyOneInActiveScene<Chapter2InteractionHUD>();
        Chapter2MonsterStingerController serializedMonsterStinger = RequireExactlyOneInActiveScene<Chapter2MonsterStingerController>();
        GameObject serializedMonsterObject = GetPrivateField<GameObject>(serializedMonsterStinger, "monsterObject");
        Transform serializedMonsterRunStart = GetPrivateField<Transform>(serializedMonsterStinger, "runStart");
        Transform serializedMonsterRunTarget = GetPrivateField<Transform>(serializedMonsterStinger, "runTarget");
        RoomNavigationManager serializedMonsterNavigation = GetPrivateField<RoomNavigationManager>(serializedMonsterStinger, "navigationManager");
        Image serializedMonsterImage = GetPrivateField<Image>(serializedMonsterStinger, "monsterImage");
        SpriteRenderer serializedMonsterSpriteRenderer = GetPrivateField<SpriteRenderer>(serializedMonsterStinger, "monsterSpriteRenderer");
        Canvas serializedMonsterOverlayCanvas = GetPrivateField<Canvas>(serializedMonsterStinger, "monsterOverlayCanvas");
        AudioSource serializedMonsterViolinSource = GetPrivateField<AudioSource>(serializedMonsterStinger, "violinAudioSource");
        GameAudioSourceVolume serializedMonsterViolinBinding = GetPrivateField<GameAudioSourceVolume>(serializedMonsterStinger, "violinAudioVolumeBinding");
        AudioClip serializedMonsterViolinClip = GetPrivateField<AudioClip>(serializedMonsterStinger, "violinAudioClip");
        Sprite[] serializedMonsterRunSprites = GetPrivateField<Sprite[]>(serializedMonsterStinger, "monsterRunSprites");
        string[] expectedMonsterRunSpriteGuids =
        {
            "8414d4be92f9485e8f33a1abb721c2fd",
            "545dbfc1fc754f3fbfc3ba99fa334619",
            "ee2e37acc05b4445ba6cfc7f8e70737e",
            "432fbf9f626f4b6c84fa80dd3dab01fc",
            "94976d1632474d90914e011e989f3ae7",
            "f7e820a7807c4c159b8a465ec1909b89",
            "32ccf6ba47fe4ce19bcb7e3354484363",
            "ebfd9b9fdded4ed6a159c078f21829d3"
        };
        Sprite serializedMonsterOriginalSprite = serializedMonsterImage != null ? serializedMonsterImage.sprite : null;
        Chapter2GuestPanicController serializedGuestPanic = RequireExactlyOneInActiveScene<Chapter2GuestPanicController>();
        Chapter2GuestSearchController serializedGuestSearch = RequireExactlyOneInActiveScene<Chapter2GuestSearchController>();
        RoomNavigationManager serializedGuestSearchNavigation = GetPrivateField<RoomNavigationManager>(serializedGuestSearch, "navigationManager");
        GuestRoomScaleApplier serializedGuestScaleApplier = RequireExactlyOneInActiveScene<GuestRoomScaleApplier>();
        GuestRoomScaleCalibration serializedGuestScaleCalibration = RequireExactlyOneInActiveScene<GuestRoomScaleCalibration>();
        GuestVoiceLinePlayback serializedVoicePlayback = RequireExactlyOneInActiveScene<GuestVoiceLinePlayback>();
        SpeakingCharacterIndicator serializedSpeakingIndicator = RequireExactlyOneInActiveScene<SpeakingCharacterIndicator>();
        RuntimeSettingsMenu runtimeSettings = RequireExactlyOneInActiveScene<RuntimeSettingsMenu>();
        FireplaceAmbienceController fireplaceAmbience = RequireExactlyOneInActiveScene<FireplaceAmbienceController>();
        ClockTickingAmbienceController clockAmbience = RequireExactlyOneInActiveScene<ClockTickingAmbienceController>();
        RoomNavigationManager serializedClockAmbienceNavigation = GetPrivateField<RoomNavigationManager>(clockAmbience, "navigationManager");
        ClockTickingAmbienceCatalog serializedClockAmbienceCatalog = GetPrivateField<ClockTickingAmbienceCatalog>(clockAmbience, "catalog");
        AudioSource serializedClockAmbienceSource = GetPrivateField<AudioSource>(clockAmbience, "audioSource");
        RoomLightingController lighting = RequireExactlyOneInActiveScene<RoomLightingController>();
        SetPieceView teaTableView = FindInActiveScene<SetPieceView>()
            .Single(item => item.gameObject.name == "tea_service_table");
        SpriteRenderer teaTableRenderer = teaTableView.CutoutRenderer;
        ObjectMovementBlocker2D teaTableBlocker = FindInActiveScene<ObjectMovementBlocker2D>()
            .Single(item => item.SourceObjectName == "tea_service_table");
        PolygonCollider2D teaTableCollider = teaTableBlocker.BlockingCollider as PolygonCollider2D;
        SetPieceView purpleArmchairView = FindInActiveScene<SetPieceView>()
            .Single(item => item.gameObject.name == "purple_armchair_back");
        SpriteRenderer purpleArmchairRenderer = purpleArmchairView.CutoutRenderer;
        ObjectMovementBlocker2D purpleArmchairBlocker = FindInActiveScene<ObjectMovementBlocker2D>()
            .Single(item => item.SourceObjectName == "purple_armchair_back");
        PolygonCollider2D purpleArmchairCollider = purpleArmchairBlocker.BlockingCollider as PolygonCollider2D;
        SetPieceView purpleSofaView = FindInActiveScene<SetPieceView>()
            .Single(item => item.gameObject.name == "purple_sofa");
        SpriteRenderer purpleSofaRenderer = purpleSofaView.CutoutRenderer;
        ObjectMovementBlocker2D purpleSofaBlocker = FindInActiveScene<ObjectMovementBlocker2D>()
            .Single(item => item.SourceObjectName == "purple_sofa");
        PolygonCollider2D purpleSofaCollider = purpleSofaBlocker.BlockingCollider as PolygonCollider2D;

        Assert.That(cameraManager, Is.Not.Null);
        Assert.That(prompts, Is.Not.Null);
        Assert.That(lighting, Is.Not.Null);
        Assert.That(chapter.Clock, Is.SameAs(clock));
        Assert.That(chapter.EventScheduler, Is.SameAs(scheduler));
        Assert.That(GetPrivateField<ChapterIntroUI>(chapter, "introUI"), Is.SameAs(intro));
        Assert.That(GetPrivateField<ChapterIntroUI>(serializedChapter2, "introUI"), Is.SameAs(intro));
        intro.ValidateRequiredReferences();
        intro.ValidateRequiredReferences();
        Assert.That(intro.gameObject, Is.SameAs(characterizedIntroHost));
        Assert.That(intro.gameObject, Is.SameAs(chapter.gameObject));
        Assert.That(intro.GetComponents<ChapterIntroUI>(), Has.Length.EqualTo(1));
        Assert.That(intro.GetComponent<Canvas>(), Is.Null);
        Assert.That(GetPrivateField<Canvas>(intro, "canvas"), Is.SameAs(characterizedIntroCanvas));
        Assert.That(GetPrivateField<RectTransform>(intro, "overlayRoot"), Is.SameAs(characterizedIntroOverlay));
        Assert.That(GetPrivateField<Image>(intro, "fadeImage"), Is.SameAs(characterizedIntroFade));
        Assert.That(GetPrivateField<TMP_Text>(intro, "titleText"), Is.SameAs(characterizedIntroTitle));
        Assert.That(characterizedIntroCanvas, Is.Not.Null);
        Assert.That(characterizedIntroCanvas.gameObject.name, Is.EqualTo("Canvas_ChapterIntroOverlay"));
        Assert.That(characterizedIntroCanvas.transform.parent, Is.Null);
        Assert.That(characterizedIntroCanvas.GetComponents<Component>(), Has.Length.EqualTo(4));
        Assert.That(characterizedIntroCanvas.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("UI")));
        Assert.That(characterizedIntroCanvas.gameObject.activeSelf, Is.True);
        Assert.That(characterizedIntroCanvas.enabled, Is.True);
        Assert.That(characterizedIntroCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(characterizedIntroCanvas.overrideSorting, Is.False);
        Assert.That(characterizedIntroCanvas.sortingOrder, Is.EqualTo(12000));
        Assert.That(characterizedIntroCanvas.transform.childCount, Is.EqualTo(1));
        Assert.That(characterizedIntroCanvasRect, Is.Not.Null);
        Assert.That(characterizedIntroCanvasRect.rect.width, Is.GreaterThan(0f));
        Assert.That(characterizedIntroCanvasRect.rect.height, Is.GreaterThan(0f));
        Assert.That(characterizedIntroScaler, Is.Not.Null);
        Assert.That(characterizedIntroScaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
        Assert.That(characterizedIntroScaler.referenceResolution, Is.EqualTo(new Vector2(1366f, 768f)));
        Assert.That(characterizedIntroScaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight));
        Assert.That(characterizedIntroScaler.matchWidthOrHeight, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(characterizedIntroRaycaster, Is.Not.Null);
        Assert.That(characterizedIntroOverlay, Is.Not.Null);
        Assert.That(characterizedIntroOverlay.gameObject.name, Is.EqualTo("ChapterIntroUI_Runtime"));
        Assert.That(characterizedIntroOverlay.parent, Is.SameAs(characterizedIntroCanvas.transform));
        Assert.That(characterizedIntroOverlay.GetComponents<Component>(), Has.Length.EqualTo(1));
        Assert.That(characterizedIntroOverlay.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("UI")));
        Assert.That(characterizedIntroOverlay.anchorMin, Is.EqualTo(Vector2.zero));
        Assert.That(characterizedIntroOverlay.anchorMax, Is.EqualTo(Vector2.one));
        Assert.That(characterizedIntroOverlay.offsetMin, Is.EqualTo(Vector2.zero));
        Assert.That(characterizedIntroOverlay.offsetMax, Is.EqualTo(Vector2.zero));
        Assert.That(characterizedIntroOverlay.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        Assert.That(characterizedIntroOverlay.localScale, Is.EqualTo(Vector3.one));
        Assert.That(
            characterizedIntroOverlay.rect.width,
            Is.EqualTo(characterizedIntroCanvasRect.rect.width).Within(0.01f));
        Assert.That(
            characterizedIntroOverlay.rect.height,
            Is.EqualTo(characterizedIntroCanvasRect.rect.height).Within(0.01f));
        Assert.That(characterizedIntroOverlay.childCount, Is.EqualTo(2));
        Assert.That(characterizedIntroFade, Is.Not.Null);
        Assert.That(characterizedIntroFade.gameObject.name, Is.EqualTo("Image_ChapterIntroFade"));
        Assert.That(characterizedIntroFade.transform.parent, Is.SameAs(characterizedIntroOverlay));
        Assert.That(characterizedIntroFade.GetComponents<Component>(), Has.Length.EqualTo(3));
        Assert.That(characterizedIntroFade.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("UI")));
        Assert.That(characterizedIntroFade.enabled, Is.True);
        Assert.That(characterizedIntroFade.sprite, Is.Null);
        Assert.That(characterizedIntroFade.raycastTarget, Is.True);
        Assert.That(characterizedIntroFade.color.r, Is.Zero.Within(0.0001f));
        Assert.That(characterizedIntroFade.color.g, Is.Zero.Within(0.0001f));
        Assert.That(characterizedIntroFade.color.b, Is.Zero.Within(0.0001f));
        Assert.That(characterizedIntroFadeRect, Is.Not.Null);
        Assert.That(characterizedIntroFadeRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        Assert.That(characterizedIntroFadeRect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        Assert.That(characterizedIntroFadeRect.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        Assert.That(characterizedIntroFadeRect.anchoredPosition, Is.EqualTo(Vector2.zero));
        Assert.That(characterizedIntroFadeRect.sizeDelta, Is.EqualTo(new Vector2(10000f, 10000f)));
        Assert.That(characterizedIntroFadeRect.localScale, Is.EqualTo(Vector3.one));
        Assert.That(characterizedIntroTitle, Is.Not.Null);
        Assert.That(characterizedIntroTitle.gameObject.name, Is.EqualTo("Text_ChapterIntroTitle"));
        Assert.That(characterizedIntroTitle.transform.parent, Is.SameAs(characterizedIntroOverlay));
        Assert.That(characterizedIntroTitle.GetComponents<Component>(), Has.Length.EqualTo(3));
        Assert.That(characterizedIntroTitle.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("UI")));
        Assert.That(characterizedIntroTitle.enabled, Is.True);
        Assert.That(characterizedIntroTitle.fontSize, Is.EqualTo(72f).Within(0.0001f));
        Assert.That(characterizedIntroTitle.color, Is.EqualTo(Color.white));
        Assert.That(characterizedIntroTitle.alignment, Is.EqualTo(TextAlignmentOptions.Center));
        Assert.That(characterizedIntroTitle.raycastTarget, Is.False);
        Assert.That(characterizedIntroTitleRect, Is.Not.Null);
        Assert.That(characterizedIntroTitleRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        Assert.That(characterizedIntroTitleRect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        Assert.That(characterizedIntroTitleRect.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        Assert.That(characterizedIntroTitleRect.anchoredPosition, Is.EqualTo(Vector2.zero));
        Assert.That(characterizedIntroTitleRect.sizeDelta, Is.EqualTo(new Vector2(900f, 180f)));
        Assert.That(characterizedIntroTitleRect.localScale, Is.EqualTo(Vector3.one));
        Assert.That(characterizedIntroFont, Is.Not.Null);
        Assert.That(characterizedIntroFontMaterial, Is.Not.Null);
        Assert.That(
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                characterizedIntroFont,
                out string introFontGuid,
                out long introFontFileId),
            Is.True);
        Assert.That(introFontGuid, Is.EqualTo("8f586378b4e144a9851e7b34d9b748ee"));
        Assert.That(introFontFileId, Is.EqualTo(11400000L));
        Assert.That(
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                characterizedIntroFontMaterial,
                out string introFontMaterialGuid,
                out long introFontMaterialFileId),
            Is.True);
        Assert.That(introFontMaterialGuid, Is.EqualTo("8f586378b4e144a9851e7b34d9b748ee"));
        Assert.That(introFontMaterialFileId, Is.EqualTo(2180264L));
        Assert.That(FindInActiveScene<Transform>().Count(item => item.name == "Canvas_ChapterIntroOverlay"), Is.EqualTo(1));
        Assert.That(FindInActiveScene<Transform>().Count(item => item.name == "ChapterIntroUI_Runtime"), Is.EqualTo(1));
        Assert.That(FindInActiveScene<Transform>().Count(item => item.name == "Image_ChapterIntroFade"), Is.EqualTo(1));
        Assert.That(FindInActiveScene<Transform>().Count(item => item.name == "Text_ChapterIntroTitle"), Is.EqualTo(1));
        Debug.Log(
            $"[ChapterIntroUICharacterization] owner={intro.GetInstanceID()} canvas={characterizedIntroCanvas.GetInstanceID()} " +
            $"overlay={characterizedIntroOverlay.GetInstanceID()} fade={characterizedIntroFade.GetInstanceID()} " +
            $"title={characterizedIntroTitle.GetInstanceID()}");
        Assert.That(serializedArrivalChapterManager, Is.SameAs(chapter));
        Assert.That(serializedArrivalClock, Is.SameAs(clock));
        Assert.That(serializedArrivalScheduler, Is.SameAs(scheduler));
        Assert.That(serializedArrivalCamera, Is.SameAs(cameraManager));
        Assert.That(serializedArrivalNavigation, Is.SameAs(navigation));
        Assert.That(serializedArrivalPlayerMovement, Is.Not.Null);
        Assert.That(serializedArrivalButlerRoot, Is.SameAs(serializedArrivalPlayerMovement.gameObject));
        Assert.That(entranceClockRoom, Is.Not.Null);
        Assert.That(entranceClockRoom.RoomName, Is.EqualTo(EntranceRoom));
        Assert.That(drawingRoomClockRoom, Is.Not.Null);
        Assert.That(drawingRoomClockRoom.RoomName, Is.EqualTo(DrawingRoom));
        Assert.That(entranceClockPlaceholder.parent.name, Is.EqualTo("Props"));
        Assert.That(drawingRoomClockPlaceholder.parent.name, Is.EqualTo("Props"));
        Assert.That(entranceClockPlaceholder.localPosition, Is.EqualTo(new Vector3(-545f, -205f, -7691.114f)));
        Assert.That(drawingRoomClockPlaceholder.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(entranceClockPlaceholder.GetComponents<Component>(), Has.Length.EqualTo(1));
        Assert.That(drawingRoomClockPlaceholder.GetComponents<Component>(), Has.Length.EqualTo(1));
        Assert.That(entranceClockPlaceholder.GetComponent<AudioSource>(), Is.Null);
        Assert.That(drawingRoomClockPlaceholder.GetComponent<AudioSource>(), Is.Null);
        Assert.That(entranceClockPlaceholder.GetComponent<GameAudioSourceVolume>(), Is.Null);
        Assert.That(drawingRoomClockPlaceholder.GetComponent<GameAudioSourceVolume>(), Is.Null);
        Assert.That(entranceClockPlaceholder.GetComponentsInChildren<RoomAnchor>(true), Has.Length.EqualTo(2));
        Assert.That(drawingRoomClockPlaceholder.GetComponentsInChildren<RoomAnchor>(true), Has.Length.EqualTo(2));
        AssertGrandfatherClockPlaceholdersRemainUnmodified(entranceClockPlaceholder, drawingRoomClockPlaceholder);
        Debug.Log(
            $"[GrandfatherClockRetirement] entrance={entranceClockPlaceholder.GetInstanceID()} " +
            $"drawing={drawingRoomClockPlaceholder.GetInstanceID()} injectedComponents=0");
        Assert.That(characterizedTimeCanvas, Is.Not.Null);
        Assert.That(characterizedGameTimeHud.gameObject, Is.SameAs(characterizedTimeCanvas.gameObject));
        Assert.That(characterizedGameTimeHudClock, Is.SameAs(clock));
        Assert.That(characterizedGameTimeHud.IsConfiguredFor(clock), Is.True);
        Assert.That(characterizedGameTimeHud.GetComponents<GameTimeHUD>(), Has.Length.EqualTo(1));
        Assert.That(arrival.GetComponent<GameTimeHUD>(), Is.Null);
        Assert.That(characterizedGameTimeHud.HasGameContext, Is.True);
        Assert.That(serializedSceneBehaviours.Count(item => item == characterizedGameTimeHud), Is.EqualTo(1));
        Assert.That(characterizedTimeCanvas.gameObject.name, Is.EqualTo("Canvas_GameTimeHUD"));
        Assert.That(characterizedTimeCanvas.transform.parent, Is.SameAs(gameRoot.transform));
        Assert.That(characterizedTimeCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(characterizedTimeCanvas.sortingOrder, Is.EqualTo(9000));
        Assert.That(characterizedTimeCanvas.gameObject.activeSelf, Is.True);
        Assert.That(characterizedTimeCanvas.enabled, Is.True);
        Assert.That(characterizedTimeCanvas.transform.childCount, Is.EqualTo(1));
        Assert.That(characterizedTimeCanvas.GetComponents<Component>(), Has.Length.EqualTo(5));
        Assert.That(characterizedTimeCanvasRect, Is.Not.Null);
        Assert.That(characterizedTimeScaler, Is.Not.Null);
        Assert.That(characterizedTimeRaycaster, Is.Not.Null);
        Assert.That(characterizedTimeScaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
        Assert.That(characterizedTimeScaler.referenceResolution, Is.EqualTo(new Vector2(1366f, 768f)));
        Assert.That(characterizedTimeScaler.matchWidthOrHeight, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(characterizedTimeText, Is.Not.Null);
        Assert.That(characterizedTimeText.gameObject.name, Is.EqualTo("Text_CurrentGameTime"));
        Assert.That(characterizedTimeText.gameObject.activeSelf, Is.True);
        Assert.That(characterizedTimeText.enabled, Is.True);
        Assert.That(characterizedTimeText.transform.GetSiblingIndex(), Is.EqualTo(characterizedTimeCanvas.transform.childCount - 1));
        Assert.That(characterizedTimeText.transform.parent, Is.SameAs(characterizedTimeCanvas.transform));
        Assert.That(characterizedTimeText.GetComponents<Component>(), Has.Length.EqualTo(4));
        Assert.That(characterizedTimeText.fontSize, Is.EqualTo(24f).Within(0.0001f));
        Assert.That(characterizedTimeText.color, Is.EqualTo(Color.white));
        Assert.That(characterizedTimeText.alignment, Is.EqualTo(TextAlignmentOptions.BottomLeft));
        Assert.That(characterizedTimeText.raycastTarget, Is.False);
        Assert.That(characterizedTimeText.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
        Assert.That(characterizedTimeText.text, Is.EqualTo(clock.CurrentTimeLabel));
        Assert.That(characterizedTimeTextRect, Is.Not.Null);
        Assert.That(characterizedTimeTextRect.anchorMin, Is.EqualTo(Vector2.zero));
        Assert.That(characterizedTimeTextRect.anchorMax, Is.EqualTo(Vector2.zero));
        Assert.That(characterizedTimeTextRect.pivot, Is.EqualTo(Vector2.zero));
        Assert.That(characterizedTimeTextRect.anchoredPosition, Is.EqualTo(new Vector2(18f, 18f)));
        Assert.That(characterizedTimeTextRect.sizeDelta, Is.EqualTo(new Vector2(220f, 36f)));
        Assert.That(characterizedTimeShadow, Is.Not.Null);
        Assert.That(GetPrivateField<Shadow>(characterizedGameTimeHud, "clockShadow"), Is.SameAs(characterizedTimeShadow));
        Assert.That(characterizedTimeShadow.effectColor, Is.EqualTo(new Color(0f, 0f, 0f, 0.85f)));
        Assert.That(characterizedTimeShadow.effectDistance, Is.EqualTo(new Vector2(2f, -2f)));
        Assert.That(characterizedTimeShadow.useGraphicAlpha, Is.True);
        Assert.That(characterizedTimeFont, Is.Not.Null);
        Assert.That(characterizedTimeFontMaterial, Is.Not.Null);
        Assert.That(
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                characterizedTimeFont,
                out string timeFontGuid,
                out long timeFontFileId),
            Is.True);
        Assert.That(timeFontGuid, Is.EqualTo("8f586378b4e144a9851e7b34d9b748ee"));
        Assert.That(timeFontFileId, Is.EqualTo(11400000L));
        Assert.That(
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                characterizedTimeFontMaterial,
                out string timeFontMaterialGuid,
                out long timeFontMaterialFileId),
            Is.True);
        Assert.That(timeFontMaterialGuid, Is.EqualTo("8f586378b4e144a9851e7b34d9b748ee"));
        Assert.That(timeFontMaterialFileId, Is.EqualTo(2180264L));
        Assert.That(FindInActiveScene<Transform>().Count(item => item.name == "Canvas_GameTimeHUD"), Is.EqualTo(1));
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name == "Canvas_ChapterTimeSettings"), Is.False);
        Assert.That(FindInActiveScene<Transform>().Count(item => item.name == "Text_CurrentGameTime"), Is.EqualTo(1));
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name == "Panel_TimeSettings"), Is.False);
        Debug.Log(
            $"[GameTimeHUDSerialization] owner={characterizedGameTimeHud.GetInstanceID()} " +
            $"clock={characterizedGameTimeHudClock.GetInstanceID()} canvas={characterizedTimeCanvas.GetInstanceID()} " +
            $"text={characterizedTimeText.GetInstanceID()} shadow={characterizedTimeShadow.GetInstanceID()}");
        Assert.That(characterizedDoorbell.gameObject, Is.SameAs(arrival.gameObject));
        Assert.That(GetPrivateField<ChapterClock>(characterizedDoorbell, "chapterClock"), Is.SameAs(clock));
        Assert.That(characterizedDoorbellSource, Is.Not.Null);
        Assert.That(characterizedDoorbellSource.gameObject, Is.SameAs(arrival.gameObject));
        Assert.That(characterizedDoorbell.GetComponents<AudioSource>(), Has.Length.EqualTo(1));
        Assert.That(characterizedDoorbellBinding, Is.Not.Null);
        Assert.That(characterizedDoorbellBinding.gameObject, Is.SameAs(arrival.gameObject));
        Assert.That(characterizedDoorbell.GetComponents<GameAudioSourceVolume>(), Has.Length.EqualTo(1));
        Assert.That(GetPrivateField<AudioSource>(characterizedDoorbellBinding, "audioSource"), Is.SameAs(characterizedDoorbellSource));
        Assert.That(GetPrivateField<GameAudioSourceVolume>(characterizedDoorbell, "audioVolumeBinding"), Is.SameAs(characterizedDoorbellBinding));
        Assert.That(characterizedDoorbellBinding.Channel, Is.EqualTo(GameAudioChannel.GameSounds));
        Assert.That(characterizedDoorbellBinding.BaseVolume, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(characterizedDoorbellSource.playOnAwake, Is.False);
        Assert.That(characterizedDoorbellSource.loop, Is.False);
        Assert.That(characterizedDoorbellSource.mute, Is.False);
        Assert.That(characterizedDoorbellSource.enabled, Is.True);
        Assert.That(characterizedDoorbellSource.spatialBlend, Is.Zero.Within(0.0001f));
        Assert.That(characterizedDoorbellSource.ignoreListenerPause, Is.False);
        Assert.That(characterizedDoorbellSource.clip, Is.Null, "DoorbellSystem should own the one-shot clip; the AudioSource resource remains empty.");
        Assert.That(serializedDoorbellClip, Is.Not.Null);
        characterizedDoorbell.StartRinging(clock.ElapsedGameMinutes, true, false);
        AudioClip characterizedDoorbellClip = GetPrivateField<AudioClip>(characterizedDoorbell, "doorbellClip");
        Assert.That(characterizedDoorbellClip, Is.SameAs(serializedDoorbellClip));
        Assert.That(
            AssetDatabase.GetAssetPath(characterizedDoorbellClip),
            Is.EqualTo("Assets/Resources/Audio/SFX/old_fashioned_door_bell_youtube_IqFKjVlaOik_48khz.wav"));
        Assert.That(
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                characterizedDoorbellClip,
                out string characterizedDoorbellClipGuid,
                out long characterizedDoorbellClipFileId),
            Is.True);
        Assert.That(characterizedDoorbellClipGuid, Is.EqualTo("67dc6970d473422a86e0c071ef23abd1"));
        Assert.That(characterizedDoorbellClipFileId, Is.EqualTo(8300000L));
        characterizedDoorbell.StopRinging();
        characterizedDoorbellSource.Stop();
        characterizedDoorbell.Initialize(clock);
        Assert.That(GetPrivateField<AudioSource>(characterizedDoorbell, "audioSource"), Is.SameAs(characterizedDoorbellSource));
        Assert.That(characterizedDoorbell.GetComponent<GameAudioSourceVolume>(), Is.SameAs(characterizedDoorbellBinding));
        Assert.That(GetPrivateField<AudioClip>(characterizedDoorbell, "doorbellClip"), Is.SameAs(characterizedDoorbellClip));
        Assert.That(FindInActiveScene<DoorbellSystem>(), Has.Length.EqualTo(1));

        Chapter1SceneAction[] authoredChapter1Actions = FindInActiveScene<Chapter1SceneAction>();
        Assert.That(authoredChapter1Actions, Has.Length.EqualTo(2));
        CollectionAssert.AreEquivalent(
            new[] { authoredFrontDoorAction, entranceCoatAction },
            authoredChapter1Actions);
        InvokePrivateMethod(arrival, "EnsureRuntimeInteractionSystems");
        InvokePrivateMethod(arrival, "EnsureRuntimeInteractionSystems");
        AssertGrandfatherClockPlaceholdersRemainUnmodified(entranceClockPlaceholder, drawingRoomClockPlaceholder);
        AssertGameTimeHudGraphRemainsStable(
            arrival,
            characterizedGameTimeHud,
            characterizedGameTimeHudClock,
            characterizedTimeCanvas,
            characterizedTimeText,
            characterizedTimeShadow,
            characterizedTimeFont,
            characterizedTimeFontMaterial,
            characterizedTimeEventSystem);
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name == "Chapter1_ClickTarget_DrawingRoomExit"), Is.False);
        CollectionAssert.AreEquivalent(
            new[] { authoredFrontDoorAction, entranceCoatAction },
            FindInActiveScene<Chapter1SceneAction>());
        Assert.That(authoredFrontDoorRenderer, Is.Not.Null);
        Assert.That(authoredFrontDoorCollider, Is.Not.Null);
        Assert.That(authoredFrontDoorAction, Is.Not.Null);
        Assert.That(resolvedFrontDoorAction, Is.SameAs(authoredFrontDoorAction));
        Assert.That(authoredFrontDoorTrigger.GetComponents<SpriteRenderer>(), Has.Length.EqualTo(1));
        Assert.That(authoredFrontDoorTrigger.GetComponents<BoxCollider2D>(), Has.Length.EqualTo(1));
        Assert.That(authoredFrontDoorTrigger.GetComponents<Chapter1SceneAction>(), Has.Length.EqualTo(1));
        Assert.That(authoredFrontDoorCollider.enabled, Is.True);
        Assert.That(authoredFrontDoorCollider.isTrigger, Is.True);
        Assert.That(authoredFrontDoorCollider.offset, Is.EqualTo(Vector2.zero));
        Assert.That(authoredFrontDoorCollider.size, Is.EqualTo(Vector2.one));
        Assert.That(authoredFrontDoorCollider.edgeRadius, Is.Zero.Within(0.0001f));
        Assert.That(GetPrivateValue<Chapter1SceneActionType>(authoredFrontDoorAction, "actionType"), Is.EqualTo(Chapter1SceneActionType.FrontDoor));
        Assert.That(GetPrivateField<Chapter1ArrivalController>(authoredFrontDoorAction, "arrivalController"), Is.SameAs(arrival));
        Assert.That(resolvedGuestFootstepCatalog, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(resolvedGuestFootstepCatalog), Is.EqualTo("Assets/Resources/Audio/GuestFootstepCatalog.asset"));
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(resolvedGuestFootstepCatalog)), Is.EqualTo("0e780686c6653db1a1c74916a591d484"));
        Assert.That(
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                resolvedGuestFootstepCatalog,
                out string guestFootstepCatalogGuid,
                out long guestFootstepCatalogFileId),
            Is.True);
        Assert.That(guestFootstepCatalogGuid, Is.EqualTo("0e780686c6653db1a1c74916a591d484"));
        Assert.That(guestFootstepCatalogFileId, Is.EqualTo(11400000L));
        Assert.That(resolvedGuestEntranceSpawnPlacemark, Is.SameAs(authoredGuestEntranceSpawnPlacemark));
        Assert.That(GetPrivateField<Transform>(arrival, "drawingRoomDoorTarget"), Is.SameAs(characterizedDrawingRoomDoorTarget));
        Assert.That(GetPrivateField<RoomContentGroup>(arrival, "entryRoomContent"), Is.SameAs(characterizedEntranceRoomContent));
        Assert.That(GetPrivateField<RoomContentGroup>(arrival, "drawingRoomContent"), Is.SameAs(characterizedDrawingRoomContent));
        CollectionAssert.AreEqual(characterizedDrawingRoomGuestPoints, GetPrivateField<Transform[]>(arrival, "drawingRoomGuestPoints"));
        Assert.That(authoredGuestEntranceSpawnPlacemark.localPosition.x, Is.EqualTo(-7.216162f).Within(0.0001f));
        Assert.That(authoredGuestEntranceSpawnPlacemark.localPosition.y, Is.EqualTo(-178f).Within(0.0001f));
        Assert.That(authoredGuestEntranceSpawnPlacemark.localPosition.z, Is.EqualTo(-7691.114f).Within(0.001f));
        Assert.That(authoredGuestEntranceSpawnPlacemark.localScale, Is.EqualTo(Vector3.one));
        Assert.That(authoredGuestEntranceSpawnPlacemark.GetComponent<RoomAnchor>().AnchorId, Is.EqualTo("Placemark_guests_entrance"));
        Assert.That(authoredGuestEntranceSpawnPlacemark.GetComponent<RoomAnchor>().RoomId, Is.EqualTo(EntranceRoom));
        Assert.That(characterizedDrawingRoomDoorTarget.localPosition.x, Is.EqualTo(-704f).Within(0.0001f));
        Assert.That(characterizedDrawingRoomDoorTarget.localPosition.y, Is.EqualTo(-116f).Within(0.0001f));
        Assert.That(characterizedDrawingRoomDoorTarget.localPosition.z, Is.EqualTo(-7691.114f).Within(0.001f));
        Assert.That(characterizedDrawingRoomDoorTarget.localScale, Is.EqualTo(new Vector3(1.5f, 1.5f, 1.5f)));
        Assert.That(characterizedDrawingRoomDoorTarget.GetComponent<RoomAnchor>().AnchorId, Is.EqualTo("GuestDrawingRoomDoorTarget"));
        Assert.That(characterizedDrawingRoomDoorTarget.GetComponent<RoomAnchor>().RoomId, Is.EqualTo(EntranceRoom));
        Assert.That(InvokePrivateStringMethod<RoomContentGroup>(arrival, "FindRoomContentGroup", EntranceRoom), Is.SameAs(characterizedEntranceRoomContent));
        Assert.That(InvokePrivateStringMethod<RoomContentGroup>(arrival, "FindRoomContentGroup", DrawingRoom), Is.SameAs(characterizedDrawingRoomContent));

        for (int i = 0; i < characterizedDrawingRoomGuestPoints.Length; i++)
        {
            Transform guestPoint = characterizedDrawingRoomGuestPoints[i];
            RoomAnchor guestPointAnchor = guestPoint.GetComponent<RoomAnchor>();
            Assert.That(InvokePrivateIntMethod<Transform>(arrival, "GetDrawingRoomGuestPoint", i), Is.SameAs(guestPoint));
            Assert.That(guestPoint.IsChildOf(characterizedDrawingRoomContent.transform), Is.True);
            Assert.That(guestPoint.localPosition.x, Is.EqualTo(expectedDrawingRoomGuestPointPositions[i].x).Within(0.0001f));
            Assert.That(guestPoint.localPosition.y, Is.EqualTo(expectedDrawingRoomGuestPointPositions[i].y).Within(0.0001f));
            Assert.That(guestPoint.localPosition.z, Is.EqualTo(expectedDrawingRoomGuestPointPositions[i].z).Within(0.001f));
            Assert.That(guestPoint.localScale, Is.EqualTo(Vector3.one));
            Assert.That(guestPointAnchor, Is.Not.Null);
            Assert.That(guestPointAnchor.AnchorId, Is.EqualTo($"DrawingRoomGuestPoint_{i + 1:00}"));
            Assert.That(guestPointAnchor.RoomId, Is.EqualTo(DrawingRoom));
        }

        Transform[] serializedGuestPoints = GetPrivateField<Transform[]>(arrival, "drawingRoomGuestPoints");
        Transform firstSerializedGuestPoint = serializedGuestPoints[0];
        Transform secondSerializedGuestPoint = serializedGuestPoints[1];

        try
        {
            serializedGuestPoints[0] = secondSerializedGuestPoint;
            serializedGuestPoints[1] = firstSerializedGuestPoint;
            Chateau.Architecture.ValidationReport reorderedGuestPointValidation = new Chateau.Architecture.ValidationReport();
            arrival.ValidateConfiguration(reorderedGuestPointValidation);
            Assert.That(
                reorderedGuestPointValidation.Messages.Any(message =>
                    message.Message.Contains("Drawing Room guest point slot 1 must reference ordered RoomAnchor 'DrawingRoomGuestPoint_01'")),
                Is.True,
                "Configuration validation must reject Inspector reordering that would silently swap guest placement.");
        }
        finally
        {
            serializedGuestPoints[0] = firstSerializedGuestPoint;
            serializedGuestPoints[1] = secondSerializedGuestPoint;
        }

        Chateau.Architecture.ValidationReport restoredGuestPointValidation = new Chateau.Architecture.ValidationReport();
        arrival.ValidateConfiguration(restoredGuestPointValidation);
        Assert.That(restoredGuestPointValidation.HasErrors, Is.False);

        Assert.That(FindInActiveScene<Transform>().Any(item => item.name.StartsWith("DrawingRoomSeat_Runtime_")), Is.False);

        Assert.That(authoredFrontDoorTrigger.localPosition.x, Is.EqualTo(-7.216162f).Within(0.0001f));
        Assert.That(authoredFrontDoorTrigger.localPosition.y, Is.EqualTo(-13.4132805f).Within(0.0001f));
        Assert.That(authoredFrontDoorTrigger.localPosition.z, Is.Zero.Within(0.001f));
        Assert.That(authoredFrontDoorRenderer.sortingLayerID, Is.EqualTo(1040854321));
        Assert.That(authoredFrontDoorRenderer.sortingOrder, Is.EqualTo(20));
        Assert.That(
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                authoredFrontDoorRenderer.sprite,
                out string frontDoorSpriteGuid,
                out long frontDoorSpriteFileId),
            Is.True);
        Assert.That(frontDoorSpriteGuid, Is.EqualTo("311925a002f4447b3a28927169b83ea6"));
        Assert.That(frontDoorSpriteFileId, Is.EqualTo(7482667652216324306L));
        int frontDoorActionCount = FindInActiveScene<Chapter1SceneAction>().Length;
        int frontDoorColliderCount = FindInActiveScene<BoxCollider2D>().Length;
        InvokePrivateMethod(arrival, "ConfigureFrontDoorAction");
        InvokePrivateMethod(arrival, "ConfigureFrontDoorAction");
        Assert.That(GetPrivateField<Chapter1SceneAction>(arrival, "frontDoorSceneAction"), Is.SameAs(authoredFrontDoorAction));
        Assert.That(authoredFrontDoorTrigger.GetComponent<SpriteRenderer>(), Is.SameAs(authoredFrontDoorRenderer));
        Assert.That(authoredFrontDoorTrigger.GetComponent<BoxCollider2D>(), Is.SameAs(authoredFrontDoorCollider));
        Assert.That(authoredFrontDoorTrigger.GetComponent<Chapter1SceneAction>(), Is.SameAs(authoredFrontDoorAction));
        Assert.That(GetPrivateValue<bool>(authoredFrontDoorAction, "isActionAvailable"), Is.True);
        Assert.That(FindInActiveScene<Transform>().Count(item => item.name == "Door_answer_trigger"), Is.EqualTo(1));
        Assert.That(FindInActiveScene<Chapter1SceneAction>(), Has.Length.EqualTo(frontDoorActionCount));
        Assert.That(FindInActiveScene<BoxCollider2D>(), Has.Length.EqualTo(frontDoorColliderCount));
        SubtitleService subtitle = RequireExactlyOneInActiveScene<SubtitleService>();
        DialogueSpeechService speech = RequireExactlyOneInActiveScene<DialogueSpeechService>();
        Assert.That(subtitle.IsInitialized, Is.True);
        Assert.That(subtitle.HasGameContext, Is.True);
        Assert.That(speech.IsInitialized, Is.True);
        Assert.That(speech.HasGameContext, Is.True);
        Assert.That(chapter1Hud.gameObject, Is.SameAs(arrival.gameObject));
        Assert.That(entranceCoatHangerRenderer, Is.Not.Null);
        Assert.That(FindInActiveScene<CoatCloset>(), Has.Length.EqualTo(1));
        Assert.That(entranceCoatCloset, Is.Not.Null);
        Assert.That(entranceCoatAction, Is.Not.Null);
        Assert.That(entranceCoatCollider, Is.Not.Null);
        Assert.That(entranceCoatHanger.GetComponents<CoatCloset>(), Has.Length.EqualTo(1));
        Assert.That(entranceCoatHanger.GetComponents<Chapter1SceneAction>(), Has.Length.EqualTo(1));
        Assert.That(entranceCoatHanger.GetComponents<BoxCollider2D>(), Has.Length.EqualTo(1));
        Assert.That(GetPrivateField<CoatCloset>(arrival, "coatCloset"), Is.SameAs(entranceCoatCloset));
        Assert.That(GetPrivateField<Transform>(arrival, "closetPoint"), Is.SameAs(entranceCoatHanger));
        Assert.That(GetPrivateValue<Chapter1SceneActionType>(entranceCoatAction, "actionType"), Is.EqualTo(Chapter1SceneActionType.CoatCloset));
        Assert.That(GetPrivateField<Chapter1ArrivalController>(entranceCoatAction, "arrivalController"), Is.SameAs(arrival));
        Assert.That(GetPrivateValue<bool>(entranceCoatAction, "isActionAvailable"), Is.True);
        Assert.That(entranceCoatCollider.enabled, Is.True);
        Assert.That(entranceCoatCollider.isTrigger, Is.True);
        Assert.That(entranceCoatCollider.offset.x, Is.Zero.Within(0.0001f));
        Assert.That(entranceCoatCollider.offset.y, Is.Zero.Within(0.0001f));
        Assert.That(entranceCoatCollider.size.x, Is.EqualTo(4.41f).Within(0.0001f));
        Assert.That(entranceCoatCollider.size.y, Is.EqualTo(9.79f).Within(0.0001f));
        Assert.That(entranceCoatCollider.edgeRadius, Is.Zero.Within(0.0001f));
        Assert.That(entranceCoatHanger.localPosition.x, Is.EqualTo(-255.00697f).Within(0.0001f));
        Assert.That(entranceCoatHanger.localPosition.y, Is.EqualTo(-12.514666f).Within(0.0001f));
        Assert.That(entranceCoatHanger.localPosition.z, Is.Zero.Within(0.001f));
        Assert.That(entranceCoatHanger.localScale.x, Is.EqualTo(30.161037f).Within(0.0001f));
        Assert.That(entranceCoatHanger.localScale.y, Is.EqualTo(23.970196f).Within(0.0001f));
        Assert.That(entranceCoatHanger.localScale.z, Is.EqualTo(63.526222f).Within(0.0001f));
        Assert.That(entranceCoatHangerRenderer.sortingLayerName, Is.EqualTo("People"));
        Assert.That(entranceCoatHangerRenderer.sortingOrder, Is.Zero);
        Assert.That(
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                entranceCoatHangerRenderer.sprite,
                out string entranceCoatSpriteGuid,
                out long entranceCoatSpriteFileId),
            Is.True);
        Assert.That(entranceCoatSpriteGuid, Is.EqualTo("60c34e6293838a6c7988f33040dad54d"));
        Assert.That(entranceCoatSpriteFileId, Is.EqualTo(1166796266648169557L));
        Assert.That(
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entranceCoatHangerRenderer.sharedMaterial)),
            Is.EqualTo("a97c105638bdf8b4a8650670310a4cd3"));

        entranceCoatCloset.StoreCoat("architecture_characterization_coat");
        InvokePrivateMethod(arrival, "ResolveReferences");
        InvokePrivateMethod(arrival, "ResolveReferences");
        Assert.That(GetPrivateField<ChapterManager>(arrival, "chapterManager"), Is.SameAs(serializedArrivalChapterManager));
        Assert.That(GetPrivateField<ChapterClock>(arrival, "chapterClock"), Is.SameAs(serializedArrivalClock));
        Assert.That(GetPrivateField<ChapterEventScheduler>(arrival, "eventScheduler"), Is.SameAs(serializedArrivalScheduler));
        Assert.That(GetPrivateField<CameraManager>(arrival, "cameraManager"), Is.SameAs(serializedArrivalCamera));
        Assert.That(GetPrivateField<RoomNavigationManager>(arrival, "navigationManager"), Is.SameAs(serializedArrivalNavigation));
        Assert.That(GetPrivateField<PointClickPlayerMovement>(arrival, "playerMovement"), Is.SameAs(serializedArrivalPlayerMovement));
        Assert.That(GetPrivateField<Chapter1SceneAction>(arrival, "frontDoorSceneAction"), Is.SameAs(authoredFrontDoorAction));
        Assert.That(GetPrivateField<GuestFootstepCatalog>(arrival, "guestFootstepCatalog"), Is.SameAs(resolvedGuestFootstepCatalog));
        Assert.That(GetPrivateField<Transform>(arrival, "guestEntranceSpawnPlacemark"), Is.SameAs(authoredGuestEntranceSpawnPlacemark));
        Assert.That(GetPrivateField<Transform>(arrival, "drawingRoomDoorTarget"), Is.SameAs(characterizedDrawingRoomDoorTarget));
        Assert.That(GetPrivateField<RoomContentGroup>(arrival, "entryRoomContent"), Is.SameAs(characterizedEntranceRoomContent));
        Assert.That(GetPrivateField<RoomContentGroup>(arrival, "drawingRoomContent"), Is.SameAs(characterizedDrawingRoomContent));
        CollectionAssert.AreEqual(characterizedDrawingRoomGuestPoints, GetPrivateField<Transform[]>(arrival, "drawingRoomGuestPoints"));
        Assert.That(authoredFrontDoorTrigger.GetComponent<BoxCollider2D>(), Is.SameAs(authoredFrontDoorCollider));
        Assert.That(GetPrivateField<GameObject>(arrival, "playerButlerReference"), Is.SameAs(serializedArrivalButlerRoot));
        Assert.That(GetPrivateField<CoatCloset>(arrival, "coatCloset"), Is.SameAs(entranceCoatCloset));
        Assert.That(GetPrivateField<Transform>(arrival, "closetPoint"), Is.SameAs(entranceCoatHanger));
        Assert.That(entranceCoatHanger.GetComponent<CoatCloset>(), Is.SameAs(entranceCoatCloset));
        Assert.That(entranceCoatHanger.GetComponent<Chapter1SceneAction>(), Is.SameAs(entranceCoatAction));
        Assert.That(entranceCoatHanger.GetComponent<BoxCollider2D>(), Is.SameAs(entranceCoatCollider));
        Assert.That(FindInActiveScene<CoatCloset>(), Has.Length.EqualTo(1));
        Assert.That(entranceCoatCloset.ContainsCoat("architecture_characterization_coat"), Is.True);
        entranceCoatCloset.ClearStoredCoats();
        Canvas chapter1Canvas = FindInActiveScene<Canvas>().Single(item => item.name == "Canvas_Chapter1HUD");
        Assert.That(chapter1Canvas.sortingOrder, Is.EqualTo(9100));
        Assert.That(FindInActiveScene<Transform>().Count(item => item.name == "Text_Chapter1Status"), Is.EqualTo(1));
        Canvas settingsCanvas = FindInActiveScene<Canvas>().Single(item => item.name == "Canvas_RuntimeSettingsMenu");
        EventSystem serializedEventSystem = RequireExactlyOneInActiveScene<EventSystem>();
        Assert.That(serializedEventSystem, Is.SameAs(characterizedTimeEventSystem));
        AudioSource explorationMusicSource = FindInActiveScene<AudioSource>()
            .Single(item => item.gameObject.name == "Audio_ExplorationMusic");
        GameAudioSourceVolume[] explorationMusicBindings = explorationMusicSource.GetComponents<GameAudioSourceVolume>();
        Assert.That(GetPrivateField<RoomNavigationManager>(runtimeSettings, "navigationManager"), Is.SameAs(navigation));
        Assert.That(GetPrivateField<ChapterManager>(runtimeSettings, "chapterManager"), Is.SameAs(chapter));
        Assert.That(GetPrivateField<ChapterClock>(runtimeSettings, "chapterClock"), Is.SameAs(clock));
        Assert.That(GetPrivateField<AudioSource>(runtimeSettings, "explorationMusicSource"), Is.SameAs(explorationMusicSource));
        Assert.That(explorationMusicBindings, Has.Length.EqualTo(1), "Exploration music must have exactly one channel-volume owner after settings initialization.");
        GameAudioSourceVolume explorationMusicBinding = explorationMusicBindings[0];
        Assert.That(explorationMusicBinding.gameObject, Is.SameAs(explorationMusicSource.gameObject));
        Assert.That(explorationMusicBinding.Channel, Is.EqualTo(GameAudioChannel.Music));
        Assert.That(explorationMusicBinding.BaseVolume, Is.EqualTo(0.125f).Within(0.0001f));
        Assert.That(explorationMusicSource.clip, Is.Not.Null);
        Assert.That(explorationMusicSource.playOnAwake, Is.False);
        Assert.That(explorationMusicSource.loop, Is.True);
        Assert.That(explorationMusicSource.spatialBlend, Is.Zero);
        Assert.That(explorationMusicSource.ignoreListenerVolume, Is.True);
        Assert.That(
            explorationMusicSource.volume,
            Is.EqualTo(explorationMusicBinding.BaseVolume * GameAudioSettings.GetVolume(GameAudioChannel.Music)).Within(0.0001f));
        int settingsDescendantCount = runtimeSettings.GetComponentsInChildren<Transform>(true).Length;
        runtimeSettings.Initialize(navigation);
        yield return null;
        Assert.That(RequireExactlyOneInActiveScene<EventSystem>(), Is.SameAs(serializedEventSystem));
        Assert.That(
            FindInActiveScene<AudioSource>().Single(item => item.gameObject.name == "Audio_ExplorationMusic"),
            Is.SameAs(explorationMusicSource));
        Assert.That(explorationMusicSource.GetComponents<GameAudioSourceVolume>(), Has.Length.EqualTo(1));
        Assert.That(explorationMusicSource.GetComponent<GameAudioSourceVolume>(), Is.SameAs(explorationMusicBinding));
        Assert.That(runtimeSettings.GetComponentsInChildren<Transform>(true), Has.Length.EqualTo(settingsDescendantCount), "Reinitializing the serialized settings owner must reuse every lazy control.");
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
        Assert.That(clockSource, Is.SameAs(serializedClockAmbienceSource));
        AssertClockAmbienceGraphRemainsCanonical(
            clockAmbience,
            serializedClockAmbienceNavigation,
            serializedClockAmbienceCatalog,
            serializedClockAmbienceSource,
            navigation);
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
        Assert.That(teaTableView.gameObject.activeInHierarchy, Is.False, "The Drawing Room set piece starts inactive with its room.");
        Assert.That(purpleArmchairView.gameObject.activeInHierarchy, Is.False);
        Assert.That(purpleArmchairView.GetComponent<RoomProjectedEntity>(), Is.Null);
        Assert.That(purpleArmchairRenderer, Is.Not.Null);
        Assert.That(purpleArmchairBlocker.SourceObject, Is.SameAs(purpleArmchairView.gameObject));
        Assert.That(purpleArmchairBlocker.SortSourceRenderers, Is.False);
        Assert.That(purpleArmchairCollider, Is.Not.Null);
        Assert.That(purpleArmchairCollider.isTrigger, Is.True);
        Assert.That(purpleArmchairView.HasGameContext, Is.True, "GameRoot should bind the inactive armchair set-piece owner.");
        Assert.That(purpleArmchairView.RoomLocalOcclusionAnchor, Is.EqualTo(new Vector2(243.62f, -315.58f)));
        Assert.That(purpleArmchairView.DepthProfile, Is.Not.Null);
        Assert.That(RoomDepthResolver.Resolve(purpleArmchairView.DepthProfile, purpleArmchairView.RoomLocalOcclusionAnchor), Is.EqualTo(8289));
        Assert.That(purpleArmchairView.CurrentSortingOrder, Is.EqualTo(8289));
        Assert.That(purpleArmchairRenderer.sortingOrder, Is.EqualTo(8289));
        Assert.That(purpleArmchairRenderer.sortingLayerName, Is.EqualTo("People"));
        Assert.That(purpleArmchairRenderer.spriteSortPoint, Is.EqualTo(SpriteSortPoint.Pivot));
        Assert.That(purpleArmchairView.transform.parent.name, Is.EqualTo("Set Pieces"));
        Assert.That(purpleArmchairView.transform.parent.parent.name, Is.EqualTo("Props"));
        Assert.That(purpleArmchairView.transform.localPosition.x, Is.EqualTo(246.18f).Within(0.001f));
        Assert.That(purpleArmchairView.transform.localPosition.y, Is.EqualTo(-323.26f).Within(0.001f));
        Assert.That(purpleArmchairView.transform.localPosition.z, Is.Zero, "RoomContentGroup currently flattens room art at runtime; the authored Z remains covered statically.");
        Assert.That(purpleArmchairView.transform.localScale.x, Is.EqualTo(98.85839f).Within(0.0001f));
        Assert.That(purpleArmchairView.transform.localScale.y, Is.EqualTo(100.35108f).Within(0.0001f));
        Assert.That(purpleArmchairView.transform.localScale.z, Is.EqualTo(73.00117f).Within(0.0001f));
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(purpleArmchairRenderer.sprite)), Is.EqualTo("84e185b06bd4d9a19842586e593673e5"));
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(purpleArmchairRenderer.sharedMaterial)), Is.EqualTo("a97c105638bdf8b4a8650670310a4cd3"));
        Assert.That(purpleSofaView.gameObject.activeInHierarchy, Is.False);
        Assert.That(purpleSofaView.GetComponent<RoomProjectedEntity>(), Is.Null);
        Assert.That(purpleSofaRenderer, Is.Not.Null);
        Assert.That(purpleSofaBlocker.SourceObject, Is.SameAs(purpleSofaView.gameObject));
        Assert.That(purpleSofaBlocker.SortSourceRenderers, Is.False);
        Assert.That(purpleSofaCollider, Is.Not.Null);
        Assert.That(purpleSofaCollider.isTrigger, Is.True);
        Assert.That(purpleSofaView.HasGameContext, Is.True, "GameRoot should bind the inactive sofa set-piece owner.");
        Assert.That(purpleSofaView.RoomLocalOcclusionAnchor, Is.EqualTo(new Vector2(-377.13f, -134.04f)));
        Assert.That(purpleSofaView.DepthProfile, Is.Not.Null);
        Assert.That(RoomDepthResolver.Resolve(purpleSofaView.DepthProfile, purpleSofaView.RoomLocalOcclusionAnchor), Is.EqualTo(5385));
        Assert.That(purpleSofaView.CurrentSortingOrder, Is.EqualTo(5385));
        Assert.That(purpleSofaRenderer.sortingOrder, Is.EqualTo(5385));
        Assert.That(purpleSofaRenderer.sortingLayerName, Is.EqualTo("People"));
        Assert.That(purpleSofaRenderer.spriteSortPoint, Is.EqualTo(SpriteSortPoint.Pivot));
        Assert.That(purpleSofaView.transform.parent.name, Is.EqualTo("Set Pieces"));
        Assert.That(purpleSofaView.transform.parent.parent.name, Is.EqualTo("Props"));
        Assert.That(purpleSofaRenderer.transform.localPosition.x, Is.EqualTo(-377.13f).Within(0.001f));
        Assert.That(purpleSofaRenderer.transform.localPosition.y, Is.EqualTo(-134.04f).Within(0.001f));
        Assert.That(purpleSofaRenderer.transform.localPosition.z, Is.Zero, "RoomContentGroup currently flattens room art at runtime; the authored Z remains covered statically.");
        Assert.That(purpleSofaRenderer.transform.localScale.x, Is.EqualTo(96.26519f).Within(0.0001f));
        Assert.That(purpleSofaRenderer.transform.localScale.y, Is.EqualTo(107.24464f).Within(0.0001f));
        Assert.That(purpleSofaRenderer.transform.localScale.z, Is.EqualTo(73.00117f).Within(0.0001f));
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(purpleSofaRenderer.sprite)), Is.EqualTo("c19ed6f7fe405144f9b93978ebeab1c7"));
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(purpleSofaRenderer.sharedMaterial)), Is.EqualTo("a97c105638bdf8b4a8650670310a4cd3"));
        Assert.That(teaTableView.HasGameContext, Is.True, "GameRoot should bind the serialized set-piece owner even while its room is inactive.");
        Assert.That(teaTableRenderer, Is.Not.Null);
        Assert.That(teaTableView.GetComponent<RoomProjectedEntity>(), Is.Null);
        Assert.That(teaTableBlocker.SourceObject, Is.SameAs(teaTableView.gameObject));
        Assert.That(teaTableBlocker.SortSourceRenderers, Is.False, "Collision keeps the accepted footprint but no longer owns visual sorting.");
        Assert.That(teaTableCollider, Is.Not.Null);
        Assert.That(teaTableCollider.isTrigger, Is.True);
        Assert.That(teaTableView.RoomLocalOcclusionAnchor, Is.EqualTo(new Vector2(-80.26f, -211.67f)));
        Assert.That(teaTableView.DepthProfile, Is.Not.Null);
        Assert.That(RoomDepthResolver.Resolve(teaTableView.DepthProfile, teaTableView.RoomLocalOcclusionAnchor), Is.EqualTo(6627));
        Assert.That(teaTableView.CurrentSortingOrder, Is.EqualTo(6627));
        Assert.That(teaTableRenderer.sortingOrder, Is.EqualTo(6627));
        Assert.That(teaTableRenderer.sortingLayerName, Is.EqualTo("People"));
        Assert.That(teaTableRenderer.spriteSortPoint, Is.EqualTo(SpriteSortPoint.Pivot));
        Assert.That(teaTableView.transform.parent.name, Is.EqualTo("Set Pieces"));
        Assert.That(teaTableView.transform.parent.parent.name, Is.EqualTo("Props"));
        Assert.That(teaTableView.transform.localPosition.x, Is.EqualTo(-80.26f).Within(0.001f));
        Assert.That(teaTableView.transform.localPosition.y, Is.EqualTo(-211.67f).Within(0.001f));
        Assert.That(teaTableView.transform.localPosition.z, Is.Zero, "RoomContentGroup currently flattens room art at runtime; the authored Z remains covered statically.");
        Assert.That(teaTableView.transform.localScale.x, Is.EqualTo(99.52793f).Within(0.0001f));
        Assert.That(teaTableView.transform.localScale.y, Is.EqualTo(99.40213f).Within(0.0001f));
        Assert.That(teaTableView.transform.localScale.z, Is.EqualTo(73.00117f).Within(0.0001f));
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
        Assert.That(
            FindInActiveScene<MonoBehaviour>().Any(item => item.GetType().Name == "GameClockHandsDisplay"),
            Is.False,
            "The legacy analog-clock runtime hook should not attach to any authored Gameplay sprite.");
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name.StartsWith("Canvas_AnalogClockHands")), Is.False);
        Assert.That(serializedChapter2.CurrentPhase, Is.EqualTo(Chapter2Phase.NotStarted));
        Assert.That(serializedChapter2.HasGameContext, Is.True);
        Assert.That(GetPrivateField<ChapterManager>(serializedChapter2, "chapterManager"), Is.SameAs(chapter));
        AudioSource serializedClockStrikeSource = GetPrivateField<AudioSource>(serializedChapter2, "clockStrikeAudioSource");
        GameAudioSourceVolume serializedClockStrikeBinding = GetPrivateField<GameAudioSourceVolume>(serializedChapter2, "clockStrikeVolumeBinding");
        AudioClip serializedClockStrikeClip = GetPrivateField<AudioClip>(serializedChapter2, "clockStrikeClip");
        Assert.That(serializedClockStrikeSource, Is.Not.Null);
        Assert.That(serializedClockStrikeBinding, Is.Not.Null);
        Assert.That(serializedClockStrikeClip, Is.Not.Null);
        Assert.That(serializedClockStrikeSource.gameObject.name, Is.EqualTo("Audio_Chapter2ClockStrike"));
        Assert.That(serializedClockStrikeSource.transform.parent, Is.SameAs(serializedChapter2.transform));
        Assert.That(serializedClockStrikeSource.clip, Is.SameAs(serializedClockStrikeClip));
        Assert.That(serializedClockStrikeBinding.gameObject, Is.SameAs(serializedClockStrikeSource.gameObject));
        Assert.That(GetPrivateField<AudioSource>(serializedClockStrikeBinding, "audioSource"), Is.SameAs(serializedClockStrikeSource));
        Assert.That(serializedClockStrikeBinding.Channel, Is.EqualTo(GameAudioChannel.GameSounds));
        Assert.That(serializedClockStrikeBinding.BaseVolume, Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(serializedChapter2.GetComponents<AudioSource>(), Is.Empty);
        Assert.That(serializedChapter2.GetComponents<GameAudioSourceVolume>(), Is.Empty);
        Assert.That(serializedClockStrikeSource.GetComponents<AudioSource>(), Has.Length.EqualTo(1));
        Assert.That(serializedClockStrikeSource.GetComponents<GameAudioSourceVolume>(), Has.Length.EqualTo(1));
        Assert.That(serializedMonsterStinger.HasGameContext, Is.True);
        Assert.That(serializedMonsterObject, Is.Not.Null);
        Assert.That(serializedMonsterObject.name, Is.EqualTo("Ch2_Monster"));
        Assert.That(serializedMonsterRunStart, Is.Not.Null);
        Assert.That(serializedMonsterRunStart.name, Is.EqualTo("Ch2_MonsterRunStart"));
        Assert.That(serializedMonsterRunTarget, Is.Not.Null);
        Assert.That(serializedMonsterRunTarget.name, Is.EqualTo("Ch2_MonsterFreezeTarget"));
        Assert.That(serializedMonsterNavigation, Is.SameAs(navigation));
        Assert.That(serializedMonsterImage, Is.Not.Null);
        Assert.That(serializedMonsterImage.gameObject, Is.SameAs(serializedMonsterObject));
        Assert.That(serializedMonsterSpriteRenderer, Is.Null);
        Assert.That(serializedMonsterOriginalSprite, Is.Not.Null);
        Assert.That(serializedMonsterOverlayCanvas, Is.Not.Null);
        Assert.That(serializedMonsterOverlayCanvas.gameObject, Is.SameAs(serializedMonsterObject));
        Assert.That(serializedMonsterOverlayCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(serializedMonsterOverlayCanvas.overrideSorting, Is.True);
        Assert.That(serializedMonsterOverlayCanvas.sortingLayerName, Is.EqualTo("People"));
        Assert.That(serializedMonsterOverlayCanvas.sortingOrder, Is.EqualTo(10000));
        Assert.That(serializedMonsterViolinSource, Is.Not.Null);
        Assert.That(serializedMonsterViolinBinding, Is.Not.Null);
        Assert.That(serializedMonsterViolinClip, Is.Not.Null);
        Assert.That(serializedMonsterViolinSource.gameObject, Is.SameAs(serializedMonsterObject));
        Assert.That(serializedMonsterViolinBinding.gameObject, Is.SameAs(serializedMonsterObject));
        Assert.That(GetPrivateField<AudioSource>(serializedMonsterViolinBinding, "audioSource"), Is.SameAs(serializedMonsterViolinSource));
        Assert.That(serializedMonsterViolinSource.clip, Is.SameAs(serializedMonsterViolinClip));
        Assert.That(serializedMonsterStinger.GetComponents<AudioSource>(), Is.Empty);
        Assert.That(serializedMonsterStinger.GetComponents<GameAudioSourceVolume>(), Is.Empty);
        Assert.That(serializedMonsterObject.GetComponents<AudioSource>(), Has.Length.EqualTo(1));
        Assert.That(serializedMonsterObject.GetComponents<GameAudioSourceVolume>(), Has.Length.EqualTo(1));
        Assert.That(serializedMonsterRunSprites, Has.Length.EqualTo(expectedMonsterRunSpriteGuids.Length));
        for (int i = 0; i < expectedMonsterRunSpriteGuids.Length; i++)
        {
            Assert.That(serializedMonsterRunSprites[i], Is.Not.Null);
            Assert.That(
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(serializedMonsterRunSprites[i])),
                Is.EqualTo(expectedMonsterRunSpriteGuids[i]));
        }
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name == "Chapter2_MonsterPlaceholder_Runtime"), Is.False);
        Assert.That(serializedGuestPanic.HasGameContext, Is.True);
        Assert.That(serializedGuestSearch.HasGameContext, Is.True);
        Assert.That(serializedGuestSearchNavigation, Is.SameAs(navigation));
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
        Assert.That(serializedArrivalPlayerMovement, Is.SameAs(player));
        Assert.That(serializedArrivalButlerRoot, Is.SameAs(playerObject));
        PlayerMovement legacyPlayerMovement = playerObject.GetComponent<PlayerMovement>();
        CharacterController2D legacyPlayerController = playerObject.GetComponent<CharacterController2D>();
        Assert.That(legacyPlayerMovement, Is.Not.Null);
        Assert.That(legacyPlayerController, Is.Not.Null);
        Assert.That(GetPrivateField<PointClickPlayerMovement>(chapter, "playerInput"), Is.SameAs(player));
        Assert.That(GetPrivateField<Chapter2Controller>(chapter, "chapter2Controller"), Is.SameAs(serializedChapter2));
        Assert.That(chapter.PlayerButlerReference, Is.SameAs(playerObject));
        Assert.That(player.enabled, Is.True);
        Assert.That(legacyPlayerMovement.enabled, Is.False);
        Assert.That(legacyPlayerController.enabled, Is.False);
        Assert.That(serializedGuestScaleApplier.Calibration, Is.SameAs(serializedGuestScaleCalibration));
        Assert.That(serializedGuestScaleCalibration.ButlerScaleSource, Is.SameAs(player));
        Assert.That(serializedVoicePlayback.IsPlaying, Is.False);
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name == "Canvas_Subtitles"), Is.False);
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name == "Sprite_ChatBubble"), Is.False);

        const string catalogedVoiceLineId = "SUB_CH01_BUTLER_WELCOME_001";
        Assert.That(
            subtitle.TryResolveSpeechLine(
                catalogedVoiceLineId,
                "Butler",
                "Welcome to Chateau Chantilly.",
                out string catalogedSpeaker,
                out string catalogedSubtitle,
                out _,
                out _),
            Is.True);
        Assert.That(catalogedSpeaker, Is.EqualTo("Butler"));
        Assert.That(catalogedSubtitle, Is.EqualTo("Good evening. Welcome to Chateau Chantilly."));
        Assert.That(
            serializedVoicePlayback.TryGetDialogueClip(
                catalogedVoiceLineId,
                "Butler",
                "Welcome to Chateau Chantilly.",
                out AudioClip catalogedVoiceClip,
                out float catalogedVoiceVolume),
            Is.True);
        Assert.That(catalogedVoiceClip, Is.Not.Null);
        Assert.That(catalogedVoiceClip.name, Is.EqualTo(catalogedVoiceLineId));
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(catalogedVoiceClip)), Is.EqualTo("84efe7bf90e143bfb05245e94cffe965"));
        Assert.That(catalogedVoiceClip.length, Is.GreaterThan(0f));
        AudioSource serializedVoiceSource = GetPrivateField<AudioSource>(serializedVoicePlayback, "audioSource");
        Assert.That(serializedVoiceSource, Is.Not.Null);
        GameAudioSourceVolume serializedVoiceVolume = GetPrivateField<GameAudioSourceVolume>(serializedVoicePlayback, "audioVolumeBinding");
        Assert.That(serializedVoiceVolume, Is.Not.Null, "The primary dialogue source must have its volume owner before first speech.");
        Assert.That(serializedVoiceSource.GetComponent<GameAudioSourceVolume>(), Is.SameAs(serializedVoiceVolume));
        bool inputEnabledBeforeSpeechTest = player.InputEnabled;
        player.SetInputEnabled(true);
        bool firstBlockingSpeechStarted = false;
        bool firstBlockingSpeechCompleted = false;
        bool inputEnabledWhenFirstSpeechStarted = true;
        speech.BeginSpeakLine(
            catalogedVoiceLineId,
            "Butler",
            "Welcome to Chateau Chantilly.",
            blockInput: true,
            onComplete: () => firstBlockingSpeechCompleted = true,
            showSubtitleOverlay: false,
            onSpeechLineStarted: (_, _) =>
            {
                firstBlockingSpeechStarted = true;
                inputEnabledWhenFirstSpeechStarted = player.InputEnabled;
            });
        yield return null;

        Assert.That(firstBlockingSpeechStarted, Is.True);
        Assert.That(inputEnabledWhenFirstSpeechStarted, Is.False, "Blocking dialogue must disable the serialized Butler movement owner before presenting the line.");
        Assert.That(serializedVoiceSource.clip, Is.SameAs(catalogedVoiceClip));
        Assert.That(serializedVoiceSource.GetComponent<GameAudioSourceVolume>(), Is.SameAs(serializedVoiceVolume));
        Assert.That(serializedVoiceVolume.Channel, Is.EqualTo(GameAudioChannel.Dialogue));
        Assert.That(serializedVoiceVolume.BaseVolume, Is.EqualTo(catalogedVoiceVolume).Within(0.0001f));
        Assert.That(
            serializedVoiceSource.volume,
            Is.EqualTo(catalogedVoiceVolume * GameAudioSettings.GetVolume(GameAudioChannel.Dialogue)).Within(0.0001f));
        speech.CancelQueuedSpeech();
        for (int frame = 0; frame < 60 && !firstBlockingSpeechCompleted; frame++)
        {
            yield return null;
        }
        Assert.That(firstBlockingSpeechCompleted, Is.True, "Cancelled blocking dialogue must unwind its input lease.");
        Assert.That(speech.IsNormalSpeechActive, Is.False);
        Assert.That(serializedVoicePlayback.IsPlaying, Is.False);
        Assert.That(serializedVoiceSource.clip, Is.Null);
        Assert.That(player.InputEnabled, Is.True, "Cancelling blocking dialogue must restore previously enabled input.");

        player.SetInputEnabled(false);
        bool secondBlockingSpeechStarted = false;
        bool secondBlockingSpeechCompleted = false;
        bool inputEnabledWhenSecondSpeechStarted = true;
        speech.BeginSpeakLine(
            catalogedVoiceLineId,
            "Butler",
            "Welcome to Chateau Chantilly.",
            blockInput: true,
            onComplete: () => secondBlockingSpeechCompleted = true,
            showSubtitleOverlay: false,
            onSpeechLineStarted: (_, _) =>
            {
                secondBlockingSpeechStarted = true;
                inputEnabledWhenSecondSpeechStarted = player.InputEnabled;
            });
        yield return null;
        Assert.That(secondBlockingSpeechStarted, Is.True);
        Assert.That(inputEnabledWhenSecondSpeechStarted, Is.False);
        Assert.That(serializedVoiceSource.GetComponent<GameAudioSourceVolume>(), Is.SameAs(serializedVoiceVolume));
        speech.CancelQueuedSpeech();
        for (int frame = 0; frame < 60 && !secondBlockingSpeechCompleted; frame++)
        {
            yield return null;
        }
        Assert.That(secondBlockingSpeechCompleted, Is.True);
        Assert.That(serializedVoiceSource.clip, Is.Null);
        Assert.That(player.InputEnabled, Is.False, "Cancelling dialogue must not enable input that was already disabled.");

        player.SetInputEnabled(true);
        bool transitionSpeechStarted = false;
        bool inputEnabledWhenTransitionSpeechStarted = true;
        speech.BeginSpeakLine(
            catalogedVoiceLineId,
            "Butler",
            "Welcome to Chateau Chantilly.",
            blockInput: true,
            showSubtitleOverlay: false,
            onSpeechLineStarted: (_, _) =>
            {
                transitionSpeechStarted = true;
                inputEnabledWhenTransitionSpeechStarted = player.InputEnabled;
            });
        yield return null;
        Assert.That(transitionSpeechStarted, Is.True);
        Assert.That(inputEnabledWhenTransitionSpeechStarted, Is.False);
        speech.CancelQueuedSpeech();
        Assert.That(player.InputEnabled, Is.True, "Cancellation must release blocking input synchronously before a transition applies its own state.");
        player.SetInputEnabled(false);
        yield return null;
        yield return null;
        Assert.That(player.InputEnabled, Is.False, "A cancelled speech coroutine must not re-enable input after the transition disabled it.");

        player.SetInputEnabled(true);
        bool replacementSpeechStarted = false;
        bool inputEnabledWhenReplacementSpeechStarted = true;
        speech.BeginSpeakLine(
            catalogedVoiceLineId,
            "Butler",
            "Welcome to Chateau Chantilly.",
            blockInput: true,
            showSubtitleOverlay: false,
            onSpeechLineStarted: (_, _) =>
            {
                replacementSpeechStarted = true;
                inputEnabledWhenReplacementSpeechStarted = player.InputEnabled;
            });
        yield return null;
        Assert.That(replacementSpeechStarted, Is.True);
        Assert.That(inputEnabledWhenReplacementSpeechStarted, Is.False);
        speech.CancelQueuedSpeech();
        speech.BeginSpeakLine(
            catalogedVoiceLineId,
            "Butler",
            "Welcome to Chateau Chantilly.",
            blockInput: true,
            showSubtitleOverlay: false);
        yield return null;
        Assert.That(player.InputEnabled, Is.False, "An older cancelled routine must not release a newer blocking-input lease.");
        yield return null;
        Assert.That(player.InputEnabled, Is.False);
        speech.CancelQueuedSpeech();
        Assert.That(player.InputEnabled, Is.True);
        yield return null;
        player.SetInputEnabled(inputEnabledBeforeSpeechTest);

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
        DoorTriggerNavigation reverse = RequireSceneObject<DoorTriggerNavigation>(
            "DoorTrigger_DrawingRoom_GEH");
        RectTransform outboundRect = outbound.transform as RectTransform;
        RectTransform reverseRect = reverse.transform as RectTransform;
        Image outboundImage = outbound.GetComponent<Image>();
        Image reverseImage = reverse.GetComponent<Image>();
        TMP_Text passagePromptText = GetPrivateField<TMP_Text>(prompts, "promptText");
        AudioSource passageAudioSource = FindInActiveScene<AudioSource>()
            .Single(item => item.gameObject.name == "Audio_DoorOpen");
        int characterizedDoorTriggerCount = FindInActiveScene<DoorTriggerNavigation>().Length;
        int characterizedRoomContentGroupCount = FindInActiveScene<RoomContentGroup>().Length;
        int characterizedAudioSourceCount = FindInActiveScene<AudioSource>().Length;
        int characterizedCanvasCount = FindInActiveScene<Canvas>().Length;
        int characterizedTextCount = FindInActiveScene<TMP_Text>().Length;
        Vector2 outboundAnchoredPosition = outboundRect.anchoredPosition;
        Vector2 outboundSizeDelta = outboundRect.sizeDelta;
        Vector2 reverseAnchoredPosition = reverseRect.anchoredPosition;
        Vector2 reverseSizeDelta = reverseRect.sizeDelta;
        Vector3 entranceStageLocalPosition = characterizedEntranceRoomContent.transform.localPosition;
        Vector3 entranceStageLocalScale = characterizedEntranceRoomContent.transform.localScale;
        Assert.That(characterizedEntranceRoomContent.TryGetRoomBackgroundTexture(out Texture entranceBackground), Is.True);
        Assert.That(characterizedDrawingRoomContent.TryGetRoomBackgroundTexture(out Texture drawingRoomBackground), Is.True);
        List<string> characterizedPassageRoomChanges = new List<string>();
        UnityEngine.Events.UnityAction<string> recordPassageRoomChange = characterizedPassageRoomChanges.Add;
        navigation.OnCurrentRoomChanged.AddListener(recordPassageRoomChange);
        bool characterizedPassageInputEnabled = player.InputEnabled;

        Assert.That(outbound.SourceRoom, Is.EqualTo(EntranceRoom));
        Assert.That(outbound.DoorName, Is.EqualTo("GEH_Drawing_Room"));
        Assert.That(outbound.DestinationRoom, Is.EqualTo(DrawingRoom));
        Assert.That(outbound.UsesCameraSequence, Is.False);
        Assert.That(outbound.IsStairway, Is.False);
        Assert.That(reverse.SourceRoom, Is.EqualTo(DrawingRoom));
        Assert.That(reverse.DoorName, Is.EqualTo("DrawingRoom_GEH"));
        Assert.That(reverse.DestinationRoom, Is.EqualTo(EntranceRoom));
        Assert.That(reverse.UsesCameraSequence, Is.False);
        Assert.That(reverse.IsStairway, Is.False);
        Assert.That(outboundRect, Is.Not.Null);
        Assert.That(reverseRect, Is.Not.Null);
        Assert.That(outboundRect.parent.name, Is.EqualTo("Doors"));
        Assert.That(reverseRect.parent.name, Is.EqualTo("Doors"));
        Assert.That(outboundAnchoredPosition, Is.EqualTo(new Vector2(-687.8042f, 18.2886f)));
        Assert.That(outboundSizeDelta, Is.EqualTo(new Vector2(211.9224f, 341.6918f)));
        Assert.That(reverseAnchoredPosition, Is.EqualTo(new Vector2(582.52795f, 53.43762f)));
        Assert.That(reverseSizeDelta, Is.EqualTo(new Vector2(345.5079f, 363.6107f)));
        Assert.That(outboundImage, Is.Not.Null);
        Assert.That(reverseImage, Is.Not.Null);
        Assert.That(GetPrivateField<Image>(outbound, "image"), Is.SameAs(outboundImage));
        Assert.That(GetPrivateField<RoomNavigationManager>(outbound, "navigationManager"), Is.SameAs(navigation));
        Assert.That(GetPrivateField<Transform>(outbound, "player"), Is.SameAs(player.transform),
            "The characterized trigger must deserialize the exact Player transform before interaction.");
        Assert.That(GetPrivateField<AudioSource>(outbound, "doorOpenAudioSource"), Is.SameAs(passageAudioSource));
        DoorOpenSoundCatalog passageDoorCatalog = GetPrivateField<DoorOpenSoundCatalog>(outbound, "doorOpenSoundCatalog");
        Assert.That(passageDoorCatalog, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(passageDoorCatalog), Is.EqualTo("Assets/Resources/Audio/DoorOpenSoundCatalog.asset"));
        AssertDoorTriggerCompatibilityBindings(
            outbound,
            reverse,
            navigation,
            player.transform,
            passageAudioSource,
            passageDoorCatalog);
        AudioClip characterizedPassageFallbackClip = passageAudioSource.clip;
        int passageAudioBindingCountBeforeUse = FindInActiveScene<GameAudioSourceVolume>().Length;
        Assert.That(passageAudioSource.GetComponents<GameAudioSourceVolume>(), Is.Empty,
            "The current shared passage source receives its legacy volume binding on first use.");
        Assert.That(InvokePrivateResult<bool>(outbound, "TryPlayNavigationSoundNow"), Is.True);
        GameAudioSourceVolume characterizedPassageAudioBinding = passageAudioSource.GetComponent<GameAudioSourceVolume>();
        Assert.That(characterizedPassageAudioBinding, Is.Not.Null);
        Assert.That(GetPrivateField<AudioSource>(characterizedPassageAudioBinding, "audioSource"), Is.SameAs(passageAudioSource));
        Assert.That(characterizedPassageAudioBinding.Channel, Is.EqualTo(GameAudioChannel.GameSounds));
        Assert.That(characterizedPassageAudioBinding.BaseVolume, Is.EqualTo(0.8f).Within(0.0001f));
        Assert.That(passageAudioSource.clip, Is.SameAs(characterizedPassageFallbackClip));
        Assert.That(FindInActiveScene<GameAudioSourceVolume>(), Has.Length.EqualTo(passageAudioBindingCountBeforeUse + 1));
        InvokePrivateStaticMethod(typeof(DoorTriggerNavigation), "StopCurrentNavigationSound");
        Assert.That(GetPrivateStaticField<AudioSource>(typeof(DoorTriggerNavigation), "activeNavigationAudioSource"), Is.Null);
        Assert.That(InvokePrivateResult<bool>(outbound, "TryPlayNavigationSoundNow"), Is.True);
        Assert.That(passageAudioSource.GetComponent<GameAudioSourceVolume>(), Is.SameAs(characterizedPassageAudioBinding));
        Assert.That(passageAudioSource.GetComponents<GameAudioSourceVolume>(), Has.Length.EqualTo(1));
        Assert.That(FindInActiveScene<GameAudioSourceVolume>(), Has.Length.EqualTo(passageAudioBindingCountBeforeUse + 1));
        InvokePrivateStaticMethod(typeof(DoorTriggerNavigation), "StopCurrentNavigationSound");
        int characterizedAudioBindingCount = FindInActiveScene<GameAudioSourceVolume>().Length;
        Assert.That(characterizedEntranceRoomContent.gameObject.activeSelf, Is.True);
        Assert.That(characterizedEntranceRoomContent.gameObject.activeInHierarchy, Is.True);
        Assert.That(characterizedDrawingRoomContent.gameObject.activeSelf, Is.False);
        Assert.That(characterizedDrawingRoomContent.gameObject.activeInHierarchy, Is.False);
        AssertCanonicalRoomViewsRemainStable(
            characterizedEntranceRoomView,
            characterizedDrawingRoomView,
            characterizedEntranceRoomDefinition,
            characterizedDrawingRoomDefinition,
            characterizedEntranceRoomContent,
            characterizedDrawingRoomContent,
            entranceVisible: true,
            drawingVisible: false);
        Assert.That(outbound.gameObject.activeInHierarchy, Is.True);
        Assert.That(reverse.gameObject.activeInHierarchy, Is.False);
        Assert.That(GetPrivateField<RoomContentGroup>(cameraManager, "activeRoomContentGroup"), Is.SameAs(characterizedEntranceRoomContent));
        Assert.That(GetPrivateField<RectTransform>(cameraManager, "activeRoomStage"), Is.SameAs(characterizedEntranceRoomContent.transform));
        Assert.That(cameraManager.cameraBackground.texture, Is.SameAs(entranceBackground));
        Assert.That(passagePromptText, Is.Not.Null);
        Assert.That(passagePromptText.gameObject.activeSelf, Is.False);
        Assert.That(DoorTriggerNavigation.HoveredTrigger, Is.Null);
        Assert.That(GetPrivateStaticField<object>(typeof(NavigationCursorController), "doorHoverOwner"), Is.Null);
        Assert.That(player.InputEnabled, Is.EqualTo(characterizedPassageInputEnabled));
        Assert.That(player.HasDestination, Is.False);
        Assert.That(RuntimeSettingsMenu.BlocksGameInput, Is.False);
        Assert.That(chapter.CurrentChapterId, Is.EqualTo(ChapterManager.Chapter1Id));

        player.SetInputEnabled(true);
        outbound.OnPointerEnter(null);
        Assert.That(GetPrivateField<Transform>(outbound, "player"), Is.SameAs(player.transform));
        Assert.That(DoorTriggerNavigation.HoveredTrigger, Is.SameAs(outbound));
        Assert.That(GetPrivateStaticField<object>(typeof(NavigationCursorController), "doorHoverOwner"), Is.SameAs(outbound));
        Assert.That(passagePromptText.gameObject.activeSelf, Is.True);
        Assert.That(passagePromptText.text, Is.EqualTo("Open Door"));
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
        Assert.That(characterizedPassageRoomChanges, Is.EqualTo(new[] { DrawingRoom }));
        Assert.That(characterizedEntranceRoomContent.gameObject.activeSelf, Is.False);
        Assert.That(characterizedEntranceRoomContent.gameObject.activeInHierarchy, Is.False);
        Assert.That(characterizedDrawingRoomContent.gameObject.activeSelf, Is.True);
        Assert.That(characterizedDrawingRoomContent.gameObject.activeInHierarchy, Is.True);
        AssertCanonicalRoomViewsRemainStable(
            characterizedEntranceRoomView,
            characterizedDrawingRoomView,
            characterizedEntranceRoomDefinition,
            characterizedDrawingRoomDefinition,
            characterizedEntranceRoomContent,
            characterizedDrawingRoomContent,
            entranceVisible: false,
            drawingVisible: true);
        Assert.That(outbound.gameObject.activeInHierarchy, Is.False);
        Assert.That(reverse.gameObject.activeInHierarchy, Is.True);
        Assert.That(GetPrivateField<RoomContentGroup>(cameraManager, "activeRoomContentGroup"), Is.SameAs(characterizedDrawingRoomContent));
        Assert.That(GetPrivateField<RectTransform>(cameraManager, "activeRoomStage"), Is.SameAs(characterizedDrawingRoomContent.transform));
        Assert.That(cameraManager.cameraBackground.texture, Is.SameAs(drawingRoomBackground));
        Assert.That(GetPrivateField<Image>(reverse, "image"), Is.SameAs(reverseImage));
        Assert.That(GetPrivateField<RoomNavigationManager>(reverse, "navigationManager"), Is.SameAs(navigation));
        Assert.That(GetPrivateField<Transform>(reverse, "player"), Is.SameAs(player.transform),
            "The inactive reverse trigger must already contain the serialized Player transform.");
        Assert.That(GetPrivateField<AudioSource>(reverse, "doorOpenAudioSource"), Is.SameAs(passageAudioSource));
        Assert.That(GetPrivateField<DoorOpenSoundCatalog>(reverse, "doorOpenSoundCatalog"), Is.SameAs(passageDoorCatalog));
        Assert.That(DoorTriggerNavigation.HoveredTrigger, Is.Null);
        Assert.That(GetPrivateStaticField<object>(typeof(NavigationCursorController), "doorHoverOwner"), Is.Null);
        Assert.That(passagePromptText.gameObject.activeSelf, Is.False);
        Assert.That(player.InputEnabled, Is.True,
            "Room traversal must not mutate the input state enabled for passage interaction.");
        Assert.That(player.HasDestination, Is.False);
        Vector2 characterizedDrawingRoomArrival = player.LogicalPosition;
        Assert.That(reverse.TryFindArrivalDestination(player, out Vector2 evaluatedDrawingRoomArrival), Is.True);
        Assert.That(float.IsNaN(characterizedDrawingRoomArrival.x) || float.IsInfinity(characterizedDrawingRoomArrival.x), Is.False);
        Assert.That(float.IsNaN(characterizedDrawingRoomArrival.y) || float.IsInfinity(characterizedDrawingRoomArrival.y), Is.False);
        Assert.That(float.IsNaN(evaluatedDrawingRoomArrival.x) || float.IsInfinity(evaluatedDrawingRoomArrival.x), Is.False);
        Assert.That(float.IsNaN(evaluatedDrawingRoomArrival.y) || float.IsInfinity(evaluatedDrawingRoomArrival.y), Is.False);
        Assert.That(characterizedDrawingRoomArrival.x, Is.EqualTo(5.267176f).Within(0.005f));
        Assert.That(characterizedDrawingRoomArrival.y, Is.EqualTo(-2.104616f).Within(0.005f));
        Assert.That(chapter.CurrentChapterId, Is.EqualTo(ChapterManager.Chapter1Id));
        Debug.Log($"[PassageCharacterization] forwardArrival={characterizedDrawingRoomArrival.x:0.######},{characterizedDrawingRoomArrival.y:0.######}");
        Assert.That(RequireExactlyOneInActiveScene<FireplaceAmbienceController>(), Is.SameAs(fireplaceAmbience));
        Assert.That(RequireExactlyOneInActiveScene<ClockTickingAmbienceController>(), Is.SameAs(clockAmbience));
        AssertClockAmbienceGraphRemainsCanonical(
            clockAmbience,
            serializedClockAmbienceNavigation,
            serializedClockAmbienceCatalog,
            serializedClockAmbienceSource,
            navigation);
        Assert.That(fireplaceAmbience.GetComponent<AudioSource>(), Is.SameAs(fireplaceSource));
        Assert.That(clockAmbience.GetComponent<AudioSource>(), Is.SameAs(clockSource));
        Assert.That(fireplaceSource.clip, Is.Not.Null);
        Assert.That(clockSource.clip, Is.Not.Null);
        teaTableRenderer.enabled = false;
        Assert.That(teaTableRenderer.enabled, Is.False);
        characterizedDrawingRoomContent.gameObject.SetActive(false);
        Assert.That(teaTableRenderer.gameObject.activeInHierarchy, Is.False);
        Assert.That(characterizedDrawingRoomView.IsVisible, Is.False);
        Assert.That(characterizedDrawingRoomView.IsVisible, Is.EqualTo(characterizedDrawingRoomContent.gameObject.activeSelf));
        characterizedDrawingRoomContent.gameObject.SetActive(true);
        Assert.That(characterizedDrawingRoomView.IsVisible, Is.True);
        Assert.That(characterizedDrawingRoomView.IsVisible, Is.EqualTo(characterizedDrawingRoomContent.gameObject.activeSelf));
        Assert.That(teaTableRenderer.enabled, Is.True,
            "Legacy RoomContentGroup reactivation currently force-enables disabled descendant renderers; this defect is intentionally characterized before its ownership fix.");
        Assert.That(RequireOnlyActiveRoom(DrawingRoom), Is.SameAs(characterizedDrawingRoomContent));
        Assert.That(teaTableView.gameObject.activeInHierarchy, Is.True);
        Assert.That(purpleArmchairView.gameObject.activeInHierarchy, Is.True);
        Assert.That(purpleArmchairBlocker.gameObject.activeInHierarchy, Is.True);
        Vector2[] purpleArmchairFootprint = purpleArmchairCollider.points;
        Assert.That(purpleArmchairFootprint, Has.Length.EqualTo(4));
        Assert.That(purpleArmchairFootprint[0], Is.EqualTo(new Vector2(163.29713f, -469.27084f)));
        Assert.That(purpleArmchairFootprint[1], Is.EqualTo(new Vector2(329.0629f, -469.27084f)));
        Assert.That(purpleArmchairFootprint[2], Is.EqualTo(new Vector2(329.0629f, -381.66437f)));
        Assert.That(purpleArmchairFootprint[3], Is.EqualTo(new Vector2(163.29713f, -381.66437f)));
        purpleArmchairRenderer.sortingOrder = -22345;
        Assert.That(purpleArmchairView.ApplyPresentation(), Is.True);
        int purpleSetPieceOrder = purpleArmchairRenderer.sortingOrder;
        Assert.That(purpleSetPieceOrder, Is.EqualTo(8289));
        Assert.That(purpleArmchairView.CurrentSortingOrder, Is.EqualTo(8289));
        purpleArmchairRenderer.sortingOrder = -22346;
        Physics2D.SyncTransforms();
        purpleArmchairBlocker.ApplySourceSortingNow();
        Assert.That(purpleArmchairRenderer.sortingOrder, Is.EqualTo(-22346), "Collision must not overwrite armchair presentation.");
        Assert.That(purpleArmchairView.ApplyPresentation(), Is.True);
        Assert.That(purpleArmchairRenderer.sortingOrder, Is.EqualTo(purpleSetPieceOrder));
        CollectionAssert.AreEqual(purpleArmchairFootprint, purpleArmchairCollider.points);
        Debug.Log($"[SetPieceMigration] purple_armchair_back order={purpleSetPieceOrder} blockerSorting={purpleArmchairBlocker.SortSourceRenderers} footprintPoints={purpleArmchairCollider.points.Length}");
        Assert.That(purpleSofaView.gameObject.activeInHierarchy, Is.True);
        Assert.That(purpleSofaBlocker.gameObject.activeInHierarchy, Is.True);
        Vector2[] purpleSofaFootprint = purpleSofaCollider.points;
        Assert.That(purpleSofaFootprint, Has.Length.EqualTo(4));
        Assert.That(purpleSofaFootprint[0], Is.EqualTo(new Vector2(-509.97592f, -252.54533f)));
        Assert.That(purpleSofaFootprint[1], Is.EqualTo(new Vector2(-244.28398f, -252.54533f)));
        Assert.That(purpleSofaFootprint[2], Is.EqualTo(new Vector2(-244.28398f, -176.70192f)));
        Assert.That(purpleSofaFootprint[3], Is.EqualTo(new Vector2(-509.97592f, -176.70192f)));
        purpleSofaRenderer.sortingOrder = -32345;
        Assert.That(purpleSofaView.ApplyPresentation(), Is.True);
        int purpleSofaOrder = purpleSofaRenderer.sortingOrder;
        Assert.That(purpleSofaOrder, Is.EqualTo(5385));
        Assert.That(purpleSofaView.CurrentSortingOrder, Is.EqualTo(5385));
        purpleSofaRenderer.sortingOrder = -32346;
        Physics2D.SyncTransforms();
        purpleSofaBlocker.ApplySourceSortingNow();
        Assert.That(purpleSofaRenderer.sortingOrder, Is.EqualTo(-32346), "Collision must not overwrite sofa presentation.");
        Assert.That(purpleSofaView.ApplyPresentation(), Is.True);
        Assert.That(purpleSofaRenderer.sortingOrder, Is.EqualTo(purpleSofaOrder));
        CollectionAssert.AreEqual(purpleSofaFootprint, purpleSofaCollider.points);
        Debug.Log($"[SetPieceMigration] purple_sofa order={purpleSofaOrder} blockerSorting={purpleSofaBlocker.SortSourceRenderers} footprintPoints={purpleSofaCollider.points.Length}");
        Assert.That(FindInActiveScene<MonoBehaviour>().Any(item => item.GetType().Name == "GameClockHandsDisplay"), Is.False);
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name.StartsWith("Canvas_AnalogClockHands")), Is.False);
        Assert.That(teaTableBlocker.gameObject.activeInHierarchy, Is.True);
        teaTableRenderer.sortingOrder = -12345;
        Assert.That(teaTableView.ApplyPresentation(), Is.True);
        int projectionSortingOrder = teaTableRenderer.sortingOrder;
        Assert.That(projectionSortingOrder, Is.EqualTo(6627));
        Assert.That(projectionSortingOrder, Is.EqualTo(teaTableView.CurrentSortingOrder));

        teaTableRenderer.sortingOrder = -12346;
        Physics2D.SyncTransforms();
        teaTableBlocker.ApplySourceSortingNow();
        Assert.That(teaTableRenderer.sortingOrder, Is.EqualTo(-12346), "Navigation collision must not overwrite set-piece presentation.");

        Assert.That(teaTableView.ApplyPresentation(), Is.True);
        Assert.That(teaTableRenderer.sortingOrder, Is.EqualTo(projectionSortingOrder));
        Debug.Log(
            $"[SetPieceMigration] tea_service_table order={projectionSortingOrder} " +
            $"blockerSorting={teaTableBlocker.SortSourceRenderers} footprintPoints={teaTableCollider.points.Length}");

        reverse.OnPointerEnter(null);
        Assert.That(GetPrivateField<Transform>(reverse, "player"), Is.SameAs(player.transform));
        Assert.That(DoorTriggerNavigation.HoveredTrigger, Is.SameAs(reverse));
        Assert.That(GetPrivateStaticField<object>(typeof(NavigationCursorController), "doorHoverOwner"), Is.SameAs(reverse));
        Assert.That(passagePromptText.gameObject.activeSelf, Is.True);
        Assert.That(passagePromptText.text, Is.EqualTo("Open Door"));
        Assert.That(
            navigation.MoveThroughInspectorDoor(
                reverse.SourceRoom,
                reverse.DoorName,
                reverse.DestinationRoom,
                true),
            Is.True);

        yield return WaitForSettledLayout();
        Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom));
        Assert.That(characterizedPassageRoomChanges, Is.EqualTo(new[] { DrawingRoom, EntranceRoom }));
        navigation.OnCurrentRoomChanged.RemoveListener(recordPassageRoomChange);
        Assert.That(characterizedEntranceRoomContent.gameObject.activeSelf, Is.True);
        Assert.That(characterizedEntranceRoomContent.gameObject.activeInHierarchy, Is.True);
        Assert.That(characterizedDrawingRoomContent.gameObject.activeSelf, Is.False);
        Assert.That(characterizedDrawingRoomContent.gameObject.activeInHierarchy, Is.False);
        AssertCanonicalRoomViewsRemainStable(
            characterizedEntranceRoomView,
            characterizedDrawingRoomView,
            characterizedEntranceRoomDefinition,
            characterizedDrawingRoomDefinition,
            characterizedEntranceRoomContent,
            characterizedDrawingRoomContent,
            entranceVisible: true,
            drawingVisible: false);
        Assert.That(outbound.gameObject.activeInHierarchy, Is.True);
        Assert.That(reverse.gameObject.activeInHierarchy, Is.False);
        AssertDoorTriggerCompatibilityBindings(
            outbound,
            reverse,
            navigation,
            player.transform,
            passageAudioSource,
            passageDoorCatalog);
        Assert.That(GetPrivateField<RoomContentGroup>(cameraManager, "activeRoomContentGroup"), Is.SameAs(characterizedEntranceRoomContent));
        Assert.That(GetPrivateField<RectTransform>(cameraManager, "activeRoomStage"), Is.SameAs(characterizedEntranceRoomContent.transform));
        Assert.That(cameraManager.cameraBackground.texture, Is.SameAs(entranceBackground));
        Assert.That(DoorTriggerNavigation.HoveredTrigger, Is.Not.SameAs(reverse),
            "Disabling the reverse passage must release its hover even if the zero-position batch pointer selects another Entrance trigger.");
        Assert.That(GetPrivateStaticField<object>(typeof(NavigationCursorController), "doorHoverOwner"), Is.Not.SameAs(reverse));
        if (DoorTriggerNavigation.HoveredTrigger != null)
        {
            DoorTriggerNavigation.HoveredTrigger.OnPointerExit(null);
        }
        Assert.That(DoorTriggerNavigation.HoveredTrigger, Is.Null);
        Assert.That(GetPrivateStaticField<object>(typeof(NavigationCursorController), "doorHoverOwner"), Is.Null);
        Assert.That(passagePromptText.gameObject.activeSelf, Is.False);
        Assert.That(player.InputEnabled, Is.True,
            "The reverse traversal must preserve the input state used for passage interaction.");
        Assert.That(player.HasDestination, Is.False);
        Vector2 characterizedEntranceReturnArrival = player.LogicalPosition;
        Assert.That(outbound.TryFindArrivalDestination(player, out Vector2 evaluatedEntranceArrival), Is.True);
        Assert.That(float.IsNaN(characterizedEntranceReturnArrival.x) || float.IsInfinity(characterizedEntranceReturnArrival.x), Is.False);
        Assert.That(float.IsNaN(characterizedEntranceReturnArrival.y) || float.IsInfinity(characterizedEntranceReturnArrival.y), Is.False);
        Assert.That(float.IsNaN(evaluatedEntranceArrival.x) || float.IsInfinity(evaluatedEntranceArrival.x), Is.False);
        Assert.That(float.IsNaN(evaluatedEntranceArrival.y) || float.IsInfinity(evaluatedEntranceArrival.y), Is.False);
        Assert.That(characterizedEntranceReturnArrival.x, Is.EqualTo(-7.703568f).Within(0.005f));
        Assert.That(characterizedEntranceReturnArrival.y, Is.EqualTo(-2.000136f).Within(0.005f));
        Assert.That(chapter.CurrentChapterId, Is.EqualTo(ChapterManager.Chapter1Id));
        Assert.That(outboundRect.anchoredPosition, Is.EqualTo(outboundAnchoredPosition));
        Assert.That(outboundRect.sizeDelta, Is.EqualTo(outboundSizeDelta));
        Assert.That(reverseRect.anchoredPosition, Is.EqualTo(reverseAnchoredPosition));
        Assert.That(reverseRect.sizeDelta, Is.EqualTo(reverseSizeDelta));
        Assert.That(characterizedEntranceRoomContent.transform.localPosition, Is.EqualTo(entranceStageLocalPosition));
        Assert.That(characterizedEntranceRoomContent.transform.localScale, Is.EqualTo(entranceStageLocalScale));
        Assert.That(FindInActiveScene<DoorTriggerNavigation>(), Has.Length.EqualTo(characterizedDoorTriggerCount));
        Assert.That(FindInActiveScene<RoomContentGroup>(), Has.Length.EqualTo(characterizedRoomContentGroupCount));
        Assert.That(FindInActiveScene<AudioSource>().Length, Is.GreaterThanOrEqualTo(characterizedAudioSourceCount));
        Assert.That(passageAudioSource.GetComponents<GameAudioSourceVolume>(), Has.Length.EqualTo(1));
        Assert.That(passageAudioSource.GetComponent<GameAudioSourceVolume>(), Is.SameAs(characterizedPassageAudioBinding));
        Assert.That(FindInActiveScene<GameAudioSourceVolume>().Length, Is.GreaterThanOrEqualTo(characterizedAudioBindingCount));
        Assert.That(FindInActiveScene<Canvas>(), Has.Length.EqualTo(characterizedCanvasCount));
        Assert.That(FindInActiveScene<TMP_Text>(), Has.Length.EqualTo(characterizedTextCount));
        player.SetInputEnabled(characterizedPassageInputEnabled);
        Assert.That(player.InputEnabled, Is.EqualTo(characterizedPassageInputEnabled));
        Debug.Log($"[PassageCharacterization] reverseArrival={characterizedEntranceReturnArrival.x:0.######},{characterizedEntranceReturnArrival.y:0.######} events={string.Join("->", characterizedPassageRoomChanges)}");
        ScaleSnapshot returnedScale = CaptureScale(player, RequireOnlyActiveRoom(EntranceRoom));
        Assert.That(
            returnedScale.AppliedMultiplier,
            Is.EqualTo(settledScale.AppliedMultiplier).Within(0.01f),
            "Returning to the entrance must reproduce the same room-stage scale policy.");
        Assert.That(GetPrivateField<CoatCloset>(arrival, "coatCloset"), Is.SameAs(entranceCoatCloset));
        Assert.That(GetPrivateField<Transform>(arrival, "closetPoint"), Is.SameAs(entranceCoatHanger));
        Assert.That(entranceCoatHanger.GetComponent<CoatCloset>(), Is.SameAs(entranceCoatCloset));
        Assert.That(entranceCoatHanger.GetComponent<Chapter1SceneAction>(), Is.SameAs(entranceCoatAction));
        Assert.That(entranceCoatHanger.GetComponent<BoxCollider2D>(), Is.SameAs(entranceCoatCollider));
        Assert.That(FindInActiveScene<CoatCloset>(), Has.Length.EqualTo(1));
        Assert.That(GetPrivateField<ChapterClock>(arrival, "chapterClock"), Is.SameAs(serializedArrivalClock));
        Assert.That(GetPrivateField<CameraManager>(arrival, "cameraManager"), Is.SameAs(serializedArrivalCamera));
        Assert.That(GetPrivateField<RoomNavigationManager>(arrival, "navigationManager"), Is.SameAs(serializedArrivalNavigation));
        Assert.That(GetPrivateField<PointClickPlayerMovement>(arrival, "playerMovement"), Is.SameAs(serializedArrivalPlayerMovement));
        Assert.That(GetPrivateField<Chapter1SceneAction>(arrival, "frontDoorSceneAction"), Is.SameAs(authoredFrontDoorAction));
        Assert.That(GetPrivateField<GuestFootstepCatalog>(arrival, "guestFootstepCatalog"), Is.SameAs(resolvedGuestFootstepCatalog));
        Assert.That(GetPrivateField<Transform>(arrival, "guestEntranceSpawnPlacemark"), Is.SameAs(authoredGuestEntranceSpawnPlacemark));
        Assert.That(GetPrivateField<Transform>(arrival, "drawingRoomDoorTarget"), Is.SameAs(characterizedDrawingRoomDoorTarget));
        Assert.That(GetPrivateField<RoomContentGroup>(arrival, "entryRoomContent"), Is.SameAs(characterizedEntranceRoomContent));
        Assert.That(GetPrivateField<RoomContentGroup>(arrival, "drawingRoomContent"), Is.SameAs(characterizedDrawingRoomContent));
        CollectionAssert.AreEqual(characterizedDrawingRoomGuestPoints, GetPrivateField<Transform[]>(arrival, "drawingRoomGuestPoints"));
        Assert.That(GetPrivateField<DoorbellSystem>(arrival, "doorbellSystem"), Is.SameAs(characterizedDoorbell));
        AssertGrandfatherClockPlaceholdersRemainUnmodified(entranceClockPlaceholder, drawingRoomClockPlaceholder);
        AssertGameTimeHudGraphRemainsStable(
            arrival,
            characterizedGameTimeHud,
            characterizedGameTimeHudClock,
            characterizedTimeCanvas,
            characterizedTimeText,
            characterizedTimeShadow,
            characterizedTimeFont,
            characterizedTimeFontMaterial,
            characterizedTimeEventSystem);
        Assert.That(GetPrivateField<AudioSource>(characterizedDoorbell, "audioSource"), Is.SameAs(characterizedDoorbellSource));
        Assert.That(characterizedDoorbell.GetComponent<GameAudioSourceVolume>(), Is.SameAs(characterizedDoorbellBinding));
        Assert.That(GetPrivateField<AudioClip>(characterizedDoorbell, "doorbellClip"), Is.SameAs(characterizedDoorbellClip));
        Assert.That(FindInActiveScene<DoorbellSystem>(), Has.Length.EqualTo(1));
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name == "Chapter1_ClickTarget_DrawingRoomExit"), Is.False);
        CollectionAssert.AreEquivalent(
            new[] { authoredFrontDoorAction, entranceCoatAction },
            FindInActiveScene<Chapter1SceneAction>());
        Assert.That(InvokePrivateStringMethod<RoomContentGroup>(arrival, "FindRoomContentGroup", EntranceRoom), Is.SameAs(characterizedEntranceRoomContent));
        Assert.That(InvokePrivateStringMethod<RoomContentGroup>(arrival, "FindRoomContentGroup", DrawingRoom), Is.SameAs(characterizedDrawingRoomContent));

        for (int i = 0; i < characterizedDrawingRoomGuestPoints.Length; i++)
        {
            Assert.That(InvokePrivateIntMethod<Transform>(arrival, "GetDrawingRoomGuestPoint", i), Is.SameAs(characterizedDrawingRoomGuestPoints[i]));
        }

        Assert.That(FindInActiveScene<RoomAnchor>(), Has.Length.EqualTo(characterizedRoomAnchorCount));
        Assert.That(FindInActiveScene<RoomContentGroup>(), Has.Length.EqualTo(characterizedRoomContentCount));
        Assert.That(Resources.FindObjectsOfTypeAll<GuestFootstepCatalog>(), Has.Length.EqualTo(characterizedFootstepCatalogCount));
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name.StartsWith("DrawingRoomSeat_Runtime_")), Is.False);
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name.StartsWith("DrawingRoomSeat_") && item.name != "DrawingRoomSeat_01" && item.name != "DrawingRoomSeat_02" && item.name != "DrawingRoomSeat_03"), Is.False);
        Assert.That(authoredFrontDoorTrigger.GetComponent<Chapter1SceneAction>(), Is.SameAs(authoredFrontDoorAction));
        Assert.That(authoredFrontDoorTrigger.GetComponent<BoxCollider2D>(), Is.SameAs(authoredFrontDoorCollider));
        Assert.That(FindInActiveScene<Transform>().Count(item => item.name == "Door_answer_trigger"), Is.EqualTo(1));
        Assert.That(FindInActiveScene<Chapter1SceneAction>(), Has.Length.EqualTo(frontDoorActionCount));
        Assert.That(FindInActiveScene<BoxCollider2D>(), Has.Length.EqualTo(frontDoorColliderCount));
        Debug.Log(
            $"[EntranceCoatHangerCharacterization] hanger={entranceCoatHanger.GetInstanceID()} " +
            $"closet={entranceCoatCloset.GetInstanceID()} action={entranceCoatAction.GetInstanceID()} " +
            $"collider={entranceCoatCollider.GetInstanceID()}");

        Assert.That(RequireExactlyOneInActiveScene<CameraManager>(), Is.SameAs(cameraManager));
        Assert.That(RequireExactlyOneInActiveScene<RoomNavigationManager>(), Is.SameAs(navigation));
        Assert.That(RequireExactlyOneInActiveScene<DoorPromptSequenceController>(), Is.SameAs(prompts));
        Assert.That(RequireExactlyOneInActiveScene<ChapterManager>(), Is.SameAs(chapter));
        Assert.That(RequireExactlyOneInActiveScene<ChapterClock>(), Is.SameAs(clock));
        Assert.That(RequireExactlyOneInActiveScene<ChapterEventScheduler>(), Is.SameAs(scheduler));
        Assert.That(RequireExactlyOneInActiveScene<ChapterIntroUI>(), Is.SameAs(intro));
        AssertChapterIntroGraphRemainsStable(
            intro,
            characterizedIntroHost,
            characterizedIntroCanvas,
            characterizedIntroOverlay,
            characterizedIntroFade,
            characterizedIntroTitle,
            characterizedIntroFont,
            characterizedIntroFontMaterial,
            characterizedTimeEventSystem);
        Assert.That(RequireExactlyOneInActiveScene<Chapter1ArrivalController>(), Is.SameAs(arrival));
        Assert.That(RequireExactlyOneInActiveScene<Chapter1InteractionHUD>(), Is.SameAs(chapter1Hud));
        Assert.That(RequireExactlyOneInActiveScene<RoomLightingController>(), Is.SameAs(lighting));
        Assert.That(RequireExactlyOneInActiveScene<Chateau.Architecture.GameRoot>(), Is.SameAs(gameRoot));
        Assert.That(RequireExactlyOneInActiveScene<RuntimeSettingsMenu>(), Is.SameAs(runtimeSettings));
        Assert.That(RequireExactlyOneInActiveScene<EventSystem>(), Is.SameAs(serializedEventSystem));
        Assert.That(
            FindInActiveScene<AudioSource>().Single(item => item.gameObject.name == "Audio_ExplorationMusic"),
            Is.SameAs(explorationMusicSource));
        Assert.That(explorationMusicSource.GetComponent<GameAudioSourceVolume>(), Is.SameAs(explorationMusicBinding));
        Assert.That(RequireExactlyOneInActiveScene<GuestRoomScaleApplier>(), Is.SameAs(serializedGuestScaleApplier));
        Assert.That(RequireExactlyOneInActiveScene<GuestRoomScaleCalibration>(), Is.SameAs(serializedGuestScaleCalibration));
        Assert.That(RequireExactlyOneInActiveScene<RuntimeSettingsMenu>(), Is.SameAs(runtimeSettings));
        Assert.That(RequireExactlyOneInActiveScene<FireplaceAmbienceController>(), Is.SameAs(fireplaceAmbience));
        Assert.That(RequireExactlyOneInActiveScene<ClockTickingAmbienceController>(), Is.SameAs(clockAmbience));
        AssertClockAmbienceGraphRemainsCanonical(
            clockAmbience,
            serializedClockAmbienceNavigation,
            serializedClockAmbienceCatalog,
            serializedClockAmbienceSource,
            navigation);
        Assert.That(fireplaceAmbience.GetComponent<AudioSource>(), Is.SameAs(fireplaceSource));
        Assert.That(clockAmbience.GetComponent<AudioSource>(), Is.SameAs(clockSource));
        Assert.That(fireplaceSource.clip, Is.Not.Null);
        Assert.That(clockSource.clip, Is.Not.Null);
        Assert.That(teaTableView.gameObject.activeInHierarchy, Is.False);
        Assert.That(purpleArmchairView.gameObject.activeInHierarchy, Is.False);
        Assert.That(FindInActiveScene<SetPieceView>().Single(item => item.gameObject.name == "purple_armchair_back"), Is.SameAs(purpleArmchairView));
        Assert.That(FindInActiveScene<RoomProjectedEntity>().Any(item => item.gameObject.name == "purple_armchair_back"), Is.False);
        Assert.That(FindInActiveScene<ObjectMovementBlocker2D>().Single(item => item.SourceObjectName == "purple_armchair_back"), Is.SameAs(purpleArmchairBlocker));
        CollectionAssert.AreEqual(purpleArmchairFootprint, purpleArmchairCollider.points);
        Assert.That(purpleSofaView.gameObject.activeInHierarchy, Is.False);
        Assert.That(FindInActiveScene<SetPieceView>().Single(item => item.gameObject.name == "purple_sofa"), Is.SameAs(purpleSofaView));
        Assert.That(FindInActiveScene<RoomProjectedEntity>().Any(item => item.gameObject.name == "purple_sofa"), Is.False);
        Assert.That(FindInActiveScene<ObjectMovementBlocker2D>().Single(item => item.SourceObjectName == "purple_sofa"), Is.SameAs(purpleSofaBlocker));
        CollectionAssert.AreEqual(purpleSofaFootprint, purpleSofaCollider.points);
        Assert.That(FindInActiveScene<MonoBehaviour>().Any(item => item.GetType().Name == "GameClockHandsDisplay"), Is.False);
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name.StartsWith("Canvas_AnalogClockHands")), Is.False);
        Assert.That(
            FindInActiveScene<SetPieceView>().Single(item => item.gameObject.name == "tea_service_table"),
            Is.SameAs(teaTableView));
        Assert.That(
            FindInActiveScene<ObjectMovementBlocker2D>().Single(item => item.SourceObjectName == "tea_service_table"),
            Is.SameAs(teaTableBlocker));

        InvokePrivateMethod(chapter, "StopChapterCoroutines");
        scheduler.Clear();
        arrival.PrepareGuestsForChapter2Skip();
        SetPrivateField(arrival, "sequenceActive", true);
        SetPrivateField(arrival, "chapterCompletionRequested", false);
        SetPrivateField(arrival, "finalEmptyDoorbellOccurred", true);
        SetPrivateField(arrival, "emptyDoorbellWaitingForAnswer", false);
        SetPrivateField(arrival, "butlerCarryingCoat", false);
        InvokePrivateMethod(arrival, "SubscribeToRoomChanges");
        SetPrivateField(chapter, "chapter1CompletionFadeOutDelaySeconds", 0f);
        SetPrivateField(chapter, "skipIntro", true);
        SetPrivateField(intro, "fadeFromBlackSeconds", 0f);

        Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom));
        AssertCanonicalRoomViewsRemainStable(
            characterizedEntranceRoomView,
            characterizedDrawingRoomView,
            characterizedEntranceRoomDefinition,
            characterizedDrawingRoomDefinition,
            characterizedEntranceRoomContent,
            characterizedDrawingRoomContent,
            entranceVisible: true,
            drawingVisible: false);
        Assert.That(serializedChapter2.CurrentPhase, Is.EqualTo(Chapter2Phase.NotStarted));
        Assert.That(GetPrivateValue<bool>(arrival, "sequenceActive"), Is.True);
        Assert.That(GetPrivateValue<bool>(arrival, "chapterCompletionRequested"), Is.False);
        Assert.That(GetPrivateValue<bool>(arrival, "subscribedToRoomChanges"), Is.True);
        Assert.That(
            navigation.MoveThroughInspectorDoor(
                outbound.SourceRoom,
                outbound.DoorName,
                outbound.DestinationRoom,
                true),
            Is.True);
        Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom));
        AssertCanonicalRoomViewsRemainStable(
            characterizedEntranceRoomView,
            characterizedDrawingRoomView,
            characterizedEntranceRoomDefinition,
            characterizedDrawingRoomDefinition,
            characterizedEntranceRoomContent,
            characterizedDrawingRoomContent,
            entranceVisible: false,
            drawingVisible: true);
        Assert.That(GetPrivateValue<bool>(arrival, "chapterCompletionRequested"), Is.True);
        Assert.That(GetPrivateValue<bool>(arrival, "sequenceActive"), Is.False);
        Assert.That(GetPrivateValue<bool>(arrival, "subscribedToRoomChanges"), Is.False);
        Assert.That(chapter.CurrentPhase, Is.EqualTo(ChapterPhase.Complete));
        Assert.That(serializedArrivalPlayerMovement.InputEnabled, Is.False);
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name == "Chapter1_ClickTarget_DrawingRoomExit"), Is.False);

        for (int frame = 0; frame < 30 && chapter.CurrentChapterId != ChapterManager.Chapter2Id; frame++)
        {
            yield return null;
        }

        yield return WaitForSettledLayout();

        Assert.That(chapter.CurrentChapterId, Is.EqualTo(ChapterManager.Chapter2Id));
        Assert.That(serializedChapter2.CurrentPhase, Is.Not.EqualTo(Chapter2Phase.NotStarted));
        Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom));
        AssertCanonicalRoomViewsRemainStable(
            characterizedEntranceRoomView,
            characterizedDrawingRoomView,
            characterizedEntranceRoomDefinition,
            characterizedDrawingRoomDefinition,
            characterizedEntranceRoomContent,
            characterizedDrawingRoomContent,
            entranceVisible: false,
            drawingVisible: true);
        CollectionAssert.AreEquivalent(
            new[] { authoredFrontDoorAction, entranceCoatAction },
            FindInActiveScene<Chapter1SceneAction>());

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
        AssertCanonicalRoomViewsRemainStable(
            characterizedEntranceRoomView,
            characterizedDrawingRoomView,
            characterizedEntranceRoomDefinition,
            characterizedDrawingRoomDefinition,
            characterizedEntranceRoomContent,
            characterizedDrawingRoomContent,
            entranceVisible: false,
            drawingVisible: true);
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name == "Chapter1_ClickTarget_DrawingRoomExit"), Is.False);
        CollectionAssert.AreEquivalent(
            new[] { authoredFrontDoorAction, entranceCoatAction },
            FindInActiveScene<Chapter1SceneAction>());
        Assert.That(chapter2, Is.SameAs(serializedChapter2));
        Assert.That(GetPrivateField<ChapterManager>(chapter2, "chapterManager"), Is.SameAs(chapter));
        Assert.That(chapter2Hud, Is.SameAs(serializedChapter2Hud));
        Assert.That(speech.IsNormalSpeechActive, Is.False);
        Assert.That(serializedVoicePlayback.IsPlaying, Is.False);
        Transform chatBubble = FindInActiveScene<Transform>().Single(item => item.name == "Sprite_ChatBubble");
        Assert.That(chatBubble.gameObject.activeSelf, Is.False);
        Assert.That(monsterStinger, Is.SameAs(serializedMonsterStinger));
        Assert.That(guestPanic, Is.SameAs(serializedGuestPanic));
        Assert.That(guestSearch, Is.SameAs(serializedGuestSearch));
        Assert.That(GetPrivateField<RoomNavigationManager>(guestSearch, "navigationManager"), Is.SameAs(serializedGuestSearchNavigation));
        Assert.That(GetPrivateField<ChapterClock>(arrival, "chapterClock"), Is.SameAs(serializedArrivalClock));
        Assert.That(GetPrivateField<CameraManager>(arrival, "cameraManager"), Is.SameAs(serializedArrivalCamera));
        Assert.That(GetPrivateField<RoomNavigationManager>(arrival, "navigationManager"), Is.SameAs(serializedArrivalNavigation));
        Assert.That(GetPrivateField<PointClickPlayerMovement>(arrival, "playerMovement"), Is.SameAs(serializedArrivalPlayerMovement));
        AssertGrandfatherClockPlaceholdersRemainUnmodified(entranceClockPlaceholder, drawingRoomClockPlaceholder);
        AssertGameTimeHudGraphRemainsStable(
            arrival,
            characterizedGameTimeHud,
            characterizedGameTimeHudClock,
            characterizedTimeCanvas,
            characterizedTimeText,
            characterizedTimeShadow,
            characterizedTimeFont,
            characterizedTimeFontMaterial,
            characterizedTimeEventSystem);

        chapter.SkipToChapter2ForTesting();
        yield return WaitForSettledLayout();

        Assert.That(RequireExactlyOneInActiveScene<Chapter2Controller>(), Is.SameAs(chapter2));
        Assert.That(GetPrivateField<ChapterManager>(chapter2, "chapterManager"), Is.SameAs(chapter));
        Assert.That(RequireExactlyOneInActiveScene<Chapter2InteractionHUD>(), Is.SameAs(chapter2Hud));
        Assert.That(RequireExactlyOneInActiveScene<Chapter2MonsterStingerController>(), Is.SameAs(monsterStinger));
        Assert.That(RequireExactlyOneInActiveScene<Chapter2GuestPanicController>(), Is.SameAs(guestPanic));
        Assert.That(RequireExactlyOneInActiveScene<Chapter2GuestSearchController>(), Is.SameAs(guestSearch));
        Assert.That(GetPrivateField<RoomNavigationManager>(guestSearch, "navigationManager"), Is.SameAs(serializedGuestSearchNavigation));
        AssertCanonicalRoomViewsRemainStable(
            characterizedEntranceRoomView,
            characterizedDrawingRoomView,
            characterizedEntranceRoomDefinition,
            characterizedDrawingRoomDefinition,
            characterizedEntranceRoomContent,
            characterizedDrawingRoomContent,
            entranceVisible: false,
            drawingVisible: true);
        Assert.That(RequireExactlyOneInActiveScene<Chapter1InteractionHUD>(), Is.SameAs(chapter1Hud));
        Assert.That(RequireExactlyOneInActiveScene<GuestRoomScaleApplier>(), Is.SameAs(serializedGuestScaleApplier));
        Assert.That(RequireExactlyOneInActiveScene<GuestRoomScaleCalibration>(), Is.SameAs(serializedGuestScaleCalibration));
        Assert.That(RequireExactlyOneInActiveScene<FireplaceAmbienceController>(), Is.SameAs(fireplaceAmbience));
        Assert.That(RequireExactlyOneInActiveScene<ClockTickingAmbienceController>(), Is.SameAs(clockAmbience));
        AssertClockAmbienceGraphRemainsCanonical(
            clockAmbience,
            serializedClockAmbienceNavigation,
            serializedClockAmbienceCatalog,
            serializedClockAmbienceSource,
            navigation);
        Assert.That(RequireExactlyOneInActiveScene<EventSystem>(), Is.SameAs(serializedEventSystem));
        Assert.That(explorationMusicSource.GetComponent<GameAudioSourceVolume>(), Is.SameAs(explorationMusicBinding));
        Assert.That(GetPrivateField<ChapterManager>(arrival, "chapterManager"), Is.SameAs(serializedArrivalChapterManager));
        Assert.That(GetPrivateField<ChapterEventScheduler>(arrival, "eventScheduler"), Is.SameAs(serializedArrivalScheduler));
        Assert.That(GetPrivateField<GameObject>(arrival, "playerButlerReference"), Is.SameAs(serializedArrivalButlerRoot));
        AssertGameTimeHudGraphRemainsStable(
            arrival,
            characterizedGameTimeHud,
            characterizedGameTimeHudClock,
            characterizedTimeCanvas,
            characterizedTimeText,
            characterizedTimeShadow,
            characterizedTimeFont,
            characterizedTimeFontMaterial,
            characterizedTimeEventSystem);

        chapter.SkipToSevenPMForTesting();
        yield return WaitForSettledLayout();

        AssertGameTimeHudGraphRemainsStable(
            arrival,
            characterizedGameTimeHud,
            characterizedGameTimeHudClock,
            characterizedTimeCanvas,
            characterizedTimeText,
            characterizedTimeShadow,
            characterizedTimeFont,
            characterizedTimeFontMaterial,
            characterizedTimeEventSystem);
        Assert.That(clock.CurrentTimeLabel, Is.EqualTo("7:00 PM"));
        Assert.That(characterizedTimeText.text, Is.EqualTo("7:00 PM"));
        Assert.That(GetPrivateField<RoomNavigationManager>(guestSearch, "navigationManager"), Is.SameAs(navigation));
        AudioSource clockStrikeSource = GetPrivateField<AudioSource>(chapter2, "clockStrikeAudioSource");
        AudioClip clockStrikeClip = GetPrivateField<AudioClip>(chapter2, "clockStrikeClip");
        Assert.That(clockStrikeSource, Is.SameAs(serializedClockStrikeSource));
        Assert.That(clockStrikeClip, Is.SameAs(serializedClockStrikeClip));
        Assert.That(clockStrikeSource.gameObject, Is.Not.SameAs(chapter2.gameObject));
        Assert.That(clockStrikeSource.clip, Is.SameAs(clockStrikeClip));
        Assert.That(clockStrikeClip.name, Is.EqualTo("06_heavy_wooden_case_clock_gong_seed1442486_tangoflux_raw_44k1"));
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clockStrikeClip)), Is.EqualTo("d7084eafa9124afcbcbf12529e08bc70"));
        Assert.That(clockStrikeSource.playOnAwake, Is.False);
        Assert.That(clockStrikeSource.loop, Is.False);
        Assert.That(clockStrikeSource.spatialBlend, Is.Zero);
        Assert.That(clockStrikeSource.ignoreListenerVolume, Is.True);
        Assert.That(chapter2.GetComponents<AudioSource>(), Is.Empty);
        Assert.That(chapter2.GetComponents<GameAudioSourceVolume>(), Is.Empty);
        GameAudioSourceVolume[] clockStrikeBindings = clockStrikeSource.GetComponents<GameAudioSourceVolume>();
        Assert.That(clockStrikeBindings, Has.Length.EqualTo(1));
        Assert.That(clockStrikeBindings[0], Is.SameAs(serializedClockStrikeBinding));
        Assert.That(clockStrikeBindings[0].Channel, Is.EqualTo(GameAudioChannel.GameSounds));
        Assert.That(clockStrikeBindings[0].BaseVolume, Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(GetPrivateField<AudioSource>(clockStrikeBindings[0], "audioSource"), Is.SameAs(clockStrikeSource));
        Assert.That(clockStrikeSource.volume, Is.EqualTo(0.4f * GameAudioSettings.GetVolume(GameAudioChannel.GameSounds)).Within(0.0001f));
        Debug.Log($"[Chapter2ClockStrikeCharacterization] source={clockStrikeSource.GetInstanceID()} clipGuid=d7084eafa9124afcbcbf12529e08bc70 binding={clockStrikeBindings[0].GetInstanceID()}");

        chapter.SkipToSevenPMForTesting();
        yield return WaitForSettledLayout();

        AssertGameTimeHudGraphRemainsStable(
            arrival,
            characterizedGameTimeHud,
            characterizedGameTimeHudClock,
            characterizedTimeCanvas,
            characterizedTimeText,
            characterizedTimeShadow,
            characterizedTimeFont,
            characterizedTimeFontMaterial,
            characterizedTimeEventSystem);
        Assert.That(characterizedTimeText.text, Is.EqualTo("7:00 PM"));
        Assert.That(GetPrivateField<AudioSource>(chapter2, "clockStrikeAudioSource"), Is.SameAs(clockStrikeSource));
        Assert.That(GetPrivateField<AudioClip>(chapter2, "clockStrikeClip"), Is.SameAs(clockStrikeClip));
        Assert.That(GetPrivateField<RoomNavigationManager>(guestSearch, "navigationManager"), Is.SameAs(navigation));
        Assert.That(clockStrikeSource.GetComponents<AudioSource>(), Has.Length.EqualTo(1));
        Assert.That(clockStrikeSource.GetComponents<GameAudioSourceVolume>(), Has.Length.EqualTo(1));
        Assert.That(clockStrikeSource.GetComponent<GameAudioSourceVolume>(), Is.SameAs(clockStrikeBindings[0]));

        Assert.That(navigation.DebugTeleportToRoom(DrawingRoom), Is.True);
        yield return WaitForSettledLayout();
        Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom));
        AssertCanonicalRoomViewsRemainStable(
            characterizedEntranceRoomView,
            characterizedDrawingRoomView,
            characterizedEntranceRoomDefinition,
            characterizedDrawingRoomDefinition,
            characterizedEntranceRoomContent,
            characterizedDrawingRoomContent,
            entranceVisible: false,
            drawingVisible: true);
        Assert.That(serializedMonsterStinger.GetComponents<AudioSource>(), Is.Empty);
        Assert.That(serializedMonsterStinger.GetComponents<GameAudioSourceVolume>(), Is.Empty);
        Assert.That(serializedMonsterObject.GetComponents<Canvas>(), Has.Length.EqualTo(1));
        Assert.That(serializedMonsterObject.GetComponent<Canvas>(), Is.SameAs(serializedMonsterOverlayCanvas));
        Assert.That(serializedMonsterOverlayCanvas.isActiveAndEnabled, Is.False);

        serializedMonsterStinger.BeginStinger();
        yield return null;

        Assert.That(serializedMonsterStinger.IsRunning, Is.True);
        Assert.That(GetPrivateField<GameObject>(serializedMonsterStinger, "monsterObject"), Is.SameAs(serializedMonsterObject));
        Assert.That(GetPrivateField<Transform>(serializedMonsterStinger, "runStart"), Is.SameAs(serializedMonsterRunStart));
        Assert.That(GetPrivateField<Transform>(serializedMonsterStinger, "runTarget"), Is.SameAs(serializedMonsterRunTarget));
        Assert.That(GetPrivateField<RoomNavigationManager>(serializedMonsterStinger, "navigationManager"), Is.SameAs(serializedMonsterNavigation));
        Assert.That(GetPrivateField<Image>(serializedMonsterStinger, "monsterImage"), Is.SameAs(serializedMonsterImage));
        Assert.That(GetPrivateField<SpriteRenderer>(serializedMonsterStinger, "monsterSpriteRenderer"), Is.Null);
        Assert.That(serializedMonsterObject.activeSelf, Is.True);
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name == "Chapter2_MonsterPlaceholder_Runtime"), Is.False);
        Assert.That(GetPrivateField<AudioSource>(serializedMonsterStinger, "violinAudioSource"), Is.SameAs(serializedMonsterViolinSource));
        Assert.That(GetPrivateField<GameAudioSourceVolume>(serializedMonsterStinger, "violinAudioVolumeBinding"), Is.SameAs(serializedMonsterViolinBinding));
        Assert.That(GetPrivateField<AudioClip>(serializedMonsterStinger, "violinAudioClip"), Is.SameAs(serializedMonsterViolinClip));
        Assert.That(GetPrivateField<Sprite[]>(serializedMonsterStinger, "monsterRunSprites"), Is.SameAs(serializedMonsterRunSprites));
        Assert.That(serializedMonsterViolinSource, Is.Not.Null);
        Assert.That(serializedMonsterViolinSource.gameObject, Is.SameAs(serializedMonsterObject));
        Assert.That(serializedMonsterViolinClip, Is.Not.Null);
        Assert.That(serializedMonsterViolinSource.clip, Is.SameAs(serializedMonsterViolinClip));
        Assert.That(serializedMonsterViolinClip.name, Is.EqualTo("violinscreech"));
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(serializedMonsterViolinClip)), Is.EqualTo("69f06d321e4549cdcad1133332661f6d"));
        Assert.That(serializedMonsterViolinSource.playOnAwake, Is.False);
        Assert.That(serializedMonsterViolinSource.loop, Is.True);
        Assert.That(serializedMonsterViolinSource.spatialBlend, Is.Zero);
        Assert.That(serializedMonsterStinger.GetComponents<AudioSource>(), Is.Empty);
        Assert.That(serializedMonsterStinger.GetComponents<GameAudioSourceVolume>(), Is.Empty);
        Assert.That(serializedMonsterObject.GetComponents<AudioSource>(), Has.Length.EqualTo(1));
        GameAudioSourceVolume[] serializedMonsterViolinBindings = serializedMonsterObject.GetComponents<GameAudioSourceVolume>();
        Assert.That(serializedMonsterViolinBindings, Has.Length.EqualTo(1));
        Assert.That(serializedMonsterViolinBindings[0], Is.SameAs(serializedMonsterViolinBinding));
        Assert.That(serializedMonsterViolinBindings[0].Channel, Is.EqualTo(GameAudioChannel.GameSounds));
        Assert.That(serializedMonsterViolinBindings[0].BaseVolume, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(GetPrivateField<AudioSource>(serializedMonsterViolinBindings[0], "audioSource"), Is.SameAs(serializedMonsterViolinSource));
        Canvas[] serializedMonsterCanvases = serializedMonsterObject.GetComponents<Canvas>();
        Assert.That(serializedMonsterCanvases, Has.Length.EqualTo(1));
        Assert.That(serializedMonsterCanvases[0], Is.SameAs(serializedMonsterOverlayCanvas));
        Assert.That(serializedMonsterCanvases[0].isActiveAndEnabled, Is.True);
        Assert.That(serializedMonsterCanvases[0].overrideSorting, Is.True);
        Assert.That(serializedMonsterCanvases[0].sortingLayerName, Is.EqualTo("People"));
        Assert.That(serializedMonsterCanvases[0].sortingOrder, Is.EqualTo(10000));
        Assert.That(serializedMonsterRunSprites, Has.Length.EqualTo(8));

        serializedMonsterStinger.StopStinger();
        yield return null;

        Assert.That(serializedMonsterStinger.IsRunning, Is.False);
        Assert.That(serializedMonsterObject.activeSelf, Is.False);
        Assert.That(serializedMonsterImage.sprite, Is.SameAs(serializedMonsterOriginalSprite));
        Assert.That(GetPrivateField<GameObject>(serializedMonsterStinger, "monsterObject"), Is.SameAs(serializedMonsterObject));
        Assert.That(GetPrivateField<Transform>(serializedMonsterStinger, "runStart"), Is.SameAs(serializedMonsterRunStart));
        Assert.That(GetPrivateField<Transform>(serializedMonsterStinger, "runTarget"), Is.SameAs(serializedMonsterRunTarget));
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name == "Chapter2_MonsterPlaceholder_Runtime"), Is.False);
        Assert.That(serializedMonsterViolinSource.isPlaying, Is.False);
        Assert.That(serializedMonsterStinger.GetComponents<AudioSource>(), Is.Empty);
        Assert.That(serializedMonsterStinger.GetComponents<GameAudioSourceVolume>(), Is.Empty);
        Assert.That(serializedMonsterObject.GetComponents<AudioSource>(), Has.Length.EqualTo(1));
        Assert.That(serializedMonsterObject.GetComponents<GameAudioSourceVolume>(), Has.Length.EqualTo(1));
        Assert.That(serializedMonsterObject.GetComponents<Canvas>(), Has.Length.EqualTo(1));

        serializedMonsterStinger.BeginStinger();
        yield return null;
        Assert.That(GetPrivateField<AudioSource>(serializedMonsterStinger, "violinAudioSource"), Is.SameAs(serializedMonsterViolinSource));
        Assert.That(GetPrivateField<AudioClip>(serializedMonsterStinger, "violinAudioClip"), Is.SameAs(serializedMonsterViolinClip));
        Assert.That(GetPrivateField<Sprite[]>(serializedMonsterStinger, "monsterRunSprites"), Is.SameAs(serializedMonsterRunSprites));
        Assert.That(serializedMonsterStinger.GetComponents<AudioSource>(), Is.Empty);
        Assert.That(serializedMonsterStinger.GetComponents<GameAudioSourceVolume>(), Is.Empty);
        Assert.That(serializedMonsterObject.GetComponents<AudioSource>(), Has.Length.EqualTo(1));
        Assert.That(serializedMonsterObject.GetComponent<GameAudioSourceVolume>(), Is.SameAs(serializedMonsterViolinBindings[0]));
        Assert.That(serializedMonsterObject.GetComponent<Canvas>(), Is.SameAs(serializedMonsterCanvases[0]));
        serializedMonsterStinger.StopStinger();
        yield return null;
        Assert.That(serializedMonsterStinger.IsRunning, Is.False);
        Assert.That(serializedMonsterImage.sprite, Is.SameAs(serializedMonsterOriginalSprite));
        Debug.Log($"[Chapter2MonsterStructuralCharacterization] monster={serializedMonsterObject.GetInstanceID()} start={serializedMonsterRunStart.GetInstanceID()} target={serializedMonsterRunTarget.GetInstanceID()} image={serializedMonsterImage.GetInstanceID()} navigation={serializedMonsterNavigation.GetInstanceID()}");
        Debug.Log($"[Chapter2MonsterPresentationCharacterization] source={serializedMonsterViolinSource.GetInstanceID()} clipGuid=69f06d321e4549cdcad1133332661f6d binding={serializedMonsterViolinBindings[0].GetInstanceID()} canvas={serializedMonsterCanvases[0].GetInstanceID()} sprites={serializedMonsterRunSprites.Length}");
    }

    [UnityTest]
    public IEnumerator CanonicalNavigationFacadeUsesValidatedAuthoredArrivalAnchors()
    {
        MainMenuController menu = RequireExactlyOneInActiveScene<MainMenuController>();
        menu.NewGame();
        yield return null;

        GameObject cursorChoice = GameObject.Find("Button_CursorStyle_01");
        Assert.That(cursorChoice, Is.Not.Null);
        Button cursorButton = cursorChoice.GetComponent<Button>();
        Assert.That(cursorButton, Is.Not.Null);
        cursorButton.onClick.Invoke();
        yield return SetAndWaitForRenderedGameViewResolution(1366, 768);

        RoomNavigationManager navigation = RequireExactlyOneInActiveScene<RoomNavigationManager>();
        INavigationService facade = navigation;
        Assert.That(facade, Is.SameAs(navigation));
        Chateau.Architecture.GameRoot gameRoot = RequireExactlyOneInActiveScene<Chateau.Architecture.GameRoot>();
        Assert.That(gameRoot.Services.Count(service => service is INavigationService), Is.EqualTo(1));
        Assert.That(gameRoot.Services.Single(service => service is INavigationService), Is.SameAs(navigation));

        GameObject playerObject = GameObject.Find("Player");
        Assert.That(playerObject, Is.Not.Null);
        PointClickPlayerMovement player = playerObject.GetComponent<PointClickPlayerMovement>();
        Assert.That(player, Is.Not.Null);
        CanonicalPassage forward = RequireSceneObject<CanonicalPassage>("DoorTrigger_GEH_DrawingRoom");
        CanonicalPassage reverse = RequireSceneObject<CanonicalPassage>("DoorTrigger_DrawingRoom_GEH");
        DoorTriggerNavigation outboundTrigger = forward.GetComponent<DoorTriggerNavigation>();
        DoorTriggerNavigation reverseTrigger = reverse.GetComponent<DoorTriggerNavigation>();
        Assert.That(outboundTrigger, Is.Not.Null);
        Assert.That(reverseTrigger, Is.Not.Null);
        AudioSource passageAudioSource = FindInActiveScene<AudioSource>()
            .Single(item => item.gameObject.name == "Audio_DoorOpen");
        CameraManager cameraManager = RequireExactlyOneInActiveScene<CameraManager>();
        cameraManager.panRoomWithMouseEdges = false;
        cameraManager.zoomRoomWithMouseWheel = false;
        cameraManager.ResetRoomLookForPreview();
        yield return WaitForSettledLayout();

        int characterizedTriggerCount = FindInActiveScene<DoorTriggerNavigation>().Length;
        int characterizedPassageCount = FindInActiveScene<CanonicalPassage>().Length;
        int characterizedRoomCount = FindInActiveScene<RoomContentGroup>().Length;
        int characterizedAudioSourceCount = FindInActiveScene<AudioSource>().Length;
        int outboundComponentCount = forward.GetComponents<Component>().Length;
        int reverseComponentCount = reverse.GetComponents<Component>().Length;
        List<string> roomEvents = new List<string>();
        List<Vector2> roomEventPositions = new List<Vector2>();
        List<CanonicalRoomDefinition> roomEventDefinitions = new List<CanonicalRoomDefinition>();
        int arrivalEventCount = 0;
        int movementStoppedEventCount = 0;
        UnityEngine.Events.UnityAction<string> recordRoomEvent = roomName =>
        {
            roomEvents.Add(roomName);
            roomEventPositions.Add(player.LogicalPosition);
            roomEventDefinitions.Add(facade.CurrentRoomDefinition);
        };
        System.Action recordArrival = () => arrivalEventCount++;
        System.Action recordMovementStopped = () => movementStoppedEventCount++;
        navigation.OnCurrentRoomChanged.AddListener(recordRoomEvent);
        player.ArrivedAtDestination += recordArrival;
        player.MovementStopped += recordMovementStopped;

        Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom));
        Assert.That(facade.CurrentRoomDefinition, Is.SameAs(forward.Definition.SourceRoom));
        Vector2 initialPosition = player.LogicalPosition;
        Vector2 authoredForwardApproach = forward.ApproachAnchor.LogicalPosition;
        SetPrivateField(forward.ApproachAnchor, "logicalPosition", new Vector2(float.PositiveInfinity, 0f));
        try
        {
            Assert.That(facade.CanTraverse(forward), Is.False,
                "A non-finite authored approach must be rejected before movement or room state changes.");
            Assert.That(facade.TryTraverse(forward), Is.False);
            Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom));
            Assert.That(player.HasDestination, Is.False);
            AssertVector2Within(player.LogicalPosition, initialPosition, 0.0001f, "rejected non-finite approach");
            Assert.That(roomEvents, Is.Empty);
            Assert.That(passageAudioSource.GetComponents<GameAudioSourceVolume>(), Is.Empty);
        }
        finally
        {
            SetPrivateField(forward.ApproachAnchor, "logicalPosition", authoredForwardApproach);
        }

        Vector2 authoredForwardArrival = forward.ArrivalAnchor.LogicalPosition;
        SetPrivateField(forward.ArrivalAnchor, "logicalPosition", new Vector2(float.NaN, 0f));
        try
        {
            Assert.That(facade.CanTraverse(forward), Is.False,
                "A non-finite authored arrival must be rejected before room state changes.");
            Assert.That(facade.TryTraverse(forward), Is.False);
            Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom));
            Assert.That(facade.CurrentRoomDefinition, Is.SameAs(forward.Definition.SourceRoom));
            AssertVector2Within(player.LogicalPosition, initialPosition, 0.0001f, "rejected non-finite facade traversal");
            Assert.That(roomEvents, Is.Empty);
        }
        finally
        {
            SetPrivateField(forward.ArrivalAnchor, "logicalPosition", authoredForwardArrival);
        }

        Assert.That(facade.CanTraverse(forward), Is.True);
        Assert.That(facade.CanTraverse(reverse), Is.False);
        Assert.That(facade.CanTraverse(null), Is.False);
        Assert.That(facade.TryTraverse(reverse), Is.False);
        Assert.That(facade.TryTraverse(null), Is.False);
        Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom));
        AssertVector2Within(player.LogicalPosition, initialPosition, 0.0001f, "rejected facade traversal");
        Assert.That(roomEvents, Is.Empty);

        Assert.That(player.TryWarpToExact(forward.ApproachAnchor.LogicalPosition), Is.True);
        Vector2 forwardEventPosition = player.LogicalPosition;
        Assert.That(facade.TryTraverse(forward), Is.True);
        Assert.That(roomEvents, Is.EqualTo(new[] { DrawingRoom }));
        Assert.That(roomEventDefinitions, Is.EqualTo(new[] { forward.Definition.DestinationRoom }));
        AssertVector2Within(roomEventPositions[0], forwardEventPosition, 0.0001f, "facade forward event pre-warp position");
        AssertVector2Within(player.LogicalPosition, forward.ArrivalAnchor.LogicalPosition, 0.0001f, "facade forward authored arrival");
        Assert.That(facade.CurrentRoomDefinition, Is.SameAs(reverse.Definition.SourceRoom));
        Assert.That(facade.CanTraverse(forward), Is.False);
        Assert.That(facade.CanTraverse(reverse), Is.True);
        Assert.That(outboundTrigger.gameObject.activeInHierarchy, Is.False);
        Assert.That(reverseTrigger.gameObject.activeInHierarchy, Is.True);
        Assert.That(forward.isActiveAndEnabled, Is.False);
        Assert.That(reverse.isActiveAndEnabled, Is.True);
        RequireOnlyActiveRoom(DrawingRoom);
        Vector2 drawingPosition = player.LogicalPosition;
        Assert.That(facade.TryTraverse(forward), Is.False);
        AssertVector2Within(player.LogicalPosition, drawingPosition, 0.0001f, "rejected stale forward traversal");
        Assert.That(roomEvents, Has.Count.EqualTo(1));

        Vector2 reverseEventPosition = player.LogicalPosition;
        Assert.That(facade.TryTraverse(reverse), Is.True);
        Assert.That(roomEvents, Is.EqualTo(new[] { DrawingRoom, EntranceRoom }));
        Assert.That(roomEventDefinitions, Is.EqualTo(new[]
        {
            forward.Definition.DestinationRoom,
            reverse.Definition.DestinationRoom
        }));
        AssertVector2Within(roomEventPositions[1], reverseEventPosition, 0.0001f, "facade reverse event pre-warp position");
        AssertVector2Within(player.LogicalPosition, reverse.ArrivalAnchor.LogicalPosition, 0.0001f, "facade reverse authored arrival");
        Assert.That(facade.CurrentRoomDefinition, Is.SameAs(forward.Definition.SourceRoom));
        Assert.That(facade.CanTraverse(forward), Is.True);
        Assert.That(facade.CanTraverse(reverse), Is.False);
        Assert.That(outboundTrigger.gameObject.activeInHierarchy, Is.True);
        Assert.That(reverseTrigger.gameObject.activeInHierarchy, Is.False);
        Assert.That(forward.isActiveAndEnabled, Is.True);
        Assert.That(reverse.isActiveAndEnabled, Is.False);
        RequireOnlyActiveRoom(EntranceRoom);

        Assert.That(player.TryWarpTo(new Vector2(0f, -4f), false), Is.True);
        Vector2 secondForwardEventPosition = player.LogicalPosition;
        Assert.That(facade.TryTraverse(forward), Is.True);
        Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom));
        AssertVector2Within(roomEventPositions[2], secondForwardEventPosition, 0.0001f, "second facade forward event pre-warp position");
        AssertVector2Within(player.LogicalPosition, forward.ArrivalAnchor.LogicalPosition, 0.0001f, "second facade forward authored arrival");

        Assert.That(player.TryWarpTo(new Vector2(0f, -2f), false), Is.True);
        Vector2 secondReverseEventPosition = player.LogicalPosition;
        Assert.That(facade.TryTraverse(reverse), Is.True);
        Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom));
        AssertVector2Within(roomEventPositions[3], secondReverseEventPosition, 0.0001f, "second facade reverse event pre-warp position");
        AssertVector2Within(player.LogicalPosition, reverse.ArrivalAnchor.LogicalPosition, 0.0001f, "second facade reverse authored arrival");
        Assert.That(roomEvents, Is.EqualTo(new[] { DrawingRoom, EntranceRoom, DrawingRoom, EntranceRoom }));
        Assert.That(roomEventDefinitions, Is.EqualTo(new[]
        {
            forward.Definition.DestinationRoom,
            reverse.Definition.DestinationRoom,
            forward.Definition.DestinationRoom,
            reverse.Definition.DestinationRoom
        }));
        Assert.That(facade.CurrentRoomDefinition, Is.SameAs(forward.Definition.SourceRoom));
        Assert.That(facade.CanTraverse(forward), Is.True);
        Assert.That(facade.CanTraverse(reverse), Is.False);
        Assert.That(outboundTrigger.gameObject.activeInHierarchy, Is.True);
        Assert.That(reverseTrigger.gameObject.activeInHierarchy, Is.False);
        RequireOnlyActiveRoom(EntranceRoom);

        Assert.That(arrivalEventCount, Is.Zero);
        Assert.That(movementStoppedEventCount, Is.Zero);
        Assert.That(player.HasDestination, Is.False);
        Assert.That(passageAudioSource.GetComponents<GameAudioSourceVolume>(), Is.Empty);
        Assert.That(GetPrivateStaticField<AudioSource>(typeof(DoorTriggerNavigation), "activeNavigationAudioSource"), Is.Null);
        Assert.That(FindInActiveScene<RoomNavigationManager>(), Has.Length.EqualTo(1));
        Assert.That(FindInActiveScene<MonoBehaviour>().Count(item => item is INavigationService), Is.EqualTo(1));
        Assert.That(FindInActiveScene<DoorTriggerNavigation>(), Has.Length.EqualTo(characterizedTriggerCount));
        Assert.That(FindInActiveScene<CanonicalPassage>(), Has.Length.EqualTo(characterizedPassageCount));
        Assert.That(FindInActiveScene<RoomContentGroup>(), Has.Length.EqualTo(characterizedRoomCount));
        Assert.That(FindInActiveScene<AudioSource>(), Has.Length.EqualTo(characterizedAudioSourceCount));
        Assert.That(forward.GetComponents<Component>(), Has.Length.EqualTo(outboundComponentCount));
        Assert.That(reverse.GetComponents<Component>(), Has.Length.EqualTo(reverseComponentCount));

        navigation.OnCurrentRoomChanged.RemoveListener(recordRoomEvent);
        player.ArrivedAtDestination -= recordArrival;
        player.MovementStopped -= recordMovementStopped;
    }

    [UnityTest]
    public IEnumerator EntranceDrawingPassagesWalkFromFarBeforeNavigating()
    {
        MainMenuController menu = RequireExactlyOneInActiveScene<MainMenuController>();
        menu.NewGame();
        yield return null;

        GameObject cursorChoice = GameObject.Find("Button_CursorStyle_01");
        Assert.That(cursorChoice, Is.Not.Null, "New Game must expose the cursor-style chooser.");
        Button cursorButton = cursorChoice.GetComponent<Button>();
        Assert.That(cursorButton, Is.Not.Null);
        cursorButton.onClick.Invoke();
        yield return SetAndWaitForRenderedGameViewResolution(1366, 768);

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameplaySceneName));

        RoomNavigationManager navigation = RequireExactlyOneInActiveScene<RoomNavigationManager>();
        GameObject playerObject = GameObject.Find("Player");
        Assert.That(playerObject, Is.Not.Null);
        PointClickPlayerMovement player = playerObject.GetComponent<PointClickPlayerMovement>();
        Assert.That(player, Is.Not.Null);
        DoorTriggerNavigation outbound = RequireSceneObject<DoorTriggerNavigation>("DoorTrigger_GEH_DrawingRoom");
        DoorTriggerNavigation reverse = RequireSceneObject<DoorTriggerNavigation>("DoorTrigger_DrawingRoom_GEH");
        CanonicalPassage outboundPassage = outbound.GetComponent<CanonicalPassage>();
        CanonicalPassage reversePassage = reverse.GetComponent<CanonicalPassage>();
        Assert.That(outboundPassage, Is.Not.Null);
        Assert.That(reversePassage, Is.Not.Null);
        Assert.That(GetPrivateField<CanonicalPassage>(outbound, "canonicalPassage"), Is.SameAs(outboundPassage));
        Assert.That(GetPrivateField<CanonicalPassage>(reverse, "canonicalPassage"), Is.SameAs(reversePassage));
        AudioSource passageAudioSource = FindInActiveScene<AudioSource>()
            .Single(item => item.gameObject.name == "Audio_DoorOpen");
        DoorOpenSoundCatalog passageDoorCatalog = GetPrivateField<DoorOpenSoundCatalog>(outbound, "doorOpenSoundCatalog");
        AssertDoorTriggerCompatibilityBindings(
            outbound,
            reverse,
            navigation,
            player.transform,
            passageAudioSource,
            passageDoorCatalog);
        CameraManager cameraManager = RequireExactlyOneInActiveScene<CameraManager>();
        Camera mainCamera = Camera.main;
        Assert.That(mainCamera, Is.Not.Null);
        bool originalPanRoomWithMouseEdges = cameraManager.panRoomWithMouseEdges;
        bool originalZoomRoomWithMouseWheel = cameraManager.zoomRoomWithMouseWheel;
        cameraManager.panRoomWithMouseEdges = false;
        cameraManager.zoomRoomWithMouseWheel = false;
        cameraManager.ResetRoomLookForPreview();
        yield return WaitForSettledLayout();

        Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom));
        Assert.That(outbound.gameObject.activeInHierarchy, Is.True);
        Assert.That(reverse.gameObject.activeInHierarchy, Is.False);
        bool originalInputEnabled = player.InputEnabled;
        float originalMoveSpeed = GetPrivateValue<float>(player, "moveSpeed");
        player.SetInputEnabled(true);

        List<string> orderedEvents = new List<string>();
        List<Vector2> arrivedPositions = new List<Vector2>();
        List<Vector2> movementStoppedPositions = new List<Vector2>();
        List<Vector2> roomChangedPositions = new List<Vector2>();
        System.Action recordArrival = () =>
        {
            arrivedPositions.Add(player.LogicalPosition);
            orderedEvents.Add(
                $"arrived:{navigation.CurrentRoom}:" +
                (GetPrivateStaticField<AudioSource>(typeof(DoorTriggerNavigation), "activeNavigationAudioSource") == null
                    ? "audio-idle"
                    : "audio-started"));
        };
        System.Action recordMovementStopped = () =>
        {
            movementStoppedPositions.Add(player.LogicalPosition);
            orderedEvents.Add(
                $"movement-stopped:{navigation.CurrentRoom}:" +
                (GetPrivateStaticField<AudioSource>(typeof(DoorTriggerNavigation), "activeNavigationAudioSource") == null
                    ? "audio-idle"
                    : "audio-started"));
        };
        UnityEngine.Events.UnityAction<string> recordRoomChanged = room =>
        {
            roomChangedPositions.Add(player.LogicalPosition);
            orderedEvents.Add(
                $"room-changed:{room}:" +
                (GetPrivateStaticField<AudioSource>(typeof(DoorTriggerNavigation), "activeNavigationAudioSource") == passageAudioSource
                    ? "audio-started"
                    : "audio-idle"));
        };
        player.ArrivedAtDestination += recordArrival;
        player.MovementStopped += recordMovementStopped;
        navigation.OnCurrentRoomChanged.AddListener(recordRoomChanged);

        Assert.That(passageAudioSource.GetComponents<GameAudioSourceVolume>(), Is.Empty);
        Assert.That(GetPrivateStaticField<AudioSource>(typeof(DoorTriggerNavigation), "activeNavigationAudioSource"), Is.Null);
        SetPrivateField(player, "moveSpeed", 1000f);

        Assert.That(
            TryWarpToCharacterizedFarStart(
                player,
                outbound,
                new Vector2(10f, -6f),
                out float forwardStartScreenDistance),
            Is.True,
            "The test requires one deterministic reachable far start in the Entrance.");
        Vector2 forwardStart = player.LogicalPosition;
        float forwardProximityLimit = GetPrivateValue<float>(outbound, "maxPlayerScreenDistance");
        Assert.That(InvokePrivateResult<bool>(outbound, "IsPlayerCloseEnough"), Is.False,
            "The sampled Butler position must exercise the far-click approach path.");
        Assert.That(forwardStartScreenDistance, Is.GreaterThan(forwardProximityLimit));
        Vector2 authoredForwardApproach = outboundPassage.ApproachAnchor.LogicalPosition;
        SetPrivateField(outboundPassage.ApproachAnchor, "logicalPosition", new Vector2(float.NaN, 0f));
        try
        {
            SetPrivateField(outbound, "lastPointerActivationFrame", -1);
            outbound.ActivateDoor();
            Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom));
            Assert.That(player.HasDestination, Is.False);
            AssertVector2Within(player.LogicalPosition, forwardStart, 0.0001f, "rejected non-finite trigger approach");
            Assert.That(GetPrivateStaticField<DoorTriggerNavigation>(typeof(DoorTriggerNavigation), "pendingApproachTrigger"), Is.Null);
            Assert.That(GetPrivateField<PointClickPlayerMovement>(outbound, "pendingApproachPlayer"), Is.Null);
            Assert.That(GetPrivateStaticField<AudioSource>(typeof(DoorTriggerNavigation), "activeNavigationAudioSource"), Is.Null);
            Assert.That(orderedEvents, Is.Empty);
        }
        finally
        {
            SetPrivateField(outboundPassage.ApproachAnchor, "logicalPosition", authoredForwardApproach);
        }

        Assert.That(
            TryInvokeApproachDestination(outbound, player, requireMovement: true, out Vector2 forwardLegacyApproach),
            Is.True);
        AssertVector2Within(
            forwardLegacyApproach,
            new Vector2(-7.576081f, -1.986423f),
            0.15f,
            "1366x768 legacy forward approach sample");
        Vector2 forwardApproach = outboundPassage.ApproachAnchor.LogicalPosition;
        Assert.That(Vector2.Distance(forwardLegacyApproach, forwardApproach), Is.GreaterThan(0.1f));

        string outboundLegacySource = GetPrivateField<string>(outbound, "sourceRoom");
        SetPrivateField(outbound, "sourceRoom", "__LEGACY_SOURCE_MUST_NOT_BE_USED__");
        SetPrivateField(outbound, "lastPointerActivationFrame", -1);
        outbound.ActivateDoor();

        Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom),
            "A far click must begin walking instead of changing rooms synchronously.");
        Assert.That(player.HasDestination, Is.True);
        Assert.That(GetPrivateValue<Vector2>(player, "finalDestination"), Is.EqualTo(forwardApproach));
        Assert.That(GetPrivateStaticField<DoorTriggerNavigation>(typeof(DoorTriggerNavigation), "pendingApproachTrigger"), Is.SameAs(outbound));
        Assert.That(GetPrivateField<PointClickPlayerMovement>(outbound, "pendingApproachPlayer"), Is.SameAs(player));
        Assert.That(passageAudioSource.GetComponent<GameAudioSourceVolume>(), Is.Null,
            "Door audio must not begin while the Butler is still approaching.");

        for (int frame = 0; frame < 120 && navigation.CurrentRoom == EntranceRoom && player.HasDestination; frame++)
        {
            InvokePrivateMethod(player, "MoveTowardDestination");
            yield return null;
        }
        yield return WaitForSettledLayout();
        SetPrivateField(outbound, "sourceRoom", outboundLegacySource);

        Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom));
        Assert.That(orderedEvents, Is.EqualTo(new[]
        {
            $"arrived:{EntranceRoom}:audio-idle",
            $"movement-stopped:{EntranceRoom}:audio-idle",
            $"room-changed:{DrawingRoom}:audio-started"
        }));
        Assert.That(arrivedPositions, Has.Count.EqualTo(1));
        Assert.That(movementStoppedPositions, Has.Count.EqualTo(1));
        Assert.That(roomChangedPositions, Has.Count.EqualTo(1));
        AssertVector2Within(arrivedPositions[0], forwardApproach, 0.0001f, "forward movement arrival");
        AssertVector2Within(movementStoppedPositions[0], forwardApproach, 0.0001f, "forward movement stop");
        AssertVector2Within(roomChangedPositions[0], forwardApproach, 0.0001f, "forward room event pre-warp position");
        Assert.That(GetPrivateStaticField<DoorTriggerNavigation>(typeof(DoorTriggerNavigation), "pendingApproachTrigger"), Is.Null);
        Assert.That(GetPrivateField<PointClickPlayerMovement>(outbound, "pendingApproachPlayer"), Is.Null);
        Assert.That(passageAudioSource.GetComponent<GameAudioSourceVolume>(), Is.Not.Null);
        GameAudioSourceVolume characterizedPassageBinding = passageAudioSource.GetComponent<GameAudioSourceVolume>();
        Assert.That(player.HasDestination, Is.False);
        Vector2 forwardArrival = player.LogicalPosition;
        AssertVector2Within(forwardArrival, new Vector2(5.267176f, -2.104616f), 0.0001f, "1366x768 forward arrival");

        InvokePrivateStaticMethod(typeof(DoorTriggerNavigation), "StopCurrentNavigationSound");
        Assert.That(
            TryWarpToCharacterizedFarStart(
                player,
                reverse,
                new Vector2(-10f, -6f),
                out float reverseStartScreenDistance),
            Is.True,
            "The test requires one deterministic reachable far start in the Drawing Room.");
        Vector2 reverseStart = player.LogicalPosition;
        float reverseProximityLimit = GetPrivateValue<float>(reverse, "maxPlayerScreenDistance");
        Assert.That(InvokePrivateResult<bool>(reverse, "IsPlayerCloseEnough"), Is.False,
            "The sampled Drawing Room position must exercise the reverse far-click approach path.");
        Assert.That(reverseStartScreenDistance, Is.GreaterThan(reverseProximityLimit));
        Assert.That(
            TryInvokeApproachDestination(reverse, player, requireMovement: true, out Vector2 reverseLegacyApproach),
            Is.True);
        AssertVector2Within(
            reverseLegacyApproach,
            new Vector2(5.280546f, -2.015396f),
            0.0001f,
            "1366x768 legacy reverse approach sample");
        Vector2 reverseApproach = reversePassage.ApproachAnchor.LogicalPosition;
        Assert.That(reverseApproach, Is.Not.EqualTo(reverseLegacyApproach));

        string reverseLegacySource = GetPrivateField<string>(reverse, "sourceRoom");
        SetPrivateField(reverse, "sourceRoom", "__LEGACY_SOURCE_MUST_NOT_BE_USED__");
        SetPrivateField(reverse, "lastPointerActivationFrame", -1);
        reverse.ActivateDoor();

        Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom),
            "A far reverse click must begin walking instead of changing rooms synchronously.");
        Assert.That(player.HasDestination, Is.True);
        Assert.That(GetPrivateValue<Vector2>(player, "finalDestination"), Is.EqualTo(reverseApproach));
        Assert.That(GetPrivateStaticField<DoorTriggerNavigation>(typeof(DoorTriggerNavigation), "pendingApproachTrigger"), Is.SameAs(reverse));
        Assert.That(GetPrivateField<PointClickPlayerMovement>(reverse, "pendingApproachPlayer"), Is.SameAs(player));
        Assert.That(GetPrivateStaticField<AudioSource>(typeof(DoorTriggerNavigation), "activeNavigationAudioSource"), Is.Null);

        for (int frame = 0; frame < 120 && navigation.CurrentRoom == DrawingRoom && player.HasDestination; frame++)
        {
            InvokePrivateMethod(player, "MoveTowardDestination");
            yield return null;
        }
        yield return WaitForSettledLayout();
        SetPrivateField(reverse, "sourceRoom", reverseLegacySource);

        Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom));
        Assert.That(orderedEvents, Is.EqualTo(new[]
        {
            $"arrived:{EntranceRoom}:audio-idle",
            $"movement-stopped:{EntranceRoom}:audio-idle",
            $"room-changed:{DrawingRoom}:audio-started",
            $"arrived:{DrawingRoom}:audio-idle",
            $"movement-stopped:{DrawingRoom}:audio-idle",
            $"room-changed:{EntranceRoom}:audio-started"
        }));
        Assert.That(arrivedPositions, Has.Count.EqualTo(2));
        Assert.That(movementStoppedPositions, Has.Count.EqualTo(2));
        Assert.That(roomChangedPositions, Has.Count.EqualTo(2));
        AssertVector2Within(arrivedPositions[1], reverseApproach, 0.0001f, "reverse movement arrival");
        AssertVector2Within(movementStoppedPositions[1], reverseApproach, 0.0001f, "reverse movement stop");
        AssertVector2Within(roomChangedPositions[1], reverseApproach, 0.0001f, "reverse room event pre-warp position");
        Assert.That(GetPrivateStaticField<DoorTriggerNavigation>(typeof(DoorTriggerNavigation), "pendingApproachTrigger"), Is.Null);
        Assert.That(GetPrivateField<PointClickPlayerMovement>(reverse, "pendingApproachPlayer"), Is.Null);
        Assert.That(player.HasDestination, Is.False);
        Vector2 reverseArrival = player.LogicalPosition;
        AssertVector2Within(reverseArrival, reversePassage.ArrivalAnchor.LogicalPosition, 0.0001f, "1366x768 canonical reverse arrival");
        Assert.That(passageAudioSource.GetComponents<GameAudioSourceVolume>(), Has.Length.EqualTo(1));
        Assert.That(passageAudioSource.GetComponent<GameAudioSourceVolume>(), Is.SameAs(characterizedPassageBinding));

        SetPrivateField<CanonicalPassage>(outbound, "canonicalPassage", null);

        InvokePrivateStaticMethod(typeof(DoorTriggerNavigation), "StopCurrentNavigationSound");
        Assert.That(
            TryWarpToCharacterizedFarStart(
                player,
                outbound,
                new Vector2(10f, -6f),
                out float legacyFallbackStartScreenDistance),
            Is.True);
        Assert.That(InvokePrivateResult<bool>(outbound, "IsPlayerCloseEnough"), Is.False);
        Assert.That(legacyFallbackStartScreenDistance, Is.GreaterThan(forwardProximityLimit));
        Assert.That(
            TryInvokeApproachDestination(outbound, player, requireMovement: true, out Vector2 legacyFallbackApproach),
            Is.True);
        Assert.That(Vector2.Distance(legacyFallbackApproach, outboundPassage.ApproachAnchor.LogicalPosition), Is.GreaterThan(0.1f),
            "The null-Passage branch must retain the source-sensitive legacy sampler instead of the shared canonical anchor.");
        SetPrivateField(outbound, "lastPointerActivationFrame", -1);
        outbound.ActivateDoor();
        Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom));
        Assert.That(player.HasDestination, Is.True);
        AssertVector2Within(
            GetPrivateValue<Vector2>(player, "finalDestination"),
            legacyFallbackApproach,
            0.0001f,
            "null-Passage legacy far approach");
        Assert.That(GetPrivateStaticField<DoorTriggerNavigation>(typeof(DoorTriggerNavigation), "pendingApproachTrigger"), Is.SameAs(outbound));
        Assert.That(GetPrivateField<PointClickPlayerMovement>(outbound, "pendingApproachPlayer"), Is.SameAs(player));
        Assert.That(GetPrivateStaticField<AudioSource>(typeof(DoorTriggerNavigation), "activeNavigationAudioSource"), Is.Null);
        Assert.That(arrivedPositions, Has.Count.EqualTo(2));
        Assert.That(movementStoppedPositions, Has.Count.EqualTo(2));
        Assert.That(roomChangedPositions, Has.Count.EqualTo(2));

        for (int frame = 0; frame < 120 && navigation.CurrentRoom == EntranceRoom && player.HasDestination; frame++)
        {
            InvokePrivateMethod(player, "MoveTowardDestination");
            yield return null;
        }
        yield return WaitForSettledLayout();

        Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom));
        Assert.That(player.HasDestination, Is.False);
        Assert.That(arrivedPositions, Has.Count.EqualTo(3));
        Assert.That(movementStoppedPositions, Has.Count.EqualTo(3));
        Assert.That(roomChangedPositions, Has.Count.EqualTo(3));
        AssertVector2Within(arrivedPositions[2], legacyFallbackApproach, 0.0001f, "legacy far forward movement arrival");
        AssertVector2Within(movementStoppedPositions[2], legacyFallbackApproach, 0.0001f, "legacy far forward movement stop");
        AssertVector2Within(roomChangedPositions[2], legacyFallbackApproach, 0.0001f, "legacy far forward room event pre-warp position");
        Assert.That(GetPrivateStaticField<DoorTriggerNavigation>(typeof(DoorTriggerNavigation), "pendingApproachTrigger"), Is.Null);
        Assert.That(GetPrivateField<PointClickPlayerMovement>(outbound, "pendingApproachPlayer"), Is.Null);
        Vector2 legacyFarForwardArrival = player.LogicalPosition;
        AssertVector2Within(legacyFarForwardArrival, new Vector2(5.231221f, -2.002137f), 0.001f, "legacy far forward arrival");
        Assert.That(Vector2.Distance(legacyFarForwardArrival, forwardArrival), Is.GreaterThan(0.1f),
            "The null-Passage branch must retain its legacy sampled arrival instead of using the canonical authored anchor.");
        Assert.That(passageAudioSource.GetComponent<GameAudioSourceVolume>(), Is.SameAs(characterizedPassageBinding));

        InvokePrivateStaticMethod(typeof(DoorTriggerNavigation), "StopCurrentNavigationSound");
        Assert.That(
            TryWarpToCharacterizedFarStart(
                player,
                reverse,
                new Vector2(-10f, -6f),
                out float legacyReverseFallbackStartScreenDistance),
            Is.True);
        Assert.That(InvokePrivateResult<bool>(reverse, "IsPlayerCloseEnough"), Is.False);
        Assert.That(legacyReverseFallbackStartScreenDistance, Is.GreaterThan(reverseProximityLimit));
        Assert.That(
            TryInvokeApproachDestination(reverse, player, requireMovement: true, out Vector2 legacyReverseFallbackApproach),
            Is.True);
        Assert.That(Vector2.Distance(legacyReverseFallbackApproach, reversePassage.ApproachAnchor.LogicalPosition), Is.GreaterThan(0.05f));
        SetPrivateField<CanonicalPassage>(reverse, "canonicalPassage", null);
        SetPrivateField(reverse, "lastPointerActivationFrame", -1);
        reverse.ActivateDoor();
        Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom));
        Assert.That(player.HasDestination, Is.True);
        AssertVector2Within(
            GetPrivateValue<Vector2>(player, "finalDestination"),
            legacyReverseFallbackApproach,
            0.0001f,
            "null-Passage legacy reverse far approach");
        Assert.That(GetPrivateStaticField<DoorTriggerNavigation>(typeof(DoorTriggerNavigation), "pendingApproachTrigger"), Is.SameAs(reverse));
        Assert.That(GetPrivateField<PointClickPlayerMovement>(reverse, "pendingApproachPlayer"), Is.SameAs(player));
        Assert.That(GetPrivateStaticField<AudioSource>(typeof(DoorTriggerNavigation), "activeNavigationAudioSource"), Is.Null);

        for (int frame = 0; frame < 120 && navigation.CurrentRoom == DrawingRoom && player.HasDestination; frame++)
        {
            InvokePrivateMethod(player, "MoveTowardDestination");
            yield return null;
        }
        yield return WaitForSettledLayout();

        Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom));
        Assert.That(player.HasDestination, Is.False);
        Assert.That(arrivedPositions, Has.Count.EqualTo(4));
        Assert.That(movementStoppedPositions, Has.Count.EqualTo(4));
        Assert.That(roomChangedPositions, Has.Count.EqualTo(4));
        AssertVector2Within(arrivedPositions[3], legacyReverseFallbackApproach, 0.0001f, "legacy far reverse movement arrival");
        AssertVector2Within(movementStoppedPositions[3], legacyReverseFallbackApproach, 0.0001f, "legacy far reverse movement stop");
        AssertVector2Within(roomChangedPositions[3], legacyReverseFallbackApproach, 0.0001f, "legacy far reverse room event pre-warp position");
        Assert.That(GetPrivateStaticField<DoorTriggerNavigation>(typeof(DoorTriggerNavigation), "pendingApproachTrigger"), Is.Null);
        Assert.That(GetPrivateField<PointClickPlayerMovement>(reverse, "pendingApproachPlayer"), Is.Null);
        Vector2 legacyFarReverseArrival = player.LogicalPosition;
        AssertVector2Within(legacyFarReverseArrival, new Vector2(-7.703568f, -2.000136f), 0.001f, "legacy far reverse arrival");
        Assert.That(passageAudioSource.GetComponent<GameAudioSourceVolume>(), Is.SameAs(characterizedPassageBinding));

        int arrivalEventCountBeforeNearRoutes = arrivedPositions.Count;
        int movementStopCountBeforeNearRoutes = movementStoppedPositions.Count;
        InvokePrivateStaticMethod(typeof(DoorTriggerNavigation), "StopCurrentNavigationSound");
        Assert.That(InvokePrivateResult<bool>(outbound, "IsPlayerCloseEnough"), Is.True,
            "The reverse arrival must be near the reciprocal Entrance passage.");
        SetPrivateField(outbound, "lastPointerActivationFrame", -1);
        Vector2 legacyNearForwardSource = player.LogicalPosition;
        outbound.ActivateDoor();
        Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom),
            "A near forward click must navigate synchronously without starting movement.");
        Assert.That(player.HasDestination, Is.False);
        Assert.That(arrivedPositions, Has.Count.EqualTo(arrivalEventCountBeforeNearRoutes));
        Assert.That(movementStoppedPositions, Has.Count.EqualTo(movementStopCountBeforeNearRoutes));
        Assert.That(roomChangedPositions, Has.Count.EqualTo(5));
        AssertVector2Within(roomChangedPositions[4], legacyNearForwardSource, 0.0001f, "legacy near forward room event pre-warp position");
        Vector2 nearForwardArrival = player.LogicalPosition;
        AssertVector2Within(
            nearForwardArrival,
            new Vector2(5.231221f, -2.002137f),
            0.001f,
            "1366x768 near forward arrival");
        Assert.That(Vector2.Distance(nearForwardArrival, forwardArrival), Is.GreaterThan(0.1f),
            "The legacy arrival sampler is currently source-position-sensitive; the canonical anchor slice must normalize this deliberately.");

        InvokePrivateStaticMethod(typeof(DoorTriggerNavigation), "StopCurrentNavigationSound");
        Assert.That(InvokePrivateResult<bool>(reverse, "IsPlayerCloseEnough"), Is.True,
            "The forward arrival must be near the reciprocal Drawing Room passage.");
        Vector2 legacyNearReverseSource = player.LogicalPosition;
        reverse.ActivateDoor();
        Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom),
            "A near reverse click must navigate synchronously without starting movement.");
        Assert.That(player.HasDestination, Is.False);
        Assert.That(arrivedPositions, Has.Count.EqualTo(arrivalEventCountBeforeNearRoutes));
        Assert.That(movementStoppedPositions, Has.Count.EqualTo(movementStopCountBeforeNearRoutes));
        Assert.That(roomChangedPositions, Has.Count.EqualTo(6));
        AssertVector2Within(roomChangedPositions[5], legacyNearReverseSource, 0.0001f, "legacy near reverse room event pre-warp position");
        Vector2 nearReverseArrival = player.LogicalPosition;
        AssertVector2Within(nearReverseArrival, new Vector2(-7.703568f, -2.000136f), 0.001f, "1366x768 legacy near reverse arrival");
        Assert.That(Vector2.Distance(nearReverseArrival, reverseArrival), Is.GreaterThan(0.2f),
            "The legacy sampler must remain characterized separately from the shared canonical anchor.");
        SetPrivateField(outbound, "canonicalPassage", outboundPassage);
        SetPrivateField(reverse, "canonicalPassage", reversePassage);
        Assert.That(GetPrivateField<CanonicalPassage>(outbound, "canonicalPassage"), Is.SameAs(outboundPassage));
        Assert.That(GetPrivateField<CanonicalPassage>(reverse, "canonicalPassage"), Is.SameAs(reversePassage));

        InvokePrivateStaticMethod(typeof(DoorTriggerNavigation), "StopCurrentNavigationSound");
        Assert.That(InvokePrivateResult<bool>(outbound, "IsPlayerCloseEnough"), Is.True);
        INavigationService restoredFacade = navigation;
        Chateau.Architecture.GameRoot restoredGameRoot = RequireExactlyOneInActiveScene<Chateau.Architecture.GameRoot>();
        Assert.That(outboundPassage.HasGameContext, Is.True);
        Assert.That(reversePassage.HasGameContext, Is.True);
        Assert.That(restoredFacade.CurrentRoomDefinition, Is.SameAs(outboundPassage.Definition.SourceRoom));
        Assert.That(outboundPassage.SourceRoomView.Definition, Is.SameAs(outboundPassage.Definition.SourceRoom));
        Assert.That(reversePassage.SourceRoomView.Definition, Is.SameAs(outboundPassage.Definition.DestinationRoom));
        Assert.That(outboundPassage.ReversePassage, Is.SameAs(reversePassage));
        Assert.That(reversePassage.ReversePassage, Is.SameAs(outboundPassage));
        Assert.That(restoredGameRoot.Context.Database.Definitions, Does.Contain(outboundPassage.Definition));
        Assert.That(restoredGameRoot.Context.Database.Definitions, Does.Contain(reversePassage.Definition));
        Assert.That(restoredFacade.CanTraverse(outboundPassage), Is.True);
        Assert.That(player.InputEnabled, Is.True);
        SetPrivateField(outbound, "lastPointerActivationFrame", -1);
        Vector2 canonicalNearForwardSource = player.LogicalPosition;
        outbound.ActivateDoor();
        Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom));
        Assert.That(player.HasDestination, Is.False);
        Assert.That(arrivedPositions, Has.Count.EqualTo(arrivalEventCountBeforeNearRoutes));
        Assert.That(movementStoppedPositions, Has.Count.EqualTo(movementStopCountBeforeNearRoutes));
        Assert.That(roomChangedPositions, Has.Count.EqualTo(7));
        AssertVector2Within(roomChangedPositions[6], canonicalNearForwardSource, 0.0001f, "canonical near forward room event pre-warp position");
        Vector2 canonicalNearForwardArrival = player.LogicalPosition;
        AssertVector2Within(canonicalNearForwardArrival, outboundPassage.ArrivalAnchor.LogicalPosition, 0.0001f, "canonical near forward authored arrival");
        AssertVector2Within(canonicalNearForwardArrival, forwardArrival, 0.0001f, "canonical forward source-invariant arrival");

        InvokePrivateStaticMethod(typeof(DoorTriggerNavigation), "StopCurrentNavigationSound");
        Assert.That(InvokePrivateResult<bool>(reverse, "IsPlayerCloseEnough"), Is.True);
        SetPrivateField(reverse, "lastPointerActivationFrame", -1);
        Vector2 canonicalNearReverseSource = player.LogicalPosition;
        reverse.ActivateDoor();
        Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom));
        Assert.That(player.HasDestination, Is.False);
        Assert.That(arrivedPositions, Has.Count.EqualTo(arrivalEventCountBeforeNearRoutes));
        Assert.That(movementStoppedPositions, Has.Count.EqualTo(movementStopCountBeforeNearRoutes));
        Assert.That(roomChangedPositions, Has.Count.EqualTo(8));
        AssertVector2Within(roomChangedPositions[7], canonicalNearReverseSource, 0.0001f, "canonical near reverse room event pre-warp position");
        Vector2 canonicalNearReverseArrival = player.LogicalPosition;
        AssertVector2Within(canonicalNearReverseArrival, reversePassage.ArrivalAnchor.LogicalPosition, 0.0001f, "canonical near reverse authored arrival");
        AssertVector2Within(canonicalNearReverseArrival, reverseArrival, 0.0001f, "canonical reverse source-invariant arrival");

        Assert.That(orderedEvents, Is.EqualTo(new[]
        {
            $"arrived:{EntranceRoom}:audio-idle",
            $"movement-stopped:{EntranceRoom}:audio-idle",
            $"room-changed:{DrawingRoom}:audio-started",
            $"arrived:{DrawingRoom}:audio-idle",
            $"movement-stopped:{DrawingRoom}:audio-idle",
            $"room-changed:{EntranceRoom}:audio-started",
            $"arrived:{EntranceRoom}:audio-idle",
            $"movement-stopped:{EntranceRoom}:audio-idle",
            $"room-changed:{DrawingRoom}:audio-started",
            $"arrived:{DrawingRoom}:audio-idle",
            $"movement-stopped:{DrawingRoom}:audio-idle",
            $"room-changed:{EntranceRoom}:audio-started",
            $"room-changed:{DrawingRoom}:audio-started",
            $"room-changed:{EntranceRoom}:audio-started",
            $"room-changed:{DrawingRoom}:audio-started",
            $"room-changed:{EntranceRoom}:audio-started"
        }));
        Assert.That(passageAudioSource.GetComponents<GameAudioSourceVolume>(), Has.Length.EqualTo(1));
        Assert.That(passageAudioSource.GetComponent<GameAudioSourceVolume>(), Is.SameAs(characterizedPassageBinding));
        AssertDoorTriggerCompatibilityBindings(
            outbound,
            reverse,
            navigation,
            player.transform,
            passageAudioSource,
            passageDoorCatalog);

        Debug.Log(
            $"[PassageApproachCharacterization] viewport={Screen.width}x{Screen.height} camera={mainCamera.pixelRect} " +
            $"forwardStart={forwardStart.x:0.######},{forwardStart.y:0.######} " +
            $"forwardStartDistance={forwardStartScreenDistance:0.###} " +
            $"forwardApproach={forwardApproach.x:0.######},{forwardApproach.y:0.######} " +
            $"forwardArrival={forwardArrival.x:0.######},{forwardArrival.y:0.######} " +
            $"nearForwardArrival={nearForwardArrival.x:0.######},{nearForwardArrival.y:0.######} " +
            $"canonicalNearForwardArrival={canonicalNearForwardArrival.x:0.######},{canonicalNearForwardArrival.y:0.######} " +
            $"reverseStart={reverseStart.x:0.######},{reverseStart.y:0.######} " +
            $"reverseStartDistance={reverseStartScreenDistance:0.###} " +
            $"reverseApproach={reverseApproach.x:0.######},{reverseApproach.y:0.######} " +
            $"reverseArrival={reverseArrival.x:0.######},{reverseArrival.y:0.######} " +
            $"nearReverseArrival={nearReverseArrival.x:0.######},{nearReverseArrival.y:0.######} " +
            $"canonicalNearReverseArrival={canonicalNearReverseArrival.x:0.######},{canonicalNearReverseArrival.y:0.######} " +
            $"legacyFarForwardArrival={legacyFarForwardArrival.x:0.######},{legacyFarForwardArrival.y:0.######} " +
            $"legacyFarReverseArrival={legacyFarReverseArrival.x:0.######},{legacyFarReverseArrival.y:0.######} " +
            $"events={string.Join("->", orderedEvents)}");

        InvokePrivateStaticMethod(typeof(DoorTriggerNavigation), "StopCurrentNavigationSound");
        Assert.That(GetPrivateStaticField<DoorTriggerNavigation>(typeof(DoorTriggerNavigation), "pendingApproachTrigger"), Is.Null);
        Assert.That(GetPrivateField<PointClickPlayerMovement>(outbound, "pendingApproachPlayer"), Is.Null);
        Assert.That(GetPrivateField<PointClickPlayerMovement>(reverse, "pendingApproachPlayer"), Is.Null);
        Assert.That(GetPrivateStaticField<AudioSource>(typeof(DoorTriggerNavigation), "activeNavigationAudioSource"), Is.Null);
        player.ArrivedAtDestination -= recordArrival;
        player.MovementStopped -= recordMovementStopped;
        navigation.OnCurrentRoomChanged.RemoveListener(recordRoomChanged);
        SetPrivateField(player, "moveSpeed", originalMoveSpeed);
        player.SetInputEnabled(originalInputEnabled);
        cameraManager.panRoomWithMouseEdges = originalPanRoomWithMouseEdges;
        cameraManager.zoomRoomWithMouseWheel = originalZoomRoomWithMouseWheel;
    }

    [UnityTest]
    public IEnumerator DrawingMusicLegacyPassagesAreCharacterizedBeforeCanonicalMigration()
    {
        MainMenuController menu = RequireExactlyOneInActiveScene<MainMenuController>();
        menu.NewGame();
        yield return null;

        GameObject cursorChoice = GameObject.Find("Button_CursorStyle_01");
        Assert.That(cursorChoice, Is.Not.Null);
        Button cursorButton = cursorChoice.GetComponent<Button>();
        Assert.That(cursorButton, Is.Not.Null);
        cursorButton.onClick.Invoke();
        yield return SetAndWaitForRenderedGameViewResolution(1366, 768);

        RoomNavigationManager navigation = RequireExactlyOneInActiveScene<RoomNavigationManager>();
        INavigationService navigationFacade = navigation;
        PointClickPlayerMovement player = GameObject.Find("Player").GetComponent<PointClickPlayerMovement>();
        Assert.That(player, Is.Not.Null);
        CameraManager cameraManager = RequireExactlyOneInActiveScene<CameraManager>();
        bool originalInputEnabled = player.InputEnabled;
        float originalMoveSpeed = GetPrivateValue<float>(player, "moveSpeed");
        bool originalPanRoomWithMouseEdges = cameraManager.panRoomWithMouseEdges;
        bool originalZoomRoomWithMouseWheel = cameraManager.zoomRoomWithMouseWheel;
        player.SetInputEnabled(true);
        cameraManager.panRoomWithMouseEdges = false;
        cameraManager.zoomRoomWithMouseWheel = false;
        cameraManager.ResetRoomLookForPreview();
        yield return WaitForSettledLayout();

        DoorTriggerNavigation forward = RequireSceneObject<DoorTriggerNavigation>("DoorTrigger_DrawingRoom_MusicRoom");
        DoorTriggerNavigation reverse = RequireSceneObject<DoorTriggerNavigation>("DoorTrigger_MusicRoom_DrawingRoom");
        Assert.That(GetPrivateField<CanonicalPassage>(forward, "canonicalPassage"), Is.Null);
        Assert.That(GetPrivateField<CanonicalPassage>(reverse, "canonicalPassage"), Is.Null);
        AssertSerializedDrawingMusicDependenciesAreBound();

        DoorTriggerNavigation setupTrigger = RequireSceneObject<DoorTriggerNavigation>("DoorTrigger_GEH_DrawingRoom");
        CanonicalPassage setupPassage = setupTrigger.GetComponent<CanonicalPassage>();
        Assert.That(setupPassage, Is.Not.Null);
        Assert.That(player.TryWarpToExact(setupPassage.ApproachAnchor.LogicalPosition), Is.True);
        Assert.That(navigationFacade.TryTraverse(setupPassage), Is.True);
        yield return WaitForSettledLayout();
        Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom));
        RoomContentGroup drawingRoomContent = RequireOnlyActiveRoom(DrawingRoom);
        RoomContentGroup musicRoomContent = FindInActiveScene<RoomContentGroup>()
            .Single(item => item.RoomName == MusicRoom);
        CanonicalRoomView drawingRoomView = drawingRoomContent.GetComponent<CanonicalRoomView>();
        CanonicalRoomView musicRoomView = musicRoomContent.GetComponent<CanonicalRoomView>();
        Assert.That(drawingRoomView, Is.Not.Null);
        Assert.That(musicRoomView, Is.Not.Null);
        Assert.That(musicRoomView.Definition, Is.Not.Null);
        Assert.That(musicRoomView.Definition.StableId, Is.EqualTo("room.music-room"));
        Assert.That(musicRoomView.LegacyContentGroup, Is.SameAs(musicRoomContent));
        Assert.That(musicRoomView.Root, Is.SameAs(musicRoomContent.transform));
        Assert.That(musicRoomView.HasGameContext, Is.True);
        Assert.That(FindInActiveScene<CanonicalRoomView>(), Has.Length.EqualTo(3));
        Assert.That(FindInActiveScene<CanonicalPassage>(), Has.Length.EqualTo(4));
        CanonicalPassage forwardPassage = forward.GetComponent<CanonicalPassage>();
        CanonicalPassage reversePassage = reverse.GetComponent<CanonicalPassage>();
        AssertDrawingMusicPassagesRemainPassive(
            forward,
            reverse,
            forwardPassage,
            reversePassage,
            drawingRoomView,
            musicRoomView,
            true,
            false);
        Assert.That(drawingRoomContent.TryGetRoomBackgroundTexture(out Texture drawingBackground), Is.True);
        Assert.That(musicRoomContent.TryGetRoomBackgroundTexture(out Texture musicBackground), Is.True);
        Assert.That(cameraManager.cameraBackground.texture, Is.SameAs(drawingBackground));
        Assert.That(GetPrivateField<RoomContentGroup>(cameraManager, "activeRoomContentGroup"),
            Is.SameAs(drawingRoomContent));
        Assert.That(GetPrivateField<RectTransform>(cameraManager, "activeRoomStage"),
            Is.SameAs(drawingRoomContent.transform));

        Assert.That(forward.SourceRoom, Is.EqualTo(DrawingRoom));
        Assert.That(forward.DoorName, Is.EqualTo("DrawingRoom_MusicRoom"));
        Assert.That(forward.DestinationRoom, Is.EqualTo(MusicRoom));
        Assert.That(reverse.SourceRoom, Is.EqualTo(MusicRoom));
        Assert.That(reverse.DoorName, Is.EqualTo("MusicRoom_DrawingRoom"));
        Assert.That(reverse.DestinationRoom, Is.EqualTo(DrawingRoom));
        RectTransform forwardRect = forward.transform as RectTransform;
        RectTransform reverseRect = reverse.transform as RectTransform;
        Assert.That(forwardRect, Is.Not.Null);
        Assert.That(reverseRect, Is.Not.Null);
        Assert.That(forwardRect.parent.name, Is.EqualTo("Doors"));
        Assert.That(reverseRect.parent.name, Is.EqualTo("Doors"));
        Assert.That(forwardRect.parent.parent, Is.SameAs(drawingRoomContent.transform));
        Assert.That(reverseRect.parent.parent, Is.SameAs(musicRoomContent.transform));
        AssertVector2Within(forwardRect.anchoredPosition, new Vector2(-628f, 55f), 0.0001f,
            "Drawing-to-Music trigger position");
        AssertVector2Within(forwardRect.sizeDelta, new Vector2(163.2982f, 299.816f), 0.0001f,
            "Drawing-to-Music trigger size");
        AssertVector2Within(reverseRect.anchoredPosition, new Vector2(-685f, -36f), 0.0001f,
            "Music-to-Drawing trigger position");
        AssertVector2Within(reverseRect.sizeDelta, new Vector2(210.47021f, 416.47205f), 0.0001f,
            "Music-to-Drawing trigger size");
        Assert.That(forwardRect.localScale, Is.EqualTo(Vector3.one));
        Assert.That(reverseRect.localScale, Is.EqualTo(new Vector3(0.8625f, 1f, 1f)));
        Assert.That(forward.GetComponents<Component>(), Has.Length.EqualTo(5));
        Assert.That(reverse.GetComponents<Component>(), Has.Length.EqualTo(5));
        Assert.That(forward.GetComponent<Image>(), Is.Not.Null);
        Assert.That(reverse.GetComponent<Image>(), Is.Not.Null);
        Assert.That(forward.GetComponent<CanonicalPassage>(), Is.SameAs(forwardPassage));
        Assert.That(reverse.GetComponent<CanonicalPassage>(), Is.SameAs(reversePassage));
        Assert.That(drawingRoomContent.gameObject.activeSelf, Is.True);
        Assert.That(musicRoomContent.gameObject.activeSelf, Is.False);
        Assert.That(musicRoomView.IsVisible, Is.False);
        Assert.That(forward.gameObject.activeInHierarchy, Is.True);
        Assert.That(reverse.gameObject.activeInHierarchy, Is.False);
        Assert.That(forwardPassage.isActiveAndEnabled, Is.True);
        Assert.That(reversePassage.isActiveAndEnabled, Is.False);

        Chapter2GuestPanicController guestPanic = RequireExactlyOneInActiveScene<Chapter2GuestPanicController>();
        Assert.That(GetPrivateField<RectTransform>(guestPanic, "leftExitTarget"), Is.SameAs(forwardRect));
        Assert.That(GetPrivateField<string>(guestPanic, "leftExitTargetName"),
            Is.EqualTo("DoorTrigger_DrawingRoom_MusicRoom"));

        DoorPromptSequenceController prompts = RequireExactlyOneInActiveScene<DoorPromptSequenceController>();
        TMP_Text passagePromptText = GetPrivateField<TMP_Text>(prompts, "promptText");
        Assert.That(passagePromptText, Is.Not.Null);
        if (DoorTriggerNavigation.HoveredTrigger != null)
        {
            DoorTriggerNavigation.HoveredTrigger.OnPointerExit(null);
        }
        Assert.That(DoorTriggerNavigation.HoveredTrigger, Is.Null);
        Assert.That(passagePromptText.gameObject.activeSelf, Is.False);

        AudioSource passageAudioSource = FindInActiveScene<AudioSource>()
            .Single(item => item.gameObject.name == "Audio_DoorOpen");
        DoorOpenSoundCatalog passageDoorCatalog =
            GetPrivateField<DoorOpenSoundCatalog>(setupTrigger, "doorOpenSoundCatalog");
        InvokePrivateMethod(forward, "ResolvePlayerReference");
        InvokePrivateMethod(reverse, "ResolvePlayerReference");
        AssertDrawingMusicRuntimeDependenciesMatchTemplate(
            forward,
            reverse,
            setupTrigger,
            navigation,
            player.transform,
            passageAudioSource,
            passageDoorCatalog);
        Assert.That(GetPrivateField<RoomNavigationManager>(forward, "navigationManager"), Is.SameAs(navigation));
        Assert.That(GetPrivateField<Transform>(forward, "player"), Is.SameAs(player.transform));
        Assert.That(GetPrivateField<AudioSource>(forward, "doorOpenAudioSource"), Is.SameAs(passageAudioSource));
        Assert.That(GetPrivateField<DoorOpenSoundCatalog>(forward, "doorOpenSoundCatalog"), Is.SameAs(passageDoorCatalog));
        Assert.That(GetPrivateField<DoorOpenSoundCatalog>(forward, "stairwaySoundCatalog"), Is.Null);
        Assert.That(GetPrivateField<CanonicalPassage>(forward, "canonicalPassage"), Is.Null);
        InvokePrivateStaticMethod(typeof(DoorTriggerNavigation), "StopCurrentNavigationSound");
        Assert.That(passageAudioSource.GetComponents<GameAudioSourceVolume>(), Is.Empty);

        List<string> orderedEvents = new List<string>();
        List<Vector2> arrivedPositions = new List<Vector2>();
        List<Vector2> movementStoppedPositions = new List<Vector2>();
        List<Vector2> roomChangedPositions = new List<Vector2>();
        System.Action recordArrival = () =>
        {
            arrivedPositions.Add(player.LogicalPosition);
            orderedEvents.Add(
                $"arrived:{navigation.CurrentRoom}:" +
                (GetPrivateStaticField<AudioSource>(typeof(DoorTriggerNavigation), "activeNavigationAudioSource") == null
                    ? "audio-idle"
                    : "audio-started"));
        };
        System.Action recordMovementStopped = () =>
        {
            movementStoppedPositions.Add(player.LogicalPosition);
            orderedEvents.Add(
                $"movement-stopped:{navigation.CurrentRoom}:" +
                (GetPrivateStaticField<AudioSource>(typeof(DoorTriggerNavigation), "activeNavigationAudioSource") == null
                    ? "audio-idle"
                    : "audio-started"));
        };
        UnityEngine.Events.UnityAction<string> recordRoomChanged = room =>
        {
            roomChangedPositions.Add(player.LogicalPosition);
            orderedEvents.Add(
                $"room-changed:{room}:" +
                (GetPrivateStaticField<AudioSource>(typeof(DoorTriggerNavigation), "activeNavigationAudioSource") == passageAudioSource
                    ? "audio-started"
                    : "audio-idle"));
        };
        player.ArrivedAtDestination += recordArrival;
        player.MovementStopped += recordMovementStopped;
        navigation.OnCurrentRoomChanged.AddListener(recordRoomChanged);
        SetPrivateField(player, "moveSpeed", 1000f);

        InvokePrivateMethod(forward, "ResolvePlayerReference");
        Vector2 forwardStart = player.LogicalPosition;
        float forwardStartDistance = InvokePrivateResult<float>(forward, "GetPlayerScreenDistanceToTrigger");
        Assert.That(InvokePrivateResult<bool>(forward, "IsPlayerCloseEnough"), Is.False);
        Assert.That(forwardStartDistance, Is.GreaterThan(GetPrivateValue<float>(forward, "maxPlayerScreenDistance")));
        Assert.That(TryInvokeApproachDestination(forward, player, true, out Vector2 forwardApproach), Is.True);
        AssertFinite(forwardApproach, "Drawing-to-Music far approach");
        AssertApproachWithinActivationDistance(forward, player, forwardApproach, "Drawing-to-Music far approach");

        forward.OnPointerEnter(null);
        Assert.That(DoorTriggerNavigation.HoveredTrigger, Is.SameAs(forward));
        Assert.That(GetPrivateStaticField<object>(typeof(NavigationCursorController), "doorHoverOwner"),
            Is.SameAs(forward));
        Assert.That(passagePromptText.gameObject.activeSelf, Is.True);
        Assert.That(passagePromptText.text, Is.EqualTo("Open Door"));
        SetPrivateField(forward, "lastPointerActivationFrame", -1);
        forward.ActivateDoor();
        Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom));
        Assert.That(player.HasDestination, Is.True);
        AssertVector2Within(GetPrivateValue<Vector2>(player, "finalDestination"), forwardApproach, 0.0001f,
            "Drawing-to-Music movement command");
        Assert.That(GetPrivateStaticField<DoorTriggerNavigation>(typeof(DoorTriggerNavigation), "pendingApproachTrigger"),
            Is.SameAs(forward));
        Assert.That(GetPrivateStaticField<AudioSource>(typeof(DoorTriggerNavigation), "activeNavigationAudioSource"), Is.Null);

        for (int frame = 0; frame < 120 && navigation.CurrentRoom == DrawingRoom && player.HasDestination; frame++)
        {
            InvokePrivateMethod(player, "MoveTowardDestination");
            yield return null;
        }
        yield return WaitForSettledLayout();

        Assert.That(navigation.CurrentRoom, Is.EqualTo(MusicRoom));
        Assert.That(RequireOnlyActiveRoom(MusicRoom), Is.SameAs(musicRoomContent));
        Assert.That(cameraManager.cameraBackground.texture, Is.SameAs(musicBackground));
        Assert.That(GetPrivateField<RoomContentGroup>(cameraManager, "activeRoomContentGroup"),
            Is.SameAs(musicRoomContent));
        Assert.That(GetPrivateField<RectTransform>(cameraManager, "activeRoomStage"),
            Is.SameAs(musicRoomContent.transform));
        Assert.That(drawingRoomContent.gameObject.activeSelf, Is.False);
        Assert.That(musicRoomContent.gameObject.activeSelf, Is.True);
        Assert.That(musicRoomView.IsVisible, Is.True);
        Assert.That(forward.gameObject.activeInHierarchy, Is.False);
        Assert.That(reverse.gameObject.activeInHierarchy, Is.True);
        Assert.That(forwardPassage.isActiveAndEnabled, Is.False);
        Assert.That(reversePassage.isActiveAndEnabled, Is.True);
        Assert.That(DoorTriggerNavigation.HoveredTrigger, Is.Not.SameAs(forward));
        if (DoorTriggerNavigation.HoveredTrigger != null)
        {
            DoorTriggerNavigation.HoveredTrigger.OnPointerExit(null);
        }
        Assert.That(DoorTriggerNavigation.HoveredTrigger, Is.Null);
        Assert.That(GetPrivateStaticField<object>(typeof(NavigationCursorController), "doorHoverOwner"), Is.Null);
        Assert.That(passagePromptText.gameObject.activeSelf, Is.False);
        Assert.That(player.HasDestination, Is.False);
        Assert.That(GetPrivateStaticField<DoorTriggerNavigation>(typeof(DoorTriggerNavigation), "pendingApproachTrigger"),
            Is.Null);
        Assert.That(GetPrivateField<PointClickPlayerMovement>(forward, "pendingApproachPlayer"), Is.Null);
        Assert.That(arrivedPositions, Has.Count.EqualTo(1));
        Assert.That(movementStoppedPositions, Has.Count.EqualTo(1));
        Assert.That(roomChangedPositions, Has.Count.EqualTo(1));
        AssertVector2Within(arrivedPositions[0], forwardApproach, 0.0001f, "Drawing-to-Music movement arrival");
        AssertVector2Within(movementStoppedPositions[0], forwardApproach, 0.0001f, "Drawing-to-Music movement stop");
        AssertVector2Within(roomChangedPositions[0], forwardApproach, 0.0001f, "Drawing-to-Music pre-warp event");
        Vector2 forwardArrival = player.LogicalPosition;
        AssertFinite(forwardArrival, "Drawing-to-Music legacy arrival");
        GameAudioSourceVolume characterizedBinding = passageAudioSource.GetComponent<GameAudioSourceVolume>();
        Assert.That(characterizedBinding, Is.Not.Null);
        Assert.That(passageAudioSource.GetComponents<GameAudioSourceVolume>(), Has.Length.EqualTo(1));

        InvokePrivateStaticMethod(typeof(DoorTriggerNavigation), "StopCurrentNavigationSound");
        Assert.That(GetPrivateStaticField<AudioSource>(typeof(DoorTriggerNavigation), "activeNavigationAudioSource"),
            Is.Null);
        InvokePrivateMethod(reverse, "ResolvePlayerReference");
        Assert.That(GetPrivateField<RoomNavigationManager>(reverse, "navigationManager"), Is.SameAs(navigation));
        Assert.That(GetPrivateField<Transform>(reverse, "player"), Is.SameAs(player.transform));
        Assert.That(GetPrivateField<AudioSource>(reverse, "doorOpenAudioSource"), Is.SameAs(passageAudioSource));
        Assert.That(GetPrivateField<DoorOpenSoundCatalog>(reverse, "doorOpenSoundCatalog"), Is.SameAs(passageDoorCatalog));
        Assert.That(GetPrivateField<DoorOpenSoundCatalog>(reverse, "stairwaySoundCatalog"), Is.Null);
        Assert.That(GetPrivateField<CanonicalPassage>(reverse, "canonicalPassage"), Is.Null);
        Assert.That(
            TryWarpToCharacterizedFarStart(player, reverse, new Vector2(10f, -6f), out float reverseStartDistance),
            Is.True);
        Vector2 reverseStart = player.LogicalPosition;
        Assert.That(InvokePrivateResult<bool>(reverse, "IsPlayerCloseEnough"), Is.False);
        Assert.That(reverseStartDistance, Is.GreaterThan(GetPrivateValue<float>(reverse, "maxPlayerScreenDistance")));
        Assert.That(TryInvokeApproachDestination(reverse, player, true, out Vector2 reverseApproach), Is.True);
        AssertFinite(reverseApproach, "Music-to-Drawing far approach");
        AssertApproachWithinActivationDistance(reverse, player, reverseApproach, "Music-to-Drawing far approach");

        reverse.OnPointerEnter(null);
        Assert.That(DoorTriggerNavigation.HoveredTrigger, Is.SameAs(reverse));
        Assert.That(GetPrivateStaticField<object>(typeof(NavigationCursorController), "doorHoverOwner"),
            Is.SameAs(reverse));
        Assert.That(passagePromptText.gameObject.activeSelf, Is.True);
        Assert.That(passagePromptText.text, Is.EqualTo("Open Door"));
        SetPrivateField(reverse, "lastPointerActivationFrame", -1);
        reverse.ActivateDoor();
        Assert.That(navigation.CurrentRoom, Is.EqualTo(MusicRoom));
        Assert.That(player.HasDestination, Is.True);
        AssertVector2Within(GetPrivateValue<Vector2>(player, "finalDestination"), reverseApproach, 0.0001f,
            "Music-to-Drawing movement command");

        for (int frame = 0; frame < 120 && navigation.CurrentRoom == MusicRoom && player.HasDestination; frame++)
        {
            InvokePrivateMethod(player, "MoveTowardDestination");
            yield return null;
        }
        yield return WaitForSettledLayout();

        Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom));
        Assert.That(RequireOnlyActiveRoom(DrawingRoom), Is.SameAs(drawingRoomContent));
        Assert.That(cameraManager.cameraBackground.texture, Is.SameAs(drawingBackground));
        Assert.That(GetPrivateField<RoomContentGroup>(cameraManager, "activeRoomContentGroup"),
            Is.SameAs(drawingRoomContent));
        Assert.That(GetPrivateField<RectTransform>(cameraManager, "activeRoomStage"),
            Is.SameAs(drawingRoomContent.transform));
        Assert.That(drawingRoomContent.gameObject.activeSelf, Is.True);
        Assert.That(musicRoomContent.gameObject.activeSelf, Is.False);
        Assert.That(musicRoomView.IsVisible, Is.False);
        Assert.That(forward.gameObject.activeInHierarchy, Is.True);
        Assert.That(reverse.gameObject.activeInHierarchy, Is.False);
        Assert.That(forwardPassage.isActiveAndEnabled, Is.True);
        Assert.That(reversePassage.isActiveAndEnabled, Is.False);
        Assert.That(DoorTriggerNavigation.HoveredTrigger, Is.Not.SameAs(reverse));
        if (DoorTriggerNavigation.HoveredTrigger != null)
        {
            DoorTriggerNavigation.HoveredTrigger.OnPointerExit(null);
        }
        Assert.That(DoorTriggerNavigation.HoveredTrigger, Is.Null);
        Assert.That(GetPrivateStaticField<object>(typeof(NavigationCursorController), "doorHoverOwner"), Is.Null);
        Assert.That(passagePromptText.gameObject.activeSelf, Is.False);
        Assert.That(GetPrivateStaticField<DoorTriggerNavigation>(typeof(DoorTriggerNavigation), "pendingApproachTrigger"),
            Is.Null);
        Assert.That(GetPrivateField<PointClickPlayerMovement>(reverse, "pendingApproachPlayer"), Is.Null);
        Assert.That(arrivedPositions, Has.Count.EqualTo(2));
        Assert.That(movementStoppedPositions, Has.Count.EqualTo(2));
        Assert.That(roomChangedPositions, Has.Count.EqualTo(2));
        AssertVector2Within(arrivedPositions[1], reverseApproach, 0.0001f, "Music-to-Drawing movement arrival");
        AssertVector2Within(movementStoppedPositions[1], reverseApproach, 0.0001f, "Music-to-Drawing movement stop");
        AssertVector2Within(roomChangedPositions[1], reverseApproach, 0.0001f, "Music-to-Drawing pre-warp event");
        Vector2 reverseArrival = player.LogicalPosition;
        AssertFinite(reverseArrival, "Music-to-Drawing legacy arrival");
        Assert.That(passageAudioSource.GetComponent<GameAudioSourceVolume>(), Is.SameAs(characterizedBinding));

        int arrivalCountBeforeNear = arrivedPositions.Count;
        int stopCountBeforeNear = movementStoppedPositions.Count;
        InvokePrivateStaticMethod(typeof(DoorTriggerNavigation), "StopCurrentNavigationSound");
        Assert.That(InvokePrivateResult<bool>(forward, "IsPlayerCloseEnough"), Is.True);
        Vector2 nearForwardSource = player.LogicalPosition;
        SetPrivateField(forward, "lastPointerActivationFrame", -1);
        forward.ActivateDoor();
        yield return WaitForSettledLayout();
        Assert.That(navigation.CurrentRoom, Is.EqualTo(MusicRoom));
        Assert.That(RequireOnlyActiveRoom(MusicRoom), Is.SameAs(musicRoomContent));
        Assert.That(cameraManager.cameraBackground.texture, Is.SameAs(musicBackground));
        Assert.That(GetPrivateField<RoomContentGroup>(cameraManager, "activeRoomContentGroup"),
            Is.SameAs(musicRoomContent));
        Assert.That(GetPrivateField<RectTransform>(cameraManager, "activeRoomStage"),
            Is.SameAs(musicRoomContent.transform));
        Assert.That(drawingRoomContent.gameObject.activeSelf, Is.False);
        Assert.That(musicRoomContent.gameObject.activeSelf, Is.True);
        Assert.That(musicRoomView.IsVisible, Is.True);
        Assert.That(forward.gameObject.activeInHierarchy, Is.False);
        Assert.That(reverse.gameObject.activeInHierarchy, Is.True);
        Assert.That(forwardPassage.isActiveAndEnabled, Is.False);
        Assert.That(reversePassage.isActiveAndEnabled, Is.True);
        Assert.That(player.HasDestination, Is.False);
        Assert.That(arrivedPositions, Has.Count.EqualTo(arrivalCountBeforeNear));
        Assert.That(movementStoppedPositions, Has.Count.EqualTo(stopCountBeforeNear));
        Assert.That(roomChangedPositions, Has.Count.EqualTo(3));
        AssertVector2Within(roomChangedPositions[2], nearForwardSource, 0.0001f, "Drawing-to-Music near pre-warp event");
        Vector2 nearForwardArrival = player.LogicalPosition;
        AssertFinite(nearForwardArrival, "Drawing-to-Music near arrival");

        InvokePrivateStaticMethod(typeof(DoorTriggerNavigation), "StopCurrentNavigationSound");
        Assert.That(InvokePrivateResult<bool>(reverse, "IsPlayerCloseEnough"), Is.True);
        Vector2 nearReverseSource = player.LogicalPosition;
        SetPrivateField(reverse, "lastPointerActivationFrame", -1);
        reverse.ActivateDoor();
        yield return WaitForSettledLayout();
        Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom));
        Assert.That(RequireOnlyActiveRoom(DrawingRoom), Is.SameAs(drawingRoomContent));
        Assert.That(cameraManager.cameraBackground.texture, Is.SameAs(drawingBackground));
        Assert.That(GetPrivateField<RoomContentGroup>(cameraManager, "activeRoomContentGroup"),
            Is.SameAs(drawingRoomContent));
        Assert.That(GetPrivateField<RectTransform>(cameraManager, "activeRoomStage"),
            Is.SameAs(drawingRoomContent.transform));
        Assert.That(drawingRoomContent.gameObject.activeSelf, Is.True);
        Assert.That(musicRoomContent.gameObject.activeSelf, Is.False);
        Assert.That(musicRoomView.IsVisible, Is.False);
        Assert.That(forward.gameObject.activeInHierarchy, Is.True);
        Assert.That(reverse.gameObject.activeInHierarchy, Is.False);
        Assert.That(forwardPassage.isActiveAndEnabled, Is.True);
        Assert.That(reversePassage.isActiveAndEnabled, Is.False);
        Assert.That(player.HasDestination, Is.False);
        Assert.That(arrivedPositions, Has.Count.EqualTo(arrivalCountBeforeNear));
        Assert.That(movementStoppedPositions, Has.Count.EqualTo(stopCountBeforeNear));
        Assert.That(roomChangedPositions, Has.Count.EqualTo(4));
        AssertVector2Within(roomChangedPositions[3], nearReverseSource, 0.0001f, "Music-to-Drawing near pre-warp event");
        Vector2 nearReverseArrival = player.LogicalPosition;
        AssertFinite(nearReverseArrival, "Music-to-Drawing near arrival");

        AssertVector2Within(forwardStart, new Vector2(5.267176f, -2.104616f), 0.005f,
            "1366x768 Drawing-to-Music far start");
        AssertVector2Within(forwardApproach, new Vector2(-7.10601f, -1.508934f), 0.01f,
            "1366x768 Drawing-to-Music far approach");
        AssertVector2Within(forwardArrival, new Vector2(-7.737432f, -3.180156f), 0.01f,
            "1366x768 Drawing-to-Music far arrival");
        AssertVector2Within(nearForwardArrival, forwardArrival, 0.001f,
            "1366x768 Drawing-to-Music near arrival");
        AssertVector2Within(reverseStart, new Vector2(10f, -6f), 0.001f,
            "1366x768 Music-to-Drawing far start");
        AssertVector2Within(reverseApproach, forwardArrival, 0.001f,
            "1366x768 Music-to-Drawing far approach reciprocity");
        AssertVector2Within(reverseArrival, forwardApproach, 0.001f,
            "1366x768 Music-to-Drawing far arrival reciprocity");
        AssertVector2Within(nearReverseArrival, reverseArrival, 0.001f,
            "1366x768 Music-to-Drawing near arrival");

        Assert.That(orderedEvents, Is.EqualTo(new[]
        {
            $"arrived:{DrawingRoom}:audio-idle",
            $"movement-stopped:{DrawingRoom}:audio-idle",
            $"room-changed:{MusicRoom}:audio-started",
            $"arrived:{MusicRoom}:audio-idle",
            $"movement-stopped:{MusicRoom}:audio-idle",
            $"room-changed:{DrawingRoom}:audio-started",
            $"room-changed:{MusicRoom}:audio-started",
            $"room-changed:{DrawingRoom}:audio-started"
        }));
        Assert.That(GetPrivateStaticField<DoorTriggerNavigation>(typeof(DoorTriggerNavigation), "pendingApproachTrigger"), Is.Null);
        Assert.That(GetPrivateField<PointClickPlayerMovement>(forward, "pendingApproachPlayer"), Is.Null);
        Assert.That(GetPrivateField<PointClickPlayerMovement>(reverse, "pendingApproachPlayer"), Is.Null);

        player.ArrivedAtDestination -= recordArrival;
        player.MovementStopped -= recordMovementStopped;
        navigation.OnCurrentRoomChanged.RemoveListener(recordRoomChanged);
        InvokePrivateStaticMethod(typeof(DoorTriggerNavigation), "StopCurrentNavigationSound");

        Vector2Int[] renderedSizes =
        {
            new Vector2Int(1366, 768),
            new Vector2Int(1440, 1080),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1080)
        };
        Vector2[] expectedForwardApproaches =
        {
            new Vector2(-7.10601f, -1.508934f),
            new Vector2(-6.152483f, -1.306456f),
            new Vector2(-7.104277f, -1.508566f),
            new Vector2(-8.188315f, -1.742414f)
        };
        Vector2[] expectedForwardArrivals =
        {
            new Vector2(-7.737432f, -3.180156f),
            new Vector2(-6.699176f, -2.753424f),
            new Vector2(-7.735544f, -3.179381f),
            new Vector2(-8.932235f, -3.671232f)
        };
        Vector2[] expectedReverseApproaches =
        {
            new Vector2(-7.737432f, -3.180156f),
            new Vector2(-6.699176f, -2.753424f),
            new Vector2(-7.735544f, -3.179381f),
            new Vector2(-8.932235f, -3.671232f)
        };
        Vector2[] expectedReverseArrivals =
        {
            new Vector2(-7.10601f, -1.508934f),
            new Vector2(-6.152483f, -1.306456f),
            new Vector2(-7.104277f, -1.508566f),
            new Vector2(-8.188308f, -1.741942f)
        };
        List<string> aspectEvidence = new List<string>();

        for (int sizeIndex = 0; sizeIndex < renderedSizes.Length; sizeIndex++)
        {
            Vector2Int renderedSize = renderedSizes[sizeIndex];
            yield return SetAndWaitForRenderedGameViewResolution((uint)renderedSize.x, (uint)renderedSize.y);
            cameraManager.ResetRoomLookForPreview();
            yield return WaitForSettledLayout();
            Physics2D.SyncTransforms();

            Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom));
            Assert.That(player.TryWarpTo(new Vector2(0f, -2f), true), Is.True);
            Vector2 aspectForwardStart = player.LogicalPosition;
            Assert.That(TryInvokeApproachDestination(forward, player, true, out Vector2 aspectForwardApproach), Is.True);
            Assert.That(TryGetTriggerScreenBounds(forward, out Vector2 forwardMin, out Vector2 forwardMax), Is.True);
            Vector2 forwardLeftClick = BuildPreferredTriggerClick(forwardMin, forwardMax, 0.15f);
            Vector2 forwardCenterClick = BuildPreferredTriggerClick(forwardMin, forwardMax, 0.5f);
            Vector2 forwardRightClick = BuildPreferredTriggerClick(forwardMin, forwardMax, 0.85f);
            Assert.That(TryInvokeApproachDestination(
                forward, player, true, out Vector2 aspectForwardLeft, forwardLeftClick), Is.True);
            Assert.That(TryInvokeApproachDestination(
                forward, player, true, out Vector2 aspectForwardCenter, forwardCenterClick), Is.True);
            Assert.That(TryInvokeApproachDestination(
                forward, player, true, out Vector2 aspectForwardRight, forwardRightClick), Is.True);
            AssertFinite(aspectForwardApproach, "aspect Drawing-to-Music null-click approach");
            AssertFinite(aspectForwardLeft, "aspect Drawing-to-Music left-click approach");
            AssertFinite(aspectForwardCenter, "aspect Drawing-to-Music center-click approach");
            AssertFinite(aspectForwardRight, "aspect Drawing-to-Music right-click approach");
            AssertApproachWithinActivationDistance(forward, player, aspectForwardApproach, "aspect Drawing-to-Music approach");
            AssertApproachWithinActivationDistance(forward, player, aspectForwardLeft, "aspect Drawing-to-Music left-click approach");
            AssertApproachWithinActivationDistance(forward, player, aspectForwardCenter, "aspect Drawing-to-Music center-click approach");
            AssertApproachWithinActivationDistance(forward, player, aspectForwardRight, "aspect Drawing-to-Music right-click approach");
            Assert.That(player.TryWarpTo(aspectForwardApproach, false), Is.True);
            Assert.That(navigation.MoveThroughInspectorDoor(
                forward.SourceRoom,
                forward.DoorName,
                forward.DestinationRoom,
                true), Is.True);
            yield return WaitForSettledLayout();
            Vector2 aspectForwardArrival = player.LogicalPosition;
            Assert.That(navigation.CurrentRoom, Is.EqualTo(MusicRoom));
            Assert.That(GetPrivateField<RoomContentGroup>(cameraManager, "activeRoomContentGroup"),
                Is.SameAs(musicRoomContent));
            Assert.That(GetPrivateField<RectTransform>(cameraManager, "activeRoomStage"),
                Is.SameAs(musicRoomContent.transform));

            Assert.That(player.TryWarpTo(new Vector2(0f, -2f), true), Is.True);
            Vector2 aspectReverseStart = player.LogicalPosition;
            Assert.That(TryInvokeApproachDestination(reverse, player, true, out Vector2 aspectReverseApproach), Is.True);
            Assert.That(TryGetTriggerScreenBounds(reverse, out Vector2 reverseMin, out Vector2 reverseMax), Is.True);
            Vector2 reverseLeftClick = BuildPreferredTriggerClick(reverseMin, reverseMax, 0.15f);
            Vector2 reverseCenterClick = BuildPreferredTriggerClick(reverseMin, reverseMax, 0.5f);
            Vector2 reverseRightClick = BuildPreferredTriggerClick(reverseMin, reverseMax, 0.85f);
            Assert.That(TryInvokeApproachDestination(
                reverse, player, true, out Vector2 aspectReverseLeft, reverseLeftClick), Is.True);
            Assert.That(TryInvokeApproachDestination(
                reverse, player, true, out Vector2 aspectReverseCenter, reverseCenterClick), Is.True);
            Assert.That(TryInvokeApproachDestination(
                reverse, player, true, out Vector2 aspectReverseRight, reverseRightClick), Is.True);
            AssertFinite(aspectReverseApproach, "aspect Music-to-Drawing null-click approach");
            AssertFinite(aspectReverseLeft, "aspect Music-to-Drawing left-click approach");
            AssertFinite(aspectReverseCenter, "aspect Music-to-Drawing center-click approach");
            AssertFinite(aspectReverseRight, "aspect Music-to-Drawing right-click approach");
            AssertApproachWithinActivationDistance(reverse, player, aspectReverseApproach, "aspect Music-to-Drawing approach");
            AssertApproachWithinActivationDistance(reverse, player, aspectReverseLeft, "aspect Music-to-Drawing left-click approach");
            AssertApproachWithinActivationDistance(reverse, player, aspectReverseCenter, "aspect Music-to-Drawing center-click approach");
            AssertApproachWithinActivationDistance(reverse, player, aspectReverseRight, "aspect Music-to-Drawing right-click approach");
            Assert.That(player.TryWarpTo(aspectReverseApproach, false), Is.True);
            Assert.That(navigation.MoveThroughInspectorDoor(
                reverse.SourceRoom,
                reverse.DoorName,
                reverse.DestinationRoom,
                true), Is.True);
            yield return WaitForSettledLayout();
            Vector2 aspectReverseArrival = player.LogicalPosition;
            Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom));
            Assert.That(GetPrivateField<RoomContentGroup>(cameraManager, "activeRoomContentGroup"),
                Is.SameAs(drawingRoomContent));
            Assert.That(GetPrivateField<RectTransform>(cameraManager, "activeRoomStage"),
                Is.SameAs(drawingRoomContent.transform));

            aspectEvidence.Add(
                $"{renderedSize.x}x{renderedSize.y}:" +
                $"forwardStart={FormatVector(aspectForwardStart)} " +
                $"forwardApproach={FormatVector(aspectForwardApproach)} " +
                $"forwardArrival={FormatVector(aspectForwardArrival)} " +
                $"reverseStart={FormatVector(aspectReverseStart)} " +
                $"reverseApproach={FormatVector(aspectReverseApproach)} " +
                $"reverseArrival={FormatVector(aspectReverseArrival)}");
            Debug.Log($"[DrawingMusicPassageAspect] {aspectEvidence[aspectEvidence.Count - 1]}");

            float viewportEnvelopeTolerance = sizeIndex == 0 ? 0.15f : sizeIndex == 3 ? 0.2f : 0.05f;
            AssertVector2Within(aspectForwardStart, new Vector2(0f, -2f), 0.001f,
                "aspect Drawing-to-Music invariant start");
            AssertVector2Within(aspectForwardApproach, expectedForwardApproaches[sizeIndex],
                viewportEnvelopeTolerance, "aspect Drawing-to-Music null-click approach");
            AssertVector2Within(aspectForwardArrival, expectedForwardArrivals[sizeIndex],
                viewportEnvelopeTolerance, "aspect Drawing-to-Music arrival");
            AssertVector2Within(aspectReverseStart, new Vector2(0f, -2f), 0.001f,
                "aspect Music-to-Drawing invariant start");
            AssertVector2Within(aspectReverseApproach, expectedReverseApproaches[sizeIndex],
                viewportEnvelopeTolerance, "aspect Music-to-Drawing null-click approach");
            AssertVector2Within(aspectReverseArrival, expectedReverseArrivals[sizeIndex],
                viewportEnvelopeTolerance, "aspect Music-to-Drawing arrival");
            AssertVector2Within(aspectReverseApproach, aspectForwardArrival,
                viewportEnvelopeTolerance, "aspect Music-side reciprocity");
            AssertVector2Within(aspectReverseArrival, aspectForwardApproach,
                viewportEnvelopeTolerance, "aspect Drawing-side reciprocity");
        }

        Debug.Log(
            $"[DrawingMusicPassageCharacterization] " +
            $"forwardStart={FormatVector(forwardStart)} forwardStartDistance={forwardStartDistance:0.###} " +
            $"forwardApproach={FormatVector(forwardApproach)} forwardArrival={FormatVector(forwardArrival)} " +
            $"nearForwardArrival={FormatVector(nearForwardArrival)} " +
            $"reverseStart={FormatVector(reverseStart)} reverseStartDistance={reverseStartDistance:0.###} " +
            $"reverseApproach={FormatVector(reverseApproach)} reverseArrival={FormatVector(reverseArrival)} " +
            $"nearReverseArrival={FormatVector(nearReverseArrival)} " +
            $"aspects={string.Join(" | ", aspectEvidence)}");

        InvokePrivateStaticMethod(typeof(DoorTriggerNavigation), "StopCurrentNavigationSound");
        if (DoorTriggerNavigation.HoveredTrigger != null)
        {
            DoorTriggerNavigation.HoveredTrigger.OnPointerExit(null);
        }
        Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom));
        Assert.That(GetPrivateField<CanonicalPassage>(forward, "canonicalPassage"), Is.Null);
        Assert.That(GetPrivateField<CanonicalPassage>(reverse, "canonicalPassage"), Is.Null);
        AssertDrawingMusicPassagesRemainPassive(
            forward,
            reverse,
            forwardPassage,
            reversePassage,
            drawingRoomView,
            musicRoomView,
            true,
            false);
        Assert.That(GetPrivateStaticField<DoorTriggerNavigation>(typeof(DoorTriggerNavigation), "pendingApproachTrigger"),
            Is.Null);
        Assert.That(GetPrivateField<PointClickPlayerMovement>(forward, "pendingApproachPlayer"), Is.Null);
        Assert.That(GetPrivateField<PointClickPlayerMovement>(reverse, "pendingApproachPlayer"), Is.Null);
        Assert.That(GetPrivateStaticField<AudioSource>(typeof(DoorTriggerNavigation), "activeNavigationAudioSource"),
            Is.Null);
        Assert.That(passageAudioSource.isPlaying, Is.False);
        Assert.That(DoorTriggerNavigation.HoveredTrigger, Is.Null);
        Assert.That(GetPrivateStaticField<object>(typeof(NavigationCursorController), "doorHoverOwner"), Is.Null);
        Assert.That(passagePromptText.gameObject.activeSelf, Is.False);
        Assert.That(passageAudioSource.GetComponents<GameAudioSourceVolume>(), Has.Length.EqualTo(1));
        Assert.That(passageAudioSource.GetComponent<GameAudioSourceVolume>(), Is.SameAs(characterizedBinding));
        SetPrivateField(player, "moveSpeed", originalMoveSpeed);
        player.SetInputEnabled(originalInputEnabled);
        cameraManager.panRoomWithMouseEdges = originalPanRoomWithMouseEdges;
        cameraManager.zoomRoomWithMouseWheel = originalZoomRoomWithMouseWheel;
    }

    [UnityTest]
    public IEnumerator EntranceDrawingPassageSelectionAndArrivalsAreCharacterizedAcrossRenderedAspects()
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

        RoomNavigationManager navigation = RequireExactlyOneInActiveScene<RoomNavigationManager>();
        INavigationService facade = navigation;
        GameObject playerObject = GameObject.Find("Player");
        Assert.That(playerObject, Is.Not.Null);
        PointClickPlayerMovement player = playerObject.GetComponent<PointClickPlayerMovement>();
        Assert.That(player, Is.Not.Null);
        DoorTriggerNavigation outbound = RequireSceneObject<DoorTriggerNavigation>("DoorTrigger_GEH_DrawingRoom");
        DoorTriggerNavigation reverse = RequireSceneObject<DoorTriggerNavigation>("DoorTrigger_DrawingRoom_GEH");
        CanonicalPassage forwardPassage = outbound.GetComponent<CanonicalPassage>();
        CanonicalPassage reversePassage = reverse.GetComponent<CanonicalPassage>();
        Assert.That(forwardPassage, Is.Not.Null);
        Assert.That(reversePassage, Is.Not.Null);
        AudioSource passageAudioSource = FindInActiveScene<AudioSource>()
            .Single(item => item.gameObject.name == "Audio_DoorOpen");
        DoorOpenSoundCatalog passageDoorCatalog = GetPrivateField<DoorOpenSoundCatalog>(outbound, "doorOpenSoundCatalog");
        CameraManager cameraManager = RequireExactlyOneInActiveScene<CameraManager>();
        bool originalInputEnabled = player.InputEnabled;
        bool originalPanRoomWithMouseEdges = cameraManager.panRoomWithMouseEdges;
        bool originalZoomRoomWithMouseWheel = cameraManager.zoomRoomWithMouseWheel;
        player.SetInputEnabled(true);
        cameraManager.panRoomWithMouseEdges = false;
        cameraManager.zoomRoomWithMouseWheel = false;
        cameraManager.ResetRoomLookForPreview();
        yield return WaitForSettledLayout();
        AssertDoorTriggerCompatibilityBindings(
            outbound,
            reverse,
            navigation,
            player.transform,
            passageAudioSource,
            passageDoorCatalog);

        Vector2Int[] renderedSizes =
        {
            new Vector2Int(1366, 768),
            new Vector2Int(1440, 1080),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1080)
        };
        Vector2[] expectedForwardApproaches =
        {
            new Vector2(-7.703568f, -2.000136f),
            new Vector2(-6.655989f, -1.737491f),
            new Vector2(-7.701689f, -1.999648f),
            new Vector2(-8.74597f, -2.293164f)
        };
        Vector2[] expectedForwardArrivals =
        {
            new Vector2(5.267176f, -2.104616f),
            new Vector2(4.589555f, -1.67502f),
            new Vector2(5.265821f, -2.104132f),
            new Vector2(6.093467f, -2.228136f)
        };
        Vector2[] expectedReverseApproaches =
        {
            new Vector2(5.231221f, -2.002132f),
            new Vector2(4.544261f, -1.73348f),
            new Vector2(5.247262f, -2.001651f),
            new Vector2(6.059016f, -2.311308f)
        };
        Vector2[] expectedReverseArrivals =
        {
            new Vector2(-7.703568f, -2.000136f),
            new Vector2(-6.655989f, -1.737491f),
            new Vector2(-7.685683f, -2.006279f),
            new Vector2(-8.879283f, -2.314737f)
        };
        for (int sizeIndex = 0; sizeIndex < renderedSizes.Length; sizeIndex++)
        {
            Vector2Int renderedSize = renderedSizes[sizeIndex];
            float viewportEnvelopeTolerance = sizeIndex == 0
                ? 0.15f
                : sizeIndex == renderedSizes.Length - 1 ? 0.2f : 0.05f;
            yield return SetAndWaitForRenderedGameViewResolution(
                (uint)renderedSize.x,
                (uint)renderedSize.y);
            cameraManager.ResetRoomLookForPreview();
            yield return WaitForSettledLayout();
            Physics2D.SyncTransforms();

            Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom));
            Vector2 invariantStart = new Vector2(0f, -4f);
            Assert.That(player.TryWarpTo(invariantStart, false), Is.True,
                "The Entrance aspect probe requires one invariant logical start.");
            InvokePrivateMethod(outbound, "ResolvePlayerReference");
            float forwardStartDistance = InvokePrivateResult<float>(outbound, "GetPlayerScreenDistanceToTrigger");
            Vector2 forwardStart = player.LogicalPosition;
            Assert.That(InvokePrivateResult<bool>(outbound, "IsPlayerCloseEnough"), Is.False);
            Assert.That(forwardStartDistance, Is.GreaterThan(GetPrivateValue<float>(outbound, "maxPlayerScreenDistance")));
            Assert.That(
                TryInvokeApproachDestination(outbound, player, true, out Vector2 forwardApproach),
                Is.True);
            Assert.That(TryGetTriggerScreenBounds(outbound, out Vector2 forwardMin, out Vector2 forwardMax), Is.True);
            Vector2 forwardLeftClick = BuildPreferredTriggerClick(forwardMin, forwardMax, 0.15f);
            Vector2 forwardCenterClick = BuildPreferredTriggerClick(forwardMin, forwardMax, 0.5f);
            Vector2 forwardRightClick = BuildPreferredTriggerClick(forwardMin, forwardMax, 0.85f);
            Assert.That(TryInvokeApproachDestination(outbound, player, true, out Vector2 forwardLeft, forwardLeftClick), Is.True);
            Assert.That(TryInvokeApproachDestination(outbound, player, true, out Vector2 forwardCenter, forwardCenterClick), Is.True);
            Assert.That(TryInvokeApproachDestination(outbound, player, true, out Vector2 forwardRight, forwardRightClick), Is.True);
            AssertFinite(forwardApproach, "forward null-click approach");
            AssertFinite(forwardLeft, "forward left-click approach");
            AssertFinite(forwardCenter, "forward center-click approach");
            AssertFinite(forwardRight, "forward right-click approach");
            Debug.Log(
                $"[EntranceDrawingPassageAspectForward] {renderedSize.x}x{renderedSize.y} " +
                $"start={FormatVector(forwardStart)} approach={FormatVector(forwardApproach)}");
            AssertVector2Within(forwardStart, invariantStart, 0.0001f, "rendered forward invariant start");
            AssertVector2Within(forwardApproach, expectedForwardApproaches[sizeIndex], viewportEnvelopeTolerance, "rendered forward null-click approach");
            AssertApproachWithinActivationDistance(outbound, player, forwardApproach, "rendered forward null-click approach");
            AssertApproachWithinActivationDistance(outbound, player, forwardLeft, "rendered forward left-click approach");
            AssertApproachWithinActivationDistance(outbound, player, forwardCenter, "rendered forward center-click approach");
            AssertApproachWithinActivationDistance(outbound, player, forwardRight, "rendered forward right-click approach");

            Assert.That(
                player.TryWarpToExact(forwardPassage.ApproachAnchor.LogicalPosition),
                Is.True,
                $"The authored forward approach must be exactly walkable at {renderedSize.x}x{renderedSize.y}.");
            AssertApproachWithinActivationDistance(
                outbound,
                player,
                forwardPassage.ApproachAnchor.LogicalPosition,
                "rendered authored forward approach");
            Assert.That(InvokePrivateResult<bool>(outbound, "IsPlayerCloseEnough"), Is.True);
            Assert.That(player.TryWarpTo(invariantStart, false), Is.True);

            Assert.That(
                TryInvokeTraversalApproachDestination(outbound, player, out Vector2 canonicalForwardNull, null),
                Is.True);
            Assert.That(
                TryInvokeTraversalApproachDestination(outbound, player, out Vector2 canonicalForwardLeft, forwardLeftClick),
                Is.True);
            Assert.That(
                TryInvokeTraversalApproachDestination(outbound, player, out Vector2 canonicalForwardCenter, forwardCenterClick),
                Is.True);
            Assert.That(
                TryInvokeTraversalApproachDestination(outbound, player, out Vector2 canonicalForwardRight, forwardRightClick),
                Is.True);
            AssertVector2Within(canonicalForwardNull, forwardPassage.ApproachAnchor.LogicalPosition, 0.0001f, "canonical forward null-click approach");
            AssertVector2Within(canonicalForwardLeft, canonicalForwardNull, 0.0001f, "canonical forward left-click approach");
            AssertVector2Within(canonicalForwardCenter, canonicalForwardNull, 0.0001f, "canonical forward center-click approach");
            AssertVector2Within(canonicalForwardRight, canonicalForwardNull, 0.0001f, "canonical forward right-click approach");
            SetPrivateField(outbound, "lastPointerActivationFrame", -1);
            outbound.ActivateDoor();
            Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom));
            Assert.That(player.HasDestination, Is.True);
            AssertVector2Within(
                GetPrivateValue<Vector2>(player, "finalDestination"),
                forwardPassage.ApproachAnchor.LogicalPosition,
                0.0001f,
                "rendered canonical forward movement command");
            Assert.That(GetPrivateStaticField<DoorTriggerNavigation>(typeof(DoorTriggerNavigation), "pendingApproachTrigger"), Is.SameAs(outbound));
            Assert.That(GetPrivateStaticField<AudioSource>(typeof(DoorTriggerNavigation), "activeNavigationAudioSource"), Is.Null);
            InvokePrivateMethod(outbound, "CancelPendingPlayerApproach");
            Assert.That(player.TryWarpToExact(invariantStart), Is.True);
            Assert.That(player.HasDestination, Is.False);

            Assert.That(player.TryWarpTo(forwardApproach, false), Is.True);
            Assert.That(
                navigation.MoveThroughInspectorDoor(
                    outbound.SourceRoom,
                    outbound.DoorName,
                    outbound.DestinationRoom,
                    true),
                Is.True);
            yield return WaitForSettledLayout();
            Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom));
            Vector2 forwardArrival = player.LogicalPosition;
            AssertVector2Within(forwardArrival, expectedForwardArrivals[sizeIndex], viewportEnvelopeTolerance, "rendered forward arrival");

            Vector2 reverseInvariantStart = new Vector2(0f, -2f);
            Assert.That(player.TryWarpTo(reverseInvariantStart, false), Is.True,
                "The Drawing Room aspect probe requires one invariant logical start.");
            InvokePrivateMethod(reverse, "ResolvePlayerReference");
            float reverseStartDistance = InvokePrivateResult<float>(reverse, "GetPlayerScreenDistanceToTrigger");
            Vector2 reverseStart = player.LogicalPosition;
            Assert.That(InvokePrivateResult<bool>(reverse, "IsPlayerCloseEnough"), Is.False);
            Assert.That(reverseStartDistance, Is.GreaterThan(GetPrivateValue<float>(reverse, "maxPlayerScreenDistance")));
            Assert.That(
                TryInvokeApproachDestination(reverse, player, true, out Vector2 reverseApproach),
                Is.True);
            Assert.That(TryGetTriggerScreenBounds(reverse, out Vector2 reverseMin, out Vector2 reverseMax), Is.True);
            Vector2 reverseLeftClick = BuildPreferredTriggerClick(reverseMin, reverseMax, 0.15f);
            Vector2 reverseCenterClick = BuildPreferredTriggerClick(reverseMin, reverseMax, 0.5f);
            Vector2 reverseRightClick = BuildPreferredTriggerClick(reverseMin, reverseMax, 0.85f);
            Assert.That(TryInvokeApproachDestination(reverse, player, true, out Vector2 reverseLeft, reverseLeftClick), Is.True);
            Assert.That(TryInvokeApproachDestination(reverse, player, true, out Vector2 reverseCenter, reverseCenterClick), Is.True);
            Assert.That(TryInvokeApproachDestination(reverse, player, true, out Vector2 reverseRight, reverseRightClick), Is.True);
            AssertFinite(reverseApproach, "reverse null-click approach");
            AssertFinite(reverseLeft, "reverse left-click approach");
            AssertFinite(reverseCenter, "reverse center-click approach");
            AssertFinite(reverseRight, "reverse right-click approach");
            AssertVector2Within(reverseStart, reverseInvariantStart, 0.0001f, "rendered reverse invariant start");
            AssertVector2Within(reverseApproach, expectedReverseApproaches[sizeIndex], viewportEnvelopeTolerance, "rendered reverse null-click approach");
            AssertApproachWithinActivationDistance(reverse, player, reverseApproach, "rendered reverse null-click approach");
            AssertApproachWithinActivationDistance(reverse, player, reverseLeft, "rendered reverse left-click approach");
            AssertApproachWithinActivationDistance(reverse, player, reverseCenter, "rendered reverse center-click approach");
            AssertApproachWithinActivationDistance(reverse, player, reverseRight, "rendered reverse right-click approach");

            Assert.That(
                player.TryWarpToExact(reversePassage.ApproachAnchor.LogicalPosition),
                Is.True,
                $"The authored reverse approach must be exactly walkable at {renderedSize.x}x{renderedSize.y}.");
            AssertApproachWithinActivationDistance(
                reverse,
                player,
                reversePassage.ApproachAnchor.LogicalPosition,
                "rendered authored reverse approach");
            Assert.That(InvokePrivateResult<bool>(reverse, "IsPlayerCloseEnough"), Is.True);
            Assert.That(player.TryWarpTo(reverseInvariantStart, false), Is.True);

            Assert.That(
                TryInvokeTraversalApproachDestination(reverse, player, out Vector2 canonicalReverseNull, null),
                Is.True);
            Assert.That(
                TryInvokeTraversalApproachDestination(reverse, player, out Vector2 canonicalReverseLeft, reverseLeftClick),
                Is.True);
            Assert.That(
                TryInvokeTraversalApproachDestination(reverse, player, out Vector2 canonicalReverseCenter, reverseCenterClick),
                Is.True);
            Assert.That(
                TryInvokeTraversalApproachDestination(reverse, player, out Vector2 canonicalReverseRight, reverseRightClick),
                Is.True);
            AssertVector2Within(canonicalReverseNull, reversePassage.ApproachAnchor.LogicalPosition, 0.0001f, "canonical reverse null-click approach");
            AssertVector2Within(canonicalReverseLeft, canonicalReverseNull, 0.0001f, "canonical reverse left-click approach");
            AssertVector2Within(canonicalReverseCenter, canonicalReverseNull, 0.0001f, "canonical reverse center-click approach");
            AssertVector2Within(canonicalReverseRight, canonicalReverseNull, 0.0001f, "canonical reverse right-click approach");
            SetPrivateField(reverse, "lastPointerActivationFrame", -1);
            reverse.ActivateDoor();
            Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom));
            Assert.That(player.HasDestination, Is.True);
            AssertVector2Within(
                GetPrivateValue<Vector2>(player, "finalDestination"),
                reversePassage.ApproachAnchor.LogicalPosition,
                0.0001f,
                "rendered canonical reverse movement command");
            Assert.That(GetPrivateStaticField<DoorTriggerNavigation>(typeof(DoorTriggerNavigation), "pendingApproachTrigger"), Is.SameAs(reverse));
            Assert.That(GetPrivateStaticField<AudioSource>(typeof(DoorTriggerNavigation), "activeNavigationAudioSource"), Is.Null);
            InvokePrivateMethod(reverse, "CancelPendingPlayerApproach");
            Assert.That(player.TryWarpToExact(reverseInvariantStart), Is.True);
            Assert.That(player.HasDestination, Is.False);

            Assert.That(player.TryWarpTo(reverseApproach, false), Is.True);
            Assert.That(
                navigation.MoveThroughInspectorDoor(
                    reverse.SourceRoom,
                    reverse.DoorName,
                    reverse.DestinationRoom,
                    true),
                Is.True);
            yield return WaitForSettledLayout();
            Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom));
            Vector2 reverseArrival = player.LogicalPosition;
            AssertVector2Within(reverseArrival, expectedReverseArrivals[sizeIndex], viewportEnvelopeTolerance, "rendered reverse arrival");
            Assert.That(player.HasDestination, Is.False);

            Assert.That(player.TryWarpTo(invariantStart, false), Is.True);
            Assert.That(facade.TryTraverse(forwardPassage), Is.True);
            Vector2 canonicalForwardArrival = player.LogicalPosition;
            AssertVector2Within(
                canonicalForwardArrival,
                forwardPassage.ArrivalAnchor.LogicalPosition,
                0.0001f,
                "rendered canonical forward authored arrival");
            Assert.That(player.TryWarpTo(reverseInvariantStart, false), Is.True);
            Assert.That(facade.TryTraverse(reversePassage), Is.True);
            Vector2 canonicalReverseArrival = player.LogicalPosition;
            AssertVector2Within(
                canonicalReverseArrival,
                reversePassage.ArrivalAnchor.LogicalPosition,
                0.0001f,
                "rendered canonical reverse authored arrival");
            Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom));
            Assert.That(player.HasDestination, Is.False);
            AssertDoorTriggerCompatibilityBindings(
                outbound,
                reverse,
                navigation,
                player.transform,
                passageAudioSource,
                passageDoorCatalog);

            Debug.Log(
                $"[PassageViewportCharacterization] viewport={renderedSize.x}x{renderedSize.y} " +
                $"forwardStart={FormatVector(forwardStart)} forwardStartDistance={forwardStartDistance:0.###} " +
                $"forwardNull={FormatVector(forwardApproach)} forwardLeft={FormatVector(forwardLeft)} " +
                $"forwardCenter={FormatVector(forwardCenter)} forwardRight={FormatVector(forwardRight)} " +
                $"forwardArrival={FormatVector(forwardArrival)} canonicalForwardArrival={FormatVector(canonicalForwardArrival)} " +
                $"reverseStart={FormatVector(reverseStart)} reverseStartDistance={reverseStartDistance:0.###} " +
                $"reverseNull={FormatVector(reverseApproach)} reverseLeft={FormatVector(reverseLeft)} " +
                $"reverseCenter={FormatVector(reverseCenter)} reverseRight={FormatVector(reverseRight)} " +
                $"reverseArrival={FormatVector(reverseArrival)} canonicalReverseArrival={FormatVector(canonicalReverseArrival)}");
        }

        Assert.That(navigation.CurrentRoom, Is.EqualTo(EntranceRoom));
        player.SetInputEnabled(originalInputEnabled);
        cameraManager.panRoomWithMouseEdges = originalPanRoomWithMouseEdges;
        cameraManager.zoomRoomWithMouseWheel = originalZoomRoomWithMouseWheel;
    }

    private static IEnumerator WaitForSettledLayout()
    {
        for (int frame = 0; frame < 4; frame++)
        {
            Canvas.ForceUpdateCanvases();
            yield return null;
        }
    }

    private static void AssertDoorTriggerCompatibilityBindings(
        DoorTriggerNavigation outbound,
        DoorTriggerNavigation reverse,
        RoomNavigationManager navigation,
        Transform playerTransform,
        AudioSource passageAudioSource,
        DoorOpenSoundCatalog passageDoorCatalog)
    {
        Assert.That(passageDoorCatalog, Is.Not.Null);
        Assert.That(
            AssetDatabase.GetAssetPath(passageDoorCatalog),
            Is.EqualTo("Assets/Resources/Audio/DoorOpenSoundCatalog.asset"));

        DoorTriggerNavigation[] triggers = { outbound, reverse };
        for (int i = 0; i < triggers.Length; i++)
        {
            DoorTriggerNavigation trigger = triggers[i];
            Assert.That(GetPrivateField<RoomNavigationManager>(trigger, "navigationManager"), Is.SameAs(navigation));
            Assert.That(GetPrivateField<Transform>(trigger, "player"), Is.SameAs(playerTransform));
            Assert.That(GetPrivateField<AudioSource>(trigger, "doorOpenAudioSource"), Is.SameAs(passageAudioSource));
            Assert.That(GetPrivateField<DoorOpenSoundCatalog>(trigger, "doorOpenSoundCatalog"), Is.SameAs(passageDoorCatalog));
        }
    }

    private static IEnumerator SetAndWaitForRenderedGameViewResolution(uint width, uint height)
    {
        PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
        PlayModeWindow.SetCustomRenderingResolution(width, height, CharacterizationGameViewSizeName);

        for (int frame = 0; frame < 16; frame++)
        {
            Canvas.ForceUpdateCanvases();
            yield return null;

            PlayModeWindow.GetRenderingResolution(out uint actualWidth, out uint actualHeight);
            if (actualWidth == width &&
                actualHeight == height &&
                Screen.width == (int)width &&
                Screen.height == (int)height)
            {
                yield return WaitForSettledLayout();
                yield break;
            }
        }

        PlayModeWindow.GetRenderingResolution(out uint finalWidth, out uint finalHeight);
        Assert.Fail(
            $"Rendered Game view did not settle at {width}x{height}; " +
            $"editor={finalWidth}x{finalHeight}, runtime={Screen.width}x{Screen.height}.");
    }

    private static void AssertChapterIntroGraphRemainsStable(
        ChapterIntroUI intro,
        GameObject introHost,
        Canvas introCanvas,
        RectTransform introOverlay,
        Image introFade,
        TMP_Text introTitle,
        TMP_FontAsset introFont,
        Material introFontMaterial,
        EventSystem eventSystem)
    {
        ChapterManager chapterManager = RequireExactlyOneInActiveScene<ChapterManager>();
        Chapter2Controller chapter2 = RequireExactlyOneInActiveScene<Chapter2Controller>();
        Assert.That(RequireExactlyOneInActiveScene<ChapterIntroUI>(), Is.SameAs(intro));
        Assert.That(intro.gameObject, Is.SameAs(introHost));
        Assert.That(GetPrivateField<ChapterIntroUI>(chapterManager, "introUI"), Is.SameAs(intro));
        Assert.That(GetPrivateField<ChapterIntroUI>(chapter2, "introUI"), Is.SameAs(intro));
        Assert.That(GetPrivateField<Canvas>(intro, "canvas"), Is.SameAs(introCanvas));
        Assert.That(GetPrivateField<RectTransform>(intro, "overlayRoot"), Is.SameAs(introOverlay));
        Assert.That(GetPrivateField<Image>(intro, "fadeImage"), Is.SameAs(introFade));
        Assert.That(GetPrivateField<TMP_Text>(intro, "titleText"), Is.SameAs(introTitle));
        Assert.That(intro.GetComponents<ChapterIntroUI>(), Has.Length.EqualTo(1));
        Assert.That(intro.GetComponent<Canvas>(), Is.Null);
        Assert.That(introCanvas.transform.parent, Is.Null);
        Assert.That(introCanvas.GetComponents<Component>(), Has.Length.EqualTo(4));
        Assert.That(introCanvas.transform.childCount, Is.EqualTo(1));
        Assert.That(introOverlay.parent, Is.SameAs(introCanvas.transform));
        Assert.That(introOverlay.GetComponents<Component>(), Has.Length.EqualTo(1));
        Assert.That(introOverlay.childCount, Is.EqualTo(2));
        Assert.That(introFade.transform.parent, Is.SameAs(introOverlay));
        Assert.That(introFade.GetComponents<Component>(), Has.Length.EqualTo(3));
        Assert.That(introTitle.transform.parent, Is.SameAs(introOverlay));
        Assert.That(introTitle.GetComponents<Component>(), Has.Length.EqualTo(3));
        Assert.That(introTitle.font, Is.SameAs(introFont));
        Assert.That(introTitle.fontSharedMaterial, Is.SameAs(introFontMaterial));
        Assert.That(RequireExactlyOneInActiveScene<EventSystem>(), Is.SameAs(eventSystem));
        Assert.That(FindInActiveScene<Transform>().Count(item => item.name == "Canvas_ChapterIntroOverlay"), Is.EqualTo(1));
        Assert.That(FindInActiveScene<Transform>().Count(item => item.name == "ChapterIntroUI_Runtime"), Is.EqualTo(1));
        Assert.That(FindInActiveScene<Transform>().Count(item => item.name == "Image_ChapterIntroFade"), Is.EqualTo(1));
        Assert.That(FindInActiveScene<Transform>().Count(item => item.name == "Text_ChapterIntroTitle"), Is.EqualTo(1));
    }

    private static void AssertGameTimeHudGraphRemainsStable(
        Chapter1ArrivalController arrival,
        GameTimeHUD gameTimeHud,
        ChapterClock chapterClock,
        Canvas timeCanvas,
        TMP_Text timeText,
        Shadow timeShadow,
        TMP_FontAsset timeFont,
        Material timeFontMaterial,
        EventSystem eventSystem)
    {
        Chateau.Architecture.GameRoot gameRoot = RequireExactlyOneInActiveScene<Chateau.Architecture.GameRoot>();
        List<Chateau.Architecture.ChateauBehaviour> sceneBehaviours =
            GetPrivateField<List<Chateau.Architecture.ChateauBehaviour>>(gameRoot, "sceneBehaviours");
        Assert.That(RequireExactlyOneInActiveScene<GameTimeHUD>(), Is.SameAs(gameTimeHud));
        Assert.That(arrival.GetComponent<GameTimeHUD>(), Is.Null);
        Assert.That(gameTimeHud.HasGameContext, Is.True);
        Assert.That(sceneBehaviours.Count(item => item == gameTimeHud), Is.EqualTo(1));
        Assert.That(GetPrivateField<ChapterClock>(gameTimeHud, "chapterClock"), Is.SameAs(chapterClock));
        Assert.That(GetPrivateField<Canvas>(gameTimeHud, "canvas"), Is.SameAs(timeCanvas));
        Assert.That(GetPrivateField<TMP_Text>(gameTimeHud, "clockText"), Is.SameAs(timeText));
        Assert.That(GetPrivateField<Shadow>(gameTimeHud, "clockShadow"), Is.SameAs(timeShadow));
        Assert.That(gameTimeHud.IsConfiguredFor(chapterClock), Is.True);
        Assert.That(gameTimeHud.GetComponents<GameTimeHUD>(), Has.Length.EqualTo(1));
        Assert.That(gameTimeHud.gameObject, Is.SameAs(timeCanvas.gameObject));
        Assert.That(timeCanvas.gameObject.name, Is.EqualTo("Canvas_GameTimeHUD"));
        Assert.That(timeCanvas.transform.parent, Is.SameAs(gameRoot.transform));
        Assert.That(timeCanvas.GetComponents<Component>(), Has.Length.EqualTo(5));
        Assert.That(timeCanvas.gameObject.activeSelf, Is.True);
        Assert.That(timeCanvas.enabled, Is.True);
        Assert.That(timeCanvas.transform.childCount, Is.EqualTo(1));
        Assert.That(timeText.GetComponents<Component>(), Has.Length.EqualTo(4));
        Assert.That(timeText.gameObject.activeSelf, Is.True);
        Assert.That(timeText.enabled, Is.True);
        Assert.That(timeText.transform.GetSiblingIndex(), Is.EqualTo(timeCanvas.transform.childCount - 1));
        Assert.That(timeText.text, Is.EqualTo(chapterClock.CurrentTimeLabel));
        Assert.That(timeText.GetComponent<Shadow>(), Is.SameAs(timeShadow));
        Assert.That(timeText.font, Is.SameAs(timeFont));
        Assert.That(timeText.fontSharedMaterial, Is.SameAs(timeFontMaterial));
        Assert.That(RequireExactlyOneInActiveScene<EventSystem>(), Is.SameAs(eventSystem));
        Assert.That(FindInActiveScene<Transform>().Count(item => item.name == "Canvas_GameTimeHUD"), Is.EqualTo(1));
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name == "Canvas_ChapterTimeSettings"), Is.False);
        Assert.That(FindInActiveScene<Transform>().Count(item => item.name == "Text_CurrentGameTime"), Is.EqualTo(1));
        Assert.That(FindInActiveScene<Transform>().Any(item => item.name == "Panel_TimeSettings"), Is.False);
    }

    private static void AssertClockAmbienceGraphRemainsCanonical(
        ClockTickingAmbienceController clockAmbience,
        RoomNavigationManager serializedNavigation,
        ClockTickingAmbienceCatalog serializedCatalog,
        AudioSource serializedSource,
        RoomNavigationManager expectedNavigation)
    {
        Assert.That(RequireExactlyOneInActiveScene<ClockTickingAmbienceController>(), Is.SameAs(clockAmbience));
        Assert.That(GetPrivateField<RoomNavigationManager>(clockAmbience, "navigationManager"), Is.SameAs(serializedNavigation));
        Assert.That(serializedNavigation, Is.SameAs(expectedNavigation));
        Assert.That(GetPrivateField<ClockTickingAmbienceCatalog>(clockAmbience, "catalog"), Is.SameAs(serializedCatalog));
        Assert.That(GetPrivateField<AudioSource>(clockAmbience, "audioSource"), Is.SameAs(serializedSource));
        Assert.That(clockAmbience.GetComponents<AudioSource>(), Has.Length.EqualTo(1));
        Assert.That(clockAmbience.GetComponent<AudioSource>(), Is.SameAs(serializedSource));
        Assert.That(
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                serializedCatalog,
                out string catalogGuid,
                out long catalogFileId),
            Is.True);
        Assert.That(catalogGuid, Is.EqualTo("d1c5479f74b94514cdf7a37d49f95fbe"));
        Assert.That(catalogFileId, Is.EqualTo(11400000L));
    }

    private static void AssertCanonicalRoomViewsRemainStable(
        CanonicalRoomView entranceView,
        CanonicalRoomView drawingView,
        CanonicalRoomDefinition entranceDefinition,
        CanonicalRoomDefinition drawingDefinition,
        RoomContentGroup entranceContent,
        RoomContentGroup drawingContent,
        bool entranceVisible,
        bool drawingVisible)
    {
        CanonicalRoomView[] roomViews = FindInActiveScene<CanonicalRoomView>();
        Assert.That(roomViews, Has.Length.EqualTo(3),
            "Entrance, Drawing, and Music must remain the only passive RoomView scene owners at this gate.");
        CanonicalPassage[] passages = FindInActiveScene<CanonicalPassage>();
        Assert.That(passages, Has.Length.EqualTo(4),
            "The first two route grafts must remain exactly four reciprocal passive scene bindings.");

        Assert.That(
            roomViews.Single(item => item.Definition != null && item.Definition.StableId == "room.grand-entrance-hall"),
            Is.SameAs(entranceView));
        Assert.That(
            roomViews.Single(item => item.Definition != null && item.Definition.StableId == "room.drawing-room"),
            Is.SameAs(drawingView));
        CanonicalRoomView musicView = roomViews.Single(item =>
            item.Definition != null && item.Definition.StableId == "room.music-room");
        RoomContentGroup musicContent = FindInActiveScene<RoomContentGroup>()
            .Single(item => item.RoomName == MusicRoom);
        Assert.That(entranceView.Definition, Is.SameAs(entranceDefinition));
        Assert.That(drawingView.Definition, Is.SameAs(drawingDefinition));
        Assert.That(entranceDefinition.StableId, Is.EqualTo("room.grand-entrance-hall"));
        Assert.That(drawingDefinition.StableId, Is.EqualTo("room.drawing-room"));
        Assert.That(entranceView.LegacyContentGroup, Is.SameAs(entranceContent));
        Assert.That(drawingView.LegacyContentGroup, Is.SameAs(drawingContent));
        Assert.That(entranceView.gameObject, Is.SameAs(entranceContent.gameObject));
        Assert.That(drawingView.gameObject, Is.SameAs(drawingContent.gameObject));
        Assert.That(entranceView.Root, Is.SameAs(entranceContent.transform));
        Assert.That(drawingView.Root, Is.SameAs(drawingContent.transform));
        Assert.That(entranceView.HasGameContext, Is.True);
        Assert.That(drawingView.HasGameContext, Is.True);
        Assert.That(musicView.Definition.StableId, Is.EqualTo("room.music-room"));
        Assert.That(musicView.LegacyContentGroup, Is.SameAs(musicContent));
        Assert.That(musicView.gameObject, Is.SameAs(musicContent.gameObject));
        Assert.That(musicView.Root, Is.SameAs(musicContent.transform));
        Assert.That(musicView.HasGameContext, Is.True);
        Assert.That(musicView.IsVisible, Is.EqualTo(musicContent.gameObject.activeSelf));

        DoorTriggerNavigation drawingMusicForwardTrigger =
            RequireSceneObject<DoorTriggerNavigation>("DoorTrigger_DrawingRoom_MusicRoom");
        DoorTriggerNavigation drawingMusicReverseTrigger =
            RequireSceneObject<DoorTriggerNavigation>("DoorTrigger_MusicRoom_DrawingRoom");
        AssertDrawingMusicPassagesRemainPassive(
            drawingMusicForwardTrigger,
            drawingMusicReverseTrigger,
            drawingMusicForwardTrigger.GetComponent<CanonicalPassage>(),
            drawingMusicReverseTrigger.GetComponent<CanonicalPassage>(),
            drawingView,
            musicView,
            drawingVisible,
            musicContent.gameObject.activeSelf);

        CanonicalPassage forwardPassage = passages.Single(item =>
            item.Definition != null &&
            item.Definition.StableId == "passage.grand-entrance-hall.drawing-room");
        CanonicalPassage reversePassage = passages.Single(item =>
            item.Definition != null &&
            item.Definition.StableId == "passage.drawing-room.grand-entrance-hall");
        Assert.That(forwardPassage.gameObject.name, Is.EqualTo("DoorTrigger_GEH_DrawingRoom"));
        Assert.That(reversePassage.gameObject.name, Is.EqualTo("DoorTrigger_DrawingRoom_GEH"));
        Assert.That(forwardPassage.GetComponents<CanonicalPassage>(), Has.Length.EqualTo(1));
        Assert.That(reversePassage.GetComponents<CanonicalPassage>(), Has.Length.EqualTo(1));
        Assert.That(forwardPassage.GetComponents<Component>(), Has.Length.EqualTo(5));
        Assert.That(reversePassage.GetComponents<Component>(), Has.Length.EqualTo(5));
        DoorTriggerNavigation forwardTrigger = forwardPassage.GetComponent<DoorTriggerNavigation>();
        DoorTriggerNavigation reverseTrigger = reversePassage.GetComponent<DoorTriggerNavigation>();
        Assert.That(forwardTrigger, Is.Not.Null,
            "The passive binding must remain co-located with the unchanged legacy interaction owner until cutover.");
        Assert.That(reverseTrigger, Is.Not.Null);
        Assert.That(forwardPassage.SourceRoomView, Is.SameAs(entranceView));
        Assert.That(reversePassage.SourceRoomView, Is.SameAs(drawingView));
        Assert.That(forwardPassage.ReversePassage, Is.SameAs(reversePassage));
        Assert.That(reversePassage.ReversePassage, Is.SameAs(forwardPassage));
        Assert.That(forwardPassage.Definition.SourceRoom, Is.SameAs(entranceDefinition));
        Assert.That(forwardPassage.Definition.DestinationRoom, Is.SameAs(drawingDefinition));
        Assert.That(reversePassage.Definition.SourceRoom, Is.SameAs(drawingDefinition));
        Assert.That(reversePassage.Definition.DestinationRoom, Is.SameAs(entranceDefinition));
        Assert.That(forwardPassage.Definition.LegacyDoorId, Is.EqualTo(forwardTrigger.DoorName));
        Assert.That(reversePassage.Definition.LegacyDoorId, Is.EqualTo(reverseTrigger.DoorName));
        Assert.That(GetPrivateField<CanonicalPassage>(forwardTrigger, "canonicalPassage"), Is.SameAs(forwardPassage));
        Assert.That(GetPrivateField<CanonicalPassage>(reverseTrigger, "canonicalPassage"), Is.SameAs(reversePassage));
        Assert.That(
            FindInActiveScene<DoorTriggerNavigation>()
                .Count(trigger => GetPrivateField<CanonicalPassage>(trigger, "canonicalPassage") != null),
            Is.EqualTo(2));
        AssertVector2Within(
            forwardPassage.ApproachAnchor.LogicalPosition,
            new Vector2(-7.75f, -2.22f),
            0.0001f,
            "passive forward approach anchor");
        AssertVector2Within(
            forwardPassage.ArrivalAnchor.LogicalPosition,
            new Vector2(5.267176f, -2.104616f),
            0.0001f,
            "passive forward arrival anchor");
        AssertVector2Within(
            reversePassage.ApproachAnchor.LogicalPosition,
            new Vector2(5.267176f, -2.104616f),
            0.0001f,
            "passive reverse approach anchor");
        AssertVector2Within(
            reversePassage.ArrivalAnchor.LogicalPosition,
            new Vector2(-7.75f, -2.22f),
            0.0001f,
            "passive reverse arrival anchor");
        AssertVector2Within(
            forwardPassage.ApproachAnchor.LogicalPosition,
            reversePassage.ArrivalAnchor.LogicalPosition,
            0.0001f,
            "Entrance shared doorway anchor");
        AssertVector2Within(
            reversePassage.ApproachAnchor.LogicalPosition,
            forwardPassage.ArrivalAnchor.LogicalPosition,
            0.0001f,
            "Drawing Room shared doorway anchor");
        Assert.That(forwardPassage.HasGameContext, Is.True);
        Assert.That(reversePassage.HasGameContext, Is.True);
        Assert.That(forwardPassage.enabled, Is.True);
        Assert.That(reversePassage.enabled, Is.True);
        Assert.That(forwardPassage.isActiveAndEnabled, Is.EqualTo(entranceVisible));
        Assert.That(reversePassage.isActiveAndEnabled, Is.EqualTo(drawingVisible));

        Assert.That(entranceView.IsVisible, Is.EqualTo(entranceContent.gameObject.activeSelf));
        Assert.That(drawingView.IsVisible, Is.EqualTo(drawingContent.gameObject.activeSelf));
        Assert.That(entranceView.IsVisible, Is.EqualTo(entranceVisible));
        Assert.That(drawingView.IsVisible, Is.EqualTo(drawingVisible));

        Chateau.Architecture.ValidationReport entranceReport = new Chateau.Architecture.ValidationReport();
        Chateau.Architecture.ValidationReport drawingReport = new Chateau.Architecture.ValidationReport();
        Chateau.Architecture.ValidationReport musicReport = new Chateau.Architecture.ValidationReport();
        entranceView.ValidateConfiguration(entranceReport);
        drawingView.ValidateConfiguration(drawingReport);
        musicView.ValidateConfiguration(musicReport);
        Assert.That(entranceReport.Messages, Is.Empty);
        Assert.That(drawingReport.Messages, Is.Empty);
        Assert.That(musicReport.Messages, Is.Empty);
        Chateau.Architecture.ValidationReport forwardReport = new Chateau.Architecture.ValidationReport();
        Chateau.Architecture.ValidationReport reverseReport = new Chateau.Architecture.ValidationReport();
        forwardPassage.ValidateConfiguration(forwardReport);
        reversePassage.ValidateConfiguration(reverseReport);
        Assert.That(forwardReport.Messages, Is.Empty);
        Assert.That(reverseReport.Messages, Is.Empty);

        Chateau.Architecture.GameRoot gameRoot = RequireExactlyOneInActiveScene<Chateau.Architecture.GameRoot>();
        List<Chateau.Architecture.ChateauBehaviour> sceneBehaviours =
            GetPrivateField<List<Chateau.Architecture.ChateauBehaviour>>(gameRoot, "sceneBehaviours");
        Assert.That(sceneBehaviours.Count(item => item == entranceView), Is.EqualTo(1));
        Assert.That(sceneBehaviours.Count(item => item == drawingView), Is.EqualTo(1));
        Assert.That(sceneBehaviours.Count(item => item == musicView), Is.EqualTo(1));
        Assert.That(sceneBehaviours.Count(item => item == forwardPassage), Is.EqualTo(1));
        Assert.That(sceneBehaviours.Count(item => item == reversePassage), Is.EqualTo(1));
    }

    private static void AssertDrawingMusicPassagesRemainPassive(
        DoorTriggerNavigation forwardTrigger,
        DoorTriggerNavigation reverseTrigger,
        CanonicalPassage forwardPassage,
        CanonicalPassage reversePassage,
        CanonicalRoomView drawingView,
        CanonicalRoomView musicView,
        bool drawingVisible,
        bool musicVisible)
    {
        Assert.That(forwardTrigger, Is.Not.Null);
        Assert.That(reverseTrigger, Is.Not.Null);
        Assert.That(forwardPassage, Is.Not.Null);
        Assert.That(reversePassage, Is.Not.Null);
        Assert.That(drawingView, Is.Not.Null);
        Assert.That(musicView, Is.Not.Null);
        Assert.That(FindInActiveScene<CanonicalPassage>(), Has.Length.EqualTo(4));

        Assert.That(forwardPassage.gameObject, Is.SameAs(forwardTrigger.gameObject));
        Assert.That(reversePassage.gameObject, Is.SameAs(reverseTrigger.gameObject));
        Assert.That(forwardTrigger.GetComponents<CanonicalPassage>(), Has.Length.EqualTo(1));
        Assert.That(reverseTrigger.GetComponents<CanonicalPassage>(), Has.Length.EqualTo(1));
        Assert.That(forwardTrigger.GetComponents<Component>(), Has.Length.EqualTo(5));
        Assert.That(reverseTrigger.GetComponents<Component>(), Has.Length.EqualTo(5));

        Assert.That(forwardPassage.Definition, Is.Not.Null);
        Assert.That(reversePassage.Definition, Is.Not.Null);
        Assert.That(forwardPassage.Definition.StableId, Is.EqualTo("passage.drawing-room.music-room"));
        Assert.That(reversePassage.Definition.StableId, Is.EqualTo("passage.music-room.drawing-room"));
        Assert.That(forwardPassage.Definition.LegacyDoorId, Is.EqualTo(forwardTrigger.DoorName));
        Assert.That(reversePassage.Definition.LegacyDoorId, Is.EqualTo(reverseTrigger.DoorName));
        Assert.That(forwardPassage.Definition.SourceRoom, Is.SameAs(drawingView.Definition));
        Assert.That(forwardPassage.Definition.DestinationRoom, Is.SameAs(musicView.Definition));
        Assert.That(reversePassage.Definition.SourceRoom, Is.SameAs(musicView.Definition));
        Assert.That(reversePassage.Definition.DestinationRoom, Is.SameAs(drawingView.Definition));
        Assert.That(forwardPassage.Definition.Reverse, Is.SameAs(reversePassage.Definition));
        Assert.That(reversePassage.Definition.Reverse, Is.SameAs(forwardPassage.Definition));
        Assert.That(
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                forwardPassage.Definition,
                out string forwardDefinitionGuid,
                out long forwardDefinitionFileId),
            Is.True);
        Assert.That(
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                reversePassage.Definition,
                out string reverseDefinitionGuid,
                out long reverseDefinitionFileId),
            Is.True);
        Assert.That(forwardDefinitionGuid, Is.EqualTo("3167361ca4c671298c0e84f43320619b"));
        Assert.That(reverseDefinitionGuid, Is.EqualTo("01544de8f55723585d60e5c0915345fd"));
        Assert.That(forwardDefinitionFileId, Is.EqualTo(11400000L));
        Assert.That(reverseDefinitionFileId, Is.EqualTo(11400000L));

        Assert.That(forwardPassage.SourceRoomView, Is.SameAs(drawingView));
        Assert.That(reversePassage.SourceRoomView, Is.SameAs(musicView));
        Assert.That(forwardPassage.ReversePassage, Is.SameAs(reversePassage));
        Assert.That(reversePassage.ReversePassage, Is.SameAs(forwardPassage));
        Assert.That(forwardPassage.ApproachAnchor, Is.Not.Null);
        Assert.That(forwardPassage.ArrivalAnchor, Is.Not.Null);
        Assert.That(reversePassage.ApproachAnchor, Is.Not.Null);
        Assert.That(reversePassage.ArrivalAnchor, Is.Not.Null);
        AssertVector2Within(
            forwardPassage.ApproachAnchor.LogicalPosition,
            new Vector2(-7.10601f, -1.508934f),
            0.0001f,
            "passive Drawing-to-Music approach anchor");
        AssertVector2Within(
            forwardPassage.ArrivalAnchor.LogicalPosition,
            new Vector2(-7.737432f, -3.180156f),
            0.0001f,
            "passive Drawing-to-Music arrival anchor");
        AssertVector2Within(
            reversePassage.ApproachAnchor.LogicalPosition,
            new Vector2(-7.737432f, -3.180156f),
            0.0001f,
            "passive Music-to-Drawing approach anchor");
        AssertVector2Within(
            reversePassage.ArrivalAnchor.LogicalPosition,
            new Vector2(-7.10601f, -1.508934f),
            0.0001f,
            "passive Music-to-Drawing arrival anchor");
        AssertVector2Within(
            forwardPassage.ApproachAnchor.LogicalPosition,
            reversePassage.ArrivalAnchor.LogicalPosition,
            0.0001f,
            "Drawing Room shared passive doorway anchor");
        AssertVector2Within(
            reversePassage.ApproachAnchor.LogicalPosition,
            forwardPassage.ArrivalAnchor.LogicalPosition,
            0.0001f,
            "Music Room shared passive doorway anchor");

        Assert.That(GetPrivateField<CanonicalPassage>(forwardTrigger, "canonicalPassage"), Is.Null);
        Assert.That(GetPrivateField<CanonicalPassage>(reverseTrigger, "canonicalPassage"), Is.Null);
        Assert.That(forwardPassage.HasGameContext, Is.True);
        Assert.That(reversePassage.HasGameContext, Is.True);
        Assert.That(forwardPassage.enabled, Is.True);
        Assert.That(reversePassage.enabled, Is.True);
        Assert.That(forwardPassage.isActiveAndEnabled, Is.EqualTo(drawingVisible));
        Assert.That(reversePassage.isActiveAndEnabled, Is.EqualTo(musicVisible));

        Chateau.Architecture.ValidationReport forwardReport = new Chateau.Architecture.ValidationReport();
        Chateau.Architecture.ValidationReport reverseReport = new Chateau.Architecture.ValidationReport();
        forwardPassage.ValidateConfiguration(forwardReport);
        reversePassage.ValidateConfiguration(reverseReport);
        Assert.That(forwardReport.Messages, Is.Empty);
        Assert.That(reverseReport.Messages, Is.Empty);

        Chateau.Architecture.GameRoot gameRoot = RequireExactlyOneInActiveScene<Chateau.Architecture.GameRoot>();
        List<Chateau.Architecture.ChateauBehaviour> sceneBehaviours =
            GetPrivateField<List<Chateau.Architecture.ChateauBehaviour>>(gameRoot, "sceneBehaviours");
        Assert.That(sceneBehaviours.Count(item => item == forwardPassage), Is.EqualTo(1));
        Assert.That(sceneBehaviours.Count(item => item == reversePassage), Is.EqualTo(1));
    }

    private static void AssertDrawingMusicRuntimeDependenciesMatchTemplate(
        DoorTriggerNavigation forwardTrigger,
        DoorTriggerNavigation reverseTrigger,
        DoorTriggerNavigation templateTrigger,
        RoomNavigationManager navigation,
        Transform playerTransform,
        AudioSource passageAudioSource,
        DoorOpenSoundCatalog passageDoorCatalog)
    {
        Assert.That(templateTrigger, Is.Not.Null);
        Assert.That(passageDoorCatalog, Is.Not.Null);
        Assert.That(
            AssetDatabase.GetAssetPath(passageDoorCatalog),
            Is.EqualTo("Assets/Resources/Audio/DoorOpenSoundCatalog.asset"));
        Assert.That(GetPrivateField<RoomNavigationManager>(templateTrigger, "navigationManager"), Is.SameAs(navigation));
        Assert.That(GetPrivateField<Transform>(templateTrigger, "player"), Is.SameAs(playerTransform));
        Assert.That(GetPrivateField<AudioSource>(templateTrigger, "doorOpenAudioSource"), Is.SameAs(passageAudioSource));
        Assert.That(
            GetPrivateField<DoorOpenSoundCatalog>(templateTrigger, "doorOpenSoundCatalog"),
            Is.SameAs(passageDoorCatalog));

        AssertDoorTriggerCompatibilityBindings(
            forwardTrigger,
            reverseTrigger,
            navigation,
            playerTransform,
            passageAudioSource,
            passageDoorCatalog);

        DoorTriggerNavigation[] drawingMusicTriggers = { forwardTrigger, reverseTrigger };
        for (int i = 0; i < drawingMusicTriggers.Length; i++)
        {
            DoorTriggerNavigation trigger = drawingMusicTriggers[i];
            Assert.That(GetPrivateField<DoorOpenSoundCatalog>(trigger, "stairwaySoundCatalog"), Is.Null);
            Assert.That(GetPrivateField<CanonicalPassage>(trigger, "canonicalPassage"), Is.Null);
        }
    }

    private static void AssertSerializedDrawingMusicDependenciesAreBound()
    {
        string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
        string sceneText = System.IO.File.ReadAllText(System.IO.Path.Combine(projectRoot, GameplayScenePath));
        AssertSerializedLegacyTriggerDependenciesAreBound(
            RequireSerializedUnityDocument(sceneText, "2300000099"),
            "DrawingRoom_MusicRoom");
        AssertSerializedLegacyTriggerDependenciesAreBound(
            RequireSerializedUnityDocument(sceneText, "2300000089"),
            "MusicRoom_DrawingRoom");
    }

    private static void AssertSerializedLegacyTriggerDependenciesAreBound(
        string triggerDocument,
        string legacyDoorId)
    {
        Assert.That(triggerDocument, Does.Contain($"doorName: {legacyDoorId}"));
        Assert.That(triggerDocument, Does.Not.Contain("canonicalPassage:"));
        Assert.That(triggerDocument, Does.Contain("navigationManager: {fileID: 1878886997}"));
        Assert.That(triggerDocument, Does.Contain("doorOpenAudioSource: {fileID: 2201000013}"));
        Assert.That(triggerDocument, Does.Contain("player: {fileID: 81962843}"));
        Assert.That(triggerDocument, Does.Contain(
            "doorOpenSoundCatalog: {fileID: 11400000, guid: 9a77542e25184fbc945d6a79f77007e7, type: 2}"));
        Assert.That(triggerDocument, Does.Contain("stairwaySoundCatalog: {fileID: 0}"));
    }

    private static string RequireSerializedUnityDocument(string sceneText, string fileId)
    {
        string normalizedText = sceneText.Replace("\r\n", "\n");
        string header = $"--- !u!114 &{fileId}\n";
        int start = normalizedText.IndexOf(header, System.StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing serialized MonoBehaviour document {fileId}.");
        int end = normalizedText.IndexOf("\n--- !u!", start + header.Length, System.StringComparison.Ordinal);
        return end < 0 ? normalizedText.Substring(start) : normalizedText.Substring(start, end - start);
    }

    private static void AssertGrandfatherClockPlaceholdersRemainUnmodified(
        Transform entranceClockPlaceholder,
        Transform drawingRoomClockPlaceholder)
    {
        Assert.That(
            FindInActiveScene<Transform>().Single(item => item.name == "GrandfatherClock"),
            Is.SameAs(entranceClockPlaceholder));
        Assert.That(
            FindInActiveScene<Transform>().Single(item => item.name == "GrandfatherClock_Optional"),
            Is.SameAs(drawingRoomClockPlaceholder));
        Assert.That(
            FindInActiveScene<MonoBehaviour>()
                .Any(component => component != null && component.GetType().Name == "GrandfatherClockInteraction"),
            Is.False);
        Assert.That(entranceClockPlaceholder.parent.name, Is.EqualTo("Props"));
        Assert.That(drawingRoomClockPlaceholder.parent.name, Is.EqualTo("Props"));
        Assert.That(entranceClockPlaceholder.localPosition, Is.EqualTo(new Vector3(-545f, -205f, -7691.114f)));
        Assert.That(drawingRoomClockPlaceholder.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(entranceClockPlaceholder.GetComponents<Component>(), Has.Length.EqualTo(1));
        Assert.That(drawingRoomClockPlaceholder.GetComponents<Component>(), Has.Length.EqualTo(1));
        Assert.That(entranceClockPlaceholder.GetComponent<AudioSource>(), Is.Null);
        Assert.That(drawingRoomClockPlaceholder.GetComponent<AudioSource>(), Is.Null);
        Assert.That(entranceClockPlaceholder.GetComponent<GameAudioSourceVolume>(), Is.Null);
        Assert.That(drawingRoomClockPlaceholder.GetComponent<GameAudioSourceVolume>(), Is.Null);
        RoomAnchor[] entranceAnchors = entranceClockPlaceholder.GetComponentsInChildren<RoomAnchor>(true);
        RoomAnchor[] drawingRoomAnchors = drawingRoomClockPlaceholder.GetComponentsInChildren<RoomAnchor>(true);
        Assert.That(entranceAnchors, Has.Length.EqualTo(2));
        Assert.That(drawingRoomAnchors, Has.Length.EqualTo(2));
        CollectionAssert.AreEquivalent(new[] { "ApproachFront", "CloseUpCamera" }, entranceAnchors.Select(anchor => anchor.AnchorId));
        CollectionAssert.AreEquivalent(new[] { "ApproachFront", "CloseUpCamera" }, drawingRoomAnchors.Select(anchor => anchor.AnchorId));
        Assert.That(entranceAnchors.All(anchor => anchor.RoomId == EntranceRoom), Is.True);
        Assert.That(drawingRoomAnchors.All(anchor => anchor.RoomId == DrawingRoom), Is.True);
        Assert.That(
            Resources.FindObjectsOfTypeAll<AudioClip>()
                .Any(clip => clip != null && clip.name == "RuntimeGrandfatherClockTicking"),
            Is.False);

        string[] retiredModalObjectNames =
        {
            "Canvas_GrandfatherClockCloseUp",
            "Panel_GrandfatherClockCloseUp",
            "Image_ClockFace",
            "Text_ClockTime",
            "Button_CloseClock"
        };

        for (int i = 0; i < retiredModalObjectNames.Length; i++)
        {
            string retiredName = retiredModalObjectNames[i];
            Assert.That(
                FindInActiveScene<Transform>().Any(item => item.name == retiredName),
                Is.False,
                $"Retired grandfather-clock presentation object '{retiredName}' must not be created.");
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

    private static T GetPrivateField<T>(object owner, string fieldName) where T : class
    {
        System.Reflection.FieldInfo field = owner.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on {owner.GetType().Name}.");
        return field.GetValue(owner) as T;
    }

    private static T GetPrivateValue<T>(object owner, string fieldName)
    {
        System.Reflection.FieldInfo field = owner.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on {owner.GetType().Name}.");
        return (T)field.GetValue(owner);
    }

    private static T GetPrivateStaticField<T>(System.Type ownerType, string fieldName)
    {
        System.Reflection.FieldInfo field = ownerType.GetField(
            fieldName,
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing private static field '{fieldName}' on {ownerType.Name}.");
        return (T)field.GetValue(null);
    }

    private static T InvokePrivateResult<T>(object owner, string methodName)
    {
        System.Reflection.MethodInfo method = owner.GetType().GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null,
            System.Type.EmptyTypes,
            null);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}()' on {owner.GetType().Name}.");
        return (T)method.Invoke(owner, null);
    }

    private static void InvokePrivateStaticMethod(System.Type ownerType, string methodName)
    {
        System.Reflection.MethodInfo method = ownerType.GetMethod(
            methodName,
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
            null,
            System.Type.EmptyTypes,
            null);
        Assert.That(method, Is.Not.Null, $"Missing private static method '{methodName}()' on {ownerType.Name}.");
        method.Invoke(null, null);
    }

    private static bool TryInvokeApproachDestination(
        DoorTriggerNavigation trigger,
        PointClickPlayerMovement player,
        bool requireMovement,
        out Vector2 destination,
        Vector2? preferredScreenPosition = null)
    {
        System.Reflection.MethodInfo method = typeof(DoorTriggerNavigation).GetMethod(
            "TryFindBestApproachDestination",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null,
            new[]
            {
                typeof(PointClickPlayerMovement),
                typeof(bool),
                typeof(Vector2).MakeByRefType(),
                typeof(Vector2?)
            },
            null);
        Assert.That(method, Is.Not.Null,
            "Missing private DoorTriggerNavigation.TryFindBestApproachDestination characterization seam.");

        object[] arguments = { player, requireMovement, Vector2.zero, preferredScreenPosition };
        bool found = (bool)method.Invoke(trigger, arguments);
        destination = (Vector2)arguments[2];
        return found;
    }

    private static bool TryInvokeTraversalApproachDestination(
        DoorTriggerNavigation trigger,
        PointClickPlayerMovement player,
        out Vector2 destination,
        Vector2? preferredScreenPosition)
    {
        System.Reflection.MethodInfo method = typeof(DoorTriggerNavigation).GetMethod(
            "TryFindTraversalApproachDestination",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null,
            new[]
            {
                typeof(PointClickPlayerMovement),
                typeof(Vector2).MakeByRefType(),
                typeof(Vector2?)
            },
            null);
        Assert.That(method, Is.Not.Null,
            "Missing private DoorTriggerNavigation.TryFindTraversalApproachDestination seam.");

        object[] arguments = { player, Vector2.zero, preferredScreenPosition };
        bool found = (bool)method.Invoke(trigger, arguments);
        destination = (Vector2)arguments[1];
        return found;
    }

    private static bool TryGetTriggerScreenBounds(
        DoorTriggerNavigation trigger,
        out Vector2 min,
        out Vector2 max)
    {
        System.Reflection.MethodInfo method = typeof(DoorTriggerNavigation).GetMethod(
            "TryGetTriggerScreenBounds",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null,
            new[] { typeof(Vector2).MakeByRefType(), typeof(Vector2).MakeByRefType() },
            null);
        Assert.That(method, Is.Not.Null,
            "Missing private DoorTriggerNavigation.TryGetTriggerScreenBounds characterization seam.");

        object[] arguments = { Vector2.zero, Vector2.zero };
        bool found = (bool)method.Invoke(trigger, arguments);
        min = (Vector2)arguments[0];
        max = (Vector2)arguments[1];
        return found;
    }

    private static Vector2 BuildPreferredTriggerClick(Vector2 min, Vector2 max, float horizontal01)
    {
        return new Vector2(
            Mathf.Lerp(min.x, max.x, Mathf.Clamp01(horizontal01)),
            Mathf.Lerp(min.y, max.y, 0.5f));
    }

    private static string FormatVector(Vector2 value)
    {
        return $"{value.x:0.######},{value.y:0.######}";
    }

    private static bool TryWarpToCharacterizedFarStart(
        PointClickPlayerMovement player,
        DoorTriggerNavigation trigger,
        Vector2 requestedPosition,
        out float screenDistance)
    {
        InvokePrivateMethod(trigger, "ResolvePlayerReference");
        Assert.That(GetPrivateField<Transform>(trigger, "player"), Is.SameAs(player.transform));

        if (!player.TryWarpTo(requestedPosition, true))
        {
            screenDistance = 0f;
            return false;
        }

        screenDistance = InvokePrivateResult<float>(trigger, "GetPlayerScreenDistanceToTrigger");
        return !float.IsNaN(screenDistance) && !float.IsInfinity(screenDistance);
    }

    private static void AssertFinite(Vector2 value, string label)
    {
        Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x), Is.False, $"{label} x must be finite.");
        Assert.That(float.IsNaN(value.y) || float.IsInfinity(value.y), Is.False, $"{label} y must be finite.");
    }

    private static void AssertApproachWithinActivationDistance(
        DoorTriggerNavigation trigger,
        PointClickPlayerMovement player,
        Vector2 logicalDestination,
        string label)
    {
        Assert.That(
            player.TryGetScreenPointFromLogicalPosition(logicalDestination, out Vector2 destinationScreenPoint),
            Is.True,
            $"{label} must project into the rendered room.");
        Assert.That(TryGetTriggerScreenBounds(trigger, out Vector2 min, out Vector2 max), Is.True);
        Vector2 closestThresholdPoint = new Vector2(
            Mathf.Clamp(destinationScreenPoint.x, min.x, max.x),
            min.y);
        float screenDistance = Vector2.Distance(destinationScreenPoint, closestThresholdPoint);
        float activationDistance = GetPrivateValue<float>(trigger, "maxPlayerScreenDistance");
        Assert.That(
            screenDistance,
            Is.LessThanOrEqualTo(activationDistance + 0.5f),
            $"{label} must finish close enough for automatic activation.");
    }

    private static void AssertVector2Within(Vector2 actual, Vector2 expected, float tolerance, string label)
    {
        AssertFinite(actual, label);
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance), $"{label} x changed.");
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance), $"{label} y changed.");
    }

    private static void SetPrivateField<T>(object owner, string fieldName, T value)
    {
        System.Reflection.FieldInfo field = owner.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on {owner.GetType().Name}.");
        field.SetValue(owner, value);
    }

    private static void InvokePrivateMethod(object owner, string methodName)
    {
        System.Reflection.MethodInfo method = owner.GetType().GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null,
            System.Type.EmptyTypes,
            null);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}()' on {owner.GetType().Name}.");
        method.Invoke(owner, null);
    }

    private static T InvokePrivateIntMethod<T>(object owner, string methodName, int value) where T : class
    {
        System.Reflection.MethodInfo method = owner.GetType().GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null,
            new[] { typeof(int) },
            null);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}(int)' on {owner.GetType().Name}.");
        return method.Invoke(owner, new object[] { value }) as T;
    }

    private static T InvokePrivateStringMethod<T>(object owner, string methodName, string value) where T : class
    {
        System.Reflection.MethodInfo method = owner.GetType().GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null,
            new[] { typeof(string) },
            null);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}(string)' on {owner.GetType().Name}.");
        return method.Invoke(owner, new object[] { value }) as T;
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
