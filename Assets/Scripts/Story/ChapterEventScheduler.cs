using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ChapterEventScheduler : Chateau.Architecture.GameServiceBase
{
    private sealed class ScheduledChapterEvent
    {
        public string EventId;
        public float FireAtElapsedSeconds;
        public int FireAtGameTotalMinutes;
        public bool UsesGameTime;
        public Action Callback;
        public bool Fired;
        public bool Completed;
    }

    [SerializeField] private ChapterClock chapterClock;

    private readonly List<ScheduledChapterEvent> scheduledEvents = new List<ScheduledChapterEvent>();

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (chapterClock == null || !chapterClock.IsRunning)
        {
            return;
        }

        float elapsedSeconds = chapterClock.ElapsedSeconds;

        for (int i = 0; i < scheduledEvents.Count; i++)
        {
            ScheduledChapterEvent scheduledEvent = scheduledEvents[i];

            if (scheduledEvent == null || scheduledEvent.Fired)
            {
                continue;
            }

            bool shouldFire = scheduledEvent.UsesGameTime
                ? chapterClock.CurrentTotalMinutes >= scheduledEvent.FireAtGameTotalMinutes
                : elapsedSeconds >= scheduledEvent.FireAtElapsedSeconds;

            if (!shouldFire)
            {
                continue;
            }

            FireEvent(scheduledEvent);
        }
    }

    public bool ScheduleOneShot(string eventId, float delaySeconds, Action callback)
    {
        if (!EnsureClockReference())
        {
            return false;
        }

        string cleanEventId = string.IsNullOrWhiteSpace(eventId) ? "chapter_event" : eventId.Trim();

        if (HasPendingOrFiredEvent(cleanEventId))
        {
            Debug.LogWarning($"Chapter event '{cleanEventId}' was already scheduled or fired and will not be scheduled twice.", this);
            return false;
        }

        float elapsedSeconds = chapterClock != null ? chapterClock.ElapsedSeconds : 0f;
        ScheduledChapterEvent scheduledEvent = new ScheduledChapterEvent
        {
            EventId = cleanEventId,
            FireAtElapsedSeconds = elapsedSeconds + Mathf.Max(0f, delaySeconds),
            UsesGameTime = false,
            Callback = callback,
            Fired = false,
            Completed = false
        };

        scheduledEvents.Add(scheduledEvent);
        Debug.Log($"Chapter event scheduled: {cleanEventId} at {scheduledEvent.FireAtElapsedSeconds:0.00}s", this);
        return true;
    }

    public bool ScheduleOneShotAtClockTime(string eventId, int hour, int minute, Action callback)
    {
        if (!EnsureClockReference())
        {
            return false;
        }

        string cleanEventId = string.IsNullOrWhiteSpace(eventId) ? "chapter_clock_event" : eventId.Trim();

        if (HasPendingOrFiredEvent(cleanEventId))
        {
            Debug.LogWarning($"Chapter event '{cleanEventId}' was already scheduled or fired and will not be scheduled twice.", this);
            return false;
        }

        int targetTotalMinutes = ChapterClock.ToTotalMinutes(hour, minute);
        ScheduledChapterEvent scheduledEvent = new ScheduledChapterEvent
        {
            EventId = cleanEventId,
            FireAtGameTotalMinutes = targetTotalMinutes,
            UsesGameTime = true,
            Callback = callback,
            Fired = false,
            Completed = false
        };

        scheduledEvents.Add(scheduledEvent);
        Debug.Log($"Chapter event scheduled: {cleanEventId} at {ChapterClock.FormatTime(hour, minute)}", this);
        return true;
    }

    public void Clear()
    {
        scheduledEvents.Clear();
    }

    private void FireEvent(ScheduledChapterEvent scheduledEvent)
    {
        scheduledEvent.Fired = true;
        Debug.Log($"Chapter event fired: {scheduledEvent.EventId}", this);

        try
        {
            scheduledEvent.Callback?.Invoke();
        }
        finally
        {
            scheduledEvent.Completed = true;
            Debug.Log($"Chapter event completed: {scheduledEvent.EventId}", this);
        }
    }

    private bool HasPendingOrFiredEvent(string eventId)
    {
        for (int i = 0; i < scheduledEvents.Count; i++)
        {
            ScheduledChapterEvent scheduledEvent = scheduledEvents[i];

            if (scheduledEvent != null &&
                string.Equals(scheduledEvent.EventId, eventId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public override void ValidateConfiguration(Chateau.Architecture.ValidationReport report)
    {
        base.ValidateConfiguration(report);

        if (chapterClock == null)
        {
            report.AddError("ChapterEventScheduler requires an explicit ChapterClock reference on the same configured gameplay root.", this);
        }
    }

    private bool EnsureClockReference()
    {
        ResolveReferences();

        if (chapterClock != null)
        {
            return true;
        }

        Debug.LogError("ChapterEventScheduler cannot schedule an event because no ChapterClock is assigned.", this);
        return false;
    }

    private void ResolveReferences()
    {
        if (chapterClock == null)
        {
            chapterClock = GetComponent<ChapterClock>();
        }
    }
}
