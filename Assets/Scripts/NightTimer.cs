using System;
using TMPro;
using UnityEngine;

public class NightTimer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NightManager nightManager;
    [SerializeField] private TMP_Text textClockHUD;

    [Header("Time")]
    [SerializeField] private float hourLength = 60f;

    public event Action OnNightBegin;
    public event Action<int> OnHourChanged;
    public event Action OnNightEnd;

    private readonly string[] hourLabels =
    {
        "12:00 AM",
        "01:00 AM",
        "02:00 AM",
        "03:00 AM",
        "04:00 AM",
        "05:00 AM",
        "06:00 AM"
    };

    private int hour;
    private float time;
    private bool running;

    public int CurrentHour => hour;
    public bool IsRunning => running;

    public void Configure(NightManager manager, TMP_Text clockText)
    {
        if (manager != null)
        {
            nightManager = manager;
        }

        if (clockText != null)
        {
            textClockHUD = clockText;
        }

        UpdateHUD();
    }

    public void BeginNight()
    {
        ResolveReferences();
        hour = 0;
        time = 0f;
        running = true;
        SetClockVisible(true);
        UpdateHUD();
        OnNightBegin?.Invoke();
    }

    public void ResetTimer()
    {
        running = false;
        hour = 0;
        time = 0f;
        UpdateHUD();
        SetClockVisible(false);
    }

    public void StopNight(bool hideClock = true)
    {
        running = false;

        if (hideClock)
        {
            SetClockVisible(false);
        }
    }

    public void Reset()
    {
        ResetTimer();
    }

    private void Update()
    {
        if (!running)
        {
            return;
        }

        time += Time.deltaTime;

        while (running && time >= Mathf.Max(0.01f, hourLength))
        {
            time -= Mathf.Max(0.01f, hourLength);
            AdvanceHour();
        }
    }

    private void AdvanceHour()
    {
        hour++;
        UpdateHUD();
        OnHourChanged?.Invoke(hour);

        if (hour >= 6)
        {
            running = false;
            OnNightEnd?.Invoke();
        }
    }

    private void ResolveReferences()
    {
        if (nightManager == null)
        {
            nightManager = GetComponent<NightManager>();
        }

        if (nightManager == null)
        {
            nightManager = FindObjectOfType<NightManager>();
        }

        if (textClockHUD == null)
        {
            TMP_Text[] texts = FindObjectsOfType<TMP_Text>(true);

            foreach (TMP_Text text in texts)
            {
                if (text.name == "Text_ClockHUD")
                {
                    textClockHUD = text;
                    break;
                }
            }
        }
    }

    private void UpdateHUD()
    {
        if (textClockHUD == null)
        {
            return;
        }

        int labelIndex = Mathf.Clamp(hour, 0, hourLabels.Length - 1);
        textClockHUD.text = hourLabels[labelIndex];
    }

    private void SetClockVisible(bool visible)
    {
        if (textClockHUD != null)
        {
            textClockHUD.gameObject.SetActive(visible);
        }
    }
}
