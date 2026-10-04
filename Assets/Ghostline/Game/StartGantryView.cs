using Ghostline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ghostline.Game
{
    /// <summary>Draws five start lamps using the default UI material, without texture assets.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StartGantryView : MaskableGraphic
    {
        private static readonly Color HousingColor = new Color(0.15f, 0.16f, 0.18f, 1f);
        private static readonly Color RimColor = new Color(0.055f, 0.06f, 0.07f, 1f);
        private static readonly Color LitColor = new Color(1f, 0.035f, 0.02f, 1f);
        private static readonly Color UnlitColor = new Color(0.24f, 0.025f, 0.02f, 1f);
        private int _litLampCount;

        public int LitLampCount => _litLampCount;

        public void Render(StartSequenceState state, int litLampCount)
        {
            int count = Mathf.Clamp(litLampCount, 0, 5);
            if (_litLampCount != count)
            {
                _litLampCount = count;
                SetVerticesDirty();
            }
            gameObject.SetActive(state != StartSequenceState.Done);
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect rectangle = GetPixelAdjustedRect();
            float scale = Mathf.Min(rectangle.width / 360f, rectangle.height / 96f);
            Vector2 center = rectangle.center;
            for (int i = 0; i < 5; i++)
            {
                Vector2 position = center + Vector2.right * ((i - 2) * 72f * scale);
                AddHousing(vertices, position, new Vector2(62f, 84f) * scale);
                AddDisc(vertices, position, 26f * scale, RimColor);
                AddDisc(vertices, position, 22f * scale, i < _litLampCount ? LitColor : UnlitColor);
            }
        }

        private static void AddHousing(VertexHelper vertices, Vector2 center, Vector2 size)
        {
            int start = vertices.currentVertCount;
            Vector2 half = size * 0.5f;
            vertices.AddVert(center + new Vector2(-half.x, -half.y), HousingColor, Vector2.zero);
            vertices.AddVert(center + new Vector2(-half.x, half.y), HousingColor, Vector2.zero);
            vertices.AddVert(center + new Vector2(half.x, half.y), HousingColor, Vector2.zero);
            vertices.AddVert(center + new Vector2(half.x, -half.y), HousingColor, Vector2.zero);
            vertices.AddTriangle(start, start + 1, start + 2);
            vertices.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddDisc(VertexHelper vertices, Vector2 center, float radius, Color color)
        {
            const int segments = 64;
            int start = vertices.currentVertCount;
            vertices.AddVert(center, color, Vector2.zero);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * (2f * Mathf.PI / segments);
                vertices.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    color, Vector2.zero);
            }
            for (int i = 0; i < segments; i++)
                vertices.AddTriangle(start, start + 1 + (i + 1) % segments, start + 1 + i);
        }
    }
}
