using System;
using Ghostline.Core;
using NUnit.Framework;

namespace Ghostline.Tests.EditMode
{
    public sealed class TrackOffsetGeometryTests
    {
        [Test]
        public void TightCornerLimitsTheWholeWallAndKeepsClearance()
        {
            Assert.That(TrackOffsetGeometry.ClampHalfWidth(2f, 1.5f, 0.2f, 0.3f), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(TrackOffsetGeometry.ClampHalfWidth(1f, float.PositiveInfinity, 0.2f, 0.3f), Is.EqualTo(1f));
            Assert.That(TrackOffsetGeometry.ClampHalfWidth(1f, 0.1f, 0.2f, 0.3f), Is.Zero);
        }

        [Test]
        public void CircularRadiusAndStraightSegmentsAreDistinguished()
        {
            Assert.That(TrackOffsetGeometry.Radius(new TrackPoint(1f, 0f), new TrackPoint(0f, 1f),
                new TrackPoint(-1f, 0f)), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(TrackOffsetGeometry.Radius(new TrackPoint(0f, 0f), new TrackPoint(1f, 0f),
                new TrackPoint(2f, 0f)), Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void SmoothingOnlyNarrowsAndPropagatesAcrossTheClosedSeam()
        {
            float[] widths = { 2f, 2f, 2f, 2f, 0.5f };
            TrackOffsetGeometry.SmoothClosed(widths, 1f, 0.25f);
            Assert.That(widths, Is.EqualTo(new[] { 0.75f, 1f, 1f, 0.75f, 0.5f }));
        }

        [Test]
        public void NonAdjacentWallIntersectionIsDetectedButAnOpenCrossoverIsExcluded()
        {
            TrackPoint[] wall = { new TrackPoint(-1f, -1f), new TrackPoint(1f, 1f),
                new TrackPoint(-1f, 1f), new TrackPoint(1f, -1f) };
            bool[] closed = { true, true, true, true };
            Assert.That(TrackOffsetGeometry.FindIntersections(wall, closed, wall, new bool[4], 1f), Has.Count.EqualTo(1));
            closed[2] = false;
            Assert.That(TrackOffsetGeometry.FindIntersections(wall, closed, wall, new bool[4], 1f), Is.Empty);
        }

        [Test]
        public void SegmentTestsIncludeCollinearOverlapAndEndpointContact()
        {
            Assert.That(TrackOffsetGeometry.SegmentsIntersect(new TrackPoint(0f, 0f), new TrackPoint(2f, 0f),
                new TrackPoint(1f, 0f), new TrackPoint(3f, 0f)), Is.True);
            Assert.That(TrackOffsetGeometry.SegmentsIntersect(new TrackPoint(0f, 0f), new TrackPoint(1f, 0f),
                new TrackPoint(1f, 0f), new TrackPoint(1f, 1f)), Is.True);
            Assert.That(TrackOffsetGeometry.SegmentsIntersect(new TrackPoint(0f, 0f), new TrackPoint(1f, 0f),
                new TrackPoint(2f, 0f), new TrackPoint(3f, 0f)), Is.False);
        }

        [Test]
        public void WidthInterpolationWrapsBothDirectionsAcrossTheSeam()
        {
            float[] widths = { 1f, 2f, 3f, 4f };
            Assert.That(TrackOffsetGeometry.InterpolateClosed(widths, 3.5f, 4f), Is.EqualTo(2.5f));
            Assert.That(TrackOffsetGeometry.InterpolateClosed(widths, -0.5f, 4f), Is.EqualTo(2.5f));
            Assert.That(TrackOffsetGeometry.InterpolateClosed(widths, 4f, 4f), Is.EqualTo(1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => TrackOffsetGeometry.InterpolateClosed(widths, float.NaN, 4f));
        }

        [Test]
        public void IntersectionReductionIsLocalAndReportsMeasuredIntervals()
        {
            float[] widths = { 2f, 2f, 2f, 2f, 2f };
            TrackOffsetGeometry.ShrinkAtIntersections(widths, new[] { new TrackIntersection(1, 2) }, 0.5f);
            Assert.That(widths, Is.EqualTo(new[] { 2f, 1f, 1f, 1f, 2f }));
            var limits = TrackOffsetGeometry.GetWidthLimits(widths, 3f, 4f);
            Assert.That(limits, Has.Count.EqualTo(1));
            Assert.That(limits[0].FromDistance, Is.EqualTo(3f));
            Assert.That(limits[0].ToDistance, Is.EqualTo(12f));
            Assert.That(limits[0].MinimumWidth, Is.EqualTo(2f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        public void InvalidWidthsAreRejected(float width)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TrackOffsetGeometry.ClampHalfWidth(width, 2f, 0.2f, 0.3f));
        }
    }
}
