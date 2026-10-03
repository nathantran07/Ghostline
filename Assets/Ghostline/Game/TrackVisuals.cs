using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace Ghostline.Game
{
    /// <summary>Builds presentation meshes without creating or modifying track colliders or triggers.</summary>
    [RequireComponent(typeof(TrackGenerator))]
    public sealed class TrackVisuals : MonoBehaviour
    {
        [Header("Ground")]
        [SerializeField] private bool _showGrass = true;
        [SerializeField] private Color _grassColor = new Color(0.035f, 0.095f, 0.05f);
        [SerializeField] private Color _grassStripeColor = new Color(0.055f, 0.14f, 0.07f);
        [SerializeField, Min(0.1f)] private float _grassStripeWidth = 8f;
        [SerializeField, Min(0f)] private float _grassMargin = 18f;
        [SerializeField, Min(0.1f)] private float _screenHeight = 18f;
        [Header("Edge lines and curbs")]
        [SerializeField] private bool _showEdgeLines = true;
        [SerializeField] private bool _showCurbs = true;
        [SerializeField] private Color _white = Color.white;
        [SerializeField] private Color _red = new Color(0.85f, 0.045f, 0.04f);
        [SerializeField, Min(0.01f)] private float _edgeWidth = 0.15f;
        [SerializeField, Min(0f)] private float _edgeInset = 0.05f;
        [SerializeField, Min(0.01f)] private float _curbWidth = 0.5f;
        [SerializeField, Min(0.001f)] private float _curbCurvatureThreshold = 0.035f;
        [SerializeField, Min(0f)] private float _curbMinimumRunLength = 1.5f;
        [SerializeField, Min(0.01f)] private float _curbStripeLength = 1f;
        [Header("Barriers and tire walls")]
        [SerializeField] private Color _barrierColor = new Color(0.65f, 0.68f, 0.7f);
        [SerializeField] private Color _barrierCenterColor = new Color(0.25f, 0.28f, 0.3f);
        [SerializeField, Min(0f)] private float _barrierGap = 0.05f;
        [SerializeField, Min(0.01f)] private float _barrierWidth = 0.4f;
        [SerializeField, Min(0.01f)] private float _barrierCenterWidth = 0.04f;
        [SerializeField, Min(0.01f)] private float _tireWidth = 0.8f;
        [SerializeField, Min(0.001f)] private float _tireCurvatureThreshold = 0.13f;
        [SerializeField, Min(0f)] private float _tireMinimumRunLength = 1.5f;
        [SerializeField, Min(0.01f)] private float _tireBlockLength = 1.2f;
        [Header("Start/finish and grid")]
        [SerializeField] private Color _black = new Color(0.015f, 0.015f, 0.015f);
        [SerializeField, Min(0.01f)] private float _checkerDepth = 1f;
        [SerializeField, Min(0.01f)] private float _gridSlotLength = 1.4f;
        [SerializeField, Min(0.01f)] private float _gridSlotWidth = 0.7f;
        [SerializeField, Min(0.01f)] private float _gridOutlineWidth = 0.04f;
        [SerializeField, Min(1f)] private float _gridRowSpacing = 1.5f;
        [SerializeField, Min(0f)] private float _gridStagger = 0.75f;
        [SerializeField, Min(0.01f)] private float _frontGridDistance = 4f;
        [SerializeField, Min(0.001f)] private float _gridCurvatureLimit = 0.015f;
        [Header("Sector lines")]
        [SerializeField] private Color _sector1Color = new Color(228f / 255f, 17f / 255f, 115f / 255f);
        [SerializeField] private Color _sector2Color = new Color(1f, 218f / 255f, 0f);
        [SerializeField] private Color _sector3Color = new Color(67f / 255f, 148f / 255f, 221f / 255f);
        [SerializeField, Min(0.01f)] private float _sectorLineWidth = 0.15f;
        [SerializeField, Min(0)] private int _corner8Knot = 30;
        [SerializeField, Min(0)] private int _corner15Knot = 65;
        [SerializeField] private MeshFilter[] _filters = Array.Empty<MeshFilter>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<TrackVisualRun> _curbRuns = new List<TrackVisualRun>();
        private readonly List<TrackVisualRun> _tireRuns = new List<TrackVisualRun>();
        private readonly List<TrackGridSlot> _gridSlots = new List<TrackGridSlot>();
        private readonly List<float> _sectorDistances = new List<float>();
        private readonly List<TrackVisualQuad> _ribbonQuads = new List<TrackVisualQuad>();
        private TrackGenerator _track;

        public IReadOnlyList<TrackVisualRun> CurbRuns => _curbRuns;
        public IReadOnlyList<TrackVisualRun> TireRuns => _tireRuns;
        public IReadOnlyList<TrackGridSlot> GridSlots => _gridSlots;
        public IReadOnlyList<float> SectorDistances => _sectorDistances;
        public IReadOnlyList<TrackVisualQuad> RibbonQuads => _ribbonQuads;

        public bool IsSectorBoundary(float distance)
        {
            return _sectorDistances.Count == 3 && (distance == _sectorDistances[1] || distance == _sectorDistances[2]);
        }

        public void Configure(float screenHeight, float carLength, float carWidth, int corner8Knot, int corner15Knot)
        {
            _screenHeight = screenHeight;
            _gridSlotLength = carLength;
            _gridSlotWidth = carWidth + 0.15f;
            _corner8Knot = corner8Knot;
            _corner15Knot = corner15Knot;
        }

        public TrackSample GetSpawnSample(TrackGenerator track)
        {
            _track = track;
            ValidateSettings();
            PrepareGrid();
            if (_gridSlots.Count == 0)
                return track.GetSample(-_frontGridDistance);
            TrackGridSlot slot = _gridSlots[0];
            return new TrackSample(slot.Center, slot.Tangent, slot.Distance);
        }

        public void Build(TrackGenerator track, Transform parent, Material material)
        {
            _track = track;
            ValidateSettings();
            ReleaseMeshes();
            string[] names = { "Grass", "Barriers", "Tire Walls", "Edge Lines", "Curbs", "Grid", "Sector Lines", "Start Finish Checker" };
            // Existing wall (3), gate (2), ghost (4), car (5), and HUD orders remain unchanged.
            int[] orders = { -7, -6, -6, -4, -3, -2, -1, 0 };
            _filters = new MeshFilter[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                Transform child = parent.Find(names[i]);
                GameObject visual = child != null ? child.gameObject : new GameObject(names[i], typeof(MeshFilter), typeof(MeshRenderer));
                visual.transform.SetParent(parent, false);
                visual.isStatic = true;
                _filters[i] = visual.GetComponent<MeshFilter>();
                MeshRenderer renderer = visual.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.sortingOrder = orders[i];
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            BuildGrass();
            BuildRibbons();
            PrepareGrid();
            BuildGrid();
            BuildSectorLines();
            BuildChecker();
        }

        public void ReleaseMeshes()
        {
            foreach (MeshFilter filter in _filters)
                if (filter != null)
                    filter.sharedMesh = null;
            foreach (Mesh mesh in _meshes)
                if (mesh != null)
                {
                    if (Application.isPlaying)
                        Destroy(mesh);
                    else
                        DestroyImmediate(mesh);
                }
            _meshes.Clear();
        }

        private void OnDisable()
        {
            ReleaseMeshes();
        }

        private void ValidateSettings()
        {
            float[] positive = { _screenHeight, _grassStripeWidth, _edgeWidth, _curbWidth, _curbCurvatureThreshold, _curbStripeLength,
                _barrierWidth, _barrierCenterWidth, _tireWidth, _tireCurvatureThreshold, _tireBlockLength,
                _checkerDepth, _gridSlotLength, _gridSlotWidth, _gridOutlineWidth, _gridRowSpacing,
                _frontGridDistance, _gridCurvatureLimit, _sectorLineWidth };
            foreach (float value in positive)
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                    throw new InvalidOperationException("Track visual dimensions and thresholds must be finite and positive.");
            float[] nonnegative = { _grassMargin, _edgeInset, _barrierGap, _curbMinimumRunLength, _tireMinimumRunLength, _gridStagger };
            foreach (float value in nonnegative)
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                    throw new InvalidOperationException("Track visual margins and minimum run lengths must be finite and nonnegative.");
            if (_tireCurvatureThreshold <= _curbCurvatureThreshold || _barrierCenterWidth >= _barrierWidth
                || _edgeInset + _edgeWidth + _curbWidth >= _track.RoadWidth * 0.5f
                || _gridOutlineWidth * 2f >= Mathf.Min(_gridSlotWidth, _gridSlotLength) || _gridRowSpacing < 1f)
                throw new InvalidOperationException("Track visual widths must fit the road; tire threshold must exceed the curb threshold.");
            Spline spline = _track.GetComponent<SplineContainer>().Spline;
            if (_corner8Knot < 0 || _corner8Knot >= spline.Count || _corner15Knot <= _corner8Knot || _corner15Knot >= spline.Count)
                throw new InvalidOperationException("Sector anchor knots must be ordered and present in the spline.");
        }

        private void BuildGrass()
        {
            var mesh = new ColoredMesh();
            if (!_showGrass)
            {
                Assign(0, mesh);
                return;
            }
            Vector2 minimum = _track.Samples[0].Position;
            Vector2 maximum = minimum;
            foreach (TrackSample sample in _track.Samples)
            {
                minimum = Vector2.Min(minimum, sample.Position);
                maximum = Vector2.Max(maximum, sample.Position);
            }
            float height = _screenHeight;
            Transform cameraTransform = transform.parent != null ? transform.parent.Find("Main Camera") : null;
            Camera camera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            if (camera != null && camera.orthographic)
                height = Mathf.Max(height, camera.orthographicSize * 2f);
            float scale = Mathf.Min(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
            float margin = Mathf.Max(_grassMargin, height) / Mathf.Max(scale, 0.0001f)
                + _track.RoadWidth * 0.5f + _track.WallThickness + _barrierGap + _barrierWidth + _tireWidth;
            minimum -= Vector2.one * margin;
            maximum += Vector2.one * margin;
            float firstBand = Mathf.Floor(minimum.y / _grassStripeWidth);
            float bandCount = Mathf.Ceil(maximum.y / _grassStripeWidth) - firstBand;
            if (float.IsNaN(bandCount) || float.IsInfinity(bandCount) || bandCount < 1f || bandCount > 4096f)
                throw new InvalidOperationException("Grass stripe width must produce between 1 and 4096 bands.");
            for (int i = 0; i < (int)bandCount; i++)
            {
                float band = firstBand + i;
                float bottom = Mathf.Max(minimum.y, band * _grassStripeWidth);
                float top = Mathf.Min(maximum.y, (band + 1f) * _grassStripeWidth);
                Color color = band % 2f == 0f ? _grassColor : _grassStripeColor;
                mesh.AddQuad(new Vector2(minimum.x, bottom), new Vector2(maximum.x, bottom),
                    new Vector2(maximum.x, top), new Vector2(minimum.x, top), color);
            }
            Assign(0, mesh);
        }

        private void BuildRibbons()
        {
            var barriers = new ColoredMesh();
            var tires = new ColoredMesh();
            var edges = new ColoredMesh();
            var curbs = new ColoredMesh();
            _curbRuns.Clear();
            _tireRuns.Clear();
            _ribbonQuads.Clear();
            float roadEdge = _track.RoadWidth * 0.5f;
            float lineOuter = roadEdge - _edgeInset;
            float lineInner = lineOuter - _edgeWidth;
            // Wall center is (road width + wall thickness)/2, with half-thickness edge radius.
            float barrierInner = roadEdge + _track.WallThickness + _barrierGap;
            for (int side = -1; side <= 1; side += 2)
            {
                if (_showEdgeLines)
                {
                    List<TrackVisualRun> edgeRuns = FindRuns(side, lineInner, lineOuter, 0f, 0f, false);
                    foreach (TrackVisualRun run in edgeRuns)
                        AddRibbon(edges, "Edge Lines", run, lineInner, lineOuter, _white, 0f);
                }
                List<TrackVisualRun> barrierRuns = FindRuns(side, barrierInner, barrierInner + _barrierWidth, 0f, 0f, false);
                float center = barrierInner + _barrierWidth * 0.5f;
                foreach (TrackVisualRun run in barrierRuns)
                {
                    AddRibbon(barriers, "Barriers", run, barrierInner, center - _barrierCenterWidth * 0.5f, _barrierColor, 0f);
                    AddRibbon(barriers, "Barriers", run, center - _barrierCenterWidth * 0.5f, center + _barrierCenterWidth * 0.5f, _barrierCenterColor, 0f);
                    AddRibbon(barriers, "Barriers", run, center + _barrierCenterWidth * 0.5f, barrierInner + _barrierWidth, _barrierColor, 0f);
                }
                if (_showCurbs)
                {
                    List<TrackVisualRun> curbRuns = FindRuns(side, lineInner - _curbWidth, lineInner,
                        _curbCurvatureThreshold, _curbMinimumRunLength, false);
                    _curbRuns.AddRange(curbRuns);
                    foreach (TrackVisualRun run in curbRuns)
                        AddRibbon(curbs, "Curbs", run, lineInner - _curbWidth, lineInner, _red, _curbStripeLength);
                }
                float tireInner = barrierInner + _barrierWidth;
                List<TrackVisualRun> tireRuns = FindRuns(side, tireInner, tireInner + _tireWidth,
                    _tireCurvatureThreshold, _tireMinimumRunLength, true);
                _tireRuns.AddRange(tireRuns);
                foreach (TrackVisualRun run in tireRuns)
                    AddRibbon(tires, "Tire Walls", run, tireInner, tireInner + _tireWidth, _white, _tireBlockLength);
            }
            Assign(1, barriers);
            Assign(2, tires);
            Assign(3, edges);
            Assign(4, curbs);
        }

        private List<TrackVisualRun> FindRuns(int side, float inner, float outer, float threshold, float minimum, bool outside)
        {
            int count = _track.Samples.Count;
            var allowed = new bool[count];
            int gap = -1;
            for (int i = 0; i < count; i++)
            {
                float curvature = _track.GetSignedCurvature(i) * side * (outside ? -1f : 1f);
                allowed[i] = (threshold == 0f || curvature > threshold) && RibbonClear(_track.Samples[i].Distance, side, inner, outer);
                if (!allowed[i])
                    gap = i;
            }
            var runs = new List<TrackVisualRun>();
            int start = gap < 0 ? 0 : (gap + 1) % count;
            int runStart = -1;
            int runCount = 0;
            for (int n = 0; n <= count; n++)
            {
                int i = (start + n) % count;
                if (n < count && allowed[i])
                {
                    if (runStart < 0)
                        runStart = i;
                    runCount++;
                }
                else if (runStart >= 0)
                {
                    int segments = gap < 0 ? runCount : runCount - 1;
                    float length = segments * _track.SampleSpacing;
                    if (segments > 0 && length >= minimum)
                        runs.Add(new TrackVisualRun(runStart, segments, side, _track.Samples[runStart].Distance, length));
                    runStart = -1;
                    runCount = 0;
                }
            }
            return runs;
        }

        private float AdjustOffset(float offset, float distance)
        {
            return offset - (_track.RoadWidth - _track.GetRoadWidth(distance)) * 0.5f;
        }

        private bool RibbonClear(float distance, int side, float inner, float outer)
        {
            TrackSample sample = _track.GetSample(distance);
            inner = AdjustOffset(inner, distance);
            outer = AdjustOffset(outer, distance);
            if (inner < 0f || outer <= inner)
                return false;
            return !_track.InsideOtherRoad(sample.Position + sample.Normal * (inner * side), sample.Distance)
                && !_track.InsideOtherRoad(sample.Position + sample.Normal * (outer * side), sample.Distance)
                && !_track.InsideOtherRoad(sample.Position + sample.Normal * ((inner + outer) * 0.5f * side), sample.Distance);
        }

        private void AddRibbon(ColoredMesh mesh, string layer, TrackVisualRun run, float inner, float outer, Color firstColor, float stripeLength)
        {
            for (int segment = 0; segment < run.SegmentCount; segment++)
            {
                float from = segment * _track.SampleSpacing;
                float end = (segment + 1) * _track.SampleSpacing;
                while (from < end - 0.00001f)
                {
                    int stripe = stripeLength > 0f ? Mathf.FloorToInt((from + 0.00001f) / stripeLength) : 0;
                    float to = stripeLength > 0f ? Mathf.Min(end, (stripe + 1) * stripeLength) : end;
                    float aDistance = run.Distance + from;
                    float bDistance = run.Distance + to;
                    if (RibbonClear(aDistance, run.Side, inner, outer) && RibbonClear(bDistance, run.Side, inner, outer)
                        && RibbonClear((aDistance + bDistance) * 0.5f, run.Side, inner, outer))
                    {
                        TrackSample a = _track.GetSample(aDistance);
                        TrackSample b = _track.GetSample(bDistance);
                        float aInner = AdjustOffset(inner, aDistance);
                        float aOuter = AdjustOffset(outer, aDistance);
                        float bInner = AdjustOffset(inner, bDistance);
                        float bOuter = AdjustOffset(outer, bDistance);
                        Color color = stripe % 2 == 0 ? firstColor : (firstColor == _white ? _red : _white);
                        _ribbonQuads.Add(new TrackVisualQuad(layer, mesh.VertexCount, aDistance, bDistance));
                        mesh.AddQuad(a.Position + a.Normal * (aInner * run.Side), a.Position + a.Normal * (aOuter * run.Side),
                            b.Position + b.Normal * (bOuter * run.Side), b.Position + b.Normal * (bInner * run.Side), color);
                    }
                    from = to;
                }
            }
        }

        private void PrepareGrid()
        {
            _gridSlots.Clear();
            float checkedDistance = _checkerDepth * 0.5f;
            bool straight = true;
            for (int slot = 0; slot < 10; slot++)
            {
                float behind = _frontGridDistance + slot / 2 * _gridSlotLength * _gridRowSpacing
                    + slot % 2 * _gridSlotLength * _gridStagger;
                float farEnd = behind + _gridSlotLength * 0.5f;
                if (farEnd >= _track.Length * 0.5f)
                    break;
                while (checkedDistance <= farEnd)
                {
                    int index = Mathf.FloorToInt(Mathf.Repeat(-checkedDistance, _track.Length) / _track.SampleSpacing);
                    TrackSample sample = _track.GetSample(-checkedDistance);
                    if (Mathf.Abs(_track.GetSignedCurvature(index)) > _gridCurvatureLimit
                        || _track.InsideOtherRoad(sample.Position, sample.Distance))
                    {
                        straight = false;
                        break;
                    }
                    checkedDistance += _track.SampleSpacing * 0.5f;
                }
                if (!straight)
                    break;
                TrackSample center = _track.GetSample(-behind);
                float side = slot % 2 == 0 ? 1f : -1f;
                Vector2 position = center.Position + center.Normal * (_track.GetRoadWidth(center.Distance) * 0.25f * side);
                var candidate = new TrackGridSlot(position, center.Tangent, center.Distance, _gridSlotLength, _gridSlotWidth);
                if (!GridSlotFits(candidate))
                    break;
                _gridSlots.Add(candidate);
            }
        }

        private bool GridSlotFits(TrackGridSlot slot)
        {
            Vector2 normal = new Vector2(-slot.Tangent.y, slot.Tangent.x);
            int steps = Mathf.CeilToInt(slot.Length / (_track.SampleSpacing * 0.5f));
            for (int i = 0; i <= steps; i++)
            {
                float longitudinal = Mathf.Lerp(-slot.Length * 0.5f, slot.Length * 0.5f, (float)i / steps);
                TrackSample sample = _track.GetSample(slot.Distance + longitudinal);
                for (int lateral = -1; lateral <= 1; lateral++)
                {
                    Vector2 point = slot.Center + slot.Tangent * longitudinal + normal * (slot.Width * 0.5f * lateral);
                    if (Mathf.Abs(Vector2.Dot(point - sample.Position, sample.Normal)) >= _track.GetRoadWidth(sample.Distance) * 0.5f - _edgeInset
                        || _track.InsideOtherRoad(point, sample.Distance))
                        return false;
                }
            }
            return true;
        }

        private void BuildGrid()
        {
            var mesh = new ColoredMesh();
            foreach (TrackGridSlot slot in _gridSlots)
            {
                float x = slot.Length * 0.5f;
                float y = slot.Width * 0.5f;
                AddRectangle(mesh, slot.Center, slot.Tangent, -x, -y, x, -y + _gridOutlineWidth, _white);
                AddRectangle(mesh, slot.Center, slot.Tangent, -x, y - _gridOutlineWidth, x, y, _white);
                AddRectangle(mesh, slot.Center, slot.Tangent, -x, -y + _gridOutlineWidth, -x + _gridOutlineWidth, y - _gridOutlineWidth, _white);
                AddRectangle(mesh, slot.Center, slot.Tangent, x - _gridOutlineWidth, -y + _gridOutlineWidth, x, y - _gridOutlineWidth, _white);
            }
            Assign(5, mesh);
            if (_gridSlots.Count < 10)
                Debug.LogWarning($"Track grid fits {_gridSlots.Count} of 10 slots before a corner, wall, or crossover.", this);
        }

        private void BuildSectorLines()
        {
            _sectorDistances.Clear();
            _sectorDistances.Add(0f);
            _sectorDistances.Add(NearestGateToKnot(_corner8Knot));
            _sectorDistances.Add(NearestGateToKnot(_corner15Knot));
            if (_sectorDistances[1] >= _sectorDistances[2])
                throw new InvalidOperationException("Sector boundaries require distinct gates in corner order.");
            var mesh = new ColoredMesh();
            Color[] colors = { _sector1Color, _sector2Color, _sector3Color };
            for (int i = 0; i < _sectorDistances.Count; i++)
            {
                TrackSample sample = _track.GetSample(_sectorDistances[i]);
                // The checker covers the line center at start; its pink leading edge stays visible.
                float offset = i == 0 ? -_checkerDepth * 0.5f : 0f;
                float halfWidth = _track.GetRoadWidth(sample.Distance) * 0.5f;
                AddRectangle(mesh, sample.Position + sample.Tangent * offset, sample.Tangent,
                    -_sectorLineWidth * 0.5f, -halfWidth, _sectorLineWidth * 0.5f, halfWidth, colors[i]);
            }
            Assign(6, mesh);
        }

        private float NearestGateToKnot(int knot)
        {
            Vector3 knotPosition = _track.GetComponent<SplineContainer>().Spline[knot].Position;
            int nearestSample = 0;
            for (int i = 1; i < _track.Samples.Count; i++)
                if (Vector2.SqrMagnitude(_track.Samples[i].Position - (Vector2)knotPosition)
                    < Vector2.SqrMagnitude(_track.Samples[nearestSample].Position - (Vector2)knotPosition))
                    nearestSample = i;
            float distance = _track.Samples[nearestSample].Distance;
            float nearest = _track.GateDistances[0];
            foreach (float gate in _track.GateDistances)
                if (Mathf.Abs(gate - distance) < Mathf.Abs(nearest - distance))
                    nearest = gate;
            return nearest;
        }

        private void BuildChecker()
        {
            var mesh = new ColoredMesh();
            TrackSample sample = _track.GetSample(0f);
            float square = _checkerDepth * 0.5f;
            float width = _track.GetRoadWidth(0f);
            int columns = Mathf.CeilToInt(width / square);
            for (int row = 0; row < 2; row++)
                for (int column = 0; column < columns; column++)
                {
                    float y = -width * 0.5f + column * square;
                    float x = -_checkerDepth * 0.5f + row * square;
                    AddRectangle(mesh, sample.Position, sample.Tangent, x, y, x + square,
                        Mathf.Min(y + square, width * 0.5f), (row + column) % 2 == 0 ? _white : _black);
                }
            Assign(7, mesh);
        }

        private static void AddRectangle(ColoredMesh mesh, Vector2 center, Vector2 tangent,
            float minX, float minY, float maxX, float maxY, Color color)
        {
            Vector2 normal = new Vector2(-tangent.y, tangent.x);
            mesh.AddQuad(center + tangent * minX + normal * minY, center + tangent * maxX + normal * minY,
                center + tangent * maxX + normal * maxY, center + tangent * minX + normal * maxY, color);
        }

        private void Assign(int index, ColoredMesh data)
        {
            Mesh mesh = data.Create("Ghostline " + _filters[index].name);
            _meshes.Add(mesh);
            _filters[index].sharedMesh = mesh;
        }

        private sealed class ColoredMesh
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<int> _triangles = new List<int>();
            private readonly List<Color> _colors = new List<Color>();
            public int VertexCount => _vertices.Count;

            public void AddQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
            {
                int offset = _vertices.Count;
                _vertices.Add(a);
                _vertices.Add(b);
                _vertices.Add(c);
                _vertices.Add(d);
                Color vertexColor = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
                for (int i = 0; i < 4; i++)
                    _colors.Add(vertexColor);
                // Consistent front face for both sides of a ribbon.
                float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                _triangles.AddRange(cross < 0f
                    ? new[] { offset, offset + 1, offset + 2, offset, offset + 2, offset + 3 }
                    : new[] { offset, offset + 2, offset + 1, offset, offset + 3, offset + 2 });
            }

            public Mesh Create(string name)
            {
                var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(_vertices);
                mesh.SetTriangles(_triangles, 0);
                mesh.SetColors(_colors);
                var uv = new Vector2[_vertices.Count];
                var normals = new Vector3[_vertices.Count];
                for (int i = 0; i < uv.Length; i++)
                {
                    uv[i] = Vector2.one * 0.5f;
                    normals[i] = Vector3.back;
                }
                mesh.uv = uv;
                mesh.normals = normals;
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }

    public readonly struct TrackVisualRun
    {
        public TrackVisualRun(int firstSample, int segmentCount, int side, float distance, float length)
        {
            FirstSample = firstSample;
            SegmentCount = segmentCount;
            Side = side;
            Distance = distance;
            Length = length;
        }

        public int FirstSample { get; }
        public int SegmentCount { get; }
        public int Side { get; }
        public float Distance { get; }
        public float Length { get; }
    }

    public readonly struct TrackVisualQuad
    {
        public TrackVisualQuad(string layer, int vertexOffset, float fromDistance, float toDistance)
        {
            Layer = layer;
            VertexOffset = vertexOffset;
            FromDistance = fromDistance;
            ToDistance = toDistance;
        }

        public string Layer { get; }
        public int VertexOffset { get; }
        public float FromDistance { get; }
        public float ToDistance { get; }
    }

    public readonly struct TrackGridSlot
    {
        public TrackGridSlot(Vector2 center, Vector2 tangent, float distance, float length, float width)
        {
            Center = center;
            Tangent = tangent;
            Distance = distance;
            Length = length;
            Width = width;
        }

        public Vector2 Center { get; }
        public Vector2 Tangent { get; }
        public float Distance { get; }
        public float Length { get; }
        public float Width { get; }
    }
}
