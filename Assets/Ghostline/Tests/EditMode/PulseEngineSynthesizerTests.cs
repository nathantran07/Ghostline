using System;
using NUnit.Framework;
using Ghostline.Core;

namespace Ghostline.Tests.EditMode
{
    public sealed class PulseEngineSynthesizerTests
    {
        [TestCase(8000, 1000f)]
        [TestCase(8000, 6000f)]
        [TestCase(8000, 8500f)]
        [TestCase(44100, 1000f)]
        [TestCase(44100, 6000f)]
        [TestCase(44100, 8500f)]
        [TestCase(48000, 1000f)]
        [TestCase(48000, 6000f)]
        [TestCase(48000, 8500f)]
        [TestCase(96000, 1000f)]
        [TestCase(96000, 6000f)]
        [TestCase(96000, 8500f)]
        public void FiringSpacingMatchesRpmWithOneAlternatingClock(int rate, float rpm)
        {
            var train = new PulseTrain(rate, RegularPulses());
            double frequency = AudioMath.FiringFrequency(rpm);
            int count = 0;
            double lastTime = 0d;
            for (int i = 0; i < rate; i++)
            {
                CylinderPulse pulse = train.Advance(frequency);
                if (!pulse.Fired)
                    continue;
                double time = (i + 1d) / rate - pulse.AgeSeconds;
                Assert.That(time - lastTime, Is.EqualTo(1d / frequency).Within(1d / rate));
                Assert.That(pulse.Bank, Is.EqualTo(count % 2));
                Assert.That(pulse.Amplitude, Is.EqualTo(1f));
                lastTime = time;
                count++;
            }
            Assert.That(count, Is.InRange((int)frequency - 1, (int)frequency));
        }

        [Test]
        public void BankBiasPreservesPairedMeanSpacingAndJitterRemainsBounded()
        {
            var settings = new PulseEngineSettings(bankTimingOffset: 0.1f, bankDetune: 0f,
                amplitudeJitter: 0.2f, timingJitter: 0f);
            var train = new PulseTrain(48000, settings);
            double last = 0d;
            int count = 0;
            for (int i = 0; i < 48000; i++)
            {
                CylinderPulse pulse = train.Advance(600d);
                if (!pulse.Fired)
                    continue;
                double time = (i + 1d) / 48000d - pulse.AgeSeconds;
                double expected = (count % 2 == 0 ? 0.9d : 1.1d) / 600d;
                Assert.That(time - last, Is.EqualTo(expected).Within(0.00000001d));
                Assert.That(pulse.Amplitude, Is.InRange(0.8f, 1.2f));
                Assert.That(pulse.Bank, Is.EqualTo(count % 2));
                last = time;
                count++;
            }
            Assert.That(count, Is.InRange(599, 600), "Bank detune cannot double the firing rate.");
            settings = new PulseEngineSettings(bankTimingOffset: 0.1f, bankDetune: 0.02f, timingJitter: 0.1f);
            train = new PulseTrain(48000, settings);
            last = 0d;
            for (int i = 0; i < 48000; i++)
            {
                CylinderPulse pulse = train.Advance(600d);
                if (!pulse.Fired)
                    continue;
                double time = (i + 1d) / 48000d - pulse.AgeSeconds;
                Assert.That(time - last, Is.InRange(0.789999d / 600d, 1.210001d / 600d));
                last = time;
            }
        }

        [Test]
        public void BankDetuneDriftsWithinBoundsWithoutChangingPairedFiringRate()
        {
            var train = new PulseTrain(48000, new PulseEngineSettings(bankTimingOffset: 0f,
                bankDetune: 0.02f, amplitudeJitter: 0f, timingJitter: 0f));
            double previousPair = 0d;
            double minimum = double.MaxValue;
            double maximum = 0d;
            for (int i = 0; i < 48000; i++)
            {
                CylinderPulse pulse = train.Advance(600d);
                if (!pulse.Fired)
                    continue;
                double time = (i + 1d) / 48000d - pulse.AgeSeconds;
                if (pulse.Bank == 0)
                {
                    double interval = time - previousPair;
                    minimum = Math.Min(minimum, interval);
                    maximum = Math.Max(maximum, interval);
                    Assert.That(interval, Is.InRange(0.989999d / 600d, 1.010001d / 600d));
                }
                else
                {
                    Assert.That(time - previousPair, Is.EqualTo(2d / 600d).Within(0.00000001d));
                    previousPair = time;
                }
            }
            Assert.That(maximum - minimum, Is.GreaterThan(0.001d / 600d), "Detune must produce slow drift, not duplicate a static offset.");
        }

        [TestCase(0u)]
        [TestCase(123u)]
        [TestCase(uint.MaxValue)]
        public void SeededRenderIsIdenticalAcrossBufferBoundaries(uint seed)
        {
            var settings = new PulseEngineSettings(seed: seed);
            var whole = new PulseEngineSynthesizer(48000, settings);
            var split = new PulseEngineSynthesizer(48000, settings);
            var expected = new float[8192];
            var first = new float[2048];
            var second = new float[6144];
            whole.Render(expected, 2, 600f, 0.8f, 1f, 0.4f);
            split.Render(first, 2, 600f, 0.8f, 1f, 0.4f);
            split.Render(second, 2, 600f, 0.8f, 1f, 0.4f);
            for (int i = 0; i < expected.Length; i++)
                Assert.That(i < first.Length ? first[i] : second[i - first.Length], Is.EqualTo(expected[i]));
        }

        [Test]
        public void DifferentSeedsChangePulseJitterEvenWithNoiseOff()
        {
            var first = new float[12000];
            var second = new float[12000];
            new PulseEngineSynthesizer(48000, new PulseEngineSettings(seed: 123), noiseAmount: 0f)
                .Render(first, 1, 600f, 1f, 1f, 0.4f);
            new PulseEngineSynthesizer(48000, new PulseEngineSettings(seed: 456), noiseAmount: 0f)
                .Render(second, 1, 600f, 1f, 1f, 0.4f);
            Assert.That(second, Is.Not.EqualTo(first));
        }

        [TestCase(8000)]
        [TestCase(44100)]
        [TestCase(48000)]
        [TestCase(96000)]
        [TestCase(384000)]
        public void PulseOutputIsFiniteBoundedAudibleAndStartsQuietly(int rate)
        {
            var synth = new PulseEngineSynthesizer(rate);
            var samples = new float[rate / 5 * 2];
            synth.Render(samples, 2, 850f, 1f, 1f, 1f, 1f);
            Assert.That(Math.Abs(samples[0]), Is.LessThan(0.001f));
            Assert.That(samples, Is.All.InRange(-0.85f, 0.85f));
            Assert.That(Array.Exists(samples, sample => Math.Abs(sample) > 0.01f), Is.True);
            for (int i = 0; i < samples.Length; i += 2)
                Assert.That(samples[i], Is.EqualTo(samples[i + 1]));
        }

        [TestCase(8000, 1f, 0.2f)]
        [TestCase(8000, float.MaxValue, 20f)]
        [TestCase(48000, 1f, 20f)]
        [TestCase(48000, float.MaxValue, 0.2f)]
        [TestCase(1536000, 1f, 20f)]
        public void ResonantFilterStateIsStableUnderSustainedAndImpulseInputs(int rate, float frequency, float q)
        {
            var filter = new ResonantBandpass(rate, frequency, q);
            for (int i = 0; i < 48000; i++)
            {
                double sample = filter.Process(i < 24000 ? Math.Sin(i * 0.123d) : i == 24000 ? 1d : 0d);
                Assert.That(double.IsNaN(sample) || double.IsInfinity(sample), Is.False);
                Assert.That(Math.Abs(sample), Is.LessThan(5d), "The filter itself must remain stable before soft clipping.");
            }
        }

        [TestCase(0.0001f)]
        [TestCase(0.02f)]
        public void ExtremePulseWidthAndFilterControlsStayBoundedThroughChanges(float width)
        {
            var resonances = new[] { new PulseResonance(1f, 20f, 1f),
                new PulseResonance(float.MaxValue, 20f, 1f), new PulseResonance(3600f, 0.2f, 1f) };
            var settings = new PulseEngineSettings(width, 0.1f, 0.02f, 0.2f, 0.1f, resonances: resonances);
            var voice = new EngineVoiceSettings(lowPassMin: float.Epsilon, lowPassMax: float.MaxValue);
            var synth = new PulseEngineSynthesizer(8000, settings, voice);
            var samples = new float[2048];
            foreach (float frequency in new[] { 100f, 850f, 0f, float.MaxValue, 600f })
            {
                synth.Render(samples, 1, frequency, 1f, 1f, 1f, 1f);
                Assert.That(samples, Is.All.InRange(-0.85f, 0.85f));
            }
        }

        [Test]
        public void ABLiveCrossfadeConvergesToUnchangedAdditiveOutput()
        {
            var additive = new EngineSynthesizer(48000);
            var synth = new PulseEngineSynthesizer(48000);
            var expected = new float[12000];
            var actual = new float[12000];
            additive.Render(expected, 1, 600f, 1f, 1f, 0.4f);
            synth.Render(actual, 1, 600f, 1f, 1f, 0.4f);
            Assert.That(actual, Is.Not.EqualTo(expected));
            additive.Render(expected, 1, 600f, 1f, 1f, 0.4f);
            synth.Render(actual, 1, 600f, 1f, 1f, 0.4f, useAdditive: true);
            for (int i = 11000; i < actual.Length; i++)
                Assert.That(actual[i], Is.EqualTo(expected[i]).Within(0.00001f));
            Assert.That(actual, Is.All.InRange(-0.85f, 0.85f));
            synth.Render(actual, 1, 850f, 1f, 1f, 0.4f);
            Assert.That(actual, Is.All.InRange(-0.85f, 0.85f));
        }

        [Test]
        public void PulsePitchScaleChangesSpacingWithoutChangingFiringMath()
        {
            var train = new PulseTrain(48000, RegularPulses());
            var voice = new EngineVoiceSettings(pitchScale: 0.5f);
            int count = 0;
            for (int i = 0; i < 48000; i++)
                if (train.Advance(AudioMath.FiringFrequency(6000f) * voice.PitchScale).Fired)
                    count++;
            Assert.That(count, Is.InRange(299, 300));
            Assert.That(AudioMath.FiringFrequency(6000f), Is.EqualTo(600f));
            Assert.That(voice.CutoffAtRpm(6000f), Is.EqualTo(3833.333f).Within(0.001f));
        }

        [Test]
        public void RendererPitchScaleMovesThePulseFundamental()
        {
            var scaled = new PulseEngineSynthesizer(48000, RegularPulses(),
                new EngineVoiceSettings(pitchScale: 0.5f), noiseAmount: 0f);
            var normal = new PulseEngineSynthesizer(48000, RegularPulses(), noiseAmount: 0f);
            var lower = new float[48000];
            var original = new float[48000];
            scaled.Render(lower, 1, 600f, 1f, 1f, 1f);
            normal.Render(original, 1, 600f, 1f, 1f, 1f);
            scaled.Render(lower, 1, 600f, 1f, 1f, 1f);
            normal.Render(original, 1, 600f, 1f, 1f, 1f);
            Assert.That(ToneAmplitude(lower, 300d), Is.GreaterThan(0.005d));
            Assert.That(ToneAmplitude(original, 300d), Is.LessThan(0.00001d));
            Assert.That(ToneAmplitude(original, 600d), Is.GreaterThan(0.005d));
        }

        [Test]
        public void ShortPulsesAtLowSampleRateSuppressAboveNyquistHarmonicAlias()
        {
            var resonances = new[] { new PulseResonance(3600f, 0.2f, 1f),
                new PulseResonance(3600f, 0.2f, 1f), new PulseResonance(3600f, 0.2f, 1f) };
            var settings = new PulseEngineSettings(0.0001f, 0f, 0f, 0f, 0f, resonances: resonances);
            var voice = new EngineVoiceSettings(lowPassMin: 5000f, lowPassMax: 5000f);
            var synth = new PulseEngineSynthesizer(8000, settings, voice, noiseAmount: 0f);
            var samples = new float[8000];
            synth.Render(samples, 1, 1350f, 1f, 1f, 0.4f);
            synth.Render(samples, 1, 1350f, 1f, 1f, 0.4f);
            double fundamental = ToneAmplitude(samples, 1350d, 8000);
            double foldedThird = ToneAmplitude(samples, 3950d, 8000); // 4050 Hz folds around 4000 Hz.
            Assert.That(fundamental, Is.GreaterThan(0.005d));
            Assert.That(foldedThird / fundamental, Is.LessThan(0.002d));
        }

        [Test]
        public void RenderingBothVoicesAndABSwitchesAllocatesNoManagedMemoryAfterWarmup()
        {
            var synth = new PulseEngineSynthesizer(48000);
            var samples = new float[512];
            for (int i = 0; i < 16; i++)
                synth.Render(samples, 2, 600f, 1f, 1f, 0.4f, useAdditive: i % 2 == 0);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++)
                synth.Render(samples, 2, i % 2 == 0 ? 850f : 100f, 0.8f, 1f, 0.4f, useAdditive: i % 3 == 0);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
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

        [Test]
        public void ConstructorClonesResonanceSettings()
        {
            var input = new[] { new PulseResonance(180f, 1f, 1f), new PulseResonance(650f, 1f, 1f),
                new PulseResonance(1800f, 1f, 1f) };
            var settings = new PulseEngineSettings(resonances: input);
            input[0] = new PulseResonance(900f, 2f, 0f);
            Assert.That(settings.ResonanceAt(0).Frequency, Is.EqualTo(180f));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.ResonanceAt(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.ResonanceAt(3));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidPulseAndFilterValuesAreRejected(float value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PulseEngineSettings(pulseWidth: value));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PulseEngineSettings(bankTimingOffset: value));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PulseEngineSettings(bankDetune: value));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PulseEngineSettings(amplitudeJitter: value));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PulseEngineSettings(timingJitter: value));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResonantBandpass(48000, value, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResonantBandpass(48000, 100f, value));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PulseTrain(48000).Advance(value));
        }

        [Test]
        public void InvalidRangesAndRenderBuffersAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PulseEngineSettings(pulseWidth: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PulseEngineSettings(pulseWidth: 0.03f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PulseEngineSettings(bankTimingOffset: 0.11f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PulseEngineSettings(bankDetune: 0.03f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PulseEngineSettings(amplitudeJitter: 0.3f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PulseEngineSettings(timingJitter: 0.2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PulseEngineSettings(resonances: new PulseResonance[3]));
            Assert.Throws<ArgumentException>(() => new PulseEngineSettings(resonances: new PulseResonance[2]));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResonantBandpass(48000, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResonantBandpass(48000, 100f, 21f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResonantBandpass(0, 100f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PulseTrain(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PulseTrain(8000).Advance(4000d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PulseEngineSynthesizer(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PulseEngineSynthesizer(48000, rampTime: 0f));
            var synth = new PulseEngineSynthesizer(48000);
            Assert.Throws<ArgumentNullException>(() => synth.Render(null, 1, 100f, 1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => synth.Render(new float[3], 2, 100f, 1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => synth.Render(new float[2], 1, float.NaN, 1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => synth.Render(new float[2], 1, 100f, 2f, 1f, 1f));
        }

        private static PulseEngineSettings RegularPulses()
        {
            return new PulseEngineSettings(bankTimingOffset: 0f, bankDetune: 0f, amplitudeJitter: 0f, timingJitter: 0f);
        }

        [Test]
        public void PulseVoiceExistsInPureCore()
        {
            Assert.That(typeof(EngineSynthesizer).Assembly.GetType("Ghostline.Core.PulseEngineSynthesizer"),
                Is.Not.Null, "Cylinder firing synthesis must live in pure Core.");
        }
    }
}
