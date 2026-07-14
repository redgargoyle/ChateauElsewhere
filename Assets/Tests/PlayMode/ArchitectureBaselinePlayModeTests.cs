using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class ArchitectureBaselinePlayModeTests
{
    private const string MainMenuSceneName = "MainMenu";
    private const string GameplaySceneName = "Gameplay";
    private const string EntranceRoomName = "Grand Entrance Hall";
    private const string DrawingRoomName = "Drawing Room";
    private const uint EvidenceWidth = 1366;
    private const uint EvidenceHeight = 768;
    private const string EvidenceResolutionName = "Chantilly Architecture Slice 0.3";
    private const float EvidenceHorizontalRoomPan = -0.55f;
    private const float EvidenceVerticalRoomPan = -1f;
    private const float EvidenceRoomFov = 0.8f;
    private const string ExpectedColdStartFingerprintSha256 =
        "34ea66772abd7375f965b2277e7342c82dbd853bc1efecc8d82a00e1b403dd96";
    private uint previousRenderingWidth;
    private uint previousRenderingHeight;
    private UnityEngine.Random.State previousRandomState;
    private float previousCaptureDeltaTime;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        GetRenderingResolution(out previousRenderingWidth, out previousRenderingHeight);
        previousRandomState = UnityEngine.Random.state;
        previousCaptureDeltaTime = Time.captureDeltaTime;
        SetFixedRenderingResolution(EvidenceWidth, EvidenceHeight);
        SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
        yield return WaitForScene(MainMenuSceneName, 120);
        yield return WaitForGameObject("Button_NewGame", 120);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        MonoBehaviour speech = FindSceneComponents("DialogueSpeechService").SingleOrDefault();

        if (speech != null)
        {
            InvokeMethod(speech, "StopCurrentSpeech");
        }

        SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
        yield return WaitForScene(MainMenuSceneName, 120);

        UnityEngine.Random.state = previousRandomState;
        Time.captureDeltaTime = previousCaptureDeltaTime;

        if (previousRenderingWidth > 0 && previousRenderingHeight > 0)
        {
            SetFixedRenderingResolution(previousRenderingWidth, previousRenderingHeight);
            yield return WaitForFixedRenderingResolution(
                previousRenderingWidth,
                previousRenderingHeight,
                30);
        }
    }

    [UnityTest]
    public IEnumerator MainMenuBootEstablishesInitialArchitectureScaleAndDialogueState()
    {
        yield return BootGameplayFromRealMenu();

        MonoBehaviour gameRoot = RequireSingleSceneComponent("GameRoot");
        MonoBehaviour navigation = RequireSingleSceneComponent("RoomNavigationManager");
        MonoBehaviour chapter = RequireSingleSceneComponent("ChapterManager");
        MonoBehaviour clock = RequireSingleSceneComponent("ChapterClock");
        MonoBehaviour arrival = RequireSingleSceneComponent("Chapter1ArrivalController");
        MonoBehaviour player = RequireComponentOnGameObject("Player", "PointClickPlayerMovement");
        MonoBehaviour subtitle = RequireSingleSceneComponent("SubtitleService");
        MonoBehaviour speech = RequireSingleSceneComponent("DialogueSpeechService");

        Assert.That(GetProperty<bool>(gameRoot, "IsInitialized"), Is.True);
        Assert.That(GetProperty<object>(gameRoot, "Context"), Is.Not.Null);
        Assert.That(GetProperty<object>(gameRoot, "Database"), Is.Not.Null);

        object[] services = ((IEnumerable)GetProperty<object>(gameRoot, "Services"))
            .Cast<object>()
            .ToArray();
        Assert.That(services, Has.Length.EqualTo(8));
        Assert.That(services.Select(service => service.GetType()).Distinct().Count(), Is.EqualTo(8));
        Assert.That(services.All(service => GetProperty<bool>(service, "IsInitialized")), Is.True);

        Assert.That(GetProperty<string>(navigation, "CurrentRoom"), Is.EqualTo(EntranceRoomName));
        Assert.That(GetProperty<string>(chapter, "CurrentChapterId"), Is.EqualTo("chapter_01_arrivals"));
        string initialPhase = GetProperty<object>(chapter, "CurrentPhase").ToString();
        Assert.That(
            new[] { "BlackScreen", "ShowActTitle", "FadeIntoRoom" },
            Does.Contain(initialPhase),
            "The real startup test must observe Chapter 1's intro beat before free interaction begins.");
        Assert.That(GetProperty<int>(clock, "StartTotalMinutes"), Is.EqualTo(17 * 60 + 59));
        Assert.That(GetProperty<string>(clock, "CurrentTimeLabel"), Is.EqualTo("5:59 PM"));
        Assert.That(GetProperty<int>(arrival, "CurrentGuestIndex"), Is.EqualTo(-1));
        string initialBeatState = (string)InvokeMethod(arrival, "BuildDebugState");
        Assert.That(initialBeatState, Does.Contain("current guest index: -1"));
        Assert.That(initialBeatState, Does.Contain("chapterOneComplete: False"));

        Assert.That(GetProperty<bool>(subtitle, "IsInitialized"), Is.True);
        Assert.That(GetProperty<bool>(speech, "IsInitialized"), Is.True);
        Assert.That(FindSceneComponents("SubtitleService"), Has.Count.EqualTo(1));
        Assert.That(FindSceneComponents("DialogueSpeechService"), Has.Count.EqualTo(1));

        MethodInfo beginSpeech = RequireMethod(speech, "BeginSpeakLine", 8);
        beginSpeech.Invoke(
            speech,
            new object[]
            {
                "ARCH_SLICE_0_3_STARTUP",
                "Butler",
                "Architecture startup characterization.",
                false,
                false,
                null,
                true,
                null
            });
        yield return null;

        Assert.That(FindSceneGameObject("Canvas_Subtitles"), Is.Not.Null);
        Assert.That(FindSceneGameObject("Sprite_ChatBubble"), Is.Not.Null);
        InvokeMethod(speech, "StopCurrentSpeech");
        yield return null;

        Assert.That(GetProperty<float>(player, "RoomPresentationScale"), Is.EqualTo(0.7528645f).Within(0.000001f));
        Assert.That(
            GetProperty<float>(player, "CurrentWorldActorScaleMultiplier"),
            Is.EqualTo(0.7528645f).Within(0.001f));

        MethodInfo frontDoorApproachMethod = RequireMethod(arrival, "TryGetFrontDoorApproachDestination", 2);
        object[] frontDoorApproachArguments = { player, Vector2.zero };
        Assert.That((bool)frontDoorApproachMethod.Invoke(arrival, frontDoorApproachArguments), Is.True);
        Vector2 frontDoorApproach = (Vector2)frontDoorApproachArguments[1];
        Assert.That(frontDoorApproach.x, Is.EqualTo(-0.09396f).Within(0.001f));
        Assert.That(frontDoorApproach.y, Is.EqualTo(-1.223958f).Within(0.001f));
        Assert.That((bool)InvokeMethod(player, "TryWarpToExact", frontDoorApproach), Is.True);
        yield return null;
        yield return null;

        SpriteRenderer playerRenderer = player.GetComponent<SpriteRenderer>();
        GameObject frontDoor = RequireSceneGameObject("Door_answer_trigger");
        SpriteRenderer doorRenderer = frontDoor.GetComponent<SpriteRenderer>();
        Camera camera = Camera.main;
        Assert.That(playerRenderer, Is.Not.Null);
        Assert.That(doorRenderer, Is.Not.Null);
        Assert.That(camera, Is.Not.Null);

        float playerHeight = GetRenderedScreenHeight(playerRenderer, camera);
        float doorHeight = GetRenderedScreenHeight(doorRenderer, camera);
        float playerDoorRatio = playerHeight / doorHeight;
        Assert.That(playerDoorRatio, Is.InRange(0.65f, 0.85f),
            "The entrance Butler should render at approximately three quarters of the entrance door height.");

        AssertFixedRenderingResolution();
        Debug.Log(
            $"[Slice03EntranceGolden] resolution={Screen.width}x{Screen.height} " +
            $"room={GetProperty<string>(navigation, "CurrentRoom")} phase={initialPhase} " +
            $"time={GetProperty<string>(clock, "CurrentTimeLabel")} services={services.Length} " +
            $"doorApproach={Format(frontDoorApproach)} " +
            $"playerScale={player.transform.localScale.y:0.######} " +
            $"presentationScale={GetProperty<float>(player, "RoomPresentationScale"):0.#######} " +
            $"playerScreenHeight={playerHeight:0.###} doorScreenHeight={doorHeight:0.###} " +
            $"playerDoorRatio={playerDoorRatio:0.######} sorting={playerRenderer.sortingOrder}");
    }

    [UnityTest]
    public IEnumerator ColdStartScaleAndSortFingerprintMatchesApprovedBaseline()
    {
        yield return BootGameplayFromRealMenu();

        MonoBehaviour gameRoot = RequireSingleSceneComponent("GameRoot");
        MonoBehaviour clock = RequireSingleSceneComponent("ChapterClock");
        MonoBehaviour scheduler = RequireSingleSceneComponent("ChapterEventScheduler");
        MonoBehaviour cameraService = RequireSingleSceneComponent("CameraManager");
        MonoBehaviour navigation = RequireSingleSceneComponent("RoomNavigationManager");
        MonoBehaviour lighting = RequireSingleSceneComponent("RoomLightingController");
        MonoBehaviour subtitles = RequireSingleSceneComponent("SubtitleService");
        MonoBehaviour dialogue = RequireSingleSceneComponent("DialogueSpeechService");
        MonoBehaviour chapter = RequireSingleSceneComponent("ChapterManager");
        MonoBehaviour arrival = RequireSingleSceneComponent("Chapter1ArrivalController");
        MonoBehaviour player = RequireComponentOnGameObject("Player", "PointClickPlayerMovement");
        SpriteRenderer playerRenderer = player.GetComponent<SpriteRenderer>();
        SpriteRenderer doorRenderer = RequireSceneGameObject("Door_answer_trigger").GetComponent<SpriteRenderer>();
        Camera camera = Camera.main;

        object context = GetProperty<object>(gameRoot, "Context");
        object[] contextServices = ((IEnumerable)GetProperty<object>(context, "Services"))
            .Cast<object>()
            .ToArray();
        object[] expectedServices =
        {
            clock,
            scheduler,
            cameraService,
            navigation,
            lighting,
            subtitles,
            dialogue,
            chapter
        };

        Assert.That(GetProperty<object>(context, "Clock"), Is.SameAs(clock));
        Assert.That(GetProperty<object>(context, "Scheduler"), Is.SameAs(scheduler));
        Assert.That(GetProperty<object>(context, "Camera"), Is.SameAs(cameraService));
        Assert.That(GetProperty<object>(context, "Navigation"), Is.SameAs(navigation));
        Assert.That(GetProperty<object>(context, "Lighting"), Is.SameAs(lighting));
        Assert.That(GetProperty<object>(context, "Dialogue"), Is.SameAs(dialogue));
        Assert.That(GetProperty<object>(context, "GameFlow"), Is.SameAs(chapter));
        Assert.That(contextServices, Is.EqualTo(expectedServices));
        Assert.That(
            contextServices.Select(service => GetProperty<int>(service, "InitializationOrder")),
            Is.EqualTo(new[] { 100, 200, 300, 400, 500, 600, 700, 800 }));
        Assert.That(contextServices.All(service => GetProperty<bool>(service, "IsInitialized")), Is.True);

        Assert.That(playerRenderer, Is.Not.Null);
        Assert.That(doorRenderer, Is.Not.Null);
        Assert.That(camera, Is.Not.Null);

        MethodInfo frontDoorApproachMethod = RequireMethod(arrival, "TryGetFrontDoorApproachDestination", 2);
        object[] frontDoorApproachArguments = { player, Vector2.zero };
        Assert.That((bool)frontDoorApproachMethod.Invoke(arrival, frontDoorApproachArguments), Is.True);
        Vector2 frontDoorApproach = (Vector2)frontDoorApproachArguments[1];
        Assert.That(frontDoorApproach.x, Is.EqualTo(-0.09396f).Within(0.001f));
        Assert.That(frontDoorApproach.y, Is.EqualTo(-1.223958f).Within(0.001f));
        Assert.That((bool)InvokeMethod(player, "TryWarpToExact", frontDoorApproach), Is.True);
        yield return null;
        yield return null;

        float playerScreenHeight = GetRenderedScreenHeight(playerRenderer, camera);
        float doorScreenHeight = GetRenderedScreenHeight(doorRenderer, camera);
        float playerDoorRatio = playerScreenHeight / doorScreenHeight;
        Assert.That(GetProperty<float>(player, "RoomPresentationScale"), Is.EqualTo(0.7528645f).Within(0.000001f));
        Assert.That(GetProperty<float>(player, "CurrentWorldActorScaleMultiplier"), Is.EqualTo(0.7528645f).Within(0.001f));
        Assert.That(playerScreenHeight, Is.EqualTo(114.417f).Within(0.5f));
        Assert.That(doorScreenHeight, Is.EqualTo(141.481f).Within(0.5f));
        Assert.That(playerDoorRatio, Is.EqualTo(0.808710f).Within(0.001f));
        Assert.That(playerDoorRatio, Is.InRange(0.65f, 0.85f));
        Assert.That(playerRenderer.sortingOrder, Is.EqualTo(1075));

        string butlerFingerprint = BuildButlerScaleFingerprint(player, playerRenderer);
        yield return null;
        yield return null;
        Assert.That(
            BuildButlerScaleFingerprint(player, playerRenderer),
            Is.EqualTo(butlerFingerprint),
            "Butler scale/sort must settle before the cold-start observation and remain idempotent.");

        InvokeMethod(chapter, "SkipToChapter2ForTesting");
        yield return WaitForCurrentRoom(navigation, DrawingRoomName, 120);
        FreezeRoomLookForEvidence();
        yield return null;
        yield return null;

        MonoBehaviour[] visibleGuests = FindSceneComponents("ActorRoomState")
            .Where(actor => GetProperty<bool>(actor, "IsVisibleInCurrentRoom"))
            .OrderBy(actor => GetProperty<string>(actor, "ActorId"), StringComparer.Ordinal)
            .ToArray();
        Assert.That(visibleGuests, Has.Length.EqualTo(8));
        Assert.That(
            visibleGuests.Select(actor => GetProperty<string>(actor, "ActorId")).Distinct().Count(),
            Is.EqualTo(8));

        string guestFingerprint = BuildGuestScaleFingerprint(visibleGuests);
        yield return null;
        yield return null;
        Assert.That(
            BuildGuestScaleFingerprint(visibleGuests),
            Is.EqualTo(guestFingerprint),
            "Guest scale/sort must settle before the cold-start observation and remain idempotent.");

        AssertFixedRenderingResolution();
        string canonicalFingerprint = $"{butlerFingerprint}|{guestFingerprint}";
        string fingerprintSha256 = ComputeSha256(canonicalFingerprint);

        Debug.Log(
            $"[Slice04ColdStartFingerprint] sha256={fingerprintSha256} " +
            $"playerHeight={playerScreenHeight:0.###} doorHeight={doorScreenHeight:0.###} " +
            $"ratio={playerDoorRatio:0.######} canonical={canonicalFingerprint}");

        Assert.That(
            fingerprintSha256,
            Is.EqualTo(ExpectedColdStartFingerprintSha256),
            "The authored cold-start scale/sort baseline changed.");
    }

    [UnityTest]
    public IEnumerator DrawingRoomRoundTripCharacterizesVisibilityCollisionAndHiddenChildDebt()
    {
        yield return BootGameplayFromRealMenu();

        MonoBehaviour navigation = RequireSingleSceneComponent("RoomNavigationManager");
        MonoBehaviour player = RequireComponentOnGameObject("Player", "PointClickPlayerMovement");
        MonoBehaviour entranceView = RequireRoomView("room.grand-entrance-hall");
        MonoBehaviour drawingView = RequireRoomView("room.drawing-room");
        MonoBehaviour forwardPassage = RequireComponentOnGameObject(
            "DoorTrigger_GEH_DrawingRoom",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour reversePassage = RequireComponentOnGameObject(
            "DoorTrigger_DrawingRoom_GEH",
            "Chateau.World.Rooms.Passages.Passage");

        Assert.That(GetProperty<string>(navigation, "CurrentRoom"), Is.EqualTo(EntranceRoomName));
        Assert.That(GetProperty<bool>(entranceView, "IsVisible"), Is.True);
        Assert.That(GetProperty<bool>(drawingView, "IsVisible"), Is.False);
        Vector2 entrancePosition = GetProperty<Vector2>(player, "LogicalPosition");

        Assert.That((bool)InvokeMethod(navigation, "TryTraverse", forwardPassage), Is.True);
        yield return WaitForCurrentRoom(navigation, DrawingRoomName, 60);
        FreezeRoomLookForEvidence();
        yield return null;

        Assert.That(GetProperty<bool>(entranceView, "IsVisible"), Is.False);
        Assert.That(GetProperty<bool>(drawingView, "IsVisible"), Is.True);
        Vector2 drawingPosition = GetProperty<Vector2>(player, "LogicalPosition");

        MonoBehaviour teaTableBlocker = FindSceneComponents("ObjectMovementBlocker2D")
            .Single(component => GetProperty<string>(component, "SourceObjectName") == "tea_service_table");
        Collider2D blockingCollider = GetProperty<Collider2D>(teaTableBlocker, "BlockingCollider");
        Assert.That(blockingCollider, Is.Not.Null);
        Assert.That(blockingCollider.enabled, Is.True);

        Vector2 blockedScreenPoint = Camera.main.WorldToScreenPoint(blockingCollider.bounds.center);
        MethodInfo evaluateMovement = RequireMethod(player, "TryEvaluateMovementAtScreenPoint", 3);
        Type queryType = evaluateMovement.GetParameters()[2].ParameterType.GetElementType();
        object[] movementArguments =
        {
            blockedScreenPoint,
            true,
            Activator.CreateInstance(queryType)
        };
        Assert.That((bool)evaluateMovement.Invoke(player, movementArguments), Is.True);
        object movementQuery = movementArguments[2];
        Assert.That(GetProperty<bool>(movementQuery, "ExactPointWalkable"), Is.False);
        Assert.That(GetProperty<bool>(movementQuery, "HasReachableDestination"), Is.True);
        Assert.That(GetProperty<bool>(movementQuery, "UsesProjectedDestination"), Is.True);
        Vector2 projectedDestination = GetProperty<Vector2>(movementQuery, "Destination");

        Vector2 movementStart = GetProperty<Vector2>(player, "LogicalPosition");
        Assert.That(
            (bool)InvokeMethod(player, "TrySetDestinationFromScreenPoint", blockedScreenPoint, true, true),
            Is.True,
            "The real movement owner must accept the collision-projected destination.");
        Assert.That(GetProperty<bool>(player, "HasDestination"), Is.True);
        yield return WaitForMovementStop(player, 300);

        Vector2 movementEnd = GetProperty<Vector2>(player, "LogicalPosition");
        Assert.That(Vector2.Distance(movementStart, movementEnd), Is.GreaterThan(0.1f));
        Assert.That(Vector2.Distance(movementEnd, projectedDestination), Is.LessThan(0.05f));

        object[] finalWorldPointArguments = { movementEnd, Vector2.zero };
        Assert.That(
            (bool)RequireMethod(player, "TryGetWorldPointFromLogicalPosition", 2)
                .Invoke(player, finalWorldPointArguments),
            Is.True);
        Assert.That(
            blockingCollider.OverlapPoint((Vector2)finalWorldPointArguments[1]),
            Is.False,
            "The completed movement must stop outside the real tea-table blocker.");

        Debug.Log(
            $"[Slice04FixedLookDrawingMeasurement] blockedClick={Format(blockedScreenPoint)} " +
            $"projectedDestination={Format(projectedDestination)} movementEnd={Format(movementEnd)}");

        Assert.That(drawingPosition.x, Is.EqualTo(5.267176f).Within(0.001f));
        Assert.That(drawingPosition.y, Is.EqualTo(-2.104616f).Within(0.001f));
        Assert.That(blockedScreenPoint.x, Is.EqualTo(654.4744f).Within(0.5f));
        Assert.That(blockedScreenPoint.y, Is.EqualTo(135.5689f).Within(0.5f));
        Assert.That(projectedDestination.x, Is.EqualTo(-1.045052f).Within(0.001f));
        Assert.That(projectedDestination.y, Is.EqualTo(-3.514679f).Within(0.001f));

        GameObject teaTable = RequireSceneGameObject("tea_service_table");
        SpriteRenderer teaTableRenderer = teaTable.GetComponent<SpriteRenderer>();
        Assert.That(teaTableRenderer, Is.Not.Null);
        teaTableRenderer.enabled = false;

        Assert.That((bool)InvokeMethod(navigation, "TryTraverse", reversePassage), Is.True);
        yield return WaitForCurrentRoom(navigation, EntranceRoomName, 60);
        Assert.That(GetProperty<bool>(entranceView, "IsVisible"), Is.True);
        Assert.That(GetProperty<bool>(drawingView, "IsVisible"), Is.False);

        Assert.That((bool)InvokeMethod(navigation, "TryTraverse", forwardPassage), Is.True);
        yield return WaitForCurrentRoom(navigation, DrawingRoomName, 60);
        yield return null;

        Assert.That(teaTableRenderer.enabled, Is.True,
            "Baseline truth: RoomContentGroup currently force-enables an intentionally hidden child when its room is reactivated. A later visibility-ownership slice must reverse this assertion.");

        Assert.That((bool)InvokeMethod(navigation, "TryTraverse", reversePassage), Is.True);
        yield return WaitForCurrentRoom(navigation, EntranceRoomName, 60);
        Vector2 returnedPosition = GetProperty<Vector2>(player, "LogicalPosition");
        Assert.That(returnedPosition.x, Is.EqualTo(-7.75f).Within(0.001f));
        Assert.That(returnedPosition.y, Is.EqualTo(-2.22f).Within(0.001f));

        AssertFixedRenderingResolution();
        Debug.Log(
            $"[Slice03PassageGolden] resolution={Screen.width}x{Screen.height} " +
            $"entrance={Format(entrancePosition)} drawing={Format(drawingPosition)} " +
            $"return={Format(returnedPosition)} blockedClick={Format(blockedScreenPoint)} " +
            $"projectedDestination={Format(projectedDestination)} " +
            $"hiddenChildAfterCycle={teaTableRenderer.enabled}");
        Debug.Log(
            $"[Slice03DrawingGolden] room={DrawingRoomName} roomViewVisibleDuringCapture=True " +
            $"blocker=tea_service_table collider={blockingCollider.GetType().Name}");
    }

    [UnityTest]
    public IEnumerator ButlersPantryBilliardRoundTripUsesAuthoredCanonicalPassages()
    {
        const string DiningRoomName = "Dining Room";
        const string ButlersPantryRoomName = "Butlers Pantry";
        const string BilliardRoomName = "Billiard Room";
        Vector2 pantryAnchor = new Vector2(3.244461f, -3.108338f);
        Vector2 billiardAnchor = new Vector2(6.9f, -1.6f);

        yield return BootGameplayFromRealMenu();

        MonoBehaviour navigation = RequireSingleSceneComponent("RoomNavigationManager");
        MonoBehaviour player = RequireComponentOnGameObject("Player", "PointClickPlayerMovement");
        MonoBehaviour entranceToDining = RequireComponentOnGameObject(
            "DoorTrigger_GEH_DiningRoom",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour diningToPantry = RequireComponentOnGameObject(
            "DoorTrigger_DiningRoom_ButlersPantry",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour pantryToBilliard = RequireComponentOnGameObject(
            "DoorTrigger_Butlers_Pantry_BilliardRoom",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour billiardToPantry = RequireComponentOnGameObject(
            "DoorTrigger_BilliardRoom_ButlersPantry",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour pantryTrigger = RequireComponentOnGameObject(
            "DoorTrigger_Butlers_Pantry_BilliardRoom",
            "DoorTriggerNavigation");
        MonoBehaviour billiardTrigger = RequireComponentOnGameObject(
            "DoorTrigger_BilliardRoom_ButlersPantry",
            "DoorTriggerNavigation");
        MonoBehaviour pantryView = RequireRoomView("room.butlers-pantry");
        MonoBehaviour billiardView = RequireRoomView("room.billiard-room");

        Assert.That(GetField<MonoBehaviour>(pantryTrigger, "canonicalPassage"), Is.SameAs(pantryToBilliard));
        Assert.That(GetField<MonoBehaviour>(billiardTrigger, "canonicalPassage"), Is.SameAs(billiardToPantry));
        Assert.That(GetField<float>(pantryTrigger, "maxPlayerScreenDistance"), Is.EqualTo(145f));
        Assert.That(GetField<float>(billiardTrigger, "maxPlayerScreenDistance"), Is.EqualTo(145f));
        Assert.That(GetProperty<object>(pantryToBilliard, "AnchorMigrationStage").ToString(),
            Is.EqualTo("AuthoredAnchors"));
        Assert.That(GetProperty<object>(billiardToPantry, "AnchorMigrationStage").ToString(),
            Is.EqualTo("AuthoredAnchors"));
        Assert.That(
            GetProperty<Vector2>(GetProperty<object>(pantryToBilliard, "ApproachAnchor"), "LogicalPosition"),
            Is.EqualTo(pantryAnchor));
        Assert.That(
            GetProperty<Vector2>(GetProperty<object>(pantryToBilliard, "ArrivalAnchor"), "LogicalPosition"),
            Is.EqualTo(billiardAnchor));
        Assert.That(
            GetProperty<Vector2>(GetProperty<object>(billiardToPantry, "ApproachAnchor"), "LogicalPosition"),
            Is.EqualTo(billiardAnchor));
        Assert.That(
            GetProperty<Vector2>(GetProperty<object>(billiardToPantry, "ArrivalAnchor"), "LogicalPosition"),
            Is.EqualTo(pantryAnchor));

        Assert.That((bool)InvokeMethod(navigation, "TryTraverse", entranceToDining), Is.True);
        yield return WaitForCurrentRoom(navigation, DiningRoomName, 60);
        Assert.That((bool)InvokeMethod(navigation, "TryTraverse", diningToPantry), Is.True);
        yield return WaitForCurrentRoom(navigation, ButlersPantryRoomName, 60);
        FreezeRoomLookForEvidence();
        yield return null;

        Assert.That(GetProperty<bool>(pantryView, "IsVisible"), Is.True);
        Assert.That(GetProperty<bool>(billiardView, "IsVisible"), Is.False);
        InvokeMethod(player, "SetInputEnabled", true);
        Assert.That((bool)InvokeMethod(player, "TryWarpToExact", pantryAnchor), Is.True);
        SetField(pantryTrigger, "lastPointerActivationFrame", -1);
        InvokeMethod(pantryTrigger, "ActivateDoor");
        yield return WaitForCurrentRoom(navigation, BilliardRoomName, 60);
        FreezeRoomLookForEvidence();
        yield return null;

        Assert.That(GetProperty<Vector2>(player, "LogicalPosition"), Is.EqualTo(billiardAnchor));
        Assert.That(GetProperty<bool>(pantryView, "IsVisible"), Is.False);
        Assert.That(GetProperty<bool>(billiardView, "IsVisible"), Is.True);
        SetField(billiardTrigger, "lastPointerActivationFrame", -1);
        InvokeMethod(billiardTrigger, "ActivateDoor");
        yield return WaitForCurrentRoom(navigation, ButlersPantryRoomName, 60);
        FreezeRoomLookForEvidence();
        yield return null;

        Assert.That(GetProperty<Vector2>(player, "LogicalPosition"), Is.EqualTo(pantryAnchor));
        Assert.That(GetProperty<bool>(pantryView, "IsVisible"), Is.True);
        Assert.That(GetProperty<bool>(billiardView, "IsVisible"), Is.False);
        AssertFixedRenderingResolution();
        Debug.Log(
            $"[Slice21ButlersBilliardPlayMode] resolution={Screen.width}x{Screen.height} " +
            $"pantry={Format(pantryAnchor)} billiard={Format(billiardAnchor)} " +
            "callers=bound stages=authored-anchors threshold=145");
    }

    [UnityTest]
    public IEnumerator ButlersPantryServiceCorridorRoundTripUsesAuthoredCanonicalPassages()
    {
        const string DiningRoomName = "Dining Room";
        const string ButlersPantryRoomName = "Butlers Pantry";
        const string ServiceCorridorRoomName = "Service Corridor";
        Vector2 pantryAnchor = new Vector2(7f, -2.8f);
        Vector2 serviceAnchor = new Vector2(4.2f, -3.3f);

        yield return BootGameplayFromRealMenu();

        MonoBehaviour navigation = RequireSingleSceneComponent("RoomNavigationManager");
        MonoBehaviour player = RequireComponentOnGameObject("Player", "PointClickPlayerMovement");
        MonoBehaviour entranceToDining = RequireComponentOnGameObject(
            "DoorTrigger_GEH_DiningRoom",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour diningToPantry = RequireComponentOnGameObject(
            "DoorTrigger_DiningRoom_ButlersPantry",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour pantryToService = RequireComponentOnGameObject(
            "DoorTrigger_ButlersPantry_ServiceCorridor",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour serviceToPantry = RequireComponentOnGameObject(
            "DoorTrigger_ServiceCorridor_ButlersPantry",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour pantryTrigger = RequireComponentOnGameObject(
            "DoorTrigger_ButlersPantry_ServiceCorridor",
            "DoorTriggerNavigation");
        MonoBehaviour serviceTrigger = RequireComponentOnGameObject(
            "DoorTrigger_ServiceCorridor_ButlersPantry",
            "DoorTriggerNavigation");
        MonoBehaviour pantryView = RequireRoomView("room.butlers-pantry");
        MonoBehaviour serviceView = RequireRoomView("room.service-corridor");

        Assert.That(GetField<MonoBehaviour>(pantryTrigger, "canonicalPassage"), Is.SameAs(pantryToService));
        Assert.That(GetField<MonoBehaviour>(serviceTrigger, "canonicalPassage"), Is.SameAs(serviceToPantry));
        Assert.That(GetField<float>(pantryTrigger, "maxPlayerScreenDistance"), Is.EqualTo(145f));
        Assert.That(GetField<float>(serviceTrigger, "maxPlayerScreenDistance"), Is.EqualTo(145f));
        Assert.That(GetProperty<object>(pantryToService, "AnchorMigrationStage").ToString(),
            Is.EqualTo("AuthoredAnchors"));
        Assert.That(GetProperty<object>(serviceToPantry, "AnchorMigrationStage").ToString(),
            Is.EqualTo("AuthoredAnchors"));
        Assert.That(
            GetProperty<Vector2>(GetProperty<object>(pantryToService, "ApproachAnchor"), "LogicalPosition"),
            Is.EqualTo(pantryAnchor));
        Assert.That(
            GetProperty<Vector2>(GetProperty<object>(pantryToService, "ArrivalAnchor"), "LogicalPosition"),
            Is.EqualTo(serviceAnchor));
        Assert.That(
            GetProperty<Vector2>(GetProperty<object>(serviceToPantry, "ApproachAnchor"), "LogicalPosition"),
            Is.EqualTo(serviceAnchor));
        Assert.That(
            GetProperty<Vector2>(GetProperty<object>(serviceToPantry, "ArrivalAnchor"), "LogicalPosition"),
            Is.EqualTo(pantryAnchor));

        Assert.That((bool)InvokeMethod(navigation, "TryTraverse", entranceToDining), Is.True);
        yield return WaitForCurrentRoom(navigation, DiningRoomName, 60);
        Assert.That((bool)InvokeMethod(navigation, "TryTraverse", diningToPantry), Is.True);
        yield return WaitForCurrentRoom(navigation, ButlersPantryRoomName, 60);
        FreezeRoomLookForEvidence();
        yield return null;

        Assert.That(GetProperty<bool>(pantryView, "IsVisible"), Is.True);
        Assert.That(GetProperty<bool>(serviceView, "IsVisible"), Is.False);
        InvokeMethod(player, "SetInputEnabled", true);
        Assert.That((bool)InvokeMethod(player, "TryWarpToExact", pantryAnchor), Is.True);
        SetField(pantryTrigger, "lastPointerActivationFrame", -1);
        InvokeMethod(pantryTrigger, "ActivateDoor");
        yield return WaitForCurrentRoom(navigation, ServiceCorridorRoomName, 60);
        FreezeRoomLookForEvidence();
        yield return null;

        Assert.That(GetProperty<Vector2>(player, "LogicalPosition"), Is.EqualTo(serviceAnchor));
        Assert.That(GetProperty<bool>(pantryView, "IsVisible"), Is.False);
        Assert.That(GetProperty<bool>(serviceView, "IsVisible"), Is.True);
        SetField(serviceTrigger, "lastPointerActivationFrame", -1);
        InvokeMethod(serviceTrigger, "ActivateDoor");
        yield return WaitForCurrentRoom(navigation, ButlersPantryRoomName, 60);
        FreezeRoomLookForEvidence();
        yield return null;

        Assert.That(GetProperty<Vector2>(player, "LogicalPosition"), Is.EqualTo(pantryAnchor));
        Assert.That(GetProperty<bool>(pantryView, "IsVisible"), Is.True);
        Assert.That(GetProperty<bool>(serviceView, "IsVisible"), Is.False);
        AssertFixedRenderingResolution();
        Debug.Log(
            $"[Slice22Group07PlayMode] resolution={Screen.width}x{Screen.height} " +
            $"pantry={Format(pantryAnchor)} service={Format(serviceAnchor)} " +
            "callers=bound stages=authored-anchors threshold=145 blockers=2");
    }

    [UnityTest]
    public IEnumerator ServiceCorridorKitchenRoundTripUsesRoomViewLocalCanonicalPassages()
    {
        const string DiningRoomName = "Dining Room";
        const string ButlersPantryRoomName = "Butlers Pantry";
        const string ServiceCorridorRoomName = "Service Corridor";
        const string KitchenRoomName = "Kitchen";
        Vector2 serviceAnchor = new Vector2(589.9897f, -419.25894f);
        Vector2 kitchenAnchor = new Vector2(-478.36285f, -156.76599f);

        yield return BootGameplayFromRealMenu();

        MonoBehaviour navigation = RequireSingleSceneComponent("RoomNavigationManager");
        MonoBehaviour player = RequireComponentOnGameObject("Player", "PointClickPlayerMovement");
        MonoBehaviour cameraManager = RequireSingleSceneComponent("CameraManager");
        MonoBehaviour entranceToDining = RequireComponentOnGameObject(
            "DoorTrigger_GEH_DiningRoom",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour diningToPantry = RequireComponentOnGameObject(
            "DoorTrigger_DiningRoom_ButlersPantry",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour pantryToService = RequireComponentOnGameObject(
            "DoorTrigger_ButlersPantry_ServiceCorridor",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour serviceToKitchen = RequireComponentOnGameObject(
            "DoorTrigger_ServiceCorridor_Kitchen",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour kitchenToService = RequireComponentOnGameObject(
            "DoorTrigger_Kitchen_ServiceCorridor",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour serviceTrigger = RequireComponentOnGameObject(
            "DoorTrigger_ServiceCorridor_Kitchen",
            "DoorTriggerNavigation");
        MonoBehaviour kitchenTrigger = RequireComponentOnGameObject(
            "DoorTrigger_Kitchen_ServiceCorridor",
            "DoorTriggerNavigation");
        MonoBehaviour serviceView = RequireRoomView("room.service-corridor");
        MonoBehaviour kitchenView = RequireRoomView("room.kitchen");

        Assert.That(GetField<MonoBehaviour>(serviceTrigger, "canonicalPassage"), Is.SameAs(serviceToKitchen));
        Assert.That(GetField<MonoBehaviour>(kitchenTrigger, "canonicalPassage"), Is.SameAs(kitchenToService));
        Assert.That(GetField<float>(serviceTrigger, "maxPlayerScreenDistance"), Is.EqualTo(145f));
        Assert.That(GetField<float>(kitchenTrigger, "maxPlayerScreenDistance"), Is.EqualTo(145f));
        Assert.That(GetProperty<object>(serviceToKitchen, "AnchorMigrationStage").ToString(),
            Is.EqualTo("AuthoredAnchors"));
        Assert.That(GetProperty<object>(kitchenToService, "AnchorMigrationStage").ToString(),
            Is.EqualTo("AuthoredAnchors"));
        AssertRoomViewLocalPassageAnchors(serviceToKitchen, serviceAnchor, kitchenAnchor);
        AssertRoomViewLocalPassageAnchors(kitchenToService, kitchenAnchor, serviceAnchor);

        Assert.That((bool)InvokeMethod(navigation, "TryTraverse", entranceToDining), Is.True);
        yield return WaitForCurrentRoom(navigation, DiningRoomName, 60);
        Assert.That((bool)InvokeMethod(navigation, "TryTraverse", diningToPantry), Is.True);
        yield return WaitForCurrentRoom(navigation, ButlersPantryRoomName, 60);
        Assert.That((bool)InvokeMethod(navigation, "TryTraverse", pantryToService), Is.True);
        yield return WaitForCurrentRoom(navigation, ServiceCorridorRoomName, 60);
        FreezeRoomLookForEvidence();
        yield return null;

        Assert.That(GetProperty<bool>(serviceView, "IsVisible"), Is.True);
        Assert.That(GetProperty<bool>(kitchenView, "IsVisible"), Is.False);
        InvokeMethod(player, "SetInputEnabled", true);
        Vector2 serviceApproach = ResolvePassageAnchorLogicalPosition(
            serviceToKitchen,
            "ApproachAnchor",
            player);
        Assert.That((bool)InvokeMethod(player, "TryWarpToExact", serviceApproach), Is.True);
        yield return null;
        AssertRoomViewLocalPlayerPosition(player, cameraManager, serviceAnchor, "Service approach");

        SetField(serviceTrigger, "lastPointerActivationFrame", -1);
        InvokeMethod(serviceTrigger, "ActivateDoor");
        yield return WaitForCurrentRoom(navigation, KitchenRoomName, 60);
        FreezeRoomLookForEvidence();
        yield return null;

        Assert.That(GetProperty<bool>(serviceView, "IsVisible"), Is.False);
        Assert.That(GetProperty<bool>(kitchenView, "IsVisible"), Is.True);
        AssertRoomViewLocalPlayerPosition(player, cameraManager, kitchenAnchor, "Kitchen arrival");

        SetField(kitchenTrigger, "lastPointerActivationFrame", -1);
        InvokeMethod(kitchenTrigger, "ActivateDoor");
        yield return WaitForCurrentRoom(navigation, ServiceCorridorRoomName, 60);
        FreezeRoomLookForEvidence();
        yield return null;

        Assert.That(GetProperty<bool>(serviceView, "IsVisible"), Is.True);
        Assert.That(GetProperty<bool>(kitchenView, "IsVisible"), Is.False);
        AssertRoomViewLocalPlayerPosition(player, cameraManager, serviceAnchor, "Service return arrival");
        AssertFixedRenderingResolution();
        Debug.Log(
            $"[Slice22Group08PlayMode] resolution={Screen.width}x{Screen.height} " +
            $"serviceLocal={Format(serviceAnchor)} kitchenLocal={Format(kitchenAnchor)} " +
            "callers=bound stages=authored-anchors space=room-view-local threshold=145");
    }

    [UnityTest]
    public IEnumerator ServiceCorridorChapelRoundTripUsesRoomViewLocalCanonicalPassages()
    {
        const string DiningRoomName = "Dining Room";
        const string ButlersPantryRoomName = "Butlers Pantry";
        const string ServiceCorridorRoomName = "Service Corridor";
        const string ChapelRoomName = "Chapel";
        Vector2 serviceAnchor = new Vector2(-133.2642f, -171.8258f);
        Vector2 chapelAnchor = new Vector2(461.4019f, -190.7613f);

        yield return BootGameplayFromRealMenu();

        MonoBehaviour navigation = RequireSingleSceneComponent("RoomNavigationManager");
        MonoBehaviour player = RequireComponentOnGameObject("Player", "PointClickPlayerMovement");
        MonoBehaviour cameraManager = RequireSingleSceneComponent("CameraManager");
        MonoBehaviour entranceToDining = RequireComponentOnGameObject(
            "DoorTrigger_GEH_DiningRoom",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour diningToPantry = RequireComponentOnGameObject(
            "DoorTrigger_DiningRoom_ButlersPantry",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour pantryToService = RequireComponentOnGameObject(
            "DoorTrigger_ButlersPantry_ServiceCorridor",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour serviceToChapel = RequireComponentOnGameObject(
            "DoorTrigger_ServiceCorridor_Chapel",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour chapelToService = RequireComponentOnGameObject(
            "DoorTrigger_Chapel_ServiceCorridor",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour serviceTrigger = RequireComponentOnGameObject(
            "DoorTrigger_ServiceCorridor_Chapel",
            "DoorTriggerNavigation");
        MonoBehaviour chapelTrigger = RequireComponentOnGameObject(
            "DoorTrigger_Chapel_ServiceCorridor",
            "DoorTriggerNavigation");
        MonoBehaviour serviceView = RequireRoomView("room.service-corridor");
        MonoBehaviour chapelView = RequireRoomView("room.chapel");

        Assert.That(GetField<MonoBehaviour>(serviceTrigger, "canonicalPassage"), Is.SameAs(serviceToChapel));
        Assert.That(GetField<MonoBehaviour>(chapelTrigger, "canonicalPassage"), Is.SameAs(chapelToService));
        Assert.That(GetField<float>(serviceTrigger, "maxPlayerScreenDistance"), Is.EqualTo(145f));
        Assert.That(GetField<float>(chapelTrigger, "maxPlayerScreenDistance"), Is.EqualTo(145f));
        Assert.That(GetProperty<object>(serviceToChapel, "AnchorMigrationStage").ToString(),
            Is.EqualTo("AuthoredAnchors"));
        Assert.That(GetProperty<object>(chapelToService, "AnchorMigrationStage").ToString(),
            Is.EqualTo("AuthoredAnchors"));
        AssertRoomViewLocalPassageAnchors(serviceToChapel, serviceAnchor, chapelAnchor);
        AssertRoomViewLocalPassageAnchors(chapelToService, chapelAnchor, serviceAnchor);

        Assert.That((bool)InvokeMethod(navigation, "TryTraverse", entranceToDining), Is.True);
        yield return WaitForCurrentRoom(navigation, DiningRoomName, 60);
        Assert.That((bool)InvokeMethod(navigation, "TryTraverse", diningToPantry), Is.True);
        yield return WaitForCurrentRoom(navigation, ButlersPantryRoomName, 60);
        Assert.That((bool)InvokeMethod(navigation, "TryTraverse", pantryToService), Is.True);
        yield return WaitForCurrentRoom(navigation, ServiceCorridorRoomName, 60);
        FreezeRoomLookForEvidence();
        yield return null;

        Assert.That(GetProperty<bool>(serviceView, "IsVisible"), Is.True);
        Assert.That(GetProperty<bool>(chapelView, "IsVisible"), Is.False);
        InvokeMethod(player, "SetInputEnabled", true);
        Vector2 serviceApproach = ResolvePassageAnchorLogicalPosition(
            serviceToChapel,
            "ApproachAnchor",
            player);
        Assert.That((bool)InvokeMethod(player, "TryWarpToExact", serviceApproach), Is.True);
        yield return null;
        AssertRoomViewLocalPlayerPosition(player, cameraManager, serviceAnchor, "Service approach");

        SetField(serviceTrigger, "lastPointerActivationFrame", -1);
        InvokeMethod(serviceTrigger, "ActivateDoor");
        yield return WaitForCurrentRoom(navigation, ChapelRoomName, 60);
        FreezeRoomLookForEvidence();
        yield return null;

        Assert.That(GetProperty<bool>(serviceView, "IsVisible"), Is.False);
        Assert.That(GetProperty<bool>(chapelView, "IsVisible"), Is.True);
        AssertRoomViewLocalPlayerPosition(player, cameraManager, chapelAnchor, "Chapel arrival");

        SetField(chapelTrigger, "lastPointerActivationFrame", -1);
        InvokeMethod(chapelTrigger, "ActivateDoor");
        yield return WaitForCurrentRoom(navigation, ServiceCorridorRoomName, 60);
        FreezeRoomLookForEvidence();
        yield return null;

        Assert.That(GetProperty<bool>(serviceView, "IsVisible"), Is.True);
        Assert.That(GetProperty<bool>(chapelView, "IsVisible"), Is.False);
        AssertRoomViewLocalPlayerPosition(player, cameraManager, serviceAnchor, "Service return arrival");
        AssertFixedRenderingResolution();
        Debug.Log(
            $"[Slice22Group09PlayMode] resolution={Screen.width}x{Screen.height} " +
            $"serviceLocal={Format(serviceAnchor)} chapelLocal={Format(chapelAnchor)} " +
            "callers=bound stages=authored-anchors space=room-view-local threshold=145 blockers=2/3");
    }

    [UnityTest]
    public IEnumerator GrandEntranceRearViewRoundTripUsesCanonicalDestinationRegions()
    {
        const string RearDisplayName = "Grand Entrance Hall Rear View";
        const string RearLegacyName = "Grand Entrance Hall Rear view";
        Vector2 entranceStart = new Vector2(0f, -2.2f);
        Vector2 rearStart = new Vector2(0f, -2f);
        // Locked by an A/B traversal through the preserved legacy path under this suite's
        // frozen pan/FOV; the default-preview rendered characterization uses other values.
        Vector2 expectedRearArrivalLocal = new Vector2(785.5335f, -391.4933f);
        Vector2 expectedEntranceArrivalLocal = new Vector2(418.3665f, -440.4239f);

        yield return BootGameplayFromRealMenu();

        MonoBehaviour gameRoot = RequireSingleSceneComponent("GameRoot");
        MonoBehaviour chapter = RequireSingleSceneComponent("ChapterManager");
        MonoBehaviour navigation = RequireSingleSceneComponent("RoomNavigationManager");
        MonoBehaviour player = RequireComponentOnGameObject("Player", "PointClickPlayerMovement");
        MonoBehaviour cameraManager = RequireSingleSceneComponent("CameraManager");
        MonoBehaviour forwardPassage = RequireComponentOnGameObject(
            "DoorTrigger_GEH_toRearView",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour reversePassage = RequireComponentOnGameObject(
            "DoorTrigger_GEH_Rear_GEH_Front",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour forwardTrigger = RequireComponentOnGameObject(
            "DoorTrigger_GEH_toRearView",
            "DoorTriggerNavigation");
        MonoBehaviour reverseTrigger = RequireComponentOnGameObject(
            "DoorTrigger_GEH_Rear_GEH_Front",
            "DoorTriggerNavigation");
        MonoBehaviour entranceView = RequireRoomView("room.grand-entrance-hall");
        MonoBehaviour rearView = RequireRoomView("room.grand-entrance-hall-rear-view");

        Assert.That(FindSceneComponents("Chateau.World.Rooms.RoomView"), Has.Count.EqualTo(18));
        Assert.That(FindSceneComponents("Chateau.World.Rooms.Passages.Passage"), Has.Count.EqualTo(36));
        List<MonoBehaviour> triggers = FindSceneComponents("DoorTriggerNavigation");
        Assert.That(triggers.Count(trigger => GetField<MonoBehaviour>(trigger, "canonicalPassage") != null),
            Is.EqualTo(36));
        Assert.That(triggers.Count(trigger => GetField<MonoBehaviour>(trigger, "canonicalPassage") == null),
            Is.EqualTo(9));
        Assert.That(GetField<MonoBehaviour>(forwardTrigger, "canonicalPassage"), Is.SameAs(forwardPassage));
        Assert.That(GetField<MonoBehaviour>(reverseTrigger, "canonicalPassage"), Is.SameAs(reversePassage));
        Assert.That(GetField<MonoBehaviour>(forwardTrigger, "navigationManager"), Is.SameAs(navigation));
        Assert.That(GetField<MonoBehaviour>(reverseTrigger, "navigationManager"), Is.SameAs(navigation));
        Assert.That(GetField<Transform>(forwardTrigger, "player"), Is.SameAs(player.transform));
        Assert.That(GetField<Transform>(reverseTrigger, "player"), Is.SameAs(player.transform));
        AudioSource passageAudioSource = RequireSceneGameObject("Audio_DoorOpen").GetComponent<AudioSource>();
        Assert.That(passageAudioSource, Is.Not.Null);
        Assert.That(GetField<AudioSource>(forwardTrigger, "doorOpenAudioSource"),
            Is.SameAs(passageAudioSource));
        Assert.That(GetField<AudioSource>(reverseTrigger, "doorOpenAudioSource"),
            Is.SameAs(passageAudioSource));
        UnityEngine.Object doorCatalog = GetField<UnityEngine.Object>(forwardTrigger, "doorOpenSoundCatalog");
        Assert.That(doorCatalog, Is.Not.Null);
        Assert.That(GetField<UnityEngine.Object>(reverseTrigger, "doorOpenSoundCatalog"),
            Is.SameAs(doorCatalog));

        object forwardDefinition = GetProperty<object>(forwardPassage, "Definition");
        object reverseDefinition = GetProperty<object>(reversePassage, "Definition");
        object rearDefinition = GetProperty<object>(rearView, "Definition");
        Assert.That(GetProperty<string>(rearDefinition, "DisplayName"), Is.EqualTo(RearDisplayName));
        Assert.That(GetProperty<string>(rearDefinition, "PrimaryLegacyName"), Is.EqualTo(RearLegacyName));
        Assert.That(GetProperty<object>(rearView, "LegacyContentGroup"), Is.Not.Null);
        Assert.That(GetProperty<string>(GetProperty<object>(rearView, "LegacyContentGroup"), "RoomName"),
            Is.EqualTo(RearLegacyName));
        Assert.That(GetProperty<string>(forwardDefinition, "CompatibilityDestinationRoomName"),
            Is.EqualTo(RearDisplayName));
        Assert.That(GetProperty<bool>(forwardDefinition, "HasExplicitCompatibilityDestinationRoomName"),
            Is.True);
        Assert.That(GetProperty<string>(reverseDefinition, "CompatibilityDestinationRoomName"),
            Is.EqualTo(EntranceRoomName));
        Assert.That(GetProperty<bool>(reverseDefinition, "HasExplicitCompatibilityDestinationRoomName"),
            Is.False);

        object database = GetProperty<object>(gameRoot, "Database");
        object[] registeredDefinitions = ((IEnumerable)GetProperty<object>(database, "Definitions"))
            .Cast<object>()
            .ToArray();
        Assert.That(registeredDefinitions, Has.Length.EqualTo(55));
        Assert.That(registeredDefinitions, Does.Contain(forwardDefinition));
        Assert.That(registeredDefinitions, Does.Contain(reverseDefinition));
        Assert.That(registeredDefinitions, Does.Contain(rearDefinition));

        AssertCanonicalDestinationRegionPassage(
            forwardPassage,
            entranceView,
            reversePassage,
            "passage.grand-entrance-hall.grand-entrance-hall-rear-view",
            "GEH_GEH_Rear",
            RearDisplayName,
            new Vector2(0.00030518f, -456.4991f),
            new Vector2(-764.707458f, -451.0935f),
            new Vector2(-764.707458f, -423.094452f),
            new Vector2(785.200256f, -423.094452f),
            new Vector2(785.200256f, -451.0935f));
        AssertCanonicalDestinationRegionPassage(
            reversePassage,
            rearView,
            forwardPassage,
            "passage.grand-entrance-hall-rear-view.grand-entrance-hall",
            "GEH_Rear_GEH_Front",
            EntranceRoomName,
            new Vector2(10.2463989f, -437.093964f),
            new Vector2(-835.9997f, -470.4991f),
            new Vector2(-835.9997f, -442.4991f),
            new Vector2(836.0003f, -442.4991f),
            new Vector2(836.0003f, -470.4991f));

        Assert.That(GetField<bool>(forwardTrigger, "useBottomScreenEdgeInteraction"), Is.True);
        Assert.That(GetField<bool>(reverseTrigger, "useBottomScreenEdgeInteraction"), Is.True);
        Assert.That(GetField<float>(forwardTrigger, "bottomScreenEdgeActivationPixels"), Is.EqualTo(28f));
        Assert.That(GetField<float>(reverseTrigger, "bottomScreenEdgeActivationPixels"), Is.EqualTo(28f));
        Assert.That(GetField<bool>(forwardTrigger, "requirePlayerProximity"), Is.False);
        Assert.That(GetField<bool>(reverseTrigger, "requirePlayerProximity"), Is.False);

        // Isolate this navigation ownership probe from the concurrent Chapter 1 intro input lease.
        InvokeMethod(chapter, "StopChapterCoroutines");
        InvokeMethod(chapter, "StopActiveDialogueForDebugTransition");
        InvokeMethod(player, "SetInputEnabled", true);
        Assert.That(GetProperty<bool>(player, "InputEnabled"), Is.True);
        FreezeRoomLookForEvidence();
        yield return null;

        int movementArrivals = 0;
        int movementStops = 0;
        Action recordArrival = () => movementArrivals++;
        Action recordMovementStop = () => movementStops++;
        EventInfo arrivalEvent = player.GetType().GetEvent("ArrivedAtDestination");
        EventInfo movementStopEvent = player.GetType().GetEvent("MovementStopped");
        Assert.That(arrivalEvent, Is.Not.Null);
        Assert.That(movementStopEvent, Is.Not.Null);
        arrivalEvent.AddEventHandler(player, recordArrival);
        movementStopEvent.AddEventHandler(player, recordMovementStop);

        try
        {
            Assert.That((bool)InvokeMethod(player, "TryWarpTo", entranceStart, true), Is.True);
            movementArrivals = 0;
            movementStops = 0;
            SetField(forwardTrigger, "lastPointerActivationFrame", -1);
            InvokeMethod(forwardTrigger, "ActivateDoor");
            yield return WaitForCurrentRoom(navigation, RearDisplayName, 60);
            FreezeRoomLookForEvidence();
            yield return null;

            Assert.That(GetProperty<object>(navigation, "CurrentRoomDefinition"), Is.SameAs(rearDefinition));
            Assert.That(GetProperty<bool>(entranceView, "IsVisible"), Is.False);
            Assert.That(GetProperty<bool>(rearView, "IsVisible"), Is.True);
            Assert.That(GetProperty<bool>(player, "HasDestination"), Is.False);
            Assert.That(GetProperty<bool>(player, "InputEnabled"), Is.True);
            Assert.That(movementArrivals, Is.Zero);
            Assert.That(movementStops, Is.Zero);
            AssertRoomViewLocalPlayerPosition(
                player,
                cameraManager,
                expectedRearArrivalLocal,
                "Grand Entrance rear canonical region arrival");

            Assert.That((bool)InvokeMethod(player, "TryWarpTo", rearStart, true), Is.True);
            movementArrivals = 0;
            movementStops = 0;
            SetField(reverseTrigger, "lastPointerActivationFrame", -1);
            InvokeMethod(reverseTrigger, "ActivateDoor");
            yield return WaitForCurrentRoom(navigation, EntranceRoomName, 60);
            FreezeRoomLookForEvidence();
            yield return null;

            Assert.That(GetProperty<bool>(entranceView, "IsVisible"), Is.True);
            Assert.That(GetProperty<bool>(rearView, "IsVisible"), Is.False);
            Assert.That(GetProperty<bool>(player, "HasDestination"), Is.False);
            Assert.That(GetProperty<bool>(player, "InputEnabled"), Is.True);
            Assert.That(movementArrivals, Is.Zero);
            Assert.That(movementStops, Is.Zero);
            AssertRoomViewLocalPlayerPosition(
                player,
                cameraManager,
                expectedEntranceArrivalLocal,
                "Grand Entrance canonical region return");
            AssertFixedRenderingResolution();
        }
        finally
        {
            arrivalEvent.RemoveEventHandler(player, recordArrival);
            movementStopEvent.RemoveEventHandler(player, recordMovementStop);
            InvokeMethod(forwardTrigger, "CancelPendingPlayerApproach");
            InvokeMethod(reverseTrigger, "CancelPendingPlayerApproach");
            if (GetProperty<bool>(player, "HasDestination"))
            {
                InvokeMethod(player, "CancelDestination");
            }
        }

        Debug.Log(
            $"[Slice22Group10PlayMode] resolution={Screen.width}x{Screen.height} " +
            $"rearLocal={Format(expectedRearArrivalLocal)} entranceLocal={Format(expectedEntranceArrivalLocal)} " +
            "roomViews=18 passages=36 callers=36/9 stages=authored-anchors " +
            "placement=best-reachable-region aliases=Grand Entrance Hall Rear View/Grand Entrance Hall Rear view");
    }

    [UnityTest]
    public IEnumerator GrandEntranceRearBilliardRoundTripUsesCanonicalSourceAndDestinationRegions()
    {
        const string RearDisplayName = "Grand Entrance Hall Rear View";
        const string RearLegacyName = "Grand Entrance Hall Rear view";
        const string BilliardRoomName = "Billiard Room";
        Vector2 rearFarStart = new Vector2(0f, -2f);
        Vector2 billiardFarStart = new Vector2(0f, -2f);

        yield return BootGameplayFromRealMenu();

        MonoBehaviour gameRoot = RequireSingleSceneComponent("GameRoot");
        MonoBehaviour chapter = RequireSingleSceneComponent("ChapterManager");
        MonoBehaviour navigation = RequireSingleSceneComponent("RoomNavigationManager");
        MonoBehaviour player = RequireComponentOnGameObject("Player", "PointClickPlayerMovement");
        MonoBehaviour cameraManager = RequireSingleSceneComponent("CameraManager");
        MonoBehaviour setupPassage = RequireComponentOnGameObject(
            "DoorTrigger_GEH_toRearView",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour setupTrigger = RequireComponentOnGameObject(
            "DoorTrigger_GEH_toRearView",
            "DoorTriggerNavigation");
        MonoBehaviour forwardPassage = RequireComponentOnGameObject(
            "DoorTrigger_GEH_Rear_BilliardRoom",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour reversePassage = RequireComponentOnGameObject(
            "DoorTrigger_BilliardRoom_GEH",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour forwardTrigger = RequireComponentOnGameObject(
            "DoorTrigger_GEH_Rear_BilliardRoom",
            "DoorTriggerNavigation");
        MonoBehaviour reverseTrigger = RequireComponentOnGameObject(
            "DoorTrigger_BilliardRoom_GEH",
            "DoorTriggerNavigation");
        MonoBehaviour entranceView = RequireRoomView("room.grand-entrance-hall");
        MonoBehaviour rearView = RequireRoomView("room.grand-entrance-hall-rear-view");
        MonoBehaviour billiardView = RequireRoomView("room.billiard-room");

        Assert.That(FindSceneComponents("Chateau.World.Rooms.RoomView"), Has.Count.EqualTo(18));
        Assert.That(FindSceneComponents("Chateau.World.Rooms.Passages.Passage"), Has.Count.EqualTo(36));
        List<MonoBehaviour> triggers = FindSceneComponents("DoorTriggerNavigation");
        Assert.That(triggers, Has.Count.EqualTo(45));
        Assert.That(triggers.Count(trigger => GetField<MonoBehaviour>(trigger, "canonicalPassage") != null),
            Is.EqualTo(36));
        Assert.That(triggers.Count(trigger => GetField<MonoBehaviour>(trigger, "canonicalPassage") == null),
            Is.EqualTo(9));
        Assert.That(GetField<MonoBehaviour>(forwardTrigger, "canonicalPassage"), Is.SameAs(forwardPassage));
        Assert.That(GetField<MonoBehaviour>(reverseTrigger, "canonicalPassage"), Is.SameAs(reversePassage));
        Assert.That(GetField<MonoBehaviour>(forwardTrigger, "navigationManager"), Is.SameAs(navigation));
        Assert.That(GetField<MonoBehaviour>(reverseTrigger, "navigationManager"), Is.SameAs(navigation));
        Assert.That(GetField<Transform>(forwardTrigger, "player"), Is.SameAs(player.transform));
        Assert.That(GetField<Transform>(reverseTrigger, "player"), Is.SameAs(player.transform));
        AudioSource passageAudioSource = RequireSceneGameObject("Audio_DoorOpen").GetComponent<AudioSource>();
        Assert.That(passageAudioSource, Is.Not.Null);
        Assert.That(GetField<AudioSource>(forwardTrigger, "doorOpenAudioSource"),
            Is.SameAs(passageAudioSource));
        Assert.That(GetField<AudioSource>(reverseTrigger, "doorOpenAudioSource"),
            Is.SameAs(passageAudioSource));
        UnityEngine.Object doorCatalog = GetField<UnityEngine.Object>(forwardTrigger, "doorOpenSoundCatalog");
        Assert.That(doorCatalog, Is.Not.Null);
        Assert.That(GetField<UnityEngine.Object>(reverseTrigger, "doorOpenSoundCatalog"),
            Is.SameAs(doorCatalog));

        object forwardDefinition = GetProperty<object>(forwardPassage, "Definition");
        object reverseDefinition = GetProperty<object>(reversePassage, "Definition");
        object rearDefinition = GetProperty<object>(rearView, "Definition");
        object billiardDefinition = GetProperty<object>(billiardView, "Definition");
        Assert.That(GetProperty<string>(rearDefinition, "DisplayName"), Is.EqualTo(RearDisplayName));
        Assert.That(GetProperty<string>(rearDefinition, "PrimaryLegacyName"), Is.EqualTo(RearLegacyName));
        Assert.That(GetProperty<object>(rearView, "LegacyContentGroup"), Is.Not.Null);
        Assert.That(GetProperty<string>(GetProperty<object>(rearView, "LegacyContentGroup"), "RoomName"),
            Is.EqualTo(RearLegacyName));
        Assert.That(GetProperty<object>(forwardDefinition, "SourceRoom"), Is.SameAs(rearDefinition));
        Assert.That(GetProperty<object>(forwardDefinition, "DestinationRoom"), Is.SameAs(billiardDefinition));
        Assert.That(GetProperty<object>(forwardDefinition, "Reverse"), Is.SameAs(reverseDefinition));
        Assert.That(GetProperty<object>(reverseDefinition, "SourceRoom"), Is.SameAs(billiardDefinition));
        Assert.That(GetProperty<object>(reverseDefinition, "DestinationRoom"), Is.SameAs(rearDefinition));
        Assert.That(GetProperty<object>(reverseDefinition, "Reverse"), Is.SameAs(forwardDefinition));
        Assert.That(GetProperty<string>(forwardDefinition, "CompatibilityDestinationRoomName"),
            Is.EqualTo(BilliardRoomName));
        Assert.That(GetProperty<bool>(forwardDefinition, "HasExplicitCompatibilityDestinationRoomName"),
            Is.False);
        Assert.That(GetProperty<string>(reverseDefinition, "CompatibilityDestinationRoomName"),
            Is.EqualTo(RearDisplayName));
        Assert.That(GetProperty<bool>(reverseDefinition, "HasExplicitCompatibilityDestinationRoomName"),
            Is.True);

        object database = GetProperty<object>(gameRoot, "Database");
        object[] registeredDefinitions = ((IEnumerable)GetProperty<object>(database, "Definitions"))
            .Cast<object>()
            .ToArray();
        Assert.That(registeredDefinitions, Has.Length.EqualTo(55));
        Assert.That(registeredDefinitions, Does.Contain(forwardDefinition));
        Assert.That(registeredDefinitions, Does.Contain(reverseDefinition));
        Assert.That(registeredDefinitions, Does.Contain(rearDefinition));
        Assert.That(registeredDefinitions, Does.Contain(billiardDefinition));

        AssertCanonicalSourceAndDestinationRegionPassage(
            forwardPassage,
            rearView,
            reversePassage,
            "passage.grand-entrance-hall-rear-view.billiard-room",
            "GEH_BilliardRoom",
            BilliardRoomName,
            false,
            new Vector2(-745.00006f, -114.72981f),
            new Vector2(-745.00006f, 238.13548f),
            new Vector2(-501.32404f, 238.13548f),
            new Vector2(-501.32404f, -114.72981f));
        AssertCanonicalSourceAndDestinationRegionPassage(
            reversePassage,
            billiardView,
            forwardPassage,
            "passage.billiard-room.grand-entrance-hall-rear-view",
            "BilliardRoom_GEH",
            RearDisplayName,
            true,
            new Vector2(579.6167f, -250.84499f),
            new Vector2(579.6167f, 31.911606f),
            new Vector2(702.0674f, 31.911606f),
            new Vector2(702.0674f, -250.84499f));

        Assert.That(GetField<bool>(forwardTrigger, "requirePlayerProximity"), Is.True);
        Assert.That(GetField<bool>(reverseTrigger, "requirePlayerProximity"), Is.True);
        Assert.That(GetField<bool>(forwardTrigger, "walkPlayerToTriggerWhenFar"), Is.True);
        Assert.That(GetField<bool>(reverseTrigger, "walkPlayerToTriggerWhenFar"), Is.True);
        Assert.That(GetField<bool>(forwardTrigger, "autoActivateAfterApproach"), Is.True);
        Assert.That(GetField<bool>(reverseTrigger, "autoActivateAfterApproach"), Is.True);
        Assert.That(GetField<float>(forwardTrigger, "maxPlayerScreenDistance"), Is.EqualTo(145f));
        Assert.That(GetField<float>(reverseTrigger, "maxPlayerScreenDistance"), Is.EqualTo(145f));

        // Isolate this navigation ownership probe from the concurrent Chapter 1 intro input lease.
        InvokeMethod(chapter, "StopChapterCoroutines");
        InvokeMethod(chapter, "StopActiveDialogueForDebugTransition");
        InvokeMethod(player, "SetInputEnabled", true);
        Assert.That(GetProperty<bool>(player, "InputEnabled"), Is.True);
        float originalMoveSpeed = GetField<float>(player, "moveSpeed");
        SetField(player, "moveSpeed", 1000f);
        FreezeRoomLookForEvidence();
        yield return null;

        List<Vector2> approachStops = new List<Vector2>(2);
        List<string> approachRooms = new List<string>(2);
        int movementStops = 0;
        Action recordArrival = () =>
        {
            approachStops.Add(GetProperty<Vector2>(player, "LogicalPosition"));
            approachRooms.Add(GetProperty<string>(navigation, "CurrentRoom"));
        };
        Action recordMovementStop = () => movementStops++;
        EventInfo arrivalEvent = player.GetType().GetEvent("ArrivedAtDestination");
        EventInfo movementStopEvent = player.GetType().GetEvent("MovementStopped");
        Assert.That(arrivalEvent, Is.Not.Null);
        Assert.That(movementStopEvent, Is.Not.Null);
        arrivalEvent.AddEventHandler(player, recordArrival);
        movementStopEvent.AddEventHandler(player, recordMovementStop);

        Vector2 billiardLandingLocal = Vector2.zero;
        Vector2 rearLandingLocal = Vector2.zero;
        try
        {
            SetField(setupTrigger, "lastPointerActivationFrame", -1);
            InvokeMethod(setupTrigger, "ActivateDoor");
            yield return WaitForCurrentRoom(navigation, RearDisplayName, 60);
            FreezeRoomLookForEvidence();
            yield return null;
            Assert.That(GetProperty<object>(setupPassage, "Definition"), Is.Not.Null);
            Assert.That(GetProperty<object>(navigation, "CurrentRoomDefinition"), Is.SameAs(rearDefinition));
            Assert.That(GetProperty<bool>(entranceView, "IsVisible"), Is.False);
            Assert.That(GetProperty<bool>(rearView, "IsVisible"), Is.True);

            Assert.That((bool)InvokeMethod(player, "TryWarpToExact", rearFarStart), Is.True);
            SetField(forwardTrigger, "lastPointerActivationFrame", -1);
            InvokeMethod(forwardTrigger, "ActivateDoor");
            Assert.That(GetProperty<bool>(player, "HasDestination"), Is.True,
                "The Rear-to-Billiard leg must exercise a real source-region approach.");
            yield return WaitForCurrentRoom(navigation, BilliardRoomName, 240);
            FreezeRoomLookForEvidence();
            yield return null;

            Assert.That(GetProperty<object>(navigation, "CurrentRoomDefinition"), Is.SameAs(billiardDefinition));
            Assert.That(GetProperty<bool>(rearView, "IsVisible"), Is.False);
            Assert.That(GetProperty<bool>(billiardView, "IsVisible"), Is.True);
            Assert.That(GetProperty<bool>(player, "HasDestination"), Is.False);
            Assert.That(GetProperty<bool>(player, "InputEnabled"), Is.True);
            Assert.That(approachStops, Has.Count.EqualTo(1));
            Assert.That(movementStops, Is.EqualTo(1));
            Assert.That(approachRooms, Is.EqualTo(new[] { RearDisplayName }));
            AssertFinite(approachStops[0], "Rear source-region approach stop");
            billiardLandingLocal = GetRoomViewLocalPlayerPosition(player, cameraManager,
                "Billiard destination-region landing");
            AssertFinite(billiardLandingLocal, "Billiard destination-region landing");

            Assert.That((bool)InvokeMethod(player, "TryWarpToExact", billiardFarStart), Is.True);
            SetField(reverseTrigger, "lastPointerActivationFrame", -1);
            InvokeMethod(reverseTrigger, "ActivateDoor");
            Assert.That(GetProperty<bool>(player, "HasDestination"), Is.True,
                "The Billiard-to-Rear leg must exercise a real source-region approach.");
            yield return WaitForCurrentRoom(navigation, RearDisplayName, 240);
            FreezeRoomLookForEvidence();
            yield return null;

            Assert.That(GetProperty<string>(navigation, "CurrentRoom"), Is.EqualTo(RearDisplayName),
                "The reverse definition must preserve the title-case compatibility destination.");
            Assert.That(GetProperty<object>(navigation, "CurrentRoomDefinition"), Is.SameAs(rearDefinition));
            Assert.That(GetProperty<bool>(rearView, "IsVisible"), Is.True);
            Assert.That(GetProperty<bool>(billiardView, "IsVisible"), Is.False);
            Assert.That(GetProperty<bool>(player, "HasDestination"), Is.False);
            Assert.That(GetProperty<bool>(player, "InputEnabled"), Is.True);
            Assert.That(approachStops, Has.Count.EqualTo(2));
            Assert.That(movementStops, Is.EqualTo(2));
            Assert.That(approachRooms, Is.EqualTo(new[] { RearDisplayName, BilliardRoomName }));
            AssertFinite(approachStops[1], "Billiard source-region approach stop");
            rearLandingLocal = GetRoomViewLocalPlayerPosition(player, cameraManager,
                "Rear destination-region landing");
            AssertFinite(rearLandingLocal, "Rear destination-region landing");
            AssertFixedRenderingResolution();
        }
        finally
        {
            arrivalEvent.RemoveEventHandler(player, recordArrival);
            movementStopEvent.RemoveEventHandler(player, recordMovementStop);
            SetField(player, "moveSpeed", originalMoveSpeed);
            InvokeMethod(setupTrigger, "CancelPendingPlayerApproach");
            InvokeMethod(forwardTrigger, "CancelPendingPlayerApproach");
            InvokeMethod(reverseTrigger, "CancelPendingPlayerApproach");
            if (GetProperty<bool>(player, "HasDestination"))
            {
                InvokeMethod(player, "CancelDestination");
            }
        }

        Debug.Log(
            $"[Slice22Group11PlayMode] resolution={Screen.width}x{Screen.height} " +
            $"billiardLocal={Format(billiardLandingLocal)} rearLocal={Format(rearLandingLocal)} " +
            $"forwardStop={Format(approachStops[0])} reverseStop={Format(approachStops[1])} " +
            "roomViews=18 passages=36 callers=36/9 stages=authored-anchors " +
            "placement=source-region/destination-region " +
            "compatibilityDestination=Grand Entrance Hall Rear View");
    }

    [UnityTest]
    public IEnumerator GrandEntranceRearConservatoryMixedRoundTripUsesCanonicalSourceAndDestinationRegions()
    {
        const string RearDisplayName = "Grand Entrance Hall Rear View";
        const string RearLegacyName = "Grand Entrance Hall Rear view";
        const string ConservatoryRoomName = "Conservatory";
        Vector2 rearFarStart = new Vector2(-7f, -2f);
        Vector2 conservatoryStart = new Vector2(0f, -2f);

        yield return BootGameplayFromRealMenu();

        MonoBehaviour gameRoot = RequireSingleSceneComponent("GameRoot");
        MonoBehaviour chapter = RequireSingleSceneComponent("ChapterManager");
        MonoBehaviour navigation = RequireSingleSceneComponent("RoomNavigationManager");
        MonoBehaviour player = RequireComponentOnGameObject("Player", "PointClickPlayerMovement");
        MonoBehaviour cameraManager = RequireSingleSceneComponent("CameraManager");
        MonoBehaviour setupTrigger = RequireComponentOnGameObject(
            "DoorTrigger_GEH_toRearView",
            "DoorTriggerNavigation");
        MonoBehaviour forwardPassage = RequireComponentOnGameObject(
            "DoorTrigger_GEH_Rear_Conservatory",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour reversePassage = RequireComponentOnGameObject(
            "DoorTrigger_Conservatory_GEH_Rear_View",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour forwardTrigger = RequireComponentOnGameObject(
            "DoorTrigger_GEH_Rear_Conservatory",
            "DoorTriggerNavigation");
        MonoBehaviour reverseTrigger = RequireComponentOnGameObject(
            "DoorTrigger_Conservatory_GEH_Rear_View",
            "DoorTriggerNavigation");
        MonoBehaviour rearView = RequireRoomView("room.grand-entrance-hall-rear-view");
        MonoBehaviour conservatoryView = RequireRoomView("room.conservatory");

        Assert.That(FindSceneComponents("Chateau.World.Rooms.RoomView"), Has.Count.EqualTo(18));
        Assert.That(FindSceneComponents("Chateau.World.Rooms.Passages.Passage"), Has.Count.EqualTo(36));
        List<MonoBehaviour> triggers = FindSceneComponents("DoorTriggerNavigation");
        Assert.That(triggers, Has.Count.EqualTo(45));
        Assert.That(triggers.Count(trigger => GetField<MonoBehaviour>(trigger, "canonicalPassage") != null),
            Is.EqualTo(36));
        Assert.That(triggers.Count(trigger => GetField<MonoBehaviour>(trigger, "canonicalPassage") == null),
            Is.EqualTo(9));
        Assert.That(GetField<MonoBehaviour>(forwardTrigger, "canonicalPassage"), Is.SameAs(forwardPassage));
        Assert.That(GetField<MonoBehaviour>(reverseTrigger, "canonicalPassage"), Is.SameAs(reversePassage));
        Assert.That(GetField<MonoBehaviour>(forwardTrigger, "navigationManager"), Is.SameAs(navigation));
        Assert.That(GetField<MonoBehaviour>(reverseTrigger, "navigationManager"), Is.SameAs(navigation));
        Assert.That(GetField<Transform>(forwardTrigger, "player"), Is.SameAs(player.transform));
        Assert.That(GetField<Transform>(reverseTrigger, "player"), Is.SameAs(player.transform));

        object forwardDefinition = GetProperty<object>(forwardPassage, "Definition");
        object reverseDefinition = GetProperty<object>(reversePassage, "Definition");
        object rearDefinition = GetProperty<object>(rearView, "Definition");
        object conservatoryDefinition = GetProperty<object>(conservatoryView, "Definition");
        Assert.That(GetProperty<string>(rearDefinition, "DisplayName"), Is.EqualTo(RearDisplayName));
        Assert.That(GetProperty<string>(rearDefinition, "PrimaryLegacyName"), Is.EqualTo(RearLegacyName));
        Assert.That(GetProperty<string>(conservatoryDefinition, "DisplayName"),
            Is.EqualTo(ConservatoryRoomName));
        Assert.That(GetProperty<object>(forwardDefinition, "SourceRoom"), Is.SameAs(rearDefinition));
        Assert.That(GetProperty<object>(forwardDefinition, "DestinationRoom"),
            Is.SameAs(conservatoryDefinition));
        Assert.That(GetProperty<object>(forwardDefinition, "Reverse"), Is.SameAs(reverseDefinition));
        Assert.That(GetProperty<object>(reverseDefinition, "SourceRoom"),
            Is.SameAs(conservatoryDefinition));
        Assert.That(GetProperty<object>(reverseDefinition, "DestinationRoom"), Is.SameAs(rearDefinition));
        Assert.That(GetProperty<object>(reverseDefinition, "Reverse"), Is.SameAs(forwardDefinition));

        object database = GetProperty<object>(gameRoot, "Database");
        object[] registeredDefinitions = ((IEnumerable)GetProperty<object>(database, "Definitions"))
            .Cast<object>()
            .ToArray();
        Assert.That(registeredDefinitions, Has.Length.EqualTo(55));
        Assert.That(registeredDefinitions, Does.Contain(forwardDefinition));
        Assert.That(registeredDefinitions, Does.Contain(reverseDefinition));
        Assert.That(registeredDefinitions, Does.Contain(conservatoryDefinition));

        AssertCanonicalSourceAndDestinationRegionPassage(
            forwardPassage,
            rearView,
            reversePassage,
            "passage.grand-entrance-hall-rear-view.conservatory",
            "GEH_Conservatory",
            ConservatoryRoomName,
            false,
            new Vector2(-764.7062f, -451.093567f),
            new Vector2(-764.7062f, -423.094543f),
            new Vector2(785.199036f, -423.094543f),
            new Vector2(785.199036f, -451.093567f));
        AssertCanonicalSourceAndDestinationRegionPassage(
            reversePassage,
            conservatoryView,
            forwardPassage,
            "passage.conservatory.grand-entrance-hall-rear-view",
            "Conservatory_GEH_Rear_View",
            RearDisplayName,
            true,
            new Vector2(-53.342514f, -138.5048f),
            new Vector2(-53.342514f, 72.50481f),
            new Vector2(53.3424873f, 72.50481f),
            new Vector2(53.3424873f, -138.5048f));

        Assert.That(GetField<bool>(forwardTrigger, "useBottomScreenEdgeInteraction"), Is.False);
        Assert.That(GetField<bool>(forwardTrigger, "requirePlayerProximity"), Is.True);
        Assert.That(GetField<bool>(forwardTrigger, "walkPlayerToTriggerWhenFar"), Is.True);
        Assert.That(GetField<bool>(reverseTrigger, "useBottomScreenEdgeInteraction"), Is.True);
        Assert.That(GetField<float>(reverseTrigger, "bottomScreenEdgeActivationPixels"), Is.EqualTo(28f));
        Assert.That(GetField<bool>(reverseTrigger, "requirePlayerProximity"), Is.False);
        Assert.That(GetField<bool>(reverseTrigger, "walkPlayerToTriggerWhenFar"), Is.False);
        Assert.That(GetField<bool>(forwardTrigger, "autoActivateAfterApproach"), Is.True);
        Assert.That(GetField<bool>(reverseTrigger, "autoActivateAfterApproach"), Is.True);

        InvokeMethod(chapter, "StopChapterCoroutines");
        InvokeMethod(chapter, "StopActiveDialogueForDebugTransition");
        InvokeMethod(player, "SetInputEnabled", true);
        float originalMoveSpeed = GetField<float>(player, "moveSpeed");
        SetField(player, "moveSpeed", 1000f);
        FreezeRoomLookForEvidence();
        yield return null;

        List<Vector2> approachStops = new List<Vector2>(1);
        List<string> approachRooms = new List<string>(1);
        int movementStops = 0;
        Action recordArrival = () =>
        {
            approachStops.Add(GetProperty<Vector2>(player, "LogicalPosition"));
            approachRooms.Add(GetProperty<string>(navigation, "CurrentRoom"));
        };
        Action recordMovementStop = () => movementStops++;
        EventInfo arrivalEvent = player.GetType().GetEvent("ArrivedAtDestination");
        EventInfo movementStopEvent = player.GetType().GetEvent("MovementStopped");
        Assert.That(arrivalEvent, Is.Not.Null);
        Assert.That(movementStopEvent, Is.Not.Null);
        arrivalEvent.AddEventHandler(player, recordArrival);
        movementStopEvent.AddEventHandler(player, recordMovementStop);

        Vector2 conservatoryLandingLocal = Vector2.zero;
        Vector2 rearLandingLocal = Vector2.zero;
        try
        {
            SetField(setupTrigger, "lastPointerActivationFrame", -1);
            InvokeMethod(setupTrigger, "ActivateDoor");
            yield return WaitForCurrentRoom(navigation, RearDisplayName, 60);
            FreezeRoomLookForEvidence();
            yield return null;
            Assert.That(GetProperty<bool>(rearView, "IsVisible"), Is.True);
            Assert.That(GetProperty<bool>(conservatoryView, "IsVisible"), Is.False);

            Assert.That((bool)InvokeMethod(player, "TryWarpTo", rearFarStart, true), Is.True);
            SetField(forwardTrigger, "lastPointerActivationFrame", -1);
            InvokeMethod(forwardTrigger, "ActivateDoor");
            Assert.That(GetProperty<bool>(player, "HasDestination"), Is.True,
                "The Rear standard door must exercise its real source-region approach.");
            yield return WaitForCurrentRoom(navigation, ConservatoryRoomName, 240);
            FreezeRoomLookForEvidence();
            yield return null;

            Assert.That(GetProperty<object>(navigation, "CurrentRoomDefinition"),
                Is.SameAs(conservatoryDefinition));
            Assert.That(GetProperty<bool>(rearView, "IsVisible"), Is.False);
            Assert.That(GetProperty<bool>(conservatoryView, "IsVisible"), Is.True);
            Assert.That(GetProperty<bool>(player, "HasDestination"), Is.False);
            Assert.That(approachStops, Has.Count.EqualTo(1));
            Assert.That(movementStops, Is.EqualTo(1));
            Assert.That(approachRooms, Is.EqualTo(new[] { RearDisplayName }));
            conservatoryLandingLocal = GetRoomViewLocalPlayerPosition(
                player,
                cameraManager,
                "Conservatory destination-region landing");
            AssertFinite(conservatoryLandingLocal, "Conservatory destination-region landing");

            Assert.That((bool)InvokeMethod(player, "TryWarpTo", conservatoryStart, true), Is.True);
            SetField(reverseTrigger, "lastPointerActivationFrame", -1);
            InvokeMethod(reverseTrigger, "ActivateDoor");
            Assert.That(GetProperty<bool>(player, "HasDestination"), Is.False,
                "The Conservatory bottom-edge return must remain immediate.");
            yield return WaitForCurrentRoom(navigation, RearDisplayName, 60);
            FreezeRoomLookForEvidence();
            yield return null;

            Assert.That(GetProperty<string>(navigation, "CurrentRoom"), Is.EqualTo(RearDisplayName));
            Assert.That(GetProperty<object>(navigation, "CurrentRoomDefinition"), Is.SameAs(rearDefinition));
            Assert.That(GetProperty<bool>(rearView, "IsVisible"), Is.True);
            Assert.That(GetProperty<bool>(conservatoryView, "IsVisible"), Is.False);
            Assert.That(approachStops, Has.Count.EqualTo(1),
                "The bottom-edge return must not emit a movement-arrival callback.");
            Assert.That(movementStops, Is.EqualTo(1),
                "The bottom-edge return must not emit a movement-stop callback.");
            rearLandingLocal = GetRoomViewLocalPlayerPosition(
                player,
                cameraManager,
                "Rear destination-region landing");
            AssertFinite(rearLandingLocal, "Rear destination-region landing");
            AssertFixedRenderingResolution();
        }
        finally
        {
            arrivalEvent.RemoveEventHandler(player, recordArrival);
            movementStopEvent.RemoveEventHandler(player, recordMovementStop);
            SetField(player, "moveSpeed", originalMoveSpeed);
            InvokeMethod(setupTrigger, "CancelPendingPlayerApproach");
            InvokeMethod(forwardTrigger, "CancelPendingPlayerApproach");
            InvokeMethod(reverseTrigger, "CancelPendingPlayerApproach");
            if (GetProperty<bool>(player, "HasDestination"))
            {
                InvokeMethod(player, "CancelDestination");
            }
        }

        Debug.Log(
            $"[Slice22Group12PlayMode] resolution={Screen.width}x{Screen.height} " +
            $"conservatoryLocal={Format(conservatoryLandingLocal)} rearLocal={Format(rearLandingLocal)} " +
            $"forwardStop={Format(approachStops[0])} " +
            "roomViews=18 passages=36 callers=36/9 stages=authored-anchors " +
            "placement=source-region/destination-region profile=standard/bottom-edge " +
            "compatibilityDestination=Grand Entrance Hall Rear View");
    }

    [UnityTest]
    public IEnumerator ServiceCorridorSideStairMudroomFarRoundTripUsesCanonicalReciprocalRegions()
    {
        const string ServiceRoom = "Service Corridor";
        const string SideDisplay = "Side Stair & Mudroom";
        const string SideLegacy = "Side Stair Mudroom";

        yield return BootGameplayFromRealMenu();

        MonoBehaviour gameRoot = RequireSingleSceneComponent("GameRoot");
        MonoBehaviour chapter = RequireSingleSceneComponent("ChapterManager");
        MonoBehaviour navigation = RequireSingleSceneComponent("RoomNavigationManager");
        MonoBehaviour player = RequireComponentOnGameObject("Player", "PointClickPlayerMovement");
        MonoBehaviour cameraManager = RequireSingleSceneComponent("CameraManager");
        MonoBehaviour forwardTrigger = RequireComponentOnGameObject(
            "DoorTrigger_ServiceCorridor_SideStairMudroom", "DoorTriggerNavigation");
        MonoBehaviour reverseTrigger = RequireComponentOnGameObject(
            "DoorTrigger_SideStairMudroom_ServiceCorridor", "DoorTriggerNavigation");
        MonoBehaviour forwardPassage = RequireComponentOnGameObject(
            "DoorTrigger_ServiceCorridor_SideStairMudroom", "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour reversePassage = RequireComponentOnGameObject(
            "DoorTrigger_SideStairMudroom_ServiceCorridor", "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour group14ForwardTrigger = RequireComponentOnGameObject(
            "StairwayTrigger_SideStairMudroom_UpperSittingHall", "DoorTriggerNavigation");
        MonoBehaviour group14ForwardPassage = RequireComponentOnGameObject(
            "StairwayTrigger_SideStairMudroom_UpperSittingHall",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour group14ReverseTrigger = RequireComponentOnGameObject(
            "DoorTrigger_UpperSittingHall_SideStairMudroom", "DoorTriggerNavigation");
        MonoBehaviour group14ReversePassage = RequireComponentOnGameObject(
            "DoorTrigger_UpperSittingHall_SideStairMudroom",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour serviceView = RequireRoomView("room.service-corridor");
        MonoBehaviour sideView = RequireRoomView("room.side-stair-mudroom");

        Assert.That(FindSceneComponents("Chateau.World.Rooms.RoomView"), Has.Count.EqualTo(18));
        Assert.That(FindSceneComponents("Chateau.World.Rooms.Passages.Passage"), Has.Count.EqualTo(36));
        List<MonoBehaviour> triggers = FindSceneComponents("DoorTriggerNavigation");
        Assert.That(triggers, Has.Count.EqualTo(45));
        Assert.That(triggers.Count(trigger => GetField<MonoBehaviour>(trigger, "canonicalPassage") != null),
            Is.EqualTo(36));
        Assert.That(triggers.Count(trigger => GetField<MonoBehaviour>(trigger, "canonicalPassage") == null),
            Is.EqualTo(9));
        Assert.That(GetField<MonoBehaviour>(forwardTrigger, "canonicalPassage"), Is.SameAs(forwardPassage));
        Assert.That(GetField<MonoBehaviour>(reverseTrigger, "canonicalPassage"), Is.SameAs(reversePassage));
        Assert.That(GetField<MonoBehaviour>(group14ForwardTrigger, "canonicalPassage"),
            Is.SameAs(group14ForwardPassage));
        Assert.That(GetField<MonoBehaviour>(group14ReverseTrigger, "canonicalPassage"),
            Is.SameAs(group14ReversePassage));
        Assert.That(GetField<MonoBehaviour>(group14ForwardTrigger, "navigationManager"),
            Is.SameAs(navigation));
        Assert.That(GetField<MonoBehaviour>(group14ReverseTrigger, "navigationManager"),
            Is.SameAs(navigation));

        object serviceDefinition = GetProperty<object>(serviceView, "Definition");
        object sideDefinition = GetProperty<object>(sideView, "Definition");
        object forwardDefinition = GetProperty<object>(forwardPassage, "Definition");
        object reverseDefinition = GetProperty<object>(reversePassage, "Definition");
        Assert.That(GetProperty<string>(sideDefinition, "DisplayName"), Is.EqualTo(SideDisplay));
        Assert.That((bool)InvokeMethod(sideDefinition, "MatchesLegacyName", SideDisplay), Is.True);
        Assert.That((bool)InvokeMethod(sideDefinition, "MatchesLegacyName", SideLegacy), Is.True);
        Assert.That(GetProperty<object>(forwardDefinition, "SourceRoom"), Is.SameAs(serviceDefinition));
        Assert.That(GetProperty<object>(forwardDefinition, "DestinationRoom"), Is.SameAs(sideDefinition));
        Assert.That(GetProperty<object>(forwardDefinition, "Reverse"), Is.SameAs(reverseDefinition));
        Assert.That(GetProperty<object>(reverseDefinition, "SourceRoom"), Is.SameAs(sideDefinition));
        Assert.That(GetProperty<object>(reverseDefinition, "DestinationRoom"), Is.SameAs(serviceDefinition));
        Assert.That(GetProperty<object>(reverseDefinition, "Reverse"), Is.SameAs(forwardDefinition));
        object[] registered = ((IEnumerable)GetProperty<object>(GetProperty<object>(gameRoot, "Database"),
            "Definitions")).Cast<object>().ToArray();
        Assert.That(registered, Has.Length.EqualTo(55));

        AssertCanonicalSourceAndDestinationRegionPassage(
            forwardPassage, serviceView, reversePassage,
            "passage.service-corridor.side-stair-mudroom",
            "ServiceCorridor_SideStairMudroom", SideDisplay, false,
            new Vector2(-569.48f, -470.50003f), new Vector2(-569.48f, -338.82755f),
            new Vector2(836.02f, -338.82755f), new Vector2(836.02f, -470.50003f));
        AssertCanonicalSourceAndDestinationRegionPassage(
            reversePassage, sideView, forwardPassage,
            "passage.side-stair-mudroom.service-corridor",
            "SideStairMudroom_ServiceCorridor", ServiceRoom, false,
            new Vector2(52.839996f, -166.62186f), new Vector2(52.839996f, 188.62186f),
            new Vector2(172.84f, 188.62186f), new Vector2(172.84f, -166.62186f));

        InvokeMethod(chapter, "StopChapterCoroutines");
        InvokeMethod(chapter, "StopActiveDialogueForDebugTransition");
        InvokeMethod(player, "SetInputEnabled", true);
        float originalMoveSpeed = GetField<float>(player, "moveSpeed");
        SetField(player, "moveSpeed", 1000f);
        FreezeRoomLookForEvidence();
        List<Vector2> approachStops = new List<Vector2>(2);
        List<string> approachRooms = new List<string>(2);
        int movementStops = 0;
        Action arrival = () =>
        {
            approachStops.Add(GetProperty<Vector2>(player, "LogicalPosition"));
            approachRooms.Add(GetProperty<string>(navigation, "CurrentRoom"));
        };
        Action stopped = () => movementStops++;
        EventInfo arrivalEvent = player.GetType().GetEvent("ArrivedAtDestination");
        EventInfo stoppedEvent = player.GetType().GetEvent("MovementStopped");
        arrivalEvent.AddEventHandler(player, arrival);
        stoppedEvent.AddEventHandler(player, stopped);
        Vector2 sideLanding = Vector2.zero;
        Vector2 serviceLanding = Vector2.zero;
        try
        {
            string[] setupNames =
            {
                "DoorTrigger_GEH_DiningRoom",
                "DoorTrigger_DiningRoom_ButlersPantry",
                "DoorTrigger_ButlersPantry_ServiceCorridor"
            };
            string[] setupRooms = { "Dining Room", "Butlers Pantry", ServiceRoom };
            for (int i = 0; i < setupNames.Length; i++)
            {
                MonoBehaviour setupPassage = RequireComponentOnGameObject(
                    setupNames[i], "Chateau.World.Rooms.Passages.Passage");
                Assert.That((bool)InvokeMethod(navigation, "TryTraverse", setupPassage), Is.True);
                yield return WaitForCurrentRoom(navigation, setupRooms[i], 60);
            }

            Assert.That((bool)InvokeMethod(player, "TryWarpTo", new Vector2(-7f, -2f), true), Is.True);
            SetField(forwardTrigger, "lastPointerActivationFrame", -1);
            InvokeMethod(forwardTrigger, "ActivateDoor");
            Assert.That(GetProperty<bool>(player, "HasDestination"), Is.True);
            yield return WaitForCurrentRoom(navigation, SideDisplay, 240);
            FreezeRoomLookForEvidence();
            yield return null;
            Assert.That(GetProperty<object>(navigation, "CurrentRoomDefinition"), Is.SameAs(sideDefinition));
            Assert.That(GetProperty<bool>(sideView, "IsVisible"), Is.True);
            sideLanding = GetRoomViewLocalPlayerPosition(player, cameraManager, "Side Stair landing");

            Assert.That((bool)InvokeMethod(player, "TryWarpTo", Vector2.zero, true), Is.True);
            SetField(reverseTrigger, "lastPointerActivationFrame", -1);
            InvokeMethod(reverseTrigger, "ActivateDoor");
            Assert.That(GetProperty<bool>(player, "HasDestination"), Is.True,
                "The reverse standard door must retain its real far approach.");
            yield return WaitForCurrentRoom(navigation, ServiceRoom, 240);
            FreezeRoomLookForEvidence();
            yield return null;
            Assert.That(GetProperty<object>(navigation, "CurrentRoomDefinition"), Is.SameAs(serviceDefinition));
            Assert.That(GetProperty<bool>(serviceView, "IsVisible"), Is.True);
            serviceLanding = GetRoomViewLocalPlayerPosition(player, cameraManager, "Service landing");
            Assert.That(approachStops, Has.Count.EqualTo(2));
            Assert.That(movementStops, Is.EqualTo(2));
            Assert.That(approachRooms, Is.EqualTo(new[] { ServiceRoom, SideDisplay }));
            AssertFixedRenderingResolution();
        }
        finally
        {
            arrivalEvent.RemoveEventHandler(player, arrival);
            stoppedEvent.RemoveEventHandler(player, stopped);
            SetField(player, "moveSpeed", originalMoveSpeed);
            InvokeMethod(forwardTrigger, "CancelPendingPlayerApproach");
            InvokeMethod(reverseTrigger, "CancelPendingPlayerApproach");
            if (GetProperty<bool>(player, "HasDestination")) InvokeMethod(player, "CancelDestination");
        }

        Debug.Log($"[Slice22Group13PlayMode] sideLocal={Format(sideLanding)} " +
            $"serviceLocal={Format(serviceLanding)} forwardStop={Format(approachStops[0])} " +
            $"reverseStop={Format(approachStops[1])} roomViews=18 passages=36 callers=36/9 " +
            "placement=source-region/destination-region aliases=Side Stair & Mudroom/Side Stair Mudroom");
    }

    [UnityTest]
    public IEnumerator SideStairMudroomUpperSittingHallMixedRoundTripUsesCanonicalReciprocalRegions()
    {
        const string SideDisplay = "Side Stair & Mudroom";
        const string SideLegacy = "Side Stair Mudroom";
        const string UpperRoom = "Upper Sitting Hall";

        yield return BootGameplayFromRealMenu();

        MonoBehaviour gameRoot = RequireSingleSceneComponent("GameRoot");
        MonoBehaviour chapter = RequireSingleSceneComponent("ChapterManager");
        MonoBehaviour navigation = RequireSingleSceneComponent("RoomNavigationManager");
        MonoBehaviour player = RequireComponentOnGameObject("Player", "PointClickPlayerMovement");
        MonoBehaviour cameraManager = RequireSingleSceneComponent("CameraManager");
        MonoBehaviour forwardTrigger = RequireComponentOnGameObject(
            "StairwayTrigger_SideStairMudroom_UpperSittingHall", "DoorTriggerNavigation");
        MonoBehaviour reverseTrigger = RequireComponentOnGameObject(
            "DoorTrigger_UpperSittingHall_SideStairMudroom", "DoorTriggerNavigation");
        MonoBehaviour forwardPassage = RequireComponentOnGameObject(
            "StairwayTrigger_SideStairMudroom_UpperSittingHall",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour reversePassage = RequireComponentOnGameObject(
            "DoorTrigger_UpperSittingHall_SideStairMudroom",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour sideView = RequireRoomView("room.side-stair-mudroom");
        MonoBehaviour upperView = RequireRoomView("room.upper-sitting-hall");

        Assert.That(FindSceneComponents("Chateau.World.Rooms.RoomView"), Has.Count.EqualTo(18));
        Assert.That(FindSceneComponents("Chateau.World.Rooms.Passages.Passage"), Has.Count.EqualTo(36));
        List<MonoBehaviour> triggers = FindSceneComponents("DoorTriggerNavigation");
        Assert.That(triggers, Has.Count.EqualTo(45));
        Assert.That(triggers.Count(trigger => GetField<MonoBehaviour>(trigger, "canonicalPassage") != null),
            Is.EqualTo(36));
        Assert.That(triggers.Count(trigger => GetField<MonoBehaviour>(trigger, "canonicalPassage") == null),
            Is.EqualTo(9));
        Assert.That(forwardTrigger.GetComponents<Component>(), Has.Length.EqualTo(5));
        Assert.That(reverseTrigger.GetComponents<Component>(), Has.Length.EqualTo(5));
        Assert.That(GetField<MonoBehaviour>(forwardTrigger, "canonicalPassage"),
            Is.SameAs(forwardPassage));
        Assert.That(GetField<MonoBehaviour>(reverseTrigger, "canonicalPassage"),
            Is.SameAs(reversePassage));
        Assert.That(GetField<MonoBehaviour>(forwardTrigger, "navigationManager"), Is.SameAs(navigation));
        Assert.That(GetField<MonoBehaviour>(reverseTrigger, "navigationManager"), Is.SameAs(navigation));
        Assert.That(GetField<Transform>(forwardTrigger, "player"), Is.SameAs(player.transform));
        Assert.That(GetField<Transform>(reverseTrigger, "player"), Is.SameAs(player.transform));

        Assert.That(GetProperty<string>(forwardTrigger, "SourceRoom"), Is.EqualTo(SideLegacy));
        Assert.That(GetProperty<string>(forwardTrigger, "DoorName"),
            Is.EqualTo("SideStairMudroom_Stairway_UpperSittingHall"));
        Assert.That(GetProperty<string>(forwardTrigger, "DestinationRoom"), Is.EqualTo(UpperRoom));
        Assert.That(GetProperty<bool>(forwardTrigger, "IsStairway"), Is.True);
        Assert.That(GetProperty<string>(forwardTrigger, "InteractionLabel"), Is.EqualTo("Stairway"));
        Assert.That(InvokeMethod(forwardTrigger, "GetNavigationCursorIcon").ToString(),
            Is.EqualTo("StairsUp"));
        Assert.That(GetProperty<string>(reverseTrigger, "SourceRoom"), Is.EqualTo(UpperRoom));
        Assert.That(GetProperty<string>(reverseTrigger, "DoorName"),
            Is.EqualTo("UpperSittingHall_SideStairMudroom"));
        Assert.That(GetProperty<string>(reverseTrigger, "DestinationRoom"), Is.EqualTo(SideLegacy));
        Assert.That(GetProperty<bool>(reverseTrigger, "IsStairway"), Is.False);
        Assert.That(GetProperty<string>(reverseTrigger, "InteractionLabel"), Is.EqualTo("Door"));
        Assert.That(InvokeMethod(reverseTrigger, "GetNavigationCursorIcon").ToString(),
            Is.EqualTo("Door"));
        foreach (MonoBehaviour trigger in new[] { forwardTrigger, reverseTrigger })
        {
            Assert.That(GetField<object>(trigger, "triggerKind").ToString(), Is.EqualTo("Door"));
            Assert.That(GetField<object>(trigger, "stairwayDirection").ToString(), Is.EqualTo("Auto"));
            Assert.That(GetField<bool>(trigger, "requirePlayerProximity"), Is.True);
            Assert.That(GetField<bool>(trigger, "walkPlayerToTriggerWhenFar"), Is.True);
            Assert.That(GetField<bool>(trigger, "autoActivateAfterApproach"), Is.True);
            Assert.That(GetField<float>(trigger, "maxPlayerScreenDistance"), Is.EqualTo(145f));
        }

        AudioSource passageAudio = RequireSceneGameObject("Audio_DoorOpen").GetComponent<AudioSource>();
        Assert.That(GetField<AudioSource>(forwardTrigger, "doorOpenAudioSource"), Is.SameAs(passageAudio));
        Assert.That(GetField<AudioSource>(reverseTrigger, "doorOpenAudioSource"), Is.SameAs(passageAudio));
        UnityEngine.Object doorCatalog = GetField<UnityEngine.Object>(reverseTrigger, "doorOpenSoundCatalog");
        UnityEngine.Object stairCatalog = GetField<UnityEngine.Object>(forwardTrigger, "stairwaySoundCatalog");
        Assert.That(doorCatalog, Is.Not.Null);
        Assert.That(GetField<UnityEngine.Object>(forwardTrigger, "doorOpenSoundCatalog"), Is.Null,
            "The authored stairway caller must serialize only its stairway catalog before activation.");
        Assert.That(stairCatalog, Is.Not.Null);
        Assert.That(GetField<UnityEngine.Object>(reverseTrigger, "stairwaySoundCatalog"), Is.Null);

        object sideDefinition = GetProperty<object>(sideView, "Definition");
        object upperDefinition = GetProperty<object>(upperView, "Definition");
        object forwardDefinition = GetProperty<object>(forwardPassage, "Definition");
        object reverseDefinition = GetProperty<object>(reversePassage, "Definition");
        Assert.That(GetProperty<string>(sideDefinition, "DisplayName"), Is.EqualTo(SideDisplay));
        Assert.That((bool)InvokeMethod(sideDefinition, "MatchesLegacyName", SideDisplay), Is.True);
        Assert.That((bool)InvokeMethod(sideDefinition, "MatchesLegacyName", SideLegacy), Is.True);
        Assert.That(GetProperty<string>(upperDefinition, "DisplayName"), Is.EqualTo(UpperRoom));
        Assert.That(GetProperty<object>(forwardDefinition, "Kind").ToString(), Is.EqualTo("Stairway"));
        Assert.That(GetProperty<string>(forwardDefinition, "PromptText"), Is.EqualTo("Use Stairway"));
        Assert.That(GetProperty<object>(forwardDefinition, "SourceRoom"), Is.SameAs(sideDefinition));
        Assert.That(GetProperty<object>(forwardDefinition, "DestinationRoom"), Is.SameAs(upperDefinition));
        Assert.That(GetProperty<object>(forwardDefinition, "Reverse"), Is.SameAs(reverseDefinition));
        Assert.That(GetProperty<string>(forwardDefinition, "CompatibilityDestinationRoomName"),
            Is.EqualTo(UpperRoom));
        Assert.That(GetProperty<bool>(forwardDefinition, "HasExplicitCompatibilityDestinationRoomName"),
            Is.False);
        Assert.That(GetProperty<object>(reverseDefinition, "Kind").ToString(), Is.EqualTo("Door"));
        Assert.That(GetProperty<string>(reverseDefinition, "PromptText"), Is.EqualTo("Open Door"));
        Assert.That(GetProperty<object>(reverseDefinition, "SourceRoom"), Is.SameAs(upperDefinition));
        Assert.That(GetProperty<object>(reverseDefinition, "DestinationRoom"), Is.SameAs(sideDefinition));
        Assert.That(GetProperty<object>(reverseDefinition, "Reverse"), Is.SameAs(forwardDefinition));
        Assert.That(GetProperty<string>(reverseDefinition, "CompatibilityDestinationRoomName"),
            Is.EqualTo(SideLegacy));
        Assert.That(GetProperty<bool>(reverseDefinition, "HasExplicitCompatibilityDestinationRoomName"),
            Is.True);
        object[] registered = ((IEnumerable)GetProperty<object>(GetProperty<object>(gameRoot, "Database"),
            "Definitions")).Cast<object>().ToArray();
        Assert.That(registered, Has.Length.EqualTo(55));
        Assert.That(registered, Does.Contain(forwardDefinition));
        Assert.That(registered, Does.Contain(reverseDefinition));

        AssertCanonicalSourceAndDestinationRegionPassage(
            forwardPassage, sideView, reversePassage,
            "passage.side-stair-mudroom.upper-sitting-hall",
            "SideStairMudroom_Stairway_UpperSittingHall", UpperRoom, false,
            new Vector2(70.8670044f, -32.7176476f), new Vector2(70.8670044f, 130.0000458f),
            new Vector2(129f, 130.0000458f), new Vector2(129f, -32.7176476f));
        AssertCanonicalSourceAndDestinationRegionPassage(
            reversePassage, upperView, forwardPassage,
            "passage.upper-sitting-hall.side-stair-mudroom",
            "UpperSittingHall_SideStairMudroom", SideLegacy, true,
            new Vector2(253.170868f, -202.263962f), new Vector2(253.170868f, 236.559952f),
            new Vector2(471.909576f, 236.559952f), new Vector2(471.909576f, -202.263962f));

        InvokeMethod(chapter, "StopChapterCoroutines");
        InvokeMethod(chapter, "StopActiveDialogueForDebugTransition");
        InvokeMethod(player, "SetInputEnabled", true);
        float originalMoveSpeed = GetField<float>(player, "moveSpeed");
        SetField(player, "moveSpeed", 1000f);
        FreezeRoomLookForEvidence();
        List<Vector2> approachStops = new List<Vector2>(2);
        List<string> approachRooms = new List<string>(2);
        int movementStops = 0;
        Action arrived = () =>
        {
            approachStops.Add(GetProperty<Vector2>(player, "LogicalPosition"));
            approachRooms.Add(GetProperty<string>(navigation, "CurrentRoom"));
        };
        Action stopped = () => movementStops++;
        EventInfo arrivalEvent = player.GetType().GetEvent("ArrivedAtDestination");
        EventInfo stoppedEvent = player.GetType().GetEvent("MovementStopped");
        arrivalEvent.AddEventHandler(player, arrived);
        stoppedEvent.AddEventHandler(player, stopped);
        Vector2 upperLanding = Vector2.zero;
        Vector2 sideLanding = Vector2.zero;
        try
        {
            string[] setupNames =
            {
                "DoorTrigger_GEH_DiningRoom",
                "DoorTrigger_DiningRoom_ButlersPantry",
                "DoorTrigger_ButlersPantry_ServiceCorridor",
                "DoorTrigger_ServiceCorridor_SideStairMudroom"
            };
            string[] setupRooms = { "Dining Room", "Butlers Pantry", "Service Corridor", SideDisplay };
            for (int i = 0; i < setupNames.Length; i++)
            {
                MonoBehaviour setupPassage = RequireComponentOnGameObject(
                    setupNames[i], "Chateau.World.Rooms.Passages.Passage");
                Assert.That((bool)InvokeMethod(navigation, "TryTraverse", setupPassage), Is.True);
                yield return WaitForCurrentRoom(navigation, setupRooms[i], 60);
            }
            Assert.That((bool)InvokeMethod(navigation, "MoveToRoom", SideLegacy), Is.True);
            yield return WaitForCurrentRoom(navigation, SideLegacy, 60);

            Assert.That((bool)InvokeMethod(player, "TryWarpTo", Vector2.zero, true), Is.True);
            Assert.That((float)InvokeMethod(forwardTrigger, "GetPlayerScreenDistanceToTrigger"),
                Is.GreaterThan(145f));
            SetField(forwardTrigger, "lastPointerActivationFrame", -1);
            InvokeMethod(forwardTrigger, "ActivateDoor");
            Assert.That(GetProperty<bool>(player, "HasDestination"), Is.True);
            yield return WaitForCurrentRoom(navigation, UpperRoom, 240);
            FreezeRoomLookForEvidence();
            yield return null;
            Assert.That(GetProperty<object>(navigation, "CurrentRoomDefinition"), Is.SameAs(upperDefinition));
            Assert.That(GetProperty<bool>(upperView, "IsVisible"), Is.True);
            upperLanding = GetRoomViewLocalPlayerPosition(player, cameraManager, "Upper Sitting Hall landing");

            Assert.That((bool)InvokeMethod(player, "TryWarpTo", new Vector2(-5f, -2f), true), Is.True);
            Assert.That((float)InvokeMethod(reverseTrigger, "GetPlayerScreenDistanceToTrigger"),
                Is.GreaterThan(145f));
            SetField(reverseTrigger, "lastPointerActivationFrame", -1);
            InvokeMethod(reverseTrigger, "ActivateDoor");
            Assert.That(GetProperty<bool>(player, "HasDestination"), Is.True);
            yield return WaitForCurrentRoom(navigation, SideLegacy, 240);
            FreezeRoomLookForEvidence();
            yield return null;
            Assert.That(GetProperty<string>(navigation, "CurrentRoom"), Is.EqualTo(SideLegacy));
            Assert.That(GetProperty<object>(navigation, "CurrentRoomDefinition"), Is.SameAs(sideDefinition));
            Assert.That(GetProperty<bool>(sideView, "IsVisible"), Is.True);
            sideLanding = GetRoomViewLocalPlayerPosition(player, cameraManager, "Side Stair legacy landing");
            Assert.That(approachStops, Has.Count.EqualTo(2));
            Assert.That(approachRooms, Is.EqualTo(new[] { SideLegacy, UpperRoom }));
            Assert.That(movementStops, Is.EqualTo(2));
            AssertFinite(approachStops[0], "Side Stair source-region approach stop");
            AssertFinite(approachStops[1], "Upper Sitting source-region approach stop");
            AssertFinite(upperLanding, "Upper Sitting destination-region landing");
            AssertFinite(sideLanding, "Side Stair destination-region landing");
            AssertFixedRenderingResolution();
        }
        finally
        {
            arrivalEvent.RemoveEventHandler(player, arrived);
            stoppedEvent.RemoveEventHandler(player, stopped);
            SetField(player, "moveSpeed", originalMoveSpeed);
            InvokeMethod(forwardTrigger, "CancelPendingPlayerApproach");
            InvokeMethod(reverseTrigger, "CancelPendingPlayerApproach");
            if (GetProperty<bool>(player, "HasDestination")) InvokeMethod(player, "CancelDestination");
        }

        Debug.Log($"[Slice22Group14PlayMode] upperLocal={Format(upperLanding)} " +
            $"sideLocal={Format(sideLanding)} forwardStop={Format(approachStops[0])} " +
            $"reverseStop={Format(approachStops[1])} roomViews=18 passages=36 callers=36/9 " +
            "profile=inferred-stairway-up/standard-door aliases=Side Stair & Mudroom/Side Stair Mudroom");
    }

    [UnityTest]
    public IEnumerator UpperSittingHallUpperGalleryFarRoundTripUsesCanonicalReciprocalRegions()
    {
        const string UpperRoom = "Upper Sitting Hall";
        const string GalleryRoom = "Upper Gallery";

        yield return BootGameplayFromRealMenu();

        MonoBehaviour gameRoot = RequireSingleSceneComponent("GameRoot");
        MonoBehaviour chapter = RequireSingleSceneComponent("ChapterManager");
        MonoBehaviour navigation = RequireSingleSceneComponent("RoomNavigationManager");
        MonoBehaviour player = RequireComponentOnGameObject("Player", "PointClickPlayerMovement");
        MonoBehaviour cameraManager = RequireSingleSceneComponent("CameraManager");
        MonoBehaviour forwardTrigger = RequireComponentOnGameObject(
            "DoorTrigger_UpperSittingHall_UpperGallery", "DoorTriggerNavigation");
        MonoBehaviour reverseTrigger = RequireComponentOnGameObject(
            "DoorTrigger_UpperGallery_UpperSittingHall", "DoorTriggerNavigation");
        MonoBehaviour forwardPassage = RequireComponentOnGameObject(
            "DoorTrigger_UpperSittingHall_UpperGallery",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour reversePassage = RequireComponentOnGameObject(
            "DoorTrigger_UpperGallery_UpperSittingHall",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour upperView = RequireRoomView("room.upper-sitting-hall");
        MonoBehaviour galleryView = RequireRoomView("room.upper-gallery");

        Assert.That(FindSceneComponents("Chateau.World.Rooms.RoomView"), Has.Count.EqualTo(18));
        Assert.That(FindSceneComponents("Chateau.World.Rooms.Passages.Passage"), Has.Count.EqualTo(36));
        List<MonoBehaviour> triggers = FindSceneComponents("DoorTriggerNavigation");
        Assert.That(triggers, Has.Count.EqualTo(45));
        Assert.That(triggers.Count(trigger => GetField<MonoBehaviour>(trigger, "canonicalPassage") != null),
            Is.EqualTo(36));
        Assert.That(triggers.Count(trigger => GetField<MonoBehaviour>(trigger, "canonicalPassage") == null),
            Is.EqualTo(9));
        Assert.That(forwardTrigger.GetComponents<Component>(), Has.Length.EqualTo(5));
        Assert.That(reverseTrigger.GetComponents<Component>(), Has.Length.EqualTo(5));
        Assert.That(GetField<MonoBehaviour>(forwardTrigger, "canonicalPassage"),
            Is.SameAs(forwardPassage));
        Assert.That(GetField<MonoBehaviour>(reverseTrigger, "canonicalPassage"),
            Is.SameAs(reversePassage));
        foreach (MonoBehaviour trigger in new[] { forwardTrigger, reverseTrigger })
        {
            Assert.That(GetField<MonoBehaviour>(trigger, "navigationManager"), Is.SameAs(navigation));
            Assert.That(GetField<Transform>(trigger, "player"), Is.SameAs(player.transform));
            Assert.That(GetField<object>(trigger, "triggerKind").ToString(), Is.EqualTo("Door"));
            Assert.That(GetField<bool>(trigger, "requirePlayerProximity"), Is.True);
            Assert.That(GetField<bool>(trigger, "walkPlayerToTriggerWhenFar"), Is.True);
            Assert.That(GetField<bool>(trigger, "autoActivateAfterApproach"), Is.True);
            Assert.That(GetField<float>(trigger, "maxPlayerScreenDistance"), Is.EqualTo(145f));
            Assert.That(GetProperty<bool>(trigger, "IsStairway"), Is.False);
            Assert.That(GetProperty<string>(trigger, "InteractionLabel"), Is.EqualTo("Door"));
            Assert.That(InvokeMethod(trigger, "GetNavigationCursorIcon").ToString(), Is.EqualTo("Door"));
        }
        Assert.That(GetProperty<string>(forwardTrigger, "SourceRoom"), Is.EqualTo(UpperRoom));
        Assert.That(GetProperty<string>(forwardTrigger, "DoorName"),
            Is.EqualTo("UpperSittingHall_UpperGallery"));
        Assert.That(GetProperty<string>(forwardTrigger, "DestinationRoom"), Is.EqualTo(GalleryRoom));
        Assert.That(GetProperty<string>(reverseTrigger, "SourceRoom"), Is.EqualTo(GalleryRoom));
        Assert.That(GetProperty<string>(reverseTrigger, "DoorName"),
            Is.EqualTo("UpperGallery_UpperSittingHall"));
        Assert.That(GetProperty<string>(reverseTrigger, "DestinationRoom"), Is.EqualTo(UpperRoom));

        AudioSource passageAudio = RequireSceneGameObject("Audio_DoorOpen").GetComponent<AudioSource>();
        UnityEngine.Object forwardCatalog = GetField<UnityEngine.Object>(forwardTrigger,
            "doorOpenSoundCatalog");
        Assert.That(forwardCatalog, Is.Not.Null);
        Assert.That(GetField<UnityEngine.Object>(reverseTrigger, "doorOpenSoundCatalog"),
            Is.SameAs(forwardCatalog));
        Assert.That(GetField<AudioSource>(forwardTrigger, "doorOpenAudioSource"), Is.SameAs(passageAudio));
        Assert.That(GetField<AudioSource>(reverseTrigger, "doorOpenAudioSource"), Is.SameAs(passageAudio));
        Assert.That(GetField<UnityEngine.Object>(forwardTrigger, "stairwaySoundCatalog"), Is.Null);
        Assert.That(GetField<UnityEngine.Object>(reverseTrigger, "stairwaySoundCatalog"), Is.Null);

        object upperDefinition = GetProperty<object>(upperView, "Definition");
        object galleryDefinition = GetProperty<object>(galleryView, "Definition");
        object forwardDefinition = GetProperty<object>(forwardPassage, "Definition");
        object reverseDefinition = GetProperty<object>(reversePassage, "Definition");
        Assert.That(GetProperty<string>(upperDefinition, "DisplayName"), Is.EqualTo(UpperRoom));
        Assert.That(GetProperty<string>(galleryDefinition, "DisplayName"), Is.EqualTo(GalleryRoom));
        Assert.That(GetProperty<object>(forwardDefinition, "Kind").ToString(), Is.EqualTo("Door"));
        Assert.That(GetProperty<object>(reverseDefinition, "Kind").ToString(), Is.EqualTo("Door"));
        Assert.That(GetProperty<string>(forwardDefinition, "PromptText"), Is.EqualTo("Open Door"));
        Assert.That(GetProperty<string>(reverseDefinition, "PromptText"), Is.EqualTo("Open Door"));
        object[] registered = ((IEnumerable)GetProperty<object>(GetProperty<object>(gameRoot, "Database"),
            "Definitions")).Cast<object>().ToArray();
        Assert.That(registered, Has.Length.EqualTo(55));
        Assert.That(registered, Does.Contain(forwardDefinition));
        Assert.That(registered, Does.Contain(reverseDefinition));

        AssertCanonicalSourceAndDestinationRegionPassage(
            forwardPassage, upperView, reversePassage,
            "passage.upper-sitting-hall.upper-gallery",
            "UpperSittingHall_UpperGallery", GalleryRoom, false,
            new Vector2(587.2802124f, -69.99983978f),
            new Vector2(587.2802124f, 225.1838379f),
            new Vector2(710.7197876f, 225.1838379f),
            new Vector2(710.7197876f, -69.99983978f));
        AssertCanonicalSourceAndDestinationRegionPassage(
            reversePassage, galleryView, forwardPassage,
            "passage.upper-gallery.upper-sitting-hall",
            "UpperGallery_UpperSittingHall", UpperRoom, false,
            new Vector2(-174.1340942f, -42.82170105f),
            new Vector2(-174.1340942f, 207.995697f),
            new Vector2(-135.8659058f, 207.995697f),
            new Vector2(-135.8659058f, -42.82170105f));

        InvokeMethod(chapter, "StopChapterCoroutines");
        InvokeMethod(chapter, "StopActiveDialogueForDebugTransition");
        InvokeMethod(player, "SetInputEnabled", true);
        float originalMoveSpeed = GetField<float>(player, "moveSpeed");
        SetField(player, "moveSpeed", 1000f);
        FreezeRoomLookForEvidence();
        List<Vector2> approachStops = new List<Vector2>(2);
        List<string> approachRooms = new List<string>(2);
        int movementStops = 0;
        Action arrived = () =>
        {
            approachStops.Add(GetProperty<Vector2>(player, "LogicalPosition"));
            approachRooms.Add(GetProperty<string>(navigation, "CurrentRoom"));
        };
        Action stopped = () => movementStops++;
        EventInfo arrivalEvent = player.GetType().GetEvent("ArrivedAtDestination");
        EventInfo stoppedEvent = player.GetType().GetEvent("MovementStopped");
        arrivalEvent.AddEventHandler(player, arrived);
        stoppedEvent.AddEventHandler(player, stopped);
        Vector2 galleryLanding = Vector2.zero;
        Vector2 upperLanding = Vector2.zero;
        try
        {
            string[] setupNames =
            {
                "DoorTrigger_GEH_DiningRoom",
                "DoorTrigger_DiningRoom_ButlersPantry",
                "DoorTrigger_ButlersPantry_ServiceCorridor",
                "DoorTrigger_ServiceCorridor_SideStairMudroom"
            };
            string[] setupRooms =
                { "Dining Room", "Butlers Pantry", "Service Corridor", "Side Stair & Mudroom" };
            for (int i = 0; i < setupNames.Length; i++)
            {
                MonoBehaviour setupPassage = RequireComponentOnGameObject(
                    setupNames[i], "Chateau.World.Rooms.Passages.Passage");
                Assert.That((bool)InvokeMethod(navigation, "TryTraverse", setupPassage), Is.True);
                yield return WaitForCurrentRoom(navigation, setupRooms[i], 60);
            }
            Assert.That((bool)InvokeMethod(navigation, "MoveToRoom", "Side Stair Mudroom"), Is.True);
            yield return WaitForCurrentRoom(navigation, "Side Stair Mudroom", 60);
            MonoBehaviour upperSetup = RequireComponentOnGameObject(
                "StairwayTrigger_SideStairMudroom_UpperSittingHall",
                "Chateau.World.Rooms.Passages.Passage");
            Assert.That((bool)InvokeMethod(navigation, "TryTraverse", upperSetup), Is.True);
            yield return WaitForCurrentRoom(navigation, UpperRoom, 60);

            Assert.That((bool)InvokeMethod(player, "TryWarpTo", new Vector2(5f, -2f), true), Is.True);
            Assert.That((float)InvokeMethod(forwardTrigger, "GetPlayerScreenDistanceToTrigger"),
                Is.GreaterThan(145f));
            SetField(forwardTrigger, "lastPointerActivationFrame", -1);
            InvokeMethod(forwardTrigger, "ActivateDoor");
            Assert.That(GetProperty<bool>(player, "HasDestination"), Is.True);
            yield return WaitForCurrentRoom(navigation, GalleryRoom, 240);
            FreezeRoomLookForEvidence();
            yield return null;
            Assert.That(GetProperty<object>(navigation, "CurrentRoomDefinition"),
                Is.SameAs(galleryDefinition));
            Assert.That(GetProperty<bool>(galleryView, "IsVisible"), Is.True);
            galleryLanding = GetRoomViewLocalPlayerPosition(player, cameraManager,
                "Upper Gallery landing");

            Assert.That((bool)InvokeMethod(player, "TryWarpTo", new Vector2(-5f, -2f), true), Is.True);
            Assert.That((float)InvokeMethod(reverseTrigger, "GetPlayerScreenDistanceToTrigger"),
                Is.GreaterThan(145f));
            SetField(reverseTrigger, "lastPointerActivationFrame", -1);
            InvokeMethod(reverseTrigger, "ActivateDoor");
            Assert.That(GetProperty<bool>(player, "HasDestination"), Is.True);
            yield return WaitForCurrentRoom(navigation, UpperRoom, 240);
            FreezeRoomLookForEvidence();
            yield return null;
            Assert.That(GetProperty<object>(navigation, "CurrentRoomDefinition"),
                Is.SameAs(upperDefinition));
            Assert.That(GetProperty<bool>(upperView, "IsVisible"), Is.True);
            upperLanding = GetRoomViewLocalPlayerPosition(player, cameraManager,
                "Upper Sitting Hall landing");
            Assert.That(approachStops, Has.Count.EqualTo(2));
            Assert.That(approachRooms, Is.EqualTo(new[] { UpperRoom, GalleryRoom }));
            Assert.That(movementStops, Is.EqualTo(2));
            AssertFinite(approachStops[0], "Upper Sitting source-region approach stop");
            AssertFinite(approachStops[1], "Upper Gallery source-region approach stop");
            AssertFinite(galleryLanding, "Upper Gallery destination-region landing");
            AssertFinite(upperLanding, "Upper Sitting destination-region landing");
            AssertFixedRenderingResolution();
        }
        finally
        {
            arrivalEvent.RemoveEventHandler(player, arrived);
            stoppedEvent.RemoveEventHandler(player, stopped);
            SetField(player, "moveSpeed", originalMoveSpeed);
            InvokeMethod(forwardTrigger, "CancelPendingPlayerApproach");
            InvokeMethod(reverseTrigger, "CancelPendingPlayerApproach");
            if (GetProperty<bool>(player, "HasDestination")) InvokeMethod(player, "CancelDestination");
        }

        Debug.Log($"[Slice22Group15PlayMode] galleryLocal={Format(galleryLanding)} " +
            $"upperLocal={Format(upperLanding)} forwardStop={Format(approachStops[0])} " +
            $"reverseStop={Format(approachStops[1])} roomViews=18 passages=36 callers=36/9 " +
            "profile=standard-door/standard-door protected=gallery-boundary/stairwell/content");
    }

    [UnityTest]
    public IEnumerator Chapter2PanicFrameHasApprovedGuestsAndStableRenderedEvidence()
    {
        yield return BootGameplayFromRealMenu();

        MonoBehaviour chapter = RequireSingleSceneComponent("ChapterManager");
        MonoBehaviour navigation = RequireSingleSceneComponent("RoomNavigationManager");
        MonoBehaviour panic = RequireSingleSceneComponent("Chapter2GuestPanicController");

        InvokeMethod(chapter, "SkipToChapter2ForTesting");
        yield return WaitForCurrentRoom(navigation, DrawingRoomName, 120);
        FreezeRoomLookForEvidence();
        yield return null;

        Time.captureDeltaTime = 1f / 60f;
        UnityEngine.Random.InitState(20260713);
        object panicRoutine = InvokeMethod(panic, "BeginPanic");
        Assert.That(panicRoutine, Is.Not.Null);
        Assert.That(GetProperty<bool>(panic, "IsRunning"), Is.True);

        MonoBehaviour[] visibleGuests = FindSceneComponents("ActorRoomState")
            .Where(actor => GetProperty<bool>(actor, "IsVisibleInCurrentRoom"))
            .OrderBy(actor => actor.gameObject.name, StringComparer.Ordinal)
            .ToArray();
        Assert.That(visibleGuests, Has.Length.EqualTo(8));

        MonoBehaviour firstGuest = visibleGuests[0];
        SpriteRenderer firstGuestRenderer = firstGuest.GetComponentInChildren<SpriteRenderer>(true);
        Assert.That(firstGuestRenderer, Is.Not.Null);
        Assert.That(firstGuestRenderer.sprite, Is.Not.Null);
        float guestScreenHeight = GetRenderedScreenHeight(firstGuestRenderer, Camera.main);
        Vector3 guestScreenPoint = Camera.main.WorldToScreenPoint(firstGuestRenderer.bounds.center);

        Debug.Log(
            $"[Slice04FixedPanicMeasurement] screenCenter={guestScreenPoint.x:0.###},{guestScreenPoint.y:0.###} " +
            $"screenHeight={guestScreenHeight:0.###} sorting={firstGuestRenderer.sortingOrder}");

        Assert.That(firstGuest.gameObject.name, Is.EqualTo("Guest 1"));
        Assert.That(firstGuestRenderer.sprite.name, Is.EqualTo("lady_sitting_01"));
        Assert.That(guestScreenPoint.x, Is.EqualTo(477.884f).Within(1f));
        Assert.That(guestScreenPoint.y, Is.EqualTo(301.136f).Within(1f));
        Assert.That(guestScreenHeight, Is.EqualTo(255.668f).Within(1f));
        Assert.That(firstGuestRenderer.sortingOrder, Is.EqualTo(1620));

        AssertFixedRenderingResolution();
        Debug.Log(
            $"[Slice03Chapter2PanicGolden] resolution={Screen.width}x{Screen.height} " +
            $"room={GetProperty<string>(navigation, "CurrentRoom")} visibleGuests={visibleGuests.Length} " +
            $"guest={firstGuest.gameObject.name} sprite={firstGuestRenderer.sprite.name} " +
            $"screenCenter={guestScreenPoint.x:0.###},{guestScreenPoint.y:0.###} " +
            $"screenHeight={guestScreenHeight:0.###} sorting={firstGuestRenderer.sortingOrder} " +
            $"panicRunning={GetProperty<bool>(panic, "IsRunning")}");

        InvokeMethod(panic, "StopPanic");
        Assert.That(GetProperty<bool>(panic, "IsRunning"), Is.False);
        Time.captureDeltaTime = previousCaptureDeltaTime;
        yield return null;
    }

    private static IEnumerator BootGameplayFromRealMenu()
    {
        RequireSceneGameObject("Button_NewGame").GetComponent<Button>().onClick.Invoke();
        yield return WaitForGameObject("Button_CursorStyle_01", 120);
        RequireSceneGameObject("Button_CursorStyle_01").GetComponent<Button>().onClick.Invoke();
        yield return WaitForScene(GameplaySceneName, 240);
        SetFixedRenderingResolution(EvidenceWidth, EvidenceHeight);
        yield return WaitForFixedRenderingResolution(EvidenceWidth, EvidenceHeight, 30);
        FreezeRoomLookForEvidence();
        yield return null;
    }

    private static void FreezeRoomLookForEvidence()
    {
        MonoBehaviour cameraManager = RequireSingleSceneComponent("CameraManager");
        SetField(cameraManager, "panRoomWithMouseEdges", false);
        SetField(cameraManager, "moveRoomVerticallyWithMouseEdges", false);
        SetField(cameraManager, "autoEnableVerticalRoomPan", false);
        SetField(cameraManager, "zoomRoomWithMouseWheel", false);
        InvokeMethod(
            cameraManager,
            "SetRoomLookForPreview",
            EvidenceHorizontalRoomPan,
            EvidenceVerticalRoomPan,
            EvidenceRoomFov);
        Assert.That(
            GetProperty<float>(cameraManager, "CurrentRoomHorizontalPan"),
            Is.EqualTo(EvidenceHorizontalRoomPan).Within(0.000001f));
        Assert.That(
            GetProperty<float>(cameraManager, "CurrentRoomVerticalPan"),
            Is.EqualTo(EvidenceVerticalRoomPan).Within(0.000001f));
        Assert.That(
            GetProperty<float>(cameraManager, "CurrentRoomFov"),
            Is.EqualTo(EvidenceRoomFov).Within(0.000001f));
    }

    private static string BuildButlerScaleFingerprint(
        MonoBehaviour player,
        SpriteRenderer renderer)
    {
        return string.Join(
            ",",
            "butler",
            $"room={GetProperty<string>(RequireSingleSceneComponent("RoomNavigationManager"), "CurrentRoom")}",
            $"localScale={FormatFingerprint(player.transform.localScale)}",
            $"presentation={FormatFingerprint(GetProperty<float>(player, "RoomPresentationScale"))}",
            $"worldMultiplier={FormatFingerprint(GetProperty<float>(player, "CurrentWorldActorScaleMultiplier"))}",
            $"sort={renderer.sortingOrder}");
    }

    private static string BuildGuestScaleFingerprint(IEnumerable<MonoBehaviour> actors)
    {
        return "guests=" + string.Join(
            ";",
            actors.Select(actor =>
            {
                MonoBehaviour participant = actor.GetComponents<MonoBehaviour>()
                    .Single(component => TypeMatches(component, "GuestScaleParticipant"));
                Transform scaleRoot = GetProperty<Transform>(participant, "ScaleRoot");
                SpriteRenderer renderer = actor.GetComponentsInChildren<SpriteRenderer>(true)
                    .First(candidate => candidate.sprite != null);
                Assert.That(scaleRoot, Is.Not.Null, $"Guest '{actor.gameObject.name}' has no scale root.");

                return string.Join(
                    ",",
                    GetProperty<string>(actor, "ActorId"),
                    $"room={GetProperty<string>(actor, "CurrentRoomId")}",
                    $"character={GetProperty<string>(participant, "CharacterId")}",
                    $"localScale={FormatFingerprint(scaleRoot.localScale)}",
                    $"capturedBase={FormatFingerprint(GetProperty<Vector3>(participant, "CapturedBaseScale"))}",
                    $"hasBase={GetProperty<bool>(participant, "HasCapturedBaseScale")}",
                    $"butlerScale={FormatFingerprint(GetProperty<float>(actor, "CurrentButlerCharacterScale"))}",
                    $"sort={renderer.sortingOrder}");
            }));
    }

    private static string ComputeSha256(string value)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            return BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    private static string FormatFingerprint(float value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string FormatFingerprint(Vector3 value)
    {
        return $"{FormatFingerprint(value.x)}:{FormatFingerprint(value.y)}:{FormatFingerprint(value.z)}";
    }

    private static IEnumerator WaitForScene(string sceneName, int maximumFrames)
    {
        for (int frame = 0; frame < maximumFrames; frame++)
        {
            if (SceneManager.GetActiveScene().name == sceneName)
            {
                yield break;
            }

            yield return null;
        }

        Assert.Fail($"Scene '{sceneName}' did not become active within {maximumFrames} frames.");
    }

    private static IEnumerator WaitForGameObject(string objectName, int maximumFrames)
    {
        for (int frame = 0; frame < maximumFrames; frame++)
        {
            if (FindSceneGameObject(objectName) != null)
            {
                yield break;
            }

            yield return null;
        }

        Assert.Fail($"GameObject '{objectName}' did not appear within {maximumFrames} frames.");
    }

    private static IEnumerator WaitForCurrentRoom(MonoBehaviour navigation, string roomName, int maximumFrames)
    {
        for (int frame = 0; frame < maximumFrames; frame++)
        {
            if (GetProperty<string>(navigation, "CurrentRoom") == roomName)
            {
                yield break;
            }

            yield return null;
        }

        Assert.Fail($"Room '{roomName}' did not become current within {maximumFrames} frames.");
    }

    private static IEnumerator WaitForMovementStop(MonoBehaviour player, int maximumFrames)
    {
        for (int frame = 0; frame < maximumFrames; frame++)
        {
            if (!GetProperty<bool>(player, "HasDestination"))
            {
                yield break;
            }

            yield return null;
        }

        Assert.Fail($"Player movement did not stop within {maximumFrames} frames.");
    }

    private static IEnumerator WaitForFixedRenderingResolution(uint width, uint height, int maximumFrames)
    {
        for (int frame = 0; frame < maximumFrames; frame++)
        {
            GetRenderingResolution(out uint renderingWidth, out uint renderingHeight);

            if (renderingWidth == width && renderingHeight == height &&
                Screen.width == (int)width && Screen.height == (int)height)
            {
                yield break;
            }

            yield return null;
        }

        GetRenderingResolution(out uint finalWidth, out uint finalHeight);
        Assert.Fail(
            $"Rendering resolution did not settle at {width}x{height}; " +
            $"PlayModeWindow={finalWidth}x{finalHeight}, Screen={Screen.width}x{Screen.height}.");
    }

    private static void SetFixedRenderingResolution(uint width, uint height)
    {
        Type playModeWindowType = FindLoadedType("UnityEditor.PlayModeWindow");

        if (playModeWindowType == null)
        {
            Screen.SetResolution((int)width, (int)height, false);
            return;
        }

        Type viewType = playModeWindowType.GetNestedType("PlayModeViewTypes", BindingFlags.Public);
        MethodInfo setViewType = playModeWindowType.GetMethod("SetViewType", BindingFlags.Public | BindingFlags.Static);
        MethodInfo setResolution = playModeWindowType.GetMethod(
            "SetCustomRenderingResolution",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(viewType, Is.Not.Null);
        Assert.That(setViewType, Is.Not.Null);
        Assert.That(setResolution, Is.Not.Null);
        setViewType.Invoke(null, new[] { Enum.Parse(viewType, "GameView") });
        setResolution.Invoke(null, new object[] { width, height, EvidenceResolutionName });
    }

    private static void GetRenderingResolution(out uint width, out uint height)
    {
        Type playModeWindowType = FindLoadedType("UnityEditor.PlayModeWindow");

        if (playModeWindowType == null)
        {
            width = (uint)Screen.width;
            height = (uint)Screen.height;
            return;
        }

        MethodInfo getResolution = playModeWindowType.GetMethod(
            "GetRenderingResolution",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(getResolution, Is.Not.Null);
        object[] arguments = { 0u, 0u };
        getResolution.Invoke(null, arguments);
        width = (uint)arguments[0];
        height = (uint)arguments[1];
    }

    private static void AssertFixedRenderingResolution()
    {
        GetRenderingResolution(out uint width, out uint height);
        Assert.That(width, Is.EqualTo(EvidenceWidth));
        Assert.That(height, Is.EqualTo(EvidenceHeight));
        Assert.That(Screen.width, Is.EqualTo((int)EvidenceWidth));
        Assert.That(Screen.height, Is.EqualTo((int)EvidenceHeight));
    }

    private static Type FindLoadedType(string fullName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName, false))
            .FirstOrDefault(type => type != null);
    }

    private static MonoBehaviour RequireRoomView(string stableId)
    {
        MonoBehaviour[] matches = FindSceneComponents("Chateau.World.Rooms.RoomView")
            .Where(view =>
            {
                object definition = GetProperty<object>(view, "Definition");
                return definition != null && GetProperty<string>(definition, "StableId") == stableId;
            })
            .ToArray();
        Assert.That(matches, Has.Length.EqualTo(1), $"Expected one RoomView for '{stableId}'.");
        return matches[0];
    }

    private static MonoBehaviour RequireComponentOnGameObject(string objectName, string componentTypeName)
    {
        GameObject owner = RequireSceneGameObject(objectName);
        MonoBehaviour[] matches = owner.GetComponents<MonoBehaviour>()
            .Where(component => TypeMatches(component, componentTypeName))
            .ToArray();
        Assert.That(matches, Has.Length.EqualTo(1),
            $"Expected one '{componentTypeName}' on '{objectName}'.");
        return matches[0];
    }

    private static MonoBehaviour RequireSingleSceneComponent(string typeName)
    {
        List<MonoBehaviour> matches = FindSceneComponents(typeName);
        Assert.That(matches, Has.Count.EqualTo(1), $"Expected one active-scene component '{typeName}'.");
        return matches[0];
    }

    private static void AssertCanonicalSourceAndDestinationRegionPassage(
        MonoBehaviour passage,
        MonoBehaviour expectedSourceRoomView,
        MonoBehaviour expectedReversePassage,
        string expectedStableId,
        string expectedLegacyDoorId,
        string expectedCompatibilityDestination,
        bool expectedExplicitCompatibilityDestination,
        Vector2 expectedBottomLeft,
        Vector2 expectedTopLeft,
        Vector2 expectedTopRight,
        Vector2 expectedBottomRight)
    {
        object definition = GetProperty<object>(passage, "Definition");
        object reverseDefinition = GetProperty<object>(expectedReversePassage, "Definition");
        object sourceDefinition = GetProperty<object>(expectedSourceRoomView, "Definition");
        object destinationDefinition = GetProperty<object>(
            GetProperty<object>(expectedReversePassage, "SourceRoomView"),
            "Definition");
        Assert.That(GetProperty<bool>(passage, "HasGameContext"), Is.True);
        Assert.That(GetProperty<string>(definition, "StableId"), Is.EqualTo(expectedStableId));
        Assert.That(GetProperty<string>(definition, "LegacyDoorId"), Is.EqualTo(expectedLegacyDoorId));
        Assert.That(GetProperty<string>(definition, "CompatibilityDestinationRoomName"),
            Is.EqualTo(expectedCompatibilityDestination));
        Assert.That(GetProperty<bool>(definition, "HasExplicitCompatibilityDestinationRoomName"),
            Is.EqualTo(expectedExplicitCompatibilityDestination));
        Assert.That(GetProperty<object>(definition, "SourceRoom"), Is.SameAs(sourceDefinition));
        Assert.That(GetProperty<object>(definition, "DestinationRoom"), Is.SameAs(destinationDefinition));
        Assert.That(GetProperty<object>(definition, "Reverse"), Is.SameAs(reverseDefinition));
        Assert.That(GetProperty<object>(passage, "SourceRoomView"), Is.SameAs(expectedSourceRoomView));
        Assert.That(GetProperty<object>(passage, "ReversePassage"), Is.SameAs(expectedReversePassage));
        Assert.That(GetProperty<object>(passage, "AnchorMigrationStage").ToString(),
            Is.EqualTo("AuthoredAnchors"));
        Assert.That(GetProperty<bool>(passage, "HasValidAnchorMigrationStage"), Is.True);
        Assert.That(GetProperty<bool>(passage, "UsesAuthoredApproach"), Is.True);
        Assert.That(GetProperty<object>(passage, "ApproachPlacementMode").ToString(),
            Is.EqualTo("BestReachableInSourceRegion"));
        Assert.That(GetProperty<bool>(passage, "HasValidApproachPlacementMode"), Is.True);
        Assert.That(GetProperty<bool>(passage, "UsesBestReachableApproachRegion"), Is.True);
        Assert.That(GetProperty<bool>(passage, "HasMatchingApproachRegionGeometry"), Is.True);
        Assert.That(GetProperty<object>(passage, "ApproachRegion"),
            Is.SameAs(GetProperty<object>(expectedReversePassage, "ArrivalRegion")));
        Assert.That(GetProperty<object>(passage, "ArrivalPlacementMode").ToString(),
            Is.EqualTo("BestReachableInAuthoredRegion"));
        Assert.That(GetProperty<bool>(passage, "HasValidArrivalPlacementMode"), Is.True);
        Assert.That(GetProperty<bool>(passage, "UsesBestReachableArrivalRegion"), Is.True);

        // Omitted serializable-class fields can appear as zero-valued runtime objects.
        // Region placement must remain the only authored placement data either way.
        foreach (string unusedPointProperty in new[] { "ApproachAnchor", "ArrivalAnchor" })
        {
            object unusedPoint = GetProperty<object>(passage, unusedPointProperty);
            if (unusedPoint == null)
            {
                continue;
            }

            Assert.That(GetProperty<Vector2>(unusedPoint, "LogicalPosition"), Is.EqualTo(Vector2.zero));
            Assert.That(GetProperty<Vector2>(unusedPoint, "RoomViewLocalPosition"), Is.EqualTo(Vector2.zero));
        }

        object region = GetProperty<object>(passage, "ArrivalRegion");
        Assert.That(region, Is.Not.Null);
        Assert.That(GetProperty<bool>(region, "HasValidRoomViewLocalCorners"), Is.True);
        AssertVector2Within(GetProperty<Vector2>(region, "BottomLeft"), expectedBottomLeft, 0.0001f,
            $"{passage.gameObject.name} bottom-left");
        AssertVector2Within(GetProperty<Vector2>(region, "TopLeft"), expectedTopLeft, 0.0001f,
            $"{passage.gameObject.name} top-left");
        AssertVector2Within(GetProperty<Vector2>(region, "TopRight"), expectedTopRight, 0.0001f,
            $"{passage.gameObject.name} top-right");
        AssertVector2Within(GetProperty<Vector2>(region, "BottomRight"), expectedBottomRight, 0.0001f,
            $"{passage.gameObject.name} bottom-right");
        Assert.That(passage.GetComponents<Component>(), Has.Length.EqualTo(5));
    }

    private static void AssertCanonicalDestinationRegionPassage(
        MonoBehaviour passage,
        MonoBehaviour expectedSourceRoomView,
        MonoBehaviour expectedReversePassage,
        string expectedStableId,
        string expectedLegacyDoorId,
        string expectedCompatibilityDestination,
        Vector2 expectedApproach,
        Vector2 expectedBottomLeft,
        Vector2 expectedTopLeft,
        Vector2 expectedTopRight,
        Vector2 expectedBottomRight)
    {
        object definition = GetProperty<object>(passage, "Definition");
        object reverseDefinition = GetProperty<object>(expectedReversePassage, "Definition");
        object sourceDefinition = GetProperty<object>(expectedSourceRoomView, "Definition");
        object destinationDefinition = GetProperty<object>(
            GetProperty<object>(expectedReversePassage, "SourceRoomView"),
            "Definition");
        Assert.That(GetProperty<bool>(passage, "HasGameContext"), Is.True);
        Assert.That(GetProperty<string>(definition, "StableId"), Is.EqualTo(expectedStableId));
        Assert.That(GetProperty<string>(definition, "LegacyDoorId"), Is.EqualTo(expectedLegacyDoorId));
        Assert.That(GetProperty<string>(definition, "CompatibilityDestinationRoomName"),
            Is.EqualTo(expectedCompatibilityDestination));
        Assert.That(GetProperty<object>(definition, "SourceRoom"), Is.SameAs(sourceDefinition));
        Assert.That(GetProperty<object>(definition, "DestinationRoom"), Is.SameAs(destinationDefinition));
        Assert.That(GetProperty<object>(definition, "Reverse"), Is.SameAs(reverseDefinition));
        Assert.That(GetProperty<object>(passage, "SourceRoomView"), Is.SameAs(expectedSourceRoomView));
        Assert.That(GetProperty<object>(passage, "ReversePassage"), Is.SameAs(expectedReversePassage));
        Assert.That(GetProperty<object>(passage, "AnchorMigrationStage").ToString(),
            Is.EqualTo("AuthoredAnchors"));
        Assert.That(GetProperty<bool>(passage, "HasValidAnchorMigrationStage"), Is.True);
        Assert.That(GetProperty<bool>(passage, "UsesAuthoredApproach"), Is.True);
        Assert.That(GetProperty<object>(passage, "ArrivalPlacementMode").ToString(),
            Is.EqualTo("BestReachableInAuthoredRegion"));
        Assert.That(GetProperty<bool>(passage, "HasValidArrivalPlacementMode"), Is.True);
        Assert.That(GetProperty<bool>(passage, "UsesBestReachableArrivalRegion"), Is.True);

        object approach = GetProperty<object>(passage, "ApproachAnchor");
        Assert.That(approach, Is.Not.Null);
        Assert.That(GetProperty<object>(approach, "CoordinateSpace").ToString(),
            Is.EqualTo("RoomViewLocal"));
        Assert.That(GetProperty<Vector2>(approach, "LogicalPosition"), Is.EqualTo(Vector2.zero));
        AssertVector2Within(
            GetProperty<Vector2>(approach, "RoomViewLocalPosition"),
            expectedApproach,
            0.0001f,
            $"{passage.gameObject.name} approach");
        object unusedDefaultArrival = GetProperty<object>(passage, "ArrivalAnchor");
        Assert.That(unusedDefaultArrival, Is.Not.Null,
            "Unity materializes the omitted serializable class as a default runtime object.");
        Assert.That(GetProperty<Vector2>(unusedDefaultArrival, "LogicalPosition"), Is.EqualTo(Vector2.zero));
        Assert.That(GetProperty<Vector2>(unusedDefaultArrival, "RoomViewLocalPosition"), Is.EqualTo(Vector2.zero));

        object region = GetProperty<object>(passage, "ArrivalRegion");
        Assert.That(region, Is.Not.Null);
        Assert.That(GetProperty<bool>(region, "HasValidRoomViewLocalCorners"), Is.True);
        AssertVector2Within(GetProperty<Vector2>(region, "BottomLeft"), expectedBottomLeft, 0.0001f,
            $"{passage.gameObject.name} bottom-left");
        AssertVector2Within(GetProperty<Vector2>(region, "TopLeft"), expectedTopLeft, 0.0001f,
            $"{passage.gameObject.name} top-left");
        AssertVector2Within(GetProperty<Vector2>(region, "TopRight"), expectedTopRight, 0.0001f,
            $"{passage.gameObject.name} top-right");
        AssertVector2Within(GetProperty<Vector2>(region, "BottomRight"), expectedBottomRight, 0.0001f,
            $"{passage.gameObject.name} bottom-right");
        Assert.That(passage.GetComponents<Component>(), Has.Length.EqualTo(5));
    }

    private static void AssertRoomViewLocalPassageAnchors(
        MonoBehaviour passage,
        Vector2 expectedApproach,
        Vector2 expectedArrival)
    {
        object approach = GetProperty<object>(passage, "ApproachAnchor");
        object arrival = GetProperty<object>(passage, "ArrivalAnchor");

        Assert.That(GetProperty<object>(approach, "CoordinateSpace").ToString(), Is.EqualTo("RoomViewLocal"));
        Assert.That(GetProperty<object>(arrival, "CoordinateSpace").ToString(), Is.EqualTo("RoomViewLocal"));
        Assert.That(GetProperty<Vector2>(approach, "LogicalPosition"), Is.EqualTo(Vector2.zero));
        Assert.That(GetProperty<Vector2>(arrival, "LogicalPosition"), Is.EqualTo(Vector2.zero));
        AssertVector2Within(
            GetProperty<Vector2>(approach, "RoomViewLocalPosition"),
            expectedApproach,
            0.0001f,
            $"{passage.gameObject.name} approach data");
        AssertVector2Within(
            GetProperty<Vector2>(arrival, "RoomViewLocalPosition"),
            expectedArrival,
            0.0001f,
            $"{passage.gameObject.name} arrival data");
    }

    private static Vector2 ResolvePassageAnchorLogicalPosition(
        MonoBehaviour passage,
        string anchorProperty,
        MonoBehaviour player)
    {
        object anchor = GetProperty<object>(passage, anchorProperty);
        object[] arguments = { player, Vector2.zero };
        Assert.That(
            (bool)RequireMethod(anchor, "TryResolveLogicalPosition", 2).Invoke(anchor, arguments),
            Is.True,
            $"{passage.gameObject.name} {anchorProperty} must resolve through the active RoomView.");
        return (Vector2)arguments[1];
    }

    private static void AssertRoomViewLocalPlayerPosition(
        MonoBehaviour player,
        MonoBehaviour cameraManager,
        Vector2 expected,
        string label)
    {
        AssertVector2Within(
            GetRoomViewLocalPlayerPosition(player, cameraManager, label),
            expected,
            0.05f,
            label);
    }

    private static Vector2 GetRoomViewLocalPlayerPosition(
        MonoBehaviour player,
        MonoBehaviour cameraManager,
        string label)
    {
        Vector2 logicalPosition = GetProperty<Vector2>(player, "LogicalPosition");
        object[] worldArguments = { logicalPosition, Vector2.zero };
        Assert.That(
            (bool)RequireMethod(player, "TryGetWorldPointFromLogicalPosition", 2)
                .Invoke(player, worldArguments),
            Is.True,
            $"{label} must resolve to a world foot point.");
        Vector2 worldPoint = (Vector2)worldArguments[1];
        object[] localArguments =
        {
            new Vector3(worldPoint.x, worldPoint.y, player.transform.position.z),
            Vector2.zero
        };
        Assert.That(
            (bool)RequireMethod(cameraManager, "TryGetActiveRoomStageLocalPoint", 2)
                .Invoke(cameraManager, localArguments),
            Is.True,
            $"{label} must map back through the active RoomView.");
        return (Vector2)localArguments[1];
    }

    private static void AssertFinite(Vector2 value, string label)
    {
        Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x), Is.False,
            $"{label} x must be finite.");
        Assert.That(float.IsNaN(value.y) || float.IsInfinity(value.y), Is.False,
            $"{label} y must be finite.");
    }

    private static void AssertVector2Within(Vector2 actual, Vector2 expected, float tolerance, string label)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance), $"{label} x changed.");
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance), $"{label} y changed.");
    }

    private static List<MonoBehaviour> FindSceneComponents(string typeName)
    {
        Scene scene = SceneManager.GetActiveScene();
        return Resources.FindObjectsOfTypeAll<MonoBehaviour>()
            .Where(component =>
                component != null &&
                component.gameObject.scene == scene &&
                TypeMatches(component, typeName))
            .ToList();
    }

    private static bool TypeMatches(MonoBehaviour component, string typeName)
    {
        Type type = component.GetType();
        return type.Name == typeName || type.FullName == typeName;
    }

    private static GameObject RequireSceneGameObject(string objectName)
    {
        GameObject found = FindSceneGameObject(objectName);
        Assert.That(found, Is.Not.Null, $"Missing active-scene GameObject '{objectName}'.");
        return found;
    }

    private static GameObject FindSceneGameObject(string objectName)
    {
        Scene scene = SceneManager.GetActiveScene();
        Transform match = Resources.FindObjectsOfTypeAll<Transform>()
            .FirstOrDefault(transform =>
                transform != null &&
                transform.gameObject.scene == scene &&
                transform.name == objectName);
        return match != null ? match.gameObject : null;
    }

    private static T GetProperty<T>(object owner, string propertyName)
    {
        PropertyInfo property = owner.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null,
            $"Missing property '{propertyName}' on {owner.GetType().FullName}.");
        return (T)property.GetValue(owner);
    }

    private static T GetField<T>(object owner, string fieldName)
    {
        FieldInfo field = owner.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null,
            $"Missing field '{fieldName}' on {owner.GetType().FullName}.");
        return (T)field.GetValue(owner);
    }

    private static void SetField(object owner, string fieldName, object value)
    {
        FieldInfo field = owner.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null,
            $"Missing field '{fieldName}' on {owner.GetType().FullName}.");
        field.SetValue(owner, value);
    }

    private static object InvokeMethod(object owner, string methodName, params object[] arguments)
    {
        MethodInfo method = RequireMethod(owner, methodName, arguments.Length);
        return method.Invoke(owner, arguments);
    }

    private static MethodInfo RequireMethod(object owner, string methodName, int parameterCount)
    {
        MethodInfo[] methods = owner.GetType().GetMethods(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        MethodInfo[] matches = methods
            .Where(method => method.Name == methodName && method.GetParameters().Length == parameterCount)
            .ToArray();
        Assert.That(matches, Has.Length.EqualTo(1),
            $"Expected one {owner.GetType().FullName}.{methodName} overload with {parameterCount} parameters.");
        return matches[0];
    }

    private static float GetRenderedScreenHeight(Renderer renderer, Camera camera)
    {
        float minimumY = camera.WorldToScreenPoint(renderer.bounds.min).y;
        float maximumY = camera.WorldToScreenPoint(renderer.bounds.max).y;
        return Mathf.Abs(maximumY - minimumY);
    }

    private static string Format(Vector2 value)
    {
        return $"{value.x:0.######},{value.y:0.######}";
    }

    [UnityTest]
    public IEnumerator UpperGalleryMasterBedroomSuiteFarRoundTripUsesCanonicalReciprocalScaledRegions()
    {
        const string GalleryRoom = "Upper Gallery";
        const string MasterRoom = "Master Bedroom Suite";

        yield return BootGameplayFromRealMenu();

        MonoBehaviour gameRoot = RequireSingleSceneComponent("GameRoot");
        MonoBehaviour chapter = RequireSingleSceneComponent("ChapterManager");
        MonoBehaviour navigation = RequireSingleSceneComponent("RoomNavigationManager");
        MonoBehaviour player = RequireComponentOnGameObject("Player", "PointClickPlayerMovement");
        MonoBehaviour cameraManager = RequireSingleSceneComponent("CameraManager");
        MonoBehaviour forwardTrigger = RequireComponentOnGameObject(
            "DoorTrigger_UpperGallery_MasterBedroomSuite", "DoorTriggerNavigation");
        MonoBehaviour reverseTrigger = RequireComponentOnGameObject(
            "DoorTrigger_MasterBedroomSuite_UpperGallery", "DoorTriggerNavigation");
        MonoBehaviour forwardPassage = RequireComponentOnGameObject(
            "DoorTrigger_UpperGallery_MasterBedroomSuite",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour reversePassage = RequireComponentOnGameObject(
            "DoorTrigger_MasterBedroomSuite_UpperGallery",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour galleryView = RequireRoomView("room.upper-gallery");
        MonoBehaviour masterView = RequireRoomView("room.master-bedroom-suite");

        Assert.That(FindSceneComponents("Chateau.World.Rooms.RoomView"), Has.Count.EqualTo(18));
        Assert.That(FindSceneComponents("Chateau.World.Rooms.Passages.Passage"), Has.Count.EqualTo(36));
        List<MonoBehaviour> triggers = FindSceneComponents("DoorTriggerNavigation");
        Assert.That(triggers, Has.Count.EqualTo(45));
        Assert.That(triggers.Count(trigger => GetField<MonoBehaviour>(trigger, "canonicalPassage") != null),
            Is.EqualTo(36));
        Assert.That(triggers.Count(trigger => GetField<MonoBehaviour>(trigger, "canonicalPassage") == null),
            Is.EqualTo(9));
        Assert.That(forwardTrigger.GetComponents<Component>(), Has.Length.EqualTo(5));
        Assert.That(reverseTrigger.GetComponents<Component>(), Has.Length.EqualTo(5));
        Assert.That(GetField<MonoBehaviour>(forwardTrigger, "canonicalPassage"),
            Is.SameAs(forwardPassage));
        Assert.That(GetField<MonoBehaviour>(reverseTrigger, "canonicalPassage"),
            Is.SameAs(reversePassage));
        foreach (MonoBehaviour trigger in new[] { forwardTrigger, reverseTrigger })
        {
            Assert.That(GetField<MonoBehaviour>(trigger, "navigationManager"), Is.SameAs(navigation));
            Assert.That(GetField<Transform>(trigger, "player"), Is.SameAs(player.transform));
            Assert.That(GetField<object>(trigger, "triggerKind").ToString(), Is.EqualTo("Door"));
            Assert.That(GetField<bool>(trigger, "requirePlayerProximity"), Is.True);
            Assert.That(GetField<bool>(trigger, "walkPlayerToTriggerWhenFar"), Is.True);
            Assert.That(GetField<bool>(trigger, "autoActivateAfterApproach"), Is.True);
            Assert.That(GetField<float>(trigger, "maxPlayerScreenDistance"), Is.EqualTo(145f));
            Assert.That(GetProperty<bool>(trigger, "IsStairway"), Is.False);
            Assert.That(GetProperty<string>(trigger, "InteractionLabel"), Is.EqualTo("Door"));
            Assert.That(InvokeMethod(trigger, "GetNavigationCursorIcon").ToString(), Is.EqualTo("Door"));
        }
        Assert.That(GetProperty<string>(forwardTrigger, "SourceRoom"), Is.EqualTo(GalleryRoom));
        Assert.That(GetProperty<string>(forwardTrigger, "DoorName"),
            Is.EqualTo("UpperGallery_MasterBedroomSuite"));
        Assert.That(GetProperty<string>(forwardTrigger, "DestinationRoom"), Is.EqualTo(MasterRoom));
        Assert.That(GetProperty<string>(reverseTrigger, "SourceRoom"), Is.EqualTo(MasterRoom));
        Assert.That(GetProperty<string>(reverseTrigger, "DoorName"),
            Is.EqualTo("MasterBedroomSuite_UpperGallery"));
        Assert.That(GetProperty<string>(reverseTrigger, "DestinationRoom"), Is.EqualTo(GalleryRoom));

        AudioSource passageAudio = RequireSceneGameObject("Audio_DoorOpen").GetComponent<AudioSource>();
        UnityEngine.Object forwardCatalog = GetField<UnityEngine.Object>(forwardTrigger,
            "doorOpenSoundCatalog");
        Assert.That(forwardCatalog, Is.Not.Null);
        Assert.That(GetField<UnityEngine.Object>(reverseTrigger, "doorOpenSoundCatalog"),
            Is.SameAs(forwardCatalog));
        Assert.That(GetField<AudioSource>(forwardTrigger, "doorOpenAudioSource"), Is.SameAs(passageAudio));
        Assert.That(GetField<AudioSource>(reverseTrigger, "doorOpenAudioSource"), Is.SameAs(passageAudio));
        Assert.That(GetField<UnityEngine.Object>(forwardTrigger, "stairwaySoundCatalog"), Is.Null);
        Assert.That(GetField<UnityEngine.Object>(reverseTrigger, "stairwaySoundCatalog"), Is.Null);

        object galleryDefinition = GetProperty<object>(galleryView, "Definition");
        object masterDefinition = GetProperty<object>(masterView, "Definition");
        object forwardDefinition = GetProperty<object>(forwardPassage, "Definition");
        object reverseDefinition = GetProperty<object>(reversePassage, "Definition");
        Assert.That(GetProperty<string>(galleryDefinition, "DisplayName"), Is.EqualTo(GalleryRoom));
        Assert.That(GetProperty<string>(masterDefinition, "DisplayName"), Is.EqualTo(MasterRoom));
        Assert.That(GetProperty<object>(forwardDefinition, "Kind").ToString(), Is.EqualTo("Door"));
        Assert.That(GetProperty<object>(reverseDefinition, "Kind").ToString(), Is.EqualTo("Door"));
        Assert.That(GetProperty<string>(forwardDefinition, "PromptText"), Is.EqualTo("Open Door"));
        Assert.That(GetProperty<string>(reverseDefinition, "PromptText"), Is.EqualTo("Open Door"));
        object[] registered = ((IEnumerable)GetProperty<object>(GetProperty<object>(gameRoot, "Database"),
            "Definitions")).Cast<object>().ToArray();
        Assert.That(registered, Has.Length.EqualTo(55));
        Assert.That(registered, Does.Contain(forwardDefinition));
        Assert.That(registered, Does.Contain(reverseDefinition));

        AssertCanonicalSourceAndDestinationRegionPassage(
            forwardPassage, galleryView, reversePassage,
            "passage.upper-gallery.master-bedroom-suite",
            "UpperGallery_MasterBedroomSuite", MasterRoom, false,
            new Vector2(-770.5059814f, -210.125f),
            new Vector2(-770.5059814f, 264.125f),
            new Vector2(-599.4940186f, 264.125f),
            new Vector2(-599.4940186f, -210.125f));
        AssertCanonicalSourceAndDestinationRegionPassage(
            reversePassage, masterView, forwardPassage,
            "passage.master-bedroom-suite.upper-gallery",
            "MasterBedroomSuite_UpperGallery", GalleryRoom, false,
            new Vector2(-722.8327637f, -70.00001526f),
            new Vector2(-722.8327637f, 195.9999847f),
            new Vector2(-604.0205078f, 195.9999847f),
            new Vector2(-604.0205078f, -70.00001526f));

        InvokeMethod(chapter, "StopChapterCoroutines");
        InvokeMethod(chapter, "StopActiveDialogueForDebugTransition");
        InvokeMethod(player, "SetInputEnabled", true);
        float originalMoveSpeed = GetField<float>(player, "moveSpeed");
        SetField(player, "moveSpeed", 1000f);
        FreezeRoomLookForEvidence();
        List<Vector2> approachStops = new List<Vector2>(2);
        List<string> approachRooms = new List<string>(2);
        int movementStops = 0;
        Action arrived = () =>
        {
            approachStops.Add(GetProperty<Vector2>(player, "LogicalPosition"));
            approachRooms.Add(GetProperty<string>(navigation, "CurrentRoom"));
        };
        Action stopped = () => movementStops++;
        EventInfo arrivalEvent = player.GetType().GetEvent("ArrivedAtDestination");
        EventInfo stoppedEvent = player.GetType().GetEvent("MovementStopped");
        arrivalEvent.AddEventHandler(player, arrived);
        stoppedEvent.AddEventHandler(player, stopped);
        Vector2 masterLanding = Vector2.zero;
        Vector2 galleryLanding = Vector2.zero;
        try
        {
            string[] setupNames =
            {
                "DoorTrigger_GEH_DiningRoom",
                "DoorTrigger_DiningRoom_ButlersPantry",
                "DoorTrigger_ButlersPantry_ServiceCorridor",
                "DoorTrigger_ServiceCorridor_SideStairMudroom"
            };
            string[] setupRooms =
                { "Dining Room", "Butlers Pantry", "Service Corridor", "Side Stair & Mudroom" };
            for (int i = 0; i < setupNames.Length; i++)
            {
                MonoBehaviour setupPassage = RequireComponentOnGameObject(
                    setupNames[i], "Chateau.World.Rooms.Passages.Passage");
                Assert.That((bool)InvokeMethod(navigation, "TryTraverse", setupPassage), Is.True);
                yield return WaitForCurrentRoom(navigation, setupRooms[i], 60);
            }
            Assert.That((bool)InvokeMethod(navigation, "MoveToRoom", "Side Stair Mudroom"), Is.True);
            yield return WaitForCurrentRoom(navigation, "Side Stair Mudroom", 60);
            MonoBehaviour upperSetup = RequireComponentOnGameObject(
                "StairwayTrigger_SideStairMudroom_UpperSittingHall",
                "Chateau.World.Rooms.Passages.Passage");
            Assert.That((bool)InvokeMethod(navigation, "TryTraverse", upperSetup), Is.True);
            yield return WaitForCurrentRoom(navigation, "Upper Sitting Hall", 60);
            MonoBehaviour gallerySetup = RequireComponentOnGameObject(
                "DoorTrigger_UpperSittingHall_UpperGallery",
                "Chateau.World.Rooms.Passages.Passage");
            Assert.That((bool)InvokeMethod(navigation, "TryTraverse", gallerySetup), Is.True);
            yield return WaitForCurrentRoom(navigation, GalleryRoom, 60);

            Assert.That((bool)InvokeMethod(player, "TryWarpTo", new Vector2(5f, -2f), true), Is.True);
            Assert.That((float)InvokeMethod(forwardTrigger, "GetPlayerScreenDistanceToTrigger"),
                Is.GreaterThan(145f));
            SetField(forwardTrigger, "lastPointerActivationFrame", -1);
            InvokeMethod(forwardTrigger, "ActivateDoor");
            Assert.That(GetProperty<bool>(player, "HasDestination"), Is.True);
            yield return WaitForCurrentRoom(navigation, MasterRoom, 240);
            FreezeRoomLookForEvidence();
            yield return null;
            Assert.That(GetProperty<object>(navigation, "CurrentRoomDefinition"),
                Is.SameAs(masterDefinition));
            Assert.That(GetProperty<bool>(masterView, "IsVisible"), Is.True);
            masterLanding = GetRoomViewLocalPlayerPosition(player, cameraManager,
                "Master Bedroom Suite landing");

            Assert.That((bool)InvokeMethod(player, "TryWarpTo", new Vector2(-5f, -2f), true), Is.True);
            Assert.That((float)InvokeMethod(reverseTrigger, "GetPlayerScreenDistanceToTrigger"),
                Is.GreaterThan(145f));
            SetField(reverseTrigger, "lastPointerActivationFrame", -1);
            InvokeMethod(reverseTrigger, "ActivateDoor");
            Assert.That(GetProperty<bool>(player, "HasDestination"), Is.True);
            yield return WaitForCurrentRoom(navigation, GalleryRoom, 240);
            FreezeRoomLookForEvidence();
            yield return null;
            Assert.That(GetProperty<object>(navigation, "CurrentRoomDefinition"),
                Is.SameAs(galleryDefinition));
            Assert.That(GetProperty<bool>(galleryView, "IsVisible"), Is.True);
            galleryLanding = GetRoomViewLocalPlayerPosition(player, cameraManager,
                "Upper Gallery landing");
            Assert.That(approachStops, Has.Count.EqualTo(2));
            Assert.That(approachRooms, Is.EqualTo(new[] { GalleryRoom, MasterRoom }));
            Assert.That(movementStops, Is.EqualTo(2));
            AssertFinite(approachStops[0], "Upper Gallery source-region approach stop");
            AssertFinite(approachStops[1], "Master Bedroom Suite source-region approach stop");
            AssertFinite(masterLanding, "Master Bedroom Suite destination-region landing");
            AssertFinite(galleryLanding, "Upper Gallery destination-region landing");
            AssertFixedRenderingResolution();
        }
        finally
        {
            arrivalEvent.RemoveEventHandler(player, arrived);
            stoppedEvent.RemoveEventHandler(player, stopped);
            SetField(player, "moveSpeed", originalMoveSpeed);
            InvokeMethod(forwardTrigger, "CancelPendingPlayerApproach");
            InvokeMethod(reverseTrigger, "CancelPendingPlayerApproach");
            if (GetProperty<bool>(player, "HasDestination")) InvokeMethod(player, "CancelDestination");
        }

        Debug.Log($"[Slice22Group16PlayMode] masterLocal={Format(masterLanding)} " +
            $"galleryLocal={Format(galleryLanding)} forwardStop={Format(approachStops[0])} " +
            $"reverseStop={Format(approachStops[1])} roomViews=18 passages=36 callers=36/9 " +
            "profile=standard-door/standard-door scaled-master-region protected=gallery/master-content");
    }

    [UnityTest]
    public IEnumerator UpperSittingHallNurseryFarRoundTripUsesCanonicalReciprocalRegions()
    {
        const string UpperRoom = "Upper Sitting Hall";
        const string NurseryRoom = "Nursery";

        yield return BootGameplayFromRealMenu();

        MonoBehaviour gameRoot = RequireSingleSceneComponent("GameRoot");
        MonoBehaviour chapter = RequireSingleSceneComponent("ChapterManager");
        MonoBehaviour navigation = RequireSingleSceneComponent("RoomNavigationManager");
        MonoBehaviour player = RequireComponentOnGameObject("Player", "PointClickPlayerMovement");
        MonoBehaviour cameraManager = RequireSingleSceneComponent("CameraManager");
        MonoBehaviour forwardTrigger = RequireComponentOnGameObject(
            "DoorTrigger_UpperSittingHall_Nursery", "DoorTriggerNavigation");
        MonoBehaviour reverseTrigger = RequireComponentOnGameObject(
            "DoorTrigger_Nursery_UpperSittingHall", "DoorTriggerNavigation");
        MonoBehaviour forwardPassage = RequireComponentOnGameObject(
            "DoorTrigger_UpperSittingHall_Nursery",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour reversePassage = RequireComponentOnGameObject(
            "DoorTrigger_Nursery_UpperSittingHall",
            "Chateau.World.Rooms.Passages.Passage");
        MonoBehaviour upperView = RequireRoomView("room.upper-sitting-hall");
        MonoBehaviour nurseryView = RequireRoomView("room.nursery");

        Assert.That(FindSceneComponents("Chateau.World.Rooms.RoomView"), Has.Count.EqualTo(18));
        Assert.That(FindSceneComponents("Chateau.World.Rooms.Passages.Passage"), Has.Count.EqualTo(36));
        List<MonoBehaviour> triggers = FindSceneComponents("DoorTriggerNavigation");
        Assert.That(triggers, Has.Count.EqualTo(45));
        Assert.That(triggers.Count(trigger => GetField<MonoBehaviour>(trigger, "canonicalPassage") != null),
            Is.EqualTo(36));
        Assert.That(triggers.Count(trigger => GetField<MonoBehaviour>(trigger, "canonicalPassage") == null),
            Is.EqualTo(9));
        Assert.That(forwardTrigger.GetComponents<Component>(), Has.Length.EqualTo(5));
        Assert.That(reverseTrigger.GetComponents<Component>(), Has.Length.EqualTo(5));
        Assert.That(GetField<MonoBehaviour>(forwardTrigger, "canonicalPassage"),
            Is.SameAs(forwardPassage));
        Assert.That(GetField<MonoBehaviour>(reverseTrigger, "canonicalPassage"),
            Is.SameAs(reversePassage));
        foreach (MonoBehaviour trigger in new[] { forwardTrigger, reverseTrigger })
        {
            Assert.That(GetField<MonoBehaviour>(trigger, "navigationManager"), Is.SameAs(navigation));
            Assert.That(GetField<Transform>(trigger, "player"), Is.SameAs(player.transform));
            Assert.That(GetField<object>(trigger, "triggerKind").ToString(), Is.EqualTo("Door"));
            Assert.That(GetField<bool>(trigger, "requirePlayerProximity"), Is.True);
            Assert.That(GetField<bool>(trigger, "walkPlayerToTriggerWhenFar"), Is.True);
            Assert.That(GetField<bool>(trigger, "autoActivateAfterApproach"), Is.True);
            Assert.That(GetField<float>(trigger, "maxPlayerScreenDistance"), Is.EqualTo(145f));
            Assert.That(GetProperty<bool>(trigger, "IsStairway"), Is.False);
            Assert.That(GetProperty<string>(trigger, "InteractionLabel"), Is.EqualTo("Door"));
            Assert.That(InvokeMethod(trigger, "GetNavigationCursorIcon").ToString(), Is.EqualTo("Door"));
        }
        Assert.That(GetProperty<string>(forwardTrigger, "SourceRoom"), Is.EqualTo(UpperRoom));
        Assert.That(GetProperty<string>(forwardTrigger, "DoorName"),
            Is.EqualTo("UpperSittingHall_Nursery"));
        Assert.That(GetProperty<string>(forwardTrigger, "DestinationRoom"), Is.EqualTo(NurseryRoom));
        Assert.That(GetProperty<string>(reverseTrigger, "SourceRoom"), Is.EqualTo(NurseryRoom));
        Assert.That(GetProperty<string>(reverseTrigger, "DoorName"),
            Is.EqualTo("Nursery_UpperSittingHall"));
        Assert.That(GetProperty<string>(reverseTrigger, "DestinationRoom"), Is.EqualTo(UpperRoom));

        AudioSource passageAudio = RequireSceneGameObject("Audio_DoorOpen").GetComponent<AudioSource>();
        UnityEngine.Object forwardCatalog = GetField<UnityEngine.Object>(forwardTrigger,
            "doorOpenSoundCatalog");
        Assert.That(forwardCatalog, Is.Not.Null);
        Assert.That(GetField<UnityEngine.Object>(reverseTrigger, "doorOpenSoundCatalog"),
            Is.SameAs(forwardCatalog));
        Assert.That(GetField<AudioSource>(forwardTrigger, "doorOpenAudioSource"), Is.SameAs(passageAudio));
        Assert.That(GetField<AudioSource>(reverseTrigger, "doorOpenAudioSource"), Is.SameAs(passageAudio));
        Assert.That(GetField<UnityEngine.Object>(forwardTrigger, "stairwaySoundCatalog"), Is.Null);
        Assert.That(GetField<UnityEngine.Object>(reverseTrigger, "stairwaySoundCatalog"), Is.Null);

        object upperDefinition = GetProperty<object>(upperView, "Definition");
        object nurseryDefinition = GetProperty<object>(nurseryView, "Definition");
        object forwardDefinition = GetProperty<object>(forwardPassage, "Definition");
        object reverseDefinition = GetProperty<object>(reversePassage, "Definition");
        Assert.That(GetProperty<string>(upperDefinition, "DisplayName"), Is.EqualTo(UpperRoom));
        Assert.That(GetProperty<string>(nurseryDefinition, "DisplayName"), Is.EqualTo(NurseryRoom));
        Assert.That(GetProperty<object>(forwardDefinition, "Kind").ToString(), Is.EqualTo("Door"));
        Assert.That(GetProperty<object>(reverseDefinition, "Kind").ToString(), Is.EqualTo("Door"));
        Assert.That(GetProperty<string>(forwardDefinition, "PromptText"), Is.EqualTo("Open Door"));
        Assert.That(GetProperty<string>(reverseDefinition, "PromptText"), Is.EqualTo("Open Door"));
        object[] registered = ((IEnumerable)GetProperty<object>(GetProperty<object>(gameRoot, "Database"),
            "Definitions")).Cast<object>().ToArray();
        Assert.That(registered, Has.Length.EqualTo(55));
        Assert.That(registered, Does.Contain(forwardDefinition));
        Assert.That(registered, Does.Contain(reverseDefinition));

        AssertCanonicalSourceAndDestinationRegionPassage(
            forwardPassage, upperView, reversePassage,
            "passage.upper-sitting-hall.nursery",
            "UpperSittingHall_Nursery", NurseryRoom, false,
            new Vector2(157f, -88.39423f),
            new Vector2(157f, 209.43507f),
            new Vector2(277f, 209.43507f),
            new Vector2(277f, -88.39423f));
        AssertCanonicalSourceAndDestinationRegionPassage(
            reversePassage, nurseryView, forwardPassage,
            "passage.nursery.upper-sitting-hall",
            "Nursery_UpperSittingHall", UpperRoom, false,
            new Vector2(-78.9274f, -32.718f),
            new Vector2(-78.9274f, 178.862f),
            new Vector2(46.9532f, 178.862f),
            new Vector2(46.9532f, -32.718f));

        Assert.That(GetField<MonoBehaviour>(RequireComponentOnGameObject(
            "DoorTrigger_UpperSittingHall_UpperGallery", "DoorTriggerNavigation"),
            "canonicalPassage"), Is.Not.Null);
        foreach (string queuedTriggerName in new[]
        {
            "DoorTrigger_UpperSittingHall_BlueBedroom",
            "DoorTrigger_BlueBedroom_UpperSittingHall",
            "DoorTrigger_Nursery_BlueBedroom",
            "DoorTrigger_BlueBedroom_Nursery"
        })
        {
            MonoBehaviour queuedTrigger = RequireComponentOnGameObject(
                queuedTriggerName, "DoorTriggerNavigation");
            Assert.That(GetField<MonoBehaviour>(queuedTrigger, "canonicalPassage"), Is.Null);
            Assert.That(GetField<MonoBehaviour>(queuedTrigger, "navigationManager"), Is.Null);
        }
        foreach (string protectedName in new[]
        {
            "Room_Nursery", "nursery_chair_0", "nursery_table_0", "nursery_chest_0",
            "dog_toy_nursery_0", "PlayerBlocker_nursery_chair_0",
            "PlayerBlocker_nursery_table_0", "PatchCandidate_Nursery_Mobile_Or_Toy_Flicker"
        })
            Assert.That(RequireSceneGameObject(protectedName), Is.Not.Null);

        InvokeMethod(chapter, "StopChapterCoroutines");
        InvokeMethod(chapter, "StopActiveDialogueForDebugTransition");
        InvokeMethod(player, "SetInputEnabled", true);
        float originalMoveSpeed = GetField<float>(player, "moveSpeed");
        SetField(player, "moveSpeed", 1000f);
        FreezeRoomLookForEvidence();
        List<Vector2> approachStops = new List<Vector2>(2);
        List<string> approachRooms = new List<string>(2);
        int movementStops = 0;
        Action arrived = () =>
        {
            approachStops.Add(GetProperty<Vector2>(player, "LogicalPosition"));
            approachRooms.Add(GetProperty<string>(navigation, "CurrentRoom"));
        };
        Action stopped = () => movementStops++;
        EventInfo arrivalEvent = player.GetType().GetEvent("ArrivedAtDestination");
        EventInfo stoppedEvent = player.GetType().GetEvent("MovementStopped");
        arrivalEvent.AddEventHandler(player, arrived);
        stoppedEvent.AddEventHandler(player, stopped);
        Vector2 nurseryLanding = Vector2.zero;
        Vector2 upperLanding = Vector2.zero;
        try
        {
            string[] setupNames =
            {
                "DoorTrigger_GEH_DiningRoom",
                "DoorTrigger_DiningRoom_ButlersPantry",
                "DoorTrigger_ButlersPantry_ServiceCorridor",
                "DoorTrigger_ServiceCorridor_SideStairMudroom"
            };
            string[] setupRooms =
                { "Dining Room", "Butlers Pantry", "Service Corridor", "Side Stair & Mudroom" };
            for (int i = 0; i < setupNames.Length; i++)
            {
                MonoBehaviour setupPassage = RequireComponentOnGameObject(
                    setupNames[i], "Chateau.World.Rooms.Passages.Passage");
                Assert.That((bool)InvokeMethod(navigation, "TryTraverse", setupPassage), Is.True);
                yield return WaitForCurrentRoom(navigation, setupRooms[i], 60);
            }
            Assert.That((bool)InvokeMethod(navigation, "MoveToRoom", "Side Stair Mudroom"), Is.True);
            yield return WaitForCurrentRoom(navigation, "Side Stair Mudroom", 60);
            MonoBehaviour upperSetup = RequireComponentOnGameObject(
                "StairwayTrigger_SideStairMudroom_UpperSittingHall",
                "Chateau.World.Rooms.Passages.Passage");
            Assert.That((bool)InvokeMethod(navigation, "TryTraverse", upperSetup), Is.True);
            yield return WaitForCurrentRoom(navigation, UpperRoom, 60);

            Assert.That((bool)InvokeMethod(player, "TryWarpTo", new Vector2(5f, -2f), true), Is.True);
            Assert.That((float)InvokeMethod(forwardTrigger, "GetPlayerScreenDistanceToTrigger"),
                Is.GreaterThan(145f));
            SetField(forwardTrigger, "lastPointerActivationFrame", -1);
            InvokeMethod(forwardTrigger, "ActivateDoor");
            Assert.That(GetProperty<bool>(player, "HasDestination"), Is.True);
            yield return WaitForCurrentRoom(navigation, NurseryRoom, 240);
            FreezeRoomLookForEvidence();
            yield return null;
            Assert.That(GetProperty<object>(navigation, "CurrentRoomDefinition"),
                Is.SameAs(nurseryDefinition));
            Assert.That(GetProperty<bool>(nurseryView, "IsVisible"), Is.True);
            nurseryLanding = GetRoomViewLocalPlayerPosition(player, cameraManager,
                "Nursery landing");

            Assert.That((bool)InvokeMethod(player, "TryWarpTo", new Vector2(-5f, -2f), true), Is.True);
            Assert.That((float)InvokeMethod(reverseTrigger, "GetPlayerScreenDistanceToTrigger"),
                Is.GreaterThan(145f));
            SetField(reverseTrigger, "lastPointerActivationFrame", -1);
            InvokeMethod(reverseTrigger, "ActivateDoor");
            Assert.That(GetProperty<bool>(player, "HasDestination"), Is.True);
            yield return WaitForCurrentRoom(navigation, UpperRoom, 240);
            FreezeRoomLookForEvidence();
            yield return null;
            Assert.That(GetProperty<object>(navigation, "CurrentRoomDefinition"),
                Is.SameAs(upperDefinition));
            Assert.That(GetProperty<bool>(upperView, "IsVisible"), Is.True);
            upperLanding = GetRoomViewLocalPlayerPosition(player, cameraManager,
                "Upper Sitting Hall landing");
            Assert.That(approachStops, Has.Count.EqualTo(2));
            Assert.That(approachRooms, Is.EqualTo(new[] { UpperRoom, NurseryRoom }));
            Assert.That(movementStops, Is.EqualTo(2));
            AssertFinite(approachStops[0], "Upper Sitting Hall source-region approach stop");
            AssertFinite(approachStops[1], "Nursery source-region approach stop");
            AssertFinite(nurseryLanding, "Nursery destination-region landing");
            AssertFinite(upperLanding, "Upper Sitting Hall destination-region landing");
            AssertFixedRenderingResolution();
        }
        finally
        {
            arrivalEvent.RemoveEventHandler(player, arrived);
            stoppedEvent.RemoveEventHandler(player, stopped);
            SetField(player, "moveSpeed", originalMoveSpeed);
            InvokeMethod(forwardTrigger, "CancelPendingPlayerApproach");
            InvokeMethod(reverseTrigger, "CancelPendingPlayerApproach");
            if (GetProperty<bool>(player, "HasDestination")) InvokeMethod(player, "CancelDestination");
        }

        Debug.Log($"[Slice22Group17PlayMode] nurseryLocal={Format(nurseryLanding)} " +
            $"upperLocal={Format(upperLanding)} forwardStop={Format(approachStops[0])} " +
            $"reverseStop={Format(approachStops[1])} roomViews=18 passages=36 callers=36/9 " +
            "profile=standard-door/standard-door protected=upper/nursery-content");
    }
}
