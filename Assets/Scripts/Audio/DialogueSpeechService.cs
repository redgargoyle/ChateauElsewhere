using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DialogueSpeechService : MonoBehaviour
{
    private sealed class GuestMovementPauseLease
    {
        public NPCWaypointMover Mover;
        public bool Released;
    }

    private const string ServiceObjectName = "DialogueSpeechService";
    private const float CharactersPerSecond = 24f;
    private const float MinimumReadSeconds = 1.25f;
    private const float MaximumReadSeconds = 6f;
    private const float VoiceTailSeconds = 0.1f;

    [SerializeField] private SubtitleService subtitleService;
    [SerializeField] private GuestVoiceLinePlayback voicePlayback;
    [SerializeField] private SpeakingCharacterIndicator speakingIndicator;
    [SerializeField] private bool logMissingVoiceLines;

    private bool normalSpeechActive;
    private bool skipRequested;
    private bool speechInterruptionActive;
    private bool activeSpeechPaused;
    private bool interruptionSkipRequested;
    private int activeSpeechToken;
    private int speechQueueToken;
    private int speechInterruptionToken;
    private int pendingNormalSpeechCount;
    private string activeLineId = string.Empty;
    private string activeSpeakerId = string.Empty;
    private string activeSpeakerDisplayName = string.Empty;
    private string activeText = string.Empty;
    private bool activeShowSubtitleOverlay;
    private RoomNavigationManager navigationManager;
    private bool subscribedToRoomChanges;
    private readonly List<GuestMovementPauseLease> guestMovementPauseLeases = new List<GuestMovementPauseLease>();

    public bool IsNormalSpeechActive => normalSpeechActive;
    public bool IsSpeechInterruptionActive => speechInterruptionActive;

    public struct SpeechInterruption
    {
        public SpeechInterruption(
            bool hadActiveSpeech,
            bool hadQueuedSpeech,
            string lineId,
            string speakerId,
            string speakerDisplayName,
            string text)
        {
            HadActiveSpeech = hadActiveSpeech;
            HadQueuedSpeech = hadQueuedSpeech;
            LineId = lineId;
            SpeakerId = speakerId;
            SpeakerDisplayName = speakerDisplayName;
            Text = text;
        }

        public bool HadActiveSpeech { get; }
        public bool HadQueuedSpeech { get; }
        public bool HadAnySpeech => HadActiveSpeech || HadQueuedSpeech;
        public string LineId { get; }
        public string SpeakerId { get; }
        public string SpeakerDisplayName { get; }
        public string Text { get; }
    }

    public static DialogueSpeechService FindOrCreate()
    {
        DialogueSpeechService existing = FindAnyObjectByType<DialogueSpeechService>(FindObjectsInactive.Include);

        if (existing != null)
        {
            existing.ResolveReferences();
            return existing;
        }

        GameObject serviceObject = new GameObject(ServiceObjectName, typeof(DialogueSpeechService));
        DialogueSpeechService service = serviceObject.GetComponent<DialogueSpeechService>();
        service.ResolveReferences();
        return service;
    }

    public static void StopAnyCurrentSpeech()
    {
        DialogueSpeechService existing = FindAnyObjectByType<DialogueSpeechService>(FindObjectsInactive.Include);
        existing?.StopCurrentSpeech();
    }

    public Coroutine BeginSpeakLine(
        string lineId,
        string speakerId,
        string fallbackText,
        bool allowOverlap = false,
        bool blockInput = false,
        Action onComplete = null,
        bool showSubtitleOverlay = true,
        Action<string, string> onSpeechLineStarted = null)
    {
        ResolveReferences();
        return StartCoroutine(SpeakLineRoutine(lineId, speakerId, fallbackText, allowOverlap, blockInput, onComplete, showSubtitleOverlay, onSpeechLineStarted));
    }

    public IEnumerator SpeakLine(
        string lineId,
        string speakerId,
        string fallbackText,
        bool allowOverlap = false,
        bool blockInput = false,
        bool showSubtitleOverlay = true,
        Action<string, string> onSpeechLineStarted = null)
    {
        yield return SpeakLineRoutine(lineId, speakerId, fallbackText, allowOverlap, blockInput, null, showSubtitleOverlay, onSpeechLineStarted);
    }

    public void SkipCurrentSpeech()
    {
        if (speechInterruptionActive)
        {
            interruptionSkipRequested = true;
            voicePlayback?.StopOverlappingLines();
            return;
        }

        RequestSkip(activeSpeechToken);
    }

    public void StopCurrentSpeech()
    {
        CancelQueuedSpeech();
    }

    public SpeechInterruption CancelQueuedSpeech()
    {
        SpeechInterruption interruption = GetCurrentSpeech();
        activeSpeechToken++;
        speechQueueToken++;
        speechInterruptionToken++;
        ReleaseAllGuestMovementPauses();

        skipRequested = false;
        interruptionSkipRequested = false;
        speechInterruptionActive = false;
        activeSpeechPaused = false;
        normalSpeechActive = false;
        voicePlayback?.StopCurrentLine();
        subtitleService?.HideCurrent();
        speakingIndicator?.Hide();
        SpeakingCharacterIndicator.HideAnyCurrent();
        ClearActiveSpeechInfo();

        return interruption;
    }

    public SpeechInterruption GetCurrentSpeech()
    {
        bool hadActiveSpeech = normalSpeechActive || !string.IsNullOrWhiteSpace(activeLineId);
        bool hadQueuedSpeech = pendingNormalSpeechCount > (normalSpeechActive ? 1 : 0);
        return new SpeechInterruption(
            hadActiveSpeech,
            hadQueuedSpeech,
            activeLineId,
            activeSpeakerId,
            activeSpeakerDisplayName,
            activeText);
    }

    public bool InterruptCurrentSpeechAndResume(
        string interruptionLineId,
        string interruptionSpeakerId,
        string interruptionFallbackText,
        bool showSubtitleOverlay = true,
        Action<string, string> onInterruptionStarted = null)
    {
        ResolveReferences();

        if (speechInterruptionActive ||
            !normalSpeechActive ||
            string.IsNullOrWhiteSpace(activeText) ||
            subtitleService == null ||
            !subtitleService.TryResolveSpeechLine(
                interruptionLineId,
                interruptionSpeakerId,
                interruptionFallbackText,
                out string interruptionSpeaker,
                out string interruptionText,
                out float minDuration,
                out float maxDuration))
        {
            return false;
        }

        SpeechInterruption interruptedSpeech = GetCurrentSpeech();
        int originalSpeechToken = activeSpeechToken;
        int originalQueueToken = speechQueueToken;
        int interruptionToken = ++speechInterruptionToken;

        speechInterruptionActive = true;
        activeSpeechPaused = true;
        interruptionSkipRequested = false;
        voicePlayback?.PauseCurrentLine();
        subtitleService.HideCurrent();
        speakingIndicator?.HideForSpeechToken(originalSpeechToken);

        StartCoroutine(InterruptionSpeechRoutine(
            interruptionLineId,
            interruptionSpeaker,
            interruptionText,
            minDuration,
            maxDuration,
            showSubtitleOverlay,
            onInterruptionStarted,
            interruptedSpeech,
            originalSpeechToken,
            originalQueueToken,
            interruptionToken));
        return true;
    }

    private IEnumerator SpeakLineRoutine(
        string lineId,
        string speakerId,
        string fallbackText,
        bool allowOverlap,
        bool blockInput,
        Action onComplete,
        bool showSubtitleOverlay,
        Action<string, string> onSpeechLineStarted)
    {
        ResolveReferences();
        int queueToken = speechQueueToken;
        bool countedAsPendingNormalSpeech = !allowOverlap;
        GuestMovementPauseLease guestMovementPauseLease = null;

        if (countedAsPendingNormalSpeech)
        {
            pendingNormalSpeechCount++;
        }

        if (subtitleService == null)
        {
            CompleteSpeechRoutine(countedAsPendingNormalSpeech, onComplete, guestMovementPauseLease);
            yield break;
        }

        if (!allowOverlap && normalSpeechActive)
        {
            guestMovementPauseLease = TryAcquireGuestMovementPause(lineId, speakerId);
        }

        while (!allowOverlap && normalSpeechActive)
        {
            if (queueToken != speechQueueToken)
            {
                CompleteSpeechRoutine(countedAsPendingNormalSpeech, onComplete, guestMovementPauseLease);
                yield break;
            }

            yield return null;
        }

        if (!allowOverlap && queueToken != speechQueueToken)
        {
            CompleteSpeechRoutine(countedAsPendingNormalSpeech, onComplete, guestMovementPauseLease);
            yield break;
        }

        if (!subtitleService.TryResolveSpeechLine(
                lineId,
                speakerId,
                fallbackText,
                out string speaker,
                out string text,
                out float minDuration,
                out float maxDuration))
        {
            Debug.LogWarning($"[DialogueSpeech] Missing subtitle text for '{FormatLineId(lineId)}'.", this);
            CompleteSpeechRoutine(countedAsPendingNormalSpeech, onComplete, guestMovementPauseLease);
            yield break;
        }

        int speechToken = ++activeSpeechToken;
        skipRequested = false;

        if (!allowOverlap)
        {
            normalSpeechActive = true;
            SetActiveSpeechInfo(lineId, speakerId, speaker, text, showSubtitleOverlay);
        }

        PointClickPlayerMovement blockedMovement = null;
        bool previousInputEnabled = true;

        if (blockInput)
        {
            blockedMovement = FindAnyObjectByType<PointClickPlayerMovement>(FindObjectsInactive.Include);

            if (blockedMovement != null)
            {
                previousInputEnabled = blockedMovement.InputEnabled;
                blockedMovement.SetInputEnabled(false);
            }
        }

        onSpeechLineStarted?.Invoke(speaker, text);
        speakingIndicator?.ShowForSpeechLine(speechToken, lineId, speaker, text);

        if (showSubtitleOverlay)
        {
            subtitleService.ShowSpeechLine(lineId, speaker, text, true, () => RequestSkip(speechToken));
        }

        float voiceDuration = 0f;

        if (voicePlayback != null)
        {
            voiceDuration = voicePlayback.PlayForDialogue(lineId, speaker, text, allowOverlap);
        }

        if (voiceDuration <= 0f && logMissingVoiceLines && !string.IsNullOrWhiteSpace(lineId))
        {
            Debug.LogWarning($"[DialogueSpeech] Missing voice clip for '{lineId.Trim()}'. Using subtitle read-time fallback.", this);
        }

        float duration = voiceDuration > 0f
            ? voiceDuration + VoiceTailSeconds
            : GetReadDuration(text, minDuration, maxDuration);
        float elapsed = 0f;

        while (speechToken == activeSpeechToken && queueToken == speechQueueToken && elapsed < duration)
        {
            if (!allowOverlap && activeSpeechPaused)
            {
                yield return null;
                continue;
            }

            if (skipRequested)
            {
                break;
            }

            if (voiceDuration > 0f && !allowOverlap && voicePlayback != null && !voicePlayback.IsPlaying && elapsed > 0.05f)
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (speechToken == activeSpeechToken && queueToken == speechQueueToken)
        {
            if (skipRequested && !allowOverlap)
            {
                voicePlayback?.StopCurrentLine();
            }

            if (showSubtitleOverlay)
            {
                subtitleService.HideCurrent();
            }

            speakingIndicator?.HideForSpeechToken(speechToken);
        }

        if (blockedMovement != null)
        {
            blockedMovement.SetInputEnabled(previousInputEnabled);
        }

        if (!allowOverlap && speechToken == activeSpeechToken && queueToken == speechQueueToken)
        {
            normalSpeechActive = false;
            ClearActiveSpeechInfo();
        }

        if (speechToken == activeSpeechToken && queueToken == speechQueueToken)
        {
            skipRequested = false;
        }

        CompleteSpeechRoutine(countedAsPendingNormalSpeech, onComplete, guestMovementPauseLease);
    }

    private IEnumerator InterruptionSpeechRoutine(
        string lineId,
        string speaker,
        string text,
        float minDuration,
        float maxDuration,
        bool showSubtitleOverlay,
        Action<string, string> onInterruptionStarted,
        SpeechInterruption interruptedSpeech,
        int originalSpeechToken,
        int originalQueueToken,
        int interruptionToken)
    {
        int indicatorToken = unchecked(int.MinValue + interruptionToken);
        onInterruptionStarted?.Invoke(speaker, text);
        speakingIndicator?.ShowForSpeechLine(indicatorToken, lineId, speaker, text);

        if (showSubtitleOverlay)
        {
            subtitleService.ShowSpeechLine(
                lineId,
                speaker,
                text,
                true,
                () => RequestInterruptionSkip(interruptionToken));
        }

        float voiceDuration = voicePlayback != null
            ? voicePlayback.PlayForDialogue(lineId, speaker, text, true)
            : 0f;
        float duration = voiceDuration > 0f
            ? voiceDuration + VoiceTailSeconds
            : GetReadDuration(text, minDuration, maxDuration);
        float elapsed = 0f;

        while (interruptionToken == speechInterruptionToken &&
               originalSpeechToken == activeSpeechToken &&
               originalQueueToken == speechQueueToken &&
               elapsed < duration)
        {
            if (interruptionSkipRequested)
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (interruptionToken != speechInterruptionToken ||
            originalSpeechToken != activeSpeechToken ||
            originalQueueToken != speechQueueToken ||
            !normalSpeechActive)
        {
            if (interruptionToken == speechInterruptionToken)
            {
                CancelQueuedSpeech();
            }

            yield break;
        }

        voicePlayback?.StopOverlappingLines();

        if (showSubtitleOverlay)
        {
            subtitleService.HideCurrent();
        }

        speakingIndicator?.HideForSpeechToken(indicatorToken);
        speechInterruptionActive = false;
        interruptionSkipRequested = false;

        if (activeShowSubtitleOverlay)
        {
            subtitleService.ShowSpeechLine(
                interruptedSpeech.LineId,
                interruptedSpeech.SpeakerDisplayName,
                interruptedSpeech.Text,
                true,
                () => RequestSkip(originalSpeechToken));
        }

        speakingIndicator?.ShowForSpeechLine(
            originalSpeechToken,
            interruptedSpeech.LineId,
            interruptedSpeech.SpeakerDisplayName,
            interruptedSpeech.Text);
        voicePlayback?.ResumeCurrentLine();
        activeSpeechPaused = false;
    }

    private void Update()
    {
        if (normalSpeechActive && Input.GetKeyDown(KeyCode.Escape))
        {
            SkipCurrentSpeech();
        }
    }

    private void OnDisable()
    {
        UnregisterRoomChangeHandler();
        ReleaseAllGuestMovementPauses();
    }

    private void ResolveReferences()
    {
        if (subtitleService == null)
        {
            subtitleService = SubtitleService.FindOrCreate();
        }

        if (voicePlayback == null)
        {
            voicePlayback = GuestVoiceLinePlayback.FindOrCreate();
        }

        if (speakingIndicator == null)
        {
            speakingIndicator = SpeakingCharacterIndicator.FindOrCreate();
        }

        ResolveRoomNavigation();
        RegisterRoomChangeHandler();
    }

    private void ResolveRoomNavigation()
    {
        if (navigationManager == null)
        {
            navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
        }
    }

    private void RegisterRoomChangeHandler()
    {
        if (navigationManager == null)
        {
            return;
        }

        navigationManager.OnCurrentRoomChanged.RemoveListener(HandleCurrentRoomChanged);
        navigationManager.OnCurrentRoomChanged.AddListener(HandleCurrentRoomChanged);
        subscribedToRoomChanges = true;
    }

    private void UnregisterRoomChangeHandler()
    {
        if (!subscribedToRoomChanges || navigationManager == null)
        {
            return;
        }

        navigationManager.OnCurrentRoomChanged.RemoveListener(HandleCurrentRoomChanged);
        subscribedToRoomChanges = false;
    }

    private void HandleCurrentRoomChanged(string roomName)
    {
        CancelQueuedSpeech();
    }

    private void RequestSkip(int speechToken)
    {
        if (speechToken == activeSpeechToken)
        {
            skipRequested = true;
        }
    }

    private void RequestInterruptionSkip(int interruptionToken)
    {
        if (!speechInterruptionActive || interruptionToken != speechInterruptionToken)
        {
            return;
        }

        interruptionSkipRequested = true;
        voicePlayback?.StopOverlappingLines();
    }

    private void SetActiveSpeechInfo(
        string lineId,
        string speakerId,
        string speakerDisplayName,
        string text,
        bool showSubtitleOverlay)
    {
        activeLineId = string.IsNullOrWhiteSpace(lineId) ? string.Empty : lineId.Trim();
        activeSpeakerId = string.IsNullOrWhiteSpace(speakerId) ? string.Empty : speakerId.Trim();
        activeSpeakerDisplayName = string.IsNullOrWhiteSpace(speakerDisplayName) ? string.Empty : speakerDisplayName.Trim();
        activeText = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        activeShowSubtitleOverlay = showSubtitleOverlay;
    }

    private void ClearActiveSpeechInfo()
    {
        activeLineId = string.Empty;
        activeSpeakerId = string.Empty;
        activeSpeakerDisplayName = string.Empty;
        activeText = string.Empty;
        activeShowSubtitleOverlay = false;
    }

    private GuestMovementPauseLease TryAcquireGuestMovementPause(string lineId, string speakerId)
    {
        if (!SpeakingCharacterIndicator.TryResolveGuestSpeakerTarget(
                lineId,
                speakerId,
                out Transform target,
                out ActorRoomState actor))
        {
            return null;
        }

        NPCWaypointMover mover = ResolveGuestMover(target, actor);

        if (mover == null)
        {
            return null;
        }

        GuestMovementPauseLease lease = new GuestMovementPauseLease
        {
            Mover = mover
        };
        guestMovementPauseLeases.Add(lease);
        mover.AcquireSpeechPause();
        return lease;
    }

    private static NPCWaypointMover ResolveGuestMover(Transform target, ActorRoomState actor)
    {
        NPCWaypointMover mover = actor != null ? actor.GetComponent<NPCWaypointMover>() : null;

        if (mover == null && target != null)
        {
            mover = target.GetComponent<NPCWaypointMover>();
        }

        if (mover == null && target != null)
        {
            mover = target.GetComponentInChildren<NPCWaypointMover>(true);
        }

        if (mover == null && target != null)
        {
            mover = target.GetComponentInParent<NPCWaypointMover>(true);
        }

        return mover;
    }

    private void ReleaseGuestMovementPause(GuestMovementPauseLease lease)
    {
        if (lease == null || lease.Released)
        {
            return;
        }

        lease.Released = true;
        guestMovementPauseLeases.Remove(lease);

        if (lease.Mover != null)
        {
            lease.Mover.ReleaseSpeechPause();
        }
    }

    private void ReleaseAllGuestMovementPauses()
    {
        for (int i = guestMovementPauseLeases.Count - 1; i >= 0; i--)
        {
            GuestMovementPauseLease lease = guestMovementPauseLeases[i];

            if (lease == null || lease.Released)
            {
                continue;
            }

            lease.Released = true;

            if (lease.Mover != null)
            {
                lease.Mover.ReleaseSpeechPause();
            }
        }

        guestMovementPauseLeases.Clear();
    }

    private void CompleteSpeechRoutine(
        bool countedAsPendingNormalSpeech,
        Action onComplete,
        GuestMovementPauseLease guestMovementPauseLease)
    {
        ReleaseGuestMovementPause(guestMovementPauseLease);

        if (countedAsPendingNormalSpeech)
        {
            pendingNormalSpeechCount = Mathf.Max(0, pendingNormalSpeechCount - 1);
        }

        onComplete?.Invoke();
    }

    private static float GetReadDuration(string text, float minDuration, float maxDuration)
    {
        float low = Mathf.Max(0.1f, minDuration <= 0f ? MinimumReadSeconds : minDuration);
        float high = Mathf.Max(low, maxDuration <= 0f ? MaximumReadSeconds : maxDuration);
        float readableSeconds = MinimumReadSeconds + Mathf.Max(0f, (text ?? string.Empty).Length) / CharactersPerSecond;
        return Mathf.Clamp(readableSeconds, low, Mathf.Max(high, MaximumReadSeconds));
    }

    private static string FormatLineId(string lineId)
    {
        return string.IsNullOrWhiteSpace(lineId) ? "<inline>" : lineId.Trim();
    }
}
