using System;

namespace Ghostline.Core
{
    /// <summary>Immutable timbre controls, independent of the drivetrain's tested RPM model.</summary>
    public sealed class EngineVoiceSettings
    {
        public float CrankWeight { get; }
        public float HalfOrderWeight { get; }
        public float SecondCrankWeight { get; }
        public float ThirdCrankWeight { get; }
        public float PitchScale { get; }
        public float LowPassMin { get; }
        public float LowPassMax { get; }
        public float IdleRpm { get; }
        public float RedlineRpm { get; }

        public EngineVoiceSettings(float crankWeight = 0.8f, float halfOrderWeight = 0.15f,
            float secondCrankWeight = 0.45f, float thirdCrankWeight = 0.35f,
            float pitchScale = 1f, float lowPassMin = 1500f, float lowPassMax = 5000f,
            float idleRpm = 1000f, float redlineRpm = 8500f)
        {
            AudioMath.Unit(crankWeight, nameof(crankWeight));
            AudioMath.Unit(halfOrderWeight, nameof(halfOrderWeight));
            AudioMath.Unit(secondCrankWeight, nameof(secondCrankWeight));
            AudioMath.Unit(thirdCrankWeight, nameof(thirdCrankWeight));
            NumericGuard.Nonnegative(pitchScale, nameof(pitchScale));
            NumericGuard.Nonnegative(lowPassMin, nameof(lowPassMin));
            NumericGuard.Nonnegative(lowPassMax, nameof(lowPassMax));
            NumericGuard.Nonnegative(idleRpm, nameof(idleRpm));
            NumericGuard.Nonnegative(redlineRpm, nameof(redlineRpm));
            if (pitchScale == 0f)
                throw new ArgumentOutOfRangeException(nameof(pitchScale));
            if (lowPassMin == 0f || lowPassMax < lowPassMin)
                throw new ArgumentOutOfRangeException(nameof(lowPassMin));
            if (idleRpm == 0f || redlineRpm <= idleRpm || redlineRpm > 60000f)
                throw new ArgumentOutOfRangeException(nameof(redlineRpm));
            CrankWeight = crankWeight;
            HalfOrderWeight = halfOrderWeight;
            SecondCrankWeight = secondCrankWeight;
            ThirdCrankWeight = thirdCrankWeight;
            PitchScale = pitchScale;
            LowPassMin = lowPassMin;
            LowPassMax = lowPassMax;
            IdleRpm = idleRpm;
            RedlineRpm = redlineRpm;
        }

        public float CutoffAtRpm(float rpm)
        {
            NumericGuard.Nonnegative(rpm, nameof(rpm));
            double fraction = Math.Max(0d, Math.Min(1d, ((double)rpm - IdleRpm) / (RedlineRpm - IdleRpm)));
            return (float)(LowPassMin + ((double)LowPassMax - LowPassMin) * fraction);
        }

        public static float[] CreateDefaultHarmonics()
        {
            return new[] { 1f, 0.7f, 0.5f, 0.35f, 0.25f, 0.18f,
                0.12f, 0.08f, 0.06f, 0.04f, 0.03f, 0.02f };
        }

        public static bool IsLegacyFactoryHarmonics(float[] weights)
        {
            float[] legacy = { 1f, 0.65f, 0.5f, 0.4f, 0.32f, 0.25f,
                0.18f, 0.14f, 0.1f, 0.08f, 0.06f, 0.04f };
            if (weights == null || weights.Length != legacy.Length)
                return false;
            for (int i = 0; i < legacy.Length; i++)
                if (weights[i] != legacy[i])
                    return false;
            return true;
        }
    }
}
