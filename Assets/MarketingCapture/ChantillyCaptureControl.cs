#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Adapted from the September 11 production helper. Explicit development/editor
// capture only. No checkpoint, teleport, clock skip, or dialogue skip commands.
public sealed class ChantillyCaptureControl : MonoBehaviour
{
    private string directory;
    private bool clean = true;
    private bool busy;
    private float nextPoll;
    private StreamWriter frames;

    [Serializable] private sealed class Result
    {
        public string request_id, command, status, message;
        public int player_pid;
        public double realtime;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-chantilly-capture");
        if (index < 0 || index + 1 >= args.Length) return;
        string requested = args[index + 1];
        if (!Path.IsPathRooted(requested)) throw new ArgumentException("Capture directory must be absolute.");
        if (FindAnyObjectByType<ChantillyCaptureControl>(FindObjectsInactive.Include) != null) return;
        string path = Path.GetFullPath(requested);
        Directory.CreateDirectory(path);
        if (Directory.EnumerateFileSystemEntries(path).Any())
            throw new IOException("Capture requires a new, empty control directory: " + path);
        var host = new GameObject("ChantillyMarketingCapture");
        DontDestroyOnLoad(host);
        var control = host.AddComponent<ChantillyCaptureControl>();
        control.directory = path;
        control.frames = new StreamWriter(new FileStream(Path.Combine(path, "frames.csv"), FileMode.CreateNew));
        control.frames.WriteLine("frame,realtime,delta");
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        control.WriteResult("ready.json", "", "", "ready", "Development capture enabled; normal gameplay progression only.");
    }

    private T One<T>() where T : UnityEngine.Object => FindFirstObjectByType<T>();

    private void Update()
    {
        if (directory == null) return;
        frames.WriteLine($"{Time.frameCount},{Time.realtimeSinceStartupAsDouble:F6},{Time.unscaledDeltaTime:F6}");
        if (Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + 0.15f;
        frames.Flush();
        if (busy) return;
        string path = Path.Combine(directory, "command.txt");
        if (!File.Exists(path)) return;
        string request = File.ReadAllText(path).Trim();
        File.Delete(path);
        int separator = request.IndexOf('\t');
        string id = separator < 0 ? "" : request.Substring(0, separator);
        string command = separator < 0 ? request : request.Substring(separator + 1);
        StartCoroutine(Execute(id, command));
    }

    private IEnumerator Execute(string id, string command)
    {
        busy = true;
        Exception failure = null;
        IEnumerator action = Run(command);
        while (true)
        {
            bool more = false;
            object current = null;
            try { more = action.MoveNext(); if (more) current = action.Current; }
            catch (Exception error) { failure = error; }
            if (failure != null || !more) break;
            yield return current;
        }
        string status = failure == null ? "ok" : "error";
        File.AppendAllText(Path.Combine(directory, "actions.log"),
            $"{DateTime.UtcNow:O} {Time.realtimeSinceStartup:F3} {id} {status} {command}\n");
        if (failure != null) File.AppendAllText(Path.Combine(directory, "errors.log"), failure + "\n");
        WriteResult("result.json", id, command, status, failure?.Message ?? "Completed");
        busy = false;
    }

    private IEnumerator Run(string command)
    {
        var arrival = One<Chapter1ArrivalController>();
        var navigation = One<RoomNavigationManager>();
        if (command == "start")
        {
            if (SceneManager.GetActiveScene().name == "Gameplay") throw new InvalidOperationException("Gameplay is already running.");
            yield return SceneManager.LoadSceneAsync("Gameplay");
        }
        else if (command == "door")
        {
            var player = PointClickPlayerMovement.FindActiveRoutePlanner("Player");
            Vector2 target;
            if (arrival.TryGetFrontDoorApproachDestination(player, out target))
            {
                player.TrySetDestination(target, true);
                float deadline = Time.time + 20;
                while (player.HasDestination && Time.time < deadline) yield return null;
            }
            if (!arrival.IsButlerCloseToFrontDoor(player)) throw new InvalidOperationException("Door approach failed.");
            arrival.AnswerFrontDoor();
        }
        else if (command.StartsWith("coat "))
            arrival.HandleCoatClicked(FindObjectsByType<Chapter1CoatPickup>(FindObjectsSortMode.None)
                .First(x => x.GuestId == command.Substring(5) || x.CoatId == command.Substring(5)));
        else if (command == "closet") arrival.HandleClosetClicked();
        else if (command == "hud on") clean = false;
        else if (command == "hud off") clean = true;
        else if (command.StartsWith("navigate "))
        {
            string destination = command.Substring(9);
            FindObjectsByType<DoorTriggerNavigation>(FindObjectsSortMode.None)
                .First(x => Normalize(x.SourceRoom) == Normalize(navigation.CurrentRoom)
                    && Normalize(x.DestinationRoom) == Normalize(destination)).ActivateDoor();
            float deadline = Time.time + 30;
            while (Normalize(navigation.CurrentRoom) != Normalize(destination) && Time.time < deadline) yield return null;
            if (Normalize(navigation.CurrentRoom) != Normalize(destination)) throw new InvalidOperationException("Door transition timed out.");
        }
        else if (command == "address")
        {
            var chapter = One<Chapter2Controller>();
            if (chapter.CurrentPhase.ToString() != "AwaitingAddressPrompt") throw new InvalidOperationException("Formal address is not ready.");
            chapter.HandleAddressGuestsPrompt();
        }
        else if (command.StartsWith("guest "))
        {
            if (!One<Chapter2GuestSearchController>().TryStartGuestConversation(command.Substring(6)))
                throw new InvalidOperationException("Guest conversation was rejected by normal game rules.");
        }
        else if (command.StartsWith("button "))
        {
            string key = command.Substring(7);
            if (key != "Button_NewGame" && !key.StartsWith("Button_SubtitleChoice", StringComparison.Ordinal))
                throw new ArgumentException("Only New Game and genuine dialogue choices are allowed.");
            FindObjectsByType<Button>(FindObjectsSortMode.None)
                .First(x => x.interactable && x.gameObject.activeInHierarchy && x.name == key).onClick.Invoke();
        }
        else if (command == "dump") Dump();
        else if (command.StartsWith("musicstart "))
        {
            string prefix = command.Substring(11);
            if (!Path.IsPathRooted(prefix)) throw new ArgumentException("Music prefix must be absolute.");
            int index = 0;
            foreach (var binding in FindObjectsByType<GameAudioSourceVolume>(FindObjectsSortMode.None).Where(x => x.Channel == GameAudioChannel.Music))
            {
                var source = binding.GetComponent<AudioSource>();
                if (source == null || !source.isPlaying) continue;
                var tap = source.GetComponent<ChantillyMusicTap>() ?? source.gameObject.AddComponent<ChantillyMusicTap>();
                tap.Begin(prefix + "-" + index++ + ".wav", source);
            }
            if (index == 0) throw new InvalidOperationException("No currently playing music source to tap.");
        }
        else if (command == "musicstop")
            foreach (var tap in FindObjectsByType<ChantillyMusicTap>(FindObjectsSortMode.None)) tap.End();
        else throw new ArgumentException("Unsupported capture command: " + command);
    }

    private static string Normalize(string name) => string.Join(" ", name.ToLowerInvariant().Replace("&", " ").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

    private void LateUpdate()
    {
        if (directory == null) return;
        Cursor.visible = false;
        foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            if (text.name == "Text_CurrentRoom" || text.name == "Text_Chapter1Status" || text.name == "Text_Chapter2Status") text.enabled = !clean;
        foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
            if (button.name == "Button_Settings" || button.name == "Button_Lights")
                foreach (var graphic in button.GetComponentsInChildren<Graphic>()) graphic.enabled = !clean;
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (canvas.name.IndexOf("Debug", StringComparison.OrdinalIgnoreCase) >= 0) canvas.enabled = false;
    }

    private void Dump()
    {
        var text = new StringBuilder();
        text.AppendLine("SCENE " + SceneManager.GetActiveScene().name);
        var manager = One<ChapterManager>(); if (manager) text.AppendLine("CHAPTER " + manager.CurrentChapterId);
        var chapter = One<Chapter2Controller>(); if (chapter) text.AppendLine("PHASE " + chapter.CurrentPhase);
        var search = One<Chapter2GuestSearchController>();
        if (search) text.AppendLine("SEARCH " + search.FoundGuestCount + "/" + search.GuestCount + " pending_exits=" + search.HasPendingGuestExitsToDining);
        var speech = One<DialogueSpeechService>();
        if (speech)
        {
            var current = speech.GetCurrentSpeech();
            text.AppendLine($"SPEECH active={current.HadActiveSpeech} queued={current.HadQueuedSpeech} interruption={speech.IsSpeechInterruptionActive} line={current.LineId} speaker={current.SpeakerId}");
        }
        var player = PointClickPlayerMovement.FindActiveRoutePlanner("Player");
        if (player) text.AppendLine($"PLAYER moving={player.HasDestination} input={player.InputEnabled}");
        var clock = One<ChapterClock>(); if (clock) text.AppendLine("CLOCK " + clock.CurrentTimeLabel + " running=" + clock.IsRunning);
        var navigation = One<RoomNavigationManager>(); if (navigation) text.AppendLine("ROOM " + navigation.CurrentRoom);
        var arrival = One<Chapter1ArrivalController>(); if (arrival) text.AppendLine(arrival.BuildDebugState());
        foreach (var door in FindObjectsByType<DoorTriggerNavigation>(FindObjectsSortMode.None)) text.AppendLine("DOOR " + door.name + " " + door.SourceRoom + " -> " + door.DestinationRoom);
        foreach (var actor in FindObjectsByType<ActorRoomState>(FindObjectsSortMode.None)) text.AppendLine($"ACTOR {actor.ActorId} room={actor.CurrentRoomId} visible={actor.IsVisibleInCurrentRoom} seated={actor.IsSeated}");
        foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None)) text.AppendLine("BUTTON " + button.name + " active=" + (button.interactable && button.gameObject.activeInHierarchy) + " text=" + string.Join(" / ", button.GetComponentsInChildren<TMP_Text>().Select(t => t.text)));
        foreach (var line in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)) if (line.isActiveAndEnabled) text.AppendLine("TEXT " + line.name + " " + line.text);
        File.WriteAllText(Path.Combine(directory, "state.txt"), text.ToString());
    }

    private void WriteResult(string file, string id, string command, string status, string message)
    {
        var result = new Result { request_id = id, command = command, status = status, message = message,
            player_pid = System.Diagnostics.Process.GetCurrentProcess().Id, realtime = Time.realtimeSinceStartupAsDouble };
        File.WriteAllText(Path.Combine(directory, file), JsonUtility.ToJson(result, true));
    }

    private void OnDestroy() { frames?.Dispose(); }
}

// Existing music only: dry float stem, removed from the captured main mix while
// enabled. This is not a dialogue/effects splitter. Finish gracefully for headers.
public sealed class ChantillyMusicTap : MonoBehaviour
{
    private readonly object gate = new object();
    private BinaryWriter writer;
    private int rate, channels = 2;
    private long samples;
    public void Begin(string path, AudioSource source)
    {
        lock (gate) if (writer != null) throw new InvalidOperationException("Music tap already recording.");
        if (File.Exists(path) || File.Exists(path + ".timing.json")) throw new IOException("Music output already exists: " + path);
        rate = AudioSettings.outputSampleRate;
        lock (gate)
        {
            writer = new BinaryWriter(new FileStream(path, FileMode.CreateNew));
            writer.Write(new byte[44]); samples = 0;
        }
        // Timestamp is the enable request, not an exact FFmpeg synchronization
        // marker. Measure and verify the lead-in during production.
        File.WriteAllText(path + ".timing.json", "{\"requested_utc\":\"" + DateTime.UtcNow.ToString("O")
            + "\",\"unity_realtime\":" + Time.realtimeSinceStartupAsDouble.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
            + ",\"source\":\"" + source.name.Replace("\"", "'") + "\"}");
    }
    private void OnAudioFilterRead(float[] data, int count)
    {
        lock (gate)
        {
            if (writer == null) return;
            channels = count;
            foreach (float sample in data) writer.Write(sample);
            samples += data.Length;
            Array.Clear(data, 0, data.Length);
        }
    }
    public void End()
    {
        lock (gate)
        {
            if (writer == null) return;
            writer.BaseStream.Position = 0;
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write((int)(36 + samples * 4));
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)3); writer.Write((short)channels); writer.Write(rate);
            writer.Write(rate * channels * 4); writer.Write((short)(channels * 4)); writer.Write((short)32);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write((int)(samples * 4));
            writer.Dispose(); writer = null;
        }
    }
    private void OnDestroy() { End(); }
}
#endif
