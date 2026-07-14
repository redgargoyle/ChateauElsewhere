#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Chateau.Architecture;
using NUnit.Framework;
using UnityEngine;

public sealed class ClockSchedulerContractTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private GameObject fixtureObject;
    private GameDatabase database;
    private GameContext context;
    private ChapterClock clock;
    private ChapterEventScheduler scheduler;

    [SetUp]
    public void SetUp()
    {
        fixtureObject = new GameObject("ClockSchedulerContractFixture");
        fixtureObject.SetActive(false);

        GameRoot root = fixtureObject.AddComponent<GameRoot>();
        clock = fixtureObject.AddComponent<ChapterClock>();
        scheduler = fixtureObject.AddComponent<ChapterEventScheduler>();
        SetPrivateField(scheduler, "chapterClock", clock);

        database = ScriptableObject.CreateInstance<GameDatabase>();
        database.hideFlags = HideFlags.HideAndDontSave;
        context = CreateContext(root, database, clock, scheduler);

        clock.Initialize(context);
        scheduler.Initialize(context);
    }

    [TearDown]
    public void TearDown()
    {
        if (scheduler != null && scheduler.IsInitialized)
        {
            scheduler.Shutdown(context);
        }

        if (clock != null && clock.IsInitialized)
        {
            clock.Shutdown(context);
        }

        if (fixtureObject != null)
        {
            UnityEngine.Object.DestroyImmediate(fixtureObject);
        }

        if (database != null)
        {
            UnityEngine.Object.DestroyImmediate(database);
        }

        context = null;
        clock = null;
        scheduler = null;
    }

    [Test]
    public void ClockPauseAndResumePreserveElapsedTime()
    {
        clock.StartClock();
        AdvanceClock(1.25f);

        clock.StopClock();
        AdvanceClock(10f);
        Assert.That(clock.ElapsedSeconds, Is.EqualTo(1.25f).Within(0.0001f));

        clock.StartClock();
        AdvanceClock(0.75f);

        Assert.That(clock.IsRunning, Is.True);
        Assert.That(clock.ElapsedSeconds, Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void ClockSpeedChangePreservesGameTimeAndMinuteBoundary()
    {
        clock.SetStartTime(17, 59);
        clock.SetSecondsPerGameMinute(5f);
        clock.StartClock();

        AdvanceClock(4.999f);
        Assert.That(clock.CurrentTimeLabel, Is.EqualTo("5:59 PM"));

        AdvanceClock(0.001f);
        Assert.That(clock.CurrentTimeLabel, Is.EqualTo("6:00 PM"));
        Assert.That(clock.ElapsedGameMinutes, Is.EqualTo(1f).Within(0.0001f));

        clock.SetSecondsPerGameMinute(10f);
        Assert.That(clock.ElapsedSeconds, Is.EqualTo(10f).Within(0.0001f));
        Assert.That(clock.ElapsedGameMinutes, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(clock.CurrentTimeLabel, Is.EqualTo("6:00 PM"));

        AdvanceClock(10f);
        Assert.That(clock.CurrentTimeLabel, Is.EqualTo("6:01 PM"));
    }

    [Test]
    public void ClockStateCaptureAndRestoreAreExactAndAtomic()
    {
        clock.SetStartTime(18, 7);
        clock.SetSecondsPerGameMinute(2.5f);
        clock.StartClock();
        AdvanceClock(3.75f);
        GameClockState captured = clock.CaptureState();

        clock.StopClock();
        clock.SetStartTime(1, 2);
        clock.SetSecondsPerGameMinute(99f);
        clock.RestoreState(captured);

        Assert.That(clock.CaptureState(), Is.EqualTo(captured));
        Assert.That(clock.ElapsedSeconds, Is.EqualTo(3.75f).Within(0.0001f));
        Assert.That(clock.ElapsedGameMinutes, Is.EqualTo(1.5f).Within(0.0001f));
        Assert.That(clock.StartTotalMinutes, Is.EqualTo(18 * 60 + 7));
        Assert.That(clock.CurrentTimeLabel, Is.EqualTo("6:08 PM"));
        Assert.That(clock.IsRunning, Is.True);
    }

    [Test]
    public void SchedulerDoesNotProcessDueWorkWhilePausedThenFiresOnceWhenResumed()
    {
        int fireCount = 0;
        Assert.That(scheduler.ScheduleOneShot("pause-contract", 1f, () => fireCount++), Is.True);

        clock.RestoreState(new GameClockState(2f, false, 5f, 17, 59));
        ProcessScheduler();
        Assert.That(fireCount, Is.Zero);
        Assert.That(scheduler.PendingEventCount, Is.EqualTo(1));

        clock.RestoreState(new GameClockState(2f, true, 5f, 17, 59));
        ProcessScheduler();
        ProcessScheduler();

        Assert.That(fireCount, Is.EqualTo(1));
        Assert.That(scheduler.PendingEventCount, Is.Zero);
    }

    [Test]
    public void SchedulerCrossingSeveralClockTimesPreservesRegistrationOrderAndExactlyOnce()
    {
        List<string> fired = new List<string>();
        clock.SetStartTime(17, 59);
        clock.SetSecondsPerGameMinute(1f);

        Assert.That(scheduler.ScheduleOneShotAtClockTime("six", 18, 0, () => fired.Add("six")), Is.True);
        Assert.That(scheduler.ScheduleOneShotAtClockTime("six-one", 18, 1, () => fired.Add("six-one")), Is.True);
        Assert.That(scheduler.ScheduleOneShotAtClockTime("six-two", 18, 2, () => fired.Add("six-two")), Is.True);

        clock.StartClock();
        AdvanceClock(3f);
        AdvanceClock(1f);

        Assert.That(fired, Is.EqualTo(new[] { "six", "six-one", "six-two" }));
        Assert.That(scheduler.PendingEventCount, Is.Zero);
    }

    [Test]
    public void SchedulerCancelIsCaseInsensitiveIdempotentAndReleasesTheId()
    {
        int fireCount = 0;
        Assert.That(scheduler.ScheduleOneShot("  Mixed_Event  ", 0.5f, () => fireCount++), Is.True);
        Assert.That(scheduler.Cancel("mixed_event"), Is.True);
        Assert.That(scheduler.Cancel("MIXED_EVENT"), Is.False);
        Assert.That(scheduler.PendingEventCount, Is.Zero);

        Assert.That(scheduler.ScheduleOneShot("MIXED_EVENT", 0.5f, () => fireCount++), Is.True);
        clock.StartClock();
        AdvanceClock(0.5f);

        Assert.That(fireCount, Is.EqualTo(1));
        Assert.That(scheduler.Cancel("mixed_event"), Is.False);
    }

    [Test]
    public void SchedulerClearReleasesAllIdsAndClockRestoreSupportsCleanOwnerRearm()
    {
        int staleFireCount = 0;
        int restoredFireCount = 0;
        clock.StartClock();
        AdvanceClock(4f);
        GameClockState captured = clock.CaptureState();

        Assert.That(scheduler.ScheduleOneShot("restore-event", 6f, () => staleFireCount++), Is.True);
        Assert.That(scheduler.ScheduleOneShot("other-event", 2f, () => staleFireCount++), Is.True);
        Assert.That(scheduler.PendingEventCount, Is.EqualTo(2));

        scheduler.Clear();
        Assert.That(scheduler.PendingEventCount, Is.Zero);
        AdvanceClock(20f);
        clock.RestoreState(captured);

        Assert.That(scheduler.ScheduleOneShot("RESTORE-EVENT", 6f, () => restoredFireCount++), Is.True);
        AdvanceClock(5.9f);
        Assert.That(restoredFireCount, Is.Zero);
        AdvanceClock(0.1f);

        Assert.That(staleFireCount, Is.Zero);
        Assert.That(restoredFireCount, Is.EqualTo(1));
        Assert.That(scheduler.PendingEventCount, Is.Zero);
    }

    [Test]
    public void SchedulerHasNoReferenceRepairAndClockHudRemainsReadOnlyPresentation()
    {
        string schedulerSource = ReadAssetSource("Scripts/Story/ChapterEventScheduler.cs");
        string hudSource = ReadAssetSource("_Chateau/Runtime/UI/GameTimeHUD.cs");
        string arrivalSource = ReadAssetSource("_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs");

        Assert.That(schedulerSource, Does.Not.Contain("GetComponent<ChapterClock>"));
        Assert.That(schedulerSource, Does.Not.Contain("private void Awake()"));
        Assert.That(schedulerSource, Does.Contain("TimeAdvanced += HandleClockAdvanced"));
        Assert.That(schedulerSource, Does.Contain("TimeAdvanced -= HandleClockAdvanced"));
        Assert.That(arrivalSource, Does.Not.Contain("FallbackScheduleAtClockTime"));

        Assert.That(hudSource, Does.Contain("chapterClock.CurrentTimeLabel"));
        Assert.That(hudSource, Does.Not.Contain("StartClock("));
        Assert.That(hudSource, Does.Not.Contain("StopClock("));
        Assert.That(hudSource, Does.Not.Contain("ResetClock("));
        Assert.That(hudSource, Does.Not.Contain("SetStartTime("));
        Assert.That(hudSource, Does.Not.Contain("SetSecondsPerGameMinute("));
        Assert.That(hudSource, Does.Not.Contain("RestoreState("));
    }

    private void AdvanceClock(float deltaSeconds)
    {
        MethodInfo advance = typeof(ChapterClock).GetMethod("Advance", PrivateInstance);
        Assert.That(advance, Is.Not.Null);
        advance.Invoke(clock, new object[] { deltaSeconds });
    }

    private void ProcessScheduler()
    {
        MethodInfo process = typeof(ChapterEventScheduler).GetMethod("HandleClockAdvanced", PrivateInstance);
        Assert.That(process, Is.Not.Null);
        process.Invoke(scheduler, null);
    }

    private static GameContext CreateContext(
        GameRoot root,
        GameDatabase gameDatabase,
        ChapterClock clockService,
        ChapterEventScheduler schedulerService)
    {
        IReadOnlyList<IGameService> services = new IGameService[]
        {
            clockService,
            schedulerService,
            new ClockSchedulerContractCameraService(),
            new ClockSchedulerContractNavigationService(),
            new ClockSchedulerContractLightingService(),
            new ClockSchedulerContractPresentationService(),
            new ClockSchedulerContractDialogueService(),
            new ClockSchedulerContractGameFlowService()
        };

        ConstructorInfo constructor = typeof(GameContext).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            new[] { typeof(GameRoot), typeof(GameDatabase), typeof(IReadOnlyList<IGameService>) },
            modifiers: null);
        Assert.That(constructor, Is.Not.Null);
        return (GameContext)constructor.Invoke(new object[] { root, gameDatabase, services });
    }

    private static void SetPrivateField(object owner, string fieldName, object value)
    {
        FieldInfo field = owner.GetType().GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(owner, value);
    }

    private static string ReadAssetSource(string relativePath)
    {
        string path = Path.Combine(Application.dataPath, relativePath);
        Assert.That(File.Exists(path), Is.True, path);
        return File.ReadAllText(path);
    }
}

internal abstract class ClockSchedulerContractService : IGameService
{
    protected ClockSchedulerContractService(int initializationOrder)
    {
        InitializationOrder = initializationOrder;
    }

    public int InitializationOrder { get; }
    public bool IsInitialized { get; private set; }

    public void Initialize(GameContext context)
    {
        IsInitialized = true;
    }

    public void Shutdown(GameContext context)
    {
        IsInitialized = false;
    }

    public void ValidateConfiguration(ValidationReport report)
    {
    }
}

internal sealed class ClockSchedulerContractCameraService : ClockSchedulerContractService, ICameraService
{
    internal ClockSchedulerContractCameraService() : base(GameServiceInitializationOrder.Camera)
    {
    }
}

internal sealed class ClockSchedulerContractNavigationService : ClockSchedulerContractService, INavigationRuntimeService
{
    internal ClockSchedulerContractNavigationService() : base(GameServiceInitializationOrder.Navigation)
    {
    }
}

internal sealed class ClockSchedulerContractLightingService : ClockSchedulerContractService, ILightingService
{
    internal ClockSchedulerContractLightingService() : base(GameServiceInitializationOrder.Lighting)
    {
    }
}

internal sealed class ClockSchedulerContractPresentationService : ClockSchedulerContractService
{
    internal ClockSchedulerContractPresentationService()
        : base(GameServiceInitializationOrder.TransitionalSubtitlePresentation)
    {
    }
}

internal sealed class ClockSchedulerContractDialogueService : ClockSchedulerContractService, IDialogueService
{
    internal ClockSchedulerContractDialogueService() : base(GameServiceInitializationOrder.Dialogue)
    {
    }
}

internal sealed class ClockSchedulerContractGameFlowService : ClockSchedulerContractService, IGameFlowService
{
    internal ClockSchedulerContractGameFlowService() : base(GameServiceInitializationOrder.GameFlow)
    {
    }
}
#endif
