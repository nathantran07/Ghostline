using System;
using Ghostline.Core;
using NUnit.Framework;

namespace Ghostline.Tests.EditMode
{
    public sealed class DrivingMathTests
    {
        [TestCase(0.02f)]
        [TestCase(0.05f)]
        [TestCase(0.1f)]
        public void ThrottleRampsAndReleasesInPointFourSeconds(float step)
        {
            float throttle = 0f;
            for (int i = 0; i < (int)Math.Round(0.4f / step); i++)
                throttle = DrivingMath.SmoothThrottle(throttle, 1f, 0.4f, step);
            Assert.That(throttle, Is.EqualTo(1f).Within(0.00001f));
            for (int i = 0; i < (int)Math.Round(0.4f / step); i++)
                throttle = DrivingMath.SmoothThrottle(throttle, 0f, 0.4f, step);
            Assert.That(throttle, Is.Zero.Within(0.00001f));
        }

        [Test]
        public void ThrottleHandlesImmediateRampAndCannotOvershoot()
        {
            Assert.That(DrivingMath.SmoothThrottle(0f, 1f, 0f, 0.02f), Is.EqualTo(1f));
            Assert.That(DrivingMath.SmoothThrottle(0.9f, 1f, 0.4f, 1f), Is.EqualTo(1f));
            Assert.That(DrivingMath.SmoothThrottle(0.5f, 0f, 0.4f, 0f), Is.EqualTo(0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => DrivingMath.SmoothThrottle(0f, 1f, -1f, 0.02f));
            Assert.Throws<ArgumentOutOfRangeException>(() => DrivingMath.SmoothThrottle(0f, 1f, 0.4f, float.NaN));
        }

        [Test]
        public void AccelerationMatchesThrottleTimesCurveTimesMaximum()
        {
            Assert.That(DrivingMath.Acceleration(0.5f, 6f, 12f, 18f, Curve()), Is.EqualTo(8.1f).Within(0.0001f));
            Assert.That(DrivingMath.Acceleration(1f, 24f, 12f, 18f, Curve()), Is.EqualTo(10.8f).Within(0.0001f));
            var negative = new DrivingCurve(new[] { new CurveKey(0f, 1f), new CurveKey(1f, -0.5f) });
            Assert.That(DrivingMath.Acceleration(1f, 12f, 12f, 18f, negative), Is.EqualTo(-9f));
        }

        [Test]
        public void DragProducesATerminalSpeedWithoutAClamp()
        {
            float speed = 0f;
            const float step = 0.02f;
            for (int i = 0; i < 3000; i++)
                speed += (DrivingMath.Acceleration(1f, speed, 12f, 18f, Curve()) - speed * 1.2f) * step;
            Assert.That(speed, Is.EqualTo(12f).Within(0.01f));
            Assert.That(DrivingMath.Acceleration(1f, 15f, 12f, 18f, Curve()), Is.GreaterThan(0f));
            Assert.That(DrivingMath.Acceleration(1f, 15f, 12f, 18f, Curve()) - 15f * 0.4f, Is.GreaterThan(0f));
        }

        [Test]
        public void BrakingStopsForwardMotionBeforeReverseAndCoastingCannotReverse()
        {
            Assert.That(DrivingMath.BrakeOrReverse(5f, 30f, 8f, 0.3f, 0.02f), Is.EqualTo(-30f));
            Assert.That(DrivingMath.BrakeOrReverse(0.4f, 30f, 8f, 0.3f, 0.02f), Is.EqualTo(-20f).Within(0.0001f));
            Assert.That(DrivingMath.BrakeOrReverse(0.2f, 30f, 8f, 0.3f, 0.02f), Is.EqualTo(-8f));
            Assert.That(DrivingMath.BrakeOrReverse(-2f, 30f, 8f, 0.3f, 0.02f), Is.EqualTo(-8f));
            Assert.That(DrivingMath.EngineBraking(0.01f, 3f, 0.02f), Is.EqualTo(-0.5f).Within(0.0001f));
            Assert.That(DrivingMath.EngineBraking(-0.01f, 3f, 0.02f), Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void SteeringDecreasesAtSpeedAndReversesWithDirection()
        {
            var curve = new DrivingCurve(new[] { new CurveKey(0f, 1f, -0.6f, -0.6f), new CurveKey(1f, 0.4f, -0.6f, -0.6f) });
            Assert.That(DrivingMath.SteeringRate(12f, 12f, 150f, 2f, curve), Is.EqualTo(60f).Within(0.0001f));
            Assert.That(DrivingMath.SteeringRate(-12f, 12f, 150f, 2f, curve), Is.EqualTo(-60f).Within(0.0001f));
            Assert.That(DrivingMath.SteeringRate(0f, 12f, 150f, 2f, curve), Is.Zero);
        }

        [Test]
        public void WallImpactRemovesOnlyTheConfiguredFraction()
        {
            Assert.That(DrivingMath.RetainedSpeed(10f, 0.35f), Is.EqualTo(6.5f).Within(0.0001f));
            Assert.That(DrivingMath.RetainedSpeed(10f, 0f), Is.EqualTo(10f));
            Assert.That(DrivingMath.RetainedSpeed(10f, 1f), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => DrivingMath.RetainedSpeed(1f, 1.1f));
        }

        [Test]
        public void GripAndDragHandleZeroTimeAndLargeFixedSteps()
        {
            Assert.That(DrivingMath.GripFraction(12f, 0f), Is.Zero);
            Assert.That(DrivingMath.GripFraction(12f, 1f), Is.InRange(0f, 1f));
            Assert.That(DrivingMath.DragRate(1.2f, 0f), Is.Zero);
            Assert.That(DrivingMath.DragRate(20f, 0.1f), Is.EqualTo(10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => DrivingMath.GripFraction(float.NaN, 0.02f));
            Assert.Throws<ArgumentOutOfRangeException>(() => DrivingMath.Acceleration(1f, 1f, 0f, 1f, Curve()));
        }

        private static DrivingCurve Curve()
        {
            return new DrivingCurve(new[] { new CurveKey(0f, 1f, -0.2f, -0.2f), new CurveKey(2f, 0.6f, -0.2f, -0.2f) });
        }
    }
}
