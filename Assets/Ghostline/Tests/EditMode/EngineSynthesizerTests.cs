using System;
using NUnit.Framework;
using Ghostline.Core;

namespace Ghostline.Tests.EditMode
{
    public sealed class EngineSynthesizerTests
    {
        [Test]
        public void RetunedVoiceHasPureConfiguration()
        {
            Assert.That(typeof(EngineSynthesizer).Assembly.GetType("Ghostline.Core.EngineVoiceSettings"),
                Is.Not.Null, "Phase A voice settings belong in pure Core.");
        }

        [TestCase(0f, 0f)]
        [TestCase(1000f, 100f)]
        [TestCase(6000f, 600f)]
        [TestCase(8500f, 850f)]
        public void V12FiringFrequencyIsSixFiringsPerRevolution(float rpm, float frequency)
        {
            Assert.That(AudioMath.FiringFrequency(rpm), Is.EqualTo(frequency));
        }

        [TestCase(8000)]
        [TestCase(44100)]
        [TestCase(48000)]
        [TestCase(96000)]
        public void RenderedEngineIsFiniteBoundedAndStartsQuietly(int rate)
        {
            var synth = new EngineSynthesizer(rate);
            var buffer = new float[rate * 2];
            synth.Render(buffer, 2, 850f, 1f, 1f, 1f, 1f);
            Assert.That(Math.Abs(buffer[0]), Is.LessThan(0.001f));
            bool audible = false;
            for (int i = 0; i < buffer.Length; i += 2)
            {
                Assert.That(buffer[i], Is.InRange(-0.85f, 0.85f));
                Assert.That(buffer[i + 1], Is.EqualTo(buffer[i]));
                audible |= Math.Abs(buffer[i]) > 0.01f;
            }
            Assert.That(audible, Is.True);
        }

        [Test]
        public void UnsupportedHarmonicsAreSilentAfterFrequencyRamp()
        {
            var weights = new float[12];
            weights[11] = 1f;
            var synth = new EngineSynthesizer(8000, weights, noiseAmount: 0f, voice: SilentOrders());
            var warmup = new float[8000];
            synth.Render(warmup, 1, 600f, 1f, 1f, 1f);
            var buffer = new float[1024];
            synth.Render(buffer, 1, 600f, 1f, 1f, 1f);
            Assert.That(buffer, Is.All.EqualTo(0f));
        }

        [Test]
        public void BelowCutoffCrankOrderKeepsItsPitchWhenFiringOrdersAreExcluded()
        {
            var weights = new float[12];
            weights[0] = 1f; // Pitched firing fundamental 4800 Hz exceeds the 3600 Hz cutoff.
            var voice = new EngineVoiceSettings(0f, 0f, 0f, 1f, pitchScale: 8f);
            var synth = new EngineSynthesizer(8000, weights, detune: 0f, noiseAmount: 0f, voice: voice);
            var samples = new float[8000];
            synth.Render(samples, 1, 600f, 1f, 1f, 1f);
            synth.Render(samples, 1, 600f, 1f, 1f, 1f);
            Assert.That(ToneAmplitude(samples, 2400d, 8000), Is.GreaterThan(0.1d));
            Assert.That(ToneAmplitude(samples, 1800d, 8000), Is.LessThan(0.001d), "No whole-voice pitch clamp.");
            Assert.That(ToneAmplitude(samples, 3200d, 8000), Is.LessThan(0.001d), "No aliased firing fundamental.");
        }

        [Test]
        public void AboveCutoffCrankOrderIsExcluded()
        {
            var synth = new EngineSynthesizer(8000, new float[12], noiseAmount: 0f,
                voice: new EngineVoiceSettings(0f, 0f, 0f, 1f, pitchScale: 16f));
            var samples = new float[8000];
            synth.Render(samples, 1, 600f, 1f, 1f, 1f);
            synth.Render(samples, 1, 600f, 1f, 1f, 1f);
            Assert.That(samples, Is.All.EqualTo(0f));
        }

        [Test]
        public void EachDetunedBankIsExcludedAtItsOwnCutoff()
        {
            var weights = new float[12];
            weights[0] = 1f;
            var synth = new EngineSynthesizer(8000, weights, detune: 0.02f, noiseAmount: 0f,
                voice: SilentOrders());
            var samples = new float[8000];
            synth.Render(samples, 1, 3600f, 1f, 1f, 1f);
            synth.Render(samples, 1, 3600f, 1f, 1f, 1f);
            Assert.That(ToneAmplitude(samples, 3564d, 8000), Is.GreaterThan(0.01d));
            Assert.That(ToneAmplitude(samples, 3636d, 8000), Is.LessThan(0.001d));
        }

        [Test]
        public void BufferBoundariesDoNotResetPhasesFiltersOrRamps()
        {
            var whole = new EngineSynthesizer(48000);
            var split = new EngineSynthesizer(48000);
            var expected = new float[2048];
            var first = new float[1024];
            var second = new float[1024];
            whole.Render(expected, 1, 600f, 0.8f, 1f, 0.4f);
            split.Render(first, 1, 600f, 0.8f, 1f, 0.4f);
            split.Render(second, 1, 600f, 0.8f, 1f, 0.4f);
            for (int i = 0; i < 1024; i++)
            {
                Assert.That(first[i], Is.EqualTo(expected[i]));
                Assert.That(second[i], Is.EqualTo(expected[i + 1024]));
            }
        }

        [Test]
        public void FilteredNoiseIsScaledByThrottleAndOptionalTireInput()
        {
            var quiet = new EngineSynthesizer(48000, new float[12], noiseAmount: 0.1f, voice: SilentOrders());
            var loud = new EngineSynthesizer(48000, new float[12], noiseAmount: 0.1f, voice: SilentOrders());
            var tire = new EngineSynthesizer(48000, new float[12], noiseAmount: 0f, voice: SilentOrders());
            var buffer = new float[4800];
            quiet.Render(buffer, 1, 100f, 1f, 0f, 1f);
            Assert.That(buffer, Is.All.EqualTo(0f));
            loud.Render(buffer, 1, 100f, 1f, 1f, 1f);
            Assert.That(Array.Exists(buffer, value => Math.Abs(value) > 0.001f), Is.True);
            tire.Render(buffer, 1, 100f, 1f, 0f, 1f, 1f);
            Assert.That(Array.Exists(buffer, value => Math.Abs(value) > 0.001f), Is.True);
        }

        [Test]
        public void FinalLimiterBoundsOverloadedMixAndRampsFromSilence()
        {
            var limiter = new AudioOutputLimiter(48000);
            var buffer = new float[4096];
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = 3f;
            limiter.Process(buffer, 2, 0.5f);
            Assert.That(buffer[0], Is.InRange(0f, 0.002f));
            Assert.That(buffer, Is.All.InRange(-0.85f, 0.85f));
            buffer[0] = float.NaN;
            buffer[1] = float.PositiveInfinity;
            limiter.Process(buffer, 2, 0f);
            Assert.That(buffer[0], Is.Zero);
            Assert.That(buffer[1], Is.Zero);
        }

        [TestCase(0, 50d)]
        [TestCase(1, 100d)]
        [TestCase(2, 200d)]
        [TestCase(3, 300d)]
        public void LowOrdersAreAudibleAtTheirCrankFrequency(int order, double expected)
        {
            var voice = new EngineVoiceSettings(crankWeight: order == 1 ? 1f : 0f,
                halfOrderWeight: order == 0 ? 1f : 0f, secondCrankWeight: order == 2 ? 1f : 0f,
                thirdCrankWeight: order == 3 ? 1f : 0f);
            float[] samples = Capture(voice, new float[12]);
            Assert.That(ToneAmplitude(samples, expected), Is.GreaterThan(0.1d));
            // The retained soft clip adds a small third overtone to the 200 Hz voice.
            Assert.That(ToneAmplitude(samples, 600d), Is.LessThan(ToneAmplitude(samples, expected) * 0.1d));
            // In particular, half order must not restart every crank revolution.
            Assert.That(ToneAmplitude(samples, expected + 25d), Is.LessThan(0.002d));
        }

        [TestCase(0, 25d)]
        [TestCase(1, 50d)]
        [TestCase(2, 100d)]
        [TestCase(3, 150d)]
        [TestCase(4, 300d)]
        public void PitchScaleMovesEveryOrderWithoutChangingFiringMath(int order, double expected)
        {
            var voice = new EngineVoiceSettings(crankWeight: order == 1 ? 1f : 0f,
                halfOrderWeight: order == 0 ? 1f : 0f, secondCrankWeight: order == 2 ? 1f : 0f,
                thirdCrankWeight: order == 3 ? 1f : 0f, pitchScale: 0.5f);
            var weights = new float[12];
            if (order == 4)
                weights[0] = 1f;
            float[] samples = Capture(voice, weights);
            Assert.That(ToneAmplitude(samples, expected), Is.GreaterThan(0.1d));
            Assert.That(ToneAmplitude(samples, expected * 2d), Is.LessThan(0.01d));
            Assert.That(AudioMath.FiringFrequency(6000f), Is.EqualTo(600f));
            Assert.That(voice.CutoffAtRpm(6000f), Is.EqualTo(3833.333f).Within(0.001f));
        }

        [TestCase(0f, 1500f)]
        [TestCase(1000f, 1500f)]
        [TestCase(4750f, 3250f)]
        [TestCase(8500f, 5000f)]
        [TestCase(60000f, 5000f)]
        public void LowPassCutoffFollowsRpmAndClamps(float rpm, float expected)
        {
            Assert.That(new EngineVoiceSettings().CutoffAtRpm(rpm), Is.EqualTo(expected));
        }

        [Test]
        public void LowPassAttenuatesUpperHarmonicsAndUsesUnpitchedRpm()
        {
            var weights = new float[12];
            weights[4] = 1f; // 3000 Hz at 6000 RPM.
            float[] low = Capture(SilentOrders(1500f, 1500f), weights);
            float[] high = Capture(SilentOrders(5000f, 5000f), weights);
            Assert.That(ToneAmplitude(low, 3000d), Is.LessThan(ToneAmplitude(high, 3000d) * 0.75d));
            // At half pitch the same 6000 RPM must still select 3833.333 Hz, not an idle cutoff.
            var following = new EngineVoiceSettings(0f, 0f, 0f, 0f, pitchScale: 0.5f);
            var fixedCutoff = new EngineVoiceSettings(0f, 0f, 0f, 0f, pitchScale: 0.5f,
                lowPassMin: following.CutoffAtRpm(6000f), lowPassMax: following.CutoffAtRpm(6000f));
            Assert.That(ToneAmplitude(Capture(following, weights), 1500d),
                Is.EqualTo(ToneAmplitude(Capture(fixedCutoff, weights), 1500d)).Within(0.00001d));
        }

        [Test]
        public void HarmonicFactoriesReturnIndependentArraysAndOnlyRecognizeExactLegacyPreset()
        {
            float[] expected = { 1f, 0.7f, 0.5f, 0.35f, 0.25f, 0.18f, 0.12f, 0.08f, 0.06f, 0.04f, 0.03f, 0.02f };
            Assert.That(EngineVoiceSettings.CreateDefaultHarmonics(), Is.EqualTo(expected));
            float[] changed = EngineVoiceSettings.CreateDefaultHarmonics();
            changed[0] = 0f;
            Assert.That(EngineVoiceSettings.CreateDefaultHarmonics(), Is.EqualTo(expected));
            float[] legacy = { 1f, 0.65f, 0.5f, 0.4f, 0.32f, 0.25f, 0.18f, 0.14f, 0.1f, 0.08f, 0.06f, 0.04f };
            Assert.That(EngineVoiceSettings.IsLegacyFactoryHarmonics(legacy), Is.True);
            legacy[1] += 0.00001f;
            Assert.That(EngineVoiceSettings.IsLegacyFactoryHarmonics(legacy), Is.False);
            Assert.That(EngineVoiceSettings.IsLegacyFactoryHarmonics(expected), Is.False);
            Assert.That(EngineVoiceSettings.IsLegacyFactoryHarmonics(null), Is.False);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidVoiceValuesAreRejected(float value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineVoiceSettings(crankWeight: value));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineVoiceSettings(halfOrderWeight: value));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineVoiceSettings(secondCrankWeight: value));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineVoiceSettings(thirdCrankWeight: value));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineVoiceSettings(pitchScale: value));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineVoiceSettings(lowPassMin: value));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineVoiceSettings(lowPassMax: value));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineVoiceSettings(idleRpm: value));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineVoiceSettings(redlineRpm: value));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineVoiceSettings().CutoffAtRpm(value));
        }

        [Test]
        public void InvalidVoiceRangesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineVoiceSettings(pitchScale: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineVoiceSettings(lowPassMin: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineVoiceSettings(lowPassMax: 1000f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineVoiceSettings(idleRpm: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineVoiceSettings(redlineRpm: 1000f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineVoiceSettings(crankWeight: 1.1f));
        }

        [TestCase(8000)]
        [TestCase(48000)]
        public void ExtremeValidPitchAndCutoffStayFiniteBounded(int rate)
        {
            var synth = new EngineSynthesizer(rate, voice: new EngineVoiceSettings(pitchScale: float.MaxValue,
                lowPassMin: float.Epsilon, lowPassMax: float.MaxValue));
            var buffer = new float[4096];
            synth.Render(buffer, 1, float.MaxValue, 1f, 1f, 1f);
            Assert.That(buffer, Is.All.InRange(-0.85f, 0.85f));
        }

        private static EngineVoiceSettings SilentOrders(float minimum = 1500f, float maximum = 5000f)
        {
            return new EngineVoiceSettings(0f, 0f, 0f, 0f, lowPassMin: minimum, lowPassMax: maximum);
        }

        private static float[] Capture(EngineVoiceSettings voice, float[] weights)
        {
            var synth = new EngineSynthesizer(48000, weights, detune: 0f, noiseAmount: 0f, voice: voice);
            var samples = new float[48000];
            synth.Render(samples, 1, 600f, 1f, 1f, 1f);
            synth.Render(samples, 1, 600f, 1f, 1f, 1f);
            return samples;
        }

        private static double ToneAmplitude(float[] samples, double frequency, int rate = 48000)
        {
            double real = 0d;
            double imaginary = 0d;
            for (int i = 0; i < samples.Length; i++)
            {
                double phase = 2d * Math.PI * frequency * i / rate;
                real += samples[i] * Math.Cos(phase);
                imaginary += samples[i] * Math.Sin(phase);
            }
            return 2d * Math.Sqrt(real * real + imaginary * imaginary) / samples.Length;
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidFrequencyAndSamplesAreRejected(float value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AudioMath.FiringFrequency(value));
            if (float.IsNaN(value) || float.IsInfinity(value))
                Assert.Throws<ArgumentOutOfRangeException>(() => AudioMath.SoftClip(value));
        }

        [Test]
        public void InvalidSynthesisSettingsAndBuffersAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineSynthesizer(0));
            Assert.Throws<ArgumentException>(() => new EngineSynthesizer(48000, new[] { 1f }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineSynthesizer(48000, detune: float.NaN));
            var synth = new EngineSynthesizer(48000);
            Assert.Throws<ArgumentNullException>(() => synth.Render(null, 1, 100f, 1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => synth.Render(new float[3], 2, 100f, 1f, 1f, 1f));
        }
    }
}
