using System;

namespace Chateau.Architecture
{
    public abstract class StoryBeatBase
    {
        protected StoryBeatBase(string beatId)
        {
            BeatId = string.IsNullOrWhiteSpace(beatId)
                ? throw new ArgumentException("A story beat requires a stable ID.", nameof(beatId))
                : beatId.Trim();
        }

        public string BeatId { get; }
        public bool IsComplete { get; protected set; }

        public abstract void Enter();
        public abstract void Tick(float unscaledDeltaTime);
        public abstract void Cancel();
    }
}
