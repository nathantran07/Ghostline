using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ghostline.Game
{
    public enum DecorCategory { Trees, Grandstands, Tires, Banners }

    /// <summary>A conservative oriented box containing every fill, highlight, and shadow.</summary>
    public readonly struct DecorFootprint
    {
        public DecorFootprint(DecorCategory category, Vector2 center, Vector2 tangent, Vector2 halfSize,
            float trackDistance, int side, int variant)
        {
            Category = category;
            Center = center;
            Tangent = tangent;
            HalfSize = halfSize;
            TrackDistance = trackDistance;
            Side = side;
            Variant = variant;
        }

        public DecorCategory Category { get; }
        public Vector2 Center { get; }
        public Vector2 Tangent { get; }
        public Vector2 HalfSize { get; }
        public float TrackDistance { get; }
        public int Side { get; }
        public int Variant { get; }
        public Vector2[] Corners => DecorPlacementGeometry.Rectangle(Center, Tangent, HalfSize);
    }

    /// <summary>Indexes actual road/wall triangles and wall capsules in track-local coordinates.</summary>
    public sealed class DecorPlacementGeometry
    {
        private const float CellSize = 8f;
        private readonly List<Obstacle> _obstacles = new List<Obstacle>();
        private readonly Dictionary<Vector2Int, List<int>> _cells = new Dictionary<Vector2Int, List<int>>();
        private readonly HashSet<int> _visited = new HashSet<int>();

        public DecorPlacementGeometry(TrackGenerator track)
        {
            if (track == null)
                throw new ArgumentNullException(nameof(track));
            foreach (string path in new[] { "Generated Circuit/Road", "Generated Circuit/Walls/Wall Surface" })
            {
                Transform child = track.transform.Find(path);
                Mesh mesh = child != null ? child.GetComponent<MeshFilter>().sharedMesh : null;
                if (mesh == null)
                    throw new InvalidOperationException("Build the track before generating decor.");
                Matrix4x4 matrix = track.transform.worldToLocalMatrix * child.localToWorldMatrix;
                Vector3[] vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                int[] indices = mesh.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                    Add(new[] { (Vector2)vertices[indices[i]], (Vector2)vertices[indices[i + 1]],
                        (Vector2)vertices[indices[i + 2]] }, 0f);
            }
            foreach (EdgeCollider2D wall in track.transform.Find("Generated Circuit").GetComponentsInChildren<EdgeCollider2D>())
            {
                Matrix4x4 matrix = track.transform.worldToLocalMatrix * wall.transform.localToWorldMatrix;
                // A conservative radius also covers nonuniform child scaling and rounded end caps.
                float scale = Mathf.Max(matrix.MultiplyVector(Vector3.right).magnitude,
                    matrix.MultiplyVector(Vector3.up).magnitude);
                Vector2[] points = wall.points;
                for (int i = 1; i < points.Length; i++)
                    Add(new[] { (Vector2)matrix.MultiplyPoint3x4(points[i - 1] + wall.offset),
                        (Vector2)matrix.MultiplyPoint3x4(points[i] + wall.offset) }, wall.edgeRadius * scale);
            }
        }

        public bool ClearsTrack(DecorFootprint footprint, float margin)
        {
            Vector2[] corners = footprint.Corners;
            BoundsOf(corners, margin, out Vector2 minimum, out Vector2 maximum);
            _visited.Clear();
            Vector2Int first = Cell(minimum);
            Vector2Int last = Cell(maximum);
            for (int y = first.y; y <= last.y; y++)
                for (int x = first.x; x <= last.x; x++)
                    if (_cells.TryGetValue(new Vector2Int(x, y), out List<int> indices))
                        foreach (int index in indices)
                        {
                            if (!_visited.Add(index))
                                continue;
                            Obstacle obstacle = _obstacles[index];
                            float separation = DistanceSquared(corners, obstacle.Points);
                            float clearance = margin + obstacle.Radius;
                            if (separation <= 0.0000001f || separation < clearance * clearance)
                                return false;
                        }
            return true;
        }

        public static bool ClearsDecor(DecorFootprint footprint, IReadOnlyList<DecorFootprint> placed, float margin)
        {
            Vector2[] corners = footprint.Corners;
            foreach (DecorFootprint other in placed)
            {
                float radius = footprint.HalfSize.magnitude + other.HalfSize.magnitude + margin;
                if ((footprint.Center - other.Center).sqrMagnitude > radius * radius)
                    continue;
                float separation = DistanceSquared(corners, other.Corners);
                if (separation <= 0.0000001f || separation < margin * margin)
                    return false;
            }
            return true;
        }

        public static Vector2[] Rectangle(Vector2 center, Vector2 tangent, Vector2 halfSize)
        {
            Vector2 along = tangent * halfSize.x;
            Vector2 across = new Vector2(-tangent.y, tangent.x) * halfSize.y;
            return new[] { center - along - across, center + along - across,
                center + along + across, center - along + across };
        }

        public static bool Contains(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            if (polygon.Count < 3)
                return false;
            float area = 0f;
            bool positive = false;
            bool negative = false;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Count];
                area += Cross(a, b);
                float side = Cross(b - a, point - a);
                // Cross products have area units; scale the roundoff tolerance to edge length.
                float tolerance = 0.00001f * (b - a).magnitude;
                positive |= side > tolerance;
                negative |= side < -tolerance;
                if (positive && negative)
                    return false;
            }
            return Mathf.Abs(area) > 0.000001f;
        }

        public static float DistanceSquared(IReadOnlyList<Vector2> first, IReadOnlyList<Vector2> second)
        {
            foreach (Vector2 point in first)
                if (Contains(second, point))
                    return 0f;
            foreach (Vector2 point in second)
                if (Contains(first, point))
                    return 0f;
            float minimum = float.PositiveInfinity;
            for (int i = 0; i < first.Count; i++)
                for (int j = 0; j < second.Count; j++)
                {
                    Vector2 a = first[i];
                    Vector2 b = first[(i + 1) % first.Count];
                    Vector2 c = second[j];
                    Vector2 d = second[(j + 1) % second.Count];
                    float denominator = Cross(b - a, d - c);
                    if (Mathf.Abs(denominator) > 0.0000001f)
                    {
                        float t = Cross(c - a, d - c) / denominator;
                        float u = Cross(c - a, b - a) / denominator;
                        if (t >= 0f && t <= 1f && u >= 0f && u <= 1f)
                            return 0f;
                    }
                    minimum = Mathf.Min(minimum, Mathf.Min(Mathf.Min(PointSegment(a, c, d), PointSegment(b, c, d)),
                        Mathf.Min(PointSegment(c, a, b), PointSegment(d, a, b))));
                }
            return minimum;
        }

        private void Add(Vector2[] points, float radius)
        {
            int index = _obstacles.Count;
            _obstacles.Add(new Obstacle(points, radius));
            BoundsOf(points, radius, out Vector2 minimum, out Vector2 maximum);
            Vector2Int first = Cell(minimum);
            Vector2Int last = Cell(maximum);
            for (int y = first.y; y <= last.y; y++)
                for (int x = first.x; x <= last.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!_cells.TryGetValue(cell, out List<int> indices))
                    {
                        indices = new List<int>();
                        _cells.Add(cell, indices);
                    }
                    indices.Add(index);
                }
        }

        private static void BoundsOf(IReadOnlyList<Vector2> points, float margin, out Vector2 minimum, out Vector2 maximum)
        {
            minimum = points[0];
            maximum = points[0];
            foreach (Vector2 point in points)
            {
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
            minimum -= Vector2.one * margin;
            maximum += Vector2.one * margin;
        }

        private static Vector2Int Cell(Vector2 point)
        {
            return new Vector2Int(Mathf.FloorToInt(point.x / CellSize), Mathf.FloorToInt(point.y / CellSize));
        }

        private static float PointSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 delta = b - a;
            float t = delta.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, delta) / delta.sqrMagnitude) : 0f;
            return (point - a - delta * t).sqrMagnitude;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private readonly struct Obstacle
        {
            public Obstacle(Vector2[] points, float radius)
            {
                Points = points;
                Radius = radius;
            }

            public Vector2[] Points { get; }
            public float Radius { get; }
        }
    }
}
