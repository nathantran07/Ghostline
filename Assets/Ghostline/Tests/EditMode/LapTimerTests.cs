using System;
using Ghostline.Core;
using NUnit.Framework;

namespace Ghostline.Tests.EditMode
{
    public sealed class LapTimerTests
    {
        [Test]
        public void TimeAccumulatesOnlyWhileRunning()
        {
            var timer = new LapTimer();
            Assert.That(timer.State, Is.EqualTo(LapTimerState.NotStarted));
            timer.Tick(5f);
            Assert.That(timer.ElapsedTime, Is.Zero);
            Assert.That(timer.Finish(), Is.False);
            Assert.That(timer.Start(), Is.True);
            Assert.That(timer.Start(), Is.False);
            timer.Tick(0.5f);
            timer.Tick(1.5f);
            Assert.That(timer.ElapsedTime, Is.EqualTo(2f));
            Assert.That(timer.Finish(), Is.True);
            Assert.That(timer.State, Is.EqualTo(LapTimerState.Finished));
            Assert.That(timer.Finish(), Is.False);
            Assert.That(timer.Start(), Is.False);
            timer.Tick(9f);
            Assert.That(timer.ElapsedTime, Is.EqualTo(2f));
            timer.Reset();
            Assert.That(timer.State, Is.EqualTo(LapTimerState.NotStarted));
            Assert.That(timer.ElapsedTime, Is.Zero);
            Assert.That(timer.Start(), Is.True);
        }

        [Test]
        public void EventsAreRaisedOnceForEachTransition()
        {
            var timer = new LapTimer();
            int starts = 0;
            int resets = 0;
            float finishTime = -1f;
            timer.Started += () => starts++;
            timer.ResetOccurred += () => resets++;
            timer.Finished += time => finishTime = time;
            timer.Start();
            timer.Start();
            timer.Tick(3f);
            timer.Finish();
            timer.Finish();
            timer.Reset();
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(resets, Is.EqualTo(1));
            Assert.That(finishTime, Is.EqualTo(3f));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDeltaTimeIsRejected(float deltaTime)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LapTimer().Tick(deltaTime));
        }
    }
}
