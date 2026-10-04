using System;

namespace Ghostline.Core
{
    /// <summary>Allocation-free additive engine renderer; all state belongs to one audio thread.</summary>
    public sealed class EngineSynthesizer
    {
        private readonly int _sampleRate;
        private readonly float[] _weights;
        private readonly double _normalization;
        private readonly double _detune;
        private readonly double _noiseAmount;
        private readonly double _ramp;
        private readonly double _noiseLowStep;
        private readonly double _noiseHighStep;
        private double _phaseA;
        private double _phaseB;
        private double _frequency = 100d;
        private double _gain;
        private double _throttle;
        private double _tire;
        private double _noiseLow;
        private double _noiseHigh;
        private uint _random = 0x12345678;

        public EngineSynthesizer(int sampleRate, float[] harmonicWeights = null,
            float detune = 0.003f, float noiseAmount = 0.06f, float rampTime = 0.02f)
        {
            AudioMath.SampleRate(sampleRate);
            NumericGuard.Nonnegative(detune, nameof(detune));
            AudioMath.Unit(noiseAmount, nameof(noiseAmount));
            NumericGuard.Nonnegative(rampTime, nameof(rampTime));
            if (detune > 0.02f || rampTime == 0f)
                throw new ArgumentOutOfRangeException(nameof(detune));
            harmonicWeights = harmonicWeights ?? new[] { 1f, 0.65f, 0.5f, 0.4f, 0.32f, 0.25f,
                0.18f, 0.14f, 0.1f, 0.08f, 0.06f, 0.04f };
            if (harmonicWeights.Length != 12)
                throw new ArgumentException("Supply weights for harmonic orders 1 through 12.", nameof(harmonicWeights));
            _weights = (float[])harmonicWeights.Clone();
            double total = 0d;
            foreach (float weight in _weights)
            {
                AudioMath.Unit(weight, nameof(harmonicWeights));
                total += weight;
            }
            _normalization = total > 0d ? 0.75d / (2d * total) : 0d;
            _sampleRate = sampleRate;
            _detune = detune;
            _noiseAmount = noiseAmount;
            _ramp = 1d - Math.Exp(-1d / (sampleRate * (double)rampTime));
            _noiseLowStep = 1d - Math.Exp(-2d * Math.PI * 3200d / sampleRate);
            _noiseHighStep = 1d - Math.Exp(-2d * Math.PI * 250d / sampleRate);
        }

        public void Render(float[] buffer, int channels, float fundamental, float loudness,
            float throttle, float engineVolume, float tireAmount = 0f)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (channels <= 0 || buffer.Length % channels != 0)
                throw new ArgumentOutOfRangeException(nameof(channels));
            NumericGuard.Nonnegative(fundamental, nameof(fundamental));
            AudioMath.Unit(loudness, nameof(loudness));
            AudioMath.Unit(throttle, nameof(throttle));
            AudioMath.Unit(engineVolume, nameof(engineVolume));
            AudioMath.Unit(tireAmount, nameof(tireAmount));
            double targetFrequency = Math.Min(fundamental, _sampleRate * 0.45d);
            for (int i = 0; i < buffer.Length; i += channels)
            {
                _frequency += (targetFrequency - _frequency) * _ramp;
                _gain += (engineVolume * loudness - _gain) * _ramp;
                _throttle += (throttle - _throttle) * _ramp;
                _tire += (tireAmount * engineVolume - _tire) * _ramp;
                double frequencyA = _frequency * (1d - _detune * 0.5d);
                double frequencyB = _frequency * (1d + _detune * 0.5d);
                double signal = 0d;
                for (int order = 1; order <= 12; order++)
                {
                    if (_weights[order - 1] == 0f)
                        continue;
                    if (frequencyA * order < _sampleRate * 0.45d)
                        signal += _weights[order - 1] * BandGain(frequencyA * order) * Math.Sin(_phaseA * order);
                    if (frequencyB * order < _sampleRate * 0.45d)
                        signal += _weights[order - 1] * BandGain(frequencyB * order) * Math.Sin(_phaseB * order);
                }
                _random ^= _random << 13;
                _random ^= _random >> 17;
                _random ^= _random << 5;
                double noise = _random / (double)uint.MaxValue * 2d - 1d;
                // One-pole low-pass difference forms a broad intake/exhaust and tire band.
                _noiseLow += (noise - _noiseLow) * _noiseLowStep;
                _noiseHigh += (noise - _noiseHigh) * _noiseHighStep;
                double filtered = _noiseLow - _noiseHigh;
                float sample = AudioMath.SoftClip((float)((signal * _normalization
                    + filtered * _noiseAmount * _throttle) * _gain + filtered * _tire * 0.22d));
                for (int c = 0; c < channels; c++)
                    buffer[i + c] = sample;
                _phaseA = (_phaseA + 2d * Math.PI * frequencyA / _sampleRate) % (2d * Math.PI);
                _phaseB = (_phaseB + 2d * Math.PI * frequencyB / _sampleRate) % (2d * Math.PI);
            }
        }

        private double BandGain(double frequency)
        {
            // Taper before exclusion to avoid discontinuities when an order crosses the cutoff.
            return Math.Max(0d, Math.Min(1d, (_sampleRate * 0.45d - frequency) / (_sampleRate * 0.05d)));
        }
    }
}
