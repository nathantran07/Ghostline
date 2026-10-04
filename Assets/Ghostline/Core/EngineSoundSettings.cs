using System;

namespace Ghostline.Core
{
    /// <summary>Immutable, engine-free configuration for the simulated single-clutch V12.</summary>
    public sealed class EngineSoundSettings
    {
        private readonly float[] _gearEnds;
        public float IdleRpm { get; }
        public float RedlineRpm { get; }
        public int GearCount => _gearEnds.Length;
        public float ShiftGap { get; }
        public float RiseTime { get; }
        public float FallTime { get; }
        public float DownshiftHysteresis { get; }
        public bool LimiterEnabled { get; }
        public float LimiterDepth { get; }
        public float LimiterFrequency { get; }

        public EngineSoundSettings(float idleRpm = 1000f, float redlineRpm = 8500f,
            int gearCount = 7, float[] gearEnds = null, float shiftGap = 0.08f,
            float riseTime = 0.12f, float fallTime = 0.35f, float downshiftHysteresis = 0.015f,
            bool limiterEnabled = false, float limiterDepth = 180f, float limiterFrequency = 18f)
        {
            NumericGuard.Nonnegative(idleRpm, nameof(idleRpm));
            NumericGuard.Nonnegative(redlineRpm, nameof(redlineRpm));
            if (idleRpm == 0f || redlineRpm <= idleRpm || redlineRpm > 60000f)
                throw new ArgumentOutOfRangeException(nameof(redlineRpm));
            if (gearCount < 1 || gearCount > 16)
                throw new ArgumentOutOfRangeException(nameof(gearCount));
            NumericGuard.Nonnegative(shiftGap, nameof(shiftGap));
            NumericGuard.Nonnegative(riseTime, nameof(riseTime));
            NumericGuard.Nonnegative(fallTime, nameof(fallTime));
            NumericGuard.Nonnegative(downshiftHysteresis, nameof(downshiftHysteresis));
            NumericGuard.Nonnegative(limiterDepth, nameof(limiterDepth));
            NumericGuard.Nonnegative(limiterFrequency, nameof(limiterFrequency));
            if (downshiftHysteresis >= 1f || limiterDepth > redlineRpm - idleRpm || limiterFrequency == 0f)
                throw new ArgumentOutOfRangeException(nameof(limiterDepth));
            if (gearEnds == null)
            {
                gearEnds = gearCount == 7 ? new[] { 0.16f, 0.28f, 0.41f, 0.55f, 0.70f, 0.85f, 1f }
                    : new float[gearCount];
                if (gearCount != 7)
                    for (int i = 0; i < gearCount; i++)
                        gearEnds[i] = (i + 1f) / gearCount;
            }
            if (gearEnds.Length != gearCount)
                throw new ArgumentException("Supply one upper speed fraction per gear.", nameof(gearEnds));
            _gearEnds = (float[])gearEnds.Clone();
            float previous = 0f;
            foreach (float end in _gearEnds)
            {
                NumericGuard.Finite(end, nameof(gearEnds));
                if (end <= previous || end > 1f || (previous > 0f && end - previous <= downshiftHysteresis))
                    throw new ArgumentException("Gear ends must rise to 1 and exceed downshift hysteresis.", nameof(gearEnds));
                previous = end;
            }
            if (previous != 1f)
                throw new ArgumentException("The last gear must end at the top-speed reference.", nameof(gearEnds));
            IdleRpm = idleRpm;
            RedlineRpm = redlineRpm;
            ShiftGap = shiftGap;
            RiseTime = riseTime;
            FallTime = fallTime;
            DownshiftHysteresis = downshiftHysteresis;
            LimiterEnabled = limiterEnabled;
            LimiterDepth = limiterDepth;
            LimiterFrequency = limiterFrequency;
        }

        public float GearEnd(int gear)
        {
            if (gear < 1 || gear > GearCount)
                throw new ArgumentOutOfRangeException(nameof(gear));
            return _gearEnds[gear - 1];
        }
    }
}
