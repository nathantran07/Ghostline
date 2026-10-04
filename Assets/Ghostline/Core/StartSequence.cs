using System;

namespace Ghostline.Core
{
    /// <summary>The presentation and input phases of a standing start.</summary>
    public enum StartSequenceState
    {
        /// <summary>Accumulates red start lamps with driving locked.</summary>
        Counting,
        /// <summary>Shows lights out with driving allowed.</summary>
        Go,
        /// <summary>Hides the gantry with driving still allowed.</summary>
        Done
    }

    /// <summary>Runs a countdown on supplied seconds without starting the lap timer.</summary>
    public sealed class StartSequence
    {
        private readonly double _countDuration;
        private readonly double _goDuration;
        private readonly double _tolerance;
        private double _elapsed;

        /// <summary>Creates a sequence with positive finite seconds per number and for GO.</summary>
        public StartSequence(float countDuration = 1f, float goDuration = 0.75f)
        {
            NumericGuard.Nonnegative(countDuration, nameof(countDuration));
            NumericGuard.Nonnegative(goDuration, nameof(goDuration));
            if (countDuration == 0f)
                throw new ArgumentOutOfRangeException(nameof(countDuration));
            if (goDuration == 0f)
                throw new ArgumentOutOfRangeException(nameof(goDuration));
            _countDuration = countDuration;
            _goDuration = goDuration;
            // Accommodate float fixed-step representation at an exact duration boundary.
            _tolerance = Math.Min(_countDuration, _goDuration) * 0.00001d;
        }

        /// <summary>Gets the phase at the accumulated time.</summary>
        public StartSequenceState State => !HasReached(3d * _countDuration) ? StartSequenceState.Counting
            : !HasReached(3d * _countDuration + _goDuration) ? StartSequenceState.Go : StartSequenceState.Done;

        /// <summary>Gets legacy 3, 2, 1, GO, or empty labels for the unchanged audio schedule.</summary>
        public string Label => State == StartSequenceState.Done ? string.Empty
            : State == StartSequenceState.Go ? "GO"
            : HasReached(2d * _countDuration) ? "1" : HasReached(_countDuration) ? "2" : "3";

        /// <summary>Gets cumulative gantry lamps; lights out at the existing driving-unlock boundary.</summary>
        public int LitLampCount
        {
            get
            {
                if (State != StartSequenceState.Counting)
                    return 0;
                int count = 1;
                while (count < 5 && HasReached(count * (3d * _countDuration / 5d)))
                    count++;
                return count;
            }
        }

        /// <summary>Gets whether countdown locking has ended; true from GO onward.</summary>
        public bool DrivingAllowed => State != StartSequenceState.Counting;

        /// <summary>Advances across any number of phases using finite, nonnegative seconds.</summary>
        public void Tick(float deltaTime)
        {
            NumericGuard.Nonnegative(deltaTime, nameof(deltaTime));
            _elapsed = Math.Min(_elapsed + deltaTime, 3d * _countDuration + _goDuration);
        }

        /// <summary>Returns to one lit lamp and legacy label 3 with driving locked.</summary>
        public void Reset()
        {
            _elapsed = 0d;
        }

        private bool HasReached(double seconds)
        {
            return _elapsed + _tolerance >= seconds;
        }
    }
}
