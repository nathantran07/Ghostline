using System;
using NUnit.Framework;
using Ghostline.Core;

namespace Ghostline.Tests.EditMode
{
    public sealed class EngineSynthesizerTests
    {
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
            var synth = new EngineSynthesizer(8000, weights, noiseAmount: 0f);
            var warmup = new float[8000];
            synth.Render(warmup, 1, 600f, 1f, 1f, 1f);
            var buffer = new float[1024];
            synth.Render(buffer, 1, 600f, 1f, 1f, 1f);
            Assert.That(buffer, Is.All.EqualTo(0f));
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
            var quiet = new EngineSynthesizer(48000, new float[12], noiseAmount: 0.1f);
            var loud = new EngineSynthesizer(48000, new float[12], noiseAmount: 0.1f);
            var tire = new EngineSynthesizer(48000, new float[12], noiseAmount: 0f);
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
