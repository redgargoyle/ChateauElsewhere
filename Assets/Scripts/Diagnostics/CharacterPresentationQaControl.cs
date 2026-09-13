using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

// DEBUG/QA ONLY. This component is never created unless the process is launched
// with an explicit "-character-presentation-qa <absolute-directory>" argument.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
[DisallowMultipleComponent]
public sealed class CharacterPresentationQaControl : MonoBehaviour
{
    private const string Argument = "-character-presentation-qa";
    private const string GameplaySceneName = "Gameplay";
    private const float CommandPollInterval = 0.25f;
    private const float ScreenshotTimeoutSeconds = 10f;

    private readonly Queue<string> pendingCommands = new Queue<string>();
    private string outputDirectory;
    private float nextCommandPoll;
    private bool commandBusy;
    private bool gameplayLoadRequested;
    private bool exitRequested;
    private int commandSequence;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootFromCommandLine()
    {
        if (!TryReadOutputDirectory(Environment.GetCommandLineArgs(), out string directory))
        {
            return;
        }

        CharacterPresentationQaControl existing = FindAnyObjectByType<CharacterPresentationQaControl>(
            FindObjectsInactive.Include);
        if (existing != null)
        {
            return;
        }

        GameObject host = new GameObject("CharacterPresentationQaControl");
        DontDestroyOnLoad(host);
        CharacterPresentationQaControl control = host.AddComponent<CharacterPresentationQaControl>();
        control.Initialize(directory);
    }

    private static bool TryReadOutputDirectory(string[] arguments, out string directory)
    {
        directory = string.Empty;
        int argumentIndex = Array.IndexOf(arguments, Argument);
        if (argumentIndex < 0 || argumentIndex + 1 >= arguments.Length)
        {
            return false;
        }

        string requestedDirectory = arguments[argumentIndex + 1];
        if (string.IsNullOrWhiteSpace(requestedDirectory))
        {
            return false;
        }

        directory = Path.GetFullPath(requestedDirectory.Trim());
        return true;
    }

    private void Initialize(string directory)
    {
        outputDirectory = directory;
        Directory.CreateDirectory(outputDirectory);
        Application.runInBackground = true;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        AppendLog("qa.log", $"START directory={outputDirectory}");
        WriteHelp();
        StartCoroutine(EnsureGameplayScene());
    }

    private IEnumerator EnsureGameplayScene()
    {
        yield return null;
        if (!IsGameplayLoaded())
        {
            RequestGameplayLoad();
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        gameplayLoadRequested = false;
        AppendLog("qa.log", $"SCENE name={scene.name} mode={mode}");
    }

    private void Update()
    {
        if (string.IsNullOrEmpty(outputDirectory))
        {
            return;
        }

        if (Time.realtimeSinceStartup >= nextCommandPoll)
        {
            nextCommandPoll = Time.realtimeSinceStartup + CommandPollInterval;
            PollCommandFile();
        }

        if (!commandBusy && pendingCommands.Count > 0)
        {
            StartCoroutine(ExecuteNextCommand());
        }
    }

    private void PollCommandFile()
    {
        string commandPath = Path.Combine(outputDirectory, "command.txt");
        if (!File.Exists(commandPath))
        {
            return;
        }

        try
        {
            string[] lines = File.ReadAllLines(commandPath);
            File.Delete(commandPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string command = lines[i].Trim();
                if (!string.IsNullOrEmpty(command) && !command.StartsWith("#", StringComparison.Ordinal))
                {
                    pendingCommands.Enqueue(command);
                }
            }
        }
        catch (Exception exception)
        {
            LogError("poll", exception);
        }
    }

    private IEnumerator ExecuteNextCommand()
    {
        commandBusy = true;
        string command = pendingCommands.Dequeue();
        int sequence = ++commandSequence;
        bool succeeded = false;
        string result = string.Empty;

        Stack<IEnumerator> executionStack = new Stack<IEnumerator>();
        executionStack.Push(ExecuteCommand(command, value => result = value));
        while (executionStack.Count > 0)
        {
            bool hasNext;
            object yieldedValue = null;
            try
            {
                IEnumerator execution = executionStack.Peek();
                hasNext = execution.MoveNext();
                if (hasNext)
                {
                    yieldedValue = execution.Current;
                }
                else
                {
                    executionStack.Pop();
                    succeeded = executionStack.Count == 0;
                }
            }
            catch (Exception exception)
            {
                hasNext = false;
                result = exception.Message;
                LogError(command, exception);
                executionStack.Clear();
            }

            if (!hasNext)
            {
                if (executionStack.Count == 0)
                {
                    break;
                }

                continue;
            }

            if (yieldedValue is IEnumerator nestedExecution)
            {
                executionStack.Push(nestedExecution);
                continue;
            }

            yield return yieldedValue;
        }

        string status = succeeded ? "OK" : "ERROR";
        AppendLog("actions.log", $"{sequence:D4} {status} {command} result={result}");
        File.WriteAllText(
            Path.Combine(outputDirectory, "last-result.txt"),
            $"sequence={sequence}\nstatus={status}\ncommand={command}\nresult={result}\n");
        commandBusy = false;

        if (exitRequested)
        {
            StartCoroutine(ExitAfterResultFlush());
        }
    }

    private IEnumerator ExecuteCommand(string command, Action<string> setResult)
    {
        string verb;
        string argument;
        SplitCommand(command, out verb, out argument);

        switch (verb.ToLowerInvariant())
        {
            case "start":
                RequestGameplayLoad();
                setResult("Gameplay load requested");
                break;
            case "restart":
                SceneManager.LoadScene(GameplaySceneName);
                setResult("Gameplay reloaded");
                break;
            case "room":
                RequireArgument(verb, argument);
                RoomNavigationManager navigation = RequireOne<RoomNavigationManager>();
                if (!navigation.DebugTeleportToRoom(argument))
                {
                    throw new InvalidOperationException($"Room teleport failed for '{argument}'.");
                }
                setResult($"room={navigation.CurrentRoom}");
                break;
            case "walk":
                ExecuteWalk(argument, setResult);
                break;
            case "skip2":
                RequireOne<ChapterManager>().SkipToChapter2ForTesting();
                setResult("Chapter 2 skip requested");
                break;
            case "seven":
                RequireOne<ChapterManager>().SkipToSevenPMForTesting();
                setResult("7:00 PM skip requested");
                break;
            case "dump":
                WriteStateDump(string.IsNullOrWhiteSpace(argument) ? "state" : SafeStem(argument), "manual dump");
                setResult("state dump written");
                break;
            case "screenshot":
            case "capture":
                yield return CaptureAtFrameEnd(argument, setResult);
                break;
            case "lineup":
                StageLineup(argument);
                yield return null;
                WriteStateDump("lineup", "QA STAGED LINEUP - not normal play");
                setResult("QA STAGED LINEUP - actual Butler and selected guest aligned to one room Y");
                break;
            case "exit":
            case "stop":
                exitRequested = true;
                setResult("QA process exit requested after result flush");
                break;
            case "help":
                WriteHelp();
                setResult("help.txt written");
                break;
            default:
                throw new ArgumentException($"Unknown QA command '{verb}'. See help.txt.");
        }
    }

    private void ExecuteWalk(string argument, Action<string> setResult)
    {
        string[] values = argument.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (values.Length != 2 ||
            !float.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float screenX) ||
            !float.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float screenYFromTop))
        {
            throw new ArgumentException("walk requires: walk <screenX> <screenY-from-top>");
        }

        PointClickPlayerMovement movement = PointClickPlayerMovement.FindActiveRoutePlanner("Player");
        if (movement == null)
        {
            throw new InvalidOperationException("No active Butler PointClickPlayerMovement was found.");
        }

        Vector2 unityScreenPoint = new Vector2(screenX, Screen.height - screenYFromTop);
        if (!movement.TrySetDestinationFromScreenPoint(unityScreenPoint, true, true))
        {
            throw new InvalidOperationException($"TrySetDestinationFromScreenPoint rejected ({screenX},{screenYFromTop}).");
        }

        setResult($"accepted screen(top-left)=({screenX:0.##},{screenYFromTop:0.##}) unity={unityScreenPoint}");
    }

    private IEnumerator CaptureAtFrameEnd(string argument, Action<string> setResult)
    {
        string fileName = string.IsNullOrWhiteSpace(argument) ? "capture.png" : Path.GetFileName(argument.Trim());
        if (!fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".png";
        }

        string screenshotPath = Path.Combine(outputDirectory, fileName);
        string stem = SafeStem(Path.GetFileNameWithoutExtension(fileName));
        if (File.Exists(screenshotPath))
        {
            File.Delete(screenshotPath);
        }

        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(screenshotPath, 1);

        float deadline = Time.realtimeSinceStartup + ScreenshotTimeoutSeconds;
        while ((!File.Exists(screenshotPath) || new FileInfo(screenshotPath).Length == 0) &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        if (!File.Exists(screenshotPath) || new FileInfo(screenshotPath).Length == 0)
        {
            throw new IOException($"ScreenCapture did not finish writing '{screenshotPath}' within {ScreenshotTimeoutSeconds:0} seconds.");
        }

        WriteStateDump(stem, $"capture {fileName}");
        setResult($"screenshot={screenshotPath} dump={stem}.txt/.json");
    }

    private void StageLineup(string requestedGuestId)
    {
        RoomNavigationManager navigation = RequireOne<RoomNavigationManager>();
        Camera camera = Camera.main;
        if (camera == null)
        {
            throw new InvalidOperationException("No MainCamera is active.");
        }

        ActorRoomState[] actors = FindObjectsByType<ActorRoomState>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        PointClickPlayerMovement activeButlerMovement = PointClickPlayerMovement.FindActiveRoutePlanner("Player");
        GameObject butler = activeButlerMovement != null
            ? activeButlerMovement.gameObject
            : actors.Where(IsButler).Select(actor => actor.gameObject).FirstOrDefault();
        List<ActorRoomState> guests = actors
            .Where(actor =>
                actor != null &&
                actor.ActorId.StartsWith("Guest", StringComparison.OrdinalIgnoreCase))
            .OrderBy(actor => actor.ActorId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        string cleanGuestId = string.IsNullOrWhiteSpace(requestedGuestId)
            ? string.Empty
            : requestedGuestId.Trim().Replace("_", string.Empty).Replace(" ", string.Empty);
        ActorRoomState guest = string.IsNullOrEmpty(cleanGuestId)
            ? guests.FirstOrDefault()
            : guests.FirstOrDefault(actor =>
                actor.ActorId.Replace("_", string.Empty).Replace(" ", string.Empty)
                    .Equals(cleanGuestId, StringComparison.OrdinalIgnoreCase));

        if (butler == null || guest == null)
        {
            throw new InvalidOperationException(
                $"Lineup requires the active Butler and requested guest '{requestedGuestId}'.");
        }

        string room = navigation.CurrentRoom;
        guest.SetCurrentRoom(room);
        guest.SetVisibleByChapterState(true);
        guest.SetAvailableInCurrentChapter(true);
        guest.SetSeated(false);
        guest.ClearRoomStagePointBinding();

        ActorRoomState butlerState = butler.GetComponent<ActorRoomState>();
        if (butlerState != null)
        {
            butlerState.SetCurrentRoom(room);
            butlerState.SetVisibleByChapterState(true);
            butlerState.SetAvailableInCurrentChapter(true);
            butlerState.SetSeated(false);
            butlerState.ClearRoomStagePointBinding();
        }

        NPCWaypointMover guestMover = guest.GetComponent<NPCWaypointMover>();
        if (guestMover != null)
        {
            guestMover.enabled = false;
        }

        float depth = Mathf.Abs(butler.transform.position.z - camera.transform.position.z);
        if (depth < 0.01f)
        {
            depth = 10f;
        }

        float screenY = Screen.height * 0.62f;
        Vector3 leftWorld = camera.ScreenToWorldPoint(new Vector3(Screen.width * 0.43f, screenY, depth));
        Vector3 rightWorld = camera.ScreenToWorldPoint(new Vector3(Screen.width * 0.57f, screenY, depth));

        CharacterFloorReference butlerFloor = CharacterFloorReference.EnsureForActor(butler);
        CharacterFloorReference guestFloor = CharacterFloorReference.EnsureForActor(guest.gameObject);
        PointClickPlayerMovement movement = activeButlerMovement != null
            ? activeButlerMovement
            : butler.GetComponent<PointClickPlayerMovement>();
        bool warped = false;
        if (movement != null && movement.TryGetLogicalPositionFromWorldPoint(
                leftWorld,
                true,
                movement.LogicalPosition,
                out Vector2 butlerLogicalPoint))
        {
            warped = movement.TryWarpTo(butlerLogicalPoint, true);
        }

        if (!warped)
        {
            butlerFloor.AlignActorToWorldPoint(leftWorld);
        }

        float commonWorldY = butlerFloor.TryGetWorldPoint(out Vector3 settledButlerFloor)
            ? settledButlerFloor.y
            : leftWorld.y;
        guestFloor.AlignActorToWorldPoint(new Vector2(rightWorld.x, commonWorldY));
        butler.GetComponent<CharacterAnimationDisplay>()?.TryApplyCurrentRoomScale();
        guest.GetComponent<CharacterAnimationDisplay>()?.TryApplyCurrentRoomScale();

        AppendLog(
            "qa.log",
            $"QA STAGED LINEUP room={room} butler={GetActorId(butler, butlerState)} guest={guest.ActorId} roomY-world={commonWorldY:0.###} label=not-normal-play");
    }

    private void WriteStateDump(string stem, string label)
    {
        QaSnapshot snapshot = BuildSnapshot(label);
        string json = JsonUtility.ToJson(snapshot, true);
        string text = BuildText(snapshot);
        File.WriteAllText(Path.Combine(outputDirectory, stem + ".json"), json);
        File.WriteAllText(Path.Combine(outputDirectory, stem + ".txt"), text);
        File.WriteAllText(Path.Combine(outputDirectory, "state.json"), json);
        File.WriteAllText(Path.Combine(outputDirectory, "state.txt"), text);
    }

    private QaSnapshot BuildSnapshot(string label)
    {
        RoomNavigationManager navigation = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
        ChapterManager chapter = FindAnyObjectByType<ChapterManager>(FindObjectsInactive.Include);
        QaSnapshot snapshot = new QaSnapshot
        {
            label = label,
            utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            scene = SceneManager.GetActiveScene().name,
            currentRoom = navigation != null ? navigation.CurrentRoom : string.Empty,
            chapter = chapter != null ? chapter.CurrentChapterId : string.Empty,
            phase = chapter != null ? chapter.CurrentPhase.ToString() : string.Empty,
            screen = $"{Screen.width}x{Screen.height}",
            actors = new List<QaActorSnapshot>()
        };

        List<GameObject> actorRoots = FindActorRoots();
        actorRoots.Sort((left, right) => string.Compare(
            GetActorId(left, left != null ? left.GetComponent<ActorRoomState>() : null),
            GetActorId(right, right != null ? right.GetComponent<ActorRoomState>() : null),
            StringComparison.OrdinalIgnoreCase));

        for (int i = 0; i < actorRoots.Count; i++)
        {
            GameObject actorRoot = actorRoots[i];
            ActorRoomState actorState = actorRoot != null ? actorRoot.GetComponent<ActorRoomState>() : null;
            if (!IsActuallyVisible(actorRoot, actorState))
            {
                continue;
            }

            snapshot.actors.Add(BuildActorSnapshot(actorRoot, actorState));
        }

        return snapshot;
    }

    private QaActorSnapshot BuildActorSnapshot(GameObject actorRoot, ActorRoomState actorState)
    {
        Transform root = actorRoot.transform;
        CharacterAnimationDisplay characterDisplay = actorRoot.GetComponent<CharacterAnimationDisplay>();
        Transform display = characterDisplay != null && characterDisplay.AnimationDisplay != null
            ? characterDisplay.AnimationDisplay
            : root.Find("AnimationDisplay");
        CharacterFloorReference floorReference = actorRoot.GetComponent<CharacterFloorReference>();
        Vector3 floorWorld = root.position;
        bool hasFloorReference = floorReference != null && floorReference.TryGetWorldPoint(out floorWorld);
        PointClickPlayerMovement movement = actorRoot.GetComponent<PointClickPlayerMovement>();
        if (movement != null && movement.isActiveAndEnabled &&
            movement.TryGetWorldPointFromLogicalPosition(movement.LogicalPosition, out Vector2 logicalFloor))
        {
            floorWorld = new Vector3(logicalFloor.x, logicalFloor.y, root.position.z);
        }
        else if (!hasFloorReference)
        {
            CharacterFootPositionUtility.TryGetWorldPoint(actorRoot, true, true, out floorWorld);
        }
        Vector3 floorScreen = Camera.main != null ? Camera.main.WorldToScreenPoint(floorWorld) : Vector3.zero;

        QaActorSnapshot result = new QaActorSnapshot
        {
            actorId = GetActorId(actorRoot, actorState),
            objectName = actorRoot.name,
            room = actorState != null ? actorState.CurrentRoomId : ResolveCurrentRoom(),
            seated = actorState != null && actorState.IsSeated,
            visible = actorState == null || actorState.IsVisibleInCurrentRoom,
            rootWorldPosition = Format(root.position),
            rootLocalScale = Format(root.localScale),
            rootLossyScale = Format(root.lossyScale),
            displayPath = display != null ? GetHierarchyPath(display) : "<missing>",
            displayWorldPosition = display != null ? Format(display.position) : "<missing>",
            displayLocalScale = display != null ? Format(display.localScale) : "<missing>",
            displayLossyScale = display != null ? Format(display.lossyScale) : "<missing>",
            floorReferenceInitialized = hasFloorReference,
            floorWorld = Format(floorWorld),
            floorScreen = Format(floorScreen),
            renderers = new List<QaSpriteSnapshot>(),
            sortingGroups = new List<string>(),
            roomCoordinateY = "<unavailable>",
            roomStageScale = "<unavailable>"
        };

        CharacterScaleRoom[] scaleRooms = FindObjectsByType<CharacterScaleRoom>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        CharacterScaleRoom scaleRoom = scaleRooms.FirstOrDefault(candidate =>
            candidate != null && SameRoom(candidate.RoomName, result.room));
        if (scaleRoom != null)
        {
            result.roomStageScale = scaleRoom.CurrentStageScale.ToString("0.######", CultureInfo.InvariantCulture);
            if (scaleRoom.TryGetCharacterRoomY(floorWorld, out float roomY))
            {
                result.roomCoordinateY = roomY.ToString("0.###", CultureInfo.InvariantCulture);
            }
        }

        SpriteRenderer[] renderers = actorRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
            {
                result.renderers.Add(BuildSpriteSnapshot(renderer));
            }
        }

        SortingGroup[] groups = actorRoot.GetComponentsInChildren<SortingGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            SortingGroup group = groups[i];
            if (group != null)
            {
                result.sortingGroups.Add(
                    $"{GetHierarchyPath(group.transform)} enabled={group.enabled} layer={group.sortingLayerName} order={group.sortingOrder}");
            }
        }

        return result;
    }

    private QaSpriteSnapshot BuildSpriteSnapshot(SpriteRenderer renderer)
    {
        Sprite sprite = renderer.sprite;
        QaSpriteSnapshot result = new QaSpriteSnapshot
        {
            rendererPath = GetHierarchyPath(renderer.transform),
            enabled = renderer.enabled,
            sortingLayer = renderer.sortingLayerName,
            sortingOrder = renderer.sortingOrder,
            rendererBoundsCenter = Format(renderer.bounds.center),
            rendererBoundsSize = Format(renderer.bounds.size),
            spriteName = sprite != null ? sprite.name : "<null>",
            spriteAssetPath = GetSpriteAssetPath(sprite),
            spriteBoundsCenter = sprite != null ? Format(sprite.bounds.center) : "<null>",
            spriteBoundsSize = sprite != null ? Format(sprite.bounds.size) : "<null>",
            pixelsPerUnit = sprite != null
                ? sprite.pixelsPerUnit.ToString("0.###", CultureInfo.InvariantCulture)
                : "<null>",
            pivot = sprite != null ? Format(sprite.pivot) : "<null>",
            rect = sprite != null ? Format(sprite.rect) : "<null>",
            sortingGroupAncestry = new List<string>()
        };

        Transform cursor = renderer.transform;
        while (cursor != null)
        {
            SortingGroup[] groups = cursor.GetComponents<SortingGroup>();
            for (int i = 0; i < groups.Length; i++)
            {
                SortingGroup group = groups[i];
                result.sortingGroupAncestry.Add(
                    $"{GetHierarchyPath(cursor)} enabled={group.enabled} layer={group.sortingLayerName} order={group.sortingOrder}");
            }

            cursor = cursor.parent;
        }

        return result;
    }

    private static List<GameObject> FindActorRoots()
    {
        List<GameObject> results = new List<GameObject>();
        HashSet<GameObject> seen = new HashSet<GameObject>();
        PointClickPlayerMovement butlerMovement = PointClickPlayerMovement.FindActiveRoutePlanner("Player");
        if (butlerMovement != null && seen.Add(butlerMovement.gameObject))
        {
            results.Add(butlerMovement.gameObject);
        }

        ActorRoomState[] actorStates = FindObjectsByType<ActorRoomState>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < actorStates.Length; i++)
        {
            ActorRoomState actorState = actorStates[i];
            if (actorState != null && seen.Add(actorState.gameObject))
            {
                results.Add(actorState.gameObject);
            }
        }

        return results;
    }

    private static bool IsActuallyVisible(GameObject actorRoot, ActorRoomState actorState)
    {
        if (actorRoot == null ||
            !actorRoot.activeInHierarchy ||
            (actorState != null && !actorState.IsVisibleInCurrentRoom))
        {
            return false;
        }

        SpriteRenderer[] renderers = actorRoot.GetComponentsInChildren<SpriteRenderer>(true);
        return renderers.Any(renderer =>
            renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy);
    }

    private static string GetActorId(GameObject actorRoot, ActorRoomState actorState)
    {
        if (actorState != null)
        {
            return actorState.ActorId;
        }

        PointClickPlayerMovement movement = actorRoot != null
            ? actorRoot.GetComponent<PointClickPlayerMovement>()
            : null;
        return movement != null ? "Butler" : actorRoot != null ? actorRoot.name : "<missing>";
    }

    private static string ResolveCurrentRoom()
    {
        RoomNavigationManager navigation = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
        return navigation != null ? navigation.CurrentRoom : string.Empty;
    }

    private static bool IsButler(ActorRoomState actor)
    {
        if (actor == null)
        {
            return false;
        }

        return actor.GetComponent<PointClickPlayerMovement>() != null ||
               actor.ActorId.IndexOf("Butler", StringComparison.OrdinalIgnoreCase) >= 0 ||
               actor.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string BuildText(QaSnapshot snapshot)
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine($"LABEL {snapshot.label}");
        report.AppendLine($"UTC {snapshot.utc}");
        report.AppendLine($"SCENE {snapshot.scene}");
        report.AppendLine($"ROOM {snapshot.currentRoom}");
        report.AppendLine($"CHAPTER {snapshot.chapter} PHASE {snapshot.phase}");
        report.AppendLine($"SCREEN {snapshot.screen}");
        report.AppendLine($"VISIBLE_ACTORS {snapshot.actors.Count}");

        for (int i = 0; i < snapshot.actors.Count; i++)
        {
            QaActorSnapshot actor = snapshot.actors[i];
            report.AppendLine($"ACTOR {actor.actorId} object={actor.objectName} room={actor.room} seated={actor.seated} visible={actor.visible}");
            report.AppendLine($"  root position={actor.rootWorldPosition} localScale={actor.rootLocalScale} lossyScale={actor.rootLossyScale}");
            report.AppendLine($"  display path={actor.displayPath} position={actor.displayWorldPosition} localScale={actor.displayLocalScale} lossyScale={actor.displayLossyScale}");
            report.AppendLine($"  floor initialized={actor.floorReferenceInitialized} world={actor.floorWorld} screen={actor.floorScreen}");
            report.AppendLine($"  roomY={actor.roomCoordinateY} stageScale={actor.roomStageScale}");
            for (int rendererIndex = 0; rendererIndex < actor.renderers.Count; rendererIndex++)
            {
                QaSpriteSnapshot renderer = actor.renderers[rendererIndex];
                report.AppendLine($"  SPRITE {renderer.rendererPath}");
                report.AppendLine($"    asset={renderer.spriteAssetPath} name={renderer.spriteName} boundsCenter={renderer.spriteBoundsCenter} boundsSize={renderer.spriteBoundsSize}");
                report.AppendLine($"    ppu={renderer.pixelsPerUnit} pivot={renderer.pivot} rect={renderer.rect} rendererBoundsCenter={renderer.rendererBoundsCenter} rendererBoundsSize={renderer.rendererBoundsSize}");
                report.AppendLine($"    sorting layer={renderer.sortingLayer} order={renderer.sortingOrder}");
                for (int groupIndex = 0; groupIndex < renderer.sortingGroupAncestry.Count; groupIndex++)
                {
                    report.AppendLine($"    sortingGroup {renderer.sortingGroupAncestry[groupIndex]}");
                }
            }
            for (int groupIndex = 0; groupIndex < actor.sortingGroups.Count; groupIndex++)
            {
                report.AppendLine($"  GROUP {actor.sortingGroups[groupIndex]}");
            }
        }

        return report.ToString();
    }

    private void RequestGameplayLoad()
    {
        if (IsGameplayLoaded() || gameplayLoadRequested)
        {
            return;
        }

        gameplayLoadRequested = true;
        SceneManager.LoadScene(GameplaySceneName);
    }

    private static bool IsGameplayLoaded()
    {
        return string.Equals(
            SceneManager.GetActiveScene().name,
            GameplaySceneName,
            StringComparison.OrdinalIgnoreCase);
    }

    private static T RequireOne<T>() where T : UnityEngine.Object
    {
        T instance = FindAnyObjectByType<T>(FindObjectsInactive.Include);
        if (instance == null)
        {
            throw new InvalidOperationException($"Required {typeof(T).Name} was not found in the loaded scene.");
        }

        return instance;
    }

    private static void SplitCommand(string command, out string verb, out string argument)
    {
        int separator = command.IndexOfAny(new[] { ' ', '\t' });
        if (separator < 0)
        {
            verb = command;
            argument = string.Empty;
            return;
        }

        verb = command.Substring(0, separator).Trim();
        argument = command.Substring(separator + 1).Trim();
    }

    private static void RequireArgument(string verb, string argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            throw new ArgumentException($"{verb} requires an argument.");
        }
    }

    private static string SafeStem(string value)
    {
        string stem = string.IsNullOrWhiteSpace(value) ? "state" : value.Trim();
        char[] invalid = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalid.Length; i++)
        {
            stem = stem.Replace(invalid[i], '_');
        }

        return stem;
    }

    private static string Format(Vector2 value)
    {
        return $"({value.x:0.###},{value.y:0.###})";
    }

    private static string Format(Vector3 value)
    {
        return $"({value.x:0.###},{value.y:0.###},{value.z:0.###})";
    }

    private static string Format(Rect value)
    {
        return $"({value.x:0.###},{value.y:0.###},{value.width:0.###},{value.height:0.###})";
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return "<null>";
        }

        Stack<string> parts = new Stack<string>();
        Transform cursor = transform;
        while (cursor != null)
        {
            parts.Push(cursor.name);
            cursor = cursor.parent;
        }

        return string.Join("/", parts);
    }

    private static string GetSpriteAssetPath(Sprite sprite)
    {
#if UNITY_EDITOR
        return sprite != null ? AssetDatabase.GetAssetPath(sprite) : "<null>";
#else
        return sprite != null ? "<unavailable outside editor>" : "<null>";
#endif
    }

    private static bool SameRoom(string left, string right)
    {
        return string.Equals(
            CharacterScaleCatalog.NormalizeRoomName(left),
            CharacterScaleCatalog.NormalizeRoomName(right),
            StringComparison.Ordinal);
    }

    private void WriteHelp()
    {
        string help =
            "Character Presentation QA commands (one per line; queued in file order)\n" +
            "start                         Load Gameplay if it is not loaded\n" +
            "restart                       Reload Gameplay\n" +
            "dump [stem]                   Write <stem>.txt/.json and state.txt/.json\n" +
            "capture [filename.png]        Full-UI ScreenCapture at frame end, then matching dump\n" +
            "screenshot [filename.png]     Alias for capture\n" +
            "room <room name>              Debug-teleport through RoomNavigationManager\n" +
            "walk <screenX> <screenY>      Real TrySetDestinationFromScreenPoint; Y is from image top\n" +
            "skip2                         Invoke ChapterManager chapter-2 testing skip\n" +
            "seven                         Invoke ChapterManager 7:00 PM testing skip\n" +
            "lineup [guest id]             QA STAGED: actual Butler + selected Guest at one room Y\n" +
            "stop / exit                   Flush the command result, then exit Unity cleanly\n" +
            "help                          Rewrite this file\n";
        File.WriteAllText(Path.Combine(outputDirectory, "help.txt"), help);
    }

    private IEnumerator ExitAfterResultFlush()
    {
        yield return null;
        AppendLog("qa.log", "EXIT requested by QA command");
#if UNITY_EDITOR
        EditorApplication.Exit(0);
#else
        Application.Quit(0);
#endif
    }

    private void AppendLog(string fileName, string message)
    {
        File.AppendAllText(
            Path.Combine(outputDirectory, fileName),
            $"{DateTime.UtcNow:O} frame={Time.frameCount} realtime={Time.realtimeSinceStartup:0.000} {message}\n");
    }

    private void LogError(string command, Exception exception)
    {
        AppendLog("errors.log", $"command={command} exception={exception}");
        Debug.LogError($"[CharacterPresentationQA] command '{command}' failed: {exception}", this);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    [Serializable]
    private sealed class QaSnapshot
    {
        public string label;
        public string utc;
        public string scene;
        public string currentRoom;
        public string chapter;
        public string phase;
        public string screen;
        public List<QaActorSnapshot> actors;
    }

    [Serializable]
    private sealed class QaActorSnapshot
    {
        public string actorId;
        public string objectName;
        public string room;
        public bool seated;
        public bool visible;
        public string rootWorldPosition;
        public string rootLocalScale;
        public string rootLossyScale;
        public string displayPath;
        public string displayWorldPosition;
        public string displayLocalScale;
        public string displayLossyScale;
        public bool floorReferenceInitialized;
        public string floorWorld;
        public string floorScreen;
        public string roomCoordinateY;
        public string roomStageScale;
        public List<QaSpriteSnapshot> renderers;
        public List<string> sortingGroups;
    }

    [Serializable]
    private sealed class QaSpriteSnapshot
    {
        public string rendererPath;
        public bool enabled;
        public string sortingLayer;
        public int sortingOrder;
        public string rendererBoundsCenter;
        public string rendererBoundsSize;
        public string spriteName;
        public string spriteAssetPath;
        public string spriteBoundsCenter;
        public string spriteBoundsSize;
        public string pixelsPerUnit;
        public string pivot;
        public string rect;
        public List<string> sortingGroupAncestry;
    }
}
#endif
