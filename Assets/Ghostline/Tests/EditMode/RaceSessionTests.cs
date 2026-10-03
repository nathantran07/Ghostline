using Ghostline.Core;
using NUnit.Framework;

namespace Ghostline.Tests.EditMode
{
    public sealed class RaceSessionTests
    {
        [Test]
        public void FinishRequiresStartOrderedCheckpointsAndAnotherFinishCrossing()
        {
            var race = new RaceSession(4);
            Assert.That(race.PassCheckpoint(0), Is.False);
            Assert.That(race.CrossStartFinish(0f, 0f, 0f), Is.True);
            race.Tick(1f, 1f, 0f, 0f);
            Assert.That(race.CrossStartFinish(1f, 0f, 0f), Is.False);
            Assert.That(race.PassCheckpoint(1), Is.False);
            for (int i = 0; i < 4; i++)
                Assert.That(race.PassCheckpoint(i), Is.True);
            race.Tick(1f, 2f, 0f, 0f);
            Assert.That(race.CrossStartFinish(2f, 0f, 0f), Is.True);
            Assert.That(race.Timer.State, Is.EqualTo(LapTimerState.Finished));
            Assert.That(race.CompletedLap.LapTime, Is.EqualTo(2f));
            Assert.That(race.CompletedLap.Samples[0].Time, Is.Zero);
            Assert.That(race.CrossStartFinish(2f, 0f, 0f), Is.False);
            race.Reset();
            Assert.That(race.Timer.State, Is.EqualTo(LapTimerState.NotStarted));
            Assert.That(race.CompletedLap, Is.Null);
        }
    }
}
