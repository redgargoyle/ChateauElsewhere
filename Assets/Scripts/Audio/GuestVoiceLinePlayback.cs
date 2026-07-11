using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GuestVoiceLinePlayback : MonoBehaviour
{
    private const string PlayerObjectName = "GuestVoiceLinePlayback";
    private const string DefaultCatalogResourcePath = "Audio/GuestVoiceLineCatalog";

    [SerializeField] private GuestVoiceLineCatalog catalog;
    [SerializeField] private string catalogResourcePath = DefaultCatalogResourcePath;
    [SerializeField, Range(0f, 1f)] private float baseVolume = 1f;
    [SerializeField] private bool logMissingVoiceLines;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private RoomNavigationManager navigationManager;
    private bool subscribedToRoomChanges;
    private string playbackRoom = string.Empty;

    public bool IsPlaying => audioSource != null && audioSource.isPlaying;

    public float PlayForDialogue(string lineId, string speaker, string text)
    {
        return PlayForDialogue(lineId, speaker, text, false);
    }

    public float PlayForDialogue(string lineId, string speaker, string text, bool allowOverlap)
    {
        if (!Application.isPlaying)
        {
            return 0f;
        }

        if (!TryResolveClipForDialogue(lineId, speaker, text, out AudioClip clip, out float lineVolume))
        {
            return 0f;
        }

        if (allowOverlap)
        {
            return PlayOverlappingClip(clip, lineVolume);
        }

        if (!EnsureAudioSource())
        {
            return 0f;
        }

        if (clip.loadState == AudioDataLoadState.Unloaded)
        {
            clip.LoadAudioData();
        }

        float sourceBaseVolume = Mathf.Clamp01(baseVolume) * Mathf.Clamp01(lineVolume);
        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.loop = false;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerVolume = true;
        GameAudioSettings.EnsureBinding(audioSource, GameAudioChannel.Dialogue, sourceBaseVolume);
        playbackRoom = navigationManager != null ? navigationManager.CurrentRoom : string.Empty;
        return GameAudioSettings.TryPlay(audioSource) ? clip.length : 0f;
    }

    public bool TryGetDialogueClip(string lineId, string speaker, string text, out AudioClip clip, out float lineVolume)
    {
        return TryResolveClipForDialogue(lineId, speaker, text, out clip, out lineVolume);
    }

    public float GetDurationForDialogue(string lineId, string speaker, string text)
    {
        if (!Application.isPlaying)
        {
            return 0f;
        }

        return TryResolveClipForDialogue(lineId, speaker, text, out AudioClip clip, out _)
            ? clip.length
            : 0f;
    }

    public void StopCurrentLine()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }

        playbackRoom = string.Empty;
    }

    public static void StopAnyCurrentLine()
    {
        GuestVoiceLinePlayback existing = FindAnyObjectByType<GuestVoiceLinePlayback>(FindObjectsInactive.Include);
        existing?.StopCurrentLine();
    }

    private void ResolveReferences()
    {
        if (catalog == null)
        {
            string resourcePath = string.IsNullOrWhiteSpace(catalogResourcePath)
                ? DefaultCatalogResourcePath
                : catalogResourcePath.Trim();
            catalog = Resources.Load<GuestVoiceLineCatalog>(resourcePath);
        }

        EnsureAudioSource();
        ResolveRoomNavigation();
        RegisterRoomChangeHandler();
    }

    private bool TryResolveClipForDialogue(string lineId, string speaker, string text, out AudioClip clip, out float lineVolume)
    {
        clip = null;
        lineVolume = 1f;

        ResolveReferences();

        if (catalog == null ||
            !TryResolveAudioLineId(lineId, speaker, text, out string audioLineId))
        {
            return false;
        }

        if (!catalog.TryGetVoiceLine(audioLineId, out clip, out lineVolume) || clip == null)
        {
            if (logMissingVoiceLines)
            {
                Debug.LogWarning($"[VoiceLine] Missing voice clip for '{audioLineId}'.", this);
            }

            return false;
        }

        return true;
    }

    private float PlayOverlappingClip(AudioClip clip, float lineVolume)
    {
        if (clip == null)
        {
            return 0f;
        }

        GameObject overlapObject = new GameObject($"{PlayerObjectName}_Overlap");
        AudioSource overlapSource = overlapObject.AddComponent<AudioSource>();
        float sourceBaseVolume = Mathf.Clamp01(baseVolume) * Mathf.Clamp01(lineVolume);
        overlapSource.clip = clip;
        overlapSource.loop = false;
        overlapSource.playOnAwake = false;
        overlapSource.spatialBlend = 0f;
        overlapSource.ignoreListenerVolume = true;
        GameAudioSettings.EnsureBinding(overlapSource, GameAudioChannel.Dialogue, sourceBaseVolume);
        if (!GameAudioSettings.TryPlay(overlapSource))
        {
            Destroy(overlapObject);
            return 0f;
        }

        Destroy(overlapObject, clip.length + 0.25f);
        return clip.length;
    }

    private void OnDisable()
    {
        UnregisterRoomChangeHandler();
    }

    private bool EnsureAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        return audioSource != null;
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
        if (audioSource == null || !audioSource.isPlaying)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(playbackRoom) ||
            !string.Equals(playbackRoom.Trim(), (roomName ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
        {
            StopCurrentLine();
        }
    }

    private static bool TryResolveAudioLineId(string lineId, string speaker, string text, out string audioLineId)
    {
        audioLineId = null;

        string cleanLineId = string.IsNullOrWhiteSpace(lineId) ? string.Empty : lineId.Trim();

        if (IsAudioLineId(cleanLineId))
        {
            audioLineId = cleanLineId;
            return true;
        }

        if (TryResolveSubtitleLineId(cleanLineId, text, out audioLineId))
        {
            return true;
        }

        if (TryResolveKnownInlineGuestLine(speaker, text, out audioLineId))
        {
            return true;
        }

        return false;
    }

    private static bool TryResolveSubtitleLineId(string lineId, string text, out string audioLineId)
    {
        audioLineId = null;

        if (TryExtractGuestNumber(lineId, "SUB_CH01_G", out int chapter1GuestNumber, out string chapter1Suffix))
        {
            string guestToken = $"G{chapter1GuestNumber:00}";

            if (chapter1Suffix.IndexOf("_GREETING_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                audioLineId = $"CH1_{guestToken}_ENTRY";
                return true;
            }

            if (chapter1Suffix.IndexOf("_ANNOYED_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                audioLineId = $"CH1_{guestToken}_DELAYED";
                return true;
            }

            if (chapter1Suffix.IndexOf("_AMBIENT_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                audioLineId = TryResolveChapter1AmbientLine(chapter1GuestNumber, text, out string ambientLineId)
                    ? ambientLineId
                    : $"CH1_{guestToken}_AMBIENT_01";
                return true;
            }
        }

        if (TryExtractGuestNumber(lineId, "SUB_CH02_G", out int chapter2GuestNumber, out string chapter2Suffix) &&
            chapter2Suffix.IndexOf("_FINAL_ACK_", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            audioLineId = $"CH2_G{chapter2GuestNumber:00}_EXIT_TO_DINING";
            return true;
        }

        return false;
    }

    private static bool TryResolveChapter1AmbientLine(int guestNumber, string text, out string audioLineId)
    {
        audioLineId = null;
        string normalizedText = NormalizeDialogueText(text);

        if (string.IsNullOrEmpty(normalizedText))
        {
            return false;
        }

        switch (guestNumber)
        {
            case 1:
                return TryResolveAmbientPair(normalizedText, "this house is colder than i expected.", "the fire looks arranged rather than lit.", "CH1_G01", out audioLineId);
            case 2:
                return TryResolveAmbientPair(normalizedText, "the host is late, isn't he?", "i keep thinking someone is standing just behind the curtains.", "CH1_G02", out audioLineId);
            case 3:
                return TryResolveAmbientPair(normalizedText, "did you hear something upstairs?", "if the house is settling, it is doing so with theatrical timing.", "CH1_G03", out audioLineId);
            case 4:
                return TryResolveAmbientPair(normalizedText, "the drawing room should be warmer.", "old houses groan. this one seems to choose its words.", "CH1_G04", out audioLineId);
            case 5:
                return TryResolveAmbientPair(normalizedText, "this house is colder than i expected.", "the portraits look recently offended.", "CH1_G05", out audioLineId);
            case 6:
                return TryResolveAmbientPair(normalizedText, "the host is late, isn't he?", "i dislike a clock that seems to be waiting for me personally.", "CH1_G06", out audioLineId);
            case 7:
                return TryResolveAmbientPair(normalizedText, "did you hear something upstairs?", "the ceiling has footsteps in it, and not all of them are human.", "CH1_G07", out audioLineId);
            case 8:
                return TryResolveAmbientPair(normalizedText, "the drawing room should be warmer.", "there is a draft here that does not come from any door.", "CH1_G08", out audioLineId);
            default:
                return false;
        }
    }

    private static bool TryResolveAmbientPair(
        string normalizedText,
        string firstAmbient,
        string secondAmbient,
        string linePrefix,
        out string audioLineId)
    {
        audioLineId = null;

        if (string.Equals(normalizedText, firstAmbient, StringComparison.OrdinalIgnoreCase))
        {
            audioLineId = $"{linePrefix}_AMBIENT_01";
            return true;
        }

        if (string.Equals(normalizedText, secondAmbient, StringComparison.OrdinalIgnoreCase))
        {
            audioLineId = $"{linePrefix}_AMBIENT_02";
            return true;
        }

        return false;
    }

    private static bool TryResolveKnownInlineGuestLine(string speaker, string text, out string audioLineId)
    {
        audioLineId = null;

        if (!TryExtractGuestNumberFromSpeaker(speaker, out int guestNumber))
        {
            return false;
        }

        string normalizedText = NormalizeDialogueText(text);

        if (string.IsNullOrEmpty(normalizedText))
        {
            return false;
        }

        if (string.Equals(normalizedText, "very good. i shall present myself in the dining room and recover what dignity remains to us.", StringComparison.OrdinalIgnoreCase))
        {
            audioLineId = $"CH2_G{guestNumber:00}_EXIT_TO_DINING";
            return true;
        }

        return false;
    }

    private static bool TryExtractGuestNumberFromSpeaker(string speaker, out int guestNumber)
    {
        guestNumber = 0;

        if (string.IsNullOrWhiteSpace(speaker))
        {
            return false;
        }

        string cleanSpeaker = speaker.Trim();

        for (int i = 0; i < cleanSpeaker.Length; i++)
        {
            if (!char.IsDigit(cleanSpeaker[i]))
            {
                continue;
            }

            int start = i;

            while (i < cleanSpeaker.Length && char.IsDigit(cleanSpeaker[i]))
            {
                i++;
            }

            string numberText = cleanSpeaker.Substring(start, i - start);
            return int.TryParse(numberText, out guestNumber) && guestNumber > 0;
        }

        return false;
    }

    private static bool TryExtractGuestNumber(string value, string prefix, out int guestNumber, out string suffix)
    {
        guestNumber = 0;
        suffix = string.Empty;

        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int index = prefix.Length;
        int numberStart = index;

        while (index < value.Length && char.IsDigit(value[index]))
        {
            index++;
        }

        if (index == numberStart ||
            !int.TryParse(value.Substring(numberStart, index - numberStart), out guestNumber) ||
            guestNumber <= 0)
        {
            return false;
        }

        suffix = index < value.Length ? value.Substring(index) : string.Empty;
        return true;
    }

    private static bool IsAudioLineId(string lineId)
    {
        return !string.IsNullOrWhiteSpace(lineId) &&
            (lineId.StartsWith("CH1_G", StringComparison.OrdinalIgnoreCase) ||
             lineId.StartsWith("CH2_G", StringComparison.OrdinalIgnoreCase) ||
             lineId.StartsWith("SUB_CH01_BUTLER_", StringComparison.OrdinalIgnoreCase) ||
             lineId.StartsWith("SUB_CH02_BUTLER_", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeDialogueText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return string.Join(" ", text.Trim().ToLowerInvariant().Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
    }
}
