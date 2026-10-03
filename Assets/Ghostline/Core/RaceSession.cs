using System.Collections.Generic;

namespace Ghostline.Core
{
    /// <summary>Coordinates one lap attempt, ordered checkpoints, and its ghost recording.</summary>
    public sealed class RaceSession
    {
        private readonly GhostRecorder _recorder;
        private readonly List<float> _splits = new List<float>();

        /// <summary>Creates a race with a positive checkpoint count and recording interval.</summary>
        public RaceSession(int checkpointCount, float sampleInterval = 0.05f)
        {
            Timer = new LapTimer();
            Checkpoints = new CheckpointTracker(checkpointCount);
            _recorder = new GhostRecorder(sampleInterval);
            Splits = _splits.AsReadOnly();
        }

        /// <summary>Gets the attempt's lap timer.</summary>
        public LapTimer Timer { get; }
        /// <summary>Gets the attempt's ordered checkpoint progress.</summary>
        public CheckpointTracker Checkpoints { get; }
        /// <summary>Gets accepted checkpoint entry times in gate order.</summary>
        public IReadOnlyList<float> Splits { get; }
        /// <summary>Gets completed lap data, or null until a valid finish crossing.</summary>
        public BestLapData CompletedLap { get; private set; }

        /// <summary>Starts at the first crossing or finishes after all checkpoints; returns whether accepted.</summary>
        public bool CrossStartFinish(float x, float y, float rotation)
        {
            if (Timer.State == LapTimerState.NotStarted)
            {
                _recorder.Begin(x, y, rotation);
                return Timer.Start();
            }
            if (Timer.State != LapTimerState.Running || !Checkpoints.TryCompleteLap())
                return false;
            GhostRecording recording = _recorder.Complete(Timer.ElapsedTime, x, y, rotation);
            CompletedLap = new BestLapData
            {
                LapTime = Timer.ElapsedTime,
                Splits = _splits.ToArray(),
                Samples = new List<GhostSample>(recording.Samples)
            };
            Timer.Finish();
            return true;
        }

        /// <summary>Accepts a checkpoint only during a running attempt.</summary>
        public bool PassCheckpoint(int checkpointIndex)
        {
            if (Timer.State != LapTimerState.Running || !Checkpoints.TryPass(checkpointIndex))
                return false;
            _splits.Add(Timer.ElapsedTime);
            return true;
        }

        /// <summary>Advances the timer and supplies the car pose at the same lap time.</summary>
        public void Tick(float deltaTime, float x, float y, float rotation)
        {
            Timer.Tick(deltaTime);
            if (Timer.State == LapTimerState.Running)
                _recorder.Record(Timer.ElapsedTime, x, y, rotation);
        }

        /// <summary>Clears attempt progress and completed data without affecting saved laps.</summary>
        public void Reset()
        {
            Timer.Reset();
            Checkpoints.Reset();
            _splits.Clear();
            CompletedLap = null;
        }
    }
}
