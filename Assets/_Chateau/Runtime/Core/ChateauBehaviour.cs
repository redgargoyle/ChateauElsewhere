using System;
using UnityEngine;

namespace Chateau.Architecture
{
    /// <summary>
    /// Small root for Chateau runtime components. It provides explicit GameContext binding
    /// and validation only. It intentionally does not perform scene searches or expose
    /// singleton/service-locator helpers.
    /// </summary>
    public abstract class ChateauBehaviour : MonoBehaviour, IArchitectureValidatable
    {
        [NonSerialized] private GameContext gameContext;

        public bool HasGameContext => gameContext != null;

        protected GameContext GameContext
        {
            get
            {
                if (gameContext == null)
                {
                    throw new InvalidOperationException($"{GetType().Name} has not been bound to a GameContext.");
                }

                return gameContext;
            }
        }

        internal void BindGameContext(GameContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (ReferenceEquals(gameContext, context))
            {
                return;
            }

            if (gameContext != null)
            {
                throw new InvalidOperationException($"{GetType().Name} is already bound to another GameContext.");
            }

            gameContext = context;
            OnGameContextBound(context);
        }

        internal void UnbindGameContext(GameContext context)
        {
            if (!ReferenceEquals(gameContext, context))
            {
                return;
            }

            OnGameContextUnbinding(context);
            gameContext = null;
        }

        protected virtual void OnGameContextBound(GameContext context)
        {
        }

        protected virtual void OnGameContextUnbinding(GameContext context)
        {
        }

        public virtual void ValidateConfiguration(ValidationReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }
        }
    }
}
