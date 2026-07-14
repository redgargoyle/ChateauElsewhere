namespace Chateau.Architecture
{
    public interface IGameService : IArchitectureValidatable
    {
        int InitializationOrder { get; }
        bool IsInitialized { get; }
        void Initialize(GameContext context);
        void Shutdown(GameContext context);
    }

    // These narrow roles make the composition contract explicit without making Core
    // depend on migration-era concrete services. Domain behavior stays on the owning
    // service API until its dedicated migration slice introduces a domain contract.
    public interface IClockService
    {
    }

    public interface ISchedulerService
    {
    }

    public interface ICameraService
    {
    }

    public interface INavigationRuntimeService
    {
    }

    public interface ILightingService
    {
    }

    public interface IDialogueService
    {
    }

    public interface IGameFlowService
    {
    }

    public static class GameServiceInitializationOrder
    {
        public const int Clock = 100;
        public const int Scheduler = 200;
        public const int Camera = 300;
        public const int Navigation = 400;
        public const int Lighting = 500;
        public const int TransitionalSubtitlePresentation = 600;
        public const int Dialogue = 700;
        public const int GameFlow = 800;
    }
}
