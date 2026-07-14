#if UNITY_EDITOR
using System;
using System.IO;
using Chateau.Architecture;
using NUnit.Framework;

public sealed class ArchitectureFoundationTests
{
    private enum TestState
    {
        Idle,
        Running,
        Complete
    }

    [Test]
    public void StateMachineRejectsUndeclaredTransition()
    {
        StateMachine<TestState> machine = new StateMachine<TestState>(TestState.Idle)
            .Allow(TestState.Idle, TestState.Running)
            .Allow(TestState.Running, TestState.Complete);

        Assert.That(machine.TryTransition(TestState.Complete), Is.False);
        Assert.That(machine.Current, Is.EqualTo(TestState.Idle));
    }

    [Test]
    public void StateMachinePublishesValidTransition()
    {
        StateMachine<TestState> machine = new StateMachine<TestState>(TestState.Idle)
            .Allow(TestState.Idle, TestState.Running);
        TestState observedFrom = TestState.Complete;
        TestState observedTo = TestState.Complete;
        string observedReason = null;

        machine.Transitioned += (from, to, reason) =>
        {
            observedFrom = from;
            observedTo = to;
            observedReason = reason;
        };

        machine.TransitionOrThrow(TestState.Running, "test");

        Assert.That(machine.Current, Is.EqualTo(TestState.Running));
        Assert.That(observedFrom, Is.EqualTo(TestState.Idle));
        Assert.That(observedTo, Is.EqualTo(TestState.Running));
        Assert.That(observedReason, Is.EqualTo("test"));
    }

    [Test]
    public void ValidationReportTracksErrorsAndWarnings()
    {
        ValidationReport report = new ValidationReport();
        report.AddWarning("warning");
        report.AddError("error");

        Assert.That(report.HasErrors, Is.True);
        Assert.That(report.ErrorCount, Is.EqualTo(1));
        Assert.That(report.WarningCount, Is.EqualTo(1));
        Assert.That(report.Messages.Count, Is.EqualTo(2));
    }

    [Test]
    public void GameRootNeverRepairsDependenciesAtRuntime()
    {
        string rootText = File.ReadAllText("Assets/_Chateau/Runtime/Core/GameRoot.cs");

        Assert.That(rootText, Does.Not.Contain("FindAnyObjectByType"));
        Assert.That(rootText, Does.Not.Contain("FindFirstObjectByType"));
        Assert.That(rootText, Does.Not.Contain("GameObject.Find"));
        Assert.That(rootText, Does.Not.Contain("new GameObject"));
        Assert.That(rootText, Does.Not.Contain("AddComponent<"));
        Assert.That(rootText, Does.Not.Contain("Resources.Load"));
        Assert.That(rootText, Does.Not.Contain("RuntimeInitializeOnLoadMethod"));
    }

    [Test]
    public void MajorManagersEnterTheArchitectureFamiliesWithoutChangingScriptFiles()
    {
        Assert.That(File.ReadAllText("Assets/Scripts/Story/ChapterManager.cs"), Does.Contain("Chateau.Architecture.GameServiceBase"));
        Assert.That(File.ReadAllText("Assets/Scripts/Story/ChapterClock.cs"), Does.Contain("Chateau.Architecture.GameServiceBase"));
        Assert.That(File.ReadAllText("Assets/Scripts/Navigation/RoomNavigationManager.cs"), Does.Contain("Chateau.Architecture.GameServiceBase"));
        Assert.That(File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs"), Does.Contain("Chateau.Architecture.ChapterControllerBase"));
        Assert.That(File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Controller.cs"), Does.Contain("Chateau.Architecture.ChapterControllerBase"));
    }

    [Test]
    public void ProvenDeadRuntimeScriptsStayPruned()
    {
        Assert.That(File.Exists("Assets/Scripts/NewBehaviourScript.cs"), Is.False);
        Assert.That(File.Exists("Assets/Scripts/PickupObject.cs"), Is.False);
        Assert.That(File.Exists("Assets/Scripts/Story/GameClockHandsDisplay.cs"), Is.False);
        Assert.That(File.Exists("Assets/Scripts/Story/GameClockHandsDisplay.cs.meta"), Is.False);
        Assert.That(File.Exists("Assets/Scripts/Story/GrandfatherClockInteraction.cs"), Is.False);
        Assert.That(File.Exists("Assets/Scripts/Story/GrandfatherClockInteraction.cs.meta"), Is.False);
    }

    [Test]
    public void RuntimeNavigationBootstrapStaysPruned()
    {
        Assert.That(File.Exists("Assets/Scripts/Navigation/RoomNavigationBootstrap.cs"), Is.False);
        Assert.That(File.Exists("Assets/Scripts/Navigation/RoomNavigationBootstrap.cs.meta"), Is.False);
    }

    [Test]
    public void ChapterStackIsSerializedInsteadOfRepairedAtRuntime()
    {
        string managerText = File.ReadAllText("Assets/Scripts/Story/ChapterManager.cs");
        string introText = File.ReadAllText("Assets/Scripts/Story/ChapterIntroUI.cs");
        string sceneText = File.ReadAllText("Assets/Scenes/Gameplay.unity");
        string playerInstanceDocument = ExtractDocument(sceneText, "--- !u!1001 &81962841");
        string gameRootDocument = ExtractDocument(sceneText, "--- !u!114 &1878886998");
        string gameRootTransformDocument = ExtractDocument(sceneText, "--- !u!4 &1878886999");
        string introDocument = ExtractDocument(sceneText, "--- !u!114 &3301000003");
        string chapterManagerDocument = ExtractDocument(sceneText, "--- !u!114 &3301000004");
        string chapter2Document = ExtractDocument(sceneText, "--- !u!114 &3301000006");
        string introCanvasObjectDocument = ExtractDocument(sceneText, "--- !u!1 &1878887140");
        string introCanvasRectDocument = ExtractDocument(sceneText, "--- !u!224 &1878887141");
        string introCanvasDocument = ExtractDocument(sceneText, "--- !u!223 &1878887142");
        string introScalerDocument = ExtractDocument(sceneText, "--- !u!114 &1878887143");
        string introRaycasterDocument = ExtractDocument(sceneText, "--- !u!114 &1878887144");
        string introOverlayObjectDocument = ExtractDocument(sceneText, "--- !u!1 &1878887150");
        string introOverlayRectDocument = ExtractDocument(sceneText, "--- !u!224 &1878887151");
        string introFadeObjectDocument = ExtractDocument(sceneText, "--- !u!1 &1878887160");
        string introFadeRectDocument = ExtractDocument(sceneText, "--- !u!224 &1878887161");
        string introFadeRendererDocument = ExtractDocument(sceneText, "--- !u!222 &1878887162");
        string introFadeImageDocument = ExtractDocument(sceneText, "--- !u!114 &1878887163");
        string introTitleObjectDocument = ExtractDocument(sceneText, "--- !u!1 &1878887170");
        string introTitleRectDocument = ExtractDocument(sceneText, "--- !u!224 &1878887171");
        string introTitleRendererDocument = ExtractDocument(sceneText, "--- !u!222 &1878887172");
        string introTitleTextDocument = ExtractDocument(sceneText, "--- !u!114 &1878887173");
        string sceneRootsDocument = ExtractDocument(sceneText, "--- !u!1660057539 &9223372036854775807");

        Assert.That(managerText, Does.Not.Contain("BootstrapChapterManagerForGameplay"));
        Assert.That(managerText, Does.Not.Contain("ChapterManager_Runtime"));
        Assert.That(managerText, Does.Not.Contain("managerObject.AddComponent<ChapterClock>"));
        Assert.That(managerText, Does.Not.Contain("managerObject.AddComponent<ChapterEventScheduler>"));
        Assert.That(managerText, Does.Not.Contain("managerObject.AddComponent<ChapterIntroUI>"));
        Assert.That(managerText, Does.Not.Contain("managerObject.AddComponent<Chapter1ArrivalController>"));
        Assert.That(managerText, Does.Not.Contain("managerObject.AddComponent<ChapterManager>"));
        Assert.That(managerText, Does.Not.Contain("AddComponent<Chapter2Controller>"));
        Assert.That(managerText, Does.Not.Contain("ResolveChapter2Controller"));
        Assert.That(managerText, Does.Not.Contain("ResolveReferences"));
        Assert.That(managerText, Does.Not.Contain("ResolvePlayerReference"));
        Assert.That(managerText, Does.Not.Contain("FindPlayerInput"));
        Assert.That(managerText, Does.Not.Contain("GameObject.Find"));
        Assert.That(managerText, Does.Not.Contain("FindAnyObjectByType"));
        Assert.That(managerText, Does.Not.Contain("FindObjectsByType"));
        Assert.That(managerText, Does.Not.Contain("Canvas_ChapterDebug"));
        Assert.That(sceneText, Does.Contain("playerInput: {fileID: 81962842}"));
        Assert.That(sceneText, Does.Contain("chapter2Controller: {fileID: 3301000006}"));
        string legacyControllerOverride = "target: {fileID: 7110128061864666233, guid: 3c2a23f8d68b2d05cace0338fba9a1d1, type: 3}\n      propertyPath: m_Enabled\n      value: 0";
        string legacyMovementOverride = "target: {fileID: 7656683542599176262, guid: 3c2a23f8d68b2d05cace0338fba9a1d1, type: 3}\n      propertyPath: m_Enabled\n      value: 0";
        Assert.That(playerInstanceDocument, Does.Contain(legacyControllerOverride));
        Assert.That(playerInstanceDocument, Does.Contain(legacyMovementOverride));
        Assert.That(CountOccurrences(sceneText, legacyControllerOverride), Is.EqualTo(1));
        Assert.That(CountOccurrences(sceneText, legacyMovementOverride), Is.EqualTo(1));
        Assert.That(managerText, Does.Contain("ChapterManager requires its serialized PointClickPlayerMovement."));
        Assert.That(managerText, Does.Contain("ChapterManager requires its serialized Chapter2Controller."));
        Assert.That(CountOccurrences(sceneText, "guid: 97af4a761ae641b1b180d4ae9898b061"), Is.EqualTo(1));
        Assert.That(introText, Does.Contain("[SerializeField] private RectTransform overlayRoot;"));
        Assert.That(introDocument, Does.Contain("m_GameObject: {fileID: 2099709257}"));
        Assert.That(introDocument, Does.Contain("canvas: {fileID: 1878887142}"));
        Assert.That(introDocument, Does.Contain("overlayRoot: {fileID: 1878887151}"));
        Assert.That(introDocument, Does.Contain("fadeImage: {fileID: 1878887163}"));
        Assert.That(introDocument, Does.Contain("titleText: {fileID: 1878887173}"));
        Assert.That(introDocument, Does.Contain("defaultTitle: Act 1"));
        Assert.That(introDocument, Does.Contain("titleHoldSeconds: 2"));
        Assert.That(introDocument, Does.Contain("fadeFromBlackSeconds: 1.5"));
        Assert.That(introDocument, Does.Not.Contain("titleFontSize:"));
        Assert.That(introDocument, Does.Not.Contain("titleColor:"));
        Assert.That(introDocument, Does.Not.Contain("useDedicatedOverlayCanvas:"));
        Assert.That(introDocument, Does.Not.Contain("overlayCanvasObjectName:"));
        Assert.That(introDocument, Does.Not.Contain("overlaySortingOrder:"));
        Assert.That(introDocument, Does.Not.Contain("createRuntimeFallbackIfMissing:"));
        Assert.That(introDocument, Does.Not.Contain("overlayObjectName:"));
        Assert.That(introDocument, Does.Not.Contain("fadeObjectName:"));
        Assert.That(introDocument, Does.Not.Contain("titleObjectName:"));
        Assert.That(chapterManagerDocument, Does.Contain("introUI: {fileID: 3301000003}"));
        Assert.That(chapter2Document, Does.Contain("introUI: {fileID: 3301000003}"));
        Assert.That(gameRootDocument, Does.Not.Contain("- {fileID: 3301000003}"));

        string[] introHeaders =
        {
            "--- !u!1 &1878887140",
            "--- !u!224 &1878887141",
            "--- !u!223 &1878887142",
            "--- !u!114 &1878887143",
            "--- !u!114 &1878887144",
            "--- !u!1 &1878887150",
            "--- !u!224 &1878887151",
            "--- !u!1 &1878887160",
            "--- !u!224 &1878887161",
            "--- !u!222 &1878887162",
            "--- !u!114 &1878887163",
            "--- !u!1 &1878887170",
            "--- !u!224 &1878887171",
            "--- !u!222 &1878887172",
            "--- !u!114 &1878887173"
        };

        for (int i = 0; i < introHeaders.Length; i++)
        {
            Assert.That(CountOccurrences(sceneText, introHeaders[i]), Is.EqualTo(1), introHeaders[i]);
        }

        Assert.That(CountOccurrences(sceneText, "\n--- !u!"), Is.EqualTo(6043));
        Assert.That(CountOccurrences(gameRootTransformDocument, "- {fileID: 1878887141}"), Is.Zero);
        Assert.That(sceneRootsDocument, Does.Not.Contain("1878887140"));
        Assert.That(CountOccurrences(sceneRootsDocument, "- {fileID: 1878887141}"), Is.EqualTo(1));

        Assert.That(introCanvasObjectDocument, Does.Contain(
            "  m_Component:\n" +
            "  - component: {fileID: 1878887141}\n" +
            "  - component: {fileID: 1878887142}\n" +
            "  - component: {fileID: 1878887143}\n" +
            "  - component: {fileID: 1878887144}"));
        Assert.That(introCanvasObjectDocument, Does.Contain("m_Layer: 5"));
        Assert.That(introCanvasObjectDocument, Does.Contain("m_Name: Canvas_ChapterIntroOverlay"));
        Assert.That(introCanvasObjectDocument, Does.Contain("m_IsActive: 1"));
        Assert.That(introCanvasRectDocument, Does.Contain("m_Father: {fileID: 0}"));
        Assert.That(introCanvasRectDocument, Does.Contain("  m_Children:\n  - {fileID: 1878887151}"));
        Assert.That(introCanvasRectDocument, Does.Contain("m_LocalScale: {x: 0, y: 0, z: 0}"));
        Assert.That(introCanvasRectDocument, Does.Contain("m_AnchorMin: {x: 0, y: 0}"));
        Assert.That(introCanvasRectDocument, Does.Contain("m_AnchorMax: {x: 0, y: 0}"));
        Assert.That(introCanvasRectDocument, Does.Contain("m_SizeDelta: {x: 0, y: 0}"));
        Assert.That(introCanvasRectDocument, Does.Contain("m_Pivot: {x: 0, y: 0}"));
        Assert.That(introCanvasDocument, Does.Contain("m_GameObject: {fileID: 1878887140}"));
        Assert.That(introCanvasDocument, Does.Contain("m_RenderMode: 0"));
        Assert.That(introCanvasDocument, Does.Contain("m_OverrideSorting: 0"));
        Assert.That(introCanvasDocument, Does.Contain("m_AdditionalShaderChannelsFlag: 25"));
        Assert.That(introCanvasDocument, Does.Contain("m_SortingOrder: 12000"));
        Assert.That(introScalerDocument, Does.Contain("guid: 0cd44c1031e13a943bb63640046fad76"));
        Assert.That(introScalerDocument, Does.Contain("m_UiScaleMode: 1"));
        Assert.That(introScalerDocument, Does.Contain("m_ReferenceResolution: {x: 1366, y: 768}"));
        Assert.That(introScalerDocument, Does.Contain("m_MatchWidthOrHeight: 0.5"));
        Assert.That(introRaycasterDocument, Does.Contain("guid: dc42784cf147c0c48a680349fa168899"));

        Assert.That(introOverlayObjectDocument, Does.Contain("  m_Component:\n  - component: {fileID: 1878887151}"));
        Assert.That(introOverlayObjectDocument, Does.Contain("m_Layer: 5"));
        Assert.That(introOverlayObjectDocument, Does.Contain("m_Name: ChapterIntroUI_Runtime"));
        Assert.That(introOverlayRectDocument, Does.Contain("m_Father: {fileID: 1878887141}"));
        Assert.That(introOverlayRectDocument, Does.Contain(
            "  m_Children:\n" +
            "  - {fileID: 1878887161}\n" +
            "  - {fileID: 1878887171}"));
        Assert.That(introOverlayRectDocument, Does.Contain("m_AnchorMin: {x: 0, y: 0}"));
        Assert.That(introOverlayRectDocument, Does.Contain("m_AnchorMax: {x: 1, y: 1}"));

        Assert.That(introFadeObjectDocument, Does.Contain(
            "  m_Component:\n" +
            "  - component: {fileID: 1878887161}\n" +
            "  - component: {fileID: 1878887162}\n" +
            "  - component: {fileID: 1878887163}"));
        Assert.That(introFadeObjectDocument, Does.Contain("m_Layer: 5"));
        Assert.That(introFadeObjectDocument, Does.Contain("m_Name: Image_ChapterIntroFade"));
        Assert.That(introFadeRectDocument, Does.Contain("m_Father: {fileID: 1878887151}"));
        Assert.That(introFadeRectDocument, Does.Contain("m_AnchorMin: {x: 0.5, y: 0.5}"));
        Assert.That(introFadeRectDocument, Does.Contain("m_AnchorMax: {x: 0.5, y: 0.5}"));
        Assert.That(introFadeRectDocument, Does.Contain("m_SizeDelta: {x: 10000, y: 10000}"));
        Assert.That(introFadeRendererDocument, Does.Contain("m_GameObject: {fileID: 1878887160}"));
        Assert.That(introFadeImageDocument, Does.Contain("guid: fe87c0e1cc204ed48ad3b37840f39efc"));
        Assert.That(introFadeImageDocument, Does.Contain("m_Color: {r: 0, g: 0, b: 0, a: 1}"));
        Assert.That(introFadeImageDocument, Does.Contain("m_RaycastTarget: 1"));
        Assert.That(introFadeImageDocument, Does.Contain("m_Sprite: {fileID: 0}"));

        Assert.That(introTitleObjectDocument, Does.Contain(
            "  m_Component:\n" +
            "  - component: {fileID: 1878887171}\n" +
            "  - component: {fileID: 1878887172}\n" +
            "  - component: {fileID: 1878887173}"));
        Assert.That(introTitleObjectDocument, Does.Contain("m_Layer: 5"));
        Assert.That(introTitleObjectDocument, Does.Contain("m_Name: Text_ChapterIntroTitle"));
        Assert.That(introTitleRectDocument, Does.Contain("m_Father: {fileID: 1878887151}"));
        Assert.That(introTitleRectDocument, Does.Contain("m_AnchorMin: {x: 0.5, y: 0.5}"));
        Assert.That(introTitleRectDocument, Does.Contain("m_AnchorMax: {x: 0.5, y: 0.5}"));
        Assert.That(introTitleRectDocument, Does.Contain("m_SizeDelta: {x: 900, y: 180}"));
        Assert.That(introTitleRendererDocument, Does.Contain("m_GameObject: {fileID: 1878887170}"));
        Assert.That(introTitleTextDocument, Does.Contain("guid: f4688fdb7df04437aeb418b961361dc5"));
        Assert.That(introTitleTextDocument, Does.Contain("m_Color: {r: 1, g: 1, b: 1, a: 1}"));
        Assert.That(introTitleTextDocument, Does.Contain("m_fontColor: {r: 1, g: 1, b: 1, a: 1}"));
        Assert.That(introTitleTextDocument, Does.Contain("m_RaycastTarget: 0"));
        Assert.That(introTitleTextDocument, Does.Contain("m_text: Act 1"));
        Assert.That(introTitleTextDocument, Does.Contain(
            "m_fontAsset: {fileID: 11400000, guid: 8f586378b4e144a9851e7b34d9b748ee, type: 2}"));
        Assert.That(introTitleTextDocument, Does.Contain(
            "m_sharedMaterial: {fileID: 2180264, guid: 8f586378b4e144a9851e7b34d9b748ee, type: 2}"));
        Assert.That(introTitleTextDocument, Does.Contain("m_fontSize: 72"));
        Assert.That(introTitleTextDocument, Does.Contain("m_fontSizeBase: 72"));
        Assert.That(introTitleTextDocument, Does.Contain("m_HorizontalAlignment: 2"));
        Assert.That(introTitleTextDocument, Does.Contain("m_VerticalAlignment: 512"));
        Assert.That(introTitleTextDocument, Does.Contain("m_TextWrappingMode: 1"));
        Assert.That(introText, Does.Not.Contain("EnsureUI"));
        Assert.That(introText, Does.Not.Contain("EnsureCanvasLayer"));
        Assert.That(introText, Does.Not.Contain("GetOrCreateIntroCanvas"));
        Assert.That(introText, Does.Not.Contain("GameObject.Find"));
        Assert.That(introText, Does.Not.Contain("FindAnyObjectByType"));
        Assert.That(introText, Does.Not.Contain("FindObjectsByType"));
        Assert.That(introText, Does.Not.Contain("Resources.Load"));
        Assert.That(introText, Does.Not.Contain("new GameObject"));
        Assert.That(introText, Does.Not.Contain("AddComponent<"));
        Assert.That(introText, Does.Not.Contain("GetComponent<"));
        Assert.That(introText, Does.Not.Contain("GetComponentsInChildren"));
        Assert.That(introText, Does.Not.Contain("SetParent"));
        Assert.That(introText, Does.Not.Contain("PostProcessSafeCanvasUtility"));
        Assert.That(introText, Does.Not.Contain("createRuntimeFallbackIfMissing"));
        Assert.That(introText, Does.Not.Contain("overlayCanvasObjectName"));
        Assert.That(introText, Does.Not.Contain("overlayObjectName"));
        Assert.That(introText, Does.Not.Contain("fadeObjectName"));
        Assert.That(introText, Does.Not.Contain("titleObjectName"));
        Assert.That(introText, Does.Not.Contain("ConfigureIntroCanvas"));
        Assert.That(introText, Does.Not.Contain("ConfigureOverlayRoot"));
        Assert.That(introText, Does.Not.Contain("ConfigureFadeImage"));
        Assert.That(introText, Does.Not.Contain("ConfigureTitleText"));
        Assert.That(introText, Does.Not.Contain("StretchToParent"));
        Assert.That(introText, Does.Not.Contain("CoverViewport"));
        Assert.That(introText, Does.Not.Contain("SetLayerRecursively"));
        Assert.That(introText, Does.Contain("ChapterIntroUI missing required field: canvas."));
        Assert.That(introText, Does.Contain("ChapterIntroUI missing required field: overlayRoot."));
        Assert.That(introText, Does.Contain("overlayRoot.parent != canvas.transform"));
        Assert.That(introText, Does.Contain("fadeImage.transform.parent != overlayRoot"));
        Assert.That(introText, Does.Contain("titleText.transform.parent != overlayRoot"));

        string chapter2Text = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Controller.cs");
        Assert.That(chapter2Text, Does.Not.Contain("AddComponent<Chapter2InteractionHUD>"));
        Assert.That(chapter2Text, Does.Not.Contain("AddComponent<Chapter2MonsterStingerController>"));
        Assert.That(chapter2Text, Does.Not.Contain("AddComponent<Chapter2GuestPanicController>"));
        Assert.That(chapter2Text, Does.Not.Contain("AddComponent<Chapter2GuestSearchController>"));
    }

    [Test]
    public void Chapter2FeatureControllersAreSerializedOnce()
    {
        string sceneText = File.ReadAllText("Assets/Scenes/Gameplay.unity");
        string stingerDocument = ExtractDocument(sceneText, "--- !u!114 &3301000007");
        string monsterGameObjectDocument = ExtractDocument(sceneText, "--- !u!1 &3700000000");
        string monsterTransformDocument = ExtractDocument(sceneText, "--- !u!224 &3700000001");
        string monsterImageDocument = ExtractDocument(sceneText, "--- !u!114 &3700000003");
        string monsterViolinSourceDocument = ExtractDocument(sceneText, "--- !u!82 &3700000004");
        string monsterViolinBindingDocument = ExtractDocument(sceneText, "--- !u!114 &3700000005");
        string monsterCanvasDocument = ExtractDocument(sceneText, "--- !u!223 &3700000006");
        string runStartGameObjectDocument = ExtractDocument(sceneText, "--- !u!1 &98514616");
        string runStartTransformDocument = ExtractDocument(sceneText, "--- !u!224 &98514617");
        string runStartAnchorDocument = ExtractDocument(sceneText, "--- !u!114 &3600000001");
        string runTargetGameObjectDocument = ExtractDocument(sceneText, "--- !u!1 &382498959");
        string runTargetTransformDocument = ExtractDocument(sceneText, "--- !u!224 &382498960");
        string runTargetAnchorDocument = ExtractDocument(sceneText, "--- !u!114 &3600000002");
        string guestSearchDocument = ExtractDocument(sceneText, "--- !u!114 &3301000009");
        string stingerText = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2MonsterStingerController.cs");
        string guestSearchText = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestSearchController.cs");

        Assert.That(CountOccurrences(sceneText, "guid: 684198ee76c12a66cb4335c3ab64b1bc"), Is.EqualTo(1));
        Assert.That(CountOccurrences(sceneText, "guid: aa4143ddf6de4b6b9b8c1edc0f9e2a31"), Is.EqualTo(1));
        Assert.That(CountOccurrences(sceneText, "guid: 5daaf625b50c2b1048154975a147950a"), Is.EqualTo(1));
        Assert.That(sceneText, Does.Contain("monsterStinger: {fileID: 3301000007}"));
        Assert.That(sceneText, Does.Contain("guestPanic: {fileID: 3301000008}"));
        Assert.That(sceneText, Does.Contain("guestSearch: {fileID: 3301000009}"));
        Assert.That(stingerDocument, Does.Contain("monsterObject: {fileID: 3700000000}"));
        Assert.That(stingerDocument, Does.Not.Contain("monsterObjectName"));
        Assert.That(stingerDocument, Does.Contain("runStart: {fileID: 98514617}"));
        Assert.That(stingerDocument, Does.Contain("runTarget: {fileID: 382498960}"));
        Assert.That(stingerDocument, Does.Contain("navigationManager: {fileID: 1878886997}"));
        Assert.That(stingerDocument, Does.Contain("monsterImage: {fileID: 3700000003}"));
        Assert.That(stingerDocument, Does.Contain("monsterSpriteRenderer: {fileID: 0}"));
        Assert.That(stingerDocument, Does.Contain("monsterOverlayCanvas: {fileID: 3700000006}"));
        Assert.That(stingerDocument, Does.Contain("violinAudioSource: {fileID: 3700000004}"));
        Assert.That(stingerDocument, Does.Contain("violinAudioVolumeBinding: {fileID: 3700000005}"));
        Assert.That(stingerDocument, Does.Contain("violinAudioClip: {fileID: 8300000, guid: 69f06d321e4549cdcad1133332661f6d, type: 3}"));
        Assert.That(stingerDocument, Does.Not.Contain("fallbackViolinClipName"));
        Assert.That(stingerDocument, Does.Not.Contain("monsterRunSprites: []"));
        Assert.That(stingerDocument, Does.Contain(
            "  monsterRunSprites:\n" +
            "  - {fileID: 21300000, guid: 8414d4be92f9485e8f33a1abb721c2fd, type: 3}\n" +
            "  - {fileID: 21300000, guid: 545dbfc1fc754f3fbfc3ba99fa334619, type: 3}\n" +
            "  - {fileID: 21300000, guid: ee2e37acc05b4445ba6cfc7f8e70737e, type: 3}\n" +
            "  - {fileID: 21300000, guid: 432fbf9f626f4b6c84fa80dd3dab01fc, type: 3}\n" +
            "  - {fileID: 21300000, guid: 94976d1632474d90914e011e989f3ae7, type: 3}\n" +
            "  - {fileID: 21300000, guid: f7e820a7807c4c159b8a465ec1909b89, type: 3}\n" +
            "  - {fileID: 21300000, guid: 32ccf6ba47fe4ce19bcb7e3354484363, type: 3}\n" +
            "  - {fileID: 21300000, guid: ebfd9b9fdded4ed6a159c078f21829d3, type: 3}\n"));
        Assert.That(CountOccurrences(stingerDocument, "- {fileID: 21300000, guid:"), Is.EqualTo(8));
        Assert.That(stingerDocument, Does.Not.Contain("monsterRunSpritesResourcePath"));
        Assert.That(stingerDocument, Does.Not.Contain("createPlaceholderMonsterIfMissing"));
        Assert.That(monsterGameObjectDocument, Does.Contain("m_Name: Ch2_Monster"));
        Assert.That(monsterGameObjectDocument, Does.Contain("- component: {fileID: 3700000003}"));
        Assert.That(monsterGameObjectDocument, Does.Contain("- component: {fileID: 3700000004}"));
        Assert.That(monsterGameObjectDocument, Does.Contain("- component: {fileID: 3700000005}"));
        Assert.That(monsterGameObjectDocument, Does.Contain("- component: {fileID: 3700000006}"));
        Assert.That(monsterTransformDocument, Does.Contain("m_Father: {fileID: 2300000006}"));
        Assert.That(monsterTransformDocument, Does.Contain("m_AnchoredPosition: {x: -600, y: -79}"));
        Assert.That(monsterTransformDocument, Does.Contain("m_SizeDelta: {x: 520, y: 435}"));
        Assert.That(monsterImageDocument, Does.Contain("m_GameObject: {fileID: 3700000000}"));
        Assert.That(monsterImageDocument, Does.Contain("m_Sprite: {fileID: 21300000, guid: ec4a2578f6304d97b9d29f7e77436e2c, type: 3}"));
        Assert.That(monsterViolinSourceDocument, Does.Contain("m_GameObject: {fileID: 3700000000}"));
        Assert.That(monsterViolinSourceDocument, Does.Contain("m_Resource: {fileID: 8300000, guid: 69f06d321e4549cdcad1133332661f6d, type: 3}"));
        Assert.That(monsterViolinSourceDocument, Does.Contain("m_PlayOnAwake: 0"));
        Assert.That(monsterViolinSourceDocument, Does.Contain("m_Volume: 1"));
        Assert.That(monsterViolinSourceDocument, Does.Contain("Loop: 1"));
        Assert.That(monsterViolinBindingDocument, Does.Contain("m_GameObject: {fileID: 3700000000}"));
        Assert.That(monsterViolinBindingDocument, Does.Contain("audioSource: {fileID: 3700000004}"));
        Assert.That(monsterViolinBindingDocument, Does.Contain("channel: 1"));
        Assert.That(monsterViolinBindingDocument, Does.Contain("baseVolume: 1"));
        Assert.That(monsterCanvasDocument, Does.Contain("m_GameObject: {fileID: 3700000000}"));
        Assert.That(monsterCanvasDocument, Does.Contain("m_RenderMode: 0"));
        Assert.That(monsterCanvasDocument, Does.Contain("m_OverrideSorting: 1"));
        Assert.That(monsterCanvasDocument, Does.Contain("m_SortingLayerID: -114244515"));
        Assert.That(monsterCanvasDocument, Does.Contain("m_SortingOrder: 10000"));
        Assert.That(CountOccurrences(sceneText, "69f06d321e4549cdcad1133332661f6d"), Is.EqualTo(2));
        Assert.That(runStartGameObjectDocument, Does.Contain("m_Name: Ch2_MonsterRunStart"));
        Assert.That(runStartGameObjectDocument, Does.Contain("- component: {fileID: 98514617}"));
        Assert.That(runStartGameObjectDocument, Does.Contain("- component: {fileID: 3600000001}"));
        Assert.That(runStartTransformDocument, Does.Contain("m_Father: {fileID: 2300000006}"));
        Assert.That(runStartTransformDocument, Does.Contain("m_AnchoredPosition: {x: -600, y: -79}"));
        Assert.That(runStartAnchorDocument, Does.Contain("m_GameObject: {fileID: 98514616}"));
        Assert.That(runStartAnchorDocument, Does.Contain("anchorId: Ch2_MonsterRunStart"));
        Assert.That(runStartAnchorDocument, Does.Contain("roomId: Drawing Room"));
        Assert.That(runTargetGameObjectDocument, Does.Contain("m_Name: Ch2_MonsterFreezeTarget"));
        Assert.That(runTargetGameObjectDocument, Does.Contain("- component: {fileID: 382498960}"));
        Assert.That(runTargetGameObjectDocument, Does.Contain("- component: {fileID: 3600000002}"));
        Assert.That(runTargetTransformDocument, Does.Contain("m_Father: {fileID: 2300000006}"));
        Assert.That(runTargetTransformDocument, Does.Contain("m_AnchoredPosition: {x: -171, y: -81}"));
        Assert.That(runTargetAnchorDocument, Does.Contain("m_GameObject: {fileID: 382498959}"));
        Assert.That(runTargetAnchorDocument, Does.Contain("anchorId: Ch2_MonsterFreezeTarget"));
        Assert.That(runTargetAnchorDocument, Does.Contain("roomId: Drawing Room"));
        Assert.That(sceneText, Does.Not.Contain("m_Name: Chapter2_MonsterPlaceholder_Runtime"));
        Assert.That(stingerText, Does.Not.Contain("FindRoomAnchor"));
        Assert.That(stingerText, Does.Not.Contain("FindSceneMonsterObject"));
        Assert.That(stingerText, Does.Not.Contain("CreatePlaceholderMonster"));
        Assert.That(stingerText, Does.Not.Contain("FindAnyObjectByType"));
        Assert.That(stingerText, Does.Not.Contain("FindObjectsByType"));
        Assert.That(stingerText, Does.Not.Contain("GetComponentInChildren<Image>"));
        Assert.That(stingerText, Does.Not.Contain("GetComponentInChildren<SpriteRenderer>"));
        Assert.That(stingerText, Does.Not.Contain("GameObject.CreatePrimitive"));
        Assert.That(stingerText, Does.Contain("public override void ValidateConfiguration"));
        Assert.That(stingerText, Does.Contain("Chapter2MonsterStingerController requires its serialized RoomNavigationManager."));
        Assert.That(stingerText, Does.Contain("Chapter2MonsterStingerController requires its serialized monster object."));
        Assert.That(stingerText, Does.Contain("Chapter2MonsterStingerController requires a serialized monster Image or SpriteRenderer."));
        Assert.That(stingerText, Does.Contain("Chapter2MonsterStingerController requires its serialized violin AudioSource."));
        Assert.That(stingerText, Does.Contain("Chapter2MonsterStingerController requires its serialized violin AudioClip."));
        Assert.That(stingerText, Does.Contain("Chapter2MonsterStingerController requires its serialized violin volume binding."));
        Assert.That(stingerText, Does.Contain("Chapter2MonsterStingerController requires its serialized monster overlay Canvas."));
        Assert.That(stingerText, Does.Contain("Chapter2MonsterStingerController requires its eight serialized monster run sprites."));
        Assert.That(stingerText, Does.Not.Contain("GetComponent<AudioSource>()"));
        Assert.That(stingerText, Does.Not.Contain("AddComponent<AudioSource>()"));
        Assert.That(stingerText, Does.Not.Contain("GameAudioSettings.EnsureBinding("));
        Assert.That(stingerText, Does.Not.Contain("FindViolinClip"));
        Assert.That(stingerText, Does.Not.Contain("Resources.Load<AudioClip>"));
        Assert.That(stingerText, Does.Not.Contain("AssetDatabase.FindAssets"));
        Assert.That(stingerText, Does.Contain("violinAudioVolumeBinding.Configure("));
        Assert.That(stingerText, Does.Not.Contain("monsterRunSpritesResourcePath"));
        Assert.That(stingerText, Does.Not.Contain("LoadMonsterRunSpritesIfNeeded"));
        Assert.That(stingerText, Does.Not.Contain("Resources.LoadAll<Sprite>"));
        Assert.That(stingerText, Does.Not.Contain("CompareSpritesByName"));
        Assert.That(stingerText, Does.Not.Contain("System.Array.Sort(loadedSprites"));
        Assert.That(stingerText, Does.Not.Contain("GetComponent<Canvas>()"));
        Assert.That(stingerText, Does.Not.Contain("AddComponent<Canvas>()"));
        Assert.That(stingerText, Does.Not.Contain("EnsureMonsterOverlayCanvas"));
        Assert.That(stingerText, Does.Contain("ApplyMonsterOverlaySorting"));
        Assert.That(stingerText, Does.Contain("monsterOverlayCanvas.overrideSorting = true"));
        Assert.That(CountOccurrences(guestSearchDocument, "navigationManager: {fileID: 1878886997}"), Is.EqualTo(1));
        Assert.That(guestSearchText, Does.Not.Contain("ResolveRoomNavigation"));
        Assert.That(guestSearchText, Does.Not.Contain("FindAnyObjectByType<RoomNavigationManager>"));
        Assert.That(guestSearchText, Does.Contain("public override void ValidateConfiguration"));
        Assert.That(guestSearchText, Does.Contain("Chapter2GuestSearchController requires its serialized RoomNavigationManager."));
    }

    [Test]
    public void Chapter2DependenciesAreSerializedWithoutRepairSearches()
    {
        string sceneText = File.ReadAllText("Assets/Scenes/Gameplay.unity");
        string chapter2Text = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Controller.cs");
        string chapter2Document = ExtractDocument(sceneText, "--- !u!114 &3301000006");
        string hostGameObjectDocument = ExtractDocument(sceneText, "--- !u!1 &2099709257");
        string hostTransformDocument = ExtractDocument(sceneText, "--- !u!4 &2099709258");
        string clockGameObjectDocument = ExtractDocument(sceneText, "--- !u!1 &3301000010");
        string clockTransformDocument = ExtractDocument(sceneText, "--- !u!4 &3301000011");
        string clockSourceDocument = ExtractDocument(sceneText, "--- !u!82 &3301000012");
        string clockBindingDocument = ExtractDocument(sceneText, "--- !u!114 &3301000013");

        Assert.That(sceneText, Does.Contain("chapterManager: {fileID: 3301000004}"));
        Assert.That(sceneText, Does.Contain("navigationManager: {fileID: 1878886997}"));
        Assert.That(sceneText, Does.Contain("introUI: {fileID: 3301000003}"));
        Assert.That(sceneText, Does.Contain("chapterClock: {fileID: 3301000001}"));
        Assert.That(sceneText, Does.Contain("playerMovement: {fileID: 81962842}"));
        Assert.That(sceneText, Does.Contain("interactionHUD: {fileID: 3301000005}"));
        Assert.That(sceneText, Does.Contain("monsterStinger: {fileID: 3301000007}"));
        Assert.That(sceneText, Does.Contain("guestPanic: {fileID: 3301000008}"));
        Assert.That(sceneText, Does.Contain("guestSearch: {fileID: 3301000009}"));
        Assert.That(sceneText, Does.Contain("subtitleService: {fileID: 1878886995}"));
        Assert.That(sceneText, Does.Contain("speechService: {fileID: 1878886994}"));
        Assert.That(chapter2Document, Does.Contain("clockStrikeAudioSource: {fileID: 3301000012}"));
        Assert.That(chapter2Document, Does.Contain("clockStrikeVolumeBinding: {fileID: 3301000013}"));
        Assert.That(chapter2Document, Does.Contain("clockStrikeClip: {fileID: 8300000, guid: d7084eafa9124afcbcbf12529e08bc70, type: 3}"));
        Assert.That(hostGameObjectDocument, Does.Not.Contain("3301000012"), "The shared ChapterManager host must not own the clock source that the violin fallback can discover.");
        Assert.That(hostGameObjectDocument, Does.Not.Contain("3301000013"));
        Assert.That(CountOccurrences(hostTransformDocument, "- {fileID: 3301000011}"), Is.EqualTo(1));
        Assert.That(clockGameObjectDocument, Does.Contain("m_Name: Audio_Chapter2ClockStrike"));
        Assert.That(clockGameObjectDocument, Does.Contain("- component: {fileID: 3301000011}"));
        Assert.That(clockGameObjectDocument, Does.Contain("- component: {fileID: 3301000012}"));
        Assert.That(clockGameObjectDocument, Does.Contain("- component: {fileID: 3301000013}"));
        Assert.That(clockTransformDocument, Does.Contain("m_Father: {fileID: 2099709258}"));
        Assert.That(clockSourceDocument, Does.Contain("m_Resource: {fileID: 8300000, guid: d7084eafa9124afcbcbf12529e08bc70, type: 3}"));
        Assert.That(clockSourceDocument, Does.Contain("m_PlayOnAwake: 0"));
        Assert.That(clockSourceDocument, Does.Contain("m_Volume: 0.4"));
        Assert.That(clockSourceDocument, Does.Contain("Loop: 0"));
        Assert.That(clockBindingDocument, Does.Contain("audioSource: {fileID: 3301000012}"));
        Assert.That(clockBindingDocument, Does.Contain("channel: 1"));
        Assert.That(clockBindingDocument, Does.Contain("baseVolume: 0.4"));
        Assert.That(chapter2Document, Does.Not.Contain("clockStrikeClipResourcePath"));
        Assert.That(chapter2Text, Does.Not.Contain("ResolveReferences"));
        Assert.That(chapter2Text, Does.Not.Contain("FindAnyObjectByType"));
        Assert.That(chapter2Text, Does.Not.Contain("GameObject.Find(\"Player\")"));
        Assert.That(chapter2Text, Does.Not.Contain("GetComponent<Chapter"));
        Assert.That(chapter2Text, Does.Not.Contain("GetComponent<PointClickPlayerMovement>"));
        Assert.That(chapter2Text, Does.Not.Contain("ResolveClockStrikeClip"));
        Assert.That(chapter2Text, Does.Not.Contain("CreateRuntimeClockStrikeClip"));
        Assert.That(chapter2Text, Does.Not.Contain("clockStrikeClipResourcePath"));
        Assert.That(chapter2Text, Does.Not.Contain("Resources.Load<AudioClip>"));
        Assert.That(chapter2Text, Does.Not.Contain("AddComponent<AudioSource>"));
        Assert.That(chapter2Text, Does.Not.Contain("GameAudioSettings.EnsureBinding(clockStrikeAudioSource"));
        Assert.That(chapter2Text, Does.Contain("clockStrikeVolumeBinding.Configure(clockStrikeAudioSource, GameAudioChannel.GameSounds, baseVolume)"));
        Assert.That(chapter2Text, Does.Contain("public override void ValidateConfiguration"));
        Assert.That(chapter2Text, Does.Contain("RegisterRoomChangeHandler();"));
    }

    [Test]
    public void Chapter1DataOwnersAreSerializedWithoutFallbacks()
    {
        string sceneText = File.ReadAllText("Assets/Scenes/Gameplay.unity");
        string chapter1Document = ExtractDocument(sceneText, "--- !u!114 &3302000001");
        string hangerDocument = ExtractDocument(sceneText, "--- !u!1 &1592234992");
        string hangerTransformDocument = ExtractDocument(sceneText, "--- !u!4 &1592234993");
        string hangerRendererDocument = ExtractDocument(sceneText, "--- !u!212 &1592234994");
        string hangerColliderDocument = ExtractDocument(sceneText, "--- !u!61 &1592234996");
        string hangerActionDocument = ExtractDocument(sceneText, "--- !u!114 &1592234995");
        string frontDoorObjectDocument = ExtractDocument(sceneText, "--- !u!1 &1180734296");
        string frontDoorTransformDocument = ExtractDocument(sceneText, "--- !u!4 &1180734297");
        string frontDoorRendererDocument = ExtractDocument(sceneText, "--- !u!212 &1180734298");
        string frontDoorColliderDocument = ExtractDocument(sceneText, "--- !u!61 &1180734299");
        string frontDoorActionDocument = ExtractDocument(sceneText, "--- !u!114 &1180734300");
        string pantryPropsTransformDocument = ExtractDocument(sceneText, "--- !u!4 &3503000001");
        string serializedClosetDocument = ExtractDocument(sceneText, "--- !u!114 &3303000001");

        Assert.That(sceneText, Does.Contain("guestRoomScaleApplier: {fileID: 86244178}"));
        Assert.That(sceneText, Does.Contain("calibration: {fileID: 1844861547}"));
        Assert.That(sceneText, Does.Contain("butlerScaleSource: {fileID: 81962842}"));
        Assert.That(chapter1Document, Does.Contain("coatCloset: {fileID: 3303000001}"));
        Assert.That(chapter1Document, Does.Contain("closetPoint: {fileID: 1592234993}"));
        Assert.That(chapter1Document, Does.Contain("chapterManager: {fileID: 3301000004}"));
        Assert.That(chapter1Document, Does.Contain("eventScheduler: {fileID: 3301000002}"));
        Assert.That(chapter1Document, Does.Contain("chapterClock: {fileID: 3301000001}"));
        Assert.That(chapter1Document, Does.Contain("cameraManager: {fileID: 2050006783}"));
        Assert.That(chapter1Document, Does.Contain("navigationManager: {fileID: 1878886997}"));
        Assert.That(chapter1Document, Does.Contain("playerMovement: {fileID: 81962842}"));
        Assert.That(chapter1Document, Does.Contain("playerButlerReference: {fileID: 0}"));
        Assert.That(chapter1Document, Does.Contain("frontDoorSceneAction: {fileID: 1180734300}"));
        Assert.That(chapter1Document, Does.Not.Contain("grandfatherClock:"));
        Assert.That(chapter1Document, Does.Contain("guestFootstepCatalog: {fileID: 11400000, guid: 0e780686c6653db1a1c74916a591d484, type: 2}"));
        Assert.That(chapter1Document, Does.Not.Contain("guestFootstepCatalogResourcePath"));
        Assert.That(chapter1Document, Does.Contain("guestEntranceSpawnPlacemark: {fileID: 3501000027}"));
        Assert.That(chapter1Document, Does.Contain("drawingRoomDoorTarget: {fileID: 3501000021}"));
        Assert.That(chapter1Document, Does.Contain("entryRoomContent: {fileID: 2102000002}"));
        Assert.That(chapter1Document, Does.Contain("drawingRoomContent: {fileID: 2300000007}"));
        Assert.That(chapter1Document, Does.Contain(
            "  drawingRoomGuestPoints:\n" +
            "  - {fileID: 3502000101}\n" +
            "  - {fileID: 3502000104}\n" +
            "  - {fileID: 3502000107}\n" +
            "  - {fileID: 3502000110}\n" +
            "  - {fileID: 3502000113}\n" +
            "  - {fileID: 3502000116}\n" +
            "  - {fileID: 3502000119}\n" +
            "  - {fileID: 3502000122}\n"));
        Assert.That(chapter1Document, Does.Not.Contain("drawingRoomSeat01:"));
        Assert.That(chapter1Document, Does.Not.Contain("drawingRoomSeat02:"));
        Assert.That(chapter1Document, Does.Not.Contain("drawingRoomSeat03:"));
        Assert.That(chapter1Document, Does.Not.Contain("drawingRoomSeatSpacing:"));
        Assert.That(hangerDocument, Does.Contain("m_Name: entrance_coat_hanger_0"));
        Assert.That(hangerDocument, Does.Contain("- component: {fileID: 1592234993}"));
        Assert.That(hangerDocument, Does.Contain("- component: {fileID: 1592234994}"));
        Assert.That(hangerDocument, Does.Contain(
            "  - component: {fileID: 1592234996}\n" +
            "  - component: {fileID: 1592234995}\n" +
            "  - component: {fileID: 3303000001}"));
        Assert.That(hangerTransformDocument, Does.Contain("m_LocalPosition: {x: -255.00697, y: -12.514666, z: -5717.36}"));
        Assert.That(hangerTransformDocument, Does.Contain("m_LocalScale: {x: 30.161037, y: 23.970196, z: 63.526222}"));
        Assert.That(hangerTransformDocument, Does.Contain("m_Father: {fileID: 567115834}"));
        Assert.That(hangerRendererDocument, Does.Contain("m_Materials:\n  - {fileID: 2100000, guid: a97c105638bdf8b4a8650670310a4cd3, type: 2}"));
        Assert.That(hangerRendererDocument, Does.Contain("m_SortingLayerID: -114244515"));
        Assert.That(hangerRendererDocument, Does.Contain("m_SortingOrder: 0"));
        Assert.That(hangerRendererDocument, Does.Contain("m_Sprite: {fileID: 1166796266648169557, guid: 60c34e6293838a6c7988f33040dad54d, type: 3}"));
        Assert.That(hangerColliderDocument, Does.Contain("m_GameObject: {fileID: 1592234992}"));
        Assert.That(hangerColliderDocument, Does.Contain("m_IsTrigger: 1"));
        Assert.That(hangerColliderDocument, Does.Contain("m_Offset: {x: 0, y: 0}"));
        Assert.That(hangerColliderDocument, Does.Contain("m_Size: {x: 4.41, y: 9.79}"));
        Assert.That(hangerColliderDocument, Does.Contain("m_EdgeRadius: 0"));
        Assert.That(hangerActionDocument, Does.Contain("m_GameObject: {fileID: 1592234992}"));
        Assert.That(hangerActionDocument, Does.Contain("actionType: 1"));
        Assert.That(hangerActionDocument, Does.Contain("arrivalController: {fileID: 3302000001}"));
        Assert.That(hangerActionDocument, Does.Not.Contain("clockInteraction:"));
        Assert.That(hangerActionDocument, Does.Contain("isActionAvailable: 1"));
        Assert.That(pantryPropsTransformDocument, Does.Contain("m_Children: []"));
        Assert.That(sceneText, Does.Not.Contain("&3503000002"));
        Assert.That(sceneText, Does.Not.Contain("&3503000003"));
        Assert.That(sceneText, Does.Not.Contain("&3503000004"));
        Assert.That(sceneText, Does.Not.Contain("&3503000005"));
        Assert.That(sceneText, Does.Not.Contain("&3503000006"));
        Assert.That(sceneText, Does.Not.Contain("&3503000007"));
        Assert.That(sceneText, Does.Not.Contain("&3503000008"));
        Assert.That(sceneText, Does.Not.Contain("fileID: 3503000002"));
        Assert.That(sceneText, Does.Not.Contain("fileID: 3503000003"));
        Assert.That(sceneText, Does.Not.Contain("fileID: 3503000004"));
        Assert.That(sceneText, Does.Not.Contain("fileID: 3503000005"));
        Assert.That(sceneText, Does.Not.Contain("fileID: 3503000006"));
        Assert.That(sceneText, Does.Not.Contain("fileID: 3503000007"));
        Assert.That(sceneText, Does.Not.Contain("fileID: 3503000008"));
        Assert.That(serializedClosetDocument, Does.Contain("m_GameObject: {fileID: 1592234992}"));
        Assert.That(frontDoorObjectDocument, Does.Contain("m_Name: Door_answer_trigger"));
        Assert.That(frontDoorObjectDocument, Does.Contain(
            "  - component: {fileID: 1180734297}\n" +
            "  - component: {fileID: 1180734298}\n" +
            "  - component: {fileID: 1180734299}\n" +
            "  - component: {fileID: 1180734300}"));
        Assert.That(frontDoorTransformDocument, Does.Contain("m_LocalPosition: {x: -7.216162, y: -13.4132805, z: -7456.425}"));
        Assert.That(frontDoorTransformDocument, Does.Contain("m_LocalScale: {x: 107.62186, y: 163.37209, z: 82.84917}"));
        Assert.That(frontDoorTransformDocument, Does.Contain("m_Father: {fileID: 567115834}"));
        Assert.That(frontDoorRendererDocument, Does.Contain("m_Sprite: {fileID: 7482667652216324306, guid: 311925a002f4447b3a28927169b83ea6, type: 3}"));
        Assert.That(frontDoorRendererDocument, Does.Contain("m_SortingLayerID: 1040854321"));
        Assert.That(frontDoorRendererDocument, Does.Contain("m_SortingOrder: 20"));
        Assert.That(frontDoorColliderDocument, Does.Contain("m_IsTrigger: 1"));
        Assert.That(frontDoorColliderDocument, Does.Contain("m_Offset: {x: 0, y: 0}"));
        Assert.That(frontDoorColliderDocument, Does.Contain("m_Size: {x: 1, y: 1}"));
        Assert.That(frontDoorActionDocument, Does.Contain("actionType: 0"));
        Assert.That(frontDoorActionDocument, Does.Contain("arrivalController: {fileID: 3302000001}"));
        Assert.That(frontDoorActionDocument, Does.Not.Contain("clockInteraction:"));
        Assert.That(sceneText, Does.Not.Contain("c6da9f56f65d9988ff5f7da0f8e59fb0"));
        Assert.That(File.Exists("Assets/Scripts/Story/GrandfatherClockInteraction.cs"), Is.False);
        Assert.That(File.Exists("Assets/Scripts/Story/GrandfatherClockInteraction.cs.meta"), Is.False);

        string chapter1Text = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs");
        string chapter1ActionText = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1SceneAction.cs");
        string chapter1HudText = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1InteractionHUD.cs");
        string applierText = File.ReadAllText("Assets/Scripts/Characters/GuestRoomScaleApplier.cs");
        Assert.That(chapter1Text, Does.Not.Contain("GuestRoomScaleApplier.EnsureInScene"));
        Assert.That(chapter1Text, Does.Not.Contain("new GameObject(\"GuestRoomScaleCalibration\")"));
        Assert.That(chapter1Text, Does.Not.Contain("AddComponent<GuestRoomScaleCalibration>"));
        Assert.That(applierText, Does.Not.Contain("EnsureInScene"));
        Assert.That(applierText, Does.Not.Contain("AddComponent<GuestRoomScaleApplier>"));
        Assert.That(applierText, Does.Not.Contain("FindAnyObjectByType<GuestRoomScaleCalibration>"));
        Assert.That(applierText, Does.Contain("AddComponent<GuestScaleParticipant>"));
        Assert.That(chapter1Text, Does.Not.Contain("EntranceCoatHangerName"));
        Assert.That(chapter1Text, Does.Not.Contain("EnsureEntranceCoatHanger"));
        Assert.That(chapter1Text, Does.Not.Contain("ConfigureAuthoredCoatHangerObject"));
        Assert.That(chapter1Text, Does.Not.Contain("EnsureCoatHangerCollider"));
        Assert.That(chapter1Text, Does.Not.Contain("GetCoatHangerColliderSize"));
        Assert.That(chapter1Text, Does.Not.Contain("CoatHangerFallbackColliderSize"));
        Assert.That(chapter1Text, Does.Not.Contain("AddComponent<CoatCloset>"));
        Assert.That(chapter1Text, Does.Not.Contain("coatHangerObject.AddComponent<Chapter1SceneAction>"));
        Assert.That(chapter1Text, Does.Not.Contain("coatHangerObject.AddComponent<BoxCollider2D>"));
        Assert.That(chapter1Text, Does.Not.Contain("FindObjectsByType<CoatCloset>"));
        Assert.That(chapter1Text, Does.Not.Contain("FindPropAnchor"));
        Assert.That(chapter1Text, Does.Not.Contain("IsUnderNamedTransform"));
        Assert.That(chapter1Text, Does.Contain("Chapter1ArrivalController requires its serialized Entrance coat closet."));
        Assert.That(chapter1Text, Does.Contain("Chapter1ArrivalController requires its serialized Entrance closet approach point."));
        Assert.That(chapter1Text, Does.Not.Contain("FindAnyObjectByType<ChapterManager>"));
        Assert.That(chapter1Text, Does.Not.Contain("FindAnyObjectByType<ChapterClock>"));
        Assert.That(chapter1Text, Does.Not.Contain("FindAnyObjectByType<ChapterEventScheduler>"));
        Assert.That(chapter1Text, Does.Not.Contain("FindAnyObjectByType<RoomNavigationManager>"));
        Assert.That(chapter1Text, Does.Not.Contain("FindAnyObjectByType<CameraManager>"));
        Assert.That(chapter1Text, Does.Not.Contain("GameObject.Find(\"Player\")"));
        Assert.That(chapter1Text, Does.Not.Contain("FindPlayerMovement"));
        Assert.That(chapter1Text, Does.Contain("playerButlerReference = playerMovement != null ? playerMovement.gameObject : null"));
        Assert.That(chapter1Text, Does.Not.Contain("chapterManager = manager"));
        Assert.That(chapter1Text, Does.Contain("AcceptsManagerCommandFrom(manager)"));
        Assert.That(chapter1Text, Does.Contain("Chapter1ArrivalController rejected a command from a different ChapterManager."));
        Assert.That(chapter1Text, Does.Contain("[SerializeField] private Chapter1SceneAction frontDoorSceneAction;"));
        Assert.That(chapter1Text, Does.Contain("frontDoorSceneAction != null"));
        Assert.That(chapter1Text, Does.Contain("Chapter1ArrivalController requires its serialized front-door action."));
        Assert.That(chapter1Text, Does.Contain("frontDoorSceneAction.IsConfiguredFor(Chapter1SceneActionType.FrontDoor, this)"));
        Assert.That(chapter1Text, Does.Contain("!frontDoorCollider.enabled || !frontDoorCollider.isTrigger"));
        Assert.That(chapter1Text, Does.Contain("ConfigureFrontDoorAction();"));
        Assert.That(chapter1Text, Does.Contain("frontDoorSceneAction.Initialize(Chapter1SceneActionType.FrontDoor, this);"));
        Assert.That(chapter1Text, Does.Contain("interactionHUD.Initialize(this);"));
        Assert.That(chapter1Text, Does.Not.Contain("grandfatherClock"));
        Assert.That(chapter1Text, Does.Not.Contain("FindGameObjectByNormalizedName"));
        Assert.That(chapter1Text, Does.Not.Contain("AddComponent<GrandfatherClockInteraction>"));
        Assert.That(chapter1ActionText, Does.Not.Contain("GrandfatherClock"));
        Assert.That(chapter1ActionText, Does.Not.Contain("clockInteraction"));
        Assert.That(chapter1ActionText, Does.Not.Contain("HoverIcon.Inspect"));
        Assert.That(chapter1HudText, Does.Not.Contain("clockInteraction"));
        Assert.That(chapter1HudText, Does.Not.Contain("chapterClock"));
        Assert.That(chapter1HudText, Does.Not.Contain("Button_InspectClock"));
        Assert.That(chapter1Text, Does.Not.Contain("DoorAnswerTriggerName"));
        Assert.That(chapter1Text, Does.Not.Contain("FindDoorAnswerTriggerObject"));
        Assert.That(chapter1Text, Does.Not.Contain("CreateDoorAnswerTriggerFallback"));
        Assert.That(chapter1Text, Does.Not.Contain("EnsureDoorAnswerTriggerAction"));
        Assert.That(chapter1Text, Does.Not.Contain("EnsureDoorAnswerTriggerCanReceiveClicks"));
        Assert.That(chapter1Text, Does.Not.Contain("GetDoorAnswerTriggerColliderSize"));
        Assert.That(chapter1Text, Does.Not.Contain("if (actionType == Chapter1SceneActionType.FrontDoor)"));
        Assert.That(chapter1Text, Does.Not.Contain("Resources.Load<GuestFootstepCatalog>"));
        Assert.That(chapter1Text, Does.Not.Contain("guestFootstepCatalogResourcePath"));
        Assert.That(chapter1Text, Does.Not.Contain("ResolveGuestFootstepCatalog"));
        Assert.That(chapter1Text, Does.Not.Contain("ResolveAnchors"));
        Assert.That(chapter1Text, Does.Not.Contain("FindAnchor("));
        Assert.That(chapter1Text, Does.Not.Contain("FindDrawingRoomGuestPoint"));
        Assert.That(chapter1Text, Does.Not.Contain("FindObjectsByType<RoomContentGroup>"));
        Assert.That(chapter1Text, Does.Not.Contain("FindObjectsByType<DoorTriggerNavigation>"));
        Assert.That(chapter1Text, Does.Not.Contain("TryGetGrandEntranceDrawingRoomDoorPosition"));
        Assert.That(chapter1Text, Does.Not.Contain("DrawingRoomSeat_Runtime_"));
        Assert.That(chapter1Text, Does.Not.Contain("drawingRoomSeat01"));
        Assert.That(chapter1Text, Does.Not.Contain("drawingRoomSeat02"));
        Assert.That(chapter1Text, Does.Not.Contain("drawingRoomSeat03"));
        Assert.That(chapter1Text, Does.Contain("return drawingRoomGuestPoints[guestIndex];"));
        Assert.That(chapter1Text, Does.Contain("return drawingRoomDoorTarget != null"));
        Assert.That(chapter1Text, Does.Contain("return entryRoomContent;"));
        Assert.That(chapter1Text, Does.Contain("Chapter1ArrivalController requires its serialized guest footstep catalog."));
        Assert.That(chapter1Text, Does.Contain("Chapter1ArrivalController requires its serialized Entrance room-content owner."));
        Assert.That(chapter1Text, Does.Contain("Chapter1ArrivalController requires exactly eight serialized Drawing Room guest points."));
        Assert.That(chapter1Text, Does.Contain("Drawing Room guest point slot {i + 1} must reference ordered RoomAnchor"));
        Assert.That(chapter1Text, Does.Contain("string.Equals(guestPointAnchor.AnchorId, expectedAnchorId, StringComparison.Ordinal)"));
        Assert.That(chapter1Text, Does.Contain("ValidateConfiguration(configurationReport);"));
        Assert.That(chapter1Text, Does.Contain("Chapter1 startup configuration: {message.Message}"));
        Assert.That(chapter1Text, Does.Contain("Chapter1ArrivalController requires its serialized ChapterManager."));
        Assert.That(chapter1Text, Does.Contain("Chapter1ArrivalController requires its serialized ChapterClock."));
        Assert.That(chapter1Text, Does.Contain("Chapter1ArrivalController requires its serialized ChapterEventScheduler."));
        Assert.That(chapter1Text, Does.Contain("Chapter1ArrivalController requires its serialized CameraManager."));
        Assert.That(chapter1Text, Does.Contain("Chapter1ArrivalController requires its serialized RoomNavigationManager."));
        Assert.That(chapter1Text, Does.Contain("Chapter1ArrivalController requires its serialized Player movement owner."));
    }

    [Test]
    public void Chapter1HudAndDoorbellOwnersAreSerializedOnce()
    {
        string sceneText = File.ReadAllText("Assets/Scenes/Gameplay.unity");
        string hostDocument = ExtractDocument(sceneText, "--- !u!1 &1696549391");
        string controllerDocument = ExtractDocument(sceneText, "--- !u!114 &3302000001");
        string doorbellDocument = ExtractDocument(sceneText, "--- !u!114 &3302000003");
        string sourceDocument = ExtractDocument(sceneText, "--- !u!82 &3302000004");
        string bindingDocument = ExtractDocument(sceneText, "--- !u!114 &3302000005");
        string gameRootDocument = ExtractDocument(sceneText, "--- !u!114 &1878886998");
        string gameRootTransformDocument = ExtractDocument(sceneText, "--- !u!4 &1878886999");
        string gameTimeObjectDocument = ExtractDocument(sceneText, "--- !u!1 &1878887120");
        string gameTimeRectDocument = ExtractDocument(sceneText, "--- !u!224 &1878887121");
        string gameTimeCanvasDocument = ExtractDocument(sceneText, "--- !u!223 &1878887122");
        string gameTimeScalerDocument = ExtractDocument(sceneText, "--- !u!114 &1878887123");
        string gameTimeHudDocument = ExtractDocument(sceneText, "--- !u!114 &1878887125");
        string gameTimeTextObjectDocument = ExtractDocument(sceneText, "--- !u!1 &1878887130");
        string gameTimeTextRectDocument = ExtractDocument(sceneText, "--- !u!224 &1878887131");
        string gameTimeTextDocument = ExtractDocument(sceneText, "--- !u!114 &1878887133");
        string gameTimeShadowDocument = ExtractDocument(sceneText, "--- !u!114 &1878887134");

        Assert.That(CountOccurrences(sceneText, "guid: a7a7a747ac7ae2fb48c9d60608ca3dc9"), Is.EqualTo(1));
        Assert.That(sceneText, Does.Contain("interactionHUD: {fileID: 3302000002}"));
        Assert.That(sceneText, Does.Contain("- component: {fileID: 3302000002}"));
        Assert.That(CountOccurrences(sceneText, "guid: 4b5410ab5e584743be969413e655ecb4"), Is.EqualTo(1));
        Assert.That(controllerDocument, Does.Contain("doorbellSystem: {fileID: 3302000003}"));
        Assert.That(hostDocument, Does.Contain(
            "  - component: {fileID: 3302000002}\n" +
            "  - component: {fileID: 3302000003}\n" +
            "  - component: {fileID: 3302000004}\n" +
            "  - component: {fileID: 3302000005}"));
        Assert.That(doorbellDocument, Does.Contain("m_GameObject: {fileID: 1696549391}"));
        Assert.That(doorbellDocument, Does.Contain("chapterClock: {fileID: 3301000001}"));
        Assert.That(doorbellDocument, Does.Contain("audioSource: {fileID: 3302000004}"));
        Assert.That(doorbellDocument, Does.Contain("audioVolumeBinding: {fileID: 3302000005}"));
        Assert.That(doorbellDocument, Does.Contain("doorbellClip: {fileID: 8300000, guid: 67dc6970d473422a86e0c071ef23abd1, type: 3}"));
        Assert.That(doorbellDocument, Does.Not.Contain("doorbellClipResourcePath:"));
        Assert.That(sourceDocument, Does.Contain("m_GameObject: {fileID: 1696549391}"));
        Assert.That(sourceDocument, Does.Contain("m_Resource: {fileID: 0}"));
        Assert.That(sourceDocument, Does.Contain("m_PlayOnAwake: 0"));
        Assert.That(sourceDocument, Does.Contain("m_Volume: 1"));
        Assert.That(sourceDocument, Does.Contain("Loop: 0"));
        Assert.That(sourceDocument, Does.Contain("Pan2D: 0"));
        Assert.That(bindingDocument, Does.Contain("audioSource: {fileID: 3302000004}"));
        Assert.That(bindingDocument, Does.Contain("channel: 1"));
        Assert.That(bindingDocument, Does.Contain("baseVolume: 1"));

        string chapter1Text = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs");
        string doorbellText = File.ReadAllText("Assets/Scripts/Story/DoorbellSystem.cs");
        string chapter1ActionText = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1SceneAction.cs");
        string installerText = File.ReadAllText("Assets/_Chateau/Editor/Architecture/GameRootInstaller.cs");
        string gameTimeHudText = File.ReadAllText("Assets/_Chateau/Runtime/UI/GameTimeHUD.cs");
        string gameTimeHudMetaText = File.ReadAllText("Assets/_Chateau/Runtime/UI/GameTimeHUD.cs.meta");
        Assert.That(chapter1Text, Does.Not.Contain("FindAnyObjectByType<Chapter1InteractionHUD>"));
        Assert.That(chapter1Text, Does.Not.Contain("AddComponent<Chapter1InteractionHUD>"));
        Assert.That(chapter1Text, Does.Not.Contain("createRuntimeHud"));
        Assert.That(chapter1Text, Does.Contain("Chapter1ArrivalController requires its serialized DoorbellSystem."));
        Assert.That(chapter1Text, Does.Contain("doorbellSystem.IsConfiguredFor(gameObject, chapterClock)"));
        Assert.That(doorbellText, Does.Contain("public void ValidateConfiguration"));
        Assert.That(doorbellText, Does.Contain("DoorbellSystem requires its serialized imported doorbell clip."));
        Assert.That(chapter1Text, Does.Not.Contain("FindAnyObjectByType<DoorbellSystem>"));
        Assert.That(chapter1Text, Does.Not.Contain("AddComponent<DoorbellSystem>"));
        Assert.That(doorbellText, Does.Not.Contain("FindAnyObjectByType<ChapterClock>"));
        Assert.That(doorbellText, Does.Not.Contain("GetComponent<AudioSource>"));
        Assert.That(doorbellText, Does.Not.Contain("AddComponent<AudioSource>"));
        Assert.That(doorbellText, Does.Not.Contain("GameAudioSettings.EnsureBinding"));
        Assert.That(doorbellText, Does.Not.Contain("Resources.Load<AudioClip>"));
        Assert.That(doorbellText, Does.Not.Contain("AudioClip.Create"));
        Assert.That(doorbellText, Does.Not.Contain("doorbellClipResourcePath"));
        Assert.That(doorbellText, Does.Contain("audioVolumeBinding.Configure(audioSource, GameAudioChannel.GameSounds, 1f)"));
        Assert.That(CountOccurrences(sceneText, "guid: 7a33ae09185ce66224eb1fc576eef96d"), Is.EqualTo(2));
        Assert.That(sceneText, Does.Not.Contain("createRuntimeClickTargets:"));
        Assert.That(chapter1Text, Does.Not.Contain("Chapter1_ClickTarget_DrawingRoomExit"));
        Assert.That(chapter1Text, Does.Not.Contain("EnsureSceneActionTargets"));
        Assert.That(chapter1Text, Does.Not.Contain("RemoveClickTarget"));
        Assert.That(chapter1Text, Does.Not.Contain("CreateClickTarget"));
        Assert.That(chapter1Text, Does.Not.Contain("runtimeCoatSprite"));
        Assert.That(chapter1Text, Does.Not.Contain("GetRuntimeCoatSprite"));
        Assert.That(chapter1Text, Does.Not.Contain("TryCompleteChapterFromDrawingRoomExit"));
        Assert.That(chapter1ActionText, Does.Not.Contain("DrawingRoomExit"));
        Assert.That(gameTimeHudMetaText, Does.Contain("guid: 12bd9a3afa94b05b1e9ce52146c9c7f4"));
        Assert.That(File.Exists("Assets/Scripts/Story/ChapterTimeSettingsUI.cs"), Is.False);
        Assert.That(File.Exists("Assets/Scripts/Story/ChapterTimeSettingsUI.cs.meta"), Is.False);
        Assert.That(chapter1Text, Does.Not.Contain("[FormerlySerializedAs(\"timeSettingsUI\")]"));
        Assert.That(controllerDocument, Does.Not.Contain("timeSettingsUI:"));
        Assert.That(controllerDocument, Does.Not.Contain("gameTimeHUD:"));
        Assert.That(CountOccurrences(sceneText, "guid: 12bd9a3afa94b05b1e9ce52146c9c7f4"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRootDocument, "- {fileID: 1878887125}"), Is.EqualTo(1));
        Assert.That(gameRootTransformDocument, Does.Contain("- {fileID: 1878887121}"));
        Assert.That(gameTimeObjectDocument, Does.Contain("m_Name: Canvas_GameTimeHUD"));
        Assert.That(gameTimeObjectDocument, Does.Contain("m_Layer: 0"));
        Assert.That(gameTimeObjectDocument, Does.Contain("- component: {fileID: 1878887125}"));
        Assert.That(gameTimeRectDocument, Does.Contain("m_Father: {fileID: 1878886999}"));
        Assert.That(gameTimeRectDocument, Does.Contain("- {fileID: 1878887131}"));
        Assert.That(gameTimeCanvasDocument, Does.Contain("m_RenderMode: 0"));
        Assert.That(gameTimeCanvasDocument, Does.Contain("m_SortingOrder: 9000"));
        Assert.That(gameTimeScalerDocument, Does.Contain("m_UiScaleMode: 1"));
        Assert.That(gameTimeScalerDocument, Does.Contain("m_ReferenceResolution: {x: 1366, y: 768}"));
        Assert.That(gameTimeScalerDocument, Does.Contain("m_MatchWidthOrHeight: 0.5"));
        Assert.That(gameTimeHudDocument, Does.Contain("m_GameObject: {fileID: 1878887120}"));
        Assert.That(gameTimeHudDocument, Does.Contain("m_EditorClassIdentifier: Assembly-CSharp::Chateau.UI.GameTimeHUD"));
        Assert.That(gameTimeHudDocument, Does.Contain("chapterClock: {fileID: 3301000001}"));
        Assert.That(gameTimeHudDocument, Does.Contain("canvas: {fileID: 1878887122}"));
        Assert.That(gameTimeHudDocument, Does.Contain("clockText: {fileID: 1878887133}"));
        Assert.That(gameTimeHudDocument, Does.Contain("clockShadow: {fileID: 1878887134}"));
        Assert.That(gameTimeTextObjectDocument, Does.Contain("m_Name: Text_CurrentGameTime"));
        Assert.That(gameTimeTextObjectDocument, Does.Contain("m_Layer: 0"));
        Assert.That(gameTimeTextRectDocument, Does.Contain("m_Father: {fileID: 1878887121}"));
        Assert.That(gameTimeTextRectDocument, Does.Contain("m_AnchorMin: {x: 0, y: 0}"));
        Assert.That(gameTimeTextRectDocument, Does.Contain("m_AnchorMax: {x: 0, y: 0}"));
        Assert.That(gameTimeTextRectDocument, Does.Contain("m_AnchoredPosition: {x: 18, y: 18}"));
        Assert.That(gameTimeTextRectDocument, Does.Contain("m_SizeDelta: {x: 220, y: 36}"));
        Assert.That(gameTimeTextRectDocument, Does.Contain("m_Pivot: {x: 0, y: 0}"));
        Assert.That(gameTimeTextDocument, Does.Contain("m_RaycastTarget: 0"));
        Assert.That(gameTimeTextDocument, Does.Contain("m_fontAsset: {fileID: 11400000, guid: 8f586378b4e144a9851e7b34d9b748ee, type: 2}"));
        Assert.That(gameTimeTextDocument, Does.Contain("m_sharedMaterial: {fileID: 2180264, guid: 8f586378b4e144a9851e7b34d9b748ee, type: 2}"));
        Assert.That(gameTimeTextDocument, Does.Contain("m_HorizontalAlignment: 1"));
        Assert.That(gameTimeTextDocument, Does.Contain("m_VerticalAlignment: 1024"));
        Assert.That(gameTimeTextDocument, Does.Contain("m_TextWrappingMode: 0"));
        Assert.That(gameTimeShadowDocument, Does.Contain("m_EffectColor: {r: 0, g: 0, b: 0, a: 0.85}"));
        Assert.That(gameTimeShadowDocument, Does.Contain("m_EffectDistance: {x: 2, y: -2}"));
        Assert.That(gameTimeShadowDocument, Does.Contain("m_UseGraphicAlpha: 1"));
        Assert.That(sceneText, Does.Not.Contain("m_Name: Canvas_ChapterTimeSettings"));
        Assert.That(chapter1Text, Does.Not.Contain("GameTimeHUD"));
        Assert.That(chapter1Text, Does.Not.Contain("gameTimeHUD"));
        Assert.That(chapter1Text, Does.Not.Contain("Chateau.UI"));
        Assert.That(chapter1Text, Does.Not.Contain("ResolveStoryHelpers"));
        Assert.That(gameTimeHudText, Does.Contain("[SerializeField] private global::ChapterClock chapterClock"));
        Assert.That(gameTimeHudText, Does.Contain("[SerializeField] private Canvas canvas"));
        Assert.That(gameTimeHudText, Does.Contain("[SerializeField] private TMP_Text clockText"));
        Assert.That(gameTimeHudText, Does.Contain("[SerializeField] private Shadow clockShadow"));
        Assert.That(gameTimeHudText, Does.Contain("namespace Chateau.UI"));
        Assert.That(gameTimeHudText, Does.Contain("public sealed class GameTimeHUD : UIScreenBase"));
        Assert.That(gameTimeHudText, Does.Contain("public override void ValidateConfiguration"));
        Assert.That(chapter1Text, Does.Not.Contain("staged serialized GameTimeHUD edge"));
        Assert.That(installerText, Does.Contain("RequireExactlyOne<Chateau.UI.GameTimeHUD>(scene, report, \"global game-time HUD\")"));
        Assert.That(gameTimeHudText, Does.Not.Contain("public void Initialize"));
        Assert.That(gameTimeHudText, Does.Not.Contain("ResolveReferences"));
        Assert.That(gameTimeHudText, Does.Not.Contain("EnsureUI"));
        Assert.That(gameTimeHudText, Does.Not.Contain("CreateText"));
        Assert.That(gameTimeHudText, Does.Not.Contain("HideLegacyTimeSettingsPanel"));
        Assert.That(gameTimeHudText, Does.Not.Contain("EnsureEventSystem"));
        Assert.That(gameTimeHudText, Does.Not.Contain("GameObject.Find"));
        Assert.That(gameTimeHudText, Does.Not.Contain("FindAnyObjectByType"));
        Assert.That(gameTimeHudText, Does.Not.Contain("new GameObject"));
        Assert.That(gameTimeHudText, Does.Not.Contain("AddComponent<"));
        Assert.That(gameTimeHudText, Does.Not.Contain("UnityEngine.EventSystems"));
    }

    [Test]
    public void DialogueCoreServicesAreSerializedAndBound()
    {
        string sceneText = File.ReadAllText("Assets/Scenes/Gameplay.unity");

        Assert.That(sceneText, Does.Contain("subtitleService: {fileID: 1878886995}"));
        Assert.That(sceneText, Does.Contain("speechService: {fileID: 1878886994}"));
        Assert.That(sceneText, Does.Contain("lineBank: {fileID: 11400000, guid: 47d20ba9660546050951e9ea07a0b3da, type: 2}"));
        Assert.That(sceneText, Does.Contain("navigationManager: {fileID: 1878886997}"));
        Assert.That(sceneText, Does.Contain("playerMovement: {fileID: 81962842}"));

        string speechText = File.ReadAllText("Assets/Scripts/Audio/DialogueSpeechService.cs");
        string subtitleText = File.ReadAllText("Assets/Scripts/UI/SubtitleService.cs");
        string chapter1Text = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs");
        string chapter2Text = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Controller.cs");
        Assert.That(speechText, Does.Not.Contain("DialogueSpeechService FindOrCreate"));
        Assert.That(subtitleText, Does.Not.Contain("SubtitleService FindOrCreate"));
        Assert.That(speechText, Does.Not.Contain("SubtitleService.FindOrCreate"));
        Assert.That(subtitleText, Does.Not.Contain("Resources.Load<SubtitleLineBank>"));
        Assert.That(subtitleText, Does.Not.Contain("lineBankResourcePath"));
        Assert.That(subtitleText, Does.Not.Contain("FindAnyObjectByType<RoomNavigationManager>"));
        Assert.That(subtitleText, Does.Not.Contain("ResolveReferences"));
        Assert.That(subtitleText, Does.Contain("PreparePresentation"));
        Assert.That(chapter1Text, Does.Not.Contain("DialogueSpeechService.FindOrCreate"));
        Assert.That(chapter1Text, Does.Not.Contain("SubtitleService.FindOrCreate"));
        Assert.That(chapter2Text, Does.Not.Contain("DialogueSpeechService.FindOrCreate"));
        Assert.That(chapter2Text, Does.Not.Contain("SubtitleService.FindOrCreate"));
        Assert.That(speechText, Does.Not.Contain("GuestVoiceLinePlayback.FindOrCreate"));
        Assert.That(speechText, Does.Not.Contain("SpeakingCharacterIndicator.FindOrCreate"));
        Assert.That(speechText, Does.Not.Contain("FindAnyObjectByType<PointClickPlayerMovement>"));
        Assert.That(speechText, Does.Not.Contain("previousInputEnabled"));
        Assert.That(speechText, Does.Contain("AcquireBlockedPlayerInput(speechToken)"));
        Assert.That(speechText, Does.Contain("ReleaseBlockedPlayerInput();"));
        Assert.That(CountOccurrences(sceneText, "speechService: {fileID: 1878886994}"), Is.EqualTo(3));
        Assert.That(CountOccurrences(sceneText, "subtitleService: {fileID: 1878886995}"), Is.EqualTo(4));
    }

    [Test]
    public void DialogueAuxiliaryOwnersAreSerializedOnce()
    {
        string sceneText = File.ReadAllText("Assets/Scenes/Gameplay.unity");

        Assert.That(CountOccurrences(sceneText, "guid: 9e13b0cd7a5f44a69fe0b75a2cb76123"), Is.EqualTo(1));
        Assert.That(CountOccurrences(sceneText, "guid: 9963bb0aa9d84cc7a8cb801c668a92ee"), Is.EqualTo(1));
        Assert.That(sceneText, Does.Contain("voicePlayback: {fileID: 1878887001}"));
        Assert.That(sceneText, Does.Contain("speakingIndicator: {fileID: 1878887002}"));
        Assert.That(CountOccurrences(sceneText, "speakingIndicator: {fileID: 1878887002}"), Is.EqualTo(2));
        Assert.That(sceneText, Does.Contain("audioSource: {fileID: 1878887000}"));
        Assert.That(sceneText, Does.Contain("audioVolumeBinding: {fileID: 1878887003}"));
        Assert.That(CountOccurrences(sceneText, "guid: 5161da2d2e1b408d859e3792f47407f4"), Is.EqualTo(5));
        Assert.That(sceneText, Does.Match(@"--- !u!114 &1878887003[\s\S]*?m_GameObject: \{fileID: 1878886993\}[\s\S]*?audioSource: \{fileID: 1878887000\}[\s\S]*?channel: 0[\s\S]*?baseVolume: 1"));
        Assert.That(sceneText, Does.Contain("catalog: {fileID: 11400000, guid: 147a8473c4c849c9908200b092d13691, type: 2}"));
        Assert.That(sceneText, Does.Contain("bubbleSprite: {fileID: 21300000, guid: b40c2d5917304c3e822fad1b6f3e5960, type: 3}"));

        string playbackText = File.ReadAllText("Assets/Scripts/Audio/GuestVoiceLinePlayback.cs");
        string indicatorText = File.ReadAllText("Assets/Scripts/UI/SpeakingCharacterIndicator.cs");
        string dialogueText = File.ReadAllText("Assets/Scripts/Audio/DialogueSpeechService.cs");
        string subtitleText = File.ReadAllText("Assets/Scripts/UI/SubtitleService.cs");
        string chapter2Text = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Controller.cs");
        string chapterManagerText = File.ReadAllText("Assets/Scripts/Story/ChapterManager.cs");
        string settingsText = File.ReadAllText("Assets/Scripts/UI/RuntimeSettingsMenu.cs");
        Assert.That(playbackText, Does.Not.Contain("GuestVoiceLinePlayback FindOrCreate"));
        Assert.That(playbackText, Does.Not.Contain("EnsureAudioSource"));
        Assert.That(playbackText, Does.Not.Contain("GameAudioSettings.EnsureBinding(audioSource"));
        Assert.That(playbackText, Does.Contain("audioVolumeBinding.Configure(audioSource, GameAudioChannel.Dialogue, sourceBaseVolume)"));
        Assert.That(playbackText, Does.Not.Contain("Resources.Load<GuestVoiceLineCatalog>"));
        Assert.That(playbackText, Does.Not.Contain("catalogResourcePath"));
        Assert.That(playbackText, Does.Not.Contain("FindAnyObjectByType<RoomNavigationManager>"));
        Assert.That(playbackText, Does.Not.Contain("ResolveReferences"));
        Assert.That(indicatorText, Does.Not.Contain("SpeakingCharacterIndicator FindOrCreate"));
        Assert.That(indicatorText, Does.Not.Contain("HideAnyCurrent"));
        Assert.That(dialogueText, Does.Not.Contain("StopAnyCurrentSpeech"));
        Assert.That(subtitleText, Does.Not.Contain("HideAnyCurrent"));
        Assert.That(subtitleText, Does.Not.Contain("GuestVoiceLinePlayback.StopAnyCurrentLine"));
        Assert.That(chapter2Text, Does.Not.Contain("StopAnyCurrentSpeech"));
        Assert.That(playbackText, Does.Not.Contain("StopAnyCurrentLine"));
        Assert.That(chapterManagerText, Does.Not.Contain("FindAnyObjectByType<SubtitleService>"));
        Assert.That(chapterManagerText, Does.Not.Contain("GuestVoiceLinePlayback.StopAnyCurrentLine"));
        Assert.That(settingsText, Does.Not.Contain("GuestVoiceLinePlayback.StopAnyCurrentLine"));
        Assert.That(settingsText, Does.Not.Contain("FindAnyObjectByType<SubtitleService>"));
        Assert.That(chapterManagerText, Does.Match(@"StopActiveDialogueForDebugTransition\s*\(\s*\)[\s\S]*CancelQueuedSpeech\s*\(\s*\)[\s\S]*ClearAll\s*\(\s*\)"));
        Assert.That(sceneText, Does.Contain("speakingIndicator: {fileID: 1878887002}"));
        Assert.That(indicatorText, Does.Contain("new GameObject(SpriteObjectName)"), "Only the indicator's nested presentation child should remain lazy.");
    }

    [Test]
    public void RuntimeSettingsOwnerIsSerializedOnce()
    {
        string sceneText = File.ReadAllText("Assets/Scenes/Gameplay.unity");

        Assert.That(CountOccurrences(sceneText, "guid: 06d3a7eb4f7d428f9bc3e64b6c47f0b6"), Is.EqualTo(1));
        Assert.That(sceneText, Does.Contain("runtimeSettingsMenu: {fileID: 1878887112}"));
        Assert.That(sceneText, Does.Contain("m_Name: Canvas_RuntimeSettingsMenu"));
        Assert.That(sceneText, Does.Contain("m_SortingOrder: 10050"));
        Assert.That(sceneText, Does.Contain("m_ReferenceResolution: {x: 1366, y: 768}"));
        Assert.That(sceneText, Does.Contain("chapterManager: {fileID: 3301000004}"));
        Assert.That(sceneText, Does.Contain("chapterClock: {fileID: 3301000001}"));
        Assert.That(sceneText, Does.Contain("explorationMusicSource: {fileID: 2201000003}"));
        Assert.That(sceneText, Does.Contain("explorationMusicVolumeBinding: {fileID: 2201000004}"));
        Assert.That(sceneText, Does.Match(@"--- !u!114 &2201000004[\s\S]*?m_GameObject: \{fileID: 2201000001\}[\s\S]*?audioSource: \{fileID: 2201000003\}[\s\S]*?channel: 3[\s\S]*?baseVolume: 0\.125"));

        string settingsText = File.ReadAllText("Assets/Scripts/UI/RuntimeSettingsMenu.cs");
        string navigationText = File.ReadAllText("Assets/Scripts/Navigation/RoomNavigationManager.cs");
        Assert.That(settingsText, Does.Not.Contain("RuntimeSettingsMenu FindOrCreate"));
        Assert.That(settingsText, Does.Not.Contain("GetOrCreateMenuCanvas"));
        Assert.That(settingsText, Does.Not.Contain("new GameObject(MenuObjectName"));
        Assert.That(settingsText, Does.Not.Contain("GameAudioSettings.EnsureBinding(musicSource"));
        Assert.That(settingsText, Does.Not.Contain("FindAnyObjectByType"));
        Assert.That(settingsText, Does.Not.Contain("FindObjectsByType"));
        Assert.That(settingsText, Does.Not.Contain("GameObject.Find"));
        Assert.That(settingsText, Does.Not.Contain("ResolveChapterManager"));
        Assert.That(settingsText, Does.Not.Contain("ResolveChapterClock"));
        Assert.That(settingsText, Does.Not.Contain("ResolveExplorationMusicSource"));
        Assert.That(settingsText, Does.Not.Contain("EnsureEventSystem"));
        Assert.That(settingsText, Does.Not.Contain("AddComponent<RectTransform>"));
        Assert.That(settingsText, Does.Contain("public void ValidateConfiguration"));
        Assert.That(navigationText, Does.Contain("runtimeSettingsMenu.ValidateConfiguration(report)"));
        Assert.That(navigationText, Does.Not.Contain("RuntimeSettingsMenu.FindOrCreate"));
        Assert.That(navigationText, Does.Contain("runtimeSettingsMenu?.Initialize(this)"));
        Assert.That(settingsText, Does.Contain("FindOrCreateSettingsOverlay"), "Nested settings controls remain deliberate owner-scoped view construction.");
    }

    [Test]
    public void RoomAmbienceOwnersAreSerializedOnce()
    {
        string sceneText = File.ReadAllText("Assets/Scenes/Gameplay.unity");

        Assert.That(CountOccurrences(sceneText, "guid: c5d8eebb18904780a5d77a1c9da6ce6f"), Is.EqualTo(1));
        Assert.That(CountOccurrences(sceneText, "guid: 65e29c4687b6bad242fac7bcb6849828"), Is.EqualTo(1));
        Assert.That(sceneText, Does.Contain("fireplaceAmbienceController: {fileID: 2201000025}"));
        Assert.That(sceneText, Does.Contain("clockTickingAmbienceController: {fileID: 2201000034}"));
        Assert.That(sceneText, Does.Contain("catalog: {fileID: 11400000, guid: 950e4008c31a44739c468b6ccd0efd68, type: 2}"));
        Assert.That(sceneText, Does.Contain("catalog: {fileID: 11400000, guid: d1c5479f74b94514cdf7a37d49f95fbe, type: 2}"));
        Assert.That(sceneText, Does.Contain("audioSource: {fileID: 2201000023}"));
        Assert.That(sceneText, Does.Contain("highPassFilter: {fileID: 2201000024}"));
        Assert.That(sceneText, Does.Contain("audioSource: {fileID: 2201000033}"));
        Assert.That(sceneText, Does.Match(@"--- !u!4 &2201000022[\s\S]*?m_GameObject: \{fileID: 2201000021\}[\s\S]*?m_Father: \{fileID: 1878886999\}"));
        Assert.That(sceneText, Does.Match(@"--- !u!4 &2201000032[\s\S]*?m_GameObject: \{fileID: 2201000031\}[\s\S]*?m_Father: \{fileID: 1878886999\}"));

        string fireplaceText = File.ReadAllText("Assets/Scripts/Audio/FireplaceAmbienceController.cs");
        string clockText = File.ReadAllText("Assets/Scripts/Audio/ClockTickingAmbienceController.cs");
        string navigationText = File.ReadAllText("Assets/Scripts/Navigation/RoomNavigationManager.cs");
        string[] ownerTexts = { fireplaceText, clockText };

        for (int i = 0; i < ownerTexts.Length; i++)
        {
            Assert.That(ownerTexts[i], Does.Not.Contain("FindOrCreate"));
            Assert.That(ownerTexts[i], Does.Not.Contain("FindAnyObjectByType"));
            Assert.That(ownerTexts[i], Does.Not.Contain("Resources.Load"));
            Assert.That(ownerTexts[i], Does.Not.Contain("new GameObject"));
            Assert.That(ownerTexts[i], Does.Not.Contain("AddComponent<"));
            Assert.That(ownerTexts[i], Does.Not.Contain("GetComponent<"));
        }

        Assert.That(sceneText, Does.Not.Contain("catalogResourcePath: Audio/FireplaceAmbienceCatalog"));
        Assert.That(sceneText, Does.Not.Contain("catalogResourcePath: Audio/ClockTickingAmbienceCatalog"));
        Assert.That(navigationText, Does.Not.Contain("FireplaceAmbienceController.FindOrCreate"));
        Assert.That(navigationText, Does.Not.Contain("ClockTickingAmbienceController.FindOrCreate"));
        Assert.That(navigationText, Does.Contain("fireplaceAmbienceController?.Initialize(this)"));
        Assert.That(navigationText, Does.Contain("clockTickingAmbienceController?.Initialize(this)"));
    }

    private static int CountOccurrences(string text, string value)
    {
        return text.Split(new[] { value }, StringSplitOptions.None).Length - 1;
    }

    private static string ExtractDocument(string assetText, string header)
    {
        int start = assetText.IndexOf(header, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing document '{header}'.");
        int end = assetText.IndexOf("\n--- !u!", start + header.Length, StringComparison.Ordinal);
        return end >= 0 ? assetText.Substring(start, end - start) : assetText.Substring(start);
    }

    [Test]
    public void InvalidTransitionCanThrowWithUsefulMessage()
    {
        StateMachine<TestState> machine = new StateMachine<TestState>(TestState.Idle);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            machine.TransitionOrThrow(TestState.Complete));

        Assert.That(exception.Message, Does.Contain("Idle"));
        Assert.That(exception.Message, Does.Contain("Complete"));
    }
}
#endif
