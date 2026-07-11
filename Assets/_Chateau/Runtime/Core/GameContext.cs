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
            this.services = services ?? Array.Empty<IGameService>();
        }

        public GameRoot Root { get; }
        public GameDatabase Database { get; }
        public IReadOnlyList<IGameService> Services => services;
    }
}
