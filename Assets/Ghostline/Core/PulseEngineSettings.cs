using System;

namespace Ghostline.Core
{
    public readonly struct PulseResonance
    {
        public float Frequency { get; }
        public float Q { get; }
        public float Gain { get; }

        public PulseResonance(float frequency, float q, float gain)
        {
            NumericGuard.Nonnegative(frequency, nameof(frequency));
            NumericGuard.Finite(q, nameof(q));
            AudioMath.Unit(gain, nameof(gain));
            if (frequency == 0f)
                throw new ArgumentOutOfRangeException(nameof(frequency));
            if (q < 0.2f || q > 20f)
                throw new ArgumentOutOfRangeException(nameof(q));
            Frequency = frequency;
            Q = q;
            Gain = gain;
        }
    }

    /// <summary>Immutable pulse controls. Width is the approximate time for a burst to decay by 99%.</summary>
    public sealed class PulseEngineSettings
    {
        private readonly PulseResonance[] _resonances;
        public float PulseWidth { get; }
        public float BankTimingOffset { get; }
        public float BankDetune { get; }
        public float AmplitudeJitter { get; }
        public float TimingJitter { get; }
        public uint Seed { get; }

        public PulseEngineSettings(float pulseWidth = 0.0012f, float bankTimingOffset = 0.01f,
            float bankDetune = 0.003f, float amplitudeJitter = 0.03f, float timingJitter = 0.02f,
            uint seed = 0x12345678, PulseResonance[] resonances = null)
        {
            NumericGuard.Finite(pulseWidth, nameof(pulseWidth));
            AudioMath.Unit(bankTimingOffset, nameof(bankTimingOffset));
            AudioMath.Unit(bankDetune, nameof(bankDetune));
            AudioMath.Unit(amplitudeJitter, nameof(amplitudeJitter));
            AudioMath.Unit(timingJitter, nameof(timingJitter));
            if (pulseWidth < 0.0001f || pulseWidth > 0.02f)
                throw new ArgumentOutOfRangeException(nameof(pulseWidth));
            if (bankTimingOffset > 0.1f || bankDetune > 0.02f
                || amplitudeJitter > 0.2f || timingJitter > 0.1f)
                throw new ArgumentOutOfRangeException(nameof(bankTimingOffset));
            resonances = resonances ?? new[] { new PulseResonance(180f, 1.2f, 0.5f),
                new PulseResonance(650f, 1.5f, 0.3f), new PulseResonance(1800f, 1f, 0.2f) };
            if (resonances.Length != 3)
                throw new ArgumentException("Supply exactly three exhaust/intake resonances.", nameof(resonances));
            _resonances = (PulseResonance[])resonances.Clone();
            // Revalidate default(struct) values as well as normally constructed resonances.
            foreach (PulseResonance resonance in _resonances)
                _ = new PulseResonance(resonance.Frequency, resonance.Q, resonance.Gain);
            PulseWidth = pulseWidth;
            BankTimingOffset = bankTimingOffset;
            BankDetune = bankDetune;
            AmplitudeJitter = amplitudeJitter;
            TimingJitter = timingJitter;
            Seed = seed;
        }

        public PulseResonance ResonanceAt(int index)
        {
            if (index < 0 || index >= _resonances.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _resonances[index];
        }
    }
}
