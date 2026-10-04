using System;

namespace Ghostline.Core
{
    public static class AudioMath
    {
        public static float FiringFrequency(float rpm)
        {
            NumericGuard.Nonnegative(rpm, nameof(rpm));
            return (float)((double)rpm / 60d * 6d);
        }

        /// <summary>Bounded final mix with 15% peak headroom, even for very large finite input.</summary>
        public static float SoftClip(float sample)
        {
            NumericGuard.Finite(sample, nameof(sample));
            return (float)(0.85d * Math.Tanh(sample / 0.85d));
        }

        public static float ImpactVolume(float impactSpeed, float referenceSpeed)
        {
            NumericGuard.Nonnegative(impactSpeed, nameof(impactSpeed));
            NumericGuard.Nonnegative(referenceSpeed, nameof(referenceSpeed));
            if (referenceSpeed == 0f)
                throw new ArgumentOutOfRangeException(nameof(referenceSpeed));
            return (float)Math.Min(1d, (double)impactSpeed / referenceSpeed);
        }

        internal static void Unit(float value, string parameterName)
        {
            NumericGuard.Finite(value, parameterName);
            if (value < 0f || value > 1f)
                throw new ArgumentOutOfRangeException(parameterName);
        }

        internal static void SampleRate(int sampleRate)
        {
            if (sampleRate < 8000 || sampleRate > 384000)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
        }
    }

    /// <summary>Audio-thread-owned final master ramp and soft clip, shared by all player sounds.</summary>
    public sealed class AudioOutputLimiter
    {
        private readonly double _ramp;
        private double _gain;

        public AudioOutputLimiter(int sampleRate)
        {
            AudioMath.SampleRate(sampleRate);
            _ramp = 1d - Math.Exp(-1d / (sampleRate * 0.02d));
        }

        public void Process(float[] buffer, int channels, float masterVolume)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (channels <= 0 || buffer.Length % channels != 0)
                throw new ArgumentOutOfRangeException(nameof(channels));
            AudioMath.Unit(masterVolume, nameof(masterVolume));
            for (int i = 0; i < buffer.Length; i += channels)
            {
                _gain += (masterVolume - _gain) * _ramp;
                for (int c = 0; c < channels; c++)
                {
                    float sample = buffer[i + c];
                    // A malformed upstream sample cannot contaminate the output device.
                    buffer[i + c] = float.IsNaN(sample) || float.IsInfinity(sample) ? 0f
                        : AudioMath.SoftClip((float)(sample * _gain));
                }
            }
        }
    }
}
