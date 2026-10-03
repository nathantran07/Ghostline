using System;

namespace Ghostline.Core
{
    /// <summary>The lifecycle of a single lap attempt.</summary>
    public enum LapTimerState
    {
        /// <summary>The car has not crossed the start line.</summary>
        NotStarted,
        /// <summary>The lap is accumulating time.</summary>
        Running,
        /// <summary>The completed lap time is frozen until reset.</summary>
        Finished
    }

    /// <summary>Measures a lap using caller-supplied time, independently of the engine.</summary>
    public sealed class LapTimer
    {
        /// <summary>Raised once when an attempt starts.</summary>
        public event Action Started;
        /// <summary>Raised once with the completed lap time.</summary>
        public event Action<float> Finished;
        /// <summary>Raised when the attempt is reset.</summary>
        public event Action ResetOccurred;

        /// <summary>Gets the current lifecycle state.</summary>
        public LapTimerState State { get; private set; }
        /// <summary>Gets elapsed seconds in the current attempt.</summary>
        public float ElapsedTime { get; private set; }

        /// <summary>Starts an unstarted attempt; returns false for other states.</summary>
        public bool Start()
        {
            if (State != LapTimerState.NotStarted)
                return false;
            State = LapTimerState.Running;
            Started?.Invoke();
            return true;
        }

        /// <summary>Accumulates finite, nonnegative seconds only while running.</summary>
        public void Tick(float deltaTime)
        {
            NumericGuard.Nonnegative(deltaTime, nameof(deltaTime));
            if (State == LapTimerState.Running)
            {
                float nextTime = ElapsedTime + deltaTime;
                NumericGuard.Nonnegative(nextTime, nameof(deltaTime));
                ElapsedTime = nextTime;
            }
        }

        /// <summary>Freezes a running attempt and raises Finished; otherwise returns false.</summary>
        public bool Finish()
        {
            if (State != LapTimerState.Running)
                return false;
            State = LapTimerState.Finished;
            Finished?.Invoke(ElapsedTime);
            return true;
        }

        /// <summary>Clears elapsed time and returns to NotStarted.</summary>
        public void Reset()
        {
            ElapsedTime = 0f;
            State = LapTimerState.NotStarted;
            ResetOccurred?.Invoke();
        }
    }
}
