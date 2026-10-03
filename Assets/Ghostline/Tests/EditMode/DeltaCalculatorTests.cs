using System;
using Ghostline.Core;
using NUnit.Framework;

namespace Ghostline.Tests.EditMode
{
    public sealed class DeltaCalculatorTests
    {
        [TestCase(4.858f, -0.142f)]
        [TestCase(5.312f, 0.312f)]
        [TestCase(5f, 0f)]
        public void CheckpointDeltaIsCurrentMinusBest(float current, float expected)
        {
            var best = new BestLapData { LapTime = 10f, Splits = new[] { 2f, 5f } };
            Assert.That(DeltaCalculator.AtCheckpoint(best, 1, current), Is.EqualTo(expected).Within(0.00001f));
        }

        [Test]
        public void MissingBestHasNoCheckpointOrFinishDelta()
        {
            Assert.That(DeltaCalculator.AtCheckpoint(null, 0, 2f), Is.Null);
            Assert.That(DeltaCalculator.AtFinish(null, 10f), Is.Null);
        }

        [TestCase(9f, -1f)]
        [TestCase(11f, 1f)]
        [TestCase(10f, 0f)]
        public void FinishDeltaUsesPreviousBestDuration(float current, float expected)
        {
            var best = new BestLapData { LapTime = 10f, Splits = new[] { 5f } };
            Assert.That(DeltaCalculator.AtFinish(best, current), Is.EqualTo(expected));
            Assert.That(best.LapTime, Is.EqualTo(10f));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void InvalidCheckpointIndexIsRejected(int index)
        {
            var best = new BestLapData { Splits = new[] { 1f, 2f } };
            Assert.Throws<ArgumentOutOfRangeException>(() => DeltaCalculator.AtCheckpoint(best, index, 1f));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidCurrentTimeIsRejected(float current)
        {
            var best = new BestLapData { LapTime = 2f, Splits = new[] { 1f } };
            Assert.Throws<ArgumentOutOfRangeException>(() => DeltaCalculator.AtCheckpoint(best, 0, current));
            Assert.Throws<ArgumentOutOfRangeException>(() => DeltaCalculator.AtFinish(best, current));
        }
    }
}
