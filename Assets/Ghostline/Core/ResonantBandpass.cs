using System;

namespace Ghostline.Core
{
    /// <summary>Trapezoidal state-variable bandpass with unity peak gain and bounded coefficients.</summary>
    public sealed class ResonantBandpass
    {
        private readonly double _a1;
        private readonly double _a2;
        private readonly double _a3;
        private readonly double _damping;
        private double _state1;
        private double _state2;

        public ResonantBandpass(int sampleRate, float frequency, float q)
        {
            if (sampleRate < 8000 || sampleRate > 1536000)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            _ = new PulseResonance(frequency, q, 1f);
            double cutoff = Math.Max(1d, Math.Min(frequency, sampleRate * 0.45d));
            double g = Math.Tan(Math.PI * cutoff / sampleRate);
            _damping = 1d / q;
            _a1 = 1d / (1d + g * (g + _damping));
            _a2 = g * _a1;
            _a3 = g * _a2;
        }

        public double Process(double input)
        {
            if (double.IsNaN(input) || double.IsInfinity(input) || Math.Abs(input) > 1000000d)
                throw new ArgumentOutOfRangeException(nameof(input));
            double v3 = input - _state2;
            double v1 = _a1 * _state1 + _a2 * v3;
            double v2 = _state2 + _a2 * _state1 + _a3 * v3;
            _state1 = 2d * v1 - _state1;
            _state2 = 2d * v2 - _state2;
            return _damping * v1;
        }
    }
}
