using System;
using System.Collections.Generic;

namespace Chateau.Architecture
{
    public sealed class StateMachine<TState>
    {
        private readonly Dictionary<TState, HashSet<TState>> allowedTransitions = new Dictionary<TState, HashSet<TState>>();

        public StateMachine(TState initialState)
        {
            Current = initialState;
        }

        public TState Current { get; private set; }
        public event Action<TState, TState, string> Transitioned;

        public StateMachine<TState> Allow(TState from, TState to)
        {
            if (!allowedTransitions.TryGetValue(from, out HashSet<TState> destinations))
            {
                destinations = new HashSet<TState>();
                allowedTransitions.Add(from, destinations);
            }

            destinations.Add(to);
            return this;
        }

        public bool CanTransitionTo(TState next)
        {
            return EqualityComparer<TState>.Default.Equals(Current, next) ||
                   (allowedTransitions.TryGetValue(Current, out HashSet<TState> destinations) && destinations.Contains(next));
        }

        public bool TryTransition(TState next, string reason = null)
        {
            if (!CanTransitionTo(next))
            {
                return false;
            }

            if (EqualityComparer<TState>.Default.Equals(Current, next))
            {
                return true;
            }

            TState previous = Current;
            Current = next;
            Transitioned?.Invoke(previous, next, reason ?? string.Empty);
            return true;
        }

        public void TransitionOrThrow(TState next, string reason = null)
        {
            if (!TryTransition(next, reason))
            {
                throw new InvalidOperationException($"Invalid state transition from '{Current}' to '{next}'.");
            }
        }
    }
}
