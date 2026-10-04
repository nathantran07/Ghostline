using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghostline.Game
{
    /// <summary>Builds static trackside fills on enable or explicit rebuild; never mutates the circuit.</summary>
    [ExecuteAlways, DefaultExecutionOrder(100)]
    [RequireComponent(typeof(TrackGenerator))]
    public sealed class DecorGenerator : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] private int _seed = 271828;
        [SerializeField, Min(0f)] private float _placementMargin = 0.8f;
        [SerializeField, Range(1, 128)] private int _maxPlacementAttempts = 32;
        [Header("Tree groves")]
        [SerializeField] private bool _showTrees = true;
        [SerializeField, Range(0f, 4f)] private float _treeDensity = 1f;
        [SerializeField, Min(1f)] private float _groveSpacing = 18f;
        [SerializeField, Min(0.1f)] private float _treeFalloff = 5f;
        [SerializeField, Min(0.1f)] private float _treeReach = 16f;
        [SerializeField, Range(0f, 1f)] private float _blossomFraction = 0.18f;
        // Dark greens, blossoms, green/pink highlights, and offset shadow.
        [SerializeField] private Color[] _treePalette = {
            new Color(31f / 255f, 90f / 255f, 43f / 255f), new Color(42f / 255f, 107f / 255f, 51f / 255f),
            new Color(248f / 255f, 187f / 255f, 208f / 255f), new Color(244f / 255f, 143f / 255f, 177f / 255f),
            new Color(0.26f, 0.53f, 0.29f), new Color(0.99f, 0.87f, 0.92f), new Color(0.055f, 0.16f, 0.075f) };
        [Header("Finish-area grandstands")]
        [SerializeField] private bool _showGrandstands = true;
        [SerializeField, Range(0f, 4f)] private float _grandstandDensity = 1f;
        [SerializeField] private Vector2 _grandstandSize = new Vector2(12f, 3.4f);
        // Roof, seating base, four crowd colors.
        [SerializeField] private Color[] _grandstandPalette = {
            new Color(0.48f, 0.51f, 0.55f), new Color(0.25f, 0.28f, 0.32f),
            new Color(0.95f, 0.45f, 0.44f), new Color(0.37f, 0.68f, 0.88f),
            new Color(0.98f, 0.82f, 0.37f), new Color(0.88f, 0.87f, 0.83f) };
        [Header("Corner-exit tire stacks")]
        [SerializeField] private bool _showTires = true;
        [SerializeField, Range(0f, 4f)] private float _tireDensity = 1f;
        [SerializeField, Min(0.001f)] private float _exitCurvatureThreshold = 0.06f;
        // Dark rubber, lighter inner ring, alternating red/white pair pads.
        [SerializeField] private Color[] _tirePalette = {
            new Color(33f / 255f, 33f / 255f, 33f / 255f), new Color(0.39f, 0.41f, 0.43f),
            new Color(0.85f, 0.045f, 0.04f), new Color(0.94f, 0.94f, 0.92f) };
        [Header("Straight-section banners")]
        [SerializeField] private bool _showBanners = true;
        [SerializeField, Range(0f, 4f)] private float _bannerDensity = 1f;
        [SerializeField, Min(0.1f)] private float _bannerSpacing = 10f;
        [SerializeField, Min(0.001f)] private float _straightCurvatureLimit = 0.025f;
        [SerializeField] private Color[] _bannerPalette = {
            new Color(0.95f, 0.35f, 0.33f), new Color(0.22f, 0.59f, 0.85f),
            new Color(0.97f, 0.77f, 0.21f), new Color(0.62f, 0.42f, 0.78f) };
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<DecorFootprint> _footprints = new List<DecorFootprint>();
        private Transform _generatedRoot;

        public IReadOnlyList<DecorFootprint> Footprints => _footprints;
        public float PlacementMargin => _placementMargin;
        public Transform GeneratedRoot => _generatedRoot;

        [ContextMenu("Rebuild Decor")]
        public void Rebuild()
        {
            ValidateSettings();
            TrackGenerator track = GetComponent<TrackGenerator>();
            var geometry = new DecorPlacementGeometry(track);
            var reserved = new List<DecorFootprint>();
            // Priority and occupancy are independent of visibility. Hidden categories retain reservations.
            PlaceGrandstands(track, geometry, reserved);
            PlaceTires(track, geometry, reserved);
            PlaceBanners(track, geometry, reserved);
            PlaceTrees(track, geometry, reserved);
            Material material = track.transform.Find("Generated Circuit/Road").GetComponent<MeshRenderer>().sharedMaterial;
            if (material == null)
                throw new InvalidOperationException("Decor requires the existing track material.");
            ReleaseMeshes();
            _generatedRoot = new GameObject("Generated Decor").transform;
            _generatedRoot.SetParent(transform, false);
            _generatedRoot.gameObject.hideFlags = HideFlags.DontSave;
            foreach (DecorCategory category in Enum.GetValues(typeof(DecorCategory)))
            {
                var data = new DecorMeshBuilder();
                if (Visible(category))
                    foreach (DecorFootprint footprint in reserved)
                        if (footprint.Category == category)
                        {
                            _footprints.Add(footprint);
                            data.Add(footprint, Palette(category), _blossomFraction);
                        }
                var visual = new GameObject(category.ToString(), typeof(MeshFilter), typeof(MeshRenderer));
                visual.transform.SetParent(_generatedRoot, false);
                visual.hideFlags = HideFlags.DontSave;
                visual.isStatic = true;
                Mesh mesh = data.Create("Ghostline Decor " + category);
                _meshes.Add(mesh);
                visual.GetComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = visual.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.sortingOrder = 1;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        public void ReleaseMeshes()
        {
            if (_generatedRoot != null)
            {
                _generatedRoot.gameObject.SetActive(false);
                Release(_generatedRoot.gameObject);
                _generatedRoot = null;
            }
            foreach (Mesh mesh in _meshes)
                if (mesh != null)
                    Release(mesh);
            _meshes.Clear();
            _footprints.Clear();
        }

        private void OnEnable()
        {
            Transform road = transform.Find("Generated Circuit/Road");
            if (road != null && road.GetComponent<MeshFilter>().sharedMesh != null)
                Rebuild();
        }

        private void OnDisable()
        {
            ReleaseMeshes();
        }

        private void PlaceGrandstands(TrackGenerator track, DecorPlacementGeometry geometry, List<DecorFootprint> placed)
        {
            var random = Stream(0x13579);
            int count = Mathf.CeilToInt(_grandstandDensity * 2f);
            for (int i = 0; i < count; i++)
            {
                float distance = 10f + i * (_grandstandSize.x + 4f);
                if (distance + _grandstandSize.x * 0.5f > Mathf.Min(50f, track.Length * 0.1f))
                    break;
                if (!Straight(track, distance, _grandstandSize.x * 0.5f))
                    continue;
                for (int attempt = 0; attempt < _maxPlacementAttempts; attempt++)
                    if (TryPlace(track, geometry, placed, DecorCategory.Grandstands, distance, 1,
                        _grandstandSize * 0.5f, 0.7f + attempt * 0.6f, random.Next()))
                        break;
            }
        }

        private void PlaceTires(TrackGenerator track, DecorPlacementGeometry geometry, List<DecorFootprint> placed)
        {
            var random = Stream(0x24680);
            float runLength = 0f;
            int turn = 0;
            for (int i = 0; i < track.Samples.Count; i++)
            {
                float curvature = track.GetSignedCurvature(i);
                int direction = Mathf.Abs(curvature) >= _exitCurvatureThreshold ? (curvature > 0f ? 1 : -1) : 0;
                if (direction != turn)
                {
                    if (turn != 0 && runLength >= 1.5f && _tireDensity > 0f && random.NextDouble() < Mathf.Min(1f, _tireDensity))
                        for (int row = 0; row < Mathf.CeilToInt(_tireDensity); row++)
                            for (int attempt = 0; attempt < _maxPlacementAttempts; attempt++)
                                if (TryPlace(track, geometry, placed, DecorCategory.Tires,
                                    track.Samples[i].Distance + 1.5f, -turn, new Vector2(1.8f, 0.35f),
                                    0.7f + row * 1.8f + attempt * 0.5f, random.Next()))
                                    break;
                    runLength = 0f;
                    turn = direction;
                }
                runLength += track.SampleSpacing;
            }
        }

        private void PlaceBanners(TrackGenerator track, DecorPlacementGeometry geometry, List<DecorFootprint> placed)
        {
            if (_bannerDensity == 0f)
                return;
            var random = Stream(0x35791);
            float spacing = _bannerSpacing / _bannerDensity;
            int count = BoundedCount(track.Length / spacing);
            for (int i = 0; i < count; i++)
            {
                float distance = i * spacing;
                if (!Straight(track, distance, 1.4f))
                    continue;
                for (int attempt = 0; attempt < _maxPlacementAttempts; attempt++)
                    if (TryPlace(track, geometry, placed, DecorCategory.Banners, distance, i % 2 == 0 ? -1 : 1,
                        new Vector2(1.4f, 0.35f), 0.7f + attempt * 0.5f, random.Next()))
                        break;
            }
        }

        private void PlaceTrees(TrackGenerator track, DecorPlacementGeometry geometry, List<DecorFootprint> placed)
        {
            var random = Stream(0x46802);
            int count = BoundedCount(track.Length * 0.3f * _treeDensity);
            int groves = Mathf.Max(1, BoundedCount(track.Length / _groveSpacing));
            for (int i = 0; i < count; i++)
                for (int attempt = 0; attempt < _maxPlacementAttempts; attempt++)
                {
                    int grove = random.Next(groves);
                    float distance = grove * _groveSpacing + ((float)random.NextDouble() - 0.5f) * 8f;
                    int side = grove % 2 == 0 ? -1 : 1;
                    float scale = 0.65f + (float)random.NextDouble() * 0.55f;
                    // Truncated exponential: more grove candidates near the walls than far away.
                    float range = 1f - Mathf.Exp(-_treeReach / _treeFalloff);
                    float offset = -Mathf.Log(1f - (float)random.NextDouble() * range) * _treeFalloff;
                    if (TryPlace(track, geometry, placed, DecorCategory.Trees, distance, side,
                        Vector2.one * (1.6f * scale), 0.7f + offset, random.Next()))
                        break;
                }
        }

        private bool TryPlace(TrackGenerator track, DecorPlacementGeometry geometry, List<DecorFootprint> placed,
            DecorCategory category, float distance, int side, Vector2 halfSize, float offset, int variant)
        {
            var sample = track.GetSample(distance);
            Vector2 center = sample.Position + sample.Normal * side
                * (track.GetRoadWidth(distance) * 0.5f + track.WallThickness + _placementMargin + halfSize.y + offset);
            var footprint = new DecorFootprint(category, center, sample.Tangent, halfSize, distance, side, variant);
            if (!geometry.ClearsTrack(footprint, _placementMargin)
                || !DecorPlacementGeometry.ClearsDecor(footprint, placed, _placementMargin))
                return false;
            if (placed.Count >= 4096)
                throw new InvalidOperationException("Decor settings exceed the 4096-footprint budget.");
            placed.Add(footprint);
            return true;
        }

        private bool Straight(TrackGenerator track, float distance, float halfLength)
        {
            int count = Mathf.CeilToInt(halfLength * 2f / track.SampleSpacing);
            for (int i = 0; i <= count; i++)
            {
                float d = distance - halfLength + i * halfLength * 2f / Mathf.Max(1, count);
                int sample = Mathf.FloorToInt(Mathf.Repeat(d, track.Length) / track.SampleSpacing) % track.Samples.Count;
                if (Mathf.Abs(track.GetSignedCurvature(sample)) > _straightCurvatureLimit)
                    return false;
            }
            return true;
        }

        private System.Random Stream(int salt)
        {
            return new System.Random(unchecked(_seed * 397 ^ salt));
        }

        private bool Visible(DecorCategory category)
        {
            switch (category)
            {
                case DecorCategory.Trees: return _showTrees;
                case DecorCategory.Grandstands: return _showGrandstands;
                case DecorCategory.Tires: return _showTires;
                case DecorCategory.Banners: return _showBanners;
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        private Color[] Palette(DecorCategory category)
        {
            switch (category)
            {
                case DecorCategory.Trees: return _treePalette;
                case DecorCategory.Grandstands: return _grandstandPalette;
                case DecorCategory.Tires: return _tirePalette;
                case DecorCategory.Banners: return _bannerPalette;
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        private void ValidateSettings()
        {
            foreach (float density in new[] { _treeDensity, _grandstandDensity, _tireDensity, _bannerDensity })
                if (!Finite(density) || density < 0f || density > 4f)
                    throw new InvalidOperationException("Decor densities must be finite, between zero and four.");
            foreach (float value in new[] { _groveSpacing, _treeFalloff, _treeReach, _grandstandSize.x,
                _grandstandSize.y, _exitCurvatureThreshold, _bannerSpacing, _straightCurvatureLimit })
                if (!Finite(value) || value <= 0f || value > 100f)
                    throw new InvalidOperationException("Decor dimensions, spacing and thresholds must be finite, positive and at most 100.");
            if (!Finite(_placementMargin) || _placementMargin < 0f || _placementMargin > 50f
                || !Finite(_blossomFraction) || _blossomFraction < 0f || _blossomFraction > 1f
                || _maxPlacementAttempts < 1 || _maxPlacementAttempts > 128)
                throw new InvalidOperationException("Decor requires a finite 0–50 margin, 0–1 blossom fraction and 1–128 attempts.");
            if (_grandstandSize.x < 0.8f || _grandstandSize.y < 0.8f)
                throw new InvalidOperationException("Grandstands must be at least 0.8 by 0.8 units to contain the crowd dots.");
            Color[][] palettes = { _treePalette, _grandstandPalette, _tirePalette, _bannerPalette };
            int[] lengths = { 7, 6, 4, 4 };
            for (int i = 0; i < palettes.Length; i++)
            {
                if (palettes[i] == null || palettes[i].Length != lengths[i])
                    throw new InvalidOperationException("Keep palette entries in their documented category order.");
                foreach (Color color in palettes[i])
                    if (!Finite(color.r) || !Finite(color.g) || !Finite(color.b) || !Finite(color.a)
                        || color.r < 0f || color.r > 1f || color.g < 0f || color.g > 1f || color.b < 0f || color.b > 1f || color.a != 1f)
                        throw new InvalidOperationException("Decor palettes require opaque colors with finite RGB values between zero and one.");
            }
        }

        private static int BoundedCount(float count)
        {
            if (!Finite(count) || count < 0f || count > 4096f)
                throw new InvalidOperationException("Decor settings exceed the 4096-candidate budget.");
            return Mathf.CeilToInt(count);
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void Release(UnityEngine.Object value)
        {
            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }
    }
}
