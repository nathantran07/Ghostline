using System;
using System.Collections.Generic;

namespace Ghostline.Core
{
    /// <summary>A copied, read-only sequence of poses with clamped interpolated playback.</summary>
    public sealed class GhostRecording
    {
        /// <summary>Copies a nonempty sequence whose sample times strictly increase.</summary>
        public GhostRecording(IEnumerable<GhostSample> samples)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));
            var copy = new List<GhostSample>(samples);
            if (copy.Count == 0)
                throw new ArgumentException("A recording needs at least one sample.", nameof(samples));
            for (int i = 1; i < copy.Count; i++)
            {
                if (copy[i].Time <= copy[i - 1].Time)
                    throw new ArgumentException("Sample times must strictly increase.", nameof(samples));
            }
            Samples = copy.AsReadOnly();
        }

        /// <summary>Gets the recording's immutable sample sequence.</summary>
        public IReadOnlyList<GhostSample> Samples { get; }

        /// <summary>Interpolates position and shortest-path rotation; clamps finite times to endpoints.</summary>
        public GhostSample Evaluate(float time)
        {
            NumericGuard.Finite(time, nameof(time));
            if (time <= Samples[0].Time)
                return Samples[0];
            int last = Samples.Count - 1;
            if (time >= Samples[last].Time)
                return Samples[last];
            int low = 0;
            int high = last;
            while (high - low > 1)
            {
                int middle = low + (high - low) / 2;
                if (Samples[middle].Time <= time)
                    low = middle;
                else
                    high = middle;
            }
            return GhostSample.Interpolate(Samples[low], Samples[high], time);
        }
    }
}
