#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Chateau.Architecture;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GameContextContractTests
{
    private static readonly string[] RoleNames =
    {
        "clock",
        "scheduler",
        "camera",
        "navigation",
        "lighting",
        "dialogue",
        "game flow"
    };

    private static readonly int[] RequiredRoleIndexes = { 0, 1, 2, 3, 4, 6, 7 };

    private GameObject rootObject;
    private GameRoot root;

    [SetUp]
    public void SetUp()
    {
        rootObject = new GameObject("GameContextContractTests_Root")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        rootObject.SetActive(false);
        root = rootObject.AddComponent<GameRoot>();
    }

    [TearDown]
    public void TearDown()
    {
        if (rootObject != null)
        {
            UnityEngine.Object.DestroyImmediate(rootObject);
        }
    }

    [Test]
    public void ContextExposesTypedRolesWithoutLocatorOrGlobalAccess()
    {
        Type contextType = typeof(GameContext);
        Dictionary<string, Type> expectedProperties = new Dictionary<string, Type>
        {
            { "Clock", typeof(IClockService) },
            { "Scheduler", typeof(ISchedulerService) },
            { "Camera", typeof(ICameraService) },
            { "Navigation", typeof(INavigationRuntimeService) },
            { "Lighting", typeof(ILightingService) },
            { "Dialogue", typeof(IDialogueService) },
            { "GameFlow", typeof(IGameFlowService) }
        };

        foreach (KeyValuePair<string, Type> expected in expectedProperties)
        {
            PropertyInfo property = contextType.GetProperty(expected.Key, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, expected.Key);
            Assert.That(property.PropertyType, Is.EqualTo(expected.Value), expected.Key);
            Assert.That(property.CanWrite, Is.False, expected.Key);
            Assert.That(
                typeof(IGameService).IsAssignableFrom(property.PropertyType),
                Is.False,
                $"{expected.Key} must not expose GameRoot-owned lifecycle controls.");
        }

        Assert.That(contextType.GetProperty("Subtitles", BindingFlags.Instance | BindingFlags.Public), Is.Null);

        MemberInfo[] declaredPublicStatics = contextType.GetMembers(
            BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static);
        MethodInfo[] declaredPublicMethods = contextType.GetMethods(
            BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        MethodInfo[] allDeclaredMethods = contextType.GetMethods(
            BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static);
        FieldInfo[] allDeclaredFields = contextType.GetFields(
            BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static);

        Assert.That(declaredPublicStatics, Is.Empty, "GameContext must never expose global access.");
        Assert.That(allDeclaredMethods.Any(method => method.IsGenericMethodDefinition), Is.False);
        Assert.That(
            allDeclaredMethods.Any(method =>
                !method.IsSpecialName &&
                (method.Name.StartsWith("Get", StringComparison.Ordinal) ||
                 method.Name.StartsWith("TryGet", StringComparison.Ordinal) ||
                 method.Name.StartsWith("Resolve", StringComparison.Ordinal) ||
                 method.Name.StartsWith("Find", StringComparison.Ordinal))),
            Is.False,
            "GameContext must bind roles once rather than resolve them on demand.");
        Assert.That(
            allDeclaredFields.Any(field =>
                field.FieldType.IsGenericType &&
                field.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>) &&
                (field.FieldType.GetGenericArguments()[0] == typeof(string) ||
                 field.FieldType.GetGenericArguments()[0] == typeof(Type))),
            Is.False,
            "GameContext must not hide a string- or Type-keyed service map.");
        Assert.That(
            declaredPublicMethods.Any(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(string))),
            Is.False,
            "GameContext must never expose string-keyed lookup.");
        Assert.That(
            contextType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Any(property => property.GetIndexParameters().Length > 0),
            Is.False,
            "GameContext must never expose indexer lookup.");
    }

    [Test]
    public void ValidOrderedServicesBindTypedPropertiesByReference()
    {
        List<IGameService> services = CreateValidServices();

        GameContext context = CreateContext(services);

        Assert.That(context.Root, Is.SameAs(root));
        Assert.That(context.Database, Is.Null);
        Assert.That(context.Clock, Is.SameAs(services[0]));
        Assert.That(context.Scheduler, Is.SameAs(services[1]));
        Assert.That(context.Camera, Is.SameAs(services[2]));
        Assert.That(context.Navigation, Is.SameAs(services[3]));
        Assert.That(context.Lighting, Is.SameAs(services[4]));
        Assert.That(context.Dialogue, Is.SameAs(services[6]));
        Assert.That(context.GameFlow, Is.SameAs(services[7]));
        Assert.That(context.Services, Is.EqualTo(services));
    }

    [Test]
    public void MissingRequiredServicesAreRejected()
    {
        for (int missingRoleIndex = 0; missingRoleIndex < RoleNames.Length; missingRoleIndex++)
        {
            List<IGameService> services = CreateValidServices();
            services.RemoveAt(RequiredRoleIndexes[missingRoleIndex]);

            ArgumentException exception = Assert.Throws<ArgumentException>(() => CreateContext(services));
            Assert.That(exception.Message, Does.Contain("missing required service role"));
            Assert.That(exception.Message, Does.Contain(RoleNames[missingRoleIndex]));
        }
    }

    [Test]
    public void NullServiceSlotsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => CreateContext(null));

        int serviceCount = CreateValidServices().Count;
        for (int nullIndex = 0; nullIndex < serviceCount; nullIndex++)
        {
            List<IGameService> services = CreateValidServices();
            services[nullIndex] = null;

            ArgumentException exception = Assert.Throws<ArgumentException>(() => CreateContext(services));
            Assert.That(exception.Message, Does.Contain($"slot {nullIndex} is null"));
        }
    }

    [Test]
    public void DuplicateInvalidClaimsAndFailedRootCompositionAreRejected()
    {
        List<IGameService> duplicateInstance = CreateValidServices();
        duplicateInstance[1] = duplicateInstance[0];
        Assert.That(
            Assert.Throws<ArgumentException>(() => CreateContext(duplicateInstance)).Message,
            Does.Contain("appears more than once"));

        List<IGameService> duplicateRole = CreateValidServices();
        duplicateRole[1] = new AlternateClockService();
        Assert.That(
            Assert.Throws<ArgumentException>(() => CreateContext(duplicateRole)).Message,
            Does.Contain("more than one clock service"));

        List<IGameService> multipleRoles = CreateValidServices();
        multipleRoles[0] = new ClockAndSchedulerService();
        Assert.That(
            Assert.Throws<ArgumentException>(() => CreateContext(multipleRoles)).Message,
            Does.Contain("more than one composition role"));

        List<IGameService> additionalTransitionalService = CreateValidServices();
        UnrecognizedService extraService = new UnrecognizedService(650);
        additionalTransitionalService.Insert(6, extraService);
        GameContext expandedContext = CreateContext(additionalTransitionalService);
        Assert.That(expandedContext.Services, Has.Count.EqualTo(9));
        Assert.That(expandedContext.Services, Does.Contain(extraService));

        UnbindProbe probe = rootObject.AddComponent<UnbindProbe>();
        SetPrivateField(root, "sceneBehaviours", new List<ChateauBehaviour> { probe });
        LogAssert.Expect(LogType.Log, "Chateau GameRoot validation failed.");
        LogAssert.Expect(
            LogType.Warning,
            "GameRoot has no GameDatabase yet. This is allowed during migration but must be resolved before the legacy data paths are removed.");
        LogAssert.Expect(LogType.Error, new Regex("GameContext is missing required service role"));
        LogAssert.Expect(LogType.Exception, new Regex("ArgumentException: GameContext is missing required service role"));

        Assert.That(root.InitializeNow(), Is.False);
        Assert.That(root.IsInitialized, Is.False);
        Assert.That(root.Context, Is.Null);
        Assert.That(probe.HasGameContext, Is.False);
        Assert.That(probe.UnbindCount, Is.Zero, "Never-bound behaviours must not receive a null unbind callback.");
    }

    [Test]
    public void IncorrectInitializationOrderIsRejected()
    {
        List<IGameService> swappedServices = CreateValidServices();
        (swappedServices[0], swappedServices[1]) = (swappedServices[1], swappedServices[0]);
        Assert.That(
            Assert.Throws<ArgumentException>(() => CreateContext(swappedServices)).Message,
            Does.Contain("service order is invalid"));

        List<IGameService> wrongDeclaredOrder = CreateValidServices();
        wrongDeclaredOrder[0] = new IncorrectlyOrderedClockService();
        ArgumentException orderException = Assert.Throws<ArgumentException>(() => CreateContext(wrongDeclaredOrder));
        Assert.That(orderException.Message, Does.Contain("must declare initialization order 100"));
        Assert.That(orderException.Message, Does.Contain("declared 999"));
    }

    [Test]
    public void ConstructionSnapshotsWithoutInitializingOrMutatingServices()
    {
        List<IGameService> services = CreateValidServices();
        IGameService originalClock = services[0];

        GameContext context = CreateContext(services);
        services[0] = new AlternateClockService();

        Assert.That(context.Clock, Is.SameAs(originalClock));
        Assert.That(context.Services, Has.Count.EqualTo(8));
        Assert.That(context.Services[0], Is.SameAs(originalClock));
        Assert.That(context.Services.All(service => !service.IsInitialized), Is.True);
        Assert.That(context.Services.Cast<FakeService>().All(service => service.InitializeCount == 0), Is.True);

        IList<IGameService> listSurface = context.Services as IList<IGameService>;
        Assert.That(listSurface, Is.Not.Null);
        Assert.That(listSurface.IsReadOnly, Is.True);
        Assert.Throws<NotSupportedException>(() => listSurface[0] = new AlternateClockService());
    }

    private GameContext CreateContext(IReadOnlyList<IGameService> services)
    {
        ConstructorInfo constructor = typeof(GameContext).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { typeof(GameRoot), typeof(GameDatabase), typeof(IReadOnlyList<IGameService>) },
            null);
        Assert.That(constructor, Is.Not.Null);

        try
        {
            return (GameContext)constructor.Invoke(new object[] { root, null, services });
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static List<IGameService> CreateValidServices()
    {
        return new List<IGameService>
        {
            new ClockService(),
            new SchedulerService(),
            new CameraService(),
            new NavigationService(),
            new LightingService(),
            new TransitionalSubtitleService(),
            new DialogueService(),
            new GameFlowService()
        };
    }

    private static void SetPrivateField(object owner, string fieldName, object value)
    {
        FieldInfo field = owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(owner, value);
    }

    private abstract class FakeService : IGameService
    {
        protected FakeService(int initializationOrder)
        {
            InitializationOrder = initializationOrder;
        }

        public int InitializationOrder { get; }
        public bool IsInitialized { get; private set; }
        public int InitializeCount { get; private set; }

        public void Initialize(GameContext context)
        {
            InitializeCount++;
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

    private sealed class ClockService : FakeService, IClockService
    {
        public ClockService() : base(100) { }
    }

    private sealed class AlternateClockService : FakeService, IClockService
    {
        public AlternateClockService() : base(100) { }
    }

    private sealed class IncorrectlyOrderedClockService : FakeService, IClockService
    {
        public IncorrectlyOrderedClockService() : base(999) { }
    }

    private sealed class SchedulerService : FakeService, ISchedulerService
    {
        public SchedulerService() : base(200) { }
    }

    private sealed class CameraService : FakeService, ICameraService
    {
        public CameraService() : base(300) { }
    }

    private sealed class NavigationService : FakeService, INavigationRuntimeService
    {
        public NavigationService() : base(400) { }
    }

    private sealed class LightingService : FakeService, ILightingService
    {
        public LightingService() : base(500) { }
    }

    private sealed class TransitionalSubtitleService : FakeService
    {
        public TransitionalSubtitleService() : base(600) { }
    }

    private sealed class DialogueService : FakeService, IDialogueService
    {
        public DialogueService() : base(700) { }
    }

    private sealed class GameFlowService : FakeService, IGameFlowService
    {
        public GameFlowService() : base(800) { }
    }

    private sealed class ClockAndSchedulerService : FakeService, IClockService, ISchedulerService
    {
        public ClockAndSchedulerService() : base(100) { }
    }

    private sealed class UnrecognizedService : FakeService
    {
        public UnrecognizedService(int order) : base(order) { }
    }

    private sealed class UnbindProbe : ChateauBehaviour
    {
        public int UnbindCount { get; private set; }

        protected override void OnGameContextUnbinding(GameContext context)
        {
            UnbindCount++;
        }
    }
}
#endif
