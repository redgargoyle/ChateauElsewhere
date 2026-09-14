using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class Chapter2MonsterStingerBehaviorTests
{
    private SceneSetup[] previousSceneSetup;

    [TestCase(9f)]
    [TestCase(-9f)]
    [TestCase(0f)]
    public void ThreeRunsReachTheAuthoredTargetWithoutOvershoot(float distance)
    {
        using (StingerFixture fixture = new StingerFixture())
        {
            Vector3 start = new Vector3(4f, 2f, 3f);
            fixture.Monster.transform.position = start;
            fixture.Target.position = new Vector3(start.x + distance, 20f, 30f);
            SetField(fixture.Controller, "isRunning", true);
            Invoke(fixture.Controller, "CaptureRunPath");

            for (int cycle = 0; cycle < 3; cycle++)
            {
                IEnumerator run = (IEnumerator)Invoke(fixture.Controller,
                    "MoveMonsterToNextFreezeTarget", 0f, cycle);
                Assert.That(run.MoveNext(), Is.False);
                Vector3 position = fixture.Monster.transform.position;
                Assert.That(position.x, Is.EqualTo(start.x + distance * (cycle + 1f) / 3f).Within(0.0001f));
                Assert.That(position.x, Is.InRange(Mathf.Min(start.x, start.x + distance), Mathf.Max(start.x, start.x + distance)));
                Assert.That(position.y, Is.EqualTo(start.y));
                Assert.That(position.z, Is.EqualTo(start.z));
            }
        }
    }

    [Test]
    public void MissingTargetUsesOneFallbackSpanForAllThreeRuns()
    {
        using (StingerFixture fixture = new StingerFixture())
        {
            SetField(fixture.Controller, "runTarget", null);
            SetField(fixture.Controller, "fallbackRunRightDistance", 6f);
            SetField(fixture.Controller, "isRunning", true);
            Invoke(fixture.Controller, "CaptureRunPath");

            for (int cycle = 0; cycle < 3; cycle++)
            {
                IEnumerator run = (IEnumerator)Invoke(fixture.Controller,
                    "MoveMonsterToNextFreezeTarget", 0f, cycle);
                Assert.That(run.MoveNext(), Is.False);
                Assert.That(fixture.Monster.transform.position.x, Is.EqualTo(2f * (cycle + 1)).Within(0.0001f));
            }
        }
    }

    [Test]
    public void StoppedNestedRunCannotSnapForwardOrReactivateTheMonster()
    {
        using (StingerFixture fixture = new StingerFixture())
        {
            SetField(fixture.Controller, "isRunning", true);
            Invoke(fixture.Controller, "CaptureRunPath");
            IEnumerator run = (IEnumerator)Invoke(fixture.Controller,
                "MoveMonsterToNextFreezeTarget", 100f, 0);
            Assert.That(run.MoveNext(), Is.True);
            Vector3 interruptedPosition = fixture.Monster.transform.position;

            fixture.Controller.StopStinger();

            Assert.That(run.MoveNext(), Is.False);
            Assert.That(fixture.Monster.transform.position, Is.EqualTo(interruptedPosition));
            Assert.That(fixture.Monster.activeSelf, Is.False);
            Assert.That(fixture.Controller.IsRunning, Is.False);
        }
    }

    [UnityTest]
    public IEnumerator RunningBeatsPlayAudioFreezesStaySilentAndStopCleansUp()
    {
        previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();

        float previousTimeScale = Time.timeScale;
        bool previousAudioPause = AudioListener.pause;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        using (StingerFixture fixture = new StingerFixture())
        {
            try
            {
                fixture.Host.AddComponent<AudioListener>();
                SetField(fixture.Controller, "minimumRunSeconds", 0.2f);
                SetField(fixture.Controller, "maximumRunSeconds", 0.2f);
                SetField(fixture.Controller, "minimumFreezeSeconds", 0.2f);
                SetField(fixture.Controller, "maximumFreezeSeconds", 0.2f);
                SetField(fixture.Controller, "monsterRunWorldShakeUnits", 100f);
                SetField(fixture.Controller, "shakeMonsterWhileRunning", true);
                fixture.Controller.BeginStinger();

                int runCount = 0;
                int freezeCount = 0;
                bool? previousRunBeat = null;
                float deadline = Time.realtimeSinceStartup + 5f;
                while (fixture.Controller.IsRunning && Time.realtimeSinceStartup < deadline)
                {
                    bool runBeat = GetField<bool>(fixture.Controller, "isRunBeat");
                    if (previousRunBeat != runBeat)
                    {
                        if (runBeat) runCount++;
                        else freezeCount++;
                        previousRunBeat = runBeat;
                    }

                    Assert.That(fixture.Audio.isPlaying, Is.EqualTo(runBeat),
                        "The runtime test needs Unity audio enabled; each run must play and each freeze must stop the source.");
                    Assert.That(fixture.Monster.transform.position.x, Is.InRange(0f, 9f),
                        "Even extreme presentation shake must stay inside the authored horizontal path.");
                    yield return null;
                }

                Assert.That(fixture.Controller.IsRunning, Is.False, "The three-cycle effect must complete.");
                Assert.That(runCount, Is.EqualTo(3));
                Assert.That(freezeCount, Is.EqualTo(3));
                Assert.That(fixture.Monster.transform.position.x, Is.EqualTo(9f).Within(0.0001f));
                Assert.That(fixture.Monster.activeSelf, Is.False);
                Assert.That(fixture.Audio.isPlaying, Is.False);

                fixture.Controller.BeginStinger();
                deadline = Time.realtimeSinceStartup + 2f;
                while (GetField<bool>(fixture.Controller, "isRunBeat") && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(fixture.Controller.IsRunning, Is.True, "Interrupt the active first freeze.");
                Assert.That(GetField<bool>(fixture.Controller, "isRunBeat"), Is.False);
                fixture.Controller.StopStinger();
                Vector3 stoppedPosition = fixture.Monster.transform.position;
                yield return null;
                yield return null;

                Assert.That(fixture.Controller.IsRunning, Is.False);
                Assert.That(fixture.Monster.activeSelf, Is.False);
                Assert.That(fixture.Audio.isPlaying, Is.False);
                Assert.That(fixture.Monster.transform.position, Is.EqualTo(stoppedPosition));
                Assert.That(GetField<bool>(fixture.Controller, "subscribedToRoomChanges"), Is.False);
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                AudioListener.pause = previousAudioPause;
            }
        }

        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator RestoreEditorScene()
    {
        if (EditorApplication.isPlaying)
        {
            yield return new ExitPlayMode();
        }

        if (previousSceneSetup != null && previousSceneSetup.Length > 0)
        {
            EditorSceneManager.RestoreSceneManagerSetup(previousSceneSetup);
        }

        previousSceneSetup = null;
    }

    private static object Invoke(object target, string method, params object[] arguments)
    {
        MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(info, Is.Not.Null, method);
        return info.Invoke(target, arguments);
    }

    private static void SetField(object target, string field, object value)
    {
        FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(info, Is.Not.Null, field);
        info.SetValue(target, value);
    }

    private static T GetField<T>(object target, string field)
    {
        return (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    private sealed class StingerFixture : System.IDisposable
    {
        public readonly GameObject Host = new GameObject("StingerBehaviorTest");
        public readonly GameObject Monster = new GameObject("StingerBehaviorMonster");
        public readonly Transform Start = new GameObject("StingerBehaviorStart").transform;
        public readonly Transform Target = new GameObject("StingerBehaviorTarget").transform;
        public readonly Chapter2MonsterStingerController Controller;
        public readonly AudioSource Audio;
        private readonly AudioClip clip;

        public StingerFixture()
        {
            Controller = Host.AddComponent<Chapter2MonsterStingerController>();
            Audio = Host.AddComponent<AudioSource>();
            Audio.playOnAwake = false;
            Audio.mute = true;
            clip = AudioClip.Create("SilentStingerBehaviorProbe", 44100, 1, 44100, false);
            Audio.clip = clip;
            Target.position = new Vector3(9f, 2f, 3f);
            SetField(Controller, "monsterObject", Monster);
            SetField(Controller, "runStart", Start);
            SetField(Controller, "runTarget", Target);
            SetField(Controller, "violinAudioSource", Audio);
            SetField(Controller, "violinAudioClip", clip);
            SetField(Controller, "forceMonsterToFront", false);
            SetField(Controller, "shakeMonsterWhileRunning", false);
            SetField(Controller, "monsterRunSpritesResourcePath", string.Empty);
            SetField(Controller, "createPlaceholderMonsterIfMissing", false);
        }

        public void Dispose()
        {
            Controller.StopStinger();
            Object.DestroyImmediate(Host);
            Object.DestroyImmediate(Monster);
            Object.DestroyImmediate(Start.gameObject);
            Object.DestroyImmediate(Target.gameObject);
            Object.DestroyImmediate(clip);
        }
    }
}
