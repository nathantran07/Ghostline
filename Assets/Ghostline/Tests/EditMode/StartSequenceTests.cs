using System;
using Ghostline.Core;
using NUnit.Framework;

namespace Ghostline.Tests.EditMode
{
    public sealed class StartSequenceTests
    {
        [Test]
        public void DefaultLabelsAndDrivingFollowExactBoundaries()
        {
            var sequence = new StartSequence();
            Assert.That(sequence.State, Is.EqualTo(StartSequenceState.Counting));
            Assert.That(sequence.Label, Is.EqualTo("3"));
            Assert.That(sequence.DrivingAllowed, Is.False);
            sequence.Tick(0.5f);
            Assert.That(sequence.Label, Is.EqualTo("3"));
            sequence.Tick(0.5f);
            Assert.That(sequence.Label, Is.EqualTo("2"));
            Assert.That(sequence.DrivingAllowed, Is.False);
            sequence.Tick(1f);
            Assert.That(sequence.Label, Is.EqualTo("1"));
            Assert.That(sequence.DrivingAllowed, Is.False);
            sequence.Tick(1f);
            Assert.That(sequence.State, Is.EqualTo(StartSequenceState.Go));
            Assert.That(sequence.Label, Is.EqualTo("GO"));
            Assert.That(sequence.DrivingAllowed, Is.True);
            sequence.Tick(0.5f);
            Assert.That(sequence.Label, Is.EqualTo("GO"));
            sequence.Tick(0.25f);
            Assert.That(sequence.State, Is.EqualTo(StartSequenceState.Done));
            Assert.That(sequence.Label, Is.Empty);
            Assert.That(sequence.DrivingAllowed, Is.True);
            sequence.Tick(100f);
            Assert.That(sequence.State, Is.EqualTo(StartSequenceState.Done));
            sequence.Reset();
            Assert.That(sequence.Label, Is.EqualTo("3"));
            Assert.That(sequence.DrivingAllowed, Is.False);
        }

        [TestCase(0f, "3", false)]
        [TestCase(2.5f, "1", false)]
        [TestCase(3.5f, "GO", true)]
        [TestCase(3.75f, "", true)]
        [TestCase(100f, "", true)]
        public void LargeTicksCarryAcrossAllTransitions(float tick, string label, bool driving)
        {
            var sequence = new StartSequence();
            sequence.Tick(tick);
            Assert.That(sequence.Label, Is.EqualTo(label));
            Assert.That(sequence.DrivingAllowed, Is.EqualTo(driving));
        }

        [Test]
        public void CustomDurationsReachGoAndDoneOnTime()
        {
            var sequence = new StartSequence(0.5f, 0.25f);
            sequence.Tick(0.5f);
            Assert.That(sequence.Label, Is.EqualTo("2"));
            sequence.Tick(1f);
            Assert.That(sequence.Label, Is.EqualTo("GO"));
            sequence.Tick(0.25f);
            Assert.That(sequence.Label, Is.Empty);
        }

        [TestCase(0.02f)]
        [TestCase(0.0199999921f)]
        public void FixedStepsReachGoAtThreeSeconds(float step)
        {
            var sequence = new StartSequence();
            for (int i = 0; i < 149; i++)
                sequence.Tick(step);
            Assert.That(sequence.State, Is.EqualTo(StartSequenceState.Counting));
            sequence.Tick(step);
            Assert.That(sequence.State, Is.EqualTo(StartSequenceState.Go));
        }

        [TestCase(0f, 1, false)]
        [TestCase(0.599f, 1, false)]
        [TestCase(0.6f, 2, false)]
        [TestCase(1.199f, 2, false)]
        [TestCase(1.2f, 3, false)]
        [TestCase(1.799f, 3, false)]
        [TestCase(1.8f, 4, false)]
        [TestCase(2.399f, 4, false)]
        [TestCase(2.4f, 5, false)]
        [TestCase(2.999f, 5, false)]
        [TestCase(3f, 0, true)]
        [TestCase(3.749f, 0, true)]
        [TestCase(3.75f, 0, true)]
        [TestCase(100f, 0, true)]
        public void GantryLampBoundariesAndSkippedPhases(float tick, int litLamps, bool driving)
        {
            var sequence = new StartSequence();
            sequence.Tick(tick);
            Assert.That(sequence.LitLampCount, Is.EqualTo(litLamps));
            Assert.That(sequence.DrivingAllowed, Is.EqualTo(driving));
            sequence.Reset();
            Assert.That(sequence.LitLampCount, Is.EqualTo(1));
            Assert.That(sequence.DrivingAllowed, Is.False);
        }

        [TestCase(0.02f)]
        [TestCase(0.0199999921f)]
        public void GantryLampChangesFollowFixedSteps(float step)
        {
            var sequence = new StartSequence();
            for (int tick = 0; tick <= 150; tick++)
            {
                Assert.That(sequence.LitLampCount, Is.EqualTo(tick == 150 ? 0 : 1 + tick / 30),
                    $"Lamp state at fixed tick {tick}");
                Assert.That(sequence.DrivingAllowed, Is.EqualTo(tick == 150));
                sequence.Tick(step);
            }
        }

        [Test]
        public void GantryUsesConfiguredCountdownDurationWithoutChangingStartTime()
        {
            var sequence = new StartSequence(0.5f, 0.25f);
            sequence.Tick(0.3f);
            Assert.That(sequence.LitLampCount, Is.EqualTo(2));
            sequence.Tick(1.2f);
            Assert.That(sequence.LitLampCount, Is.Zero);
            Assert.That(sequence.State, Is.EqualTo(StartSequenceState.Go));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidTicksAreRejectedWithoutAdvancing(float tick)
        {
            var sequence = new StartSequence();
            Assert.Throws<ArgumentOutOfRangeException>(() => sequence.Tick(tick));
            Assert.That(sequence.Label, Is.EqualTo("3"));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDurationsAreRejected(float duration)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StartSequence(duration, 0.75f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StartSequence(1f, duration));
        }
    }
}
