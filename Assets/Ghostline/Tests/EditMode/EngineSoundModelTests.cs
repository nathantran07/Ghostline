using NUnit.Framework;
using Ghostline.Core;
using System;

namespace Ghostline.Tests.EditMode
{
    public sealed class EngineSoundModelTests
    {
        [Test]
        public void EngineModelIsAvailableInTheEngineFreeCoreAssembly()
        {
            Assert.That(typeof(StartSequence).Assembly.GetType("Ghostline.Core.EngineSoundModel"), Is.Not.Null);
        }

        [Test]
        public void DefaultsAndIdleMatchTheV12Specification()
        {
            var settings = new EngineSoundSettings();
            Assert.That(settings.IdleRpm, Is.EqualTo(1000f));
            Assert.That(settings.RedlineRpm, Is.EqualTo(8500f));
            Assert.That(settings.GearCount, Is.EqualTo(7));
            Assert.That(settings.ShiftGap, Is.EqualTo(0.08f));
            Assert.That(new EngineSoundModel(settings).Tick(0f, 1f, 100f, 1f).Rpm, Is.EqualTo(1000f));
        }

        [Test]
        public void RpmRisesWithinTheFirstGear()
        {
            var model = new EngineSoundModel();
            float low = model.Tick(5f, 1f, 100f, 1f).Rpm;
            EngineSoundFrame high = model.Tick(15f, 1f, 100f, 1f);
            Assert.That(high.Rpm, Is.GreaterThan(low));
            Assert.That(high.Gear, Is.EqualTo(1));
        }

        [Test]
        public void ShiftCutsAudioThrottleForExactlyItsGapThenDropsRpm()
        {
            var model = new EngineSoundModel();
            float before = model.Tick(15.9f, 1f, 100f, 1f).Rpm;
            EngineSoundFrame gap = model.Tick(17f, 1f, 100f, 0.079f);
            Assert.That(gap.IsShifting, Is.True);
            Assert.That(gap.EffectiveThrottle, Is.Zero);
            Assert.That(gap.Gear, Is.EqualTo(1));
            EngineSoundFrame after = model.Tick(17f, 1f, 100f, 0.001f);
            Assert.That(after.IsShifting, Is.False);
            Assert.That(after.EffectiveThrottle, Is.EqualTo(1f));
            Assert.That(after.Gear, Is.EqualTo(2));
            Assert.That(after.Rpm, Is.LessThan(before));
        }

        [Test]
        public void ReleasedThrottleSmoothlyLowersRpmAndLoudness()
        {
            var model = new EngineSoundModel();
            EngineSoundFrame on = model.Tick(15f, 1f, 100f, 1f);
            EngineSoundFrame off = model.Tick(15f, 0f, 100f, 0.02f);
            Assert.That(off.Rpm, Is.InRange(1000.01f, on.Rpm - 0.01f));
            Assert.That(off.Loudness, Is.LessThan(on.Loudness));
            Assert.That(model.Tick(15f, 0f, 100f, 10f).Rpm, Is.EqualTo(1000f).Within(0.01f));
        }

        [Test]
        public void LargeTicksCarryAcrossGearsAndResetClearsPendingShift()
        {
            var model = new EngineSoundModel();
            Assert.That(model.Tick(100f, 1f, 100f, 10f).Gear, Is.EqualTo(7));
            Assert.That(model.Tick(10f, 1f, 100f, 10f).Gear, Is.EqualTo(1));
            model.Tick(17f, 1f, 100f, 0.01f);
            model.Reset();
            Assert.That(model.Current.IsShifting, Is.False);
            Assert.That(model.Current.Rpm, Is.EqualTo(1000f));
        }

        [Test]
        public void HysteresisPreventsBoundaryChatterAndZeroTimePreservesState()
        {
            var model = new EngineSoundModel();
            model.Tick(17f, 1f, 100f, 1f);
            Assert.That(model.Tick(15.9f, 1f, 100f, 0.02f).Gear, Is.EqualTo(2));
            EngineSoundFrame before = model.Current;
            Assert.That(model.Tick(0f, 0f, 100f, 0f).Rpm, Is.EqualTo(before.Rpm));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExtremeValidInputsAndLimiterRemainBounded(bool limiter)
        {
            var model = new EngineSoundModel(new EngineSoundSettings(limiterEnabled: limiter));
            for (int i = 0; i < 100; i++)
            {
                EngineSoundFrame frame = model.Tick(i % 3 == 0 ? float.MaxValue : 100f,
                    i % 5 == 0 ? 0f : 1f, float.Epsilon, i == 0 ? float.MaxValue : 0.02f);
                Assert.That(frame.Rpm, Is.InRange(1000f, 8500f));
                Assert.That(frame.Loudness, Is.InRange(0f, 1f));
                Assert.That(frame.EffectiveThrottle, Is.InRange(0f, 1f));
                Assert.That(frame.Gear, Is.InRange(1, 7));
            }
        }

        [Test]
        public void LimiterBounceVariesAtRedlineWhenEnabled()
        {
            var model = new EngineSoundModel(new EngineSoundSettings(gearCount: 1, riseTime: 0f,
                fallTime: 0f, limiterEnabled: true));
            float first = model.Tick(100f, 1f, 100f, 0.02f).Rpm;
            Assert.That(model.Tick(100f, 1f, 100f, 0.02f).Rpm, Is.Not.EqualTo(first));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidInputsAreRejectedWithoutAdvancing(float invalid)
        {
            var model = new EngineSoundModel();
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Tick(invalid, 1f, 100f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Tick(10f, invalid, 100f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Tick(10f, 1f, invalid, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Tick(10f, 1f, 100f, invalid));
            Assert.That(model.Current.Gear, Is.EqualTo(1));
            Assert.That(model.Current.Rpm, Is.EqualTo(1000f));
        }

        [Test]
        public void InvalidSettingsAndNonUnitThrottleAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineSoundSettings(idleRpm: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineSoundSettings(redlineRpm: 1000f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineSoundSettings(gearCount: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineSoundSettings(shiftGap: -1f));
            Assert.Throws<ArgumentException>(() => new EngineSoundSettings(gearEnds: new[] { 1f }));
            Assert.Throws<ArgumentException>(() => new EngineSoundSettings(gearCount: 2, gearEnds: new[] { 0.5f, 0.5f }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineSoundModel().Tick(1f, 1.1f, 100f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EngineSoundModel().Tick(1f, 1f, 0f, 1f));
        }

        [Test]
        public void SettingsOwnTheirGearFractions()
        {
            var ends = new[] { 0.5f, 1f };
            var settings = new EngineSoundSettings(gearCount: 2, gearEnds: ends);
            ends[0] = float.NaN;
            Assert.That(settings.GearEnd(1), Is.EqualTo(0.5f));
        }
    }
}
