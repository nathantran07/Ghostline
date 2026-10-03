using System;
using Ghostline.Core;
using NUnit.Framework;

namespace Ghostline.Tests.EditMode
{
    public sealed class DrivingCurveTests
    {
        [Test]
        public void HermiteTangentsDetermineAccelerationBetweenKeys()
        {
            var curve = new DrivingCurve(new[] { new CurveKey(0f, 0f, 0f, 2f), new CurveKey(1f, 1f) });
            Assert.That(curve.Evaluate(0.5f), Is.EqualTo(0.75f).Within(0.00001f));
            Assert.That(curve.Evaluate(-1f), Is.Zero);
            Assert.That(curve.Evaluate(2f), Is.EqualTo(1f));
        }

        [Test]
        public void WeightedTangentsAndStepKeysAreSupported()
        {
            var weighted = new DrivingCurve(new[] { new CurveKey(0f, 0f, 0f, 2f, outWeight: 1f, weightedOut: true),
                new CurveKey(1f, 1f, inWeight: 1f, weightedIn: true) });
            Assert.That(weighted.Evaluate(0.5f), Is.EqualTo(1.25f).Within(0.00001f));
            var step = new DrivingCurve(new[] { new CurveKey(0f, 2f, outTangent: float.PositiveInfinity), new CurveKey(1f, 4f) });
            Assert.That(step.Evaluate(0.999f), Is.EqualTo(2f));
            Assert.That(step.Evaluate(1f), Is.EqualTo(4f));
        }

        [Test]
        public void WrapModesWorkBeforeAndAfterTheKeys()
        {
            CurveKey[] keys = { new CurveKey(0f, 0f, 1f, 1f), new CurveKey(1f, 1f, 1f, 1f) };
            var loop = new DrivingCurve(keys, CurveWrap.Loop, CurveWrap.Loop);
            Assert.That(loop.Evaluate(-0.25f), Is.EqualTo(0.75f).Within(0.00001f));
            Assert.That(loop.Evaluate(1.25f), Is.EqualTo(0.25f).Within(0.00001f));
            Assert.That(loop.Evaluate(1f), Is.Zero);
            Assert.That(loop.Evaluate(2f), Is.Zero);
            var pingPong = new DrivingCurve(keys, CurveWrap.PingPong, CurveWrap.PingPong);
            Assert.That(pingPong.Evaluate(-0.25f), Is.EqualTo(0.25f).Within(0.00001f));
            Assert.That(pingPong.Evaluate(1.25f), Is.EqualTo(0.75f).Within(0.00001f));
        }

        [Test]
        public void CurveCopiesKeysAndHandlesEmptyOrSingleKeyInput()
        {
            CurveKey[] keys = { new CurveKey(0f, 3f) };
            var curve = new DrivingCurve(keys);
            keys[0] = new CurveKey(0f, 9f);
            Assert.That(curve.Evaluate(5f), Is.EqualTo(3f));
            Assert.That(new DrivingCurve(Array.Empty<CurveKey>()).Evaluate(0f), Is.Zero);
            Assert.Throws<ArgumentException>(() => new DrivingCurve(new[] { new CurveKey(1f, 0f), new CurveKey(0f, 0f) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => curve.Evaluate(float.NaN));
        }
    }
}
