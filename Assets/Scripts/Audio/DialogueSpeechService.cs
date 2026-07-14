using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DialogueSpeechService : Chateau.Architecture.GameServiceBase, Chateau.Architecture.IDialogueService
{
    public override int InitializationOrder => Chateau.Architecture.GameServiceInitializationOrder.Dialogue;

    private const float CharactersPerSecond = 24f;
    private const float MinimumReadSeconds = 1.25f;
    private const float MaximumReadSeconds = 6f;
    private const float VoiceTailSeconds = 0.1f;

    [SerializeField] private SubtitleService subtitleService;
    [SerializeField] private GuestVoiceLinePlayback voicePlayback;
    [SerializeField] private SpeakingCharacterIndicator speakingIndicator;
    [SerializeField] private PointClickPlayerMovement playerMovement;
    [SerializeField] private bool logMissingVoiceLines;

    private bool normalSpeechActive;
    private bool skipRequested;
    private int activeSpeechToken;
    private int speechQueueToken;
    private int pendingNormalSpeechCount;
    private string activeLineId = string.Empty;
    private string activeSpeakerId = string.Empty;
    private string activeSpeakerDisplayName = string.Empty;
    private string activeText = string.Empty;
    private PointClickPlayerMovement blockedInputOwner;
    private bool blockedInputPreviousState;
    private int blockedInputSpeechToken;
    private bool ownsBlockedInput;

    public bool IsNormalSpeechActive => normalSpeechActive;

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
        RequestSkip(activeSpeechToken);
    }

    public void StopCurrentSpeech()
    {
        CancelQueuedSpeech();
    }

    public SpeechInterruption CancelQueuedSpeech()
    {
        activeSpeechToken++;
        speechQueueToken++;
        ReleaseBlockedPlayerInput();

        bool hadActiveSpeech = normalSpeechActive || !string.IsNullOrWhiteSpace(activeLineId);
        bool hadQueuedSpeech = pendingNormalSpeechCount > (normalSpeechActive ? 1 : 0);
        SpeechInterruption interruption = new SpeechInterruption(
            hadActiveSpeech,
            hadQueuedSpeech,
            activeLineId,
            activeSpeakerId,
            activeSpeakerDisplayName,
            activeText);

        skipRequested = false;
        normalSpeechActive = false;
        voicePlayback?.StopCurrentLine();
        subtitleService?.HideCurrent();
        speakingIndicator?.Hide();
        ClearActiveSpeechInfo();

        return interruption;
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
        int queueToken = speechQueueToken;
        bool countedAsPendingNormalSpeech = !allowOverlap;

        if (countedAsPendingNormalSpeech)
        {
            pendingNormalSpeechCount++;
        }

        if (subtitleService == null)
        {
            CompleteSpeechRoutine(countedAsPendingNormalSpeech, onComplete);
            yield break;
        }

        while (!allowOverlap && normalSpeechActive)
        {
            if (queueToken != speechQueueToken)
            {
                CompleteSpeechRoutine(countedAsPendingNormalSpeech, onComplete);
                yield break;
            }

            yield return null;
        }

        if (!allowOverlap && queueToken != speechQueueToken)
        {
            CompleteSpeechRoutine(countedAsPendingNormalSpeech, onComplete);
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
            CompleteSpeechRoutine(countedAsPendingNormalSpeech, onComplete);
            yield break;
        }

        int speechToken = ++activeSpeechToken;
        skipRequested = false;

        if (!allowOverlap)
        {
            normalSpeechActive = true;
            SetActiveSpeechInfo(lineId, speakerId, speaker, text);
        }

        if (blockInput)
        {
            AcquireBlockedPlayerInput(speechToken);
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

        ReleaseBlockedPlayerInput(speechToken);

        if (!allowOverlap && speechToken == activeSpeechToken && queueToken == speechQueueToken)
        {
            normalSpeechActive = false;
            ClearActiveSpeechInfo();
        }

        if (speechToken == activeSpeechToken && queueToken == speechQueueToken)
        {
            skipRequested = false;
        }

        CompleteSpeechRoutine(countedAsPendingNormalSpeech, onComplete);
    }

    private void Update()
    {
        if (normalSpeechActive && Input.GetKeyDown(KeyCode.Escape))
        {
            RequestSkip(activeSpeechToken);
        }
    }

    private void OnDisable()
    {
        ReleaseBlockedPlayerInput();
    }

    protected override void OnShutdown(Chateau.Architecture.GameContext context)
    {
        ReleaseBlockedPlayerInput();
        base.OnShutdown(context);
    }

    public override void ValidateConfiguration(Chateau.Architecture.ValidationReport report)
    {
        base.ValidateConfiguration(report);

        if (subtitleService == null)
        {
            report.AddError("DialogueSpeechService requires its serialized SubtitleService.", this);
        }

        if (voicePlayback == null)
        {
            report.AddError("DialogueSpeechService requires its serialized GuestVoiceLinePlayback.", this);
        }
        else
        {
            voicePlayback.ValidateConfiguration(report);
        }

        if (speakingIndicator == null)
        {
            report.AddError("DialogueSpeechService requires its serialized SpeakingCharacterIndicator.", this);
        }

        if (playerMovement == null)
        {
            report.AddError("DialogueSpeechService requires its serialized Butler movement owner.", this);
        }
    }

    private void RequestSkip(int speechToken)
    {
        if (speechToken == activeSpeechToken)
        {
            skipRequested = true;
        }
    }

    private void AcquireBlockedPlayerInput(int speechToken)
    {
        if (playerMovement == null)
        {
            return;
        }

        if (!ownsBlockedInput)
        {
            blockedInputOwner = playerMovement;
            blockedInputPreviousState = playerMovement.InputEnabled;
            ownsBlockedInput = true;
        }

        blockedInputSpeechToken = speechToken;
        blockedInputOwner.SetInputEnabled(false);
    }

    private void ReleaseBlockedPlayerInput(int speechToken)
    {
        if (!ownsBlockedInput || blockedInputSpeechToken != speechToken)
        {
            return;
        }

        ReleaseBlockedPlayerInput();
    }

    private void ReleaseBlockedPlayerInput()
    {
        if (!ownsBlockedInput)
        {
            return;
        }

        PointClickPlayerMovement owner = blockedInputOwner;
        bool restoreInputEnabled = blockedInputPreviousState;
        blockedInputOwner = null;
        blockedInputPreviousState = true;
        blockedInputSpeechToken = 0;
        ownsBlockedInput = false;

        if (owner != null)
        {
            owner.SetInputEnabled(restoreInputEnabled);
        }
    }

    private void SetActiveSpeechInfo(string lineId, string speakerId, string speakerDisplayName, string text)
    {
        activeLineId = string.IsNullOrWhiteSpace(lineId) ? string.Empty : lineId.Trim();
        activeSpeakerId = string.IsNullOrWhiteSpace(speakerId) ? string.Empty : speakerId.Trim();
        activeSpeakerDisplayName = string.IsNullOrWhiteSpace(speakerDisplayName) ? string.Empty : speakerDisplayName.Trim();
        activeText = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
    }

    private void ClearActiveSpeechInfo()
    {
        activeLineId = string.Empty;
        activeSpeakerId = string.Empty;
        activeSpeakerDisplayName = string.Empty;
        activeText = string.Empty;
    }

    private void CompleteSpeechRoutine(bool countedAsPendingNormalSpeech, Action onComplete)
    {
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
