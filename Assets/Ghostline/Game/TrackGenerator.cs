using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace Ghostline.Game
{
    /// <summary>Builds a flat closed circuit, including open wall gaps at road crossovers.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SplineContainer))]
    public sealed class TrackGenerator : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float _roadWidth = 2.2f;
        [SerializeField, Min(0.01f)] private float _wallThickness = 0.2f;
        [SerializeField, Range(128, 8192)] private int _sampleCount = 2048;
        [SerializeField, Min(2)] private int _checkpointCount = 12;
        [SerializeField, Min(0.01f)] private float _radiusMargin = 0.3f;
        [SerializeField] private Color _roadColor = new Color(0.17f, 0.2f, 0.25f);
        [SerializeField] private Material _material;
        [SerializeField] private Transform _generatedRoot;
        [SerializeField] private MeshFilter _roadFilter;
        [SerializeField] private MeshFilter _wallFilter;
        private Mesh _roadMesh;
        private Mesh _wallMesh;
        private TrackSample[] _samples = Array.Empty<TrackSample>();
        private float[] _arcLengths = Array.Empty<float>();
        private readonly List<TrackCrossing> _crossings = new List<TrackCrossing>();
        private readonly List<float> _gateDistances = new List<float>();
        private readonly Dictionary<Vector2Int, List<int>> _roadCells = new Dictionary<Vector2Int, List<int>>();
        private float _corridorCellSize;

        public float RoadWidth => _roadWidth;
        public float WallThickness => _wallThickness;
        public int CheckpointCount => _checkpointCount;
        public float Length { get; private set; }
        public float SampleSpacing => Length / _sampleCount;
        public float MinimumRadius { get; private set; }
        public float RequiredRadius => _roadWidth * 0.5f + _radiusMargin;
        public IReadOnlyList<TrackSample> Samples => _samples;
        public IReadOnlyList<TrackCrossing> Crossings => _crossings;
        public IReadOnlyList<float> GateDistances => _gateDistances;

        public float GetSignedCurvature(int index)
        {
            if (index < 0 || index >= _samples.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            Vector2 a = _samples[(index + _samples.Length - 1) % _samples.Length].Position;
            Vector2 b = _samples[index].Position;
            Vector2 c = _samples[(index + 1) % _samples.Length].Position;
            float denominator = Vector2.Distance(a, b) * Vector2.Distance(b, c) * Vector2.Distance(c, a);
            return denominator > 0.000001f ? 2f * Cross(b - a, c - a) / denominator : 0f;
        }

        public void Configure(Material material, int checkpointCount = 12, float roadWidth = 2.2f,
            float wallThickness = 0.2f, int sampleCount = 2048)
        {
            if (material == null)
                throw new ArgumentNullException(nameof(material));
            if (checkpointCount < 2)
                throw new ArgumentOutOfRangeException(nameof(checkpointCount));
            if (!IsPositive(roadWidth) || !IsPositive(wallThickness) || wallThickness >= _radiusMargin * 2f)
                throw new ArgumentOutOfRangeException(nameof(roadWidth), "Use positive widths and wall thickness below twice the radius margin.");
            if (sampleCount < 128 || sampleCount > 8192)
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            _material = material;
            _checkpointCount = checkpointCount;
            _roadWidth = roadWidth;
            _wallThickness = wallThickness;
            _sampleCount = sampleCount;
            SampleSpline();
        }

        public TrackSample GetSample(float distance)
        {
            if (!IsPositive(Length) || float.IsNaN(distance) || float.IsInfinity(distance))
                throw new InvalidOperationException("Sample a configured track with a finite arc length.");
            Spline spline = GetComponent<SplineContainer>().Spline;
            float wrappedDistance = Mathf.Repeat(distance, Length);
            int upper = Array.BinarySearch(_arcLengths, wrappedDistance);
            if (upper < 0)
                upper = ~upper;
            upper = Mathf.Clamp(upper, 1, _arcLengths.Length - 1);
            float fraction = Mathf.InverseLerp(_arcLengths[upper - 1], _arcLengths[upper], wrappedDistance);
            float t = (upper - 1 + fraction) / (_arcLengths.Length - 1);
            Vector3 position = spline.EvaluatePosition(t);
            Vector3 tangent = spline.EvaluateTangent(t);
            Vector2 direction = new Vector2(tangent.x, tangent.y).normalized;
            if (direction.sqrMagnitude < 0.99f)
                throw new InvalidOperationException($"Track has no tangent at arc length {wrappedDistance:0.00}.");
            return new TrackSample(new Vector2(position.x, position.y), direction, wrappedDistance);
        }

        public bool ValidateRadius()
        {
            MinimumRadius = float.PositiveInfinity;
            float tightestDistance = 0f;
            for (int i = 0; i < _samples.Length; i++)
            {
                // Only consecutive arc-length samples: the other crossover pass is unrelated.
                Vector2 a = _samples[(i + _samples.Length - 1) % _samples.Length].Position;
                Vector2 b = _samples[i].Position;
                Vector2 c = _samples[(i + 1) % _samples.Length].Position;
                float twiceArea = Mathf.Abs(Cross(b - a, c - a));
                float radius = twiceArea < 0.000001f ? float.PositiveInfinity
                    : Vector2.Distance(a, b) * Vector2.Distance(b, c) * Vector2.Distance(c, a) / (2f * twiceArea);
                if (radius < MinimumRadius)
                {
                    MinimumRadius = radius;
                    tightestDistance = _samples[i].Distance;
                }
            }
            if (MinimumRadius > RequiredRadius)
                return true;
            Debug.LogError($"Track corner at arc length {tightestDistance:0.00} has radius {MinimumRadius:0.00}; must exceed {RequiredRadius:0.00}.", this);
            return false;
        }

        public void Generate(RaceManager race)
        {
            if (race == null)
                throw new ArgumentNullException(nameof(race));
            if (race.CheckpointCount != _checkpointCount)
                throw new InvalidOperationException("Configure RaceManager with the track's checkpoint count before generation.");
            SampleSpline();
            if (!ValidateRadius())
                throw new InvalidOperationException("Track radius validation failed. Widen the named corners before generating geometry.");
            FindCrossings();
            PlaceGateDistances();
            if (_generatedRoot != null)
            {
                _generatedRoot.gameObject.SetActive(false);
                Release(_generatedRoot.gameObject);
            }
            ReleaseMeshes();
            _generatedRoot = new GameObject("Generated Circuit").transform;
            _generatedRoot.SetParent(transform, false);
            _roadFilter = CreateMeshObject("Road", _generatedRoot, -5);
            var walls = new GameObject("Walls").transform;
            walls.SetParent(_generatedRoot, false);
            _wallFilter = CreateMeshObject("Wall Surface", walls, 3);
            BuildWalls(walls, 1f);
            BuildWalls(walls, -1f);
            RebuildMeshes();
            var gates = new GameObject("Checkpoints").transform;
            gates.SetParent(_generatedRoot, false);
            CreateGate(gates, race, 0f, true, 0);
            for (int i = 0; i < _gateDistances.Count; i++)
                CreateGate(gates, race, _gateDistances[i], false, i);
        }

        private void OnEnable()
        {
            if (_roadFilter == null || _material == null)
                return;
            // Meshes are transient; colliders and gates are serialized in the scene.
            SampleSpline();
            FindCrossings();
            PlaceGateDistances();
            RebuildMeshes();
        }

        private void OnDisable()
        {
            ReleaseMeshes();
            GetComponent<TrackVisuals>()?.ReleaseMeshes();
        }

        private void SampleSpline()
        {
            Spline spline = GetComponent<SplineContainer>().Spline;
            if (!spline.Closed || spline.Count < 4)
                throw new InvalidOperationException("Track requires a closed spline with at least four knots.");
            if (!IsPositive(_roadWidth) || !IsPositive(_wallThickness) || !IsPositive(_radiusMargin)
                || _wallThickness >= _radiusMargin * 2f || _checkpointCount < 2
                || _sampleCount < 128 || _sampleCount > 8192)
                throw new InvalidOperationException("Track geometry settings are invalid.");
            // Splines' per-curve distance table has only 30 entries. A dense measured table
            // avoids uneven sample spacing on long or sharply varying Bezier segments.
            int resolution = Mathf.Max(_sampleCount * 8, spline.Count * 128);
            _arcLengths = new float[resolution + 1];
            Vector3 previous = spline.EvaluatePosition(0f);
            for (int i = 1; i <= resolution; i++)
            {
                Vector3 position = spline.EvaluatePosition((float)i / resolution);
                _arcLengths[i] = _arcLengths[i - 1] + Vector3.Distance(previous, position);
                previous = position;
            }
            Length = _arcLengths[resolution];
            if (!IsPositive(Length) || Length / _sampleCount > _roadWidth * 0.25f)
                throw new InvalidOperationException("Track needs a nonzero length and sample spacing no greater than a quarter road width.");
            _samples = new TrackSample[_sampleCount];
            for (int i = 0; i < _sampleCount; i++)
                _samples[i] = GetSample(i * SampleSpacing);
            IndexRoadCorridors();
        }

        private void IndexRoadCorridors()
        {
            _roadCells.Clear();
            _corridorCellSize = _roadWidth * 0.5f + _wallThickness + SampleSpacing * 0.5f;
            for (int i = 0; i < _sampleCount; i++)
            {
                Vector2 a = _samples[i].Position;
                Vector2 b = _samples[(i + 1) % _sampleCount].Position;
                Vector2 minimum = Vector2.Min(a, b) - Vector2.one * _corridorCellSize;
                Vector2 maximum = Vector2.Max(a, b) + Vector2.one * _corridorCellSize;
                for (int x = Mathf.FloorToInt(minimum.x / _corridorCellSize); x <= Mathf.FloorToInt(maximum.x / _corridorCellSize); x++)
                    for (int y = Mathf.FloorToInt(minimum.y / _corridorCellSize); y <= Mathf.FloorToInt(maximum.y / _corridorCellSize); y++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (!_roadCells.TryGetValue(cell, out List<int> segments))
                        {
                            segments = new List<int>();
                            _roadCells.Add(cell, segments);
                        }
                        segments.Add(i);
                    }
            }
        }

        private void FindCrossings()
        {
            _crossings.Clear();
            for (int i = 0; i < _sampleCount; i++)
            {
                Vector2 a = _samples[i].Position;
                Vector2 r = _samples[(i + 1) % _sampleCount].Position - a;
                for (int j = i + 1; j < _sampleCount; j++)
                {
                    if (LoopDistance(i * SampleSpacing, j * SampleSpacing) <= _roadWidth * 3f)
                        continue;
                    Vector2 b = _samples[j].Position;
                    Vector2 s = _samples[(j + 1) % _sampleCount].Position - b;
                    float denominator = Cross(r, s);
                    if (Mathf.Abs(denominator) < 0.000001f)
                        continue;
                    float u = Cross(b - a, s) / denominator;
                    float v = Cross(b - a, r) / denominator;
                    if (u < 0f || u >= 1f || v < 0f || v >= 1f)
                        continue;
                    _crossings.Add(new TrackCrossing(a + r * u,
                        (i + u) * SampleSpacing, (j + v) * SampleSpacing));
                }
            }
        }

        private void PlaceGateDistances()
        {
            _gateDistances.Clear();
            float spacing = Length / _checkpointCount;
            for (int i = 0; i < _checkpointCount; i++)
                _gateDistances.Add(NudgeGate((i + 0.5f) * spacing));
            foreach (TrackCrossing crossing in _crossings)
            {
                EnsureArcHasGate(crossing.FirstDistance, crossing.SecondDistance);
                EnsureArcHasGate(crossing.SecondDistance, crossing.FirstDistance + Length);
            }
            _gateDistances.Sort();
            for (int i = 0; i < _gateDistances.Count; i++)
            {
                float previous = i == 0 ? 0f : _gateDistances[i - 1];
                if (_gateDistances[i] - previous < _roadWidth || Length - _gateDistances[i] < _roadWidth)
                    throw new InvalidOperationException("Track gates cannot be separated from each other and start/finish; use fewer gates or a longer layout.");
            }
        }

        private void EnsureArcHasGate(float from, float to)
        {
            foreach (float distance in _gateDistances)
            {
                float unwrapped = distance < from ? distance + Length : distance;
                if (unwrapped > from && unwrapped < to)
                    return;
            }
            float midpoint = Mathf.Repeat((from + to) * 0.5f, Length);
            int nearest = 0;
            for (int i = 1; i < _gateDistances.Count; i++)
                if (LoopDistance(_gateDistances[i], midpoint) < LoopDistance(_gateDistances[nearest], midpoint))
                    nearest = i;
            float candidate = NudgeGate(midpoint);
            float unwrappedCandidate = candidate < from ? candidate + Length : candidate;
            if (unwrappedCandidate <= from || unwrappedCandidate >= to)
                throw new InvalidOperationException("Crossover arc is too short for a gate outside the intersection.");
            _gateDistances[nearest] = candidate;
        }

        private float NudgeGate(float distance)
        {
            for (int i = 0; i < _sampleCount; i++)
            {
                float candidate = Mathf.Repeat(distance + i * SampleSpacing, Length);
                Vector2 position = GetSample(candidate).Position;
                bool clear = true;
                foreach (TrackCrossing crossing in _crossings)
                    if (Vector2.Distance(position, crossing.Position) <= _roadWidth)
                        clear = false;
                if (clear)
                    return candidate;
            }
            throw new InvalidOperationException("No checkpoint position lies outside the crossover.");
        }

        private void BuildWalls(Transform parent, float side)
        {
            var points = new Vector2[_sampleCount];
            var allowed = new bool[_sampleCount];
            int firstGap = -1;
            for (int i = 0; i < _sampleCount; i++)
            {
                points[i] = _samples[i].Position + _samples[i].Normal * ((_roadWidth + _wallThickness) * 0.5f * side);
                allowed[i] = !InsideOtherRoad(points[i], _samples[i].Distance);
                if (!allowed[i])
                    firstGap = i;
            }
            var run = new List<Vector2>();
            int start = firstGap < 0 ? 0 : (firstGap + 1) % _sampleCount;
            for (int n = 0; n < _sampleCount; n++)
            {
                int i = (start + n) % _sampleCount;
                if (allowed[i])
                    run.Add(points[i]);
                else
                    FlushWall(parent, run, side);
            }
            if (firstGap < 0)
                run.Add(points[0]);
            FlushWall(parent, run, side);
        }

        public bool InsideOtherRoad(Vector2 point, float distance)
        {
            if (_samples.Length == 0)
                throw new InvalidOperationException("Configure the track before querying crossover corridors.");
            if (float.IsNaN(distance) || float.IsInfinity(distance))
                throw new ArgumentOutOfRangeException(nameof(distance));
            distance = Mathf.Repeat(distance, Length);
            // Include wall radius and half a sampling step so retained segment ends stay clear.
            float corridor = _roadWidth * 0.5f + _wallThickness + SampleSpacing * 0.5f;
            // Conservative expanded segment bounds only narrow candidates; the exact wall rule below is unchanged.
            var cell = new Vector2Int(Mathf.FloorToInt(point.x / _corridorCellSize), Mathf.FloorToInt(point.y / _corridorCellSize));
            if (!_roadCells.TryGetValue(cell, out List<int> segments))
                return false;
            foreach (int j in segments)
            {
                Vector2 a = _samples[j].Position;
                Vector2 delta = _samples[(j + 1) % _sampleCount].Position - a;
                float t = Mathf.Clamp01(Vector2.Dot(point - a, delta) / delta.sqrMagnitude);
                if (LoopDistance(distance, (j + t) * SampleSpacing) <= _roadWidth * 3f)
                    continue;
                if ((point - a - delta * t).sqrMagnitude <= corridor * corridor)
                    return true;
            }
            return false;
        }

        private void FlushWall(Transform parent, List<Vector2> points, float side)
        {
            if (points.Count >= 2)
            {
                var wall = new GameObject(side > 0f ? "Left Wall" : "Right Wall", typeof(EdgeCollider2D));
                wall.transform.SetParent(parent, false);
                EdgeCollider2D collider = wall.GetComponent<EdgeCollider2D>();
                collider.points = points.ToArray();
                collider.edgeRadius = _wallThickness * 0.5f;
            }
            points.Clear();
        }

        private MeshFilter CreateMeshObject(string name, Transform parent, int sortingOrder)
        {
            var gameObject = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            gameObject.isStatic = true;
            gameObject.transform.SetParent(parent, false);
            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return gameObject.GetComponent<MeshFilter>();
        }

        private void RebuildMeshes()
        {
            ReleaseMeshes();
            var left = new Vector2[_sampleCount + 1];
            var right = new Vector2[_sampleCount + 1];
            for (int i = 0; i <= _sampleCount; i++)
            {
                TrackSample sample = _samples[i % _sampleCount];
                left[i] = sample.Position + sample.Normal * (_roadWidth * 0.5f);
                right[i] = sample.Position - sample.Normal * (_roadWidth * 0.5f);
            }
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            AddStrip(vertices, triangles, left, right);
            _roadMesh = CreateMesh("Ghostline Road", vertices, triangles, _roadColor);
            _roadFilter.sharedMesh = _roadMesh;
            vertices.Clear();
            triangles.Clear();
            foreach (EdgeCollider2D wall in _generatedRoot.GetComponentsInChildren<EdgeCollider2D>())
            {
                Vector2[] points = wall.points;
                left = new Vector2[points.Length];
                right = new Vector2[points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    Vector2 direction = (points[Mathf.Min(i + 1, points.Length - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
                    Vector2 normal = new Vector2(-direction.y, direction.x) * (_wallThickness * 0.5f);
                    left[i] = points[i] + normal;
                    right[i] = points[i] - normal;
                }
                AddStrip(vertices, triangles, left, right);
            }
            _wallMesh = CreateMesh("Ghostline Walls", vertices, triangles, new Color(0.42f, 0.47f, 0.54f));
            _wallFilter.sharedMesh = _wallMesh;
            GetComponent<TrackVisuals>()?.Build(this, _generatedRoot, _material);
        }

        private static void AddStrip(List<Vector3> vertices, List<int> triangles, Vector2[] left, Vector2[] right)
        {
            int offset = vertices.Count;
            for (int i = 0; i < left.Length; i++)
            {
                vertices.Add(left[i]);
                vertices.Add(right[i]);
                if (i == left.Length - 1)
                    continue;
                int index = offset + i * 2;
                triangles.AddRange(new[] { index, index + 2, index + 1, index + 1, index + 2, index + 3 });
            }
        }

        private static Mesh CreateMesh(string name, List<Vector3> vertices, List<int> triangles, Color color)
        {
            // Mesh vertex colors do not receive SpriteRenderer's sRGB-to-linear conversion.
            // Keep the specified dark road tint consistent with the project's color space.
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                color = color.linear;
            var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave,
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            var colors = new Color[vertices.Count];
            var uv = new Vector2[vertices.Count];
            var normals = new Vector3[vertices.Count];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = color;
                uv[i] = Vector2.one * 0.5f;
                normals[i] = Vector3.back;
            }
            mesh.colors = colors;
            mesh.uv = uv;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            return mesh;
        }

        private void CreateGate(Transform parent, RaceManager race, float distance, bool startFinish, int index)
        {
            TrackSample sample = GetSample(distance);
            var gate = new GameObject(startFinish ? "Start Finish" : $"Checkpoint {index + 1}",
                typeof(SpriteRenderer), typeof(SolidSprite), typeof(BoxCollider2D), typeof(CheckpointTrigger));
            gate.transform.SetParent(parent, false);
            gate.transform.localPosition = sample.Position;
            gate.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(sample.Tangent.y, sample.Tangent.x) * Mathf.Rad2Deg);
            gate.GetComponent<SolidSprite>().Configure(startFinish ? Color.white : new Color(1f, 0.65f, 0.15f, 0.7f),
                new Vector2(0.25f, _roadWidth), 2);
            gate.GetComponent<SpriteRenderer>().sharedMaterial = _material;
            gate.GetComponent<BoxCollider2D>().size = Vector2.one;
            gate.GetComponent<CheckpointTrigger>().Configure(race, startFinish, index,
                transform.TransformDirection(sample.Tangent));
            TrackVisuals visuals = GetComponent<TrackVisuals>();
            if (visuals != null && (startFinish || visuals.IsSectorBoundary(distance)))
                gate.GetComponent<SpriteRenderer>().enabled = false;
        }

        private void ReleaseMeshes()
        {
            if (_roadFilter != null)
                _roadFilter.sharedMesh = null;
            if (_wallFilter != null)
                _wallFilter.sharedMesh = null;
            if (_roadMesh != null)
                Release(_roadMesh);
            if (_wallMesh != null)
                Release(_wallMesh);
            _roadMesh = null;
            _wallMesh = null;
        }

        private float LoopDistance(float a, float b)
        {
            float distance = Mathf.Abs(a - b);
            return Mathf.Min(distance, Length - distance);
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static bool IsPositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static void Release(UnityEngine.Object value)
        {
            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }
    }

    public readonly struct TrackSample
    {
        public TrackSample(Vector2 position, Vector2 tangent, float distance)
        {
            Position = position;
            Tangent = tangent;
            Distance = distance;
        }

        public Vector2 Position { get; }
        public Vector2 Tangent { get; }
        public Vector2 Normal => new Vector2(-Tangent.y, Tangent.x);
        public float Distance { get; }
    }

    public readonly struct TrackCrossing
    {
        public TrackCrossing(Vector2 position, float firstDistance, float secondDistance)
        {
            Position = position;
            FirstDistance = firstDistance;
            SecondDistance = secondDistance;
        }

        public Vector2 Position { get; }
        public float FirstDistance { get; }
        public float SecondDistance { get; }
    }
}
