using System;
using System.Runtime.ExceptionServices;

namespace Chateau.Architecture
{
    /// <summary>
    /// Base for scene-level services serialized by GameRoot. The base owns deterministic
    /// initialization and shutdown; concrete services own exactly one domain.
    /// </summary>
    public abstract class GameServiceBase : ChateauBehaviour, IGameService
    {
        private bool isInitializing;
        private bool isShuttingDown;

        public virtual int InitializationOrder => 0;
        public bool IsInitialized { get; private set; }

        public void Initialize(GameContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (isInitializing || isShuttingDown)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} cannot initialize while it is " +
                    (isInitializing ? "initializing." : "shutting down."));
            }

            if (IsInitialized)
            {
                if (IsBoundToGameContext(context))
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"{GetType().Name} is already initialized for a different GameContext.");
            }

            isInitializing = true;

            try
            {
                BindGameContext(context);
                OnInitialize(context);
                IsInitialized = true;
            }
            catch (Exception initializationException)
            {
                try
                {
                    UnbindGameContext(context);
                }
                catch (Exception unbindingException)
                {
                    throw new AggregateException(
                        $"{GetType().Name} failed to initialize and then failed to release its GameContext.",
                        initializationException,
                        unbindingException);
                }

                throw;
            }
            finally
            {
                isInitializing = false;
            }
        }

        public void Shutdown(GameContext context)
        {
            if (isShuttingDown)
            {
                return;
            }

            if (isInitializing)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} cannot shut down while it is initializing.");
            }

            if (!IsInitialized)
            {
                return;
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!IsBoundToGameContext(context))
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} cannot shut down through a different GameContext.");
            }

            isShuttingDown = true;
            Exception shutdownException = null;

            try
            {
                OnShutdown(context);
            }
            catch (Exception exception)
            {
                shutdownException = exception;
            }

            try
            {
                IsInitialized = false;

                try
                {
                    UnbindGameContext(context);
                }
                catch (Exception unbindingException)
                {
                    if (shutdownException != null)
                    {
                        throw new AggregateException(
                            $"{GetType().Name} failed to shut down and then failed to release its GameContext.",
                            shutdownException,
                            unbindingException);
                    }

                    throw;
                }

                if (shutdownException != null)
                {
                    ExceptionDispatchInfo.Capture(shutdownException).Throw();
                }
            }
            finally
            {
                isShuttingDown = false;
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
