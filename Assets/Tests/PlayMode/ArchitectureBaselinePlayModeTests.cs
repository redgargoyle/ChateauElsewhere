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

        MonoBehaviour navigation = RequireSingleSceneComponent("RoomNavigationManager");
        MonoBehaviour chapter = RequireSingleSceneComponent("ChapterManager");
        MonoBehaviour arrival = RequireSingleSceneComponent("Chapter1ArrivalController");
        MonoBehaviour player = RequireComponentOnGameObject("Player", "PointClickPlayerMovement");
        SpriteRenderer playerRenderer = player.GetComponent<SpriteRenderer>();
        SpriteRenderer doorRenderer = RequireSceneGameObject("Door_answer_trigger").GetComponent<SpriteRenderer>();
        Camera camera = Camera.main;

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
}
