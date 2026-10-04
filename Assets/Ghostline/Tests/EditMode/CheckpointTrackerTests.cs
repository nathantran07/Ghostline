using System;
using Ghostline.Core;
using NUnit.Framework;

namespace Ghostline.Tests.EditMode
{
    public sealed class CheckpointTrackerTests
    {
        [TestCase(4)]
        [TestCase(12)]
        [TestCase(1)]
        public void OutOfOrderSkippedAndDuplicateCheckpointsAreRejected(int count)
        {
            var tracker = new CheckpointTracker(count);
            Assert.That(tracker.CheckpointCount, Is.EqualTo(count));
            Assert.That(tracker.TryPass(1), Is.False);
            if (count > 1)
                Assert.That(tracker.TryPass(count - 1), Is.False);
            Assert.That(tracker.TryCompleteLap(), Is.False);
            Assert.That(tracker.TryPass(0), Is.True);
            Assert.That(tracker.TryPass(0), Is.False);
            Assert.That(tracker.TryPass(2), Is.False);
            Assert.That(tracker.NextCheckpointIndex, Is.EqualTo(1));
            Assert.That(tracker.TryPass(-1), Is.False);
            Assert.That(tracker.TryPass(count), Is.False);
        }

        [TestCase(4)]
        [TestCase(12)]
        [TestCase(1)]
        public void LapCompletesOnlyAfterOrderedCheckpointsAndFinish(int count)
        {
            var tracker = new CheckpointTracker(count);
            int completions = 0;
            tracker.LapCompleted += () => completions++;
            for (int i = 0; i < count; i++)
            {
                Assert.That(tracker.TryCompleteLap(), Is.False);
                Assert.That(tracker.TryPass(i), Is.True);
            }
            Assert.That(tracker.NextCheckpointIndex, Is.EqualTo(count));
            Assert.That(completions, Is.Zero);
            Assert.That(tracker.TryCompleteLap(), Is.True);
            Assert.That(tracker.TryCompleteLap(), Is.False);
            Assert.That(completions, Is.EqualTo(1));
            tracker.Reset();
            Assert.That(tracker.NextCheckpointIndex, Is.Zero);
            Assert.That(tracker.CheckpointCount, Is.EqualTo(count));
            Assert.That(tracker.TryCompleteLap(), Is.False);
            for (int i = 0; i < count; i++)
                Assert.That(tracker.TryPass(i), Is.True);
            Assert.That(tracker.TryCompleteLap(), Is.True);
            Assert.That(completions, Is.EqualTo(2));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void CheckpointCountMustBePositive(int count)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CheckpointTracker(count));
        }
    }
}
