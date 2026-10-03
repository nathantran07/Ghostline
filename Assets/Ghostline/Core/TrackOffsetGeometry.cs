using System;
using System.Collections.Generic;

namespace Ghostline.Core
{
    public readonly struct TrackPoint
    {
        public TrackPoint(float x, float y)
        {
            NumericGuard.Finite(x, nameof(x));
            NumericGuard.Finite(y, nameof(y));
            X = x;
            Y = y;
        }
        public float X { get; }
        public float Y { get; }
    }

    public readonly struct TrackIntersection
    {
        public TrackIntersection(int firstIndex, int secondIndex)
        {
            FirstIndex = firstIndex;
            SecondIndex = secondIndex;
        }
        public int FirstIndex { get; }
        public int SecondIndex { get; }
    }

    /// <summary>Pure closed-offset safety math; crossover gaps are supplied as segment masks.</summary>
    public static class TrackOffsetGeometry
    {
        public static float ClampHalfWidth(float width, float radius, float thickness, float margin)
        {
            NumericGuard.Nonnegative(width, nameof(width));
            NumericGuard.Nonnegative(thickness, nameof(thickness));
            NumericGuard.Nonnegative(margin, nameof(margin));
            if (float.IsNaN(radius) || radius < 0f)
                throw new ArgumentOutOfRangeException(nameof(radius));
            // The outside edge of the inner wall must also fit inside the curvature radius.
            return Math.Min(width, Math.Max(0f, radius - thickness - margin));
        }

        public static float Radius(TrackPoint a, TrackPoint b, TrackPoint c)
        {
            double twiceArea = Math.Abs(Cross(a, b, c));
            if (twiceArea < 0.000001)
                return float.PositiveInfinity;
            return (float)(Distance(a, b) * Distance(b, c) * Distance(c, a) / (2d * twiceArea));
        }

        public static void SmoothClosed(float[] widths, float spacing, float slope)
        {
            if (widths == null || widths.Length < 3)
                throw new ArgumentException("A closed profile needs at least three widths.", nameof(widths));
            Positive(spacing, nameof(spacing));
            Positive(slope, nameof(slope));
            foreach (float width in widths)
                NumericGuard.Nonnegative(width, nameof(widths));
            float step = spacing * slope;
            // Two complete traversals in each direction carry restrictions through the seam.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < widths.Length; i++)
                    widths[i] = Math.Min(widths[i], widths[(i + widths.Length - 1) % widths.Length] + step);
                for (int i = widths.Length - 1; i >= 0; i--)
                    widths[i] = Math.Min(widths[i], widths[(i + 1) % widths.Length] + step);
            }
        }

        public static float InterpolateClosed(float[] widths, float distance, float length)
        {
            if (widths == null || widths.Length < 3)
                throw new ArgumentException("A closed profile needs at least three widths.", nameof(widths));
            NumericGuard.Finite(distance, nameof(distance));
            Positive(length, nameof(length));
            double wrapped = (double)distance % length;
            if (wrapped < 0d)
                wrapped += length;
            double index = wrapped * widths.Length / length;
            int lower = (int)index;
            double fraction = index - lower;
            return (float)(widths[lower] + (widths[(lower + 1) % widths.Length] - widths[lower]) * fraction);
        }

        public static void ShrinkAtIntersections(float[] widths, IReadOnlyList<TrackIntersection> intersections, float fraction)
        {
            if (widths == null || widths.Length < 3 || intersections == null)
                throw new ArgumentException("A closed profile and intersection list are required.");
            NumericGuard.Nonnegative(fraction, nameof(fraction));
            if (fraction >= 1f)
                throw new ArgumentOutOfRangeException(nameof(fraction));
            var indices = new HashSet<int>();
            foreach (TrackIntersection intersection in intersections)
            {
                if (intersection.FirstIndex < 0 || intersection.FirstIndex >= widths.Length
                    || intersection.SecondIndex < 0 || intersection.SecondIndex >= widths.Length)
                    throw new ArgumentOutOfRangeException(nameof(intersections));
                indices.Add(intersection.FirstIndex);
                indices.Add((intersection.FirstIndex + 1) % widths.Length);
                indices.Add(intersection.SecondIndex);
                indices.Add((intersection.SecondIndex + 1) % widths.Length);
            }
            foreach (int index in indices)
                widths[index] *= fraction;
        }

        public static List<TrackWidthLimit> GetWidthLimits(float[] widths, float spacing, float requestedWidth)
        {
            if (widths == null || widths.Length < 3)
                throw new ArgumentException("A closed profile is required.", nameof(widths));
            Positive(spacing, nameof(spacing));
            Positive(requestedWidth, nameof(requestedWidth));
            var limits = new List<TrackWidthLimit>();
            int start = -1;
            float minimum = requestedWidth;
            for (int i = 0; i <= widths.Length; i++)
            {
                if (i < widths.Length)
                    NumericGuard.Nonnegative(widths[i], nameof(widths));
                if (i < widths.Length && widths[i] * 2f < requestedWidth - 0.0001f)
                {
                    if (start < 0)
                        start = i;
                    minimum = Math.Min(minimum, widths[i] * 2f);
                }
                else if (start >= 0)
                {
                    limits.Add(new TrackWidthLimit(start * spacing, i * spacing, minimum));
                    start = -1;
                    minimum = requestedWidth;
                }
            }
            return limits;
        }

        public static List<TrackIntersection> FindIntersections(TrackPoint[] left, bool[] leftSegments,
            TrackPoint[] right, bool[] rightSegments, float cellSize)
        {
            if (left == null || right == null || leftSegments == null || rightSegments == null
                || left.Length < 3 || right.Length != left.Length
                || leftSegments.Length != left.Length || rightSegments.Length != left.Length)
                throw new ArgumentException("Both wall paths and segment masks must have matching closed lengths.");
            Positive(cellSize, nameof(cellSize));
            int count = left.Length;
            var cells = new Dictionary<(int X, int Y), List<int>>();
            var intersections = new List<TrackIntersection>();
            for (int id = 0; id < count * 2; id++)
            {
                int index = id % count;
                TrackPoint[] path = id < count ? left : right;
                if (!(id < count ? leftSegments : rightSegments)[index])
                    continue;
                TrackPoint a = path[index];
                TrackPoint b = path[(index + 1) % count];
                int minX = (int)Math.Floor(Math.Min(a.X, b.X) / cellSize);
                int maxX = (int)Math.Floor(Math.Max(a.X, b.X) / cellSize);
                int minY = (int)Math.Floor(Math.Min(a.Y, b.Y) / cellSize);
                int maxY = (int)Math.Floor(Math.Max(a.Y, b.Y) / cellSize);
                var checkedSegments = new HashSet<int>();
                for (int x = minX; x <= maxX; x++)
                    for (int y = minY; y <= maxY; y++)
                    {
                        var cell = (x, y);
                        if (!cells.TryGetValue(cell, out List<int> candidates))
                        {
                            candidates = new List<int>();
                            cells.Add(cell, candidates);
                        }
                        foreach (int other in candidates)
                        {
                            if (!checkedSegments.Add(other))
                                continue;
                            int j = other % count;
                            if (id / count == other / count && (Math.Abs(index - j) == 1 || Math.Abs(index - j) == count - 1))
                                continue;
                            TrackPoint[] otherPath = other < count ? left : right;
                            if (SegmentsIntersect(a, b, otherPath[j], otherPath[(j + 1) % count]))
                                intersections.Add(new TrackIntersection(index, j));
                        }
                        candidates.Add(id);
                    }
            }
            return intersections;
        }

        public static bool SegmentsIntersect(TrackPoint a, TrackPoint b, TrackPoint c, TrackPoint d)
        {
            if (Math.Max(a.X, b.X) < Math.Min(c.X, d.X) || Math.Max(c.X, d.X) < Math.Min(a.X, b.X)
                || Math.Max(a.Y, b.Y) < Math.Min(c.Y, d.Y) || Math.Max(c.Y, d.Y) < Math.Min(a.Y, b.Y))
                return false;
            double abC = Cross(a, b, c);
            double abD = Cross(a, b, d);
            double cdA = Cross(c, d, a);
            double cdB = Cross(c, d, b);
            return abC * abD <= 0d && cdA * cdB <= 0d;
        }

        private static double Cross(TrackPoint a, TrackPoint b, TrackPoint c)
        {
            return ((double)b.X - a.X) * ((double)c.Y - a.Y) - ((double)b.Y - a.Y) * ((double)c.X - a.X);
        }

        private static double Distance(TrackPoint a, TrackPoint b)
        {
            double x = (double)b.X - a.X;
            double y = (double)b.Y - a.Y;
            return Math.Sqrt(x * x + y * y);
        }

        private static void Positive(float value, string name)
        {
            NumericGuard.Nonnegative(value, name);
            if (value == 0f)
                throw new ArgumentOutOfRangeException(name);
        }
    }
}
