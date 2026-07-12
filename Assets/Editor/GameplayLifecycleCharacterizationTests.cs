using System.Collections;
using System.Linq;
using Chateau.World.Rooms.Props;
using NUnit.Framework;
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
        Transform entranceCoatHanger = FindInActiveScene<Transform>()
            .Single(item => item.name == "entrance_coat_hanger_0");
        SpriteRenderer entranceCoatHangerRenderer = entranceCoatHanger.GetComponent<SpriteRenderer>();
        CoatCloset pantryCoatCloset = RequireExactlyOneInActiveScene<CoatCloset>();
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
        SubtitleService subtitle = RequireExactlyOneInActiveScene<SubtitleService>();
        DialogueSpeechService speech = RequireExactlyOneInActiveScene<DialogueSpeechService>();
        Assert.That(subtitle.IsInitialized, Is.True);
        Assert.That(subtitle.HasGameContext, Is.True);
        Assert.That(speech.IsInitialized, Is.True);
        Assert.That(speech.HasGameContext, Is.True);
        Assert.That(chapter1Hud.gameObject, Is.SameAs(arrival.gameObject));
        Assert.That(entranceCoatHangerRenderer, Is.Not.Null);
        Assert.That(FindInActiveScene<CoatCloset>(), Has.Length.EqualTo(1));
        Assert.That(entranceCoatHanger.GetComponents<CoatCloset>(), Is.Empty);
        Assert.That(entranceCoatHanger.GetComponents<Chapter1SceneAction>(), Is.Empty);
        Assert.That(entranceCoatHanger.GetComponents<BoxCollider2D>(), Is.Empty);
        Assert.That(pantryCoatCloset.gameObject.name, Is.EqualTo("CoatCloset"));
        Assert.That(GetPrivateField<CoatCloset>(arrival, "coatCloset"), Is.SameAs(pantryCoatCloset));

        InvokePrivateBooleanMethod(arrival, "ResolveReferences", true);
        CoatCloset entranceCoatCloset = entranceCoatHanger.GetComponent<CoatCloset>();
        Chapter1SceneAction entranceCoatAction = entranceCoatHanger.GetComponent<Chapter1SceneAction>();
        BoxCollider2D entranceCoatCollider = entranceCoatHanger.GetComponent<BoxCollider2D>();

        Assert.That(FindInActiveScene<CoatCloset>(), Has.Length.EqualTo(2));
        Assert.That(entranceCoatCloset, Is.Not.Null);
        Assert.That(entranceCoatAction, Is.Not.Null);
        Assert.That(entranceCoatCollider, Is.Not.Null);
        Assert.That(entranceCoatHanger.GetComponents<CoatCloset>(), Has.Length.EqualTo(1));
        Assert.That(entranceCoatHanger.GetComponents<Chapter1SceneAction>(), Has.Length.EqualTo(1));
        Assert.That(entranceCoatHanger.GetComponents<BoxCollider2D>(), Has.Length.EqualTo(1));
        Assert.That(GetPrivateField<CoatCloset>(arrival, "coatCloset"), Is.SameAs(entranceCoatCloset));
        Assert.That(GetPrivateField<Transform>(arrival, "closetPoint"), Is.SameAs(entranceCoatHanger));
        Assert.That(pantryCoatCloset, Is.Not.SameAs(entranceCoatCloset));
        Assert.That(pantryCoatCloset.StoredCoatCount, Is.Zero);
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
        InvokePrivateBooleanMethod(arrival, "ResolveReferences", true);
        InvokePrivateBooleanMethod(arrival, "ResolveReferences", true);
        Assert.That(GetPrivateField<CoatCloset>(arrival, "coatCloset"), Is.SameAs(entranceCoatCloset));
        Assert.That(GetPrivateField<Transform>(arrival, "closetPoint"), Is.SameAs(entranceCoatHanger));
        Assert.That(entranceCoatHanger.GetComponent<CoatCloset>(), Is.SameAs(entranceCoatCloset));
        Assert.That(entranceCoatHanger.GetComponent<Chapter1SceneAction>(), Is.SameAs(entranceCoatAction));
        Assert.That(entranceCoatHanger.GetComponent<BoxCollider2D>(), Is.SameAs(entranceCoatCollider));
        Assert.That(FindInActiveScene<CoatCloset>(), Has.Length.EqualTo(2));
        Assert.That(entranceCoatCloset.ContainsCoat("architecture_characterization_coat"), Is.True);
        Assert.That(pantryCoatCloset.StoredCoatCount, Is.Zero);
        entranceCoatCloset.ClearStoredCoats();
        Canvas chapter1Canvas = FindInActiveScene<Canvas>().Single(item => item.name == "Canvas_Chapter1HUD");
        Assert.That(chapter1Canvas.sortingOrder, Is.EqualTo(9100));
        Assert.That(FindInActiveScene<Transform>().Count(item => item.name == "Text_Chapter1Status"), Is.EqualTo(1));
        Canvas settingsCanvas = FindInActiveScene<Canvas>().Single(item => item.name == "Canvas_RuntimeSettingsMenu");
        EventSystem serializedEventSystem = RequireExactlyOneInActiveScene<EventSystem>();
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
        Assert.That(GetPrivateField<CoatCloset>(arrival, "coatCloset"), Is.SameAs(entranceCoatCloset));
        Assert.That(GetPrivateField<Transform>(arrival, "closetPoint"), Is.SameAs(entranceCoatHanger));
        Assert.That(entranceCoatHanger.GetComponent<CoatCloset>(), Is.SameAs(entranceCoatCloset));
        Assert.That(entranceCoatHanger.GetComponent<Chapter1SceneAction>(), Is.SameAs(entranceCoatAction));
        Assert.That(entranceCoatHanger.GetComponent<BoxCollider2D>(), Is.SameAs(entranceCoatCollider));
        Assert.That(FindInActiveScene<CoatCloset>(), Has.Length.EqualTo(2));
        Debug.Log(
            $"[EntranceCoatHangerCharacterization] hanger={entranceCoatHanger.GetInstanceID()} " +
            $"closet={entranceCoatCloset.GetInstanceID()} action={entranceCoatAction.GetInstanceID()} " +
            $"collider={entranceCoatCollider.GetInstanceID()} pantryCloset={pantryCoatCloset.GetInstanceID()}");

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

        chapter.SkipToChapter2ForTesting();
        yield return WaitForSettledLayout();

        Assert.That(RequireExactlyOneInActiveScene<Chapter2Controller>(), Is.SameAs(chapter2));
        Assert.That(GetPrivateField<ChapterManager>(chapter2, "chapterManager"), Is.SameAs(chapter));
        Assert.That(RequireExactlyOneInActiveScene<Chapter2InteractionHUD>(), Is.SameAs(chapter2Hud));
        Assert.That(RequireExactlyOneInActiveScene<Chapter2MonsterStingerController>(), Is.SameAs(monsterStinger));
        Assert.That(RequireExactlyOneInActiveScene<Chapter2GuestPanicController>(), Is.SameAs(guestPanic));
        Assert.That(RequireExactlyOneInActiveScene<Chapter2GuestSearchController>(), Is.SameAs(guestSearch));
        Assert.That(GetPrivateField<RoomNavigationManager>(guestSearch, "navigationManager"), Is.SameAs(serializedGuestSearchNavigation));
        Assert.That(RequireExactlyOneInActiveScene<Chapter1InteractionHUD>(), Is.SameAs(chapter1Hud));
        Assert.That(RequireExactlyOneInActiveScene<GuestRoomScaleApplier>(), Is.SameAs(serializedGuestScaleApplier));
        Assert.That(RequireExactlyOneInActiveScene<GuestRoomScaleCalibration>(), Is.SameAs(serializedGuestScaleCalibration));
        Assert.That(RequireExactlyOneInActiveScene<FireplaceAmbienceController>(), Is.SameAs(fireplaceAmbience));
        Assert.That(RequireExactlyOneInActiveScene<ClockTickingAmbienceController>(), Is.SameAs(clockAmbience));
        Assert.That(RequireExactlyOneInActiveScene<EventSystem>(), Is.SameAs(serializedEventSystem));
        Assert.That(explorationMusicSource.GetComponent<GameAudioSourceVolume>(), Is.SameAs(explorationMusicBinding));

        chapter.SkipToSevenPMForTesting();
        yield return null;

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
        yield return null;

        Assert.That(GetPrivateField<AudioSource>(chapter2, "clockStrikeAudioSource"), Is.SameAs(clockStrikeSource));
        Assert.That(GetPrivateField<AudioClip>(chapter2, "clockStrikeClip"), Is.SameAs(clockStrikeClip));
        Assert.That(GetPrivateField<RoomNavigationManager>(guestSearch, "navigationManager"), Is.SameAs(navigation));
        Assert.That(clockStrikeSource.GetComponents<AudioSource>(), Has.Length.EqualTo(1));
        Assert.That(clockStrikeSource.GetComponents<GameAudioSourceVolume>(), Has.Length.EqualTo(1));
        Assert.That(clockStrikeSource.GetComponent<GameAudioSourceVolume>(), Is.SameAs(clockStrikeBindings[0]));

        Assert.That(navigation.DebugTeleportToRoom(DrawingRoom), Is.True);
        yield return WaitForSettledLayout();
        Assert.That(navigation.CurrentRoom, Is.EqualTo(DrawingRoom));
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

    private static void InvokePrivateBooleanMethod(object owner, string methodName, bool value)
    {
        System.Reflection.MethodInfo method = owner.GetType().GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null,
            new[] { typeof(bool) },
            null);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}(bool)' on {owner.GetType().Name}.");
        method.Invoke(owner, new object[] { value });
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
