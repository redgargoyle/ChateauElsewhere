using System;

namespace Chateau.Architecture
{
    /// <summary>
    /// Base for scene-level services serialized by GameRoot. The base owns deterministic
    /// initialization and shutdown; concrete services own exactly one domain.
    /// </summary>
    public abstract class GameServiceBase : ChateauBehaviour, IGameService
    {
        public virtual int InitializationOrder => 0;
        public bool IsInitialized { get; private set; }

        public void Initialize(GameContext context)
        {
            if (IsInitialized)
            {
                return;
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            BindGameContext(context);

            try
            {
                OnInitialize(context);
                IsInitialized = true;
            }
            catch
            {
                UnbindGameContext(context);
                throw;
            }
        }

        public void Shutdown(GameContext context)
        {
            if (!IsInitialized)
            {
                return;
            }

            try
            {
                OnShutdown(context);
            }
            finally
            {
                IsInitialized = false;
                UnbindGameContext(context);
            }
        }

        protected virtual void OnInitialize(GameContext context)
        {
        }

        protected virtual void OnShutdown(GameContext context)
        {
        }
    }
}
