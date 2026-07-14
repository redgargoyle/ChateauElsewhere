using System;
using System.Collections.Generic;

namespace Chateau.Architecture
{
    /// <summary>
    /// Explicit runtime context created by GameRoot. Dependencies should still be visible
    /// as serialized fields or constructor/initializer arguments; this object is not a
    /// global singleton and does not expose string-key lookup.
    /// </summary>
    public sealed class GameContext
    {
        private readonly IReadOnlyList<IGameService> services;

        internal GameContext(GameRoot root, GameDatabase database, IReadOnlyList<IGameService> services)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Database = database;

            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            IGameService[] snapshot = new IGameService[services.Count];
            HashSet<IGameService> uniqueInstances = new HashSet<IGameService>();
            IClockService clock = null;
            ISchedulerService scheduler = null;
            ICameraService camera = null;
            INavigationRuntimeService navigation = null;
            ILightingService lighting = null;
            IDialogueService dialogue = null;
            IGameFlowService gameFlow = null;

            for (int i = 0; i < services.Count; i++)
            {
                IGameService service = services[i];
                snapshot[i] = service;

                if (service == null)
                {
                    throw new ArgumentException($"GameContext service slot {i} is null.", nameof(services));
                }

                if (!uniqueInstances.Add(service))
                {
                    throw new ArgumentException(
                        $"GameContext service instance '{service.GetType().Name}' appears more than once.",
                        nameof(services));
                }

                int claimedRoles = 0;

                if (service is IClockService clockCandidate)
                {
                    claimedRoles++;
                    if (clock != null)
                    {
                        throw DuplicateRole("clock", service);
                    }

                    clock = clockCandidate;
                }

                if (service is ISchedulerService schedulerCandidate)
                {
                    claimedRoles++;
                    if (scheduler != null)
                    {
                        throw DuplicateRole("scheduler", service);
                    }

                    scheduler = schedulerCandidate;
                }

                if (service is ICameraService cameraCandidate)
                {
                    claimedRoles++;
                    if (camera != null)
                    {
                        throw DuplicateRole("camera", service);
                    }

                    camera = cameraCandidate;
                }

                if (service is INavigationRuntimeService navigationCandidate)
                {
                    claimedRoles++;
                    if (navigation != null)
                    {
                        throw DuplicateRole("navigation", service);
                    }

                    navigation = navigationCandidate;
                }

                if (service is ILightingService lightingCandidate)
                {
                    claimedRoles++;
                    if (lighting != null)
                    {
                        throw DuplicateRole("lighting", service);
                    }

                    lighting = lightingCandidate;
                }

                if (service is IDialogueService dialogueCandidate)
                {
                    claimedRoles++;
                    if (dialogue != null)
                    {
                        throw DuplicateRole("dialogue", service);
                    }

                    dialogue = dialogueCandidate;
                }

                if (service is IGameFlowService gameFlowCandidate)
                {
                    claimedRoles++;
                    if (gameFlow != null)
                    {
                        throw DuplicateRole("game flow", service);
                    }

                    gameFlow = gameFlowCandidate;
                }

                if (claimedRoles > 1)
                {
                    throw new ArgumentException(
                        $"GameContext service '{service.GetType().Name}' declares more than one composition role.",
                        nameof(services));
                }
            }

            List<string> missingRoles = new List<string>();
            if (clock == null) missingRoles.Add("clock");
            if (scheduler == null) missingRoles.Add("scheduler");
            if (camera == null) missingRoles.Add("camera");
            if (navigation == null) missingRoles.Add("navigation");
            if (lighting == null) missingRoles.Add("lighting");
            if (dialogue == null) missingRoles.Add("dialogue");
            if (gameFlow == null) missingRoles.Add("game flow");

            if (missingRoles.Count > 0)
            {
                throw new ArgumentException(
                    $"GameContext is missing required service role(s): {string.Join(", ", missingRoles)}.",
                    nameof(services));
            }

            RequireDeclaredRoleOrder(clock, GameServiceInitializationOrder.Clock, "clock");
            RequireDeclaredRoleOrder(scheduler, GameServiceInitializationOrder.Scheduler, "scheduler");
            RequireDeclaredRoleOrder(camera, GameServiceInitializationOrder.Camera, "camera");
            RequireDeclaredRoleOrder(navigation, GameServiceInitializationOrder.Navigation, "navigation");
            RequireDeclaredRoleOrder(lighting, GameServiceInitializationOrder.Lighting, "lighting");
            RequireDeclaredRoleOrder(dialogue, GameServiceInitializationOrder.Dialogue, "dialogue");
            RequireDeclaredRoleOrder(gameFlow, GameServiceInitializationOrder.GameFlow, "game flow");
            RequireStrictlyIncreasingServiceOrder(snapshot);

            Clock = clock;
            Scheduler = scheduler;
            Camera = camera;
            Navigation = navigation;
            Lighting = lighting;
            Dialogue = dialogue;
            GameFlow = gameFlow;
            this.services = Array.AsReadOnly(snapshot);
        }

        public GameRoot Root { get; }
        public GameDatabase Database { get; }
        public IClockService Clock { get; }
        public ISchedulerService Scheduler { get; }
        public ICameraService Camera { get; }
        public INavigationRuntimeService Navigation { get; }
        public ILightingService Lighting { get; }
        public IDialogueService Dialogue { get; }
        public IGameFlowService GameFlow { get; }
        public IReadOnlyList<IGameService> Services => services;

        private static ArgumentException DuplicateRole(string role, IGameService duplicate)
        {
            return new ArgumentException(
                $"GameContext has more than one {role} service; duplicate type is '{duplicate.GetType().Name}'.",
                "services");
        }

        private static void RequireDeclaredRoleOrder(
            object role,
            int expectedInitializationOrder,
            string roleName)
        {
            if (!(role is IGameService service))
            {
                throw new ArgumentException($"GameContext {roleName} role is not an initialized service.", "services");
            }

            if (service.InitializationOrder != expectedInitializationOrder)
            {
                throw new ArgumentException(
                    $"GameContext {roleName} service must declare initialization order {expectedInitializationOrder} " +
                    $"but declared {service.InitializationOrder}.",
                    "services");
            }
        }

        private static void RequireStrictlyIncreasingServiceOrder(IGameService[] orderedServices)
        {
            for (int i = 1; i < orderedServices.Length; i++)
            {
                IGameService previous = orderedServices[i - 1];
                IGameService current = orderedServices[i];

                if (previous.InitializationOrder >= current.InitializationOrder)
                {
                    throw new ArgumentException(
                        "GameContext service order is invalid: " +
                        $"'{previous.GetType().Name}' ({previous.InitializationOrder}) must precede " +
                        $"'{current.GetType().Name}' ({current.InitializationOrder}) with no ties.",
                        nameof(orderedServices));
                }
            }
        }
    }
}
