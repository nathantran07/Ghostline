using System;
using Ghostline.Core;
using NUnit.Framework;

namespace Ghostline.Tests.EditMode
{
    public sealed class CheckpointTrackerTests
    {
        [Test]
        public void OutOfOrderSkippedAndDuplicateCheckpointsAreRejected()
        {
            var tracker = new CheckpointTracker(4);
            Assert.That(tracker.TryPass(1), Is.False);
            Assert.That(tracker.TryPass(3), Is.False);
            Assert.That(tracker.TryCompleteLap(), Is.False);
            Assert.That(tracker.TryPass(0), Is.True);
            Assert.That(tracker.TryPass(0), Is.False);
            Assert.That(tracker.TryPass(2), Is.False);
            Assert.That(tracker.NextCheckpointIndex, Is.EqualTo(1));
            Assert.That(tracker.TryPass(-1), Is.False);
            Assert.That(tracker.TryPass(4), Is.False);
        }

        [Test]
        public void LapCompletesOnlyAfterFourOrderedCheckpointsAndFinish()
        {
            var tracker = new CheckpointTracker(4);
            int completions = 0;
            tracker.LapCompleted += () => completions++;
            for (int i = 0; i < 4; i++)
                Assert.That(tracker.TryPass(i), Is.True);
            Assert.That(completions, Is.Zero);
            Assert.That(tracker.TryCompleteLap(), Is.True);
            Assert.That(tracker.TryCompleteLap(), Is.False);
            Assert.That(completions, Is.EqualTo(1));
            tracker.Reset();
            Assert.That(tracker.NextCheckpointIndex, Is.Zero);
            Assert.That(tracker.TryPass(0), Is.True);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void CheckpointCountMustBePositive(int count)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CheckpointTracker(count));
        }
    }
}
