using UnityEngine;

[DisallowMultipleComponent]
public class DoorbellSystem : MonoBehaviour, Chateau.Architecture.IArchitectureValidatable
{
    [Header("References")]
    [SerializeField] private ChapterClock chapterClock;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private GameAudioSourceVolume audioVolumeBinding;
    [SerializeField] private AudioClip doorbellClip;

    [Header("Escalation")]
    [SerializeField] private float normalIntervalSeconds = 6f;
    [SerializeField] private float urgentIntervalSeconds = 3.5f;
    [SerializeField] private float aggressiveIntervalSeconds = 1.8f;
    [SerializeField] private float normalVolume = 1f;
    [SerializeField] private float urgentVolume = 1f;
    [SerializeField] private float aggressiveVolume = 1f;

    private bool isRinging;
    private bool hasWaitingGuests;
    private bool emptyRing;
    private float oldestQueuedGameMinute;
    private float nextRingTime;
    private int currentIntensityLevel = -1;

    public bool IsRinging => isRinging;
    public int CurrentIntensityLevel => Mathf.Max(0, currentIntensityLevel);

    public bool IsConfiguredFor(GameObject expectedOwner, ChapterClock expectedClock)
    {
        return expectedOwner != null &&
            gameObject == expectedOwner &&
            chapterClock == expectedClock;
    }

    public void ValidateConfiguration(Chateau.Architecture.ValidationReport report)
    {
        if (report == null)
        {
            return;
        }

        if (chapterClock == null)
        {
            report.AddError("DoorbellSystem requires its serialized ChapterClock.", this);
        }

        if (audioSource == null)
        {
            report.AddError("DoorbellSystem requires its serialized AudioSource.", this);
        }

        if (audioVolumeBinding == null)
        {
            report.AddError("DoorbellSystem requires its serialized Game-Sounds volume binding.", this);
        }

        if (doorbellClip == null)
        {
            report.AddError("DoorbellSystem requires its serialized imported doorbell clip.", this);
        }

        if (audioSource != null &&
            (audioSource.gameObject != gameObject ||
             GetComponents<AudioSource>().Length != 1 ||
             audioSource.playOnAwake ||
             audioSource.loop ||
             audioSource.spatialBlend != 0f))
        {
            report.AddError("DoorbellSystem requires one same-owner, non-looping, play-on-awake-disabled 2D AudioSource.", this);
        }

        if (audioVolumeBinding != null &&
            (audioVolumeBinding.gameObject != gameObject ||
             GetComponents<GameAudioSourceVolume>().Length != 1 ||
             audioVolumeBinding.Channel != GameAudioChannel.GameSounds ||
             !Mathf.Approximately(audioVolumeBinding.BaseVolume, 1f)))
        {
            report.AddError("DoorbellSystem requires one same-owner Game-Sounds volume binding at base volume 1.", this);
        }
    }

    private void Awake()
    {
        ConfigureSerializedAudio();
    }

    private void Update()
    {
        if (!isRinging)
        {
            return;
        }

        UpdateIntensity();

        if (Time.time < nextRingTime)
        {
            return;
        }

        RingOnce();
    }

    public void Initialize(ChapterClock clock)
    {
        if (clock != null && clock != chapterClock)
        {
            Debug.LogError("DoorbellSystem rejected initialization from a different ChapterClock.", this);
            return;
        }

        ConfigureSerializedAudio();
    }

    public void StartRinging(float queuedAtGameMinute, bool waitingGuests, bool isEmptyRing)
    {
        oldestQueuedGameMinute = queuedAtGameMinute;
        hasWaitingGuests = waitingGuests;
        emptyRing = isEmptyRing;
        isRinging = true;
        nextRingTime = 0f;
        currentIntensityLevel = -1;
        UpdateIntensity();
        Debug.Log(hasWaitingGuests ? "Doorbell started: guests waiting outside." : "Doorbell started: no one visible outside.", this);
        RingOnce();
    }

    public void RefreshQueueState(float queuedAtGameMinute, bool waitingGuests, bool isEmptyRing)
    {
        oldestQueuedGameMinute = queuedAtGameMinute;
        hasWaitingGuests = waitingGuests;
        emptyRing = isEmptyRing;

        if (waitingGuests || isEmptyRing)
        {
            if (!isRinging)
            {
                StartRinging(queuedAtGameMinute, waitingGuests, isEmptyRing);
            }
        }
        else
        {
            StopRinging();
        }
    }

    public void StopRinging()
    {
        if (!isRinging)
        {
            return;
        }

        isRinging = false;
        hasWaitingGuests = false;
        emptyRing = false;
        currentIntensityLevel = -1;
        Debug.Log("Doorbell stopped.", this);
    }

    private void UpdateIntensity()
    {
        float waitedGameMinutes = 0f;

        if (hasWaitingGuests && chapterClock != null)
        {
            waitedGameMinutes = Mathf.Max(0f, chapterClock.ElapsedGameMinutes - oldestQueuedGameMinute);
        }

        int nextIntensity = waitedGameMinutes >= 2f ? 2 : waitedGameMinutes >= 1f ? 1 : 0;

        if (emptyRing && !hasWaitingGuests)
        {
            nextIntensity = 0;
        }

        if (currentIntensityLevel == nextIntensity)
        {
            return;
        }

        currentIntensityLevel = nextIntensity;
        Debug.Log($"Doorbell intensity changed: {currentIntensityLevel}", this);
    }

    private void RingOnce()
    {
        float interval = currentIntensityLevel >= 2
            ? aggressiveIntervalSeconds
            : currentIntensityLevel == 1 ? urgentIntervalSeconds : normalIntervalSeconds;

        float volume = currentIntensityLevel >= 2
            ? aggressiveVolume
            : currentIntensityLevel == 1 ? urgentVolume : normalVolume;

        if (audioSource != null && doorbellClip != null)
        {
            GameAudioSettings.TryPlayOneShot(audioSource, doorbellClip, volume);
        }

        Debug.Log(hasWaitingGuests ? $"Doorbell rings. Intensity {currentIntensityLevel}." : "Doorbell rings, but no new guests arrive.", this);
        nextRingTime = Time.time + Mathf.Max(0.25f, interval);
    }

    private void ConfigureSerializedAudio()
    {
        if (audioSource == null || audioVolumeBinding == null)
        {
            return;
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.mute = false;
        audioSource.enabled = true;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 1f;
        audioSource.ignoreListenerPause = true;
        audioVolumeBinding.Configure(audioSource, GameAudioChannel.GameSounds, 1f);
    }
}
