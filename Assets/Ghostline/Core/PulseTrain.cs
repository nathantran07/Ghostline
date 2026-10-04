using System;

namespace Ghostline.Core
{
    public readonly struct CylinderPulse
    {
        public bool Fired { get; }
        public int Bank { get; }
        public float Amplitude { get; }
        public double AgeSeconds { get; }

        internal CylinderPulse(int bank, float amplitude, double ageSeconds)
        {
            Fired = true;
            Bank = bank;
            Amplitude = amplitude;
            AgeSeconds = ageSeconds;
        }
    }

    /// <summary>One alternating firing clock; offsets are fractions of a nominal firing interval.</summary>
    public sealed class PulseTrain
    {
        private readonly int _sampleRate;
        private readonly PulseEngineSettings _settings;
        private PulseRandom _random;
        private double _phase;
        private double _interval;
        private double _detunePhase;
        private double _bankBias;
        private int _bank;

        public PulseTrain(int sampleRate, PulseEngineSettings settings = null)
        {
            // Also supports the renderer's fixed 4x internal sample rate.
            if (sampleRate < 8000 || sampleRate > 1536000)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            _sampleRate = sampleRate;
            _settings = settings ?? new PulseEngineSettings();
            _random = new PulseRandom(_settings.Seed);
            _interval = NextInterval();
        }

        public CylinderPulse Advance(double firingFrequency)
        {
            if (double.IsNaN(firingFrequency) || double.IsInfinity(firingFrequency)
                || firingFrequency < 0d || firingFrequency > _sampleRate * 0.45d)
                throw new ArgumentOutOfRangeException(nameof(firingFrequency));
            if (firingFrequency == 0d)
                return default;
            _phase += firingFrequency / _sampleRate;
            if (_phase < _interval)
                return default;
            _phase -= _interval;
            var pulse = new CylinderPulse(_bank, (float)(1d + _random.Next() * _settings.AmplitudeJitter),
                _phase / firingFrequency);
            _bank = 1 - _bank;
            _interval = NextInterval();
            return pulse;
        }

        private double NextInterval()
        {
            // Opposing bank timing biases cancel over a pair; there is never a second firing stream.
            if (_bank == 0)
            {
                _detunePhase = (_detunePhase + 2d * Math.PI * _settings.BankDetune) % (2d * Math.PI);
                _bankBias = _settings.BankTimingOffset
                    + _settings.BankDetune * 0.5d * Math.Sin(_detunePhase);
            }
            return 1d + (_bank == 0 ? -_bankBias : _bankBias) + _random.Next() * _settings.TimingJitter;
        }
    }

    internal struct PulseRandom
    {
        private uint _state;

        internal PulseRandom(uint seed)
        {
            // Zero is a valid user seed, mapped away from xorshift's absorbing state.
            _state = seed == 0 ? 0x9E3779B9 : seed;
        }

        internal double Next()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state / (double)uint.MaxValue * 2d - 1d;
        }
    }
}
