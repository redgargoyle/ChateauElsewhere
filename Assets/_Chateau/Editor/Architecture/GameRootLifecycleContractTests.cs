#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Chateau.Architecture;
using Chateau.Editor.Architecture;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class GameRootLifecycleContractTests
{
    private readonly List<Scene> fixtureScenes = new List<Scene>();
    private readonly List<LifecycleFixture> fixtures = new List<LifecycleFixture>();
    private readonly List<GameObject> fixtureObjects = new List<GameObject>();
    private Scene previousActiveScene;
    private Scene fixtureScene;
    private GameDatabase database;

    [SetUp]
    public void SetUp()
    {
        previousActiveScene = SceneManager.GetActiveScene();
        fixtureScene = previousActiveScene;
        Assert.That(fixtureScene.IsValid() && fixtureScene.isLoaded, Is.True);

        database = ScriptableObject.CreateInstance<GameDatabase>();
        database.hideFlags = HideFlags.HideAndDontSave;
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = fixtures.Count - 1; i >= 0; i--)
        {
            LifecycleFixture fixture = fixtures[i];
            fixture.DisableInjectedFailures();

            try
            {
                fixture.Root.ShutdownNow();
            }
            catch
            {
                // Closing the unsaved fixture scene is the final safety net for a failed assertion.
            }
        }

        for (int i = fixtureObjects.Count - 1; i >= 0; i--)
        {
            if (fixtureObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(fixtureObjects[i]);
            }
        }

        for (int i = fixtureScenes.Count - 1; i >= 0; i--)
        {
            Scene scene = fixtureScenes[i];
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        if (database != null)
        {
            UnityEngine.Object.DestroyImmediate(database);
        }

        if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
        {
            SceneManager.SetActiveScene(previousActiveScene);
        }

        fixtures.Clear();
        fixtureObjects.Clear();
        fixtureScenes.Clear();
        database = null;
    }

    [Test]
    public void ValidRootInitializesThenBindsAndUnbindsBeforeReverseShutdown()
    {
        LifecycleFixture fixture = CreateValidFixture("Valid", binderCount: 2);
        fixture.Binders[1].ReenterShutdown = true;
        fixture.Dialogue.ReenterShutdown = true;

        Assert.That(fixture.Root.InitializeNow(), Is.True);
        Assert.That(fixture.Root.InitializeNow(), Is.True, "Repeated initialization must be idempotent.");
        Assert.That(fixture.Root.IsInitialized, Is.True);
        Assert.That(fixture.Root.Context, Is.Not.Null);
        Assert.That(
            fixture.Trace,
            Is.EqualTo(new[]
            {
                "init:clock",
                "init:scheduler",
                "init:camera",
                "init:navigation",
                "init:lighting",
                "init:subtitles",
                "init:dialogue",
                "init:game-flow",
                "bind:binder-a",
                "bind:binder-b"
            }));

        Assert.That(fixture.Root.Context.Clock, Is.SameAs(fixture.Clock));
        Assert.That(fixture.Root.Context.Scheduler, Is.SameAs(fixture.Scheduler));
        Assert.That(fixture.Root.Context.Camera, Is.SameAs(fixture.Camera));
        Assert.That(fixture.Root.Context.Navigation, Is.SameAs(fixture.Navigation));
        Assert.That(fixture.Root.Context.Lighting, Is.SameAs(fixture.Lighting));
        Assert.That(fixture.Root.Context.Services[5], Is.SameAs(fixture.Subtitles));
        Assert.That(fixture.Root.Context.Dialogue, Is.SameAs(fixture.Dialogue));
        Assert.That(fixture.Root.Context.GameFlow, Is.SameAs(fixture.GameFlow));
        Assert.That(fixture.Services.All(service => service.IsInitialized && service.HasGameContext), Is.True);
        Assert.That(fixture.Binders.All(binder => binder.HasGameContext), Is.True);
        Assert.That(fixture.Binders.All(binder => binder.AllServicesInitializedOnBind), Is.True);

        fixture.Root.ShutdownNow();
        fixture.Root.ShutdownNow();

        Assert.That(
            fixture.Trace,
            Is.EqualTo(new[]
            {
                "init:clock",
                "init:scheduler",
                "init:camera",
                "init:navigation",
                "init:lighting",
                "init:subtitles",
                "init:dialogue",
                "init:game-flow",
                "bind:binder-a",
                "bind:binder-b",
                "unbind:binder-b",
                "unbind:binder-a",
                "shutdown:game-flow",
                "shutdown:dialogue",
                "shutdown:subtitles",
                "shutdown:lighting",
                "shutdown:navigation",
                "shutdown:camera",
                "shutdown:scheduler",
                "shutdown:clock"
            }));
        Assert.That(fixture.Binders.All(binder => binder.AllServicesInitializedOnUnbind), Is.True);
        AssertFullyReleased(fixture);
    }

    [Test]
    public void ServiceAndBinderFailuresRollbackEveryCompletedLifecycleStep()
    {
        LifecycleFixture initializationFailure = CreateValidFixture("InitializeFailure");
        initializationFailure.Lighting.ThrowOnInitialize = true;
        LogAssert.Expect(LogType.Exception, new Regex("Synthetic initialization failure: lighting"));

        Assert.That(initializationFailure.Root.InitializeNow(), Is.False);
        Assert.That(
            initializationFailure.Trace,
            Is.EqualTo(new[]
            {
                "init:clock",
                "init:scheduler",
                "init:camera",
                "init:navigation",
                "init:lighting",
                "shutdown:navigation",
                "shutdown:camera",
                "shutdown:scheduler",
                "shutdown:clock"
            }));
        Assert.That(initializationFailure.Binders[0].BindCount, Is.Zero);
        Assert.That(initializationFailure.Binders[0].UnbindCount, Is.Zero);
        AssertFullyReleased(initializationFailure);

        LifecycleFixture contextBindingFailure = CreateValidFixture("ContextBindingFailure");
        contextBindingFailure.Lighting.ThrowOnContextBind = true;
        LogAssert.Expect(LogType.Exception, new Regex("Synthetic context binding failure: lighting"));

        Assert.That(contextBindingFailure.Root.InitializeNow(), Is.False);
        Assert.That(
            contextBindingFailure.Trace,
            Is.EqualTo(new[]
            {
                "init:clock",
                "init:scheduler",
                "init:camera",
                "init:navigation",
                "shutdown:navigation",
                "shutdown:camera",
                "shutdown:scheduler",
                "shutdown:clock"
            }));
        Assert.That(contextBindingFailure.Lighting.HasGameContext, Is.False);
        Assert.That(contextBindingFailure.Lighting.IsInitialized, Is.False);
        AssertFullyReleased(contextBindingFailure);

        LifecycleFixture serviceReentryFailure = CreateValidFixture("ServiceReentryFailure");
        serviceReentryFailure.Lighting.ReenterInitialize = true;
        LogAssert.Expect(
            LogType.Exception,
            new Regex("LifecycleLightingService cannot initialize while it is initializing"));

        Assert.That(serviceReentryFailure.Root.InitializeNow(), Is.False);
        Assert.That(
            serviceReentryFailure.Trace,
            Is.EqualTo(new[]
            {
                "init:clock",
                "init:scheduler",
                "init:camera",
                "init:navigation",
                "init:lighting",
                "shutdown:navigation",
                "shutdown:camera",
                "shutdown:scheduler",
                "shutdown:clock"
            }));
        AssertFullyReleased(serviceReentryFailure);

        LifecycleFixture cleanupFailure = CreateValidFixture("CleanupFailure", binderCount: 2);
        cleanupFailure.Binders[1].ThrowOnBind = true;
        cleanupFailure.Binders[0].ThrowOnUnbind = true;
        cleanupFailure.Dialogue.ThrowOnShutdown = true;
        LogAssert.Expect(LogType.Exception, new Regex("Synthetic binder binding failure: binder-b"));
        LogAssert.Expect(LogType.Exception, new Regex("Synthetic binder cleanup failure: binder-a"));
        LogAssert.Expect(LogType.Exception, new Regex("Synthetic shutdown failure: dialogue"));

        Assert.That(cleanupFailure.Root.InitializeNow(), Is.False);
        Assert.That(
            cleanupFailure.Trace,
            Is.EqualTo(new[]
            {
                "init:clock",
                "init:scheduler",
                "init:camera",
                "init:navigation",
                "init:lighting",
                "init:subtitles",
                "init:dialogue",
                "init:game-flow",
                "bind:binder-a",
                "bind:binder-b",
                "unbind:binder-b",
                "unbind:binder-a",
                "shutdown:game-flow",
                "shutdown:dialogue",
                "shutdown:subtitles",
                "shutdown:lighting",
                "shutdown:navigation",
                "shutdown:camera",
                "shutdown:scheduler",
                "shutdown:clock"
            }));
        Assert.That(cleanupFailure.Binders.All(binder => binder.AllServicesInitializedOnUnbind), Is.True);
        AssertFullyReleased(cleanupFailure);
    }

    [Test]
    public void DuplicateNullAndMiscategorizedRegistrationsAreFatalBeforeStartup()
    {
        LifecycleFixture fixture = CreateValidFixture("InvalidRegistrations");
        fixture.SerializedServices.Add(fixture.Clock);
        fixture.SerializedServices.Add(null);
        fixture.SerializedBinders.Add(fixture.Binders[0]);
        fixture.SerializedBinders.Add(null);
        fixture.SerializedBinders.Add(fixture.Clock);

        ValidationReport report = new ValidationReport();
        fixture.Root.ValidateConfiguration(report);

        Assert.That(report.ErrorCount, Is.EqualTo(6));
        Assert.That(
            report.Messages.Count(message =>
                message.Message.StartsWith("Service instance", StringComparison.Ordinal) &&
                message.Message.Contains("appears more than once in GameRoot")),
            Is.EqualTo(1));
        Assert.That(report.Messages.Count(message => message.Message.Contains("More than one service of type")), Is.EqualTo(1));
        Assert.That(report.Messages.Count(message => message.Message.Contains("service slot 9 is null")), Is.EqualTo(1));
        Assert.That(report.Messages.Count(message => message.Message.Contains("Scene behaviour") && message.Message.Contains("appears more than once")), Is.EqualTo(1));
        Assert.That(report.Messages.Count(message => message.Message.Contains("scene-behaviour slot 2 is null")), Is.EqualTo(1));
        Assert.That(report.Messages.Count(message => message.Message.Contains("must be registered in services")), Is.EqualTo(1));

        LogAssert.Expect(LogType.Log, "Chateau GameRoot validation failed.");
        LogAssert.Expect(LogType.Error, new Regex("Service instance .* appears more than once in GameRoot"));
        LogAssert.Expect(LogType.Error, new Regex("More than one service of type"));
        LogAssert.Expect(LogType.Error, "GameRoot service slot 9 is null.");
        LogAssert.Expect(LogType.Error, new Regex("Scene behaviour .* appears more than once in GameRoot"));
        LogAssert.Expect(LogType.Error, "GameRoot scene-behaviour slot 2 is null.");
        LogAssert.Expect(LogType.Error, new Regex("must be registered in services, not scene behaviours"));

        Assert.That(fixture.Root.InitializeNow(), Is.False);
        Assert.That(fixture.Root.enabled, Is.False);
        Assert.That(fixture.Trace, Is.Empty);
        AssertFullyReleased(fixture);
    }

    [Test]
    public void EditorValidationRejectsMissingForeignAndDuplicateSceneOwnership()
    {
        LifecycleFixture fixture = CreateValidFixture("SceneValidation");

        LifecycleUnownedService omittedService = CreateInactiveObject("OmittedService", fixtureScene)
            .AddComponent<LifecycleUnownedService>();
        omittedService.Configure(fixture.Trace, fixture.Root);
        LifecycleFixtureBinder omittedBinder = CreateInactiveObject("OmittedBinder", fixtureScene)
            .AddComponent<LifecycleFixtureBinder>();
        omittedBinder.Configure("omitted-binder", fixture.Trace, fixture.Root, fixture.Services);

        GameServiceBase[] servicesBeforeValidation = fixture.SerializedServices.ToArray();
        ChateauBehaviour[] bindersBeforeValidation = fixture.SerializedBinders.ToArray();
        bool dirtyBeforeValidation = fixtureScene.isDirty;
        ValidationReport omittedReport = GameRootInstaller.ValidateScene(fixtureScene);

        Assert.That(fixtureScene.isDirty, Is.EqualTo(dirtyBeforeValidation), "Validation must be read-only.");
        Assert.That(fixture.SerializedServices, Is.EqualTo(servicesBeforeValidation));
        Assert.That(fixture.SerializedBinders, Is.EqualTo(bindersBeforeValidation));
        Assert.That(
            omittedReport.Messages.Count(message =>
                message.Severity == ValidationSeverity.Error &&
                message.Message.Contains("Scene service 'OmittedService' is not serialized in GameRoot.services")),
            Is.EqualTo(1));
        Assert.That(
            omittedReport.Messages.Count(message =>
                message.Severity == ValidationSeverity.Error &&
                message.Message.Contains("Scene scene behaviour 'OmittedBinder' is not serialized in GameRoot.sceneBehaviours")),
            Is.EqualTo(1));

        string foreignScenePath = fixtureScene.path == "Assets/Scenes/MainMenu.unity"
            ? "Assets/Scenes/Gameplay.unity"
            : "Assets/Scenes/MainMenu.unity";
        Scene foreignScene = EditorSceneManager.OpenScene(foreignScenePath, OpenSceneMode.Additive);
        fixtureScenes.Add(foreignScene);
        LifecycleUnownedService foreignService = CreateInactiveObject("ForeignService", foreignScene)
            .AddComponent<LifecycleUnownedService>();
        foreignService.Configure(fixture.Trace, fixture.Root);
        LifecycleFixtureBinder foreignBinder = CreateInactiveObject("ForeignBinder", foreignScene)
            .AddComponent<LifecycleFixtureBinder>();
        foreignBinder.Configure("foreign-binder", fixture.Trace, fixture.Root, fixture.Services);
        fixture.SerializedServices.Add(foreignService);
        fixture.SerializedBinders.Add(foreignBinder);

        LifecycleUnsupportedService unsupportedService = CreateInactiveObject("UnsupportedService", fixtureScene)
            .AddComponent<LifecycleUnsupportedService>();

        ValidationReport foreignReport = new ValidationReport();
        fixture.Root.ValidateConfiguration(foreignReport);
        Assert.That(foreignReport.Messages.Count(message => message.Message == "Service 'ForeignService' belongs to another scene."), Is.EqualTo(1));
        Assert.That(foreignReport.Messages.Count(message => message.Message == "Scene behaviour 'ForeignBinder' belongs to another scene."), Is.EqualTo(1));

        ValidationReport exactSetReport = GameRootInstaller.ValidateScene(fixtureScene);
        Assert.That(
            exactSetReport.Messages.Count(message => message.Message.Contains("serialized service 'ForeignService' is not an expected service")),
            Is.EqualTo(1));
        Assert.That(
            exactSetReport.Messages.Count(message => message.Message.Contains("serialized scene behaviour 'ForeignBinder' is not an expected scene behaviour")),
            Is.EqualTo(1));
        Assert.That(
            exactSetReport.Messages.Count(message =>
                message.Message.Contains("Scene service 'UnsupportedService' implements IGameService") &&
                message.Message.Contains("does not derive from GameServiceBase")),
            Is.EqualTo(1));
        Assert.That(unsupportedService.IsInitialized, Is.False);

        GameObject duplicateRootObject = CreateInactiveObject("DuplicateRoot", fixtureScene);
        duplicateRootObject.AddComponent<GameRoot>();
        ValidationReport duplicateRootReport = GameRootInstaller.ValidateScene(fixtureScene);
        Assert.That(
            duplicateRootReport.Messages.Count(message =>
                message.Severity == ValidationSeverity.Error &&
                message.Message.Contains("requires exactly one GameRoot (composition root); found 2")),
            Is.EqualTo(1));
    }

    [Test]
    public void GameplaySerializesStrictExactCompositionAndRuntimeRootContainsNoRepair()
    {
        const string gameplayPath = "Assets/Scenes/Gameplay.unity";
        const string rootSourcePath = "Assets/_Chateau/Runtime/Core/GameRoot.cs";
        const string rootMetaPath = "Assets/_Chateau/Runtime/Core/GameRoot.cs.meta";
        const string installerPath = "Assets/_Chateau/Editor/Architecture/GameRootInstaller.cs";

        string gameplay = NormalizeNewlines(File.ReadAllText(gameplayPath));
        string rootSource = File.ReadAllText(rootSourcePath);
        string rootMeta = File.ReadAllText(rootMetaPath);
        string installerSource = File.ReadAllText(installerPath);
        string rootGuid = Regex.Match(rootMeta, @"(?m)^guid: (?<guid>[0-9a-f]{32})$").Groups["guid"].Value;

        Assert.That(rootGuid, Is.Not.Empty);
        MatchCollection rootScriptReferences = Regex.Matches(
            gameplay,
            @"m_Script: \{fileID: 11500000, guid: " + Regex.Escape(rootGuid) + @", type: 3\}");
        Assert.That(rootScriptReferences.Count, Is.EqualTo(1));

        string rootDocument = ExtractYamlDocument(gameplay, rootScriptReferences[0].Index);
        Assert.That(rootDocument, Does.Contain("  initializeOnAwake: 1\n"));
        Assert.That(rootDocument, Does.Contain("  failStartupOnValidationErrors: 1\n"));

        string serviceBlock = ExtractYamlBlock(rootDocument, "services", "sceneBehaviours");
        string binderBlock = ExtractYamlBlock(rootDocument, "sceneBehaviours", "initializeOnAwake");
        MatchCollection serviceReferences = Regex.Matches(serviceBlock, @"(?m)^  - \{fileID: (?<id>-?\d+)\}$");
        MatchCollection binderReferences = Regex.Matches(binderBlock, @"(?m)^  - \{fileID: (?<id>-?\d+)\}$");
        Assert.That(serviceReferences.Count, Is.EqualTo(8));
        Assert.That(serviceReferences.Cast<Match>().All(match => match.Groups["id"].Value != "0"), Is.True);
        Assert.That(serviceReferences.Cast<Match>().Select(match => match.Groups["id"].Value).Distinct().Count(), Is.EqualTo(8));
        Assert.That(binderReferences.Count, Is.EqualTo(45));
        Assert.That(binderReferences.Cast<Match>().All(match => match.Groups["id"].Value != "0"), Is.True);
        Assert.That(binderReferences.Cast<Match>().Select(match => match.Groups["id"].Value).Distinct().Count(), Is.EqualTo(45));
        Assert.That(
            installerSource,
            Does.Contain("serializedRoot.FindProperty(\"failStartupOnValidationErrors\").boolValue = true;"));

        string[] forbiddenRepairSurfaces =
        {
            "FindAnyObjectByType",
            "FindFirstObjectByType",
            "FindObjectsByType",
            "GameObject.Find",
            "Resources.Load",
            "new GameObject",
            "AddComponent<",
            "RuntimeInitializeOnLoadMethod"
        };

        for (int i = 0; i < forbiddenRepairSurfaces.Length; i++)
        {
            Assert.That(rootSource, Does.Not.Contain(forbiddenRepairSurfaces[i]), forbiddenRepairSurfaces[i]);
        }

        byte[] gameplayBytesBeforeValidation = File.ReadAllBytes(gameplayPath);
        Scene gameplayScene = EditorSceneManager.OpenScene(gameplayPath, OpenSceneMode.Additive);
        fixtureScenes.Add(gameplayScene);
        ValidationReport gameplayReport;

        try
        {
            gameplayReport = GameRootInstaller.ValidateScene(gameplayScene);
            Assert.That(
                gameplayReport.Messages.Where(message => message.Severity == ValidationSeverity.Error),
                Is.Empty,
                string.Join("\n", gameplayReport.Messages.Select(message => message.ToString())));
            Assert.That(gameplayScene.isDirty, Is.False, "Validation must not dirty Gameplay.");
        }
        finally
        {
            EditorSceneManager.CloseScene(gameplayScene, removeScene: true);
            fixtureScenes.Remove(gameplayScene);
        }

        Assert.That(File.ReadAllBytes(gameplayPath), Is.EqualTo(gameplayBytesBeforeValidation));
    }

    private LifecycleFixture CreateValidFixture(string name, int binderCount = 1)
    {
        GameObject host = CreateInactiveObject(name, fixtureScene);
        LifecycleFixture fixture = new LifecycleFixture
        {
            Root = host.AddComponent<GameRoot>(),
            Clock = host.AddComponent<LifecycleClockService>(),
            Scheduler = host.AddComponent<LifecycleSchedulerService>(),
            Camera = host.AddComponent<LifecycleCameraService>(),
            Navigation = host.AddComponent<LifecycleNavigationService>(),
            Lighting = host.AddComponent<LifecycleLightingService>(),
            Subtitles = host.AddComponent<LifecycleSubtitleService>(),
            Dialogue = host.AddComponent<LifecycleDialogueService>(),
            GameFlow = host.AddComponent<LifecycleGameFlowService>()
        };

        fixture.Services.Add(fixture.Clock);
        fixture.Services.Add(fixture.Scheduler);
        fixture.Services.Add(fixture.Camera);
        fixture.Services.Add(fixture.Navigation);
        fixture.Services.Add(fixture.Lighting);
        fixture.Services.Add(fixture.Subtitles);
        fixture.Services.Add(fixture.Dialogue);
        fixture.Services.Add(fixture.GameFlow);

        for (int i = 0; i < fixture.Services.Count; i++)
        {
            fixture.Services[i].Configure(fixture.Trace, fixture.Root);
        }

        for (int i = 0; i < binderCount; i++)
        {
            LifecycleFixtureBinder binder = host.AddComponent<LifecycleFixtureBinder>();
            binder.Configure($"binder-{(char)('a' + i)}", fixture.Trace, fixture.Root, fixture.Services);
            fixture.Binders.Add(binder);
        }

        fixture.SerializedServices.Add(fixture.GameFlow);
        fixture.SerializedServices.Add(fixture.Camera);
        fixture.SerializedServices.Add(fixture.Clock);
        fixture.SerializedServices.Add(fixture.Subtitles);
        fixture.SerializedServices.Add(fixture.Lighting);
        fixture.SerializedServices.Add(fixture.Scheduler);
        fixture.SerializedServices.Add(fixture.Dialogue);
        fixture.SerializedServices.Add(fixture.Navigation);
        fixture.SerializedBinders.AddRange(fixture.Binders);

        SetPrivateField(fixture.Root, "gameDatabase", database);
        SetPrivateField(fixture.Root, "services", fixture.SerializedServices);
        SetPrivateField(fixture.Root, "sceneBehaviours", fixture.SerializedBinders);
        SetPrivateField(fixture.Root, "initializeOnAwake", false);
        SetPrivateField(fixture.Root, "failStartupOnValidationErrors", true);
        SetPrivateField(fixture.Root, "logSuccessfulInitialization", false);

        fixtures.Add(fixture);
        return fixture;
    }

    private GameObject CreateInactiveObject(string name, Scene scene)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.SetActive(false);
        SceneManager.MoveGameObjectToScene(gameObject, scene);
        fixtureObjects.Add(gameObject);
        return gameObject;
    }

    private static void AssertFullyReleased(LifecycleFixture fixture)
    {
        Assert.That(fixture.Root.IsInitialized, Is.False);
        Assert.That(fixture.Root.Context, Is.Null);
        Assert.That(fixture.Services.All(service => !service.IsInitialized && !service.HasGameContext), Is.True);
        Assert.That(fixture.Binders.All(binder => !binder.HasGameContext), Is.True);
    }

    private static void SetPrivateField(object owner, string fieldName, object value)
    {
        FieldInfo field = owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(owner, value);
    }

    private static string NormalizeNewlines(string value)
    {
        return value.Replace("\r\n", "\n");
    }

    private static string ExtractYamlDocument(string yaml, int containedIndex)
    {
        int start = yaml.LastIndexOf("--- !u!114 &", containedIndex, StringComparison.Ordinal);
        int end = yaml.IndexOf("\n--- !u!", containedIndex, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0));
        Assert.That(end, Is.GreaterThan(start));
        return yaml.Substring(start, end - start + 1);
    }

    private static string ExtractYamlBlock(string document, string fieldName, string nextFieldName)
    {
        string startMarker = $"  {fieldName}:\n";
        string endMarker = $"  {nextFieldName}:";
        int start = document.IndexOf(startMarker, StringComparison.Ordinal);
        int end = document.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), fieldName);
        Assert.That(end, Is.GreaterThan(start), nextFieldName);
        return document.Substring(start + startMarker.Length, end - start - startMarker.Length);
    }

    private sealed class LifecycleFixture
    {
        internal readonly List<string> Trace = new List<string>();
        internal readonly List<LifecycleFixtureService> Services = new List<LifecycleFixtureService>();
        internal readonly List<LifecycleFixtureBinder> Binders = new List<LifecycleFixtureBinder>();
        internal readonly List<GameServiceBase> SerializedServices = new List<GameServiceBase>();
        internal readonly List<ChateauBehaviour> SerializedBinders = new List<ChateauBehaviour>();

        internal GameRoot Root;
        internal LifecycleClockService Clock;
        internal LifecycleSchedulerService Scheduler;
        internal LifecycleCameraService Camera;
        internal LifecycleNavigationService Navigation;
        internal LifecycleLightingService Lighting;
        internal LifecycleSubtitleService Subtitles;
        internal LifecycleDialogueService Dialogue;
        internal LifecycleGameFlowService GameFlow;

        internal void DisableInjectedFailures()
        {
            for (int i = 0; i < Services.Count; i++)
            {
                Services[i].DisableInjectedFailures();
            }

            for (int i = 0; i < Binders.Count; i++)
            {
                Binders[i].DisableInjectedFailures();
            }
        }
    }
}

internal abstract class LifecycleFixtureService : GameServiceBase
{
    internal List<string> Trace;
    internal GameRoot Root;
    internal bool ThrowOnContextBind;
    internal bool ThrowOnInitialize;
    internal bool ThrowOnShutdown;
    internal bool ReenterInitialize;
    internal bool ReenterShutdown;
    protected abstract string RoleName { get; }
    public event Action TimeAdvanced { add { } remove { } }
    public float ElapsedSeconds => 0f;
    public bool IsRunning => false;
    public float SecondsPerGameMinute => 1f;
    public float ElapsedGameMinutes => 0f;
    public int StartTotalMinutes => 0;
    public int CurrentTotalMinutes => 0;
    public int CurrentHour => 0;
    public int CurrentMinute => 0;
    public string CurrentTimeLabel => "12:00 AM";
    public int PendingEventCount => 0;

    internal void Configure(List<string> trace, GameRoot root)
    {
        Trace = trace;
        Root = root;
    }

    internal void DisableInjectedFailures()
    {
        ThrowOnContextBind = false;
        ThrowOnInitialize = false;
        ThrowOnShutdown = false;
        ReenterInitialize = false;
        ReenterShutdown = false;
    }

    public void ResetClock() { }
    public void SetStartTime(int hour, int minute) { }
    public void SetSecondsPerGameMinute(float value) { }
    public GameClockState CaptureState() => default;
    public void RestoreState(GameClockState state) { }
    public void StartClock() { }
    public void StopClock() { }
    public bool ScheduleOneShot(string eventId, float delaySeconds, Action callback) => true;
    public bool ScheduleOneShotAtClockTime(string eventId, int hour, int minute, Action callback) => true;
    public bool Cancel(string eventId) => true;
    public void Clear() { }

    protected override void OnGameContextBound(GameContext context)
    {
        if (ThrowOnContextBind)
        {
            throw new InvalidOperationException($"Synthetic context binding failure: {RoleName}");
        }
    }

    protected override void OnInitialize(GameContext context)
    {
        Trace.Add($"init:{RoleName}");
        if (ReenterInitialize)
        {
            Initialize(context);
        }

        if (ThrowOnInitialize)
        {
            throw new InvalidOperationException($"Synthetic initialization failure: {RoleName}");
        }
    }

    protected override void OnShutdown(GameContext context)
    {
        Trace.Add($"shutdown:{RoleName}");
        if (ReenterShutdown)
        {
            Shutdown(context);
        }

        if (ThrowOnShutdown)
        {
            throw new InvalidOperationException($"Synthetic shutdown failure: {RoleName}");
        }
    }
}

internal sealed class LifecycleClockService : LifecycleFixtureService, IClockService
{
    public override int InitializationOrder => GameServiceInitializationOrder.Clock;
    protected override string RoleName => "clock";
}

internal sealed class LifecycleSchedulerService : LifecycleFixtureService, ISchedulerService
{
    public override int InitializationOrder => GameServiceInitializationOrder.Scheduler;
    protected override string RoleName => "scheduler";
}

internal sealed class LifecycleCameraService : LifecycleFixtureService, ICameraService
{
    public override int InitializationOrder => GameServiceInitializationOrder.Camera;
    protected override string RoleName => "camera";
}

internal sealed class LifecycleNavigationService : LifecycleFixtureService, INavigationRuntimeService
{
    public override int InitializationOrder => GameServiceInitializationOrder.Navigation;
    protected override string RoleName => "navigation";
}

internal sealed class LifecycleLightingService : LifecycleFixtureService, ILightingService
{
    public override int InitializationOrder => GameServiceInitializationOrder.Lighting;
    protected override string RoleName => "lighting";
}

internal sealed class LifecycleSubtitleService : LifecycleFixtureService
{
    public override int InitializationOrder => GameServiceInitializationOrder.TransitionalSubtitlePresentation;
    protected override string RoleName => "subtitles";
}

internal sealed class LifecycleDialogueService : LifecycleFixtureService, IDialogueService
{
    public override int InitializationOrder => GameServiceInitializationOrder.Dialogue;
    protected override string RoleName => "dialogue";
}

internal sealed class LifecycleGameFlowService : LifecycleFixtureService, IGameFlowService
{
    public override int InitializationOrder => GameServiceInitializationOrder.GameFlow;
    protected override string RoleName => "game-flow";
}

internal sealed class LifecycleUnownedService : LifecycleFixtureService
{
    public override int InitializationOrder => 650;
    protected override string RoleName => "unowned";
}

internal sealed class LifecycleUnsupportedService : MonoBehaviour, IGameService
{
    public int InitializationOrder => 650;
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

internal sealed class LifecycleFixtureBinder : ChateauBehaviour
{
    internal string BinderName;
    internal List<string> Trace;
    internal GameRoot Root;
    internal IReadOnlyList<LifecycleFixtureService> Services;
    internal bool ThrowOnBind;
    internal bool ThrowOnUnbind;
    internal bool ReenterShutdown;
    internal int BindCount;
    internal int UnbindCount;
    internal bool AllServicesInitializedOnBind;
    internal bool AllServicesInitializedOnUnbind;

    internal void Configure(
        string binderName,
        List<string> trace,
        GameRoot root,
        IReadOnlyList<LifecycleFixtureService> services)
    {
        BinderName = binderName;
        Trace = trace;
        Root = root;
        Services = services;
    }

    internal void DisableInjectedFailures()
    {
        ThrowOnBind = false;
        ThrowOnUnbind = false;
        ReenterShutdown = false;
    }

    protected override void OnGameContextBound(GameContext context)
    {
        BindCount++;
        AllServicesInitializedOnBind = Services.All(service => service.IsInitialized);
        Trace.Add($"bind:{BinderName}");

        if (ThrowOnBind)
        {
            throw new InvalidOperationException($"Synthetic binder binding failure: {BinderName}");
        }
    }

    protected override void OnGameContextUnbinding(GameContext context)
    {
        UnbindCount++;
        AllServicesInitializedOnUnbind = Services.All(service => service.IsInitialized);
        Trace.Add($"unbind:{BinderName}");

        if (ReenterShutdown)
        {
            Root.ShutdownNow();
        }

        if (ThrowOnUnbind)
        {
            throw new InvalidOperationException($"Synthetic binder cleanup failure: {BinderName}");
        }
    }
}
#endif
