using System;
using System.Collections.Generic;
using Ghostline.Core;
using NUnit.Framework;

namespace Ghostline.Tests.EditMode
{
    public sealed class GhostRecordingTests
    {
        [TestCase(-1f, 0f, 0f, 0f)]
        [TestCase(0f, 0f, 0f, 0f)]
        [TestCase(1f, 5f, 10f, 45f)]
        [TestCase(2f, 10f, 20f, 90f)]
        [TestCase(3f, 10f, 20f, 90f)]
        public void EvaluationInterpolatesAndClamps(float time, float x, float y, float rotation)
        {
            var recording = new GhostRecording(new[]
            {
                new GhostSample(0f, 0f, 0f, 0f),
                new GhostSample(2f, 10f, 20f, 90f)
            });
            GhostSample sample = recording.Evaluate(time);
            Assert.That(sample.X, Is.EqualTo(x).Within(0.0001f));
            Assert.That(sample.Y, Is.EqualTo(y).Within(0.0001f));
            Assert.That(sample.Rotation, Is.EqualTo(rotation).Within(0.0001f));
        }

        [Test]
        public void RotationTakesShortestPathAcrossZero()
        {
            var recording = new GhostRecording(new[]
            {
                new GhostSample(0f, 0f, 0f, 350f),
                new GhostSample(1f, 0f, 0f, 10f)
            });
            Assert.That(recording.Evaluate(0.5f).Rotation % 360f, Is.Zero.Within(0.0001f));
        }

        [Test]
        public void RecordingCopiesInputAndSupportsSingleSample()
        {
            var samples = new List<GhostSample> { new GhostSample(0f, 4f, 5f, 6f) };
            var recording = new GhostRecording(samples);
            samples.Clear();
            Assert.That(recording.Samples.Count, Is.EqualTo(1));
            Assert.That(recording.Evaluate(10f).X, Is.EqualTo(4f));
        }

        [Test]
        public void EmptyUnorderedAndNonFiniteSamplesAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new GhostRecording(new GhostSample[0]));
            Assert.Throws<ArgumentException>(() => new GhostRecording(new[]
            {
                new GhostSample(1f, 0f, 0f, 0f), new GhostSample(1f, 1f, 1f, 1f)
            }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GhostSample(0f, float.NaN, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GhostSample(-1f, 0f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GhostRecording(new[]
            {
                new GhostSample(0f, 0f, 0f, 0f)
            }).Evaluate(float.NaN));
        }
    }
}
