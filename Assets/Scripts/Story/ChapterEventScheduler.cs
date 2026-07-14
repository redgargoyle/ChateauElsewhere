using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ChapterEventScheduler : Chateau.Architecture.GameServiceBase, Chateau.Architecture.ISchedulerService
{
    public override int InitializationOrder => Chateau.Architecture.GameServiceInitializationOrder.Scheduler;

    private sealed class ScheduledEvent
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

    private readonly List<ScheduledEvent> scheduledEvents = new List<ScheduledEvent>();

    public int PendingEventCount
    {
        get
        {
            int pendingCount = 0;

            for (int i = 0; i < scheduledEvents.Count; i++)
            {
                ScheduledEvent scheduledEvent = scheduledEvents[i];

                if (scheduledEvent != null && !scheduledEvent.Fired)
                {
                    pendingCount++;
                }
            }

            return pendingCount;
        }
    }

    public bool ScheduleOneShot(string eventId, float delaySeconds, Action callback)
    {
        if (!EnsureClockReference())
        {
            return false;
        }

        if (float.IsNaN(delaySeconds) || float.IsInfinity(delaySeconds))
        {
            Debug.LogError("ChapterEventScheduler requires a finite one-shot delay.", this);
            return false;
        }

        string cleanEventId = string.IsNullOrWhiteSpace(eventId) ? "scheduled_event" : eventId.Trim();

        if (HasPendingOrFiredEvent(cleanEventId))
        {
            Debug.LogWarning($"Scheduled event '{cleanEventId}' was already scheduled or fired and will not be scheduled twice.", this);
            return false;
        }

        float elapsedSeconds = chapterClock != null ? chapterClock.ElapsedSeconds : 0f;
        ScheduledEvent scheduledEvent = new ScheduledEvent
        {
            EventId = cleanEventId,
            FireAtElapsedSeconds = elapsedSeconds + Mathf.Max(0f, delaySeconds),
            UsesGameTime = false,
            Callback = callback,
            Fired = false,
            Completed = false
        };

        scheduledEvents.Add(scheduledEvent);
        Debug.Log($"Scheduled event registered: {cleanEventId} at {scheduledEvent.FireAtElapsedSeconds:0.00}s", this);
        return true;
    }

    public bool ScheduleOneShotAtClockTime(string eventId, int hour, int minute, Action callback)
    {
        if (!EnsureClockReference())
        {
            return false;
        }

        string cleanEventId = string.IsNullOrWhiteSpace(eventId) ? "scheduled_clock_event" : eventId.Trim();

        if (HasPendingOrFiredEvent(cleanEventId))
        {
            Debug.LogWarning($"Scheduled event '{cleanEventId}' was already scheduled or fired and will not be scheduled twice.", this);
            return false;
        }

        int targetTotalMinutes = ChapterClock.ToTotalMinutes(hour, minute);
        ScheduledEvent scheduledEvent = new ScheduledEvent
        {
            EventId = cleanEventId,
            FireAtGameTotalMinutes = targetTotalMinutes,
            UsesGameTime = true,
            Callback = callback,
            Fired = false,
            Completed = false
        };

        scheduledEvents.Add(scheduledEvent);
        Debug.Log($"Scheduled event registered: {cleanEventId} at {ChapterClock.FormatTime(hour, minute)}", this);
        return true;
    }

    public bool Cancel(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return false;
        }

        string cleanEventId = eventId.Trim();

        for (int i = 0; i < scheduledEvents.Count; i++)
        {
            ScheduledEvent scheduledEvent = scheduledEvents[i];

            if (scheduledEvent == null ||
                scheduledEvent.Fired ||
                !string.Equals(scheduledEvent.EventId, cleanEventId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            scheduledEvents.RemoveAt(i);
            Debug.Log($"Scheduled event cancelled: {scheduledEvent.EventId}", this);
            return true;
        }

        return false;
    }

    public void Clear()
    {
        scheduledEvents.Clear();
    }

    protected override void OnInitialize(Chateau.Architecture.GameContext context)
    {
        if (chapterClock == null)
        {
            throw new InvalidOperationException(
                "ChapterEventScheduler cannot initialize without its serialized ChapterClock.");
        }

        if (!ReferenceEquals(context.Clock, chapterClock))
        {
            throw new InvalidOperationException(
                "ChapterEventScheduler requires the same ChapterClock registered in GameContext.");
        }

        chapterClock.TimeAdvanced += HandleClockAdvanced;
    }

    protected override void OnShutdown(Chateau.Architecture.GameContext context)
    {
        if (chapterClock != null)
        {
            chapterClock.TimeAdvanced -= HandleClockAdvanced;
        }

        Clear();
    }

    private void HandleClockAdvanced()
    {
        if (!IsInitialized || chapterClock == null || !chapterClock.IsRunning)
        {
            return;
        }

        ProcessDueEvents(chapterClock.ElapsedSeconds, chapterClock.CurrentTotalMinutes);
    }

    private void ProcessDueEvents(float elapsedSeconds, int currentTotalMinutes)
    {
        List<ScheduledEvent> dueEvents = null;

        for (int i = 0; i < scheduledEvents.Count; i++)
        {
            ScheduledEvent scheduledEvent = scheduledEvents[i];

            if (scheduledEvent == null || scheduledEvent.Fired)
            {
                continue;
            }

            bool shouldFire = scheduledEvent.UsesGameTime
                ? currentTotalMinutes >= scheduledEvent.FireAtGameTotalMinutes
                : elapsedSeconds >= scheduledEvent.FireAtElapsedSeconds;

            if (!shouldFire)
            {
                continue;
            }

            if (dueEvents == null)
            {
                dueEvents = new List<ScheduledEvent>();
            }

            dueEvents.Add(scheduledEvent);
        }

        if (dueEvents == null)
        {
            return;
        }

        for (int i = 0; i < dueEvents.Count; i++)
        {
            ScheduledEvent scheduledEvent = dueEvents[i];

            if (scheduledEvents.Contains(scheduledEvent) && !scheduledEvent.Fired)
            {
                FireEvent(scheduledEvent);
            }
        }
    }

    private void FireEvent(ScheduledEvent scheduledEvent)
    {
        scheduledEvent.Fired = true;
        Debug.Log($"Scheduled event fired: {scheduledEvent.EventId}", this);

        try
        {
            scheduledEvent.Callback?.Invoke();
        }
        finally
        {
            scheduledEvent.Completed = true;
            Debug.Log($"Scheduled event completed: {scheduledEvent.EventId}", this);
        }
    }

    private bool HasPendingOrFiredEvent(string eventId)
    {
        for (int i = 0; i < scheduledEvents.Count; i++)
        {
            ScheduledEvent scheduledEvent = scheduledEvents[i];

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
        else if (chapterClock.gameObject != gameObject)
        {
            report.AddError("ChapterEventScheduler and its serialized ChapterClock must share the configured gameplay root.", this);
        }
    }

    private bool EnsureClockReference()
    {
        if (chapterClock != null)
        {
            return true;
        }

        Debug.LogError("ChapterEventScheduler cannot schedule an event because no ChapterClock is assigned.", this);
        return false;
    }
}
