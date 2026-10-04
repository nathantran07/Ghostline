using System;

namespace Ghostline.Core
{
    public readonly struct EngineSoundFrame
    {
        public float Rpm { get; }
        public float Loudness { get; }
        public float EffectiveThrottle { get; }
        public int Gear { get; }
        public bool IsShifting { get; }

        internal EngineSoundFrame(float rpm, float loudness, float throttle, int gear, bool shifting)
        {
            Rpm = rpm;
            Loudness = loudness;
            EffectiveThrottle = throttle;
            Gear = gear;
            IsShifting = shifting;
        }
    }

    /// <summary>Audio-only gearbox and RPM response; never modifies driving input or forces.</summary>
    public sealed class EngineSoundModel
    {
        private readonly EngineSoundSettings _settings;
        private int _gear = 1;
        private int _pendingGear;
        private double _shiftRemaining;
        private double _limiterPhase;
        private float _rpm;
        private float _loudness;
        public EngineSoundFrame Current { get; private set; }

        public EngineSoundModel(EngineSoundSettings settings = null)
        {
            _settings = settings ?? new EngineSoundSettings();
            Reset();
        }

        public void Reset()
        {
            _gear = 1;
            _pendingGear = 0;
            _shiftRemaining = 0d;
            _limiterPhase = 0d;
            _rpm = _settings.IdleRpm;
            _loudness = 0.12f;
            Current = new EngineSoundFrame(_rpm, _loudness, 0f, _gear, false);
        }

        public EngineSoundFrame Tick(float speed, float throttle, float topSpeed, float deltaTime)
        {
            NumericGuard.Nonnegative(speed, nameof(speed));
            AudioMath.Unit(throttle, nameof(throttle));
            NumericGuard.Nonnegative(topSpeed, nameof(topSpeed));
            NumericGuard.Nonnegative(deltaTime, nameof(deltaTime));
            if (topSpeed == 0f)
                throw new ArgumentOutOfRangeException(nameof(topSpeed));
            if (deltaTime == 0f)
                return Current;
            double limiterPeriod = 1d / _settings.LimiterFrequency;
            _limiterPhase = (_limiterPhase + (deltaTime % limiterPeriod)
                * _settings.LimiterFrequency * 2d * Math.PI) % (2d * Math.PI);
            double fraction = Math.Min(1d, (double)speed / topSpeed);
            if (speed == 0f)
            {
                _gear = 1;
                _pendingGear = 0;
                _shiftRemaining = 0d;
                _rpm = _settings.IdleRpm;
            }
            double remaining = deltaTime;
            // At most GearCount-1 shifts are possible for one supplied speed.
            for (int transition = 0; transition <= _settings.GearCount && remaining > 0d; transition++)
            {
                if (_pendingGear == 0)
                {
                    int next = _gear;
                    if (_gear < _settings.GearCount && fraction >= _settings.GearEnd(_gear))
                        next++;
                    else if (_gear > 1 && fraction < _settings.GearEnd(_gear - 1) - _settings.DownshiftHysteresis)
                        next--;
                    if (next != _gear)
                    {
                        _pendingGear = next;
                        _shiftRemaining = _settings.ShiftGap;
                    }
                }
                if (_pendingGear != 0)
                {
                    double spent = Math.Min(remaining, _shiftRemaining);
                    _loudness = Approach(_loudness, 0.12f, spent, 0.02f);
                    _shiftRemaining -= spent;
                    remaining -= spent;
                    if (_shiftRemaining > 0.00000001d)
                        break;
                    _gear = _pendingGear;
                    _pendingGear = 0;
                    _rpm = TargetRpm(fraction, throttle);
                }
                else
                {
                    float target = TargetRpm(fraction, throttle);
                    _rpm = Approach(_rpm, target, remaining,
                        target > _rpm ? _settings.RiseTime : _settings.FallTime);
                    _loudness = Approach(_loudness, 0.12f + 0.88f * throttle, remaining, 0.04f);
                    remaining = 0d;
                }
            }
            _rpm = Math.Max(_settings.IdleRpm, Math.Min(_settings.RedlineRpm, _rpm));
            _loudness = Math.Max(0f, Math.Min(1f, _loudness));
            Current = new EngineSoundFrame(_rpm, _loudness, _pendingGear == 0 ? throttle : 0f,
                _gear, _pendingGear != 0);
            return Current;
        }

        private float TargetRpm(double fraction, float throttle)
        {
            double driven = Math.Max(_settings.IdleRpm,
                Math.Min(_settings.RedlineRpm, fraction / _settings.GearEnd(_gear) * _settings.RedlineRpm));
            if (_settings.LimiterEnabled && throttle > 0.9f && driven >= _settings.RedlineRpm - _settings.LimiterDepth)
            {
                driven -= _settings.LimiterDepth * (0.5d + 0.5d * Math.Sin(_limiterPhase));
            }
            return (float)(_settings.IdleRpm + (driven - _settings.IdleRpm) * throttle);
        }

        private static float Approach(float current, float target, double elapsed, float seconds)
        {
            return seconds == 0f ? target : (float)(target + (current - target) * Math.Exp(-elapsed / seconds));
        }
    }
}
