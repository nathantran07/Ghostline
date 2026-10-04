using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghostline.Game
{
    /// <summary>Solid vertex-color fills; reuses the track's unlit material without textures or outlines.</summary>
    public sealed class DecorMeshBuilder
    {
        private const int CircleSegments = 20;
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<int> _indices = new List<int>();
        private readonly List<Color> _colors = new List<Color>();

        public void Add(DecorFootprint footprint, Color[] palette, float blossomFraction)
        {
            var random = new System.Random(footprint.Variant);
            switch (footprint.Category)
            {
                case DecorCategory.Trees:
                    AddTree(footprint, palette, random, blossomFraction);
                    break;
                case DecorCategory.Grandstands:
                    AddGrandstand(footprint, palette, random);
                    break;
                case DecorCategory.Tires:
                    AddTires(footprint, palette);
                    break;
                case DecorCategory.Banners:
                    AddBanner(footprint, palette, random);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(footprint));
            }
        }

        public Mesh Create(string name)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(_vertices);
            mesh.SetTriangles(_indices, 0);
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

        private void AddTree(DecorFootprint footprint, Color[] palette, System.Random random, float blossomFraction)
        {
            float scale = footprint.HalfSize.x / 1.6f;
            Circle(footprint, new Vector2(0.55f, -0.55f) * scale, 0.48f * scale, palette[6]);
            bool blossom = random.NextDouble() < blossomFraction;
            int count = random.Next(3, 7);
            float phase = (float)random.NextDouble() * Mathf.PI * 2f;
            for (int i = 0; i < count; i++)
            {
                float angle = phase + i * Mathf.PI * 2f / count;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (0.25f + (float)random.NextDouble() * 0.15f) * scale;
                float radius = (0.55f + (float)random.NextDouble() * 0.35f) * scale;
                Circle(footprint, offset, radius, palette[(blossom ? 2 : 0) + random.Next(2)]);
            }
            Circle(footprint, new Vector2(-0.3f, 0.32f) * scale, 0.28f * scale, palette[blossom ? 5 : 4]);
        }

        private void AddGrandstand(DecorFootprint footprint, Color[] palette, System.Random random)
        {
            float length = footprint.HalfSize.x;
            float width = footprint.HalfSize.y;
            // Local y * Side points away from the track; the seating fills its facing half.
            Rect(footprint, -length, length, -width, width, palette[1]);
            float facing = -footprint.Side;
            Rect(footprint, -length, length, Mathf.Min(0f, -facing * width), Mathf.Max(0f, -facing * width), palette[0]);
            int columns = Mathf.Max(2, Mathf.FloorToInt(length * 2f / 0.42f));
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < columns; column++)
                {
                    float x = Mathf.Lerp(-length + 0.25f, length - 0.25f, column / (float)(columns - 1));
                    float y = facing * width * (0.2f + row * 0.28f);
                    Circle(footprint, new Vector2(x, y), 0.08f, palette[2 + random.Next(4)]);
                }
        }

        private void AddTires(DecorFootprint footprint, Color[] palette)
        {
            float length = footprint.HalfSize.x;
            float width = footprint.HalfSize.y;
            // Paired red/white pads under dark tire stacks are geometry fills, not strokes.
            for (int pair = 0; pair < 4; pair++)
            {
                float x0 = Mathf.Lerp(-length, length, pair / 4f);
                float x1 = Mathf.Lerp(-length, length, (pair + 1) / 4f);
                Rect(footprint, x0, x1, -width, width, palette[2 + pair % 2]);
                for (int tire = 0; tire < 2; tire++)
                {
                    Vector2 center = new Vector2(Mathf.Lerp(x0, x1, (tire + 0.5f) / 2f), 0f);
                    float radius = Mathf.Min(width * 0.84f, (x1 - x0) * 0.23f);
                    Circle(footprint, center, radius, palette[0]);
                    Circle(footprint, center, radius * 0.55f, palette[1]);
                    Circle(footprint, center, radius * 0.32f, palette[0]);
                }
            }
        }

        private void AddBanner(DecorFootprint footprint, Color[] palette, System.Random random)
        {
            int color = random.Next(palette.Length);
            float length = footprint.HalfSize.x;
            float width = footprint.HalfSize.y;
            Rect(footprint, -length, length, -width, width, palette[color]);
            for (int stripe = 0; stripe < 3; stripe++)
            {
                float x = -length + length * (0.3f + stripe * 0.55f);
                Rect(footprint, x, x + length * 0.18f, -width, width, palette[(color + 1) % palette.Length]);
            }
            float facing = -footprint.Side;
            Rect(footprint, -length, length, Mathf.Min(facing * width, facing * width * 0.65f),
                Mathf.Max(facing * width, facing * width * 0.65f), palette[(color + 2) % palette.Length]);
        }

        private void Circle(DecorFootprint footprint, Vector2 center, float radius, Color color)
        {
            int start = _vertices.Count;
            Vertex(Transform(footprint, center), color);
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = -i * Mathf.PI * 2f / CircleSegments;
                Vertex(Transform(footprint, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius), color);
            }
            for (int i = 0; i < CircleSegments; i++)
                _indices.AddRange(new[] { start, start + 1 + i, start + 1 + (i + 1) % CircleSegments });
        }

        private void Rect(DecorFootprint footprint, float left, float right, float bottom, float top, Color color)
        {
            int start = _vertices.Count;
            Vertex(Transform(footprint, new Vector2(left, bottom)), color);
            Vertex(Transform(footprint, new Vector2(left, top)), color);
            Vertex(Transform(footprint, new Vector2(right, top)), color);
            Vertex(Transform(footprint, new Vector2(right, bottom)), color);
            _indices.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        private void Vertex(Vector2 point, Color color)
        {
            _vertices.Add(point);
            _colors.Add(QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color);
        }

        private static Vector2 Transform(DecorFootprint footprint, Vector2 point)
        {
            return footprint.Center + footprint.Tangent * point.x
                + new Vector2(-footprint.Tangent.y, footprint.Tangent.x) * point.y;
        }
    }
}
