using System;
using Ghostline.Core;
using NUnit.Framework;

namespace Ghostline.Tests.EditMode
{
    public sealed class MinimapProjectionTests
    {
        [Test]
        public void MapsBoundsWithPaddingAndFlipsY()
        {
            var projection = new MinimapProjection(new TrackPoint(-10f, -5f), new TrackPoint(10f, 5f), 220f, 120f, 10f);
            AssertPoint(projection.Project(new TrackPoint(-10f, -5f)), 10f, 110f);
            AssertPoint(projection.Project(new TrackPoint(10f, 5f)), 210f, 10f);
            AssertPoint(projection.Project(new TrackPoint(0f, 0f)), 110f, 60f);
        }

        [TestCase(220f, 220f, 10f, 60f, 210f, 160f)]
        [TestCase(420f, 120f, 110f, 10f, 310f, 110f)]
        public void PreservesAspectRatioAndCentersUnusedSpace(float width, float height,
            float left, float top, float right, float bottom)
        {
            var projection = new MinimapProjection(new TrackPoint(0f, 0f), new TrackPoint(20f, 10f), width, height, 10f);
            AssertPoint(projection.Project(new TrackPoint(0f, 10f)), left, top);
            AssertPoint(projection.Project(new TrackPoint(20f, 0f)), right, bottom);
        }

        [Test]
        public void ClampsToPaddedRectWhileUnclampedProjectionCanLeaveIt()
        {
            var projection = new MinimapProjection(new TrackPoint(0f, 0f), new TrackPoint(10f, 10f), 120f, 120f, 10f);
            AssertPoint(projection.Project(new TrackPoint(-5f, 20f)), -40f, -90f);
            AssertPoint(projection.ProjectClamped(new TrackPoint(-5f, 20f)), 10f, 10f);
            AssertPoint(projection.ProjectClamped(new TrackPoint(20f, -5f)), 110f, 110f);
            AssertPoint(projection.ProjectClamped(new TrackPoint(5f, 5f)), 60f, 60f);
        }

        [TestCase(0f, 10f, 60f, 10f)]
        [TestCase(10f, 0f, 110f, 60f)]
        [TestCase(0f, 0f, 60f, 60f)]
        public void DegenerateBoundsCenterCollapsedAxes(float worldWidth, float worldHeight, float x, float y)
        {
            var projection = new MinimapProjection(new TrackPoint(3f, 4f),
                new TrackPoint(3f + worldWidth, 4f + worldHeight), 120f, 120f, 10f);
            AssertPoint(projection.Project(new TrackPoint(3f + worldWidth, 4f + worldHeight)), x, y);
            AssertPoint(projection.ProjectClamped(new TrackPoint(3f + worldWidth, 4f + worldHeight)), x, y);
        }

        [Test]
        public void ZeroPaddingAndExtremeFiniteBoundsRemainUsable()
        {
            var projection = new MinimapProjection(new TrackPoint(-float.MaxValue, -float.MaxValue),
                new TrackPoint(float.MaxValue, float.MaxValue), 100f, 100f, 0f);
            AssertPoint(projection.Project(new TrackPoint(float.MaxValue, float.MaxValue)), 100f, 0f);
            AssertPoint(projection.Project(new TrackPoint(0f, 0f)), 50f, 50f);
        }

        [TestCase(0f, 100f, 0f)]
        [TestCase(100f, 0f, 0f)]
        [TestCase(-1f, 100f, 0f)]
        [TestCase(100f, -1f, 0f)]
        [TestCase(float.NaN, 100f, 0f)]
        [TestCase(100f, float.PositiveInfinity, 0f)]
        [TestCase(100f, 100f, -1f)]
        [TestCase(100f, 100f, float.NaN)]
        [TestCase(100f, 100f, float.PositiveInfinity)]
        [TestCase(100f, 100f, 50f)]
        [TestCase(100f, 100f, 51f)]
        public void RejectsInvalidRectAndPadding(float width, float height, float padding)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MinimapProjection(
                new TrackPoint(0f, 0f), new TrackPoint(10f, 10f), width, height, padding));
        }

        [TestCase(-1f, 1f)]
        [TestCase(1f, -1f)]
        public void RejectsInvertedBounds(float maximumX, float maximumY)
        {
            Assert.Throws<ArgumentException>(() => new MinimapProjection(
                new TrackPoint(0f, 0f), new TrackPoint(maximumX, maximumY), 100f, 100f, 0f));
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(0f, float.PositiveInfinity)]
        public void NonfinitePositionsAreRejectedByCorePoint(float x, float y)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrackPoint(x, y));
        }

        private static void AssertPoint(TrackPoint point, float x, float y)
        {
            Assert.That(point.X, Is.EqualTo(x).Within(0.0001f));
            Assert.That(point.Y, Is.EqualTo(y).Within(0.0001f));
        }
    }
}
