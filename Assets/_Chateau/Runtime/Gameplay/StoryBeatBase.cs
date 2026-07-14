using System;

namespace Chateau.Architecture
{
    public abstract class StoryBeatBase
    {
        private readonly BeatId id;

        protected StoryBeatBase(string beatId)
        {
            BeatId = string.IsNullOrWhiteSpace(beatId)
                ? throw new ArgumentException("A story beat requires a stable ID.", nameof(beatId))
                : beatId.Trim();
            global::Chateau.Architecture.BeatId.TryParse(
                BeatId,
                out global::Chateau.Architecture.BeatId parsedId);
            id = parsedId;
        }

        protected StoryBeatBase(BeatId beatId)
        {
            if (!beatId.IsValid)
            {
                throw new ArgumentException("A story beat requires a valid typed stable ID.", nameof(beatId));
            }

            id = beatId;
            BeatId = beatId.Value;
        }

        public string BeatId { get; }
        public BeatId Id => id.IsValid
            ? id
            : throw new InvalidOperationException(
                $"Story beat '{BeatId}' uses the legacy string compatibility path and has no typed BeatId.");
        public bool IsComplete { get; protected set; }

        public bool TryGetId(out BeatId beatId)
        {
            beatId = id;
            return beatId.IsValid;
        }

        public abstract void Enter();
        public abstract void Tick(float unscaledDeltaTime);
        public abstract void Cancel();
    }
}
