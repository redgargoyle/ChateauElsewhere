using System;
using Chateau.Architecture;
using UnityEngine;

[DisallowMultipleComponent]
public class ChapterClock : Chateau.Architecture.GameServiceBase, Chateau.Architecture.IClockService
{
    public override int InitializationOrder => Chateau.Architecture.GameServiceInitializationOrder.Clock;

    [Header("Game Time")]
    [SerializeField, Range(1f, 300f)] private float secondsPerGameMinute = 5f;
    [SerializeField, Range(0, 23)] private int startHour = 17;
    [SerializeField, Range(0, 59)] private int startMinute = 59;
    [SerializeField] private bool logClockStateChanges;

    private float elapsedSeconds;
    private bool isRunning;

    public event Action TimeAdvanced;

    public float ElapsedSeconds => elapsedSeconds;
    public bool IsRunning => isRunning;
    public float SecondsPerGameMinute => secondsPerGameMinute;
    public float ElapsedGameMinutes => secondsPerGameMinute > 0f ? elapsedSeconds / secondsPerGameMinute : 0f;
    public int StartTotalMinutes => startHour * 60 + startMinute;
    public int CurrentTotalMinutes => StartTotalMinutes + Mathf.FloorToInt(ElapsedGameMinutes + 0.0001f);
    public int CurrentHour => Mathf.FloorToInt(Mathf.Repeat(CurrentTotalMinutes, 1440f) / 60f);
    public int CurrentMinute => Mathf.FloorToInt(Mathf.Repeat(CurrentTotalMinutes, 1440f) % 60f);
    public string CurrentTimeLabel => FormatTime(CurrentHour, CurrentMinute);

    private void Update()
    {
        if (!IsInitialized)
        {
            return;
        }

        Advance(Time.deltaTime);
    }

    private void OnValidate()
    {
        secondsPerGameMinute = SanitizeSecondsPerGameMinute(secondsPerGameMinute);
        startHour = Mathf.Clamp(startHour, 0, 23);
        startMinute = Mathf.Clamp(startMinute, 0, 59);
    }

    public void ResetClock()
    {
        elapsedSeconds = 0f;
    }

    public void SetStartTime(int hour, int minute)
    {
        startHour = Mathf.Clamp(hour, 0, 23);
        startMinute = Mathf.Clamp(minute, 0, 59);
        ResetClock();
    }

    public void SetSecondsPerGameMinute(float value)
    {
        float elapsedGameMinutes = ElapsedGameMinutes;
        secondsPerGameMinute = SanitizeSecondsPerGameMinute(value);
        elapsedSeconds = elapsedGameMinutes * secondsPerGameMinute;
    }

    public GameClockState CaptureState()
    {
        return new GameClockState(
            elapsedSeconds,
            isRunning,
            secondsPerGameMinute,
            startHour,
            startMinute);
    }

    public void RestoreState(GameClockState state)
    {
        elapsedSeconds = SanitizeElapsedSeconds(state.ElapsedSeconds);
        isRunning = state.IsRunning;
        secondsPerGameMinute = SanitizeSecondsPerGameMinute(state.SecondsPerGameMinute);
        startHour = Mathf.Clamp(state.StartHour, 0, 23);
        startMinute = Mathf.Clamp(state.StartMinute, 0, 59);
    }

    public bool HasReachedTime(int hour, int minute)
    {
        return CurrentTotalMinutes >= ToTotalMinutes(hour, minute);
    }

    public float GetGameMinutesUntil(int hour, int minute)
    {
        return Mathf.Max(0f, ToTotalMinutes(hour, minute) - (StartTotalMinutes + ElapsedGameMinutes));
    }

    public void StartClock()
    {
        isRunning = true;

        if (logClockStateChanges)
        {
            Debug.Log("Chapter clock started.", this);
        }
    }

    public void StopClock()
    {
        isRunning = false;

        if (logClockStateChanges)
        {
            Debug.Log("Chapter clock stopped.", this);
        }
    }

    public override void ValidateConfiguration(Chateau.Architecture.ValidationReport report)
    {
        base.ValidateConfiguration(report);

        if (secondsPerGameMinute < 1f)
        {
            report.AddError("ChapterClock requires at least one real second per game minute.", this);
        }

        if (startHour < 0 || startHour > 23 || startMinute < 0 || startMinute > 59)
        {
            report.AddError("ChapterClock requires a valid serialized 24-hour start time.", this);
        }
    }

    protected override void OnShutdown(Chateau.Architecture.GameContext context)
    {
        StopClock();
        TimeAdvanced = null;
    }

    private void Advance(float deltaSeconds)
    {
        if (!isRunning)
        {
            return;
        }

        float cleanDeltaSeconds = SanitizeElapsedSeconds(deltaSeconds);

        if (cleanDeltaSeconds <= 0f)
        {
            return;
        }

        elapsedSeconds += cleanDeltaSeconds;
        TimeAdvanced?.Invoke();
    }

    private static float SanitizeElapsedSeconds(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value)
            ? 0f
            : Mathf.Max(0f, value);
    }

    private static float SanitizeSecondsPerGameMinute(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value)
            ? 1f
            : Mathf.Max(1f, value);
    }

    public static int ToTotalMinutes(int hour, int minute)
    {
        return Mathf.Clamp(hour, 0, 23) * 60 + Mathf.Clamp(minute, 0, 59);
    }

    public static string FormatTime(int hour, int minute)
    {
        int cleanHour = Mathf.Clamp(hour, 0, 23);
        int cleanMinute = Mathf.Clamp(minute, 0, 59);
        int displayHour = cleanHour % 12;

        if (displayHour == 0)
        {
            displayHour = 12;
        }

        string suffix = cleanHour < 12 ? "AM" : "PM";
        return $"{displayHour}:{cleanMinute:00} {suffix}";
    }
}
