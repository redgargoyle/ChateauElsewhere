using UnityEngine;

[DisallowMultipleComponent]
public class DoorbellSystem : MonoBehaviour
{
    private const string DefaultDoorbellClipResourcePath = "Audio/SFX/old_fashioned_door_bell_youtube_IqFKjVlaOik_48khz";

    [Header("References")]
    [SerializeField] private ChapterClock chapterClock;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip doorbellClip;
    [SerializeField] private string doorbellClipResourcePath = DefaultDoorbellClipResourcePath;

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

    private void Awake()
    {
        ResolveReferences();
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
        chapterClock = clock != null ? clock : chapterClock;
        ResolveReferences();
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
        ResolveReferences();

        float interval = currentIntensityLevel >= 2
            ? aggressiveIntervalSeconds
            : currentIntensityLevel == 1 ? urgentIntervalSeconds : normalIntervalSeconds;

        float volume = currentIntensityLevel >= 2
            ? aggressiveVolume
            : currentIntensityLevel == 1 ? urgentVolume : normalVolume;

        if (audioSource != null)
        {
            if (doorbellClip == null)
            {
                doorbellClip = ResolveDoorbellClip();
            }

            GameAudioSettings.TryPlayOneShot(audioSource, doorbellClip, volume);
        }

        Debug.Log(hasWaitingGuests ? $"Doorbell rings. Intensity {currentIntensityLevel}." : "Doorbell rings, but no new guests arrive.", this);
        nextRingTime = Time.time + Mathf.Max(0.25f, interval);
    }

    private void ResolveReferences()
    {
        if (chapterClock == null)
        {
            chapterClock = FindAnyObjectByType<ChapterClock>(FindObjectsInactive.Include);
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.mute = false;
        audioSource.enabled = true;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 1f;
        audioSource.ignoreListenerPause = true;
        GameAudioSettings.EnsureBinding(audioSource, GameAudioChannel.GameSounds, 1f);
    }

    private AudioClip ResolveDoorbellClip()
    {
        if (doorbellClip != null)
        {
            return doorbellClip;
        }

        if (!string.IsNullOrWhiteSpace(doorbellClipResourcePath))
        {
            doorbellClip = Resources.Load<AudioClip>(doorbellClipResourcePath);
        }

        if (doorbellClip == null)
        {
            doorbellClip = CreateDoorbellClip();
        }

        return doorbellClip;
    }

    private static AudioClip CreateDoorbellClip()
    {
        const int sampleRate = 44100;
        int samples = Mathf.RoundToInt(sampleRate * 1.1f);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float sample = 0f;

            if (t < 0.42f)
            {
                float envelope = Mathf.Exp(-4.8f * t);
                float toneA = Mathf.Sin(2f * Mathf.PI * 880f * t);
                float toneB = Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.45f;
                sample = (toneA + toneB) * 0.55f * envelope;
            }
            else if (t > 0.52f)
            {
                float localTime = t - 0.52f;
                float envelope = Mathf.Exp(-4.3f * localTime);
                float toneA = Mathf.Sin(2f * Mathf.PI * 660f * localTime);
                float toneB = Mathf.Sin(2f * Mathf.PI * 990f * localTime) * 0.45f;
                sample = (toneA + toneB) * 0.58f * envelope;
            }

            data[i] = Mathf.Clamp(sample, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("RuntimeDoorbell", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
