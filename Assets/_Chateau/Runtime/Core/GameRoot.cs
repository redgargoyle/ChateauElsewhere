using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chateau.Architecture
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class GameRoot : MonoBehaviour, IArchitectureValidatable
    {
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
        private bool initialized;

        public GameContext Context => context;
        public bool IsInitialized => initialized;
        public GameDatabase Database => gameDatabase;
        public IReadOnlyList<GameServiceBase> Services => services;

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
            if (initialized)
            {
                return true;
            }

            ValidationReport report = new ValidationReport();
            ValidateConfiguration(report);

            if (report.HasErrors)
            {
                report.LogToUnity("Chateau GameRoot validation failed.");

                if (failStartupOnValidationErrors)
                {
                    enabled = false;
                    return false;
                }
            }

            List<GameServiceBase> orderedServices = BuildOrderedServiceList();
            List<IGameService> serviceInterfaces = new List<IGameService>(orderedServices.Count);

            for (int i = 0; i < orderedServices.Count; i++)
            {
                serviceInterfaces.Add(orderedServices[i]);
            }

            try
            {
                context = new GameContext(this, gameDatabase, serviceInterfaces);
                BindSceneBehaviours();

                for (int i = 0; i < orderedServices.Count; i++)
                {
                    GameServiceBase service = orderedServices[i];
                    service.Initialize(context);
                    initializedServices.Add(service);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                ShutdownInitializedServices();

                if (context != null)
                {
                    UnbindSceneBehaviours();
                }

                context = null;
                enabled = false;
                return false;
            }

            initialized = true;

            if (logSuccessfulInitialization)
            {
                Debug.Log($"Chateau GameRoot initialized {initializedServices.Count} services.", this);
            }

            return true;
        }

        public void ShutdownNow()
        {
            if (context == null && initializedServices.Count == 0)
            {
                initialized = false;
                return;
            }

            ShutdownInitializedServices();

            UnbindSceneBehaviours();

            context = null;
            initialized = false;
        }

        public void ValidateConfiguration(ValidationReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (gameDatabase == null)
            {
                report.AddWarning("GameRoot has no GameDatabase yet. This is allowed during migration but must be resolved before the legacy data paths are removed.", this);
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
                    report.AddWarning($"GameRoot scene-behaviour slot {i} is null.", this);
                    continue;
                }

                if (!uniqueBehaviours.Add(behaviour))
                {
                    report.AddWarning($"Scene behaviour '{behaviour.name}' appears more than once in GameRoot.", behaviour);
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
                    behaviour.UnbindGameContext(context);
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
