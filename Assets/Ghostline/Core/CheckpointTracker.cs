using System;

namespace Ghostline.Core
{
    /// <summary>Accepts zero-based checkpoints in order, followed by a separate finish crossing.</summary>
    public sealed class CheckpointTracker
    {
        private readonly int _checkpointCount;
        private bool _completed;

        /// <summary>Creates a tracker with a positive number of checkpoints.</summary>
        public CheckpointTracker(int checkpointCount)
        {
            if (checkpointCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(checkpointCount));
            _checkpointCount = checkpointCount;
        }

        /// <summary>Raised once when all checkpoints and the finish crossing are accepted.</summary>
        public event Action LapCompleted;
        /// <summary>Gets the next expected checkpoint, or the count when all have been passed.</summary>
        public int NextCheckpointIndex { get; private set; }

        /// <summary>Accepts only the next checkpoint; rejects duplicates, invalid indices, and skips.</summary>
        public bool TryPass(int checkpointIndex)
        {
            if (_completed || checkpointIndex != NextCheckpointIndex || checkpointIndex >= _checkpointCount)
                return false;
            NextCheckpointIndex++;
            return true;
        }

        /// <summary>Completes the lap once after all ordered checkpoints have been accepted.</summary>
        public bool TryCompleteLap()
        {
            if (_completed || NextCheckpointIndex != _checkpointCount)
                return false;
            _completed = true;
            LapCompleted?.Invoke();
            return true;
        }

        /// <summary>Clears checkpoint progress for a new attempt.</summary>
        public void Reset()
        {
            NextCheckpointIndex = 0;
            _completed = false;
        }
    }
}
