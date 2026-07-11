namespace Chateau.Architecture
{
    public interface IGameService : IArchitectureValidatable
    {
        int InitializationOrder { get; }
        bool IsInitialized { get; }
        void Initialize(GameContext context);
        void Shutdown(GameContext context);
    }
}
