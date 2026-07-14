using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chateau.Architecture
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class GameRoot : MonoBehaviour, IArchitectureValidatable
    {
        private enum LifecycleState
        {
            Uninitialized,
            Initializing,
            Initialized,
            ShuttingDown
        }

        [Header("Composition Root")]
        [SerializeField] private GameDatabase gameDatabase;
        [SerializeField] private List<GameServiceBase> services = new List<GameServiceBase>();
        [SerializeField] private List<ChateauBehaviour> sceneBehaviours = new List<ChateauBehaviour>();

        [Header("Migration Safety")]
        [SerializeField] private bool initializeOnAwake = true;
        [SerializeField] private bool failStartupOnValidationErrors;
        [SerializeField] private bool logSuccessfulInitialization;

        private readonly List<GameServiceBase> initializedServices = new List<GameServiceBase>();
        private GameContext context;
        private LifecycleState lifecycleState;

        public GameContext Context => context;
        public bool IsInitialized => lifecycleState == LifecycleState.Initialized;
        public GameDatabase Database => gameDatabase;
        public IReadOnlyList<GameServiceBase> Services => services;
        public bool FailsStartupOnValidationErrors => failStartupOnValidationErrors;

        private void Awake()
        {
            if (initializeOnAwake)
            {
                InitializeNow();
            }
        }

        private void OnDestroy()
        {
            ShutdownNow();
        }

        public bool InitializeNow()
        {
            if (lifecycleState == LifecycleState.Initialized)
            {
                return true;
            }

            if (lifecycleState != LifecycleState.Uninitialized)
            {
                throw new InvalidOperationException(
                    $"GameRoot cannot initialize while it is {lifecycleState}.");
            }

            lifecycleState = LifecycleState.Initializing;
            ValidationReport report = new ValidationReport();

            try
            {
                ValidateConfiguration(report);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                lifecycleState = LifecycleState.Uninitialized;
                enabled = false;
                return false;
            }

            if (report.HasErrors)
            {
                report.LogToUnity("Chateau GameRoot validation failed.");

                if (failStartupOnValidationErrors)
                {
                    lifecycleState = LifecycleState.Uninitialized;
                    enabled = false;
                    return false;
                }
            }

            try
            {
                List<GameServiceBase> orderedServices = BuildOrderedServiceList();
                List<IGameService> serviceInterfaces = new List<IGameService>(orderedServices.Count);

                for (int i = 0; i < orderedServices.Count; i++)
                {
                    serviceInterfaces.Add(orderedServices[i]);
                }

                context = new GameContext(this, gameDatabase, serviceInterfaces);

                for (int i = 0; i < orderedServices.Count; i++)
                {
                    GameServiceBase service = orderedServices[i];
                    service.Initialize(context);
                    initializedServices.Add(service);
                }

                BindSceneBehaviours();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                lifecycleState = LifecycleState.ShuttingDown;

                try
                {
                    if (context != null)
                    {
                        UnbindSceneBehaviours();
                    }
                }
                finally
                {
                    ShutdownInitializedServices();
                    context = null;
                    lifecycleState = LifecycleState.Uninitialized;
                    enabled = false;
                }

                return false;
            }

            lifecycleState = LifecycleState.Initialized;

            if (logSuccessfulInitialization)
            {
                Debug.Log($"Chateau GameRoot initialized {initializedServices.Count} services.", this);
            }

            return true;
        }

        public void ShutdownNow()
        {
            if (lifecycleState == LifecycleState.ShuttingDown)
            {
                return;
            }

            if (lifecycleState == LifecycleState.Initializing)
            {
                throw new InvalidOperationException("GameRoot cannot shut down while it is initializing.");
            }

            if (context == null && initializedServices.Count == 0)
            {
                lifecycleState = LifecycleState.Uninitialized;
                return;
            }

            lifecycleState = LifecycleState.ShuttingDown;

            try
            {
                UnbindSceneBehaviours();
            }
            finally
            {
                ShutdownInitializedServices();
                context = null;
                lifecycleState = LifecycleState.Uninitialized;
            }
        }

        public void ValidateConfiguration(ValidationReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (gameDatabase == null)
            {
                report.AddError("GameRoot requires its serialized GameDatabase.", this);
            }
            else
            {
                gameDatabase.ValidateConfiguration(report);
            }

            HashSet<GameServiceBase> uniqueServices = new HashSet<GameServiceBase>();
            HashSet<Type> uniqueServiceTypes = new HashSet<Type>();

            for (int i = 0; i < services.Count; i++)
            {
                GameServiceBase service = services[i];

                if (service == null)
                {
                    report.AddError($"GameRoot service slot {i} is null.", this);
                    continue;
                }

                if (!uniqueServices.Add(service))
                {
                    report.AddError($"Service instance '{service.name}' appears more than once in GameRoot.", service);
                }

                if (!uniqueServiceTypes.Add(service.GetType()))
                {
                    report.AddError($"More than one service of type {service.GetType().Name} is registered. One domain must have one owner.", service);
                }

                if (service.gameObject.scene != gameObject.scene)
                {
                    report.AddError($"Service '{service.name}' belongs to another scene.", service);
                }

                if ((service.IsInitialized || service.HasGameContext) &&
                    !service.IsBoundToGameContext(context))
                {
                    report.AddError($"Service '{service.name}' is already initialized or bound by another GameRoot.", service);
                }

                service.ValidateConfiguration(report);
            }

            List<GameServiceBase> orderedServices = BuildOrderedServiceList();
            List<IGameService> serviceInterfaces = new List<IGameService>(orderedServices.Count);

            for (int i = 0; i < orderedServices.Count; i++)
            {
                serviceInterfaces.Add(orderedServices[i]);
            }

            try
            {
                _ = new GameContext(this, gameDatabase, serviceInterfaces);
            }
            catch (ArgumentException exception)
            {
                report.AddError(exception.Message, this);
            }

            HashSet<ChateauBehaviour> uniqueBehaviours = new HashSet<ChateauBehaviour>();

            for (int i = 0; i < sceneBehaviours.Count; i++)
            {
                ChateauBehaviour behaviour = sceneBehaviours[i];

                if (behaviour == null)
                {
                    report.AddError($"GameRoot scene-behaviour slot {i} is null.", this);
                    continue;
                }

                if (!uniqueBehaviours.Add(behaviour))
                {
                    report.AddError($"Scene behaviour '{behaviour.name}' appears more than once in GameRoot.", behaviour);
                }

                if (behaviour is IGameService)
                {
                    report.AddError($"Service '{behaviour.name}' must be registered in services, not scene behaviours.", behaviour);
                }

                if (behaviour.gameObject.scene != gameObject.scene)
                {
                    report.AddError($"Scene behaviour '{behaviour.name}' belongs to another scene.", behaviour);
                }

                if (behaviour.HasGameContext && !behaviour.IsBoundToGameContext(context))
                {
                    report.AddError($"Scene behaviour '{behaviour.name}' is already bound to another GameRoot.", behaviour);
                }

                behaviour.ValidateConfiguration(report);
            }
        }


        private void BindSceneBehaviours()
        {
            for (int i = 0; i < sceneBehaviours.Count; i++)
            {
                ChateauBehaviour behaviour = sceneBehaviours[i];

                if (behaviour != null && !(behaviour is IGameService))
                {
                    behaviour.BindGameContext(context);
                }
            }
        }

        private void UnbindSceneBehaviours()
        {
            for (int i = sceneBehaviours.Count - 1; i >= 0; i--)
            {
                ChateauBehaviour behaviour = sceneBehaviours[i];

                if (behaviour != null && !(behaviour is IGameService))
                {
                    try
                    {
                        behaviour.UnbindGameContext(context);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, behaviour);
                    }
                }
            }
        }

        private List<GameServiceBase> BuildOrderedServiceList()
        {
            List<GameServiceBase> ordered = new List<GameServiceBase>();

            for (int i = 0; i < services.Count; i++)
            {
                if (services[i] != null && !ordered.Contains(services[i]))
                {
                    ordered.Add(services[i]);
                }
            }

            ordered.Sort((left, right) =>
            {
                int orderComparison = left.InitializationOrder.CompareTo(right.InitializationOrder);
                return orderComparison != 0
                    ? orderComparison
                    : string.CompareOrdinal(left.GetType().FullName, right.GetType().FullName);
            });

            return ordered;
        }

        private void ShutdownInitializedServices()
        {
            for (int i = initializedServices.Count - 1; i >= 0; i--)
            {
                GameServiceBase service = initializedServices[i];

                if (service == null)
                {
                    continue;
                }

                try
                {
                    service.Shutdown(context);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, service);
                }
            }

            initializedServices.Clear();
        }
    }
}
