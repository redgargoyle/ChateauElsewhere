using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class ClockSchedulerPlayModeTests
{
    private const string GameplaySceneName = "Gameplay";
    private const string MainMenuSceneName = "MainMenu";
    private const string ContractEventId = "clock_scheduler_playmode_pause_resume_contract";
    private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
        yield return null;
    }

    [UnityTest]
    public IEnumerator RealGameplayClockPauseBlocksScheduledWorkAndResumeFiresExactlyOnce()
    {
        SceneManager.LoadScene(GameplaySceneName, LoadSceneMode.Single);

        for (int frame = 0; frame < 120 && SceneManager.GetActiveScene().name != GameplaySceneName; frame++)
        {
            yield return null;
        }

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameplaySceneName));

        MonoBehaviour chapter = RequireSingleActiveSceneComponent("ChapterManager");
        SetPrivateField(chapter, "skipIntro", true);
        MonoBehaviour clock = RequireSingleActiveSceneComponent("ChapterClock");
        MonoBehaviour scheduler = RequireSingleActiveSceneComponent("ChapterEventScheduler");

        for (int frame = 0; frame < 600; frame++)
        {
            if (GetProperty<bool>(clock, "IsInitialized") &&
                GetProperty<bool>(scheduler, "IsInitialized") &&
                GetProperty<bool>(clock, "IsRunning") &&
                GetProperty<int>(scheduler, "PendingEventCount") > 0)
            {
                break;
            }

            yield return null;
        }

        Assert.That(GetProperty<bool>(clock, "IsInitialized"), Is.True);
        Assert.That(GetProperty<bool>(scheduler, "IsInitialized"), Is.True);
        Assert.That(GetProperty<bool>(clock, "IsRunning"), Is.True);
        int chapterPendingCount = GetProperty<int>(scheduler, "PendingEventCount");
        Assert.That(chapterPendingCount, Is.GreaterThan(0), "Chapter 1 must arm its real scheduler before the pause proof.");

        int fireCount = 0;
        Action callback = () => fireCount++;
        Assert.That(
            (bool)InvokeMethod(scheduler, "ScheduleOneShot", ContractEventId, 0.05f, callback),
            Is.True);
        Assert.That(GetProperty<int>(scheduler, "PendingEventCount"), Is.EqualTo(chapterPendingCount + 1));

        InvokeMethod(clock, "StopClock");
        float pausedElapsedSeconds = GetProperty<float>(clock, "ElapsedSeconds");
        yield return new WaitForSecondsRealtime(0.15f);

        Assert.That(fireCount, Is.Zero);
        Assert.That(GetProperty<float>(clock, "ElapsedSeconds"), Is.EqualTo(pausedElapsedSeconds).Within(0.0001f));

        InvokeMethod(clock, "StartClock");
        for (int frame = 0; frame < 120 && fireCount == 0; frame++)
        {
            yield return null;
        }

        Assert.That(fireCount, Is.EqualTo(1));
        for (int frame = 0; frame < 5; frame++)
        {
            yield return null;
        }

        Assert.That(fireCount, Is.EqualTo(1));
        Assert.That((bool)InvokeMethod(scheduler, "Cancel", ContractEventId), Is.False);
    }

    private static MonoBehaviour RequireSingleActiveSceneComponent(string typeName)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        MonoBehaviour match = null;
        MonoBehaviour[] candidates = Resources.FindObjectsOfTypeAll<MonoBehaviour>();

        for (int i = 0; i < candidates.Length; i++)
        {
            MonoBehaviour candidate = candidates[i];

            if (candidate == null ||
                candidate.gameObject.scene != activeScene ||
                candidate.GetType().Name != typeName)
            {
                continue;
            }

            Assert.That(match, Is.Null, $"Expected one {typeName} in {activeScene.name}.");
            match = candidate;
        }

        Assert.That(match, Is.Not.Null, $"Expected one {typeName} in {activeScene.name}.");
        return match;
    }

    private static T GetProperty<T>(object owner, string propertyName)
    {
        PropertyInfo property = owner.GetType().GetProperty(propertyName, PublicInstance);
        Assert.That(property, Is.Not.Null, propertyName);
        return (T)property.GetValue(owner);
    }

    private static object InvokeMethod(object owner, string methodName, params object[] arguments)
    {
        MethodInfo match = null;
        MethodInfo[] methods = owner.GetType().GetMethods(PublicInstance);

        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo candidate = methods[i];

            if (candidate.Name == methodName && candidate.GetParameters().Length == arguments.Length)
            {
                Assert.That(match, Is.Null, $"Ambiguous method {owner.GetType().Name}.{methodName}.");
                match = candidate;
            }
        }

        Assert.That(match, Is.Not.Null, $"Missing method {owner.GetType().Name}.{methodName}.");
        return match.Invoke(owner, arguments);
    }

    private static void SetPrivateField(object owner, string fieldName, object value)
    {
        FieldInfo field = owner.GetType().GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(owner, value);
    }
}
