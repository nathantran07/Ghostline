using System;

namespace Ghostline.Core
{
    /// <summary>Compares lap-clock times with a saved best; negative seconds mean ahead.</summary>
    public static class DeltaCalculator
    {
        /// <summary>Returns current minus best at a zero-based gate, or null without a best lap.</summary>
        public static float? AtCheckpoint(BestLapData bestLap, int checkpointIndex, float lapClockTime)
        {
            if (bestLap == null)
                return null;
            NumericGuard.Nonnegative(lapClockTime, nameof(lapClockTime));
            if (bestLap.Splits == null)
                throw new ArgumentException("The best lap must include checkpoint splits.", nameof(bestLap));
            if (checkpointIndex < 0 || checkpointIndex >= bestLap.Splits.Length)
                throw new ArgumentOutOfRangeException(nameof(checkpointIndex));
            NumericGuard.Nonnegative(bestLap.Splits[checkpointIndex], nameof(bestLap));
            return lapClockTime - bestLap.Splits[checkpointIndex];
        }

        /// <summary>Returns finished time minus the previous best duration, or null without a best.</summary>
        public static float? AtFinish(BestLapData bestLap, float finishedLapTime)
        {
            if (bestLap == null)
                return null;
            NumericGuard.Nonnegative(finishedLapTime, nameof(finishedLapTime));
            NumericGuard.Nonnegative(bestLap.LapTime, nameof(bestLap));
            return finishedLapTime - bestLap.LapTime;
        }
    }
}
