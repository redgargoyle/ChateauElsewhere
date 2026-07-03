using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DialogueSpeechService : MonoBehaviour
{
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
    private int activeSpeechToken;

    public bool IsNormalSpeechActive => normalSpeechActive;

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
        RequestSkip(activeSpeechToken);
    }

    public void StopCurrentSpeech()
    {
        activeSpeechToken++;
        skipRequested = false;
        normalSpeechActive = false;
        voicePlayback?.StopCurrentLine();
        subtitleService?.HideCurrent();
        speakingIndicator?.Hide();
        SpeakingCharacterIndicator.HideAnyCurrent();
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

        if (subtitleService == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        while (!allowOverlap && normalSpeechActive)
        {
            yield return null;
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
            onComplete?.Invoke();
            yield break;
        }

        int speechToken = ++activeSpeechToken;
        skipRequested = false;

        if (!allowOverlap)
        {
            normalSpeechActive = true;
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

        while (speechToken == activeSpeechToken && elapsed < duration)
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

        if (speechToken == activeSpeechToken)
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

        if (!allowOverlap && speechToken == activeSpeechToken)
        {
            normalSpeechActive = false;
        }

        if (speechToken == activeSpeechToken)
        {
            skipRequested = false;
        }

        onComplete?.Invoke();
    }

    private void Update()
    {
        if (normalSpeechActive && Input.GetKeyDown(KeyCode.Escape))
        {
            RequestSkip(activeSpeechToken);
        }
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
    }

    private void RequestSkip(int speechToken)
    {
        if (speechToken == activeSpeechToken)
        {
            skipRequested = true;
        }
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
