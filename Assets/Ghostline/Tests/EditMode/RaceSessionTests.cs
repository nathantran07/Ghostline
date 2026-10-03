using Ghostline.Core;
using NUnit.Framework;

namespace Ghostline.Tests.EditMode
{
    public sealed class RaceSessionTests
    {
        [TestCase(4)]
        [TestCase(12)]
        [TestCase(1)]
        public void FinishRequiresStartOrderedCheckpointsAndAnotherFinishCrossing(int count)
        {
            var race = new RaceSession(count);
            Assert.That(race.Checkpoints.CheckpointCount, Is.EqualTo(count));
            Assert.That(race.PassCheckpoint(0), Is.False);
            Assert.That(race.CrossStartFinish(0f, 0f, 0f), Is.True);
            race.Tick(1f, 1f, 0f, 0f);
            Assert.That(race.CrossStartFinish(1f, 0f, 0f), Is.False);
            Assert.That(race.PassCheckpoint(1), Is.False);
            Assert.That(race.PassCheckpoint(-1), Is.False);
            Assert.That(race.PassCheckpoint(count), Is.False);
            for (int i = 0; i < count; i++)
            {
                Assert.That(race.CrossStartFinish(1f, 0f, 0f), Is.False);
                Assert.That(race.PassCheckpoint(i), Is.True);
                Assert.That(race.PassCheckpoint(i), Is.False);
                if (i + 2 < count)
                    Assert.That(race.PassCheckpoint(i + 2), Is.False);
            }
            race.Tick(1f, 2f, 0f, 0f);
            Assert.That(race.CrossStartFinish(2f, 0f, 0f), Is.True);
            Assert.That(race.Timer.State, Is.EqualTo(LapTimerState.Finished));
            Assert.That(race.CompletedLap.LapTime, Is.EqualTo(2f));
            Assert.That(race.CompletedLap.Samples[0].Time, Is.Zero);
            Assert.That(race.CrossStartFinish(2f, 0f, 0f), Is.False);
            race.Reset();
            Assert.That(race.Timer.State, Is.EqualTo(LapTimerState.NotStarted));
            Assert.That(race.CompletedLap, Is.Null);
            Assert.That(race.Checkpoints.NextCheckpointIndex, Is.Zero);
            Assert.That(race.PassCheckpoint(0), Is.False);
            Assert.That(race.CrossStartFinish(0f, 0f, 0f), Is.True);
            Assert.That(race.CrossStartFinish(0f, 0f, 0f), Is.False);
            Assert.That(race.PassCheckpoint(0), Is.True);
        }

        [Test]
        public void RecordsAcceptedSplitsInOrderAndClearsThemOnReset()
        {
            var race = new RaceSession(2);
            race.CrossStartFinish(0f, 0f, 0f);
            race.Tick(1f, 0f, 0f, 0f);
            Assert.That(race.PassCheckpoint(1), Is.False);
            Assert.That(race.PassCheckpoint(0), Is.True);
            Assert.That(race.PassCheckpoint(0), Is.False);
            race.Tick(2f, 0f, 0f, 0f);
            Assert.That(race.PassCheckpoint(1), Is.True);
            Assert.That(race.Splits, Is.EqualTo(new[] { 1f, 3f }));
            race.Tick(1f, 0f, 0f, 0f);
            race.CrossStartFinish(0f, 0f, 0f);
            Assert.That(race.CompletedLap.Splits, Is.EqualTo(new[] { 1f, 3f }));
            Assert.That(race.CompletedLap.LapTime, Is.EqualTo(4f));
            race.Reset();
            Assert.That(race.Splits, Is.Empty);
            Assert.That(race.CompletedLap, Is.Null);
        }
    }
}
