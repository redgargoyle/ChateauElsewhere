using System;

namespace Chateau.Architecture
{
    [Serializable]
    public readonly struct GameClockState : IEquatable<GameClockState>
    {
        public GameClockState(
            float elapsedSeconds,
            bool isRunning,
            float secondsPerGameMinute,
            int startHour,
            int startMinute)
        {
            ElapsedSeconds = elapsedSeconds;
            IsRunning = isRunning;
            SecondsPerGameMinute = secondsPerGameMinute;
            StartHour = startHour;
            StartMinute = startMinute;
        }

        public float ElapsedSeconds { get; }
        public bool IsRunning { get; }
        public float SecondsPerGameMinute { get; }
        public int StartHour { get; }
        public int StartMinute { get; }

        public bool Equals(GameClockState other)
        {
            return ElapsedSeconds.Equals(other.ElapsedSeconds) &&
                IsRunning == other.IsRunning &&
                SecondsPerGameMinute.Equals(other.SecondsPerGameMinute) &&
                StartHour == other.StartHour &&
                StartMinute == other.StartMinute;
        }

        public override bool Equals(object obj)
        {
            return obj is GameClockState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = ElapsedSeconds.GetHashCode();
                hashCode = (hashCode * 397) ^ IsRunning.GetHashCode();
                hashCode = (hashCode * 397) ^ SecondsPerGameMinute.GetHashCode();
                hashCode = (hashCode * 397) ^ StartHour;
                hashCode = (hashCode * 397) ^ StartMinute;
                return hashCode;
            }
        }
    }

    public interface IGameService : IArchitectureValidatable
    {
        int InitializationOrder { get; }
        bool IsInitialized { get; }
        void Initialize(GameContext context);
        void Shutdown(GameContext context);
    }

    public interface IClockService
    {
        event Action TimeAdvanced;

        float ElapsedSeconds { get; }
        bool IsRunning { get; }
        float SecondsPerGameMinute { get; }
        float ElapsedGameMinutes { get; }
        int StartTotalMinutes { get; }
        int CurrentTotalMinutes { get; }
        int CurrentHour { get; }
        int CurrentMinute { get; }
        string CurrentTimeLabel { get; }

        void ResetClock();
        void SetStartTime(int hour, int minute);
        void SetSecondsPerGameMinute(float value);
        GameClockState CaptureState();
        void RestoreState(GameClockState state);
        void StartClock();
        void StopClock();
    }

    public interface ISchedulerService
    {
        int PendingEventCount { get; }

        bool ScheduleOneShot(string eventId, float delaySeconds, Action callback);
        bool ScheduleOneShotAtClockTime(string eventId, int hour, int minute, Action callback);
        bool Cancel(string eventId);
        void Clear();
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
